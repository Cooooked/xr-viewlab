using System;
using System.Windows;

namespace XRViewLab.UI;

internal enum OverlayPreviewAnchor { Centre, TopLeft, BottomRight, Edge }
internal enum OverlayPreviewStyle { Hud, Trace, Clock, Notification, Sticky, System }
internal enum OverlayPreviewEditKind { Position, Scale }

internal readonly record struct OverlayPreviewItem(
    string Id, string Label, double X, double Y, double ReferenceWidth, double ReferenceHeight, double Scale,
    double MinScale, double MaxScale, double Opacity, OverlayPreviewAnchor Anchor,
    OverlayPreviewStyle Style, int Theme = 0)
{
    public bool Editable => Anchor != OverlayPreviewAnchor.Edge && !string.IsNullOrWhiteSpace(Id);
}

internal static class OverlayPreviewReplicaLayout
{
    // Reference dimensions describe the native overlay footprint at scale 1. Scale is applied once,
    // and any preview-bound fit uses one factor for both axes so the aspect ratio cannot be distorted.
    internal static Size ResolveSize(OverlayPreviewItem item, Size available)
    {
        double width = Math.Max(0.5, item.ReferenceWidth * available.Width * item.Scale);
        double height = Math.Max(0.5, item.ReferenceHeight * available.Height * item.Scale);
        double fit = Math.Min(1.0, Math.Min(
            available.Width * 0.75 / Math.Max(0.5, width),
            available.Height * 0.45 / Math.Max(0.5, height)));
        return new Size(width * fit, height * fit);
    }
}

internal sealed class OverlayPreviewChangedEventArgs : EventArgs
{
    public OverlayPreviewChangedEventArgs(string id, OverlayPreviewEditKind kind, double x, double y, double scale)
    {
        Id = id;
        Kind = kind;
        X = x;
        Y = y;
        Scale = scale;
    }

    public string Id { get; }
    public OverlayPreviewEditKind Kind { get; }
    public double X { get; }
    public double Y { get; }
    public double Scale { get; }
}
