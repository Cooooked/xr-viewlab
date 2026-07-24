using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace XRViewLab.UI;

public partial class FailureDiagnosticsWindow : Window
{
	private sealed class Row
	{
		public string Category { get; init; } = "";
		public string Summary { get; init; } = "";
		public string EvidenceLabeled { get; init; } = "";
		public string RecommendationLabeled { get; init; } = "";
		public string CertaintyLabel { get; init; } = "";
		public Brush CertaintyBrush { get; init; } = Brushes.Gray;
	}

	private readonly string _configDirectory;
	private readonly bool _layerRegistered;
	private CrashMarker.Record? _crashForThisWindowSession;
	private bool _crashRead;

	public FailureDiagnosticsWindow(string configDirectory, bool layerRegistered)
	{
		_configDirectory = configDirectory;
		_layerRegistered = layerRegistered;
		InitializeComponent();
		LoadFindings();
	}

	private void LoadFindings()
	{
		string logPath = Path.Combine(_configDirectory, "Logs", "ViewLab.log");
		string logText = "";
		bool anyLogLineToday = false;
		try
		{
			if (File.Exists(logPath))
			{
				logText = File.ReadAllText(logPath);
				string todayPrefix = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
				anyLogLineToday = logText.Contains(todayPrefix, StringComparison.Ordinal);
			}
		}
		catch { /* an unreadable log is treated the same as no log — no fabricated finding */ }

		// Read (and clear) the crash marker once per window session, not once per Refresh click —
		// otherwise clicking Refresh would silently make a real crash finding disappear.
		if (!_crashRead) { _crashForThisWindowSession = CrashMarker.TryReadAndClear(_configDirectory); _crashRead = true; }

		(string? state, string? detail) broker = ReadJsonStateDetail(Path.Combine(_configDirectory, "notification-broker-status.json"));
		(string? state, string? detail) iracing = ReadJsonStateDetail(Path.Combine(_configDirectory, "iracing-status.json"));

		// The background helper is a separate process. Its absence produces NO error anywhere — the
		// status files simply stop being written — so it is checked directly, and only reported when
		// the user actually has a feature switched on that depends on it.
		bool brokerRunning = Process.GetProcessesByName("ViewLab.NotificationBroker").Length > 0;
		bool brokerFeaturesOn = AnyBrokerFeatureEnabled();
		TimeSpan? statusAge = NewestStatusAge();

		var findings = FailureDiagnostics.Analyze(logText, _layerRegistered, anyLogLineToday, _crashForThisWindowSession,
			broker.state, broker.detail, iracing.detail,
			brokerRunning, brokerFeaturesOn, statusAge, ReadRecentSystemEvents());

		Row[] rows = findings.Select(f => new Row
		{
			Category = f.Category,
			Summary = f.Summary,
			EvidenceLabeled = "Evidence: " + f.Evidence,
			RecommendationLabeled = f.Recommendation,
			CertaintyLabel = f.Certainty == FailureCertainty.Confirmed ? "CONFIRMED" : "LIKELY",
			CertaintyBrush = f.Certainty == FailureCertainty.Confirmed
				? (Brush)FindResource("ConfirmedBrush")
				: (Brush)FindResource("LikelyBrush")
		}).ToArray();

		FindingsList.ItemsSource = rows;
		if (rows.Length == 0)
		{
			FindingsList.ItemsSource = new[] { new Row
			{
				Category = "No issues detected",
				Summary = "Nothing in the current log or last-run state matches a known failure pattern.",
				EvidenceLabeled = "",
				RecommendationLabeled = "",
				CertaintyLabel = "",
				CertaintyBrush = Brushes.Transparent
			}};
		}
	}

	// Only complain about a stopped helper if something the user switched on actually needs it.
	private bool AnyBrokerFeatureEnabled()
	{
		string ini = Path.Combine(_configDirectory, "xr-viewlab.ini");
		foreach (string key in new[] { "iracing_enabled", "notify_enabled", "media_notify_enabled", "obs_indicator_enabled" })
		{
			var value = new StringBuilder(8);
			GetPrivateProfileStringW("Settings", key, "0", value, (uint)value.Capacity, ini);
			if (value.ToString().Trim() is "1" or "true" or "True") return true;
		}
		return false;
	}

	// How long ago the helper last published anything at all.
	private TimeSpan? NewestStatusAge()
	{
		DateTimeOffset? newest = null;
		foreach (string name in new[] { "notification-broker-status.json", "iracing-status.json" })
		{
			try
			{
				string path = Path.Combine(_configDirectory, name);
				if (!File.Exists(path)) continue;
				using var doc = JsonDocument.Parse(File.ReadAllText(path));
				if (doc.RootElement.TryGetProperty("updatedUtc", out var u) &&
					DateTimeOffset.TryParse(u.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var when) &&
					(newest == null || when > newest)) newest = when;
			}
			catch { /* an unreadable status file is no signal, not a finding */ }
		}
		return newest == null ? null : DateTimeOffset.UtcNow - newest.Value;
	}

	// Crashes and anti-cheat records live in Windows' Application event log, never in ViewLab's own
	// log — without this the window could not explain a game crash or an anti-cheat block at all.
	// Bounded to the last 12 hours and a small number of entries so opening this window stays cheap.
	private static IReadOnlyList<FailureDiagnostics.SystemEvent> ReadRecentSystemEvents()
	{
		var results = new List<FailureDiagnostics.SystemEvent>();
		try
		{
			DateTime since = DateTime.Now.AddHours(-12);
			string query = "*[System[TimeCreated[@SystemTime>='" +
				since.ToUniversalTime().ToString("s", CultureInfo.InvariantCulture) + "Z'] and (Level=1 or Level=2 or Level=3)]]";
			var reader = new EventLogReader(new EventLogQuery("Application", PathType.LogName, query) { ReverseDirection = true });
			for (int read = 0; read < 400; read++)
			{
				using EventRecord? record = reader.ReadEvent();
				if (record == null) break;
				string message;
				try { message = record.FormatDescription() ?? ""; } catch { message = ""; }
				if (message.Length == 0) continue;
				results.Add(new FailureDiagnostics.SystemEvent(
					record.ProviderName ?? "", message, record.TimeCreated?.ToLocalTime() ?? DateTime.Now));
			}
		}
		catch { /* event log unavailable or access denied — no signal, never a fabricated finding */ }
		results.Reverse(); // oldest first, so LastOrDefault in the analyzer picks the most recent match
		return results;
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern uint GetPrivateProfileStringW(string section, string key, string defaultValue, StringBuilder value, uint size, string filePath);

	private void Help_Click(object sender, MouseButtonEventArgs e) =>
		BuiltInHelpWindow.Show(this, "What happened?", BuiltInHelpWindow.WhatHappenedSections);

	private static (string? state, string? detail) ReadJsonStateDetail(string path)
	{
		try
		{
			if (!File.Exists(path)) return (null, null);
			using var doc = JsonDocument.Parse(File.ReadAllText(path));
			string? state = doc.RootElement.TryGetProperty("state", out var s) ? s.GetString() : null;
			string? detail = doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;
			return (state, detail);
		}
		catch { return (null, null); /* an unreadable or malformed status file is "no signal", not an error */ }
	}

	private void Refresh_Click(object sender, RoutedEventArgs e) => LoadFindings();
	private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
