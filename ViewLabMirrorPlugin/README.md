# ViewLab Media Capture (VLMC) — OBS Studio plugin

ViewLab's own OBS capture source:

**ViewLab Media Capture** source (`viewlab_media_capture`) — brings the ViewLab-composited VR
view (game frame plus the selected ViewLab overlays) into OBS. It fills the same role as the
third-party `OpenXR Mirror Capture` source but is driven by a ViewLab-owned producer, so
ViewLab's own drawing can never be overwritten (the defect the third-party route suffers).

Colour grading, sharpening and stabilization are **not** here — they live in the separate
**ViewLab Enhancer** filter (`ViewLabStabilizerFilter/`), which you add to this source (or any
source) via its Filters menu.

## Architecture

- `viewlab-mirror.c` — the OBS module: registers the capture source and (via
  `viewlab_media_filter.c`) the media filter. Every libobs function is resolved at runtime from
  the `obs.dll` already loaded in the OBS process, so the plugin builds with MSVC alone — no OBS
  SDK checkout, no import library, no version-pinned link dependency.
- `viewlab_media_filter.c` — the effect-based video filter. Its OBS effect is compiled from an
  embedded string at create time, so no external `.effect` data file ships.
- `obs_abi.h` — minimal transcription of the libobs C ABI used (the `obs_source_info` layout
  plus the source/filter/effect entry points), from obs-studio (GPL-2.0-or-later).
  `obs_register_source_s` receives our struct size and libobs copies the compatible prefix.
- `viewlab_mirror_contract.h` — the versioned shared frame-transfer contract
  (`Local\XRViewLabMirrorSurface`, **v2**): a control block plus a triple-buffered ring of D3D11
  shared textures. The ViewLab OpenXR layer is the producer; it copies the submitted eye(s) plus
  every selected ViewLab feature once per frame at the post-`xrBeginFrame`/`xrEndFrame` capture
  point, then publishes `displayIndex`/`frameNumber`/`heartbeatTick`. **v2** adds
  `requestedEyeMode`: the OBS source writes the user's selected eye (left / right /
  side-by-side) and the producer publishes that eye, falling back to left on mono titles.

## Build

MSBuild x64 Release: `msbuild ViewLabMirrorPlugin.vcxproj /p:Configuration=Release /p:Platform=x64`
→ `x64/Release/viewlab-mirror.dll`. `build.ps1` builds it as part of the full release.

## Installation

ViewLab's `Install ViewLab Media Capture Plugin` button copies the bundled DLL into the OBS
install's `obs-plugins\64bit` folder (the location current OBS actually scans). Restart OBS,
then add the **ViewLab Media Capture** source and/or the **ViewLab Media Filter**.

## Provenance and licence

- Reference implementation studied (not copied): Jabbah/OpenXR-Layer-OBSMirror, MIT.
- libobs ABI declarations transcribed from obs-studio (GPL-2.0-or-later).
- This plugin: **GPL-2.0-or-later** (see `LICENSE`).
