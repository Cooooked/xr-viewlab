using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace XRViewLab.UI;

public partial class DiagMonLibraryWindow : Window
{
    private readonly DiagMonStore _store; private List<DiagMonIndexEntry> _all = new();
    private DiagMonIndexEntry? Selected => SessionsGrid.SelectedItem as DiagMonIndexEntry;
    public DiagMonLibraryWindow(DiagMonStore store)
    {
        InitializeComponent(); _store = store;
        ValidationCombo.ItemsSource = Enum.GetValues<DiagMonValidation>(); TypeCombo.ItemsSource = Enum.GetValues<DiagMonSessionType>();
        ValidationFilter.ItemsSource = new[] { "All", "Unreviewed", "Valid", "Invalid" }; ValidationFilter.SelectedIndex = 0; Reload();
    }
    private void Reload() { string? selected = Selected?.Id; _all = _store.LoadIndex().Sessions; ApplyFilter(); if (selected != null) SessionsGrid.SelectedItem = _all.FirstOrDefault(x => x.Id == selected); }
    private void ApplyFilter()
    {
        if (SessionsGrid == null) return; string text = FilterBox?.Text?.Trim() ?? ""; string validation = ValidationFilter?.SelectedItem?.ToString() ?? "All";
        SessionsGrid.ItemsSource = _all.Where(x => (text.Length == 0 || x.Application.Contains(text, StringComparison.OrdinalIgnoreCase) || x.UserLabel.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Tags.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Notes.Contains(text, StringComparison.OrdinalIgnoreCase)) && (validation == "All" || x.Validation.ToString() == validation)).ToList();
    }
    private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();
    private void SessionsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Selected == null) return; string? dir = _store.ResolveDirectory(Selected); if (dir == null) return; var a = _store.LoadAnnotations(dir); ValidationCombo.SelectedItem = a.Validation; TypeCombo.SelectedItem = a.SessionType; NotesBox.Text = a.Notes; TagsBox.Text = string.Join(", ", a.Tags);
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (Selected == null) return; _store.SaveAnnotations(Selected, new DiagMonAnnotations { Validation = (DiagMonValidation)ValidationCombo.SelectedItem, SessionType = (DiagMonSessionType)TypeCombo.SelectedItem, Notes = NotesBox.Text.Trim(), Tags = TagsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList() }); Reload();
    }
    private void Open_Click(object sender, RoutedEventArgs e) { if (Selected is { } s && _store.ResolveDirectory(s) is { } d) new DiagMonSessionWindow(d, _store) { Owner = this }.Show(); }
    private void Compare_Click(object sender, RoutedEventArgs e)
    {
        List<DiagMonIndexEntry> selected = SessionsGrid.SelectedItems.Cast<DiagMonIndexEntry>().OrderBy(x => x.StartUtc).ToList();
        if (selected.Count < 2) { MessageBox.Show(this, "Select at least two sessions using Ctrl or Shift.", "Compare sessions", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        new DiagMonComparisonWindow(selected) { Owner = this }.Show();
    }
    private void Folder_Click(object sender, RoutedEventArgs e) { if (Selected is { } s && _store.ResolveDirectory(s) is { } d) Process.Start(new ProcessStartInfo { FileName = d, UseShellExecute = true }); }
    private void Export_Click(object sender, RoutedEventArgs e) { if (Selected is not { } s) return; try { string zip = _store.ExportPackage(s); Process.Start(new ProcessStartInfo { FileName = zip, UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed"); } }
    private void Delete_Click(object sender, RoutedEventArgs e) { if (Selected is not { } s) return; if (MessageBox.Show(this, $"Permanently delete the session '{s.UserLabel}' and all its raw evidence?", "Delete DiagMon(ster) session", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes) { _store.Delete(s); Reload(); } }
}
