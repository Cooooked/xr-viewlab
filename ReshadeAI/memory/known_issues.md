# Known Issues & Solutions

## Fixed

| # | Issue | Fix | File |
|---|-------|-----|------|
| 1 | Single-instance guard kills XR layer | Detect XR layer by module name, continue instead of abort | `dll_main.cpp:131,220-226` |
| 2 | VR menu only worked on SteamVR | `if (!has_steamvr) return` → `if (!has_steamvr && !has_openxr) return` | `runtime_gui_vr.cpp` |
| 3 | VR runtime had no keyboard input | Find game HWND via EnumWindows, register for input | `runtime.cpp:444-470` |
| 4 | Overlay swapchain format wrong | Vulkan format 37 → DXGI format 29 (sRGB) | `openxr_hooks_swapchain.cpp` |
| 5 | HMD colors washed out | Non-sRGB format caused double gamma. Use format 29 (sRGB) | swapchain create |
| 6 | Desktop preview brown/wrong colors | GPU=RGBA, Win32 DIB=BGRA. Added R↔B swap in preview_update() | `openxr_overlay_preview.cpp` |
| 7 | Home key didn't toggle HMD overlay | `open_overlay()` never called `set_overlay_visible_active()` | `runtime_gui.cpp` |
| 8 | Texture size wrong | Was using game framebuffer (7568×804 stereo). Fixed to 500×500 | `openxr_overlay.hpp` |
| 9 | Preview window wrong size | Was 250×250. Fixed to 500×500 | `openxr_overlay_preview.cpp` |
| 10 | VR quad too large | Was 1.0×1.0m. Changed to OKB Kneeboard 8: 0.32×0.30m | session struct defaults |
| 11 | Per-frame logging | Removed all per-frame [OVERLAY] log lines | multiple files |
| 12 | Per-frame heap alloc in xrEndFrame | `std::vector` → stack array `[16]` | `openxr_hooks_swapchain.cpp` |
| 13 | Desktop runtime competing for hotkeys | Gameplay mode gate: desktop skips non-overlay hotkeys | `runtime_gui.cpp` |
| 14 | Desktop runtime running effects | Gameplay mode gate: desktop skips `update_effects()` | `runtime.cpp` |
| 15 | Preview window always empty | Staging readback was gated on tuning_mode=false. Removed gate — always runs | `openxr_hooks_swapchain.cpp` |
| 16 | VR draw_gui_vr early-exit on `_show_overlay` | VR runtime's `_show_overlay` is always false. Removed early exit. | `runtime_gui_vr.cpp` |
| 17 | "Play" confused with ReShade's Performance Mode | Renamed Play → Gameplay throughout | all overlay files |
| 18 | Splash bar stalls on VR runtime | VR runtime `_show_overlay` never set via Home key → tutorial_index==0 condition persists. Fix: add `!_is_vr` to that clause | `runtime_gui.cpp:797` |
| 19 | Splash bar stalls on desktop Gameplay mode | `_reload_count<=1` timer clause persists since effects never load. Fix: gate on `!desktop_gameplay_mode` | `runtime_gui.cpp:797` |

---

## Active / Known Limitations

### OKB Kneeboard 8 position not confirmed in HMD
Code uses LOCAL reference space. Defaults: pos=(-0.32, -0.16, -0.81), quat=(-0.0778, 0.1301, 0.0102, 0.9884), size=(0.32, 0.30). User reports still appearing centered. **Workaround:** use Reposition mode to drag quad to correct position — saves to `C:\ProgramData\ReShade\openxr_quad_transform.ini` and reloads on next launch.

### Mouse interaction in preview window not fed to ImGui
`preview_get_mouse_state()` captures mouse, drag deltas tracked. Not yet wired into ImGui input — clicking the preview doesn't interact with the HMD menu.

### Quad alpha (opacity) not yet applied to ImGui render
`quad_alpha` tracked in ctrl block and persisted in ini. Actual ImGui alpha wiring (multiply render target pixels by alpha) not implemented yet.

### ViewLab not built
Companion app that writes `Local\ReShadeXRControl` doesn't exist yet. Default is Gameplay mode. ReShade handles position persistence itself until ViewLab is ready.

### iRacing not tested
All testing on Pistol Whip. iRacing has a launcher. Test when ready.
