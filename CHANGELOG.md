# Changelog

## Unreleased - 2026-07-18 (Advanced help and truthful ReShade state)

- Split ReShade payload file deployment from ViewLab layer registration and added truthful Not installed,
  Installed but disabled, Installed and enabled, and live-handshake-only Connected states.
- Added scrollable built-in help with white circular help icons to ReShade Remote and DiagMon(ster).
- Completed the inherited OBS mirror-surface route with independent mirror-only overlay visibility controls.
- Removed the duplicated ReShade explanation from the Remote panel, made the complete In-HMD quad section
  accessible without shrinking controls, and clarified the Desktop Menu wording.
- Fresh desktop previews now start focusable instead of forced headless, and Remote state widgets update only
  when the shared control revision changes. Static contracts pin the Home/shared-memory/OpenXR quad route.
- The Remote window now sizes its height from visible content, retains its existing width and caps itself to the
  current work area; its existing scroll viewer remains the DPI/display-scaling overflow path.
- Recovered the exact modified ReShade fork into `ReShadePayloadSource/`, with original path, upstream commit,
  dirty-file inventory, exact build commands and packaging route documented in-repository.
- Fixed desktop-menu text entry by forwarding Win32 character and control-key input to the VR ImGui context.
  The preview now keeps a joined, reference-counted window thread, repaints only for new frames or real control
  changes, paints black during empty frames, and double-buffers GPU readback to avoid same-frame map stalls.

## Unreleased - 2026-07-18 (profile persistence and centred preview)

- Added visible IPD spinner buttons to the main preview editor. The buttons and keyboard arrows share
  the existing 0.1 mm stepping path; profile previews consume the same global calibration and guide modes
  without exposing a second set of global display-preference controls.
- Made the preview's vertical scale canonical and direct. Vertical `1.0` now uses the exact outer-frame
  top and height, while split top/bottom values retain their existing preview translation.
- Lowered only Clock + Timer and Notifications scale minima to `0.1`. Their sliders, preview resize
  pins, saved-setting load and native startup/live clamps now agree; defaults and maxima are unchanged.
- Separated the per-app `Use global visor settings` choice from the explicit `Use Global Values`
  reset. Saving crop or resolution edits now preserves and enables the app profile, stores only the
  visor as global when requested, and refreshes the main table from the registry immediately.
- Rebuilt the Quest 3 preview as one centred fixed-aspect coordinate space. Crop percentages now map
  directly to the outer box, the visor and all widget anchors use that same space once, and asymmetric
  vertical values translate the crop in the expected direction. The periphery-guide toggle preserves one
  binocular oval mode and adds two overlapping true-circle mode without changing the shared boundary.
  A separate frame-guide toggle selects one combined binocular rectangle or two overlapping per-eye
  rectangles at the actual 2064:2208 eye aspect; both switches are preview-only.
  Added a persisted 67.0 mm IPD calibration input with 0.1 mm steps; it live-adjusts only dual-guide
  overlap and has no effect on crop, visor, overlay placement or runtime output.
- Corrected outer-only horizontal runtime cropping and recommended resolution scaling so `0.8` retains
  exactly 80% of each eye's full horizontal span, with the entire reduction taken from its outer edge.
- Hydrated saved overlay widgets in the preview as soon as settings load, without requiring a toggle.
- Added mirrored Nose Spread X control, INI/profile persistence and live-state/native rendering support.
  Zero preserves the former visor geometry.
- Added deterministic geometry/profile contracts for direct percentage scale, centring, circular guides,
  runtime parity, profile-save intent and startup preview hydration.

## Unreleased - 2026-07-17 (cross-runtime renderer diagnosis)

- Added bounded verbose `PIPE` evidence across OpenXR frame timing, locate-view pose/FOV acquisition,
  reference-space creation, projection-layer submission and the swapchain acquire/wait/release lifecycle.
  Diagnostics now expose whether a release-time draw consumed a stale prior-frame layout and whether the
  submitted projection actually matches the locate result for the same session and display time. This is
  instrumentation only: it introduces no application detection, offsets, renderer-policy or geometry change.
- Replaced per-swapchain stereo-coordinate state with one display-time-correlated, frame-level projection context.
  Released swapchains now select their own destination views while every fused overlay resolves against the complete
  submitted primary-stereo view set. Array, split-swapchain, atlas and mixed sub-image layouts therefore share one
  renderer path. Correlated original FOV is used only when the application submitted the located cropped FOV unchanged.
