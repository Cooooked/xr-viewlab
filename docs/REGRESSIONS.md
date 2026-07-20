# Regression memory

## R47 — Crosshair preview rendered at real headset scale

**Symptom:** The desktop crosshair preview appeared as a tiny black pixel and was not useful for
judging style, colour, thickness, gap, outline or scale changes.

**Cause:** `BeanMaskEditor.DrawCrosshair` and the standalone `CrosshairPreview` both computed arm,
thickness and gap sizes from the real headset reference-pixel factor (`eyeHeight/1080` via
`Quest3PreviewGeometry.TangentReferencePixelsUniform`, and the standalone `1.125*VrScale` factor).
Those real-world sizes are only a few pixels on the desktop preview, so the crosshair was
indistinguishable from a dot.

**Contract:** The real `CrosshairSettings` (size, thickness, gap, outline thickness, alpha, scale,
dot, T-style, colour) and the native renderer are unchanged. A preview-only
`CrosshairPreview.PreviewDisplayScale` multiplier and shared `CrosshairPreview.Measure` helper
apply only inside `CrosshairPreview.OnRender` and `BeanMaskEditor.DrawCrosshair`. Positioning still
uses `Quest3PreviewGeometry.ResolveCentredOffset`. The preview enlargement is never persisted and
does not affect headset rendering. Optical-centred transform and the permanent `+0.077` widget
preview shim are untouched.

## R46 — Notification card pixel data not uploaded for non-Bold themes

**Symptom:** Notifications stopped rendering in the headset after the theme/palette redesign. Test
Presentation and real Windows/music notifications were invisible for all themes except Bold.

**Cause:** The redesigned themes introduced distinct footprints (Classic 336×92, Compact Banner
336×44, Minimal 288×72, Bold 336×96). `NotificationService.ComposeCard` returned a tightly-packed
`w×h` RGBA bitmap, but the shared-memory contract and `WriteBlock` expected every card to fill the
fixed 336×96 slot (`CardPixels = CardW * CardH * 4`). `WriteBlock` silently skipped uploading pixel
data whenever `c.Rgba.Length != CardPixels`, so only the Bold design (which exactly matches the slot)
rendered. The native layer received metadata and a stale/empty texture for the other three designs.

**Contract:** `NotificationCardLayout` owns the fixed slot size (`SlotW`, `SlotH`) and the per-design
`DesignFootprint`. `ComposeCard` always returns `PadToSlot(rgba, w, h)`, which pads smaller footprints
to the slot stride. `WriteBlock` treats a length mismatch as a fatal contract error instead of
silently skipping pixels. The `Tests/NotificationCardFixtures` project asserts every theme's footprint,
verifies composed cards have visible pixels, and checks that `PadToSlot` preserves content and leaves
padding transparent.

## R45 — OBS Recording Cue preview used the full-lens frame

**Symptom:** The OBS Recording Cue preview border stayed on the dotted outer frame instead of moving and resizing
with the solid post-crop rectangle. Its top-left label also collided with the iRacing label when both were visible.

**Cause:** The main preview created OBS with the generic `Edge` anchor, and the per-app preview did not add its
effective OBS feature state to the shared preview item list.

**Contract:** Main and per-app previews create OBS with `RecordingRenderEdge`. `BeanMaskEditor` resolves that anchor
to the exact `PreviewCropRect` and labels it bottom-left. The existing iRacing `RenderEdge` inset and top-left label,
ordinary widget transforms, visor geometry and native OBS cue renderer remain unchanged.

## R44 — Widget preview forward and inverse transforms must share the full-lens rectangle

**Symptom:** Widgets bunched near the centre, ordinary drags became far too sensitive and the usable movement range
collapsed after a preview-only correction attempted to place X/Y inside the small post-crop rectangle.

