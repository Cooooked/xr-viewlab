# FEATURES.md

## Overview
This document enumerates the major features of the ViewLab application and the modified ReShade DLL, describing their purpose, UI surface, involved files, data/config usage, runtime behavior, and known risks. It is intended for future AI agents to understand the system safely.

---

## 1. Gameplay Mode
- **Purpose**: Enables a curated VR experience by disabling desktop effects and switching to XR/HMD rendering.
- **UI/Control Surface**: Checkbox in MainWindow labeled â€œGameplay Modeâ€.
- **Files Involved**: 
  - `MainWindow.xaml` (checkbox binding)
  - `ReShadeControlService.cs` (writes `xr_mode` to shared memory)
  - `runtime.cpp` (reads `xr_mode` and adjusts effect pipeline)
- **Data/Config Used**: 
  - INI setting `GameplayMode=1` (persisted across sessions)
  - Sharedâ€‘memory field `xr_mode` (0 = off, 1 = on)
- **Runtime Behaviour**: 
  - When enabled, desktop runtime effects are suppressed; XR/HMD runtime continues.
  - Loading bar may need special handling (see Loading/Progress Bar).
- **Known Risks**: 
  - If loading bar reâ€‘enables desktop effects, visual artifacts may appear.
  - Incorrect sharedâ€‘memory write can leave mode stuck.

---

## 2. Tuning Mode
- **Purpose**: Allows fineâ€‘grained adjustment of effect parameters while keeping desktop effects enabled for preview.
- **UI/Control Surface**: â€œTuning Modeâ€ toggle.
- **Files Involved**: 
  - `MainWindow.xaml` (toggle)
  - `ReShadeControlService.cs` (writes `tuning_mode` flag)
  - `runtime_gui.cpp` (uses flag to enable UI sliders)
- **Data/Config Used**: 
  - INI `TuningMode=1`
  - Sharedâ€‘memory `tuning_mode` flag
- **Runtime Behaviour**: 
  - Enables runtime UI overlays for parameter tweaking.
  - Effects are applied instantly to preview.
- **Known Risks**: 
  - Overâ€‘exposure of sliders may allow invalid values that crash the effect compiler.

---

## 3. VR Quad Position/Scale/Alpha Controls
- **Purpose**: Adjust the 3D quad that displays the VR overlay (position, scale, opacity).
- **UI/Control Surface**: Sliders/inputs in MainWindow for X/Y/Z position, Scale, Alpha.
- **Files Involved**: 
  - `MainWindow.xaml` (UI elements)
  - `XRControlBlock` struct in `shared_memory.cpp`
  - `ReShadeControlService.cs` (writes to shared memory)
- **Data/Config Used**: 
  - Sharedâ€‘memory struct fields: `quad_position`, `quad_scale`, `quad_alpha`
- **Runtime Behaviour**: 
  - Values are read by the ReShade DLL to transform the overlay quad in real time.
- **Known Risks**: 
  - Rapid changes can cause visual â€œjumpingâ€ or echo artifacts.
  - Incorrect bounds may clip the overlay.

---

## 4. VR Quad Reset / Save Default
- **Purpose**: Reset the VR Quad to its saved baseline or save current configuration as the new default.
- **UI/Control Surface**: Buttons â€œResetâ€ and â€œSave Defaultâ€.
- **Files Involved**: 
  - `MainWindow.xaml` (button click handlers)
  - `ReShadeControlService.cs` (writes reset/save commands to shared memory)
  - `runtime.cpp` (applies saved baseline on next frame)
- **Data/Config Used**: 
  - Sharedâ€‘memory `quad_baseline` struct for saved values.
- **Runtime Behaviour**: 
  - Reset restores saved baseline; Save Default overwrites the stored baseline.
- **Known Risks**: 
  - Confusing interaction if saved baseline is not set before reset.

---

