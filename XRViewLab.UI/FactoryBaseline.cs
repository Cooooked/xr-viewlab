using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace XRViewLab.UI;

internal static class FactoryBaseline
{
    internal const string Version = "4.1.255";
    internal const string MigrationMarker = "FactoryBaselineAppliedVersion";

    private sealed class BaselineDocument
    {
        public Dictionary<string, string> IniSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, uint> ReshadeRemote { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly BaselineDocument Document = Load();
    internal static IReadOnlyDictionary<string, string> IniSettings => Document.IniSettings;
    internal static IReadOnlyDictionary<string, uint> ReShadeRemote => Document.ReshadeRemote;

    internal static string Resolve(string key, string fallback) =>
        Document.IniSettings.TryGetValue(key, out string? value) ? value : fallback;

    private static BaselineDocument Load()
    {
        Assembly assembly = typeof(FactoryBaseline).Assembly;
        string? name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith("factory-baseline-v4.1.255.json", StringComparison.OrdinalIgnoreCase));
        if (name == null) throw new InvalidOperationException("Embedded ViewLab factory baseline is missing.");
        using Stream stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Cannot open embedded ViewLab factory baseline.");
        return JsonSerializer.Deserialize<BaselineDocument>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("ViewLab factory baseline is invalid.");
    }
}
