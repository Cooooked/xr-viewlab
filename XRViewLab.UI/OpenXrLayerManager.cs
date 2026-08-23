using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Win32;

namespace XRViewLab.UI;

public enum LayerScope
{
    Win64Machine,
    Win64User,
    Win32Machine,
    Win32User,
}

public sealed class LayerEntry
{
    public string ManifestPath { get; set; } = "";
    public string FileName => Path.GetFileName(ManifestPath);
    public bool Enabled { get; set; }
    public int Order { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? DllPath { get; set; }
    public bool ManifestExists { get; set; }
    public bool DllExists { get; set; }
    public string? Signer { get; set; }
    public bool SignatureTrusted { get; set; }

    public List<string> Warnings { get; } = new();
    public bool HasWarning => Warnings.Count > 0;
    public string WarningText => string.Join("  •  ", Warnings);
    public string StateText => Enabled ? "ON" : "off";
}

/// <summary>
/// Reads and edits the OpenXR implicit API layer registry.
///
/// Conventions that matter and are easy to get wrong:
///  * The DWORD is a DISABLE flag. 0 means the layer is ACTIVE. Any non-zero
///    value means the loader skips it. Every UI here says "enabled" meaning 0.
///  * 64-bit and 32-bit applications read different registry views of the same
///    logical key, so a layer can be live for one and absent for the other.
///  * The loader applies implicit layers in the order the values are
///    enumerated, so reordering means rewriting the whole value set.
/// </summary>
public static class LayerManager
{
    private const string SubKey = @"SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit";

    // Layers with known behavioural problems, surfaced the way the OpenXR API
    // Layers GUI does. Substring match on the manifest filename.
    private static readonly (string Match, string Note)[] KnownIssues =
    {
        ("MBUCCHIA_toolkit", "OpenXR Toolkit is unsupported and is known to cause crashes in modern games; disable it if you hit problems."),
    };

    public static (RegistryHive Hive, RegistryView View) Resolve(LayerScope scope) => scope switch
    {
        LayerScope.Win64Machine => (RegistryHive.LocalMachine, RegistryView.Registry64),
        LayerScope.Win64User => (RegistryHive.CurrentUser, RegistryView.Registry64),
        LayerScope.Win32Machine => (RegistryHive.LocalMachine, RegistryView.Registry32),
        _ => (RegistryHive.CurrentUser, RegistryView.Registry32),
    };

    public static string ScopeLabel(LayerScope s) => s switch
    {
        LayerScope.Win64Machine => "Win64 · All users",
        LayerScope.Win64User => "Win64 · This user",
        LayerScope.Win32Machine => "Win32 · All users",
        _ => "Win32 · This user",
    };

    public static bool RequiresElevation(LayerScope s)
        => s is LayerScope.Win64Machine or LayerScope.Win32Machine;

    public static List<LayerEntry> Read(LayerScope scope)
    {
        var list = new List<LayerEntry>();
        var (hive, view) = Resolve(scope);
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.OpenSubKey(SubKey);
        if (key is null) return list;

        int order = 0;
        foreach (var name in key.GetValueNames())
        {
            var raw = key.GetValue(name);
            int val = raw is int i ? i : 1;
            var e = new LayerEntry
            {
                ManifestPath = name,
                Enabled = val == 0,
                Order = order++,
            };
            Inspect(e);
            list.Add(e);
        }
        return list;
    }

    private static void Inspect(LayerEntry e)
    {
        e.ManifestExists = File.Exists(e.ManifestPath);
        if (!e.ManifestExists)
        {
            e.Warnings.Add("Manifest file is missing; this entry does nothing.");
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(e.ManifestPath));
            if (doc.RootElement.TryGetProperty("api_layer", out var al))
            {
                if (al.TryGetProperty("name", out var n)) e.Name = n.GetString();
                if (al.TryGetProperty("description", out var d)) e.Description = d.GetString();
                if (al.TryGetProperty("library_path", out var lp))
                {
                    var rel = lp.GetString() ?? "";
                    var dir = Path.GetDirectoryName(e.ManifestPath) ?? "";
                    e.DllPath = Path.GetFullPath(Path.IsPathRooted(rel) ? rel : Path.Combine(dir, rel));
                }
            }
        }
        catch (Exception ex)
        {
            e.Warnings.Add("Manifest is not valid JSON: " + ex.Message);
            return;
        }

        if (e.DllPath is null) { e.Warnings.Add("Manifest declares no library_path."); return; }

        e.DllExists = File.Exists(e.DllPath);
        if (!e.DllExists) { e.Warnings.Add("Layer DLL is missing: " + e.DllPath); return; }

