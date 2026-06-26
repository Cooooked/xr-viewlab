# FEATURES.md

## Overview
This document enumerates the major features of the ViewLab application and the modified ReShade DLL, describing their purpose, UI surface, involved files, data/config usage, runtime behavior, and known risks. It is intended for future AI agents to understand the system safely.

---

## 1. Gameplay Mode
- **Purpose**: Enables a curated VR experience by disabling desktop effects and switching to XR/HMD rendering.
- **UI/Control Surface**: Checkbox in MainWindow labeled “Gameplay Mode”.
- **Files Involved**: 
  - `MainWindow.xaml` (checkbox binding)
  - `ReShadeControlService.cs` (writes `xr_mode` to shared memory)
  - `runtime.cpp` (reads `xr_mode` and adjusts effect pipeline)
- **Data/Config Used**: 
  - INI setting `GameplayMode=1` (persisted across sessions)
  - Shared‑memory field `xr_mode` (0 = off, 1 = on)
- **Runtime Behaviour**: 
  - When enabled, desktop runtime effects are suppressed; XR/HMD runtime continues.
  - Loading bar may need special handling (see Loading/Progress Bar).
- **Known Risks**: 
  - If loading bar re‑enables desktop effects, visual artifacts may appear.
  - Incorrect shared‑memory write can leave mode stuck.

---

## 2. Tuning Mode
- **Purpose**: Allows fine‑grained adjustment of effect parameters while keeping desktop effects enabled for preview.
- **UI/Control Surface**: “Tuning Mode” toggle.
- **Files Involved**: 
  - `MainWindow.xaml` (toggle)
  - `ReShadeControlService.cs` (writes `tuning_mode` flag)
  - `runtime_gui.cpp` (uses flag to enable UI sliders)
- **Data/Config Used**: 
  - INI `TuningMode=1`
  - Shared‑memory `tuning_mode` flag
- **Runtime Behaviour**: 
  - Enables runtime UI overlays for parameter tweaking.
  - Effects are applied instantly to preview.
- **Known Risks**: 
  - Over‑exposure of sliders may allow invalid values that crash the effect compiler.

---

## 3. VR Quad Position/Scale/Alpha Controls
- **Purpose**: Adjust the 3D quad that displays the VR overlay (position, scale, opacity).
- **UI/Control Surface**: Sliders/inputs in MainWindow for X/Y/Z position, Scale, Alpha.
- **Files Involved**: 
  - `MainWindow.xaml` (UI elements)
  - `XRControlBlock` struct in `shared_memory.cpp`
  - `ReShadeControlService.cs` (writes to shared memory)
- **Data/Config Used**: 
  - Shared‑memory struct fields: `quad_position`, `quad_scale`, `quad_alpha`
- **Runtime Behaviour**: 
  - Values are read by the ReShade DLL to transform the overlay quad in real time.
- **Known Risks**: 
  - Rapid changes can cause visual “jumping” or echo artifacts.
  - Incorrect bounds may clip the overlay.

---

## 4. VR Quad Reset / Save Default
- **Purpose**: Reset the VR Quad to its saved baseline or save current configuration as the new default.
- **UI/Control Surface**: Buttons “Reset” and “Save Default”.
- **Files Involved**: 
  - `MainWindow.xaml` (button click handlers)
  - `ReShadeControlService.cs` (writes reset/save commands to shared memory)
  - `runtime.cpp` (applies saved baseline on next frame)
- **Data/Config Used**: 
  - Shared‑memory `quad_baseline` struct for saved values.
- **Runtime Behaviour**: 
  - Reset restores saved baseline; Save Default overwrites the stored baseline.
- **Known Risks**: 
  - Confusing interaction if saved baseline is not set before reset.

---

## 5. OpenXR/ReShade Menu Open‑Close
- **Purpose**: Toggle the in‑HMD ReShade menu (used for effect selection, tuning, etc.).
- **UI/Control Surface**: Menu button in MainWindow.
- **Files Involved**: 
  - `MainWindow.xaml` (button)
  - `ReShadeControlService.cs` (edge‑triggered command to DLL)
  - `openxr_overlay.hpp` (overlay struct definitions)
- **Data/Config Used**: 
  - Shared‑memory command flag `menu_toggle` (edge‑triggered)
- **Runtime Behaviour**: 
  - Toggles visibility of overlay; uses edge‑triggered semantics to avoid spam.
- **Known Risks**: 
  - Incorrect edge detection may cause flickering or missed toggles.

