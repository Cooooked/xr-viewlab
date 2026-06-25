# Project Goal: ReShade In-HMD Menu via OpenXR

## The Problem

ReShade's VR menu is SteamVR-only (OpenVR overlay). The user runs VDXR (Quest 3 + Virtual Desktop), which provides OpenXR but not SteamVR. The menu is completely invisible in headset.

## The Solution

Render the ReShade ImGui menu to an `XrCompositionLayerQuad` overlay submitted via `xrEndFrame`. No SteamVR required. Works on any OpenXR runtime.

---

## Current Status (2026-06-25 session 3)

### ✅ Working

1. HMD overlay renders ImGui menu — 500×500, correct colors
2. Home key toggles HMD overlay on/off
3. Desktop "ReShade VR Overlay" preview window — always shows content
4. sRGB swapchain (format 29) — no double-gamma in HMD
5. R/B swap in desktop preview — correct colors
6. Gameplay mode (default): desktop dormant — skips effects + most hotkeys
7. Tuning mode (ViewLab): desktop fully active + Effect Runtime Sync
8. Shared memory control block: `Local\ReShadeXRControl`
9. No per-frame logging, no per-frame heap allocs
10. Desktop preview window flags: headless (WS_POPUP), always-on-top, snap-to-cursor, draggable-in-headless
11. VR quad edit modes: blue border (reposition), orange border (transform)
12. Drag delta tracking: LMB/RMB/MMB → quad position/scale/rotation
13. **Global quad transform persistence**: `C:\ProgramData\ReShade\openxr_quad_transform.ini` — loads on session start, saves on edit mode exit. Global across all games.
14. Splash bar fix: won't stall on VR runtime or desktop Gameplay mode

### 🔄 Pending

1. OKB Kneeboard 8 position may not appear correct in HMD — user to verify with new build. If still wrong, use reposition mode to drag to correct position (will save to ini).
2. Mouse/cursor interaction in preview window not yet fed to ImGui
3. ViewLab companion app not yet built
4. iRacing validation (Pistol Whip only so far)
5. Portable payload packaging
6. Quad alpha (opacity) — field tracked, ImGui alpha wiring not yet done

---

## Two Modes

### Gameplay Mode (default)
- VR runtime: full effects + in-HMD menu
- Desktop runtime: dormant — Home key only
- Preview window: always shows HMD overlay content
- `xr_mode = 0` (XR_MODE_GAMEPLAY)

### Tuning Mode (ViewLab checkbox)
- VR runtime: full effects + in-HMD menu
- Desktop runtime: full effects + all hotkeys + Effect Runtime Sync
- `xr_mode = 1` (XR_MODE_TUNING)

---

## Global Quad Transform File

`C:\ProgramData\ReShade\openxr_quad_transform.ini`

```ini
[Transform]
pos_x=-0.320000    ; stage-space X metres
pos_y=-0.160000    ; stage-space Y metres
pos_z=0.810000     ; stage-space Z (positive=forward, negated for OpenXR)
quat_x=-0.077800   ; orientation quaternion
quat_y=0.130100
quat_z=0.010200
quat_w=0.988400
width=0.320000     ; quad width metres
height=0.300000    ; quad height metres
alpha=1.000000     ; opacity 0-1
```

Created with OKB Kneeboard 8 defaults if missing. ViewLab will become the owner/UI for these values later.

---

## ViewLab / ReShade Division of Responsibility

**ViewLab (the remote control):**
- UI checkboxes: Headless, Always on top, Reposition desktop, Reposition HMD, Transform HMD
- Sends control values via `Local\ReShadeXRControl` shared memory
- Reads back live position from shared memory
- Saves user preferences

**ReShade DLL (the machinery):**
- Applies desktop window style changes (WS_POPUP, HWND_TOPMOST, snap-to-cursor)
- Draws colored border in preview window (blue=reposition, orange=transform)
- Applies mouse drag deltas to quad position/rotation/scale
- Reads/writes `openxr_quad_transform.ini` for persistence
- Renders HMD menu at final transform

---

## Backup

`F:\ReshadeAI\backups\2026-06-25_build2\` — DLL + all source files from build2 (working, pre-persistence)

## Test Target

**Primary:** Pistol Whip — `D:\VR Games\Pistol Whip-working\Pistol Whip.exe`
**Secondary:** iRacing — `C:\Program Files (x86)\iRacing\iRacingSim64DX11.exe`
Log: `D:\VR Games\Pistol Whip-working\ReShade.log`