- Matched headset validation passed in native VDXR split-swapchain (Pools), native VDXR array-swapchain
  (Pistol Whip), OpenComposite split-swapchain (Eleven Table Tennis), and OpenComposite layered/atlas menu and
  cockpit presentation (DiRT Rally 2). PID-bound telemetry showed complete two-view context with zero stale layouts,
  locate misses, form-factor errors or failed frame submissions.

## Unreleased - 2026-07-15 (Product polish consolidation)

- Made clock enable, timer lane, 12/24-hour format, theme, position, scale, opacity and visibility bind
  update live. Added Graphite, Paper, OLED, Amber and Mint clock themes and removed the blue-only card.
- Replaced the single rectangular note with up to eight independently enabled, themed, positioned, scaled
  and faded square paper notes; existing single-note settings migrate into note one.
- Added five notification-card themes and per-widget HUD label/symbol selection. Labels remain the safe
  default while the earlier pictograms can be restored independently for familiar widgets.
- Turned the visor canvas into an overlay placement map: enabled HUD, trace, clock/timer, notification,
  sticky-note, crosshair, OBS and racing cues appear as labelled, scaled placeholders at their configured anchors.
  Ordinary overlay nodes now have live top-centre move and top-right scale handles; their labels and handles
  retain a readable screen-space size while the canvas is zoomed.
- Corrected overlay preview scaling so reference width and height receive one uniform scale transform and
  one aspect-preserving fit. Performance Trace Scale now scales the complete graph while Width defines its
  base shape. Added a dotted Quest 3 full-binocular H/V 1.00 reference around the solid current-crop rectangle.
- Made crop a coverage boundary rather than an overlay coordinate system. Preview and native overlays retain
  their full-binocular position and angular scale as crop changes; content beyond crop remains visible in the
  editor and is naturally clipped in-headset. Added an approximate binocular-visibility oval for the corners
  a user cannot naturally see, and made disabled visor geometry a subdued grey reference instead of active red.
- Removed the four unsupported notch-detail sliders from the main visor editor. The remaining Nose control
  adjusts notch depth against one fixed, repeatable curve instead of exposing unstable curve internals.
- Centralised ordinary overlay enable, show/hide hotkey, position, scale, opacity, reset and persistence
  for clock, HUD, trace, sticky note, crosshair and notifications, preserving existing layout keys and
  migrating the former sticky-only bind. Removed the private clock/reset paths and hidden no-op mask controls.
- Added persisted warning/critical controls to every HUD widget, removed the System-only controls, replaced
  ambiguous pictograms with literal labels and explicit units, and aligned Session Graph event terminology.
- Gave the clock local time a stronger typographic hierarchy; restored independent visor Width/Height plus
  a single Nose control while keeping Size as aspect-preserving uniform scale; simplified Edge Mask to supported
  user-facing choices.
- Reorganised Calibration, Overlays and DiagMonster ownership. Added deterministic full and cropped PNG
  references for all ten calibration tools.
- Archived every native VR performance trace as a unique session while retaining `latest.csv` compatibility.
  Added Session Graph history browsing, selected-run comparison, confirmed deletion, DiagMon selected-session
  comparison, and configurable non-destructive retention guidance.

## Unreleased - 2026-07-15 (Shared presentation regression repair and ViewLab Bridge core)

- Restored the previously working transparent stereo projection carrier for common ViewLab features
  after a head-locked quad was accepted but not displayed by a translated menu compositor. Selection
  uses current graphics/frame capabilities and contains no executable-name policy.
- Added one shared readiness hand-off: direct presentation remains active during ordered allocation,
  then every normal ViewLab feature moves to the front-most carrier together with no duplicate path.
- Made the visor emit guaranteed opaque black pixels on that same shared carrier instead of relying
  on an interpolated alpha semantic that could leave a valid draw invisible.
- Corrected release diagnostics so a backend-suppressed direct draw can no longer be reported as a
  successful visor draw, and added one-shot backend, dimensions, layer-count and submission-result logs.
- Added a compilable x64/Win32 `ViewLabBridge` static-library boundary with capability-driven overlay
  selection and normalized texture-bounds mapping. The current external translation route remains active.
