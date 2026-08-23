# iRacing implementation

## Shared-memory model

`IRacingTelemetryProvider` opens the official `Local\IRSDKMemMapFileName` mapping read-only. It
validates the fixed header, variable count/table, type, count, data offset, buffer count, buffer
length and every selected buffer range before reading a value. It chooses the highest valid SDK
tick and processes that tick once. Raw iRacing fields stop at the provider; render consumers receive
only `ViewLabEvent`, `SpotterState` and `RacingFlagState` values.

The enum and flag constants follow the iRacing SDK `irsdk_defines.h` contract reproduced under its
BSD licence by the SDK community:
https://github.com/SIMRacingApps/SIMRacingAppsSIMPluginiRacing/blob/master/irsdk/irsdk_defines.h

## Official left/right mapping

| Raw | SDK meaning | Generic state |
|---:|---|---|
| 0 | off | Clear |
| 1 | clear | Clear |
| 2 | car left | CarLeft |
| 3 | car right | CarRight |
| 4 | car on each side | CarsBothSides |
| 5 | two cars left | TwoCarsLeft |
| 6 | two cars right | TwoCarsRight |

Unknown values fail closed, clear presentation, disconnect the bad read, and retry only on the
bounded 500 ms connection interval.

## Worker lifecycle and latency

There is at most one background worker per provider. `Start` is idempotent; `Stop` cancels through a
wait handle rather than sleeping, joins for at most 1.5 seconds, and does not create an overlapping
worker. Active mappings are checked every 8 ms. Missing/error reconnects are bounded to 500 ms.
Only advancing SDK ticks create samples. A tick unchanged for 750 ms is stale and immediately clears
spotter/flag presentation; inactive status, disconnect, session change and stop also clear it.

## Event semantics

- Spotter events retain the exact six actionable SDK states, with side and car-count scalars for a
  simulator-independent spatial renderer.
- Flag events apply a stable safety priority: disqualified, black, red, yellow/caution, blue,
  debris, white, checkered, green, clear. Unchanged states do not animate again.
- Lap events publish only when `LapCompleted` advances. They include lap number, positive-time
  validity, personal-best state, delta to the previously known personal best, and the
  `SessionUniqueID:SessionNum` identity. Session-best remains null because the four scalar telemetry
  fields do not authoritatively establish it; ViewLab does not invent it.
- A session identity change or lap counter rollback establishes a fresh baseline without a phantom
  lap result.

## Fixture validation

`Tests/IRacingFixtures` creates a genuine named memory map with the official header, variable table,
buffer metadata and typed values. It then runs the production provider against that map. The fixture
covers every left/right value, repeated and stale ticks, inactive/reconnect, rapid stop/start,
invalid enum and buffer offset, supported flag transitions/priority, valid and invalid laps,
duplicate lap values and session reset. Run `Tests\Invoke-IRacingFixtures.ps1`.

## Independent presentation bridge

The installed medium-integrity ViewLab broker owns the single provider worker. It starts/stops from
the global iRacing enable setting, survives settings-window exit, and publishes only generic events
to `Local\XRViewLabRacingState`. Test buttons command that same broker and route; there is no second
test renderer. The native layer validates a generation-last 64-byte block and has no iRacing field,
mapping or enum knowledge.

## Spotter presentation

Left, right and both-side states draw an immediate peripheral glow on the corresponding physical
side(s). Two-cars-left remains left; two-cars-right remains right. Eight fixed bands produce a
strong edge with configurable inward falloff, width, strength, opacity and RGB colour. Clear,
inactive, stale, disconnect and session reset remove the cue promptly.

## Flag presentation

The current highest-priority generic flag draws a restrained four-edge inner visor border. Colours
are green, blue, white, yellow, orange debris, red, near-black, red disqualification and white
checkered. Width and opacity are configurable. An unchanged telemetry flag emits no new event and
therefore replays no animation. Spotter glow and flag border deliberately coexist.

## Lap-result presentation

Valid/invalid time, PB or previous-best delta and lap number are formatted by the broker into a
transient textured lap card. It reuses the established card compositor/renderer but is gated by
`iracing_lap_popup`, not `notify_enabled`; it cannot silently enable Windows notification consent or
collection. Duration is configurable from 1–15 seconds.

## Remaining validation

Provider and bridge fixtures plus native compilation are complete. Installed build 4.1.204 accepted
left/right/both/yellow/lap/clear commands through the packaged broker pipe and published the expected
generic mapping states (`1`, `2`, `3`, flag `Yellow`, lap 12 flags, then clear). With lap cards
temporarily enabled and the original setting restored in `finally`, the same command published one
active textured card to the production queue. Live/replay iRacing and headset appearance/latency
remain mandatory before release.

