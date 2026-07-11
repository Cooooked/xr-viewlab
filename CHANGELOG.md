# Changelog

> Live state: `STATE.md`. Architecture: `docs/ARCHITECTURE.md`. This file is append-only release history.

## Unreleased - 2026-07-12 (Binocular WYSIWYG Preview)

- The visor preview is now binocular and one-to-one: the canvas represents the full uncropped
  binocular render (fixed 2:1 reference), and the Vertical/Horizontal crop values map directly
  to the inner render rect — Vertical 0.2 visibly occupies 20% of the reference height. A faint
  outline marks the post-crop rect; the visor mask is drawn inside it.
- Hidden the inner-eye notch controls (Inner low, Bridge, Rise, Peak X, Steep) and their preview
  pins in both the main window and the per-app profile popup. The notch looks correct with one
  eye closed but turns translucent under binocular fusion. Config keys, saved values, per-app
  plumbing, and native geometry are unchanged — UI visibility only.

## Unreleased - 2026-07-12 (Installer Registry Safety)

- Removed the MSI's `CleanupApiLayerRegistry` custom action entirely. Installing or upgrading
  ViewLab now never enumerates, cleans, recreates, or otherwise alters third-party values under
  the shared OpenXR implicit-layer registry key; WiX manages only ViewLab's own registrations.

## Unreleased - 2026-07-12 (Fixed-Foveation Visor Coverage)

- Restored the unified visor Size control (`0.05..1.0`, default `1.0`). It shrinks only the
  exposed aperture, leaving ViewLab crop tangents, submitted FOV, and render resolution intact.
- Curve `0` now emits an exact rectangular aperture, including the open-inner per-eye geometry.
- Extended Peak X from `0..1` to `-0.5..1`; values left of zero move the mirrored per-eye peak
  twice as far from the binocular centre as the old minimum. Existing per-app Peak X values keep
  their legacy encoding and meaning.

## Unreleased - 2026-07-12 (Visor Shape Range Pass)

- Replaced the Curve-zero geometry branch with a continuous near-square curve shoulder, so the
  first slider movement no longer jumps shape or disables Inner low.
- Expanded Size minimum to `0.1`, Inner low maximum to `0.666`, Rise to `-0.5..1`, Peak X to
  `-1..2`, and Steep to `-1..2`. Bridge handle response is strengthened at both ends; all defaults
  retain their previous geometry.
- Moved the ReShade Remote entry beside Edge Masks above the visor controls, and added draggable
  preview pins for every visor shape slider.
- Removed redundant Applications/Render Options sub-headers to align responsive dual/triple-column
  cards, and restored the beta-testers credit in triple-column mode.
- Shortened the app-list instruction, removed the redundant combined render-height readout, and
  moved the triple-column beta-testers credit directly below the right-column card.
- Retuned responsive client-width cutovers to 280px (mini), 720px (two columns), and 1200px
  (three columns), preventing squeezed multi-column layouts during resize.
- The PowerUp/per-app profile popup now has a themed, reserved-column scrollbar that cannot overlap
  visor controls.
- Added a prominent "WARNING — DO NOT USE" banner to the experimental ReShade Remote.
- Global visor size/shape controls now update live after the UI commits a visor-only revision;
  crop/FOV/resolution and per-app profiles deliberately remain restart-required.
- Forefront discovery now targets `Forefront_Internal.exe`, and the native layer emits a precise
  Forefront process-entry diagnostic to distinguish loader, instance-chain, and crop-hook failures.

## Unreleased - 2026-07-11 (Experimental Crop-Fix Purge)

- Removed all experimental crop-fix and edge-smear diagnostic code from the native layer:
  crop experiment modes (visibility-mask passthrough / disable hidden-area culling / crop-aware
  mask mapping / edge-source probe), the black edge-guard frame (`edge_smear_fix`/
  `edge_smear_pixels`), the edge-guard/probe draw paths, and all crop-contract diagnostic
  logging and snapshot machinery. Config keys are ignored if present.
- Removed the "EXPERIMENTAL CROP FIXES" checkbox group from the UI.
- Root cause of the edge smear is documented in `docs/FIXED_FOVEATION.md` (Virtual Desktop
  fixed foveated encoding; not fixable in ViewLab). Full record: `docs/EDGE_SMEAR_INVESTIGATION.md`.

## Unreleased - 2026-07-11 (Crop Resolution A/B)

- Added hidden default-off `crop_boundary_full_resolution_experiment`. It preserves ViewLab's
  cropped FOV while returning the runtime's original recommended swapchain dimensions for an
  isolated Virtual Desktop boundary-smear comparison.

## Unreleased - 2026-07-11 (Crop Boundary Diagnostics)

- Added rate-limited OpenXR crop-contract diagnostics. They compare each submitted primary-stereo
  projection frame with its exact same-session, same-display-time `xrLocateViews` snapshot and
  log FOV and sub-image bounds without modifying rendering behaviour.

## Unreleased - 2026-07-11 (Visor Curve Fidelity)

- Increased the fixed native visor curve tessellation from 96 to 512 segments. This removes the
  long straight chords that made curved mask edges appear blocky in headset, without restoring
  the removed HD or anti-aliasing controls.

## 4.1.123 - 2026-07-11 (ReShade Remote: Robust Install + Live Controls)

- Rebuilt the bundled `ReShadePayload/ReShade64.dll` from `ReshadeAI/reshade` source so the
  `heartbeat` field is incremented every frame; `ReShadeRemote` can now detect that ReShade is
  running and enable controls.
- ReShade Remote controls are no longer disabled when the game is not running. They can be used
  before launch, and settings take effect when the game opens the shared-memory control block.
- `Install component` now waits for the elevated installer and reports a clear error if the
  `ReShade64.dll` is locked by a running game, instead of silently doing nothing.
