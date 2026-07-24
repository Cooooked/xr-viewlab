# Decision log

## D32 — Overlay categories, inheritance and baseline (2026-07-18)

Clock, HUD, Performance Trace, Sticky Notes, Crosshair and Notifications are configurable overlays. OBS Recording
Cue and iRacing Telemetry are feature modules with global detail settings and per-app enable gates. Boundary Flash
belongs to HUD/Trace layout feedback. A profile inherits until edited; `Use Global Values` removes its complete
overlay/module configuration and layout. The 4.1.255 baseline is an embedded JSON allowlist and never migrates app
profiles or machine-specific ReShade deployment state.

## D31 — Per-app visor state and shape form one override (2026-07-18; supersedes D4)

`Use global visor settings` means the entire visor configuration follows the main editor. Its registry sentinel is
`visor_size=0`, and custom-only visor keys are removed. When unchecked, the profile owns enable, Size, Width,
Height, Curve, Outer Dip, Nose and Nose Spread X; the native layer reads that enable only when the custom sentinel
is active. The profile and main editors share `BeanMaskEditor` as their geometry implementation and identical
ranges/defaults. Rejected: a global-only enable switch and a profile editor with a second, obsolete control set.

## D30 — ReShade files, registration and connection evidence are independent

Install/Uninstall touch only ViewLab's two payload files. Enable/Disable touch only ViewLab's 64-bit implicit-layer
manifest value and never enumerate or reorder other layers. Connected requires a heartbeat transition observed
after attachment; mapping existence and first-read contents are not a live handshake.

The desktop preview starts as a normal focusable window; headless/borderless is an explicit user choice. Remote
polling may observe heartbeat freshness, but it updates displayed control state only after a revision change.
`ReShadePayloadSource/` is the sole canonical modified payload source. It was recovered by an exact pre-change
DLL hash match and retains its original repository path, remote, base commit and dirty-file inventory in
`README.ViewLab.md`. Payload-internal behaviour is changed and verified there, then the focused x64 Release output
must hash-match `ReShadePayload/ReShade64.dll` before packaging. ViewLab UI code must not imitate or conceal
payload lifecycle faults.

## D29 — Ordinary overlays share one configuration contract; history is append-only by default

Clock, Performance HUD, Performance Trace, sticky note, crosshair and notifications use one catalogue
for enable state, optional show/hide hotkey, position, scale, opacity and reset defaults. Features may
retain genuinely specific controls, but may not privately reimplement common persistence. Existing INI
keys remain authoritative so upgrades preserve layouts; the former sticky-note-only bind is a read/mirror
migration input. Native visibility toggles are centralised and do not create a second presentation route.

Native VR performance traces receive unique timestamped files. `latest.csv` is only a compatibility alias;
it is not the history model. Session Graph lives in DiagMonster, supports browsing and selected-session
comparison, and deletes only after explicit confirmation. Retention limits are user-configurable guidance,
never permission for silent evidence deletion. Rejected: one-off overlay settings handlers, forced hotkeys,
overwriting the only historical trace, and automatic deletion.

## D28 — DiagMon sessions are portable evidence, with user-owned validity

The View Lab settings process owns capture orchestration and writes portable timestamped directories
under its local data area. Each collector reports running/complete/partial/missing/failed independently;
a clean orchestrator exit is not proof that PresentMon or another collector produced evidence. Standard
mode never enables WPR. Trace mode is explicit and time-bounded. Interrupted `.partial` directories are
recoverable and become incomplete sessions rather than vanishing.

ViewLab ships a hash-pinned PresentMon 2.4.1 console plus its MIT notice. It owns CSV persistence by
streaming `--output_stdout` to the partial session with one-second durable flushes and owns shutdown by
terminating only the session's unique ETW name. Target exit and the Trace deadline automatically run the
same finaliser as a manual stop; merely breaking the sampling loop is forbidden.

Calculated history accepts only valid normal sessions or user-selected baselines with the same target
and compatible fingerprint. Invalid, experimental, stress-test and incomplete sessions remain inspectable
but are excluded. Baselines use medians and IQR-derived tolerance, record their inputs and selection/
exclusion reasons, and remain low-confidence below three comparable runs. Frame heuristics are long frames
above `max(33.33 ms, 2x median)` and severe stutters above `max(100 ms, 3x median)`. `AllowsTearing` is
never dropped-frame evidence. Rejected: a database server, fixed last-N comparisons, silent evidence
deletion, WPR-by-default, title-specific core logic, and deterministic root-cause claims.

