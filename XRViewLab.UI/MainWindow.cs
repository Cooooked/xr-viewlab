using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace XRViewLab.UI;

public partial class MainWindow : Window
{
	private const string ReleasesApiUrl = "https://api.github.com/repos/Cooooked/xr-viewlab/releases";

	private const string ManifestFileName = "XR_APILAYER_cooooked_xrviewlab.json";

	private const string AppRegistryRoot = "Software\\cooooked\\xr-viewlab\\Apps";

	private const string OpenXrRegistryRoot = "Software\\Khronos\\OpenXR\\1\\ApiLayers\\Implicit";

	private const string SupportFooterText = "Support the broke loser who made this app";

	private readonly ObservableCollection<AppProfile> _apps = new ObservableCollection<AppProfile>();

	private readonly ObservableCollection<AppProfile> _enabledProfiles = new ObservableCollection<AppProfile>();

	private bool _loading = true;

	private bool _syncingControls;

	private string? _availableUpdateTag;

	private bool CompactLayout
	{
		get
		{
			if (base.ActualWidth > 0.0)
			{
				return base.ActualWidth < 500.0;
			}
			return false;
		}
	}

	private static string ConfigPath => Path.Combine(AppContext.BaseDirectory, "xr-viewlab.ini");

	private static string ManifestPath => Path.Combine(AppContext.BaseDirectory, "XR_APILAYER_cooooked_xrviewlab.json");

	private static string CurrentVersion => NormalizeVersion(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0");

	public MainWindow()
	{
		InitializeComponent();
		LoadWindowSize();
		LoadColumnWidths();
		RegisterColumnWidthPersistence();
		AppsGrid.ItemsSource = _apps;
		ProfilesGrid.ItemsSource = _enabledProfiles;
		LoadSettings();
		LoadAppProfiles();
		VersionText.Text = CurrentVersion;
		UpdateResponsiveLayout();
		UpdateFooterLayout();
		base.Loaded += async delegate
		{
			await CheckForUpdatesOnLaunchAsync();
		};
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
			base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
		}
		else
		{
			DragMove();
		}
	}

