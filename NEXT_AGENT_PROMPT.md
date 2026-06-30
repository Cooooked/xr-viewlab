# Next Agent Prompt - ViewLab OpenXR D3D11 Visor Renderer

You are continuing ViewLab development in `F:\AI-Projects\ViewLab`.

Start by reading, in order:

1. `F:\AI-Projects\PROJECTS.md`
2. `F:\AI-Projects\ViewLab\HANDOFF.md`
3. `F:\AI-Projects\ViewLab\PROJECT_STATUS.md`
4. `F:\AI-Projects\ViewLab\FEATURES.md`

## Current Task

Continue the first real visor-mask implementation slice:

> OpenXR D3D11 only, ViewLab-owned rounded visor mask rendered through `xrEndFrame`.

Acceptance test:

> In an OpenXR D3D11 game, with no `xrGetVisibilityMaskKHR` dependency, ViewLab can draw its own
> rounded visor mask through `xrEndFrame`.

## Hard Rules

- Do not claim universal support yet.
- Do not re-enable the old hidden-mesh visor path as the main implementation.
- Keep old `xrGetVisibilityMaskKHR` visor masking inactive.
- UI controls must only exist if they affect actual in-HMD output.
- Logs must clearly say whether the OpenXR D3D11 visor renderer is active, unavailable, or failed.
- Do not spam VR game launches.
- Check headset/runtime state before launching Pistol Whip or DiRT.
- If `XR_ERROR_FORM_FACTOR_UNAVAILABLE` appears, stop and tell the user the headset is not streaming.
- Preserve render-scale high precision and crop thousandths.

## Current Dirty Source State

Partial OpenXR D3D11 renderer work has already been applied to:

- `pch.h`
- `dllmain.cpp`

The current code may not compile. Run:

```powershell
& 'F:\AI-Projects\ViewLab\build.ps1'
```

Then fix the OpenXR layer compile/link errors before doing UI work.

Known immediate follow-up:

- `ResolveVisorOpenXrFunctions(instance)` exists and should be called from `XRViewLab_xrCreateSession`.
- The previous `std::min/std::max` macro compile failure in `FillVisorPixels` has already been patched.
- If D3D11 linker errors appear, add `d3d11.lib` and `dxgi.lib` to `XRViewLabLayer.vcxproj`.

## Current Goal List

1. Emergency handoff: update project files with exhaustive current-session state for Codex/Claude takeover.
2. Finish OpenXR D3D11 `xrEndFrame` visor renderer compile path.
3. Keep old `xrGetVisibilityMaskKHR` visor/hidden-mesh renderer inactive.
4. Wire shared rounded visor model settings: enabled, size, width, height, curve, X/Y offset.
5. Expose honest Render Options UI controls only for real renderer-backed visor settings.
6. Make desktop preview use the same rounded aperture math as the runtime texture generator.
7. Preserve crop thousandths and render-scale double precision through global/app profile save/load/sync.
8. Build WPF app, x64 layer, Win32 layer, and MSI cleanly.
9. Install/stage the new build only after layer compile succeeds.
10. Verify logs show OpenXR D3D11 visor renderer active/unavailable/failed reason clearly.
11. Check headset/runtime state before any VR game launch.
12. Test Pistol Whip cautiously for `xrEndFrame` visor mask without `xrGetVisibilityMaskKHR` dependency.
13. If Pistol Whip passes, test DiRT Rally 2 with generous crop and rounded visor mask.
14. Document unsupported backend behaviour for OpenVR/OpenComposite/Oculus without fake UI claims.
15. After OpenXR D3D11 slice is stable, plan next backend: OpenVR/SteamVR compositor overlay path.

## Rollback Point

Before the OpenXR D3D11 renderer edits, a source diff backup was written here:

`F:\AI-Projects\ViewLab\backups\source-diff-before-openxr-visor-renderer-20260630-220432.patch`

Use it only if the current partial renderer work needs to be abandoned.