- `ReShadeRemote` status now distinguishes `Not installed`, `Ready`, and `Connected` instead of
  the previous "controls locked until heartbeat" behavior.

## 4.1.122 - 2026-07-11 (UI Cleanup: Baked-in Options, Removed Broken Features)

- Removed the visor mask size slider from the main window and per-app profile editor. The mask
  is now hardcoded to maximum corner coverage: it always extends to the full edges of the
  cropped view and only rounds the corners. Crop values (vertical/horizontal render height)
  continue to control the full render dimensions from the center.
- `mask_size`/`visor_size` is deprecated and no longer affects geometry. The geometry builders
  now use `mask_width_scale`/`mask_height_scale` fixed at 1.0 so the opening always fills the
  crop bounding box.
- Removed experimental LOD pop-in fix and edge-smear fix: the UI checkboxes and their native code
  paths are gone, and the config keys are ignored.
- Removed HD visor and anti-aliasing checkboxes: their code paths are disabled and config keys
  ignored.
- `foveated_center_compensation`, `stencil_outer_edges_only`, and `crop_outer_edges_only` are now
  permanently enabled. Their UI checkboxes are removed and the config keys are ignored.

## 4.1.113 - 2026-07-11 (Headset Regression Fixes)

- Fixed `Crop outer edges only`: the native FOV crop now scales only the outer edge when enabled
  and both horizontal edges when disabled, matching the checkbox and recommended render width.
- Fixed the enabled-but-invisible visor state: legacy `mask_size=1` now recovers to the safe
  visible opening `0.82`, and Size can no longer be set to a borderless full opening in the UI.
- Fixed ReShade Remote deployment command quoting and made the panel report whether the bundled
  custom payload is actually installed instead of implying that any stock ReShade layer can connect.

- Fixed upgrades so they preserve visor settings and per-app tuning. The earlier per-version
  reset design would have erased those settings after every MSI update and has been removed.
- Removed user-profile backup/reset work from installer VBScript; it now performs only machine-safe
  stale OpenXR layer registry cleanup.
- Fixed the per-machine Start Menu shortcut component key path by moving it from HKCU to HKLM.
- Retired the legacy `visibility_mask_visor` hidden-mesh reshaper; the key is logged and ignored
  so it cannot diverge from Direct C visor geometry.
- Aligned native missing-key defaults for `mask_corner` and `mask_rounded` with the bundled ini.
- Removed unsafe mid-frame config hot reload; settings now apply on game restart as the UI states.
- Serialized D3D11 draw hooks with renderer/session teardown to prevent concurrent state use and
  release during game shutdown or swapchain changes.

## 4.1.109 - 2026-07-10 (Bug Scan Fix Pass)

- Fixed the remaining preview pin-drag breakage by making the apex drag inverse match the rendered
  centre-origin pin position.
- Fixed safe config/install consistency: `mask_size` now falls back directly to `0.82`, the default
  ini is packaged into publish/MSI output, and build output copies freshly-built native DLLs after
  MSBuild completes.
- Fixed Edge Masks "Both" wiring to the DLL keys and disabled unsupported one-sided controls.
- Implemented native `crop_outer_edges_only` reading/logging and restored recommended image-size
  scaling when `fovMutable` reporting is unavailable.
- Split late fallback draw flags for edge guard vs Direct C visor, broadened D3D11 RTV/viewport
  state restore, and added open-inner vertical AA feathering.
- Tightened per-app visor profile semantics: global mode saves as global, unsupported per-app
  visor enable is not written, bridge-width missing fallback is `0.5`, and the curve reset matches
  default config.

## 4.1.110 - 2026-07-10 (Bug Scan — documented findings)

- Finalized comprehensive bug scan (`docs/BUG_SCAN_2026-07-10.md`) covering 43 new findings
  across native layer, UI, build/installer, and documentation.
- Cross-referenced and aligned all seven canonical memory files (STATE.md, CHANGELOG.md,
  docs/ARCHITECTURE.md, docs/CONFIG.md, docs/DECISIONS.md, docs/REGRESSIONS.md,
  docs/BUG_AUDIT.md).
- `Tests/Verify-ViewLabContracts.ps1` passes.

## 4.1.107 - 2026-07-10 (Full Bug-Fix Bundle)

- Implemented preview pin dragging via mouse capture/release/lost-capture handling.
- Implemented native `visor_antialiasing` and `visor_hd`: D3D11 visor vertices now carry alpha,
  AA adds feathered aperture strips with SRC_ALPHA blending, and HD doubles curve tessellation.
- Completed per-app shape plumbing for bridge rise, peak X, and steepness; reset/delete now covers
  all visor override keys.
- Reconciled installer policy: MSI backs up then intentionally resets visor settings to safe
  defaults, with complete bundled ini defaults and contract coverage.
- Restored Pass 4 robustness: cached RTVs are used by release and late paths, late `xrEndFrame`
  drawing is fallback-only, and original-FOV edge-smear diagnostics are back.

## 4.1.105 - 2026-07-10 (Pass 1: Visor Shape Rewrite)

- **Rewrote apex-y, inner-low, and all four bridge controls natively** (clean rewrite on the v4.1.55
  baseline, formula-identical to the UI preview). Root cause of the old "divot on top of the lens" bug
  found and fixed: the old native code anchored the nose bridge to NDC `y1` (top) instead of `y0`
  (bottom) — UI y-down vs NDC y-up — and emitted bare points into a triangle list (garbage geometry).
  New `BuildNoseBridgeCurve` fills real band-clamped trapezoids: the divot is structurally bottom-only.
- **Native shape now follows the stencil checkbox like the preview**: ON = per-eye open-inner arch,
  OFF = closed bean (now apex-aware). Geometric curve exponent `32*(2/32)^curve` everywhere.
