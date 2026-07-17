using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XRViewLab.UI;

public sealed class DiagMonCaptureService : IDisposable
{
    private readonly DiagMonStore _store;
    private readonly object _gate = new();
    private CancellationTokenSource? _cancel;
    private Task? _worker;
    private Task<string?>? _stopTask;
    private Process? _presentMon;
    private string? _presentMonExecutable;
    private string? _presentMonSessionName;
    private Task? _presentMonOutputPump;
    private Process? _typePerf;
    private bool _wprStarted;
    private StreamWriter? _processWriter;
    private HashSet<int> _processSnapshot = new();
    private double _previousCpu;
    private long _previousCpuAt;
    private int _sampleCount;
    private readonly List<double> _cpu = new(), _workingMb = new(), _privateMb = new();

    public bool IsCapturing { get; private set; }
    public DiagMonSession? CurrentSession { get; private set; }
    public string? CurrentDirectory { get; private set; }
    public DateTimeOffset? CaptureStartUtc => CurrentSession?.StartUtc;
    public event EventHandler? StateChanged;

    public DiagMonCaptureService(DiagMonStore store) => _store = store;

    public async Task StartAsync(DiagMonCaptureOptions options)
    {
        lock (_gate)
        {
            if (IsCapturing) throw new InvalidOperationException("A capture is already active.");
            IsCapturing = true;
            _stopTask = null;
        }
        try
        {
            var created = _store.CreatePartial(options, Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown");
            CurrentSession = created.Session;
            CurrentDirectory = created.Directory;
            _cancel = new CancellationTokenSource();
            _processSnapshot = Process.GetProcesses().Select(p => p.Id).ToHashSet();
            SetCollectors(CurrentSession);
            CaptureConfiguration(created.Directory, "start");
            CaptureSystemProfile(created.Directory);
            CopyViewLabEvidence(created.Directory, "start");
            Process? target = ResolveInitialTarget(options);
            if (target != null) AttachTarget(target);
            StartTypePerf();
            if (options.Mode == DiagMonCaptureMode.Trace) await StartWprAsync();
            _worker = RunCaptureLoopAsync(options, _cancel.Token);
            _ = FinalizeWhenCaptureLoopEndsAsync(_worker, _cancel.Token);
            SaveManifest();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            IsCapturing = false;
            if (CurrentSession != null && CurrentDirectory != null)
            {
                CurrentSession.StopReason = "Capture start failed.";
                SaveManifest();
            }
            throw;
        }
    }

    public Task<string?> StopAsync(string reason = "Stopped by user")
    {
        lock (_gate)
        {
            if (_stopTask != null) return _stopTask;
            if (!IsCapturing || CurrentSession == null || CurrentDirectory == null)
                return Task.FromResult<string?>(null);
            _stopTask = StopCoreAsync(reason);
            return _stopTask;
        }
    }

    private async Task<string?> StopCoreAsync(string reason)
    {
        DiagMonSession session = CurrentSession ?? throw new InvalidOperationException("Capture session state was lost during finalisation.");
        string directory = CurrentDirectory ?? throw new InvalidOperationException("Capture directory state was lost during finalisation.");
        _cancel?.Cancel();
        if (_worker != null)
        {
            try { await _worker; } catch (OperationCanceledException) { } catch (Exception ex) { Mark("Process telemetry", DiagMonCollectorState.Partial, ex.Message); }
        }
        await StopOwnedCollectorAsync(_presentMon, "PresentMon");
        await StopOwnedCollectorAsync(_typePerf, "Windows counters");
        if (_wprStarted) await StopWprAsync();
        _processWriter?.Dispose(); _processWriter = null;
        CaptureModules();
        CaptureEventLogs();
        CaptureConfiguration(directory, "stop");
        CopyViewLabEvidence(directory, "stop");

        session.EndUtc = DateTimeOffset.UtcNow;
        session.DurationSeconds = Math.Max(0, (session.EndUtc.Value - session.StartUtc).TotalSeconds);
        session.StopReason = reason;
        session.Complete = session.TargetPid.HasValue && _sampleCount > 0;
        foreach (var c in session.Collectors.Where(c => c.State == DiagMonCollectorState.Pending))
        { c.State = DiagMonCollectorState.Missing; c.Message = "Collector did not start."; c.StoppedUtc = session.EndUtc; }

        DiagMonMetrics metrics = BuildMetrics();
        session.Fingerprint = BuildFingerprint();
        metrics.Comparison = _store.BuildComparison(session, metrics);
        FlagSession(metrics);
        GenerateFrameGraph(metrics);
        var annotations = _store.LoadAnnotations(directory);
        if (!session.Complete) annotations.SessionType = DiagMonSessionType.Incomplete;
        DiagMonStore.WriteSummary(directory, session, metrics);
        string finalDirectory = _store.FinalizeDirectory(directory, session);
        _store.SaveSessionFiles(finalDirectory, session, metrics, annotations);
        string result = finalDirectory;
        IsCapturing = false;
        CurrentDirectory = finalDirectory;
        StateChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }

    private async Task FinalizeWhenCaptureLoopEndsAsync(Task worker, CancellationToken token)
    {
        try { await worker; }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (Exception ex)
        {
            if (CurrentSession != null) CurrentSession.StopReason = $"Capture worker failed: {ex.Message}";
        }
        if (token.IsCancellationRequested || !IsCapturing) return;
        string reason = string.IsNullOrWhiteSpace(CurrentSession?.StopReason)
            ? "Capture worker stopped unexpectedly."
            : CurrentSession.StopReason;
        try { await StopAsync(reason); }
        catch (Exception ex)
        {
            if (CurrentSession != null) CurrentSession.StopReason = $"Automatic finalisation failed: {ex.Message}";
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public static IReadOnlyList<Process> GetCandidateProcesses() => Process.GetProcesses()
        .Where(p => { try { return p.Id != Environment.ProcessId && !string.IsNullOrWhiteSpace(p.ProcessName) && (p.MainWindowHandle != IntPtr.Zero || !string.IsNullOrWhiteSpace(p.MainWindowTitle)); } catch { return false; } })
        .OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase).ToList();

    public void Dispose()
    {
        _cancel?.Cancel();
        _processWriter?.Dispose();
        foreach (Process? p in new[] { _presentMon, _typePerf }) try { if (p != null && !p.HasExited) p.Kill(entireProcessTree: true); } catch { }
    }

    private void SetCollectors(DiagMonSession session)
    {
        session.Collectors = new()
        {
            new() { Name = "Process telemetry", State = DiagMonCollectorState.Pending, Message = "Waiting for a target process." },
            new() { Name = "PresentMon", State = DiagMonCollectorState.Pending, Message = "Waiting for a target process." },
            new() { Name = "Windows counters", State = DiagMonCollectorState.Pending },
            new() { Name = "Loaded modules", State = session.CaptureMode == DiagMonCaptureMode.Standard ? DiagMonCollectorState.Missing : DiagMonCollectorState.Pending, Message = session.CaptureMode == DiagMonCaptureMode.Standard ? "Available in Detailed or Trace mode." : "Captured when the session stops." },
            new() { Name = "Windows events", State = DiagMonCollectorState.Running, StartedUtc = DateTimeOffset.UtcNow, Message = "Application and System warnings/errors will be queried for the capture interval." },
            new() { Name = "View Lab evidence", State = DiagMonCollectorState.Running, StartedUtc = DateTimeOffset.UtcNow, Message = "Configuration, runtime logs, and the latest OpenXR trace are copied read-only." },
            new() { Name = "WPR trace", State = session.CaptureMode == DiagMonCaptureMode.Trace ? DiagMonCollectorState.Pending : DiagMonCollectorState.Missing, Message = session.CaptureMode == DiagMonCaptureMode.Trace ? "Explicit Trace mode requested." : "Disabled outside Trace mode." }
        };
    }

    private Process? ResolveInitialTarget(DiagMonCaptureOptions options)
    {
        try
        {
            if (options.TargetMode == DiagMonTargetMode.Manual && options.TargetPid.HasValue) return Process.GetProcessById(options.TargetPid.Value);
            if (options.TargetMode == DiagMonTargetMode.Foreground)
            {
                GetWindowThreadProcessId(GetForegroundWindow(), out uint pid);
                if (pid > 0 && pid != Environment.ProcessId) return Process.GetProcessById((int)pid);
            }
            if (options.TargetMode == DiagMonTargetMode.Previous)
            {
                string previous = _store.LoadIndex().Sessions.FirstOrDefault()?.Application ?? "";
                return Process.GetProcessesByName(previous).OrderByDescending(p => { try { return p.StartTime; } catch { return DateTime.MinValue; } }).FirstOrDefault();
            }
        }
        catch { }
        return null;
    }

    private async Task RunCaptureLoopAsync(DiagMonCaptureOptions options, CancellationToken token)
    {
        int interval = options.Mode == DiagMonCaptureMode.Standard ? Math.Max(1, _store.LoadSettings().StandardSampleSeconds) : Math.Max(1, _store.LoadSettings().DetailedSampleSeconds);
        DateTimeOffset? traceDeadline = options.Mode == DiagMonCaptureMode.Trace ? DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _store.LoadSettings().TraceMaximumMinutes)) : null;
        while (!token.IsCancellationRequested)
        {
            if (CurrentSession?.TargetPid == null)
            {
                Process? detected = options.TargetMode switch
                {
                    DiagMonTargetMode.NewProcess => DetectNewProcess(),
                    DiagMonTargetMode.Foreground => ResolveInitialTarget(options),
                    DiagMonTargetMode.Previous => ResolveInitialTarget(options),
                    _ => null
                };
                if (detected != null) AttachTarget(detected);
            }
            SampleTarget();
            if (CurrentSession?.TargetPid is int pid)
            {
                try { using Process _ = Process.GetProcessById(pid); }
                catch { CurrentSession.StopReason = "Target process exited unexpectedly."; break; }
            }
            if (traceDeadline.HasValue && DateTimeOffset.UtcNow >= traceDeadline.Value)
            { CurrentSession!.StopReason = "Trace mode reached its configured time cap."; break; }
            SaveManifest();
            StateChanged?.Invoke(this, EventArgs.Empty);
            await Task.Delay(TimeSpan.FromSeconds(interval), token);
        }
    }

