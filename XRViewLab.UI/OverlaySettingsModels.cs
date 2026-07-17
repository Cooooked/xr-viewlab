using System.Collections.Generic;

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
