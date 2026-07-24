using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace XRViewLab.UI;

public sealed class DiagMonStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string RootPath { get; }
    public string SessionsPath => Path.Combine(RootPath, "Sessions");
    public string IndexPath => Path.Combine(RootPath, "Index", "sessions.json");
    public string SettingsPath => Path.Combine(RootPath, "settings.json");

    public DiagMonStore(string? rootPath = null)
    {
        RootPath = Path.GetFullPath(rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab", "DiagMon"));
    }

    // Browsing DiagMon must leave no trace on disk, so the store is materialised on first write only.
    public void EnsureCreated()
    {
        Directory.CreateDirectory(SessionsPath);
        Directory.CreateDirectory(Path.GetDirectoryName(IndexPath)!);
        Directory.CreateDirectory(Path.Combine(RootPath, "Baselines"));
        if (!File.Exists(IndexPath)) WriteJsonAtomic(IndexPath, new DiagMonIndex());
        if (!File.Exists(SettingsPath)) WriteJsonAtomic(SettingsPath, new DiagMonSettings());
    }

    public DiagMonSettings LoadSettings() => ReadJson<DiagMonSettings>(SettingsPath) ?? new();
    public void SaveSettings(DiagMonSettings settings) { EnsureCreated(); WriteJsonAtomic(SettingsPath, settings); }
    public DiagMonIndex LoadIndex() => ReadJson<DiagMonIndex>(IndexPath) ?? new();
    public DiagMonSession? LoadSession(string directory) => ReadJson<DiagMonSession>(Path.Combine(directory, "session.json"));
    public DiagMonMetrics LoadMetrics(string directory) => ReadJson<DiagMonMetrics>(Path.Combine(directory, "metrics.json")) ?? new();
    public DiagMonAnnotations LoadAnnotations(string directory) => ReadJson<DiagMonAnnotations>(Path.Combine(directory, "annotations.json")) ?? new();

    public string? ResolveDirectory(DiagMonIndexEntry entry)
    {
        string full = Path.GetFullPath(Path.Combine(RootPath, entry.RelativePath));
        return full.StartsWith(Path.GetFullPath(SessionsPath), StringComparison.OrdinalIgnoreCase) && Directory.Exists(full) ? full : null;
    }

    public (DiagMonSession Session, string Directory) CreatePartial(DiagMonCaptureOptions options, string viewLabVersion)
    {
        EnsureCreated();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string id = Guid.NewGuid().ToString("D");
        string label = Sanitize(options.UserLabel, "capture");
        string name = $"{now.LocalDateTime:yyyy-MM-dd_HHmmss}_{label}_{id[..8]}.partial";
        string directory = Path.Combine(SessionsPath, name);
        Directory.CreateDirectory(directory);
        foreach (string child in new[] { "Raw", "Logs", "Events", "Traces", "Export", "Graphs" })
            Directory.CreateDirectory(Path.Combine(directory, child));
        var session = new DiagMonSession
        {
            Id = id, DirectoryName = name, UserLabel = options.UserLabel.Trim(), CaptureMode = options.Mode,
            TargetSelection = options.TargetMode, StartUtc = now, ViewLabVersion = viewLabVersion,
            AnchorStopwatchTicks = System.Diagnostics.Stopwatch.GetTimestamp(), StopwatchFrequency = System.Diagnostics.Stopwatch.Frequency
        };
        WriteJsonAtomic(Path.Combine(directory, "session.json"), session);
        WriteJsonAtomic(Path.Combine(directory, "annotations.json"), new DiagMonAnnotations());
        return (session, directory);
    }

    public string FinalizeDirectory(string partialDirectory, DiagMonSession session)
    {
        string target = partialDirectory.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)
            ? partialDirectory[..^8] : partialDirectory;
        session.DirectoryName = Path.GetFileName(target);
        WriteJsonAtomic(Path.Combine(partialDirectory, "session.json"), session);
        if (!string.Equals(partialDirectory, target, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(target)) target += "_recovered";
            Directory.Move(partialDirectory, target);
        }
        return target;
    }

    public void SaveSessionFiles(string directory, DiagMonSession session, DiagMonMetrics metrics, DiagMonAnnotations annotations)
    {
        session.RelativeFiles = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(directory, p).Replace('\\', '/')).OrderBy(p => p).ToList();
        WriteJsonAtomic(Path.Combine(directory, "session.json"), session);
        WriteJsonAtomic(Path.Combine(directory, "metrics.json"), metrics);
        WriteJsonAtomic(Path.Combine(directory, "annotations.json"), annotations);
        UpsertIndex(directory, session, metrics, annotations);
    }

    public void SaveAnnotations(DiagMonIndexEntry entry, DiagMonAnnotations annotations)
    {
        string directory = ResolveDirectory(entry) ?? throw new DirectoryNotFoundException(entry.RelativePath);
        annotations.UpdatedUtc = DateTimeOffset.UtcNow;
        WriteJsonAtomic(Path.Combine(directory, "annotations.json"), annotations);
        var session = LoadSession(directory) ?? throw new InvalidDataException("Missing session manifest.");
        UpsertIndex(directory, session, LoadMetrics(directory), annotations);
    }

    public async Task<int> RecoverAbandonedAsync()
    {
        int recovered = 0;
        if (!Directory.Exists(SessionsPath)) return recovered;
        foreach (string partial in Directory.EnumerateDirectories(SessionsPath, "*.partial"))
        {
            DiagMonSession? session = LoadSession(partial);
            if (session == null) continue;
            session.Complete = false;
            session.EndUtc ??= DateTimeOffset.UtcNow;
            session.DurationSeconds = Math.Max(0, (session.EndUtc.Value - session.StartUtc).TotalSeconds);
            session.StopReason = "View Lab or the collector stopped before the session was finalised.";
            foreach (var collector in session.Collectors.Where(c => c.State is DiagMonCollectorState.Running or DiagMonCollectorState.Pending))
            {
                collector.State = DiagMonCollectorState.Partial;
                collector.Message = "Capture was abandoned; recoverable output was retained.";
                collector.StoppedUtc = session.EndUtc;
            }
            var annotations = LoadAnnotations(partial);
            annotations.SessionType = DiagMonSessionType.Incomplete;
            var metrics = File.Exists(Path.Combine(partial, "metrics.json")) ? LoadMetrics(partial) : new DiagMonMetrics();
            if (!metrics.Flags.Contains("Interrupted capture recovered on the next View Lab launch."))
                metrics.Flags.Add("Interrupted capture recovered on the next View Lab launch.");
            string final = FinalizeDirectory(partial, session);
            WriteSummary(final, session, metrics);
            SaveSessionFiles(final, session, metrics, annotations);
            recovered++;
            await Task.Yield();
        }
        return recovered;
    }

    public DiagMonComparison BuildComparison(DiagMonSession current, DiagMonMetrics metrics)
    {
        var result = new DiagMonComparison();
        var selected = new List<(DiagMonIndexEntry Entry, DiagMonMetrics Metrics, DiagMonSession Session)>();
        foreach (var entry in LoadIndex().Sessions.OrderByDescending(e => e.StartUtc))
        {
            string? dir = ResolveDirectory(entry);
            if (dir == null || entry.Id == current.Id) continue;
            if (entry.Validation == DiagMonValidation.Invalid || entry.SessionType is DiagMonSessionType.Experiment or DiagMonSessionType.StressTest or DiagMonSessionType.Incomplete)
            {
                result.ExclusionReasons.Add($"{entry.Id}: excluded by {entry.Validation}/{entry.SessionType} classification.");
                continue;
            }
            if (entry.Validation != DiagMonValidation.Valid && entry.SessionType != DiagMonSessionType.Baseline)
            {
                result.ExclusionReasons.Add($"{entry.Id}: not validated and not a user baseline.");
                continue;
            }
            var prior = LoadSession(dir);
            if (prior == null || !string.Equals(prior.TargetExecutable, current.TargetExecutable, StringComparison.OrdinalIgnoreCase))
            {
                result.ExclusionReasons.Add($"{entry.Id}: target executable differs.");
                continue;
            }
            bool exact = prior.Fingerprint.Hash == current.Fingerprint.Hash;
            if (!exact && MajorMismatch(prior.Fingerprint, current.Fingerprint, out string reason))
            {
                result.ExclusionReasons.Add($"{entry.Id}: {reason}");
                continue;
            }
            selected.Add((entry, LoadMetrics(dir), prior));
            result.SelectionReasons.Add($"{entry.Id}: same executable; configuration {(exact ? "fingerprint matches" : "is partially comparable") }.");
        }
        result.SelectedSessionIds = selected.Select(s => s.Entry.Id).ToList();
        result.Comparability = selected.Count == 0 ? DiagMonComparability.None
            : selected.All(s => s.Session.Fingerprint.Hash == current.Fingerprint.Hash) ? DiagMonComparability.Direct : DiagMonComparability.Partial;
        result.Confidence = selected.Count >= 5 ? "High" : selected.Count >= 3 ? "Moderate" : selected.Count > 0 ? "Low" : "None";
        AddBaseline(result, "Average FPS", metrics.Frames.AverageFps, selected.Select(s => s.Metrics.Frames.AverageFps), lowerIsWorse: true);
        AddBaseline(result, "P99 frame time (ms)", metrics.Frames.P99Ms, selected.Select(s => s.Metrics.Frames.P99Ms), lowerIsWorse: false);
        AddBaseline(result, "Severe stutters", metrics.Frames.SevereStutters, selected.Select(s => (double?)s.Metrics.Frames.SevereStutters), lowerIsWorse: false);
        AddBaseline(result, "Peak process private memory (MB)", metrics.Resources.ProcessPrivatePeakMb, selected.Select(s => s.Metrics.Resources.ProcessPrivatePeakMb), lowerIsWorse: false);
        return result;
    }

    public string ExportPackage(DiagMonIndexEntry entry)
    {
        string directory = ResolveDirectory(entry) ?? throw new DirectoryNotFoundException(entry.RelativePath);
        string export = Path.Combine(directory, "Export");
        Directory.CreateDirectory(export);
        string staging = Path.Combine(export, ".package-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            CopySessionEvidence(directory, staging);
            DiagMonSession session = LoadSession(directory) ?? throw new InvalidDataException("Missing session.json");
            DiagMonMetrics metrics = LoadMetrics(directory);
            DiagMonAnnotations annotations = LoadAnnotations(directory);
            string history = Path.Combine(staging, "HistoricalContext");
            Directory.CreateDirectory(history);
            foreach (string id in metrics.Comparison.SelectedSessionIds)
            {
                var priorEntry = LoadIndex().Sessions.FirstOrDefault(s => s.Id == id);
                string? prior = priorEntry == null ? null : ResolveDirectory(priorEntry);
                if (prior == null) continue;
                string selected = Path.Combine(history, id);
                Directory.CreateDirectory(selected);
                foreach (string file in new[] { "summary.md", "metrics.json", "configuration.json", "annotations.json", "session.json" })
                    if (File.Exists(Path.Combine(prior, file))) File.Copy(Path.Combine(prior, file), Path.Combine(selected, file));
            }
            string excluded = Path.Combine(history, "RecentExcluded");
            foreach (var priorEntry in LoadIndex().Sessions.Where(e => e.Id != session.Id &&
                         e.Application.Equals(session.TargetProcessName, StringComparison.OrdinalIgnoreCase) &&
                         (e.Validation == DiagMonValidation.Invalid || e.SessionType is DiagMonSessionType.Experiment or DiagMonSessionType.StressTest or DiagMonSessionType.Incomplete))
                     .Take(3))
            {
                string? prior = ResolveDirectory(priorEntry); if (prior == null) continue;
                string selected = Path.Combine(excluded, priorEntry.Id); Directory.CreateDirectory(selected);
                foreach (string file in new[] { "summary.md", "metrics.json", "annotations.json", "session.json" })
                    if (File.Exists(Path.Combine(prior, file))) File.Copy(Path.Combine(prior, file), Path.Combine(selected, file));
            }
            var context = new { schema_version = 1, current_session = session, metrics, annotations, comparison = metrics.Comparison,
                caveats = new[] { "Built-in analysis is deterministic and does not claim a root cause.", "Missing collector output is unavailable, not zero.", "AllowsTearing is not dropped-frame evidence." } };
            WriteJsonAtomic(Path.Combine(staging, "analysis-context.json"), context);
            File.WriteAllText(Path.Combine(staging, "AI-BRIEFING.md"), BuildBriefing(session, metrics, annotations), new UTF8Encoding(false));
            var files = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).Select(path => new
            {
                path = Path.GetRelativePath(staging, path).Replace('\\', '/'),
                bytes = new FileInfo(path).Length,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()
            }).OrderBy(f => f.path).ToList();
            WriteJsonAtomic(Path.Combine(staging, "manifest.json"), new { schema_version = 1, created_utc = DateTimeOffset.UtcNow, session_id = session.Id, files });
            string zip = Path.Combine(export, $"{session.DirectoryName}-analysis.zip");
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(staging, zip, CompressionLevel.Optimal, includeBaseDirectory: false);
            return zip;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
    }

    public void Delete(DiagMonIndexEntry entry)
    {
        string? directory = ResolveDirectory(entry);
        if (directory != null) Directory.Delete(directory, recursive: true);
        var index = LoadIndex();
        index.Sessions.RemoveAll(s => s.Id == entry.Id);
        WriteJsonAtomic(IndexPath, index);
    }

    public List<string> CheckRetention()
    {
        DiagMonSettings settings = LoadSettings();
        var warnings = new List<string>();
        if (!Directory.Exists(SessionsPath)) return warnings;
        var dirs = Directory.EnumerateDirectories(SessionsPath).Select(d => new DirectoryInfo(d)).ToList();
        long bytes = dirs.Sum(d => DirectorySize(d.FullName));
        if (dirs.Count > settings.RetentionSessionCount) warnings.Add($"{dirs.Count} sessions exceed the configured count of {settings.RetentionSessionCount}.");
        if (bytes > settings.RetentionMaximumMb * 1024L * 1024L) warnings.Add($"Session storage is {bytes / 1048576d:0} MB; configured guidance is {settings.RetentionMaximumMb} MB.");
        int old = dirs.Count(d => d.CreationTimeUtc < DateTime.UtcNow.AddDays(-settings.RetentionDays));
        if (old > 0) warnings.Add($"{old} session(s) are older than {settings.RetentionDays} days.");
        return warnings; // Evidence is never silently deleted. The library provides explicit deletion.
    }

    public static void WriteSummary(string directory, DiagMonSession session, DiagMonMetrics metrics)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# DiagMon(ster) session — {session.UserLabel}").AppendLine();
        sb.AppendLine($"- Capture: {session.StartUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz} to {(session.EndUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "interrupted")}");
        sb.AppendLine($"- Duration: {TimeSpan.FromSeconds(session.DurationSeconds):hh\\:mm\\:ss}");
        sb.AppendLine($"- Target: {(string.IsNullOrWhiteSpace(session.TargetProcessName) ? "not detected" : session.TargetProcessName)} {(session.TargetPid is int pid ? $"(PID {pid})" : "")}");
        sb.AppendLine($"- Mode: {session.CaptureMode}; selection: {session.TargetSelection}; result: {(session.Complete ? "complete" : "incomplete")}").AppendLine();
        if (metrics.Frames.PresentedFrames > 0)
        {
            sb.AppendLine("## Frame presentation").AppendLine();
            sb.AppendLine($"{metrics.Frames.PresentedFrames:N0} presented frames were analysed. Average performance was {F(metrics.Frames.AverageFps)} FPS; p99 frame time was {F(metrics.Frames.P99Ms)} ms and the worst frame was {F(metrics.Frames.WorstMs)} ms.");
            sb.AppendLine($"The documented heuristic found {metrics.Frames.LongFrames:N0} long frames and {metrics.Frames.SevereStutters:N0} severe stutters. Dropped frames are {(metrics.Frames.MeasuredDroppedFrames.HasValue ? metrics.Frames.MeasuredDroppedFrames.Value.ToString(CultureInfo.InvariantCulture) : "not directly measurable from the available columns")}.").AppendLine();
        }
        else sb.AppendLine("## Frame presentation\n\nPresentMon produced no usable frame-presentation rows. Frame performance is inconclusive.\n");
        sb.AppendLine("## Resource behaviour").AppendLine();
        sb.AppendLine($"Target CPU averaged {F(metrics.Resources.ProcessCpuAveragePct)}% and peaked at {F(metrics.Resources.ProcessCpuPeakPct)}%. Private memory peaked at {F(metrics.Resources.ProcessPrivatePeakMb)} MB with {F(metrics.Resources.ProcessPrivateGrowthMb)} MB observed growth.");
        if (metrics.Resources.GpuPeakPct.HasValue) sb.AppendLine($"Target GPU-engine utilisation peaked at {F(metrics.Resources.GpuPeakPct)}%; dedicated GPU memory peaked at {F(metrics.Resources.DedicatedGpuMemoryPeakMb)} MB.");
        sb.AppendLine().AppendLine("## Historical context").AppendLine();
        if (metrics.Comparison.SelectedSessionIds.Count == 0)
            sb.AppendLine("This session is not comparable with validated history. No eligible like-for-like baseline exists.");
        else
        {
            sb.AppendLine($"{metrics.Comparison.SelectedSessionIds.Count} validated comparable session(s) formed a {metrics.Comparison.Confidence.ToLowerInvariant()}-confidence robust median/IQR baseline ({metrics.Comparison.Comparability}).");
            foreach (var b in metrics.Comparison.Baseline)
                sb.AppendLine($"- {b.Name}: current {F(b.Current)}, baseline median {b.Median:0.###}, {(b.Unusual ? "materially different" : "within the normal historical range")}.");
        }
        sb.AppendLine().AppendLine("## Evidence quality").AppendLine();
        foreach (var c in session.Collectors) sb.AppendLine($"- {c.Name}: **{c.State}** — {c.Message}");
        foreach (string flag in metrics.Flags) sb.AppendLine($"- Flag: {flag}");
        sb.AppendLine().AppendLine("The built-in analysis reports observations and deterministic comparisons. It does not assert a root cause.");
        File.WriteAllText(Path.Combine(directory, "summary.md"), sb.ToString(), new UTF8Encoding(false));
    }

    public static T? ReadJson<T>(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions) : default; }
        catch { return default; }
    }

    public static void WriteJsonAtomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    private void UpsertIndex(string directory, DiagMonSession session, DiagMonMetrics metrics, DiagMonAnnotations annotations)
    {
        var index = LoadIndex();
        index.Sessions.RemoveAll(s => s.Id == session.Id);
        index.Sessions.Add(new DiagMonIndexEntry
        {
            Id = session.Id, RelativePath = Path.GetRelativePath(RootPath, directory).Replace('\\', '/'), StartUtc = session.StartUtc,
            Application = string.IsNullOrWhiteSpace(session.TargetProcessName) ? "Unknown" : session.TargetProcessName,
            UserLabel = session.UserLabel, DurationSeconds = session.DurationSeconds, CaptureMode = session.CaptureMode,
            AverageFps = metrics.Frames.AverageFps, P99Ms = metrics.Frames.P99Ms, SevereStutters = metrics.Frames.SevereStutters,
            PeakGpuPct = metrics.Resources.GpuPeakPct, PeakGpuMemoryMb = metrics.Resources.DedicatedGpuMemoryPeakMb,
            MemoryGrowthMb = metrics.Resources.ProcessPrivateGrowthMb, Validation = annotations.Validation, SessionType = annotations.SessionType,
            Comparability = metrics.Comparison.Comparability, Notes = annotations.Notes,
            Tags = string.Join(", ", annotations.Tags)
        });
        index.Sessions = index.Sessions.OrderByDescending(s => s.StartUtc).ToList();
        WriteJsonAtomic(IndexPath, index);
    }

    private static void AddBaseline(DiagMonComparison result, string name, double? current, IEnumerable<double?> history, bool lowerIsWorse)
    {
        double[] v = history.Where(x => x.HasValue).Select(x => x!.Value).OrderBy(x => x).ToArray();
        if (current == null || v.Length == 0) return;
        double median = Percentile(v, 50), q1 = Percentile(v, 25), q3 = Percentile(v, 75), iqr = q3 - q1;
        double tolerance = Math.Max(Math.Abs(median) * 0.10, iqr * 1.5);
        bool unusual = Math.Abs(current.Value - median) > tolerance;
        result.Baseline.Add(new DiagMonBaselineMetric { Name = name, Current = current, Median = median, Q1 = q1, Q3 = q3,
            DifferencePercent = Math.Abs(median) < 0.000001 ? null : (current.Value - median) / median * 100, Unusual = unusual });
    }

    private static bool MajorMismatch(DiagMonFingerprint a, DiagMonFingerprint b, out string reason)
    {
        foreach (string key in new[] { "resolution", "graphics_api", "render_scale", "refresh_rate", "frame_limit", "hardware", "driver_major" })
        {
            a.Factors.TryGetValue(key, out string? av); b.Factors.TryGetValue(key, out string? bv);
            if (!string.IsNullOrWhiteSpace(av) && !string.IsNullOrWhiteSpace(bv) && !string.Equals(av, bv, StringComparison.OrdinalIgnoreCase))
            { reason = $"{key} differs ({av} vs {bv})."; return true; }
        }
        reason = ""; return false;
    }

    private static double Percentile(double[] sorted, double percentile)
    {
        if (sorted.Length == 1) return sorted[0];
        double position = percentile / 100 * (sorted.Length - 1);
        int lo = (int)Math.Floor(position), hi = (int)Math.Ceiling(position);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (position - lo);
    }

    private static string Sanitize(string value, string fallback)
    {
        string clean = new(value.Trim().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        clean = string.Join('-', clean.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(clean) ? fallback : clean[..Math.Min(48, clean.Length)].ToLowerInvariant();
    }

    private static string F(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "unavailable";
    private static long DirectorySize(string path) => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f => { try { return new FileInfo(f).Length; } catch { return 0; } });

    private static void CopySessionEvidence(string source, string destination)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            if (relative.StartsWith("Export" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            string target = Path.Combine(destination, "CurrentSession", relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string BuildBriefing(DiagMonSession s, DiagMonMetrics m, DiagMonAnnotations a) => $"""
        # DiagMon(ster) analysis briefing

        Analyse session `{s.Id}` for `{s.TargetProcessName}`. The user labelled it **{s.UserLabel}**, validation is **{a.Validation}**, and session type is **{a.SessionType}**.

        Observed: {m.Frames.PresentedFrames} presented frames, average FPS {F(m.Frames.AverageFps)}, p99 {F(m.Frames.P99Ms)} ms, {m.Frames.SevereStutters} severe stutters. Comparison is {m.Comparison.Comparability} with {m.Comparison.SelectedSessionIds.Count} selected historical sessions and {m.Comparison.Confidence.ToLowerInvariant()} confidence.

        Read `analysis-context.json` first, then `CurrentSession/summary.md`. Historical sessions were selected by executable, validation/classification, and configuration fingerprint. Do not treat missing data as zero, cadence estimates as measured dropped frames, or correlation as proof of cause. `AllowsTearing` is not a dropped-frame signal.
        """;
}
