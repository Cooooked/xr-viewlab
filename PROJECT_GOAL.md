# XR ViewLab Project Journal

This is the active source tree. Use `F:\AI-Projects\ViewLab` for all future work.

Do not reference or depend on files outside `F:\AI-Projects\ViewLab`.

> Live state lives in `PROJECT_STATUS.md` and `HANDOFF.md`; this journal is historical context.

## Standing User Delivery Rule

After any implementation change, build the full installer with `.\build.ps1` unless explicitly told not to.
Final replies must include the exact runnable MSI path, including file name, in a plain text block suitable
for Windows Run. Do not give only the folder path.

Current latest built installer:

```text
F:\AI-Projects\ViewLab\dist\ViewLab-4.1.64.msi
```

## Current 4.1.64 Work Log

- Completed the Apex Y / Inner low pass: main UI, per-app profile popup, draggable preview pins,
  INI/registry persistence, and Direct C native geometry are wired to the same values.
- Made Direct C shape mode match the preview mode: closed bean for normal stenciling, open-inner single
  eye when `Stencil outer edges only` is enabled.
- Extended `edge_smear_fix` with exact submitted-FOV matching, invalid imageRect bounds correction,
  4-pixel recommended-size alignment, and FOV-weighted recommended-size scaling when original runtime FOV
  is known.
- Added original runtime FOV to verbose edge-smear diagnostics.
- Cleaned ReShade Remote corrupted glyph labels and kept the Advanced popup status-driven.
- Added `Tests/Verify-ViewLabContracts.ps1` and `AUDIT_2026-07-08.md`.
- Verification passed: WPF Release build, x64 native Release build, Win32 native Release build, contract
  script, and full `build.ps1`.
- Full build produced `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.64.msi`.

## Current 4.1.63 Work Log

- Removed the stale `Technique C direct write` subtext under the `Visor mask` checkbox.
- Added experimental Direct C visor shape controls: `Apex Y` / `mask_outer_apex_y` and `Inner low` /
  `mask_inner_lower_y`.
- Added draggable preview pins for both controls and wired them through global UI, per-app profiles,
  config/registry persistence, native Direct C geometry, and verbose diagnostics.
- Expanded Edge Masks hover text to say those modes replace render cropping with masking on that axis,
  reducing or removing GPU pixel savings.
- Full build passed and produced `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.63.msi`.
- Immediate handoff focus: edge-smear/render-stretch remains unresolved at aggressive horizontal/vertical
  crops and should be investigated as a rendering/composition contract problem, not hidden with guard
  bands or black strips.

## Current 4.1.62 Work Log

- Applied Claude's Technique C review fixes: release-time Direct C draw now marks the swapchain as
  covered, and late `xrEndFrame` drawing is fallback-only.
- Reused cached RTVs in the late Direct C fallback path.
- Expanded verbose edge-smear contract diagnostics to log baseline data with `edge_smear_fix=0`, plus
  release/late draw provenance and render scale.
- Fixed per-app visor stale state: reset deletes `visor_size`, `visor_width`, and `visor_height`; saving
  with `Use global visor settings` checked writes `visor_size=0`.

## Current 4.1.61 Work Log

- Temporarily hid/bypassed visor Techniques A/B and forced Direct C as the only user-facing visor path.
- Updated the visor mask preview: normal mode shows the closed bean; `Stencil outer edges only` shows the
  single-eye open-inner shape that matches the Direct C left-eye model.
- Hid stale per-app visor X/Y offset controls along with width/height. Size and Curve remain the product
  controls.
- Bundled the custom ReShade OpenXR payload copied from the newer AIO/patched ReShade work:
  `ReShade64.dll`, `ReShade64_XR.json`, and compact reference docs under `ReShadePayload`.
- Reworked ReShade Remote as `Advanced: ReShade Remote`, with explicit status, a component install
  action, and disabled controls until the heartbeat is live.

## Current 4.1.60 Work Log