**Cause:** A preview-only pass changed drawing to `cropOrigin + saved * cropSize` and dragging to divide by crop size,
while persistence and native rendering continued using `OverlayPlacement::FullLens`. At 15% vertical crop this
compressed restarted widgets into the narrow centre band, amplified drag by 6.67× and saved values that native
interpreted in a different frame. Recorded matched-locate logs prove `fullFov` is the original FOV; the earlier
fallback assumption was false.

**Contract:** `ResolveFullLens` and `ApplyFullLensDrag` use the exact same full preview rectangle in main and per-app
previews. Crop is visibility only. Frame/eye guides, equal split crop, visor, crosshair, widgets and edge cues use one
geometric preview centre; size calibration cannot move it. Restart round-trip, forward/inverse, small-delta and preview/native numerical fixtures must pass.
Zoom/pan never enter persisted coordinates. Runtime and saved coordinates remain unchanged.

## R43 — iRacing edge preview used the full-lens guide

**Symptom:** The red iRacing telemetry outline appeared on the outer dotted Quest 3 reference instead of the
solid post-crop render boundary.

**Cause:** All non-editable edge placeholders shared `OverlayPreviewAnchor.Edge`, whose geometry was always the
full preview area. The native racing cue already used the submitted post-crop eye rectangle; only its calibration
preview represented the wrong boundary.

**Contract:** iRacing uses `OverlayPreviewAnchor.RenderEdge`, resolved through `PreviewCropRect`. The generic lens
edge anchor remains separate so unrelated feature previews are not silently reinterpreted.

## R41 — Global live mappings must not overwrite app overlay overrides

Placement-only profile data was overwritten by each global live-state generation. Active-app configuration now sets
native feature masks; live-state, telemetry-config and sticky-note consumers leave overridden features alone. Reset
deletes both `overlay_override_*` and `overlay_layout_*` values.

## R42 — Factory defaults must not drift between INI and fallback literals

The captured 4.1.255 JSON supplies missing-key fallbacks and is pinned against the packaged INI. Its one-time marker
is `FactoryBaselineAppliedVersion`; never widen it to app profiles or ReShade deployment state.

## R40 — Per-app visor configuration cannot be half global and half custom

**What:** the profile checkbox was deliberately disabled, native code ignored per-app `mask_enabled`, and the
profile preview stayed grey because it never received the enabled state. Its visible controls also predated the
main Size/Width/Height/Curve/Outer Dip/Nose/Nose Spread X model. **Fix:** custom profiles persist and load all seven
current values plus enable, while `visor_size=0` removes custom visor keys and follows globals. Both editors use
`BeanMaskEditor`; the profile now calls `SetVisorVisible` on every relevant change. **Never again:** contracts pin
control names/ranges, red active state, registry round-trip and native enable consumption.

## R39 — Split crop controls must be relative to their own lens half

**What:** The preview treated each `0..1` Top/Bottom control as a full-lens fraction and saturated it at `0.5`.
Top `0.5` plus Bottom `0.5` therefore filled the whole lens instead of the centred middle half.

**Contract:** UI controls are half-relative; each contributes `value × 0.5` of full height. Stored/native
`top_tangent` and `bottom_tangent` remain full-lens shares. Deterministic checkpoints pin `1/1`, `1/0`, `0/1`
and `0.5/0.5`.

## R38 — Remote layout and desktop focus are separate contracts

**What:** duplicated help text clipped the In-HMD controls, while fresh desktop preview windows began headless and
could not accept normal text focus. **Why:** a fixed-height, non-scrollable body gained another paragraph, and the
managed mapping defaulted `win_headless=1`. The Remote also reapplied unchanged widget values every poll. **Fix:**
details live only in help; the readable body has sufficient height plus scrolling; fresh mappings use
`win_headless=0`; UI state is revision-gated. **Never again:** focused contracts pin all four facts and the binary
markers for Home, shared control, persisted transform and OpenXR overlay. Payload-internal lag/refresh defects
cannot be declared fixed without the payload source and a rebuilt binary.

