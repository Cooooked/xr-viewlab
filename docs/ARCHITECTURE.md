# Architecture

## Overlay profile resolution and factory baseline

`OverlaySettingsModels.cs` owns the six-overlay catalogue and `OverlayProfileOverrides`. `MainWindow` snapshots
global keys and persists app overrides; `ProfileWindow` uses the shared categories and `BeanMaskEditor` layout path.
`dllmain.cpp::LoadConfig` resolves active-app overrides and marks their features so live mappings cannot replace
them. The native layer also publishes the selected executable key in `Local\XRViewLabActiveProfileV1`, allowing the
out-of-process notification broker to resolve the same profile's notification filters and presentation options.
`FactoryBaseline.cs` loads `config/factory-baseline-v4.1.255.json`; `MainWindow.EnsureConfigMigrated` owns its
one-shot allowlisted write. `CalibrationSuite.cs` owns the ten-pattern state machine and future capture seam.

> How ViewLab actually works, mapped to owning symbols. Grep the symbol; don't read the file.
> Update this document whenever a subsystem's shape changes.

## Universal compatibility contract

Compatibility is selected from observed capabilities, never product identity. Every ViewLab feature uses
one `FeaturePresentationPlan` built from the active graphics binding, writable eye textures, projection
presence and submitted layer structure. New graphics/runtime support extends bridge capability adapters;
it does not add feature-local or executable-local routing. The plan may retain direct presentation, add an
ordered composition layer, or degrade an individual feature with a diagnostic. ViewLab remains attached and
crop/config plumbing remains operational whenever the underlying OpenXR calls are available.

The intended legacy end state is game -> ViewLab Bridge -> active OpenXR runtime. Until ABI and behaviour
parity are complete, the external translator remains a validation baseline rather than a permanent product
architecture dependency.

## Product components and route boundary

```
XRViewLab.UI (WPF, .NET 8)                    dllmain.cpp (native OpenXR implicit layer)
  MainWindow writes ──► %LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini ──► LoadConfig() reads
  ProfileWindow writes ─► HKCU\Software\cooooked\xr-viewlab\Apps\<exe> ─► ReadProfileDword()
```
`ViewLabBridge/` is a native static-library boundary linked into the layer. It owns decisions made from
runtime/frame capabilities and normalized legacy texture-bound conversion. The public UI never exposes
transport names. The external legacy translator remains active while translation responsibilities move
behind this boundary incrementally.

Current runtime flows are:

1. Native OpenXR: game -> OpenXR loader -> ViewLab implicit layer -> active runtime.
2. Legacy translated: legacy loader -> external translator -> OpenXR loader -> ViewLab layer -> active
   runtime. ViewLab sees translator-owned D3D11 swapchains and submitted OpenXR layers.
3. Feature fallback: non-D3D11 sessions retain crop/FOV/resolution changes but skip native texture drawing;
   compositor allocation/submission failure resumes direct drawing where the eye texture is writable.

For a plain stereo projection all enabled common features are drawn at `xrReleaseSwapchainImage`, before
the runtime consumes the image. If observed topology shows another compositor layer can cover that
texture, `ViewLabBridge` selects the established transparent stereo projection
carrier. Its two array slices, format, dimensions, space, eye poses and FOV come from the current submitted
projection; it is appended after application layers. Direct presentation owns the allocation transition,
then the ready ordered carrier becomes the sole path for every normal ViewLab feature. Ordered failure
returns all normal features to direct presentation together. A compositor accepting an unverified quad is not
treated as evidence that the quad is visible. No application identity participates.
There is no live IPC between UI and layer (except ReShade Remote, below). Crop/FOV/resolution and
per-app profiles remain a stable startup snapshot; restart the game after changing those settings.
Global visor controls are the deliberate exception: the UI commits `visor_live_revision` last, and
the layer reloads only visor geometry values at the locked `xrEndFrame` safe point. Mid-frame hot
reload remains forbidden because it raced native render hooks. The full key contract is
`docs/CONFIG.md`.

The performance HUD hooks `xrWaitFrame`, `xrBeginFrame`, and `xrEndFrame` without altering call
flow. `predictedDisplayPeriod` is the active runtime frame budget; QPC measures each matched frame
from `xrBeginFrame` entry through `xrEndFrame` return. CPU uses `GetSystemTimes` deltas. GPU Engine samples are
grouped by adapter LUID and engine ID, restricted to 3D engines, and the D3D11 render adapter is
preferred. A 60-frame rolling window supplies the missed-budget percentage, while a 120-sample
buffer draws frame-time deviation around the runtime target interval.

The live-state mapping is optional. When the settings app is not open, the native layer attempts
to reconnect at most once per second; it never opens the mapping once per submitted frame. When
mapped, it reads the compact generation-stamped snapshot at `xrEndFrame`, so enabled calibration
and live visor adjustments take effect without INI I/O on the render path.

