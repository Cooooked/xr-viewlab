# XR ViewLab Project Journal

This is the active source tree. Use `F:\ViewLab` for all future work.

Do not continue from old recovered folders under `F:\VR Tools` unless this file explicitly says to.

## Current Goal

Rebuild XR ViewLab from the clean 4.0.0 UI target:

- One global OpenXR render tuner.
- One merged Applications table.
- ReShade availability shown as a tag inside the Applications table.
- Double-clicking an app edits that app's custom render profile.
- A separate `Install ReShade` button stays disabled unless the selected app is ReShade-ready.
- ReShade menu controls are shown as a separate `ReShade MENU` section:
  - `In-HMD ReShade Menu`
  - `Desktop Duplicate`
  - `3D ReShade Menu`

## Source Of Truth

Known-good UI reference, kept outside the source tree so Git stays clean:

- `F:\ViewLab_REFERENCE\known-good-4.0.0-ui\xr-viewlab.exe`
- `F:\ViewLab_REFERENCE\known-good-4.0.0-ui\xr-viewlab.ini`

Known-good ReShade reference payload:

- `F:\ViewLab_REFERENCE\known-good-4.0.0-reshade`

## Latest Work Log

### 2026-06-22

- Split active work into `F:\ViewLab`.
- Removed old generated/build folders from the active source copy.
- Removed the unfinished `ReShadeSyncService` backend from the clean tree.
- Restoring the 4.0.0-style ReShade menu UI and install button behaviour.
- Completed in current edit:
  - Added `ReShade MENU` checkboxes back to the WPF UI.
  - Added an explicit `Install ReShade` button that is disabled unless the selected app is ReShade-ready.
  - Kept Applications table double-click for custom render profile editing.
  - Added handoff docs requested by Claude: `AGENTS.md`, `build.ps1`, and `dllmain_features.md`.
  - Moved compiled reference binaries out of source to `F:\ViewLab_REFERENCE`.
  - Bumped version to `4.0.2`.
  - Built `F:\ViewLab\dist\XR-ViewLab-4.0.2.msi`.

### 2026-06-22 Correction

- Stop using `F:\VR Tools\dist` for current builds.
- Current installer output belongs under `F:\ViewLab\dist`.
- Updated `build.ps1`, `BUILD.md`, and `AGENTS.md` to point to `F:\ViewLab\dist`.

## Rules For Future Agents

- Update this file before and after meaningful code changes.
- Keep build outputs out of source control.
- Do not add registry-patching ReShade experiments unless the user explicitly asks for that feature again.
- Do not hijack double-click in the Applications table for install actions; double-click edits app profiles.
- ReShade install belongs on the explicit `Install ReShade` button.
