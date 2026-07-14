# STATE — live project state

> Single source of truth for "where are we". Update this file in the same commit as any
> behavior change. Do not create handoff/status/session documents — this is the only one.

**Updated:** 2026-07-14
**Current version:** 4.1.214 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.214.msi`
**Validation state:** recent builds received repeated manual Pistol Whip and DiRT Rally 2 headset
testing, but the old state log failed to attach every observation to an exact build. 4.1.103 is the
narrow confirmed reference for its stencil repair, not the last headset-tested build. See
`VIEWLAB_VALIDATION_HISTORY.md`; 4.1.202 has real packaged-notification desktop validation and
4.1.209 failed its first precise Pistol Whip headset matrix. 4.1.210 repairs those observed failures
and has clean contracts/fixtures plus WPF, broker, x64/Win32 native, signed identity package, MSI and
extracted-payload validation. Its narrow Pistol Whip headset matrix is mandatory before DiRT.
**Publish state:** 4.1.148 published at the user's direction (2026-07-12): https://github.com/Cooooked/xr-viewlab/releases/tag/v4.1.148 — includes the installer-safety repair and the binocular fixed-reference preview.

## Experimental cleanup (complete; 2026-07-14)

The isolated generic technical-history experiment is removed from UI, broker, persistence, tests and
canonical documentation without changing live notification or racing presentation. The Topmost
failure latch is now atomic across release/end-frame paths. The reusable right-click slider reset
helper and its invariant-culture fixtures are retained.

## Clock and VR session timer (implemented; headset validation pending, 2026-07-14)

A dedicated compact two-line visor card shows 24-hour local time and elapsed time since the current
successful `xrCreateSession`. Elapsed time uses monotonic uptime, resets at `xrDestroySession`, and
is independent of notification and performance-alarm state. Position, angular scale and opacity are
configurable in ordinary settings and read at the next VR session. Formatter fixtures, WPF and x64
native builds pass; binocular fusion, physical scale, legibility and Direct/Topmost presentation still
require headset validation.

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

## Visible visor feature backlog (ordered)

1. **Clock/session timer:** base clock + elapsed session card implemented; stopwatch/countdown modes
   remain a later extension after the base widget passes headset validation.
2. **Network HUD:** implemented; headset validation pending. Optional PING/LOSS/JIT/NET widgets
   probe a labelled configurable IPv4 path once per second. They do not claim game-server telemetry.
3. **Performance trace markers:** implemented; build/headset validation pending. Bind, exact QPC
   timestamp, sequence, visor confirmation, real trace storage and post-session graph navigation are wired.
4. **Post-session performance recording:** bounded actual/target recorder and marker viewer implemented;
   zoom/range inspection and spike summaries remain later extensions.
5. **Sticky note:** one short wrapped note with position/size/opacity and a show/hide bind; no note manager.
6. **OBS indicator:** subtle recording/stream border or corner. Whether ViewLab can be excluded from
   the captured output remains an explicit real-capture-path test question.
7. **Music change card:** PC-side track-change detection feeding a brief title/artist/artwork card;
   no permanent media controls.
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