## 5. OpenXR/ReShade Menu Openâ€‘Close
- **Purpose**: Toggle the inâ€‘HMD ReShade menu (used for effect selection, tuning, etc.).
- **UI/Control Surface**: Menu button in MainWindow.
- **Files Involved**: 
  - `MainWindow.xaml` (button)
  - `ReShadeControlService.cs` (edgeâ€‘triggered command to DLL)
  - `openxr_overlay.hpp` (overlay struct definitions)
- **Data/Config Used**: 
  - Sharedâ€‘memory command flag `menu_toggle` (edgeâ€‘triggered)
- **Runtime Behaviour**: 
  - Toggles visibility of overlay; uses edgeâ€‘triggered semantics to avoid spam.
- **Known Risks**: 
  - Incorrect edge detection may cause flickering or missed toggles.

---

## 6. Visual Masks
- **Purpose**: Apply perâ€‘pixel masks to the VR overlay for artistic or safety purposes.
- **UI/Control Surface**: Mask selector dropdown.
- **Files Involved**: 
  - `MainWindow.xaml` (dropdown)
  - `runtime_gui.cpp` (loads mask texture)
  - `addon.cpp` (registers mask shaders)
- **Data/Config Used**: 
  - INI `MaskFile=path\to\mask.dds`
  - Sharedâ€‘memory `mask_id` index
- **Runtime Behaviour**: 
  - Mask texture is sampled during rendering; can be swapped at runtime.
- **Known Risks**: 
  - Invalid mask file path can cause rendering fallback or crash.

---

## 7. Sharedâ€‘Memory Remote Control
- **Purpose**: Enable ViewLab to send commands (e.g., mode switches, parameter updates) to the ReShade DLL.
- **Files Involved**: 
  - `shared_memory.cpp` (defines command struct)
  - `ReShadeControlService.cs` (writes commands)
  - `runtime.cpp` (interprets commands)
- **Data/Config Used**: 
  - Sharedâ€‘memory struct `control_commands` with fields like `command_id`, `payload`
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
  - Sharedâ€‘memory flag `sync_state`
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
  - `xr_mode` sharedâ€‘memory field
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
  - Sharedâ€‘memory `_techniques` array populated after `update_effects()`
- **Runtime Behaviour**: 
  - Loadingâ€‘bar fix allowed `update_effects()` during loading, populating `_techniques`.
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
  - MSI installs files to `Program Files\ViewLab` and registers sharedâ€‘memory components.
- **Known Risks**: 
  - Version mismatch between ViewLab and DLL can cause runtime errors.

---

# WANTED / REQUESTED FEATURES (TODO)

> ## Progress since the 4.1.16 notes (updated 2026-06-30 evening)
> - **4.1.17-4.1.25 built during the latest pass.** Latest built MSI is
>   `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.25.msi`.
> - **Product rename mostly done.** Window title, assembly product, shortcut script, and build output
>   now use `ViewLab`. Compatibility paths/manifests may still contain `XR ViewLab`/`XR_` names where
>   changing them would risk breaking installed configs or OpenXR layer registration.
> - **Per-game render-resolution % added/fixed.** Values above 100% now persist instead of being clipped.
> - **App list cleanup added.** Reversible "always hide" handling exists; keep an obvious unhide path.
> - **ReShade Remote button cleanup started.** The popout now has a proper dedicated window/file in the
>   UI project; continue to keep ReShade optional for users who only want cropping/masks.
> - **4.1.24 restores the interrupted visor editor.** `XRViewLab.UI\BeanMaskEditor.cs` now exists again,
>   WPF builds, x64/Win32 layers build, and the MSI packages.
> - **New mask geometry is wired but not headset-validated.** The UI writes opening, shape, top/bottom,
>   left/right, and curve values; the OpenXR layer reads them and attempts a sampled superellipse hidden
>   mesh with a rectangular fallback if runtime buffer capacity is too small.

