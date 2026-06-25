# ReShade 6.7.3 OpenXR In-HMD Menu Project

**Last Updated:** 2026-06-25 session 3 (end)
**Status:** HMD overlay working. Preview window working. Gameplay/Tuning mode. ViewLab backend ready. Global quad persistence. Splash fixed.
**Primary Test Target:** Pistol Whip (`D:\VR Games\Pistol Whip-working\`)
**Fallback Target:** iRacing (`C:\Program Files (x86)\iRacing\`)

---

## Quick Start for Continuation

1. Read `memory/MEMORY.md` — index + current status
2. Read `memory/project_goal.md` — what's done, what's next
3. Read `memory/code_map.md` — every changed file with key line ranges
4. Read `memory/architecture_overview.md` — system design + data flow
5. Build/deploy: see `memory/build_deploy.md`

---

## Project Goal

User launches iRacing → presses Home key → ReShade ImGui menu appears as floating panel in Quest 3 headset (XrCompositionLayerQuad) → can toggle effects, switch presets in real-time.

VDXR (Quest 3 + Virtual Desktop) provides OpenXR but not SteamVR. ReShade's existing VR menu is SteamVR-only — invisible in headset. This project adds native OpenXR overlay support.

---

## Current State (what works)

- HMD overlay renders ImGui menu ✅
- Home key toggles HMD overlay ✅
- Desktop "ReShade VR Overlay" preview window shows content ✅
- Correct colors (sRGB swapchain + R/B swap) ✅
- Gameplay mode default (desktop dormant, zero overhead) ✅
- Tuning mode via ViewLab shared memory ✅ (ViewLab not yet built)
- No per-frame logging or heap allocs ✅
- Splash bar doesn't stall ✅ (fixed VR runtime + gameplay mode both)
- Desktop preview: headless/topmost/snap-cursor/draggable ✅ (ViewLab controls these)
- VR quad edit modes: blue border (reposition), orange border (transform) ✅
- Mouse drag → quad position/rotation/scale ✅
- **Global quad transform persisted to disk** ✅ `C:\ProgramData\ReShade\openxr_quad_transform.ini`

---

## Two Modes

| | Gameplay (default) | Tuning (ViewLab) |
|-|-------------------|-----------------|
| VR effects | ✅ | ✅ |
| HMD menu | ✅ Home key | ✅ Home key |
| Desktop effects | ❌ skipped | ✅ active |
| Desktop hotkeys | ❌ overlay only | ✅ all |
| Effect Runtime Sync | ❌ | ✅ |
| Preview window | ✅ always | ✅ always |

---

## Shared Memory (ViewLab interface)

Full XRControlBlock — see `memory/code_map.md` for all fields.
Key new fields vs session 2: `win_headless`, `win_always_on_top`, `win_snap_cursor`, `quad_edit_mode`, full quad transform (pos/quat/size/alpha).

ViewLab: Tuning checkbox → `xr_mode = checked ? 1 : 0; revision++;`

---

## Global Quad Transform File

`C:\ProgramData\ReShade\openxr_quad_transform.ini` — created on first run with OKB Kneeboard 8 defaults. Loaded at xrCreateSession. Saved when edit mode exits. Global across all games via the XR layer.

---

## ViewLab / ReShade Responsibility Split

**ViewLab (remote control):** UI for headless/topmost/reposition/transform toggles; writes XRControlBlock; reads back live position; saves preferences.

**ReShade DLL (machinery):** Applies window style changes; draws edit-mode borders; applies mouse drag to quad; reads/writes transform INI; renders HMD menu at final transform.

---

## Build & Deploy

```powershell
# cmake is at VS Insiders path:
$cmake = "C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
& $cmake --build "F:\ReshadeAI\reshade\build" --config Release --target ReShade

# Deploy (run as admin)
Copy-Item "F:\ReshadeAI\reshade\build\Release\ReShade64.dll" "C:\ProgramData\ReShade\ReShade64.dll" -Force

# Test
& "D:\VR Games\Pistol Whip-working\Pistol Whip.exe"

# Logs
Get-Content "D:\VR Games\Pistol Whip-working\ReShade.log" -Tail 50
```

---

## Key Files Changed (from stock ReShade 6.7.3)

| File | Change |
|------|--------|
| `source/openxr/openxr_overlay.hpp` | New: public API, full XRControlBlock with window/quad/edit fields |
| `source/openxr/openxr_hooks_swapchain.cpp` | New: overlay, xrEndFrame, quad transform persistence (INI), edit delta application |
| `source/openxr/openxr_overlay_preview.cpp` | New: preview window + headless/topmost/snap/drag/border + shared memory |
| `source/runtime_gui_vr.cpp` | Modified: draw_gui_vr() renders ImGui to overlay texture |
| `source/runtime_gui.cpp` | Modified: open_overlay() → set_overlay_visible_active(); Gameplay gate; splash !_is_vr fix |
| `source/runtime.cpp` | Modified: on_present() gates update_effects() in Gameplay mode |
| `source/dll_main.cpp` | Modified: single-instance guard allows XR layer coexistence |
| `deps/CMakeLists.txt` | Modified: added XInput.lib |
| `res/version.h` | Created: ReShade 6.7.3 version macros (not in repo, required for build) |

---

## Session History

| Date | What |
|------|------|
| 2026-06-25 AM | Guard patch, input hook, overlay swapchain, texture, ImGui rendering, quad submission |
| 2026-06-25 PM | Home key, 500×500, OKB position, sRGB, R/B fix, preview window, Gameplay/Tuning, hotkey decoupling, no per-frame log/alloc |
| 2026-06-25 PM2 | ViewLab backend: XRControlBlock extended, preview window flags (headless/topmost/snap/drag), edit borders, drag delta → quad, global transform INI persistence, splash !_is_vr fix |

## Backups

`F:\ReshadeAI\backups\2026-06-25_build2\` — last known working snapshot before session 3 changes
