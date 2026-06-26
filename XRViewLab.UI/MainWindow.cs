using System;
using System.Collections.Generic;
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

	// Render options
	private const string FoveatedCenterKey = "foveated_center_compensation";
	private const string StencilOuterEdgesKey = "stencil_outer_edges_only";
	private const string CropOuterEdgesKey = "crop_outer_edges_only";
	private const string HorizVisualMaskBothKey = "horizontal_visual_mask_both";
	private const string HorizOuterEyeMaskKey = "horizontal_outer_eye_mask";
	private const string HorizInnerEyeMaskKey = "horizontal_inner_eye_mask";
	private const string VertVisualMaskBothKey = "vertical_visual_mask_both";
	private const string VertTopMaskKey = "vertical_top_mask_only";
	private const string VertBottomMaskKey = "vertical_bottom_mask_only";

	// ReShade MENU — OpenXR
	private const string XrHmdMenuKey = "reshade_xr_hmd_menu";
	private const string Xr3dMenuKey = "reshade_xr_3d_menu";
	private const string XrHeadLockedKey = "reshade_xr_head_locked";
	private const string Xr3dCursorKey = "reshade_xr_3d_cursor";
	private const string XrOxrtkColorsKey = "reshade_xr_oxrtk_colors";

	// ReShade MENU — OpenVR
	private const string VrDesktopDupKey = "reshade_vr_desktop_dup";
	private const string VrAlwaysOnTopKey = "reshade_vr_always_on_top";
	private const string VrLockPositionKey = "reshade_vr_lock_position";
	private const string Vr3dMenuKey = "reshade_vr_3d_menu";
	private const string VrHeadLockedKey = "reshade_vr_head_locked";

	private readonly ObservableCollection<AppProfile> _apps = new ObservableCollection<AppProfile>();


	private bool _loading = true;

	private bool _xrLaunchModeApplied;

	private bool _syncingControls;

	private string? _availableUpdateTag;

	private readonly ReShadeControlService _xrControl = new();
	private System.Windows.Threading.DispatcherTimer _xrPollTimer;

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

	private static string ConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab");

	private static string ConfigPath => Path.Combine(ConfigDirectory, "xr-viewlab.ini");

	private static string LegacyConfigPath => Path.Combine(AppContext.BaseDirectory, "xr-viewlab.ini");

	private static string ProgramFilesInstallDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "xr-viewlab");

	private static string ProcessDirectory => Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;

	private static string ManifestPath
	{
		get
		{
			string localManifest = Path.Combine(ProcessDirectory, "XR_APILAYER_cooooked_xrviewlab.json");
			if (File.Exists(localManifest))
			{
				return localManifest;
			}
			string installedManifest = Path.Combine(ProgramFilesInstallDirectory, "XR_APILAYER_cooooked_xrviewlab.json");
			return File.Exists(installedManifest) ? installedManifest : localManifest;
		}
	}

	private static string CurrentVersion => NormalizeVersion(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0");

	public MainWindow()
	{
		InitializeComponent();
		EnsureConfigMigrated();
		LoadWindowSize();
		LoadColumnWidths();
		RegisterColumnWidthPersistence();
		AppsGrid.ItemsSource = _apps;
		LoadSettings();
		UpdateEnabledBadge();
		LoadAppProfiles();
		VersionText.Text = CurrentVersion;
		UpdateResponsiveLayout();
		UpdateFooterLayout();
		VisualMasksPopup.Closed += (_, _) => _visualMasksPopupClosedAt = DateTime.UtcNow;
		_xrPollTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_xrPollTimer.Tick += XrPollTimer_Tick;
		_xrPollTimer.Start();
		base.Loaded += async delegate
		{
			await CheckForUpdatesOnLaunchAsync();
			CheckManifestHealth();
		};
	}

	private DateTime _visualMasksPopupClosedAt = DateTime.MinValue;

	private void VisualMasksButton_Click(object sender, RoutedEventArgs e)
	{
		// StaysOpen=False closes the popup on MouseDown before this Click fires.
		// If it closed within the last 200ms the click was the close — don't reopen.
		if ((DateTime.UtcNow - _visualMasksPopupClosedAt).TotalMilliseconds < 200)
			return;
		VisualMasksPopup.PlacementTarget = (UIElement)sender;
		VisualMasksPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
		VisualMasksPopup.IsOpen = true;
	}

	private static void EnsureConfigMigrated()
	{
		Directory.CreateDirectory(ConfigDirectory);
		if (!File.Exists(ConfigPath) && File.Exists(LegacyConfigPath))
		{
			File.Copy(LegacyConfigPath, ConfigPath, overwrite: false);
		}
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

	private void OpenLog_Click(object sender, MouseButtonEventArgs e)
	{
		string logPath = Path.Combine(ConfigDirectory, "Logs", "ViewLab.log");
		if (!File.Exists(logPath))
		{
			StatusText.Text = "No log file found yet. Launch an OpenXR game first.";
			return;
		}
		Process.Start(new ProcessStartInfo { FileName = logPath, UseShellExecute = true });
	}

	private void CheckManifestHealth()
	{
		try
		{
			using RegistryKey? key = Registry.LocalMachine.OpenSubKey(OpenXrRegistryRoot);
			if (key == null)
			{
				StatusText.Text = "Warning: OpenXR registry root not found. Is an OpenXR runtime installed?";
				return;
			}
			string manifest = ManifestPath;
			bool found = key.GetValueNames().Any(v =>
				v.Contains("XR_APILAYER_cooooked_xrviewlab.json", StringComparison.OrdinalIgnoreCase));
			if (!found)
			{
				StatusText.Text = "Warning: layer not registered. Try reinstalling XR ViewLab.";
			}
			else if (!File.Exists(manifest))
			{
				StatusText.Text = "Warning: manifest file missing at " + manifest;
			}
		}
		catch
		{
		}
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
		SetColumnWidth(AppsValueColumn, ReadColumnWidth("apps_value_column_width", 84.0, 72.0, 220.0));
	}

	private void RegisterColumnWidthPersistence()
	{
		RegisterColumnWidthPersistence(AppsCheckColumn, "apps_check_column_width");
		RegisterColumnWidthPersistence(AppsValueColumn, "apps_value_column_width");
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
		UpdateResponsiveLayout(sizeInfo.NewSize.Width);
		UpdateFooterLayout();
		if (!_loading)
		{
			UpdateHints();
		}
	}

	private void UpdateResponsiveLayout(double w = -1)
	{
		if (MainColumn != null && SideColumn != null && WideGapColumn != null && WideGapColumn2 != null && RightColumn != null && EnabledCard != null && SidePanel != null && RightColumnPanel != null && RenderLabelColumn != null && RenderValueColumn != null && RenderHintColumn != null && RenderHintGapColumn != null && OptionsGapRow != null)
		{
			if (w < 0) w = base.ActualWidth;
			bool compact  = w > 0.0 && w < 360.0;  // min: sliders collapse
			bool twoCol   = w >= 600.0;             // medium: two columns
			bool threeCol = w >= 900.0;             // large: three columns

			MainColumn.Width     = new GridLength(1.0, GridUnitType.Star);
			WideGapColumn.Width  = twoCol    ? new GridLength(14.0) : new GridLength(0.0);
			SideColumn.Width     = twoCol    ? new GridLength(1.0, GridUnitType.Star) : new GridLength(0.0);
			WideGapColumn2.Width = threeCol  ? new GridLength(14.0) : new GridLength(0.0);
			RightColumn.Width    = threeCol  ? new GridLength(1.0, GridUnitType.Star) : new GridLength(0.0);

			// EnabledCard and RenderCard always in left col
			Grid.SetColumn(EnabledCard, 0);
			Grid.SetColumnSpan(EnabledCard, 1);
			Grid.SetRow(EnabledCard, 0);
			Grid.SetColumn(RenderCard, 0);
			Grid.SetRow(RenderCard, 2);

			// RightColumnPanel: shown in three-col only, spans all rows so not sized by left-col row heights.
			// Standalone OptionsHeader/OptionsCard (Grid rows 5/6) shown in single/two-col only.
			RightColumnPanel.Visibility = threeCol ? Visibility.Visible   : Visibility.Collapsed;
			OptionsHeader.Visibility    = threeCol ? Visibility.Collapsed : Visibility.Visible;
			OptionsCard.Visibility      = threeCol ? Visibility.Collapsed : Visibility.Visible;
			OptionsGapRow.Height        = threeCol ? new GridLength(0)    : new GridLength(10);

			// SidePanel (apps): col 2 in two/three-col, col 0 row 8 in single
			Grid.SetColumn(SidePanel, twoCol ? 2 : 0);
			Grid.SetRow(SidePanel, twoCol ? 0 : 8);
			Grid.SetRowSpan(SidePanel, twoCol ? 9 : 1);

			// ReShade cards in SidePanel: visible in single/two-col only
			ReShadeOpenXRHeader.Visibility = threeCol ? Visibility.Collapsed : Visibility.Visible;
			ReShadeOpenXRCard.Visibility   = threeCol ? Visibility.Collapsed : Visibility.Visible;
			ReShadeOpenVRHeader.Visibility = threeCol ? Visibility.Collapsed : Visibility.Visible;
			ReShadeOpenVRCard.Visibility   = threeCol ? Visibility.Collapsed : Visibility.Visible;
			ThanksText.Visibility          = threeCol ? Visibility.Collapsed : Visibility.Visible;

			AppsGridRow.Height = new GridLength(twoCol ? 260.0 : 180.0);

			// Compact render card: value box fills, hints hidden, sliders span full width
			RenderValueColumn.Width   = compact ? new GridLength(1.0, GridUnitType.Star) : new GridLength(100.0);
			RenderHintGapColumn.Width = compact ? new GridLength(0.0) : new GridLength(10.0);
			RenderHintColumn.Width    = compact ? new GridLength(0.0) : new GridLength(1.0, GridUnitType.Star);
			TotalHint.Visibility      = compact ? Visibility.Collapsed : Visibility.Visible;
			TopHint.Visibility        = compact ? Visibility.Collapsed : Visibility.Visible;
			BottomHint.Visibility     = compact ? Visibility.Collapsed : Visibility.Visible;
			HorizontalHint.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
			SetSliderCompact(TotalSlider,      compact);
			SetSliderCompact(HorizontalSlider, compact);
			SetSliderCompact(TopSlider,        compact);
			SetSliderCompact(BottomSlider,     compact);
		}
	}

	private static void SetSliderCompact(Slider slider, bool compact)
	{
		// RenderCard grid columns: 0=label, 1=gap(8px), 2=value, 3=hint-gap, 4=hint
		// Normal: slider under col 2+3+4 (start=2, span=3)
		// Compact: slider under col 0+1+2 (start=0, span=3), hints hidden
		Grid.SetColumn(slider, compact ? 0 : 2);
		Grid.SetColumnSpan(slider, compact ? 3 : 3);
	}

	private void UpdateFooterLayout()
	{
		if (StatusText == null || SupportFooterTextBlock == null || UpdatesButton == null) return;
		double fw = base.ActualWidth;
		bool mini = fw > 0.0 && fw < 360.0;

		if (mini)
		{
			// Mini mode: items Auto-sized, star gaps distribute remaining space evenly
			FooterVersionCol.Width  = new GridLength(1, GridUnitType.Auto);
			FooterGap1.Width        = new GridLength(1, GridUnitType.Star);
			FooterStatusCol.Width   = new GridLength(0);
			FooterGap2.Width        = new GridLength(0);
			FooterLogCol.Width      = new GridLength(1, GridUnitType.Auto);
			FooterGap3.Width        = new GridLength(1, GridUnitType.Star);
			FooterSupportCol.Width  = new GridLength(1, GridUnitType.Auto);
			FooterGap4.Width        = new GridLength(1, GridUnitType.Star);
			FooterUpdCol.Width      = new GridLength(1, GridUnitType.Auto);
			VersionText.HorizontalAlignment  = HorizontalAlignment.Left;
			OpenLogButton.HorizontalAlignment   = HorizontalAlignment.Center;
			SupportFooterTextBlock.HorizontalAlignment = HorizontalAlignment.Center;
			UpdatesButton.HorizontalAlignment   = HorizontalAlignment.Right;
			StatusText.Visibility = Visibility.Collapsed;
		}
		else
		{
			// Normal: auto columns with status text filling the middle
			FooterVersionCol.Width  = new GridLength(1, GridUnitType.Auto);
			FooterGap1.Width        = new GridLength(6);
			FooterStatusCol.Width   = new GridLength(1, GridUnitType.Star);
			FooterGap2.Width        = new GridLength(6);
			FooterLogCol.Width      = new GridLength(1, GridUnitType.Auto);
			FooterGap3.Width        = new GridLength(6);
			FooterSupportCol.Width  = new GridLength(1, GridUnitType.Auto);
			FooterGap4.Width        = new GridLength(6);
			FooterUpdCol.Width      = new GridLength(1, GridUnitType.Auto);
			VersionText.HorizontalAlignment  = HorizontalAlignment.Left;
			OpenLogButton.HorizontalAlignment   = HorizontalAlignment.Right;
			SupportFooterTextBlock.HorizontalAlignment = HorizontalAlignment.Right;
			UpdatesButton.HorizontalAlignment   = HorizontalAlignment.Right;
			StatusText.Visibility = fw >= 900.0 ? Visibility.Visible : Visibility.Collapsed;
		}

		SupportFooterTextBlock.Text = mini ? "Support" : SupportFooterText;
		SupportFooterTextBlock.ToolTip = SupportFooterText;
		UpdatesButton.Text = mini ? "Upd" : "Update";
		UpdatesButton.ToolTip = string.IsNullOrWhiteSpace(_availableUpdateTag) ? "Check for updates" : ("Update available: " + _availableUpdateTag);
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

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

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
		_viewlabEnabled = ReadBoolSetting("enabled", fallback: true);
		SplitCheck.IsChecked = value2;
		TotalBox.Text = FormatScale(num);
		TopBox.Text = FormatScale(ReadScaleSetting("top_tangent", num * 0.5));
		BottomBox.Text = FormatScale(ReadScaleSetting("bottom_tangent", num * 0.5));
		HorizontalBox.Text = FormatScale(value);
		// Render options
		FoveatedCenterCheck.IsChecked = ReadBoolSetting(FoveatedCenterKey, fallback: false);
		StencilOuterEdgesCheck.IsChecked = ReadBoolSetting(StencilOuterEdgesKey, fallback: true);
		CropOuterEdgesCheck.IsChecked = ReadBoolSetting(CropOuterEdgesKey, fallback: true);
		HorizVisualMaskBothCheck.IsChecked = ReadBoolSetting(HorizVisualMaskBothKey, fallback: false);
		HorizOuterEyeMaskCheck.IsChecked = ReadBoolSetting(HorizOuterEyeMaskKey, fallback: false);
		HorizInnerEyeMaskCheck.IsChecked = ReadBoolSetting(HorizInnerEyeMaskKey, fallback: false);
		VertVisualMaskBothCheck.IsChecked = ReadBoolSetting(VertVisualMaskBothKey, fallback: false);
		VertTopMaskCheck.IsChecked = ReadBoolSetting(VertTopMaskKey, fallback: false);
		VertBottomMaskCheck.IsChecked = ReadBoolSetting(VertBottomMaskKey, fallback: false);
		// Gameplay Mode (config-only, not live runtime toggle)
		bool gp = ReadBoolSetting("gameplay_mode", fallback: true);
		if (XrGameplayModeCheck != null)  XrGameplayModeCheck.IsChecked  = gp;
		if (XrGameplayModeCheck2 != null) XrGameplayModeCheck2.IsChecked = gp;
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
			SaveGlobalSettings();
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
			SaveGlobalSettings();
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

	private bool _viewlabEnabled;

	private void EnabledCard_Click(object sender, MouseButtonEventArgs e)
	{
		_viewlabEnabled = !_viewlabEnabled;
		if (!_loading)
		{
			SaveGlobalSettings();
		}
		UpdateEnabledBadge();
	}

	private void UpdateEnabledBadge()
	{
		Color border = _viewlabEnabled ? Color.FromRgb(0x2A, 0x6A, 0x2A) : Color.FromRgb(0xC9, 0x00, 0x12);
		Color bg     = _viewlabEnabled ? Color.FromRgb(0x0A, 0x1A, 0x0A) : Color.FromRgb(0x2A, 0x00, 0x08);
		Color fg     = _viewlabEnabled ? Color.FromRgb(0x4D, 0xFF, 0x88) : Color.FromRgb(0xC9, 0x00, 0x12);
		EnabledBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(border, TimeSpan.FromSeconds(0.25)));
		EnabledBgBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(bg, TimeSpan.FromSeconds(0.25)));
		EnabledStatusFg.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(fg, TimeSpan.FromSeconds(0.25)));
		EnabledStatusText.Text = _viewlabEnabled ? "VIEWLAB ENABLED" : "VIEWLAB DISABLED";
	}

