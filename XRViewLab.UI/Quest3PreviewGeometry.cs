using System;
using System.Collections.Generic;
using System.Windows;

namespace XRViewLab.UI;

// The preview is the centred, normalized binocular view the user sees in-headset. It is
// deliberately not a projection/FOV plot. Crop percentages map once and directly to this box:
// Horizontal 0.8 is 80% of its width; Top+Bottom 0.15 is 15% of its height. The fixed 55:48
// outer aspect is never stretched by the host control, and the circular eye guides are built
// from one pixel diameter so they remain true circles inside that slightly-wide box.
internal static class Quest3PreviewGeometry
{
    internal const double DefaultIpdMillimetres = 67.0;
    internal const double MinimumIpdMillimetres = 50.0;
    internal const double MaximumIpdMillimetres = 80.0;
    internal const int EyeRenderWidth = 2064;
    internal const int EyeRenderHeight = 2208;
    internal const double CanvasAspect = 55.0 / 48.0;
    internal const double UsefulHorizontal = 0.85;
    internal const double UsefulVertical = 0.90;
    internal const double OpticalPreviewCentreY = 0.403406273247302;
    // Calibrated angular spans affect replica size only. They must never move the shared
    // geometric centre used by every preview layer.
    internal const double FullHorizontalTangentSpan = 1.67819926235456;
    internal const double FullVerticalTangentSpan = 2.39383678154919;

    internal readonly record struct EyeGuides(Rect Left, Rect Right);
    internal readonly record struct EyeFrames(Rect Left, Rect Right);

    internal static Size DesiredSize(double availableWidth)
    {
        double width = double.IsInfinity(availableWidth) ? 280.0 : availableWidth;
        return new Size(width, width / CanvasAspect);
    }

    internal static Rect FitArea(Size available, double inset = 2.0)
    {
        double width = Math.Max(0.0, available.Width - inset * 2.0);
        double height = Math.Max(0.0, available.Height - inset * 2.0);
        if (width <= 0.0 || height <= 0.0) return Rect.Empty;
        double fittedWidth = Math.Min(width, height * CanvasAspect);
        double fittedHeight = fittedWidth / CanvasAspect;
        return new Rect(inset + (width - fittedWidth) * 0.5,
            inset + (height - fittedHeight) * 0.5, fittedWidth, fittedHeight);
    }

    // One rigid vertical translation for the COMPLETE preview scene. The fitted area (and with
    // it every element drawn in area coordinates: frames, crop, visor, guides, crosshair,
    // widgets and edge cues) is moved so the area centre sits at the requested
    // fraction of the viewport height. Translate only: size and internal layout never change.
    internal static Rect FitAreaAtCentre(Size available, double viewportCentreY, double inset = 2.0)
    {
        Rect area = FitArea(available, inset);
        if (area.IsEmpty) return area;
        double targetCentre = available.Height * Math.Clamp(viewportCentreY, 0.0, 1.0);
        double currentCentre = area.Top + area.Height * 0.5;
        return new Rect(area.Left, area.Top + targetCentre - currentCentre, area.Width, area.Height);
    }

    // Preview-only optical-centre translation as a delta, so the viewport/FitArea stays fixed and
    // only the headset-content group is moved. Reuses FitAreaAtCentre; adds no new formula.
    internal static double OpticalContentOffsetY(Size available, double inset = 2.0)
    {
        Rect geometric = FitArea(available, inset);
        if (geometric.IsEmpty) return 0.0;
        return FitAreaAtCentre(available, OpticalPreviewCentreY, inset).Top - geometric.Top;
    }

    // Half-lens split around the shared geometric centre: Top scales within the top half and
    // Bottom within the bottom half. Checkpoints: 1/1 full height, 1/0 top half, 0/1 bottom
    // half, 0.5/0.5 the centred middle half. The optical-centre preview mode translates the
    // whole area instead of moving this anchor.
    internal static Rect RuntimeCropRect(Rect area, double horizontal, double topScale, double bottomScale)
    {
        horizontal = Math.Clamp(horizontal, 0.01, 1.0);
        topScale = Math.Clamp(topScale, 0.0, 1.0);
        bottomScale = Math.Clamp(bottomScale, 0.0, 1.0);
        double top = 0.5 * (1.0 - topScale);
        double bottom = 0.5 + 0.5 * bottomScale;
        if (bottom - top < 0.01)
        {
            double centre = (top + bottom) * 0.5;
            top = Math.Clamp(centre - 0.005, 0.0, 0.99);
            bottom = top + 0.01;
        }
        double width = area.Width * horizontal;
        return new Rect(area.Left + (area.Width - width) * 0.5,
            area.Top + top * area.Height, width, (bottom - top) * area.Height);
    }

    internal static double RuntimeCropCentre(double topScale, double bottomScale)
    {
        double top = 0.5 * (1.0 - Math.Clamp(topScale, 0.0, 1.0));
        double bottom = 0.5 + 0.5 * Math.Clamp(bottomScale, 0.0, 1.0);
        return (top + bottom) * 0.5;
    }

