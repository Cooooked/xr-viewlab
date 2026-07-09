# OpenComposite ↔ ReShade interface-version abort — root cause & fix

## Symptom
DiRT Rally 2.0 (OpenVR title, run through OpenComposite → VDXR/OpenXR) **fails to launch** whenever
ReShade is present, in either injection mode:
- XR-layer mode → dies right after `xrCreateSession`, last log line `VR_GetGenericInterface(IVRCompositor_029)`
- DX11/dxgi mode → gets to `Resizing runtime in VR to 7770x803`, then dies on `VR_GetGenericInterface(IVROverlay_028)`

## Root cause (verified from binaries + ReShade source)
OpenComposite re-implements OpenVR on OpenXR and **hard-aborts the process** (`OOVR_ABORT`,
"Unsupported interface") when an app requests an OpenVR interface version it doesn't implement.

| Component | Versions |
|---|---|
| `dirtrally2.exe` requests | `IVRCompositor_022` (OC supports → baseline VR is fine) |
| OC `vrclient_x64.dll` implements | `IVRCompositor` ≤ **028**, `IVROverlay` ≤ **027** |
| ReShade 6.7.3 probes | `IVRCompositor_029` (forced) and `IVROverlay_028` (VR menu) |

Both ReShade probes are exactly one version above what OC has → abort. Updating OC does **not** help:
OC bundles the OpenVR 2.5.1 headers (which define 029) since May 2024 but intentionally caps the
compositor at 028. `stopOnSoftAbort` is already `false` by default and doesn't apply (it's a hard abort).

This user's layout: game-folder `openvr_api.dll` is the **genuine Valve loader** (no `HmdSystemFactory`
export); OC is the **runtime** — `vrclient_x64.dll` from `C:\Opencomposite\Runtime\bin`, registered via
`%LOCALAPPDATA%\openvr\openvrpaths.vrpath`. So OC-detection that only checks `openvr_api.dll` (as the
original code did) does NOT fire here — that was the latent bug.

## The fix (in `F:\AI-Projects\ReshadeAI\reshade\source`)
Added `reshade_is_opencomposite()` (in `openvr/openvr.cpp`):
1. `openvr_api.dll` exports `HmdSystemFactory` (OC-as-game-dll case), **or**
2. a loaded `vrclient_x64.dll` whose lower-cased path contains `opencomposite`/`openovr` (OC-as-runtime).

Then:
- `openvr/openvr.cpp` → `check_and_init_openvr_hooks()` returns early under OC (skips the forced
  `IVRCompositor_029` probe). The Submit hook is still installed from the app's own (supported)
  `IVRCompositor_022` request in `VR_GetGenericInterface`. Fixes XR-layer mode.
- `runtime_gui_vr.cpp` → `init_gui_vr()` returns early under OC (skips the OpenVR dashboard overlay /
  `IVROverlay_028`). The in-HMD menu uses the OpenXR quad path instead. Fixes DX11 mode.

Also added `#include <cwchar>` to openvr.cpp (for `wcsstr`).

## Proven working (2026-06-29, ReShade.log)
`xrCreateSession` ✓ · `Creating OpenXR overlay (500x500)` ✓ · `ReShadeVRPreview` window ✓ ·
game `IVRCompositor_022` passes through (no 029) ✓ · `Resizing runtime in VR to 7770x803` ✓ ·
`Skipping OpenVR dashboard overlay because OpenComposite is active` ✓ · process stable 30s+ (was instant abort).

## Limitation / future hardening
OC-as-runtime detection relies on the install path containing `opencomposite`/`openovr`. If OC is
installed elsewhere, add a metadata check (OC's `vrclient_x64.dll` has empty/non-Valve version info,
vs SteamVR's "Valve Corporation"). Real SteamVR is unaffected (its overlay path still runs).
