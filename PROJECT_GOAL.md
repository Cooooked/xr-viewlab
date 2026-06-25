# XR ViewLab Project Journal

This is the active source tree. Use `F:\ViewLab` for all future work.

Do not reference or depend on files outside `F:\ViewLab`.

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

Known-good UI target is documented by the 4.0.0 control list in this journal and `AGENTS.md`.

If a binary reference or ReShade payload is needed again, copy it into `F:\ViewLab` first and document it here.

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
  - Removed compiled reference binaries from the active source tree.
  - Bumped version to `4.0.60`.
  - Built `F:\ViewLab\dist\XR-ViewLab-4.0.60.msi`.

### 2026-06-22 Correction

- Current installer output belongs under `F:\ViewLab\dist`.
- Updated `build.ps1`, `BUILD.md`, and `AGENTS.md` to point to `F:\ViewLab\dist`.

### 2026-06-22 External Reference Cleanup

- Removed old project/output/reference folder dependencies from docs/source.
- ReShade install source is now expected at `ReShadePayload` beside the running app, inside the installed/project folder.
- Removed stale hardcoded registry cleanup paths for old project names from the installer.
- Removed old vertical-tangent cleanup references from installer scripts.

### 2026-06-22 Current Task: 4.0.3 ReShade UI/Install Pass

- Done: made `ReShade Available!` tag RGB/glowing, not static green.
- Done: made `Install ReShade` disabled/grey until a ReShade-ready game is selected, then red/enabled.
- Done: removed ReShade availability from iRacing for now; only Assetto Corsa is tagged.
- Done: bundled Assetto ReShade payload inside `F:\ViewLab\ReShadePayload`.
- Done: install button copies the bundled payload recursively to the selected Assetto game directory and backs up overwritten files once with `.viewlab.bak`.
- Done: rebuilt as `F:\ViewLab\dist\XR-ViewLab-4.0.3.msi`.
- Done: launched the fresh 4.0.3 EXE for UI review.
- Fixed `build.ps1` MSBuild discovery to use Visual Studio `vswhere` first, with local fallback paths only for tool discovery.
- Note: `ReShadePayload` is the one intentional binary-payload exception so the installer/app can install ReShade without depending on files outside `F:\ViewLab`.

## Rules For Future Agents

- Update this file before and after meaningful code changes.
- Keep build outputs out of source control.
- Do not add registry-patching ReShade experiments unless the user explicitly asks for that feature again.
- Do not hijack double-click in the Applications table for install actions; double-click edits app profiles.
- ReShade install belongs on the explicit `Install ReShade` button.