        // Signature. Anti-cheat middleware rejects unsigned injected DLLs, which
        // is a real and common cause of "this game won't launch in VR".
        try
        {
            var cert = X509Certificate.CreateFromSignedFile(e.DllPath);
            var c2 = new X509Certificate2(cert);
            e.Signer = c2.GetNameInfo(X509NameType.SimpleName, false);

            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            e.SignatureTrusted = chain.Build(c2);
            if (!e.SignatureTrusted)
                e.Warnings.Add("Signature is not trusted; this can break games that use anti-cheat.");
            else if (DateTime.Now > c2.NotAfter)
                e.Warnings.Add($"Signing certificate expired {c2.NotAfter:yyyy-MM-dd}.");
        }
        catch
        {
            e.Signer = null;
            e.SignatureTrusted = false;
            e.Warnings.Add("DLL is not digitally signed; very likely to cause issues with anti-cheat software.");
        }

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!e.DllPath.StartsWith(pf, StringComparison.OrdinalIgnoreCase) &&
            !e.DllPath.StartsWith(pf86, StringComparison.OrdinalIgnoreCase))
        {
            e.Warnings.Add("DLL is outside Program Files; sandboxed Store apps may not be able to load it.");
        }

        foreach (var (match, note) in KnownIssues)
            if (e.ManifestPath.Contains(match, StringComparison.OrdinalIgnoreCase))
                e.Warnings.Add(note);
    }

    public static void SetEnabled(LayerScope scope, string manifestPath, bool enabled)
    {
        var (hive, view) = Resolve(scope);
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.OpenSubKey(SubKey, writable: true)
                        ?? throw new InvalidOperationException("OpenXR implicit layer key not found for this scope.");
        key.SetValue(manifestPath, enabled ? 0 : 1, RegistryValueKind.DWord);
    }

    public static void Remove(LayerScope scope, string manifestPath)
    {
        var (hive, view) = Resolve(scope);
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.OpenSubKey(SubKey, writable: true)
                        ?? throw new InvalidOperationException("OpenXR implicit layer key not found for this scope.");
        key.DeleteValue(manifestPath, throwOnMissingValue: false);
    }

    public static void Add(LayerScope scope, string manifestPath, bool enabled = true)
    {
        var (hive, view) = Resolve(scope);
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.CreateSubKey(SubKey, writable: true)
                        ?? throw new InvalidOperationException("Could not open the OpenXR implicit layer key.");
        key.SetValue(manifestPath, enabled ? 0 : 1, RegistryValueKind.DWord);
    }

    /// <summary>
    /// Rewrite every value so the enumeration order matches the given list.
    /// A backup .reg is written first: this deletes and recreates the entire
    /// value set, and a half-applied reorder would silently change which layers
    /// wrap which.
    /// </summary>
    public static string Reorder(LayerScope scope, IReadOnlyList<LayerEntry> ordered)
    {
        var backup = ExportBackup(scope);
        var (hive, view) = Resolve(scope);
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var key = baseKey.OpenSubKey(SubKey, writable: true)
                        ?? throw new InvalidOperationException("OpenXR implicit layer key not found for this scope.");

        var snapshot = ordered.Select(e => (e.ManifestPath, Value: e.Enabled ? 0 : 1)).ToList();
        foreach (var name in key.GetValueNames()) key.DeleteValue(name, false);
        foreach (var (path, value) in snapshot) key.SetValue(path, value, RegistryValueKind.DWord);
        return backup;
    }

    public static string ExportBackup(LayerScope scope)
    {
        var dir = Path.Combine(Path.GetTempPath(), "DiagMon2-layer-backups");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"openxr-layers-{scope}-{DateTime.Now:yyyyMMdd-HHmmss}.reg");

        var (hive, view) = Resolve(scope);
        var hiveName = hive == RegistryHive.LocalMachine ? "HKEY_LOCAL_MACHINE" : "HKEY_CURRENT_USER";
        var path = view == RegistryView.Registry32 && hive == RegistryHive.LocalMachine
            ? @"SOFTWARE\WOW6432Node\Khronos\OpenXR\1\ApiLayers\Implicit"
            : SubKey;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Windows Registry Editor Version 5.00");
        sb.AppendLine();
        sb.AppendLine($"[{hiveName}\\{path}]");
        foreach (var e in Read(scope))
            sb.AppendLine($"\"{e.ManifestPath.Replace("\\", "\\\\")}\"=dword:{(e.Enabled ? 0 : 1):x8}");
        File.WriteAllText(file, sb.ToString());
        return file;
    }

    public static string BuildReport()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# OpenXR API layer report");
        sb.AppendLine();
        sb.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        foreach (LayerScope scope in Enum.GetValues<LayerScope>())
        {
            var layers = Read(scope);
            sb.AppendLine($"## {ScopeLabel(scope)}  ({layers.Count} registered)");
            sb.AppendLine();
            if (layers.Count == 0) { sb.AppendLine("_none_"); sb.AppendLine(); continue; }
            sb.AppendLine("| # | State | Manifest | Signer |");
            sb.AppendLine("| ---: | :---: | --- | --- |");
            foreach (var l in layers)
                sb.AppendLine($"| {l.Order + 1} | {(l.Enabled ? "ON" : "off")} | `{l.ManifestPath}` | {l.Signer ?? "unsigned"} |");
            sb.AppendLine();
            var warned = layers.Where(l => l.HasWarning).ToList();
            if (warned.Count > 0)
            {
                sb.AppendLine("Warnings:");
                sb.AppendLine();
                foreach (var l in warned)
                    foreach (var w in l.Warnings)
                        sb.AppendLine($"- `{l.FileName}` — {w}");
                sb.AppendLine();
            }
        }
        return sb.ToString();
    }
}
