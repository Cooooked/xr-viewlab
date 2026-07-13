# SYS remaining-headroom model

SYS answers “what is the remaining capacity of the strongest monitored bottleneck?” It is not an
OpenXR duration and it is not an average.

For valid, fresh inputs, pressure is normalised as follows:

- CPU total, CPU peak logical processor and GPU 3D: `p = clamp(utilisation / 100, 0, 1)`.
- physical RAM, commit and VRAM budget: `p = clamp((percentage - 70) / 25, 0, 1)`.
- each source is smoothed by an EMA with alpha 0.25 before composition; the resulting SYS display
  also uses alpha 0.25 to prevent the winning bottleneck changing noisily between samples.
- `strongest = max(valid pressures)`.
- `SYS = 100 × (1 - strongest)`.

Memory below 70% does not claim scarce headroom; 95% or above is treated as exhausted. Missing or
stale inputs are excluded and `headroomCoverage` records the number of valid inputs (maximum six).
An absent thermal or power-limit sensor is not healthy—it is simply outside coverage.

The widget uses inverse thresholds: 30% remaining headroom is warning and 10% is critical by
default. Entry requires three display updates; recovery requires six and respects the configured
alarm hold. Both thresholds are configurable and reset with the default layout.

Examples:

- CPU 35%, peak 62%, GPU 98%, ordinary memory use: strongest 0.98, SYS 2%.
- CPU 42%, one logical CPU 96%, GPU 55%: strongest 0.96, SYS 4%; total CPU cannot hide the core.
- CPU/GPU 50%, VRAM 90%: VRAM pressure is `(90-70)/25 = 0.8`, so SYS is 20%.
- CPU 30%, GPU 40%, RAM 60%, commit 50%, VRAM unavailable: strongest 0.4, SYS 60%, coverage five.

Limitations: Windows GPU-engine counters indicate engine load, not vendor-reported power or thermal
throttling. `CurrentMhz` is a Windows reported clock, not a residency-weighted effective frequency.
SYS therefore describes the strongest measured resource pressure, with coverage shown in the
snapshot, rather than claiming omniscience. A refreshing change for software.
