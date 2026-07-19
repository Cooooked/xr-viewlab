using System;
using System.Windows;

namespace XRViewLab.UI;

public enum OverlayPreviewAnchor { Centre, TopLeft, BottomRight, Edge, RenderEdge, RecordingRenderEdge }
public enum OverlayPreviewStyle { Hud, Trace, Clock, Notification, Sticky, System }
public enum OverlayPreviewEditKind { Position, Scale }

public readonly record struct OverlayPlacementOverride(double X, double Y, double Scale);

public readonly record struct OverlayPreviewItem(
    string Id, string Label, double X, double Y, double ReferenceWidth, double ReferenceHeight, double Scale,
    double MinScale, double MaxScale, double Opacity, OverlayPreviewAnchor Anchor,
    OverlayPreviewStyle Style, int Theme = 0)
{
    public bool Editable => Anchor is not OverlayPreviewAnchor.Edge and not OverlayPreviewAnchor.RenderEdge
        and not OverlayPreviewAnchor.RecordingRenderEdge && !string.IsNullOrWhiteSpace(Id);
}

internal static class OverlayPreviewReplicaLayout
{
    // Reference dimensions describe the native overlay footprint at scale 1. Scale is applied once,
    // and any preview-bound fit uses one factor for both axes so the aspect ratio cannot be distorted.
    internal static Size ResolveSize(OverlayPreviewItem item, Rect area)
    {
        // Native scale contracts mapped once into the same centred normalized box used for
        // placement. Content remains labelled, but crop never rescales or repositions it.
        double width, height;
        switch (item.Style)
        {
            case OverlayPreviewStyle.Hud:
                double unit = Quest3PreviewGeometry.TangentReferencePixelsUniform(area, 128.0) * item.Scale;
                double radius = unit * 0.48;
                double count = Math.Max(1.0, item.ReferenceWidth);
                double gap = unit * (0.25 + 3.0 * 0.018);
                width = radius * 2.0 * count + gap * (count - 1.0);
                height = radius * 2.0 + unit * 0.08 + unit * 0.045 * 7.0;
                break;
            case OverlayPreviewStyle.Trace:
                width = area.Width * Math.Clamp(item.ReferenceWidth, 0.10, 1.0) * item.Scale;
                height = Quest3PreviewGeometry.TangentReferencePixelsUniform(area, 128.0 * 0.55) * item.Scale;
                break;
            case OverlayPreviewStyle.Clock:
                double glyphX = Quest3PreviewGeometry.TangentReferencePixelsToX(area, 4.2) * item.Scale;
                double glyphY = Quest3PreviewGeometry.TangentReferencePixelsToY(area, 4.2) * item.Scale;
                width = 77.75 * glyphX;
                height = (item.ReferenceHeight > 0.5 ? 23.05 : 13.75) * glyphY;
                break;
            case OverlayPreviewStyle.Notification:
                width = Math.Min(Quest3PreviewGeometry.TangentReferencePixelsToX(area, 920.0) * item.Scale, area.Width * 0.60);
                height = Quest3PreviewGeometry.TangentReferencePixelsToY(area, 920.0) * item.Scale * (96.0 / 336.0);
                break;
            case OverlayPreviewStyle.Sticky:
                width = height = area.Height * 0.12 * item.Scale;
                break;
            default:
                width = item.ReferenceWidth * area.Width * item.Scale;
                height = item.ReferenceHeight * area.Height * item.Scale;
                break;
        }
        width = Math.Max(0.5, width);
        height = Math.Max(0.5, height);
        Rect shared = Quest3PreviewGeometry.SharedFullArea(area);
        double fit = Math.Min(1.0, Math.Min(
            shared.Width / Math.Max(0.5, width),
            shared.Height / Math.Max(0.5, height)));
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