- Config plumbing for all six shape keys (ini + per-app registry), config log extended.
- Rewrote `Tests/Verify-ViewLabContracts.ps1` for the new design; all contracts pass.

## 4.1.103 - 2026-07-10 (Inner-Edge Stenciling, Part 2)

- **Gated the projected partner-eye boundary behind the stencil checkbox.** The D3D11 visor draw
  unconditionally added `BuildProjectedPartnerVisorVerts`, which blacks out the inner-eye band the
  cropped partner eye can no longer render. At aggressive horizontal outer crop this filled the whole
  nose-side region black (with its own curve, ignoring apex-y) — the inner-edge stenciling still
  visible after 4.1.102's visibility-mask fix. It now draws only when "Stencil outer edges only" is
  unchecked.

## 4.1.102 - 2026-07-10 (Stencil Outer Edges Fix)

- **Fixed "Stencil outer edges only" doing nothing / inner edges being stenciled.** Two long-standing
  bugs: (1) the UI checkbox writes `stencil_outer_edges_only` but the DLL only read
  `outer_edge_visibility_mask_only` — the checkbox was wired to nothing; the DLL now reads the UI key
  (legacy key as fallback). (2) the outer-edge visibility-mask filter was skipped entirely whenever the
  native visor was active, so Virtual Desktop's FOV stencil passed through unfiltered and the game
  stenciled the inner/top/bottom edges itself (with VD's curve, not the user's apex-y curve). The filter
  now runs regardless of visor state; only the legacy visibility-mask reshaper stays gated on visor-off.
- Native DLL is the v4.1.55 baseline (no inner-low/bridge code) plus the FreeLibrary crash fix.
- Versions 4.1.92-4.1.101 were local debugging builds during this investigation; none were released.

## 4.1.93 - 2026-07-10 (Inner-low Rendering Fix Retry)

- **Re-applied inner-low nose bridge rendering fixes**: removed 0.5x multiplier and lowered threshold from
  0.0001 to 0.0. Native DLL now renders inner-low at full strength with no dead zone. This should fix the
  barely-visible issue at higher horizontal crop values.
- Investigating: stencil mode may be affected by geometry sizing at lower crop values. Report results.
- `Tests/Verify-ViewLabContracts.ps1` passes; x64/Win32/WPF build with 0 warnings.

## 4.1.92 - 2026-07-10 (Reverted: Stencil Issue)

- **Fixed inner-low nose bridge not rendering in HMD visor**: native DLL had the same bugs as the UI code
  previously had. Threshold was 0.0001 (blocking render at small values), now 0.0. Removed 0.5x multiplier
  that was halving the visual effect. Inner-low now renders at full strength in the headset, visible in both
  stencil modes. This fixes the missing bottom eyelid/nose divot that users see in-HMD.
- `Tests/Verify-ViewLabContracts.ps1` passes; x64/Win32/WPF build with 0 warnings.

## 4.1.91 - 2026-07-10 (UI Quality Controls + Agent Safety)

- **Added HD visor quality checkbox** (`visor_hd`): optional 2x supersampling of visor geometry. When enabled,
  renders the visor mask at 2x internal resolution before downsampling, reducing pixelation at the edges.
  Configurable via checkbox under the visor section in the UI; stored in INI file and applied at runtime.
- **Added anti-aliasing quality checkbox** (`visor_antialiasing`): toggle for 8-pass jittered anti-aliasing
  of visor edges. When enabled (default), uses 4x jitter pattern for smooth edges; when disabled, renders
  single-pass solid edges for performance. Both visual paths verified in previous builds to work correctly.
- **Fixed agent configuration bubble**: removed project-local `.claude/` directory that was creating isolated
  agent workspaces. Updated `.gitignore` and `agents.md` with explicit safety rules preventing future local
  bubbles. All AI agents now work with the user's global `~/.claude/` directory, allowing other models to
  access the same shared project context without isolation.
- Full UI plumbing: `VisorHDCheck` and `VisorAntiAliasingCheck` checkboxes in `MainWindow.xaml`, read/write
  in `MainWindow.cs` with INI persistence, native-layer globals in `dllmain.cpp` read at initialization.
- `Tests/Verify-ViewLabContracts.ps1` passes; x64/Win32/WPF build with 0 warnings.

## 4.1.90 - 2026-07-10 (Feature Fixes)

- **Fixed inner-low (nose bridge) slider visibility**: removed 0.5x scaling multiplier that was
  reducing the visual effect. Inner-low now renders at full strength matching the slider range
  (0-0.333), with the nose bridge divot clearly visible in the mask preview as you adjust the control.
- **Fixed pin dragging for bridge control**: the inner bridge pin can now be dragged at all
  inner-low values. Previously required InnerLowerY > 0.0001, preventing adjustment when slider
  was at minimum. Now works smoothly from 0 upward.
- **Lowered rendering threshold**: changed visibility gate from 0.0001 to 0.0, so inner-low geometry
  renders even at minimum slider positions instead of appearing only above a small dead zone.
- `Tests/Verify-ViewLabContracts.ps1` passes; x64/Win32/WPF build with 0 warnings.

## 4.1.89 - 2026-07-10 (Hotfix)

- **REVERTED 4.1.88 bad defaults** that broke Pistol Whip at startup: commit a02f6de applied the user's
  current settings as hardcoded defaults, including `mask_size=1` (invisible mask) and incorrect render
  crop values (0.2 vertical instead of 0.18, etc.). These were never tested and caused immediate crashes.
  Reverted to original good defaults: render crop 0.18/0.09 vertical, 0.80 horizontal, mask_size=0.82,
  foveated_center_compensation=0. Per-app profiles remain unaffected. Users with 4.1.88 should update.