## D27 — Completed-session graphs distinguish evidence from inference

The graph presents native OpenXR interval, cadence, display-period, app-work, runtime-wait and submit
series with their units and exact timestamps. Full-session rendering uses min/max-per-pixel buckets so
spikes survive downsampling; visible-window scaling is robust to isolated outliers and calls out clipped
spikes. Session, user-marker and persisted alarm transitions are evidence. An interval over 1.5 times
the active cadence budget is an **estimated cadence miss**, never a claimed dropped presentation.
Measured dropped presentations remain unavailable until ViewLab owns the PresentMon collector.

Schema 2 adds a UTC/QPC anchor, GPU value and existing alarm masks to the checkpointed row without a
parallel collector. Schema 1 remains readable. Rejected: smoothing shelves away, plotting every point
into one giant polyline, inventing wall-clock anchors for legacy files, and reporting unavailable
PresentMon evidence as zero.

## D26 — ViewLab is the product; DiagMon is a prototype farm

DiagMon supplies experiments and evidence, not a product dependency. Valuable capability is migrated
into ViewLab one bounded feature at a time, validated there, and then owned solely by ViewLab. There is
no DiagMon runtime, shared authority, parallel collector or parallel UI in the shipping architecture.
Rejected: merging repositories wholesale, teaching ViewLab to read DiagMon's working folders, and
leaving two implementations active after migration.

The first migration is PresentMon-backed presentation capture: it contributes independent present
mode, displayed-frame pacing, duration/latency and stutter evidence that OpenXR hook timings cannot
prove. ViewLab owns orchestration, lifecycle, compact session storage, analysis and visor/desktop
presentation. PresentMon's MIT-licensed supported collection/API components may be packaged with their
required notice; an AMD-driver installation path is never a product prerequisite. No capture or IPC
operation may block an OpenXR/D3D hook.

Ordinary capture is compact, rolling and quota-bound. ETL/kernel/provider traces remain off unless the
user explicitly starts a time-bounded deep diagnostic; deep output rotates in compressed chunks and
stops at its storage cap. A checkbox must never quietly create an unbounded trace.

## D25 — Session traces checkpoint before shutdown

Post-session trace correctness cannot depend on `xrEndSession`, `xrDestroySession`, DLL detach, or a
polite game process. The existing bounded telemetry worker checkpoints new records once per second and
durably flushes them outside OpenXR hooks; lifecycle callbacks merely request a final flush. Rejected:
file I/O on the frame thread, process-detach work under the loader lock, and further shutdown hooks.

## D24 — Alarm-only means sustained critical evidence, not ordinary utilisation

All HUD symbols share one elapsed-time alarm state machine: 750 ms sustained entry, 750 ms recovery,
then the configured hold measured once from the first non-critical input. GPU uses separate 90% warning
and 98% critical defaults because ordinary high utilisation at a flat target cadence is useful work, not
itself a performance failure. Rejected: update-count timing, a generic 90% GPU critical threshold, and
refreshing recovery from the latched display colour.

## D23 — Generic network HUD measures a declared probe path, not the game server

ViewLab sends at most one ICMP echo per second from the bounded telemetry worker to a configurable
numeric IPv4 target. PING is successful round-trip time, LOSS is the rolling 20-probe failure rate,
JIT is mean absolute change between successful RTTs, and OFF requires three consecutive misses.
Rejected: labelling arbitrary process endpoints or OS counters as game packet loss; a generic OpenXR
layer does not possess the game's authoritative networking telemetry.

## D22 — VR session duration is monotonic OpenXR-session lifetime

The visor session timer starts only after a successful `xrCreateSession` and resets during the
matching `xrDestroySession`. Elapsed duration uses `GetTickCount64`, while `GetLocalTime` supplies
only the separate wall-clock row. Rejected: settings-app uptime, process start time, and wall-clock
subtraction; none truthfully measures the active OpenXR session and wall time can jump.

## D21 — Hardware telemetry is snapshot-only at the render boundary

All Windows, PDH, DXGI and future vendor sensor work belongs to a bounded telemetry provider worker.
OpenXR/D3D hooks may only attempt a non-blocking copy of its completed fixed snapshot. Catalogue live
settings use `XRViewLabTelemetryConfigV1`; the 208-byte v7 general overlay mapping is not enlarged.
Advanced vendor sensors remain absent until licensing and failure isolation are reviewed; missing
data is never represented as zero.

