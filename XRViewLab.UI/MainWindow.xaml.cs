using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XRViewLab.UI;

public partial class MainWindow : Window
{
    private const string ManifestFileName = "XR_APILAYER_cooooked_xrviewlab.json";
    private const string AppRegistryRoot = @"Software\cooooked\xr-viewlab\Apps";
    private const string OpenXrRegistryRoot = @"Software\Khronos\OpenXR\1\ApiLayers\Implicit";
    private readonly ObservableCollection<AppProfile> _apps = new();
    private readonly ObservableCollection<AppProfile> _enabledProfiles = new();
    private bool _loading;

    public MainWindow()
    {
        InitializeComponent();
        LoadWindowSize();
        AppsList.ItemsSource = _apps;
        ProfilesList.ItemsSource = _enabledProfiles;
        LoadSettings();
        LoadAppProfiles();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        SaveWindowSize();
        base.OnClosing(e);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void Support_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://buymeacoffee.com/xqbsit");
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/Cooooked/xr-viewlab/releases/latest");
    }

    private void List_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double usableWidth = Math.Max(240.0, e.NewSize.Width - 4.0);
        if (sender == AppsList)
        {
            AppsCheckColumn.Width = 54.0;
            AppsNameColumn.Width = Math.Max(220.0, usableWidth - AppsCheckColumn.Width);
        }
        else if (sender == ProfilesList)
        {
            ProfilesValueColumn.Width = 120.0;
            ProfilesNameColumn.Width = Math.Max(180.0, usableWidth - ProfilesValueColumn.Width);
        }
    }

    private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "xr-viewlab.ini");
    private static string ManifestPath => Path.Combine(AppContext.BaseDirectory, ManifestFileName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder value, uint size, string filePath);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool WritePrivateProfileString(string section, string key, string? value, string filePath);

    private static string ReadSetting(string key, string fallback)
    {
        var buffer = new StringBuilder(256);
        GetPrivateProfileString("Settings", key, fallback, buffer, (uint)buffer.Capacity, ConfigPath);
        return buffer.ToString();
    }

    private static bool ReadBoolSetting(string key, bool fallback)
    {
        string value = ReadSetting(key, fallback ? "1" : "0");
        return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static double ReadScaleSetting(string key, double fallback)
    {
        return double.TryParse(ReadSetting(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? Math.Clamp(value, 0.0, 1.0)
            : fallback;
    }

    private static string FormatScale(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string FormatPercent(double value) => $"{Math.Clamp(value, 0.0, 1.0) * 100.0:0}%";

    private static uint ToMillis(double value) => (uint)Math.Round(Math.Clamp(value, 0.0, 1.0) * 1000.0);

    private static double ReadWindowSizeSetting(string key, double fallback)
    {
        return double.TryParse(ReadSetting(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? Math.Clamp(value, 1.0, 10000.0)
            : fallback;
    }

    private void LoadWindowSize()
    {
        double width = ReadWindowSizeSetting("window_width", MinWidth);
        double height = ReadWindowSizeSetting("window_height", MinHeight);
        Width = Math.Max(MinWidth, width);
        Height = Math.Max(MinHeight, height);
    }

    private void SaveWindowSize()
    {
        Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        double width = Math.Max(MinWidth, bounds.Width);
        double height = Math.Max(MinHeight, bounds.Height);
        WritePrivateProfileString("Settings", "window_width", width.ToString("0", CultureInfo.InvariantCulture), ConfigPath);
        WritePrivateProfileString("Settings", "window_height", height.ToString("0", CultureInfo.InvariantCulture), ConfigPath);
    }

    private static double FromMillis(object? value, double fallback)
    {
        if (value is int intValue && intValue >= 0 && intValue <= 1000)
        {
            return intValue / 1000.0;
        }
        if (value is uint uintValue && uintValue <= 1000)
        {
            return uintValue / 1000.0;
        }

        return fallback;
    }

    private static bool TryReadTextBox(TextBox box, out double value)
    {
        if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            value = 0.0;
            return false;
        }

        value = Math.Clamp(value, 0.0, 1.0);
        return true;
    }

    private void LoadSettings()
    {
        _loading = true;
        double total = ReadScaleSetting("total_render_height", ReadScaleSetting("total_share", ReadScaleSetting("vertical_tangent", 0.40)));
        bool splitMode = ReadBoolSetting("split_mode", false);
        EnabledCheck.IsChecked = ReadBoolSetting("enabled", true);
        SplitCheck.IsChecked = splitMode;
        FoveatedCenterCheck.IsChecked = ReadBoolSetting("foveated_center_compensation", false);
        TotalBox.Text = FormatScale(total);
        TopBox.Text = FormatScale(ReadScaleSetting("top_tangent", total * 0.5));
        BottomBox.Text = FormatScale(ReadScaleSetting("bottom_tangent", total * 0.5));
        _loading = false;
        UpdateModeControls();
        UpdateHints();
        StatusText.Text = $"Config: {ConfigPath}";
    }

    private void UpdateModeControls()
    {
        bool splitMode = SplitCheck.IsChecked == true;
        TotalBox.IsEnabled = !splitMode;
        TopBox.IsEnabled = splitMode;
        BottomBox.IsEnabled = splitMode;
    }

    private void UpdateHints()
    {
        bool totalOk = TryReadTextBox(TotalBox, out double total);
        bool topOk = TryReadTextBox(TopBox, out double top);
        bool bottomOk = TryReadTextBox(BottomBox, out double bottom);
        bool splitMode = SplitCheck.IsChecked == true;

        if (!splitMode && totalOk)
        {
            double half = total * 0.5;
            if (!_loading)
            {
                TopBox.TextChanged -= RenderValue_Changed;
                BottomBox.TextChanged -= RenderValue_Changed;
                TopBox.Text = FormatScale(half);
                BottomBox.Text = FormatScale(half);
                TopBox.TextChanged += RenderValue_Changed;
                BottomBox.TextChanged += RenderValue_Changed;
                top = half;
                bottom = half;
                topOk = bottomOk = true;
            }
        }

        TotalHint.Text = totalOk ? $"{FormatPercent(total)} total render height" : "Enter total render height";
        TopHint.Text = topOk ? $"{FormatPercent(top)} top" : "Enter top value";
        BottomHint.Text = bottomOk ? $"{FormatPercent(bottom)} bottom" : "Enter bottom value";
        CombinedHint.Text = splitMode && topOk && bottomOk
            ? $"Combined: {FormatPercent(top + bottom)} total render height"
            : totalOk ? $"Combined: {FormatPercent(total)} total render height" : "Combined: enter valid values";
    }

    private void RenderValue_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_loading)
        {
            UpdateHints();
        }
    }

    private void SplitCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        UpdateModeControls();
        UpdateHints();
    }

    private void EnabledCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            SaveGlobalSettings();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveGlobalSettings();

    private void SaveGlobalSettings()
    {
        bool splitMode = SplitCheck.IsChecked == true;
        if (!TryReadTextBox(TotalBox, out double total) || !TryReadTextBox(TopBox, out double top) || !TryReadTextBox(BottomBox, out double bottom))
        {
            MessageBox.Show(this, "Enter render height values from 0.000 to 1.000. Total must be at least 0.010.", "XR ViewLab", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (splitMode)
        {
            total = Math.Clamp(top + bottom, 0.01, 1.0);
        }
        else
        {
            total = Math.Clamp(total, 0.01, 1.0);
            top = total * 0.5;
            bottom = total * 0.5;
        }

        bool enabled = EnabledCheck.IsChecked == true;
        Directory.CreateDirectory(AppContext.BaseDirectory);
        WritePrivateProfileString("Settings", "enabled", enabled ? "1" : "0", ConfigPath);
        WritePrivateProfileString("Settings", "split_mode", splitMode ? "1" : "0", ConfigPath);
        WritePrivateProfileString("Settings", "foveated_center_compensation", FoveatedCenterCheck.IsChecked == true ? "1" : "0", ConfigPath);
        WritePrivateProfileString("Settings", "total_render_height", FormatScale(total), ConfigPath);
        WritePrivateProfileString("Settings", "total_share", null, ConfigPath);
        WritePrivateProfileString("Settings", "vertical_tangent", null, ConfigPath);
        WritePrivateProfileString("Settings", "top_tangent", FormatScale(top), ConfigPath);
        WritePrivateProfileString("Settings", "bottom_tangent", FormatScale(bottom), ConfigPath);
        WriteRegistryEnabled(enabled);

        _loading = true;
        TotalBox.Text = FormatScale(total);
        TopBox.Text = FormatScale(top);
        BottomBox.Text = FormatScale(bottom);
        _loading = false;
        UpdateHints();
        StatusText.Text = enabled ? "Saved. Restart the OpenXR game." : "Layer disabled.";
    }

    private static void WriteRegistryEnabled(bool enabled)
    {
        using RegistryKey key = Registry.LocalMachine.CreateSubKey(OpenXrRegistryRoot, true) ?? throw new InvalidOperationException("Could not open OpenXR registry key.");
        key.SetValue(ManifestPath, enabled ? 0 : 1, RegistryValueKind.DWord);
    }

    private void LoadAppProfiles()
    {
        _loading = true;
        _apps.Clear();
        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(AppRegistryRoot);
        if (root != null)
        {
            foreach (string keyName in root.GetSubKeyNames())
            {
                using RegistryKey? appKey = root.OpenSubKey(keyName);
                if (appKey == null)
                {
                    continue;
                }

                var profile = ReadAppProfile(keyName, appKey);
                _apps.Add(profile);
            }
        }

        _loading = false;
        RefreshEnabledProfiles();
    }

    private AppProfile ReadAppProfile(string keyName, RegistryKey appKey)
    {
        double globalTotal = ReadScaleSetting("total_render_height", ReadScaleSetting("total_share", ReadScaleSetting("vertical_tangent", 0.40)));
        double globalTop = ReadScaleSetting("top_tangent", globalTotal * 0.5);
        double globalBottom = ReadScaleSetting("bottom_tangent", globalTotal * 0.5);
        string module = Convert.ToString(appKey.GetValue("module", "")) ?? "";
        string displayName = Convert.ToString(appKey.GetValue("display_name", keyName)) ?? keyName;
        string moduleName = string.IsNullOrWhiteSpace(module) ? keyName : Path.GetFileName(module);
        bool profileEnabled = Convert.ToInt32(appKey.GetValue("profile_enabled", 0), CultureInfo.InvariantCulture) != 0;
        bool appEnabled = Convert.ToInt32(appKey.GetValue("app_enabled", 1), CultureInfo.InvariantCulture) != 0;
        double top = FromMillis(appKey.GetValue("top_tangent"), globalTop);
        double bottom = FromMillis(appKey.GetValue("bottom_tangent"), globalBottom);

        return new AppProfile
        {
            Key = keyName,
            DisplayName = displayName,
            Display = $"{displayName} ({moduleName})",
            AppEnabled = appEnabled,
            ProfileEnabled = profileEnabled,
            Top = top,
            Bottom = bottom
        };
    }

    private void RefreshEnabledProfiles()
    {
        _enabledProfiles.Clear();
        foreach (AppProfile profile in _apps.Where(app => app.AppEnabled))
        {
            profile.RefreshSummary();
            _enabledProfiles.Add(profile);
        }
    }

    private void AppEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not CheckBox box || box.DataContext is not AppProfile profile)
        {
            return;
        }

        using RegistryKey key = Registry.CurrentUser.CreateSubKey($@"{AppRegistryRoot}\{profile.Key}", true) ?? throw new InvalidOperationException("Could not write app profile.");
        key.SetValue("app_enabled", profile.AppEnabled ? 1 : 0, RegistryValueKind.DWord);
        RefreshEnabledProfiles();
        StatusText.Text = profile.AppEnabled ? $"Layer enabled for {profile.DisplayName}. Restart the OpenXR game." : $"Layer disabled for {profile.DisplayName}. Restart the OpenXR game.";
    }

    private void ReloadApps_Click(object sender, RoutedEventArgs e)
    {
        LoadAppProfiles();
        StatusText.Text = "App list reloaded.";
    }

    private void ProfilesList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProfilesList.SelectedItem is not AppProfile profile)
        {
            return;
        }

        var dialog = new ProfileWindow(profile.DisplayName, profile.Top, profile.Bottom) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.UseGlobal)
        {
            WriteAppCustomProfile(profile, profile.Top, profile.Bottom, false);
            profile.ProfileEnabled = false;
        }
        else
        {
            WriteAppCustomProfile(profile, dialog.TopValue, dialog.BottomValue, true);
            profile.Top = dialog.TopValue;
            profile.Bottom = dialog.BottomValue;
            profile.ProfileEnabled = true;
        }

        profile.RefreshSummary();
        RefreshEnabledProfiles();
        StatusText.Text = "Saved app profile. Restart the OpenXR game.";
    }

    private void ResetApp_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is not AppProfile profile)
        {
            StatusText.Text = "Select an app profile to reset.";
            return;
        }

        ResetAppCustomProfile(profile);
        profile.ProfileEnabled = false;
        RefreshEnabledProfiles();
        StatusText.Text = $"Reset {profile.DisplayName} to global values. Restart the OpenXR game.";
    }

    private static void WriteAppCustomProfile(AppProfile profile, double top, double bottom, bool profileEnabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey($@"{AppRegistryRoot}\{profile.Key}", true) ?? throw new InvalidOperationException("Could not write app profile.");
        key.SetValue("profile_enabled", profileEnabled ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("split_mode", 1, RegistryValueKind.DWord);
        key.SetValue("total_render_height", ToMillis(Math.Clamp(top + bottom, 0.01, 1.0)), RegistryValueKind.DWord);
        key.DeleteValue("total_share", false);
        key.DeleteValue("vertical_tangent", false);
        key.SetValue("top_tangent", ToMillis(top), RegistryValueKind.DWord);
        key.SetValue("bottom_tangent", ToMillis(bottom), RegistryValueKind.DWord);
    }

    private static void ResetAppCustomProfile(AppProfile profile)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey($@"{AppRegistryRoot}\{profile.Key}", true) ?? throw new InvalidOperationException("Could not write app profile.");
        key.SetValue("profile_enabled", 0, RegistryValueKind.DWord);
        key.DeleteValue("split_mode", false);
        key.DeleteValue("total_render_height", false);
        key.DeleteValue("total_share", false);
        key.DeleteValue("vertical_tangent", false);
        key.DeleteValue("top_tangent", false);
        key.DeleteValue("bottom_tangent", false);
    }
}

public sealed class AppProfile : INotifyPropertyChanged
{
    private bool _appEnabled = true;
    private bool _profileEnabled;
    private double _top = 0.20;
    private double _bottom = 0.20;

    public string Key { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Display { get; init; } = "";

    public bool AppEnabled
    {
        get => _appEnabled;
        set
        {
            if (_appEnabled != value)
            {
                _appEnabled = value;
                OnPropertyChanged(nameof(AppEnabled));
            }
        }
    }

    public bool ProfileEnabled
    {
        get => _profileEnabled;
        set
        {
            if (_profileEnabled != value)
            {
                _profileEnabled = value;
                RefreshSummary();
            }
        }
    }

    public double Top
    {
        get => _top;
        set
        {
            _top = value;
            RefreshSummary();
        }
    }

    public double Bottom
    {
        get => _bottom;
        set
        {
            _bottom = value;
            RefreshSummary();
        }
    }

    public string Summary => ProfileEnabled ? $"{Top:0.000};{Bottom:0.000}" : "Global";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshSummary()
    {
        OnPropertyChanged(nameof(ProfileEnabled));
        OnPropertyChanged(nameof(Summary));
    }

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
