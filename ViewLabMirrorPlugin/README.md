# ViewLab Mirror — OBS Studio source plugin

Initial buildable skeleton for ViewLab's own OBS capture route. It fills the same basic
role as the third-party `OpenXR Mirror Capture` source while giving ViewLab control over
future ViewLab-specific capture features (feature-selective mirroring, both-eye modes,
capture-safe overlays).

## Architecture

- `viewlab-mirror.c` — the OBS module. Registers one video source, `ViewLab Mirror`
  (`viewlab_mirror_source`), with a basic configuration UI (eye-mode selector). Every
  libobs function is resolved at runtime from the `obs.dll` already loaded in the OBS
  process, so the plugin builds with MSVC alone — no OBS SDK checkout, no import library,
  no version-pinned link dependency.
- `obs_abi.h` — minimal transcription of the libobs C ABI used (declarations and the
  `obs_source_info` layout from obs-studio's `libobs/obs-source.h`, GPL-2.0-or-later).
  `obs_register_source_s` receives our struct size and libobs copies the compatible
  prefix, which keeps the layout safe across OBS releases. `obs_module_ver` reports the
  host's own `obs_get_version()` so the loader accepts the module.
- `viewlab_mirror_contract.h` — the versioned shared frame-transfer contract
  (`Local\XRViewLabMirrorSurface`): a control block plus a triple-buffered ring of D3D11
  shared textures. The ViewLab OpenXR layer is the producer; it will copy the submitted
  eye image plus every checkbox-selected ViewLab feature once per frame at the proven
  post-`xrBeginFrame` capture point, then publish `displayIndex`/`frameNumber`. Because
  ViewLab owns the producer, its own drawing can never be overwritten by the capture copy
  (the defect of the third-party route). Until the producer ships, the source renders
  nothing — no placeholder frames are fabricated.

## Build

MSBuild x64 Release: `msbuild ViewLabMirrorPlugin.vcxproj /p:Configuration=Release /p:Platform=x64`
→ `x64/Release/viewlab-mirror.dll`. `build.ps1` builds it as part of the full release.

## Installation

ViewLab's `Install ViewLab Mirror Plugin` button copies the bundled DLL to OBS's
per-user plugin location (no elevation, supported by OBS 28+):

```
%APPDATA%\obs-studio\plugins\viewlab-mirror\bin\64bit\viewlab-mirror.dll
```

Restart OBS after installing or updating, then add the `ViewLab Mirror` source.

## Provenance and licence

- Reference implementation studied (not copied): Jabbah/OpenXR-Layer-OBSMirror, MIT.
- libobs ABI declarations transcribed from obs-studio (GPL-2.0-or-later).
- This plugin: **GPL-2.0-or-later** (see `LICENSE`).