## D20 — Performance timings are named by observable hook boundaries

APP is wall time from `xrBeginFrame` return to `xrEndFrame` entry, not exact CPU execution. Wait and
submit graph channels are durations inside their respective runtime calls. Adapter 3D utilisation is
not GPU frame time, which remains deferred until ViewLab has a correctly scoped non-blocking D3D
timestamp-query pipeline. Cadence derives from the active `predictedDisplayPeriod`, never a list of
familiar millisecond values.

> Why things are the way they are. Append-only; add an entry when a decision becomes permanent.
> Format: decision, reasoning, alternatives rejected, consequences.

## D1 — Crop lives in `xrLocateViews` + recommended-size reduction (2026-07; updated 2026-07-11)
Real GPU savings require the game to render fewer pixels, so ViewLab crops the FOV tangents the
game sees AND reduces the recommended image size. Rejected: reporting full FOV while rendering
reduced size (the `lod_popin_fix` experiment) — projection/culling and submitted geometry
disagree, producing stretched output. Consequence: aggressive crops can cause LOD pop-in.
`lod_popin_fix` and `edge_smear_fix` were removed from UI and code in 4.1.122; they were never
productized and only produced noise. Both config keys are ignored.

## D2 — Technique C Direct is the product visor path (2026-07, v4.1.61+)
Draw the mask directly into the game's eye texture at `xrReleaseSwapchainImage` (last legal write
point while the app owns the image). Rejected as product paths: A (head-locked quad layer —
works, VDXR-compatible, but composites over everything incl. runtime UI) and B (swapchain
interception — fragile across formats/MSAA). A/B code stays in-tree, hidden, for reference.

## D3 — `mask_size` is the single source of truth for the visor opening (4.1.65)
Never derive the opening from legacy `mask_vertical`/`mask_horizontal` — their missing-key
fallback computes 1.0 = full opening = invisible mask (the "no mask for 3 days" bug, R4).
Missing-key fallback is 0.82 in BOTH UI and DLL.

## D4 — Visor enable was global-only (4.1.6x; superseded by D31)
Per-app registry `mask_enabled` is deliberately ignored; stale per-app 0s silently overrode the
global toggle. Per-app SHAPE values are still honored.

## D5 — Geometry parity: `BeanMaskEditor.cs` is the spec (2026-07-10)
Preview and native visor must be formula-identical (same curve exponent, same apex math, same
bezier), with the UI-screen-y ↔ NDC-y flip in exactly one native helper (`ApexYFromConfigNdc`).
Two past regressions (apex inversion 4.1.66, divot-on-top R2) came from ad-hoc per-site flips.

## D6 — Curve slider maps geometrically: exp = 32·(2/32)^curve (4.1.67, re-adopted 2026-07-10)
Linear mapping wastes the slider's midrange (barely-changing flat shapes); 4.1.66's linear-to-7
cap lost the lemon extreme entirely. Geometric puts the old "looks maxed" point at 0.5 and keeps
exp=2 (full lemon) reachable at 1.0.

## D7 — FreeLibrary(d3dcompiler) only after ALL shader blob use (4.1.89 / 2026-07-10)
`D3DCompile` blobs point into memory that dies with the DLL; freeing before
`CreateVertexShader`/`CreatePixelShader`/`CreateInputLayout` crashes any game without
d3dcompiler_47 already resident (Pistol Whip). Free after the last blob use + on every error path.