- Removed title-specific render-height advice from the normal settings UI.

## Unreleased - 2026-07-15 (DiagMon capture lifecycle repair)

- Bundled hash-pinned PresentMon 2.4.1 with its MIT notice instead of depending on an AMD driver path.
- Streamed PresentMon stdout into ViewLab-owned CSV checkpoints and stopped the unique ETW session
  cleanly, preventing lost evidence and orphaned `ViewLab-*` sessions after capture.
- Made target exit and the Trace deadline automatically run the complete serialised finaliser, including
  WPR shutdown, and captured Detailed module/API evidence while the target is still alive.
- Isolated DiagMon integration fixtures from the production LocalAppData session index.

## Unreleased - 2026-07-14 (DiagMon(ster) capture and session library)

- Added a View Lab-owned `DiagMon(ster)` cockpit with generic manual, foreground, newly-started and
  previous-process targeting; Standard, Detailed and explicitly bounded Trace modes; elapsed status;
  per-collector health; clean stop/finalisation; and interrupted-session recovery.
- Added portable timestamped session bundles, sortable/filterable history, validation and session-type
  classification, tags/notes, robust comparable-history baselines, conservative factual summaries,
  raw evidence retention, explicit deletion, and context-bounded ZIP exports for external AI analysis.
- Ported the useful legacy PresentMon, process, Windows counter, event, module and configuration capture
  behaviour without a runtime dependency on the DiagMon prototype. `AllowsTearing` is explicitly not
  interpreted as dropped-frame evidence; unavailable collectors remain visible as missing or failed.

## Unreleased - 2026-07-14 (Interpretable completed-session graph)

- Replaced the full-session polyline with labelled time/latency axes, a complete selectable legend,
  robust automatic scaling, display-budget guides and min/max-per-pixel downsampling that retains spikes.
- Added mouse-wheel zoom, drag pan, reset, exact timestamp/value hover inspection, session/marker/alarm/
  estimated-cadence event lines and summary statistics (average, median, P95, P99 and maximum).
- Extended the durable trace format additively to schema 2 with a UTC/QPC anchor, GPU utilisation and
  the existing HUD alarm masks. Schema 1 sessions remain readable; inferred cadence misses are labelled
  estimates until the PresentMon migration supplies measured dropped-presentation evidence.

## Unreleased - 2026-07-14 (HUD alarm and last-session trace reliability)

- Replaced every HUD symbol's update-count alarm latch with one tested 750 ms time-based entry and
  recovery policy whose post-recovery hold cannot extend itself indefinitely.
- Made GPU headroom use 90% warning and 98% critical defaults, so a brief 90% excursion during flat
  target-rate delivery does not appear as a critical alarm-only incident.
- Made trace persistence independent of orderly game shutdown: the bounded telemetry worker checkpoints
  new samples and markers every second, flushes them to disk, and tolerates a partial final record after
  abrupt process exit. End/destroy callbacks remain final-flush optimisations.

## Unreleased - 2026-07-14 (Music track-change card completion)

- Completed the opt-in Windows Now Playing (SMTC) provider for brief title/artist/artwork cards with
  no permanent controls and deduplication across pause, seek and volume changes.
- Corrected the provider lifecycle to unsubscribe the exact session-change handler on stop, allowing
  clean broker reconfiguration without retained duplicate callbacks.

## Unreleased - 2026-07-14 (OBS recording indicator)

- Added an optional broker-side OBS WebSocket v5 connection that authenticates locally and queries
  `GetRecordStatus`; merely finding an OBS process does not light the cue.
- Added subtle adjustable red visor corners while recording is active. Capture exclusion is not
  claimed: the cue is drawn into submitted eye textures and a real OBS capture-path test is pending.

## Unreleased - 2026-07-14 (Sticky note visor widget)

- Added one short native visor note with bounded four-line word wrapping, shared-binocular position,
  angular size and opacity controls. It remains independent of notification cards.
- Added an F6–F12 configurable rising-edge show/hide bind (F7 default). The note starts visible when
  enabled and settings are applied at the next OpenXR session.

## Unreleased - 2026-07-14 (Real performance trace markers)

