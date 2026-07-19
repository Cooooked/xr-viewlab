# STATE — live project state

> Single source of truth for "where are we". Update this file in the same commit as any
> behavior change. Do not create handoff/status/session documents — this is the only one.

**Updated:** 2026-07-19
**Current version:** 4.1.278 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.278.msi` (size 149,184,512 bytes; SHA-256
`72D0DDC8B2CAAB47F359041F1DB9C93E79CB43BB186405F314C37E7533346B19`). Adds the Rear-Closing Pressure Cue and the
Grip-O-Bar wired end-to-end (provider telemetry + per-car calibration -> racing state contract v2 -> native glow/bar
-> settings/persistence/preview), on top of 4.1.277's race-start light. Full WPF/broker/signed-identity/x64+Win32
native/OBS-plugin/MSI build 0/0; extracted-payload validation passes; all 24 deterministic scripts pass. Live
iRacing/headset validation pending.
**Prior version:** 4.1.277 — Race-Start Border Light wired end-to-end (`dist/ViewLab-4.1.277.msi`).
**Older:** 4.1.276 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.276.msi` (size 149,151,744 bytes; SHA-256
`B66D945C49A0589D48DA0C2CFF92DB8BF00E3BAC1C380AA2BAC70E7EEC2D0027`). Preview repairs: the crosshair now converges at
the post-crop (crop/visor) centre rather than the full-lens box, and the Optical-centred checkbox is a content-only
upward shim (crop, visor, crosshair and widgets shift up by 0.077 of frame height; the fixed 55:48 frame viewport,
periphery guides and labels never move) instead of the old whole-viewport refit that read as a pan. The permanent
`WidgetPreviewShimY = 0.077` widget correction remains a separate transform. Performance Trace's dedicated Reset
Position button is removed (per-slider right-click reset remains). Full WPF/broker/signed-identity/x64+Win32
native/OBS-plugin/MSI build 0/0; extracted-payload validation passes. 23 deterministic scripts pass; the
`Invoke-RealNotificationFixture` integration test needs a live broker process and is environmental. Headset/OBS/iRacing
live validation remains pending.
**Prior version:** 4.1.275 — per-app per-overlay inheritance, deterministic new-iRacing-cue logic, OBS
"ViewLab Mirror Capture" identity (`dist/ViewLab-4.1.275.msi`).
**Branch workflow:** `master` is the stable validated integration branch; `dev` is the sole ordinary
AI working branch. Experiment branches are created only at the user's explicit request. The disconnected
remote `main` history is not used. Force pushes, history rewrites and branch deletion require explicit approval.
**Validation state:** recent builds received repeated manual Pistol Whip and DiRT Rally 2 headset
testing, but the old state log failed to attach every observation to an exact build. 4.1.103 is the
narrow confirmed reference for its stencil repair, not the last headset-tested build. See
`VIEWLAB_VALIDATION_HISTORY.md`; 4.1.202 has real packaged-notification desktop validation and
4.1.209 failed its first precise Pistol Whip headset matrix. 4.1.210 repairs those observed failures
and has clean contracts/fixtures plus WPF, broker, x64/Win32 native, signed identity package, MSI and
extracted-payload validation. Build 4.1.224 additionally passes the full deterministic suite and fresh
WPF, broker, signed identity, x64/Win32 native, MSI extraction, pinned PresentMon hash/notice validation;
its DiagMon real-game CSV and live Trace-cap checks remain mandatory before release.
**Publish state:** 4.1.252 published at the user's direction (2026-07-18): https://github.com/Cooooked/xr-viewlab/releases/tag/v4.1.252.

## Rear-closing and Grip-O-Bar wired end-to-end (implemented; live iRacing/headset validation pending, 2026-07-19)

Both remaining new iRacing cues are now connected through the whole product, not just calculation classes.

**Rear-Closing Pressure Cue (item 4).** The provider derives the nearest-car-behind distance from the
`CarIdxLapDistPct` array + `PlayerCarIdx`, converted to metres via `TrackLength` parsed from the session-info string
(nominal fallback). It runs the shared `RearClosingCue` state machine at telemetry cadence (target-change protection,
hysteresis, fades) and publishes a quantized packed state (active/opacity/width/intensity) only on change; any spotter
side means overlap, so the cue clears and the existing spotter takes over. `RacingStateService` writes the packed word
into `RacingStateBlock.reserved1` (offset 60). Native draws a restrained top-centre glow whose width grows with
proximity and brightness with closing speed — no side inferred. Enable + glow opacity persist as `iracing_rear_closing*`.

**Grip-O-Bar (item 6).** The provider reads Steering/Speed/YawRate/VelocityX/Y, resolves the player's car path from
the session string, and keeps a versioned per-car calibration in `%LOCALAPPDATA%\XR ViewLab\grip-calibration.json`
(`GripCalibrationStore`; foreign-schema records are dropped on load, periodic durable save, UI reset button). It
accumulates the calibration only from clean higher-speed samples and runs `GripOMeter` (calibrated expected-vs-actual
yaw + lateral slip → whole-car direction, severity band, understeer/oversteer/sideslip dominance; low-speed and
uncalibrated suppression). The packed state (active/dominance/direction/severity) rides a **grown racing contract v2
(68 bytes)** in the new `gripState` word at offset 64 — native and the C# service both moved to v2 and reject other
versions. Native draws a lower-left or lower-right peripheral bar coloured yellow/orange/red by severity; it never
claims an individual tyre. Enable + bar opacity persist as `iracing_grip_bar*`.

Both cues gate under the existing racing-cues mirror feature and show the iRacing edge placeholder in the desktop
preview when enabled. Fixtures cover the state machines, the race/rear phase logic and the calibration store's
persistence/migration/reset; contracts pin each UI→provider→racing-state→native chain. Managed + broker + native
x64/Win32 compile; all deterministic scripts pass. Live iRacing driving and in-headset appearance remain pending.
The iRacing connection HUD icons (item 5 of the original list) and the OBS shared-frame producer remain not started.

## Race-start border light wired end-to-end (implemented; live iRacing/headset validation pending, 2026-07-19)

The Race-Start Border Light (item 5) is now connected through the whole product, not just a state machine.
`RaceStartFlags.Phase` (a shared, deterministically-tested helper in `IRacingCues.cs`) maps iRacing
`SessionFlags` (startReady/startSet/startGo, green) to a latched phase — 0 inactive, 1 waiting/red, 2 started/green.
The provider computes it per sample, publishes a `ViewLabEventKind.RaceStart` on change, and resets the latch on
session change; joining a race already green (no waiting phase, no rising edge) never flashes green, and replay/
garage/reconnect/tick-reset cannot trigger it. The broker forwards the event to `RacingStateService`, which writes
the phase into the `RacingStateBlock` `reserved0` slot (offset 44, no contract version bump — old layers ignore it).
Native reads `reserved0` and draws a restrained inner border: red while waiting, green when started, with a
native-owned hold+fade envelope (so telemetry ticks cannot replay it). Enable plus green-hold ms, border thickness
and red/green opacity persist as `iracing_race_start*` keys, resolved through the profile/global INI (so per-app
profiles carry them); the desktop preview shows the iRacing edge placeholder when enabled. Managed + broker + native
x64/Win32 compile; the cue-logic fixture adds standing/rolling/join-in-progress/re-arm phase assertions and contracts
pin the full UI->provider->racing-state->native chain. Live iRacing driving and in-headset appearance remain pending.
The Rear-Closing cue and Grip-O-Bar retain complete calculation logic + simulations but are NOT yet rendered natively.

## Per-app inheritance, new iRacing cue logic, OBS identity (implemented; headset/live validation pending, 2026-07-19)

**Per-app editor parity + per-overlay inheritance (items 23, 24).** The per-app editor reuses the global overlay
catalogue and the tag-based `feature:key` override system (no second settings architecture). It now also carries the
per-metric HUD `unit` override (item 16 parity). Each overlay section (Clock, Performance HUD, Performance Trace,
Sticky Notes, Crosshair, Notifications) has a **Use Global Values** checkbox. An overlay inherits exactly when it has
no override keys: ticking the box calls `OverlayProfileOverrides.ClearFeature(feature)` (removing only that overlay's
keys, so it follows future global changes) and disables its local controls; unticking seeds that overlay's keys from
the current effective values (`EnsureFeatureCustom`) and enables editing. Editing any control auto-unticks its box.
The existing whole-profile `Use Global Values` bulk action and the separate visor inheritance are preserved. A WPF-free
console fixture proves override creation/deletion, global propagation while inherited, local stability after inheritance
is disabled, per-overlay isolation and existing-profile migration; repository contracts pin the model and UI wiring.

**New iRacing cue logic (items 4, 6, 7).** Implemented as shared, deterministic, WPF-free state machines in
`IRacingCues.cs` (no drawing, no wall-clock — callers pass dt), so the same logic drives preview and runtime and is
fully simulated. `RearClosingCue`: tracks the nearest valid car behind, derives smoothed closing speed from distance
change, protects against target-identity changes (no false spike), activates/deactivates with hysteresis and minimum
persistence, maps distance->glow width and closing speed->intensity, and clears on side overlap so the existing spotter
takes over (never infers a side). `RaceStartLight`: red while officially waiting, one latched green at the official
start, configurable hold then fade, re-arm on session change; replay/garage/disconnect/tick-reset never trigger green.
`GripOMeter` + versioned `GripCarCalibration`: compares actual yaw with a calibrated expected yaw (per-car yaw gain
accumulated from clean higher-speed samples, bounded) plus lateral-vs-forward slip, reports whole-car direction
(lower-left/right) and severity (yellow/orange/red via a shared `SeverityBand`), classifies understeer/oversteer/
sideslip dominance, suppresses at low speed and when uncalibrated — never claims an individual tyre. A 27-assertion
simulation fixture covers every listed state. Native rendering, persistence, UI and live provider wiring are the
documented next step; the calculation logic and mappings are complete and validated.

**OBS ViewLab Mirror Capture identity (item 8).** The plugin registers the unique stable source id
`viewlab_mirror_capture` with display name "ViewLab Mirror Capture"; the module name is now also "ViewLab Mirror
Capture" (the last legacy "ViewLab Mirror" string is gone). It never reuses the third-party "OpenXR Mirror Capture"
id/name, so both sources coexist. The ViewLab UI now tells the user to add the "ViewLab Mirror Capture" source (not
"OpenXR Mirror Capture"). Contracts assert the unique id, the final display name and the absence of a copied id. The
layer-side shared-frame producer (item 9) remains the next step; live OBS visual confirmation is pending.

## Notification redesign, visor RGB colour, calibration pack review (implemented; headset validation pending, 2026-07-19)

**Notification theme redesign (item 15).** The four compositor designs (composited in `NotificationService.ComposeCard`)
now have genuinely distinct footprints and structure: Classic 336x92 (10px round, leading accent bar, 44px icon,
app-name caption above the title, two-line body), Compact Banner 336x44 (single dense row, 24px icon, bottom accent
underline, no app name), Minimal 288x72 (square, text-only, hairline frame, left tick, tiny app-name label, airy
hierarchy) and Bold 336x96 (16px round, tall filled top accent band carrying the app name, 56px icon, heavy 18px
title). App-name placement, corner radius, accent position, icon treatment and typography scale all differ per design;
because each card carries its own height the native layer stacks them at different densities. Theme/Palette separation,
privacy modes, artwork/icon handling, queue/duration/opacity/max-visible are unchanged. Contract fixtures pin the
four distinct footprints and the per-design app-name treatment.

**Configurable visor mask colour (item 21).** The visor fill is no longer hard-coded black. `kVisorPS` reads a
`VisorColor` constant buffer (register b0) and `DrawVisorBorderToTexture` binds a dedicated dynamic `visorColorCb`
before every visor draw, so the colour flows through the one shared visor path into direct, ordered/topmost, OBS
mirror and calibration-capture output alike. The colour is `0x00RRGGBB`, default 0 = black (existing behaviour
unchanged). It is persisted as `mask_color`, resolved through the same profile/global INI as the other visor keys
(so per-app custom visor profiles carry their own colour) and also published live: the live-state contract grew to
**v11 / 272 bytes** with the colour in the new tail; the native `LiveStateBlock` matches (`static_assert ==272`) and
rejects any block whose version/size is not exactly v11/272. The UI adds R/G/B 0-255 sliders with numeric readouts,
a live swatch, right-click reset and Black/Magenta/Green/Cyan presets (true magenta is 255,0,255; whether a streaming
environment-blend detects it is explicitly not assumed). Managed + x64 + Win32 all compile; contract fixtures pin the
shader cbuffer, the draw-time binding, the grown struct and the UI. Only in-headset visual confirmation of the colour
remains.

