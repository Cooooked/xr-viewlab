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
using System.Text.RegularExpressions;
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
	private const string MaskEnabledKey = "mask_enabled";
	private const string OverlayForceDirectKey = "overlay_force_direct";
	private const string MaskSizeKey = "mask_size";
	private const string MaskRoundedKey = "mask_rounded";
	private const string MaskCornerKey = "mask_corner";
	private const string MaskOffsetYKey = "mask_offset_y";
	private const string MaskTopBiasKey = "mask_top_bias";
	private const string MaskBottomBiasKey = "mask_bottom_bias";
	private const string MaskLeftBiasKey = "mask_left_bias";
	private const string MaskRightBiasKey = "mask_right_bias";
	private const string MaskTopCurveKey = "mask_top_curve";
	private const string MaskBottomCurveKey = "mask_bottom_curve";
	private const string MaskOuterApexYKey = "mask_outer_apex_y";
	private const string MaskInnerLowerYKey = "mask_inner_lower_y";
	private const string MaskInnerBridgeWidthKey = "mask_inner_bridge_width";
	private const string MaskInnerBridgeRiseKey = "mask_inner_bridge_rise";
	private const string MaskInnerBridgePeakXKey = "mask_inner_bridge_peak_x";
	private const string MaskInnerBridgeSteepnessKey = "mask_inner_bridge_steepness";
	private const string HorizVisualMaskBothKey = "horizontal_visual_mask_only";
	private const string HorizOuterEyeMaskKey = "horizontal_outer_eye_mask";
	private const string HorizInnerEyeMaskKey = "horizontal_inner_eye_mask";
	private const string VertVisualMaskBothKey = "visual_mask_only";
	private const string VertTopMaskKey = "vertical_top_mask_only";
	private const string VertBottomMaskKey = "vertical_bottom_mask_only";
	private const string CalibrationGridKey = "calibration_grid";
	private const string CalibrationRulerKey = "calibration_ruler";
	private const string CalibrationGratingsKey = "calibration_gratings";
	private const string CalibrationBarsKey = "calibration_bars";
	private const string CalibrationBeaconKey = "calibration_beacon";
	private const string CalibrationEdgeProbesKey = "calibration_edge_probes";
	private const string CalibrationCheckerboardsKey = "calibration_checkerboards";
	private const string CalibrationZonePlateKey = "calibration_zone_plate";
	private const string CalibrationClippingStepsKey = "calibration_clipping_steps";
	private const string CalibrationMotionStripKey = "calibration_motion_strip";
	private const string HudEnabledKey = "hud_enabled";
	private const string HudTraceEnabledKey = "hud_trace_enabled";
	private const string HudAnchorXKey = "hud_anchor_x";
	private const string HudAnchorYKey = "hud_anchor_y";
	private const string HudScaleKey = "hud_scale";
	private const string HudTraceSensitivityKey = "hud_trace_sensitivity_ms";
	private const string HudTraceXKey = "hud_trace_x";
	private const string HudTraceYKey = "hud_trace_y";
	private const string HudTraceScaleKey = "hud_trace_scale";
	private const string HudTraceWidthKey = "hud_trace_width";
	private const string HudTraceHistoryKey = "hud_trace_history";
	private const string HudTraceVisibilityKey = "hud_trace_visibility_mode";
	private const string HudAlarmOnlyKey = "hud_alarm_only";
	private const string HudAlarmHoldKey = "hud_alarm_hold_ms";
	private const string HudSafeMarginKey = "hud_safe_margin";
	private const string HudClampKey = "hud_clamp_to_visible";
	private const string HudGraphModeKey = "hud_graph_mode";
	private const string TelemetrySettingsVersionKey = "telemetry_settings_version";
	private static readonly string[] HudWidgetIds = { "cpu", "gpu", "app", "vr", "cpu_peak", "cpu_frequency", "ram", "commit", "vram", "sys", "fps", "frame_interval" };
	private static readonly string[] HudGraphChannelIds = { "frame_interval", "fps", "budget_deviation", "app_work", "wait_duration", "submit_duration", "display_period" };

	// Feature 2: crosshair
	private const string CrosshairEnabledKey = "crosshair_enabled";
	private const string CrosshairDotKey = "crosshair_dot";
	private const string CrosshairOutlineKey = "crosshair_outline";
	private const string CrosshairTStyleKey = "crosshair_tstyle";
	private const string CrosshairSizeKey = "crosshair_size";
	private const string CrosshairGapKey = "crosshair_gap";
	private const string CrosshairThicknessKey = "crosshair_thickness";
	private const string CrosshairOutlineThicknessKey = "crosshair_outline_thickness";
	private const string CrosshairAlphaKey = "crosshair_alpha";
	private const string CrosshairScaleKey = "crosshair_scale";
	private const string CrosshairColorKey = "crosshair_color";
	private const string CrosshairOffsetXKey = "crosshair_offset_x";
	private const string CrosshairOffsetYKey = "crosshair_offset_y";

	// Feature 3: notifications
	private const string NotifyEnabledKey = "notify_enabled";
	private const string NotifyXKey = "notify_x";
	private const string NotifyYKey = "notify_y";
	private const string NotifyScaleKey = "notify_scale";
	private const string NotifyOpacityKey = "notify_opacity";
	private const string NotifyDurationKey = "notify_duration_ms";
	private const string NotifyMaxKey = "notify_max_visible";
	private const string NotifyPrivacyKey = "notify_privacy";
	private const string NotifyShowIconKey = "notify_show_icon";
	private const string NotifyShowImageKey = "notify_show_image";
	private const string NotifyAllowlistModeKey = "notify_allowlist_mode";
	private const string NotifyFiltersKey = "notify_app_filters";

	// iRacing provider and generic racing presentation
	private const string IRacingEnabledKey = "iracing_enabled";
	private const string IRacingLapPopupKey = "iracing_lap_popup";
	private const string IRacingSpotterGlowKey = "iracing_spotter_glow";
	private const string IRacingFlagBorderKey = "iracing_flag_border";
	private const string IRacingSpotterWidthKey = "iracing_spotter_width";
	private const string IRacingSpotterStrengthKey = "iracing_spotter_strength";
	private const string IRacingSpotterOpacityKey = "iracing_spotter_opacity";
	private const string IRacingSpotterFadeKey = "iracing_spotter_fade";
	private const string IRacingSpotterColorKey = "iracing_spotter_color";
	private const string IRacingFlagWidthKey = "iracing_flag_width";
	private const string IRacingFlagOpacityKey = "iracing_flag_opacity";
	private const string IRacingLapDurationKey = "iracing_lap_duration_ms";

	// ReShade MENU � OpenXR
	private const string XrHmdMenuKey = "reshade_xr_hmd_menu";
	private const string Xr3dMenuKey = "reshade_xr_3d_menu";
	private const string XrHeadLockedKey = "reshade_xr_head_locked";
	private const string Xr3dCursorKey = "reshade_xr_3d_cursor";
	private const string XrOxrtkColorsKey = "reshade_xr_oxrtk_colors";

	// ReShade MENU � OpenVR
	private const string VrDesktopDupKey = "reshade_vr_desktop_dup";
	private const string VrAlwaysOnTopKey = "reshade_vr_always_on_top";
	private const string VrLockPositionKey = "reshade_vr_lock_position";
	private const string Vr3dMenuKey = "reshade_vr_3d_menu";
	private const string VrHeadLockedKey = "reshade_vr_head_locked";

	private readonly ObservableCollection<AppProfile> _apps = new ObservableCollection<AppProfile>();
	private readonly ObservableCollection<HudWidgetOption> _hudWidgets = new()
	{
		new() { MetricId=0, Id="cpu", Label="CPU — total utilisation", Provider="Windows / GetSystemTimes", Unit="%", ToolTip="Total machine CPU utilisation; sampled every 250 ms." },
		new() { MetricId=1, Id="gpu", Label="GPU — 3D utilisation", Provider="Windows PDH / adapter LUID", Unit="%", ToolTip="3D-engine utilisation for the render adapter." },
		new() { MetricId=2, Id="app", Label="APP — application workload", Provider="OpenXR timing", Unit="%", ToolTip="Application work against the cadence-aware budget." },
		new() { MetricId=3, Id="vr", Label="VR — cadence", Provider="OpenXR timing", Unit="ms", ToolTip="Wait-to-wait interval judged against the display period." },
		new() { MetricId=4, Id="cpu_peak", Label="PEAK — busiest logical CPU", Provider="Windows PDH", Unit="%", ToolTip="Smoothed busiest logical processor." },
		new() { MetricId=5, Id="cpu_frequency", Label="CLK — CPU reported clock", Provider="Windows power API", Unit="MHz", ToolTip="Average Windows CurrentMhz; not residency-derived effective clock." },
		new() { MetricId=6, Id="ram", Label="RAM — physical memory", Provider="GlobalMemoryStatusEx", Unit="%", ToolTip="Physical memory pressure." },
		new() { MetricId=7, Id="commit", Label="CMT — committed memory", Provider="GetPerformanceInfo", Unit="%", ToolTip="Commit charge relative to commit limit." },
		new() { MetricId=8, Id="vram", Label="VRAM — budget pressure", Provider="DXGI 1.4", Unit="%", ToolTip="Local video-memory use relative to the OS budget." },
		new() { MetricId=9, Id="sys", Label="SYS — remaining headroom", Provider="ViewLab composite", Unit="%", ToolTip="Remaining capacity after the strongest valid pressure; higher is healthier." },
		new() { MetricId=10, Id="fps", Label="FPS — effective cadence", Provider="OpenXR timing", Unit="fps", ToolTip="1000 divided by application frame interval." },
		new() { MetricId=11, Id="frame_interval", Label="FT — frame interval", Provider="OpenXR timing", Unit="ms", ToolTip="Rolling application frame interval." }
	};

	private bool _loading = true;

	private bool _xrLaunchModeApplied;

	private bool _syncingControls;

	private bool _optionsInRightPanel;

	private string? _availableUpdateTag;

	private readonly ReShadeControlService _xrControl = new();
	private readonly LiveStateService _liveState = new();
	private readonly TelemetryConfigService _telemetryConfig = new();
	private readonly CrosshairSettings _crosshair = new();
	private readonly NotificationBrokerClient _notificationBroker = new();
	private bool _boundaryDragActive;
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
		HudWidgetList.ItemsSource = _hudWidgets;
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
		CalibrationPopup.Closed += (_, _) => _calibrationPopupClosedAt = DateTime.UtcNow;
		OverlaysPopup.Closed += (_, _) => _overlaysPopupClosedAt = DateTime.UtcNow;
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
	private DateTime _calibrationPopupClosedAt = DateTime.MinValue;
	private DateTime _overlaysPopupClosedAt = DateTime.MinValue;

	private void OverlaysButton_Click(object sender, RoutedEventArgs e)
	{
		if ((DateTime.UtcNow - _overlaysPopupClosedAt).TotalMilliseconds < 200)
			return;
		OverlaysPopup.PlacementTarget = (UIElement)sender;
		OverlaysPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
		OverlaysPopup.IsOpen = true;
	}

	private void VisualMasksButton_Click(object sender, RoutedEventArgs e)
	{
		// StaysOpen=False closes the popup on MouseDown before this Click fires.
		// If it closed within the last 200ms the click was the close � don't reopen.
		if ((DateTime.UtcNow - _visualMasksPopupClosedAt).TotalMilliseconds < 200)
			return;
		VisualMasksPopup.PlacementTarget = (UIElement)sender;
		VisualMasksPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
		VisualMasksPopup.IsOpen = true;
	}

	private void CalibrationButton_Click(object sender, RoutedEventArgs e)
	{
		if ((DateTime.UtcNow - _calibrationPopupClosedAt).TotalMilliseconds < 200)
			return;
		CalibrationPopup.PlacementTarget = (UIElement)sender;
		CalibrationPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
		CalibrationPopup.IsOpen = true;
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
			StatusText.Text = "No log file found yet. Launch a VR game first.";
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
				StatusText.Text = "Warning: layer not registered. Try reinstalling ViewLab.";
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
			UpdateRelease? latest = await GetLatestReleaseAsync(client);
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
		try
		{
			StatusText.Text = "Checking for updates...";
			using HttpClient client = CreateGitHubClient();
			UpdateRelease? latest = await GetLatestReleaseAsync(client);
			if (latest == null)
			{
				StatusText.Text = "No update release found.";
				MessageBox.Show(this, "No ViewLab update release was found.", "ViewLab Updates", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			int comparison = CompareReleaseVersions(latest.TagName, CurrentVersion);
			if (comparison < 0)
			{
				StatusText.Text = "Running newer build than published release (" + CurrentVersion + ").";
				MessageBox.Show(this, "You are running a newer build than the latest published release.\n\nInstalled: " + CurrentVersion + "\nLatest: " + latest.TagName, "ViewLab Updates", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			if (comparison == 0)
			{
				StatusText.Text = "ViewLab is up to date (" + CurrentVersion + ").";
				MessageBox.Show(this, "You are up to date.\n\nInstalled: " + CurrentVersion + "\nLatest: " + latest.TagName, "ViewLab Updates", MessageBoxButton.OK, MessageBoxImage.Asterisk);
				return;
			}
			_availableUpdateTag = latest.TagName;
			MarkUpdateAvailable();
			if (MessageBox.Show(this, "Update detected: " + latest.TagName + "\n\nInstalled: " + CurrentVersion + "\n\nDownload and install now?\n\nViewLab will close after the installer starts.", "ViewLab Update", MessageBoxButton.YesNo, MessageBoxImage.Asterisk) != MessageBoxResult.Yes)
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
			MessageBox.Show(this, "Update check failed.\n\n" + ex.Message, "ViewLab Updates", MessageBoxButton.OK, MessageBoxImage.Exclamation);
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
			string? text2 = null;
			string? text3 = null;
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
		string text = Path.Combine(Path.GetTempPath(), "ViewLab Updates");
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
		if (!Version.TryParse(text, out Version? result))
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
		SetColumnWidth(AppsNameColumn, ReadColumnWidth("apps_name_column_width", 180.0, 80.0, 500.0));
		SetColumnWidth(AppsValueColumn, ReadColumnWidth("apps_value_column_width", 62.0, 36.0, 260.0));
		SetColumnWidth(AppsXrTypeColumn, ReadColumnWidth("apps_xr_column_width", 42.0, 28.0, 160.0));
		SetColumnWidth(AppsStatusColumn, ReadColumnWidth("apps_status_column_width", 66.0, 52.0, 180.0));
	}

	private void RegisterColumnWidthPersistence()
	{
		RegisterColumnWidthPersistence(AppsCheckColumn, "apps_check_column_width");
		RegisterColumnWidthPersistence(AppsNameColumn, "apps_name_column_width");
		RegisterColumnWidthPersistence(AppsValueColumn, "apps_value_column_width");
		RegisterColumnWidthPersistence(AppsXrTypeColumn, "apps_xr_column_width");
		RegisterColumnWidthPersistence(AppsStatusColumn, "apps_status_column_width");
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
		if (MainColumn != null && SideColumn != null && WideGapColumn != null && WideGapColumn2 != null && RightColumn != null && EnabledCard != null && SidePanel != null && RightColumnPanel != null && LeftColumnPanel != null && RenderLabelColumn != null && RenderValueColumn != null && RenderHintColumn != null && RenderHintGapColumn != null && OptionsGapRow != null)
		{
			if (w < 0) w = base.ActualWidth;
			// These thresholds are client widths, not nominal window widths. Three card columns
			// need substantially more room than the old 900px cutover allowed.
			bool compact  = w > 0.0 && w < 280.0;   // mini: sliders collapse only when genuinely narrow
			bool twoCol   = w >= 720.0;              // medium: two usable card columns
			bool threeCol = w >= 1200.0;             // large: three full-width card columns

			MainColumn.Width     = new GridLength(1.0, GridUnitType.Star);
			WideGapColumn.Width  = twoCol    ? new GridLength(14.0) : new GridLength(0.0);
			SideColumn.Width     = twoCol    ? new GridLength(1.0, GridUnitType.Star) : new GridLength(0.0);
			WideGapColumn2.Width = threeCol  ? new GridLength(14.0) : new GridLength(0.0);
			RightColumn.Width    = threeCol  ? new GridLength(1.0, GridUnitType.Star) : new GridLength(0.0);

			// EnabledCard and RenderCard live in LeftColumnPanel (always left col) � see XAML.
			// LeftColumnPanel is a single top-aligned StackPanel spanning the grid rows, so the
			// tall RowSpan side panels (cols 2/4) cannot inject blank space between the left cards.

			// In 3-col mode, move OptionsHeader/OptionsCard into RightColumnPanel.
			// In 1/2-col mode they live in LeftColumnPanel under RenderCard.
			if (threeCol && !_optionsInRightPanel)
			{
				LeftColumnPanel.Children.Remove(OptionsHeader);
				LeftColumnPanel.Children.Remove(OptionsCard);
				RightColumnPanel.Children.Insert(0, OptionsCard);
				RightColumnPanel.Children.Insert(0, OptionsHeader);
				_optionsInRightPanel = true;
			}
			else if (!threeCol && _optionsInRightPanel)
			{
				RightColumnPanel.Children.Remove(OptionsHeader);
				RightColumnPanel.Children.Remove(OptionsCard);
				LeftColumnPanel.Children.Add(OptionsHeader);
				LeftColumnPanel.Children.Add(OptionsCard);
				_optionsInRightPanel = false;
			}
			RightColumnPanel.Visibility = threeCol ? Visibility.Visible   : Visibility.Collapsed;
			Grid.SetColumn(RightColumnPanel, 4);
			Grid.SetRow(RightColumnPanel, 0);
			Grid.SetRowSpan(RightColumnPanel, 9);
			OptionsHeader.Visibility    = Visibility.Collapsed;
			OptionsCard.Visibility      = Visibility.Visible;
			OptionsGapRow.Height        = threeCol ? new GridLength(0) : new GridLength(10);

			// SidePanel (apps): col 2 in two/three-col, col 0 row 8 in single
			Grid.SetColumn(SidePanel, twoCol ? 2 : 0);
			Grid.SetRow(SidePanel, twoCol ? 0 : 8);
			Grid.SetRowSpan(SidePanel, twoCol ? 9 : 1);

			// ReShade cards in SidePanel: visible in single/two-col only
			ReShadeOpenXRHeader.Visibility = Visibility.Collapsed;
			ReShadeOpenXRCard.Visibility   = Visibility.Collapsed;
			ReShadeOpenXRHeader2.Visibility = Visibility.Collapsed;
			ReShadeOpenXRCard2.Visibility   = Visibility.Collapsed;
			ThanksText.Visibility          = threeCol ? Visibility.Collapsed : Visibility.Visible;
			ThanksTextRight.Visibility     = threeCol ? Visibility.Visible : Visibility.Collapsed;

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

	private static double ReadRangeSetting(string key, double fallback, double min, double max)
	{
		if (!double.TryParse(ReadSetting(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return fallback;
		}
		return Math.Clamp(result, min, max);
	}

	private static double ReadSignedScaleSetting(string key, double fallback)
	{
		if (!double.TryParse(ReadSetting(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return fallback;
		}
		return Math.Clamp(result, -1.0, 1.0);
	}

	private static string FormatScale(double value)
	{
		return value.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private static string FormatStorageScale(double value)
	{
		return value.ToString("0.########", CultureInfo.InvariantCulture);
	}

	private static string FormatPercent(double value)
	{
		double pct = Math.Clamp(value, 0.0, 1.0) * 100.0;
		return pct.ToString("0.#####", CultureInfo.InvariantCulture) + "%";
	}

	private static uint ToMillis(double value)
	{
		return (uint)Math.Round(Math.Clamp(value, 0.0, 1.0) * 1000.0);
	}

	private static uint ToMillis(double value, double max)
	{
		return (uint)Math.Round(Math.Clamp(value, 0.0, max) * 1000.0);
	}

	private static uint ToRenderScaleUnits(double value)
	{
		return (uint)Math.Round(Math.Clamp(value, 0.1, 3.0) * 1000000.0);
	}

	private static uint ToSignedMillis(double value)
	{
		return (uint)Math.Round((Math.Clamp(value, -1.0, 1.0) + 1.0) * 1000.0);
	}

	// 10000 distinguishes the extended Peak X range from existing 0..1000 profiles.
	private static uint ToPeakXMillis(double value)
	{
		return (uint)Math.Round(10000.0 + (Math.Clamp(value, -1.0, 2.0) + 1.0) * 1000.0);
	}

	private static uint ToRiseMillis(double value) => (uint)Math.Round(20000.0 + (Math.Clamp(value, -0.5, 1.0) + 0.5) * 1000.0);
	private static uint ToSteepMillis(double value) => (uint)Math.Round(30000.0 + (Math.Clamp(value, -1.0, 2.0) + 1.0) * 1000.0);

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

	private static double FromMillis(object? value, double fallback, double max)
	{
		if (value is int num && num >= 0 && num <= max * 1000.0)
		{
			return (double)num / 1000.0;
		}
		if (value is uint num2 && num2 <= max * 1000.0)
		{
			return (double)num2 / 1000.0;
		}
		return fallback;
	}

	private static double FromRenderScaleUnits(object? value, double fallback)
	{
		if (value is string text && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
		{
			return Math.Clamp(parsed, 0.1, 3.0);
		}
		if (value is int num && num > 0)
		{
			return Math.Clamp(num > 3000 ? num / 1000000.0 : num / 1000.0, 0.1, 3.0);
		}
		if (value is uint num2 && num2 > 0)
		{
			return Math.Clamp(num2 > 3000 ? num2 / 1000000.0 : num2 / 1000.0, 0.1, 3.0);
		}
		return fallback;
	}

	private static double FromSignedMillis(object? value, double fallback)
	{
		if (value is int num && num >= 0 && num <= 2000)
		{
			return ((double)num / 1000.0) - 1.0;
		}
		if (value is uint num2 && num2 <= 2000)
		{
			return ((double)num2 / 1000.0) - 1.0;
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

	private double CurrentVerticalCrop()
	{
		if (SplitCheck?.IsChecked == true &&
			TryReadTextBox(TopBox, out var top) &&
			TryReadTextBox(BottomBox, out var bottom))
		{
			return Math.Clamp(top + bottom, 0.01, 1.0);
		}
		return TryReadTextBox(TotalBox, out var total) ? Math.Clamp(total, 0.01, 1.0) : 1.0;
	}

	private double CurrentHorizontalCrop()
	{
		return TryReadTextBox(HorizontalBox, out var horizontal) ? Math.Clamp(horizontal, 0.01, 1.0) : 1.0;
	}

	private void SyncMaskEditorFromSliders()
	{
		if (MaskBeanEditor == null)
		{
			return;
		}
		MaskBeanEditor.Size = MaskSizeSlider?.Value ?? 1.0;
		MaskBeanEditor.Curve = MaskRoundnessSlider?.Value ?? 0.5;
		MaskBeanEditor.OffsetX = MaskOffsetXSlider?.Value ?? 0.0;
		MaskBeanEditor.OffsetY = MaskOffsetYSlider?.Value ?? 0.0;
		MaskBeanEditor.OuterApexY = MaskApexYSlider?.Value ?? 0.0;
		MaskBeanEditor.InnerLowerY = MaskInnerLowerSlider?.Value ?? 0.0;
		MaskBeanEditor.InnerBridgeWidth = MaskInnerBridgeSlider?.Value ?? 0.5;
		MaskBeanEditor.InnerBridgeRise = MaskInnerBridgeRiseSlider?.Value ?? 0.0;
		MaskBeanEditor.InnerBridgePeakX = MaskInnerBridgePeakXSlider?.Value ?? 0.5;
		MaskBeanEditor.InnerBridgeSteepness = MaskInnerBridgeSteepnessSlider?.Value ?? 0.5;
		MaskBeanEditor.OpenInnerPreview = true; // Stencil outer edges only is permanently enabled
		// Preview rect tracks the post-crop render area so the mask aspect is WYSIWYG.
		MaskBeanEditor.CropVertical = CurrentVerticalCrop();
		MaskBeanEditor.CropHorizontal = CurrentHorizontalCrop();
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
		// Mask (visor): absolute bounds, default 1.0 = no mask on that axis.
		MaskEnabledCheck.IsChecked = ReadBoolSetting(MaskEnabledKey, fallback: false);
		MaskSizeSlider.Value = ReadRangeSetting(MaskSizeKey, 1.0, 0.1, 1.0);
		MaskRoundedCheck.IsChecked = ReadBoolSetting(MaskRoundedKey, fallback: true);
		MaskVerticalBox.Text = FormatScale(ReadScaleSetting("mask_vertical", 1.0));
		MaskHorizontalBox.Text = FormatScale(ReadScaleSetting("mask_horizontal", 1.0));
		MaskRoundnessSlider.Value = 1.0 - ReadScaleSetting(MaskCornerKey, 0.5);

		MaskApexYSlider.Value = ReadRangeSetting(MaskOuterApexYKey, 0.0, -0.5, 0.5);
		MaskInnerLowerSlider.Value = ReadRangeSetting(MaskInnerLowerYKey, 0.0, 0.0, 0.666);
		MaskInnerBridgeSlider.Value = ReadRangeSetting(MaskInnerBridgeWidthKey, 0.5, 0.0, 1.0);
		MaskInnerBridgeRiseSlider.Value = ReadRangeSetting(MaskInnerBridgeRiseKey, 0.0, -0.5, 1.0);
		MaskInnerBridgePeakXSlider.Value = ReadRangeSetting(MaskInnerBridgePeakXKey, 0.5, -1.0, 2.0);
		MaskInnerBridgeSteepnessSlider.Value = ReadRangeSetting(MaskInnerBridgeSteepnessKey, 0.5, -1.0, 2.0);
		MaskOffsetXSlider.Value = 0.0;
		MaskOffsetYSlider.Value = 0.0;
		SyncMaskEditorFromSliders();
		// Render options: foveated center, stencil outer edges, and crop outer edges are permanently enabled.
		MaskBeanEditor.OpenInnerPreview = true;
		HorizVisualMaskBothCheck.IsChecked = ReadBoolSetting(HorizVisualMaskBothKey, fallback: false);
		HorizOuterEyeMaskCheck.IsChecked = ReadBoolSetting(HorizOuterEyeMaskKey, fallback: false);
		HorizInnerEyeMaskCheck.IsChecked = ReadBoolSetting(HorizInnerEyeMaskKey, fallback: false);
		VertVisualMaskBothCheck.IsChecked = ReadBoolSetting(VertVisualMaskBothKey, fallback: false);
		VertTopMaskCheck.IsChecked = ReadBoolSetting(VertTopMaskKey, fallback: false);
		VertBottomMaskCheck.IsChecked = ReadBoolSetting(VertBottomMaskKey, fallback: false);
		CalGridCheck.IsChecked = ReadBoolSetting(CalibrationGridKey, fallback: false);
		CalRulerCheck.IsChecked = ReadBoolSetting(CalibrationRulerKey, fallback: false);
		CalGratingsCheck.IsChecked = ReadBoolSetting(CalibrationGratingsKey, fallback: false);
		CalBarsCheck.IsChecked = ReadBoolSetting(CalibrationBarsKey, fallback: false);
		CalBeaconCheck.IsChecked = ReadBoolSetting(CalibrationBeaconKey, fallback: false);
		CalEdgeProbesCheck.IsChecked = ReadBoolSetting(CalibrationEdgeProbesKey, fallback: false);
		CalCheckerboardsCheck.IsChecked = ReadBoolSetting(CalibrationCheckerboardsKey, fallback: false);
		CalZonePlateCheck.IsChecked = ReadBoolSetting(CalibrationZonePlateKey, fallback: false);
		CalClippingCheck.IsChecked = ReadBoolSetting(CalibrationClippingStepsKey, fallback: false);
		CalMotionCheck.IsChecked = ReadBoolSetting(CalibrationMotionStripKey, fallback: false);
		HudEnabledCheck.IsChecked = ReadBoolSetting(HudEnabledKey, fallback: false);
		string traceModeText = ReadSetting(HudTraceVisibilityKey, string.Empty);
		HudTraceVisibilityCombo.SelectedIndex = int.TryParse(traceModeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int traceMode)
			? Math.Clamp(traceMode, 0, 2) : (ReadBoolSetting(HudTraceEnabledKey, fallback: false) ? 1 : 0);
		HudXSlider.Value = ReadRangeSetting(HudAnchorXKey, 0.04, 0.0, 1.0);
		HudYSlider.Value = ReadRangeSetting(HudAnchorYKey, 0.05, 0.0, 1.0);
		HudScaleSlider.Value = ReadRangeSetting(HudScaleKey, 1.0, 0.15, 3.0);
		HudTraceSensitivitySlider.Value = ReadRangeSetting(HudTraceSensitivityKey, 2.0, 0.5, 8.0);
		HudTraceXSlider.Value = ReadRangeSetting(HudTraceXKey, 0.05, 0.0, 1.0);
		HudTraceYSlider.Value = ReadRangeSetting(HudTraceYKey, 0.75, 0.0, 1.0);
		HudTraceScaleSlider.Value = ReadRangeSetting(HudTraceScaleKey, 1.0, 0.25, 3.0);
		HudTraceWidthSlider.Value = ReadRangeSetting(HudTraceWidthKey, 0.42, 0.1, 1.0);
		HudTraceHistorySlider.Value = ReadRangeSetting(HudTraceHistoryKey, 120.0, 30.0, 600.0);
		HudAlarmOnlyCheck.IsChecked = ReadBoolSetting(HudAlarmOnlyKey, fallback: false);
		HudSafeMarginSlider.Value = ReadRangeSetting(HudSafeMarginKey, 0.025, 0.0, 0.25);
		HudClampCheck.IsChecked = ReadBoolSetting(HudClampKey, fallback: true);
		foreach (HudWidgetOption widget in _hudWidgets)
			widget.Enabled = ReadBoolSetting($"hud_widget_{widget.Id}_enabled", widget.Id is "cpu" or "gpu" or "sys" or "vr");
		var orderedWidgets = _hudWidgets.OrderBy(w => ReadRangeSetting($"hud_widget_{w.Id}_order", Array.IndexOf(HudWidgetIds, w.Id), 0, HudWidgetIds.Length - 1)).ToList();
		_hudWidgets.Clear(); foreach (HudWidgetOption widget in orderedWidgets) _hudWidgets.Add(widget);
		HudMaxPerRowCombo.SelectedIndex = (int)ReadRangeSetting("hud_max_per_row", 4, 2, 8) - 2;
		HudSysWarningSlider.Value = ReadRangeSetting("hud_sys_warning", 30, 10, 60);
		HudSysCriticalSlider.Value = ReadRangeSetting("hud_sys_critical", 10, 0, 30);
		HudGraphModeCombo.SelectedIndex = (int)ReadRangeSetting(HudGraphModeKey, 0, 0, 3);
		HudGraphFrameIntervalCheck.IsChecked = ReadBoolSetting("hud_graph_frame_interval", false);
		HudGraphFpsCheck.IsChecked = ReadBoolSetting("hud_graph_fps", false);
		HudGraphBudgetDeviationCheck.IsChecked = ReadBoolSetting("hud_graph_budget_deviation", true);
		HudGraphAppWorkCheck.IsChecked = ReadBoolSetting("hud_graph_app_work", false);
		HudGraphWaitDurationCheck.IsChecked = ReadBoolSetting("hud_graph_wait_duration", false);
		HudGraphSubmitDurationCheck.IsChecked = ReadBoolSetting("hud_graph_submit_duration", false);
		HudGraphDisplayPeriodCheck.IsChecked = ReadBoolSetting("hud_graph_display_period", false);
		UpdateGraphChannelAvailability();

		// Feature 2: crosshair
		_crosshair.Size = ReadRangeSetting(CrosshairSizeKey, 5.0, 0.0, 1000.0);
		_crosshair.Gap = ReadRangeSetting(CrosshairGapKey, -2.0, -50.0, 50.0);
		_crosshair.Thickness = ReadRangeSetting(CrosshairThicknessKey, 1.0, 0.1, 50.0);
		_crosshair.OutlineThickness = ReadRangeSetting(CrosshairOutlineThicknessKey, 1.0, 0.0, 10.0);
		_crosshair.Alpha = ReadRangeSetting(CrosshairAlphaKey, 1.0, 0.0, 1.0);
		_crosshair.VrScale = ReadRangeSetting(CrosshairScaleKey, 1.0, 0.1, 10.0);
		_crosshair.Dot = ReadBoolSetting(CrosshairDotKey, false);
		_crosshair.Outline = ReadBoolSetting(CrosshairOutlineKey, true);
		_crosshair.TStyle = ReadBoolSetting(CrosshairTStyleKey, false);
		{
			uint col = (uint)ReadRangeSetting(CrosshairColorKey, 0x00FF00, 0, 0xFFFFFF);
			_crosshair.R = (byte)((col >> 16) & 0xFF); _crosshair.G = (byte)((col >> 8) & 0xFF); _crosshair.B = (byte)(col & 0xFF);
		}
		CrosshairEnabledCheck.IsChecked = ReadBoolSetting(CrosshairEnabledKey, false);
		CrosshairOffsetXSlider.Value = ReadRangeSetting(CrosshairOffsetXKey, 0.0, -1.0, 1.0);
		CrosshairOffsetYSlider.Value = ReadRangeSetting(CrosshairOffsetYKey, 0.0, -1.0, 1.0);
		SyncCrosshairControlsFromModel();

		// Feature 3: notifications
		NotifyEnabledCheck.IsChecked = ReadBoolSetting(NotifyEnabledKey, false);
		NotifyXSlider.Value = ReadRangeSetting(NotifyXKey, 0.98, 0.0, 1.0);
		NotifyYSlider.Value = ReadRangeSetting(NotifyYKey, 0.98, 0.0, 1.0);
		NotifyScaleSlider.Value = ReadRangeSetting(NotifyScaleKey, 1.0, 0.25, 3.0);
		NotifyOpacitySlider.Value = ReadRangeSetting(NotifyOpacityKey, 1.0, 0.1, 1.0);
		NotifyDurationSlider.Value = ReadRangeSetting(NotifyDurationKey, 3000.0, 500.0, 15000.0);
		NotifyMaxSlider.Value = ReadRangeSetting(NotifyMaxKey, 3.0, 1.0, 6.0);
		NotifyPrivacyCombo.SelectedIndex = (int)ReadRangeSetting(NotifyPrivacyKey, 0, 0, 2);
		NotifyShowIconCheck.IsChecked = ReadBoolSetting(NotifyShowIconKey, true);
		NotifyShowImageCheck.IsChecked = ReadBoolSetting(NotifyShowImageKey, true);
		NotifyAllowlistModeCheck.IsChecked = ReadBoolSetting(NotifyAllowlistModeKey, false);
		NotifyFiltersBox.Text = ReadSetting(NotifyFiltersKey, string.Empty);

		// iRacing provider and generic racing presentation
		IRacingEnabledCheck.IsChecked = ReadBoolSetting(IRacingEnabledKey, false);
		IRacingLapPopupCheck.IsChecked = ReadBoolSetting(IRacingLapPopupKey, false);
		IRacingSpotterGlowCheck.IsChecked = ReadBoolSetting(IRacingSpotterGlowKey, false);
		IRacingFlagBorderCheck.IsChecked = ReadBoolSetting(IRacingFlagBorderKey, false);
		IRacingSpotterWidthSlider.Value = ReadRangeSetting(IRacingSpotterWidthKey, 0.12, 0.03, 0.35);
		IRacingSpotterStrengthSlider.Value = ReadRangeSetting(IRacingSpotterStrengthKey, 1.0, 0.1, 2.0);
		IRacingSpotterOpacitySlider.Value = ReadRangeSetting(IRacingSpotterOpacityKey, 0.65, 0.05, 1.0);
		IRacingSpotterFadeSlider.Value = ReadRangeSetting(IRacingSpotterFadeKey, 1.8, 0.25, 4.0);
		uint spotterColor = (uint)ReadRangeSetting(IRacingSpotterColorKey, 0xFF4500, 0, 0xFFFFFF);
		IRacingSpotterColorBox.Text = spotterColor.ToString("X6", CultureInfo.InvariantCulture);
		IRacingSpotterColorPreview.Background = new SolidColorBrush(Color.FromRgb((byte)(spotterColor >> 16), (byte)(spotterColor >> 8), (byte)spotterColor));
		IRacingFlagWidthSlider.Value = ReadRangeSetting(IRacingFlagWidthKey, 0.018, 0.003, 0.12);
		IRacingFlagOpacitySlider.Value = ReadRangeSetting(IRacingFlagOpacityKey, 0.60, 0.05, 1.0);
		IRacingLapDurationSlider.Value = ReadRangeSetting(IRacingLapDurationKey, 4500, 1000, 15000);

		// Gameplay/Tuning + menu/window controls now live in the ReShade Remote pop-out (ReShadeRemoteWindow).
		SyncSlidersFromText();
		_loading = false;
		EnsureIRacingProvider();
		ApplyNotificationSettings();
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
			if (TryReadTextBox(MaskVerticalBox, out var maskV))
			{
				SetSliderValue(MaskVerticalSlider, maskV);
			}
			if (TryReadTextBox(MaskHorizontalBox, out var maskH))
			{
				SetSliderValue(MaskHorizontalSlider, maskH);
			}
			if (MaskBeanEditor != null && MaskRoundnessSlider != null)
			{
				SyncMaskEditorFromSliders();
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
			if (MaskBeanEditor != null)
			{
				SyncMaskEditorFromSliders();
			}
			SaveGlobalSettings();
		}
	}

	private void MaskRoundnessSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_loading && !_syncingControls && MaskBeanEditor != null)
		{
			SyncMaskEditorFromSliders();
			MaskRoundedCheck.IsChecked = true;
			SaveGlobalSettings();
		}
	}

	private static double FromPeakXMillis(object? value, double fallback)
	{
		if (value is int num)
		{
			return num >= 10000
				? Math.Clamp((num - 10000) / 1000.0 - 1.0, -1.0, 2.0)
				: Math.Clamp(num / 1000.0, 0.0, 1.0);
		}
		return fallback;
	}

	private static double FromRiseMillis(object? value, double fallback)
	{
		if (value is int num)
		{
			return num >= 20000 ? Math.Clamp((num - 20000) / 1000.0 - 0.5, -0.5, 1.0) : Math.Clamp(num / 1000.0, 0.0, 0.5);
		}
		return fallback;
	}

	private static double FromSteepMillis(object? value, double fallback)
	{
		if (value is int num)
		{
			return num >= 30000 ? Math.Clamp((num - 30000) / 1000.0 - 1.0, -1.0, 2.0) : Math.Clamp(num / 1000.0, 0.0, 1.0);
		}
		return fallback;
	}

	private void MaskSizeSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_loading && !_syncingControls && MaskBeanEditor != null)
		{
			SyncMaskEditorFromSliders();
		}
	}

	private void MaskShapeSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_loading && !_syncingControls && MaskBeanEditor != null)
		{
			_syncingControls = true;
			SyncMaskEditorFromSliders();
			_syncingControls = false;
			SaveGlobalSettings();
		}
	}

	private void MaskBeanEditor_ShapeChanged(object sender, EventArgs e)
	{
		if (!_loading && !_syncingControls)
		{
			_syncingControls = true;
			SetSliderValue(MaskApexYSlider, MaskBeanEditor.OuterApexY);
			SetSliderValue(MaskSizeSlider, MaskBeanEditor.Size);
			SetSliderValue(MaskRoundnessSlider, MaskBeanEditor.Curve);
			SetSliderValue(MaskInnerLowerSlider, MaskBeanEditor.InnerLowerY);
			SetSliderValue(MaskInnerBridgeSlider, MaskBeanEditor.InnerBridgeWidth);
			SetSliderValue(MaskInnerBridgeRiseSlider, MaskBeanEditor.InnerBridgeRise);
			SetSliderValue(MaskInnerBridgePeakXSlider, MaskBeanEditor.InnerBridgePeakX);
			SetSliderValue(MaskInnerBridgeSteepnessSlider, MaskBeanEditor.InnerBridgeSteepness);
			_syncingControls = false;
			SaveGlobalSettings();
		}
	}

	private void MaskApexYSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_loading && !_syncingControls && MaskBeanEditor != null)
		{
			SyncMaskEditorFromSliders();
			SaveGlobalSettings();
		}
	}

	private void MaskInnerLowerSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_loading && !_syncingControls && MaskBeanEditor != null)
		{
			SyncMaskEditorFromSliders();
			SaveGlobalSettings();
		}
	}

	private void MaskInnerBridgeSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_loading && !_syncingControls && MaskBeanEditor != null)
		{
			SyncMaskEditorFromSliders();
			SaveGlobalSettings();
		}
	}

	private void MaskInnerBridgeRiseSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_loading && !_syncingControls && MaskBeanEditor != null)
		{
			SyncMaskEditorFromSliders();
			SaveGlobalSettings();
		}
	}

	private void MaskInnerBridgePeakXSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_loading && !_syncingControls && MaskBeanEditor != null)
		{
			SyncMaskEditorFromSliders();
			SaveGlobalSettings();
		}
	}

	private void MaskInnerBridgeSteepnessSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_loading && !_syncingControls && MaskBeanEditor != null)
		{
			SyncMaskEditorFromSliders();
			SaveGlobalSettings();
		}
	}

	private void MaskSliderReset_RightClick(object sender, MouseButtonEventArgs e)
	{
		if (_loading) return;
		e.Handled = true;
		_syncingControls = true;
		if (sender == MaskSizeSlider) MaskSizeSlider.Value = 1.0;
		else if (sender == MaskRoundnessSlider) MaskRoundnessSlider.Value = 0.5;
		else if (sender == MaskApexYSlider)     MaskApexYSlider.Value     = 0.0;
		else if (sender == MaskInnerLowerSlider) MaskInnerLowerSlider.Value = 0.0;
		else if (sender == MaskInnerBridgeSlider) MaskInnerBridgeSlider.Value = 0.5;
		else if (sender == MaskInnerBridgeRiseSlider) MaskInnerBridgeRiseSlider.Value = 0.0;
		else if (sender == MaskInnerBridgePeakXSlider) MaskInnerBridgePeakXSlider.Value = 0.5;
		else if (sender == MaskInnerBridgeSteepnessSlider) MaskInnerBridgeSteepnessSlider.Value = 0.5;
		SyncMaskEditorFromSliders();
		_syncingControls = false;
		SaveGlobalSettings();
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
			if (MaskBeanEditor != null)
			{
				SyncMaskEditorFromSliders();
			}
			SaveGlobalSettings();
		}
	}

	private void MaskValue_Changed(object sender, TextChangedEventArgs e)
	{
		if (!_loading && !_syncingControls)
		{
			SyncSlidersFromText();
			SaveGlobalSettings();
		}
	}

	private void MaskSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_loading && !_syncingControls && MaskVerticalBox != null && MaskHorizontalBox != null)
		{
			if (sender == MaskVerticalSlider)
			{
				SyncTextFromSlider(MaskVerticalBox, MaskVerticalSlider);
			}
			else if (sender == MaskHorizontalSlider)
			{
				SyncTextFromSlider(MaskHorizontalBox, MaskHorizontalSlider);
			}
			if (MaskBeanEditor != null)
			{
				SyncMaskEditorFromSliders();
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
			if (MaskBeanEditor != null)
			{
				SyncMaskEditorFromSliders();
			}
			SaveGlobalSettings();
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
		PublishLiveState();
		StatusText.Text = "Visor setting applied live.";
	}

	private void HudLayout_Changed(object sender, RoutedEventArgs e)
	{
		if (_loading) return;
		SaveCalibrationSettings();
		PublishLiveState();
		StatusText.Text = "HUD position applied live.";
	}

	private void HudLayoutSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => HudLayout_Changed(sender, e);

	private void HudWidgetToggle_Changed(object sender, RoutedEventArgs e) => HudLayout_Changed(sender, e);

	private void HudWidgetUp_Click(object sender, RoutedEventArgs e) => MoveHudWidget(sender, -1);
	private void HudWidgetDown_Click(object sender, RoutedEventArgs e) => MoveHudWidget(sender, 1);
	private void MoveHudWidget(object sender, int delta)
	{
		if (sender is not Button { CommandParameter: string id }) return;
		int index = _hudWidgets.ToList().FindIndex(widget => widget.Id == id);
		int target = Math.Clamp(index + delta, 0, _hudWidgets.Count - 1);
		if (index < 0 || index == target) return;
		_hudWidgets.Move(index, target);
		HudLayout_Changed(sender, new RoutedEventArgs());
	}
	private void HudDefaults_Click(object sender, RoutedEventArgs e)
	{
		string[] defaults={"cpu","gpu","sys","vr"};
		foreach(var widget in _hudWidgets)widget.Enabled=defaults.Contains(widget.Id);
		var ordered=defaults.Select(id=>_hudWidgets.First(w=>w.Id==id)).Concat(_hudWidgets.Where(w=>!defaults.Contains(w.Id))).ToList();
		_hudWidgets.Clear();foreach(var widget in ordered)_hudWidgets.Add(widget);
		HudMaxPerRowCombo.SelectedIndex=2;HudSysWarningSlider.Value=30;HudSysCriticalSlider.Value=10;
		HudLayout_Changed(sender,new RoutedEventArgs());
	}

	private void HudGraphMode_Changed(object sender, SelectionChangedEventArgs e)
	{
		UpdateGraphChannelAvailability();
		HudLayout_Changed(sender, e);
	}

	private void UpdateGraphChannelAvailability()
	{
		int mode = HudGraphModeCombo?.SelectedIndex ?? 0;
		if (HudGraphFrameIntervalCheck == null) return;
		HudGraphFrameIntervalCheck.IsEnabled = mode is 1 or 3;
		HudGraphFpsCheck.IsEnabled = mode == 2;
		HudGraphBudgetDeviationCheck.IsEnabled = mode == 0;
		HudGraphAppWorkCheck.IsEnabled = mode is 1 or 3;
		HudGraphWaitDurationCheck.IsEnabled = mode == 1;
		HudGraphSubmitDurationCheck.IsEnabled = mode == 1;
		HudGraphDisplayPeriodCheck.IsEnabled = mode == 1;
	}

	private void SaveExperimentalSettings()
	{
		Directory.CreateDirectory(ConfigDirectory);
		WritePrivateProfileString("Settings", MaskEnabledKey, MaskEnabledCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", MaskRoundedKey, MaskRoundedCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", HorizVisualMaskBothKey, HorizVisualMaskBothCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", HorizOuterEyeMaskKey, HorizOuterEyeMaskCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", HorizInnerEyeMaskKey, HorizInnerEyeMaskCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", VertVisualMaskBothKey, VertVisualMaskBothCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", VertTopMaskKey, VertTopMaskCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", VertBottomMaskKey, VertBottomMaskCheck.IsChecked == true ? "1" : "0", ConfigPath);
	}

	private void CalibrationCheck_Changed(object sender, RoutedEventArgs e)
	{
		if (_loading) return;
		SaveCalibrationSettings();
		PublishLiveState();
		StatusText.Text = "Calibration setting applied live.";
	}

	private void PublishLiveState()
	{
		uint mask = 0;
		if (CalGridCheck.IsChecked == true) mask |= 1u << 0; if (CalRulerCheck.IsChecked == true) mask |= 1u << 1;
		if (CalGratingsCheck.IsChecked == true) mask |= 1u << 2; if (CalBarsCheck.IsChecked == true) mask |= 1u << 3;
		if (CalBeaconCheck.IsChecked == true) mask |= 1u << 4; if (CalEdgeProbesCheck.IsChecked == true) mask |= 1u << 5;
		if (CalCheckerboardsCheck.IsChecked == true) mask |= 1u << 6; if (CalZonePlateCheck.IsChecked == true) mask |= 1u << 7;
		if (CalClippingCheck.IsChecked == true) mask |= 1u << 8; if (CalMotionCheck.IsChecked == true) mask |= 1u << 9;
		uint widgetMask=0, widgetOrder=0;
		for(int slot=0;slot<_hudWidgets.Count;++slot) { int id=Array.IndexOf(HudWidgetIds,_hudWidgets[slot].Id); if(id<0)continue; if(_hudWidgets[slot].Enabled)widgetMask|=1u<<id; widgetOrder|=(uint)id<<(slot*8); }
		var graphChecks=new[]{HudGraphFrameIntervalCheck,HudGraphFpsCheck,HudGraphBudgetDeviationCheck,HudGraphAppWorkCheck,HudGraphWaitDurationCheck,HudGraphSubmitDurationCheck,HudGraphDisplayPeriodCheck};
		uint graphChannels=0; for(int i=0;i<graphChecks.Length;++i)if(graphChecks[i].IsChecked==true)graphChannels|=1u<<i;
		_telemetryConfig.Publish(_hudWidgets, (HudMaxPerRowCombo?.SelectedIndex ?? 2) + 2, HudSysWarningSlider?.Value ?? 30, HudSysCriticalSlider?.Value ?? 10);
		_liveState.Publish(mask,
			MaskEnabledCheck.IsChecked == true, !ReadBoolSetting(OverlayForceDirectKey, false), MaskSizeSlider.Value, 1.0 - MaskRoundnessSlider.Value,
			MaskApexYSlider.Value, MaskInnerLowerSlider.Value, MaskInnerBridgeSlider.Value,
			MaskInnerBridgeRiseSlider.Value, MaskInnerBridgePeakXSlider.Value, MaskInnerBridgeSteepnessSlider.Value,
			HudEnabledCheck.IsChecked == true, Math.Max(0, HudTraceVisibilityCombo.SelectedIndex), HudXSlider.Value, HudYSlider.Value, HudScaleSlider.Value,
			HudSafeMarginSlider.Value, HudClampCheck.IsChecked == true, HudAlarmOnlyCheck.IsChecked == true,
			HudTraceSensitivitySlider.Value, HudTraceXSlider.Value, HudTraceYSlider.Value, HudTraceScaleSlider.Value,
			HudTraceWidthSlider.Value, HudTraceHistorySlider.Value, ReadRangeSetting(HudAlarmHoldKey, 1500.0, 0.0, 10000.0),
			widgetMask, widgetOrder, graphChannels, (uint)Math.Max(0,HudGraphModeCombo.SelectedIndex),
			_boundaryDragActive,
			CrosshairEnabledCheck.IsChecked == true, _crosshair.Dot, _crosshair.Outline, _crosshair.TStyle,
			_crosshair.Size, _crosshair.Gap, _crosshair.Thickness, _crosshair.OutlineThickness, _crosshair.Alpha, _crosshair.VrScale, _crosshair.ColorRgb,
			CrosshairOffsetXSlider.Value, CrosshairOffsetYSlider.Value,
			NotifyEnabledCheck.IsChecked == true, NotifyShowIconCheck.IsChecked == true, NotifyShowImageCheck.IsChecked == true,
			NotifyXSlider.Value, NotifyYSlider.Value, NotifyScaleSlider.Value, NotifyOpacitySlider.Value, NotifyDurationSlider.Value,
			(uint)Math.Round(NotifyMaxSlider.Value), (uint)Math.Max(0, NotifyPrivacyCombo.SelectedIndex),
			IRacingEnabledCheck.IsChecked == true, IRacingLapPopupCheck.IsChecked == true,
			IRacingSpotterGlowCheck.IsChecked == true, IRacingFlagBorderCheck.IsChecked == true);
	}

	// ---- Feature 1: render-boundary flash drag signalling --------------------------------------
	// Raised by drag-sensitive layout controls (HUD/trace position/size/width/height/scale). Only
	// these controls flash the boundary — telemetry, colour, opacity, and threshold controls do not.
	private void BoundaryDrag_Start(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
	{
		if (_loading) return;
		_boundaryDragActive = true;
		PublishLiveState();
	}

	private void BoundaryDrag_End(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
	{
		if (_loading) return;
		if (!_boundaryDragActive) return;
		_boundaryDragActive = false;
		PublishLiveState(); // native stamps the fade-out on the falling edge
	}

	// ---- Feature 2: crosshair UI ---------------------------------------------------------------
	private void SyncCrosshairControlsFromModel()
	{
		if (CrosshairSizeSlider == null) return;
		bool prev = _loading; _loading = true;
		CrosshairSizeSlider.Value = Math.Clamp(_crosshair.Size, 0.0, 100.0);
		CrosshairGapSlider.Value = Math.Clamp(_crosshair.Gap, -50.0, 50.0);
		CrosshairThicknessSlider.Value = Math.Clamp(_crosshair.Thickness, 0.1, 50.0);
		CrosshairOutlineThicknessSlider.Value = Math.Clamp(_crosshair.OutlineThickness, 0.0, 10.0);
		CrosshairAlphaSlider.Value = Math.Clamp(_crosshair.Alpha, 0.0, 1.0);
		CrosshairScaleSlider.Value = Math.Clamp(_crosshair.VrScale, 0.1, 10.0);
		CrosshairDotCheck.IsChecked = _crosshair.Dot;
		CrosshairOutlineCheck.IsChecked = _crosshair.Outline;
		CrosshairTStyleCheck.IsChecked = _crosshair.TStyle;
		CrosshairColorPreview.Background = new SolidColorBrush(Color.FromRgb(_crosshair.R, _crosshair.G, _crosshair.B));
		CrosshairColorBox.Text = $"{_crosshair.R:X2}{_crosshair.G:X2}{_crosshair.B:X2}";
		CrosshairOverlayPreview?.Apply(_crosshair);
		MaskBeanEditor?.SetCrosshair(_crosshair);
		_loading = prev;
	}

	private void CrosshairControl_Changed(object sender, RoutedEventArgs e)
	{
		if (_loading) return;
		_crosshair.Size = CrosshairSizeSlider.Value;
		_crosshair.Gap = CrosshairGapSlider.Value;
		_crosshair.Thickness = CrosshairThicknessSlider.Value;
		_crosshair.OutlineThickness = CrosshairOutlineThicknessSlider.Value;
		_crosshair.Alpha = CrosshairAlphaSlider.Value;
		_crosshair.VrScale = CrosshairScaleSlider.Value;
		_crosshair.Dot = CrosshairDotCheck.IsChecked == true;
		_crosshair.Outline = CrosshairOutlineCheck.IsChecked == true;
		_crosshair.TStyle = CrosshairTStyleCheck.IsChecked == true;
		CrosshairOverlayPreview?.Apply(_crosshair);
		MaskBeanEditor?.SetCrosshair(_crosshair);
		SaveCrosshairSettings();
		PublishLiveState();
		StatusText.Text = "Crosshair applied live.";
	}

	private void CrosshairControlSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => CrosshairControl_Changed(sender, e);

	// Colour is entered as a hex string (e.g. "00FF00" or "#00FF00") — no WinForms dependency.
	private void CrosshairColor_Apply(object sender, RoutedEventArgs e)
	{
		if (_loading) return;
		string hex = (CrosshairColorBox.Text ?? string.Empty).Trim().TrimStart('#');
		if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
		{
			_crosshair.R = (byte)((rgb >> 16) & 0xFF); _crosshair.G = (byte)((rgb >> 8) & 0xFF); _crosshair.B = (byte)(rgb & 0xFF);
			CrosshairColorPreview.Background = new SolidColorBrush(Color.FromRgb(_crosshair.R, _crosshair.G, _crosshair.B));
			CrosshairOverlayPreview?.Apply(_crosshair);
			MaskBeanEditor?.SetCrosshair(_crosshair);
			SaveCrosshairSettings();
			PublishLiveState();
			StatusText.Text = "Crosshair colour applied.";
		}
		else StatusText.Text = "Enter a 6-digit hex colour, e.g. 00FF00.";
	}

	private void CrosshairImport_Click(object sender, RoutedEventArgs e)
	{
		string status = _crosshair.ImportAny(CrosshairImportBox.Text ?? string.Empty);
		SyncCrosshairControlsFromModel();
		SaveCrosshairSettings();
		PublishLiveState();
		StatusText.Text = status;
	}

	private void CrosshairExport_Click(object sender, RoutedEventArgs e)
	{
		string cfg = _crosshair.ToLegacyConfig();
		try { Clipboard.SetText(cfg); StatusText.Text = "Crosshair config copied to clipboard."; }
		catch { StatusText.Text = "Could not access clipboard."; }
		CrosshairImportBox.Text = cfg;
	}

	private void SaveCrosshairSettings()
	{
		Directory.CreateDirectory(ConfigDirectory);
		var c = CultureInfo.InvariantCulture;
		WritePrivateProfileString("Settings", CrosshairEnabledKey, CrosshairEnabledCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CrosshairDotKey, _crosshair.Dot ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CrosshairOutlineKey, _crosshair.Outline ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CrosshairTStyleKey, _crosshair.TStyle ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CrosshairSizeKey, _crosshair.Size.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", CrosshairGapKey, _crosshair.Gap.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", CrosshairThicknessKey, _crosshair.Thickness.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", CrosshairOutlineThicknessKey, _crosshair.OutlineThickness.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", CrosshairAlphaKey, _crosshair.Alpha.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", CrosshairScaleKey, _crosshair.VrScale.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", CrosshairColorKey, ((int)_crosshair.ColorRgb).ToString(c), ConfigPath);
		WritePrivateProfileString("Settings", CrosshairOffsetXKey, CrosshairOffsetXSlider.Value.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", CrosshairOffsetYKey, CrosshairOffsetYSlider.Value.ToString("0.###", c), ConfigPath);
	}

	private void CrosshairPosition_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_loading) return; SaveCrosshairSettings(); PublishLiveState();
		StatusText.Text = "Crosshair calibration applied live.";
	}

	private void CrosshairPosition_Reset(object sender, RoutedEventArgs e)
	{
		CrosshairOffsetXSlider.Value = 0; CrosshairOffsetYSlider.Value = 0;
		SaveCrosshairSettings(); PublishLiveState(); StatusText.Text = "Crosshair position reset.";
	}

	private void CrosshairOffset_ResetRightClick(object sender, MouseButtonEventArgs e)
	{
		if (_loading) return;
		e.Handled = true;
		if (sender == CrosshairOffsetXSlider) CrosshairOffsetXSlider.Value = 0;
		else if (sender == CrosshairOffsetYSlider) CrosshairOffsetYSlider.Value = 0;
		SaveCrosshairSettings(); PublishLiveState();
	}

	private void CrosshairEnabled_Changed(object sender, RoutedEventArgs e)
	{
		if (_loading) return;
		SaveCrosshairSettings();
		PublishLiveState();
		StatusText.Text = "Crosshair applied live.";
	}

	// ---- Feature 3: notifications UI -----------------------------------------------------------
	private void ApplyNotificationSettings(bool requestAccess = false)
	{
		if (NotifyEnabledCheck.IsChecked == true || requestAccess)
			_notificationBroker.Start(requestAccess);
		if (NotifyStatusText != null) NotifyStatusText.Text = _notificationBroker.RefreshStatus();
	}

	private static string Manifest32Path
	{
		get
		{
			string localManifest = Path.Combine(ProcessDirectory, "XR_APILAYER_cooooked_xrviewlab32.json");
			if (File.Exists(localManifest)) return localManifest;
			string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "xr-viewlab", "XR_APILAYER_cooooked_xrviewlab32.json");
			return File.Exists(installed) ? installed : localManifest;
		}
	}

	private void TestNotification_Click(object sender, RoutedEventArgs e)
	{
		NotifyEnabledCheck.IsChecked = true;
		SaveNotificationSettings();
		_notificationBroker.SendTest(requestAccess: true);
		PublishLiveState();
		if (NotifyStatusText != null) NotifyStatusText.Text = "Test presentation requested through the notification broker.";
	}

	private void NotifyControl_Changed(object sender, RoutedEventArgs e)
	{
		if (_loading) return;
		SaveNotificationSettings();
		PublishLiveState();
		ApplyNotificationSettings(requestAccess: sender == NotifyEnabledCheck && NotifyEnabledCheck.IsChecked == true);
		StatusText.Text = "Notification settings applied.";
	}

	private void NotifyControlSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => NotifyControl_Changed(sender, e);

	private void SaveNotificationSettings()
	{
		Directory.CreateDirectory(ConfigDirectory);
		var c = CultureInfo.InvariantCulture;
		WritePrivateProfileString("Settings", NotifyEnabledKey, NotifyEnabledCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", NotifyXKey, NotifyXSlider.Value.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", NotifyYKey, NotifyYSlider.Value.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", NotifyScaleKey, NotifyScaleSlider.Value.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", NotifyOpacityKey, NotifyOpacitySlider.Value.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", NotifyDurationKey, NotifyDurationSlider.Value.ToString("0", c), ConfigPath);
		WritePrivateProfileString("Settings", NotifyMaxKey, Math.Round(NotifyMaxSlider.Value).ToString("0", c), ConfigPath);
		WritePrivateProfileString("Settings", NotifyPrivacyKey, Math.Max(0, NotifyPrivacyCombo.SelectedIndex).ToString("0", c), ConfigPath);
		WritePrivateProfileString("Settings", NotifyShowIconKey, NotifyShowIconCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", NotifyShowImageKey, NotifyShowImageCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", NotifyAllowlistModeKey, NotifyAllowlistModeCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", NotifyFiltersKey, NotifyFiltersBox.Text ?? string.Empty, ConfigPath);
	}

	// ---- iRacing integration UI ----------------------------------------------------------------
	private void IRacingControl_Changed(object sender, RoutedEventArgs e)
	{
		if (_loading) return;
		Directory.CreateDirectory(ConfigDirectory);
		WritePrivateProfileString("Settings", IRacingEnabledKey, IRacingEnabledCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", IRacingLapPopupKey, IRacingLapPopupCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", IRacingSpotterGlowKey, IRacingSpotterGlowCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", IRacingFlagBorderKey, IRacingFlagBorderCheck.IsChecked == true ? "1" : "0", ConfigPath);
		var c = CultureInfo.InvariantCulture;
		WritePrivateProfileString("Settings", IRacingSpotterWidthKey, IRacingSpotterWidthSlider.Value.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", IRacingSpotterStrengthKey, IRacingSpotterStrengthSlider.Value.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", IRacingSpotterOpacityKey, IRacingSpotterOpacitySlider.Value.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", IRacingSpotterFadeKey, IRacingSpotterFadeSlider.Value.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", IRacingFlagWidthKey, IRacingFlagWidthSlider.Value.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", IRacingFlagOpacityKey, IRacingFlagOpacitySlider.Value.ToString("0.###", c), ConfigPath);
		WritePrivateProfileString("Settings", IRacingLapDurationKey, IRacingLapDurationSlider.Value.ToString("0", c), ConfigPath);
		PublishLiveState();
		EnsureIRacingProvider();
		StatusText.Text = "iRacing telemetry settings applied.";
	}

	private void EnsureIRacingProvider()
	{
		_notificationBroker.Start(requestAccess: false);
		IRacingDiagnosticsChanged();
	}

	private void IRacingControlSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => IRacingControl_Changed(sender, e);

	private void IRacingSpotterColor_Apply(object sender, RoutedEventArgs e)
	{
		if (_loading) return;
		string hex = (IRacingSpotterColorBox.Text ?? string.Empty).Trim().TrimStart('#');
		if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
		{
			WritePrivateProfileString("Settings", IRacingSpotterColorKey, rgb.ToString(CultureInfo.InvariantCulture), ConfigPath);
			IRacingSpotterColorPreview.Background = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
			StatusText.Text = "Spotter glow colour saved; an active game picks it up at session start.";
		}
		else StatusText.Text = "Enter a 6-digit hex colour, e.g. FF4500.";
	}

	private void IRacingDiagnosticsChanged() => Dispatcher.BeginInvoke(() =>
	{
		if (IRacingStatusText != null)
			IRacingStatusText.Text = _notificationBroker.RefreshIRacingStatus();
	});

	private void SimulateIRacing(string kind){EnsureIRacingProvider();_notificationBroker.SendCommand("simulate-"+kind.ToLowerInvariant());}
	private void IRacingTestLeft_Click(object s,RoutedEventArgs e)=>SimulateIRacing("Left");
	private void IRacingTestRight_Click(object s,RoutedEventArgs e)=>SimulateIRacing("Right");
	private void IRacingTestBoth_Click(object s,RoutedEventArgs e)=>SimulateIRacing("Both");
	private void IRacingTestClear_Click(object s,RoutedEventArgs e)=>SimulateIRacing("Clear");
	private void IRacingTestLap_Click(object s,RoutedEventArgs e)=>SimulateIRacing("Lap");
	private void IRacingTestYellow_Click(object s,RoutedEventArgs e)=>SimulateIRacing("Yellow");
	private void IRacingTestBlue_Click(object s,RoutedEventArgs e)=>SimulateIRacing("Blue");
	private void ClearHistory_Click(object sender, RoutedEventArgs e)
	{
		StatusText.Text = _notificationBroker.SendCommand("clear-history")
			? "Bounded technical history cleared."
			: "Could not clear technical history: " + _notificationBroker.Status;
	}

	private void SaveCalibrationSettings()
	{
		Directory.CreateDirectory(ConfigDirectory);
		WritePrivateProfileString("Settings", CalibrationGridKey, CalGridCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CalibrationRulerKey, CalRulerCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CalibrationGratingsKey, CalGratingsCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CalibrationBarsKey, CalBarsCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CalibrationBeaconKey, CalBeaconCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CalibrationEdgeProbesKey, CalEdgeProbesCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CalibrationCheckerboardsKey, CalCheckerboardsCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CalibrationZonePlateKey, CalZonePlateCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CalibrationClippingStepsKey, CalClippingCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", CalibrationMotionStripKey, CalMotionCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", HudEnabledKey, HudEnabledCheck.IsChecked == true ? "1" : "0", ConfigPath);
		int traceVisibility = Math.Max(0, HudTraceVisibilityCombo.SelectedIndex);
		WritePrivateProfileString("Settings", HudTraceVisibilityKey, traceVisibility.ToString(CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudTraceEnabledKey, traceVisibility != 0 ? "1" : "0", ConfigPath); // legacy migration key
		WritePrivateProfileString("Settings", HudAnchorXKey, HudXSlider.Value.ToString("0.###", CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudAnchorYKey, HudYSlider.Value.ToString("0.###", CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudScaleKey, HudScaleSlider.Value.ToString("0.###", CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudTraceSensitivityKey, HudTraceSensitivitySlider.Value.ToString("0.##", CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudTraceXKey, HudTraceXSlider.Value.ToString("0.###", CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudTraceYKey, HudTraceYSlider.Value.ToString("0.###", CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudTraceScaleKey, HudTraceScaleSlider.Value.ToString("0.###", CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudTraceWidthKey, HudTraceWidthSlider.Value.ToString("0.###", CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudTraceHistoryKey, HudTraceHistorySlider.Value.ToString("0", CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudAlarmOnlyKey, HudAlarmOnlyCheck.IsChecked == true ? "1" : "0", ConfigPath);
		WritePrivateProfileString("Settings", HudSafeMarginKey, HudSafeMarginSlider.Value.ToString("0.###", CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudClampKey, HudClampCheck.IsChecked == true ? "1" : "0", ConfigPath);
		for (int order = 0; order < _hudWidgets.Count; ++order)
		{
			HudWidgetOption widget = _hudWidgets[order];
			WritePrivateProfileString("Settings", $"hud_widget_{widget.Id}_enabled", widget.Enabled ? "1" : "0", ConfigPath);
			WritePrivateProfileString("Settings", $"hud_widget_{widget.Id}_order", order.ToString(CultureInfo.InvariantCulture), ConfigPath);
		}
		WritePrivateProfileString("Settings", TelemetrySettingsVersionKey, "1", ConfigPath);
		WritePrivateProfileString("Settings", "hud_max_per_row", ((HudMaxPerRowCombo?.SelectedIndex ?? 2)+2).ToString(CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", "hud_sys_warning", (HudSysWarningSlider?.Value ?? 30).ToString("0",CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", "hud_sys_critical", (HudSysCriticalSlider?.Value ?? 10).ToString("0",CultureInfo.InvariantCulture), ConfigPath);
		WritePrivateProfileString("Settings", HudGraphModeKey, Math.Max(0, HudGraphModeCombo.SelectedIndex).ToString(CultureInfo.InvariantCulture), ConfigPath);
		var graphChecks = new[] { HudGraphFrameIntervalCheck, HudGraphFpsCheck, HudGraphBudgetDeviationCheck, HudGraphAppWorkCheck, HudGraphWaitDurationCheck, HudGraphSubmitDurationCheck, HudGraphDisplayPeriodCheck };
		for (int i = 0; i < graphChecks.Length; ++i)
			WritePrivateProfileString("Settings", $"hud_graph_{HudGraphChannelIds[i]}", graphChecks[i].IsChecked == true ? "1" : "0", ConfigPath);
	}

	private void ReShadeMenuSetting_Changed(object sender, RoutedEventArgs e) { }

	private void SaveReShadeMenuSettings() { }

	private ReShadeRemoteWindow? _reshadeRemote;
	private void OpenReShadeRemote_Click(object sender, RoutedEventArgs e)
	{
		if (_reshadeRemote == null)
		{
			_reshadeRemote = new ReShadeRemoteWindow { Owner = this };
			_reshadeRemote.Closed += (_, _) => _reshadeRemote = null;
			_reshadeRemote.Show();
		}
		else
		{
			// Re-clicking the dropdown button closes the popout (toggle).
			_reshadeRemote.Close();
		}
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

	private void XrPollTimer_Tick(object? sender, EventArgs e)
	{
		if (NotifyStatusText != null && NotifyEnabledCheck.IsChecked == true)
			NotifyStatusText.Text = _notificationBroker.RefreshStatus();
		if (IRacingStatusText != null && IRacingEnabledCheck.IsChecked == true)
			IRacingStatusText.Text = _notificationBroker.RefreshIRacingStatus();
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

	// Old ReShade checkbox handlers removed � these controls now live in ReShadeRemoteWindow.


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

	// ReShade menu/window/quad controls moved to the ReShade Remote pop-out (ReShadeRemoteWindow).
	private void XrSyncToUI() { }

	private void SaveGlobalSettings()
	{
		bool valueOrDefault = SplitCheck.IsChecked == true;
		if (!TryReadTextBox(TotalBox, out var value) || !TryReadTextBox(TopBox, out var value2) || !TryReadTextBox(BottomBox, out var value3) || !TryReadTextBox(HorizontalBox, out var value4))
		{
			MessageBox.Show(this, "Enter render values from 0.00 to 1.00. Total and horizontal must be at least 0.01.", "ViewLab", MessageBoxButton.OK, MessageBoxImage.Exclamation);
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
		WritePrivateProfileString("Settings", "total_render_height", FormatStorageScale(value), ConfigPath);
		WritePrivateProfileString("Settings", "total_share", null, ConfigPath);
		WritePrivateProfileString("Settings", "vertical_tangent", null, ConfigPath);
		WritePrivateProfileString("Settings", "top_tangent", FormatStorageScale(value2), ConfigPath);
		WritePrivateProfileString("Settings", "bottom_tangent", FormatStorageScale(value3), ConfigPath);
		WritePrivateProfileString("Settings", "horizontal_render_width", FormatStorageScale(value4), ConfigPath);
		double maskV = TryReadTextBox(MaskVerticalBox, out var mv) ? Math.Clamp(mv, 0.01, 1.0) : 1.0;
		double maskH = TryReadTextBox(MaskHorizontalBox, out var mh) ? Math.Clamp(mh, 0.01, 1.0) : 1.0;
		WritePrivateProfileString("Settings", "mask_vertical", FormatStorageScale(maskV), ConfigPath);
		WritePrivateProfileString("Settings", "mask_horizontal", FormatStorageScale(maskH), ConfigPath);
		WritePrivateProfileString("Settings", MaskSizeKey, FormatStorageScale(MaskSizeSlider.Value), ConfigPath);
		

		WritePrivateProfileString("Settings", MaskCornerKey, FormatStorageScale(1.0 - MaskRoundnessSlider.Value), ConfigPath);
		WritePrivateProfileString("Settings", MaskOuterApexYKey, FormatStorageScale(MaskApexYSlider.Value), ConfigPath);
		WritePrivateProfileString("Settings", MaskInnerLowerYKey, FormatStorageScale(MaskInnerLowerSlider.Value), ConfigPath);
		WritePrivateProfileString("Settings", MaskInnerBridgeWidthKey, FormatStorageScale(MaskInnerBridgeSlider.Value), ConfigPath);
		WritePrivateProfileString("Settings", MaskInnerBridgeRiseKey, FormatStorageScale(MaskInnerBridgeRiseSlider.Value), ConfigPath);
		WritePrivateProfileString("Settings", MaskInnerBridgePeakXKey, FormatStorageScale(MaskInnerBridgePeakXSlider.Value), ConfigPath);
		WritePrivateProfileString("Settings", MaskInnerBridgeSteepnessKey, FormatStorageScale(MaskInnerBridgeSteepnessSlider.Value), ConfigPath);
		WritePrivateProfileString("Settings", MaskOffsetYKey, "0", ConfigPath);
		WritePrivateProfileString("Settings", MaskTopBiasKey, "0", ConfigPath);
		WritePrivateProfileString("Settings", MaskBottomBiasKey, "0", ConfigPath);
		WritePrivateProfileString("Settings", MaskLeftBiasKey, "0", ConfigPath);
		WritePrivateProfileString("Settings", MaskRightBiasKey, "0", ConfigPath);
		WritePrivateProfileString("Settings", MaskTopCurveKey, "0", ConfigPath);
		WritePrivateProfileString("Settings", MaskBottomCurveKey, "0", ConfigPath);
		// Written last: the native layer only live-reloads visor values after this revision changes.
		WritePrivateProfileString("Settings", "visor_live_revision", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture), ConfigPath);
		PublishLiveState();
		SaveReShadeMenuSettings();
		SaveExperimentalSettings();
		WriteRegistryEnabled(valueOrDefault2);
		_loading = true;
		TotalBox.Text = FormatScale(value);
		TopBox.Text = FormatScale(value2);
		BottomBox.Text = FormatScale(value3);
		HorizontalBox.Text = FormatScale(value4);
		MaskVerticalBox.Text = FormatScale(maskV);
		MaskHorizontalBox.Text = FormatScale(maskH);
		SyncSlidersFromText();
		_loading = false;
		UpdateHints();
		StatusText.Text = (valueOrDefault2 ? "Saved. Restart the VR game." : "Layer disabled.");
	}

	private static void WriteRegistryEnabled(bool enabled)
	{
		if (LayerRegistrationMatches(RegistryView.Registry64, ManifestPath, enabled) &&
			LayerRegistrationMatches(RegistryView.Registry32, Manifest32Path, enabled)) return;

		string exe = Environment.ProcessPath ?? throw new InvalidOperationException("ViewLab executable path is unavailable.");
		using Process? helper = Process.Start(new ProcessStartInfo
		{
			FileName = exe,
			Arguments = $"--set-layer-enabled {(enabled ? 1 : 0)} \"{ManifestPath}\" \"{Manifest32Path}\"",
			UseShellExecute = true,
			Verb = "runas",
			WorkingDirectory = ProcessDirectory
		});
		helper?.WaitForExit();
		if (helper == null || helper.ExitCode != 0 ||
			!LayerRegistrationMatches(RegistryView.Registry64, ManifestPath, enabled) ||
			!LayerRegistrationMatches(RegistryView.Registry32, Manifest32Path, enabled))
			throw new InvalidOperationException("ViewLab layer registration was not changed. Windows administrator approval is required.");
	}

	private static bool LayerRegistrationMatches(RegistryView view, string manifestPath, bool enabled)
	{
		try
		{
			using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
			using RegistryKey? key = baseKey.OpenSubKey(OpenXrRegistryRoot, writable: false);
			if (key == null || Convert.ToInt32(key.GetValue(manifestPath, -1), CultureInfo.InvariantCulture) != (enabled ? 0 : 1)) return false;
			return !key.GetValueNames().Any(name => name.Contains("XR_APILAYER_cooooked_xrviewlab", StringComparison.OrdinalIgnoreCase) && !name.Equals(manifestPath, StringComparison.OrdinalIgnoreCase));
		}
		catch { return false; }
	}

	private static readonly HashSet<string> AppKeyBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
	{
		"steam", "steam.exe", "steamvr", "steamtours", "steamtours.exe",
		"racelab_vr", "racelab_vr.exe", "racelab", "vrmonitor", "vrmonitor.exe",
		"vrserver", "vrserver.exe", "vrdashboard", "vrdashboard.exe",
		"openvr_overlay", "xr_composition_layer_override",
		"pivotpoint", "pivotpoint.exe",
	};

	private sealed record KnownSteamVrApp(string AppId, string ExeName, string DisplayName, string XrType);

	private static readonly KnownSteamVrApp[] KnownSteamVrApps =
	{
		new("2981220", "Forefront_Internal.exe", "Forefront", "OpenXR"),
	};

	private void LoadAppProfiles()
	{
		_loading = true;
		_apps.Clear();
		DiscoverKnownSteamApps();
		List<AppProfile> loadedApps = new();
		using RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(AppRegistryRoot);
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
				using RegistryKey? registryKey2 = registryKey.OpenSubKey(text);
				if (registryKey2 != null)
				{
					AppProfile item = ReadAppProfile(text, registryKey2);
					if (item.Hidden && ShowHiddenAppsCheck?.IsChecked != true)
					{
						continue;
					}
					loadedApps.Add(item);
				}
			}
		}
		foreach (AppProfile app in loadedApps.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase))
		{
			_apps.Add(app);
		}
		_loading = false;
		if (AppsEmptyText != null)
		{
			AppsEmptyText.Visibility = _apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		}
	}

	private static void DiscoverKnownSteamApps()
	{
		foreach (string library in GetSteamLibraryFolders())
		{
			string steamApps = Path.Combine(library, "steamapps");
			foreach (KnownSteamVrApp app in KnownSteamVrApps)
			{
				string manifest = Path.Combine(steamApps, "appmanifest_" + app.AppId + ".acf");
				if (!File.Exists(manifest))
				{
					continue;
				}
				string installDir = ReadAcfValue(manifest, "installdir");
				string modulePath = "";
				if (!string.IsNullOrWhiteSpace(installDir))
				{
					string guessed = Path.Combine(steamApps, "common", installDir, app.ExeName);
					modulePath = File.Exists(guessed) ? guessed : "";
				}
				EnsureDiscoveredAppProfile(app.ExeName, app.DisplayName, modulePath, app.XrType, "Steam " + app.AppId);
			}
		}
	}

	private static IEnumerable<string> GetSteamLibraryFolders()
	{
		HashSet<string> folders = new(StringComparer.OrdinalIgnoreCase);
		string steamPath = "";
		using (RegistryKey? steamKey = Registry.CurrentUser.OpenSubKey("Software\\Valve\\Steam"))
		{
			steamPath = Convert.ToString(steamKey?.GetValue("SteamPath", "")) ?? "";
			if (string.IsNullOrWhiteSpace(steamPath))
			{
				string steamExe = Convert.ToString(steamKey?.GetValue("SteamExe", "")) ?? "";
				steamPath = string.IsNullOrWhiteSpace(steamExe) ? "" : Path.GetDirectoryName(steamExe) ?? "";
			}
		}
		if (!string.IsNullOrWhiteSpace(steamPath) && Directory.Exists(steamPath))
		{
			folders.Add(steamPath.Replace('/', Path.DirectorySeparatorChar));
			string libraryFolders = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
			if (File.Exists(libraryFolders))
			{
				foreach (string path in ReadSteamLibraryPaths(libraryFolders))
				{
					if (Directory.Exists(path))
					{
						folders.Add(path);
					}
				}
			}
		}
		return folders;
	}

	private static IEnumerable<string> ReadSteamLibraryPaths(string libraryFoldersPath)
	{
		string text = File.ReadAllText(libraryFoldersPath);
		HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
		foreach (Match match in Regex.Matches(text, "\"path\"\\s+\"(?<path>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase))
		{
			string path = UnescapeSteamValue(match.Groups["path"].Value);
			if (!string.IsNullOrWhiteSpace(path) && paths.Add(path))
			{
				yield return path.Replace('/', Path.DirectorySeparatorChar);
			}
		}
		foreach (Match match in Regex.Matches(text, "\"\\d+\"\\s+\"(?<path>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase))
		{
			string path = UnescapeSteamValue(match.Groups["path"].Value);
			if (!string.IsNullOrWhiteSpace(path) && paths.Add(path))
			{
				yield return path.Replace('/', Path.DirectorySeparatorChar);
			}
		}
	}

	private static string ReadAcfValue(string manifestPath, string key)
	{
		string text = File.ReadAllText(manifestPath);
		Match match = Regex.Match(text, "\"" + Regex.Escape(key) + "\"\\s+\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.IgnoreCase);
		return match.Success ? UnescapeSteamValue(match.Groups["value"].Value) : "";
	}

	private static string UnescapeSteamValue(string value)
	{
		return value.Replace("\\\\", "\\").Replace("\\\"", "\"");
	}

	private static string SanitizeAppKey(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return "Unknown.exe";
		}
		char[] invalid = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
		foreach (char ch in invalid)
		{
			value = value.Replace(ch, '_');
		}
		return value;
	}

	private static void EnsureDiscoveredAppProfile(string exeName, string displayName, string modulePath, string xrType, string source)
	{
		string keyName = SanitizeAppKey(Path.GetFileName(exeName));
		if (AppKeyBlacklist.Contains(keyName) || AppKeyBlacklist.Contains(Path.GetFileNameWithoutExtension(keyName)))
		{
			return;
		}
		using RegistryKey appKey = Registry.CurrentUser.CreateSubKey(AppRegistryRoot + "\\" + keyName, writable: true) ?? throw new InvalidOperationException("Could not write app profile.");
		SetRegistryStringIfEmpty(appKey, "display_name", string.IsNullOrWhiteSpace(displayName) ? Path.GetFileNameWithoutExtension(keyName) : displayName);
		if (!string.IsNullOrWhiteSpace(modulePath))
		{
			SetRegistryStringIfEmpty(appKey, "module", modulePath);
		}
		SetRegistryStringIfEmpty(appKey, "discovered_source", source);
		SetRegistryStringIfEmpty(appKey, "discovered_xr_type", xrType);
		SetRegistryDwordIfMissing(appKey, "app_enabled", 1);
		SetRegistryDwordIfMissing(appKey, "profile_enabled", 0);
	}

	private static void SetRegistryStringIfEmpty(RegistryKey key, string name, string value)
	{
		if (string.IsNullOrWhiteSpace(Convert.ToString(key.GetValue(name, ""))))
		{
			key.SetValue(name, value, RegistryValueKind.String);
		}
	}

	private static void SetRegistryDwordIfMissing(RegistryKey key, string name, int value)
	{
		if (key.GetValue(name) == null)
		{
			key.SetValue(name, value, RegistryValueKind.DWord);
		}
	}

	private AppProfile ReadAppProfile(string keyName, RegistryKey appKey)
	{
		double num = ReadScaleSetting("total_render_height", ReadScaleSetting("total_share", ReadScaleSetting("vertical_tangent", 0.18)));
		double fallback = ReadScaleSetting("top_tangent", num * 0.5);
		double fallback2 = ReadScaleSetting("bottom_tangent", num * 0.5);
		double fallback3 = ReadScaleSetting("horizontal_render_width", 0.8);
		bool fallbackMaskEnabled = ReadBoolSetting(MaskEnabledKey, fallback: false);
		double fallbackMaskVertical = ReadScaleSetting("mask_vertical", 1.0);
		double fallbackMaskHorizontal = ReadScaleSetting("mask_horizontal", 1.0);
		bool fallbackMaskRounded = ReadBoolSetting(MaskRoundedKey, fallback: true);
		double fallbackMaskCorner = ReadScaleSetting(MaskCornerKey, 0.5);
		double fallbackMaskOffsetY = ReadSignedScaleSetting(MaskOffsetYKey, 0.0);
		double fallbackMaskTopBias = ReadSignedScaleSetting(MaskTopBiasKey, fallbackMaskOffsetY);
		double fallbackMaskBottomBias = ReadSignedScaleSetting(MaskBottomBiasKey, fallbackMaskOffsetY);
		double fallbackMaskLeftBias = ReadSignedScaleSetting(MaskLeftBiasKey, 0.0);
		double fallbackMaskRightBias = ReadSignedScaleSetting(MaskRightBiasKey, 0.0);
		double fallbackMaskTopCurve = ReadSignedScaleSetting(MaskTopCurveKey, 0.0);
		double fallbackMaskBottomCurve = ReadSignedScaleSetting(MaskBottomCurveKey, 0.0);
		string rawDisplayName = Convert.ToString(appKey.GetValue("display_name", keyName)) ?? keyName;
		string text = Convert.ToString(appKey.GetValue("module", "")) ?? "";
		bool isOpenComposite = rawDisplayName.StartsWith("OpenComposite_", StringComparison.OrdinalIgnoreCase);
		string discoveredXrType = Convert.ToString(appKey.GetValue("discovered_xr_type", "")) ?? "";
		string xrType = !string.IsNullOrWhiteSpace(discoveredXrType) ? discoveredXrType : (isOpenComposite ? "OpenVR" : "OpenXR");
		// User-set custom_name wins over the layer-written display_name (the layer
		// overwrites display_name every launch, so renames must live in custom_name).
		string customName = Convert.ToString(appKey.GetValue("custom_name", null)) ?? "";
		string text2 = string.IsNullOrWhiteSpace(customName)
			? CleanAppDisplayName(rawDisplayName, keyName, text)
			: customName.Trim();
		string text3 = (string.IsNullOrWhiteSpace(text) ? keyName : Path.GetFileName(text));
		bool profileEnabled = Convert.ToInt32(appKey.GetValue("profile_enabled", 0), CultureInfo.InvariantCulture) != 0;
		bool appEnabled = Convert.ToInt32(appKey.GetValue("app_enabled", 1), CultureInfo.InvariantCulture) != 0;
		bool hidden = Convert.ToInt32(appKey.GetValue("hidden", 0), CultureInfo.InvariantCulture) != 0;
		double top = FromMillis(appKey.GetValue("top_tangent"), fallback);
		double bottom = FromMillis(appKey.GetValue("bottom_tangent"), fallback2);
		double horizontal = FromMillis(appKey.GetValue("horizontal_render_width"), fallback3);
		double renderScale = FromRenderScaleUnits(appKey.GetValue("render_scale"), 1.0);
		bool maskEnabled = Convert.ToInt32(appKey.GetValue("mask_enabled", fallbackMaskEnabled ? 1 : 0), CultureInfo.InvariantCulture) != 0;
		double maskVertical = FromMillis(appKey.GetValue("mask_vertical"), fallbackMaskVertical);
		double maskHorizontal = FromMillis(appKey.GetValue("mask_horizontal"), fallbackMaskHorizontal);
		bool maskRounded = Convert.ToInt32(appKey.GetValue("mask_rounded", fallbackMaskRounded ? 1 : 0), CultureInfo.InvariantCulture) != 0;
		double maskCorner = FromMillis(appKey.GetValue("mask_corner"), fallbackMaskCorner);
		double maskOffsetY = FromSignedMillis(appKey.GetValue("mask_offset_y"), fallbackMaskOffsetY);
		double maskTopBias = FromSignedMillis(appKey.GetValue("mask_top_bias"), maskOffsetY == 0.0 ? fallbackMaskTopBias : maskOffsetY);
		double maskBottomBias = FromSignedMillis(appKey.GetValue("mask_bottom_bias"), maskOffsetY == 0.0 ? fallbackMaskBottomBias : maskOffsetY);
		double maskLeftBias = FromSignedMillis(appKey.GetValue("mask_left_bias"), fallbackMaskLeftBias);
		double maskRightBias = FromSignedMillis(appKey.GetValue("mask_right_bias"), fallbackMaskRightBias);
		double maskTopCurve = FromSignedMillis(appKey.GetValue("mask_top_curve"), fallbackMaskTopCurve);
		double maskBottomCurve = FromSignedMillis(appKey.GetValue("mask_bottom_curve"), fallbackMaskBottomCurve);
				double visorOuterApexY = FromSignedMillis(appKey.GetValue("mask_outer_apex_y"), 0.0);
		double visorInnerLowerY = FromMillis(appKey.GetValue("mask_inner_lower_y"), 0.0);
		double visorInnerBridgeWidth = FromMillis(appKey.GetValue("mask_inner_bridge_width"), 0.5);
		double visorInnerBridgeRise = FromRiseMillis(appKey.GetValue("mask_inner_bridge_rise"), 0.0);
		double visorInnerBridgePeakX = FromPeakXMillis(appKey.GetValue("mask_inner_bridge_peak_x"), 0.5);
		double visorInnerBridgeSteepness = FromSteepMillis(appKey.GetValue("mask_inner_bridge_steepness"), 0.5);
		return new AppProfile
		{
			Key = keyName,
			ExeName = text3,
			DisplayName = text2,
			Display = text2,
			XrType = xrType,
			HookStatus = ReadHookStatus(appKey),
			AppEnabled = appEnabled,
			Hidden = hidden,
			ProfileEnabled = profileEnabled,
			Top = top,
			Bottom = bottom,
			Horizontal = horizontal,
			RenderScale = renderScale,
			MaskEnabled = maskEnabled,
			MaskVertical = maskVertical,
			MaskHorizontal = maskHorizontal,
			MaskRounded = maskRounded,
			MaskCorner = maskCorner,
			MaskTopBias = maskTopBias,
			MaskBottomBias = maskBottomBias,
			MaskLeftBias = maskLeftBias,
			MaskRightBias = maskRightBias,
			MaskTopCurve = maskTopCurve,
			MaskBottomCurve = maskBottomCurve,
			VisorSize = FromMillis(appKey.GetValue("visor_size"), 0.0),
			VisorOuterApexY = visorOuterApexY,
			VisorInnerLowerY = visorInnerLowerY,
			VisorInnerBridgeWidth = visorInnerBridgeWidth,
			VisorInnerBridgeRise = visorInnerBridgeRise,
			VisorInnerBridgePeakX = visorInnerBridgePeakX,
			VisorInnerBridgeSteepness = visorInnerBridgeSteepness
		};
	}

	private static string ReadHookStatus(RegistryKey appKey)
	{
		if (appKey.GetValue("last_seen") == null)
		{
			return "Not hooked";
		}
		object? resultValue = appKey.GetValue("last_result");
		if (resultValue != null)
		{
			try
			{
				if (Convert.ToInt32(resultValue, CultureInfo.InvariantCulture) != 0)
				{
					return "Failed";
				}
			}
			catch
			{
				return "Failed";
			}
		}
		return "Hooked";
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

	private void AppEnabled_Click(object sender, RoutedEventArgs e)
	{
		if (_loading || !(sender is CheckBox { DataContext: AppProfile dataContext }))
		{
			return;
		}
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(AppRegistryRoot + "\\" + dataContext.Key, writable: true) ?? throw new InvalidOperationException("Could not write app profile.");
		registryKey.SetValue("app_enabled", dataContext.AppEnabled ? 1 : 0, RegistryValueKind.DWord);
		StatusText.Text = (dataContext.AppEnabled ? ("Layer enabled for " + dataContext.DisplayName + ". Restart the VR game.") : ("Layer disabled for " + dataContext.DisplayName + ". Restart the VR game."));
	}

	private void ReloadApps_Click(object sender, RoutedEventArgs e)
	{
		LoadAppProfiles();
		StatusText.Text = ForefrontStatusMessage() ?? "App list reloaded.";
	}

	private void AddApp_Click(object sender, RoutedEventArgs e)
	{
		OpenFileDialog dialog = new()
		{
			Title = "Add VR application",
			Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*",
			CheckFileExists = true,
			Multiselect = false
		};
		if (dialog.ShowDialog(this) != true)
		{
			return;
		}
		string exeName = Path.GetFileName(dialog.FileName);
		string displayName = Path.GetFileNameWithoutExtension(dialog.FileName);
		EnsureDiscoveredAppProfile(exeName, displayName, dialog.FileName, "OpenXR", "Manual");
		LoadAppProfiles();
		StatusText.Text = displayName + " added. If it stays 'Not hooked' after launch, OpenXR did not load the ViewLab layer into that process.";
	}

	private string? ForefrontStatusMessage()
	{
		AppProfile? forefront = _apps.FirstOrDefault(a => a.Key.Equals("Forefront_Internal.exe", StringComparison.OrdinalIgnoreCase))
			?? _apps.FirstOrDefault(a => a.Key.Equals("Forefront.exe", StringComparison.OrdinalIgnoreCase));
		if (forefront == null)
		{
			return null;
		}
		return forefront.HookStatus switch
		{
			"Hooked" => "Forefront found and hooked by ViewLab.",
			"Failed" => "Forefront found, but OpenXR instance creation failed while ViewLab was in the layer chain. Check the log.",
			_ => "Forefront found, but ViewLab has not hooked it yet. Launch it, then send ViewLab.log so the Forefront diagnostic marker can show where startup stopped."
		};
	}

	private void AppsGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == System.Windows.Input.Key.Delete) { ToggleSelectedAppHidden(); e.Handled = true; }
	}

	private void HideApp_Click(object sender, RoutedEventArgs e) => ToggleSelectedAppHidden();

	private void ShowHiddenApps_Changed(object sender, RoutedEventArgs e)
	{
		if (!_loading)
		{
			LoadAppProfiles();
			StatusText.Text = ShowHiddenAppsCheck.IsChecked == true ? "Showing hidden apps." : "Hidden apps are hidden.";
		}
	}

	private void ToggleSelectedAppHidden()
	{
		if (AppsGrid.SelectedItem is not AppProfile app)
		{
			if (ShowHiddenAppsCheck.IsChecked != true)
			{
				ShowHiddenAppsCheck.IsChecked = true;
				LoadAppProfiles();
				StatusText.Text = "Showing hidden apps. Select one to unhide it.";
				return;
			}
			StatusText.Text = "Select an app row first.";
			return;
		}
		bool newHidden = !app.Hidden;
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(AppRegistryRoot + "\\" + app.Key, writable: true) ?? throw new InvalidOperationException("Could not write app profile.");
		registryKey.SetValue("hidden", newHidden ? 1 : 0, RegistryValueKind.DWord);
		app.Hidden = newHidden;
		if (newHidden && ShowHiddenAppsCheck.IsChecked != true)
		{
			_apps.Remove(app);
			if (AppsEmptyText != null)
			{
				AppsEmptyText.Visibility = _apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
			}
		}
		StatusText.Text = newHidden ? ("Hidden " + app.DisplayName + ".") : ("Unhidden " + app.DisplayName + ".");
	}

	private void SetAppHidden(AppProfile app, bool hidden)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(AppRegistryRoot + "\\" + app.Key, writable: true) ?? throw new InvalidOperationException("Could not write app profile.");
		registryKey.SetValue("hidden", hidden ? 1 : 0, RegistryValueKind.DWord);
		app.Hidden = hidden;
		if (hidden && ShowHiddenAppsCheck?.IsChecked != true)
		{
			_apps.Remove(app);
		}
		if (AppsEmptyText != null)
		{
			AppsEmptyText.Visibility = _apps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
		}
	}

	private (bool enabled, double vertical, double horizontal, bool rounded, double corner, double topBias, double bottomBias, double leftBias, double rightBias, double topCurve, double bottomCurve, double visorSize, double visorOuterApexY, double visorInnerLowerY, double visorInnerBridgeWidth, double visorInnerBridgeRise, double visorInnerBridgePeakX, double visorInnerBridgeSteepness) CurrentGlobalMaskValues()
	{
		TryReadTextBox(MaskVerticalBox, out var maskVertical);
		TryReadTextBox(MaskHorizontalBox, out var maskHorizontal);
		return (
			MaskEnabledCheck?.IsChecked == true,
			maskVertical,
			maskHorizontal,
			MaskRoundedCheck?.IsChecked == true,
			1.0 - (MaskRoundnessSlider?.Value ?? 0.5),
			0.0,
			0.0,
			0.0,
			0.0,
			0.0,
			0.0,
			MaskSizeSlider?.Value ?? 1.0,
			MaskApexYSlider?.Value ?? 0.0,
			MaskInnerLowerSlider?.Value ?? 0.0,
			MaskInnerBridgeSlider?.Value ?? 0.5,
			MaskInnerBridgeRiseSlider?.Value ?? 0.0,
			MaskInnerBridgePeakXSlider?.Value ?? 0.5,
			MaskInnerBridgeSteepnessSlider?.Value ?? 0.5);
	}

	private void ApplyGlobalMaskValuesToProfile(AppProfile appProfile)
	{
		var global = CurrentGlobalMaskValues();
		appProfile.MaskEnabled = global.enabled;
		appProfile.MaskVertical = global.vertical;
		appProfile.MaskHorizontal = global.horizontal;
		appProfile.MaskRounded = global.rounded;
		appProfile.MaskCorner = global.corner;
		appProfile.MaskTopBias = global.topBias;
		appProfile.MaskBottomBias = global.bottomBias;
		appProfile.MaskLeftBias = global.leftBias;
		appProfile.MaskRightBias = global.rightBias;
		appProfile.MaskTopCurve = global.topCurve;
		appProfile.MaskBottomCurve = global.bottomCurve;
		appProfile.VisorSize = 0;
		appProfile.VisorOuterApexY = 0;
		appProfile.VisorInnerLowerY = 0;
		appProfile.VisorInnerBridgeWidth = 0;
		appProfile.VisorInnerBridgeRise = 0;
		appProfile.VisorInnerBridgePeakX = 0;
		appProfile.VisorInnerBridgeSteepness = 0;
	}


	private void AppsGrid_DoubleClick(object sender, MouseButtonEventArgs e)
	{
		if (!(AppsGrid.SelectedItem is AppProfile appProfile))
		{
			return;
		}

		if (!appProfile.AppEnabled && !appProfile.Hidden)
		{
			StatusText.Text = "Enable the app first, then double-click to set custom values.";
			return;
		}

		var globalMask = CurrentGlobalMaskValues();
		ProfileWindow profileWindow = new ProfileWindow(appProfile.DisplayName, appProfile.ExeName, appProfile.Hidden, appProfile.Top, appProfile.Bottom, appProfile.Horizontal, appProfile.RenderScale, appProfile.MaskEnabled, appProfile.MaskVertical, appProfile.MaskHorizontal, appProfile.MaskRounded, appProfile.MaskCorner, appProfile.MaskTopBias, appProfile.MaskBottomBias, appProfile.MaskLeftBias, appProfile.MaskRightBias, appProfile.MaskTopCurve, appProfile.MaskBottomCurve, globalMask.enabled, globalMask.vertical, globalMask.horizontal, globalMask.corner, globalMask.leftBias, globalMask.topBias, appProfile.VisorSize, appProfile.VisorOuterApexY, appProfile.VisorInnerLowerY, appProfile.VisorInnerBridgeWidth, appProfile.VisorInnerBridgeRise, appProfile.VisorInnerBridgePeakX, appProfile.VisorInnerBridgeSteepness, globalMask.visorSize, globalMask.visorOuterApexY, globalMask.visorInnerLowerY, globalMask.visorInnerBridgeWidth, globalMask.visorInnerBridgeRise, globalMask.visorInnerBridgePeakX, globalMask.visorInnerBridgeSteepness, true) // Stencil outer edges only is permanently enabled
		{
			Owner = this
		};
		if (profileWindow.ShowDialog() == true)
		{
			if (profileWindow.HiddenChanged)
			{
				SetAppHidden(appProfile, profileWindow.HiddenValue);
			}
			string newName = profileWindow.DisplayName;
			if (!string.IsNullOrWhiteSpace(newName) && newName != appProfile.DisplayName)
			{
				WriteAppDisplayName(appProfile, newName);
				appProfile.DisplayName = newName;
				appProfile.Display = newName;
			}
			if (profileWindow.UseGlobal)
			{
				ResetAppCustomProfile(appProfile);
				ApplyGlobalMaskValuesToProfile(appProfile);
				appProfile.ProfileEnabled = false;
			}
			else
			{
				WriteAppCustomProfile(appProfile, profileWindow.TopValue, profileWindow.BottomValue, profileWindow.HorizontalValue, profileWindow.RenderScaleValue, profileWindow.MaskEnabledValue, profileWindow.MaskVerticalValue, profileWindow.MaskHorizontalValue, profileWindow.MaskRoundedValue, profileWindow.MaskCornerValue, profileWindow.MaskTopBiasValue, profileWindow.MaskBottomBiasValue, profileWindow.MaskLeftBiasValue, profileWindow.MaskRightBiasValue, profileWindow.MaskTopCurveValue, profileWindow.MaskBottomCurveValue, profileWindow.VisorSizeValue, profileWindow.VisorOuterApexYValue, profileWindow.VisorInnerLowerYValue, profileWindow.VisorInnerBridgeWidthValue, profileWindow.VisorInnerBridgeRiseValue, profileWindow.VisorInnerBridgePeakXValue, profileWindow.VisorInnerBridgeSteepnessValue, profileEnabled: true);
				appProfile.Top = profileWindow.TopValue;
				appProfile.Bottom = profileWindow.BottomValue;
				appProfile.Horizontal = profileWindow.HorizontalValue;
				appProfile.RenderScale = profileWindow.RenderScaleValue;
				appProfile.MaskEnabled = profileWindow.MaskEnabledValue;
				appProfile.MaskVertical = profileWindow.MaskVerticalValue;
				appProfile.MaskHorizontal = profileWindow.MaskHorizontalValue;
				appProfile.MaskRounded = profileWindow.MaskRoundedValue;
				appProfile.MaskCorner = profileWindow.MaskCornerValue;
				appProfile.MaskTopBias = profileWindow.MaskTopBiasValue;
				appProfile.MaskBottomBias = profileWindow.MaskBottomBiasValue;
				appProfile.MaskLeftBias = profileWindow.MaskLeftBiasValue;
				appProfile.MaskRightBias = profileWindow.MaskRightBiasValue;
				appProfile.MaskTopCurve = profileWindow.MaskTopCurveValue;
				appProfile.MaskBottomCurve = profileWindow.MaskBottomCurveValue;
				appProfile.VisorSize = profileWindow.VisorSizeValue;

				appProfile.VisorOuterApexY = profileWindow.VisorOuterApexYValue;
				appProfile.VisorInnerLowerY = profileWindow.VisorInnerLowerYValue;
				appProfile.VisorInnerBridgeWidth = profileWindow.VisorInnerBridgeWidthValue;
				appProfile.VisorInnerBridgeRise = profileWindow.VisorInnerBridgeRiseValue;
				appProfile.VisorInnerBridgePeakX = profileWindow.VisorInnerBridgePeakXValue;
				appProfile.VisorInnerBridgeSteepness = profileWindow.VisorInnerBridgeSteepnessValue;
				appProfile.ProfileEnabled = true;
			}
			appProfile.RefreshSummary();
			StatusText.Text = "Saved app profile. Restart the VR game.";
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
		ApplyGlobalMaskValuesToProfile(appProfile);
		appProfile.ProfileEnabled = false;
		StatusText.Text = "Reset " + appProfile.DisplayName + " to global values. Restart the VR game.";
	}

	private static void WriteAppDisplayName(AppProfile profile, string name)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(AppRegistryRoot + "\\" + profile.Key, writable: true) ?? throw new InvalidOperationException("Could not write app profile.");
		registryKey.SetValue("custom_name", name, RegistryValueKind.String);
	}

	private static void WriteAppCustomProfile(AppProfile profile, double top, double bottom, double horizontal, double renderScale, bool maskEnabled, double maskVertical, double maskHorizontal, bool maskRounded, double maskCorner, double maskTopBias, double maskBottomBias, double maskLeftBias, double maskRightBias, double maskTopCurve, double maskBottomCurve, double visorSize, double visorOuterApexY, double visorInnerLowerY, double visorInnerBridgeWidth, double visorInnerBridgeRise, double visorInnerBridgePeakX, double visorInnerBridgeSteepness, bool profileEnabled)
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
		registryKey.SetValue("render_scale", renderScale.ToString("0.###############", CultureInfo.InvariantCulture), RegistryValueKind.String);
		registryKey.DeleteValue("mask_enabled", throwOnMissingValue: false);
		registryKey.SetValue("mask_vertical", ToMillis(maskVertical), RegistryValueKind.DWord);
		registryKey.SetValue("mask_horizontal", ToMillis(maskHorizontal), RegistryValueKind.DWord);
		registryKey.SetValue("mask_rounded", maskRounded ? 1 : 0, RegistryValueKind.DWord);
		registryKey.SetValue("mask_corner", ToMillis(maskCorner), RegistryValueKind.DWord);
		registryKey.SetValue("mask_top_bias", ToSignedMillis(maskTopBias), RegistryValueKind.DWord);
		registryKey.SetValue("mask_bottom_bias", ToSignedMillis(maskBottomBias), RegistryValueKind.DWord);
		registryKey.SetValue("mask_left_bias", ToSignedMillis(maskLeftBias), RegistryValueKind.DWord);
		registryKey.SetValue("mask_right_bias", ToSignedMillis(maskRightBias), RegistryValueKind.DWord);
		registryKey.SetValue("mask_top_curve", ToSignedMillis(maskTopCurve), RegistryValueKind.DWord);
		registryKey.SetValue("mask_bottom_curve", ToSignedMillis(maskBottomCurve), RegistryValueKind.DWord);
		registryKey.SetValue("visor_size", ToMillis(visorSize), RegistryValueKind.DWord);

		registryKey.SetValue("mask_outer_apex_y", ToSignedMillis(visorOuterApexY), RegistryValueKind.DWord);
		registryKey.SetValue("mask_inner_lower_y", ToMillis(visorInnerLowerY), RegistryValueKind.DWord);
		registryKey.SetValue("mask_inner_bridge_width", ToMillis(visorInnerBridgeWidth), RegistryValueKind.DWord);
		registryKey.SetValue("mask_inner_bridge_rise", ToRiseMillis(visorInnerBridgeRise), RegistryValueKind.DWord);
		registryKey.SetValue("mask_inner_bridge_peak_x", ToPeakXMillis(visorInnerBridgePeakX), RegistryValueKind.DWord);
		registryKey.SetValue("mask_inner_bridge_steepness", ToSteepMillis(visorInnerBridgeSteepness), RegistryValueKind.DWord);
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
		registryKey.DeleteValue("render_scale", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_enabled", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_vertical", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_horizontal", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_rounded", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_corner", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_offset_x", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_offset_y", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_top_bias", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_bottom_bias", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_left_bias", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_right_bias", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_top_curve", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_bottom_curve", throwOnMissingValue: false);
		registryKey.DeleteValue("visor_size", throwOnMissingValue: false);
		registryKey.DeleteValue("visor_width", throwOnMissingValue: false);
		registryKey.DeleteValue("visor_height", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_outer_apex_y", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_inner_lower_y", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_inner_bridge_width", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_inner_bridge_rise", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_inner_bridge_peak_x", throwOnMissingValue: false);
		registryKey.DeleteValue("mask_inner_bridge_steepness", throwOnMissingValue: false);
	}
}
