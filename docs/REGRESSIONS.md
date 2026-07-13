# Regression memory

> Institutional scar tissue. Read before touching the areas named here. Append an entry whenever
> a significant regression occurs: what / why / how detected / fix / how to never repeat it.

## R20 — Topmost swapchain churn can collapse the user-mode display stack

**What:** During a DiRT Rally menu-to-car transition, Topmost recreated its large projection
swapchain for every submitted `imageRect` size change, including a one-pixel oscillation. Once
allocation failed it retried 193 times in about 21 seconds while the D3D device and DWM failed.

**Never again:** Topmost receives one allocation attempt per session, based on stable underlying
texture capacity. It never reallocates for submitted-rectangle jitter, retries a failed session,
re-arms by checkbox cycling, or destroys failed resources from the render path. Device removal
disables every ViewLab D3D draw until the next session. `Tests/Verify-TopmostSafety.ps1` pins this.

## R1 — FreeLibrary-before-blob-use crash (4.1.56–4.1.88 era; fixed 4.1.89, re-fixed 4.1.100)
**What:** Pistol Whip (and any game without d3dcompiler_47 resident) crashed 0xC0000005 at launch.
**Why:** `InitD3D11MaskRenderer` called `FreeLibrary(dxcLib)` before the compiled shader blobs
were consumed by `CreateVertexShader`/`CreatePixelShader`/`CreateInputLayout`.
**Detected:** game crash immediately at startup, only with the layer enabled.
**Never again:** free after last blob use + all error paths (D7). The contract test does not pin
this — verify manually when touching `InitD3D11MaskRenderer`.

## R2 — Nose divot rendered on TOP of the lens (4.1.63–4.1.101; fixed by rewrite 4.1.105)
**What:** inner-low "divot" appeared at the top of both lenses, wrong curve, barely visible.
**Why:** native code anchored the bezier to `y1` copying the UI's screen-coords formula — but
NDC y is up, so `y1` is the TOP. Plus the old `BuildNoseBridgeCurve` emitted bare points into a
TRIANGLELIST (garbage geometry).
**Never again:** divot anchors to NDC `y0`, every vertex band-clamped (structurally bottom-only);
the y-flip lives only in `ApexYFromConfigNdc` (D5). Contract test pins both.

## R3 — "Stencil outer edges only" wired to nothing (long-standing; fixed 4.1.102)
**What:** checkbox had zero effect; inner eye edges stayed stenciled.
**Why (two bugs):** UI wrote `stencil_outer_edges_only`, DLL read `outer_edge_visibility_mask_only`
— a silent key mismatch; AND the filter was skipped whenever the visor was active, so VD's FOV
stencil passed through unfiltered and the game stenciled all edges itself (D8).
**Never again:** contract test asserts the DLL reads the UI's key and that UI/DLL constants match.
When adding any key: grep BOTH sides before shipping.

## R4 — mask_size fallback 1.0 = invisible mask ("no mask for 3 days", 4.1.46 & again 4.1.65)
**What:** visor silently disappeared; drawing pipeline was fine.
**Why:** missing `mask_size` (wiped from ini) fell back to a formula that clamps to 1.0 = full
opening = zero-width border. Regressed twice via legacy `mask_vertical/horizontal` derivation.
**Never again:** fallback is 0.82 in UI and DLL; opening never derived from legacy keys (D3).
Contract tests pin the UI's direct `0.82` fallback so installer-stripped configs cannot recreate
the invisible visor on the next save.

## R5 — Untested "current settings as defaults" broke launch (4.1.88; reverted 4.1.89)
**What:** hardcoding the user's live settings as app defaults shipped `mask_size=1` (R4 again)
and wrong crop values; Pistol Whip broke immediately; the build was pushed to GitHub untested.
**Never again:** never publish untested builds (agents.md rule 2). Defaults changes are
high-risk — they interact with the MSI ini overwrite (R6).

