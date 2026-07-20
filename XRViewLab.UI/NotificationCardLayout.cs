using System;

namespace XRViewLab.UI;

// WPF-free notification card slot + raster-density contract. Must match dllmain.cpp
// NotifyCardBlock: the shared-memory slot is a fixed SlotW x SlotH RGBA area and the native
// layer reads every row at the fixed slot stride (SlotW * 4), using width/height only as the
// "used pixels" sub-rectangle.
//
// Raster density (item: overlay render quality). A notification card is the only ViewLab
// overlay that is rasterised to a bitmap and then sampled onto a quad — the clock, HUD, trace,
// crosshair and sticky notes draw native vector geometry at eye-buffer resolution, so they stay
// sharp at any scale. To stop the card looking low-resolution when enlarged, the card is
// SUPERSAMPLED: its LOGICAL footprint (physical size / layout) is separate from its RASTER
// dimensions (source pixels). The composed bitmap is rendered at logical x RasterFactor pixels,
// where RasterFactor scales with the overlay's displayed size and a quality multiplier, capped so
// memory stays bounded. Increasing physical scale therefore allocates more source pixels instead
// of stretching the same small bitmap; changing quality never changes the stored physical size.
//
// Quest 3 raster references (per eye, treated as raster-resolution not physical PPD):
//   native 2064x2208; VD Godlike 3072x3216 (~147% linear); high-quality target 4128x4416 (200%).
// RasterQuality defaults to 2.0 to target ~200% native linear raster density.
internal static class NotificationCardLayout
{
    // Logical footprint space (physical layout units). The widest logical card is 336x96.
    public const int LogicalMaxW = 336;
    public const int LogicalMaxH = 96;

    // Raster-density policy.
    public const double RasterQuality = 2.0;   // ~200% native linear density target
    public const double SupersampleCap = 3.0;  // hard cap so the slot stays bounded (~7 MB / 6 cards)

    // Fixed shared-memory slot = logical max scaled by the supersample cap.
    public const int SlotW = (int)(LogicalMaxW * SupersampleCap); // 1008
    public const int SlotH = (int)(LogicalMaxH * SupersampleCap); // 288
    public const int SlotBytes = SlotW * SlotH * 4;

    // Theme footprints in LOGICAL units (design 0 classic, 1 compact banner, 2 minimal, 3 bold).
    // These never change with render quality; height differences drive native stacking density.
    public static (int W, int H) DesignFootprint(int design) =>
        Math.Clamp(design, 0, 3) switch { 1 => (LogicalMaxW, 44), 2 => (288, 72), 3 => (LogicalMaxW, LogicalMaxH), _ => (LogicalMaxW, 92) };

    // Supersample multiplier for a card displayed at the given physical scale. Grows with the
    // displayed size (so a larger card gets proportionally more source pixels) and the quality
    // multiplier, clamped to [1, SupersampleCap]. Independent of which theme/palette is used.
    public static double RasterFactor(double displayScale)
    {
        double f = displayScale * RasterQuality;
        if (double.IsNaN(f) || f < 1.0) return 1.0;
        return f > SupersampleCap ? SupersampleCap : f;
    }

    // Raster (source-pixel) dimensions for a logical footprint at a physical scale, bounded to the
    // slot. This is the size the card bitmap is actually rasterised at.
    public static (int W, int H) RasterDimensions(int logicalW, int logicalH, double displayScale)
    {
        double f = RasterFactor(displayScale);
        int w = Math.Min(SlotW, Math.Max(1, (int)Math.Round(logicalW * f)));
        int h = Math.Min(SlotH, Math.Max(1, (int)Math.Round(logicalH * f)));
        return (w, h);
    }

    // Expand a tightly-packed w*h straight-alpha RGBA bitmap into a full slot-sized buffer laid out
    // at the fixed slot stride, top-left anchored, remaining (padding) pixels fully zeroed — so the
    // transparent padding carries NO colour that bilinear filtering could bleed (item: magenta edge).
    public static byte[] PadToSlot(byte[] rgba, int w, int h)
    {
        if (rgba == null) throw new ArgumentNullException(nameof(rgba));
        if (w <= 0 || h <= 0 || w > SlotW || h > SlotH)
            throw new ArgumentOutOfRangeException(nameof(w), $"Card {w}x{h} does not fit the {SlotW}x{SlotH} shared-memory slot.");
        if (rgba.Length != w * h * 4)
            throw new ArgumentException($"Expected {w * h * 4} bytes for {w}x{h}, got {rgba.Length}.", nameof(rgba));
        if (w == SlotW && h == SlotH) return rgba;
        var slot = new byte[SlotBytes];
        for (int y = 0; y < h; y++)
            Buffer.BlockCopy(rgba, y * w * 4, slot, y * SlotW * 4, w * 4);
        return slot;
    }
}