## Planned feature: Rear-Closing Pressure Cue

This section is an implementation plan, not shipped behaviour. The cue is intentionally centre-only: it must not
infer left/right approach direction. Once iRacing reports side overlap through `CarLeftRight`, the rear cue clears and
the existing physical-side spotter glow owns presentation.

### Telemetry and calculation

The provider should add optional, layout-validated reads for `PlayerCarIdx` (`int`), `CarIdxLapDistPct[]` (`float`),
`CarIdxLap[]` (`int`), `CarIdxTrackSurface[]` (`int`), `CarIdxOnPitRoad[]` (`bool`) and the already-required
`CarLeftRight`. Array counts must be discovered from each SDK variable header, not assumed to be 64. The minimum
usable count is the smallest count across the selected car arrays; `PlayerCarIdx` must fall inside it.

For each valid non-player car, normalize lap distance modulo one and calculate the rearward physical-track gap
`gap = Wrap01(playerLapDistPct - carLapDistPct)`. Reject zero/invalid values, cars outside a configurable rear window,
and cars reported off-track or on pit road. `CarIdxLap` is diagnostic evidence and helps reject discontinuities, but
must not turn a physically nearby lapped car into an impossible candidate. The nearest remaining positive gap is the
candidate. Because lap fraction is not metres, the first implementation reports normalized track distance and does
not fabricate a metre value or track length.

Maintain a timestamped history per selected car. Closing rate is positive when the rear gap shrinks:
`closing = -(filteredGapNow - filteredGapPrevious) / dt`, in lap fractions/second. Reject `dt` outside 5–250 ms,
wrap jumps, teleport/pit transitions and derivatives above an empirically validated ceiling. Filter gap with a
150 ms time-constant EWMA; filter closing through a five-sample median followed by a 250 ms EWMA. Calculate
`timeToOverlap = filteredGap / max(filteredClosing, epsilon)` only while closing is positive.

Candidate identity changes reset derivative history. Keep the current candidate for at least 250 ms and switch only
when another car is at least 20% or 0.002 lap closer (whichever is smaller); this prevents a width/intensity jump when
two cars exchange nearest status. Proposed defaults, to be replay-tuned, are: rear window 0.08 lap, meaningful closing
0.0015 lap/s, warning entry below 8 s time-to-overlap for 300 ms, critical entry below 3 s for 200 ms. Exit warning
above 10 s or below 0.0010 lap/s for 500 ms; exit critical above 4 s for 400 ms. This persistence plus separate exit
thresholds supplies hysteresis without delaying an immediate overlap clear.

### State and visual contract

Publish generic state `None`, `Warning` or `Critical` plus normalized proximity and closing intensity. Width is driven
only by proximity, mapping the far rear-window edge to roughly 8% of binocular width and near overlap to at most 35%.
Intensity is driven only by filtered closing rate/time-to-overlap. Native drawing adds a feathered, narrow glow at the
top centre of the post-crop render boundary. It never chooses a side. `CarLeftRight` values 2–6, a gap below the
validated overlap floor, stale telemetry, inactive SDK state, session reset or disconnect clear it immediately so the
existing left/right glow can take over.

Proposed keys are `iracing_rear_pressure_enabled` (default 0), `iracing_rear_pressure_window` (0.01–0.20 lap),
`iracing_rear_pressure_warning_ttc` (2–15 s), `iracing_rear_pressure_critical_ttc` (0.5–8 s),
`iracing_rear_pressure_min_closing` (lap/s), `iracing_rear_pressure_min_width`,
`iracing_rear_pressure_max_width`, `iracing_rear_pressure_opacity`, `iracing_rear_pressure_fade` and
`iracing_rear_pressure_color`. The global iRacing section owns them; a per-app enable override follows the existing
`overlay_override_iracing__*` route. The existing `MirrorRacingCues` switch covers it rather than adding another OBS
checkbox.

### Failure handling and tests

Missing optional arrays disable only this cue and add one bounded diagnostic; they must not disconnect lap/flag/spotter
features. Malformed types, offsets or array ranges use the provider's existing fail-closed layout rejection. NaN,
infinity, impossible player index, stale tick, inactive mapping and session change clear state. Selection changes never
reuse the previous car's derivative. If normalized track distance proves too nonlinear for reliable distance ordering,
the feature remains experimental until session-info track length or a validated alternative is available.

