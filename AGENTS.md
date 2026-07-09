# XR ViewLab Agent Handoff

Tone note from the user: dry, direct, no fluff. Good engineering, fewer ceremonies.

## One Folder, One Truth

Active source directory:

`F:\AI-Projects\ViewLab`

Do not reference or depend on files outside `F:\AI-Projects\ViewLab`.

## Current State (2026-07-08, v4.1.65)

- Latest installer: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.65.msi`.
- 4.1.65 was a bug/perf sweep: fixed the UI-side `mask_size`=1.0 clobber (regression of the 4.1.46
  invisible-mask root cause), retargeted the false visibility-mask warning to the legacy
  `visibility_mask_visor` path, removed dead legacy config reads and fossil `reshade_*` keys, and
  removed per-frame heap allocations/duplicate mutex passes from the Direct C release/end-frame
  paths. `mask_size` is the single source of truth for the visor opening; never derive it from the
  legacy `mask_vertical`/`mask_horizontal` values.
- After implementation changes, always run `.\build.ps1` and put the exact MSI path in a plain text block
  in the final reply.
- Vertical/horizontal crop is the core performance feature and must remain active unless the user
  explicitly asks otherwise.
- Visor A/B source paths remain in code but are hidden/bypassed in the product. Current debugging focuses
  on Technique C Direct: release-time plus late-end-frame direct write.
- Technique C late `xrEndFrame` drawing is fallback-only; the release-time draw is the normal/legal path.
- `lod_popin_fix` is diagnostic-only in 4.1.63. Do not restore the unsafe full-FOV path that stretched
  cropped output over the full lens.
- Release-time D3D11 visor/edge drawing caches runtime swapchain RTVs per image/slice, and visor geometry
  builders avoid per-draw heap curve buffers.
- 4.1.63 added experimental Direct C visor shape controls: `Apex Y`/`mask_outer_apex_y` moves the outer
  curve apex up/down, and `Inner low`/`mask_inner_lower_y` adds a bottom-only inner-eye stencil curve.
  These are built, not yet confirmed in headset.
- Edge smear/render stretching is still unresolved. Treat current edge-smear fixes as suspicious and
  investigate the actual render/composition path before changing code.

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
- `ReShadePayload/` - bundled custom ReShade OpenXR payload and compact reference docs for Advanced:
  ReShade Remote.
- `PROJECT_GOAL.md` - running project journal; update before/after meaningful work.
- `dllmain_features.md` - DLL INI/registry key reference.
- `build.ps1` - single build entry point.

## ReShade MENU UI

The old `reshade_hmd_menu`/`reshade_desktop_duplicate`/`reshade_3d_menu` ini keys are gone: no
code read or wrote them, so they were removed from the default ini and the dead UI constants were
deleted. ReShade Remote state lives in the `Advanced: ReShade Remote` popout (shared-memory
control block + `openxr_quad_transform.ini` under ProgramData). Do not revive the old
registry-patching `ReShadeSyncService`.

## Last Change

v4.1.63: added experimental Direct C visor apex/inner-lower shape controls with draggable preview pins.
Build is clean, but in-headset behaviour is not confirmed. The next focus is edge-smear/render-stretch
forensics at aggressive crop values.

v4.1.62: visor product path is focused on Direct C. Techniques A/B are hidden/bypassed, late Direct C
draw is fallback-only, and verbose edge-smear contract diagnostics include baseline/draw provenance.

v4.1.61: visor product path is focused on Direct C. Techniques A/B are hidden/bypassed, the preview shows
the single-eye open-inner shape when `Stencil outer edges only` is enabled, and Advanced: ReShade Remote
bundles the custom OpenXR payload with an install action. See `CHANGELOG.md` and `PROJECT_STATUS.md`.

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
  `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.63.msi`. Do not provide only the folder.
- Latest installer: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.65.msi` (builds clean).
- Source must not depend on files outside `F:\AI-Projects\ViewLab`.
- The WPF app and DLL both use `xr-viewlab.ini`, but only the WPF app knows about the ReShade MENU keys right now.
- The DLL reads render/app keys only; see `dllmain_features.md`.

## CRITICAL: AGENT SAFETY RULES

**ABSOLUTELY FORBIDDEN WITHOUT EXPLICIT USER APPROVAL:**

- NEVER run `git restore` on any file
- NEVER run `git checkout` on any file
- NEVER run `git reset` of any kind
- NEVER run `git revert` of any kind
- NEVER run `git stash` of any kind
- NEVER delete or revert uncommitted changes
- NEVER assume Git HEAD is the source of truth
- NEVER compare against Git HEAD for "original" state

**MANDATORY BEFORE ANY DESTRUCTIVE OPERATION:**

1. Check `git status` - if there are uncommitted changes, STOP
2. Ask the user for explicit approval before any git operation that changes files
3. If asked to "restore" or "revert", clarify EXACTLY what state to restore to
4. If asked to "rollback", ask for the specific commit hash or backup name
5. Check `dist/` folder for recent MSIs - these may represent the real working state
6. Check `backups/` folder for patches or snapshots
7. Check git reflog for recent operations
8. Only use `git diff` to inspect changes, never to apply them automatically

**IF THE USER IS ANGRY OR REPORTS A REGRESSION:**

1. STOP ALL IMMEDIATE CHANGES
2. Ask for the specific build number that was working
3. Ask for the specific time/date when it was working
4. Do NOT assume any git state is correct
5. Do NOT restore to Git HEAD without confirmation
6. The real working state may be:
   - Uncommitted local changes (not in git)
   - A recent MSI in `dist/`
   - A patch in `backups/`
   - A zip snapshot in `backups/`

**WORKSPACE POLICY:**

- This project should be edited in a Git workspace/branch, not directly on master
- Always create a feature branch before making changes
- Never modify files on master without explicit approval
- If working directly on master, commit frequently after each successful build

## Build

Run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\build.ps1
```

The MSI is copied to:

`F:\AI-Projects\ViewLab\dist`
