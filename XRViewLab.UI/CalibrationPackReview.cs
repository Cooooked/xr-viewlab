using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XRViewLab.UI;

// Read-only review of calibration screenshot packs captured by the suite into
// %LOCALAPPDATA%\XR ViewLab\CalibrationCaptures. This never modifies raw evidence: it only reads
// the PNG/JSON pairs, cross-checks them, hashes them and produces a concise report. It is
// deliberately free of WPF/System.Drawing so the core logic is deterministically testable.
//
// Scope honesty: every capture is the ViewLab-submitted LEFT-EYE texture at xrEndFrame. It proves
// what ViewLab handed the runtime on the PC. It does NOT capture the headset's lens optics, the
// runtime's own post-processing, foveated encoding, or the panel image. Radial/optical claims that
// depend on the physical lens cannot be made from this pack.
internal static class CalibrationPackReview
{
    // Each expected pattern maps to what it can and cannot diagnose from a PC-side left-eye capture.
    internal static readonly IReadOnlyDictionary<string, string> PatternPurpose = new Dictionary<string, string>
    {
        ["01-texture-grid"]        = "Geometry: scaling, alignment and non-uniform distortion of the submitted texture; texture-edge behaviour.",
        ["02-pixel-ruler"]         = "Exact pixel/region measurement within the captured image.",
        ["03-resolution-gratings"] = "Where 1/2/4-pixel detail stops resolving: filtering, resampling and encoder detail loss.",
        ["04-colour-bars"]         = "Channel reproduction, chroma damage, clipping and visible banding on the grey ramp.",
        ["05-frame-beacon"]        = "Frame identity / temporal progression: exposes frame synthesis or duplication.",
        ["06-edge-probes"]         = "Crop and edge behaviour: blur and foveation directly at the eye-texture edges.",
        ["07-checkerboards"]       = "Scaling blur, compression loss and moire from the checker ladder.",
        ["08-zone-plate"]          = "Radial resampling and aliasing / uneven sharpness across the field (PC-side only).",
        ["09-clipping-steps"]      = "Black/white clipping and tonal separation; crushed blacks and clipped whites.",
        ["10-motion-strip"]        = "Temporal behaviour and capture timing: smearing and reprojection artefacts.",
    };