Focused fixtures in `Tests/IRacingFixtures/Program.cs` should cover: same-lap and lapped physical followers; start/finish
wrap; falling-away cars; sustained and brief closing; two-car candidate stability; candidate teleport/pit transition;
array-count mismatch; invalid player index; overlap handoff for all actionable `CarLeftRight` values; stale/inactive/
disconnect clear; and exact warning/critical hysteresis. Native policy fixtures should pin width from proximity,
intensity from closing, centre-only geometry, crop clipping and coexistence/handoff with spotter. Integration tests
should publish through the production provider, broker and versioned racing mapping, then verify native state and the
existing non-persistent presentation-test path.

Likely modifications are `XRViewLab.UI/IRacingTelemetryProvider.cs` (array readers, candidate/filter state),
`ViewLabEvents.cs` (generic rear-pressure event/state), `RacingStateService.cs` (transport),
`NotificationBroker/Program.cs` (settings and event forwarding), `MainWindow.xaml`/`MainWindow.cs`,
`ProfileWindow.xaml`/`ProfileWindow.cs`, `LiveStateService.cs`, `dllmain.cpp`, `xr-viewlab.ini`, the captured factory
baseline, config/architecture docs and iRacing fixtures. It depends on the single broker-owned provider worker,
`Local\XRViewLabRacingState`, the render-edge cue path and `MirrorRacingCues`. Limitations are non-uniform lap fraction,
no authoritative physical range or relative velocity vector, sparse/invalid opponent telemetry, and uncertain pit/
start-finish updates; live and replay captures must establish thresholds.

## Planned feature: Connection Icons in the Performance HUD

This is an implementation plan. Connection health belongs in the existing Performance HUD icon row, not in a visor
border or a second edge effect. Healthy metrics draw nothing.

### Telemetry, filtering and states

Read the optional iRacing scalars `ChanLatency` (seconds), `ChanAvgLatency` (seconds), `ChanQuality` (percent unit),
`ChanPartnerQuality` (percent unit) and `ChanClockSkew` (seconds). Before thresholding, fixture and live telemetry must
confirm whether quality is encoded as 0–1 or 0–100; normalize exactly once and reject values outside the validated
range. The four independently classified indicators are latency (`L`), player receive/transmit quality (`Q`), partner/
server quality (`P`) and clock synchronization (`S`). Do not substitute ViewLab's ICMP probe for these simulator
channels and do not call derived quality packet-loss percentage unless iRacing documents that conversion.

Use time-based filters so behaviour is independent of SDK tick rate: latency/current-average maximum through a
five-sample median and 500 ms EWMA; each quality through a 1 s EWMA; absolute clock skew through a five-sample median
and 750 ms EWMA. A single invalid scalar suppresses only its icon. Initial proposed thresholds are latency warning
120 ms/critical 250 ms, quality warning below 98%/critical below 90%, partner quality the same, and absolute skew
warning 50 ms/critical 150 ms. Recovery thresholds are respectively 100/200 ms, 99/94%, and 30/100 ms. These defaults
must be compared with iRacing's own L/Q/S meter in live sessions before release.

Each metric has `Unavailable`, `Healthy`, `Warning` and `Critical`. Warning requires 1.5 s continuously beyond its
entry threshold; critical requires 3 s beyond critical (or promotion after warning); one sub-250 ms spike cannot
change state. Critical falls to warning only after 3 s below its critical recovery threshold. Any warning returns to
healthy only after 5 s below warning recovery. A newly active session gets a 3 s warm-up. Stale/inactive/disconnect
clears stale numerical samples; after a previously active session is lost for 1 s, one grey `LINK` unavailable icon may
appear, but normal launcher/menu idle remains quiet.

### Visual and configuration contract

The native Performance HUD packs only non-healthy connection glyphs after its existing enabled metrics: amber outline
plus `L`, `Q`, `P` or `S` for warning; red filled glyph for critical; grey `LINK` only for a sustained loss of a formerly
active feed. Multiple unhealthy channels may coexist and retain stable order. Icons use the current HUD scale, opacity,
anchor, row packing and alarm visibility rules; they do not resize other metric values unpredictably and never draw a
border. Main/profile previews need deterministic synthetic warning/critical states using the shared HUD footprint
resolver.

Proposed keys are `iracing_connection_icons_enabled` (default 0), per-channel enable keys,
`iracing_connection_latency_warning_ms`, `iracing_connection_latency_critical_ms`, quality/partner warning and critical
percentages, skew warning/critical milliseconds, `iracing_connection_warning_hold_ms`,
`iracing_connection_critical_hold_ms` and `iracing_connection_recovery_ms`. Clamp warning/critical ordering in the same
way as existing HUD thresholds. Per-app enable and threshold overrides use canonical iRacing override keys; no new
position store is created.

