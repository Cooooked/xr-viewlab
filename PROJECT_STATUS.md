# ViewLab - Project Status

**Updated:** 2026-07-10
**Current version:** 4.1.102
**Latest installer:** `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.102.msi`
**Source state:** Fixed "Stencil outer edges only" (INI key mismatch `stencil_outer_edges_only` vs `outer_edge_visibility_mask_only`, plus the filter being skipped whenever the native visor was active). Native DLL is the v4.1.55 baseline + FreeLibrary crash fix; all inner-low/bridge native code removed pending redesign. 4.1.92-4.1.101 were unreleased debugging builds.

## Non-Negotiable Build / Reply Rule

After any implementation change, run the full installer build with `.\build.ps1` unless the user
explicitly says not to. The final reply must include the full runnable installer path, including the
MSI filename, in a plain text block suitable for Windows Run. Example:

```text
F:\AI-Projects\ViewLab\dist\ViewLab-4.1.64.msi
```

Do not give only the folder path. The user expects the exact installer target every time.

## Read First — the breakthrough and the root cause

## 4.1.65 Bug/Perf Sweep (2026-07-08)

- Fixed a live UI regression of the 4.1.46 "invisible mask" bug: the visor opening was being
  re-derived from the hidden legacy `mask_vertical`/`mask_horizontal` boxes (clamping to `1.0` when
  those keys are missing/stale) at load and after every save. `mask_size` is now the single source
  of truth and its missing-key fallback is `0.82` in both UI and DLL.
- The "no in-HMD rounded mask" warning now fires only for the legacy `visibility_mask_visor` path
  when `xrGetVisibilityMaskKHR` truly never ran; it no longer false-alarms for Direct C sessions.
- The DLL no longer reads dead legacy keys (`mask_vertical`/`mask_horizontal`/`mask_rounded`/
  `mask_offset_y`/`mask_*_bias`/`mask_*_curve`, ini and per-app registry). Fossil `reshade_*` ini
  keys and unused UI constants were removed; `verbose_logging=0` added to the default ini.
- Hot-path work: release-time Direct C draw uses fixed stack buffers (no per-frame vector copies)
  and one swapchain-mutex acquisition instead of two; the late-draw fallback and per-frame flag
  reset share one locked pass per `xrEndFrame`; config reload resolves the ini path once.
- Crop math, edge-smear mitigations, and the disabled full-FOV LOD path are untouched.
  `Tests/Verify-ViewLabContracts.ps1` passes; all builds clean.

## Current 4.1.64 State

- Vertical/horizontal tangent crop is preserved. `xrLocateViews` still applies ViewLab FOV cropping and
  `xrEnumerateViewConfigurationViews` still reduces recommended render size.
- The 4.1.64 audit artifact is `AUDIT_2026-07-08.md`. It lists the feature matrix, edge-smear fixes,
  headset test checklist, known limits, and optimization/future-work queue.
- Release-time D3D11 visor/edge drawing caches runtime swapchain RTVs per tracked image/slice instead of
  creating render-target views for every eye draw.
- High-segment visor geometry builders use fixed stack arrays for curve points instead of per-draw heap
  vectors.
- `lod_popin_fix=1` is diagnostic-only in this build: it logs that the unsafe full-FOV path is disabled
  and keeps the normal crop active, avoiding the stretch bug.
- `edge_smear_fix=1` now applies four gated compositor-contract mitigations: submitted projection-layer
  FOV is matched to ViewLab's cropped `xrLocateViews` FOV, invalid/out-of-bounds submitted
  `subImage.imageRect` values are clamped to tracked swapchain bounds, recommended image sizes are aligned
  to 4-pixel boundaries where possible, and recommended size scaling can use real original runtime FOV
  tangent range instead of symmetric 50/50 assumptions when that FOV is known. It does not draw texture
  guard bands, blur, overscan, or black strip cover-ups.
- Edge-smear verbose diagnostics now compare original runtime FOV, `xrLocateViews` FOV, submitted
  projection FOV before/after, `subImage.imageRect`, swapchain size/format, recommended image size, crop
  settings, visibility-mask state, and visor state. Treat this as investigation support, not proof the
  artifact is solved.
