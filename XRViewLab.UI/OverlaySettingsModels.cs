using System.Collections.Generic;
using System.Linq;

namespace XRViewLab.UI;

/// <summary>
/// The single product contract for ordinary overlay placement and visibility.
/// Feature-specific settings remain with their feature; these keys and defaults do not.
/// </summary>
internal sealed record OverlaySettingsDefinition(
    string Id,
    string EnabledKey,
    string ToggleKey,
    string XKey,
    string YKey,
    string ScaleKey,
    string OpacityKey,
    double DefaultX,
    double DefaultY,
    double DefaultScale,
    double DefaultOpacity,
    string? LegacyToggleKey = null,
    int DefaultToggleVirtualKey = 0);

internal static class OverlaySettingsCatalog
{
    internal const int NoHotkey = 0;
    internal const int FirstFunctionKey = 117; // VK_F6
    internal const int LastFunctionKey = 123;  // VK_F12

    internal static readonly IReadOnlyDictionary<string, OverlaySettingsDefinition> All =
        new Dictionary<string, OverlaySettingsDefinition>
        {
            ["clock"] = new("clock", "clock_widget_enabled", "overlay_clock_toggle_vk", "clock_widget_x", "clock_widget_y", "clock_widget_scale", "clock_widget_opacity", .50, .10, 1, .82),
            ["hud"] = new("hud", "hud_enabled", "overlay_hud_toggle_vk", "hud_anchor_x", "hud_anchor_y", "hud_scale", "hud_opacity", .04, .05, 1, .70),
            ["trace"] = new("trace", "hud_trace_enabled", "overlay_trace_toggle_vk", "hud_trace_x", "hud_trace_y", "hud_trace_scale", "hud_trace_opacity", .05, .75, 1, .70),
            ["sticky"] = new("sticky", "sticky_note_enabled", "overlay_sticky_note_toggle_vk", "sticky_note_x", "sticky_note_y", "sticky_note_scale", "sticky_note_opacity", .78, .22, 1, .85, "sticky_note_toggle_vk", 118),
            ["crosshair"] = new("crosshair", "crosshair_enabled", "overlay_crosshair_toggle_vk", "crosshair_offset_x", "crosshair_offset_y", "crosshair_scale", "crosshair_alpha", 0, 0, 1, 1),
            ["notifications"] = new("notifications", "notify_enabled", "overlay_notifications_toggle_vk", "notify_x", "notify_y", "notify_scale", "notify_opacity", .98, .98, 1, 1),
        };

    internal static int ComboIndexFromVirtualKey(int virtualKey) =>
        virtualKey is >= FirstFunctionKey and <= LastFunctionKey ? virtualKey - FirstFunctionKey + 1 : 0;

    internal static int VirtualKeyFromComboIndex(int index) =>
        index is >= 1 and <= 7 ? FirstFunctionKey + index - 1 : NoHotkey;
}

/// <summary>Per-app overlay configuration stored as string values using the canonical INI keys.</summary>
public sealed class OverlayProfileOverrides
{
    public Dictionary<string, string> Values { get; } = new(System.StringComparer.OrdinalIgnoreCase);

    public OverlayProfileOverrides Clone()
    {
        var clone = new OverlayProfileOverrides();
        foreach (var pair in Values) clone.Values[pair.Key] = pair.Value;
        return clone;
    }

    public bool HasFeature(string id) => Values.Keys.Any(key => key.StartsWith(id + ":", System.StringComparison.OrdinalIgnoreCase));
    // Remove every override key belonging to one feature/overlay, so it falls back to the global value
    // (per-overlay "Use Global Values" inheritance). Other overlays are untouched.
    public void ClearFeature(string id)
    {
        foreach (string key in Values.Keys.Where(k => k.StartsWith(id + ":", System.StringComparison.OrdinalIgnoreCase)).ToList())
            Values.Remove(key);
    }
    public string? Get(string feature, string key) => Values.TryGetValue(feature + ":" + key, out string? value) ? value : null;
    public void Set(string feature, string key, string value) => Values[feature + ":" + key] = value;
    public void Clear() => Values.Clear();
}