## Native layer anatomy (dllmain.cpp — grep these symbols)

| Subsystem | Owning symbols |
|---|---|
| Layer bootstrap / hook install | `xrNegotiateLoaderApiLayerInterface`, `XRViewLab_xrGetInstanceProcAddr`, `EnsureInitialized` |
| Config read (ini) | `LoadConfig`, `ReadBoolSetting`, `ReadDoubleSetting`, `ReadStringSetting` |
| Config read (per-app registry) | `ReadProfileDword`, `ReadProfileDouble`, `SignedMillisToUnit` (encoding: signed = (v+1)*1000, unsigned = v*1000) |
| FOV / resolution crop (the perf feature) | `XRViewLab_xrLocateViews` (crops FOV tangents), `XRViewLab_xrEnumerateViewConfigurationViews` (reduces recommended size) |
| Renderer pipeline diagnostics | `LocateViewsEvidence`, `XRViewLab_xrCreateReferenceSpace`, `XRViewLab_xrWaitFrame`, `XRViewLab_xrBeginFrame`, `XRViewLab_xrLocateViews`, swapchain acquire/wait/release hooks and `XRViewLab_xrEndFrame`. Bounded verbose `PIPE` records correlate predicted display time, QPC timing, reference-space identity/type, original/cropped locate poses and FOV, submitted layer/view poses and FOV, sub-images, swapchain image lifecycle, prior-layout age and runtime submission result. They are evidence only and never select policy by application identity. |
| D3D11 mask renderer init | `InitD3D11MaskRenderer` (compiles shaders via d3dcompiler; FreeLibrary ordering is load-bearing — REGRESSIONS R1) |
| Visor draw entry (Technique C Direct) | `XRViewLab_xrReleaseSwapchainImage` → `DrawVisorBorderToTexture` |
| Calibration diagnostics | `DrawCalibrationPatternsToTexture`, `DrawCalibrationGridToTexture`, `DrawCalibrationOverlayToTexture`, `AnyCalibrationPattern`, `g_calibrationFrameSerial`; ten optional patterns draw after the visor. `CalibrationSuite` waits for six observed submitted-frame heartbeats per pattern; `ProcessCalibrationCaptureRequest` copies the final left-eye sub-image on the render thread and a worker writes PNG plus verified metadata. `Tests/CalibrationReferenceFixtures` pins pattern images and `Tests/CalibrationSuiteFixtures` pins orchestration, cancellation, failure and restoration. Pixel-measurement tools use literal submitted-texture pixels and the complete eye rectangle. See `docs/CALIBRATION.md`. |
| Overlay coordinates | `OverlayCoordinateResolver`, `OverlayPlacement`; builds shared selected/full-lens tangent bounds from both eyes, chooses one visor-space target, then projects it independently through each eye's read-only FOV and destination viewport. Ordinary overlay anchors and full-widget clamp/size limits use `FullLens`, so crop clips coverage without relocating or deforming features. Current selected FOV still supplies pixels-per-tangent, preserving angular size at the submitted resolution. Literal crop diagnostics may use Render Area. No game projection structure is modified. |
| FOV crop | `ApplyXRViewLabFov`; scales horizontal and vertical FOV tangents only. Asymmetric split crop never rotates or otherwise mutates `XrView.pose`; the former foveated-centre pose compensation is retired. |
| Automatic ordered overlays | `ViewLabBridge::SelectOverlayBackend`, `EnsureTopmostLayer`, `RenderTopmostLayer`, `BlockTopmostLayer`, `DestroyTopmostLayer`, `TopmostSubmission`, `LatchTopmostDemand`; projection-only applications retain direct rendering. A distinct application compositor layer latches ordered demand. When a current projection supplies valid geometry, direct owns the allocation frame, then the proven transparent stereo projection is appended after application layers as the sole normal-feature carrier. Composition-only frames do not promote an unverified carrier merely because submission succeeds. Capacity/runtime/D3D failure restores every normal feature to direct together. `overlay_force_direct=1` remains the diagnostic escape. |
| ViewLab Bridge core | `ViewLabBridge/BridgeCore.h/.cpp`: `RuntimeCapabilities`, `SelectOverlayBackend`, `MapTextureBounds`, `OverlayBackendName`. This is the sole policy boundary for staged legacy translation. Next are legacy ABI exports/interface negotiation, compositor timing and D3D11 texture submission; input and overlay APIs follow. |
| Shared overlay configuration | `OverlaySettingsCatalog`, `LoadCommonOverlaySettings`, `SaveCommonOverlaySettings`, `OverlayControls`, `OverlayResetPosition_Click`, `OverlayFeatureId` and `UpdateOverlayFeatureHotkeys` own enable, optional show/hide key, X/Y, scale, opacity and reset for clock, Performance HUD, trace, sticky note, crosshair and notifications. The global menu uses the same checkbox/label/chevron/divider shell for those sections plus OBS Recording Cue and iRacing Telemetry; expanding the latter two reveals their existing global detail controls. iRacing groups lap, spotter, flag and fuel controls and writes RGB selection through the existing `iracing_spotter_color` key. The unchanged ten OBS Mirror visibility switches are the final menu section. Existing key names preserve layouts; the old sticky bind migrates into the catalogue. There is one common persistence path and one native visibility controller. |
| Native performance HUD | `HardwareTelemetry.cpp/.h` own a bounded Windows/PDH/DXGI/ICMP worker and immutable snapshot, started on demand by `EnsureTelemetryWorker` only while recording, the HUD or the trace overlay consumes samples and stopped at `xrDestroySession`; `viewlab::telemetry::Running()` makes the per-frame re-check lock-free. `NetworkProbe.h` owns deterministic rolling probe maths. `HudWidgetId`, `kHudWidgetRegistry`, `ClassifyHudMetric`, `HudDrawSnapshot`, `DrawCalibrationOverlayToTexture`, and `RenderPolicy.h::UpdateSustainedAlarm` consume it. Every widget has persisted warning/critical values with correct higher/lower semantics. Literal compact labels and explicit units replace unrelated pictograms; the same terminology is used by session events. All enabled widgets pack into one proportional row and both eyes reuse one draw snapshot. |
| Performance Trace + Session Graph | `hudTraceVisibilityMode`, `CapturePerformanceTraceMarker`, `SavePerformanceTraceSession`, `PerformanceTrace`, `PerformanceTraceWindow` and `PerformanceTraceLibraryWindow`. Recording is opt-in (`performance_trace_recording`, default 0): while off there is no collector thread, no reserved ring, no marker key polling and no CSV. When on, once per second the telemetry worker appends and flushes the current uniquely named `session-*.csv`; `latest.csv` remains a compatibility alias. The DiagMonster-owned browser retains multiple sessions, opens prior graphs, compares selected FPS/P99 results and provides confirmed explicit deletion. Periodic rebuilds preserve the bounded one-hour ring; correctness does not depend on shutdown callbacks. |
| Visor overlays (boundary/crosshair/notifications/racing events) | `DrawViewLabOverlaysToTexture`, `EnsureNotifyCardTexture`, `ConsumeRacingState`, `AnyViewLabOverlay`, `EvaluateNotificationAnimation`, `kOverlayPS`, `kTexturedPS`; flat overlays use explicit colour and cards use textured quads. The broker composes notification cards via `NotificationService.ComposeCard` and `NotificationCardLayout` (shared fixed 336×96 slot contract); `PadToSlot` expands every theme footprint to the native texture stride. The broker publishes notification lifecycle timestamps; native evaluates fade/slide every VR frame and applies alpha once. `RacingStateService` publishes generic spotter/flag/lap semantics plus non-persistent presentation-test flags through its 64-byte mapping. Native racing edge cues draw against the submitted post-crop eye rectangle, and the desktop `RenderEdge` preview mirrors the inset cue on the current crop rather than the full-lens guide. `RecordingRenderEdge` uses the exact crop bounds for the OBS preview and places its label bottom-left; iRacing keeps its top-left label. Crosshair preview uses `CrosshairPreview` and `CrosshairPreview.Measure` with a shared preview-only display scale; real `CrosshairSettings` (size, thickness, gap, outline, colour, alpha, scale, dot, T-style) and native rendering are unchanged. |
| Clock + OpenXR session timer | `ClockWidget.h::Format` supplies bounded 12/24-hour and elapsed text; `LiveStateService` v8 publishes clock/timer/theme/layout/bind changes; `XRViewLab_xrCreateSession`/`XRViewLab_xrDestroySession` own the monotonic epoch. `DrawViewLabOverlaysToTexture` collapses between one and two lanes through the unchanged common stereo presentation path. |
| Sticky note collection | `StickyNote.h::Wrap` owns bounded text normalization/wrapping; `StickyNoteOption` and `StickyNoteLiveStateService` publish at most eight generation-safe records. Native caches the latest stable collection and draws independent square paper cards through the common presentation path. Legacy unindexed settings migrate to note zero; notes never enter notification shared memory. |
| OBS recording cue | `ObsRecordingProvider` implements OBS WebSocket v5 authentication and bounded `GetRecordStatus` polling in the broker, then publishes a 16-byte `XRViewLabObsRecordingState` mapping. States distinguish disconnected, connected-idle, recording, connecting and authentication failure; `NotificationBrokerClient.RefreshObsStatus` presents connection state in the setup UI. `ObsRecordingActive` validates a stable generation and draws only for the distinct recording state. Host/IP and Port compose into the existing `obs_websocket_url`; local and LAN endpoints use the same provider. Because drawing occurs in submitted eye textures, capture exclusion is not an architectural property. |
| Music track-change cards | `MediaSessionEventProvider` subscribes to Windows `GlobalSystemMediaTransportControlsSessionManager`, follows its current session, deduplicates trimmed title+artist, decodes optional thumbnail artwork and emits `MediaTrackInfo`. `NotificationBrokerProgram` dispatches a brief existing-pipeline card when `media_notify_enabled=1`; pause/seek/volume changes and unchanged metadata emit nothing. Start/stop subscribes and unsubscribes the same named handler. |
| Visor geometry — open-inner arch | `BuildOpenInnerEyeVisorVerts` (per-eye, apex-aware superellipse cap + full-width top/bottom bands; exact rectangle at Curve 0) |
| Visor geometry — closed bean | `BuildVisorBorderVerts` (apex-aware superellipse ring fill; exact rectangle at Curve 0) |
| Visor geometry — nose divot | `BuildNoseBridgeCurve` (cubic bezier trapezoid fill, band-clamped to NDC bottom) |
| Visor geometry — partner-eye boundary | `BuildProjectedPartnerVisorVerts` (only in closed-bean mode) |
| Shape helpers | `VisorCurveExponent` (geometric 32·(2/32)^curve), `ApexYFromConfigNdc` (THE one y-flip) |
| Visibility-mask stencil filter | `XRViewLab_xrGetVisibilityMaskKHR` (`keepOuterEdge` filter; runs even when visor active) |
| Legacy visor paths (hidden, kept for reference) | Technique A `RenderVisorCutout`/`InitVisorQuad`, Technique B interception, `visibilityMaskVisor` reshaper `ApplyVisorMask` |
| Logging | `Log` → `%LOCALAPPDATA%\XR ViewLab\ViewLab.log`; config snapshot logged by `LoadConfig` |

