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

foreach (var (theme, name) in new[] { (0, "Classic"), (1, "Compact Banner"), (2, "Minimal"), (3, "Bold") })
{
    foreach (int palette in new[] { 0, 1, 2, 3, 4 })
    {
        var settings = new NotificationSettings { Theme = theme, Palette = palette, ShowIcon = true, ShowImage = true };
        var result = compose.Invoke(null, new object[] { "ViewLab", "Test Title", "This is the notification body.", null, settings });
        var tupleType = result.GetType();
        byte[] rgba = (byte[])tupleType.GetField("Item1")!.GetValue(result)!;
        int w = (int)tupleType.GetField("Item2")!.GetValue(result)!;
        int h = (int)tupleType.GetField("Item3")!.GetValue(result)!;

        var expected = NotificationCardLayout.DesignFootprint(theme);
        Require(w == expected.W && h == expected.H, $"{name} palette {palette} footprint matches DesignFootprint ({w}x{h})");
        Require(rgba.Length == NotificationCardLayout.SlotBytes, $"{name} palette {palette} returns slot-sized buffer ({rgba.Length} bytes)");

        int nonTransparent = rgba.Where((b, i) => i % 4 == 3 && b != 0).Count();
        Require(nonTransparent > 0, $"{name} palette {palette} has visible pixels");

        int totalAlpha = 0;
        for (int i = 3; i < rgba.Length; i += 4) totalAlpha += rgba[i];
        Require(totalAlpha > 0, $"{name} palette {palette} has non-zero alpha sum");
    }
}

// Verify PadToSlot layout: a 288x72 bitmap pads to 336x96 with the original pixels at the top-left.
{
    byte[] small = new byte[288 * 72 * 4];
    for (int i = 0; i < small.Length; i += 4)
    {
        small[i] = (byte)(i % 255);
        small[i + 1] = (byte)((i + 1) % 255);
        small[i + 2] = (byte)((i + 2) % 255);
        small[i + 3] = 255;
    }
    byte[] padded = NotificationCardLayout.PadToSlot(small, 288, 72);
    Require(padded.Length == NotificationCardLayout.SlotBytes, "PadToSlot returns slot-sized buffer");
    for (int y = 0; y < 72; y++)
    {
        int srcOff = y * 288 * 4;
        int dstOff = y * NotificationCardLayout.SlotW * 4;
        for (int x = 0; x < 288 * 4; x++)
            if (padded[dstOff + x] != small[srcOff + x])
                throw new InvalidOperationException($"PadToSlot corrupted pixel data at row {y} byte {x}");
    }
    Require(true, "PadToSlot preserves all 72 rows of pixel data");
    int paddingTransparent = 0;
    for (int y = 0; y < NotificationCardLayout.SlotH; y++)
        for (int x = (y < 72 ? 288 : 0); x < NotificationCardLayout.SlotW; x++)
            if (padded[(y * NotificationCardLayout.SlotW + x) * 4 + 3] != 0)
                paddingTransparent++;
    Require(paddingTransparent == 0, "PadToSlot leaves padding transparent");
}

Console.WriteLine("Notification card layout fixtures passed.");
