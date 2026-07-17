using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace XRViewLab.UI;

public sealed record PerformanceTraceSample(long Sequence, long Qpc, double ElapsedMs, double ActualMs,
	double TargetMs, double DeviationMs, double AppMs, double WaitMs, double SubmitMs, double DisplayMs,
	int Marker, double? GpuPercent = null, ulong WarningMask = 0, ulong CriticalMask = 0, ulong VisibleAlarmMask = 0);
public sealed record PerformanceTraceMarker(int Number, long Qpc, double ElapsedMs);
public sealed record PerformanceTraceEvent(double ElapsedMs, string Kind, string Label, int Severity);
public sealed record PerformanceTraceSummary(double AverageMs, double MedianMs, double P95Ms, double P99Ms,
	double MaximumMs, int CadenceMisses, long EstimatedMissedDisplayIntervals, int GpuWarningPeriods,
	double GpuWarningSeconds, int GpuCriticalPeriods, double GpuCriticalSeconds);

public sealed class PerformanceTrace
{
	private static readonly string[] MetricNames =
	{
		"CPU (%)", "GPU (%)", "App workload (% budget)", "VR frame interval (ms)", "CPU peak (%)", "CPU clock (MHz)", "RAM (%)", "Committed memory (%)", "VRAM (%)", "System headroom (%)",
		"FPS", "Frame interval (ms)", "Ping (ms)", "Packet loss (%)", "Jitter (ms)", "Network status"
	};

	public int SchemaVersion { get; private set; }
	public long Frequency { get; private set; }
	public long StartQpc { get; private set; }
	public DateTimeOffset? StartUtc { get; private set; }
	public List<PerformanceTraceSample> Samples { get; } = new();
	public List<PerformanceTraceMarker> Markers { get; } = new();
	public List<PerformanceTraceEvent> Events { get; } = new();
	public bool HasGpuTelemetry => Samples.Any(s => s.GpuPercent.HasValue);