### Failure handling and tests

Absent channel variables leave those icons unavailable without breaking other iRacing cues. Invalid type/range,
NaN/infinity, stale tick, inactive mapping, reconnect and session reset clear filter history. The renderer receives only
generic channel states, never iRacing variable names. Version mismatch in the racing mapping fails closed and logs once;
it cannot freeze the last warning.

Focused tests cover each exact threshold, short spikes, sustained warning/critical, hysteretic downgrade and recovery,
quality normalization, one missing channel, replay/inactive data, stale/disconnect warm-up and simultaneous bad metrics.
HUD layout fixtures cover zero healthy footprint, stable icon order, row wrapping, alarm-only visibility and main/profile
preview parity. Integration fixtures write real SDK variable headers/values, run the production provider and broker,
inspect the generic mapping, and verify native state clears after stale/disconnect.

Likely files are `IRacingTelemetryProvider.cs`, `ViewLabEvents.cs`, `RacingStateService.cs`,
`NotificationBroker/Program.cs`, the HUD models/settings in `OverlaySettingsModels.cs`, `MainWindow.xaml`/
`MainWindow.cs`, `ProfileWindow.xaml`/`ProfileWindow.cs`, `LiveStateService.cs`, `dllmain.cpp`, `xr-viewlab.ini`, factory
baseline, `Tests/IRacingFixtures`, `Tests/RenderPolicyFixtures.cpp`, `Verify-PerformanceHud.ps1` and config/architecture
docs. Dependencies are the broker's sole SDK worker, generic racing mapping, existing `HudWidgetId` registry and HUD
packing/alarm policy. Limitations are simulator-specific channel semantics, undocumented quality scaling until measured,
uncertain replay/single-player partner quality, and no proof that a bad metric identifies the local ISP rather than the
server or route.

## Planned feature: Grip-O-Bar

This is an implementation plan for whole-car grip loss and signed slip direction. It must never label an individual
tyre as losing grip.

### Telemetry and derived model

Read `Speed` (m/s), `SteeringWheelAngle` and `SteeringWheelAngleMax` (rad), `YawRate` (rad/s), `VelocityX` and
`VelocityY` (m/s), `Yaw` (rad), `LatAccel` (m/s²), `Throttle`, `Brake`, `Gear`, `IsOnTrack` and `OnPitRoad`. Prefer the
normal tick-rate variables initially; `_ST` 360 Hz variants require a separately validated sub-tick reader and must not
be sampled as ordinary scalar fields. The SDK descriptions do not establish in this repository whether X/Y velocity is
body-local or world-fixed, nor its sign convention. A live straight-line and constant-radius validation must establish
axes. If world-fixed, rotate `(VelocityX, VelocityY)` by `-Yaw`; if body-local, use the validated forward/lateral axes
directly. Until that test is pinned, the feature remains disabled.

Compute steering normalization `s = clamp(SteeringWheelAngle / SteeringWheelAngleMax, -1, 1)`. A fixed bicycle model
cannot be truthful because wheelbase and steering ratio are not exposed. Instead learn an expected-yaw gain by speed
bin during stable-grip samples: `gain = YawRate / (s * Speed)`, then `expectedYaw = learnedGain(speedBin) * s * Speed`.
Update a bounded EWMA gain only at 10–60 m/s, moderate steering, low brake, low absolute slip angle, consistent yaw/
steer sign and non-spin yaw rate; require at least 3 s of accepted samples per bin before that bin can drive a warning.
Interpolate adjacent learned bins and freeze learning whenever a grip cue is active. Session/car change clears the
learned model. An optional future session-info car identity can cache validated gains, but v1 must not reuse gains
across unknown cars.

After axis validation, calculate sideslip `beta = atan2(lateralVelocity, max(abs(forwardVelocity), 1 m/s))` and yaw
response error `yawError = (YawRate - expectedYaw) / max(abs(expectedYaw), 0.15 rad/s)`. Clamp each robustly. Combine
magnitude as `severity = max(abs(beta)/betaRed, 0.65*abs(yawError) + 0.35*abs(beta)/betaOrange)`; the constants are
replay-tuned, with initial `betaOrange=0.08 rad` and `betaRed=0.16 rad`. Signed direction is a confidence-weighted blend
of lateral sideslip and yaw deviation, not steering direction alone. Positive/negative maps to lower-left/lower-right
only after live sign validation; when the two signals strongly disagree, reduce confidence or suppress rather than
show the wrong side.