- Added an F6–F12 configurable marker bind (F8 by default). Each rising edge receives an exact QPC
  timestamp and sequence number in the native performance sample stream, plus a brief numbered visor
  confirmation and a numbered line on the live trace.
- Added a bounded native session recorder that atomically saves the real trace at OpenXR session end,
  and a post-session graph that draws and navigates those same numbered marker events. Generic history
  and notification storage are not involved.

## Unreleased - 2026-07-14 (Network HUD expansion)

- Added optional PING, LOSS, JIT and NET widgets to the existing modular HUD. A bounded worker sends
  one Windows ICMP echo per second to a configurable IPv4 target and publishes rolling 20-probe loss,
  successful-probe jitter, latency and stable/unstable/unreachable state outside the render thread.
- Kept all network widgets off by default and labels explicit: these values describe the configured
  probe path, not private game-server telemetry. Three consecutive misses produce an `OFF` warning.

## Unreleased - 2026-07-14 (Clock and VR session timer)

- Added a compact dedicated visor card with current 24-hour local time and elapsed OpenXR-session
  time, rendered through the same binocular Direct/Topmost overlay path as established overlays.
- Added position, scale and opacity settings; elapsed duration uses monotonic time and resets for
  every successfully created OpenXR session.
- Removed the isolated generic technical-history experiment, including persistence, broker hooks,
  settings, fixtures and canonical documentation. Live notifications and racing cues are unchanged.
- Retained the atomic Topmost failure latch and reusable invariant-culture right-click slider reset
  helper while removing the experimental surface.

## 4.1.211

- Prevent visibility-mask filtering from returning an empty eye mesh. Indivisible one-triangle
  runtime masks now pass through unchanged, and unfamiliar topology fails open to the runtime data.

> Live state: `STATE.md`. Architecture: `docs/ARCHITECTURE.md`. This file is append-only release history.

## 4.1.210 - 2026-07-13 (4.1.209 Pistol Whip presentation repair)

- Restored the direct eye-texture backend for projection-only applications. Automatic Topmost now
  arms only after a distinct application compositor layer is observed, retains its one-attempt and
  fail-closed safety contract, and suppresses duplicates during the backend transition.
- Kept literal-pixel calibration on the submitted game texture and corrected the 64 px grid to use
  the proven explicit constant-colour shader.
- Made every graph mode select or fall back to a compatible useful channel, corrected Alarm-only
  trace transitions, and made the twelve HUD widgets one proportional single row.
- Moved notification fade/slide evaluation to native render cadence and removed doubled fade alpha.
- Made racing presentation tests explicit temporary overrides, so they work even when the production
  feature is disabled without enabling or persisting it.

## Unreleased - 2026-07-13 (Automatic topmost presentation)

- Made the repaired topmost-capable backend the normal automatic policy and removed its experimental
  checkbox from ordinary settings. `overlay_force_direct=1` remains a session-start diagnostic escape.
- Confirmed common-scene parity for visor, calibration, HUD, Trace, crosshair, notifications, lap
  cards, spotter glow and flag border; all use the same renderer/coordinates in both backends.
- Suppressed direct visor drawing once Topmost is armed, including captured-projection fallback, so
  no frame receives duplicate direct and composition-layer masks.
- Preserved first-frame direct presentation, one allocation attempt/session, stable capacity,
  device-loss latch, bounded failure and immediate next-frame direct fallback.

## Unreleased - 2026-07-13 (Performance Trace visibility)

- Replaced the ambiguous graph checkbox with explicit Off, Always visible and Alarm only modes,
  retaining the legacy enable key solely for migration.
- Alarm-only keeps the fixed 600-sample history running while hidden, appears only after existing
  sustained alarm confirmation, extends one hold during repeated trouble, then fades over 500 ms.
- Emphasised graph channels responsible for active VR/frame or APP alarms without changing channel
  values, units, stereo placement or normal always-visible behaviour.

## Unreleased - 2026-07-13 (Racing attention policy)

- Added a deliberately small four-channel policy: spatial spotter and safety flag cues are immediate
  and coexist; performance alarms remain independent; transient cards stay in their compact area.
- Desktop cards wait off-screen during active spotter or safety-flag states, then present normally if
  the state clears within five seconds or are discarded as stale. Lap cards are not self-delayed.
- Kept queue size, wait time and expiry bounded; no racing feature enables global notifications and
  no notification body is added to diagnostics.