- Render-option keys are aligned between UI and DLL: `crop_outer_edges_only`,
  `horizontal_visual_mask_only`, and `visual_mask_only` are runtime-consumed, with legacy Edge Masks popup
  keys still read as fallbacks. Edge-smear FOV comparison logging is verbose-only.
- Edge Masks one-sided controls are disabled until implemented. Only the two "Both sides" controls map to
  the real DLL visual-mask-only keys.
- Technique C is the only user-facing/product visor path in 4.1.64. A/B code remains in source for
  reference but is hidden from the UI and bypassed by config loading.
- Technique C draws at `xrReleaseSwapchainImage`, with a fallback late draw at `xrEndFrame` for
  OpenComposite/OpenVR timing paths such as DiRT. It uses higher-segment open-inner geometry and adds a
  projected partner-eye boundary only when aggressive horizontal crop makes the other eye's cropped edge
  visible in the current eye.
- The late Technique C `xrEndFrame` draw is fallback-only in 4.1.64: it skips swapchains already covered
  by the release-time Direct C draw in the same frame, and it reuses cached swapchain RTVs when it does
  run.
- Verbose edge-smear contract diagnostics now emit a rate-limited baseline even with `edge_smear_fix=0`,
  and include release/late-draw provenance plus render scale.
- The main UI and per-app popup expose Size, Curve, `Apex Y`, and `Inner low` for the visor. Width/height
  are neutralized to `1.0` and the visor aperture follows the crop rect; stale global/per-app width/height
  and X/Y values are ignored by the DLL.
- `Apex Y` (`mask_outer_apex_y`) moves the open-inner outer curve apex up/down. `Inner low`
  (`mask_inner_lower_y`) adds a bottom-only inner-eye stencil curve. Both controls are also draggable pins
  in the visor preview and are wired through global/per-app persistence plus Direct C native geometry.
  These are experimental and not yet in-headset confirmed.
- The visor checkbox is labelled `Visor mask`; the old A/B/Off selector is no longer user-facing. Old
  `visor_technique` values are forced to Direct C. The checkbox is the only off switch.
- Per-app visor profile reset/save no longer resurrects stale custom Size values: reset deletes
  `visor_size`, `visor_width`, and `visor_height`, while saving with `Use global visor settings` checked
  stores `visor_size=0`.
- The visor mask preview uses the normal closed bean when full stenciling is selected, and a single-eye
  open-inner preview when `Stencil outer edges only` is enabled.
- ReShade Remote is now `Advanced: ReShade Remote`. It includes a bundled custom ReShade OpenXR payload,
  an elevated install button, explicit status chips, and disables controls until the shared-memory
  heartbeat is live. Corrupted glyph labels in the popout were replaced with ASCII controls.
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

Current as of 4.1.64: `visor_technique` is forced to Direct C by the UI and DLL config loader. A/B source
paths still exist for later forensic work, but they are not user-facing and should not be debugged first.
Direct C uses the same closed-bean or open-inner shape mode shown by the preview, plus the Size/Curve,
Apex Y, and Inner low controls.

Enable is **global-only** (`mask_enabled`); per-app profiles tune shape, not enable. Mechanism is chosen
by `visor_technique` (config + UI radio: Quad (A) / Intercept (B) / Direct (C)), gated by
`mask_enabled`.

Authoritative 4.1.64 detail: C draws at release, with late `xrEndFrame` only as a fallback when release
did not cover that swapchain in the current frame.
Treat A/B notes below as historical source context unless the user explicitly asks to revive them.

- **Technique A — quad overlay (hidden in 4.1.64).** Renders the kidney alpha-cutout (black ring, transparent
  hole) into our own RGBA swapchain and submits it as a **single BOTH-eye** head-locked
  `XrCompositionLayerQuad`, sized to the FOV. BOTH-eye is deliberate — the earlier per-eye LEFT/RIGHT
  quads were NOT rendering on VDXR, while the BOTH-eye blue test quad did. This is the OpenKneeboard-style
  overlay and the path most likely to work on streaming runtimes.