**Calibration screenshot-pack review (item 22).** `CalibrationPackReview` (WPF-free, so it is deterministically
testable) scans `%LOCALAPPDATA%\XR ViewLab\CalibrationCaptures`, clusters captures into packs by capture-run time
proximity (a single run can straddle a second boundary, as the user's real pack did), matches each PNG with its JSON
sidecar, parses PNG dimensions straight from the IHDR, cross-checks them against the metadata, computes SHA-256 and
size, flags missing patterns / blank-or-suspicious captures (decode-free bytes-per-megapixel heuristic) / dimension
mismatches / unexpected eye, records each pattern's diagnostic purpose, compares two packs and renders a concise
report — never modifying the raw files. It states plainly that captures are the PC-side submitted left-eye image, not
headset optics or encoded output. A `Review capture pack` button in the Calibration menu shows the report via the
built-in help window. The user's real 10-pattern pack (Eleven Table Tennis, 2419x432) reviews as COMPLETE. A console
fixture (`Invoke-CalibrationPackReviewFixtures`) asserts completeness, missing-pattern, blank, dimension-mismatch,
wrong-eye and comparison detection; repository contracts pin the read-only workflow and its UI wiring.

## Per-metric HUD units, crosshair preview aspect, Trace baseline cleanup (implemented; headset validation pending, 2026-07-19)

**Per-HUD-icon unit visibility (item 16).** Every Performance HUD metric now has an independent unit checkbox
beside its Symbol checkbox. Unchecking hides only the unit suffix (`%`, `ms`, `FPS`, `MHz`, `GB`, …); the value
stays visible. Persisted globally as `hud_widget_<id>_unit` (default true, so existing configs are unchanged) and
carried through per-app overlay overrides. Metrics whose `Unit` is empty (e.g. the NetworkStatus state label)
expose `HasUnit=false` and their checkbox is disabled — the unit-suffix path is never forced onto them. The
`TelemetryConfigV1` extension mapping publishes a `unitHiddenMask` in its reserved tail (offset 56, bit set =
hide) with NO version bump: a zero/legacy value means all units shown, so an older native layer harmlessly
ignores it. The native renderer reads `reserved[0]` and omits the suffix (and its space) for masked metrics; the
ring layout is fixed per widget and `drawText` centres, so no blank gap appears. Model/XAML/persistence/live-state
/native and HUD+contract fixtures are wired and pass; in-headset legibility remains to confirm.

**Crosshair preview aspect (item 18).** The desktop crosshair preview was stretched because it derived separate
X/Y reference-pixel factors from the 55:48 preview area. It now uses one uniform factor
(`Quest3PreviewGeometry.TangentReferencePixelsUniform`) for arm length, thickness, gap and outline in both axes,
matching the native uniform `scale × eyeHeight/1080` mapping, so the preview crosshair is square and centred.
Position still uses the shared centred offset; native rendering is unchanged. Contract fixture pins the uniform
geometry.

**Performance Trace baseline cleanup (item 17).** The dark base/centre reference line (drawn at `traceBottom`
in absolute modes and `traceCentre` in deviation mode) is removed in every trace mode — this was the user-reported
"black line at the base / through the centre". The coloured data channels, budget-relative auto-scaling and cyan
numbered event markers are unaffected. A contract asserts the line no longer exists. Performance Trace already
shares the standard overlay controls (position/scale/opacity sliders with right-click reset, and the shared
`OverlayResetPosition_Click` button) with the Performance HUD, so no control realignment was required; its genuinely
Trace-specific controls (Width, History, Sensitivity, Mode, alarm-only visibility) are retained.

**iRacing control plumbing audit (item 19, partial).** Every persisted iRacing key was traced UI→INI→consumer.
The spotter (glow/width/strength/opacity/fade/color) and flag (border/width/opacity) keys are read by the native
layer at session start (`dllmain.cpp` ~4910-4916); the fuel (`iracing_fuel_warning`,
`iracing_fuel_warning_threshold_pct`) and lap (`iracing_lap_duration_ms`) keys are read by the notification broker
and iRacing provider (`NotificationBroker/Program.cs` ~81-83, 124-126). No orphaned "saves but ignored" key was
found in these groups. The one real UX caveat is that spotter/flag tuning is applied at session start rather than
live, matching the UI's "an active game picks it up at session start" status text; making it live would require
adding those fields to the live-state contract. A full sweep of any remaining lap-card/position/delta controls and
their preview/runtime parity is still open.

## Calibration capture, preview centre repair, OBS mirror routing, ViewLab Mirror plugin, themes/palettes (implemented; runtime validation pending, 2026-07-19)

The calibration suite's native backend is implemented. The UI owns a small read/write `XRViewLabCalibrationCapture`
control block; the layer stamps a heartbeat every submitted frame and answers request serials by copying the final
submitted left-eye sub-image at xrEndFrame (game pixels, crop, visor, calibration pattern and direct-drawn features;
ordered-carrier features are composited onto the copy), then a worker thread encodes a real PNG plus a verified JSON
metadata sidecar into `%LOCALAPPDATA%\XR ViewLab\CalibrationCaptures`. Each pattern settles for six observed submitted
frames rather than a wall-clock guess. Failure codes are truthful; cancellation/timeout serials stop accepted worker work
and remove partial output; prior calibration state restores in `finally`; metadata failure cannot report success.

The preview centre contract is now: every layer (frames, crop, visor, guides, crosshair, widgets, edge cues, lens
outlines) anchors around the geometric centre of the fitted 55:48 area, and the persisted optical-centre checkbox
translates that COMPLETE area rigidly via `FitAreaAtCentre(RenderSize, OpticalPreviewCentreY)` — translate only, no
zoom, resize or re-anchoring, identical in main and per-app previews. The shared editor now exposes a live hover inspector
for both full-lens and post-crop normalized coordinates using the same inverse transform as dragging. This repairs the regression where the crop was
still anchored at the optical Y with a crop-dependent compensation while guides, crosshair and widgets did not move by
the same amount. `RuntimeCropRect` is the half-lens split around 0.5 pinned by the split-crop checkpoints.

`Show in OBS Mirror` routing is fixed at its capture point. The MIT-licensed Jabbah/OpenXR-Layer-OBSMirror source
proves the mirror layer queues its compositor→sharedHandle[0] copy at xrEndFrame and only flushes it (and advances
lastProcessedIndex) at the NEXT xrBeginFrame, so ViewLab's former post-xrEndFrame drawing was overwritten at the start
of every frame. `DrawObsMirrorSurface()` now runs in `XRViewLab_xrBeginFrame` after the call returns up the chain, so
the selected features stay on the displayed texture for the whole frame in either implicit-layer order. All ten
checkboxes remain independently respected; headset visibility is unchanged. Live OBS validation remains pending.

`ViewLabMirrorPlugin/` is a real buildable OBS source plugin skeleton (`viewlab-mirror.dll`, GPL-2.0-or-later): it
registers the `ViewLab Mirror` source with an eye-mode property UI, resolves every libobs entry point at runtime from
the host's obs.dll (no SDK link), reports the host's own version from `obs_module_ver`, and consumes the versioned
`Local\XRViewLabMirrorSurface` triple-buffer contract in `viewlab_mirror_contract.h`. The layer-side producer is the
documented next step; until it ships the source renders nothing rather than fabricating frames. The MSI carries the
plugin under `ObsPlugin\`, and the Overlays menu's `Install ViewLab Mirror Plugin` button installs per user into
`%APPDATA%\obs-studio\plugins\viewlab-mirror\bin\64bit` with installed/outdated/missing SHA-256 detection and a
restart-OBS notice. OBS is never launched or controlled.

The `Show Quest 3 Lens Outlines` preview feature was designed and then removed within this same unreleased batch:
the photo-digitised truncated-circle path was visually inaccurate (Meta publishes no lens CAD) and low value. All of
its plumbing is gone — Preview submenu toggle, main/per-app rendering, `Quest3LensOutlines` geometry,
`ShowQuest3LensOutlines` state and the `preview_lens_outlines` load/save. An old saved `preview_lens_outlines` key is
harmlessly ignored. No replacement approximation was drawn. Contract tests now assert the feature no longer exists.

Clock and Notifications now separate Theme (actual visual design) from Palette (colours only). The five legacy
recolours are palettes; Clock designs are Classic Card, Minimal, Terminal and Banner (native layouts with different
type scale, padding, borders, icons and information arrangement), and Notification designs are Classic, Compact
Banner, Minimal and Bold (compositor layouts with different dimensions, corner shape, icon treatment, typography and
accent placement). Live state moves to v10 (268 bytes) adding clockPalette; `clock_widget_palette` and
`notify_palette` persist globally and through per-app overlay overrides. A missing palette key migrates the legacy
theme value into the palette and resets the design to Classic (raw ini probes bypass the factory-baseline fallback so
migration still triggers on legacy configs); the captured baseline now records theme 0 + palette 2 for the clock.
Enabled state, position, scale, opacity, hotkeys and preview/runtime behaviour are unchanged.

Build 4.1.270 passes all 20 deterministic scripts plus fresh WPF, broker, signed identity, OBS plugin, x64/Win32 native,
MSI and extracted-payload validation. Headset/OBS/calibration runtime validation remains pending. Detailed planning-only designs for
the Rear-Closing Pressure Cue, Performance HUD connection icons and Grip-O-Bar are recorded in
`IRACING_IMPLEMENTATION.md`; none of those three planned features is implemented.

## Compact overlays, app overrides and captured factory baseline (implemented; headset validation pending, 2026-07-18)

The six configurable overlays are Clock, Performance HUD, Performance Trace, Sticky Notes, Crosshair and Notifications. OBS
Recording Cue and iRacing Telemetry remain feature modules with global detail settings and per-app enable overrides.
The global Overlays menu presents all eight configurable/detail-bearing sections with one aligned checkbox,
clickable label, chevron and divider pattern. The HUD label is now Performance HUD. OBS WebSocket settings live
inside its collapsed section; iRacing settings are grouped by lap, spotter, flag and fuel features, including an
RGB spotter-colour editor backed by the existing colour key. The ten unchanged OBS Mirror visibility switches are
last in the menu. Presentation tests remain independent of a live iRacing connection.
Boundary Flash remains the transient HUD/Trace drag guide. The iRacing edge-feature preview follows the solid
post-crop render rectangle rather than the dotted full-lens guide; native cues already draw into the submitted
post-crop eye rectangle. The OBS Recording Cue preview also follows the exact post-crop bounds in main and per-app
editors, with its label at bottom-left while iRacing remains top-left. Ordinary desktop widget previews match the
native `OverlayPlacement::FullLens`: saved X/Y and inverse dragging use the full-lens rectangle, while post-crop
coverage clips visibility without redefining position. The failed crop-relative pass compressed restarted positions
into the centre band and amplified vertical dragging by 6.67× at 15% crop. The entire preview now uses one shared
centre: by default frame/eye guides, equal split crop, visor, crosshair, widgets and render-edge cues remain aligned
around the geometric centre. A persisted Preview-menu checkbox can instead move that complete coordinate system
together around the alternate optical centre; this is display-only and shared by main and per-app previews.
Main and per-app editors share `BeanMaskEditor`; zoom and pan
remain display-only. HUD, Trace, Clock and Notification preview footprints retain calibrated size conversion without
changing that shared centre. Runtime rendering and saved coordinates are unchanged. Focused restart, forward/inverse,
small-delta, split-crop, profile and preview/runtime numerical fixtures pass; headset validation remains required.
Per-app values use canonical INI keys stored as
`overlay_override_<feature>__<key>` registry strings; layout uses `overlay_layout_<feature>_{x,y,scale}`. Profiles
inherit globals until edited, and `Use Global Values` deletes all overlay/module and layout overrides. Native feature
masks prevent global live snapshots from replacing an active app's settings.

OBS setup uses the existing `obs_websocket_url` and `obs_websocket_password` persistence contract, presenting the
endpoint as Host/IP plus Port and composing it back into one WebSocket URL. Built-in help covers local and LAN OBS
configuration. The provider publishes Disconnected, Connecting, Connected or Authentication failed; only its distinct
Recording state activates the native cue, so connection and authentication failures remain safely off.

The shared visor preview treats a non-widget right-click as view navigation reset: zoom returns to startup identity,
pan clears, and the existing `FitArea` render path again aligns the outer frame. No crop, visor, overlay or profile
value is changed, so main and per-app previews receive identical behavior without runtime effects.

Nose controls remain serialized but are hidden. The launcher is `DiagMon ▾`; its popup remains `DiagMon(ster)`.
The experimental calibration suite has a cancellable ten-pattern sequence and left-eye capture interface; the
unavailable backend creates no fake files and state restores in `finally`. `experimental_draw_in_void` is persisted
and read at startup but deliberately has no rendering effect.

The captured configuration is canonical in `config/factory-baseline-v4.1.255.json`. It drives clean-install values,
missing-key fallbacks and a one-time `FactoryBaselineAppliedVersion=4.1.255` migration. Per-app profiles, ReShade
deployment/registration/handshake and unlisted settings are preserved.

The full 4.1.263 WPF, broker, signed identity, x64/Win32 layer and MSI build completed with zero warnings/errors.
Focused OBS protocol, preview/profile, overlay and repository contracts pass; MSI payload/hash validation confirms
fresh WPF/native/broker outputs, pinned PresentMon and signed identity content. Headset/runtime validation remains.

## Per-app Visor Mask parity (implemented; headset validation pending, 2026-07-18)

The per-app editor now uses the main editor's current Size, Width, Height, Curve, Outer Dip, Nose and Nose Spread X
controls, ranges, defaults, live `BeanMaskEditor` geometry and red enabled-state styling. Custom profiles persist
their own `mask_enabled` and complete visor shape; reopening reloads those registry values and the native layer
applies them only when `visor_size>0`. `Use global visor settings` keeps the crop/resolution profile but writes the
zero sentinel and removes custom visor keys so the complete global visor configuration remains authoritative.
Focused profile/preview and repository contracts pass. The full 4.1.254 WPF, broker, signed identity, x64/Win32
native and MSI build plus deterministic installer payload validation completed with zero warnings or errors.

## Split vertical crop preview repair (implemented; headset validation pending, 2026-07-18)

Top and Bottom split controls now scale within their respective half-lens rather than each consuming a full-lens
fraction. The four pinned checkpoints are `1/1` full height, `1/0` top half, `0/1` bottom half and `0.5/0.5`
the centred middle half. Existing `top_tangent`/`bottom_tangent` INI and registry values remain full-lens shares;
main and profile editors convert ×2 on load and ×0.5 on save so native runtime behaviour and old settings remain
compatible while controls, hints and preview agree.
Focused split-geometry and repository contracts pass. The full 4.1.253 WPF, broker, signed identity, x64/Win32
native and MSI build plus administrative payload extraction completed with zero warnings or errors.

## ReShade/DiagMon help and OBS mirror routing (implemented; runtime validation pending, 2026-07-18)

ReShade Remote now has concise built-in help, independent Install/Uninstall and Enable/Disable actions, four
truthful states, and a post-attachment heartbeat requirement for Connected. File actions touch only the two
ViewLab payload paths; registration actions touch only ViewLab's 64-bit manifest value. DiagMon(ster) now has a
matching white circular help icon and scrollable capture, graph, interpretation and export guide. The inherited
healthy `[Installed and enabled]` state uses normal text rather than warning yellow; Connected alone remains green.
OBS work routes selected ViewLab overlays to `OpenXROBSMirrorSurface` via live-state v9 without changing headset
visibility. WPF and native x64/Win32 direct builds plus contracts pass; runtime validation remains pending.

Focused ReShade Remote follow-up removes the duplicated payload explanation from the main panel, gives the
complete `In-HMD Menu Quad` section readable vertical space with scrolling fallback, and names the Desktop Menu
controls explicitly. Fresh control mappings now start the desktop preview focusable (`win_headless=0`) instead of
silently forcing headless/borderless mode, and the Remote reapplies displayed state only when the shared revision
changes. Static payload contracts confirm Home (`KeyOverlay=36`), `Local\ReShadeXRControl`, the persisted quad
transform and OpenXR overlay route remain present. The Remote height now follows its visible content at the
existing width, capped to the current work area with its existing scroll viewer handling unusual DPI overflow.

The exact modified payload source was recovered from `F:\AI-Projects\ReshadeAI\reshade` into the canonical
`ReShadePayloadSource/` directory. Its pre-recovery Release DLL was byte-for-byte identical to the bundled DLL
(SHA-256 `2307754A416C9F73CA9DD84BBC8C418FB67B325B5CFBC2F8F08D8AADF1590EC1`). Provenance is upstream
commit `4a50d1eddace85734871d91792ff214f13f66c01` plus the recorded dirty-file inventory in
`ReShadePayloadSource/README.ViewLab.md`. The desktop mirror now forwards Win32 text/key input to ImGui, retains
and joins one reference-counted window thread, paints an explicit black frame without class-brush erasure,
invalidates only for new content/control changes, and uses two staging textures so `xrEndFrame` maps the prior
completed capture instead of the texture copied that frame. The focused x64 ReShade build, all 18 deterministic
test scripts and the full 4.1.252 WPF/broker/signed-identity/x64/Win32/MSI build pass. Administrative extraction
proved the MSI contains the exact canonical ReShade DLL (SHA-256
`211C217BC4A9ED342C2D8B0C46530F118973F728800ED061BFFE7B4598D1412B`); the MSI SHA-256 is
`89BCCEA4AE47CDEDC1627AD48F2585AFF5557481407CB2D164CAD7B5936611CD`. Live desktop/HMD validation remains
required before publication.

## Profile persistence and centred Quest 3 preview (implemented; headset validation pending, 2026-07-18)

The profile editor now distinguishes the visor-only `Use global visor settings` checkbox from the explicit
whole-profile `Use Global Values` action. Normal Save always persists crop/resolution overrides and enables the
profile; a global visor is represented only by `visor_size=0`. The main application list reloads from the registry
after saving. Settings startup now publishes once after hydration so all enabled widget previews are present without
waiting for a toggle.

The Quest 3 preview is one centred fixed `55:48` outer box, not a projection-degree diagram. Horizontal and vertical
values map directly into it: horizontal `0.8` is exactly 80% wide and symmetric vertical `0.15` is exactly 15% high.
Split top/bottom values translate the direct crop. The periphery layer toggles between one binocular oval and two
overlapping true circles; both use 90% of box height and 85% of box width. Crop, visor geometry, widget anchors,
hit-testing and drag deltas share the same full-box normalised space;
crop is coverage only and is never applied twice. The runtime outer-only crop now also retains the exact configured
fraction, rather than converting `0.8` to an effective `0.9`.

The frame guide has its own independent display choice: one combined binocular rectangle or two overlapping
per-eye rectangles preserving the actual `2064:2208` eye aspect. Frame mode and periphery mode are guides only;
neither writes to the layer or changes crop. A persisted preview IPD helper defaults to `67.0` mm, updates live
in 0.1 mm steps, and changes only the centre separation/overlap of dual guide geometry.

The preview's product purpose is trustworthy desktop tuning: the user can read full frame, useful periphery,
post-crop coverage, final visor and overlay placement/scale without repeatedly putting the headset on. Runtime
remains the source of truth; the preview is its calibration mirror, not an independent approximation.

Focused 2026-07-18 follow-up: both preview editors now show visible IPD up/down buttons using the existing
0.1 mm live/persisted step. Preview vertical state is stored as one direct scale plus centre, and full `1.0`
is canonicalised to the outer frame's exact top and height. Clock + Timer and Notifications alone now accept
scale `0.1`; their sliders provide the preview pin bounds, and native startup/live clamps use the same minimum.
No runtime crop, horizontal mapping, guide mode, profile or other overlay bounds changed.

`mask_nose_spread_x` adds a mirrored nose-boundary translation to the global editor, per-app profiles, live-state
contract and native visor geometry. Its zero default preserves prior output. Deterministic WPF, geometry, plumbing
and native source contracts cover the change. No interactive desktop or headset control was used; this build still
requires the user's in-headset validation before publication.

## Native OpenXR stereo ghosting repair (implemented and matched headset-validated, 2026-07-17)

Pools shows binocular ghosting for direct-to-eye overlays under native OpenXR/VDXR while Pistol Whip does
not; DiRT Rally 2 and Eleven Table Tennis remain good through OpenComposite/VDXR. No title rule is permitted.
Matched build-4.1.242 `PIPE` traces prove both native titles use the same runtime, D3D11, one two-view projection
layer and identical asymmetric runtime FOV values. Across 908 Pools and 914 Pistol Whip submitted frames every
`xrEndFrame` had a same-session/display-time locate match. Submitted FOV never differed from the matched locate;
Pools had only its two startup pose mismatches when the last same-time locate used VIEW while submission used
LOCAL, and all later pose/FOV pairs were exact. Both titles submitted identical left/right orientations on every
captured frame. Every populated release consumed exactly the prior frame's layout with zero age violations.

The first causal divergence is primary-colour swapchain topology. Pools submits left and right eyes from separate
single-slice swapchains (`array=1`, different handles, `imageArrayIndex=0`); Pistol Whip submits both from one
two-slice array swapchain (`array=2`, one handle, indices 0/1). `TrackedSwapchain.eyeViews` is stored per swapchain,
and `XRViewLab_xrReleaseSwapchainImage` passes only that vector into `OverlayCoordinateResolver`. Pools therefore
resolved 1,814 colour releases with `eyes=1` and never supplied the partner eye; Pistol resolved 913 steady colour
releases with `eyes=2`. The intended shared binocular intersection silently collapses to each eye's own asymmetric
FOV in Pools, recreating the equal-normalized-eye-pixel stereo bug. At nominal centre the two independent full-FOV
targets are tangent -0.26864/+0.26864 (30.07 degrees apart), instead of shared tangent zero.

The renderer now retains one immutable ordered `ProjectionFrameContext` for the selected primary projection
layer. Each entry binds the submitted view pose, FOV, full FOV, image rectangle and array slice to its destination
swapchain, while `TrackedSwapchain` owns only texture lifecycle and D3D11 resources. Release-time drawing selects
the target views for the released swapchain but supplies the complete projection view list to
`OverlayCoordinateResolver`. This represents every valid primary-stereo `XrSwapchainSubImage` packing: one array
swapchain, separate swapchains, shared-slice atlas rectangles, overlapping targets in view-array order, and mixed
handle/slice/rectangle layouts. Swapchain destruction invalidates the prior context atomically; release order does
not alter it. Full-lens FOV is now borrowed only from the locate result correlated by session/display time and only
when the submitted FOV matches that located cropped FOV; an application-modified submitted FOV remains authoritative.
No application identity, offset, or title-specific policy exists in this repair. Deterministic topology fixtures,
the complete x64/Win32/WPF/MSI build and post-build contract suite pass. The installed 4.1.243 x64 DLL SHA-256
matched the built payload before runtime validation.

Matched 4.1.243 validation closes the incident. The user confirmed correct fused overlays in Pools, Pistol Whip,
Eleven Table Tennis and both DiRT Rally 2 menu/cockpit states. Pools captured 918 frames and 1,834 populated split-eye
colour releases, all `targetViews=1 projectionViews=2`; Pistol captured 921 frames and 920 populated array releases,
all `targetViews=2 projectionViews=2`. Eleven/OpenComposite captured 911 frames and 1,819 populated split-eye
releases, all `targetViews=1 projectionViews=2`. All three had zero locate misses, stale prior-layout ages or runtime
submission failures. DiRT/OpenComposite submitted a side-by-side projection atlas plus two quads; 903 of the 905
bounded menu frames and every post-confirmation cockpit checkpoint (frames 14,100–15,900) successfully appended the
separate Topmost projection. The two startup menu transition frames remained direct/feature-disabled as designed.
Raw PID-bound evidence and hashes are preserved under `TestResults/RendererPipeline/20260717`.

Bounded verbose `PIPE` instrumentation now records predicted display timing, QPC wait/begin/end timing,
reference-space type and pose, original/cropped locate poses and FOV, exact session/display-time correlation,
every submitted composition layer and projection view, swapchain creation/images/acquire/wait/release, the
layout frame consumed at release, ordered-carrier submission and runtime result. Raw matched evidence is preserved
under `TestResults/RendererPipeline/20260717`. The DiagMonster desktop window could not be automated because
Windows.Graphics.Capture returned `SetIsBorderRequired` 0x80004002; native PID-tagged `PIPE` capture remained
complete and is the authoritative renderer evidence. The instrumentation changes no presentation policy, geometry,
offsets or application-specific behaviour.
Build 4.1.242 completed WPF, broker, signed identity, x64/Win32 native, MSI construction and extracted-payload
validation with zero warnings or errors; the post-build contract suite passes. Automated installation was
initially stopped by Windows Installer error 1730 because the existing per-machine package required administrator
removal during upgrade. The user completed installation; installed and built x64 DLL SHA-256 values matched before
the captures.

## Product polish consolidation (implemented and internally verified; headset validation pending, 2026-07-15)

Ordinary overlay configuration now has one catalogue and one load/save/reset path for clock, Performance
HUD, Performance Trace, sticky note, crosshair and notifications. Each exposes enable, optional None/F6–F12
show/hide bind, X/Y, scale, opacity and reset where applicable. Existing layout keys are unchanged and the
old sticky bind migrates without resetting a layout. Native show/hide state is one central controller; the
working presentation carrier and ordering policy were not changed.

The clock's enable, optional session lane, 12/24-hour format, five themes, layout, opacity and visibility
bind now publish live through contract v8. Sticky notes use a dedicated generation-safe collection contract:
up to eight independently themed square notes with independent enable, position, scale and opacity; the old
single note becomes note one. Notification cards have five compositor themes. HUD label/symbol choice is
stored and published per widget, with labels remaining the migration default. Deterministic contracts and
x64 native/WPF/broker builds pass; headset appearance remains pending.

The visor editor also previews every enabled ordinary overlay: HUD, trace, clock/timer, notifications,
individual sticky notes and crosshair use their configured anchors, while OBS and racing edge cues use
labelled edge placeholders. Ordinary footprints store unscaled reference geometry and receive exactly one
uniform scale plus one aspect-preserving bounds fit; the former independent axis clamps are removed. The
top-centre move and top-right scale pins update the existing shared controls, persistence and live-state
contract, and their labels remain screen-readable under canvas zoom. Performance Trace now treats Scale as
whole-widget scale while Width defines its base shape. A dotted Quest 3 H/V 1.00 binocular reference surrounds
the solid current post-crop rectangle, with an inner oval marking approximate naturally visible binocular area.
Native coordinates remain unchanged. Recorded matched-locate evidence proves `fullFov` is the original FOV on the
active route. Desktop widget X/Y therefore uses the same full-lens normalized frame and crop affects visibility only.
Dragging is the exact inverse over the full preview; no second offset or coordinate migration occurs.
The visor outline is active red only while Visor mask is enabled and becomes a faint grey geometry reference when
disabled. Placeholder content is not yet a native pixel replica.

All sixteen HUD widgets have their own persisted warning and critical controls with higher/lower semantics.
The old System-only controls and default-key duplication are removed. HUD rings now carry literal catalogue
labels and explicit %, ms, FPS, MHz or state units; session events use the same terminology. The clock card
gives local time stronger typographic hierarchy than elapsed session time. Visor Width/Height again define
the aspect ratio, Size scales it uniformly, and Nose alone controls notch depth against a fixed curve. The four
unstable notch-detail sliders are removed from the main editor. Edge Mask exposes
only the two combinations the renderer actually supports instead of retaining hidden no-op controls.

Calibration contains only its ten tools; Overlays owns ordinary overlay controls; DiagMonster owns Session
Graph and diagnostics. Every calibration key is audited UI→INI→native and deterministic full/vertical-crop/
horizontal-crop PNG references are produced for all ten tools under `TestResults\CalibrationReferences`.
Native VR traces archive as unique `session-*.csv` files while `latest.csv` remains a compatibility alias.
The Session Graph browser opens prior runs, compares selected FPS/P99 results and explicitly deletes selected
history. DiagMon session history also supports selected-session comparison and configurable retention guidance;
evidence is never silently deleted. Overlay, HUD, calibration, Session Graph, performance-trace, RenderPolicy,
DiagMon and repository contracts pass. Build 4.1.239 completed WPF, broker, signed identity, x64/Win32 native,
MSI creation and extracted-payload hash/marker validation with zero build warnings or errors. Live headset visual
acceptance remains deliberately unclaimed.

## Cross-route visor compatibility and ViewLab Bridge (implementation candidate; 2026-07-15)

**Canonical compatibility goal:** any game, feature, graphics stack, runtime, PC and GPU is handled through
capability negotiation rather than identity rules. All ViewLab rendering shares one frame-derived feature
presentation plan. Legacy games ultimately load a ViewLab-owned translation bridge and the active OpenXR
runtime without a separately installed translator. Missing capability combinations are feature-level
fallbacks with diagnostics, never unsupported-game classifications. Representative game/hardware matrices
prove specific regressions only; they cannot by themselves prove the universal design claim.

Live reproduction on installed 4.1.222 confirmed VDXR and the Quest 3 available, then launched the
installed legacy route with D3D11. Four translated OpenXR instance creations and the recreated graphics
session all loaded the global `mask_enabled=1`; the disabled per-app profile had no visor override. The
earlier `visor_enabled=0` sessions therefore reflected the global INI at those launch times, not a profile,
migration or session-reset defect. The failure reproduced with a healthy process: ViewLab tracked the
`7260x882` translated eye texture, rendered at the submitted `3630x882` per-eye extent, created its own
swapchain and received success from `xrEndFrame`, yet the user saw no visor. Builds 4.1.225-4.1.230 then
replaced the previously working ordered stereo projection with a head-locked quad. The translated menu
compositor accepted that quad but did not display it; moving every common feature onto the same unverified
carrier made visor, HUD, trace and notifications disappear together. This was the shared regression.

Build 4.1.232 repairs the resulting duplicate-path regression. Direct rendering owns the ordered allocation
transition; once the transparent stereo projection is ready, visor, HUD, trace, notifications, clock/session,
sticky notes and every other normal shared overlay move to it together and direct normal-feature drawing
stops. Ordered failure restores the complete set to direct. The visor now emits guaranteed opaque black on
the same carrier as the colour-correct HUD. Fixtures, contracts, packaging, installed x64/Win32 hash parity,
VDXR readiness and a live Dirt session prove one `3526x882` ordered submission after the transition. Headset
visibility through menu/gameplay/menu, another translated title and native OpenXR remain pending.

## DiagMon(ster) native capture and session history (lifecycle repaired; game/Trace validation pending, 2026-07-15)

View Lab now owns the complete user-facing capture lifecycle through the exact `DiagMon(ster)` cockpit:
manual/foreground/new-process/previous targeting, Standard/Detailed/Trace modes, collector status,
elapsed time, clean stop, current-session inspection, a sortable/filterable Session Library, validation,
classification, notes/tags, explicit deletion and context-bounded AI ZIP export. Sessions live under
`%LOCALAPPDATA%\XR ViewLab\DiagMon` as portable directories with JSON manifests, raw evidence, summaries,
graphs, events, logs and annotations; `.partial` sessions are recovered and marked incomplete at launch.

The generic collector owns PresentMon, low-rate target process sampling, typeperf system/DPC/interrupt/
memory/storage/network/GPU counters, detailed module/API detection, event-window queries, View Lab evidence
and optional capped WPR. Collector failure is explicit and partial evidence survives. Deterministic frame
analysis documents long/severe thresholds, never treats `AllowsTearing` as a dropped frame, and separates
metric calculation, robust median/IQR historical selection and factual wording. Invalid, experimental,
stress-test and incomplete runs cannot enter calculated baselines. Two real short desktop fixtures (`cmd`
and PowerShell) proved generic session finalisation, metadata exclusion and export; both are marked invalid
experiments. Main WPF build, dedicated fixtures, contracts, x64/Win32 native layers, signed notification
identity, MSI creation and extracted-payload validation pass in 4.1.224. Installed UI interaction, a
sustained workload, PresentMon output on a real game and Trace-mode WPR remain to validate. The 2026-07-15
audit found that real Brave and DiRT sessions lost PresentMon output because the collector was force-killed,
leaving three owned ETW sessions behind; target exit and the Trace deadline also failed to finalise. The
repair bundles hash-pinned PresentMon 2.4.1 plus its MIT notice, streams stdout into a ViewLab-owned CSV
with one-second durable flushes, cleanly terminates the unique trace-session name, automatically serialises
target-exit/deadline finalisation including WPR stop, captures Detailed modules while the target is alive,
and isolates fixtures from production LocalAppData. Deterministic target-exit, output-pump, collector-stop
and fixture-store checks pass; a sustained real-game PresentMon CSV and live Trace cap still require testing.

## HUD alarm and crash-tolerant trace repair (implemented; trace fix user-verified, 2026-07-14)

The completed DiRT Rally diagnostic baseline held a flat 90 FPS while GPU load briefly touched 90%,
yet alarm-only showed GPU red indefinitely after recovery to 74–75%. The shared latch refreshed its
own recovery deadline from the stale critical state. Every symbol now uses one executable 750 ms
time-based entry/recovery policy with a hold measured once from the first non-critical input; GPU uses
90% warning and 98% critical defaults. Build 4.1.219 still failed to expose a completed trace after
DiRT Rally exited, proving that game supplies neither shutdown callback reliably enough for persistence.
The bounded telemetry worker now checkpoints new real samples and markers to `latest.csv` once per
second, outside the render thread, and flushes them through Windows before shutdown. End/destroy hooks
remain final-flush optimisations rather than correctness requirements. The reader skips a partial final
record if the process dies during a write. Trace/parser fixtures, contracts, WPF, broker, x64/Win32
native, signed identity package, MSI and extracted-payload validation pass in 4.1.220. A live installed
DiRT Rally 2.0 run then created `latest.csv` while the game was still running and grew it from 29,420
to 33,322 bytes over two seconds with 334 valid sample rows, proving shutdown-independent checkpointing.
After closing DiRT Rally, the user opened ViewLab and confirmed that **Open Last Session Graph** loaded
the completed session's latency graph. This is the accepted 4.1.220 regression baseline and must be
preserved. A second increased-render-resolution DiRT run completed with a continuous sequence 1–87,268
over 1,306.451 seconds and multiple 90 FPS → 22.5 FPS → 90 FPS pressure/recovery cycles. The 9,058,539-
byte trace remained unchanged after process exit, was copied intact into the diagnostic evidence, and
opening it produced a real `ViewLab — Last Performance Trace` window instead of the old no-session
message. The user supplied a screenshot showing the rendered full-session graph labelled `87,268 real
OpenXR samples · 0 markers · latest.csv`, including the repeated pressure/recovery plateaus. DiagMon's
two-second GPU evidence peaked once at 90.42% and never reached 98%; its PresentMon
child produced no CSV. The user observed alarm-only hiding and recovery behaving correctly during that
run, completing the live alarm regression pass. Raw-row correlation proved that the shelves are real
whole-session menu/loading cadence changes, while the approximately four-minute driving interval held
11.112 ms average / 12.042 ms P99 despite substantially higher resolution. The completed-session viewer
now supplies labelled axes, selectable legend, zoom/pan/reset, exact hover values, robust scaling and
spike-preserving downsampling, budget guides, event lines and percentile/GPU-saturation summaries.
Trace schema 2 adds UTC anchoring, GPU values and alarm masks without breaking schema 1 or the accepted
checkpoint/opening path. Schema fixtures, alarm-policy fixtures, contracts, WPF, broker, x64/Win32
native, signed identity package, MSI and extracted-payload validation pass in 4.1.221. Automated Windows
capture of the running installed 4.1.220 window failed in the desktop capture service, so visual and
interaction acceptance of the new 4.1.221 graph remains with the user; no false UI-pass is recorded.

## DiagMon capability migration (architecture fixed; 2026-07-14)

ViewLab is the product. DiagMon is a prototype farm and must never become a required runtime, service,
data authority or parallel UI. Valuable experiments migrate into ViewLab one bounded feature at a time;
after parity and runtime/headset validation, the ViewLab implementation is the sole product path. The
first selected capability is PresentMon-backed presentation capture because it adds independent present
mode, frame-pacing, latency and stutter evidence to ViewLab's existing OpenXR timings. Process telemetry,
per-process GPU-engine data and deep system diagnosis follow only after that slice is accepted. The
ViewLab-owned session contract, migration stages and logging caps are documented in architecture and D26.
The second DiRT baseline reinforced the selection: the prototype reported successful orchestration but
lost the entire PresentMon CSV, while buffering 7.23 MB of raw GPU rows until exit. ViewLab's migration
must stream bounded chunks, expose collector failure explicitly and preserve recoverable partial output.

## Experimental cleanup (complete; 2026-07-14)

The isolated generic technical-history experiment is removed from UI, broker, persistence, tests and
canonical documentation without changing live notification or racing presentation. The Topmost
failure latch is now atomic across release/end-frame paths. The reusable right-click slider reset
helper and its invariant-culture fixtures are retained.

## Clock and VR session timer (implemented; headset validation pending, 2026-07-14)

A dedicated compact visor card shows 12/24-hour local time and optionally adds elapsed time since the
current successful `xrCreateSession` as a second lane. Elapsed time uses monotonic uptime, resets at
`xrDestroySession`, and is independent of notification and performance-alarm state. Five themes, enable,
timer lane, position, angular scale, opacity and visibility bind publish live. Formatter fixtures, WPF,
broker and x64/Win32 native builds pass; binocular fusion and headset legibility require validation.

## Network HUD expansion (implemented; headset validation pending, 2026-07-14)

The existing modular HUD catalogue now includes optional PING latency, rolling 20-probe LOSS,
successful-probe JIT and NET stability widgets. The bounded hardware worker sends at most one Windows
ICMP echo per second to `network_probe_target` (default `1.1.1.1`); it never blocks the OpenXR render
thread. Three consecutive misses mark the configured path `OFF`; elevated loss, jitter or latency is
`BAD`. All four widgets default off. These are truthful probe-path measurements, not inferred game-
server statistics. Live provider reachability, headset legibility and overhead remain to validate.

## Real performance trace markers (implemented; build/headset validation pending, 2026-07-14)

The configurable F6–F12 bind (F8 default) now creates a rising-edge event with its exact QPC timestamp
and sequence number inside the native sample stream used by the visor graph. A numbered visor badge
confirms the press, the live graph retains its numbered line while in range, and a bounded one-hour
ring is atomically written to `PerformanceTraces\latest.csv` when the OpenXR session ends. The settings
app opens that real trace in a dedicated post-session actual-versus-target graph with previous/next
marker navigation. No generic history or notification storage participates. The deterministic trace
round-trip and full build pass; headset bind/legibility validation remains pending.

## Sticky note visor widget (implemented; headset validation pending, 2026-07-14)

Up to eight optional 120-character notes are normalized and word-wrapped by tested native logic into at
most four lines, then drawn as independently themed square paper cards through the shared binocular
presentation path. Each note owns enable, position, angular size and opacity; the collection retains one
rising-edge F6–F12 visibility bind (F7 default) and never enters the notification queue. Legacy single-note
settings migrate into note one. Headset scale, fusion and bind validation remain pending.

## OBS recording indicator (implemented; real OBS/headset validation pending, 2026-07-14)

The medium-integrity broker can authenticate to a configured local OBS WebSocket v5 endpoint and
poll the real `GetRecordStatus` response. Only `outputActive=true` publishes recording state to a
dedicated mapping; process presence is not evidence. Native rendering adds restrained red corners
with adjustable opacity/thickness. OBS was not running and port 4455 was closed during implementation,
so live integration remains unverified. The cue is drawn into submitted eye textures and must be
assumed visible in captures until a real game-capture/display-capture matrix proves otherwise.

## Music track-change notification (implemented; live player/headset validation pending, 2026-07-14)

The broker's opt-in Windows SMTC provider follows the current OS Now Playing session, deduplicates on
trimmed title+artist, decodes optional artwork and emits a brief card through the existing notification
pipeline only when the track changes. Pause, seek and volume changes do not replay it; there are no
permanent transport controls. The provider now retains the exact session-change handler so stop/restart
unsubscribes correctly. Dedup fixtures and broker builds pass; a live player/artwork/headset pass remains.

## Visible visor feature backlog (ordered)

1. **Clock/session timer:** base clock + elapsed session card implemented; stopwatch/countdown modes
   remain a later extension after the base widget passes headset validation.
2. **Network HUD:** implemented; headset validation pending. Optional PING/LOSS/JIT/NET widgets
   probe a labelled configurable IPv4 path once per second. They do not claim game-server telemetry.
3. **Performance trace markers:** implemented; build/headset validation pending. Bind, exact QPC
   timestamp, sequence, visor confirmation, real trace storage and post-session graph navigation are wired.
4. **Post-session performance recording:** bounded actual/target recorder and marker viewer implemented;
   zoom/range inspection and spike summaries remain later extensions.
5. **Sticky note:** implemented; headset validation pending. One short wrapped note with
   position/size/opacity and a show/hide bind; no note manager.
6. **OBS indicator:** implemented; real OBS/headset validation pending. Authenticated recording-state
   query and subtle red corners are wired. Capture exclusion remains explicitly unverified.
7. **Music change card:** implemented; live player/headset validation pending. PC-side track-change
   detection feeds a brief title/artist/artwork card with no permanent media controls.
8. **Failure explanation:** evidence-ranked confirmed/probable/unknown causes without invented certainty.
9. **Incremental iRacing modules:** fuel/laps/position/incidents/pit/delta/relatives one at a time;
   isolated logic tests only until the user's broken finger permits driving validation.
10. **Notification visual redesign:** slimmer cards, cleaner hierarchy and subtler animation only;
    no privacy, policy, scheduling or reduced-motion expansion.

## Elite Dangerous startup crash repair (2026-07-13, headset verified)

VDXR supplies a valid one-triangle hidden-area mask for each eye. The outer-edge visibility-mask
filter assumed mirrored centroid signs, retained the left triangle, and reduced the right mask from
three indices to zero. Four Elite Dangerous launches then produced the same null-pointer write in
`EliteDangerous64.exe` immediately after mask consumption. The filter now passes indivisible masks
through unchanged and refuses any transformation that would empty a non-empty runtime mesh. This is
a runtime-topology safety rule, not an application exception. Contracts and the complete 4.1.211
packaging build pass. The user confirmed Elite Dangerous launches and works with ViewLab enabled
after installing 4.1.211. The broader Eleven/Pistol Whip/DiRT Rally/Assetto headset matrix remains
pending before any push or release.

## 4.1.209 Pistol Whip failure and repair candidate (2026-07-13)

Headset testing failed 4.1.209: visor and calibration were absent, only the deviation graph was
reliable, Alarm-only stayed visible, HUD wrapped/scaled incorrectly, notification animation stepped,
and racing presentation buttons appeared inert. Installed WPF, broker, x64 and Win32 payload hashes
matched 4.1.209, proving this was product behaviour rather than stale installation.

The repair keeps projection-only applications on the proven direct backend and demands Topmost only
after a distinct application compositor layer is observed. Calibration remains on submitted game
pixels; the grid uses explicit constant colour. Graph/trace/HUD policy is isolated in `RenderPolicy.h`
with executable fixtures. Notifications carry lifecycle timestamps for native per-frame animation;
racing tests use temporary non-persistent gate overrides. Managed/native builds and deterministic
fixtures pass. MSI 4.1.210 is built; its narrow Pistol Whip headset check remains required before DiRT.

## Automatic topmost backend (repaired; safety headset matrix pending, 2026-07-13)

Backend choice is automatic and the experimental checkbox is gone. Projection-only applications
remain on direct eye-texture rendering. A distinct application compositor layer latches Topmost
demand; the common scene is prepared on the transition frame and submitted from the following frame
without duplicates. Literal-pixel calibration stays direct. Any capacity, render, submit or device-
loss failure latches direct fallback. `overlay_force_direct=1` remains the diagnostic escape. The
ordered Pistol Whip then DiRT headset safety matrix remains mandatory before release.

## Performance Trace modes (implemented; headset validation pending, 2026-07-13)

Trace visibility is now explicit: Off, Always visible, or Alarm only. Alarm-only continues bounded
history while hidden, relies on the existing sustained alarm state, extends the current incident,
holds through recovery, and fades once over 500 ms. Relevant VR/frame or APP channels receive a
thicker stroke during their alarm. Existing graph maths, placement and stereo projection are unchanged.

## Attention policy (implemented; visual validation pending, 2026-07-13)

Spotter and safety flags are immediate independent spatial/border channels and may coexist. Desktop
cards wait off-screen while either is active, then show normally if released within five seconds or
expire. Lap cards remain prompt and performance alarms remain independent. The policy has fixed
bounds and no cross-feature enable side effects. See `VIEWLAB_ATTENTION_POLICY.md`.

## iRacing provider correctness (complete; presentation pending, 2026-07-13)

The SDK reader now validates layout/ranges/types, consumes only advancing ticks, goes stale after
750 ms, reconnects at a bounded 500 ms interval, and owns one interruptible worker. All official
`CarLeftRight` states are distinct; inactive/stale/disconnect/session reset clears cues. Generic flag
events use stable safety priority. Lap events include authoritative validity, PB/delta/session fields
and suppress duplicates; session-best is deliberately absent until an authoritative source is read.
The production reader passed the real named-memory-map fixture matrix. Its sole worker now lives in
the installed broker and publishes a generation-safe generic state mapping independently of the
settings window. Native presentation implements side-correct configurable spotter glow, controlled
flag border, and lap cards gated separately from Windows notifications. Fixture/native build checks
pass; live/replay iRacing and headset appearance remain pending. See `IRACING_IMPLEMENTATION.md`.

## Windows notification broker (implementation complete; headset presentation pending, 2026-07-13)

ViewLab now ships a signed package-with-external-location identity and an independent medium-
integrity `ViewLab.NotificationBroker.exe`. The broker owns global Windows listener consent,
collection, deduplication, removals, filtering, privacy shaping, card composition and bounded expiry;
the settings UI may close without ending collection. The ordinary UI runs `asInvoker`, with a narrow
elevated helper used only when machine-wide OpenXR layer registration genuinely changes.

Installed build 4.1.202 registered `cooooked.ViewLab.NotificationBroker` successfully and Windows
reported `UserNotificationListenerAccessStatus=Allowed`. A disposable separately packaged fixture
then sent a real Windows toast; the production broker observed it and published card ID 1 to
`Local\XRViewLabNotifications`. This proves the Windows-to-ViewLab collection path independently of
the synthetic presentation test. Final in-headset card appearance remains to be checked. Development
packages use the ignored local self-signed certificate; production builds should provide the
release PFX through the documented build environment. See `NOTIFICATION_IMPLEMENTATION.md`.

## Hardware telemetry platform (implementation complete; build/headset validation pending, 2026-07-13)

The HUD catalogue now offers CPU total, peak logical CPU, reported CPU clock, GPU 3D, RAM, commit,
VRAM budget, genuine SYS remaining headroom, APP, VR, FPS and frame interval. One 250 ms worker owns
Windows, PDH and DXGI collection and publishes a fixed snapshot; the render thread only attempts a
non-blocking copy and both eyes reuse one draw snapshot. Default layout is CPU/GPU/SYS/VR, APP remains
optional, enabled widgets pack in saved order into one proportional row; scale changes widget
geometry and gaps together.

Telemetry settings are schema version 1 and use a separate 64-byte live mapping, preserving the
208-byte overlay v7 mapping. SYS is `100 * (1 - max(valid pressure))`; CPU/peak/GPU use raw load and
RAM/commit/VRAM map 70–95% use onto 0–100% pressure. Default remaining-headroom warning/critical
thresholds are 30/10 with sustained entry/recovery.

Advanced vendor sensors (temperature, power, fan and vendor GPU clocks) are intentionally deferred
until an optional provider has passed licensing, deployment and failure-isolation review. Hardware
history channels for the graph and percentage/absolute memory display remain known follow-ups; the
existing unit-safe OpenXR graph is unchanged. Headset/provider/overhead validation is still required.

## Modular Performance HUD redesign (build complete; headset validation pending, 2026-07-13)

CPU and GPU retain their established collectors. Unstable SYS is replaced by APP workload—the QPC
window from successful `xrBeginFrame` return to matching `xrEndFrame` entry, divided by the detected
cadence-aware budget. CPU/GPU/APP/VR are registry-backed widgets with independent enables, persisted
order, gap-free packing, sustained state hysteresis, and a 0.15–3.0 whole-widget scale. VR derives
only from the current `predictedDisplayPeriod` and rolling cadence, with explicit target, warning,
critical, stable-reprojection, unstable, and unavailable states.

Performance Trace is now a bounded seven-channel graph with Deviation, Milliseconds, FPS, and Budget
Percent modes. Live state remains 208 bytes and moves to v7 using its reserved tail. Topmost, crop,
projection, and calibration logic are unchanged. Native x64 and WPF compile plus HUD, Topmost, and
repository contracts pass. MSI 4.1.191 payload hashes/version were extracted and validated; headset
validation remains pending. See
`PERFORMANCE_HUD_REDESIGN.md` and `PERFORMANCE_HUD_VALIDATION.md`.

## Overlay coordinate unification (in progress, 2026-07-13)

The first `OverlayCoordinateResolver` pass incorrectly mapped normalized coordinates independently into each eye and used each asymmetric full-FOV midpoint for the crosshair. This produced two monocular stickers and contaminated both direct and topmost output. The resolver now builds shared selected/full tangent bounds from both eyes, chooses one visor-space target, and projects it independently into each eye viewport. Crosshair zero is shared tangent `(0,0)`; normalized offsets and Lens Pinned clamping happen in shared tangent space. HUD and trace both render binocularly. Projection capture still stops after the primary layer to prevent repeated OpenComposite texture draws. Manual headset testing found the original two-sticker regression and subsequently confirmed restored binocular overlap, crosshair fusion and flat crop behaviour. Exact build attribution is incomplete; newer HUD/telemetry and safety changes still require targeted validation. No release is authorised.

**Safety-critical Topmost repair:** the 2026-07-13 DiRT Rally incident was caused by Topmost
swapchain churn followed by 193 unbounded allocation retries during device/display-stack failure.
Topmost now makes one stable allocation attempt per OpenXR session, derives capacity from the tracked
game texture rather than submitted-rectangle jitter, and permanently fails closed to the direct path
on any allocation, render, submission, capacity, or device-loss error. Checkbox cycling cannot re-arm
a failed session, and failed resources are not destroyed on the render path. Headset validation is
mandatory before release; see `INCIDENT_REPORT.md`, `TOPMOST_ROOT_CAUSE.md`, and
`TOPMOST_VALIDATION.md`.

**Core crop regression found:** runtime evidence showed asymmetric split crop applying `pitch_offset=0.31637` radians to both game eye poses even though the live ini requested `foveated_center_compensation=0`. The setting had been made permanently on after the 4.1.81 backup, which defaulted/respected it off. Pose compensation is now retired; split crop changes only FOV tangents. Headset confirmation is required before returning to overlay calibration issues.

**Narrow remaining-repairs pass:** `SplitCheck_Changed` now persists the mode immediately, so
disabling split writes centred normal-vertical state before the next game launch. The experimental
topmost projection uses the runtime texture's legal typed RTV format and submits the already blended
transparent target as premultiplied alpha, without changing direct-render colours.
Calibration tools are divided by purpose: literal-pixel
patterns use the complete submitted eye rectangle; the 64 px grid retains exact spacing but starts
at shared tangent zero; radial spokes and rings are constructed in shared tangent space and project
per eye. Crosshair X/Y sliders reset their own axis on right-click. Deterministic verification passes;
Pistol Whip and Dirt Rally 2 headset checks remain pending. See `docs/CALIBRATION.md` for the pattern
contract.

## Historical overlay baseline (superseded by completed sections above, 2026-07-12)

**Build:** `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.191.msi` (modular HUD widgets, APP workload,
refresh-relative cadence states, seven-channel performance graph, live-state v7, and smaller scale;
Topmost safety, crop/stereo resolver, and calibration paths unchanged; WPF + native x64/Win32 +
deterministic contracts + extracted MSI payload validated; headset validation pending).

**Packaging mismatch repair:** 4.1.169's MSI metadata/filename were correct, but WiX harvested the
stale 4.1.168 executable from `bin\Release\net8.0-windows\...` after the WPF project moved to
`net8.0-windows10.0.19041.0`. Build 4.1.176 derives and checks the TFM publish path, cleans generated
WPF/WiX outputs, then administratively extracts the MSI and verifies its app version, WPF/native
hashes, and compiled overlay markers. The extracted payload passed all checks. An attempted silent
upgrade on this machine returned MSI 1730 because the agent session is not elevated; installed
4.1.169 registration / 4.1.168 executable therefore remains unchanged pending an administrator-run
install. No commit or publish was performed.

Four features added on top of the stereo HUD, all sharing the tangent-space binocular-overlap
anchoring so they fuse cleanly in both eyes. Live-state contract is now v4 (208 bytes).

**Overlay completion pass (source complete, final build below):** crosshair flat colour is moved to
an explicit constant-buffer shader after tracing the invisible result to the same VDXR interpolant
failure already proven by calibration. Both live previews mirror the native rectangles and native
logs final per-eye geometry/draw execution. Notification status preserves the exact WinRT access
status/HRESULT and a synthetic Test Notification bypasses listener permission. The iRacing baseline
now reads the SDK shared mapping off-thread, normalizes four core fields into generic events, and has
full simulations/diagnostics. VR timing colour uses rolling full-precision 105%/115% classification
with consecutive confirmation and hysteresis; displayed rounding is not used for state.

**User calibration follow-up:** tangent zero did not coincide with the radial calibration centre on
the active asymmetric eye FOV. Crosshair anchoring now uses the submitted eye-rectangle midpoint,
identical to the radial zone-plate spokes. HUD amber/red no longer uses miss counts alone: the rolling
cadence median must be genuinely degraded (>108% amber, >120% red), keeping stable 120 Hz 8.2/8.3 ms
and 90 Hz 11.1/11.2 ms green. The notification screenshot is the accurately classified unpackaged
Win32 `UserNotificationListener` identity limitation; Test Notification remains permission-independent.

**Live visor/preview follow-up:** global visor controls are now unconditionally live through the
generation-stamped mapping; the checkbox/key are removed because unchanged snapshots do no INI work
and have negligible cost. Per-app overrides remain protected. Both crosshair previews use half the
former display scale, and the binocular visor preview shows one reference crosshair rather than two.

**Overlay coordinate audit:** the equal-pixel eye-centre change in 4.1.179 was invalid for asymmetric
FOVs and caused double crosshairs. Fused overlays now start in shared tangent/angular space and
project into each eye independently. Crosshair `(0,0)` convergence and crosshair/HUD/notification
sizes use pixels-per-tangent so crop changes do not move, separate, or rescale them. Boundary flash
is a fully inset per-eye inner outline. The combined preview keeps one centred fused symbol, while
pixel calibration patterns remain intentionally per-eye diagnostics. Headset validation pending.

- **Render-boundary flash.** While a HUD/trace position/size/width/height/scale slider is dragged
  (`Thumb.DragStarted/Completed` → `interactFlags` bit0), the layer paints the exact cropped eye
  rect (= submitted sub-image) as a fixed cyan-white outline at constant screen thickness in both
  eyes, fading over `kBoundaryFadeMs`=500 ms after release. The fade timer is native, so it
  completes even if the UI closes mid-drag. Non-layout controls do not trigger it.
- **Static CS crosshair.** Drawn at the calibrated stereo centre (tan 0,0 per eye). Native reads
  size/gap(±)/thickness/dot/outline/outline-thickness/alpha/colour/T-style + a ViewLab VR scale;
  CS reference pixels map to eye pixels via `scale × eyeHeight/1080`, pixel-snapped. `CrosshairConfig`
  (C#) parses legacy `cl_crosshair*` and CS2 `CSGO-` base-58 share codes into the same settings and
  exports legacy config. Dynamic/weapon/movement settings are parsed and ignored.
- **Desktop notifications.** `NotificationService` (C#) uses WinRT `UserNotificationListener` off the
  render thread, composites each card (icon + title/sender + shortened body) to straight-alpha RGBA,
  runs the full queue (slide-in / ~3 s hold / fade+slide-out / independent expiry / upward stacking)
  and writes to `Local\XRViewLabNotifications`. The layer draws each card as one textured quad
  (new `kTexturedPS` + sampler + per-slot dynamic textures, uploaded only on content change) in both
  eyes at the bottom-right of the cropped region, inside a safe margin. Live settings: enable, X/Y,
  scale, opacity, duration, max visible, allow/blocklist, privacy (full/title/app), show icon, show
  image. **Limitation:** unpackaged Win32 apps often get `RequestAccessAsync` = Denied unless an
  AppUserModelID is registered; the service fails soft and reports status. Toast payload images are
  not exposed by the listener, so the "image/thumbnail" is the source app logo where available.
- **Historical iRacing scaffold (now completed).** `IViewLabEventProvider`/`ViewLabEvent` seam + a labelled UI
  section (enable, lap popup, spotter glow, flag border) + flags plumbed to the layer, which takes
  no action on them. No telemetry provider is wired up.

TFM raised to `net8.0-windows10.0.19041.0` for the WinRT projections. New files:
`XRViewLab.UI/CrosshairConfig.cs`, `NotificationService.cs`, `ViewLabEvents.cs`.

## Native performance HUD (in progress, 2026-07-12)

**HUD build:** `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.168.msi` (stereo refinement pass; built clean, headset validation pending)

An optional, default-off four-icon CPU/GPU/SYS/VR HUD draws directly into BOTH projection eye
textures at `xrReleaseSwapchainImage`, after the visor and before runtime submission; it never
creates an OpenXR overlay layer. Stereo coherence: both eyes render from one per-frame snapshot
(metrics + trace samples captured when the left eye draws), and every element is anchored at a
shared tangent-space point inside the binocular overlap of the cropped per-eye FOVs, mapped
through each eye's own submitted `XrFovf` into its sub-image pixels — zero angular disparity, so
it fuses at effectively infinite depth and stays clear of the opposite lens edge, while remaining
positioned relative to the final cropped tangent bounds (rect-fraction fallback if FOVs are
unavailable).

Metrics: CPU is total system utilisation from `GetSystemTimes`; GPU is the D3D11 render adapter's
busiest 3D engine from adapter/engine-grouped PDH samples; SYS is actual OpenXR frame work
(begin→end) divided by the runtime's `predictedDisplayPeriod`. The fourth indicator (headset icon)
now shows the VR frame time in ms with one decimal, no unit suffix: the QPC interval between
successive `xrWaitFrame` returns (EMA-smoothed), coloured/ring-filled against the effective budget
`predictedDisplayPeriod × cadence multiple`. The cadence multiple (1–4) is a rolling median of the
last ≤90 intervals divided by the period, and only switches after 20 consecutive agreeing
computations — half-rate reprojection (72→36, 80→40, 90→45, 120→60) is detected without
hardcoding any refresh rate and can never be triggered by a single slow frame. Cadence
transitions are logged one-shot per change.

Text renders in a 5×7 pixel font with a dedicated decimal-point glyph at integer pixel scale
(replacing the seven-segment digits, whose missing decimal glyph made 13.3 render as 133). The
scale slider (0.5–3.0) maps the full range smoothly; the old fixed 112 px ceiling and the native
2.0 config clamp are gone. The 600-sample pacing trace has live X/Y/height/width/history/
sensitivity controls (continuous ±0.5–8 ms), scrolls newest-right, and baselines on the effective
cadence budget. Alarm-only mode (live checkbox `hud_alarm_only`) hides each indicator
independently unless red, with smoothed-threshold hysteresis and a `hud_alarm_hold_ms` (default
1500 ms) post-recovery hold; the trace is never hidden and telemetry keeps updating. Live-state
shared memory is contract version 3 (120 bytes).

**Responsive/live repair:** HUD layout now travels through the generation-stamped live-state
mapping with X/Y/scale/safe-margin/clamp controls, and is clamped as a complete four-icon unit to
the active projection sub-image. Calibration and visor-enable changes publish the same snapshot
instead of requiring a restart. The zoomed preview keeps its curve pen screen-space stable.

**Telemetry pass:** the ViewLab UI has a `Performance HUD (both eyes)` checkbox and live
frame-trace position/size/history/sensitivity controls plus an alarm-only checkbox under
Calibration. OpenXR wait/begin/end timing follows the frame association
pattern reviewed in Fred Emmott's MIT-licensed XRFrameTools but is implemented locally with no
runtime dependency. Each verbose telemetry sample records raw source/unit, conversion inputs, and
final display values. Preview pin radii, outlines, hover state, hit testing, and drag acquisition are
now screen-pixel based (`modelRadius = desiredPixelRadius / currentZoom`).

## Draw in the Void research (2026-07-12)

Research is complete; implementation is deliberately deferred. ViewLab's crop is confined to
`xrLocateViews` FOV tangents and recommended projection swapchain dimensions—it does not currently
rewrite submitted projection image rects or final composition. The in-tree creepy-face probe is
unvalidated and has diagnostic gaps, so it is not evidence for or against the concept. Modern
OpenKneeboard and RaceLab are expected to submit independently composited OpenXR content, but the
current registry inspection has both disabled, so their actual layer types/order remain headset
measurements. See `docs/DRAW_IN_VOID_RESEARCH.md` for sources, ranked hypotheses, instrumentation
requirements, and the controlled test matrix. No publish was performed.

**VDXR developer confirmation (2026-07-12):** VDXR PC-composes all OpenXR layers into one encoded
stream. A ViewLab FOV-tangent crop therefore reduces the FOV of that final stream and constrains
third-party overlays too, even when they arrived as independent OpenXR quads. Retaining overlays
in the void requires either separately streamed layers (not known to be offered by any streamer)
or a ViewLab-owned full-FOV final composition including black bars. The latter is technically
possible: keep the game-facing crop, copy the reduced game projection into a full-FOV target,
submit that target with original FOV, then let later quads compose. It deliberately spends encoder
resolution/bandwidth on the unrendered region and weakens the cropped-stream crispness benefit,
so it is shelved as counter to ViewLab's primary goal—not impossible. The VDXR performance overlay
is headset-rendered; current code does not gate its crop on `fovMutable`, but the approach still
depends on a runtime honouring mutable-FOV behaviour.

**VDXR developer implementation clarification (2026-07-12):** the current `xrLocateViews` crop
depends on runtimes honouring mutable-FOV behaviour (works in VDXR; the developer expects Quest
Link not to support it portably). The full-FOV fallback would require a ViewLab-owned
tangent-space FOV-reprojection shader, not a simple copy: reproject the app's submitted cropped
eye layer into a full-FOV black-backed target, replace that projection at `xrEndFrame`, preserve
later overlay layers, and correctly transform or handle depth chains for non-VD runtimes. This is
high-risk compositor work and remains deliberately shelved pending an explicit product decision.

**Shelved cleanup (2026-07-12):** the unvalidated Draw in the Void creepy-face probe is removed
from the native layer, shared-memory contract, default INI, and UI. No unsupported composition
layer is now created or appended. The live-settings mapping no longer calls `OpenFileMappingW` on
every `xrEndFrame` when the UI is closed: reconnect attempts are limited to once per second, while
an existing mapping retains generation-checked end-frame updates. Calibration remains default-off;
normal logging stays startup/one-shot/error-only, with per-frame detail opt-in in the separate
verbose log. Contract verification and the full 4.1.159 build pass; headset validation is pending.

## Ten-pattern calibration suite (in progress, 2026-07-12)

The former hidden `calibration_grid` diagnostic is now a Calibration dropdown with ten independent,
default-off submitted-texture patterns: grid, ruler, repeated 1/2/4 px gratings, colour/grey bars,
frame beacon, edge probes, checkerboards, radial zone plate, clipping steps, and motion strip.
All patterns render after the visor at swapchain release, so they measure downstream runtime/stream
behaviour. Beacon and motion state advance once per submitted projection frame and are shared by
both eyes. Build and in-headset validation are pending; inspect one-pixel patterns at 100% capture
zoom before interpreting stream quality.

**Capture finding (4.1.153–4.1.154):** VDXR showed all new calibration geometry as black: grating
bands, zone plate wedges, and colour plates lost white/colour components. This is not a batch
overflow. The attempted semantic-only repair (`COLOR` → `TEXCOORD0`) made no visible difference,
so calibration now uses its own constant-colour pixel shader and explicit colour batches, without
changing the established visor shader or geometry. Rebuild and headset verification pending.

## Calibration grid origin (4.1.150, 2026-07-12)

The default-off `calibration_grid` draws a uniform 64 px
reference grid into the eye images at `xrReleaseSwapchainImage` — the ground-truth ruler from
the edge-smear investigation. It is now the first visible calibration option. Grid uniform in the submitted
texture ⇒ any distortion seen in-headset is downstream (VD encode). Contract-pinned with the full suite;
The suite remains default-off and is not yet headset-validated in this build.

## Binocular WYSIWYG preview + inner-eye controls hidden (4.1.146–4.1.148, 2026-07-12)

UI-only pass, not yet headset-validated:

- `BeanMaskEditor` now renders a binocular preview at a fixed one-to-one reference: the canvas
  sizes itself to the full uncropped binocular render (2:1 via `MeasureOverride`), and the crop
  rect maps the Vertical/Horizontal crop values directly inside it (Vertical 0.2 occupies 20% of
  the reference height — no zoom/fit; an earlier aspect-fit approach was rejected for destroying
  the sense of scale). `CropVertical`/`CropHorizontal` are view-only properties that do not raise
  `ShapeChanged`. The open-inner mode reuses `AddOpenInnerHalf` mirrored per eye; closed mode
  draws `AddClosedFigure` (the former `BuildGeometry`) once per eye. Pins and drag math operate
  on the left-eye rect.
- Main window and profile popup feed crop values into the preview on load and on every crop
  slider/text/split change, so the preview aspect tracks the actual post-crop render area.
- Inner-eye notch controls (Inner low, Bridge, Rise, Peak X, Steep) are hidden in both windows
  (XAML `Visibility="Collapsed"`), and their preview pins are gated behind
  `BeanMaskEditor.ShowInnerShapePins = false`. Reason: the notch is correct monocularly but
  translucent under binocular fusion. All config keys, saved values, per-app registry plumbing,
  and native geometry are untouched; contracts still pass.

## Critical installer safety repair (in progress, 2026-07-12)

**Safety build:** 4.1.143 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.143.msi` (not yet headset-validated).

Pistol Whip was recovered after the Virtual Desktop runtime found its missing
`C:\ProgramData\Virtual Desktop\accessibility.json` configuration and failed to create the D3D11
texture swapchain. The minimal valid default has been restored and Pistol Whip is again running
with ViewLab and ReShade temporarily disabled for isolation.

ViewLab's MSI no longer embeds or runs `CleanupApiLayerRegistry`. Although the script only intended
to remove stale ViewLab paths, it enumerated the shared `HKLM\...\ApiLayers\Implicit` key during every
install, which is unacceptable in a machine-wide OpenXR ecosystem. The MSI now writes/removes only
its own WiX-owned registry values. The previously lost installed third-party manifest registrations
were restored from disk; their enabled state remains conservative until headset validation.

## Fixed-foveation visor coverage controls (in progress, 2026-07-12)

Restoring the unified visor `Size` aperture scale with a compatibility default of `1.0`, adding
an exact rectangular geometry branch at Curve `0`, and extending Peak X from `0..1` to `-0.5..1`.
These are mask-only changes: crop tangents, submitted FOV, and recommended render resolution stay
unchanged. Curve now uses a continuous near-square shoulder (not a zero-only geometry branch),
with Inner low still active. Shape ranges are expanded for headset tuning, each shape slider has a
draggable preview pin, and ReShade now sits next to Edge Masks above the visor controls. Build and
headset validation are pending.

The responsive dual/triple-column layout now hides its redundant Applications and Render Options
sub-headers so the primary cards align at the top; the beta-testers credit remains visible in all
layouts. Backend audit confirms the expanded visor controls persist to the live INI and feed the
native open-inner D3D11 visor geometry at the next game start.

The app-list instruction is shortened to "Checked apps use ViewLab. Double-click for per-game
customization." The redundant combined render-height hint is hidden. In triple-column mode, the
beta-testers credit sits directly below the right-column card, preserving a cleaner applications
column.

The PowerUp/per-app profile popup now uses a ViewLab-themed scrollbar in a reserved layout column,
preventing the Windows default scrollbar from covering visor settings.

Responsive client-width thresholds are now deliberately conservative: mini below 280px, two
columns from 720px, and three columns only from 1200px. This prevents compressed three-column
layouts and delays the one-column mini presentation until it is genuinely needed.

Global visor controls now also refresh live: the UI writes a revision marker only after all visor
keys are saved, and the native layer consumes it at the renderer-locked `xrEndFrame` safe point.
Crop/FOV/resolution and per-app profile values remain startup snapshots. ReShade Remote now begins
with a prominent "WARNING — DO NOT USE" development notice.

Forefront compatibility diagnostics now target the actual reported runtime executable,
`Forefront_Internal.exe`, and emit `forefront diag: VIEWLAB_LOADED` only after ViewLab enters its
OpenXR process. See `docs/VERIFICATION.md` for the exact log-capture handoff.

## Edge smear CLOSED + experimental crop-fix purge (4.1.134, 2026-07-11)

The "edge smear" at crop boundaries is **Virtual Desktop's fixed foveated encoding** —
center-weighted stream quality baked into VD, codec/bitrate-independent, not user-modifiable
(confirmed by VD support staff in Discord). Not a ViewLab bug; not fixable in ViewLab.
Summary: `docs/FIXED_FOVEATION.md`. Full record: `docs/EDGE_SMEAR_INVESTIGATION.md`.
Practical mitigation: pull the visor opening inward (`mask_horizontal`/`mask_vertical`) so the
worst boundary band sits under visor black.

All experimental crop-fix code purged in 4.1.134 at the user's direction: crop experiment modes
1–4 (visibility-mask passthrough / disable hidden-area culling / crop-aware mask / edge-source
probe), the black edge-guard frame (`edge_smear_fix`/`edge_smear_pixels`), and the crop-contract
diagnostic logging/snapshot machinery. The "EXPERIMENTAL CROP FIXES" UI group is gone; contract
test updated to pin the removal. Note: the 8px edge guard measurably sharpened the boundary in
screenshot scanlines (63px→3px) but the user reports no noticeable in-headset difference; it was
removed with the rest.

## CRITICAL regression fixed (4.1.124): Install component wiped all OpenXR layers

`InstallPayload()` in `XRViewLab.UI\ReShadeRemoteWindow.cs` used `New-Item -Path $key -Force`
on `HKLM\...\ApiLayers\Implicit`. On an EXISTING registry key, `New-Item -Force` recreates the
key and deletes every value in it — so each press of "Install component" deleted ALL of the
user's implicit OpenXR layers (~10 on this machine: OpenXR Toolkit, OBSMirror, Racelab, Virtual
Desktop oculus-compat, XRFrameTools ×3, OpenKneeboard, ViewLab) and re-added only ReShade's.
Fixed 2026-07-11: guarded with `if(-not(Test-Path $key)){New-Item ...}`. User's layers were
restored from disk scan via `fix_profile.ps1` (repo root). RULE: never `New-Item -Force` a
registry key that may exist; never delete the Implicit key wholesale.