## R37 — A first-read ReShade heartbeat is not a live handshake

**What:** stale shared memory could initially report Connected with no live payload. **Why:** the first poll compared
a persisted non-zero heartbeat with the window's default zero and called it a fresh update. **Fix:** attachment
baselines the current value; only a later heartbeat transition starts the Connected freshness window. **Never
again:** mapping existence and initial contents are transport state, not runtime evidence.

> Institutional scar tissue. Read before touching the areas named here. Append an entry whenever
> a significant regression occurs: what / why / how detected / fix / how to never repeat it.

## R36 — Desktop calibration controls must expose and share their real bounds

**What:** IPD advertised 0.1 mm adjustment but rendered no spinner arrows; the vertical preview's full-scale edge
contract was implicit in two half-extents; Clock + Timer and Notifications stopped at large, different scales.
**Why:** IPD stepping existed only in key handling, vertical geometry reconstructed total height late, and each card
repeated its minimum across XAML and native clamps. **Fix:** visible arrow buttons call the same IPD step helper;
preview vertical state is canonical scale-plus-centre with an exact full-frame branch; the two requested cards use
`0.1` in their slider and native startup/live clamps, while preview pins consume the slider bounds. **Never again:**
focused contracts pin both arrow buttons, exact full-height mapping, the two matching scale paths, and unchanged
neighbouring widget minima. Preview calibration changes must never mutate runtime crop.

## R35 — Startup state must hydrate the preview immediately

**What:** saved overlay widgets were absent from the preview until the user toggled or changed a control.
**Why:** `LoadSettings` suppressed live/preview work while `_loading` was true but never published and refreshed
once loading ended. **Fix:** the completed load performs one deterministic `PublishLiveState`, which refreshes the
shared preview descriptors from the hydrated controls. **Never again:** source contracts pin that post-load call;
startup preview state may not depend on an input event.

## R34 — A visor-only global choice is not a whole-profile reset

**What:** saving crop or resolution edits while `Use global visor settings` remained checked could delete the
entire application profile, and the main table could remain stale. **Why:** one `UseGlobal` result represented both
the visor-only checkbox and the explicit whole-profile reset action; the caller interpreted either as permission
to clear `profile_enabled`. It then mutated the existing row without reading the registry back. **Fix:** the dialog
returns separate `UseGlobalVisor` and `UseGlobalValues` intentions. Ordinary Save always enables the app profile,
stores `visor_size=0` only for a global visor, and reloads the registry-backed list. **Never again:** only the
explicit `Use Global Values` action may erase the profile; deterministic contracts pin both branches.

## R33 — The headset preview is not a projection-degree diagram

**What:** the new preview appeared shifted upward and too small, its eye guides were ovals, and horizontal `0.8`
did not approach the dotted usable boundary. **Why:** preview geometry reinterpreted asymmetric projection angles,
split the eyes into container-shaped regions and applied crop-relative coordinates after already drawing a crop.
That mixed spaces, effectively cropped elements twice, and let aspect ratio stretch the guides. **Fix:** one centred
fixed-aspect full box now owns direct crop, visor, guide and widget coordinates. Eye guides have square bounds and
equal radii. A retained preference switches between those two circles and one binocular oval without changing the
shared periphery boundary. Independent frame/periphery guide modes and preview IPD affect guide drawing only.
`0.8` is exactly 80% of box width and `0.15` exactly 15% of height. **Never again:** preview geometry
contains no degree/tangent conversion; contracts numerically pin scale, centring, asymmetry and circularity.

## R32 — Outer-only horizontal crop must retain the requested fraction

