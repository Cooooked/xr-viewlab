using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
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
		var findings = FailureDiagnostics.Analyze(logText, _layerRegistered, anyLogLineToday, _crashForThisWindowSession);

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

	private void Refresh_Click(object sender, RoutedEventArgs e) => LoadFindings();
	private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