    private Process? DetectNewProcess()
    {
        foreach (Process p in Process.GetProcesses().OrderByDescending(p => { try { return p.StartTime; } catch { return DateTime.MinValue; } }))
        {
            try
            {
                if (!_processSnapshot.Contains(p.Id) && p.Id != Environment.ProcessId && p.MainWindowHandle != IntPtr.Zero) return p;
            }
            catch { }
        }
        return null;
    }

    private void AttachTarget(Process target)
    {
        if (CurrentSession == null || CurrentDirectory == null || CurrentSession.TargetPid.HasValue) return;
        CurrentSession.TargetPid = target.Id;
        CurrentSession.TargetProcessName = target.ProcessName;
        try { CurrentSession.TargetExecutable = target.MainModule?.FileName ?? ""; } catch { CurrentSession.TargetExecutable = ""; }
        try { CurrentSession.TargetVersion = target.MainModule?.FileVersionInfo.FileVersion ?? ""; } catch { }
        _previousCpu = target.TotalProcessorTime.TotalSeconds;
        _previousCpuAt = Stopwatch.GetTimestamp();
        string csv = Path.Combine(CurrentDirectory, "Raw", "process-telemetry.csv");
        _processWriter = new StreamWriter(new FileStream(csv, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        _processWriter.WriteLine("captured_utc,pid,cpu_pct,working_set_bytes,private_bytes,paged_bytes,handles,threads,read_bytes,write_bytes");
        _processWriter.Flush();
        Mark("Process telemetry", DiagMonCollectorState.Running, $"Sampling {target.ProcessName} PID {target.Id}.");
        StartPresentMon(target.Id);
        if (CurrentSession.CaptureMode != DiagMonCaptureMode.Standard) CaptureModules(preserveExistingOnFailure: true);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SampleTarget()
    {
        if (CurrentSession?.TargetPid is not int pid || _processWriter == null) return;
        try
        {
            using Process p = Process.GetProcessById(pid);
            long now = Stopwatch.GetTimestamp();
            double elapsed = Math.Max(0.001, (now - _previousCpuAt) / (double)Stopwatch.Frequency);
            double total = p.TotalProcessorTime.TotalSeconds;
            double cpu = Math.Max(0, Math.Min(100, (total - _previousCpu) / elapsed / Math.Max(1, Environment.ProcessorCount) * 100));
            _previousCpu = total; _previousCpuAt = now;
            double ws = p.WorkingSet64 / 1048576d, priv = p.PrivateMemorySize64 / 1048576d;
            long read = 0, write = 0;
            if (GetProcessIoCounters(p.Handle, out IO_COUNTERS io)) { read = (long)io.ReadTransferCount; write = (long)io.WriteTransferCount; }
            _processWriter.WriteLine(string.Join(',', DateTimeOffset.UtcNow.ToString("O"), pid, cpu.ToString("0.###", CultureInfo.InvariantCulture), p.WorkingSet64, p.PrivateMemorySize64, p.PagedMemorySize64, p.HandleCount, p.Threads.Count, read, write));
            _processWriter.Flush();
            _cpu.Add(cpu); _workingMb.Add(ws); _privateMb.Add(priv); _sampleCount++;
        }
        catch (Exception ex) { Mark("Process telemetry", _sampleCount > 0 ? DiagMonCollectorState.Partial : DiagMonCollectorState.Failed, ex.Message); }
    }

    private void StartPresentMon(int pid)
    {
        if (CurrentDirectory == null) return;
        string? executable = ResolvePresentMonExecutable(_store.LoadSettings().PresentMonPath);
        if (executable == null) { Mark("PresentMon", DiagMonCollectorState.Missing, "The bundled PresentMon collector is missing and no configured fallback was found."); return; }
        try
        {
            var psi = Hidden(executable);
            string output = Path.Combine(CurrentDirectory, "Raw", "presentmon.csv");
            string errors = Path.Combine(CurrentDirectory, "Logs", "presentmon-errors.log");
            _presentMonExecutable = executable;
            _presentMonSessionName = "ViewLab-" + CurrentSession!.Id[..8];
            foreach (string arg in new[] { "--process_id", pid.ToString(CultureInfo.InvariantCulture), "--output_stdout", "--session_name", _presentMonSessionName, "--date_time", "--track_gpu_video", "--no_console_stats", "--terminate_on_proc_exit" }) psi.ArgumentList.Add(arg);
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            _presentMon = Process.Start(psi);
            if (_presentMon == null) throw new InvalidOperationException("PresentMon returned no process handle.");
            _presentMonOutputPump = PumpPresentMonOutputAsync(_presentMon, output, errors);
            File.WriteAllText(Path.Combine(CurrentDirectory, "Logs", "presentmon-command.txt"), string.Join(' ', psi.ArgumentList.Select(Quote)), new UTF8Encoding(false));
            Mark("PresentMon", DiagMonCollectorState.Running, $"Started owned collector PID {_presentMon.Id} for target PID {pid}.");
        }
        catch (Exception ex) { Mark("PresentMon", DiagMonCollectorState.Failed, ex.Message); }
    }

    private void StartTypePerf()
    {
        if (CurrentDirectory == null) return;
        try
        {
            string output = Path.Combine(CurrentDirectory, "Raw", "system-counters.csv");
            var psi = Hidden(Path.Combine(Environment.SystemDirectory, "typeperf.exe"));
            foreach (string arg in new[] { @"\Processor(_Total)\% Processor Time", @"\Processor(_Total)\% DPC Time", @"\Processor(_Total)\% Interrupt Time", @"\Memory\Available MBytes", @"\Memory\Committed Bytes", @"\Memory\Pool Nonpaged Bytes", @"\Memory\Pool Paged Bytes", @"\PhysicalDisk(_Total)\Disk Bytes/sec", @"\PhysicalDisk(_Total)\Avg. Disk sec/Transfer", @"\Network Interface(*)\Bytes Total/sec", @"\GPU Engine(*)\Utilization Percentage", @"\GPU Process Memory(*)\Dedicated Usage", @"\GPU Process Memory(*)\Shared Usage" }) psi.ArgumentList.Add(arg);
            psi.ArgumentList.Add("-si"); psi.ArgumentList.Add(CurrentSession!.CaptureMode == DiagMonCaptureMode.Standard ? "2" : "1"); psi.ArgumentList.Add("-f"); psi.ArgumentList.Add("CSV"); psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(output);
            _typePerf = Process.Start(psi);
            if (_typePerf == null) throw new InvalidOperationException("typeperf returned no process handle.");
            Mark("Windows counters", DiagMonCollectorState.Running, $"System, DPC/interrupt, memory, disk, network, GPU-engine and GPU-memory counters started as PID {_typePerf.Id}.");
        }
        catch (Exception ex) { Mark("Windows counters", DiagMonCollectorState.Failed, ex.Message); }
    }

    private async Task StartWprAsync()
    {
        try
        {
            var p = Process.Start(Hidden(Path.Combine(Environment.SystemDirectory, "wpr.exe"), "-start", "GeneralProfile", "-filemode"));
            if (p == null) throw new InvalidOperationException("WPR returned no process handle.");
            await p.WaitForExitAsync();
            if (p.ExitCode != 0) throw new InvalidOperationException($"WPR start exited with code {p.ExitCode}.");
            _wprStarted = true;
            Mark("WPR trace", DiagMonCollectorState.Running, "GeneralProfile file-mode trace started; configured time cap applies.");
        }
        catch (Exception ex) { Mark("WPR trace", DiagMonCollectorState.Failed, ex.Message); }
    }

    private async Task StopWprAsync()
    {
        if (CurrentDirectory == null) return;
        try
        {
            string path = Path.Combine(CurrentDirectory, "Traces", "system.etl");
            var p = Process.Start(Hidden(Path.Combine(Environment.SystemDirectory, "wpr.exe"), "-stop", path));
            if (p == null) throw new InvalidOperationException("WPR returned no process handle.");
            await p.WaitForExitAsync();
            Mark("WPR trace", p.ExitCode == 0 && File.Exists(path) ? DiagMonCollectorState.Complete : DiagMonCollectorState.Partial, p.ExitCode == 0 ? "Trace stopped and saved." : $"WPR stop exited with code {p.ExitCode}.");
        }
        catch (Exception ex) { Mark("WPR trace", DiagMonCollectorState.Partial, ex.Message); }
        finally { _wprStarted = false; }
    }

    private async Task StopOwnedCollectorAsync(Process? process, string name)
    {
        if (process == null) return;
        try
        {
            if (!process.HasExited)
            {
                bool stoppedCleanly = name == "PresentMon" && await RequestPresentMonStopAsync();
                if (!stoppedCleanly && !process.HasExited) process.Kill(entireProcessTree: true);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                if (!process.HasExited) await process.WaitForExitAsync(timeout.Token);
            }
            if (name == "PresentMon" && _presentMonOutputPump != null)
            {
                using var pumpTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                await _presentMonOutputPump.WaitAsync(pumpTimeout.Token);
            }
            string evidencePath = name == "PresentMon" ? Path.Combine(CurrentDirectory!, "Raw", "presentmon.csv") : Path.Combine(CurrentDirectory!, "Raw", "system-counters.csv");
            bool hasEvidence = File.Exists(evidencePath) && new FileInfo(evidencePath).Length > 0;
            Mark(name, hasEvidence ? DiagMonCollectorState.Complete : DiagMonCollectorState.Partial, hasEvidence ? "Collector stopped; recoverable output retained." : "Collector stopped but produced no output file.");
        }
        catch (Exception ex) { Mark(name, DiagMonCollectorState.Partial, $"Stop failed: {ex.Message}"); }
    }

    private async Task<bool> RequestPresentMonStopAsync()
    {
        if (_presentMon == null || _presentMonExecutable == null || _presentMonSessionName == null) return false;
        try
        {
            using Process? stop = Process.Start(Hidden(_presentMonExecutable,
                "--session_name", _presentMonSessionName, "--terminate_existing_session"));
            if (stop == null) return false;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await stop.WaitForExitAsync(timeout.Token);
            if (stop.ExitCode != 0) return false;
            if (!_presentMon.HasExited) await _presentMon.WaitForExitAsync(timeout.Token);
            return _presentMon.HasExited;
        }
        catch { return false; }
    }

    internal static async Task PumpPresentMonOutputAsync(Process process, string outputPath, string errorPath)
    {
        Task<string> errors = process.StandardError.ReadToEndAsync();
        await using (var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            long lastFlush = Stopwatch.GetTimestamp();
            while (await process.StandardOutput.ReadLineAsync() is string line)
            {
                await writer.WriteLineAsync(line);
                long now = Stopwatch.GetTimestamp();
                if ((now - lastFlush) / (double)Stopwatch.Frequency >= 1.0)
                {
                    await writer.FlushAsync();
                    stream.Flush(flushToDisk: true);
                    lastFlush = now;
                }
            }
            await writer.FlushAsync();
            stream.Flush(flushToDisk: true);
        }
        string errorText = await errors;
        if (!string.IsNullOrWhiteSpace(errorText))
            await File.WriteAllTextAsync(errorPath, errorText, new UTF8Encoding(false));
    }

    internal static string? ResolvePresentMonExecutable(string? configuredPath)
    {
        string installDirectory = Path.GetDirectoryName(Environment.ProcessPath ?? "") ?? AppContext.BaseDirectory;
        foreach (string candidate in new[]
        {
            Path.Combine(installDirectory, "PresentMon-2.4.1-x64.exe"),
            configuredPath ?? "",
            Path.Combine(AppContext.BaseDirectory, "PresentMon-2.4.1-x64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PresentMon", "PresentMon.exe")
        }.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            try { if (File.Exists(candidate)) return Path.GetFullPath(candidate); } catch { }
        }
        return null;
    }

    private void CaptureModules(bool preserveExistingOnFailure = true)
    {
        if (CurrentSession?.CaptureMode == DiagMonCaptureMode.Standard || CurrentSession?.TargetPid is not int pid || CurrentDirectory == null) return;
        try
        {
            using Process target = Process.GetProcessById(pid);
            var rows = new List<string> { "module_name,file_name,file_version,product_version" };
            foreach (ProcessModule m in target.Modules) rows.Add(string.Join(',', Csv(m.ModuleName), Csv(m.FileName), Csv(m.FileVersionInfo.FileVersion), Csv(m.FileVersionInfo.ProductVersion)));
            File.WriteAllLines(Path.Combine(CurrentDirectory, "Raw", "loaded-modules.csv"), rows, new UTF8Encoding(false));
            CurrentSession.GraphicsApi = DetectGraphicsApi(target.Modules.Cast<ProcessModule>().Select(m => m.ModuleName));
            Mark("Loaded modules", DiagMonCollectorState.Complete, $"Captured {rows.Count - 1} modules; graphics API {CurrentSession.GraphicsApi}.");
        }
        catch (Exception ex)
        {
            var existing = CurrentSession?.Collectors.FirstOrDefault(c => c.Name == "Loaded modules");
            if (!preserveExistingOnFailure || existing?.State != DiagMonCollectorState.Complete)
                Mark("Loaded modules", DiagMonCollectorState.Failed, ex.Message);
        }
    }

    private void CaptureEventLogs()
    {
        if (CurrentSession == null || CurrentDirectory == null) return;
        int lookbackMs = (int)Math.Min(int.MaxValue, Math.Max(60000, CurrentSession.DurationSeconds * 1000 + 10000));
        int success = 0;
        foreach (string log in new[] { "Application", "System" })
        {
            try
            {
                string output = Path.Combine(CurrentDirectory, "Events", log.ToLowerInvariant() + ".xml");
                var psi = Hidden(Path.Combine(Environment.SystemDirectory, "wevtutil.exe"), "qe", log, $"/q:*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime)<={lookbackMs}]]]", "/f:xml", "/rd:true", "/c:250");
                psi.RedirectStandardOutput = true;
                using Process p = Process.Start(psi)!;
                string xml = p.StandardOutput.ReadToEnd(); p.WaitForExit(10000);
                File.WriteAllText(output, xml, new UTF8Encoding(false)); if (xml.Length > 20) success++;
            }
            catch { }
        }
        Mark("Windows events", success == 2 ? DiagMonCollectorState.Complete : success > 0 ? DiagMonCollectorState.Partial : DiagMonCollectorState.Failed, $"Captured {success}/2 requested event logs for the session window.");
    }

    private void CaptureConfiguration(string directory, string phase)
    {
        try
        {
            string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab", "xr-viewlab.ini");
            var snapshot = new { phase, captured_utc = DateTimeOffset.UtcNow, viewlab_ini = File.Exists(configPath) ? ParseIni(configPath) : new Dictionary<string, string>(), display_width = GetSystemMetrics(0), display_height = GetSystemMetrics(1), os = Environment.OSVersion.VersionString, processors = Environment.ProcessorCount };
            DiagMonStore.WriteJsonAtomic(Path.Combine(directory, phase == "start" ? "configuration.json" : "configuration-stop.json"), snapshot);
        }
        catch { }
    }

    private void CaptureSystemProfile(string directory)
    {
        var gpu = new List<object>();
        try
        {
            using RegistryKey? video = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video");
            foreach (string adapter in video?.GetSubKeyNames() ?? Array.Empty<string>())
            using (RegistryKey? key = video?.OpenSubKey(adapter + @"\0000"))
            {
                string description = key?.GetValue("DriverDesc")?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(description)) gpu.Add(new { name = description, version = key?.GetValue("DriverVersion")?.ToString() ?? "" });
            }
        }
        catch { }
		var memory = new MEMORYSTATUSEX();
		GlobalMemoryStatusEx(memory);
        DiagMonStore.WriteJsonAtomic(Path.Combine(directory, "system-profile.json"), new { captured_utc = DateTimeOffset.UtcNow, machine = Environment.MachineName, os = Environment.OSVersion.VersionString, os_64_bit = Environment.Is64BitOperatingSystem, cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"), logical_processors = Environment.ProcessorCount, physical_memory_bytes = memory.ullTotalPhys, display = $"{GetSystemMetrics(0)}x{GetSystemMetrics(1)}", gpu });
    }

    private void CopyViewLabEvidence(string directory, string phase)
    {
        int copied = 0;
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab");
        foreach ((string source, string name) in new[] { (Path.Combine(root, "xr-viewlab.ini"), $"xr-viewlab-{phase}.ini"), (Path.Combine(root, "Logs", "ViewLab.log"), $"ViewLab-{phase}.log"), (Path.Combine(root, "Logs", "ViewLab.verbose.log"), $"ViewLab.verbose-{phase}.log"), (Path.Combine(root, "PerformanceTraces", "latest.csv"), $"performance-trace-{phase}.csv") })
        {
            try { if (File.Exists(source)) { File.Copy(source, Path.Combine(directory, "Logs", name), overwrite: true); copied++; } } catch { }
        }
        Mark("View Lab evidence",
            phase == "start" ? DiagMonCollectorState.Running : copied > 0 ? DiagMonCollectorState.Complete : DiagMonCollectorState.Partial,
            $"Copied {copied} available View Lab configuration/log/trace file(s) at {phase}.");
    }

    private DiagMonMetrics BuildMetrics()
    {
        var metrics = new DiagMonMetrics();
        if (_sampleCount > 0)
        {
            metrics.Resources.ProcessCpuAveragePct = _cpu.Average(); metrics.Resources.ProcessCpuPeakPct = _cpu.Max();
            metrics.Resources.ProcessWorkingSetPeakMb = _workingMb.Max(); metrics.Resources.ProcessPrivatePeakMb = _privateMb.Max();
            metrics.Resources.ProcessPrivateGrowthMb = _privateMb.Last() - _privateMb.First();
            Mark("Process telemetry", DiagMonCollectorState.Complete, $"Captured {_sampleCount} target-process samples.");
        }
        string present = Path.Combine(CurrentDirectory!, "Raw", "presentmon.csv");
        if (File.Exists(present)) metrics.Frames = ParsePresentMon(present);
        ParseSystemCounters(metrics);
        return metrics;
    }

    internal static DiagMonFrameMetrics ParsePresentMon(string path)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 2) return new();
        string[] header = SplitCsv(lines[0]);
        int timeIndex = FindColumn(header, "msBetweenPresents", "MsBetweenPresents", "FrameTime", "msBetweenDisplayChange", "MsBetweenDisplayChange");
        if (timeIndex < 0) return new();
        int droppedIndex = FindColumn(header, "Dropped", "dropped", "PresentResult");
        var values = new List<double>(); int measuredDropped = 0; bool measurable = droppedIndex >= 0;
        foreach (string line in lines.Skip(1))
        {
            string[] cells = SplitCsv(line); if (cells.Length <= timeIndex) continue;
            if (double.TryParse(cells[timeIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0 && value < 60000) values.Add(value);
            if (measurable && cells.Length > droppedIndex && (cells[droppedIndex].Equals("1") || cells[droppedIndex].Contains("Dropped", StringComparison.OrdinalIgnoreCase))) measuredDropped++;
        }
        if (values.Count == 0) return new();
        double[] sorted = values.OrderBy(v => v).ToArray(); double mean = values.Average(), median = Percentile(sorted, 50);
        double longThreshold = Math.Max(33.33, median * 2), severeThreshold = Math.Max(100, median * 3);
        return new DiagMonFrameMetrics
        {
            PresentedFrames = values.Count, AverageFps = 1000 / mean, OnePercentLowFps = LowFps(sorted, 0.01), PointOnePercentLowFps = LowFps(sorted, 0.001),
            MeanMs = mean, MedianMs = median, P95Ms = Percentile(sorted, 95), P99Ms = Percentile(sorted, 99), P999Ms = Percentile(sorted, 99.9), WorstMs = sorted[^1],
            StandardDeviationMs = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / values.Count), LongFrames = values.Count(v => v > longThreshold), SevereStutters = values.Count(v => v > severeThreshold),
            MeasuredDroppedFrames = measurable ? measuredDropped : null, FrameTimeColumn = header[timeIndex]
        };
    }

    private void ParseSystemCounters(DiagMonMetrics metrics)
    {
        string path = Path.Combine(CurrentDirectory!, "Raw", "system-counters.csv");
        if (!File.Exists(path)) return;
        try
        {
            string[] lines = File.ReadAllLines(path); if (lines.Length < 2) return;
            string[] headers = SplitCsv(lines[0]); var columns = Enumerable.Range(0, headers.Length).ToDictionary(i => i, i => headers[i]);
            List<double> cpu = new(), available = new(), gpu = new(), dedicated = new(), shared = new();
            foreach (string line in lines.Skip(1))
            {
                string[] cells = SplitCsv(line);
                foreach (var c in columns.Where(c => c.Key < cells.Length && double.TryParse(cells[c.Key], NumberStyles.Float, CultureInfo.InvariantCulture, out _)))
                {
                    double value = double.Parse(cells[c.Key], CultureInfo.InvariantCulture); string h = c.Value;
                    if (h.Contains("Processor(_Total)", StringComparison.OrdinalIgnoreCase) && h.Contains("Processor Time", StringComparison.OrdinalIgnoreCase)) cpu.Add(value);
                    else if (h.Contains("Available MBytes", StringComparison.OrdinalIgnoreCase)) available.Add(value);
                    else if (CurrentSession?.TargetPid is int pid && h.Contains($"pid_{pid}_", StringComparison.OrdinalIgnoreCase) && h.Contains("Utilization Percentage", StringComparison.OrdinalIgnoreCase)) gpu.Add(value);
                    else if (CurrentSession?.TargetPid is int p1 && h.Contains($"pid_{p1}_", StringComparison.OrdinalIgnoreCase) && h.Contains("Dedicated Usage", StringComparison.OrdinalIgnoreCase)) dedicated.Add(value / 1048576d);
                    else if (CurrentSession?.TargetPid is int p2 && h.Contains($"pid_{p2}_", StringComparison.OrdinalIgnoreCase) && h.Contains("Shared Usage", StringComparison.OrdinalIgnoreCase)) shared.Add(value / 1048576d);
                }
            }
            if (cpu.Count > 0) metrics.Resources.SystemCpuAveragePct = cpu.Average(); if (available.Count > 0) metrics.Resources.AvailableMemoryMinimumMb = available.Min();
            if (gpu.Count > 0) { metrics.Resources.GpuAveragePct = gpu.Average(); metrics.Resources.GpuPeakPct = gpu.Max(); }
            if (dedicated.Count > 0) metrics.Resources.DedicatedGpuMemoryPeakMb = dedicated.Max(); if (shared.Count > 0) metrics.Resources.SharedGpuMemoryPeakMb = shared.Max();
        }
        catch { }
    }

    private DiagMonFingerprint BuildFingerprint()
    {
        var factors = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["executable"] = CurrentSession!.TargetExecutable.ToLowerInvariant(), ["graphics_api"] = CurrentSession.GraphicsApi,
            ["resolution"] = $"{GetSystemMetrics(0)}x{GetSystemMetrics(1)}", ["capture_mode"] = CurrentSession.CaptureMode.ToString()
        };
        string config = Path.Combine(CurrentDirectory!, "configuration.json");
        string raw = File.Exists(config) ? File.ReadAllText(config) : "";
        foreach ((string key, string factor) in new[] { ("render_scale", "render_scale"), ("display_refresh_rate", "refresh_rate"), ("frame_limit", "frame_limit"), ("resolution", "render_resolution") })
        {
            string? value = ExtractJsonValue(raw, key); if (!string.IsNullOrWhiteSpace(value)) factors[factor] = value;
        }
        string joined = string.Join('\n', factors.Select(f => $"{f.Key}={f.Value}"));
        return new DiagMonFingerprint { Factors = factors, Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(joined))).ToLowerInvariant() };
    }

