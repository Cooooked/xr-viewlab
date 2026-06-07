using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XRViewLab.UI;

public partial class MainWindow : Window
{
    private const string CurrentVersion = "v3.1.2-nightly.1";
    private const string ReleasesApiUrl = "https://api.github.com/repos/Cooooked/xr-viewlab/releases";
    private const string ManifestFileName = "XR_APILAYER_cooooked_xrviewlab.json";
    private const string AppRegistryRoot = @"Software\cooooked\xr-viewlab\Apps";
    private const string OpenXrRegistryRoot = @"Software\Khronos\OpenXR\1\ApiLayers\Implicit";
    private readonly ObservableCollection<AppProfile> _apps = new();
    private readonly ObservableCollection<AppProfile> _enabledProfiles = new();
    private bool _loading = true;
    private bool _syncingControls;

    public MainWindow()
    {
        InitializeComponent();
        LoadWindowSize();
        AppsList.ItemsSource = _apps;
        ProfilesList.ItemsSource = _enabledProfiles;
        LoadSettings();
        LoadAppProfiles();
        VersionText.Text = CurrentVersion;
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

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StatusText.Text = "Checking for updates...";
            using HttpClient client = CreateGitHubClient();
            UpdateRelease? latest = await GetLatestReleaseAsync(client);
            if (latest == null)
            {
                StatusText.Text = "No update release found.";
                MessageBox.Show(this, "No XR ViewLab update release was found.", "XR ViewLab Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!IsNewerRelease(latest.TagName, CurrentVersion))
            {
                StatusText.Text = $"XR ViewLab is up to date ({CurrentVersion}).";
                MessageBox.Show(this, $"You are up to date.\n\nInstalled: {CurrentVersion}\nLatest: {latest.TagName}", "XR ViewLab Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult choice = MessageBox.Show(
                this,
                $"Update detected: {latest.TagName}\n\nInstalled: {CurrentVersion}\n\nDownload and install now?\n\nXR ViewLab will close after the installer starts.",
                "XR ViewLab Update",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (choice != MessageBoxResult.Yes)
            {
                StatusText.Text = $"Update available: {latest.TagName}.";
                return;
            }

            string installerPath = await DownloadUpdateAsync(client, latest);
            StatusText.Text = $"Launching installer for {latest.TagName}...";
            Process.Start(new ProcessStartInfo
            {
                FileName = "msiexec.exe",
                Arguments = $"/i \"{installerPath}\"",
                UseShellExecute = true
            });
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Update check failed: {ex.Message}";
            MessageBox.Show(this, $"Update check failed.\n\n{ex.Message}", "XR ViewLab Updates", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static HttpClient CreateGitHubClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("XR-ViewLab");
        return client;
    }

    private static bool CurrentBuildAllowsPrereleases() =>
        CurrentVersion.Contains("nightly", StringComparison.OrdinalIgnoreCase) ||
        CurrentVersion.Contains("beta", StringComparison.OrdinalIgnoreCase) ||
        CurrentVersion.Contains("preview", StringComparison.OrdinalIgnoreCase);

    private static async Task<UpdateRelease?> GetLatestReleaseAsync(HttpClient client)
    {
        string json = await client.GetStringAsync(ReleasesApiUrl);
        using JsonDocument document = JsonDocument.Parse(json);
        bool allowPrereleases = CurrentBuildAllowsPrereleases();

        foreach (JsonElement release in document.RootElement.EnumerateArray())
        {
            bool draft = release.TryGetProperty("draft", out JsonElement draftElement) && draftElement.GetBoolean();
            bool prerelease = release.TryGetProperty("prerelease", out JsonElement prereleaseElement) && prereleaseElement.GetBoolean();
            if (draft || (!allowPrereleases && prerelease))
            {
                continue;
            }

            string tag = release.TryGetProperty("tag_name", out JsonElement tagElement)
                ? tagElement.GetString() ?? ""
                : "";
            if (string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            string? assetUrl = null;
            string? assetName = null;
            if (release.TryGetProperty("assets", out JsonElement assets))
            {
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out JsonElement nameElement)
                        ? nameElement.GetString() ?? ""
                        : "";
                    if (!name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    assetName = name;
                    assetUrl = asset.TryGetProperty("browser_download_url", out JsonElement urlElement)
                        ? urlElement.GetString()
                        : null;
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(assetUrl) && !string.IsNullOrWhiteSpace(assetName))
            {
                return new UpdateRelease(tag, assetName, assetUrl);
            }
        }

        return null;
    }

    private async Task<string> DownloadUpdateAsync(HttpClient client, UpdateRelease release)
    {
        string updateDirectory = Path.Combine(Path.GetTempPath(), "XR ViewLab Updates");
        Directory.CreateDirectory(updateDirectory);
        string installerPath = Path.Combine(updateDirectory, release.AssetName);
        StatusText.Text = $"Downloading {release.TagName}...";

        using HttpResponseMessage response = await client.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using Stream input = await response.Content.ReadAsStreamAsync();
        await using FileStream output = File.Create(installerPath);
        await input.CopyToAsync(output);
        return installerPath;
    }

    private static bool IsNewerRelease(string candidateTag, string currentTag)
    {
        Version candidate = ParseVersion(candidateTag);
        Version current = ParseVersion(currentTag);
        int comparison = candidate.CompareTo(current);
        if (comparison != 0)
        {
            return comparison > 0;
        }

        return PrereleaseNumber(candidateTag) > PrereleaseNumber(currentTag);
    }

    private static Version ParseVersion(string tag)
    {
        string cleaned = tag.Trim();
        if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[1..];
        }

        int suffixIndex = cleaned.IndexOfAny(new[] { '-', '+' });
        if (suffixIndex >= 0)
        {
            cleaned = cleaned[..suffixIndex];
        }

        return Version.TryParse(cleaned, out Version? version) ? version : new Version(0, 0, 0);
    }

    private static int PrereleaseNumber(string tag)
    {
        int lastDot = tag.LastIndexOf('.');
        return lastDot >= 0 && int.TryParse(tag[(lastDot + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : 0;
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
            ProfilesValueColumn.Width = 150.0;
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

    private static string FormatScale(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

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
        if (box == null)
        {
            value = 0.0;
            return false;
        }

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
        double horizontal = ReadScaleSetting("horizontal_render_width", 1.00);
        bool splitMode = ReadBoolSetting("split_mode", false);
        EnabledCheck.IsChecked = ReadBoolSetting("enabled", true);
        SplitCheck.IsChecked = splitMode;
        FoveatedCenterCheck.IsChecked = ReadBoolSetting("foveated_center_compensation", false);
        TotalBox.Text = FormatScale(total);
        TopBox.Text = FormatScale(ReadScaleSetting("top_tangent", total * 0.5));
        BottomBox.Text = FormatScale(ReadScaleSetting("bottom_tangent", total * 0.5));
        HorizontalBox.Text = FormatScale(horizontal);
        SyncSlidersFromText();
        _loading = false;
        UpdateModeControls();
        UpdateHints();
        StatusText.Text = $"Config: {ConfigPath}";
    }

    private void UpdateModeControls()
    {
        bool splitMode = SplitCheck.IsChecked == true;
        TotalBox.IsEnabled = !splitMode;
        TotalSlider.IsEnabled = !splitMode;
        TopBox.IsEnabled = splitMode;
        TopSlider.IsEnabled = splitMode;
        BottomBox.IsEnabled = splitMode;
        BottomSlider.IsEnabled = splitMode;
    }

    private void UpdateHints()
    {
        if (TotalHint == null || TopHint == null || BottomHint == null || HorizontalHint == null || CombinedHint == null)
        {
            return;
        }

        bool totalOk = TryReadTextBox(TotalBox, out double total);
        bool topOk = TryReadTextBox(TopBox, out double top);
        bool bottomOk = TryReadTextBox(BottomBox, out double bottom);
        bool horizontalOk = TryReadTextBox(HorizontalBox, out double horizontal);
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
        HorizontalHint.Text = horizontalOk ? $"{FormatPercent(horizontal)} horizontal render width" : "Enter horizontal width";
        CombinedHint.Text = splitMode && topOk && bottomOk
            ? $"Combined: {FormatPercent(top + bottom)} total render height"
            : totalOk ? $"Combined: {FormatPercent(total)} total render height" : "Combined: enter valid values";

        SyncSlidersFromText();
    }

    private static void SetSliderValue(Slider slider, double value)
    {
        if (slider == null)
        {
            return;
        }

        value = Math.Clamp(value, slider.Minimum, slider.Maximum);
        if (Math.Abs(slider.Value - value) > 0.0001)
        {
            slider.Value = value;
        }
    }

    private void SyncSlidersFromText()
    {
        if (_syncingControls)
        {
            return;
        }

        _syncingControls = true;
        if (TryReadTextBox(TotalBox, out double total))
        {
            SetSliderValue(TotalSlider, total);
        }
        if (TryReadTextBox(TopBox, out double top))
        {
            SetSliderValue(TopSlider, top);
        }
        if (TryReadTextBox(BottomBox, out double bottom))
        {
            SetSliderValue(BottomSlider, bottom);
        }
        if (TryReadTextBox(HorizontalBox, out double horizontal))
        {
            SetSliderValue(HorizontalSlider, horizontal);
        }
        _syncingControls = false;
    }

    private void SyncTextFromSlider(TextBox box, Slider slider)
    {
        if (_syncingControls)
        {
            return;
        }

        _syncingControls = true;
        box.Text = FormatScale(slider.Value);
        _syncingControls = false;
        UpdateHints();
    }

    private void RenderValue_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_loading && !_syncingControls)
        {
            UpdateHints();
        }
    }

    private void RenderSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading || _syncingControls || TotalBox == null || TopBox == null || BottomBox == null || HorizontalBox == null)
        {
            return;
        }

        if (sender == TotalSlider)
        {
            SyncTextFromSlider(TotalBox, TotalSlider);
        }
        else if (sender == TopSlider)
        {
            SyncTextFromSlider(TopBox, TopSlider);
        }
        else if (sender == BottomSlider)
        {
            SyncTextFromSlider(BottomBox, BottomSlider);
        }
        else if (sender == HorizontalSlider)
        {
            SyncTextFromSlider(HorizontalBox, HorizontalSlider);
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
        if (!TryReadTextBox(TotalBox, out double total) ||
            !TryReadTextBox(TopBox, out double top) ||
            !TryReadTextBox(BottomBox, out double bottom) ||
            !TryReadTextBox(HorizontalBox, out double horizontal))
        {
            MessageBox.Show(this, "Enter render values from 0.00 to 1.00. Total and horizontal must be at least 0.01.", "XR ViewLab", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        horizontal = Math.Clamp(horizontal, 0.01, 1.0);

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
        WritePrivateProfileString("Settings", "horizontal_render_width", FormatScale(horizontal), ConfigPath);
        WriteRegistryEnabled(enabled);

        _loading = true;
        TotalBox.Text = FormatScale(total);
        TopBox.Text = FormatScale(top);
        BottomBox.Text = FormatScale(bottom);
        HorizontalBox.Text = FormatScale(horizontal);
        SyncSlidersFromText();
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
        double globalHorizontal = ReadScaleSetting("horizontal_render_width", 1.00);
        string module = Convert.ToString(appKey.GetValue("module", "")) ?? "";
        string displayName = Convert.ToString(appKey.GetValue("display_name", keyName)) ?? keyName;
        string moduleName = string.IsNullOrWhiteSpace(module) ? keyName : Path.GetFileName(module);
        bool profileEnabled = Convert.ToInt32(appKey.GetValue("profile_enabled", 0), CultureInfo.InvariantCulture) != 0;
        bool appEnabled = Convert.ToInt32(appKey.GetValue("app_enabled", 1), CultureInfo.InvariantCulture) != 0;
        double top = FromMillis(appKey.GetValue("top_tangent"), globalTop);
        double bottom = FromMillis(appKey.GetValue("bottom_tangent"), globalBottom);
        double horizontal = FromMillis(appKey.GetValue("horizontal_render_width"), globalHorizontal);

        return new AppProfile
        {
            Key = keyName,
            DisplayName = displayName,
            Display = $"{displayName} ({moduleName})",
            AppEnabled = appEnabled,
            ProfileEnabled = profileEnabled,
            Top = top,
            Bottom = bottom,
            Horizontal = horizontal
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

        var dialog = new ProfileWindow(profile.DisplayName, profile.Top, profile.Bottom, profile.Horizontal) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        if (dialog.UseGlobal)
        {
            WriteAppCustomProfile(profile, profile.Top, profile.Bottom, profile.Horizontal, false);
            profile.ProfileEnabled = false;
        }
        else
        {
            WriteAppCustomProfile(profile, dialog.TopValue, dialog.BottomValue, dialog.HorizontalValue, true);
            profile.Top = dialog.TopValue;
            profile.Bottom = dialog.BottomValue;
            profile.Horizontal = dialog.HorizontalValue;
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

    private static void WriteAppCustomProfile(AppProfile profile, double top, double bottom, double horizontal, bool profileEnabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey($@"{AppRegistryRoot}\{profile.Key}", true) ?? throw new InvalidOperationException("Could not write app profile.");
        key.SetValue("profile_enabled", profileEnabled ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue("split_mode", 1, RegistryValueKind.DWord);
        key.SetValue("total_render_height", ToMillis(Math.Clamp(top + bottom, 0.01, 1.0)), RegistryValueKind.DWord);
        key.DeleteValue("total_share", false);
        key.DeleteValue("vertical_tangent", false);
        key.SetValue("top_tangent", ToMillis(top), RegistryValueKind.DWord);
        key.SetValue("bottom_tangent", ToMillis(bottom), RegistryValueKind.DWord);
        key.SetValue("horizontal_render_width", ToMillis(horizontal), RegistryValueKind.DWord);
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
        key.DeleteValue("horizontal_render_width", false);
    }
}

public sealed class AppProfile : INotifyPropertyChanged
{
    private bool _appEnabled = true;
    private bool _profileEnabled;
    private double _top = 0.20;
    private double _bottom = 0.20;
    private double _horizontal = 1.00;

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

    public double Horizontal
    {
        get => _horizontal;
        set
        {
            _horizontal = value;
            RefreshSummary();
        }
    }

    public string Summary => ProfileEnabled ? $"{Top:0.00};{Bottom:0.00};{Horizontal:0.00}" : "Global";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshSummary()
    {
        OnPropertyChanged(nameof(ProfileEnabled));
        OnPropertyChanged(nameof(Summary));
    }

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record UpdateRelease(string TagName, string AssetName, string AssetUrl);
