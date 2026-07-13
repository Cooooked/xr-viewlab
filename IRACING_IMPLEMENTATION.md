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
