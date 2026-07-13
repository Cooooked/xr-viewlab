using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

string marker = args.Length > 0 ? args[0] : DateTime.UtcNow.ToString("O");
string diagnosticPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab", "notification-fixture-status.json");
try
{
    var xml = new XmlDocument();
    xml.LoadXml($"<toast><visual><binding template='ToastGeneric'><text>External ViewLab notification fixture</text><text>Real packaged Windows notification {System.Security.SecurityElement.Escape(marker)}</text></binding></visual></toast>");
    ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(xml));
    Thread.Sleep(750);
    File.WriteAllText(diagnosticPath, JsonSerializer.Serialize(new
    {
        marker,
        package = Package.Current.Id.Name,
        family = Package.Current.Id.FamilyName,
        ownHistoryCount = ToastNotificationManager.History.GetHistory().Count,
        error = string.Empty
    }));
}
catch (Exception ex)
{
    Directory.CreateDirectory(Path.GetDirectoryName(diagnosticPath)!);
    File.WriteAllText(diagnosticPath, JsonSerializer.Serialize(new { marker, error = $"{ex.GetType().Name} (0x{ex.HResult:X8}): {ex.Message}" }));
    Environment.ExitCode = 1;
}
