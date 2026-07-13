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

## Presentation status

Provider correctness and recorded fixtures are complete. Peripheral glow, flag border, lap card,
independent broker lifetime and live/replay iRacing headset validation are the next implementation
stages and must not be reported as complete yet.