## ReShade Remote (4.1.123)

- ReShade payload rebuilt from `ReshadeAI/reshade` source to fix the missing `heartbeat` signal that kept `ReShadeRemote` controls disabled.
- `ReShadeRemote` controls are no longer disabled when the game is not running; they behave like a remote that pre-configures settings before the game starts.
- `Install component` now waits for the installer to finish and reports a clear "in use" error if ReShade is locked by a running game.
- Pistol Whip compatibility with the custom payload is still pending headset confirmation.

## Context: the 2026-07-10 recovery

A 12-hour spiral of blind edits (versions 4.1.88–4.1.101, unreleased) broke and then lost the
visor shape features. The native layer was reset to the clean v4.1.55 baseline and the features
are being **rewritten** (not restored) in passes. Full post-mortem: `docs/REGRESSIONS.md`.
Three fixes from the recovery are load-bearing and must survive all future work:
FreeLibrary-after-blob-use, stencil key wiring + filter-runs-with-visor-active, partner-eye
boundary gated to closed-bean mode (details in `docs/DECISIONS.md` D7–D9).

## Current implementation status

4.1.122 removed the experimental LOD pop-in fix, edge-smear fix, HD visor, and
anti-aliasing toggles and their code paths. `foveated_center_compensation`, `stencil_outer_edges_only`,
and `crop_outer_edges_only` are now permanently on; their UI checkboxes are removed and config
keys are ignored. The visor mask now only rounds the corners of the cropped view; the crop
values (vertical/horizontal render height) still control the full render dimensions from the
center. `mask_size`/`visor_size` controls only the unified visor opening scale: its default `1.0`
preserves the full existing opening, and lower values cover more of the already-rendered image.
It does not affect crop tangents, submitted FOV, render resolution, or GPU savings.
The following section is retained historical implementation context. Its old blanket statement that
everything after 4.1.103 lacked headset testing was incorrect: later builds were repeatedly tested,
but documentation often omitted exact build attribution. Treat each specific pending/confirmed note
on its own merits and use `VIEWLAB_VALIDATION_HISTORY.md` for the consolidated evidence record.