### Coordinate conventions (bug factory — read before touching geometry)

- UI preview (`BeanMaskEditor.cs`) is in screen coords: **y grows DOWN**.
- Native draws in D3D NDC: **y grows UP** (+1 = top of the eye texture rect).
- The flip between them lives in exactly ONE native helper: `ApexYFromConfigNdc`. The nose divot
  anchors to NDC `y0` (bottom). Anchoring anything to `y1` "like the UI does" recreates the
  divot-on-top-of-lens regression (REGRESSIONS R2).
- Left eye (viewIndex 0): outer = left = −x, inner/nose = right = +x. Right eye mirrored.

### Geometry parity rule

`BeanMaskEditor.cs` is the reference spec. Native geometry (`BuildOpenInnerEyeVisorVerts`,
`BuildVisorBorderVerts`, `BuildNoseBridgeCurve`, `VisorCurveExponent`) must stay formula-identical
to the preview (`AddOpenInnerHalf`, `BuildGeometry`, `AddNoseBridgeCurve`, `CurveExponent`),
modulo the documented y-flip. The contract test pins parts of this.

`mask_size` uniformly scales only the exposed aperture. Its compatibility default is `1.0`, which
preserves the full existing opening; lowering it hides an outer band without changing crop
tangents, submitted FOV, or recommended render resolution. Peak X is mirrored by passing the
same sign-aware Bezier formula to each eye; its extended `-0.5..1` range is intentionally stronger
on the left end. `mask_nose_spread_x` translates the left-eye nose boundary left and the right-eye
boundary right by the same normalised amount before the shared curve is built; zero is the exact
legacy geometry.

