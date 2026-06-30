# XR ViewLab Agent Handoff

Tone note from the user: dry, direct, no fluff. Good engineering, fewer ceremonies.

## One Folder, One Truth

Active source directory:

`F:\AI-Projects\ViewLab`

Do not reference or depend on files outside `F:\AI-Projects\ViewLab`.

## Current State (2026-07-01, v4.1.42)

For the live, detailed state always read `PROJECT_STATUS.md` and `HANDOFF.md` first — they supersede
this section. Summary:

- Render crop OpenXR layer (FOV + recommended resolution): done, active in `dllmain.cpp`.
- Per-app enable/disable + custom render values: done (WPF writes / DLL reads HKCU app registry keys).
- Merged Applications table, double-click app profile editor, ReShade Remote popout: done.
- Visor mask (in progress, NOT confirmed in-headset): native D3D11 direct-write of a kidney/superellipse
  border into the game's eye textures at `xrReleaseSwapchainImage`. Visibility-mask path is optional
  (`visibility_mask_visor`). A debug head-locked blue test quad (`test_quad`) probes VR layer submission.
- Key open bug: the visor "enable" toggle is not persisting — `mask_enabled=0` everywhere despite the UI;
  the D3D11 draw is gated on it, so nothing draws. See `PROJECT_STATUS.md`.

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
- `ReShadePayload/` - bundled ReShade files installed by the WPF app for Assetto Corsa.
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

v4.1.42: added the debug head-locked blue test quad (`test_quad`) to prove OpenXR layer submission in
Pistol Whip/DiRT. Preceded by the visor-mask redesign (4.1.39–4.1.41): native D3D11 direct-write at
`xrReleaseSwapchainImage`, gate removed, visibility-mask made optional. See `CHANGELOG.md`.

## Source Backup Reference

See `SOURCE_BACKUP.md` for the complete inventory of all backup sources, build versions, ReShade mod source backups, and rebuild instructions.

## Known Gotchas

- `build.ps1` auto-increments the version. When building components manually (to avoid a double bump),
  bump `Properties\AssemblyInfo.cs` + `Installer\Product.wxs` yourself and build each project directly.
- Latest installer: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.42.msi` (builds clean; visor not yet
  confirmed in-headset).
- Source must not depend on files outside `F:\AI-Projects\ViewLab`.
- The WPF app and DLL both use `xr-viewlab.ini`, but only the WPF app knows about the ReShade MENU keys right now.
- The DLL reads render/app keys only; see `dllmain_features.md`.

## Build

Run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\build.ps1
```

The MSI is copied to:

`F:\ViewLab\dist`
