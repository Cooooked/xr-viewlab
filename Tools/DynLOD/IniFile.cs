using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DynLOD;

internal sealed class IniFile
{
    public const string Main = "Graphics Options", Replay = "Replay Graphics", Fps = "LODMinFPSTarget";
    public static readonly string[] Keys = ["LODPctMin", "LODPctMax", "LODPctDynoMin", "LODPctDynoMax", "LODPctMirrorsMin", "LODPctMirrorsMax", "LODPctDynoMirrorsMin", "LODPctDynoMirrorsMax", Fps];
    readonly byte[] original;
    readonly Encoding encoding;
    readonly int preamble;
    readonly string[] lines;
    readonly Dictionary<string, Dictionary<string, int>> sections = new(StringComparer.OrdinalIgnoreCase);
    static readonly Regex Header = new(@"^\s*\[([^\]]+)\]\s*(?:[;#].*)?$", RegexOptions.Compiled);
    static readonly Regex Entry = new(@"^(\s*([^=;#\s]+)\s*=\s*)([^;#\r\n]*?)([ \t]*(?:[;#].*)?)$", RegexOptions.Compiled);

    public IniFile(string path)
    {
        original = File.ReadAllBytes(path);
        (encoding, preamble) = original.AsSpan().StartsWith(new byte[] {255,254}) ? (Encoding.Unicode, 2)
            : original.AsSpan().StartsWith(new byte[] {254,255}) ? (Encoding.BigEndianUnicode, 2)
            : (Encoding.Latin1, 0); // Byte-for-byte preservation of UTF-8, ANSI and BOMs; edited keys are ASCII.
        lines = Regex.Split(encoding.GetString(original, preamble, original.Length - preamble), "(\r\n|\n|\r)");
        Dictionary<string, int>? current = null;
        for (int i = 0; i < lines.Length; i += 2)
        {
            var h = Header.Match(lines[i].TrimStart('\uFEFF', '\u00EF', '\u00BB', '\u00BF'));
            if (h.Success)
            {
                string name = h.Groups[1].Value;
                if (sections.ContainsKey(name)) throw new InvalidDataException($"Duplicate section [{name}]. File was not changed.");
                sections[name] = current = new(StringComparer.OrdinalIgnoreCase);
                continue;
            }
            var e = Entry.Match(lines[i]);
            if (current == null || !e.Success || !Keys.Contains(e.Groups[2].Value, StringComparer.OrdinalIgnoreCase)) continue;
            if (!current.TryAdd(e.Groups[2].Value, i)) throw new InvalidDataException($"Duplicate key {e.Groups[2].Value}. File was not changed.");
        }
    }

    public Dictionary<string, string> Read(string section)
    {
        if (!sections.TryGetValue(section, out var entries)) throw new InvalidDataException($"No [{section}] section in this INI.");
        var result = new Dictionary<string, string>();
        foreach (string key in Keys)
        {
            if (!entries.TryGetValue(key, out int index)) throw new InvalidDataException($"Missing {key} in [{section}]. Choose an existing iRacing renderer INI.");
            result[key] = Entry.Match(lines[index]).Groups[3].Value.Trim();
        }
        return result;
    }

    public static void Validate(IReadOnlyDictionary<string, string> values)
    {
        foreach (string key in Keys)
        {
            if (!values.TryGetValue(key, out var text) || !Regex.IsMatch(text, @"^[0-9]+$") || !int.TryParse(text, out var n))
                throw new InvalidDataException($"{(key == Fps ? "FPS Target" : key)} must be a whole number.");
            if (key != Fps && (n < 25 || n > 500)) throw new InvalidDataException("LOD values must be between 25 and 500.");
        }
        for (int i = 0; i < 8; i += 2)
            if (int.Parse(values[Keys[i]]) > int.Parse(values[Keys[i + 1]])) throw new InvalidDataException("Each Min must be no greater than its Max.");
        if (int.Parse(values["LODPctDynoMax"]) < int.Parse(values["LODPctMin"]) ||
            int.Parse(values["LODPctDynoMirrorsMax"]) < int.Parse(values["LODPctMirrorsMin"]))
            throw new InvalidDataException("World Max must be at least Cars Min. iRacing expands World Max when these ranges do not overlap.");
    }

    public static string Apply(string path, string section, IReadOnlyDictionary<string, string> values)
    {
        Validate(values);
        var latest = new IniFile(path); // Deliberately merge into the latest file, never the UI's old snapshot.
        latest.Read(section);
        foreach (string key in Keys)
        {
            int index = latest.sections[section][key];
            var m = Entry.Match(latest.lines[index]);
            latest.lines[index] = m.Groups[1].Value + values[key] + m.Groups[4].Value;
        }
        byte[] body = latest.encoding.GetBytes(string.Concat(latest.lines));
        byte[] bytes = latest.original.Take(latest.preamble).Concat(body).ToArray();
        string temp = path + ".dynlod-" + Guid.NewGuid().ToString("N") + ".tmp";
        string backup = path + ".dynlod-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..6] + ".bak";
        try
        {
            File.WriteAllBytes(temp, bytes);
            if (!File.ReadAllBytes(path).SequenceEqual(latest.original)) throw new IOException("The INI changed during Apply. Your edits are still here; try Apply again.");
            File.Replace(temp, path, backup); // Same-volume atomic replacement with the previous file as backup.
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return backup;
    }
}