The main visor card keeps the Edge Masks and ReShade Remote pop-outs together above the visor
toggle. `BeanMaskEditor` exposes a draggable preview pin for every shape slider, so slider and
preview editing remain bidirectionally synchronised.

Responsive layout omits the Applications and Render Options sub-headers so the three primary cards
start on the same line in dual- and triple-column layouts. The beta-testers line remains visible
at every width; in triple-column mode it sits directly below the third-column card rather than
extending the applications column.

The responsive client-width cutovers are 280px (mini), 720px (two columns), and 1200px (three
columns). They intentionally favour fewer usable columns over three compressed cards.

### The stencil pipeline ("Stencil outer edges only")

Three cooperating mechanisms, all keyed to ini `stencil_outer_edges_only` (DLL global
`outerEdgeVisibilityMaskOnly`, legacy fallback key `outer_edge_visibility_mask_only`):
1. `XRViewLab_xrGetVisibilityMaskKHR` filters the runtime's hidden-area mesh (e.g. Virtual
   Desktop's FOV stencil) so the GAME only stencils each eye's outer half. Must run even when
   the visor is active — the game stencils whatever this returns (DECISIONS D8).
2. `DrawVisorBorderToTexture` picks visor geometry: ON → open-inner arch, OFF → closed bean
   (`openInnerShape = outerEdgeVisibilityMaskOnly`) — same switch the preview uses.
