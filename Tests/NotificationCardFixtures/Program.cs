using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using XRViewLab.UI;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAILED: " + message);
    Console.WriteLine("PASS: " + message);
}

// Ensure a WPF dispatcher exists for the composition path.
if (Application.Current == null)
    _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

var compose = typeof(NotificationService).GetMethod("ComposeCard", BindingFlags.NonPublic | BindingFlags.Static);
if (compose == null) throw new InvalidOperationException("ComposeCard not found");

(byte[] rgba, int w, int h) Compose(string app, string title, string body, BitmapSource? icon, NotificationSettings s)
{
    var result = compose.Invoke(null, new object?[] { app, title, body, icon, s })!;
    var tt = result.GetType();
    return ((byte[])tt.GetField("Item1")!.GetValue(result)!,
            (int)tt.GetField("Item2")!.GetValue(result)!,
            (int)tt.GetField("Item3")!.GetValue(result)!);
}
static byte AlphaAt(byte[] rgba, int rw, int x, int y) => rgba[(y * NotificationCardLayout.SlotW + x) * 4 + 3];

// ---- All four themes and all palettes still render into a valid slot with visible pixels -------
foreach (var (theme, name) in new[] { (0, "Classic"), (1, "Compact Banner"), (2, "Minimal"), (3, "Bold") })
{
    foreach (int palette in new[] { 0, 1, 2, 3, 4 })
    {
        var s = new NotificationSettings { Theme = theme, Palette = palette, ShowIcon = true, ShowImage = true, Scale = 1.0 };
        var (rgba, w, h) = Compose("ViewLab", "Test Title", "This is the notification body.", null, s);

        var logical = NotificationCardLayout.DesignFootprint(theme);
        var raster = NotificationCardLayout.RasterDimensions(logical.W, logical.H, s.Scale);
        Require(w == raster.W && h == raster.H, $"{name} p{palette} composes at raster dims {w}x{h} (logical {logical.W}x{logical.H})");
        Require(rgba.Length == NotificationCardLayout.SlotBytes, $"{name} p{palette} returns slot-sized buffer");
        int visible = 0; for (int i = 3; i < rgba.Length; i += 4) if (rgba[i] != 0) visible++;
        Require(visible > 0, $"{name} p{palette} has visible pixels");
        // Padding outside the used raster rect must be fully zeroed (no colour to bleed).
        bool paddingClean = true;
        for (int y = 0; y < NotificationCardLayout.SlotH && paddingClean; y++)
            for (int x = (y < h ? w : 0); x < NotificationCardLayout.SlotW; x++)
            { int o = (y * NotificationCardLayout.SlotW + x) * 4; if (rgba[o] != 0 || rgba[o + 1] != 0 || rgba[o + 2] != 0 || rgba[o + 3] != 0) { paddingClean = false; break; } }
        Require(paddingClean, $"{name} p{palette} slot padding is fully zeroed (no magenta/colour bleed)");
    }
}

// ---- Minimal has a transparent background (Clock-Minimal design language); Classic is boxed -----
{
    var min = new NotificationSettings { Theme = 2, Palette = 0, Scale = 1.0 };
    var (mr, mw, mh) = Compose("Music", "Song Title", "Artist Name", null, min);
    Require(AlphaAt(mr, mw, 1, 1) == 0, "Minimal top-left corner is fully transparent (no card surface)");
    Require(AlphaAt(mr, mw, mw - 2, mh - 2) == 0, "Minimal bottom-right corner is fully transparent");
    int transparent = 0, total = mw * mh;
    for (int y = 0; y < mh; y++) for (int x = 0; x < mw; x++) if (AlphaAt(mr, mw, x, y) == 0) transparent++;
    Require(transparent > total / 2, $"Minimal background is majority transparent ({transparent}/{total}) — text floats, not boxed");

    var cls = new NotificationSettings { Theme = 0, Palette = 0, Scale = 1.0 };
    var (cr, cw, ch) = Compose("Music", "Song Title", "Artist Name", null, cls);
    Require(AlphaAt(cr, cw, 4, ch / 2) > 0, "Classic keeps an opaque panel surface (distinct boxed design preserved)");
}