## Unreleased - 2026-07-13 (iRacing provider correctness)

- Replaced the prototype poller with one cancellation-bound, tick-aware worker using validated SDK
  header, variable-table, type/count, buffer-length and offset reads.
- Corrected all official `CarLeftRight` states, including distinct two-cars-left and two-cars-right;
  inactive, stale, disconnect, stop and session reset now clear cues authoritatively.
- Added bounded reconnects, 750 ms stale detection, duplicate-tick/event suppression, session
  identity/reset handling and safety-prioritised official flag mapping.
- Expanded lap events with validity, personal-best, previous-best delta and session identity without
  fabricating unavailable session-best data.
- Added a real named-shared-memory fixture covering enums, stale and repeated ticks, reconnect,
  quick restart, invalid values/offsets, flags, laps, duplicates and session changes.
- Moved the single provider worker into the independent installed broker and added a generation-safe
  generic racing-state mapping, so telemetry and cues survive settings-window exit.
- Implemented configurable side-correct peripheral spotter glow, safety-prioritised flag border and
  lap-result cards through the existing textured-card renderer without enabling desktop notifications.
- Routed every UI simulation through the same broker, generic event, mapping and native consumer as
  live telemetry; removed the settings-owned duplicate worker and stale scaffold wording.

## Unreleased - 2026-07-13 (Packaged Windows notification collection)

- Added a signed package-with-external-location identity and dedicated medium-integrity notification
  broker with the required `userNotificationListener` capability and global consent flow.
- Moved collection, filtering, privacy shaping, card composition, expiry and shared-memory publishing
  out of the settings window; collection now survives settings exit and never enters the game thread.
- Added source/notification-ID deduplication, authoritative removal, stale-history baselining, bounded
  safety polling and a five-failure latch that requires an explicit access retry.
- Changed the ordinary WPF interface to `asInvoker`; machine-wide OpenXR registration now uses a
  narrow elevated self-command only when the x64 or Win32 registry state needs changing.
- Extended MSI install, upgrade and uninstall handling for the broker, signed identity package,
  trusted signer certificate and logon startup.
- Added a disposable separately packaged Windows-notification fixture. Installed build 4.1.202
  received its real toast through Windows and published it into the production ViewLab card queue.

## Unreleased - 2026-07-13 (Hardware telemetry platform)

- Expanded the HUD from four fixed choices to twelve functional widgets: CPU, peak logical CPU,
  reported CPU clock, GPU 3D, RAM, commit, VRAM budget, SYS headroom, APP, VR, FPS and frame interval.
- Moved Windows/PDH/DXGI hardware sampling to one bounded worker. The render thread consumes only a
  non-blocking immutable snapshot; provider failure cannot disable rendering.
- Added bottleneck-aware SYS, inverse configurable thresholds, provider/unit metadata, persisted
  ordering, default restore, gap-free packing and configurable multi-row wrapping.
- Added a versioned telemetry configuration mapping without changing the 208-byte overlay contract.

## Unreleased - 2026-07-12 (Overlays: boundary flash, crosshair, notifications, iRacing scaffold)

- Redesigned Performance HUD as ordered modular CPU/GPU/APP/VR widgets with independent enables,
  gap-free packing, 0.15–3.0 whole-widget scale, sustained alarm hysteresis, and live persistence.
- Replaced unstable SYS timing with APP workload: application-side wall time from `xrBeginFrame`
  return to `xrEndFrame` entry, expressed against the cadence-aware budget.
- Reworked VR classification against the runtime's current `predictedDisplayPeriod`, including
  on-target, warning, critical, stable-reprojection, unstable, and unavailable states.
- Replaced the trace with a bounded channel graph for frame interval, FPS, budget deviation, APP
  work, wait duration, submit duration, and predicted display period in unit-safe modes.

- Safety-critical: stopped Topmost from recreating large compositor swapchains for projection-
  rectangle changes and retrying allocation every frame after failure. It now gets one stable
  allocation attempt per session and permanently falls back to direct rendering on any failure.
- Added a session-wide D3D device-removal gate, deferred failed-resource teardown to normal session
  shutdown, used the runtime texture's legal RTV format, and corrected premultiplied-alpha submission.