3. The partner-eye boundary (`BuildProjectedPartnerVisorVerts`) only draws in closed-bean mode;
   in arch mode it would black the nose side (DECISIONS D9).

Visibility-mask filtering is fail-open: an indivisible one-triangle runtime mask passes through
unchanged, and a filter result that would empty a previously non-empty mesh is discarded. Runtime
topology safety takes precedence over the optional stencil optimization.
Games query the visibility mask once per session → restart the game to see stencil changes.

### Horizontal crop modes

`crop_outer_edges_only=1` keeps each eye's inner/nose-side FOV edge fixed and reduces only its
outer edge. With it off, `XRViewLab_xrLocateViews` scales both horizontal edges around each eye's
centre. Recommended render width follows the same selected mode. This must remain a real toggle,
not merely a saved preference.

## DiagMon prototype-to-ViewLab migration architecture

ViewLab is the only product. DiagMon is inspected for proven experiments, but no shipping ViewLab
component reads its folders, invokes its scripts or requires it to be installed. Migrations are serial:
select one capability, define a ViewLab-owned contract, implement the smallest complete slice, prove
parity and overhead, obtain game/headset validation, then remove any temporary comparison path before
choosing the next feature.

### ViewLab-owned session bundle

The table in this subsection is the **target correlated/compressed bundle contract**, not the current
on-disk implementation. The implemented DiagMon(ster) subsystem described below currently writes JSON,
plain CSV/XML/log evidence, summaries, SVG graphs and annotations in a portable session directory. It
does not yet claim `.zst` chunks, correlated QPC presentation rows, rotated deep chunks or storage-cap
enforcement. Those remain staged migration work rather than facts about the shipping collector.

One session is a directory named by a UUID and written as `.partial` until finalised:

| File | Required content |
|---|---|
| `session.json` | `schema_version`, UUID, ViewLab/collector versions, application path/hash, PID and parent PID, runtime/headset identifiers when available, start/end reason, `start_utc`, `end_utc`, `qpc_frequency`, `anchor_qpc`, `anchor_utc`, capture mode, completeness and dropped-record counters |
| `frames.csv.zst` | QPC-keyed PresentMon presentation records plus ViewLab OpenXR actual/target/app/wait/submit/display fields; source and process identity remain explicit |
| `telemetry.csv.zst` | Typed metric rows: source, metric ID, value/unit, scope (system/process/adapter/engine), validity and QPC timestamp |
| `events.jsonl.zst` | Session lifecycle, F8 markers, alarm transitions (metric, raw value, threshold, prior/new severity, entry/recovery/hold reason), process/runtime changes, provider errors and diagnostic annotations |
| `deep/` | Optional rotated ETL/provider chunks and their checksums; absent in ordinary mode |

Writers checkpoint to temporary chunks and atomically update `session.json`; completion sets `end_utc`,
`end_qpc` and `complete=true` before removing the `.partial` suffix. Readers may recover a partial
session using the last valid chunk and dropped-record counters. QPC is the ordering authority;
UTC is an anchored display/cross-machine correlation aid. Metric and event IDs are stable strings,
unknown IDs are ignored, and additive fields do not require a major schema bump.

The current compact `latest.csv` is the compatibility bridge while that bundle is migrated. Schema 2
retains every schema 1 timing column and adds `start_utc_filetime`, the worker's already-collected GPU
value, and warning/critical/visible alarm masks. The reader accepts both versions and treats a malformed
tail as an interrupted checkpoint. Its completed-session viewer uses min/max-per-pixel buckets, robust
visible-window scaling, labelled axes, series toggles, budget guides, zoom/pan and exact nearest-sample
inspection. Cadence misses derived from OpenXR intervals are explicitly estimates; only the PresentMon
migration may label an event as a measured dropped presentation.

### Migration order

1. **PresentMon-backed presentation capture:** true present mode, displayed-frame pacing, CPU/GPU/
   display durations and latencies, dropped-frame evidence, percentiles and stutter events correlated
   to ViewLab's OpenXR samples by QPC. This closes the largest current evidence gap.
2. **Target-process telemetry:** CPU, working/private/commit memory, page faults, I/O, handles and
   threads for the game/runtime/streamer processes, sampled slowly enough to remain negligible.
3. **Per-process GPU engines and memory:** adapter-LUID/engine-scoped utilisation and dedicated/shared
   memory so a saturated system GPU metric can be attributed rather than guessed.
4. **Deeper system diagnostics:** scheduler/DPC/ISR, storage, power/thermal throttling and selected
   event evidence, only where a compact provider exists. WPR/ETL remains explicit deep mode.