## Current headset blockers (2026-07-11)

- Hidden A/B `crop_boundary_full_resolution_experiment=1` keeps the crop FOV but returns the
  runtime's original recommended swapchain dimensions. It is default-off and pending headset
  comparison against the normal crop-resolution path; no rendering-submission data is altered.

- Crop-boundary diagnostics now correlate each primary-stereo `xrEndFrame` projection submission
  only with an `xrLocateViews` snapshot carrying the same session and display time. They log FOV
  tangents, submitted sub-image/texture bounds, and resolution scaling to `ViewLab.verbose.log`
  without changing any submitted rendering data. Headset log capture is pending before any
  rendering correction is considered.

- The visor's fixed 96-segment curve tessellation produced long, visible straight chords at
  headset eye-texture resolutions, even though the underlying game image was sharp. The native
  renderer now uses fixed 512-segment tessellation within its existing 4096-vertex buffer;
  headset verification is pending.

- 4.1.112 proved that `crop_outer_edges_only` was read but not applied: the FOV hook always used
  outer-edge-only crop. Corrected in-tree; requires headset retest.
- The 4.1.112 "visor completely gone" report was most plausibly R9: the installer's per-launch
  settings reset wiped `mask_enabled` on every game start, so toggling the checkbox could never
  matter. The reset machinery is removed. NOTE: `mask_size=1` is a LEGAL, intended value (corners
  masked, maximum opening) — an earlier "recover 1 → 0.82" theory was wrong and its clamp is
  removed; only a MISSING key falls back to 0.82 (R4).
