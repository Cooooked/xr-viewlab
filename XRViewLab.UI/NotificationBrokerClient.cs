using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
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

    public string RefreshStatus()
    {
        try
        {
            if (!File.Exists(StatusPath)) return Status;
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
        try
        {
            if (!File.Exists(path)) return "Provider disconnected — broker has not reported telemetry.";
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            string state = document.RootElement.GetProperty("state").GetString() ?? "Disconnected";
            string detail = document.RootElement.GetProperty("detail").GetString() ?? string.Empty;
            return $"{state} — {detail}";
        }
        catch (Exception ex) { return $"Provider status unavailable ({ex.GetType().Name})."; }
    }

    public string RefreshObsStatus()
    {
        try
        {
            using MemoryMappedFile map = MemoryMappedFile.OpenExisting("Local\\XRViewLabObsRecordingState", MemoryMappedFileRights.Read);
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
