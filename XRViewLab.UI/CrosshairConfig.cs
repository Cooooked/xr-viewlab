using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace XRViewLab.UI;

// Static, game-agnostic Counter-Strike crosshair model plus importers/exporters.
//
// Two import paths populate the SAME settings:
//   * ParseLegacyConfig  — `cl_crosshair*` console commands (the fully specified path).
//   * TryParseShareCode  — CS2 / CS:GO `CSGO-xxxxx-...` share codes, decoded via the community
//     base-58 layout (the same one the popular `csgo-sharecode` tooling uses).
//
// Only static visual settings are represented. Dynamic spread, recoil, per-weapon gaps, movement
// and split-distance are parsed if present but deliberately discarded so the crosshair stays fixed.
internal sealed class CrosshairSettings
{
    public double Size = 5.0;            // cl_crosshairsize (CS reference pixels)
    public double Gap = -2.0;            // cl_crosshairgap (may be negative)
    public double Thickness = 1.0;       // cl_crosshairthickness
    public bool Dot;                     // cl_crosshairdot
    public bool Outline = true;          // cl_crosshair_drawoutline; visible built-in test default
    public double OutlineThickness = 1.0;// cl_crosshair_outlinethickness
    public double Alpha = 1.0;           // cl_crosshairalpha, normalized 0..1
    public byte R = 0, G = 255, B = 0;   // resolved RGB (preset or custom)
    public bool TStyle;                  // cl_crosshair_t
    public double VrScale = 1.0;         // ViewLab-only overall VR scale (not a CS setting)

    public uint ColorRgb => (uint)((R << 16) | (G << 8) | B);

    private static readonly (byte r, byte g, byte b)[] Presets =
    {
        (255, 0, 0),     // 0 red
        (0, 255, 0),     // 1 green
        (255, 255, 0),   // 2 yellow
        (0, 0, 255),     // 3 blue
        (0, 255, 255),   // 4 cyan
    };