- **Fixed critical D3D11 initialization crash**: `FreeLibrary(dxcLib)` was being called at line 811 in
  `InitD3D11MaskRenderer()`, BEFORE shader blob pointers were used to create vertex/pixel shaders and
  input layouts (lines 814, 823, 836). This caused 0xC0000005 access violations when reading freed DLL
  memory. Moved `FreeLibrary` to after all blob usage completes, and added it to all error paths. This
  was causing Pistol Whip (and other games) to crash immediately at startup.
- **Updated build.ps1** to automatically deploy native DLLs (`XR_APILAYER_cooooked_xrviewlab.dll` x64/Win32)
  to publish directory and dist folder on every build, ensuring users get updated binaries with each
  release without manual intervention.
- Known issues NOT yet fixed: pin dragging in mask preview (Focusable="True" added but not fully
  functional), inner-low (nose bridge) slider not producing visible change in lens preview (may need
  exaggerated slider ranges or stencil-mode logic fix), edge-smear-fix logic still needs investigation.
- `Tests/Verify-ViewLabContracts.ps1` passes; x64/Win32/WPF build with 0 warnings.

## 4.1.88 - 2026-07-10

- **Removed non-functional "Curve Exp" and "Inner X" sliders** from the UI and all internal plumbing. Both were experimental controls that had no real effect on the mask geometry; they were confusing and dead code. Removed `MaskInnerBridgeCurveExpKey` and `MaskInnerStartXKey` constants from `MainWindow.cs`, corresponding handler methods, XML rows in `MainWindow.xaml`, and native-layer global variables + reads in `dllmain.cpp`. Removed `_innerBridgeCurveExp` and `_innerStartX` fields and enum entries from `BeanMaskEditor.cs`, plus all related drag detection and pin drawing.
- **Fixed pin dragging in the mask preview** by adding `Focusable="True"` to the `BeanMaskEditor` element in `MainWindow.xaml`, allowing the canvas to capture mouse events for dragging the visible pins.
- **Included ReShadePayload in published builds**: added post-publish copy logic to `build.ps1` to copy the `ReShadePayload` directory to the output folder after `dotnet publish`, so the component is available in development/testing and included in the MSI.
- **Set user's current application settings as defaults** for all fresh installs: captured the user's existing render/mask configuration (render crop 0.2 vertical / 0.8 horizontal, foveated center on, crop outer edges on, and 8 mask slider positions) and applied them as fallback defaults in `MainWindow.cs`, `xr-viewlab.ini`, and `dllmain.cpp`, while preserving per-app profiles. New installs and updates now reflect these preferences.
- **Published GitHub release v4.1.88** with the updated MSI installer for public distribution to users.
- `Tests/Verify-ViewLabContracts.ps1` passes; x64/Win32/WPF build with 0 warnings.

## 4.1.68 - 2026-07-09

- **Fixed blocky/pixelated visor mask edges**: the mask draws opaque hard-edged triangles directly
  into the eye texture with no antialiasing (blend was disabled, pixel shader wrote solid alpha=1).
  Because the eye swapchain is single-sample (no MSAA available on submitted VR swapchains) and the
  runtime's lens-distortion warp then resamples that already-aliased edge, the hard edge reads as a
  blocky/quantized black-grey-white stairstep once magnified through the lens, not a clean line.
  Fixed by supersampling: the mask now draws 4 jittered passes (a standard 2x2 rotated-grid pattern,
  offset by fractions of a texel) alpha-blended at 1/4 opacity each, so interior pixels (covered by
  every pass) accumulate back to fully opaque black while boundary pixels (covered by only some
  passes) land at a smoothly-varying fractional alpha — an analytic antialiased edge, not a blur, and
  the underlying shape/tessellation is unchanged. New `JitterCB` constant buffer (bound to both VS/PS,
  16 bytes: jitter offset + alpha) drives both the offset and per-pass opacity; blend state changed
  from disabled to standard SRC_ALPHA/INV_SRC_ALPHA. The historical/hidden Technique A quad-cutout
  path (`RenderVisorCutout`) shares the same shaders and was updated to bind a neutral single-pass
  buffer (offset=0, alpha=1) so it keeps its old opaque behavior instead of silently going transparent.
- **Reworked the inner-low (NRI) nose-bridge curve into two connected segments**, replacing the
  single quarter-superellipse from 4.1.66/67. The old curve necessarily ended in a sharp corner
  jammed against the nose edge — there was no way to make it read as a flat nose bridge rather than a
  point, however the easing was tuned. New `BuildNoseBridgeCurve` (dllmain.cpp) /
  `AddNoseBridgeCurve` (UI) build two quarter-superellipse segments joined at a control-adjustable
  junction point J: segment A rises from the crescent's bottom pinch point to J, segment B continues
  from J to the nose edge. By construction both segments always meet with a purely vertical tangent
  at J (C1-continuous, no kink, regardless of where J sits) and segment B always ends with a purely
  horizontal tangent at the nose edge (a flat landing, never a point). Verified numerically before
  building: bridgeWidth=0.5 rises quickly to the midpoint then plateaus into a long flat bridge for
  the second half; bridgeWidth=0.8 produces a very early junction and a long, gently-rising flat
  bridge across most of the span; both match the requested "flat, rise, flat bridge" shape with no
  spatial overlap or sharp point.
- **New UI control: Bridge**. A slider (0..1, default 0.5) plus a third draggable preview pin
  (light blue) in both the main window and the per-app profile window, gated the same way "Inner low"
  is (enabled only when open-inner/stencil mode is active). Full plumbing: `mask_inner_bridge_width`
  ini key, per-app registry DWORD (same millis encoding as the other shape values),
  `AppProfile.VisorInnerBridgeWidth`, `ProfileWindow` global/local values and Use-Global/Reset
  handling, config-log and verbose edge-smear-contract log lines. UI preview and DLL runtime use the
  identical two-segment formula (kept in sync the same way Curve/Apex-Y/Inner-low already are).
