# XR ViewLab — Project Status

**Version:** 4.1.7  
**Date:** 2026-06-25

---

## Overall Architecture

XR ViewLab is a two-part system:

| Component | Location | Role |
|-----------|----------|------|
| **OpenXR API Layer** (C++ DLL) | Separate repo / `ReshadeAI/reshade/` | Intercepts OpenXR calls, applies FOV/render-height overrides, renders the ReShade HMD menu overlay |
| **ViewLab companion app** (this project, C# WPF) | `ViewLab/` | Settings UI + remote control for the layer and the ReShade DLL |

The C# app has no knowledge of game processes. It communicates with the running ReShade DLL via:
1. **INI file** (`%LocalAppData%\XR ViewLab\xr-viewlab.ini`) — persistent settings the layer reads on each session start.
2. **Windows named shared memory** (`Local\ReShadeXRControl`, struct `XRControlBlock`) — live per-session controls (gameplay mode, headless window, reposition/transform).

---

## Main Systems

### OpenXR Layer (DLL)
- Hooks `xrEndFrame`, injects an `XrCompositionLayerQuad` carrying the ReShade ImGui menu.
- Reads `XRControlBlock` each frame; applies headless/topmost/edit-mode flags to the preview window.
- Loads/saves quad transform to `C:\ProgramData\ReShade\openxr_quad_transform.ini`.
- Two modes: **Gameplay** (desktop dormant, VR-only) and **Tuning** (full desktop effects active).

### ViewLab UI (this project)
- **Render card** — vertical/horizontal render-height sliders that write directly to the INI.
- **Enabled badge** — toggles the OpenXR registry entry that enables/disables the layer globally.
- **App profiles** — per-game overrides stored in `HKCU\Software\cooooked\xr-viewlab\Apps\`.
- **ReShade OpenXR card** — live controls written to `XRControlBlock` via `ReShadeControlService`.
- **Update checker** — polls GitHub releases API, downloads and launches the MSI installer.

### ReShadeControlService
Wraps `OpenFileMappingW`/`MapViewOfFile`. Polled at 1 Hz. Writes `XRControlBlock` fields and increments `revision` on every write so the DLL detects changes cheaply.

---

## Startup Flow

1. `App.Main()` → `Application.Run()` → `MainWindow()`.
2. Config migration: if `%LocalAppData%\XR ViewLab\xr-viewlab.ini` absent but legacy INI present in EXE dir, copy it.
3. Load window size and column widths from INI.
4. `LoadSettings()` — reads all render values and checkbox states from INI.
5. `LoadAppProfiles()` — enumerates registry app subkeys, filters junk apps.
6. `CheckManifestHealth()` — warns if the layer is not registered in `HKLM\...\OpenXR\1\ApiLayers\Implicit`.
7. `CheckForUpdatesOnLaunchAsync()` — background GitHub release check.
8. `_xrPollTimer` starts (1 Hz) — attempts `OpenFileMappingW` until the game is running.

---

## Configuration

| File | Purpose |
|------|---------|
| `%LocalAppData%\XR ViewLab\xr-viewlab.ini` | All global settings (render values, enabled flag, window size, column widths, ReShade menu prefs) |
| `HKCU\Software\cooooked\xr-viewlab\Apps\<name>` | Per-app profile registry keys |
| `HKLM\Software\Khronos\OpenXR\1\ApiLayers\Implicit` | Layer registration (value = manifest path, data = 0 enabled / 1 disabled) |
| `C:\ProgramData\ReShade\openxr_quad_transform.ini` | HMD menu quad position (owned by ReShade DLL, not ViewLab) |

---

## Dependencies

- **.NET 8, WPF** — UI framework.
- **kernel32.dll** — `GetPrivateProfileString`, `WritePrivateProfileString` (INI), `OpenFileMappingW`, `MapViewOfFile` (shared memory).
- **user32.dll** — `FindWindowW`, `ShowWindow` (preview window control).
- **advapi32 / Microsoft.Win32** — Registry access for app profiles and layer registration.
- **System.Net.Http** — GitHub releases API for update check.
- No NuGet package dependencies.

---

## Current Working Features

- Global render-height control (total mode and split top/bottom mode).
- Horizontal render-width control.
- Per-app enable/disable via registry.
- Per-app custom render profiles (double-click in app list).
- OpenXR layer enable/disable (registry toggle with animated badge).
- Four responsive layout modes (Mini / Small / Medium / Large).
- Visual Masks popup (horizontal + vertical edge masks).
- Render options: foveated-center compensation, stencil/crop outer-edges-only flags.
- ReShade OpenXR live controls: gameplay mode, desktop VR menu visibility, headless, always-on-top, reposition, transform (via `XRControlBlock`).
- Auto-save settings on every slider/checkbox change.
- Config migration from legacy EXE-dir INI to `%LocalAppData%`.
- Column width persistence.
- GitHub release update checker with in-app MSI download and launch.
- Manifest health check on startup.

---

## Pending / Known Limitations

- **`SaveReShadeMenuSettings()`** — stub that will persist ReShade menu UI state (headless, always-on-top, etc.) to the INI so settings survive between sessions. Currently the controls take effect live but are not saved.
- **OpenVR ReShade menu section** — UI card exists ("Coming soon"). OpenVR control not yet implemented.
- **Quad alpha** — `XRControlBlock.quad_alpha` field is tracked but the ImGui render pipeline in ReShade does not yet apply it.
- **Preview window cursor passthrough** — clicking in the ReShade preview window does not interact with the ImGui menu (read-only).
- **Nullable warnings** — CS8632 throughout; `#nullable enable` not yet added to the project.
- **NETSDK1137** — project uses `Microsoft.NET.Sdk.WindowsDesktop` SDK alias; harmless but could be simplified to `Microsoft.NET.Sdk`.
- **`xr-viewlab.sln`** — references three non-existent C++ projects from an earlier version of the solution. The C# project is not in it. Build is done via `dotnet build xr-viewlab.csproj` directly.

---

## Future Roadmap

1. Implement `SaveReShadeMenuSettings()` — write the 10 ReShade INI keys from the live UI state.
2. Implement `LoadReShadeMenuSettings()` — restore those settings on startup and re-apply to `XRControlBlock` when a game connects.
3. OpenVR ReShade menu section — if/when OpenVR support is added to the layer.
4. Quad position display — read `XRControlBlock.quad_pos_*` during edit mode and show live coordinates in the UI.
5. Replace `xr-viewlab.sln` with a correct solution containing only the C# project.
6. Add `#nullable enable` project-wide and resolve all CS8632 warnings.
