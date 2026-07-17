using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace XRViewLab.UI;

public partial class DiagMonWindow : Window
{
    private sealed record ProcessChoice(int Pid, string Name) { public string Display => $"{Name} — PID {Pid}"; }
    private readonly DiagMonStore _store = new();
    private readonly DiagMonCaptureService _capture;
    private readonly DispatcherTimer _timer;
    private string? _lastDirectory;

    public DiagMonWindow()
    {
        InitializeComponent();
        _capture = new DiagMonCaptureService(_store);
        _capture.StateChanged += (_, _) => Dispatcher.Invoke(UpdateState);
        ModeCombo.ItemsSource = Enum.GetValues<DiagMonCaptureMode>(); ModeCombo.SelectedItem = DiagMonCaptureMode.Standard;
        TargetModeCombo.ItemsSource = Enum.GetValues<DiagMonTargetMode>(); TargetModeCombo.SelectedItem = DiagMonTargetMode.Foreground;
        DiagMonSettings retention = _store.LoadSettings();
        RetentionDaysBox.Text = retention.RetentionDays.ToString(); RetentionCountBox.Text = retention.RetentionSessionCount.ToString(); RetentionMbBox.Text = retention.RetentionMaximumMb.ToString();
        RefreshProcesses();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateState(); _timer.Start();
        Loaded += async (_, _) =>
        {
            int recovered = await _store.RecoverAbandonedAsync();
            if (recovered > 0) MessageBox.Show(this, $"Recovered {recovered} interrupted capture session(s) and marked them incomplete. Their partial evidence remains in the Session Library.", "DiagMon(ster) recovery", MessageBoxButton.OK, MessageBoxImage.Information);
            ShowRetention(); UpdateState();
        };
    }