**What:** horizontal `0.8` retained about 90% of recommended width, so runtime crop and preview could not agree.
**Why:** the outer-only path scaled one tangent magnitude, then reported `(1 + horizontal)/2` as effective width,
halving the requested reduction. **Fix:** each eye keeps its inner tangent boundary and places its outer boundary so
the remaining span is exactly `horizontal_render_width` of the submitted full span; effective resolution scale is
that same value. **Never again:** fixtures pin the direct span equation and the `0.8` result.

## R31 — Swapchain ownership is not stereo projection context

**What:** fused direct-to-eye overlays were correct in Pistol Whip but appeared as separated left/right ghosts in
Pools under the same native VDXR runtime. **Why:** `TrackedSwapchain.eyeViews` served two unrelated roles: selecting
the texture destinations owned by a swapchain and supplying the complete view set to `OverlayCoordinateResolver`.
Pistol placed both eyes in one two-slice array swapchain, so the vector happened to be binocular. Pools used separate
single-slice eye swapchains, so every release supplied only one eye and shared tangent bounds collapsed to that eye's
asymmetric FOV. **How detected:** matched PID-bound `PIPE` traces rejected timing, locate/FOV, pose/orientation,
reference-space and runtime-submission differences before identifying swapchain topology as the first divergence;
the old traces recorded Pools `eyes=1` versus Pistol `eyes=2`. **Fix:** one immutable, display-time-correlated
`ProjectionFrameContext` owns the ordered submitted views and their sub-image bindings. A release selects only its
target views while coordinate resolution always receives the complete projection view set. Correlated original FOV
is accepted only when the application submitted the matched located cropped FOV unchanged. **Never again:** texture
ownership must never define stereo context. Executable topology fixtures cover array slices, separate swapchains,
atlas rectangles, mixed packing, overlap order and release order. Contracts pin the context/target split; the matched
4.1.243 headset matrix passed Pools, Pistol Whip, Eleven Table Tennis and DiRT Rally 2 menu/cockpit without title rules.

## R30 — Successful compositor submission did not mean visible presentation

**What:** a translated D3D11 session loaded `mask_enabled=1`, rendered all enabled features and returned
success from `xrEndFrame`, yet visor, HUD, trace and notifications disappeared together in menus. **Why:**
the first bridge policy replaced the previously working transparent stereo projection with a head-locked
quad. The translated compositor accepted that quad but did not display it, and the policy briefly used
ordered readiness to suppress direct common rendering. **Never again:** one bridge-owned plan selects all
feature presentation from current frame/layer/graphics capabilities. Direct owns the allocation transition;
once ordered presentation is ready, every normal feature moves to the established two-slice transparent
projection appended after application layers and direct normal-feature drawing stops. Ordered failure moves
the complete set back together. Composition-only frames report no proven carrier rather than claiming visibility.
Fixtures pin this selection and the direct-path invariant. Creation/submission is transport evidence, never
visibility proof. No title/runtime allowlists.

## R29 — Force-killed PresentMon lost every row and leaked ETW sessions

**What:** real Brave and DiRT captures reported a running PresentMon child but produced no CSV; three
`ViewLab-*` ETW sessions remained active afterward. Target exit and the advertised Trace deadline also
left the cockpit in an active limbo and could leave WPR running. Detailed module detection then ran only
after the dead target had vanished. **Why:** finalisation existed only behind the Stop button, PresentMon
was killed as a process rather than stopped by its unique trace-session name, and ViewLab delegated the
only CSV buffer to that abruptly killed process. Fixtures used the live LocalAppData store and therefore
missed the lifecycle while risking production-index writes. **Never again:** ViewLab pumps PresentMon
stdout to a durably flushed partial CSV, stops the named collector session cleanly, automatically
serialises natural exit/deadline finalisation, samples modules while the target is alive, ships a pinned
collector and notice, and roots integration fixtures in a temporary store. Contracts pin the binary hash,
packaging, flags and lifecycle symbols; the integration fixture asserts no partial directory remains.

## R26 — A truthful full-session trace was visually uninterpretable