    internal static Rect CropRect(Rect area, double horizontal, double vertical, double verticalCentre)
    {
        horizontal = Math.Clamp(horizontal, 0.01, 1.0);
        vertical = Math.Clamp(vertical, 0.01, 1.0);
        double width = area.Width * horizontal;
        double height = vertical >= 1.0 - 0.000001 ? area.Height : area.Height * vertical;
        double centre = vertical >= 1.0 - 0.000001
            ? area.Top + area.Height * 0.5
            : area.Top + area.Height * Math.Clamp(verticalCentre, vertical * 0.5, 1.0 - vertical * 0.5);
        double topY = vertical >= 1.0 - 0.000001 ? area.Top : centre - height * 0.5;
        return new Rect(area.Left + (area.Width - width) * 0.5, topY, width, height);
    }

    internal static EyeGuides FullEyeGuides(Rect area, double ipdMillimetres = DefaultIpdMillimetres)
    {
        double diameter = area.Height * UsefulVertical;
        double unionWidth = area.Width * UsefulHorizontal;
        double centreSeparation = AdjustedCentreSeparation(Math.Max(0.0, unionWidth - diameter), ipdMillimetres);
        double centreX = area.Left + area.Width * 0.5;
        double top = area.Top + area.Height * 0.5 - diameter * 0.5;
        return new EyeGuides(
            new Rect(centreX - centreSeparation * 0.5 - diameter * 0.5, top, diameter, diameter),
            new Rect(centreX + centreSeparation * 0.5 - diameter * 0.5, top, diameter, diameter));
    }

    internal static EyeFrames FullEyeFrames(Rect area, double ipdMillimetres = DefaultIpdMillimetres)
    {
        double eyeAspect = (double)EyeRenderWidth / EyeRenderHeight;
        double eyeWidth = Math.Min(area.Width, area.Height * eyeAspect);
        double centreSeparation = AdjustedCentreSeparation(Math.Max(0.0, area.Width - eyeWidth), ipdMillimetres);
        double centreX = area.Left + area.Width * 0.5;
        return new EyeFrames(
            new Rect(centreX - centreSeparation * 0.5 - eyeWidth * 0.5, area.Top, eyeWidth, area.Height),
            new Rect(centreX + centreSeparation * 0.5 - eyeWidth * 0.5, area.Top, eyeWidth, area.Height));
    }

    internal static Rect FullBinocularOval(Rect area) => new(
        area.Left + area.Width * (1.0 - UsefulHorizontal) * 0.5,
        area.Top + area.Height * 0.5 - area.Height * UsefulVertical * 0.5,
        area.Width * UsefulHorizontal,
        area.Height * UsefulVertical);

    private static double AdjustedCentreSeparation(double calibratedSeparation, double ipdMillimetres)
    {
        double ipd = Math.Clamp(ipdMillimetres, MinimumIpdMillimetres, MaximumIpdMillimetres);
        return calibratedSeparation * ipd / DefaultIpdMillimetres;
    }

    internal static Rect SharedFullArea(Rect area) => area;

    internal static Point ResolveFullLens(Rect area, double x, double y) => new(
        area.Left + Math.Clamp(x, 0.0, 1.0) * area.Width,
        area.Top + Math.Clamp(y, 0.0, 1.0) * area.Height);

    internal static Point InvertFullLens(Rect area, Point point) => new(
        (point.X - area.Left) / Math.Max(1.0, area.Width),
        (point.Y - area.Top) / Math.Max(1.0, area.Height));

    internal static Point ApplyFullLensDrag(Rect area, Point start, Vector pixelDelta) => new(
        Math.Clamp(start.X + pixelDelta.X / Math.Max(1.0, area.Width), 0.0, 1.0),
        Math.Clamp(start.Y + pixelDelta.Y / Math.Max(1.0, area.Height), 0.0, 1.0));

    internal static Point ResolveCentredOffset(Rect area, double x, double y) => new(
        area.Left + area.Width * (0.5 + Math.Clamp(x, -1.0, 1.0) * 0.5),
        area.Top + area.Height * (0.5 + Math.Clamp(y, -1.0, 1.0) * 0.5));

    internal static double NormalizedXToPixels(Rect area, double value) => value * area.Width;
    internal static double NormalizedYToPixels(Rect area, double value) => value * area.Height;
    internal static double ReferencePixelsToX(Rect area, double pixels) => pixels / 1080.0 * area.Width;
    internal static double ReferencePixelsToY(Rect area, double pixels) => pixels / 1080.0 * area.Height;
    internal static double TangentReferencePixelsToX(Rect area, double pixels) =>
        pixels / 1080.0 * area.Width / FullHorizontalTangentSpan;
    internal static double TangentReferencePixelsToY(Rect area, double pixels) =>
        pixels / 1080.0 * area.Height / FullVerticalTangentSpan;
    internal static double TangentReferencePixelsUniform(Rect area, double pixels) =>
        pixels / 1080.0 * Math.Min(area.Width / FullHorizontalTangentSpan, area.Height / FullVerticalTangentSpan);
}
