# XR ViewLab Agent Handoff

Tone note from the user: dry, direct, no fluff. Good engineering, fewer ceremonies.

## One Folder, One Truth

Active source directory:

`F:\ViewLab`

Reference binaries are outside the repo:

`F:\ViewLab_REFERENCE`

Do not continue from `_publish_*`, `_extracted_*`, `_review_*`, or recovered folders under `F:\VR Tools`.

## Current State

- Render height/width OpenXR layer: done, active in `dllmain.cpp`.
- Per-app enable/disable: done, DLL reads HKCU app registry keys.
- Per-app custom render values: done, WPF writes HKCU app registry keys.
- Merged Applications table: done.
- ReShade Available tag: done in WPF based on known app names/paths.
- Double-click app profile editor: done; double-click edits custom values.
- Install ReShade button: done; explicit button, disabled unless selected app has ReShade tag.
- ReShade MENU UI: done; WPF writes INI toggles listed below.
- ReShade runtime-sync backend experiments: removed from clean source.
- `reshade-vr-menu.exe`: removed from clean source and explicitly removed by installer if found from older builds.

## File Map

- `MainWindow.xaml` - WPF dark UI layout, merged app table, ReShade MENU, footer.
- `XRViewLab.UI/MainWindow.cs` - WPF app logic, config read/write, app registry UI, update check, ReShade install button.
- `XRViewLab.UI/AppProfile.cs` - app row model for Applications table.
- `XRViewLab.UI/ProfileWindow.cs` - per-app custom top/bottom/horizontal editor.
- `XRViewLab.UI/UpdateRelease.cs` - update-check DTO.
- `dllmain.cpp` - OpenXR API layer hook implementation.
- `loader_interfaces.h` - OpenXR loader/API-layer structs.
- `XR_APILAYER_cooooked_xrviewlab.json` - implicit OpenXR layer manifest.
- `xr-viewlab.ini` - default config copied by installer / fallback config.
- `xr-viewlab.csproj` - WPF .NET project.
- `XRViewLabLayer.vcxproj` - native OpenXR layer project.
- `Installer/Product.wxs` - WiX MSI definition.
- `Installer/CreateDesktopShortcut.vbs` - installer prompt helpers.
- `Installer/PreserveConfig.vbs` - installer config/registry cleanup helpers.
- `app.ico` - app/installer icon.
- `PROJECT_GOAL.md` - running project journal; update before/after meaningful work.
- `dllmain_features.md` - DLL INI/registry key reference.
- `build.ps1` - single build entry point.

## ReShade MENU UI

The WPF app writes these `[Settings]` keys in `xr-viewlab.ini`:

- `reshade_hmd_menu=1|0` for `In-HMD ReShade Menu`.
- `reshade_desktop_duplicate=1|0` for `Desktop Duplicate`.
- `reshade_3d_menu=1|0` for `3D ReShade Menu`.

Current clean source only persists these options. Any actual ReShade binary/menu integration must consume these keys deliberately; do not revive the old registry-patching `ReShadeSyncService`.

## Last Change

Split the project into `F:\ViewLab`, removed the unfinished ReShade sync backend, restored the planned 4.0.0-style ReShade MENU controls, added the explicit `Install ReShade` button path, bumped to 4.0.2, and built the MSI.

## Known Gotchas

- The exact build command that produced the extracted 4.0.0 binary is not recoverable. Use `build.ps1` from now on.
- Current verified MSI: `F:\ViewLab\dist\XR-ViewLab-4.0.2.msi`.
- `F:\ViewLab_REFERENCE` contains compiled reference binaries; source repo should not.
- The WPF app and DLL both use `xr-viewlab.ini`, but only the WPF app knows about the ReShade MENU keys right now.
- The DLL reads render/app keys only; see `dllmain_features.md`.
- Do not edit `ReShadeApps.ini` or stock ReShade global state from ViewLab unless the user explicitly reopens that feature.

## Build

Run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
F:\ViewLab\build.ps1
```

The MSI is copied to:

`F:\ViewLab\dist`