Each migrated collector has exactly one ViewLab owner. OpenXR timings, predicted display cadence,
visor configuration, markers and exact alarm transitions remain native ViewLab evidence. Unsupported
measurements are unavailable, not inferred or reported as zero.

### Capture budgets

Ordinary mode samples bounded telemetry at 1–4 Hz, keeps frame/present rows compact, compresses closed
chunks, and uses both age and size retention (proposed defaults: 30 days, 20 sessions, 250 MB total;
oldest completed session first). It never enables ETL. Deep-diagnostic mode requires an explicit start,
shows the active size/time budget, defaults to 10 minutes, rotates compressed 256 MB chunks and stops
at a 2 GB cap unless the user deliberately chooses another bounded cap. Cancellation and process exit
finalise the recoverable manifest; cap exhaustion records a stop reason rather than silently continuing.

### First migration stages: presentation capture

1. Pin the supported PresentMon API/collection version and required MIT notice; create deterministic
   fixture rows for present modes, dropped presents, clock anchors, malformed tails and process exit.
2. Add a ViewLab-owned out-of-process capture component. It targets the OpenXR game's PID and writes
   bounded chunks; the implicit layer only publishes session identity/QPC anchors and never runs ETW.
3. Extend the completed-session reader with correlated presentation rows and summary statistics while
   preserving `latest.csv` and the current graph as the fallback.
4. Validate capture start/stop, crash recovery, no-data/access-denied behaviour, CPU/disk overhead and
   storage caps on desktop; then compare one DiRT run against the existing prototype output.
5. Require user-confirmed DiRT and Pistol Whip game/headset regression passes. Remove the comparison
   path, declare ViewLab sole owner, and only then select target-process telemetry as the next migration.

The 2026-07-14 high-resolution DiRT baseline is the failure fixture for stage 4: the prototype launched
its PresentMon child and later reported capture completion, but no frame CSV existed. It also retained
7.23 MB of raw GPU rows in memory until process exit. The ViewLab implementation must surface a missing
or failed collector, stream bounded recoverable chunks during the session, and never equate an
orchestrator's clean exit with valid frame evidence.

### Implemented DiagMon(ster) desktop subsystem

`DiagMonWindow` is the cockpit; `DiagMonCaptureService` owns collector processes and low-rate sampling;
`DiagMonStore` owns atomic JSON, `.partial` recovery, index updates, robust history selection, summaries,
retention warnings and ZIP export. `DiagMonLibraryWindow` owns browsing and user validity/classification;
`DiagMonSessionWindow` is the evidence reader. All storage is below `%LOCALAPPDATA%\XR ViewLab\DiagMon`.
`DiagMonDarkStyles.xaml` supplies the shared black dropdown-item, table-header, row and cell theme to every
DiagMon(ster) window so light text never falls back onto system-white control surfaces.

Standard mode runs PresentMon, target-process samples and Windows typeperf counters without ETW.
Detailed adds loaded modules and graphics-API detection. Trace adds an explicit WPR GeneralProfile with
the configured time cap. Every launched collector process is retained by handle and only that owned
process is stopped. Application/System warnings and errors are queried for the bounded capture window
after stop; View Lab config/log/trace files are copied read-only at session boundaries.

The MSI carries the pinned MIT-licensed PresentMon 2.4.1 console and its notice. ViewLab consumes
`--output_stdout`, checkpoints new CSV rows and flushes them through Windows at least once per second,
then terminates the unique `ViewLab-<session>` ETW session cleanly so buffered rows are not discarded.
Target exit and the Trace deadline both enter the same serialised finalisation path as the Stop button,
including collector and WPR shutdown. Detailed module/API evidence is sampled on attachment while the
target is alive and refreshed at stop when possible. Fixture stores are rooted in temporary test data,
never the production `%LOCALAPPDATA%` index.

Analysis is three separable layers: `ParsePresentMon` and resource aggregation calculate observations;
`BuildComparison` selects validated like-for-like sessions and calculates median/IQR baselines; and
`WriteSummary` renders conservative prose. A fingerprint records target, graphics API, display/render
configuration and capture mode. Exact matches are direct; compatible same-executable matches are partial;
major resolution/API/refresh/frame-cap/hardware/driver mismatches are excluded with reasons.

## WPF app anatomy

