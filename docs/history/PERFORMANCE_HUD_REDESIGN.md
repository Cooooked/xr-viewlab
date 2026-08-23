# Performance HUD redesign

## Current measurement analysis

The former SYS value timed wall-clock duration from entry to `xrBeginFrame` through return from
`xrEndFrame`, divided by `predictedDisplayPeriod`. It included the application's render work, both
runtime calls, compositor/submission blocking, scheduling gaps, and any driver stall inside
`xrEndFrame`. It was therefore neither system load nor application CPU time. Runtime blocking could
move between `xrWaitFrame` and `xrEndFrame` without gameplay changing, producing the observed large
swings and false alarm-only activation.

SYS is replaced by **APP workload**: wall-clock time from a successful `xrBeginFrame` return to
entry into the matching `xrEndFrame`, divided by the detected cadence-aware frame budget. This is a
precise application-side work window. It includes the game's work and waits between those hooks, but
excludes time spent blocked inside `xrBeginFrame` and `xrEndFrame`. It is smoother, refresh-rate
independent as a percentage, and directly answers whether the application is consuming its budget.
It is deliberately not labelled "CPU execution time" because ViewLab does not instrument all game
threads.

## Metrics and widgets

Each widget is described by an identifier, label/icon, source, formatter, unit, progress function,
colour/alarm state, enabled bit, and persisted order. The initial registry is CPU, GPU, APP, and VR;
rendering iterates the enabled ordered registry rather than four fixed slots.

| Widget | Value | Source | Unit |
|---|---|---|---|
| CPU | Total machine CPU utilisation | `GetSystemTimes` deltas | percent |
| GPU | Busiest 3D engine group on ViewLab's D3D adapter | PDH GPU Engine counters, adapter LUID grouped | percent |
| APP | Application-side work window / effective cadence budget | QPC, `xrBeginFrame` return to `xrEndFrame` entry | percent |
| VR | Measured application cadence | QPC interval between `xrWaitFrame` returns | milliseconds |

Unavailable sources retain an explicit unavailable state and display `--`; zero is never invented.
All widget settings and samples are copied to one immutable `HudDrawSnapshot` for both eyes.

## VR cadence model

`predictedDisplayPeriod` is the sole refresh target; no millisecond whitelist exists. A rolling
median of wait-to-wait intervals selects an integer cadence multiple from 1–4 only after sustained
agreement. Thus 8.33 ms is full rate at 120 Hz, 11.11 ms is full rate at 90 Hz but a miss at 120 Hz,
and a stable 16.67 ms at 120 Hz is identified as half-rate reprojection. State uses full-precision
rolling ratios plus hysteresis:

- **On target:** stable full-rate cadence close to the current period.
- **Approaching:** sustained median above 103% but not yet critical.
- **Missed cadence:** sustained median above 108% of the selected budget.
- **Stable reprojection:** a confirmed 1/2, 1/3, or 1/4 cadence close to its multiplied budget.
- **Unstable cadence:** cadence cannot settle on a multiple or has excessive rolling spread.

Normal 8.2/8.3 and 11.1/11.2 variation is absorbed by the rolling median, consecutive-state entry,
lower recovery thresholds, and alarm hold.

## Performance graph

The former trace becomes a bounded multi-channel graph. Channels are retained in a fixed 600-sample
ring and selected independently. Modes only draw channels with compatible units.

| Channel | Source | Unit | Compatible mode |
|---|---|---|---|
| Frame interval | wait-to-wait QPC interval | ms | Milliseconds, Budget % |
| FPS | `1000 / frame interval` | frames/s | FPS |
| Budget deviation | interval minus effective cadence budget | ms | Deviation |
| APP work | begin-return to end-entry QPC interval | ms | Milliseconds, Budget % |
| Wait duration | duration inside `xrWaitFrame` | ms | Milliseconds |
| Submit duration | duration inside `xrEndFrame` | ms | Milliseconds |
| Display period | current `predictedDisplayPeriod` | ms | Milliseconds |

Default remains a single budget-deviation line. Every channel has a fixed colour and the UI explains
its source/unit. Incompatible selected channels remain persisted but are not drawn in the current
mode, avoiding mixed-unit graphs.

## Deferred measurements

- **Render GPU time:** unavailable. Correct implementation requires D3D timestamp/disjoint queries
  around the game's GPU workload; ViewLab currently sees only its own overlay commands and adapter
  utilisation. A utilisation percentage is not a frame time.
- **Exact render CPU time:** unavailable without instrumenting the game's render threads. APP work
  is an accurate wall-clock hook window, not consumed CPU cycles.
- Runtime compositor latency, motion-to-photon latency, and actual reprojection decisions are not
  exposed by core OpenXR hooks used here. Cadence multiple is detected from observed application
  pacing and labelled accordingly.

## Configuration and migration

Existing HUD/trace keys remain valid. New installations enable all four widgets in CPU/GPU/APP/VR
order and enable only Budget deviation in Deviation mode. Missing new keys in older INI files use
those defaults. Live-state version 7 uses the existing reserved tail of the 208-byte mapping for
widget mask/order and graph mode/channels, preserving the mapping size and unrelated fields.

## Performance constraints

History is fixed at 600 samples, draw snapshots are fixed-size, and no GPU query or worker thread is
added. CPU/GPU counters keep the existing bounded 50–1000 ms update rate. Per-frame hook work is QPC
reads and fixed-ring writes only. Logging remains verbose-only and update-rate bounded.