- **Technique C — direct write (current product focus).** Draws the border into the game's eye texture at
  `xrReleaseSwapchainImage`; a late `xrEndFrame` fallback runs only if release did not draw that
  swapchain. The user has reported this is the only practically useful visor path so far; verify
  in-headset rather than trusting draw logs alone.

The debug test quad has been **removed** (it proved layer submission; job done).

## Current Experimental Options

Authoritative current detail: edge smear is a gated compositor-contract experiment, not texture
guard-band drawing, blur, overscan, or black strip hiding. The LOD pop-in toggle is diagnostic-only and
preserves normal crop.

- **Edge-smear fix** (`edge_smear_fix`, default off): UI checkbox under the Combined render-height line.
  The layer matches submitted projection-layer FOV to the cropped FOV returned from `xrLocateViews`,
  clamps only invalid/out-of-bounds submitted image rects to swapchain bounds, aligns recommended image
  dimensions to 4 pixels, can use original runtime FOV to weight recommended-size scaling, and skips the
  legacy outer-edge-only visibility-mask filter.
- **LOD pop-in fix** (`lod_popin_fix`, default off): UI checkbox under the Combined render-height line.
  Diagnostic-only in 4.1.64; normal ViewLab FOV crop remains active.
- **Visor Apex Y / Inner low** (`mask_outer_apex_y`, `mask_inner_lower_y`): experimental Direct C visor
  shaping controls. Built and saved globally/per-app, but not yet confirmed in headset.

## Status / what needs confirming

- **Awaiting in-headset confirmation that Technique C remains stable across target games** with Size,
  Curve, Apex Y, and Inner low controls.
- Confirm Size changes resize the visor correctly, Curve changes the visor shape correctly, Apex Y moves
  the outer-curve apex, and Inner low reintroduces only the lower inner-edge stencil.
- Edge-smear/render-stretch remains unresolved and is now the next handoff focus. The user reports
  pixel stretching/downscaling at aggressive crops across games/runtimes: horizontal crop around `0.6`
  or `0.4` makes the top/bottom edges stretch noticeably, and vertical crop around `0.20` to `0.15`
  creates a smaller but visible in-lens smear near the edges. Use verbose `edge-smear contract`
  diagnostics to compare FOV, imageRect, swapchain size, recommended size, crop settings, visibility-mask
  state, visor state, and draw provenance. Do not treat current `edge_smear_fix` behaviour as solved.

## Historical next work note (pre-4.1.54)

This section predates the current implementation. Edge smear is now a projection-FOV submission fix, and
LOD pop-in is diagnostic-only; do not restore imageRect inset, guard-band drawing, or full-FOV stretch
behaviour.

1. Confirm A shows in-headset (Pistol Whip, then DiRT). Log line: `visor A: submitting BOTH-eye quad ...`.
2. Once A is confirmed, add per-eye opening for a correct nose gap (two quads or offset), tuned by
   visorOffsetX per eye.
3. **Experimental toggle — edge-smear fix** (user-requested, independent of the mask): clamps submitted
   projection-layer FOV to ViewLab's cropped `xrLocateViews` FOV so runtime composition cannot stretch a
   cropped render across a wider submitted frustum.
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
- Current 4.1.64 correction: the UI no longer exposes Technique radios or Direct C status subtext. It
  shows the preview plus Size, Curve, Apex Y, and Inner low controls.

## Safety Rules

- Confirm the mask in-headset; never claim it works from logs or a clean build.
- Don't destabilise the working crop path when adding the experimental toggles — gate them, default off.
- Every feature toggleable, failing back to plain crop.
- Do not re-add: the projection-orb renderer; per-app enable override; per-eye LEFT/RIGHT quads (until
  BOTH is confirmed and a nose-gap split is deliberately built); the debug test quad as a permanent draw.

## Product Direction

Practical VR tuning for sim racers / VR players: pick a game, set generous crop (LOD sanity), apply a
separate visor mask for comfort/immersion, save per game, optional ReShade Remote. Plain, fast, reversible.