    private static readonly Regex CaptureName =
        new(@"^(?<slug>\d\d-[a-z0-9-]+?)-(?<stamp>\d{8}-\d{6})\.(?<ext>png|json)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // A PNG whose file size per megapixel falls below this is very likely blank/near-uniform, because
    // uniform images compress to almost nothing. Chosen conservatively to avoid false positives on
    // genuinely low-detail patterns.
    private const double SuspiciousBytesPerMegapixel = 6_000.0;

    internal sealed class CaptureEntry
    {
        public string Slug = "";
        public string? PngPath;
        public string? JsonPath;
        public long PngBytes;
        public int PngWidth, PngHeight;         // parsed from the PNG IHDR
        public int MetaWidth, MetaHeight;       // read from the JSON sidecar
        public string? Sha256;
        public string? Application;
        public string? CapturedUtc;
        public string? Eye;
        public readonly List<string> Issues = new();
        public string Purpose => PatternPurpose.TryGetValue(Slug, out var p) ? p : "Unknown pattern (not in the calibration catalogue).";
        public bool Complete => PngPath != null && JsonPath != null && Issues.Count == 0;
    }

    internal sealed class PackReview
    {
        public string Folder = "";
        public string Stamp = "";
        public readonly List<CaptureEntry> Entries = new();
        public readonly List<string> MissingPatterns = new();
        public bool IsComplete => MissingPatterns.Count == 0 && Entries.All(e => e.Complete);
    }

    // A single suite run captures its ten patterns sequentially over a few seconds, so a run can
    // straddle a second/minute boundary (e.g. patterns 1-5 at ...:06 and 6-10 at ...:07). Captures
    // whose timestamps are within this gap of each other therefore belong to the same pack; a larger
    // gap starts a new pack.
    private static readonly TimeSpan SameRunGap = TimeSpan.FromSeconds(120);

    // Group a folder's captures into packs by capture-run proximity. A folder with several distinct
    // capture runs yields several packs; one run split across a second boundary stays a single pack.
    internal static IReadOnlyList<PackReview> ReviewFolder(string folder)
    {
        var packs = new List<PackReview>();
        if (!Directory.Exists(folder)) return packs;

        // Collect every capture as (timestamp, slug, png?, json?), coalescing the PNG/JSON pair per file stem.
        var files = new Dictionary<string, (DateTime time, string slug, string? png, string? json)>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(folder))
        {
            Match m = CaptureName.Match(Path.GetFileName(path));
            if (!m.Success) continue;
            string stamp = m.Groups["stamp"].Value, slug = m.Groups["slug"].Value.ToLowerInvariant();
            string stem = slug + "-" + stamp;
            bool isPng = string.Equals(m.Groups["ext"].Value, "png", StringComparison.OrdinalIgnoreCase);
            DateTime.TryParseExact(stamp, "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var time);
            files.TryGetValue(stem, out var cur);
            files[stem] = (time, slug, isPng ? path : cur.png, isPng ? cur.json : path);
        }

        // Cluster by time gap.
        var ordered = files.Values.OrderBy(f => f.time).ToList();
        var clusters = new List<List<(DateTime time, string slug, string? png, string? json)>>();
        foreach (var f in ordered)
        {
            if (clusters.Count == 0 || f.time - clusters[^1][^1].time > SameRunGap)
                clusters.Add(new());
            clusters[^1].Add(f);
        }

        foreach (var cluster in clusters)
        {
            var bySlug = new Dictionary<string, (string? png, string? json)>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in cluster) bySlug[f.slug] = (f.png, f.json);
            var pack = new PackReview { Folder = folder, Stamp = cluster[0].time.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) };
            foreach (string slug in CalibrationSuite.FileStems)
            {
                if (!bySlug.TryGetValue(slug, out var pair) || (pair.png == null && pair.json == null))
                {
                    pack.MissingPatterns.Add(slug);
                    continue;
                }
                pack.Entries.Add(BuildEntry(slug, pair.png, pair.json));
            }
            foreach (var extra in bySlug.Keys.Where(k => !CalibrationSuite.FileStems.Contains(k)))
            {
                var e = BuildEntry(extra, bySlug[extra].png, bySlug[extra].json);
                e.Issues.Add("Pattern is not part of the ten-tool calibration catalogue.");
                pack.Entries.Add(e);
            }
            packs.Add(pack);
        }
        return packs;
    }

    private static CaptureEntry BuildEntry(string slug, string? png, string? json)
    {
        var e = new CaptureEntry { Slug = slug, PngPath = png, JsonPath = json };
        if (png == null) e.Issues.Add("PNG image is missing.");
        if (json == null) e.Issues.Add("JSON metadata sidecar is missing.");

        if (png != null)
        {
            try
            {
                var bytes = File.ReadAllBytes(png);
                e.PngBytes = bytes.LongLength;
                if (TryReadPngSize(bytes, out int pw, out int ph)) { e.PngWidth = pw; e.PngHeight = ph; }
                else e.Issues.Add("File is not a valid PNG (bad signature/IHDR).");
                e.Sha256 = Convert.ToHexString(SHA256.HashData(bytes));

                long px = (long)Math.Max(1, e.PngWidth) * Math.Max(1, e.PngHeight);
                double bytesPerMp = e.PngBytes / (px / 1_000_000.0);
                if (e.PngWidth > 0 && bytesPerMp < SuspiciousBytesPerMegapixel)
                    e.Issues.Add($"Suspiciously small ({e.PngBytes:N0} bytes for {e.PngWidth}x{e.PngHeight}); the capture may be blank or near-uniform.");
            }
            catch (Exception ex) { e.Issues.Add("PNG could not be read: " + ex.Message); }
        }

        if (json != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(json));
                var r = doc.RootElement;
                if (r.TryGetProperty("width", out var w)) e.MetaWidth = w.GetInt32();
                if (r.TryGetProperty("height", out var h)) e.MetaHeight = h.GetInt32();
                if (r.TryGetProperty("application", out var a)) e.Application = a.GetString();
                if (r.TryGetProperty("capturedUtc", out var c)) e.CapturedUtc = c.GetString();
                if (r.TryGetProperty("eye", out var ey)) e.Eye = ey.GetString();
                if (r.TryGetProperty("image", out var im) && png != null)
                {
                    string named = im.GetString() ?? "";
                    if (!string.Equals(named, Path.GetFileName(png), StringComparison.OrdinalIgnoreCase))
                        e.Issues.Add($"Metadata 'image' ({named}) does not match the PNG file name.");
                }
                if (e.Eye != null && !string.Equals(e.Eye, "left", StringComparison.OrdinalIgnoreCase))
                    e.Issues.Add($"Metadata eye is '{e.Eye}'; the suite captures the left eye.");
            }
            catch (Exception ex) { e.Issues.Add("JSON metadata could not be parsed: " + ex.Message); }
        }

        if (png != null && json != null && e.PngWidth > 0 && e.MetaWidth > 0 &&
            (e.PngWidth != e.MetaWidth || e.PngHeight != e.MetaHeight))
            e.Issues.Add($"Dimension mismatch: PNG is {e.PngWidth}x{e.PngHeight} but metadata records {e.MetaWidth}x{e.MetaHeight}.");

        return e;
    }

    // Read width/height from a PNG IHDR without decoding the image. Signature is 8 bytes, then a
    // length+type ("IHDR") chunk whose data begins at offset 16 with big-endian width and height.
    internal static bool TryReadPngSize(byte[] bytes, out int width, out int height)
    {
        width = height = 0;
        if (bytes.Length < 24) return false;
        ReadOnlySpan<byte> sig = stackalloc byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (!bytes.AsSpan(0, 8).SequenceEqual(sig)) return false;
        if (bytes[12] != (byte)'I' || bytes[13] != (byte)'H' || bytes[14] != (byte)'D' || bytes[15] != (byte)'R') return false;
        width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        return width > 0 && height > 0;
    }

    internal static string RenderReport(PackReview pack)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Calibration pack review — {pack.Stamp}");
        sb.AppendLine($"Folder: {pack.Folder}");
        sb.AppendLine($"Status: {(pack.IsComplete ? "COMPLETE — all 10 patterns present and consistent" : "INCOMPLETE — see issues below")}");
        var app = pack.Entries.Select(e => e.Application).FirstOrDefault(a => !string.IsNullOrEmpty(a));
        if (app != null) sb.AppendLine($"Captured application: {app}");
        sb.AppendLine();
        sb.AppendLine("Scope: each image is the ViewLab-submitted LEFT-EYE texture at xrEndFrame. It shows what");
        sb.AppendLine("ViewLab handed the runtime on the PC — NOT the headset lens optics, runtime post-processing,");
        sb.AppendLine("foveated encoding or the physical panel image. Do not infer lens properties from this pack.");
        sb.AppendLine();
        if (pack.MissingPatterns.Count > 0)
            sb.AppendLine($"Missing patterns: {string.Join(", ", pack.MissingPatterns)}");
        sb.AppendLine();
        foreach (var e in pack.Entries)
        {
            sb.AppendLine($"• {e.Slug}  [{(e.Complete ? "OK" : "ISSUES")}]");
            sb.AppendLine($"    purpose : {e.Purpose}");
            if (e.PngWidth > 0) sb.AppendLine($"    image   : {e.PngWidth}x{e.PngHeight}, {e.PngBytes:N0} bytes");
            if (e.Sha256 != null) sb.AppendLine($"    sha256  : {e.Sha256}");
            foreach (var issue in e.Issues) sb.AppendLine($"    ! {issue}");
        }
        return sb.ToString();
    }

    // Compare two packs by pattern: presence, dimensions and content hash. Raw files are never touched.
    internal static string CompareReport(PackReview a, PackReview b)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Calibration pack comparison — {a.Stamp} vs {b.Stamp}");
        sb.AppendLine();
        foreach (string slug in CalibrationSuite.FileStems)
        {
            var ea = a.Entries.FirstOrDefault(e => e.Slug == slug);
            var eb = b.Entries.FirstOrDefault(e => e.Slug == slug);
            if (ea == null && eb == null) continue;
            if (ea == null) { sb.AppendLine($"• {slug}: only in {b.Stamp}"); continue; }
            if (eb == null) { sb.AppendLine($"• {slug}: only in {a.Stamp}"); continue; }
            if (ea.Sha256 == eb.Sha256) sb.AppendLine($"• {slug}: identical ({ea.PngWidth}x{ea.PngHeight})");
            else if (ea.PngWidth != eb.PngWidth || ea.PngHeight != eb.PngHeight)
                sb.AppendLine($"• {slug}: DIFFERENT — {ea.PngWidth}x{ea.PngHeight} vs {eb.PngWidth}x{eb.PngHeight}");
            else
                sb.AppendLine($"• {slug}: DIFFERENT content, same {ea.PngWidth}x{ea.PngHeight} (hash {Short(ea.Sha256)} vs {Short(eb.Sha256)})");
        }
        return sb.ToString();
    }

    private static string Short(string? hash) => hash is { Length: >= 8 } ? hash[..8] : (hash ?? "?");
}