private void ExperimentalCheck_Changed(object sender, RoutedEventArgs e)
	{
		if (_loading) return;
		SaveExperimentalSettings();
		StatusText.Text = "Render options saved. Restart the OpenXR game.";
	}

	private void SaveExperimentalSettings()
	{
		Directory.CreateDirectory(ConfigDirectory);
		WritePrivateProfileString("Settings", FoveatedCenterKey, FoveatedCenterCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", StencilOuterEdgesKey, StencilOuterEdgesCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CropOuterEdgesKey, CropOuterEdgesCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", HorizVisualMaskBothKey, HorizVisualMaskBothCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", HorizOuterEyeMaskKey, HorizOuterEyeMaskCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", HorizInnerEyeMaskKey, HorizInnerEyeMaskCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", VertVisualMaskBothKey, VertVisualMaskBothCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", VertTopMaskKey, VertTopMaskCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", VertBottomMaskKey, VertBottomMaskCheck.IsChecked == true ? "1" : "0", ConfigPath);
	}

	private void ReShadeMenuSetting_Changed(object sender, RoutedEventArgs e) { }

	private void SaveReShadeMenuSettings() { }

	private void XrHmdMenuReposition_Click(object sender, RoutedEventArgs e)
	{
		StatusText.Text = "Reposition: not yet implemented.";
	}

	private void Xr3dMenuReposition_Click(object sender, RoutedEventArgs e)
	{
		StatusText.Text = "Reposition: not yet implemented.";
	}

	private void VrDesktopDupReposition_Click(object sender, RoutedEventArgs e)
	{
		StatusText.Text = "Reposition: not yet implemented.";
	}

	private void LogToFile(string msg)
	{
		try
		{
			var logDir = Path.Combine(ConfigDirectory, "Logs");
			Directory.CreateDirectory(logDir);
			File.AppendAllText(Path.Combine(logDir, "ViewLab.log"),
				$"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
		}
		catch { }
	}

	private void XrPollTimer_Tick(object sender, EventArgs e)
	{
		if (!_xrControl.Connected)
		{
			if (_xrControl.TryConnect())
			{
				LogToFile("XR control connected");
				ApplySavedXrLaunchMode();
				XrSyncToUI();
			}
		}
		else
		{
			if (!_xrControl.CheckMappingExists())
			{
				_xrLaunchModeApplied = false;
				_xrControl.Disconnect();
			}
			else
			{
				ApplySavedXrLaunchMode();
				XrSyncToUI();
			}
		}
	}

	private void ApplySavedXrLaunchMode()
	{
		if (_xrLaunchModeApplied || !_xrControl.Connected) return;
		bool gameplay = ReadBoolSetting("gameplay_mode", fallback: true);
		var block = _xrControl.ReadBlock();
		block.xr_mode = gameplay ? 0u : 1u;
		_xrControl.WriteBlock(ref block);
		_xrLaunchModeApplied = true;
		LogToFile($"Applied launch xr_mode={(gameplay ? 0 : 1)}");
	}

	private void XrGameplayMode_Changed(object sender, RoutedEventArgs e)
	{
		if (_loading) return;
		bool gameplay = GetXrGameplayMode();
		WritePrivateProfileString("Settings", "gameplay_mode", gameplay ? "1" : "0", ConfigPath);
		LogToFile($"Gameplay mode checkbox changed → saved gameplay_mode={(gameplay ? 1 : 0)}");
		_xrLaunchModeApplied = false;
	}

	private void XrMenuEnabled_Changed(object sender, RoutedEventArgs e)
	{
		var on = GetXrMenuVisible();
		if (_xrControl.Connected)
		{
			var block = _xrControl.ReadBlock();
			block.menu_visible = on ? 1u : 0u;
			_xrControl.WriteBlock(ref block);
		}
		var hwnd = FindWindowW("ReShadeVRPreview", null);
		if (hwnd != IntPtr.Zero)
			ShowWindow(hwnd, on ? 1 : 0);
	}

	private void XrControl_Changed(object sender, RoutedEventArgs e)
	{
		if (!_xrControl.Connected) return;
		var block = _xrControl.ReadBlock();
		block.win_headless      = GetXrHeadless()      ? 1u : 0u;
		block.win_always_on_top = GetXrTopmost()       ? 1u : 0u;
		_xrControl.WriteBlock(ref block);
	}


	private DateTime _vrQuadPopupClosedAt = DateTime.MinValue;
	private static readonly string QuadTransformIni =
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ReShade", "openxr_quad_transform.ini");

	private void VrQuadButton_Click(object sender, RoutedEventArgs e)
	{
		if ((DateTime.UtcNow - _vrQuadPopupClosedAt).TotalMilliseconds < 200)
			return;

		// Load current values from block into sliders
		if (_xrControl.Connected)
		{
			var b = _xrControl.ReadBlock();
			double rot = 2.0 * Math.Atan2(b.quad_quat_y, b.quad_quat_w) * 180.0 / Math.PI;
			VrDistSlider.Value   = float.IsNaN(b.quad_pos_z) ? 1.5 : b.quad_pos_z;
			VrWidthSlider.Value  = b.quad_width;
			VrHeightSlider.Value = b.quad_height;
			VrRotSlider.Value    = rot;
			VrAlphaSlider.Value  = b.quad_alpha;
		}

		VrQuadPopup.PlacementTarget = (UIElement)sender;
		VrQuadPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
		VrQuadPopup.IsOpen = true;
	}

	private void VrQuadDir_Click(object sender, RoutedEventArgs e)
	{
		if (!_xrControl.Connected || sender is not Button btn || btn.Tag is not string tag)
			return;
		var parts = tag.Split(',');
		if (parts.Length != 2) return;
		float dx = float.Parse(parts[0]) * 0.02f;
		float dy = float.Parse(parts[1]) * 0.02f;
		var block = _xrControl.ReadBlock();
		if (float.IsNaN(block.quad_pos_z)) { block.quad_pos_z = 1.5f; block.quad_pos_x = 0; block.quad_pos_y = 0; }
		if (dx == 0 && dy == 0) { block.quad_pos_x = 0; block.quad_pos_y = 0; }
		else { block.quad_pos_x += dx; block.quad_pos_y += dy; }
		_xrControl.WriteBlock(ref block);
	}

	private void VrQuadSlider_Changed(object sender, RoutedEventArgs e)
	{
		if (!_xrControl.Connected) return;
		var block = _xrControl.ReadBlock();
		if (float.IsNaN(block.quad_pos_z)) { block.quad_pos_z = 1.5f; block.quad_pos_x = 0; block.quad_pos_y = 0; }
		double scale = VrScaleSlider.Value;
		block.quad_pos_z   = (float)VrDistSlider.Value;
		block.quad_width   = (float)(VrWidthSlider.Value * scale);
		block.quad_height  = (float)(VrHeightSlider.Value * scale);
		block.quad_alpha   = (float)VrAlphaSlider.Value;
		double rad = VrRotSlider.Value * Math.PI / 180.0;
		block.quad_quat_y  = (float)Math.Sin(rad * 0.5);
		block.quad_quat_w  = (float)Math.Cos(rad * 0.5);
		_xrControl.WriteBlock(ref block);
		VrDistVal.Text    = VrDistSlider.Value.ToString("F2");
		VrWidthVal.Text   = VrWidthSlider.Value.ToString("F2");
		VrHeightVal.Text  = VrHeightSlider.Value.ToString("F2");
		VrScaleVal.Text   = VrScaleSlider.Value.ToString("F2");
		VrRotVal.Text     = VrRotSlider.Value.ToString("F1");
		VrAlphaVal.Text   = VrAlphaSlider.Value.ToString("F2");
	}

	private void VrQuadReset_Click(object sender, RoutedEventArgs e)
	{
		// Restore saved defaults from INI file (or in-memory fallback)
		var sb = new StringBuilder(32);
		float ReadIniFloat(string key, float def)
		{
			sb.Clear();
			GetPrivateProfileString("QuadTransform", key, "", sb, 32, QuadTransformIni);
			return sb.Length > 0 ? float.Parse(sb.ToString(), CultureInfo.InvariantCulture) : def;
		}
		float rx = ReadIniFloat("pos_x", 0), ry = ReadIniFloat("pos_y", 0), rz = ReadIniFloat("pos_z", 1.5f);
		float rw = ReadIniFloat("width", 0.5f), rh = ReadIniFloat("height", 0.5f);
		float rr = 2.0f * (float)Math.Atan2(ReadIniFloat("quat_y", 0), ReadIniFloat("quat_w", 1)) * 180.0f / (float)Math.PI;
		float ra = ReadIniFloat("alpha", 1);

		VrDistSlider.Value   = rz;
		VrWidthSlider.Value  = rw;
		VrHeightSlider.Value = rh;
		VrScaleSlider.Value  = 1;
		VrRotSlider.Value    = rr;
		VrAlphaSlider.Value  = ra;

		if (_xrControl.Connected)
		{
			var block = _xrControl.ReadBlock();
			block.quad_pos_x = rx; block.quad_pos_y = ry; block.quad_pos_z = rz;
			block.quad_width = rw; block.quad_height = rh;
			double rad = rr * Math.PI / 180.0;
			block.quad_quat_y = (float)Math.Sin(rad * 0.5); block.quad_quat_w = (float)Math.Cos(rad * 0.5);
			block.quad_alpha = ra;
			_xrControl.WriteBlock(ref block);
		}
	}

	private void VrQuadSaveDefault_Click(object sender, RoutedEventArgs e)
	{
		if (!_xrControl.Connected) return;
		var block = _xrControl.ReadBlock();
		WritePrivateProfileString("QuadTransform", "pos_x",  block.quad_pos_x.ToString("F6", CultureInfo.InvariantCulture), QuadTransformIni);
		WritePrivateProfileString("QuadTransform", "pos_y",  block.quad_pos_y.ToString("F6", CultureInfo.InvariantCulture), QuadTransformIni);
		WritePrivateProfileString("QuadTransform", "pos_z",  block.quad_pos_z.ToString("F6", CultureInfo.InvariantCulture), QuadTransformIni);
		WritePrivateProfileString("QuadTransform", "width",  block.quad_width.ToString("F6", CultureInfo.InvariantCulture), QuadTransformIni);
		WritePrivateProfileString("QuadTransform", "height", block.quad_height.ToString("F6", CultureInfo.InvariantCulture), QuadTransformIni);
		WritePrivateProfileString("QuadTransform", "quat_y", block.quad_quat_y.ToString("F6", CultureInfo.InvariantCulture), QuadTransformIni);
		WritePrivateProfileString("QuadTransform", "quat_w", block.quad_quat_w.ToString("F6", CultureInfo.InvariantCulture), QuadTransformIni);
		WritePrivateProfileString("QuadTransform", "alpha",  block.quad_alpha.ToString("F6", CultureInfo.InvariantCulture), QuadTransformIni);
	}

	private void VrQuadClose_Click(object sender, RoutedEventArgs e)
	{
		VrQuadPopup.IsOpen = false;
	}

	private void VrQuadPopup_Closed(object sender, EventArgs e)
	{
		_vrQuadPopupClosedAt = DateTime.UtcNow;
	}

	private bool GetXrGameplayMode()
	{
		if (ReShadeOpenXRCard.Visibility == Visibility.Visible && XrGameplayModeCheck != null)
			return XrGameplayModeCheck.IsChecked == true;
		if (ReShadeOpenXRCard2.Visibility == Visibility.Visible && XrGameplayModeCheck2 != null)
			return XrGameplayModeCheck2.IsChecked == true;
		return false;
	}

	private bool GetXrMenuVisible()
	{
		if (ReShadeOpenXRCard.Visibility == Visibility.Visible && XrMenuEnabledCheck != null)
			return XrMenuEnabledCheck.IsChecked == true;
		if (ReShadeOpenXRCard2.Visibility == Visibility.Visible && XrMenuEnabledCheck2 != null)
			return XrMenuEnabledCheck2.IsChecked == true;
		return false;
	}

	private bool GetXrHeadless()
	{
		if (ReShadeOpenXRCard.Visibility == Visibility.Visible && XrWinHeadlessCheck != null)
			return XrWinHeadlessCheck.IsChecked == true;
		if (ReShadeOpenXRCard2.Visibility == Visibility.Visible && XrWinHeadlessCheck2 != null)
			return XrWinHeadlessCheck2.IsChecked == true;
		return false;
	}

	private bool GetXrTopmost()
	{
		if (ReShadeOpenXRCard.Visibility == Visibility.Visible && XrWinTopmostCheck != null)
			return XrWinTopmostCheck.IsChecked == true;
		if (ReShadeOpenXRCard2.Visibility == Visibility.Visible && XrWinTopmostCheck2 != null)
			return XrWinTopmostCheck2.IsChecked == true;
		return false;
	}

	private void XrSyncToUI()
	{
		if (!_xrControl.Connected) return;
		var block = _xrControl.ReadBlock();
		if (ReShadeOpenXRCard.Visibility == Visibility.Visible)
		{
			if (XrMenuEnabledCheck != null)   XrMenuEnabledCheck.IsChecked     = block.menu_visible == 1;
			if (XrWinHeadlessCheck != null)   XrWinHeadlessCheck.IsChecked     = block.win_headless == 1;
			if (XrWinTopmostCheck != null)    XrWinTopmostCheck.IsChecked      = block.win_always_on_top == 1;
		}
		if (ReShadeOpenXRCard2.Visibility == Visibility.Visible)
		{
			if (XrMenuEnabledCheck2 != null)  XrMenuEnabledCheck2.IsChecked    = block.menu_visible == 1;
			if (XrWinHeadlessCheck2 != null)  XrWinHeadlessCheck2.IsChecked    = block.win_headless == 1;
			if (XrWinTopmostCheck2 != null)   XrWinTopmostCheck2.IsChecked     = block.win_always_on_top == 1;
		}

		// Popup sliders sync when open
		if (VrQuadPopup.IsOpen)
		{
			double rot = 2.0 * Math.Atan2(block.quad_quat_y, block.quad_quat_w) * 180.0 / Math.PI;
			VrDistSlider.Value   = float.IsNaN(block.quad_pos_z) ? 1.5 : block.quad_pos_z;
			VrWidthSlider.Value  = block.quad_width;
			VrHeightSlider.Value = block.quad_height;
			VrRotSlider.Value    = rot;
			VrAlphaSlider.Value  = block.quad_alpha;
		}

		var hwnd = FindWindowW("ReShadeVRPreview", null);
		if (hwnd != IntPtr.Zero)
			ShowWindow(hwnd, block.menu_visible == 1 ? 1 : 0);
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
		bool valueOrDefault2 = _viewlabEnabled;
		Directory.CreateDirectory(ConfigDirectory);
		WritePrivateProfileString("Settings", "enabled", valueOrDefault2 ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", "split_mode", valueOrDefault ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", "total_render_height", FormatScale(value), ConfigPath);
		WritePrivateProfileString("Settings", "total_share", null, ConfigPath);
		WritePrivateProfileString("Settings", "vertical_tangent", null, ConfigPath);
		WritePrivateProfileString("Settings", "top_tangent", FormatScale(value2), ConfigPath);
		WritePrivateProfileString("Settings", "bottom_tangent", FormatScale(value3), ConfigPath);
		WritePrivateProfileString("Settings", "horizontal_render_width", FormatScale(value4), ConfigPath);
		SaveReShadeMenuSettings();
		SaveExperimentalSettings();
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
		foreach (string valueName in registryKey.GetValueNames())
		{
			if (valueName.Contains("XR_APILAYER_cooooked_xrviewlab.json", StringComparison.OrdinalIgnoreCase) &&
				!valueName.Equals(ManifestPath, StringComparison.OrdinalIgnoreCase))
			{
				registryKey.DeleteValue(valueName, throwOnMissingValue: false);
			}
		}
		registryKey.SetValue(ManifestPath, (!enabled) ? 1 : 0, RegistryValueKind.DWord);
	}

	private static readonly HashSet<string> AppKeyBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"steam", "steam.exe", "steamvr", "steamtours", "steamtours.exe",
		"racelab_vr", "racelab_vr.exe", "racelab", "vrmonitor", "vrmonitor.exe",
		"vrserver", "vrserver.exe", "vrdashboard", "vrdashboard.exe",
		"openvr_overlay", "xr_composition_layer_override",
		"pivotpoint", "pivotpoint.exe",
	};

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
				string exeName = Path.GetFileNameWithoutExtension(text);
				if (AppKeyBlacklist.Contains(text) || AppKeyBlacklist.Contains(exeName))
				{
					continue;
				}
				using RegistryKey registryKey2 = registryKey.OpenSubKey(text);
				if (registryKey2 != null)
				{
					AppProfile item = ReadAppProfile(text, registryKey2);
					_apps.Add(item);
				}
			}
		}
		_loading = false;
		if (AppsEmptyText != null)
		{
			AppsEmptyText.Visibility = _apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		}
	}

	private AppProfile ReadAppProfile(string keyName, RegistryKey appKey)
	{
		double num = ReadScaleSetting("total_render_height", ReadScaleSetting("total_share", ReadScaleSetting("vertical_tangent", 0.18)));
		double fallback = ReadScaleSetting("top_tangent", num * 0.5);
		double fallback2 = ReadScaleSetting("bottom_tangent", num * 0.5);
		double fallback3 = ReadScaleSetting("horizontal_render_width", 0.8);
		string rawDisplayName = Convert.ToString(appKey.GetValue("display_name", keyName)) ?? keyName;
		string text = Convert.ToString(appKey.GetValue("module", "")) ?? "";
		bool isOpenComposite = rawDisplayName.StartsWith("OpenComposite_", StringComparison.OrdinalIgnoreCase);
		string xrType = isOpenComposite ? "OpenVR" : "OpenXR";
		string text2 = CleanAppDisplayName(rawDisplayName, keyName, text);
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
			XrType = xrType,
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

	private void AppEnabled_Changed(object sender, RoutedEventArgs e)
	{
		if (_loading || !(sender is CheckBox { DataContext: AppProfile dataContext }))
		{
			return;
		}
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(AppRegistryRoot + "\\" + dataContext.Key, writable: true) ?? throw new InvalidOperationException("Could not write app profile.");
		registryKey.SetValue("app_enabled", dataContext.AppEnabled ? 1 : 0, RegistryValueKind.DWord);
		StatusText.Text = (dataContext.AppEnabled ? ("Layer enabled for " + dataContext.DisplayName + ". Restart the OpenXR game.") : ("Layer disabled for " + dataContext.DisplayName + ". Restart the OpenXR game."));
	}

	private void ReloadApps_Click(object sender, RoutedEventArgs e)
	{
		LoadAppProfiles();
		StatusText.Text = "App list reloaded.";
	}


	private void AppsGrid_DoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (!(AppsGrid.SelectedItem is AppProfile appProfile))
		{
			return;
		}

		if (!appProfile.AppEnabled)
		{
			StatusText.Text = "Enable the app first, then double-click to set custom values.";
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
			StatusText.Text = "Saved app profile. Restart the OpenXR game.";
		}
	}


	private void ResetApp_Click(object sender, RoutedEventArgs e)
	{
		if (!(AppsGrid.SelectedItem is AppProfile appProfile))
		{
			StatusText.Text = "Select an app to reset its profile.";
			return;
		}
		ResetAppCustomProfile(appProfile);
		appProfile.ProfileEnabled = false;
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
