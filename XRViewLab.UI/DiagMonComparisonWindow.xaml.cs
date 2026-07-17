using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace XRViewLab.UI;

public partial class DiagMonComparisonWindow : Window
{
    private sealed record Row(string Date, string Application, string AverageFps, string P99Ms, int SevereStutters,
        string PeakGpu, string PeakVram, string MemoryGrowth, string FpsDelta, string P99Delta);

    public DiagMonComparisonWindow(IReadOnlyList<DiagMonIndexEntry> sessions)
    {
        InitializeComponent();
        DiagMonIndexEntry first = sessions[0];
        ComparisonGrid.ItemsSource = sessions.Select(s => new Row(
            s.DateDisplay, string.IsNullOrWhiteSpace(s.UserLabel) ? s.Application : $"{s.Application} — {s.UserLabel}",
            Format(s.AverageFps, "0.0", "FPS"), Format(s.P99Ms, "0.00", "ms"), s.SevereStutters,
            Format(s.PeakGpuPct, "0.0", "%"), Format(s.PeakGpuMemoryMb, "0", "MB"), Format(s.MemoryGrowthMb, "0.0", "MB"),
            Delta(s.AverageFps, first.AverageFps, higherIsBetter: true), Delta(s.P99Ms, first.P99Ms, higherIsBetter: false))).ToList();
    }

    private static string Format(double? value, string format, string unit) =>
        value is double number ? $"{number.ToString(format, CultureInfo.CurrentCulture)} {unit}" : "Unavailable";

    private static string Delta(double? value, double? reference, bool higherIsBetter)
    {
        if (value is not double current || reference is not double baseline || baseline == 0) return "Unavailable";
        double percent = (current - baseline) / baseline * 100;
        string direction = percent == 0 ? "same" : (percent > 0) == higherIsBetter ? "better" : "worse";
        return $"{percent:+0.0;-0.0;0.0}% {direction}";
    }
}
