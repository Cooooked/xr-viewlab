using System;

namespace XRViewLab.UI;

// WPF-free notification card slot contract. Must match dllmain.cpp NotifyCardBlock:
// the shared-memory slot is a fixed kNotifyCardW x kNotifyCardH RGBA area and the native
// layer reads every row at the fixed slot stride (kNotifyCardW * 4), using width/height
// only as the "used pixels" sub-rectangle. Any composed card narrower or shorter than the
// slot MUST therefore be padded row-by-row to the slot stride before publication —
// writing a tightly-packed smaller bitmap (or skipping the write because the byte count
// differs) leaves the slot transparent and the card invisible in the headset.
internal static class NotificationCardLayout
{
    public const int SlotW = 336;
    public const int SlotH = 96;
    public const int SlotBytes = SlotW * SlotH * 4;

    // Theme footprints (design 0 classic, 1 compact banner, 2 minimal, 3 bold). Every
    // footprint must fit the slot; height differences drive native stacking density.
    public static (int W, int H) DesignFootprint(int design) =>
        Math.Clamp(design, 0, 3) switch { 1 => (SlotW, 44), 2 => (288, 72), 3 => (SlotW, SlotH), _ => (SlotW, 92) };

    // Expand a tightly-packed w*h straight-alpha RGBA bitmap into a full slot-sized buffer
    // laid out at the fixed slot stride, top-left anchored, remaining pixels transparent.
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