- ReShade Remote said 'component missing' because the single-file app extracts to TEMP: AppContext.BaseDirectory never pointed at the install dir (R11, fixed: Environment.ProcessPath). Separately, the registered ProgramData DLL was stock ReShade, not ViewLab's
  bundled control-capable payload. The install command and status verification are corrected
  in-tree; Pistol Whip compatibility with the custom payload remains unconfirmed.

In-tree implementation summary:
- Preview pins capture/release mouse, clear drag state on lost capture, and sync dragged values
  back to sliders before saving.
- Native visor AA uses per-vertex alpha feather strips; HD doubles curve tessellation. With AA OFF the visor draws through the blend-disabled opaque pipeline (the 4.1.103-proven path) to isolate the unverified alpha path.
- All six visor shape controls have per-app registry plumbing.
- Release-time visor drawing uses cached RTVs; late `xrEndFrame` drawing is fallback-only.
- MSI upgrades preserve the user's live visor, crop, render, and per-app profile settings;
  the packaged ini supplies safe defaults only for a fresh install.

## What works (as of 4.1.105, pending headset confirm where noted)

- Render crop (vertical total/split + horizontal, outer-edges-only option) — core feature, stable.
- Per-app enable + custom profiles (registry).
- Visor mask Technique C Direct (D3D11 draw into eye textures at `xrReleaseSwapchainImage`).
- "Stencil outer edges only": filters the runtime's FOV-stencil mesh to outer halves AND
  switches visor geometry bean/arch (confirmed in-headset at 4.1.103 for the filter part).
