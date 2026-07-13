using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace XRViewLab.UI;

internal static class NotificationBrokerProgram
{
    private const string MutexName = "Local\\XRViewLabNotificationBroker";
    private const string PipeName = "XRViewLabNotificationBrokerCommands";
    private const string PackageName = "cooooked.ViewLab.NotificationBroker";
    private static readonly string ConfigDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab");
    private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "xr-viewlab.ini");
    private static readonly string StatusPath = Path.Combine(ConfigDirectory, "notification-broker-status.json");

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetPrivateProfileString(string section, string key, string fallback, StringBuilder value, uint size, string path);

    [STAThread]
    public static void Main(string[] args)
    {
        string command = args.Length == 0 ? "start" : args[0].TrimStart('-').ToLowerInvariant();
        using var mutex = new Mutex(true, MutexName, out bool primary);
        if (!primary)
        {
            SendCommand(command);
            return;
        }

        Directory.CreateDirectory(ConfigDirectory);
        bool identityReady = HasIdentity();
        if (!identityReady)
        {
            var registration = RegisterIdentityAsync().GetAwaiter().GetResult();
            if (registration.Success)
            {
                Relaunch(command);
                return;
            }
            WriteStatus("UnsupportedDeployment", registration.Detail, packageIdentity: false);
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += (_, _) => Run(app.Dispatcher, command, identityReady);
        app.Run();
    }

    private static void Run(Dispatcher dispatcher, string initialCommand, bool identityReady)
    {
        var service = new NotificationService(dispatcher);
        service.StatusChanged += () => WriteStatus(service.State.ToString(), service.Status, identityReady);

        NotificationSettings current = ReadSettings();
        bool initialAccessRequest = initialCommand is "request-access" or "request-access-and-test";
        service.Update(current, requestAccess: identityReady && initialAccessRequest);
        if (initialCommand is "test" or "request-access-and-test") service.EnqueueTestNotification();
        WriteStatus(service.State.ToString(), service.Status, identityReady);

        var settingsTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            NotificationSettings next = ReadSettings();
            if (!Equivalent(current, next))
            {
                current = next;
                service.Update(current);
            }
        }, dispatcher);
        settingsTimer.Start();

        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync().ConfigureAwait(false);
                    using var reader = new StreamReader(server, Encoding.UTF8, false, 256, leaveOpen: false);
                    string received = (await reader.ReadToEndAsync().ConfigureAwait(false)).Trim().ToLowerInvariant();
                    _ = dispatcher.BeginInvoke(() =>
                    {
                        switch (received)
                        {
                            case "request-access": service.Update(current, requestAccess: identityReady); break;
                            case "request-access-and-test": service.Update(current, requestAccess: identityReady); service.EnqueueTestNotification(); break;
                            case "test": service.EnqueueTestNotification(); break;
                            case "refresh": service.Update(current); break;
                            case "shutdown":
                                settingsTimer.Stop(); service.Dispose(); Application.Current.Shutdown(); break;
                        }
                    });
                    if (received == "shutdown") return;
                }
                catch (Exception ex)
                {
                    WriteStatus("BrokerError", $"Command channel failed: {ex.GetType().Name} (0x{ex.HResult:X8}).", identityReady);
                    await Task.Delay(1000).ConfigureAwait(false);
                }
            }
        });
    }

    private static bool HasIdentity()
    {
        try { return Package.Current.Id.Name.Equals(PackageName, StringComparison.Ordinal); }
        catch { return false; }
    }

    private static async Task<(bool Success, string Detail)> RegisterIdentityAsync()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            return (false, "Windows 10 version 2004 (build 19041) or newer is required for package-with-external-location identity.");

        string processPath = Environment.ProcessPath ?? string.Empty;
        string installDirectory = Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;
        string packagePath = Path.Combine(installDirectory, "ViewLab.NotificationIdentity.msix");
        if (!File.Exists(packagePath)) return (false, "ViewLab.NotificationIdentity.msix is missing. Repair or reinstall ViewLab.");

        try
        {
            var manager = new PackageManager();
            var options = new AddPackageOptions { ExternalLocationUri = new Uri(installDirectory + Path.DirectorySeparatorChar) };
            DeploymentResult result = await manager.AddPackageByUriAsync(new Uri(packagePath), options);
            if (result.ExtendedErrorCode != null && result.ExtendedErrorCode.HResult != 0)
                return (false, $"Package registration failed: {result.ErrorText} (0x{result.ExtendedErrorCode.HResult:X8}). Repair ViewLab or its signing certificate.");
            return (true, "Notification broker package identity registered.");
        }
        catch (Exception ex)
        {
            return (false, $"Package registration failed: {ex.GetType().Name} (0x{ex.HResult:X8}): {ex.Message}");
        }
    }

    private static void Relaunch(string command)
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Broker executable path is unavailable.");
        Process.Start(new ProcessStartInfo { FileName = exe, Arguments = "--" + command, UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! });
    }

    private static void SendCommand(string command)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(1500);
            using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true };
            writer.Write(command);
        }
        catch { /* the status file will reveal a broker startup failure */ }
    }

    private static NotificationSettings ReadSettings() => new()
    {
        Enabled = ReadBool("notify_enabled", false),
        X = ReadDouble("notify_x", 0.98, 0, 1),
        Y = ReadDouble("notify_y", 0.98, 0, 1),
        Scale = ReadDouble("notify_scale", 1, 0.25, 3),
        Opacity = ReadDouble("notify_opacity", 1, 0.1, 1),
        DurationMs = ReadDouble("notify_duration_ms", 3000, 500, 15000),
        MaxVisible = (int)ReadDouble("notify_max_visible", 3, 1, 6),
        Privacy = (int)ReadDouble("notify_privacy", 0, 0, 2),
        ShowIcon = ReadBool("notify_show_icon", true),
        ShowImage = ReadBool("notify_show_image", true),
        AllowlistMode = ReadBool("notify_allowlist_mode", false),
        AppFilters = Read("notify_app_filters", string.Empty).Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    };

    private static string Read(string key, string fallback)
    {
        var b = new StringBuilder(2048);
        GetPrivateProfileString("Settings", key, fallback, b, (uint)b.Capacity, ConfigPath);
        return b.ToString();
    }

    private static bool ReadBool(string key, bool fallback) => Read(key, fallback ? "1" : "0") is "1" or "true" or "yes" or "on";
    private static double ReadDouble(string key, double fallback, double min, double max) =>
        double.TryParse(Read(key, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? Math.Clamp(value, min, max) : fallback;

    private static bool Equivalent(NotificationSettings a, NotificationSettings b) =>
        a.Enabled == b.Enabled && a.X == b.X && a.Y == b.Y && a.Scale == b.Scale && a.Opacity == b.Opacity &&
        a.DurationMs == b.DurationMs && a.MaxVisible == b.MaxVisible && a.Privacy == b.Privacy &&
        a.ShowIcon == b.ShowIcon && a.ShowImage == b.ShowImage && a.AllowlistMode == b.AllowlistMode &&
        string.Join('\0', a.AppFilters) == string.Join('\0', b.AppFilters);

    private static void WriteStatus(string state, string detail, bool packageIdentity)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            string json = JsonSerializer.Serialize(new
            {
                state,
                detail,
                updatedUtc = DateTime.UtcNow,
                processId = Environment.ProcessId,
                packageIdentity
            });
            string temp = StatusPath + ".tmp";
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            File.Move(temp, StatusPath, true);
        }
        catch { /* status reporting may fail without affecting notification/rendering paths */ }
    }
}
