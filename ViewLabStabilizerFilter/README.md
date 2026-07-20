# ViewLab Enhancer — OBS Studio filter plugin

A drop-on-any-source **stabilize + enhance filter** for VR mirror capture. Add it to the
`ViewLab Mirror Capture` source (or the third-party `OpenXR Mirror Capture` source) and it
smooths shaky head-motion and colour-grades a mirrored VR view so it is comfortable to watch on
a stream or recording. The stabilizer is modelled on LiveVisionKit's (LVK) Video-Stabilization
filter, but dependency-free.

> The on-disk project and DLL keep the historical name `viewlab-stabilizer` for build stability;
> the user-facing product (OBS source, ViewLab UI) is **ViewLab Enhancer**.

## What it does

Stabilization (optional):

1. Each frame it renders the filter's target into a small offscreen luma buffer and reads it
   back to the CPU (`gs_texrender` → `gs_stagesurface`).
2. A grid of **texture-gated feature blocks** is matched frame-to-frame (flat/black regions such
   as the visor are ignored), and a least-squares **similarity fit** (translation + rotation +
   uniform scale, with outlier rejection) recovers the dominant camera motion — so it corrects
   head **roll** and **zoom**, not just pan.
3. Each motion parameter integrates into a path that a causal low-pass tracks (no buffered frame
   delay ⇒ no added latency) with anti-windup, and the residual jitter is removed by re-framing
   the output within a crop margin — intentional motion is followed, shake is cancelled.

Controls:

- **Stabilization enabled** — low-latency shake smoothing (causal, no buffered frame delay).
- **Correct rotation (head roll)** / **Correct zoom (dolly / breathing)** — toggle the rotation
  and scale components of the similarity fit independently.
- **Smoothing** (0–100) — higher is steadier but re-frames more aggressively (light by default).
- **Max crop** (0–50 %) — the border budget the re-framing is allowed to use. More crop = more
  shake it can hide, at the cost of a tighter zoom.
- **Sharpness** (0–100 %) — unsharp boost; skipped entirely on the GPU when 0.
- **Saturation** (0–200 %) / **Vibrance** (−100…100 %) — vibrance boosts muted colours while
  protecting already-vivid ones.
- **Contrast** (0–200 %), **Brightness** (0–200 %), **Gamma** (10–300 %).

The image adjustments run in a single cheap GPU pass; at their neutral values (and with
stabilization off) the filter is a true zero-cost passthrough (`obs_source_skip_video_filter`).

## Architecture

- `viewlab-stabilizer.c` — the OBS filter module. Registers one filter source,
  `ViewLab Stabilizer` (`viewlab_stabilizer`). Every libobs function is resolved at runtime
  from the `obs.dll` already loaded in the OBS process, so the plugin builds with MSVC alone —
  no OBS SDK checkout, no import library, no version-pinned link dependency.
- `obs_filter_abi.h` — minimal transcription of the libobs C ABI used (the `obs_source_info`
  layout plus the filter-render and graphics-subsystem prototypes from obs-studio,
  GPL-2.0-or-later). `obs_register_source_s` receives our struct size and libobs copies the
  compatible prefix, which keeps the layout safe across OBS releases. `obs_module_ver` reports
  the host's own `obs_get_version()` so the loader accepts the module.

No OpenCV, no external runtime — same dependency-free build style as the ViewLab Mirror plugin.

## Build

MSBuild x64 Release: `msbuild ViewLabStabilizerFilter.vcxproj /p:Configuration=Release /p:Platform=x64`
→ `x64/Release/viewlab-stabilizer.dll`. `build.ps1` builds it as part of the full release.

## Installation

Copy the built DLL into OBS's per-user plugin location (OBS 28+, no elevation):

```
%APPDATA%\obs-studio\plugins\viewlab-stabilizer\bin\64bit\viewlab-stabilizer.dll
```

Restart OBS, then right-click a source → Filters → add **ViewLab Stabilizer**.

## Provenance and licence

- Reference implementations studied (not copied): LiveVisionKit (GPL-3.0) for the stabilization
  approach; obs-studio's built-in effect filters (GPL-2.0-or-later) for the filter-render path.
- libobs ABI declarations transcribed from obs-studio (GPL-2.0-or-later).
- This plugin: **GPL-2.0-or-later** (see `LICENSE`).