    // ---- Legacy console-config import ----------------------------------------------------------
    // Accepts newline- or semicolon-separated `cl_*` assignments, tolerant of quotes and extra
    // whitespace. Returns the number of recognized commands applied.
    public int ParseLegacyConfig(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        int applied = 0;
        int colorPreset = -1;
        byte cr = R, cg = G, cb = B;
        bool useAlpha = true;
        foreach (var rawLine in text.Replace(";", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            var key = parts[0].Trim().ToLowerInvariant();
            var val = parts[1].Trim().Trim('"');
            if (!double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) continue;
            switch (key)
            {
                case "cl_crosshairsize": Size = d; applied++; break;
                case "cl_crosshairgap": Gap = d; applied++; break;
                case "cl_crosshairthickness": Thickness = d; applied++; break;
                case "cl_crosshairdot": Dot = d != 0; applied++; break;
                case "cl_crosshair_drawoutline": Outline = d != 0; applied++; break;
                case "cl_crosshair_outlinethickness": OutlineThickness = d; applied++; break;
                case "cl_crosshairalpha": Alpha = Math.Clamp(d / 255.0, 0.0, 1.0); applied++; break;
                case "cl_crosshairusealpha": useAlpha = d != 0; applied++; break;
                case "cl_crosshair_t": TStyle = d != 0; applied++; break;
                case "cl_crosshaircolor": colorPreset = (int)d; applied++; break;
                case "cl_crosshaircolor_r": cr = ClampByte(d); applied++; break;
                case "cl_crosshaircolor_g": cg = ClampByte(d); applied++; break;
                case "cl_crosshaircolor_b": cb = ClampByte(d); applied++; break;
                // Dynamic / weapon / style settings are recognized but intentionally ignored so the
                // crosshair remains static and game-agnostic.
                case "cl_crosshairstyle":
                case "cl_crosshairgap_useweaponvalue":
                case "cl_crosshair_dynamic_splitdist":
                case "cl_crosshair_dynamic_splitalpha_innermod":
                case "cl_crosshair_dynamic_splitalpha_outermod":
                case "cl_crosshair_dynamic_maxdist_splitratio":
                case "cl_fixedcrosshairgap":
                    applied++; break;
            }
        }
        ResolveColor(colorPreset, cr, cg, cb);
        if (!useAlpha) Alpha = 1.0;
        return applied;
    }

    private void ResolveColor(int preset, byte cr, byte cg, byte cb)
    {
        if (preset >= 0 && preset < Presets.Length) { (R, G, B) = Presets[preset]; }
        else { R = cr; G = cg; B = cb; } // 5 (custom) or unspecified => use r/g/b
    }

    private static byte ClampByte(double d) => (byte)Math.Clamp((int)Math.Round(d), 0, 255);

    // ---- Legacy console-config export ----------------------------------------------------------
    public string ToLegacyConfig()
    {
        var c = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("cl_crosshairsize ").Append(Size.ToString("0.###", c)).Append('\n');
        sb.Append("cl_crosshairgap ").Append(Gap.ToString("0.###", c)).Append('\n');
        sb.Append("cl_crosshairthickness ").Append(Thickness.ToString("0.###", c)).Append('\n');
        sb.Append("cl_crosshairdot ").Append(Dot ? "1" : "0").Append('\n');
        sb.Append("cl_crosshair_t ").Append(TStyle ? "1" : "0").Append('\n');
        sb.Append("cl_crosshair_drawoutline ").Append(Outline ? "1" : "0").Append('\n');
        sb.Append("cl_crosshair_outlinethickness ").Append(OutlineThickness.ToString("0.###", c)).Append('\n');
        sb.Append("cl_crosshairusealpha 1\n");
        sb.Append("cl_crosshairalpha ").Append(Math.Round(Alpha * 255.0).ToString("0", c)).Append('\n');
        sb.Append("cl_crosshaircolor 5\n"); // 5 = custom RGB
        sb.Append("cl_crosshaircolor_r ").Append(R.ToString(c)).Append('\n');
        sb.Append("cl_crosshaircolor_g ").Append(G.ToString(c)).Append('\n');
        sb.Append("cl_crosshaircolor_b ").Append(B.ToString(c)).Append('\n');
        return sb.ToString();
    }

    // ---- CS2 / CS:GO share code import ---------------------------------------------------------
    private const string Dictionary = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";

    public static bool LooksLikeShareCode(string s) =>
        !string.IsNullOrWhiteSpace(s) && s.Trim().StartsWith("CSGO-", StringComparison.OrdinalIgnoreCase);

    // Decodes a share code into these settings. Returns false (leaving settings unchanged) on any
    // malformed input. Follows the community base-58 layout; the first decoded byte is a checksum.
    public bool TryParseShareCode(string shareCode)
    {
        if (!LooksLikeShareCode(shareCode)) return false;
        var chars = shareCode.Trim().Substring(5).Replace("-", "");
        if (chars.Length != 25) return false;

        BigInteger big = BigInteger.Zero;
        foreach (char ch in chars)
        {
            int idx = Dictionary.IndexOf(ch);
            if (idx < 0) return false;
            big = big * 58 + idx;
        }

        // Big-endian, fixed 18-byte buffer.
        var raw = big.ToByteArray(isUnsigned: true, isBigEndian: true);
        if (raw.Length > 18) return false;
        var b = new byte[18];
        Array.Copy(raw, 0, b, 18 - raw.Length, raw.Length);

        // Checksum: byte[0] == sum(byte[1..17]) & 0xFF.
        int sum = 0; for (int i = 1; i < 18; i++) sum += b[i];
        if ((sum & 0xFF) != b[0]) return false;

        Gap = (sbyte)b[2] / 10.0;
        OutlineThickness = b[3] / 2.0;
        byte cr = b[4], cg = b[5], cb = b[6];
        Alpha = Math.Clamp(b[7] / 255.0, 0.0, 1.0);

        int flags1 = b[10];
        int colorPreset = flags1 & 0b111;
        Outline = (flags1 & 0b1000) != 0;
        Dot = (flags1 & 0b10000) != 0;
        bool useAlpha = (flags1 & 0b1000000) != 0;
        TStyle = (flags1 & 0b10000000) != 0;

        // cl_crosshairsize is a 13-bit little value across b[12] and the low 5 bits of b[13].
        Size = (((b[13] & 0b11111) << 8) | b[12]) / 10.0;
        Thickness = b[14] / 10.0;

        ResolveColor(colorPreset == 5 ? -1 : colorPreset, cr, cg, cb);
        if (!useAlpha) Alpha = 1.0;
        return true;
    }

    // Import either format; returns a short human-readable status.
    public string ImportAny(string text)
    {
        if (LooksLikeShareCode(text))
            return TryParseShareCode(text) ? "Imported CS2 share code." : "Invalid CS2 share code.";
        int n = ParseLegacyConfig(text);
        return n > 0 ? $"Imported {n} crosshair setting(s)." : "No crosshair settings found.";
    }
}