> ## Immediate next goals (do these before more feature work)
> - **Test 4.1.25 in DiRT.** Verify the visor mask appears, responds to the new values after restart,
>   and toggles off cleanly.
> - **Tune the superellipse mesh.** If the runtime falls back due to mesh capacity or the shape feels
>   wrong in-headset, adjust segment count/ranges and rebuild.
> - **Keep refining layout.** The real visor editor is visible in Render Options; wide-layout right
>   column currently mirrors quick toggles rather than hosting a second interactive editor.

> ## Implemented 2026-06-30 (MSI 4.1.16, built â€” not yet installed/in-headset-verified)
> - **E.15 log spam â€” DONE.** Per-frame logging gated behind `verbose_logging` (default OFF) â†’ separate
>   `ViewLab.verbose.log`; human log keeps only meaningful events. See `..\PROJECTS.md` Â§5.
> - **A.1 CROPâ†”MASK split â€” DONE (layer + main-panel UI).** CROP = existing `total_render_height` /
>   `horizontal_render_width` (FOV + render-res; keep generous). MASK = new `mask_*` settings â†’ black
>   visor border via the OpenXR visibility mesh (`ApplyVisorMask` in `dllmain.cpp`). UI: "Crop V/H"
>   labels + "Mask (visor)" enable + Mask V/H sliders, persisted to global INI. All default-OFF/toggleable.
> - **A.3/A.4 bean â€” PARTIAL.** "Pill / bean" checkbox rounds the visor corners (capacity-adaptive,
>   falls back to rectangle). Still TODO: per-eye "don't mask inner eye", feathered edge, draggable
>   anchor-point visualizer, and a per-game (not just global) mask in the double-click ProfileWindow.
> - Mask currently lives in the **global** INI (applies to every game, toggleable). Per-game mask =
>   follow-up (extend `ProfileWindow` + `WriteAppCustomProfile`).


> Product rename: ship as **"ViewLab"** (drop "XR ViewLab") everywhere â€” window title, MSI/Product
> name, Start-menu shortcut, footer.

## End goal (vision)
A program anyone can download to: crop their VR view to an **extreme race-visor vibe** (corners/edges
black like a helmet) with smart logic (**don't mask the inner eyes**, preserve useful FOV); save
**per-game setups** (crop, mask shape, render-resolution %, ReShade Remote settings, ReShade effect
settings) that **JUST WORK** â€” pick a game, its config auto-applies; switch games, the other config
auto-applies, no manual fiddling. Also **install/uninstall ReShade per game** and **tune ReShade per
game** from inside ViewLab. Must work for **ANY VR game, ANY scenario, ANY PC** (32/64-bit, OpenVR,
OpenXR, OpenVR+OpenXR, Oculus). ViewLab must **save** PC overhead, never add it.

## A. Render / crop / mask (core visor system)
1. **Split CROP vs MASK into two independent sliders** (vertical + horizontal). Today one control both
   crops the render and bounds the visible area, causing **texture LOD pop-in** at low vtangent (~0.15)
   because the game still thinks the screen extends further down. Fix:
   - **Crop slider** = how much is actually *rendered* (keep generous, e.g. ~40% vtangent â†’ correct
     LOD/culling, no pop-in).
   - **Mask slider** = where the *visible* area ends (black beyond) â€” can be tighter for the visor look.
2. **Sliders for visual-mask settings** (not just checkboxes).
3. **Universal rounded visor mask** - shared mask settings plus backend-specific renderers. This is the
   long-term product requirement, not just an OpenXR visibility-mask trick.
   - Shared model: size/visible area, width, height, curve, X/Y offset, and later feather/opacity.
   - OpenXR backend: prefer a ViewLab-owned `xrEndFrame` composition-layer path; keep
     `xrGetVisibilityMaskKHR` editing only as a limited fallback.
   - OpenVR/SteamVR backend: investigate compositor overlay or submitted-frame overlay paths.
   - OpenComposite: route according to whether the app is effectively entering the OpenXR backend or
     still needs OpenVR handling.
   - Oculus backend: separate feasibility investigation.
   - UI must be capability-aware. If a backend cannot render the rounded visor mask yet, show
     "unsupported on this runtime path" instead of exposing controls that do nothing.
   - The mask is separate from crop. Crop decides how much the game renders; mask decides what the user
     sees. Disabling mask must restore the original crop/stencil behavior without needing a rebuild.
   - Do not present an organic/kidney bean editor until the runtime output can genuinely reproduce it
     through the shared model on supported backends.4. **Per-game FOV stencil** replicating VD's "Use FOV Stencil" (which only works for Assetto Corsa via
   its CSP). Leave VD's stencil OFF; do it per-game in ViewLab via #3.

