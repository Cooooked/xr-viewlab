# Architecture Overview

## How ReShade Loads in VR

ReShade64.dll is registered as an **OpenXR implicit API layer**:

```
HKCU\Software\Khronos\OpenXR\1\ApiLayers\Implicit
  → C:\ProgramData\ReShade\ReShade64_XR.json   (manifest)
  → C:\ProgramData\ReShade\ReShade64.dll        (the DLL)
```

When a game starts OpenXR, the runtime loads ReShade64.dll automatically.

---

## Two Runtime Instances (one DLL)

One DLL creates **two separate ReShade runtime instances** in the same process:

| Instance | Config | `_is_vr` | Runs |
|----------|--------|----------|------|
| Desktop | `ReShade.ini` | false | `draw_gui()` — handles Home key |
| VR | `ReShadeVR.ini` | true | `draw_gui_vr()` — renders ImGui to overlay |

This is normal ReShade behavior — it hooks both D3D and OpenXR simultaneously.

---

## Overlay Pipeline (per frame)

```
draw_gui_vr()  [VR runtime]
  get_active_overlay_resources() → oxr_tex, oxr_target
  barrier: copy_source → render_target
  clear_render_target_view (transparent black)
  render_imgui_draw_data(cmd_list, draw_data, oxr_target)
  barrier: render_target → copy_source

xrEndFrame hook
  Staging readback (always):
    copy_texture_region(overlay_texture → staging_texture)
    map_texture_region → preview_update() → StretchDIBits in preview window
  if overlay_visible:
    copy_overlay_to_swapchain():
      xrAcquireSwapchainImage → xrWaitSwapchainImage
      barrier → CopyResource → barrier
      xrReleaseSwapchainImage
    append XrCompositionLayerQuad to layers[]
    submit modified XrFrameEndInfo
```

---

## Key Structs

### openxr_session (openxr_hooks_swapchain.cpp)
```cpp
struct openxr_session {
    XrSwapchain        overlay_swapchain = XR_NULL_HANDLE;
    api::resource      overlay_texture   = {};   // GPU render target, 500×500
    api::resource_view overlay_target    = {};   // RTV
    api::resource      staging_texture   = {};   // CPU-readable, always allocated
    bool  overlay_visible    = false;
    float overlay_quad_width = 0.32f,  overlay_quad_height = 0.30f;
    float overlay_distance   = 0.81f;
    float overlay_offset_x   = -0.32f, overlay_offset_y   = -0.16f;
    float overlay_quat_x=-0.0778f, overlay_quat_y=0.1301f,
          overlay_quat_z=0.0102f,  overlay_quat_w=0.9884f;
};
```

### XRControlBlock (shared memory `Local\ReShadeXRControl`)
```cpp
struct XRControlBlock {
    uint32_t version;   // must be 1
    uint32_t size;      // sizeof(XRControlBlock) — layout guard
    uint32_t xr_mode;   // 0 = XR_MODE_GAMEPLAY, 1 = XR_MODE_TUNING
    uint32_t revision;  // incremented by ViewLab on every mode change
};
```

---

## Overlay Texture Specs

| Property | Value |
|----------|-------|
| Size | 500 × 500 |
| GPU texture format | `DXGI_FORMAT_R8G8B8A8_UNORM` |
| Swapchain format | `DXGI_FORMAT_R8G8B8A8_UNORM_SRGB` (format 29) |
| Initial state | `copy_source` |
| Render state | `render_target` (during ImGui draw only) |

**Why sRGB swapchain:** OpenXR compositor would double-apply gamma on a non-sRGB swapchain → washed out colors. sRGB format declares the data is already gamma-encoded.

---

## Quad Position (OKB Kneeboard 8)

```cpp
XrPosef pose = {
    .orientation = { -0.0778f, 0.1301f, 0.0102f, 0.9884f },
    .position    = { -0.32f, -0.16f, -0.81f }   // meters, stage space
};
XrExtent2Df extent = { 0.32f, 0.30f };
```

Bottom-left of center, roughly kneeboard position in a sim cockpit.

---

## Home Key Toggle Flow

```
User presses Home
  → desktop runtime draw_gui() detects _overlay_key_data (0x24)
  → calls open_overlay(true, keyboard)          [runtime_gui.cpp]
  → _show_overlay = true
  → set_overlay_visible_active(true)            [openxr_overlay.hpp]
  → session.overlay_visible = true
  → next xrEndFrame: overlay_visible=true → quad submitted
```

The VR runtime (`_is_vr=true`) never runs `draw_gui()` — keyboard is handled by the desktop runtime only.

---

## Gameplay / Tuning Mode

`control_is_tuning()` is a single memory read (~0ns). Called:
- Once at `xrCreateSession` (not used anymore — tuning_mode field removed)
- Per-frame in `draw_gui()` and `on_present()` on the desktop runtime

**Gameplay mode** (`xr_mode=0`, default):
- `on_present()`: `update_effects()` skipped on desktop runtime
- `draw_gui()`: all hotkeys suppressed on desktop except overlay toggle
- Staging readback: always runs (preview window always shows content)
- Effect Runtime Sync: inactive (desktop has no effects to sync)

**Tuning mode** (`xr_mode=1`):
- `on_present()`: `update_effects()` runs on desktop runtime
- `draw_gui()`: all hotkeys active on desktop
- Staging readback: always runs
- Effect Runtime Sync: active — desktop preset changes propagate to VR

---

## Desktop Preview Window

- Win32 window, "ReShade VR Overlay", 500×500
- Spawned at `xrCreateSession` via `preview_init()` — always, regardless of mode
- 30fps WM_TIMER drives repaints
- Pixels: GPU overlay texture → staging_texture (GPU→CPU copy) → `preview_update()` → `StretchDIBits`
- **R/B swap in preview_update():** GPU gives RGBA, Win32 DIB expects BGRA
- Mouse state captured (WM_MOUSEMOVE/BUTTON/WHEEL) via `preview_get_mouse_state()` — not yet fed to ImGui

---

## File Map

```
F:\ReshadeAI\reshade\source\
  openxr\
    openxr_overlay.hpp            ← public API: constants, structs, declarations
    openxr_hooks_swapchain.cpp    ← overlay creation, copy, submission, xrEndFrame hook
    openxr_overlay_preview.cpp    ← Win32 preview window + shared memory (control_*)
    openxr_hooks_instance.cpp     ← xrCreateInstance + WaitSwapchainImage dispatch
    openxr_impl_swapchain.hpp     ← get_graphics_queue() accessor
  runtime_gui_vr.cpp              ← draw_gui_vr(): renders ImGui to overlay texture
  runtime_gui.cpp                 ← open_overlay(): calls set_overlay_visible_active()
  runtime.cpp                     ← on_present(): gates update_effects() in Gameplay mode
  runtime.hpp                     ← draw_gui_vr_overlay_settings() declaration
  dll_main.cpp:131,220-226        ← single-instance guard: XR layer coexists with dxgi.dll
```

## Deployment

```
C:\ProgramData\ReShade\ReShade64.dll   ← overwrite to deploy
C:\ProgramData\ReShade\ReShade64_XR.json  ← static manifest, never changes
```

Build: `F:\ReshadeAI\reshade\build\Release\ReShade64.dll`