- Fixed Split Top/Bottom mode changes not being persisted by the checkbox event. Turning split off
  now immediately writes `split_mode=0` and the centred normal-vertical tangents, preventing an old
  asymmetric profile from returning on the next game launch.
- Fixed pale Topmost Visor Overlay colours by using the runtime swapchain texture's legal typed RTV
  format and submitting ViewLab's already alpha-blended target as premultiplied; the standard direct
  path is unchanged.
- Reclassified calibration geometry by purpose: pixel rulers/patterns use the complete submitted
  eye rectangle and never scale to crop-overlap bounds; the 64 px grid keeps exact spacing but is
  centred on the fused crosshair; radial spokes and true circular rings are constructed in shared
  tangent space. Edge probes once again touch the submitted texture bounds.
- Added right-click zero reset to each crosshair offset slider and documented all ten calibration
  patterns in `docs/CALIBRATION.md`.
- System-wide overlay-coordinate repair: fused overlays now use shared tangent/angular positions
  projected independently through each eye. Crosshair returns to shared tangent `(0,0)` and uses
  per-axis pixels-per-tangent for crop-invariant angular size; diagnostics include shared position,
  per-eye pixels/size, tangents, rectangle, and zero angular disparity. HUD base size/spacing and
  notification card dimensions are likewise angular rather than cropped-pixel-relative.
- Boundary flash is inset beyond half its stroke and now renders as a complete inner outline inside
  each submitted eye rectangle. The combined visor preview shows one crosshair at its visual centre.
  Pixel calibration patterns remain deliberately per-eye texture diagnostics.

- Global visor tuning is now always live through the existing generation-stamped shared-memory
  snapshot; the redundant on/off checkbox and config key are removed. Unchanged frames do no INI
  parsing, and per-app profile overrides still take precedence.
- Both crosshair previews are reduced to half their former visual scale. The binocular visor editor
  now shows one left-eye reference crosshair instead of duplicating it in both preview eyes.

- Follow-up calibration: crosshair centre now exactly matches the radial zone-plate/spoke centre
  (submitted eye-rectangle midpoint) instead of tangent zero, which is displaced on asymmetric FOVs.
  VR frame warnings now use a rolling cadence median and cannot turn amber from isolated miss counts;
  stable 120 Hz 8.2/8.3 ms and 90 Hz 11.1/11.2 ms remain green.

- Crosshair visibility repair: flat overlay colour/alpha now comes from an explicit constant-buffer
  pixel shader instead of the VDXR-broken interpolated vertex colour path. Added resolved per-eye
  geometry/draw logs, an outlined built-in default, a live Overlays preview, and matching binocular
  visor-preview crosshairs.
- Notification status now distinguishes exact access denial, unsupported deployment, listener
  initialization, and internal bridge failure with the WinRT access enum/HRESULT retained. Added a
  synthetic Test Notification that exercises composition, shared memory, texture upload, stereo
  placement, animation, stacking and expiry without Windows notification permission.
- Replaced the iRacing placeholder with an optional background SDK shared-memory provider for
  `CarLeftRight`, `LapCompleted`, `LapLastLapTime`, and `SessionFlags`; it publishes generic events,
  reports connection/update/raw/normalized diagnostics, and includes left/right/both/lap/yellow/
  blue/clear simulations. No RaceLab/Garage61 dependency and no native iRacing coupling.
- VR frame-time colour now uses full-precision rolling samples, sustained 105%/115% entry bands,
  miss counts, consecutive confirmation and lower exit thresholds. The one-decimal label is display
  only; stable 120 Hz 8.3–8.4 ms timing remains green.

- **Packaging correctness repair.** The 4.1.169 MSI was correctly named/versioned but harvested
  the stale 4.1.168 app from the former `net8.0-windows` publish directory after the project moved
  to `net8.0-windows10.0.19041.0`. WiX now points at the current output, and `build.ps1` derives and
  checks that path, removes generated outputs before building, extracts the finished MSI, and
  rejects version, hash, native-payload, or compiled-Overlays marker mismatches.

- **Render-boundary flash.** Dragging any HUD or frame-trace position/size/width/height/scale
  control now paints the exact cropped render boundary in both eyes in a fixed cyan-white outline
  at constant screen-space thickness. It holds for the whole drag and fades out over ~500 ms after
  release (fade timer lives in the native layer so it survives the UI closing mid-drag). Only those
  layout controls trigger it — telemetry, colour, opacity, and threshold controls do not.