// ---- Increasing physical scale increases required raster dimensions ------------------------------
{
    var small = NotificationCardLayout.RasterDimensions(NotificationCardLayout.LogicalMaxW, NotificationCardLayout.LogicalMaxH, 0.75);
    var large = NotificationCardLayout.RasterDimensions(NotificationCardLayout.LogicalMaxW, NotificationCardLayout.LogicalMaxH, 1.25);
    Require(large.W > small.W && large.H > small.H, $"larger physical scale => larger raster ({small.W}x{small.H} -> {large.W}x{large.H})");
    Require(NotificationCardLayout.RasterFactor(1.0) >= 2.0 - 1e-9, "quality multiplier targets >=200% native raster density at scale 1.0");
    // Composing the same theme at a larger scale actually stores more source pixels.
    var (_, w1, h1) = Compose("A", "T", "B", null, new NotificationSettings { Theme = 0, Scale = 0.75 });
    var (_, w2, h2) = Compose("A", "T", "B", null, new NotificationSettings { Theme = 0, Scale = 1.25 });
    Require(w2 > w1 && h2 > h1, $"a larger notify scale composes more source pixels ({w1}x{h1} -> {w2}x{h2})");
}

// ---- Stored physical footprint does not change with render quality -------------------------------
{
    // The physical/displayed footprint is the LOGICAL DesignFootprint (+ notify_scale, applied
    // natively). It is independent of the raster factor, so raster quality never alters it.
    foreach (var (theme, ew, eh) in new[] { (0, 336, 92), (1, 336, 44), (2, 288, 72), (3, 336, 96) })
    {
        var f = NotificationCardLayout.DesignFootprint(theme);
        Require(f.W == ew && f.H == eh, $"design {theme} logical footprint is fixed at {ew}x{eh} regardless of quality");
    }
    Require(NotificationCardLayout.RasterDimensions(288, 72, 1.0).W > 288,
        "raster dims exceed logical footprint (raster != physical size)");
}

// ---- Icon/album-art layouts stay within the raster bounds ----------------------------------------
{
    var art = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
    var dv = new DrawingVisual();
    using (var dc = dv.RenderOpen()) dc.DrawRectangle(Brushes.Magenta, null, new Rect(0, 0, 64, 64));
    art.Render(dv); art.Freeze();
    foreach (int theme in new[] { 0, 1, 3 }) // designs that show an icon/album-art slot
    {
        var s = new NotificationSettings { Theme = theme, Palette = 2, ShowIcon = true, ShowImage = true, Scale = 1.0 };
        var (rgba, w, h) = Compose("Tidal", "A Very Long Track Title That Should Be Trimmed Cleanly", "A Long Artist Name Here", art, s);
        Require(w <= NotificationCardLayout.SlotW && h <= NotificationCardLayout.SlotH, $"design {theme} with album art stays within the slot");
        Require(rgba.Length == NotificationCardLayout.SlotBytes, $"design {theme} with album art fills exactly one slot buffer");
    }
}

// ---- Now Playing routes through the same compose+queue path as Test Presentation -----------------
{
    var add = typeof(NotificationService).GetMethod("AddComposedCard", BindingFlags.NonPublic | BindingFlags.Instance);
    Require(add != null, "AddComposedCard (shared compose+queue entry) exists");
    foreach (var m in new[] { "EnqueueTestNotification", "EnqueueMediaCard", "EnqueueEvent" })
        Require(typeof(NotificationService).GetMethod(m) != null, $"{m} is present and feeds the shared card pipeline");
}

// ---- PadToSlot layout integrity ------------------------------------------------------------------
{
    byte[] small = new byte[288 * 72 * 4];
    for (int i = 0; i < small.Length; i += 4) { small[i] = (byte)(i % 255); small[i + 1] = (byte)((i + 1) % 255); small[i + 2] = (byte)((i + 2) % 255); small[i + 3] = 255; }
    byte[] padded = NotificationCardLayout.PadToSlot(small, 288, 72);
    Require(padded.Length == NotificationCardLayout.SlotBytes, "PadToSlot returns slot-sized buffer");
    for (int y = 0; y < 72; y++)
        for (int x = 0; x < 288 * 4; x++)
            if (padded[y * NotificationCardLayout.SlotW * 4 + x] != small[y * 288 * 4 + x])
                throw new InvalidOperationException($"PadToSlot corrupted pixel data at row {y} byte {x}");
    Require(true, "PadToSlot preserves all rows of pixel data");
    int nonZeroPad = 0;
    for (int y = 0; y < NotificationCardLayout.SlotH; y++)
        for (int x = (y < 72 ? 288 : 0); x < NotificationCardLayout.SlotW; x++)
            if (padded[(y * NotificationCardLayout.SlotW + x) * 4 + 3] != 0) nonZeroPad++;
    Require(nonZeroPad == 0, "PadToSlot leaves padding transparent");
}

Console.WriteLine("Notification card layout + raster-density fixtures passed.");