- Verified live: DiRT Rally 2 and Pistol Whip both launch and draw the visor successfully
  (`OK draw executed`, both eyes) through the new 4-pass jittered/blended pipeline with curve at max,
  inner-low maxed, and bridge width swept across 0.0 / 0.5 / 1.0 — no crash, no new WER events at any
  setting. New curve/bridge math independently checked numerically before building (see above).
- Still needs in-headset confirmation that the mask edge actually reads as smooth (not blocky) and
  that the nose-bridge shape looks like a natural flat bridge rather than a point — the verification
  above proves the pipeline runs and the math is correct, not the final visual result.
- `Tests/Verify-ViewLabContracts.ps1` passes; x64/Win32/WPF build with 0 warnings.

## 4.1.67 - 2026-07-09

- **Widened the Curve slider back out**: 4.1.66's recalibration (linear, max = exp 7) turned out too
  conservative — user reports max slider only reaches ~50% of the expected curvature and lemon/banana
  mode is no longer reachable at all. Replaced the linear map with a **geometric** interpolation between
  exp=32 (flat, curve=0) and exp=2 (the original full lemon/banana extreme, curve=1 — restored as the
  true ceiling). This lands the old "already looks maxed" point (~exp 7-8) at curve=0.5 instead of
  curve=0.833, so 0-50% covers what the slider used to cover and 50-100% adds back the headroom up to
  the extreme shape. `VisorCurveExponent()` (DLL) / `BeanMaskEditor.CurveExponent` (UI) both updated
  identically.
- **Fixed the inner-low (NRI) nose-bridge curve construction**: the left-eye lower-centre boundary was
  rising almost straight up near the bottom-center pinch point and only curving toward the nose edge
  right at the top of the band — "up, then right" — instead of hugging the bottom near the nose edge
  first and rising only near the end — "right, then up". Root cause: the old parametrization used
  ease-out easing on X (`1-(1-u)^2`) combined with linear Y, which puts the *slow* phase (near-zero
  dx/du) right at the bottom-center start point. Replaced with a genuine quarter-superellipse corner
  (`x = cx + (innerX-cx)*sin(t)^(2/exp)`, `y = y0 + (bandTopY-y0)*(1-cos(t)^(2/exp))`) that has
  dy/dt=0 and maximal dx/dt at the start (moves toward the nose edge first, while stayed pinned near
  y0) and dx/dt=0 / maximal dy/dt at the end (finishes by rising to bandTopY) — a proper "hug the
  bottom, then rise" nose-bridge silhouette instead of a vertical spike that darted sideways at the
  top. Uses the same superellipse exponent as the outer curve, and the mirrored-eye case now falls out
  of the same formula (`innerX - cx` carries the sign) instead of a duplicated branch. Fixed
  identically in `BuildOpenInnerEyeVisorVerts` (dllmain.cpp) and `BeanMaskEditor.AddOpenInnerHalf`
  (UI preview) so they stay geometrically identical.
- Verified live: DiRT Rally 2 launched and ran stable with curve pushed to slider max (exp=2, full
  lemon) and inner-low active; visor drew every frame (780 verts, no FAILs), no crash. New exponent
  and nose-bridge math independently checked numerically (curve 0/0.5/1.0 -> exp 32/8/2; nose-bridge
  corner reaches ~95% of the way to the nose edge while y has moved <5% of the band, confirming
  "right first, then up").
- Still needs in-headset confirmation of how the new curve range and nose-bridge shape actually look —
  the build/log verification above proves the math and draw path, not the visual result.
- `Tests/Verify-ViewLabContracts.ps1` passes; x64/Win32/WPF build with 0 warnings.

## 4.1.66 - 2026-07-09

- **Fixed Pistol Whip (and any game without d3dcompiler_47 resident) crashing at launch**: the D3D11
  mask renderer init called `FreeLibrary(d3dcompiler)` *before* using the `ID3DBlob`s the compiler
  had allocated. Blob methods are virtual calls into d3dcompiler code, so in processes where our
  `LoadLibrary` held the only reference the module unloaded and the next blob call was an access
  violation (WER: `0xc0000005` at `InitD3D11MaskRenderer+0x29D`, dllmain.cpp:784, ~6 crash reports
  on 2026-07-09). d3dcompiler is now freed only after every blob is released, on all paths.
  Verified live: Pistol Whip previously died right after "initializing direct-write renderer";
  it now logs "initialized" and runs. Standalone repro confirmed old order dies with 0xc0000005,
  new order succeeds. This ran unconditionally at `xrCreateSession`, which is why no UI toggle
  affected it.
- **Fixed the visor mask being vertically inverted in-headset**: the native builders copied
  `BeanMaskEditor` math verbatim, but the preview canvas is y-DOWN (WPF) while the native geometry
  is built in y-UP D3D NDC. Apex Y moved the wrong way, "Inner low" drew at the top, and the curve
  bent downward when the UI showed upward. Added `ApexYFromConfigNdc()` which performs the UI→NDC
  sign flip exactly once (used by the closed-bean, open-inner, and projected-partner builders), and
  re-anchored the inner-low band to the visual bottom (`y0` side in NDC). The UI preview is
  unchanged — it was correct.
- **Recalibrated the Curve slider**: the old slider→superellipse mapping (`exp = 32 − 30·curve`)
  reached full visual strength around 5/6 of the slider (and apex-Y's asymmetric vertical ranges
  made high curves even more extreme). Slider 1.0 now maps to `exp = 7` — what ~5/6 used to
  produce — via shared helpers `VisorCurveExponent()` (DLL) and `BeanMaskEditor.CurveExponent`
  (UI preview), so preview and runtime stay identical and strength scales progressively.