- Visor shape controls Size/Curve/Apex Y (+ Inner low/Bridge×4 pending headset confirm).
- ReShade Remote popout + bundled payload install.
- Update check, app list, responsive UI layouts.

## Bug scan findings - 2026-07-10

Severity key: P0 = release blocker, P1 = high, P2 = medium, P3 = docs/test/low-risk.

1. **P0 - UI missing-`mask_size` fallback can still recreate the invisible visor.**
   `Installer\PreserveConfig.vbs` strips `mask_size`, and existing local configs are not replaced
   with the bundled default. `XRViewLab.UI\MainWindow.cs` still loads a missing `mask_size` with
   `OpeningFromMask(...)`; with current default crop values this clamps to `1.0`, and
   `SaveGlobalSettings()` can write `mask_size=1.0`. This is R4 again unless fixed.
   Evidence: `MainWindow.cs` `OpeningFromMask` / `LoadSettings` / `SaveGlobalSettings`, plus
   `PreserveConfig.vbs` reset list.

2. **P0 - The default `xr-viewlab.ini` is not packaged into the current MSI path.**
   `Installer\Installer.wixproj` compiles only `Product.wxs`; `Product.wxs` installs the app exe
   and icon but no `xr-viewlab.ini`. The published folder currently has no `xr-viewlab.ini`.
   `Installer\HarvestedFiles.wxs` references one, but that harvest file is not included in the
   WiX project. Fresh installs therefore depend on code fallbacks, and upgrades keep stripped
   local configs without a default-file repair path.