| Subsystem | Where |
|---|---|
| Settings load/save | `MainWindow.cs` `LoadSettings`/`SaveSettings` + `WritePrivateProfileString`; key constants at top of class |
| Visor preview + pins | `Quest3PreviewGeometry.cs` owns the fixed `55:48` full-lens box, crop rectangle, guide geometry, calibrated replica sizing and paired `ResolveFullLens`/`InvertFullLens`/`ApplyFullLensDrag` transforms. `BeanMaskEditor.cs` (`OnRender`, `DrawOverlayPreviews`, `OverlayPreviewGeometries`, `HitOverlayHandle`, `PinPositions`, mouse handlers) is the shared main/profile editor. Outer frame, eye/oval guides, equal Top/Bottom crop, visor, crosshair, widgets and render-edge cues use one shared visual centre. The default is geometric; `preview_optical_centre` optionally moves the complete display coordinate system together and is passed to per-app previews. Ordinary widget X/Y remains normalized to the full lens exactly as native `OverlayPlacement::FullLens`; crop clips visibility and never redefines coordinates. Zoom/pan are removed before hit-testing and remain display-only. A live hover inspector reports full-lens and post-crop normalized coordinates through `InvertFullLens`, so main/profile inspection and drag inversion share one transform. Saved coordinates and runtime rendering are unchanged. A non-widget right-click reuses `ResetViewToStartupFit`. Active visor geometry is red; disabled geometry is grey. Edge-only cues use their explicit render-edge contracts. |
| App list / per-app profiles | `MainWindow.cs` app-table region, `AppProfile.cs`, `ProfileWindow.xaml`/`.cs`; registry encode helpers `ToMillis`/`ToSignedMillis`/`FromMillis`/`FromSignedMillis`. The profile visor editor uses the same `BeanMaskEditor` geometry and Size/Width/Height/Curve/Outer Dip/Nose/Nose Spread X ranges as the main editor. A custom visor persists its enable and complete shape; `Use global visor settings` writes the `visor_size=0` sentinel and removes custom visor keys. The distinct `Use Global Values` action is the only whole-profile reset. After Save, the table is reloaded from the registry. |
| ReShade Remote | `ReShadeRemoteWindow.cs` + `ReShadeControlService.cs` + `BuiltInHelpWindow.cs`: scrollable concise control panel, focusable-by-default desktop preview, revision-gated shared-memory control, independently managed payload files/manifest registration, post-attachment heartbeat handshake, and built-in help for `ReShadePayload/`. `ReShadePayloadSource/` is the canonical modified ReShade source. Its `ReShade` CMake target builds `build/Release/ReShade64.dll`; the fork owns the Home menu, `Local\ReShadeXRControl`, OpenXR/OpenComposite quad route and persistent desktop mirror lifecycle. See `ReShadePayloadSource/README.ViewLab.md` and `ReShadePayload/Docs/SOURCE_BUILD.md`. |
| Update check | `UpdateRelease.cs` + GitHub releases endpoint in `MainWindow.cs` |

Split vertical crop has an explicit UI/storage boundary. Top and Bottom controls are each relative to their own
half of the lens, so each contributes `control × 0.5` of full height. INI and per-app tangent values remain
full-lens shares for native compatibility; main and profile editors convert ×2 on load and ×0.5 on save.

`ProfileWindow.xaml` owns the PowerUp/profile popup chrome, including its dedicated ViewLab-themed
scroll viewer. The scrollbar occupies a separate grid column, never an overlay on visor controls.

## Overlay coordinate contract

ViewLab deliberately uses several coordinate spaces; treating them as interchangeable creates
stereo disparity:

- **Per-eye texture pixels:** the submitted `XrSwapchainSubImage.imageRect`. Visor geometry,
  boundary clipping, and calibration diagnostics ultimately rasterize here.
- **Per-eye cropped tangent space:** `tan(angleLeft/Right/Up/Down)` from each submitted projection
  view. `x = left + (tanX-selfLeft)/(selfRight-selfLeft)*width`; Y is the corresponding inverted
  mapping. Each eye has different bounds and therefore usually different pixel coordinates for the
  same angular point.
- **Shared binocular-overlap space:** the intersection of both eyes' cropped tangent bounds. HUD,
  trace, and notification X/Y fractions select one shared tangent point inside this intersection,
  which is then projected independently into each eye.
- **Calibrated angular space:** the crosshair uses shared tangent `(0,0)` (parallel straight-ahead
  rays, zero unintended angular disparity). Its CS pixels are fixed tangent spans (`2/1080`) and
  become eye pixels through that eye's pixels-per-tangent density. Crop changes therefore change
  clipping/density, not convergence or angular size.
- **Combined visor-preview space:** a centred, normalised representation of the headset view inside
  the fixed Quest 3 `55:48` box. Crop values are direct retained fractions of that box; no projection
  degree/tangent conversion or second crop is applied. Overlay anchors, hit-testing and drag deltas use
  the same full-box coordinates. It shows one resolved crosshair at the crop's visual centre rather than
  two unequal native per-eye pixel projections.

The performance HUD and frame-trace positions are shared-overlap tangent points. Their base size is
angular; trace width remains intentionally relative to the available visible overlap because it is
a user-facing width control. Notification cards use one tangent width/height and each eye's own
X/Y density. The render-boundary flash is the exception: it is a per-eye inner outline of the final
submitted rectangle, with angularly stable stroke thickness and inset beyond half its stroke so
scissoring cannot remove it.