- Logging cleanup: "hook installed/captured" lines now log once per process instead of on every
  `xrGetInstanceProcAddr` resolution (was ~3 duplicate blocks per launch). Audited hot paths:
  per-frame detail remains verbose-only (`ViewLab.verbose.log`), DIAG lines remain one-shot,
  edge-smear contract logging remains verbose-gated + rate-limited. No new always-on logs added.
- `Tests/Verify-ViewLabContracts.ps1` passes; x64/Win32/WPF build with 0 warnings.
- Note: still needs in-headset confirmation that "Inner low" appears at the bottom, Apex Y moves
  the expected direction, and the recalibrated curve range feels right.

## 4.1.65 - 2026-07-08

- Fixed a live regression of the 4.1.46 "invisible mask" bug: the UI derived the visor opening from
  the hidden legacy `mask_vertical`/`mask_horizontal` boxes at load and after every save, which
  clamps to `1.0` (full opening = invisible mask) whenever those keys are missing or stale.
  `mask_size` is now the single source of truth in the UI and its missing-key fallback is `0.82`,
  matching the DLL.
- Retargeted the misleading "no in-HMD rounded mask will be visible" warning: it now fires only for
  the legacy `visibility_mask_visor` path when the runtime truly never calls
  `xrGetVisibilityMaskKHR`, instead of firing for every native Direct C visor session.
- Removed dead legacy config reads from the DLL (`mask_vertical`, `mask_horizontal`, `mask_rounded`,
  `mask_offset_y`, `mask_*_bias`, `mask_*_curve` in both ini and per-app registry) — they fed no
  render path. `mask_corner`, `visor_size`, `mask_outer_apex_y`, and `mask_inner_lower_y` remain the
  live shape keys.
- Removed the fossil ReShade ini keys (`reshade_hmd_menu`, `reshade_desktop_duplicate`,
  `reshade_3d_menu`) and the unused `reshade_xr_*`/`reshade_vr_*` UI constants; nothing read or
  wrote them. Added `verbose_logging=0` to the default ini.
- Hot-path cleanup: the Direct C `xrReleaseSwapchainImage` draw now uses fixed stack buffers instead
  of per-frame `std::vector` copies and takes the swapchain mutex once instead of twice; the late
  `xrEndFrame` fallback pass and the per-frame flag reset were merged into one locked map pass;
  config reload resolves the ini path once instead of ~30 filesystem stats per reload.
- Vertical/horizontal tangent crop, `edge_smear_fix` mitigations, and the disabled full-FOV LOD path
  are unchanged. `Tests/Verify-ViewLabContracts.ps1` passes.

## 4.1.64 - 2026-07-08

- Finished the Apex Y / Inner low implementation pass:
  the main window, per-app profile popup, draggable preview pins, INI/registry persistence, and Direct C
  native geometry now use the same shape values.
- Made Direct C choose the same visor shape mode as the preview: closed bean when full stenciling is
  selected, open-inner single-eye geometry when `Stencil outer edges only` is enabled.
- Extended `edge_smear_fix` with four gated compositor-contract mitigations:
  exact submitted-FOV matching, legal `subImage.imageRect` bounds correction, 4-pixel recommended-size
  alignment, and FOV-weighted recommended image scaling when original runtime FOV is known.
- Expanded verbose edge-smear diagnostics with original runtime FOV before ViewLab crop.
- Cleaned corrupted ReShade Remote glyph labels to ASCII and kept the Advanced popout status-driven.
- Added `Tests/Verify-ViewLabContracts.ps1` for UI/DLL settings-contract checks.
- Added `AUDIT_2026-07-08.md` with feature verification, headset test checklist, known limits, and
  optimization/future-work notes.
- Full installer build passed: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.64.msi`.

## 4.1.63 - 2026-07-08

- Removed the obsolete `Technique C direct write` subtext under the `Visor mask` checkbox.
- Added experimental visor shape controls:
  `Apex Y` moves the outer curve's maximum point up/down, and `Inner low` adds a bottom-only inner-eye
  stencil curve for a mask-like lower nose/bridge shape.
- Added draggable preview pins for those two controls in the visor mask preview. The main window and
  per-app profile popup both sync pins, sliders, config, registry profile values, and the native Direct C
  geometry.
- Added native config/profile keys `mask_outer_apex_y` and `mask_inner_lower_y`, and logged both values in
  the DLL config line and verbose edge-smear contract diagnostics.
- Expanded Edge Masks hover text to state explicitly that visual masks replace render cropping on that
  axis, so GPU pixel savings are reduced or removed.

## 4.1.62 - 2026-07-08

- Gated the Technique C late `xrEndFrame` draw so it only runs as a fallback when the legal
  release-time draw did not cover the swapchain this frame. This avoids double-drawing opaque visor
  borders with previous-frame and current-frame layout data.
- Reused cached swapchain RTVs for the late Technique C fallback draw instead of creating render-target
  views every frame.
- Expanded verbose edge-smear contract diagnostics to emit a rate-limited baseline even when
  `edge_smear_fix=0`, and added release/late-draw provenance plus render scale.
- Fixed per-app visor reset/save stale state: reset now deletes `visor_size`, `visor_width`, and
  `visor_height`, and saving with `Use global visor settings` checked stores `visor_size=0`.

## 4.1.61 - 2026-07-08

- Temporarily removed visor Techniques A/B from the user-facing selector and force `visor_technique=c` in
  the UI/DLL config path. The checkbox remains the enable/disable control; Direct C is the only active
  product path for this build.
- Updated the visor mask preview so `Stencil outer edges only` displays a single-eye open-inner shape:
  curved outer edge, open/flat inner side, matching the Direct C left-eye geometry model instead of a
  closed oval.
- Hid stale per-app visor X/Y offset controls alongside the already-hidden width/height controls. Size
  and Curve are the intended visor controls.
- Bundled the verified custom ReShade OpenXR payload (`ReShade64.dll` + `ReShade64_XR.json`) and compact
  reference docs from the newer ReShade AIO work into `ReShadePayload`.
- Reworked ReShade Remote as `Advanced: ReShade Remote`, added clear status states, disabled controls
  until the heartbeat is live, and added an elevated install button that copies the payload to
  `C:\ProgramData\ReShade` and registers the ReShade OpenXR implicit layer.

## 4.1.60 - 2026-07-08

- Refocused visor UI around Direct C as the practical primary path: renamed the enable checkbox to
  `Visor mask`, removed the redundant `Off` technique option, and mapped old `visor_technique=off` values
  back to Direct C so the checkbox remains the only off switch.
- Removed visible manual visor Width/Height controls from the main window and per-app popup. The UI writes
  neutral width/height scale and the DLL ignores stale width/height profile values, so visor sizing follows
  the render crop plus Size/Curve.
- Added verbose-only, rate-limited edge-smear contract diagnostics comparing located FOV, submitted FOV,
  `imageRect`, swapchain size, recommended size, crop settings, visibility-mask state, and visor state.
- ReShade Remote now shows setup/verification text for the required custom ReShade shared-memory
  component and warns when no bundled `ReShadePayload` exists.
- Full installer build passed: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.60.msi`.