---

## 6. Visual Masks
- **Purpose**: Apply per‑pixel masks to the VR overlay for artistic or safety purposes.
- **UI/Control Surface**: Mask selector dropdown.
- **Files Involved**: 
  - `MainWindow.xaml` (dropdown)
  - `runtime_gui.cpp` (loads mask texture)
  - `addon.cpp` (registers mask shaders)
- **Data/Config Used**: 
  - INI `MaskFile=path\to\mask.dds`
  - Shared‑memory `mask_id` index
- **Runtime Behaviour**: 
  - Mask texture is sampled during rendering; can be swapped at runtime.
- **Known Risks**: 
  - Invalid mask file path can cause rendering fallback or crash.

---

## 7. Shared‑Memory Remote Control
- **Purpose**: Enable ViewLab to send commands (e.g., mode switches, parameter updates) to the ReShade DLL.
- **Files Involved**: 
  - `shared_memory.cpp` (defines command struct)
  - `ReShadeControlService.cs` (writes commands)
  - `runtime.cpp` (interprets commands)
- **Data/Config Used**: 
  - Shared‑memory struct `control_commands` with fields like `command_id`, `payload`
- **Runtime Behaviour**: 
  - Commands are processed on the DLL side; may trigger mode changes, reloads, etc.
- **Known Risks**: 
  - Unvalidated command fields can lead to undefined behavior.

---

## 8. Effect Runtime Sync
- **Purpose**: Keep desktop and XR/HMD effect runs in sync when switching modes.
- **Files Involved**: 
  - `runtime.cpp` (sync logic)
  - `runtime_gui.cpp` (exposes sync status)
- **Data/Config Used**: 
  - Shared‑memory flag `sync_state`
- **Runtime Behaviour**: 
  - When switching from Gameplay to Tuning, ensures no stray desktop effects remain.
- **Known Risks**: 
  - Missed sync can cause lingering desktop effects in XR mode.

---

## 9. Desktop Runtime vs XR/HMD Runtime
- **Purpose**: Separate rendering paths: desktop for screen preview, XR/HMD for headset rendering.
- **Files Involved**: 
  - `runtime.cpp` (selects runtime based on `xr_mode`)
  - `openxr_hooks_swapchain.cpp` (XR swapchain handling)
- **Data/Config Used**: 
  - `xr_mode` shared‑memory field
- **Runtime Behaviour**: 
  - Desktop runtime suppressed in Gameplay Mode; XR runtime always active when headset is connected.
- **Known Risks**: 
  - Accidentally enabling desktop effects in XR mode can cause performance drops.

---

## 10. Loading/Progress Bar
- **Purpose**: Indicate loading progress of effects and shaders during startup.
- **UI/Control Surface**: Progress bar in MainWindow.
- **Files Involved**: 
  - `MainWindow.xaml` (progress bar)
  - `runtime.cpp` (calls `update_effects()` during load)
  - `runtime_gui.cpp` (updates UI based on `_techniques` population)
- **Data/Config Used**: 
  - Shared‑memory `_techniques` array populated after `update_effects()`
- **Runtime Behaviour**: 
  - Loading‑bar fix allowed `update_effects()` during loading, populating `_techniques`.
  - `render_effects()` must respect Gameplay/Tuning gating.
- **Known Risks**: 
  - If gating is missing, desktop effects may continue in Gameplay Mode.

---

## 11. Config Loading/Saving
- **Purpose**: Persist user settings across sessions.
- **Files Involved**: 
  - `ConfigService.cs` (reads/writes INI files)
  - `MainWindow.xaml.cs` (loads on startup)
- **Data/Config Used**: 
  - INI files in `config/` directory
- **Runtime Behaviour**: 
  - Settings loaded before UI initialization; saved on exit.
- **Known Risks**: 
  - Corrupt INI syntax can prevent load, leading to default values.

---

## 12. Build / MSI Installer
- **Purpose**: Package the application and DLL for distribution.
- **Files Involved**: 
  - `build.ps1` (orchestrates build and MSI creation)
  - `Product.wxs` (WiX installer script)
- **Data/Config Used**: 
  - Version numbers in `AssemblyInfo.cs` and `Product.wxs`
- **Runtime Behaviour**: 
  - MSI installs files to `Program Files\ViewLab` and registers shared‑memory components.
- **Known Risks**: 
  - Version mismatch between ViewLab and DLL can cause runtime errors.

*End of FEATURES.md.*