	private void Minimize_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void Close_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}

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

	private async Task CheckForUpdatesOnLaunchAsync()
	{
		try
		{
			using HttpClient client = CreateGitHubClient();
			UpdateRelease latest = await GetLatestReleaseAsync(client);
			if (latest != null && IsNewerRelease(latest.TagName, CurrentVersion))
			{
				_availableUpdateTag = latest.TagName;
				MarkUpdateAvailable();
			}
		}
		catch
		{
		}
	}

	private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
	{
		_ = 1;
		try
		{
			StatusText.Text = "Checking for updates...";
			using HttpClient client = CreateGitHubClient();
			UpdateRelease latest = await GetLatestReleaseAsync(client);
			if (latest == null)
			{
				StatusText.Text = "No update release found.";
				MessageBox.Show(this, "No XR ViewLab update release was found.", "XR ViewLab Updates", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			int comparison = CompareReleaseVersions(latest.TagName, CurrentVersion);
			if (comparison < 0)
			{
				StatusText.Text = "Running newer build than published release (" + CurrentVersion + ").";
				MessageBox.Show(this, "You are running a newer build than the latest published release.\n\nInstalled: " + CurrentVersion + "\nLatest: " + latest.TagName, "XR ViewLab Updates", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			if (comparison == 0)
			{
				StatusText.Text = "XR ViewLab is up to date (" + CurrentVersion + ").";
				MessageBox.Show(this, "You are up to date.\n\nInstalled: " + CurrentVersion + "\nLatest: " + latest.TagName, "XR ViewLab Updates", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			_availableUpdateTag = latest.TagName;
			MarkUpdateAvailable();
			if (MessageBox.Show(this, "Update detected: " + latest.TagName + "\n\nInstalled: " + CurrentVersion + "\n\nDownload and install now?\n\nXR ViewLab will close after the installer starts.", "XR ViewLab Update", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) != MessageBoxResult.Yes)
			{
				StatusText.Text = "Update available: " + latest.TagName + ".";
				return;
			}
			string text = await DownloadUpdateAsync(client, latest);
			StatusText.Text = "Launching installer for " + latest.TagName + "...";
			Process.Start(new ProcessStartInfo
			{
				FileName = "msiexec.exe",
				Arguments = "/i \"" + text + "\"",
				Verb = "runas",
				UseShellExecute = true
			});
			Close();
		}
		catch (Exception ex)
		{
			StatusText.Text = "Update check failed: " + ex.Message;
			MessageBox.Show(this, "Update check failed.\n\n" + ex.Message, "XR ViewLab Updates", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private static HttpClient CreateGitHubClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("XR-ViewLab");
		return httpClient;
	}

	private static async Task<UpdateRelease?> GetLatestReleaseAsync(HttpClient client)
	{
		using JsonDocument jsonDocument = JsonDocument.Parse(await client.GetStringAsync(ReleasesApiUrl));
		foreach (JsonElement item in jsonDocument.RootElement.EnumerateArray())
		{
			JsonElement value;
			bool num = item.TryGetProperty("draft", out value) && value.GetBoolean();
			JsonElement value2;
			bool flag2 = item.TryGetProperty("prerelease", out value2) && value2.GetBoolean();
			if (num || flag2)
			{
				continue;
			}
			JsonElement value3;
			string text = (item.TryGetProperty("tag_name", out value3) ? (value3.GetString() ?? "") : "");
			if (string.IsNullOrWhiteSpace(text))
			{
				continue;
			}
			string text2 = null;
			string text3 = null;
			if (item.TryGetProperty("assets", out var value4))
			{
				foreach (JsonElement item2 in value4.EnumerateArray())
				{
					JsonElement value5;
					string text4 = (item2.TryGetProperty("name", out value5) ? (value5.GetString() ?? "") : "");
					if (text4.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
					{
						text3 = text4;
						text2 = (item2.TryGetProperty("browser_download_url", out var value6) ? value6.GetString() : null);
						break;
					}
				}
			}
			if (!string.IsNullOrWhiteSpace(text2) && !string.IsNullOrWhiteSpace(text3))
			{
				return new UpdateRelease(text, text3, text2);
			}
		}
		return null;
	}

	private async Task<string> DownloadUpdateAsync(HttpClient client, UpdateRelease release)
	{
		string text = Path.Combine(Path.GetTempPath(), "XR ViewLab Updates");
		Directory.CreateDirectory(text);
		string installerPath = Path.Combine(text, release.AssetName);
		StatusText.Text = "Downloading " + release.TagName + "...";
		using HttpResponseMessage response = await client.GetAsync(release.AssetUrl, HttpCompletionOption.ResponseHeadersRead);
		response.EnsureSuccessStatusCode();
		string result;
		await using (Stream input = await response.Content.ReadAsStreamAsync())
		{
			string text2;
			await using (FileStream output = File.Create(installerPath))
			{
				await input.CopyToAsync(output);
				text2 = installerPath;
			}
			result = text2;
		}
		return result;
	}

	private static bool IsNewerRelease(string candidateTag, string currentTag)
	{
		return CompareReleaseVersions(candidateTag, currentTag) > 0;
	}

	private static int CompareReleaseVersions(string candidateTag, string currentTag)
	{
		Version version = ParseVersion(candidateTag);
		Version value = ParseVersion(currentTag);
		int num = version.CompareTo(value);
		if (num != 0)
		{
			return num;
		}
		return PrereleaseNumber(candidateTag).CompareTo(PrereleaseNumber(currentTag));
	}

	private static string NormalizeVersion(string version)
	{
		string text = (version ?? "").Trim();
		int num = text.IndexOf('+');
		if (num >= 0)
		{
			text = text.Substring(0, num);
		}
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "0.0.0";
		}
		if (!text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
		{
			text = "v" + text;
		}
		return text;
	}

	private static Version ParseVersion(string tag)
	{
		string text = tag.Trim();
		if (text.StartsWith("v", StringComparison.OrdinalIgnoreCase))
		{
			string text2 = text;
			text = text2.Substring(1, text2.Length - 1);
		}
		int num = text.IndexOfAny(new char[2] { '-', '+' });
		if (num >= 0)
		{
			text = text.Substring(0, num);
		}
		if (!Version.TryParse(text, out Version result))
		{
			return new Version(0, 0, 0);
		}
		return result;
	}

	private static int PrereleaseNumber(string tag)
	{
		int num = tag.LastIndexOf('.');
		if (num >= 0)
		{
			int num2 = num + 1;
			if (int.TryParse(tag.Substring(num2, tag.Length - num2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			{
				return result;
			}
		}
		return 0;
	}

	private void LoadColumnWidths()
	{
		SetColumnWidth(AppsCheckColumn, ReadColumnWidth("apps_check_column_width", 28.0, 24.0, 52.0));
		AppsNameColumn.Width = new DataGridLength(1.0, DataGridLengthUnitType.Star);
		ProfilesNameColumn.Width = new DataGridLength(1.0, DataGridLengthUnitType.Star);
		SetColumnWidth(ProfilesValueColumn, ReadColumnWidth("profiles_value_column_width", 84.0, 72.0, 220.0));
	}

	private void RegisterColumnWidthPersistence()
	{
		RegisterColumnWidthPersistence(AppsCheckColumn, "apps_check_column_width");
		RegisterColumnWidthPersistence(ProfilesValueColumn, "profiles_value_column_width");
	}

	private static void SetColumnWidth(DataGridColumn column, double width)
	{
		column.Width = new DataGridLength(width);
	}

	private static void RegisterColumnWidthPersistence(DataGridColumn column, string key)
	{
		DependencyPropertyDescriptor.FromProperty(DataGridColumn.ActualWidthProperty, typeof(DataGridColumn))?.AddValueChanged(column, delegate
		{
			if (column.ActualWidth > 0.0)
			{
				WritePrivateProfileString("Settings", key, column.ActualWidth.ToString("0", CultureInfo.InvariantCulture), ConfigPath);
			}
		});
	}

	private static double ReadColumnWidth(string key, double fallback, double min, double max)
	{
		if (!double.TryParse(ReadSetting(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return fallback;
		}
		return Math.Clamp(result, min, max);
	}

	protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
	{
		base.OnRenderSizeChanged(sizeInfo);
		UpdateResponsiveLayout();
		UpdateFooterLayout();
		if (!_loading)
		{
			UpdateHints();
		}
	}

	private void UpdateResponsiveLayout()
	{
		if (MainColumn != null && SideColumn != null && WideGapColumn != null && EnabledCard != null && SidePanel != null && RenderLabelColumn != null && RenderValueColumn != null && RenderHintColumn != null && RenderHintGapColumn != null)
		{
			bool flag = base.ActualWidth >= 640.0;
			bool flag2 = base.ActualWidth > 0.0 && base.ActualWidth < 360.0;
			MainColumn.Width = new GridLength(1.0, GridUnitType.Star);
			WideGapColumn.Width = (flag ? new GridLength(14.0) : new GridLength(0.0));
			SideColumn.Width = (flag ? new GridLength(1.0, GridUnitType.Star) : new GridLength(0.0));
			Grid.SetColumn(EnabledCard, 0);
			Grid.SetColumnSpan(EnabledCard, 1);
			Grid.SetRow(EnabledCard, 0);
			Grid.SetColumn(RenderHeader, 0);
			Grid.SetColumn(RenderCard, 0);
			Grid.SetColumn(ExperimentalHeader, 0);
			Grid.SetColumn(ExperimentalCard, 0);
			Grid.SetRow(RenderHeader, 2);
			Grid.SetRow(RenderCard, 3);
			Grid.SetRow(ExperimentalHeader, 5);
			Grid.SetRow(ExperimentalCard, 6);
			Grid.SetColumn(SidePanel, flag ? 2 : 0);
			Grid.SetRow(SidePanel, flag ? 0 : 8);
			Grid.SetRowSpan(SidePanel, flag ? 9 : 1);
			AppsGridRow.Height = new GridLength(flag ? 165.0 : 140.0);
			ProfilesGridRow.Height = new GridLength(flag ? 165.0 : 140.0);
			RenderLabelColumn.Width = (flag2 ? new GridLength(64.0) : new GridLength(76.0));
			RenderValueColumn.Width = (flag2 ? new GridLength(1.0, GridUnitType.Star) : new GridLength(100.0));
			RenderHintGapColumn.Width = (flag2 ? new GridLength(0.0) : new GridLength(10.0));
			RenderHintColumn.Width = (flag2 ? new GridLength(0.0) : new GridLength(1.0, GridUnitType.Star));
			TotalHint.Visibility = (flag2 ? Visibility.Collapsed : Visibility.Visible);
			TopHint.Visibility = (flag2 ? Visibility.Collapsed : Visibility.Visible);
			BottomHint.Visibility = (flag2 ? Visibility.Collapsed : Visibility.Visible);
			HorizontalHint.Visibility = (flag2 ? Visibility.Collapsed : Visibility.Visible);
			SetSliderCompact(TotalSlider, flag2);
			SetSliderCompact(HorizontalSlider, flag2);
			SetSliderCompact(TopSlider, flag2);
			SetSliderCompact(BottomSlider, flag2);
		}
	}

	private static void SetSliderCompact(Slider slider, bool compact)
	{
		Grid.SetColumn(slider, compact ? 0 : 1);
		Grid.SetColumnSpan(slider, compact ? 2 : 3);
	}

	private void UpdateFooterLayout()
	{
		if (StatusText != null && SupportFooterTextBlock != null && UpdatesButton != null)
		{
			bool flag = base.ActualWidth > 0.0 && base.ActualWidth < 900.0;
			bool flag2 = base.ActualWidth > 0.0 && base.ActualWidth < 460.0;
			StatusText.Visibility = (flag ? Visibility.Collapsed : Visibility.Visible);
			SupportFooterTextBlock.Text = (flag2 ? "Support" : SupportFooterText);
			UpdatesButton.Text = "Update";
			SupportFooterTextBlock.ToolTip = SupportFooterText;
			UpdatesButton.ToolTip = string.IsNullOrWhiteSpace(_availableUpdateTag) ? "Check for updates" : ("Update available: " + _availableUpdateTag);
		}
	}

	private void MarkUpdateAvailable()
	{
		if (UpdatesButton == null)
		{
			return;
		}
		UpdatesButton.ToolTip = string.IsNullOrWhiteSpace(_availableUpdateTag) ? "Update available" : ("Update available: " + _availableUpdateTag);
		if (UpdatesButton.Foreground is not SolidColorBrush solidColorBrush || solidColorBrush.IsFrozen)
		{
			solidColorBrush = new SolidColorBrush(Color.FromRgb(143, 143, 143));
			UpdatesButton.Foreground = solidColorBrush;
		}
		solidColorBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation
		{
			To = Color.FromRgb(77, 163, 255),
			Duration = TimeSpan.FromSeconds(5.0),
			AutoReverse = true,
			RepeatBehavior = RepeatBehavior.Forever
		});
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern uint GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder value, uint size, string filePath);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern bool WritePrivateProfileString(string section, string key, string? value, string filePath);

	private static string ReadSetting(string key, string fallback)
	{
		StringBuilder stringBuilder = new StringBuilder(256);
		GetPrivateProfileString("Settings", key, fallback, stringBuilder, (uint)stringBuilder.Capacity, ConfigPath);
		return stringBuilder.ToString();
	}

	private static bool ReadBoolSetting(string key, bool fallback)
	{
		string text = ReadSetting(key, fallback ? "1" : "0");
		if (!text.Equals("1", StringComparison.OrdinalIgnoreCase) && !text.Equals("true", StringComparison.OrdinalIgnoreCase) && !text.Equals("yes", StringComparison.OrdinalIgnoreCase))
		{
			return text.Equals("on", StringComparison.OrdinalIgnoreCase);
		}
		return true;
	}

	private static double ReadScaleSetting(string key, double fallback)
	{
		if (!double.TryParse(ReadSetting(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return fallback;
		}
		return Math.Clamp(result, 0.0, 1.0);
	}

	private static string FormatScale(double value)
	{
		return value.ToString("0.00", CultureInfo.InvariantCulture);
	}

	private static string FormatPercent(double value)
	{
		return $"{Math.Clamp(value, 0.0, 1.0) * 100.0:0}%";
	}

	private static uint ToMillis(double value)
	{
		return (uint)Math.Round(Math.Clamp(value, 0.0, 1.0) * 1000.0);
	}

	private static double ReadWindowSizeSetting(string key, double fallback)
	{
		if (!double.TryParse(ReadSetting(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return fallback;
		}
		return Math.Clamp(result, 1.0, 10000.0);
	}

	private void LoadWindowSize()
	{
		double val = ReadWindowSizeSetting("window_width", base.MinWidth);
		double val2 = ReadWindowSizeSetting("window_height", base.MinHeight);
		base.Width = Math.Max(base.MinWidth, val);
		base.Height = Math.Max(base.MinHeight, val2);
	}

	private void SaveWindowSize()
	{
		Rect rect = ((base.WindowState == WindowState.Normal) ? new Rect(base.Left, base.Top, base.ActualWidth, base.ActualHeight) : base.RestoreBounds);
		double num = Math.Max(base.MinWidth, rect.Width);
		double num2 = Math.Max(base.MinHeight, rect.Height);
		WritePrivateProfileString("Settings", "window_width", num.ToString("0", CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", "window_height", num2.ToString("0", CultureInfo.InvariantCulture), ConfigPath);
	}

	private static double FromMillis(object? value, double fallback)
	{
		if (value is int num && num >= 0 && num <= 1000)
		{
			return (double)num / 1000.0;
		}
		if (value is uint num2 && num2 <= 1000)
		{
			return (double)num2 / 1000.0;
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
		double num = ReadScaleSetting("total_render_height", ReadScaleSetting("total_share", ReadScaleSetting("vertical_tangent", 0.18)));
		double value = ReadScaleSetting("horizontal_render_width", 0.8);
		bool value2 = ReadBoolSetting("split_mode", fallback: false);
		EnabledCheck.IsChecked = ReadBoolSetting("enabled", fallback: true);
		SplitCheck.IsChecked = value2;
		FoveatedCenterCheck.IsChecked = ReadBoolSetting("foveated_center_compensation", fallback: false);
		VisualMaskOnlyCheck.IsChecked = ReadBoolSetting("visual_mask_only", fallback: false);
		HorizontalVisualMaskOnlyCheck.IsChecked = ReadBoolSetting("horizontal_visual_mask_only", fallback: false);
		OuterEdgeVisibilityMaskOnlyCheck.IsChecked = ReadBoolSetting("outer_edge_visibility_mask_only", fallback: true);
		TotalBox.Text = FormatScale(num);
		TopBox.Text = FormatScale(ReadScaleSetting("top_tangent", num * 0.5));
		BottomBox.Text = FormatScale(ReadScaleSetting("bottom_tangent", num * 0.5));
		HorizontalBox.Text = FormatScale(value);
		SyncSlidersFromText();
		_loading = false;
		UpdateModeControls();
		UpdateHints();
		StatusText.Text = "Config: " + ConfigPath;
	}

	private void UpdateModeControls()
	{
		bool valueOrDefault = SplitCheck.IsChecked == true;
		TotalBox.IsEnabled = !valueOrDefault;
		TotalSlider.IsEnabled = !valueOrDefault;
		TopBox.IsEnabled = valueOrDefault;
		TopSlider.IsEnabled = valueOrDefault;
		BottomBox.IsEnabled = valueOrDefault;
		BottomSlider.IsEnabled = valueOrDefault;
	}

	private void UpdateHints()
	{
		if (TotalHint == null || TopHint == null || BottomHint == null || HorizontalHint == null || CombinedHint == null)
		{
			return;
		}
		double value;
		bool flag = TryReadTextBox(TotalBox, out value);
		double value2;
		bool flag2 = TryReadTextBox(TopBox, out value2);
		double value3;
		bool flag3 = TryReadTextBox(BottomBox, out value3);
		double value4;
		bool flag4 = TryReadTextBox(HorizontalBox, out value4);
		bool valueOrDefault = SplitCheck.IsChecked == true;
		if (!valueOrDefault && flag)
		{
			double num = value * 0.5;
			if (!_loading)
			{
				TopBox.TextChanged -= RenderValue_Changed;
				BottomBox.TextChanged -= RenderValue_Changed;
				TopBox.Text = FormatScale(num);
				BottomBox.Text = FormatScale(num);
				TopBox.TextChanged += RenderValue_Changed;
				BottomBox.TextChanged += RenderValue_Changed;
				value2 = num;
				value3 = num;
				flag2 = (flag3 = true);
			}
		}
		bool compactLayout = CompactLayout;
		TotalHint.Text = ((!flag) ? "Enter render height" : (compactLayout ? (FormatPercent(value) + " height") : (FormatPercent(value) + " total render height")));
		TopHint.Text = (flag2 ? (FormatPercent(value2) + " top") : "Enter top value");
		BottomHint.Text = (flag3 ? (FormatPercent(value3) + " bottom") : "Enter bottom value");
		HorizontalHint.Text = ((!flag4) ? "Enter render width" : (compactLayout ? (FormatPercent(value4) + " width") : (FormatPercent(value4) + " total render width")));
		TotalHint.ToolTip = (flag ? (FormatPercent(value) + " total render height") : null);
		TopHint.ToolTip = (flag2 ? (FormatPercent(value2) + " top") : null);
		BottomHint.ToolTip = (flag3 ? (FormatPercent(value3) + " bottom") : null);
		HorizontalHint.ToolTip = (flag4 ? (FormatPercent(value4) + " total render width") : null);
		CombinedHint.Text = ((valueOrDefault && flag2 && flag3) ? ("Combined: " + FormatPercent(value2 + value3) + " total render height") : (flag ? ("Combined: " + FormatPercent(value) + " total render height") : "Combined: enter valid values"));
		SyncSlidersFromText();
	}

	private static void SetSliderValue(Slider slider, double value)
	{
		if (slider != null)
		{
			value = Math.Clamp(value, slider.Minimum, slider.Maximum);
			if (Math.Abs(slider.Value - value) > 0.0001)
			{
				slider.Value = value;
			}
		}
	}

	private void SyncSlidersFromText()
	{
		if (!_syncingControls)
		{
			_syncingControls = true;
			if (TryReadTextBox(TotalBox, out var value))
			{
				SetSliderValue(TotalSlider, value);
			}
			if (TryReadTextBox(TopBox, out var value2))
			{
				SetSliderValue(TopSlider, value2);
			}
			if (TryReadTextBox(BottomBox, out var value3))
			{
				SetSliderValue(BottomSlider, value3);
			}
			if (TryReadTextBox(HorizontalBox, out var value4))
			{
				SetSliderValue(HorizontalSlider, value4);
			}
			_syncingControls = false;
		}
	}

	private void SyncTextFromSlider(TextBox box, Slider slider)
	{
		if (!_syncingControls)
		{
			_syncingControls = true;
			box.Text = FormatScale(slider.Value);
			_syncingControls = false;
			UpdateHints();
		}
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
		if (!_loading && !_syncingControls && TotalBox != null && TopBox != null && BottomBox != null && HorizontalBox != null)
		{
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
	}

	private void SplitCheck_Changed(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			UpdateModeControls();
			UpdateHints();
		}
	}

	private void EnabledCheck_Changed(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			SaveGlobalSettings();
		}
	}

	private void Save_Click(object sender, RoutedEventArgs e)
	{
		SaveGlobalSettings();
	}

	private void ExperimentalCheck_Changed(object sender, RoutedEventArgs e)
	{
		if (_loading)
		{
			return;
		}
		SaveExperimentalSettings();
	}

	private void SaveExperimentalSettings()
	{
		Directory.CreateDirectory(AppContext.BaseDirectory);
		WritePrivateProfileString("Settings", "foveated_center_compensation", (FoveatedCenterCheck.IsChecked == true) ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", "visual_mask_only", (VisualMaskOnlyCheck.IsChecked == true) ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", "horizontal_visual_mask_only", (HorizontalVisualMaskOnlyCheck.IsChecked == true) ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", "outer_edge_visibility_mask_only", (OuterEdgeVisibilityMaskOnlyCheck.IsChecked == true) ? "1" : "0", ConfigPath);
		StatusText.Text = "Experimental setting saved. Restart the OpenXR game.";
	}

	private void SaveGlobalSettings()
	{
		bool valueOrDefault = SplitCheck.IsChecked == true;
		if (!TryReadTextBox(TotalBox, out var value) || !TryReadTextBox(TopBox, out var value2) || !TryReadTextBox(BottomBox, out var value3) || !TryReadTextBox(HorizontalBox, out var value4))
		{
			MessageBox.Show(this, "Enter render values from 0.00 to 1.00. Total and horizontal must be at least 0.01.", "XR ViewLab", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		if (valueOrDefault)
		{
			value = Math.Clamp(value2 + value3, 0.01, 1.0);
		}
		else
		{
			value = Math.Clamp(value, 0.01, 1.0);
			value2 = value * 0.5;
			value3 = value * 0.5;
		}
		value4 = Math.Clamp(value4, 0.01, 1.0);
		bool valueOrDefault2 = EnabledCheck.IsChecked == true;
		Directory.CreateDirectory(AppContext.BaseDirectory);
		WritePrivateProfileString("Settings", "enabled", valueOrDefault2 ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", "split_mode", valueOrDefault ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", "foveated_center_compensation", (FoveatedCenterCheck.IsChecked == true) ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", "visual_mask_only", (VisualMaskOnlyCheck.IsChecked == true) ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", "horizontal_visual_mask_only", (HorizontalVisualMaskOnlyCheck.IsChecked == true) ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", "outer_edge_visibility_mask_only", (OuterEdgeVisibilityMaskOnlyCheck.IsChecked == true) ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", "total_render_height", FormatScale(value), ConfigPath);
		WritePrivateProfileString("Settings", "total_share", null, ConfigPath);
		WritePrivateProfileString("Settings", "vertical_tangent", null, ConfigPath);
		WritePrivateProfileString("Settings", "top_tangent", FormatScale(value2), ConfigPath);
		WritePrivateProfileString("Settings", "bottom_tangent", FormatScale(value3), ConfigPath);
		WritePrivateProfileString("Settings", "horizontal_render_width", FormatScale(value4), ConfigPath);
		WriteRegistryEnabled(valueOrDefault2);
		_loading = true;
		TotalBox.Text = FormatScale(value);
		TopBox.Text = FormatScale(value2);
		BottomBox.Text = FormatScale(value3);
		HorizontalBox.Text = FormatScale(value4);
		SyncSlidersFromText();
		_loading = false;
		UpdateHints();
		StatusText.Text = (valueOrDefault2 ? "Saved. Restart the OpenXR game." : "Layer disabled.");
	}

	private static void WriteRegistryEnabled(bool enabled)
	{
		using RegistryKey registryKey = Registry.LocalMachine.CreateSubKey(OpenXrRegistryRoot, writable: true) ?? throw new InvalidOperationException("Could not open OpenXR registry key.");
		registryKey.SetValue(ManifestPath, (!enabled) ? 1 : 0, RegistryValueKind.DWord);
	}

	private void LoadAppProfiles()
	{
		_loading = true;
		_apps.Clear();
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(AppRegistryRoot);
		if (registryKey != null)
		{
			string[] subKeyNames = registryKey.GetSubKeyNames();
			foreach (string text in subKeyNames)
			{
				using RegistryKey registryKey2 = registryKey.OpenSubKey(text);
				if (registryKey2 != null)
				{
					AppProfile item = ReadAppProfile(text, registryKey2);
					_apps.Add(item);
				}
			}
		}
		_loading = false;
		RefreshEnabledProfiles();
	}

	private AppProfile ReadAppProfile(string keyName, RegistryKey appKey)
	{
		double num = ReadScaleSetting("total_render_height", ReadScaleSetting("total_share", ReadScaleSetting("vertical_tangent", 0.18)));
		double fallback = ReadScaleSetting("top_tangent", num * 0.5);
		double fallback2 = ReadScaleSetting("bottom_tangent", num * 0.5);
		double fallback3 = ReadScaleSetting("horizontal_render_width", 0.8);
		string text = Convert.ToString(appKey.GetValue("module", "")) ?? "";
		string text2 = CleanAppDisplayName(Convert.ToString(appKey.GetValue("display_name", keyName)) ?? keyName, keyName, text);
		string text3 = (string.IsNullOrWhiteSpace(text) ? keyName : Path.GetFileName(text));
		bool profileEnabled = Convert.ToInt32(appKey.GetValue("profile_enabled", 0), CultureInfo.InvariantCulture) != 0;
		bool appEnabled = Convert.ToInt32(appKey.GetValue("app_enabled", 1), CultureInfo.InvariantCulture) != 0;
		double top = FromMillis(appKey.GetValue("top_tangent"), fallback);
		double bottom = FromMillis(appKey.GetValue("bottom_tangent"), fallback2);
		double horizontal = FromMillis(appKey.GetValue("horizontal_render_width"), fallback3);
		return new AppProfile
		{
			Key = keyName,
			DisplayName = text2,
			Display = text2 + " (" + text3 + ")",
			AppEnabled = appEnabled,
			ProfileEnabled = profileEnabled,
			Top = top,
			Bottom = bottom,
			Horizontal = horizontal
		};
	}

	private static string CleanAppDisplayName(string displayName, string keyName, string modulePath)
	{
		string text = string.IsNullOrWhiteSpace(displayName) ? keyName : displayName.Trim();
		string moduleName = Path.GetFileNameWithoutExtension(modulePath);
		if (string.IsNullOrWhiteSpace(moduleName))
		{
			moduleName = Path.GetFileNameWithoutExtension(keyName);
		}
		if (text.StartsWith("OpenComposite_", StringComparison.OrdinalIgnoreCase))
		{
			text = text.Substring("OpenComposite_".Length);
		}
		if (text.StartsWith("Unity Application", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(moduleName))
		{
			text = moduleName;
		}
		if (text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
		{
			text = Path.GetFileNameWithoutExtension(text);
		}
		return string.IsNullOrWhiteSpace(text) ? keyName : text;
	}

	private void RefreshEnabledProfiles()
	{
		_enabledProfiles.Clear();
		foreach (AppProfile item in _apps.Where((AppProfile app) => app.AppEnabled))
		{
			item.RefreshSummary();
			_enabledProfiles.Add(item);
		}
	}

	private void AppEnabled_Changed(object sender, RoutedEventArgs e)
	{
		if (_loading || !(sender is CheckBox { DataContext: AppProfile dataContext }))
		{
			return;
		}
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(AppRegistryRoot + "\\" + dataContext.Key, writable: true) ?? throw new InvalidOperationException("Could not write app profile.");
		registryKey.SetValue("app_enabled", dataContext.AppEnabled ? 1 : 0, RegistryValueKind.DWord);
		RefreshEnabledProfiles();
		StatusText.Text = (dataContext.AppEnabled ? ("Layer enabled for " + dataContext.DisplayName + ". Restart the OpenXR game.") : ("Layer disabled for " + dataContext.DisplayName + ". Restart the OpenXR game."));
	}

	private void ReloadApps_Click(object sender, RoutedEventArgs e)
	{
		LoadAppProfiles();
		StatusText.Text = "App list reloaded.";
	}

	private void ProfilesGrid_DoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (!(ProfilesGrid.SelectedItem is AppProfile appProfile))
		{
			return;
		}
		ProfileWindow profileWindow = new ProfileWindow(appProfile.DisplayName, appProfile.Top, appProfile.Bottom, appProfile.Horizontal)
		{
			Owner = this
		};
		if (profileWindow.ShowDialog() == true)
		{
			if (profileWindow.UseGlobal)
			{
				WriteAppCustomProfile(appProfile, appProfile.Top, appProfile.Bottom, appProfile.Horizontal, profileEnabled: false);
				appProfile.ProfileEnabled = false;
			}
			else
			{
				WriteAppCustomProfile(appProfile, profileWindow.TopValue, profileWindow.BottomValue, profileWindow.HorizontalValue, profileEnabled: true);
				appProfile.Top = profileWindow.TopValue;
				appProfile.Bottom = profileWindow.BottomValue;
				appProfile.Horizontal = profileWindow.HorizontalValue;
				appProfile.ProfileEnabled = true;
			}
			appProfile.RefreshSummary();
			RefreshEnabledProfiles();
			StatusText.Text = "Saved app profile. Restart the OpenXR game.";
		}
	}

	private void ResetApp_Click(object sender, RoutedEventArgs e)
	{
		if (!(ProfilesGrid.SelectedItem is AppProfile appProfile))
		{
			StatusText.Text = "Select an app profile to reset.";
			return;
		}
		ResetAppCustomProfile(appProfile);
		appProfile.ProfileEnabled = false;
		RefreshEnabledProfiles();
		StatusText.Text = "Reset " + appProfile.DisplayName + " to global values. Restart the OpenXR game.";
	}

	private static void WriteAppCustomProfile(AppProfile profile, double top, double bottom, double horizontal, bool profileEnabled)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(AppRegistryRoot + "\\" + profile.Key, writable: true) ?? throw new InvalidOperationException("Could not write app profile.");
		registryKey.SetValue("profile_enabled", profileEnabled ? 1 : 0, RegistryValueKind.DWord);
		registryKey.SetValue("split_mode", 1, RegistryValueKind.DWord);
		registryKey.SetValue("total_render_height", ToMillis(Math.Clamp(top + bottom, 0.01, 1.0)), RegistryValueKind.DWord);
		registryKey.DeleteValue("total_share", throwOnMissingValue: false);
		registryKey.DeleteValue("vertical_tangent", throwOnMissingValue: false);
		registryKey.SetValue("top_tangent", ToMillis(top), RegistryValueKind.DWord);
		registryKey.SetValue("bottom_tangent", ToMillis(bottom), RegistryValueKind.DWord);
		registryKey.SetValue("horizontal_render_width", ToMillis(horizontal), RegistryValueKind.DWord);
	}

	private static void ResetAppCustomProfile(AppProfile profile)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(AppRegistryRoot + "\\" + profile.Key, writable: true) ?? throw new InvalidOperationException("Could not write app profile.");
		registryKey.SetValue("profile_enabled", 0, RegistryValueKind.DWord);
		registryKey.DeleteValue("split_mode", throwOnMissingValue: false);
		registryKey.DeleteValue("total_render_height", throwOnMissingValue: false);
		registryKey.DeleteValue("total_share", throwOnMissingValue: false);
		registryKey.DeleteValue("vertical_tangent", throwOnMissingValue: false);
		registryKey.DeleteValue("top_tangent", throwOnMissingValue: false);
		registryKey.DeleteValue("bottom_tangent", throwOnMissingValue: false);
		registryKey.DeleteValue("horizontal_render_width", throwOnMissingValue: false);
	}
}
