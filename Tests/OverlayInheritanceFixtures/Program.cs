using System;
using System.Linq;
using XRViewLab.UI;

// Deterministic tests for per-overlay "Use Global Values" inheritance (item 24). These exercise the
// OverlayProfileOverrides model and the exact effective-value resolution the per-app editor uses:
//   effective = override.Get(feature,key) ?? global.Get(feature,key) ?? fallback
// Inheriting an overlay == it has no override keys (ClearFeature); customising == seeding its keys.

int failures = 0;
void Check(bool ok, string label) { Console.WriteLine((ok ? "PASS " : "FAIL ") + label); if (!ok) failures++; }

// Mirrors ProfileWindow.OverlayValue.
string Effective(OverlayProfileOverrides ov, OverlayProfileOverrides gl, string f, string k, string fallback)
    => ov.Get(f, k) ?? gl.Get(f, k) ?? fallback;

// Mirrors ProfileWindow.EnsureFeatureCustom: seed a feature's overrides from the current globals.
void SeedFromGlobal(OverlayProfileOverrides ov, OverlayProfileOverrides gl, string feature)
{
    if (ov.HasFeature(feature)) return;
    foreach (var pair in gl.Values.Where(p => p.Key.StartsWith(feature + ":", StringComparison.OrdinalIgnoreCase)))
        ov.Values[pair.Key] = pair.Value;
}

var global = new OverlayProfileOverrides();
global.Set("clock", "clock_widget_opacity", "0.8");
global.Set("hud", "hud_opacity", "0.5");
global.Set("hud", "hud_widget_cpu_unit", "1");
global.Set("crosshair", "crosshair_size", "10");

var overrides = new OverlayProfileOverrides();

// 1. Inherited overlay follows the global value.
Check(!overrides.HasFeature("clock"), "clock starts inherited (no override keys)");
Check(Effective(overrides, global, "clock", "clock_widget_opacity", "1") == "0.8", "inherited overlay resolves to the global value");

// 2. Global change propagates while inherited.
global.Set("clock", "clock_widget_opacity", "0.9");
Check(Effective(overrides, global, "clock", "clock_widget_opacity", "1") == "0.9", "global change propagates to an inherited overlay");

// 3. Override creation: customising the crosshair records only its keys.
overrides.Set("crosshair", "crosshair_size", "20");
Check(overrides.HasFeature("crosshair"), "customised overlay has override keys");
Check(Effective(overrides, global, "crosshair", "crosshair_size", "0") == "20", "override value wins over global");
Check(!overrides.HasFeature("clock") && !overrides.HasFeature("hud"), "customising one overlay does not create keys for others");

// 4. Local value stays stable after inheritance is disabled, even if the global later changes.
SeedFromGlobal(overrides, global, "hud");
Check(overrides.HasFeature("hud"), "seeding hud from global creates its override keys");
Check(Effective(overrides, global, "hud", "hud_opacity", "1") == "0.5", "seeded hud opacity matches the global at seed time");
global.Set("hud", "hud_opacity", "0.1");
Check(Effective(overrides, global, "hud", "hud_opacity", "1") == "0.5", "customised hud does NOT follow later global changes");
Check(overrides.Get("hud", "hud_widget_cpu_unit") == "1", "seeding carried the per-metric unit override too");

// 5. Re-inheriting one overlay clears only its keys and restores global-follow; others untouched.
overrides.ClearFeature("hud");
Check(!overrides.HasFeature("hud"), "re-inheriting hud clears its override keys");
Check(Effective(overrides, global, "hud", "hud_opacity", "1") == "0.1", "re-inherited hud follows the current global again");
Check(overrides.HasFeature("crosshair") && Effective(overrides, global, "crosshair", "crosshair_size", "0") == "20", "re-inheriting hud left the crosshair override intact");

// 6. Migration: an existing profile that already has override keys loads as customised.
var existing = new OverlayProfileOverrides();
existing.Set("notifications", "notify_opacity", "0.7");
Check(existing.HasFeature("notifications"), "an existing profile with keys migrates as a customised overlay");
Check(!existing.HasFeature("clock"), "features without keys in an existing profile remain inherited");

Console.WriteLine(failures == 0 ? "All overlay-inheritance fixtures passed." : $"{failures} fixture(s) failed.");
return failures == 0 ? 0 : 1;