## D8 — The stencil filter runs even when the visor is active (4.1.102 / 2026-07-10)
Old code skipped `xrGetVisibilityMaskKHR` filtering whenever the native visor was on ("avoid
double boundaries") — wrong: the GAME stencils whatever the mask returns, so Virtual Desktop's
FOV stencil blacked the inner/top/bottom edges with VD's shape regardless of the visor. Only the
legacy reshaper stays gated on visor-off. Also: the checkbox writes `stencil_outer_edges_only`;
the DLL must read that key (it read a different name for months — R3).

## D9 — Partner-eye boundary only in closed-bean mode (4.1.103)
`BuildProjectedPartnerVisorVerts` blacks the inner band the cropped partner eye can't render
(anti binocular-rivalry at aggressive horizontal crop). In open-inner ("stencil outer edges
only") mode that's exactly the inner-edge stenciling the user is turning OFF — so it draws only
when the checkbox is unchecked. Both visor geometry and this boundary key off the same flag.

## D10 — Visor edge AA and HD were removed (2026-07-11)
The previous feathered-edge anti-aliasing implementation broke the visor mask in practice and
the HD checkbox (2× tessellation) had no visible benefit. Both checkboxes and their code paths
were removed; `visor_hd` and `visor_antialiasing` config keys are ignored. The visor is drawn
with opaque geometry at the default tessellation level.

## D11 — Repository is the canonical memory (2026-07-10)
`agents.md` = operating manual, `STATE.md` = live state, `docs/` = deep knowledge, updated in
the same commit as the change. Tool-specific files (`CLAUDE.md`) are thin pointers, never
independent memory. No handoff/session/scratchpad documents. Root-level doc sprawl from
earlier eras was consolidated 2026-07-10 (27 root .md → 4 + docs/).

## Legacy (ReShade Remote subsystem, pre-2026-07)
Gameplay mode is launch-time config, not a live toggle; shared-memory `XRControlBlock` layout is
frozen (both sides must match); commands are edge-triggered (rising edge) to prevent repeat
execution; desktop and HMD render paths stay separated. Details: `ReShadePayload/Docs/`.

## D12 - MSI upgrades preserve user settings (revised 2026-07-11)
The packaged ini supplies safe defaults for a fresh install. Ordinary upgrades do not strip the
live `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini` or per-app HKCU overrides. A changing MSI-version
reset marker was rejected because it made every update a factory reset, and running that cleanup
from the native OpenXR layer also put file/registry mutation inside a game's startup path.

## D13 — Stencil/crop outer edges stay on; foveated pose compensation is retired (revised 2026-07-13)
`stencil_outer_edges_only`/`outer_edge_visibility_mask_only` and `crop_outer_edges_only` remain
hardcoded on and their config keys are ignored. `foveated_center_compensation` is retired and fixed off:
its asymmetric-crop implementation rotated `XrView.pose.orientation`, tilting/folding the game world.
Overlay or crop code may read game poses but must never mutate them. Consequence: `crop_outer_edges_only`
always applies the horizontal crop from the outer edges, preserving the inner edge for the nose/bridge.
`stencil_outer_edges_only` keeps the open-inner (single-eye) shape in the visibility mask and preview.

## D14 — Treat prescriptive prompts as intent and constraints (2026-07-13)
Repository evidence and known-good behaviour outrank generated architectural prescriptions. When a prompt
dictates maths or structure, first verify that design against the current implementation and headset-proven
contracts. Preserve what works, infer the narrowest change from symptoms, and reject instructions that would
violate established invariants. Prompts provide intent and constraints; they are not unquestionable architecture.

## D15 — Windows notifications use a packaged medium-integrity broker (2026-07-13)

Keep the established MSI/OpenXR layout and grant only `ViewLab.NotificationBroker.exe` a signed
package-with-external-location identity plus `userNotificationListener` capability. The broker owns
global consent, collection, composition and the existing shared-memory card queue independently of
the settings window and game. The ordinary WPF settings process runs asInvoker; machine-wide layer
changes elevate through a narrow command only when registration changes. Rejected: an AUMID-only
shortcut (does not grant listener capability), permanent elevation, collection in the game, and a
full MSIX migration solely for this API.

## D16 — Topmost is automatic presentation policy, not an ordinary feature (2026-07-13)

Backend choice is automatic, but a projection-only application remains on the proven direct
eye-texture renderer. A distinct application compositor layer latches demand for the session; the
composition backend is prepared without submission on that transition frame, then direct common-
scene drawing is suppressed and Topmost is appended on following frames. Literal-pixel calibration
continues to measure the submitted game texture. Any Topmost failure permanently selects direct for
that session. Keep `overlay_force_direct` as an advanced diagnostic escape. The one-attempt,
no-duplicate-transition and device-loss latches are release invariants.

## D17 — Performance markers are events in the real QPC trace (2026-07-14)

A marker bind is sampled on its rising edge beside successful `xrWaitFrame`, stamped with that QPC
counter, assigned a monotonic session sequence and placed into the same native sample stream that
feeds the visor trace. The post-session file is a versioned, bounded recording of those samples and
marker events, written atomically at session destruction. Generic UI history, notification queues and
human-readable logs are expressly not trace storage: they cannot preserve frame-relative timing and
would make the graph a decorative reconstruction rather than evidence.

## D29 - All ViewLab presentation is frame-selected behind ViewLab Bridge (2026-07-15)

Projection-only frames keep the established direct eye-texture renderer. When actual submitted topology
shows a distinct compositor layer can cover it, ordered demand is latched while direct owns allocation.
When a current projection supplies valid geometry, the central plan presents all common features through
the live-proven transparent stereo projection carrier: two runtime array slices, current format/capacity,
space, eye poses and FOV, appended after application layers. Readiness transfers every normal feature to
that carrier and disables the obsolete direct copy; ordered failure restores the entire set to direct. A
head-locked quad is not promoted merely
because resource creation and `xrEndFrame` succeed; the translated menu regression proved that success is
not visibility. Literal-pixel calibration remains on the game texture because moving it changes what it
measures. Rejected: executable/runtime allowlists and feature-owned backend decisions.

Allocation or submission failure disables only the ordered supplement and restores writable direct common
rendering; it never classifies the game as unsupported. Legacy translation grows behind `ViewLabBridge`, a
separately compiled native boundary. The external translator remains active until ViewLab supplies compatible
legacy ABI/interface negotiation, compositor timing/session ownership, graphics texture submission, tracking,
input, properties and overlays. Copying title workarounds from the technical baseline is forbidden.

## D18 — Sticky note is one bounded native widget (2026-07-14)

The visor note is one short startup-configured string, rendered by the native common overlay path and
toggled by a rising-edge bind. Text is capped, normalized and wrapped into four lines before geometry
is emitted. It does not enter the notification card queue and it does not grow into persistence for
multiple notes, rich text, editing inside VR or a note-management subsystem.

## D19 — OBS state is queried, and capture exclusion is unclaimed (2026-07-14)

The broker uses OBS WebSocket v5 authentication and `GetRecordStatus`; an OBS process or open window
is not recording evidence. Native consumes a tiny dedicated state mapping and draws only when OBS
reports an active recording output. The cue is inside ViewLab's submitted-eye-texture overlay path,
so it is expected to be capturable by some OBS sources. ViewLab must not advertise capture exclusion
without a real source-by-source recording test.

## D20 — Music is an event card, not a media controller (2026-07-14)

Use Windows SMTC's current-session and media-properties events outside the game. Emit only when the
trimmed title+artist key changes, include artwork when available, and reuse the brief notification-card
pipeline. Do not add polling fallback, playback controls, a permanent now-playing widget or repeated
cards for seek/pause/volume events. Provider lifecycle must unsubscribe the exact registered delegate.

## D29 - Text collections use a bounded live contract (2026-07-15)

Clock numerics and common visibility binds extend the generation-safe general live-state block. Sticky
notes use a separate fixed-capacity mapping because variable text does not belong in that per-frame numeric
contract. The collection is capped at eight notes and 120 UTF-16 characters per note, keeps the established
native renderer, and migrates the former single note into slot zero. This supersedes D18's one-note limit.
UI absence leaves INI startup values operational. Notification themes remain in the off-render-thread card
compositor; HUD symbol selection rides the existing telemetry catalogue per widget.

## D30 - The visor preview edits the shared overlay model (2026-07-15)

The visor canvas is an input surface over `OverlaySettingsCatalog`, not a second layout system. Each
editable placeholder has one stable feature/collection identity and emits generic position or scale
changes. MainWindow applies those changes to the same controls, INI keys and live-state publishers used
by the Overlays menu. Labels and editor handles are compensated for canvas zoom in screen space, while
the placeholder geometry continues to scale with the scene. Edge cues without an ordinary placement
contract remain read-only. Rejected: feature-private canvas handlers and preview-only persisted values.

## D31 - Preview footprint scale is uniform and singular (2026-07-15)

Each preview descriptor stores an unscaled reference footprint. `OverlayPreviewReplicaLayout` applies the
shared Scale exactly once to both axes, then uses one common fit factor if the result exceeds the canvas.
Independent minimum/maximum axis clamps are forbidden because they turn scaling into aspect distortion.
Performance Trace follows the same product contract: Width defines the graph's base shape and Scale affects
both dimensions. The preview separately shows the full Quest 3 H/V 1.00 binocular reference and current crop.
Exact content replicas require a future native layout/content data contract; labelled footprints must not be
described as pixel-identical before that exists.

## D32 - Crop clips overlays; it never redefines overlay coordinates (revised 2026-07-19)

Native rendering and the desktop calibration mirror both keep persisted widget X/Y in full-lens normalized space.
The current crop is coverage and clipping only. Forward placement, footprint scale and inverse dragging use the
same fitted H/V 1.00 rectangle; the preview must not clamp an item merely to keep its visible footprint on-canvas
when the corresponding runtime feature is unclamped. Rejected: crop-relative coordinates, visual offsets, scaling
footprints from cropped dimensions, applying crop twice, or changing runtime coordinates to compensate for preview.

## D33 - Master is stable and dev is the single working branch (2026-07-17)

`master` is the validated integration baseline. Ordinary AI-assisted work happens on `dev`; changes move
from `dev` to `master` only after the repository contracts and full build pass and the requested validation
is recorded. Experiment or feature branches are created only on explicit request. The remote `main` ref is
an obsolete disconnected history and is not an integration target. Force pushes, history rewrites and branch
deletion remain prohibited without explicit user approval.

## D34 - The Quest 3 preview is a centred normalised headset view (2026-07-18)

The preview is not a projection-angle plot. `Quest3PreviewGeometry` fits one fixed `55:48` outer box,
centres it, and applies horizontal and vertical retained fractions directly: `0.8` is 80% of box width
and symmetric `0.15` is 15% of box height. Split top/bottom values move the rectangle according to their
actual shares. The periphery layer can show one binocular oval or two square-bounded circles. Both use the
same boundary: 90% of box height and 85% of box width; circle mode never inherits container stretching.
Independently, the full-frame guide can be one combined rectangle or two overlapping per-eye rectangles
whose `2064:2208` aspect is preserved. A persisted `67.0` mm IPD calibration input scales only the centre
separation of dual guide geometry, in 0.1 mm steps exposed through direct typing, keyboard input and visible buttons.
Vertical preview state is one retained scale plus centre; `1.0` is exactly the full outer-frame height. These are
display preferences only and never publish to runtime.
The current crop and visor shape use the fixed outer-box geometry. Widget X/Y, hit-testing, footprint scale and drag
deltas use that same full fitted box. Crop is display-only coverage, while zoom and pan are display-only transforms.
Rejected: deriving preview placement from asymmetric projection degrees, one-off widget offsets, applying crop
twice, scaling widgets from crop dimensions, or stretching an eye guide to the container aspect.

Split Top and Bottom controls are relative to their respective half of the lens. A control value contributes
`value × 0.5` to full-lens retained height, so `1/0` selects the top half, `0/1` the bottom half, `0.5/0.5`
the centred middle half, and `1/1` the full height. Existing `top_tangent` and `bottom_tangent` persistence stays
in full-lens shares for native compatibility; UI load/save performs the ×2/×0.5 boundary conversion.

## Zero idle cost: background work is demand-gated by its consumer

ViewLab is resident whenever the user is at their PC — the broker starts at login and the layer loads into
every OpenXR game — so no subsystem may cost CPU, memory or disk while the feature that needs it is off.

Diagnostics recording (`performance_trace_recording`) is therefore opt-in, default 0. The hardware-telemetry
collector thread starts only when recording, the Performance HUD or the Performance Trace overlay actually
consumes its samples (`EnsureTelemetryWorker`), and the ~24 MB session ring is reserved only when recording.
Because HUD and trace enable arrive live while recording resolves at session start, the gate is re-evaluated
per frame from a lock-free `viewlab::telemetry::Running()` check; the worker is stopped only at
`xrDestroySession` so toggling an overlay never churns a thread.

The notification broker reads settings from file-change events plus slow probes rather than a 1 Hz poll, and
the notification animation timer exists only while cards are on screen. DiagMon creates its store on first
write, so browsing it leaves no directories behind.

Corrected factory-baseline values do not bump the baseline version: re-applying a bumped baseline would
overwrite every listed key and discard user tuning that diverged after 4.1.255. A behavioral default change
that must reach existing installs instead gets its own one-shot registry marker (`DiagnosticsOptInApplied`),
which rewrites only the affected key. Per-app overrides are never rewritten — those were deliberate choices.

Rejected: a second `diagnostics_enabled` key (dual-key drift, and it would orphan the existing per-app
`overlay_override_trace__performance_trace_recording`), stopping the collector mid-session on overlay toggles,
and removing the broker's login autostart (notifications must work without the settings app open).
