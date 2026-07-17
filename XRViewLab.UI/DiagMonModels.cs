using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XRViewLab.UI;

public enum DiagMonCaptureMode { Standard, Detailed, Trace }
public enum DiagMonTargetMode { Manual, Foreground, NewProcess, Previous }
public enum DiagMonValidation { Unreviewed, Valid, Invalid }
public enum DiagMonSessionType { Normal, Baseline, Experiment, StressTest, Incomplete }
public enum DiagMonCollectorState { Pending, Running, Complete, Partial, Missing, Failed }
public enum DiagMonComparability { Direct, Partial, None }

public sealed class DiagMonCollectorStatus
{
    public string Name { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagMonCollectorState State { get; set; }
    public string Message { get; set; } = "";
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? StoppedUtc { get; set; }
}

public sealed class DiagMonAnnotations
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagMonValidation Validation { get; set; } = DiagMonValidation.Unreviewed;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagMonSessionType SessionType { get; set; } = DiagMonSessionType.Normal;
    public string Notes { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class DiagMonFingerprint
{
    public string Hash { get; set; } = "";
    public SortedDictionary<string, string> Factors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DiagMonFrameMetrics
{
    public int PresentedFrames { get; set; }
    public double? AverageFps { get; set; }
    public double? OnePercentLowFps { get; set; }
    public double? PointOnePercentLowFps { get; set; }
    public double? MeanMs { get; set; }
    public double? MedianMs { get; set; }
    public double? P95Ms { get; set; }
    public double? P99Ms { get; set; }
    public double? P999Ms { get; set; }
    public double? WorstMs { get; set; }
    public double? StandardDeviationMs { get; set; }
    public int LongFrames { get; set; }
    public int SevereStutters { get; set; }
    public int? MeasuredDroppedFrames { get; set; }
    public string FrameTimeColumn { get; set; } = "";
    public string Heuristic { get; set; } = "Long frame > max(33.33 ms, 2x session median); severe stutter > max(100 ms, 3x session median). AllowsTearing is never treated as a dropped-frame field.";
}

public sealed class DiagMonResourceMetrics
{
    public double? ProcessCpuAveragePct { get; set; }
    public double? ProcessCpuPeakPct { get; set; }
    public double? ProcessWorkingSetPeakMb { get; set; }
    public double? ProcessPrivatePeakMb { get; set; }
    public double? ProcessPrivateGrowthMb { get; set; }
    public double? SystemCpuAveragePct { get; set; }
    public double? AvailableMemoryMinimumMb { get; set; }
    public double? GpuAveragePct { get; set; }
    public double? GpuPeakPct { get; set; }
    public double? DedicatedGpuMemoryPeakMb { get; set; }
    public double? SharedGpuMemoryPeakMb { get; set; }
}

public sealed class DiagMonBaselineMetric
{
    public string Name { get; set; } = "";
    public double? Current { get; set; }
    public double Median { get; set; }
    public double Q1 { get; set; }
    public double Q3 { get; set; }
    public double? DifferencePercent { get; set; }
    public bool Unusual { get; set; }
}

public sealed class DiagMonComparison
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagMonComparability Comparability { get; set; } = DiagMonComparability.None;
    public string Confidence { get; set; } = "None";
    public List<string> SelectedSessionIds { get; set; } = new();
    public List<string> SelectionReasons { get; set; } = new();
    public List<string> ExclusionReasons { get; set; } = new();
    public List<DiagMonBaselineMetric> Baseline { get; set; } = new();
}

public sealed class DiagMonMetrics
{
    public int SchemaVersion { get; set; } = 1;
    public DiagMonFrameMetrics Frames { get; set; } = new();
    public DiagMonResourceMetrics Resources { get; set; } = new();
    public DiagMonComparison Comparison { get; set; } = new();
    public List<string> Flags { get; set; } = new();
}

public sealed class DiagMonSession
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string DirectoryName { get; set; } = "";
    public string UserLabel { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagMonCaptureMode CaptureMode { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagMonTargetMode TargetSelection { get; set; }
    public int? TargetPid { get; set; }
    public string TargetProcessName { get; set; } = "";
    public string TargetExecutable { get; set; } = "";
    public string TargetVersion { get; set; } = "";
    public string GraphicsApi { get; set; } = "Unknown";
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset? EndUtc { get; set; }
    public double DurationSeconds { get; set; }
    public bool Complete { get; set; }
    public string StopReason { get; set; } = "";
    public string ViewLabVersion { get; set; } = "";
    public long AnchorStopwatchTicks { get; set; }
    public long StopwatchFrequency { get; set; }
    public DiagMonFingerprint Fingerprint { get; set; } = new();
    public List<DiagMonCollectorStatus> Collectors { get; set; } = new();
    public List<string> RelativeFiles { get; set; } = new();
}

public sealed class DiagMonIndexEntry
{
    public string Id { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public DateTimeOffset StartUtc { get; set; }
    public string Application { get; set; } = "";
    public string UserLabel { get; set; } = "";
    public double DurationSeconds { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagMonCaptureMode CaptureMode { get; set; }
    public double? AverageFps { get; set; }
    public double? P99Ms { get; set; }
    public int SevereStutters { get; set; }
    public double? PeakGpuPct { get; set; }
    public double? PeakGpuMemoryMb { get; set; }
    public double? MemoryGrowthMb { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagMonValidation Validation { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagMonSessionType SessionType { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DiagMonComparability Comparability { get; set; }
    public string Notes { get; set; } = "";
    public string Tags { get; set; } = "";
    [JsonIgnore] public string DateDisplay => StartUtc.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    [JsonIgnore] public string DurationDisplay => TimeSpan.FromSeconds(DurationSeconds).ToString(@"hh\:mm\:ss");
}

public sealed class DiagMonIndex
{
    public int SchemaVersion { get; set; } = 1;
    public List<DiagMonIndexEntry> Sessions { get; set; } = new();
}

public sealed class DiagMonCaptureOptions
{
    public DiagMonCaptureMode Mode { get; set; }
    public DiagMonTargetMode TargetMode { get; set; }
    public int? TargetPid { get; set; }
    public string UserLabel { get; set; } = "";
}

public sealed class DiagMonSettings
{
    public int StandardSampleSeconds { get; set; } = 2;
    public int DetailedSampleSeconds { get; set; } = 1;
    public int TraceMaximumMinutes { get; set; } = 10;
    public int RetentionDays { get; set; } = 30;
    public int RetentionSessionCount { get; set; } = 20;
    public int RetentionMaximumMb { get; set; } = 250;
    public string PresentMonPath { get; set; } = "";
}