    protected override async void OnClosing(CancelEventArgs e)
    {
        if (_capture.IsCapturing)
        {
            var answer = MessageBox.Show(this, "A capture is active. Stop and finalise it before closing?", "DiagMon(ster)", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (answer == MessageBoxResult.Cancel) { e.Cancel = true; return; }
            if (answer == MessageBoxResult.No) { e.Cancel = true; return; }
            e.Cancel = true; _lastDirectory = await _capture.StopAsync("Stopped because the DiagMon(ster) window was closed."); Close(); return;
        }
        _timer.Stop(); _capture.Dispose(); base.OnClosing(e);
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var targetMode = (DiagMonTargetMode)TargetModeCombo.SelectedItem;
            int? pid = targetMode == DiagMonTargetMode.Manual ? (ProcessCombo.SelectedItem as ProcessChoice)?.Pid : null;
            if (targetMode == DiagMonTargetMode.Manual && pid == null) { MessageBox.Show(this, "Choose a running target process first.", "DiagMon(ster)", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            await _capture.StartAsync(new DiagMonCaptureOptions { Mode = (DiagMonCaptureMode)ModeCombo.SelectedItem, TargetMode = targetMode, TargetPid = pid, UserLabel = LabelBox.Text });
            _lastDirectory = _capture.CurrentDirectory; UpdateState();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Capture could not start", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopButton.IsEnabled = false;
        try { StatusText.Text = "Finalising…"; _lastDirectory = await _capture.StopAsync(); ShowRetention(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Capture finalisation failed", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { UpdateState(); }
    }

    private void OpenCurrent_Click(object sender, RoutedEventArgs e)
    {
        string? directory = _capture.CurrentDirectory ?? _lastDirectory;
        if (directory != null && Directory.Exists(directory)) new DiagMonSessionWindow(directory, _store) { Owner = this }.Show();
    }

    private void OpenLibrary_Click(object sender, RoutedEventArgs e) => new DiagMonLibraryWindow(_store) { Owner = this }.Show();

    private void SaveRetention_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RetentionDaysBox.Text, out int days) || !int.TryParse(RetentionCountBox.Text, out int count) || !int.TryParse(RetentionMbBox.Text, out int mb) || days < 1 || count < 1 || mb < 10)
        { MessageBox.Show(this, "Enter at least 1 day, 1 session and 10 MB.", "Retention guidance", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        DiagMonSettings settings = _store.LoadSettings(); settings.RetentionDays = days; settings.RetentionSessionCount = count; settings.RetentionMaximumMb = mb; _store.SaveSettings(settings); ShowRetention();
    }

    private void OpenVrSessionGraph_Click(object sender, RoutedEventArgs e)
    {
        new PerformanceTraceLibraryWindow { Owner = this }.Show();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        string? directory = _capture.CurrentDirectory ?? _lastDirectory; if (directory == null) return;
        var entry = _store.LoadIndex().Sessions.FirstOrDefault(x => string.Equals(_store.ResolveDirectory(x), directory, StringComparison.OrdinalIgnoreCase));
        if (entry == null) { MessageBox.Show(this, "Stop and finalise the capture before exporting it.", "DiagMon(ster)"); return; }
        try { string zip = _store.ExportPackage(entry); Process.Start(new ProcessStartInfo { FileName = zip, UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void TargetModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProcessCombo == null) return; ProcessCombo.IsEnabled = TargetModeCombo.SelectedItem is DiagMonTargetMode.Manual;
        if (ProcessCombo.IsEnabled) RefreshProcesses();
    }

    private void ModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModeHelpText == null || ModeCombo.SelectedItem == null) return;
        ModeHelpText.Text = (DiagMonCaptureMode)ModeCombo.SelectedItem switch
        {
            DiagMonCaptureMode.Standard => "Standard: low-rate telemetry and PresentMon. Safe default; no ETW trace.",
            DiagMonCaptureMode.Detailed => "Detailed: one-second telemetry plus loaded modules and graphics-API detection.",
            _ => $"Trace: Detailed capture plus bounded WPR GeneralProfile. Maximum {_store.LoadSettings().TraceMaximumMinutes} minutes."
        };
    }

    private void RefreshProcesses()
    {
        var choices = DiagMonCaptureService.GetCandidateProcesses().Select(p => new ProcessChoice(p.Id, p.ProcessName)).ToList(); ProcessCombo.ItemsSource = choices; ProcessCombo.SelectedIndex = choices.Count > 0 ? 0 : -1;
    }

    private void UpdateState()
    {
        bool active = _capture.IsCapturing; StartButton.IsEnabled = !active; StopButton.IsEnabled = active; ModeCombo.IsEnabled = !active; TargetModeCombo.IsEnabled = !active; ProcessCombo.IsEnabled = !active && TargetModeCombo.SelectedItem is DiagMonTargetMode.Manual; LabelBox.IsEnabled = !active;
        OpenCurrentButton.IsEnabled = _capture.CurrentDirectory != null || _lastDirectory != null; ExportButton.IsEnabled = !active && (_capture.CurrentDirectory != null || _lastDirectory != null);
        StatusText.Text = active ? (_capture.CurrentSession?.TargetPid == null ? "Capturing — waiting for target" : "Capture active") : "Idle";
        ElapsedText.Text = active && _capture.CaptureStartUtc.HasValue ? (DateTimeOffset.UtcNow - _capture.CaptureStartUtc.Value).ToString(@"hh\:mm\:ss") : "00:00:00";
        var s = _capture.CurrentSession; TargetText.Text = s?.TargetPid is int pid ? $"{s.TargetProcessName} — PID {pid}\nSelection: {s.TargetSelection}" : $"No target detected\nSelection: {s?.TargetSelection ?? (DiagMonTargetMode)TargetModeCombo.SelectedItem}";
        CollectorGrid.ItemsSource = null; CollectorGrid.ItemsSource = s?.Collectors;
    }

    private void ShowRetention() { var warnings = _store.CheckRetention(); RetentionText.Text = warnings.Count == 0 ? "Storage is within configured guidance." : string.Join("\n", warnings) + " Evidence is never deleted automatically."; }
}
