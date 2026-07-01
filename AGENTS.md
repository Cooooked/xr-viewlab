# XR ViewLab Agent Handoff

Tone note from the user: dry, direct, no fluff. Good engineering, fewer ceremonies.

## One Folder, One Truth

Active source directory:

`F:\AI-Projects\ViewLab`

Do not reference or depend on files outside `F:\AI-Projects\ViewLab`.

## Current State (2026-07-02, v4.1.55)

- Latest installer: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.55.msi`.
- After implementation changes, always run `.\build.ps1` and put the exact MSI path in a plain text block
  in the final reply.
- Vertical/horizontal crop is the core performance feature and must remain active unless the user
  explicitly asks otherwise.
- Visor A/B/C are real selectable paths. A = BOTH-eye OpenXR quad overlay with open-inner left/right
  artwork; B = D3D11 colour non-MSAA swapchain interception; C = release-time plus late-end-frame direct
  write.
- `lod_popin_fix` is diagnostic-only in 4.1.55. Do not restore the unsafe full-FOV path that stretched
  cropped output over the full lens.
- Release-time D3D11 visor/edge drawing caches runtime swapchain RTVs per image/slice, and visor geometry
  builders avoid per-draw heap curve buffers.

## Current State (2026-07-01 late, v4.1.47)

For the live, detailed state always read `PROJECT_STATUS.md` and `HANDOFF.md` first — they supersede
this section. Summary:

- Render crop OpenXR layer (FOV + recommended resolution): done, active in `dllmain.cpp`.
- Per-app enable/disable + custom render values: done (WPF writes / DLL reads HKCU app registry keys).
- Merged Applications table, double-click app profile editor, ReShade Remote popout: done.
- Layer submission CONFIRMED in-headset (BOTH-eye debug quad drew on VDXR). Prior "no mask" was
  `visorSize`=1.0 (full opening); fixed (fallback 0.82).
- Visor: enable is global-only; `visor_technique` selector (UI radio). Technique A = per-eye
  head-locked alpha-cutout quads; technique B = app-facing swapchain interception/composite; technique C =
  eye-texture direct write at release. All three use the same open-inner-eye visor shape. Debug test quad removed.
- Pending (user-requested, deferred): experimental edge-smear toggle + LOD-pop-in toggle. See
  `PROJECT_STATUS.md` / `IMPLEMENTATION_PLAN_visor.md`.

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

v4.1.47: layer submission confirmed working in-headset (BOTH-eye debug quad drew). Found + fixed the
"no mask" root cause — `visorSize`=1.0 (full opening = invisible border), fallback now 0.82. Technique A
(BOTH-eye quad overlay) is primary and awaits in-headset confirmation; technique C draws but doesn't show
on VDXR; debug test quad removed; enable is global-only; UI Technique selector added. See `CHANGELOG.md`
and `PROJECT_STATUS.md`.

## Source Backup Reference

See `SOURCE_BACKUP.md` for the complete inventory of all backup sources, build versions, ReShade mod source backups, and rebuild instructions.

## Known Gotchas

- `build.ps1` auto-increments the version. When building components manually (to avoid a double bump),
  bump `Properties\AssemblyInfo.cs` + `Installer\Product.wxs` yourself and build each project directly.
- After implementing any requested code/UI/DLL change, run the full installer build with `.\build.ps1`
  unless the user explicitly says not to. In the final reply, always give the full runnable MSI path,
  including the file name, in a plain text block the user can paste into Windows Run. Example:
  `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.55.msi`. Do not provide only the folder.
- Latest installer: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.55.msi` (builds clean).
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

`F:\AI-Projects\ViewLab\dist`