Median-filter each raw channel over five samples. Use a 120 ms EWMA for beta/yaw error and a 180 ms attack/350 ms
release envelope for severity. Initial states are `Clear`, `Yellow`, `Orange`, `Red`: enter at 0.20, 0.45 and 0.75 for
150 ms; exit below 0.15, 0.35 and 0.60 for 300 ms. Direction changes require 200 ms consistent opposite sign and pass
through clear/low intensity, preventing rapid left/right flicker.

### Gating, visual and configuration contract

Fail closed below 10 m/s, in neutral/reverse, off track, on pit road, before the expected-yaw model is trained, or with
invalid/stale telemetry. Suppress beyond useful recovery when `abs(beta) > 0.61 rad` (35 degrees),
`abs(YawRate) > 1.5 rad/s`, or direction confidence is low; a spin is not a reason to hold a permanent red cue. Thresholds
are initial validation values, not claims of vehicle physics.

Draw one directional lower-peripheral bar: signed motion/deviation toward one side activates only the corresponding
lower-left or lower-right segment. Yellow means onset, orange significant slip and red severe loss. Length and opacity
track severity within each state; a short feathered release prevents flicker. The label and docs say whole-car grip/slip
direction only. It coexists with top-centre rear pressure and side spotter glow, uses the post-crop boundary, and follows
`MirrorRacingCues`.

Proposed keys are `iracing_grip_bar_enabled` (default 0), `iracing_grip_bar_min_speed`, yellow/orange/red entry values,
`iracing_grip_bar_attack_ms`, `iracing_grip_bar_release_ms`, `iracing_grip_bar_width`,
`iracing_grip_bar_opacity`, three fixed/optional RGB colours, and an internal diagnostic sign override exposed only if
axis validation proves hardware/runtime variability. Per-app enable/settings use canonical iRacing overrides.

### Failure handling and tests

Any missing required variable disables only Grip-O-Bar with one bounded diagnostic. Reject non-finite values,
`SteeringWheelAngleMax <= 0`, impossible speed/velocity disagreement and unsupported axis calibration. Stale/inactive/
disconnect/session change clears output and learned bins. A telemetry discontinuity resets filters instead of producing
a red flash. No tyre temperature, pressure or wear heuristic substitutes for actual dynamic response; those values do
not satisfy the MAIRA-style yaw-versus-steer and sideways-motion requirement.

Focused maths fixtures cover adaptive expected-yaw learning and bin interpolation, correct response/no cue, under- and
over-response, pure sideslip, signed direction, signal disagreement, all colour thresholds/hysteresis, low speed,
reverse, pit/off-track, untrained model, stale data, discontinuity and spin suppression. Recorded straight, steady-corner,
recoverable slide and spin traces must pin axis/sign conventions before enabling production defaults. Provider/broker/
mapping integration fixtures verify generic severity/direction and prompt clear; native fixtures verify lower-left versus
lower-right geometry, post-crop clipping, colour, fade, OBS routing and main/profile preview parity.

Likely changes are `IRacingTelemetryProvider.cs` (validated dynamic model), a new compact pure-maths
`GripEstimator.cs`, `ViewLabEvents.cs`, `RacingStateService.cs`, `NotificationBroker/Program.cs`, global/profile iRacing
controls, `LiveStateService.cs`, `dllmain.cpp`, defaults/baseline and tests/docs. It depends on the sole provider worker,
generic racing transport, render-edge cue path and preview models. Principal limitations are absent wheelbase/steering
ratio/tyre forces, unknown velocity coordinate convention until measured, adaptive training time, car/setup dependence,
and inability to identify a particular tyre or perfectly separate understeer, oversteer, banking and surface effects.

## Shared transport and delivery plan for the three features

Extend the generic `Local\XRViewLabRacingState` contract once, not once per feature. Define a generation-last v2 layout
with explicit size and fields for rear state/proximity/intensity, four connection states, and grip severity/direction.
The producer creates the new size; the native consumer maps the full object, validates magic/version/size and accepts v1
as clear defaults during rolling process restarts. Unknown versions clear all new effects and log once. Keep raw SDK
fields out of `dllmain.cpp`. Add presentation-test flags without persisting global settings.

Implement in risk order: connection icons, rear pressure, then Grip-O-Bar only after recorded axis/model validation. Each
increment updates `STATE.md`, `CHANGELOG.md`, `docs/CONFIG.md`, `docs/ARCHITECTURE.md`, `repo-index.json`, factory
baseline and contracts in the same change; runs focused fixtures before the full deterministic suite; then requires live
replay and headset validation. None of these plans require theme, OBS plugin, mirror-routing or unrelated work.