	public static PerformanceTrace Load(string path)
	{
		using var reader = new StreamReader(path);
		string? signature = reader.ReadLine();
		if (signature is null || !signature.StartsWith("ViewLabPerformanceTrace,", StringComparison.Ordinal) ||
			!int.TryParse(signature.AsSpan(signature.IndexOf(',') + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) ||
			version is < 1 or > 2)
			throw new InvalidDataException("This is not a supported ViewLab performance trace.");

		var result = new PerformanceTrace { SchemaVersion = version };
		string? line;
		while ((line = reader.ReadLine()) != null)
		{
			string[] p = line.Split(',');
			if (p.Length < 2) continue;
			try
			{
				if (p[0] == "frequency") result.Frequency = ParseLong(p[1]);
				else if (p[0] == "start_qpc") result.StartQpc = ParseLong(p[1]);
				else if (p[0] == "start_utc_filetime") result.StartUtc = new DateTimeOffset(DateTime.FromFileTimeUtc(ParseLong(p[1])));
				else if (p[0] == "S" && p.Length >= 12)
				{
					double? gpu = version >= 2 && p.Length >= 13 && ParseDouble(p[12]) >= 0 ? ParseDouble(p[12]) : null;
					ulong warning = version >= 2 && p.Length >= 14 ? ParseUlong(p[13]) : 0;
					ulong critical = version >= 2 && p.Length >= 15 ? ParseUlong(p[14]) : 0;
					ulong visible = version >= 2 && p.Length >= 16 ? ParseUlong(p[15]) : 0;
					result.Samples.Add(new(ParseLong(p[1]), ParseLong(p[2]), ParseDouble(p[3]), ParseDouble(p[4]),
						ParseDouble(p[5]), ParseDouble(p[6]), ParseDouble(p[7]), ParseDouble(p[8]), ParseDouble(p[9]),
						ParseDouble(p[10]), ParseInt(p[11]), gpu, warning, critical, visible));
				}
				else if (p[0] == "M" && p.Length >= 4)
					result.Markers.Add(new(ParseInt(p[1]), ParseLong(p[2]), ParseDouble(p[3])));
			}
			catch (FormatException) { }
			catch (OverflowException) { }
		}
		result.BuildEvents();
		return result;
	}

	public PerformanceTraceSummary Analyze()
	{
		if (Samples.Count == 0) return new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
		double[] values = Samples.Select(s => s.ActualMs).Order().ToArray();
		int misses = 0; long missedIntervals = 0;
		foreach (PerformanceTraceSample sample in Samples)
		{
			double activeBudget = Math.Max(sample.TargetMs, sample.DisplayMs);
			if (activeBudget <= 0 || sample.ActualMs <= activeBudget * 1.5) continue;
			misses++;
			if (sample.DisplayMs > 0)
			{
				long actualIntervals = Math.Max(1, (long)Math.Round(sample.ActualMs / sample.DisplayMs));
				long targetIntervals = Math.Max(1, (long)Math.Round(activeBudget / sample.DisplayMs));
				missedIntervals += Math.Max(0, actualIntervals - targetIntervals);
			}
		}
		(int warningPeriods, double warningMs) = GpuPeriods(90);
		(int criticalPeriods, double criticalMs) = GpuPeriods(98);
		return new(values.Average(), Percentile(values, .5), Percentile(values, .95), Percentile(values, .99),
			values[^1], misses, missedIntervals, warningPeriods, warningMs / 1000.0, criticalPeriods, criticalMs / 1000.0);
	}

	private (int Periods, double DurationMs) GpuPeriods(double threshold)
	{
		int periods = 0; double duration = 0; bool active = false;
		for (int i = 0; i < Samples.Count; ++i)
		{
			bool now = Samples[i].GpuPercent is double gpu && gpu >= threshold;
			if (now && !active) periods++;
			if (now && i + 1 < Samples.Count) duration += Math.Clamp(Samples[i + 1].ElapsedMs - Samples[i].ElapsedMs, 0, 1000);
			active = now;
		}
		return (periods, duration);
	}

	private void BuildEvents()
	{
		Events.Clear();
		if (Samples.Count == 0) return;
		Events.Add(new(Samples[0].ElapsedMs, "session", "Session trace begins", 0));
		foreach (PerformanceTraceMarker marker in Markers)
			Events.Add(new(marker.ElapsedMs, "marker", $"Marker {marker.Number}", 0));

		ulong previousWarning = 0, previousCritical = 0;
		bool cadenceMissActive = false;
		foreach (PerformanceTraceSample sample in Samples)
		{
			double budget = Math.Max(sample.TargetMs, sample.DisplayMs);
			bool cadenceMiss = budget > 0 && sample.ActualMs > budget * 1.5;
			if (cadenceMiss && !cadenceMissActive)
				Events.Add(new(sample.ElapsedMs, "cadence", "Estimated cadence miss begins", 1));
			else if (!cadenceMiss && cadenceMissActive)
				Events.Add(new(sample.ElapsedMs, "cadence", "Estimated cadence recovered", 0));
			cadenceMissActive = cadenceMiss;
			ulong changed = previousWarning ^ sample.WarningMask;
			ulong changedCritical = previousCritical ^ sample.CriticalMask;
			for (int bit = 0; bit < MetricNames.Length; ++bit)
			{
				ulong mask = 1UL << bit;
				if ((changedCritical & mask) != 0)
					Events.Add(new(sample.ElapsedMs, "alarm", $"{MetricNames[bit]} {((sample.CriticalMask & mask) != 0 ? "critical" : "critical cleared")}", (sample.CriticalMask & mask) != 0 ? 2 : 0));
				else if ((changed & mask) != 0)
					Events.Add(new(sample.ElapsedMs, "alarm", $"{MetricNames[bit]} {((sample.WarningMask & mask) != 0 ? "warning" : "warning cleared")}", (sample.WarningMask & mask) != 0 ? 1 : 0));
			}
			previousWarning = sample.WarningMask; previousCritical = sample.CriticalMask;
		}
		Events.Add(new(Samples[^1].ElapsedMs, "session", "Last durable sample", 0));
		Events.Sort((a, b) => a.ElapsedMs.CompareTo(b.ElapsedMs));
	}

	private static double Percentile(double[] sorted, double fraction)
	{
		int index = Math.Clamp((int)Math.Ceiling(fraction * sorted.Length) - 1, 0, sorted.Length - 1);
		return sorted[index];
	}

	private static double ParseDouble(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
	private static long ParseLong(string value) => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
	private static ulong ParseUlong(string value) => ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
	private static int ParseInt(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
}
