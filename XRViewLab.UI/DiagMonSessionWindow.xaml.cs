using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace XRViewLab.UI;

public partial class DiagMonSessionWindow : Window
{
    private readonly string _directory; private readonly DiagMonStore _store;
    public DiagMonSessionWindow(string directory, DiagMonStore store)
    {
        InitializeComponent(); _directory = directory; _store = store;
        SummaryBox.Text = Read("summary.md", "The session is still active; its summary will be generated when capture stops.");
        MetricsBox.Text = Read("metrics.json", "Metrics are generated when capture stops."); ManifestBox.Text = Read("session.json", "Manifest unavailable.");
    }
    private string Read(string name, string fallback) { string p = Path.Combine(_directory, name); try { return File.Exists(p) ? File.ReadAllText(p) : fallback; } catch (Exception ex) { return ex.Message; } }
    private void OpenFolder_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo { FileName = _directory, UseShellExecute = true });
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var entry = _store.LoadIndex().Sessions.FirstOrDefault(x => string.Equals(_store.ResolveDirectory(x), _directory, StringComparison.OrdinalIgnoreCase));
        if (entry == null) { MessageBox.Show(this, "Finalise the session before exporting it.", "DiagMon(ster)"); return; }
        try { string zip = _store.ExportPackage(entry); Process.Start(new ProcessStartInfo { FileName = zip, UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed"); }
    }
}