    private void FlagSession(DiagMonMetrics metrics)
    {
        if (CurrentSession!.DurationSeconds < 15) metrics.Flags.Add("Very short capture; workload representativeness is uncertain.");
        if (CurrentSession.TargetPid == null) metrics.Flags.Add("No target process was detected.");
        if (metrics.Frames.PresentedFrames == 0) metrics.Flags.Add("Missing frame-presentation data.");
        foreach (var c in CurrentSession.Collectors.Where(c => c.State is DiagMonCollectorState.Failed or DiagMonCollectorState.Partial)) metrics.Flags.Add($"Collector {c.Name} was {c.State.ToString().ToLowerInvariant()}.");
        if (metrics.Comparison.Baseline.Any(b => b.Unusual)) metrics.Flags.Add("One or more metrics are materially outside the robust comparable-session range; user validation is required.");
        if (CurrentSession.StopReason.Contains("exited unexpectedly", StringComparison.OrdinalIgnoreCase)) metrics.Flags.Add("Target process exited before Stop Capture was pressed.");
    }

    private void GenerateFrameGraph(DiagMonMetrics metrics)
    {
        string present = Path.Combine(CurrentDirectory!, "Raw", "presentmon.csv"); if (!File.Exists(present) || metrics.Frames.PresentedFrames == 0) return;
        try
        {
            string[] lines = File.ReadAllLines(present); string[] header = SplitCsv(lines[0]); int column = FindColumn(header, metrics.Frames.FrameTimeColumn); if (column < 0) return;
            double[] values = lines.Skip(1).Select(SplitCsv).Where(c => c.Length > column && double.TryParse(c[column], NumberStyles.Float, CultureInfo.InvariantCulture, out _)).Select(c => double.Parse(c[column], CultureInfo.InvariantCulture)).Where(v => v > 0 && v < 60000).ToArray();
            int width = 1200, height = 360; double max = Math.Max(16.7, Percentile(values.OrderBy(v => v).ToArray(), 99.9));
            var points = new StringBuilder(); int step = Math.Max(1, values.Length / width);
            for (int x = 0; x < width; x++) { int i = Math.Min(values.Length - 1, x * step); double y = height - Math.Min(height, values[i] / max * (height - 20)); points.Append(CultureInfo.InvariantCulture, $"{x},{y:0.0} "); }
            string svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\"><rect width=\"100%\" height=\"100%\" fill=\"#101112\"/><line x1=\"0\" y1=\"{height - 33.33 / max * (height - 20):0.0}\" x2=\"{width}\" y2=\"{height - 33.33 / max * (height - 20):0.0}\" stroke=\"#805020\"/><polyline fill=\"none\" stroke=\"#e43b4f\" stroke-width=\"1\" points=\"{points}\"/><text x=\"8\" y=\"18\" fill=\"#bbb\">Frame time (ms), scale 0–{max:0.0}</text></svg>";
            File.WriteAllText(Path.Combine(CurrentDirectory!, "Graphs", "frametimes.svg"), svg, new UTF8Encoding(false));
        }
        catch { }
    }