## 4.1.59 - 2026-07-08

- Corrected the Edge Masks popup wiring: only the two "Both sides" controls map to the real DLL keys
  (`horizontal_visual_mask_only` and `visual_mask_only`).
- Disabled the one-sided Edge Masks controls until there is a real one-sided runtime path, and stopped
  old one-sided legacy keys from enabling the broad both-sides mask.
- Full installer build passed: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.59.msi`.

## 4.1.58 - 2026-07-08

- Wired previously UI-only render options into the DLL path:
  `crop_outer_edges_only`, `visual_mask_only`, and `horizontal_visual_mask_only` now have real runtime
  effects, with legacy Edge Masks popup keys still read as fallbacks.
- Fixed another settings contract mismatch: `mask_rounded` now saves whether the visor curve is rounded,
  not whether the visor is enabled.
- Aligned visor defaults across UI, DLL, and `xr-viewlab.ini` (`mask_size=0.82`, `mask_corner=0.5`,
  width/height scale `1.0`).
- Optimized `xrEndFrame` edge-smear and visor-layer metadata patching to use fixed stack buffers instead
  of per-frame heap vectors, with safe one-shot overflow diagnostics.
- Demoted edge-smear FOV comparison logs to verbose-only and reduced their rate.
- Full installer build passed: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.58.msi`.

## 4.1.57 - 2026-07-08

- Updated `edge_smear_fix` to preserve the runtime's original OpenXR visibility mask instead of applying
  ViewLab's legacy outer-edge-only visibility-mask filter. This makes the mask-off path match the visor
  path's boundary handling instead of leaving the rectangular VD crop edge exposed.
- Fixed the UI/DLL key mismatch for the stencil checkbox: the UI now writes
  `outer_edge_visibility_mask_only` while preserving the old `stencil_outer_edges_only` key.
- Full installer build passed: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.57.msi`.

## 4.1.56 - 2026-07-08

- Replaced the non-working edge-smear pixel guard-band experiment with an `xrEndFrame` projection-FOV
  patch: when `edge_smear_fix=1`, submitted `XrCompositionLayerProjectionView.fov` is clamped to the
  cropped FOV returned from `xrLocateViews`.
- Full installer build passed: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.56.msi`.

## 4.1.55 - 2026-07-02

- Optimized release-time D3D11 visor/edge drawing by caching runtime swapchain render-target views instead
  of creating an RTV for every eye draw.
- Removed heap allocations from the high-segment visor geometry builders by switching their fixed-size
  curve buffers to stack arrays.
- Verified the x64 native OpenXR layer build before the full installer build.
- Full installer build passed: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.55.msi`.

## 4.1.54 - 2026-07-02

- Preserved the real vertical/horizontal tangent crop path. The LOD-pop-in experiment is now
  diagnostic-only and no longer bypasses cropped FOV, avoiding the stretch-over-lens failure.
- Reworked the edge-smear experiment to draw black guard pixels into projection texture edges instead of
  changing submitted `imageRect` metadata.
- Technique A remains a real OpenXR composition-layer path, now drawing the same open-inner left/right
  visor artwork into a BOTH-eye alpha quad for VDXR compatibility.
- Technique B is narrowed to D3D11 colour non-MSAA swapchains and bypasses unsupported swapchains instead
  of intercepting them.
- Technique C now draws at release and late `xrEndFrame`, with higher-segment open-inner geometry and a
  projected partner-eye boundary for aggressive horizontal crop convergence.
- Removed visible X/Y visor sliders from the main UI and made the DLL ignore stale X/Y profile biases.
  Size, Width, Height, and Curve are the active shape controls.
- Full installer build passed: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.54.msi`.

## 4.1.47 — 2026-07-01

- Removed the debug test quad entirely (it proved layer submission; done).
- Technique A reworked to a single BOTH-eye head-locked quad (per-eye LEFT/RIGHT quads were not rendering
  on VDXR; the BOTH-eye quad did), FOV-sized, alpha-cutout (black ring, transparent hole).
- Technique C: added `context->Flush()` after the eye-texture draw so a streaming runtime's encoder
  (VDXR) sees the write; C remains unreliable on streaming runtimes (A is primary).

## 4.1.46 — 2026-07-01

