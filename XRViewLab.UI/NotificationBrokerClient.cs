using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace XRViewLab.UI;

internal sealed class NotificationBrokerClient
{
    private static string ConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab");
    private static string StatusPath => Path.Combine(ConfigDirectory, "notification-broker-status.json");
    private static string ProcessDirectory => Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? AppContext.BaseDirectory;
    private static string BrokerPath => Path.Combine(ProcessDirectory, "ViewLab.NotificationBroker.exe");

    public string Status { get; private set; } = "Unavailable: notification broker has not started.";

    // True only when a broker is already resident, so callers can nudge it without starting one.
    public static bool IsRunning => System.Threading.Mutex.TryOpenExisting(@"Local\XRViewLabNotificationBroker", out System.Threading.Mutex? mutex) && Dispose(mutex);

    private static bool Dispose(System.Threading.Mutex mutex) { mutex.Dispose(); return true; }

    public bool Start(bool requestAccess)
    {
        if (!File.Exists(BrokerPath))
        {
            Status = "Unavailable: ViewLab.NotificationBroker.exe is missing. Repair or reinstall ViewLab.";
            return false;
        }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = BrokerPath,
                Arguments = requestAccess ? "--request-access" : "--start",
                UseShellExecute = false,
                WorkingDirectory = ProcessDirectory,
                CreateNoWindow = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Error: broker launch failed: {ex.GetType().Name} (0x{ex.HResult:X8}).";
            return false;
        }
    }

    public bool SendTest(bool requestAccess)
    {
        if (!File.Exists(BrokerPath))
        {
            Status = "Unavailable: ViewLab.NotificationBroker.exe is missing. Repair or reinstall ViewLab.";
            return false;
        }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = BrokerPath,
                Arguments = requestAccess ? "--request-access-and-test" : "--test",
                UseShellExecute = false,
                WorkingDirectory = ProcessDirectory,
                CreateNoWindow = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Error: test command failed: {ex.GetType().Name} (0x{ex.HResult:X8}).";
            return false;
        }
    }

    // These two status files are re-read by the settings window's 1 s poll timer. Reading and
    // JSON-parsing them every tick regardless of whether the broker had written anything was pure
    // waste, so each read is gated on the file's last-write timestamp actually moving. The parsed
    // result is cached and returned unchanged while the file is untouched.
    private DateTime _statusStamp;
    private DateTime _iracingStamp;
    private string _iracingStatus = "Provider disconnected — broker has not reported telemetry.";

    private static bool Changed(string path, ref DateTime stamp)
    {
        DateTime written;
        try
        {
            if (!File.Exists(path)) return false;
            written = File.GetLastWriteTimeUtc(path);
        }
        catch { return false; }
        if (written == stamp) return false;
        stamp = written;
        return true;
    }

    public string RefreshStatus()
    {
        if (!Changed(StatusPath, ref _statusStamp)) return Status;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(StatusPath));
            JsonElement root = document.RootElement;
            string state = root.TryGetProperty("state", out var s) ? s.GetString() ?? "Unknown" : "Unknown";
            string detail = root.TryGetProperty("detail", out var d) ? d.GetString() ?? string.Empty : string.Empty;
            bool identity = root.TryGetProperty("packageIdentity", out var p) && p.GetBoolean();
            Status = $"{state}: {detail}" + (identity ? string.Empty : " Package identity is not active.");
        }
        catch (Exception ex)
        {
            Status = $"Unavailable: broker status could not be read ({ex.GetType().Name}).";
        }
        return Status;
    }

    public bool SendCommand(string command)
    {
        if (!Start(requestAccess: false)) return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = BrokerPath, Arguments = "--" + command, UseShellExecute = false,
                WorkingDirectory = ProcessDirectory, CreateNoWindow = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Status = $"Error: broker command failed: {ex.GetType().Name} (0x{ex.HResult:X8}).";
            return false;
        }
    }

    public string RefreshIRacingStatus()
    {
        string path = Path.Combine(ConfigDirectory, "iracing-status.json");
        if (!Changed(path, ref _iracingStamp)) return _iracingStatus;
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            string state = document.RootElement.GetProperty("state").GetString() ?? "Disconnected";
            string detail = document.RootElement.GetProperty("detail").GetString() ?? string.Empty;
            _iracingStatus = $"{state} — {detail}";
        }
        catch (Exception ex) { _iracingStatus = $"Provider status unavailable ({ex.GetType().Name})."; }
        return _iracingStatus;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenFileMappingW(uint desiredAccess, bool inheritHandle, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private const string ObsStateMapping = "Local\\XRViewLabObsRecordingState";

    public string RefreshObsStatus()
    {
        // Probe with OpenFileMapping first. MemoryMappedFile.OpenExisting THROWS when the mapping
        // is absent — i.e. whenever OBS is not running — and this runs once a second from the
        // settings window's poll timer, so the old code raised and swallowed an exception every
        // second of every session. Same non-throwing pattern the broker adopted in 4.1.295.
        IntPtr probe = OpenFileMappingW(0x0004 /* FILE_MAP_READ */, false, ObsStateMapping);
        if (probe == IntPtr.Zero) return "Disconnected";
        CloseHandle(probe);
        try
        {
            using MemoryMappedFile map = MemoryMappedFile.OpenExisting(ObsStateMapping, MemoryMappedFileRights.Read);
            using MemoryMappedViewAccessor view = map.CreateViewAccessor(0, 16, MemoryMappedFileAccess.Read);
            int magic = view.ReadInt32(0), version = view.ReadInt32(4), firstGeneration = view.ReadInt32(8);
            int state = view.ReadInt32(12);
            int stableGeneration = view.ReadInt32(8);
            if (magic != 0x314F4C56 || version != 1 || firstGeneration != stableGeneration) return "Disconnected";
            return state switch { 3 => "Connecting", 1 or 2 => "Connected", 4 => "Authentication failed", _ => "Disconnected" };
        }
        catch { return "Disconnected"; }
    }
}
