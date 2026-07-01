# ViewLab - Project Status

**Updated:** 2026-07-02 Brisbane
**Current version:** 4.1.55
**Latest installer:** `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.55.msi`
**Source state:** builds clean (x64 layer, Win32 layer, WPF UI, MSI).

## Non-Negotiable Build / Reply Rule

After any implementation change, run the full installer build with `.\build.ps1` unless the user
explicitly says not to. The final reply must include the full runnable installer path, including the
MSI filename, in a plain text block suitable for Windows Run. Example:

```text
F:\AI-Projects\ViewLab\dist\ViewLab-4.1.55.msi
```

Do not give only the folder path. The user expects the exact installer target every time.

## Read First — the breakthrough and the root cause

## Current 4.1.55 State

- Vertical/horizontal tangent crop is preserved. `xrLocateViews` still applies ViewLab FOV cropping and
  `xrEnumerateViewConfigurationViews` still reduces recommended render size.
- Release-time D3D11 visor/edge drawing caches runtime swapchain RTVs per tracked image/slice instead of
  creating render-target views for every eye draw.
- High-segment visor geometry builders use fixed stack arrays for curve points instead of per-draw heap
  vectors.
- `lod_popin_fix=1` is diagnostic-only in this build: it logs that the unsafe full-FOV path is disabled
  and keeps the normal crop active, avoiding the stretch bug.
- `edge_smear_fix=1` now draws small black guard bands into submitted projection textures. It no longer
  insets submitted `imageRect` metadata.
- Technique A is a real OpenXR composition-layer path again: a head-locked alpha quad with the same
  open-inner left/right visor artwork as C, submitted as BOTH-eye for VDXR compatibility.
- Technique B is narrowed to D3D11 colour, non-MSAA swapchains only. Unsupported swapchains are bypassed
  instead of intercepted, to reduce the previous full-black failure.
- Technique C draws at `xrReleaseSwapchainImage` and also does a late draw at `xrEndFrame` to catch
  OpenComposite/OpenVR timing paths such as DiRT. It uses higher-segment open-inner geometry and adds a
  projected partner-eye boundary only when aggressive horizontal crop makes the other eye's cropped edge
  visible in the current eye.
- The main UI now exposes only Size, Width, Height, and Curve for the visor. X/Y position sliders are
  removed; stale global/per-app X/Y bias values are ignored by the DLL.
- When native visor A/B/C is active, the legacy OpenXR visibility-mask reshaper/filter is skipped so it
  cannot create a second straight/closed boundary beside the visor.

## Historical Breakthrough

Two decisive things were learned on 2026-07-01, from the user's in-headset test + `ViewLab.log`:

1. **Composition-layer submission WORKS.** A debug head-locked blue quad (our own
   `XrCompositionLayerQuad`, BOTH-eye) rendered in-headset in Pistol Whip via VDXR. So drawing our own
   layer on top of the game is viable. This is the foundation for the visor.
2. **The "no mask for 3 days" root cause: `visorSize` resolved to 1.0.** At size 1.0 the kidney opening
   fills the whole view, so the "black outside the opening" is zero pixels wide — an invisible mask. Both
   techniques were faithfully drawing a zero-width border. Cause: `mask_size` got wiped from the ini by a
   UI rewrite, and the DLL's fallback for a missing `mask_size` computed to 1.0 for a cropped view. Fixed
   in 4.1.46: fallback is now 0.82 (a visible opening), so a missing size can never disable the mask again.

## Current visor architecture (what's in the code now)

Current as of 4.1.55: `visor_technique` has four UI choices: Quad (A), Intercept (B), Direct (C), and
Off. A is an OpenXR quad overlay, B is app-facing swapchain interception/composite, and C is direct write.
All three use the same open-inner-eye visor shape and the same Size/Width/Height/Curve sliders.

Enable is **global-only** (`mask_enabled`); per-app profiles tune shape, not enable. Mechanism is chosen
by `visor_technique` (config + UI radio: Quad (A) / Intercept (B) / Direct (C) / Off), gated by
`mask_enabled`.

Authoritative 4.1.55 detail: A is a BOTH-eye OpenXR quad overlay using the open-inner left/right visor
artwork; B is experimental D3D11 colour non-MSAA swapchain interception; C draws at release and again at
late `xrEndFrame` for OpenComposite timing. Treat any older A/C-only notes below as historical.

- **Technique A — quad overlay (PRIMARY).** Renders the kidney alpha-cutout (black ring, transparent
  hole) into our own RGBA swapchain and submits it as a **single BOTH-eye** head-locked
  `XrCompositionLayerQuad`, sized to the FOV. BOTH-eye is deliberate — the earlier per-eye LEFT/RIGHT
  quads were NOT rendering on VDXR, while the BOTH-eye blue test quad did. This is the OpenKneeboard-style
  overlay and the path most likely to work on streaming runtimes.