## B. Per-game "just works"
5. **Per-game render-resolution %** in the tuning dialog (e.g. iRacing ~117%, DiRT ~137%); set VD
   "godlike" + 100% VDXR globally, multiplier per game. Saves manual VDXR-res changes between games.
6. Each game's full config (crop, mask/bean, render-res %, ReShade Remote, ReShade) **auto-applies** on launch.

## C. ReShade integration
7. **Per-game Install/Uninstall ReShade toggle** (one button, toggles state) in the per-app
   (double-click) dialog â€” installs *official* ReShade as a fallback when the custom AIO path fails,
   and uninstalls. (User also said "hide this install reshade button in the submenu" â€” CLARIFY: likely
   remove a stray/old global Install-ReShade button and keep only the per-app toggle.)
8. **Tune ReShade per game** from ViewLab (ReShade Remote already does live control).

## D. App table / dialog UX
9. **Rename apps** in the double-click dialog (editable display name, persisted).
10. **Resizable table columns** (drag borders â€” make columns `CanUserResize=true`; name column is
    currently locked).
11. **"Always hide"** an app from the list â€” **reversible** (provide an unhide path).
12. **Remove the "ReShade MENU â€” OpenVR" section** â€” one single "ReShade Menu" section (drop the OpenVR
    "Coming soon" card in both layout copies).
13. **ReShade Remote button toggles** â€” re-clicking "ReShade Remote â–¾" should CLOSE the popout (today
    only its âœ• closes it).

## E. Packaging / cleanliness
14. **Clean up shipped files** â€” ReShade payload as lightweight + correct as possible (no stray/legacy files).
15. **FIX LOG SPAM (HIGH PRIORITY).** The layer logs `xrLocateViews` (and likely other per-frame hooks)
    EVERY FRAME â†’ unreadable log + overhead (defeats ViewLab's purpose). Gate ALL per-frame logging.
    Human log must stay readable; an optional verbose/debug log (off by default, NOT shown in the in-UI
    Log button or its live updates) is OK only if necessary.

## Known bugs
- **Pinball ReShade GPU TDR** (BGRA/format-91 VR path) â€” see `..\PROJECTS.md` Â§4.
- 32-bit specifics (WOW6432Node reg, `.def` undecorated export, no-BOM manifest) â€” `..\PROJECTS.md` Â§2.
- **ReShade-side (NOT ViewLab layer), seen in DiRT** â€” in-HMD VR quad menu: header tabs unclickable;
  VR runtime compiles all ~473 effects instead of only enabled (desktop Gameplay loads none / Tuning
  loads only enabled â€” VR runtime not synced); menu very laggy. Details + fix notes:
  `..\ReshadeAIO\memory\status-and-next.md`. (DiRT Alt+F4 crash-reporter popup = normal.)
- **Assetto Corsa won't launch** (pre-existing, suspected old OpenComposite/ReShade-payload change) â€”
  deprioritized; see `..\ReshadeAIO\memory\status-and-next.md`.

## Dev cycle
Make every feature work per game, in the user's preference order: **iRacing â†’ Assetto Corsa â†’ DiRT
Rally â†’ Golf It â†’ Half-Life: Alyx â†’ Pinball FX2 â†’ Pistol Whip â†’ Tetris Effect â†’ Ghost Town â†’ â€¦** until
ViewLab works for ANY game on ANY PC. Near-term: per-game **FOV stencil/crop working for DiRT** for an
afternoon test.

*End of FEATURES.md.*

