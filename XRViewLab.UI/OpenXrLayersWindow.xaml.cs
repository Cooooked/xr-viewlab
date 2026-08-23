using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;

namespace XRViewLab.UI;

public sealed class LayerRow
{
    public LayerEntry Entry { get; init; } = new();
    public string ManifestPath => Entry.ManifestPath;
    public string Title => string.IsNullOrWhiteSpace(Entry.Name) ? Entry.FileName : $"{Entry.Name}   ({Entry.FileName})";
    public string StateText => Entry.Enabled ? "ON" : "off";
    public string WarningText => Entry.WarningText;
    public Visibility WarningVisibility => Entry.HasWarning ? Visibility.Visible : Visibility.Collapsed;
    public Brush StateBg => Entry.Enabled
        ? new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x24))
        : new SolidColorBrush(Color.FromRgb(0x24, 0x26, 0x2A));
    public Brush StateFg => Entry.Enabled
        ? new SolidColorBrush(Color.FromRgb(0x76, 0xC8, 0x8E))
        : new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));
}

/// <summary>
/// ViewLab's OpenXR implicit layer manager.
///
/// Self-contained: it reads and writes the layer registry directly and does not
/// depend on DiagMon2 or any other tool being installed.
/// </summary>
public partial class OpenXrLayersWindow : Window
{
    private readonly ObservableCollection<LayerRow> _rows = new();
    private LayerScope _scope = LayerScope.Win64Machine;

    public OpenXrLayersWindow()
    {
        InitializeComponent();
        LayerList.ItemsSource = _rows;
        Loaded += (_, _) => Load();
    }

    private static bool IsElevated()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void Scope_Changed(object sender, RoutedEventArgs e)
    {
        if (LayerList is null) return;
        _scope = (sender as FrameworkElement)?.Tag?.ToString() switch
        {
            "1" => LayerScope.Win64User,
            "2" => LayerScope.Win32Machine,
            "3" => LayerScope.Win32User,
            _ => LayerScope.Win64Machine,
        };
        Load();
    }

    private void Load()
    {
        _rows.Clear();
        try
        {
            foreach (var e in LayerManager.Read(_scope)) _rows.Add(new LayerRow { Entry = e });
        }
        catch (Exception ex) { Status("Could not read layers: " + ex.Message); return; }

        var needsAdmin = LayerManager.RequiresElevation(_scope);
        var canEdit = !needsAdmin || IsElevated();
        foreach (var b in new[] { EnableBtn, DisableBtn, UpBtn, DownBtn, AddBtn, RemoveBtn }) b.IsEnabled = canEdit;

        var msg = $"{_rows.Count} layer(s) registered.";
        if (!canEdit)
        {
            msg += "  Editing this scope needs Administrator.";
            Status(msg);
            OfferElevation();
            return;
        }
        Status(msg);
    }

    // Asked once per window. Relaunches only this window elevated, so the user gets a single UAC
    // prompt instead of having to close ViewLab and start the whole app as Administrator.
    private bool _elevationOffered;
    private void OfferElevation()
    {
        if (_elevationOffered || IsElevated()) return;
        _elevationOffered = true;
        var answer = MessageBox.Show(this,
            "Changing layers for all users needs Administrator permission.\n\n" +
            "Reopen this window as Administrator now? ViewLab itself will stay open.",
            "ViewLab", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;
        try
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("ViewLab executable path is unavailable.");
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "--openxr-layers",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(exe)!
            });
            Close();
        }
        catch (Exception ex)
        {
            // Most often the user simply declined the UAC prompt; keep the read-only view usable.
            Status("Could not start the elevated window: " + ex.Message);
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == TitleClose) return;
        if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
    }

    private void TitleClose_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { e.Handled = true; Close(); }
    private void TitleClose_MouseEnter(object sender, MouseEventArgs e) => TitleClose.Foreground = new SolidColorBrush(Color.FromRgb(0xEC, 0x30, 0x38));
    private void TitleClose_MouseLeave(object sender, MouseEventArgs e) => TitleClose.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x8C, 0x90));

    private void Status(string s) => StatusText.Text = s;

    private LayerRow? Selected => LayerList.SelectedItem as LayerRow;

    private void SetEnabled(bool enabled)
    {
        if (Selected is not LayerRow row) { Status("Select a layer first."); return; }
        try
        {
            LayerManager.SetEnabled(_scope, row.ManifestPath, enabled);
            var idx = LayerList.SelectedIndex;
            Load();
            if (idx >= 0 && idx < _rows.Count) LayerList.SelectedIndex = idx;
            Status($"{Path.GetFileName(row.ManifestPath)} {(enabled ? "enabled" : "disabled")}. Restart the VR app for it to take effect.");
        }
        catch (Exception ex) { Status("Failed: " + ex.Message); }
    }

    private void Enable_Click(object sender, RoutedEventArgs e) => SetEnabled(true);
    private void Disable_Click(object sender, RoutedEventArgs e) => SetEnabled(false);

    private void Move(int delta)
    {
        if (Selected is not LayerRow row) { Status("Select a layer first."); return; }
        var idx = _rows.IndexOf(row);
        var target = idx + delta;
        if (target < 0 || target >= _rows.Count) return;

        var ordered = _rows.Select(r => r.Entry).ToList();
        (ordered[idx], ordered[target]) = (ordered[target], ordered[idx]);
        try
        {
            var backup = LayerManager.Reorder(_scope, ordered);
            Load();
            LayerList.SelectedIndex = target;
            Status($"Reordered. Registry backup: {backup}");
        }
        catch (Exception ex) { Status("Reorder failed: " + ex.Message); }
    }

    private void Up_Click(object sender, RoutedEventArgs e) => Move(-1);
    private void Down_Click(object sender, RoutedEventArgs e) => Move(+1);

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select an OpenXR API layer manifest",
            Filter = "OpenXR layer manifest (*.json)|*.json|All files (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != true) return;
        try { LayerManager.Add(_scope, dlg.FileName, true); Load(); Status("Layer added and enabled."); }
        catch (Exception ex) { Status("Add failed: " + ex.Message); }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not LayerRow row) { Status("Select a layer first."); return; }
        var answer = MessageBox.Show(this,
            $"Unregister this layer?\n\n{row.ManifestPath}\n\nThe file is not deleted; only the registry entry is removed.",
            "OpenXR API Layers", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (answer != MessageBoxResult.OK) return;
        try
        {
            LayerManager.ExportBackup(_scope);
            LayerManager.Remove(_scope, row.ManifestPath);
            Load();
            Status("Layer unregistered.");
        }
        catch (Exception ex) { Status("Remove failed: " + ex.Message); }
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => Load();

    private void Report_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save layer report",
            Filter = "Markdown (*.md)|*.md",
            FileName = $"openxr-layers-{DateTime.Now:yyyyMMdd-HHmm}.md",
        };
        if (dlg.ShowDialog(this) != true) return;
        try { File.WriteAllText(dlg.FileName, LayerManager.BuildReport()); Status("Report saved."); }
        catch (Exception ex) { Status("Save failed: " + ex.Message); }
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        try { Status("Backup written: " + LayerManager.ExportBackup(_scope)); }
        catch (Exception ex) { Status("Backup failed: " + ex.Message); }
    }
}
