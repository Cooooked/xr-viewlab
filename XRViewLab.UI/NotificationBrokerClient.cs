using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace XRViewLab.UI;

internal sealed class NotificationBrokerClient
{
    private static string ConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab");
    private static string StatusPath => Path.Combine(ConfigDirectory, "notification-broker-status.json");
    private static string ProcessDirectory => Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? AppContext.BaseDirectory;
    private static string BrokerPath => Path.Combine(ProcessDirectory, "ViewLab.NotificationBroker.exe");

    public string Status { get; private set; } = "Unavailable: notification broker has not started.";

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
}