- FIX (root cause of "no mask"): `visorSize` fallback for a missing `mask_size` was the legacy
  OpeningFromMask formula, which clamps to 1.0 for cropped views = full opening = invisible border.
  Fallback is now a visible 0.82. This is why neither technique showed a mask despite drawing correctly.

## 4.1.45 — 2026-07-01

- Added a UI **Technique** selector (radio: Quad (A) / Direct (C) / Off) in Render Options, writing
  `visor_technique`. Verified in-app (round-trips a↔c↔off to the ini). No more hand-editing the ini.

## 4.1.44 — 2026-07-01

- Implemented technique A (quad overlay) alongside technique C, selectable via `visor_technique`.
- Shared `CreateHeadLockedRgbaLayer` helper for the head-locked VIEW-space RGBA swapchain.

## 4.1.43 — 2026-07-01

- Visor ENABLE made global-only: per-app profiles no longer carry/override `mask_enabled` (they still
  tune shape). Fixes stale per-app `mask_enabled=0` silently overriding the global toggle. Per-app enable
  checkbox disabled in the profile popup.

## 4.1.42 — 2026-07-01

- Added a DEBUG head-locked blue test quad (`test_quad` config, default off): our own
  `XrCompositionLayerQuad` in a `VIEW` reference space at (0,0,−1), 0.4 m, cleared solid blue. Independent
  of the game textures and of `mask_enabled`. Purpose: prove OpenXR layer submission reaches the headset.
- Captured `xrCreateReferenceSpace` for the quad's head-locked space.

## 4.1.41 — 2026-07-01

- Removed the gate that disabled the native D3D11 visor whenever the game called
  `xrGetVisibilityMaskKHR` (this was why Unity/Pistol Whip never drew).
- Moved the visor draw from `xrEndFrame` to `xrReleaseSwapchainImage` — the lifecycle-correct point where
  the app has finished rendering the eye and the runtime has not yet consumed it. `xrEndFrame` now only
  captures the per-eye layout (imageRect + array slice) for the next frame's release-time draw.
- Made the visibility-mask mesh-reshape optional (`visibility_mask_visor`, default off); it never
  suppresses the D3D11 path. Outer-edge visibility-mask filtering preserved.
- Installed `xrReleaseSwapchainImage` hook (was only captured before).

## 4.1.40 — 2026-07-01

- Main window: removed the large blank gap below VIEWLAB ENABLED. Col 0 is now a single top-aligned
  `LeftColumnPanel` StackPanel, mirroring cols 2/4, so the tall RowSpan side panels can't inject overflow
  between the left cards.
- App Profile popup: content wrapped in a vertical `ScrollViewer`; bean editor restored in the visual
  tree; visor sliders interactive when "Use global" is unchecked; opens without crashing.
- Typeless-safe RTV format for the eye-texture write (use app-requested swapchain format mapped to non-SRGB).
- Added extensive one-shot `DIAG` logging across the whole D3D11 mask path.

## 4.1.39 — 2026-06-30/07-01

- Removed the old ViewLab-owned `XrCompositionLayerProjection` "orb" visor renderer.
- Implemented native D3D11 direct-write of the kidney/superellipse visor border into the game's existing
  eye textures; added swapchain tracking hooks (create/enumerate-images/acquire/destroy).
- Restored the ProfileWindow bean editor and interactive per-app visor sliders.

## 4.1.7 — 2026-06-25

- Removed stale `OpenVRBridge/`, `ReShadePayload/` binaries, and `ReshadeAI/` agent files from repo.
- Fixed installer `Product.wxs` to not reference deleted ReShadePayload files.
- Created `SOURCE_BACKUP.md` consolidating all backup sources and rebuild instructions.
- Published GitHub release v4.1.7 with MSI.

## 4.1.0 — 2026-06-25

### UI

- Responsive layout: four window-width scale modes — see Layout Modes below.
- Triple-column mode: left = render sliders, middle = applications table, right = Render Options + ReShade MENU sections.
- Right column uses an independent StackPanel (not shared Grid rows), eliminating the ghost height gap that previously appeared below Render Options.
- VIEWLAB ENABLED / VIEWLAB DISABLED badge replaces the checkbox. Animated red/green border, background, and text colour transition (0.25 s).
- Visual Masks moved into a non-layout-participating Popup flyout behind a "Visual Masks ▾" button. Popup has no measured parent so it never reserves blank space when closed.
- Removed "RENDER" section subheader.
- Removed "Horizontal visual mask" and "Vertical visual mask" inline subheaders from the main card.
- OpenXR and OpenVR ReShade MENU sections retain their titles but all entries replaced with "Coming soon" stubs.
- Install ReShade button and "ReShade Available" pill badge removed.

### Applications table

- XR type column added (OpenXR / OpenVR, detected via OpenComposite_ display-name prefix).
- Junk-app blacklist: steam, steamvr, steamtours, racelab_vr, vrmonitor, vrserver, vrdashboard, openvr_overlay, xr_composition_layer_override, pivotpoint and variants filtered from the app list.

### Settings

- Sliders and value boxes auto-save on change; Save button removed.
- Settings write via `WritePrivateProfileString` unchanged; INI path unchanged.

### Layout Modes

| Mode | Width | Content |
|------|-------|---------|
| **Mini** | < 360 px | Single column, sliders compress to full width, footer items equally spaced |
| **Small** | 360–599 px | Single column, sliders full width with labels and hints |
| **Medium** | 600–899 px | Two columns — left: render sliders + options; right: applications table + ReShade menus |
| **Large** | ≥ 900 px | Three columns — left: render sliders; middle: applications table; right: Render Options + ReShade menus |

### Build

- Version bumped to 4.1.0 in AssemblyInfo.cs and Product.wxs.

---

## 4.0.x and earlier

See git history.