**What:** the increased-resolution DiRT session opened reliably, but its 87,268-point graph looked like
unexplained shelves and exposed no units, scale, series identity or point inspection. **Why:** a single
full-duration polyline used maximum-driven scaling and no session context. Raw-row correlation proved
the shelves were genuine menu/loading cadence transitions, not aggregation or unit corruption; the
driving interval itself remained near 11.111 ms. **Never again:** axes and legend are mandatory, the
view zooms/pans/resets, hover reports exact values, min/max pixel buckets retain spikes, robust scaling
calls out clipped outliers, and inferred cadence misses remain visibly labelled estimates. Schema 1
recording/opening is fixture-pinned while schema 2 only adds UTC, GPU and alarm evidence.

## R25 — Completed trace depended on callbacks DiRT Rally never supplied

**What:** builds through 4.1.219 reported no completed session after a normal user-visible DiRT Rally
exit. **Why:** the recorder kept the entire trace in process memory until `xrEndSession` or
`xrDestroySession`; neither callback is a reliable persistence boundary when a game terminates without
orderly OpenXR teardown. **Never again:** the bounded telemetry worker checkpoints and durably flushes
new records every second, the reader ignores a partial trailing record, and lifecycle callbacks are
final-flush optimisations only. File I/O remains outside OpenXR hooks and D3D rendering.

## R24 — Alarm latches extended their own recovery forever

**What:** a brief critical reading could leave any alarm-only symbol visible and red after its metric
recovered. GPU made this obvious in a flat 90 FPS DiRT Rally run: an ordinary excursion to 90% armed
the symbol, which remained red around 74–75%. **Why:** recovery waited for a hold deadline while the
still-latched critical display state rewrote that deadline on every update. Entry/recovery also counted
display updates rather than elapsed time, and GPU shared an over-eager generic 90% critical threshold.

**Never again:** every symbol uses the executable `UpdateSustainedAlarm` time policy; lower-state
fluctuation cannot reset critical recovery, the hold starts once from the first non-critical input,
and GPU defaults to 90% warning / 98% critical. The policy fixtures pin brief entry, sustained entry,
fluctuating recovery and finite hold expiry.

## R22 — Automatic Topmost replaced a proven renderer without compositor-layer demand (4.1.208–209)

**What:** Pistol Whip lost the visor and calibration while HUD/Trace presentation regressed.
**Why:** automatic policy selected the separate composition backend for an ordinary single projection
layer even though there was nothing application-owned to occlude ViewLab. Pixel calibration was also
misdirected into ViewLab's separate target. **Never again:** projection-only sessions remain direct;
Topmost demand requires a distinct application layer and is executable policy-tested. Calibration
measures the submitted game texture. Backend transition prepares one frame before submission.

## R23 — Desktop cadence and stale UI choices leaked into VR presentation (4.1.209)

**What:** notification animation stepped, some graph modes drew nothing, Alarm-only stayed visible,
HUD wrapped at four, its gaps ignored scale, and racing test buttons were gated off.
**Why:** animation floats were published at 30 Hz and alpha applied twice; graph mode retained an
incompatible channel; trace considered unrelated widget alarms; a legacy row limit/non-scaling gap
remained; presentation tests followed production enable gates. **Never again:** `RenderPolicy.h`
owns executable graph/trace/layout/animation/backend policy fixtures; racing tests carry temporary
mapping flags and never mutate production settings.

## Hardware queries on the XR/D3D thread

**Never:** call PDH collection/enumeration, DXGI budget queries, memory APIs, WMI, ETW or vendor sensor
APIs from HUD geometry, swapchain release, `xrEndFrame`, or either eye draw. The hardware worker owns
those calls and the renderer consumes `TryGetSnapshot`. Contention or provider failure means retaining
the previous/unavailable value—not waiting, retrying per frame, or fabricating zero.

## R20 — Topmost swapchain churn can collapse the user-mode display stack

