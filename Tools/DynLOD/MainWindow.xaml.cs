using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DynLOD;

public partial class MainWindow : Window
{
    internal readonly RangeRow[] Rows = [new("World — Main"), new("Cars — Main"), new("World — Mirrors"), new("Cars — Mirrors")];
    internal readonly ComboBox WorldPresets, CarsPresets;
    internal static readonly string[] PresetNames = ["Maximum", "Medium", "Minimum", "Decrease", "Increase", "Off", "Custom"];
    // iRacing's UI and current iRSidekick map World to LODPctDyno* and Cars to LODPct*.
    // Values are Min/Max for main, then Min/Max for mirrors.
    static readonly Dictionary<string, int[]> WorldPresetValues = new()
    {
        ["Maximum"]=[25,400,25,500], ["Medium"]=[50,300,50,300], ["Minimum"]=[75,200,75,200],
        ["Decrease"]=[100,400,100,500], ["Increase"]=[25,100,25,100], ["Off"]=[100,100,100,100]
    };
    static readonly Dictionary<string, int[]> CarsPresetValues = new()
    {
        ["Maximum"]=[25,400,25,400], ["Medium"]=[50,300,50,300], ["Minimum"]=[75,200,75,200],
        ["Decrease"]=[100,400,100,500], ["Increase"]=[25,100,25,100], ["Off"]=[100,100,100,100]
    };
    static readonly (string Min, string Max)[] RowKeys =
    [
        ("LODPctDynoMin","LODPctDynoMax"), ("LODPctMin","LODPctMax"),
        ("LODPctDynoMirrorsMin","LODPctDynoMirrorsMax"), ("LODPctMirrorsMin","LODPctMirrorsMax")
    ];
    readonly string preference = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DynLOD", "selected-ini.txt");
    bool loading = true, loaded, applyingPreset;
    readonly bool savePreference;
    internal string SelectedFile => (Files.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
    internal string SelectedSection => Source.SelectedIndex == 1 ? IniFile.Replay : IniFile.Main;

    public MainWindow(string? initialPath = null, bool savePreference = true)
    {
        InitializeComponent(); this.savePreference = savePreference;
        WorldPresets = WorldPreset; CarsPresets = CarsPreset;
        foreach (string name in PresetNames) { WorldPreset.Items.Add(name); CarsPreset.Items.Add(name); }
        string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        Title = "IRACING DYNLOD " + version;
        TitleLabel.ToolTip = "iRacing DynLOD v" + version;
        foreach (var row in Rows) { Ranges.Children.Add(row); row.Edited += UpdateValidation; }
        Source.SelectedIndex = 0;
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "iRacing");
        if (Directory.Exists(directory))
            foreach (string path in Directory.GetFiles(directory, "renderer*.ini").OrderBy(p => p.EndsWith("OpenXR.ini", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(p => p)) AddFile(path);
        if (initialPath == null && savePreference)
            try { if (File.Exists(preference)) initialPath = File.ReadAllText(preference).Trim(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        if (!string.IsNullOrEmpty(initialPath)) Files.SelectedItem = AddFile(initialPath);
        else if (Files.Items.Count > 0) Files.SelectedIndex = 0;
        loading = false; Reload();
    }
    ComboBoxItem AddFile(string path)
    {
        path = Path.GetFullPath(path);
        foreach (ComboBoxItem item in Files.Items) if (string.Equals(item.Tag as string,path,StringComparison.OrdinalIgnoreCase)) return item;
        string label = Path.GetFileName(path);
        if (label.StartsWith("rendererDX11",StringComparison.OrdinalIgnoreCase)) label = label.Length > 16 ? label[12..] : "DX11.ini";
        var entry = new ComboBoxItem { Content = label, Tag = path, ToolTip = path };
        Files.Items.Add(entry); return entry;
    }
    internal void Reload()
    {
        loading = true; loaded = false;
        try
        {
            var values = new IniFile(SelectedFile).Read(SelectedSection);
            for (int i = 0; i < Rows.Length; i++) Rows[i].Load(values[RowKeys[i].Min],values[RowKeys[i].Max]);
            Fps.Text = values[IniFile.Fps]; loaded = true;
            DetectPresets();
            Files.ToolTip = SelectedFile;
            ReadLabel.Text = "[" + SelectedSection + "]";
            Status.Text = "";
            Status.ToolTip = null;
            if (savePreference)
                try { Directory.CreateDirectory(Path.GetDirectoryName(preference)!); File.WriteAllText(preference,SelectedFile); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            foreach (var row in Rows) row.Load("", "");
            Fps.Text = ""; ReadLabel.Text = "Not loaded"; Status.Text = ex.Message;
        }
        finally { loading = false; UpdateValidation(false); }
    }
    internal Dictionary<string,string> Values()
    {
        var values = new Dictionary<string,string>();
        for (int i=0; i<Rows.Length; i++) { values[RowKeys[i].Min]=Rows[i].MinBox.Text; values[RowKeys[i].Max]=Rows[i].MaxBox.Text; }
        values[IniFile.Fps] = Fps.Text; return values;
    }
    void UpdateValidation() => UpdateValidation(true);
    void UpdateValidation(bool edited)
    {
        if (loading) return;
        SyncCrossRangeLimits();
        bool valid = loaded;
        string? problem = null;
        if (loaded) try { IniFile.Validate(Values()); } catch (InvalidDataException ex) { valid = false; problem = ex.Message; }
        ApplyMain.IsEnabled = ApplyReplay.IsEnabled = valid;
        if (problem != null) Status.Text = problem;
        else if (edited && loaded) Status.Text = "";
        if (loaded && edited && !applyingPreset) DetectPresets();
        Status.Foreground = RangeRow.Brush(valid ? "#C8C8C8" : "#FF7279");
    }
    void SyncCrossRangeLimits()
    {
        foreach (var (world,cars) in new[] { (Rows[0],Rows[1]), (Rows[2],Rows[3]) })
        {
            world.Track.MaximumFloor = cars.Track.Minimum;
            cars.Track.MinimumCeiling = world.Track.Maximum;
        }
    }
    internal void Apply(string section)
    {
        try
        {
            string backup = IniFile.Apply(SelectedFile,section,Values());
            Status.Text = "Applied to " + (section == IniFile.Main ? "Main" : "Replay") + " · Backup saved beside the INI.";
            Status.ToolTip = backup; Status.Foreground = RangeRow.Brush("#C8C8C8");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        { Status.Text = ex.Message; Status.Foreground = RangeRow.Brush("#FF7279"); }
    }
    void Edited(object sender, TextChangedEventArgs e) => UpdateValidation();
    void DetectPresets()
    {
        applyingPreset = true;
        WorldPreset.SelectedItem = MatchPreset(WorldPresetValues,0,2);
        CarsPreset.SelectedItem = MatchPreset(CarsPresetValues,1,3);
        applyingPreset = false;
    }
    string MatchPreset(Dictionary<string,int[]> presets, int main, int mirrors)
    {
        foreach (var preset in presets)
        {
            int[] v=preset.Value;
            if (Rows[main].MinBox.Text==v[0].ToString() && Rows[main].MaxBox.Text==v[1].ToString() &&
                Rows[mirrors].MinBox.Text==v[2].ToString() && Rows[mirrors].MaxBox.Text==v[3].ToString()) return preset.Key;
        }
        return "Custom";
    }
    void ApplyPreset(ComboBox selector, Dictionary<string,int[]> presets, int main, int mirrors)
    {
        if (loading || applyingPreset || selector.SelectedItem is not string name || !presets.TryGetValue(name,out int[]? v)) return;
        applyingPreset=true;
        Rows[main].Load(v[0].ToString(),v[1].ToString()); Rows[mirrors].Load(v[2].ToString(),v[3].ToString());
        applyingPreset=false; UpdateValidation(true);
    }
    void WorldPreset_Changed(object sender, SelectionChangedEventArgs e) => ApplyPreset(WorldPreset,WorldPresetValues,0,2);
    void CarsPreset_Changed(object sender, SelectionChangedEventArgs e) => ApplyPreset(CarsPreset,CarsPresetValues,1,3);
    void File_Changed(object sender, SelectionChangedEventArgs e) { if (!loading) Reload(); }
    void Source_Changed(object sender, SelectionChangedEventArgs e) { if (!loading) Reload(); }
    void Refresh_Click(object sender, RoutedEventArgs e) => Reload();
    void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "iRacing INI (*.ini)|*.ini", CheckFileExists = true, Title = "Choose iRacing renderer INI" };
        if (File.Exists(SelectedFile)) dialog.InitialDirectory = Path.GetDirectoryName(SelectedFile);
        if (dialog.ShowDialog(this) == true) Files.SelectedItem = AddFile(dialog.FileName);
    }
    void ApplyMain_Click(object sender, RoutedEventArgs e) => Apply(IniFile.Main);
    void ApplyReplay_Click(object sender, RoutedEventArgs e) => Apply(IniFile.Replay);
    void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object sender, RoutedEventArgs e) => Close();
}