- **Technique C — direct write (SECONDARY/unreliable on VDXR).** Draws the border into the game's eye
  texture at `xrReleaseSwapchainImage`. It logs `OK draw executed` (it IS drawing) but does not appear on
  VDXR — a streaming runtime's encoder appears to read the image separately. Added a `context->Flush()`
  after the draw as best-effort. Keep it for native runtimes; do not rely on it for VDXR/streaming.

The debug test quad has been **removed** (it proved layer submission; job done).

## Current Experimental Options

Authoritative 4.1.55 detail: edge smear is now real black guard-band drawing into projection textures,
not `imageRect` inset metadata. The LOD pop-in toggle is diagnostic-only and preserves normal crop.

- **Edge-smear fix** (`edge_smear_fix`, default off): UI checkbox under the Combined render-height line.
  The layer draws black guard pixels into projection texture edges at release/late-end-frame.
- **LOD pop-in fix** (`lod_popin_fix`, default off): UI checkbox under the Combined render-height line.
  Diagnostic-only in 4.1.55; normal ViewLab FOV crop remains active.

## Status / what needs confirming

- **Awaiting in-headset confirmation that technique A now shows a black vignette with a rounded hole**
  (with the size=1.0 bug fixed and A reworked to BOTH-eye). This is the single open question.
- Technique C is expected NOT to show on VDXR; that's understood, not a regression.

## Historical next work note (pre-4.1.54)

This section predates 4.1.54. Edge smear is now guard-band drawing, and LOD pop-in is diagnostic-only;
do not restore imageRect inset or full-FOV stretch behaviour.

1. Confirm A shows in-headset (Pistol Whip, then DiRT). Log line: `visor A: submitting BOTH-eye quad ...`.
2. Once A is confirmed, add per-eye opening for a correct nose gap (two quads or offset), tuned by
   visorOffsetX per eye.
3. **Experimental toggle — edge-smear fix** (user-requested, independent of the mask): inset the submitted
   projection `imageRect` a few px in `xrEndFrame` so the runtime stops sampling/smearing the outermost
   rendered texels. The smear is the crop's reprojection artifact (reduced FOV → runtime reprojects →
   clamps past the rendered edge); scales with tangent, not resolution.
4. **Experimental toggle — LOD pop-in fix** (user-requested): feed the game the full (uncropped) FOV for
   culling/LOD in `xrLocateViews` while keeping the reduced recommended resolution, so DiRT stops
   undrawing trees/LODs when looking up; the visor masks the lower-density periphery. Low-overhead
   (resolution stays reduced). Both experiments were deliberately deferred so they don't destabilise the
   crop path while confirming the mask.

## How To Build (manual; avoids build.ps1 double version-bump)

MSBuild: `C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe`

```
MSBuild XRViewLabLayer.vcxproj /p:Configuration=Release /p:Platform=x64 /m
MSBuild XRViewLabLayer.vcxproj /p:Configuration=Release /p:Platform=Win32 /m
dotnet publish xr-viewlab.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
MSBuild Installer\Installer.wixproj /p:Configuration=Release /p:Platform=x64 /m
```
Bump `Properties\AssemblyInfo.cs` + `Installer\Product.wxs` each build (MajorUpgrade needs a higher
version than the installed one). Copy the MSI to `dist\ViewLab-<ver>.msi`.

For normal user-requested implementation work, prefer the full build:

```
Set-ExecutionPolicy -Scope Process Bypass -Force
.\build.ps1
```

Then give the exact generated MSI path, including file name.

## Key Paths & Symbols

- Log: `%LOCALAPPDATA%\XR ViewLab\Logs\ViewLab.log`. Config: `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini`.
  Per-app: `HKCU\Software\cooooked\xr-viewlab\Apps\<exe>` (shape only; enable is global).
- `dllmain.cpp`:
  - `RenderVisorQuadLayers` / `RenderVisorCutout` / `InitVisorQuad` — technique A.
  - `CreateHeadLockedRgbaLayer` — shared VIEW-space + RGBA swapchain helper.
  - `DrawVisorBorderToTexture` + `XRViewLab_xrReleaseSwapchainImage` — technique C (+ Flush).
  - `XRViewLab_xrEndFrame` — appends the A quad; captures per-eye layout for C.
  - `BuildVisorBorderVerts` — shared superellipse border (matches `BeanMaskEditor`).
  - Config: `mask_enabled` (global), `mask_size` (fallback 0.82), `visor_technique`, `visibility_mask_visor`.
- UI: `MainWindow.xaml` Render Options has the visor enable + Technique radios + shape sliders.

## Safety Rules

- Confirm the mask in-headset; never claim it works from logs or a clean build.
- Don't destabilise the working crop path when adding the experimental toggles — gate them, default off.
- Every feature toggleable, failing back to plain crop.
- Do not re-add: the projection-orb renderer; per-app enable override; per-eye LEFT/RIGHT quads (until
  BOTH is confirmed and a nose-gap split is deliberately built); the debug test quad as a permanent draw.

## Product Direction

Practical VR tuning for sim racers / VR players: pick a game, set generous crop (LOD sanity), apply a
separate visor mask for comfort/immersion, save per game, optional ReShade Remote. Plain, fast, reversible.
