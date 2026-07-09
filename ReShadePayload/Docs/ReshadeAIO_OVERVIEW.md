# ReshadeAIO

All-in-one ReShade payload + installer for **VR games**, with first-class support for the
**OpenVR → OpenComposite → OpenXR (VDXR)** chain that breaks stock ReShade.

This project packages and deploys a **patched ReShade** (built from the source tree in the
sibling project `F:\AI-Projects\ReshadeAI\reshade`) so that ReShade loads in-HMD on games that
run through OpenComposite, renders its menu as an OpenXR quad, and mirrors that menu to the desktop.

## Status (2026-06-29)

- ✅ **DiRT Rally 2.0 WORKS** — launches with ReShade fully loaded via the OpenXR layer under
  OpenComposite. No more `IVRCompositor_029` / `IVROverlay_028` aborts. In-HMD quad menu + desktop
  mirror + 7770×803 VR effect runtime all initialize. (Proven by `ReShade.log`; see memory notes.)
- 🔜 Roll the same payload onto the next game (UE/Unity/native-OpenXR titles in `D:\VR Games`).
- 🔜 True "AIO" one-click installer + portable package. For now a **"just works for DiRT"** flow
  via the scripts below is in place and is the template for the next game.

## The core problem & fix (read `memory/opencomposite-interface-fix.md` for detail)

OpenComposite re-implements OpenVR on top of OpenXR but **hard-aborts the process** (`OOVR_ABORT`)
when an app requests an OpenVR interface version it doesn't implement. Stock ReShade 6.7.3 probes:

| ReShade probes | OpenComposite implements | Result |
|---|---|---|
| `IVRCompositor_029` (forced) | ≤ 028 | abort (XR-layer mode) |
| `IVROverlay_028` (VR menu) | ≤ 027 | abort (DX11 mode) |

The game itself only needs `IVRCompositor_022`, which OC supports — so **baseline VR is fine; ReShade
is what triggers the abort.** Fix = two source patches that detect OpenComposite and avoid the two
over-version probes:

- `source/openvr/openvr.cpp` → `reshade_is_opencomposite()` helper; `check_and_init_openvr_hooks()`
  skips the forced `IVRCompositor_029` probe under OC.
- `source/runtime_gui_vr.cpp` → `init_gui_vr()` skips the OpenVR dashboard overlay under OC
  (the in-HMD menu uses the OpenXR quad path instead).

OC detection: `openvr_api.dll` exports `HmdSystemFactory` (OC-as-game-dll), **or** a loaded
`vrclient_x64.dll` whose path contains `opencomposite`/`openovr` (OC-as-runtime, this user's setup).

## Architecture (injection)

- **Native OpenXR games** (e.g. Pistol Whip): no `openvr_api.dll` in-process → stock-style XR layer
  works; the OC probes never fire.
- **OpenVR-via-OpenComposite games** (e.g. DiRT Rally 2): `openvr_api.dll` (genuine Valve loader)
  is present and routes to OC's `vrclient_x64.dll` (registered via
  `%LOCALAPPDATA%\openvr\openvrpaths.vrpath`). The patched XR layer is required.
- We deploy ReShade as the **global OpenXR implicit layer** (`C:\ProgramData\ReShade\ReShade64.dll`,
  registered by `ReShade64_XR.json` under `HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit`).
  One deploy covers every OpenXR/OC game. Do **not** also drop `dxgi.dll`/`d3d11.dll` in a game
  folder — that double-injects and conflicts.

## Per-game runtime config (created on first launch, in the game folder)

- `ReShade.ini` — desktop game-window runtime
- `ReShadeVR.ini` — VR effect runtime (stereo, e.g. 7770×803) ← **put your preset/techniques here**
- `ReShadeVR2.ini` — overlay/menu runtime
- Menu key: `KeyOverlay=36` (Home). Global in-HMD quad transform:
  `C:\ProgramData\ReShade\openxr_quad_transform.ini`.

## Scripts (`scripts/`)

- `Deploy-ReShade.ps1` — build the patched ReShade and copy it to ProgramData (the global layer).
- `Add-Game.ps1 -GameDir "<path>"` — prep a game folder (shaders + ini templates, remove conflicting
  game-dll injectors). Injection itself is handled by the global layer.
- `Test-Game.ps1 -Exe "<path>" [-Seconds 30]` — launch, wait, kill, and summarize the ReShade log
  (flags any OOVR/abort/interface-version lines). The core iteration loop.

## Build source

Patched ReShade source + CMake build live in `F:\AI-Projects\ReshadeAI\reshade`.
Build: `cmake --build build --config Release --target ReShade` (VS 18 2026 generator already cached).
Output: `build\Release\ReShade64.dll`.
