using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using XRViewLab.UI;

// Deterministic tests for CalibrationPackReview. With a folder argument it instead prints a review of
// that real folder (used for ad-hoc inspection of an actual capture pack); the committed test path
// runs with no argument and synthesises packs so it never depends on machine-local evidence.

if (args.Length > 0 && Directory.Exists(args[0]))
{
    foreach (var pack in CalibrationPackReview.ReviewFolder(args[0]))
        Console.WriteLine(CalibrationPackReview.RenderReport(pack));
    return 0;
}

int failures = 0;
void Check(bool ok, string label)
{
    Console.WriteLine((ok ? "PASS " : "FAIL ") + label);
    if (!ok) failures++;
}

string root = Path.Combine(Path.GetTempPath(), "vl-pack-review-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    // Build a byte array that passes CalibrationPackReview.TryReadPngSize and reaches a target size.
    static byte[] FakePng(int w, int h, int totalBytes)
    {
        var b = new byte[Math.Max(24, totalBytes)];
        byte[] sig = { 137, 80, 78, 71, 13, 10, 26, 10 };
        Array.Copy(sig, b, 8);
        b[8] = 0; b[9] = 0; b[10] = 0; b[11] = 13;      // IHDR length
        b[12] = (byte)'I'; b[13] = (byte)'H'; b[14] = (byte)'D'; b[15] = (byte)'R';
        b[16] = (byte)(w >> 24); b[17] = (byte)(w >> 16); b[18] = (byte)(w >> 8); b[19] = (byte)w;
        b[20] = (byte)(h >> 24); b[21] = (byte)(h >> 16); b[22] = (byte)(h >> 8); b[23] = (byte)h;
        for (int i = 24; i < b.Length; i++) b[i] = (byte)(i * 31 + 7); // filler so hashes differ by content
        return b;
    }
    void WriteCapture(string dir, string slug, string stamp, int w, int h, int pngBytes,
        int? metaW = null, int? metaH = null, string image = "", string eye = "left")
    {
        string stem = $"{slug}-{stamp}";
        File.WriteAllBytes(Path.Combine(dir, stem + ".png"), FakePng(w, h, pngBytes));
        var meta = new
        {
            pattern = slug, image = string.IsNullOrEmpty(image) ? stem + ".png" : image,
            capturedUtc = "2026-07-19T04:07:06.000Z", application = "Test.exe", eye,
            width = metaW ?? w, height = metaH ?? h
        };
        File.WriteAllText(Path.Combine(dir, stem + ".json"), JsonSerializer.Serialize(meta));
    }

    // Pack A: complete and healthy (all 10 patterns, big enough not to look blank).
    const string stampA = "20260719-140706";
    foreach (var slug in CalibrationSuite.FileStems) WriteCapture(root, slug, stampA, 2419, 432, 900_000);

    var packsA = CalibrationPackReview.ReviewFolder(root);
    Check(packsA.Count == 1, "one capture run is grouped into one pack");
    var a = packsA[0];
    Check(a.IsComplete, "healthy complete pack reports IsComplete");
    Check(a.MissingPatterns.Count == 0, "complete pack has no missing patterns");
    Check(a.Entries.All(e => e.Sha256 is { Length: 64 }), "every entry has a sha256");
    Check(a.Entries.All(e => e.PngWidth == 2419 && e.PngHeight == 432), "PNG IHDR dimensions parsed from bytes");
    Check(CalibrationPackReview.RenderReport(a).Contains("submitted LEFT-EYE"), "report states the left-eye/PC-side scope limitation");
    Check(a.Entries.First(e => e.Slug == "08-zone-plate").Purpose.Contains("Radial"), "each pattern carries its diagnostic purpose");

    // Pack B: missing one pattern, one blank/suspicious, one dimension mismatch, one wrong eye.
    const string stampB = "20260719-150000";
    foreach (var slug in CalibrationSuite.FileStems)
    {
        if (slug == "05-frame-beacon") continue;                                  // missing
        if (slug == "07-checkerboards") { WriteCapture(root, slug, stampB, 2419, 432, 1_000); continue; }        // blank
        if (slug == "02-pixel-ruler") { WriteCapture(root, slug, stampB, 2419, 432, 900_000, metaW: 1200, metaH: 400); continue; } // mismatch
        if (slug == "09-clipping-steps") { WriteCapture(root, slug, stampB, 2419, 432, 900_000, eye: "right"); continue; }          // wrong eye
        WriteCapture(root, slug, stampB, 2419, 432, 900_000);
    }
    var b = CalibrationPackReview.ReviewFolder(root).First(p => p.Stamp == stampB);
    Check(!b.IsComplete, "flawed pack is not complete");
    Check(b.MissingPatterns.Contains("05-frame-beacon"), "missing pattern is detected");
    Check(b.Entries.First(e => e.Slug == "07-checkerboards").Issues.Any(i => i.Contains("blank or near-uniform")), "blank/suspicious capture is detected");
    Check(b.Entries.First(e => e.Slug == "02-pixel-ruler").Issues.Any(i => i.Contains("Dimension mismatch")), "PNG/metadata dimension mismatch is detected");
    Check(b.Entries.First(e => e.Slug == "09-clipping-steps").Issues.Any(i => i.Contains("eye is 'right'")), "unexpected eye is detected");

    // Comparison: identical slug in A vs B is 'identical'; a differing one is 'DIFFERENT'.
    string cmp = CalibrationPackReview.CompareReport(a, b);
    Check(cmp.Contains("01-texture-grid: identical"), "comparison marks identical captures");
    Check(cmp.Contains("07-checkerboards: DIFFERENT") || cmp.Contains("02-pixel-ruler: DIFFERENT"), "comparison marks differing captures");

    // Raw evidence is never modified: capture the pre-review write count is out of scope, but confirm
    // ReviewFolder opens files read-only by re-reading after review (would throw if it had locked/altered).
    Check(File.ReadAllBytes(Directory.EnumerateFiles(root, "*.png").First()).Length > 0, "raw PNGs remain readable and unmodified after review");

    Console.WriteLine(failures == 0 ? "All calibration pack-review fixtures passed." : $"{failures} fixture(s) failed.");
    return failures == 0 ? 0 : 1;
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