- **Static Counter-Strike crosshair.** Optional crosshair rendered at ViewLab's calibrated stereo
  centre (tangent 0,0 in both eyes → zero disparity). Manual controls for size, gap (incl.
  negative), thickness, centre dot, outline, outline thickness, alpha, colour, T-style, and an
  overall ViewLab VR scale. Imports legacy `cl_crosshair*` console configs and CS2/CS:GO
  `CSGO-…` share codes (base-58, community layout) into the same settings, and exports the current
  settings as a legacy `cl_*` config to the clipboard. Dynamic spread/recoil/weapon/movement
  behaviour is parsed-and-ignored; the crosshair is always static. Spans are pixel-snapped for
  sharp edges at any eye-buffer resolution.
- **Windows desktop notification bridge.** Supported Windows notifications are mirrored into the
  visor as compact cards that slide in at the bottom-right of the cropped region, hold ~3 s, then
  fade and slide away; multiple cards stack upward and expire independently, in both eyes, inside a
  safe margin. Collection (WinRT `UserNotificationListener`), image decode, card compositing, and
  the whole queue/animation run in the settings-app process, entirely off the game's render thread;
  each card is pre-composited to an RGBA bitmap and drawn by the native layer as a single textured
  quad (new textured D3D11 pipeline). Live settings: enable, position X/Y, scale, opacity,
  duration, max visible, app allow/blocklist, message privacy (full / title-only / app-only), show
  app icon, show image/thumbnail. Alarm-only HUD state never suppresses notifications.
- **iRacing integration scaffold (not active).** A clearly-labelled future section with enable,
  lap-time popup, peripheral spotter glow, and flag-state border toggles, plus a generic
  `IViewLabEventProvider` / `ViewLabEvent` seam so a later telemetry provider can publish
  renderer-agnostic events without coupling the visor renderer to iRacing. No telemetry is read.
- Live-state shared-memory contract bumped to version 4 (208 bytes); a new
  `Local\XRViewLabNotifications` mapping carries pre-composited notification cards. UI target
  framework raised to `net8.0-windows10.0.19041.0` for the WinRT notification APIs.

## Unreleased - 2026-07-12 (Stereo HUD refinement, 4.1.168)

- The performance HUD now draws in BOTH eyes from a single per-frame snapshot. Every element is
  anchored at a shared tangent-space point inside the binocular overlap of the cropped per-eye
  FOVs, so it fuses cleanly with zero angular disparity, no double image, and no opposite-lens-edge
  hugging, while staying positioned relative to the final cropped tangent bounds.
- HUD scale fixed: the fixed 112 px ceiling that froze growth mid-slider is removed; the 0.5-3.0
  UI range now maps smoothly onto the rendered size relative to the eye resolution (native config
  clamp also raised from 2.0 to 3.0 to match the UI, and the anchor clamp widened to the full 0-1
  slider range).
- Replaced the seven-segment digits with a compact 5x7 pixel HUD font with a dedicated
  decimal-point glyph, integer pixel scaling, and run-merged rows: values are legible at small
  sizes, carry no unit suffixes, and 13.3 can no longer render as 133.
- The fourth (headset) indicator now shows the real VR frame time in ms with one decimal
  (e.g. 8.3 / 11.1 / 13.9). It is measured as the QPC interval between successive xrWaitFrame
  returns; its budget is predictedDisplayPeriod x cadence multiple, where the multiple (1-4) is
  detected from a rolling interval median confirmed over 20 consecutive frames - so ASW/SSW/
  reprojection half-rate modes (72->36, 80->40, 90->45, 120->60) get the correct doubled budget,
  nothing is hardcoded, and a single slow frame can never flip the cadence. Ring fill and colour
  compare frame time against that effective budget (amber >=85%, red over budget).
- Frame-pacing trace gained live X/Y/height/width/history-length/sensitivity controls (sensitivity
  is now a continuous +/-0.5-8 ms slider). Newest samples stay on the right; the baseline is the
  effective cadence budget, correct under half-rate reprojection. History extends to 600 samples.