## R6 — Visor mask size slider confused opening size with mask thickness (4.1.55–4.1.117; fixed 4.1.118)
**What:** visor mask worked at small size values but became invisible at full size (1.0).
**Why:** `mask_size` controlled the opening size (clear area). When size=1.0, the opening filled
the entire bounding box, leaving zero space for black mask triangles. The visor draws black
triangles OUTSIDE the opening, so a full opening = no mask.
**Detected:** user reported visor works at extreme small values but not at normal/full size.
**Never again:** the size slider is removed and the opening is hardcoded to fill the full crop
bounding box (mask_width_scale/height_scale fixed at 1.0). The visor mask only rounds corners;
shape controls (curve, apex, bridge) are the only tunable geometry. If an opening-size concept
returns, it must be explicitly named "opening" or "mask" and not conflated with the other.

## R7 — MSI install wipes/drops live ini keys (standing hazard)
**What:** installing an MSI overwrote `%LOCALAPPDATA%`-adjacent config; keys not present in the
bundled default ini (e.g. `mask_enabled`, `mask_size`, `mask_corner`) were dropped, silently
changing behavior after "just an update".
**Never again:** keep the bundled `xr-viewlab.ini` complete (every product key present with sane
defaults) and include it in the MSI/package output as a fresh-install template. Ordinary upgrades
preserve the live `%LOCALAPPDATA%` ini and per-app HKCU profiles. The OpenXR layer must never
rewrite settings from inside a game. Contract tests pin packaging and the absence of reset hooks.

## R8 — The 2026-07-10 spiral: 14 blind builds, features lost (4.1.88→4.1.101)
**What:** 12+ hours of guess-edit-build cycles on the inner-low/stencil symptoms; five failed
"fixes" for pin dragging alone; multiple full-file reverts (4.1.88 → 4.1.89 → 4.1.55) that threw
away working features (apex-y, inner-low, bridge, jitter AA, 4.1.64/65 perf work) while the
actual bugs (R2, R3) sat unread in the code.
**Why:** patching symptoms without reading the failing pipeline end-to-end; trusting stale
assumptions over source; version churn instead of root-cause isolation; changelog entries
claiming "tests pass" when the contract test was failing.
**How it ended:** reading `xrGetVisibilityMaskKHR`, the geometry builders, and the UI key
constants once, carefully — both root causes were visible in the code the whole time.
**Never again:** prove root cause before patching (agents.md rule 6); never claim test results
without running the test; when two consecutive fixes fail, stop editing and read the whole
subsystem; reverts are for known-good states, not exploration.

## R9 — Pin click-drag: five threshold tweaks, zero fixes (open, Pass 2)
**What:** preview pins can't be dragged; repeated "fixes" adjusted hit thresholds/Focusable.
**Why (diagnosed, unverified):** `OnMouseLeftButtonDown` never calls `CaptureMouse()`, so the
drag dies as soon as the cursor leaves the small canvas; thresholds were never the problem.
**Lesson:** input bugs are pipeline bugs (capture, routing, hit-testing) before they are
geometry bugs.

**2026-07-10 fix:** `BeanMaskEditor` now captures mouse only after a pin hit, releases on
mouse-up, and clears drag state on lost capture. Contract test pins the input pipeline.

**2026-07-10 correction:** capture alone was not enough. `ShapeChanged` saved the old slider
values, and `SaveGlobalSettings()` immediately rebuilt the editor from those stale sliders,
snapping the drag back. The host windows now sync editor values back to sliders before saving,
and the editor uses preview mouse events plus full-rectangle hit testing.

**2026-07-10 correction 2:** the apex pin drag inverse also used `pins.y0` instead of the
pin band centre. That made the centred pin map to `0.5` and clamped negative drags away. The
drag code now uses the same centre-origin formula as `PinPositions`; contract tests pin it.