3. **P1 - Per-machine installer custom actions may touch the wrong user's config/profile.**
   The MSI is per-machine, but immediate VBScript actions read/write `%LOCALAPPDATA%` and HKCU.
   In elevated installs, backup/reset may target the elevated account rather than the actual
   ViewLab user. The final fix removes upgrade-time user-setting reset work entirely.

4. **P1 - Edge Masks popup writes keys the DLL never reads.**
   The UI loads/saves `horizontal_visual_mask_both`, `horizontal_outer_eye_mask`,
   `horizontal_inner_eye_mask`, `vertical_visual_mask_both`, `vertical_top_mask_only`, and
   `vertical_bottom_mask_only`. The DLL reads only `visual_mask_only` and
   `horizontal_visual_mask_only`, so the popup controls appear to have no native effect.

5. **P1 - `crop_outer_edges_only` is saved by the UI but ignored by the DLL.**
   The main UI persists `crop_outer_edges_only`, and docs describe it as a crop mode, but
   `LoadConfig()` does not read the key. The native log hardcodes `horizontal_outer_edges_only=1`
   and `EffectiveOuterEdgeHorizontalScale()` always applies the outer-edge-only model.

6. **P1 - Apex pin drag math is not the inverse of the rendered pin position.**
   `BeanMaskEditor.PinPositions` renders the apex pin at `centerY + OuterApexY * span`, but
   dragging writes `OuterApexY = (mouse.Y - pins.y0) / span`. A default centered pin drag maps to
   `0.5`, negative apex values are effectively unreachable by drag, and the pin can appear to
   jump or clamp instead of tracking the cursor.

7. **P1 - Profile "Use global visor settings" checkbox does not save as global.**
   `ProfileWindow.UseGlobal` is only set by the `Use Global Values` button. If the checkbox is
   checked and the user presses Save, `MainWindow` sees `UseGlobal == false` and writes a custom
   profile. The editor also remains interactive while the sliders are disabled, so dragging the
   preview can create custom values while the UI claims it is using global visor settings.