**What:** During a DiRT Rally menu-to-car transition, Topmost recreated its large projection
swapchain for every submitted `imageRect` size change, including a one-pixel oscillation. Once
allocation failed it retried 193 times in about 21 seconds while the D3D device and DWM failed.

**Never again:** Topmost receives one allocation attempt per session, based on stable underlying
texture capacity. It never reallocates for submitted-rectangle jitter, retries a failed session,
re-arms by checkbox cycling, or destroys failed resources from the render path. Device removal
disables every ViewLab D3D draw until the next session. `Tests/Verify-TopmostSafety.ps1` pins this.

## R21 — Runtime blocking masqueraded as SYS load

**What:** SYS jumped between roughly 10% and 90% in stable scenes and repeatedly activated
alarm-only mode. **Why:** it measured from entry to `xrBeginFrame` through return from `xrEndFrame`,
including variable runtime/compositor/driver blocking, then labelled the result like system pressure.

**Never again:** APP measures the explicit begin-return→end-entry application window and divides it
by the cadence-aware budget. Hook wall times are named by their boundaries; GPU frame time remains
deferred until a valid timestamp-query design exists. `Tests/Verify-PerformanceHud.ps1` pins this.

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

## R8a — Visibility-mask filtering emptied one eye and crashed Elite Dangerous
**What:** VDXR returned one valid hidden-area triangle per eye. ViewLab classified triangles using
an assumed mirrored centroid sign, retained the left eye's three indices, and returned zero indices
for the right eye. Elite Dangerous deterministically null-wrote while consuming the startup masks.
**Why:** the optional outer-edge filter had no fail-open invariant for indivisible or unfamiliar
runtime topology.
**Never again:** pass one-triangle masks through verbatim and never replace a non-empty runtime mesh
with an empty result. Contract tests pin both guards; headset verification must include an OpenVR
title through OpenComposite as well as native OpenXR titles.

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

## R20 - A standard overlay must not be startup-only (fixed 2026-07-15)

**What:** clock and sticky-note enable, placement, scale and opacity appeared beside ordinary live overlay
controls but only changed after the game recreated its OpenXR session. **Why:** their first implementations
read INI values only in `LoadConfig`, outside the shared live-state plumbing. **Never again:** standard
overlay controls publish through a versioned generation-last contract; bounded text collections receive a
dedicated contract. Tests pin both sides of the binary layout and preserve INI fallback when the UI is absent.

## R21 - Preview scale must not be independently clamped per axis (4.1.237; fixed 2026-07-15)

**What:** moving the yellow Scale handle, or changing the matching slider, could appear to resize only one
axis of a preview node. **Why:** callers baked scale into guessed width/height values and the canvas then
clamped width and height independently to unrelated pixel minima and maxima. Trace additionally treated its
standard Scale control as height-only, and Trace height silently inherited HUD scale. **Never again:** preview items carry unscaled reference footprints;
one resolver applies scale once to both axes and uses a single aspect-preserving fit factor. Trace Width owns
shape while its independent Scale affects the complete graph. Fixtures prove half/double scale on both axes and constant ratio.

## R22 - Crop must not deform or relocate overlays (reported 4.1.238; fixed 2026-07-15)

**What:** reducing horizontal crop widened/fattened preview footprints, could move their anchors, and a roughly
0.4 horizontal Pinball FX session visibly deformed the HUD. **Why:** preview layout used the crop rectangle as
its coordinate and sizing area. Native anchors used selected-FOV `RenderArea`; HUD caps and notification width
also used cropped dimensions, so extreme crops could alter otherwise independent features. **Never again:**
preview layout/hit-testing uses the full reference area, native ordinary overlays use `FullLens`, and full-widget
clamps/caps use projected full-lens bounds. Crop remains a coverage boundary. Contracts pin FullLens anchors,
crop-independent preview sizing, full-bounds caps and an unclamped full-space crosshair target.