- Refocused the visor mask UI around Technique C: renamed the checkbox to `Visor mask`, removed the
  redundant `Off` radio, and made stale `visor_technique=off` fall back to Direct C.
- Removed visible manual visor width/height controls from global and per-app UI. Runtime ignores stale
  width/height values and uses neutral scale so the visor follows the crop rectangle plus Size/Curve.
- Added verbose-only edge-smear contract diagnostics covering FOV, imageRect, swapchain size, recommended
  size, crop values, visibility-mask state, and visor state.
- ReShade Remote now tells the user whether the required custom shared-memory component is present by
  heartbeat, and warns when no bundled payload exists.
- Full build passed and produced `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.60.msi`.

## 4.1.59 Work Log

- Corrected Edge Masks wiring: only the "Both sides" controls drive the real DLL `visual_mask_only` and
  `horizontal_visual_mask_only` keys. One-sided controls are disabled and old one-sided keys are cleared.
- Full build passed and produced `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.59.msi`.

## 4.1.58 Work Log

- Full settings/logging/hot-path scan found and fixed more UI/DLL key mismatches:
  `crop_outer_edges_only`, `visual_mask_only`, and `horizontal_visual_mask_only` are now native-layer
  inputs, with legacy popup keys retained as fallbacks.
- Edge-smear FOV comparison logs are now verbose-only and `xrEndFrame` metadata patching uses fixed stack
  buffers instead of per-frame heap vectors.
- Aligned visor defaults (`mask_size=0.82`, `mask_corner=0.5`) across UI, DLL, and default INI.
- Full build passed and produced `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.58.msi`.

## 4.1.57 Work Log

- Replaced the non-working edge-smear pixel guard-band path with an `xrEndFrame` projection-FOV patch:
  `edge_smear_fix=1` now clamps submitted projection-layer FOV to the cropped `xrLocateViews` FOV and
  logs locate/before/after FOV values at most once per second.
- Found the mask-on/mask-off difference: native visor paths skipped the legacy outer-edge-only visibility
  mask filter, while mask-off still applied it. `edge_smear_fix=1` now preserves the runtime visibility
  mask in both paths, and the UI writes the DLL's real `outer_edge_visibility_mask_only` key.
- Full build passed and produced `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.57.msi`.

## 4.1.55 Work Log

- Optimized release-time D3D11 visor/edge drawing by caching runtime swapchain render-target views per
  tracked image/slice and reusing them during release-time direct/edge draws.
- Removed heap allocations from the high-segment visor geometry builders by replacing fixed-size curve
  vectors with stack arrays.
- Preserved vertical/horizontal tangent crop and all A/B/C visor paths.
- Full build passed and produced `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.55.msi`.

## 4.1.54 Work Log

- Preserved vertical/horizontal tangent crop and disabled the unsafe LOD full-FOV experiment that stretched
  the image over the lens. `lod_popin_fix` is diagnostic-only in this build.
- Replaced the edge-smear metadata inset experiment with actual black guard-band drawing into projection
  texture edges.
- Reworked visor A/B/C wiring: A is a real BOTH-eye OpenXR quad with open-inner left/right artwork; B only
  intercepts D3D11 colour non-MSAA swapchains; C draws at release and late `xrEndFrame` for
  OpenComposite/DiRT timing.
- Fixed main-panel visor controls: Size/Width/Height/Curve are visible and persisted; X/Y sliders are
  removed and stale X/Y biases are ignored by the DLL.
- Full build passed and produced `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.54.msi`.

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

### 2026-07-01 late (v4.1.43 → 4.1.47, visor-mask breakthrough)

- Confirmed layer submission works in-headset (BOTH-eye debug quad drew on VDXR/Pistol Whip).
- Found the real "no mask" root cause: `visorSize`=1.0 (opening = full view = zero-width border). Fixed
  the DLL fallback to 0.82 (4.1.46).
- Enable made global-only (4.1.43); UI Technique selector added (4.1.45); technique A implemented (4.1.44)
  and reworked to a single BOTH-eye FOV-sized quad (4.1.47); technique C given a Flush (unreliable on VDXR).
