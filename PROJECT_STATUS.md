# ViewLab - Project Status

**Updated:** 2026-07-01 07:30 Brisbane
**Current version:** 4.1.42
**Latest installer:** `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.42.msi`
**Source state:** builds clean (x64 layer, Win32 layer, WPF UI, MSI). NOT visually confirmed in-headset.

## Read First (current reality, not the old design)

The visor mask has been redesigned twice. Ignore any older doc text about a ViewLab-owned
`XrCompositionLayerProjection` "orb" renderer — that approach was removed.

The **current native visor implementation** is a **D3D11 direct-write** into the game's own finished
eye textures, performed at **`xrReleaseSwapchainImage`** (the last point the app legitimately owns the
image, before the runtime composites it). It draws a black border outside a kidney/superellipse opening.
No extra projection layer, no reprojection, no pose/FOV/crop changes.

**As of 4.1.42 the visor has never been confirmed visible in-headset.** Do not claim it works.

### The decisive open finding (from reading ViewLab.log + registry, 2026-07-01)

The mask never drew in Pistol Whip/DiRT because **`mask_enabled=0`** everywhere:
- Global `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini` → `mask_enabled=0`
- Every per-app profile in `HKCU\Software\cooooked\xr-viewlab\Apps\<exe>` → `mask_enabled=0`
- Pistol Whip live DIAG line: `xrEndFrame hook called (... maskEnabled=0 ...)`

The D3D11 draw is correctly gated on `maskEnabled`, so with it off nothing draws — by design. **The UI
"enable visor mask" toggle is not persisting / not reaching the config the DLL reads.** That is the #1
functional bug to chase once drawing is proven.

## Where We Are (4.1.39 → 4.1.42)

- 4.1.39: Ripped out the projection-layer orb renderer. Added native D3D11 direct-write into eye
  textures + swapchain tracking hooks (`xrCreateSwapchain`/`EnumerateSwapchainImages`/`AcquireSwapchainImage`/
  `DestroySwapchain`). Restored ProfileWindow bean editor + interactive sliders.
- 4.1.40: UI fixes (verified on desktop):
  - Main window: removed the large blank gap below VIEWLAB ENABLED. Cause was the tall RowSpan side
    panels (cols 2/4) injecting overflow into col 0's empty grid rows. Fix: col 0 is now a single
    top-aligned `LeftColumnPanel` StackPanel mirroring cols 2/4. (`MainWindow.xaml` + `UpdateResponsiveLayout`)
  - App Profile popup: wrapped content in a vertical `ScrollViewer`, bean editor in the visual tree,
    sliders interactive when "Use global" is unchecked, opens without crashing.
  - Added typeless-safe RTV format (use app-requested swapchain format mapped to non-SRGB).
- 4.1.41: **Removed the gate** that disabled D3D11 when the game called `xrGetVisibilityMaskKHR` (this was
  why Pistol Whip/Unity never drew). **Moved the draw to `xrReleaseSwapchainImage`** (lifecycle-correct).
  Per-eye layout (imageRect + array slice) is captured in `xrEndFrame` and applied at the next frame's
  release (layout is stable frame-to-frame). Made the visibility-mask mesh-reshape **optional**
  (`visibility_mask_visor`, default off); it never suppresses the D3D11 path. Outer-edge filtering kept.
- 4.1.42: Added a **DEBUG head-locked blue test quad** (`test_quad`, default off) — our OWN
  `XrCompositionLayerQuad` in a `VIEW` reference space at position (0,0,−1), 0.4 m square, cleared solid
  blue. Independent of the game textures and of `mask_enabled`. This is the "can we draw ANYTHING in VR"
  probe. `test_quad=1` is currently set in the live ini for the next test.

## Immediate Next Work (in order)

1. **Test the blue quad** (4.1.42, `test_quad=1`) in Pistol Whip / DiRT.
   - Blue box visible 1 m in front, head-locked → layer submission works. The texture-write path is then
     the thing to fix/replace (likely rebuild the mask as a layer).
   - No blue box → layer submission itself is not reaching the compositor in the VDXR/OpenComposite path.
     That is the real root cause; investigate before any more mask work.
   - Either way, cross-check `ViewLab.log` for `test quad DIAG: initialized` and
     `test quad: submitting head-locked blue quad layer`.
2. **Fix `mask_enabled` persistence.** UI "enable visor mask" must land in the config the DLL reads:
   global `mask_enabled` in the ini and/or per-app `mask_enabled` in the registry. All are currently 0.
3. Decide the final mask mechanism based on (1), then confirm the kidney mask in-headset before any
   "it works" claim.

## How To Build (manual, avoids build.ps1 double version-bump)

MSBuild used: `C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe`

```
# x64 layer
MSBuild XRViewLabLayer.vcxproj /p:Configuration=Release /p:Platform=x64 /m
# Win32 layer (32-bit games)
MSBuild XRViewLabLayer.vcxproj /p:Configuration=Release /p:Platform=Win32 /m
# WPF UI (self-contained single file)
dotnet publish xr-viewlab.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
# Installer
MSBuild Installer\Installer.wixproj /p:Configuration=Release /p:Platform=x64 /m
```

Version lives in `Properties\AssemblyInfo.cs` and `Installer\Product.wxs` (bump both; bump per build so
the MSI upgrades over the installed one — equal versions do not trigger MajorUpgrade).

## Key Paths

- Log: `%LOCALAPPDATA%\XR ViewLab\Logs\ViewLab.log` (human) and `ViewLab.verbose.log` (per-frame).
- Global config: `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini` `[Settings]`.
- Per-app profiles: `HKCU\Software\cooooked\xr-viewlab\Apps\<exe-name>` (registry).
- Runtime source: `dllmain.cpp`. UI: `XRViewLab.UI\*.cs`, `MainWindow.xaml`, `ProfileWindow.xaml`.

## Important Runtime Symbols (dllmain.cpp)

- `DrawVisorBorderToTexture(...)` — pure D3D11 border draw (no lookup/lock); called from the release hook.
- `XRViewLab_xrReleaseSwapchainImage` — draws the visor border, then forwards release. **Primary path.**
- `XRViewLab_xrEndFrame` — captures per-eye layout into `TrackedSwapchain.eyeViews`; appends the test quad.
- `BuildVisorBorderVerts` / `ApplyVisorMask` — shared superellipse math (matches `BeanMaskEditor`).
- `InitTestQuad` / `RenderTestQuadLayer` — debug blue quad.
- Config flags: `mask_enabled`, `visibility_mask_visor` (default off), `test_quad` (default off).
- DIAG one-shot log flags: `g_diag*` — each stage logs once per session (success or failure).

## Safety Rules

- Back up source/diff before significant changes; the user wants source-level restore points.
- Do not spam VR game launches. Prefer reading `ViewLab.log` over relaunching.
- `XR_ERROR_FORM_FACTOR_UNAVAILABLE` = headset not streaming; stop and report.
- Do not claim the mask works from logs or a clean build. In-headset confirmation only.
- Every render/mask feature must be toggleable and fail back to the plain crop path.
- Do not resurrect: the projection-layer orb renderer; the hidden-mesh visor as the main feature;
  visibility-mask dependency as the primary path; preview-only UI controls; universal-support claims.

## Product Direction

ViewLab is a practical VR tuning tool for sim racers and VR players:
1. Pick a game. 2. Set generous render crop so LOD/culling stay sane. 3. Apply a separate visor mask for
comfort/performance/immersion. 4. Save per game. 5. Optionally use ReShade Remote.
UI stays plain, fast, reversible.
