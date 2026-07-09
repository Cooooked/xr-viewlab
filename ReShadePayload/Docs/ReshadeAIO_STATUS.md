# Status & Next Steps

## Works now (2026-06-29)
- **DiRT Rally 2.0** launches with patched ReShade loaded via the OpenXR layer under OpenComposite.
  Quad menu (Home), desktop mirror window, and 7770×803 VR effect runtime all init; no abort.
- Patched `ReShade64.dll` (build `6.7.3.1`, from `F:\AI-Projects\ReshadeAI\reshade`) deployed to
  `C:\ProgramData\ReShade\ReShade64.dll`. Stock backed up as `ReShade64.dll.stock-6.7.3.2150.bak`.
- Game folder `D:\VR Games\DiRT Rally 2.0 - RESHADE working`: `dxgi.dll` stashed to `dxgi.dll.bak`
  (XR-layer mode only). `reshade-shaders` present. Configs: `ReShade.ini`, `ReShadeVR.ini`,
  `ReShadeVR2.ini`. Global quad transform created at `C:\ProgramData\ReShade\openxr_quad_transform.ini`.

## Immediate next
1. **Visible effects:** `ReShadePreset.ini` is empty → no techniques enabled, so nothing looks
   different yet. Enable an effect in `ReShadeVR.ini` (the VR runtime) or via the in-HMD Home menu,
   then confirm in headset. This is the only thing between "loads" and "looks different."
2. **HMD confirmation:** user to verify quad menu position/colors and effect application in-headset
   (cannot be verified from logs alone).

## Control app (2026-06-29 later)
- **XR ViewLab** (`F:\AI-Projects\ViewLab`) is the single control app. It already drives the DLL via
  `Local\ReShadeXRControl`: Gameplay/Tuning, Show menu/overlay, Borderless, Always-on-top, and a
  working "VR Quad ▾" popup (8-dir position pad + dist/width/height/scale/rotation/alpha sliders +
  Reset + Save default). Builds clean. These controls only do anything now because the DLL finally
  loads in DiRT (the OpenComposite fix) — before that they were inert.
- `F:\AI-Projects\ReshadeAIORemote` was a standalone stepping-stone (red ViewLab-themed panel: mode,
  menu, borderless/topmost, Reposition/Transform button popups). **Superseded by ViewLab → deprecated.**
- PARKED: `menu_source` desktop-mirror-as-VR-quad capture. Needs a new shared field on both DLL+app,
  cross-device (shared-resource) copy of the desktop runtime backbuffer into the quad swapchain +
  scaling, and in-HMD iteration. Safe default would stay VR menu so DiRT never regresses.

## Then
3. Onboard the **next VR game**: `Deploy-ReShade.ps1` already covers it globally; run
   `Add-Game.ps1 -GameDir "<next game>"`, then `Test-Game.ps1 -Exe "<exe>"`. Watch for any NEW
   unsupported-interface aborts (different games may probe different versions) and extend the skip
   logic if needed.
4. Package a true AIO: portable payload (patched DLL + XR json + shaders + ini templates) + one
   installer that deploys the global layer and registers it. ViewLab integration for Gameplay/Tuning
   runtime-sync (see ReshadeAI notes) is the richer end-state.

## OPEN BUGS — in-HMD VR ReShade menu (DiRT, 2026-06-30)

Reported by user testing DiRT with the custom ReShade VR quad menu (Home key). These are
**ReShade-source** issues (`F:\AI-Projects\ReshadeAI\reshade`), not the ViewLab layer:

1. **Header tabs unclickable in the VR quad menu.** Can't select the menu's top tabs (Home/Settings/
   Statistics/Add-ons etc.) via the in-HMD quad — so can't reach the technique/effect selector there.
   Likely the OpenXR-quad cursor/hit-testing maps clicks to ImGui incorrectly for the tab bar region
   (cursor offset/scale, or input not routed while a different runtime is focused). Investigate the
   quad cursor→ImGui IO mapping in `runtime_gui_vr.cpp` / the quad input path.
2. **VR runtime compiles ALL ~473 effects instead of only enabled ones.** The VR effect runtime is not
   honoring the enabled-technique gating: the desktop runtime in **Gameplay mode** loads none, and in
   **Tuning mode** loads only enabled effects, but the **VR runtime loads everything** → massive
   compile/runtime cost. The VR runtime's preset/technique sync is out of step with the desktop one.
   Make the VR runtime respect the same enabled-technique list (and Gameplay/Tuning gating) as desktop.
3. **VR menu is very laggy.** Almost certainly a symptom of #2 (473 effects compiling/rendering) plus
   per-frame menu/quad overhead. Re-measure after fixing #2; then profile the quad render/preview copy.

NOTE (normal, not a bug): DiRT shows a **crash-reporter popup on Alt+F4** exit — expected, ignore.

## BROKEN — Assetto Corsa won't launch (pre-existing, 2026-06-30)

Assetto Corsa is now erroring on launch. User believes a PRIOR session's change to **OpenComposite**
or the **ReShade payload** broke it (predates the 2026-06-30 ViewLab changes). **Deprioritized** — do
NOT chase it yet; focus DiRT. When revisited: diff OpenComposite config + the ReShade payload/ini
against a known-good state; Assetto is also the one game affected by VD's "Use FOV Stencil" (via CSP).

## Iteration loop (how to test)
`scripts\Test-Game.ps1 -Exe "<exe>" -Seconds 30` → launch, wait, kill, print log markers
(`xrCreateSession`, overlay, VR resize, and any `OOVR`/`IVR*_0NN`/abort). Rebuild+redeploy with
`scripts\Deploy-ReShade.ps1` after any source change. **Always kill the game before redeploying**
(the layer DLL is locked while loaded).