- Removed the debug test quad (4.1.47).
- Pending: confirm A in-headset; then experimental edge-smear + LOD-pop-in toggles (deferred, gate off).

### 2026-07-01 (v4.1.39 → 4.1.42, visor-mask debugging)

- Replaced the projection-layer "orb" visor with a native D3D11 direct-write into the game's eye textures.
- 4.1.41: removed the gate that disabled D3D11 when the game used `xrGetVisibilityMaskKHR`; moved the draw
  to `xrReleaseSwapchainImage` (lifecycle-correct); made the visibility-mask reshape optional.
- 4.1.40: fixed main-window blank gap (LeftColumnPanel) and App Profile popup clipping (ScrollViewer);
  added typeless-safe RTV; added one-shot DIAG logging.
- 4.1.42: added debug head-locked blue test quad (`test_quad`) to prove VR layer submission.
- Read `ViewLab.log` + registry: proved `mask_enabled=0` everywhere → the visor was never enabled, so the
  draw never ran. Top open bug: the enable toggle isn't persisting to the config the DLL reads.
- Visor mask still NOT confirmed in-headset. Awaiting blue-quad test result.

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

### 2026-06-25 — OpenXR ReShade shared memory controls + fixes

- Added `ReShadeControlService.cs` — P/Invoke shared memory wrapper for `Local\ReShadeXRControl` (`XRControlBlock`, 80 bytes).
- Added OpenXR ReShade cards in both columns (left + right) with:
  - **GAMEPLAY MODE** toggle — writes `xr_mode` (0=gameplay/dormant, 1=tuning/active). DLL gates `update_effects()` + desktop hotkeys.
  - **Desktop VR Menu** toggle — writes `menu_visible` (maps to `win_reserved` in old DLL, no-op there; new DLL uses it to gate overlay + ShowWindow on preview). Direct `FindWindowW`/`ShowWindow` toggles the "ReShade VR Overlay" preview window.
  - **Headless** / **Always on top** checkboxes — write `win_headless`, `win_always_on_top`.
  - **Reposition** / **Transform** buttons — write `quad_edit_mode` (1=blue border drag, 2=orange border transform).
- Fixed heartbeat staleness bug: old DLL never increments `quad_reserved` (= heartbeat field), so stale check disconnected every 5s. Removed staleness entirely — poll timer is now flat: `if (!connected) try connect else sync UI`.
- Removed status text ("mini log") from both OpenXR cards.
- Removed unused `BlockUpdated` event from `ReShadeControlService`.
- Added `FindWindowW`/`ShowWindow` P/Invoke to `MainWindow.cs` for preview window control.
- Struct layout uses backward-compatible offsets (`win_reserved`→`menu_visible`, `quad_reserved`→`heartbeat`) — 80 bytes, works with old build2 DLL.
- Bumped to 4.1.6. MSI at `F:\ViewLab\dist\XR-ViewLab-4.1.6.msi`.

### 2026-06-25 — v4.1.7: Cleanup stale files, installer fix, GitHub release

- Removed stale `OpenVRBridge/`, `ReShadePayload/` binaries, and `ReshadeAI/` agent files from git.
- Fixed installer `Product.wxs` to not reference deleted `ReShadePayload/` files.
- Bumped version to 4.1.7 in `AssemblyInfo.cs` and `Product.wxs`.
- Tagged and pushed `v4.1.7` to GitHub. Release created with MSI asset.
- Created `SOURCE_BACKUP.md` consolidating all backup sources, ReShade mod backups, build versions, and rebuild instructions.

## Rules For Future Agents

- Update this file before and after meaningful code changes.
- Keep build outputs out of source control.
- Do not add registry-patching ReShade experiments unless the user explicitly asks for that feature again.
- Do not hijack double-click in the Applications table for install actions; double-click edits app profiles.
- ReShade install belongs on the explicit `Install ReShade` button.
