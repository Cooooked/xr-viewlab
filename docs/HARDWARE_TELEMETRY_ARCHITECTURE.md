# Hardware telemetry architecture

## Boundary and threading

`HardwareTelemetry.cpp` owns native hardware collection. `xrCreateSession` starts one worker and
`xrDestroySession` signals and joins it. The worker sleeps on a condition variable at a bounded
250 ms cadence; shutdown wakes it immediately. PDH queries and DXGI objects are opened once and
closed by the worker. The render thread never enumerates sensors, collects PDH data, queries DXGI
budgets, calls a vendor API, or waits for the worker.

The worker publishes a fixed-size `Snapshot`. `TryGetSnapshot` uses `try_lock`; contention retains
the previous stereo draw snapshot rather than delaying OpenXR. The left-eye draw copies values once
and the right eye reuses precisely the same values. Provider buffers and aggregation arrays are
fixed after initialisation. No sample queue is unbounded.

This is logical isolation from OpenXR and D3D rendering, not process isolation: an implicit API
layer necessarily runs in the game's process. A future out-of-process broker could implement the
same snapshot contract, but this build deliberately does not add another installed service.

## Implemented catalogue

| ID | Widget | Source | Unit | Cadence | Availability |
|---|---|---|---|---|---|
| `cpu` | CPU total | `GetSystemTimes` deltas | % | 250 ms | after two valid samples |
| `cpu_peak` | busiest logical CPU | PDH `Processor Information(*)/% Processor Utility` | % | 250 ms | when English counter exists |
| `cpu_frequency` | reported CPU clock | `CallNtPowerInformation(ProcessorInformation)` average `CurrentMhz` | MHz | 250 ms | when Windows returns processor records; not residency-derived effective clock |
| `gpu` | GPU 3D | PDH `GPU Engine(*)/Utilization Percentage`, grouped by LUID and engine | % | 250 ms | render LUID when present, otherwise busiest adapter |
| `ram` | physical RAM | `GlobalMemoryStatusEx` | % | 500 ms | Windows system provider |
| `commit` | commit pressure | `GetPerformanceInfo` | % | 500 ms | valid non-zero commit limit |
| `vram` | local-memory budget pressure | `IDXGIAdapter3::QueryVideoMemoryInfo` | % | 500 ms | DXGI 1.4 adapter and non-zero OS budget |
| `sys` | remaining system headroom | ViewLab bottleneck composite | % | 250 ms | one or more valid pressure inputs |
| `app` | application work | QPC from `xrBeginFrame` return to `xrEndFrame` entry | budget % | per frame | stable OpenXR frame samples |
| `vr` | VR cadence | QPC wait-to-wait interval | ms | per frame | stable cadence samples |
| `fps` | effective FPS | 1000 / rolling frame interval | fps | per frame | stable cadence samples |
| `frame_interval` | frame interval | rolling wait-to-wait interval | ms | per frame | stable cadence samples |

Samples older than two seconds become `Stale`, never zero. Unsupported providers leave their
metric unavailable. Failure is local to that provider; rendering and the other metrics continue.
The catalogue UI shows provider, unit and runtime-detection status. Enabled widgets pack in saved
order, wrap at the configured two-to-eight widgets per row, and retain unavailable selections.

## Persistence

INI settings use explicit `telemetry_settings_version=1`, per-widget enable/order keys,
`hud_max_per_row`, `hud_sys_warning`, and `hud_sys_critical`. The separate 64-byte
`Local\XRViewLabTelemetryConfigV1` mapping carries live catalogue state and leaves the established
208-byte v7 overlay mapping unchanged. Old explicit CPU/GPU/APP/VR keys are honoured; otherwise the
default is CPU/GPU/SYS/VR.

## Deferred providers and licensing

CPU temperature/package power and GPU clock/temperature/hotspot/board power/fan/memory clock are
not exposed without a trustworthy provider. AMD ADLX, NVIDIA NVAPI, Intel tooling, HWiNFO and
LibreHardwareMonitor remain candidates, not dependencies. They require separate distribution,
licensing, hardware and failure-policy review. ViewLab installs no driver, requires no administrator
privilege for core metrics, and does not require another monitoring application. Missing advanced
sensors are absent rather than plausible-looking zeroes.