8. **P1 - One global release-draw flag can suppress a required late fallback.**
   `g_releaseDrewThisFrame` is set by edge guard, Technique B, or Direct C release drawing, then
   `xrEndFrame` skips all late drawing when the flag is true. If edge guard draws but Direct C
   has no usable eye layout yet, or one swapchain draws while another did not, the late visor
   fallback is skipped even though the visor path still needed it.

9. **P2 - Recommended render-size scaling is incorrectly gated on `fovMutable`.**
   `XRViewLab_xrEnumerateViewConfigurationViews()` skips recommended image-size changes when
   `XrViewConfigurationProperties::fovMutable` is false. That property describes mutable FOV,
   not whether an API layer can adjust app-visible recommended swapchain dimensions, so render
   savings can be silently disabled on runtimes with immutable FOV.

10. **P2 - D3D11 state restore is incomplete for complex app state.**
    The visor/edge-guard draw paths save and restore one RTV, one viewport, and vertex-buffer slot 0.
    Apps using multiple render targets, multiple viewports, or additional IA slots can have state
    collapsed after ViewLab draws, especially in the late `xrEndFrame` path.

11. **P2 - Open-inner AA does not feather top/bottom aperture bands.**
    `BuildOpenInnerEyeVisorVerts()` ignores `featherY`; the curved outer edge gets an alpha
    feather, but the full-width top and bottom black bands remain hard-edged.

12. **P2 - UI reads missing per-app `mask_inner_bridge_width` as 0.0 while native defaults to 0.5/global.**
    Older profiles missing this newer key keep the current global bridge width in native code,
    but the UI displays `0.0`. Opening and saving can persist a different bridge shape than the
    DLL would have used.

13. **P2 - Profile popup re-enables a global-only visor enable checkbox.**
   The XAML says visor enable is global-only and starts disabled, but `SetVisorSlidersEnabled`
   enables `VisorEnabledCheck` for custom profiles. `Save_Click` writes the value, while the DLL
   intentionally ignores per-app `mask_enabled`. This invites a UI promise the runtime will not keep.

14. **P2 - Profile global mode initializes the curve slider from stale per-app state.**
   When `visorSize <= 0` means "use global", most visor controls load global values, but
   `VisorCurveSlider` still uses `1.0 - maskCorner` from the app profile instead of
   `_globalMaskCorner`. Saving can accidentally turn an old per-app curve into a fresh override.

15. **P2 - Per-app `mask_horizontal` is decoded with a render-scale helper.**
   `LoadConfig()` reads `mask_horizontal` through `MillisToRenderScale`, whose minimum clamp is
   `0.1`, not the UI's `0.01` mask range. Legacy/visual-mask per-app values below 10% cannot
   round-trip correctly.

16. **P2 - Curve right-click reset disagrees with the default config.**
   The bundled config default is `mask_corner=0.5`, which maps to curve slider `0.5`, but
   `MaskSliderReset_RightClick` resets the curve slider to `0.75` and writes `mask_corner=0.25`.

17. **P3 - `build.ps1` copies native DLLs into publish output before rebuilding them.**
    The local publish/dev-test folder can receive stale native layer DLLs from a previous build.
    The MSI sources DLLs from the rebuilt Release paths, so the shipped MSI may be correct while
    local publish-folder testing is misleading.

18. **P3 - Legacy `visibility_mask_visor` path diverges from current visor geometry.**
    `ApplyVisorMask()` uses a symmetric closed superellipse and ignores open-inner mode, apex,
    inner-low/bridge controls, AA, and HD. If `visibility_mask_visor=1` with Technique off, the
    hidden visibility-mask visor no longer matches the UI or Direct C renderer.

19. **P3 - Canonical docs/contracts are stale or contradictory.**
    `docs\CONFIG.md` still labels bridge rise/peak/steepness as global-only and says AA/HD native
    wiring is Pass 3, while the code now reads/writes those paths. `CHANGELOG.md` has no 4.1.108
    entry for the pin-drag correction. Contract tests pass while missing the behavioural failures
    above: safe UI fallback, MSI default-ini packaging, edge-mask key wiring,
    `crop_outer_edges_only`, apex drag inverse math, profile use-global save semantics,
    global-only enable state, and release/late draw feature gating.

## Bug scan fix pass - 2026-07-10

Confident fixes implemented in-tree for findings 1, 2, 4-17, and 19:

- UI missing `mask_size` now falls back directly to `0.82`; curve reset is back to `0.5`.
- Default `xr-viewlab.ini` is included in publish/MSI output, and `build.ps1` copies native DLLs
  after MSBuild so publish-folder testing does not use stale layer binaries.
- Edge Masks "Both" controls write `visual_mask_only` / `horizontal_visual_mask_only`; unsupported
  one-sided controls are disabled until the DLL has matching behaviour.
- Native reads/logs `crop_outer_edges_only`, recommended-size scaling no longer depends on
  `fovMutable`, and per-app `mask_horizontal` uses mask-scale decoding.
- Preview apex pin dragging now uses the same centre-origin inverse as the rendered pin position.
- Profile "Use global visor settings" saves as global, disables preview pin edits in global mode,
  keeps unsupported per-app visor enable disabled, and uses safe bridge/curve fallbacks.
- Direct C/edge-guard late fallback flags are independent, open-inner AA feathers top/bottom bands,
  and D3D11 visor/edge draw state restore now covers all RTV slots and viewport slots.
- Contracts now cover the fixed behaviours above.

## Residual cleanup - 2026-07-11

The two previously documented residuals are fixed in-tree:

- Finding 3: the MSI no longer runs current-user work from VBScript, and ordinary upgrades now
  preserve all live user settings. A rejected intermediate design used a changing version marker
  and would have erased visor tuning after every update. The Start Menu component key path also
  moved from HKCU to HKLM.
- Finding 18: `visibility_mask_visor=1` is retired. The DLL logs and ignores the key so the
  legacy hidden-mesh reshaper can no longer diverge from Direct C visor geometry.

No known residual from the original 19-finding scan remains intentionally unfixed. Headset
validation is still required before pushing or publishing.

Additional cleanup from `docs/BUG_SCAN_2026-07-10.md` fixed in the same pass:

- Technique A no longer passes a null release-info pointer on wait failure.
- D3D11 rasterizer/blend/depth-state creation is checked before the renderer is marked initialized.
- Release-time draw paths hold COM references to swapchain textures while drawing outside the
  swapchain map lock, including the late fallback path, and draw entry points check for a live
  D3D11 context.
- Partner-eye boundary projection now uses bbox-relative visor extents instead of raw scale values.
- Large visor vertex buffers are heap-backed instead of render-thread stack arrays.
- Native config fallback paths remain absolute when module path lookup fails.
- Native config is now a stable game-start snapshot; unsafe mid-frame hot reload was removed in
  line with the UI's existing "restart the VR game" guidance.
- D3D11 draw hooks and renderer/session teardown now share a dedicated renderer lock, preventing
  the immediate context or state objects being released during a concurrent draw.
- UI/profile defaults now agree on `mask_corner=0.5` and `mask_rounded=1`.
- Profile window global/custom toggling restores the original custom values and refreshes preview.
- 32-bit layer files are installed from a 32-bit component under `ProgramFilesFolder`.
- `build.ps1` rejects non-Release packaging, checks native exit codes, and normalizes version parsing.

Remaining broader static-audit items not closed in this pass:

- `docs/BUG_SCAN_2026-07-10.md` P1.1/P1.2: a complete D3D11/config threading refactor is still
  pending. This pass reduced the biggest release-time texture lifetime risk with COM AddRef/Release
  and context guards, but did not convert the whole config/global renderer state to immutable
  snapshots or a dedicated renderer-state lock.
- Low-priority audit cleanups P3.5/P3.7/P3.8 remain documentation/cleanup-level items: redundant
  per-app fallback INI reads, legacy custom-install migration coverage, and intentionally hidden
  legacy bias/curve keys.

## Environment facts

- Test game: Pistol Whip, `D:\VR Games\Pistol Whip-working\Pistol Whip.exe`, via Virtual
  Desktop / VDXR, Quest 3. ReShade in that folder: Home = menu, PrintScreen = screenshot
  (screenshots land in `%USERPROFILE%\Documents\ReShade\Screenshots\`) — see `docs/VERIFICATION.md`.
- Games only query the visibility mask at session start — **restart the game** after toggling
  stencil settings.
- Layer registration: HKLM `SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit` (x64) and
  `WOW6432Node` (Win32). A stray 32-bit entry in the 64-bit hive was removed 2026-07-10.

## Latest verification

- Integrated `codex/experiments` into local `master` by fast-forward on 2026-07-17. Every deterministic
  repository fixture and verification script passed on `master`; the overlay-settings verifier now accepts
  the repository's CRLF line endings when checking `notify_theme=0`. `build.ps1` then completed WPF, broker,
  signed identity, x64/Win32 native layers, MSI creation and extracted-payload hash/marker validation with
  zero build warnings or errors: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.240.msi`. The packaged live-notification
  fixture remains a separate installed-broker integration check and was not part of this deterministic pass.

- Music dedup/lifecycle contracts and fixtures passed on 2026-07-14. `build.ps1` then built WPF,
  broker, signed identity, x64/Win32 native layers and validated MSI:
  `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.217.msi`. A real SMTC player, artwork decode and brief
  headset card remain pending live validation.

- OBS WebSocket v5 authentication and `GetRecordStatus` response fixtures, repository contracts and
  native/WPF/broker builds passed on 2026-07-14. `build.ps1` produced validated x64/Win32 MSI:
  `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.216.msi`. OBS was not running and localhost port 4455
  was closed, so live state, headset corners and capture inclusion/exclusion remain explicitly unverified.

- Sticky-note normalization/wrapping/bounds fixtures and repository contracts passed on 2026-07-14.
  `build.ps1` then built WPF, broker, signed identity, x64/Win32 native layers and validated MSI:
  `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.215.msi`. Note fusion, physical scale and the show/hide
  bind remain pending headset validation.

- Performance-trace fixtures round-tripped real samples, exact marker timestamp/sequence and the
  marker-to-sample association on 2026-07-14; repository and HUD contracts passed. `build.ps1` then
  built WPF, broker, signed identity, x64/Win32 native layers and the validated MSI:
  `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.214.msi`. In-headset bind acknowledgement and a real
  completed-session graph remain pending headset/game validation.

- Network rolling-window fixtures, the real telemetry-worker smoke test, performance-HUD contracts
  and repository contracts passed on 2026-07-14. `build.ps1` then built WPF, broker, signed identity,
  x64/Win32 native layers and the MSI with fresh payload validation:
  `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.213.msi`. The smoke test exercised the configured ICMP
  path; HUD placement, legibility and runtime overhead remain pending headset validation.

- Clock/session formatter fixtures, slider-default fixtures, iRacing fixtures, RenderPolicy fixtures,
  performance-HUD contracts, Topmost safety contracts and repository contracts passed on 2026-07-14.
  `build.ps1` then built WPF, broker, signed identity package, x64/Win32 native layers and MSI with
  validated fresh payload hashes: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.212.msi`. Clock widget
  binocular appearance and Direct/Topmost behaviour remain pending headset validation.

- `Tests\Verify-ViewLabContracts.ps1` passed on 2026-07-13. `build.ps1` then produced 4.1.209
  with 0 warnings / 0 errors for WPF, broker, signed identity
  package, x64 native, Win32 native and WiX MSI; extracted payload version and fresh hashes passed:
  `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.209.msi`.
- All HUD, Topmost and iRacing deterministic suites then passed. A fresh independent
  packaged Windows toast reached the installed production broker as exactly one card (ID 2). See
  `VIEWLAB_RELEASE_VALIDATION.md` for the deliberately pending 4.1.209 headset/game safety matrix.

- `Tests\Verify-ViewLabContracts.ps1`, `Tests\Verify-PerformanceHud.ps1`, and
  `Tests\Verify-TopmostSafety.ps1` passed on integrated `master` on 2026-07-13. `build.ps1` then
  produced 4.1.195 with 0 warnings / 0 errors for WPF, x64 native, Win32 native, WiX MSI, extracted
  payload version and fresh-binary hashes: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.195.msi`.
- Manual headset evidence after 4.1.103 is now recorded without pretending that the incomplete old
  log meant no testing occurred. See `VIEWLAB_VALIDATION_HISTORY.md`.

- `Tests\Verify-ViewLabContracts.ps1` and `Tests\Verify-PerformanceHud.ps1` passed on 2026-07-13.
- The isolated native telemetry smoke test produced 20 samples in 5 seconds, full six-input SYS
  coverage, and 15.625 ms worker CPU time (0.3125% of one logical CPU over wall time).
- `build.ps1` passed on 2026-07-13 with 0 warnings / 0 errors for WPF, x64 native, Win32 native,
  WiX MSI, extracted payload version and fresh-binary hashes:
  `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.194.msi`.

- `Tests\Verify-ViewLabContracts.ps1` passed on 2026-07-11 after the 4.1.113 build.
- `dotnet build xr-viewlab.csproj -c Release --no-restore` passed with 0 warnings / 0 errors on
  2026-07-11.
- `.\build.ps1` passed on 2026-07-11 with 0 warnings / 0 errors for WPF, x64 native, Win32 native,
  and WiX MSI:
   `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.113.msi`.