## R10 - Per-version upgrade reset would erase user tuning (caught before headset release, 2026-07-11)
**What:** the MSI wrote its changing product version as a reset marker; the next UI or game launch
deleted visor and per-app visor settings. Every upgrade therefore behaved like a factory reset.
**Why:** a workaround for elevated-installer user context preserved the reset policy rather than
challenging whether an ordinary update should reset anything.
**Never again:** upgrades preserve user settings. Defaults repair fresh installs only, and native
runtime code remains read-only with respect to configuration.

## R11 - ReShade Remote "component missing" despite installed payload (fixed 2026-07-11)
**What:** the Remote reported "this build does not include the ReShade Remote component" even
though `C:\Program Files\xr-viewlab\ReShadePayload\` contained ReShade64.dll + json.
**Why:** the app is published single-file with `IncludeAllContentForSelfExtract=true`, so
`AppContext.BaseDirectory` is a %TEMP% extraction folder, not the install directory. The payload
lookup searched BaseDirectory and CWD only. MainWindow already had the correct pattern
(`Environment.ProcessPath`); the Remote never used it.
**Never again:** anything locating files next to the installed exe must use
`Environment.ProcessPath`, never `AppContext.BaseDirectory`. Contract test pins it.

## R12 - Crop toggle read but not applied; enabled visor could be invisible (caught in headset testing, 2026-07-11)
**What:** “Crop outer edges only” always behaved as enabled even when unchecked. Separately, an
enabled visor with legacy `mask_size=1` drew no border, making the visor checkbox look broken.
**Why:** `LoadConfig()` read the crop key, but the FOV calculator unconditionally retained the
inner edges. A full-size opening was still accepted by the renderer.
**Never again:** test the FOV branch, not only config read/logging. Outer-edge-only must gate the
per-eye branch; full crop must scale both sides. An enabled visor must recover an invisible
full-opening legacy value to a safe visible default.

## R13 - MSI must never enumerate the shared OpenXR implicit-layer registry (4.1.142; fixed locally)
**What:** after a ViewLab MSI upgrade, the machine retained only a fraction of its approximately
ten implicit OpenXR layer registrations.
**Why:** the MSI ran a deferred `CleanupApiLayerRegistry` script which enumerated the shared
`HKLM\...\ApiLayers\Implicit` registry key. Even narrowly filtered cleanup is unacceptable at that
ownership boundary and can turn an upgrade into a layer-loss incident.
**Never again:** the MSI contains no registry-enumerating cleanup custom action. WiX may write and
remove ViewLab's own declared registry values only; no installer code may enumerate, recreate, or
delete values in the shared implicit-layer key. Contract test pins this.

## R14 - MSI filename/version can disagree with its WPF payload (4.1.169; fixed 2026-07-12)
**What:** `ViewLab-4.1.169.msi` installed and registered as 4.1.169, but launched a byte-identical
4.1.168 executable with none of the new Overlays UI.
**Why:** the project TFM moved to `net8.0-windows10.0.19041.0`, while both `build.ps1` and WiX kept
using the old `net8.0-windows` publish directory. That stale directory remained on disk and was a
perfectly valid WiX input, an especially unhelpful form of success.
**Never again:** derive the publish directory from the project TFM, clean generated outputs, and
administratively extract every built MSI. The build must compare payload version and WPF/native
hashes to fresh outputs and require compiled overlay markers before copying an MSI to `dist`.

## R15 - Flat overlays must not depend on the VDXR vertex-colour interpolant (4.1.169-176)
**What:** crosshair state, geometry and both-eye draw routing were correct, yet the crosshair was
invisible in-headset.
**Why:** the new flat overlay pass reused the visor pixel shader's interpolated `TEXCOORD0` colour.
The calibration investigation had already demonstrated that VDXR can deliver that interpolant as
black. A green crosshair therefore became black, often on a black visor/game background.
**Never again:** boundary/crosshair flat colours come from an explicit pixel-shader constant buffer.
Contracts pin that shader and resolved per-eye draw logging; previews mirror the rectangle formulas.

## R16 - Equal eye pixels are not a fused overlay coordinate (4.1.179-180)
**What:** crosshair split into two images and moved/rescaled with crop; boundary flash showed only
corner fragments. Other overlays mixed angular placement with crop-relative sizing.
**Why:** the crosshair was forced to each eye rectangle's normalized midpoint, although asymmetric
FOVs require different pixel projections for one shared angular point. Crosshair/HUD/notification
sizes also used cropped width/height directly. The boundary stroke straddled the active scissor.
**Never again:** fused content starts in shared tangent/angular space and projects independently per
eye; fixed-size elements use pixels-per-tangent, not cropped dimensions. Pixel-centred calibration
patterns remain explicitly texture diagnostics. Boundary outlines are fully inset. Deterministic
contracts cover asymmetric projection, angular round-trip, and crop-invariant size.
# Overlay coordinate divergence and repeated projection layers (2026-07-13)

HUD, trace, calibration, and general overlays had independently evolved pixel/tangent anchoring. Asymmetric FOV crops therefore moved nominally identical centres differently. OpenComposite can also repeat a projection texture in later composition layers; accumulating every layer caused the same release-time HUD geometry to be drawn repeatedly into one image. All placement now goes through `OverlayCoordinateResolver`, and capture accepts only the primary projection layer. Never repair this with per-game offsets or opacity changes.

The first resolver implementation then introduced a systemic stereo regression: Render Area returned identical normalized pixels independently in each eye, while Lens Pinned `(0.5,0.5)` used each asymmetric eye's own FOV midpoint. Both create two monocular stickers rather than one visor-space target. The repaired contract constructs shared binocular tangent bounds, chooses one target there (crosshair zero is tangent `0,0`), and projects it through each eye independently. Tests must prove asymmetric eyes receive different local pixels while inverse-projecting to the same tangent target.
# Asymmetric crop rotated the game eye poses (2026-07-13)

`foveated_center_compensation` was changed from an optional default-off experiment to permanently on. In split mode it replaced the requested asymmetric FOV with a symmetric FOV and wrote a pitch quaternion into `XrView.pose.orientation`. Runtime logs captured `pitch_offset=0.31637` radians (about 18.1°), exactly matching the tilted/folded world report. The feature is retired: vertical crop changes FOV tangents only and must never modify game eye poses. Contract tests forbid `view.pose.orientation` writes.

## R17 - A mode checkbox must persist the mode transition (4.1.186; fixed 2026-07-13)

**What:** after disabling Split Top/Bottom, a later game launch could still place normal Vertical in
the former top-only slice. **Why:** `SplitCheck_Changed` refreshed controls and preview but omitted
`SaveGlobalSettings()`. The ini therefore retained `split_mode=1` and its asymmetric tangents until
some unrelated setting happened to save. **Never again:** mode-changing handlers persist the whole
active configuration immediately; disabling split writes both `split_mode=0` and centred tangents.

## R18 - Straight-alpha compositor layers must say so (4.1.185-186; fixed 2026-07-13)

**What:** HUD and trace colours were pale only through Topmost Visor Overlays. **Why:** Topmost was
hard-coded to linear `R8G8B8A8_UNORM` while the normal game projection could be sRGB, so identical
shader values entered the compositor under different transfer declarations. ViewLab also produced
straight RGBA without advertising the unpremultiplied convention for partially transparent content.
**Never again:** match the primary projection's compatible sRGB/UNORM format and non-sRGB RTV view,
declare straight alpha, and leave the established direct path untouched.

## R19 - Calibration bounds depend on what a pattern measures (4.1.185-186; fixed 2026-07-13)

**What:** edge probes became inset, pixel plates shrank with crop, the grid missed the crosshair,
and the radial plate appeared oval. **Why:** unrelated calibration tools were all placed inside the
shared binocular-overlap rectangle, while the radial plate used equal raw-pixel radii despite unequal
horizontal/vertical pixels per tangent. **Never again:** texture-measurement tools use the complete
submitted eye rectangle and literal pixel pitches; centre-based perceptual shapes use the shared
crosshair tangent target and per-eye projection. Do not repair either class with identical eye pixels.