    private void Mark(string name, DiagMonCollectorState state, string message)
    {
        var c = CurrentSession?.Collectors.FirstOrDefault(x => x.Name == name); if (c == null) return;
        c.State = state; c.Message = message; if (state == DiagMonCollectorState.Running) c.StartedUtc ??= DateTimeOffset.UtcNow; else if (state is DiagMonCollectorState.Complete or DiagMonCollectorState.Partial or DiagMonCollectorState.Failed) c.StoppedUtc = DateTimeOffset.UtcNow;
    }

    private void SaveManifest() { if (CurrentSession != null && CurrentDirectory != null) DiagMonStore.WriteJsonAtomic(Path.Combine(CurrentDirectory, "session.json"), CurrentSession); }

    private static Dictionary<string, string> ParseIni(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); string section = "";
        foreach (string raw in File.ReadLines(path)) { string line = raw.Trim(); if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1]; continue; } int eq = line.IndexOf('='); if (eq > 0 && !line.StartsWith(';')) values[$"{section}.{line[..eq].Trim()}"] = line[(eq + 1)..].Trim(); }
        return values;
    }

    private static string DetectGraphicsApi(IEnumerable<string> modules)
    {
        var set = modules.Select(m => m.ToLowerInvariant()).ToHashSet();
        if (set.Contains("d3d12.dll")) return "Direct3D 12"; if (set.Contains("d3d11.dll")) return "Direct3D 11"; if (set.Contains("vulkan-1.dll")) return "Vulkan"; if (set.Contains("opengl32.dll")) return "OpenGL"; return "Unknown";
    }

    private static ProcessStartInfo Hidden(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (string arg in args) psi.ArgumentList.Add(arg); return psi;
    }

    private static string Quote(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
    private static string Csv(string? value) => $"\"{(value ?? "").Replace("\"", "\"\"")}\"";
    internal static string[] SplitCsv(string line)
    {
        var result = new List<string>(); var value = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < line.Length; i++) { char c = line[i]; if (c == '"') { if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; } else quoted = !quoted; } else if (c == ',' && !quoted) { result.Add(value.ToString()); value.Clear(); } else value.Append(c); }
        result.Add(value.ToString()); return result.ToArray();
    }
    private static int FindColumn(string[] header, params string[] names) { foreach (string name in names) { int i = Array.FindIndex(header, h => h.Equals(name, StringComparison.OrdinalIgnoreCase)); if (i >= 0) return i; } return -1; }
    private static double Percentile(double[] sorted, double p) { if (sorted.Length == 0) return 0; double pos = p / 100 * (sorted.Length - 1); int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos); return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo); }
    private static double LowFps(double[] sorted, double fraction) { int count = Math.Max(1, (int)Math.Ceiling(sorted.Length * fraction)); return 1000 / sorted.Skip(Math.Max(0, sorted.Length - count)).Average(); }
    private static string? ExtractJsonValue(string json, string key) { string marker = $"\"{key}\""; int i = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase); if (i < 0) return null; i = json.IndexOf(':', i + marker.Length); if (i < 0) return null; int end = json.IndexOfAny(new[] { ',', '\r', '\n', '}' }, i + 1); return json[(i + 1)..(end < 0 ? json.Length : end)].Trim().Trim('"'); }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetProcessIoCounters(IntPtr process, out IO_COUNTERS counters);
	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX buffer);
    [StructLayout(LayoutKind.Sequential)] private struct IO_COUNTERS { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)] private sealed class MEMORYSTATUSEX { public uint dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>(); public uint dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual; }
}