- New alarm-only mode (live checkbox): each of the four indicators hides independently while
  green/amber and appears only in its red state, using the smoothed values with hysteresis plus a
  configurable post-recovery hold (hud_alarm_hold_ms, default 1500 ms). The pacing trace is
  never hidden and telemetry keeps updating.
- Live-state shared-memory contract bumped to version 3 (120 bytes) carrying the trace and alarm
  controls.

## Unreleased - 2026-07-12 (Native Performance HUD)

- Added live HUD X/Y/scale/safe-margin/clamp controls. HUD bounds now derive from and clamp to
  the current submitted cropped eye rectangle, including at aggressive vertical tangents.
- Fixed the shared live-state contract so calibration, HUD, and live visor-enable changes reach
  the locked render path on the next frame. The mask editor now uses a zoom-compensated preview
  pen, preserving readable curve thickness at every zoom.

- Added an optional, default-off four-icon CPU/GPU/SYS/VR HUD drawn directly into the submitted
  left-eye texture after the visor. Its normalized `XrRect2Di` anchor keeps the complete indicators
  and pacing trace inside the cropped rendered region; no OpenXR overlay layer is created.
- CPU now reports total system utilisation from `GetSystemTimes`. GPU groups Windows GPU Engine
  samples by adapter and engine, selects the D3D11 render adapter, and reports its busiest 3D engine
  without summing unrelated engines into impossible values.
- SYS reports actual frame work divided by the OpenXR runtime's `predictedDisplayPeriod`; VR reports
  the percentage of over-budget frames in a 60-frame window. All percentages are smoothed lightly
  and clamped to 0–100.
- Added a 120-sample frame-pacing line beneath the indicators. It scrolls left, plots deviation from
  the runtime target interval, marks missed-budget segments red, and has live ±1/±2/±4 ms sensitivity.
- Preview control pins now retain constant screen-pixel size, outline, hover, and hit tolerance at
  every zoom level.

## Unreleased - 2026-07-12 (Draw in the Void Shelved + Render-Path Cleanup)

- Shelved the Draw in the Void concept after VDXR confirmed that it PC-composes OpenXR layers into
  one reduced-FOV encoded stream. The unsupported test quad, its swapchain/reference-space code,
  UI checkbox, live-state flag, and default INI key are removed.
- With the settings app closed, the render path now attempts to reconnect the optional live-state
  shared-memory mapping at most once per second instead of calling `OpenFileMappingW` every
  submitted frame. Existing live visor and calibration updates remain generation-checked at the
  `xrEndFrame` safe point.

## Unreleased - 2026-07-12 (Ten-pattern Calibration Suite)

- Promoted the hidden calibration grid into a Calibration dropdown with ten independently
  default-off, submitted-texture diagnostics: grid, pixel ruler, repeated 1/2/4 px gratings,
  colour/grey bars, frame beacon, edge probes, checkerboards, radial zone plate, clipping steps,
  and a frame-serial motion strip.
- Temporal diagnostics are driven once per submitted projection frame, so both eyes receive the
  same beacon and motion state; they never depend on a two-release-per-frame assumption.

## Unreleased - 2026-07-11 (Calibration Grid, hidden)

- Added `calibration_grid` (ini-only, default 0, no UI): draws a uniform 64 px reference grid
  into the eye images at `xrReleaseSwapchainImage`. Because the grid is exactly uniform in the
  submitted texture, any spacing or contrast distortion seen in the headset is introduced
  downstream of ViewLab (used to characterise VD fixed foveated encoding).

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
# Unreleased

- Fixed asymmetric top/bottom cropping tilting or folding the game world by retiring the pose-rotation compensation path; crop now changes FOV tangents without modifying eye orientation.
- Repaired the systemic stereo regression introduced by normalized per-eye overlay placement: every migrated overlay now starts from shared binocular tangent-space bounds and projects independently into each eye; HUD is binocular again and calibration plates use the same shared render-area bounds.
- Added disabled-by-default `Topmost Visor Overlays`, submitting the existing visor scene in a transparent final OpenXR projection layer with automatic normal-renderer fallback.
- Unified eye-texture overlays behind Render Area, Full Lens, and Lens Pinned coordinate modes.
- Made Performance HUD and Performance Trace independent, left-eye-only controls and removed repeated OpenComposite projection-layer draws.
- Added normalized crosshair X/Y calibration and reset controls; zero remains the resolved full-lens centre.
