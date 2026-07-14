using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace XRViewLab.UI;

public sealed record PerformanceTraceSample(long Sequence, long Qpc, double ElapsedMs, double ActualMs,
	double TargetMs, double DeviationMs, double AppMs, double WaitMs, double SubmitMs, double DisplayMs, int Marker);
public sealed record PerformanceTraceMarker(int Number, long Qpc, double ElapsedMs);

public sealed class PerformanceTrace
{
	public List<PerformanceTraceSample> Samples { get; } = new();
	public List<PerformanceTraceMarker> Markers { get; } = new();

	public static PerformanceTrace Load(string path)
	{
		using var reader = new StreamReader(path);
		if (!string.Equals(reader.ReadLine(), "ViewLabPerformanceTrace,1", StringComparison.Ordinal))
			throw new InvalidDataException("This is not a ViewLab performance trace.");
		var result = new PerformanceTrace(); string? line;
		while ((line = reader.ReadLine()) != null)
		{
			string[] p = line.Split(',');
			if (p.Length < 4) continue;
			if (p[0] == "S" && p.Length >= 12)
				result.Samples.Add(new(ParseLong(p[1]), ParseLong(p[2]), ParseDouble(p[3]), ParseDouble(p[4]),
					ParseDouble(p[5]), ParseDouble(p[6]), ParseDouble(p[7]), ParseDouble(p[8]), ParseDouble(p[9]),
					ParseDouble(p[10]), ParseInt(p[11])));
			else if (p[0] == "M") result.Markers.Add(new(ParseInt(p[1]), ParseLong(p[2]), ParseDouble(p[3])));
		}
		return result;
	}

	private static double ParseDouble(string value) => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
	private static long ParseLong(string value) => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
	private static int ParseInt(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
}