**Direct projection-context invariant (repaired 2026-07-17):** `ProjectionFrameContext` owns the complete ordered
view set for the selected primary projection layer. Every entry binds submitted pose/FOV and the exact
`XrSwapchainSubImage` destination. `TrackedSwapchain` owns only image lifecycle and D3D11 resources. A release selects
all entries targeting that swapchain, then draws each destination against the complete projection context; texture
ownership therefore cannot collapse binocular coordinates. The representation is independent of packing and covers
all valid primary-stereo sub-image topologies: one array swapchain, separate swapchains, atlas rectangles in a shared
slice, overlapping targets in required view-array order, and arbitrary mixtures of handle, slice and rectangle.
Release order is irrelevant. A destroyed destination invalidates the prior context until the next projection submit.

Original full FOV comes only from the locate record matched to the submitted session/display time and only when the
application submitted that located cropped FOV unchanged. Otherwise the submitted FOV is authoritative, as OpenXR
permits applications to alter located pose/FOV. Submitted poses remain in the context for future orientation-aware
policy; the matched Pools/Pistol traces reject pose differences as this incident's cause. This claim concerns
sub-image topology within ViewLab's selected primary-stereo D3D11 projection path. Multiple independent projection
layers and non-stereo view configurations remain separate capability/presentation questions; they are not silently
relabelled as swapchain layouts.

Calibration grid, ruler, gratings, colour bars, beacon, edge probes, checkerboards, zone plate,
clipping steps, and motion strip are explicitly **per-eye texture diagnostics**, not fused visor
content. Their job is to reveal downstream scaling/encoding, so their pixel-exact geometry remains
crop-relative by design. The visor mask likewise follows each submitted eye rectangle and its
existing shape contract; it is not repositioned as a binocular overlay.

## Build & packaging

`build.ps1` (the only build entry): bumps version in `Properties/AssemblyInfo.cs` +
`Installer/Product.wxs` → `dotnet publish` WPF (win-x64 self-contained single-file) → copies the
repo default `xr-viewlab.ini` into publish output → MSBuild native layer x64 then Win32 → copies
the freshly-built layer DLLs to dist + publish dir → WiX MSI → `dist\ViewLab-<version>.msi`.
The publish path is derived from `xr-viewlab.csproj`'s `TargetFramework`; generated WPF, native,
and WiX outputs are removed before their respective builds. After WiX completes, the script
administratively extracts the MSI and fails unless its app version and WPF/native/broker/identity
hashes match the fresh outputs, the pinned PresentMon hash and MIT notice match their repository
sources, and the compiled app contains the Overlays/boundary/crosshair/notification/iRacing markers.
This prevents a target-framework change from leaving WiX pointed at an older publish tree.

Global visor edits use the same generation-stamped `Local\XRViewLabLiveState` snapshot as HUD and
overlay controls and are always live while the UI is open. Unchanged snapshots cost only the fixed
generation check; there is no per-frame INI parse. Per-app visor overrides retain precedence.
Manual builds must bump versions by hand to avoid double-bumps. MSI registers both layer
manifests under HKLM Khronos ApiLayers (x64 + WOW6432Node), and installs the app, fresh-install
default ini, and ReShadePayload. Upgrades leave the live config in `%LOCALAPPDATA%` and per-app
HKCU profiles untouched.

## 2026-07-11 visor quality / robustness update

- Crop-boundary diagnosis is read-only: a bounded mutex-protected queue correlates primary-stereo
  `xrLocateViews` results to `xrEndFrame` only when both session and display time match. Verbose
  logs compare tangent-space FOVs and submitted sub-image bounds; no submitted data is rewritten.

- The D3D11 visor vertex format is `{x, y, alpha}`. The current opaque path uses fixed
  512-segment curve tessellation, so curved visor boundaries remain smooth at HMD eye-texture
  resolutions without restoring the removed HD or anti-aliasing controls.
- Direct C draws at `xrReleaseSwapchainImage` using cached per-image/per-slice RTVs. The
  `xrEndFrame` draw is fallback-only when release-time drawing did not run for that frame, with
  independent release flags for edge guard and Direct C visor paths.
- `g_rendererMutex` serializes D3D11 immediate-context use and renderer/session teardown across
  OpenXR hooks; `g_swapchainMutex` remains responsible for the swapchain map itself.
- `XRViewLab_xrLocateViews` stores original and cropped FOVs for diagnostics; recommended-size
  logging uses per-view effective horizontal/vertical scale helpers when available.
- MSI upgrades preserve the current user's visor and per-app settings. The OpenXR layer is
  read-only with respect to user configuration; it never performs installer cleanup in a game.
- `visibility_mask_visor` is retired. The old hidden-mesh visor path is ignored because it cannot
  represent the current Direct C shape, AA, or HD contract.
