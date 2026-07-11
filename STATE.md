# STATE — live project state

> Single source of truth for "where are we". Update this file in the same commit as any
> behavior change. Do not create handoff/status/session documents — this is the only one.

**Updated:** 2026-07-11
**Current version:** 4.1.123 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.123.msi`
**Last confirmed-good in headset:** 4.1.103 (stencil inner-eye fix confirmed by user)
**Publish state:** local commits ahead of origin/master; DO NOT push until user confirms current work in-headset.

## ReShade Remote (4.1.123)

- ReShade payload rebuilt from `ReshadeAI/reshade` source to fix the missing `heartbeat` signal that kept `ReShadeRemote` controls disabled.
- `ReShadeRemote` controls are no longer disabled when the game is not running; they behave like a remote that pre-configures settings before the game starts.
- `Install component` now waits for the installer to finish and reports a clear "in use" error if ReShade is locked by a running game.
- Pistol Whip compatibility with the custom payload is still pending headset confirmation.

## Context: the 2026-07-10 recovery

A 12-hour spiral of blind edits (versions 4.1.88–4.1.101, unreleased) broke and then lost the
visor shape features. The native layer was reset to the clean v4.1.55 baseline and the features
are being **rewritten** (not restored) in passes. Full post-mortem: `docs/REGRESSIONS.md`.
Three fixes from the recovery are load-bearing and must survive all future work:
FreeLibrary-after-blob-use, stencil key wiring + filter-runs-with-visor-active, partner-eye
boundary gated to closed-bean mode (details in `docs/DECISIONS.md` D7–D9).

## Current implementation status

4.1.122 removes the **visor mask size slider** entirely and hardcodes the mask to maximum
corner coverage. It also removes the experimental LOD pop-in fix, edge-smear fix, HD visor, and
anti-aliasing toggles and their code paths. `foveated_center_compensation`, `stencil_outer_edges_only`,
and `crop_outer_edges_only` are now permanently on; their UI checkboxes are removed and config
keys are ignored. The visor mask now only rounds the corners of the cropped view; the crop
values (vertical/horizontal render height) still control the full render dimensions from the
center. `mask_size`/`visor_size` is deprecated and no longer affects geometry. The size slider
has been removed from the main window and per-app profile editor, and the preview geometry uses
`WidthScale`/`HeightScale` fixed at 1.0 so the mask always extends to the edges of the crop.
Everything below is NOT headset-confirmed; last confirmed-good remains 4.1.103.

## Current headset blockers (2026-07-11)

- 4.1.112 proved that `crop_outer_edges_only` was read but not applied: the FOV hook always used
  outer-edge-only crop. Corrected in-tree; requires headset retest.
- The 4.1.112 "visor completely gone" report was most plausibly R9: the installer's per-launch
  settings reset wiped `mask_enabled` on every game start, so toggling the checkbox could never
  matter. The reset machinery is removed. NOTE: `mask_size=1` is a LEGAL, intended value (corners
  masked, maximum opening) — an earlier "recover 1 → 0.82" theory was wrong and its clamp is
  removed; only a MISSING key falls back to 0.82 (R4).
- ReShade Remote said 'component missing' because the single-file app extracts to TEMP: AppContext.BaseDirectory never pointed at the install dir (R11, fixed: Environment.ProcessPath). Separately, the registered ProgramData DLL was stock ReShade, not ViewLab's
  bundled control-capable payload. The install command and status verification are corrected
  in-tree; Pistol Whip compatibility with the custom payload remains unconfirmed.

In-tree implementation summary:
- Preview pins capture/release mouse, clear drag state on lost capture, and sync dragged values
  back to sliders before saving.
- Native visor AA uses per-vertex alpha feather strips; HD doubles curve tessellation. With AA OFF the visor draws through the blend-disabled opaque pipeline (the 4.1.103-proven path) to isolate the unverified alpha path.
- All six visor shape controls have per-app registry plumbing.
- Release-time visor drawing uses cached RTVs; late `xrEndFrame` drawing is fallback-only.
- MSI upgrades preserve the user's live visor, crop, render, and per-app profile settings;
  the packaged ini supplies safe defaults only for a fresh install.

## What works (as of 4.1.105, pending headset confirm where noted)

- Render crop (vertical total/split + horizontal, outer-edges-only option) — core feature, stable.
- Per-app enable + custom profiles (registry).
- Visor mask Technique C Direct (D3D11 draw into eye textures at `xrReleaseSwapchainImage`).
- "Stencil outer edges only": filters the runtime's FOV-stencil mesh to outer halves AND
  switches visor geometry bean/arch (confirmed in-headset at 4.1.103 for the filter part).
- Visor shape controls Size/Curve/Apex Y (+ Inner low/Bridge×4 pending headset confirm).
- ReShade Remote popout + bundled payload install.
- Update check, app list, responsive UI layouts.

## Bug scan findings - 2026-07-10

Severity key: P0 = release blocker, P1 = high, P2 = medium, P3 = docs/test/low-risk.

1. **P0 - UI missing-`mask_size` fallback can still recreate the invisible visor.**
   `Installer\PreserveConfig.vbs` strips `mask_size`, and existing local configs are not replaced
   with the bundled default. `XRViewLab.UI\MainWindow.cs` still loads a missing `mask_size` with
   `OpeningFromMask(...)`; with current default crop values this clamps to `1.0`, and
   `SaveGlobalSettings()` can write `mask_size=1.0`. This is R4 again unless fixed.
   Evidence: `MainWindow.cs` `OpeningFromMask` / `LoadSettings` / `SaveGlobalSettings`, plus
   `PreserveConfig.vbs` reset list.

2. **P0 - The default `xr-viewlab.ini` is not packaged into the current MSI path.**
   `Installer\Installer.wixproj` compiles only `Product.wxs`; `Product.wxs` installs the app exe
   and icon but no `xr-viewlab.ini`. The published folder currently has no `xr-viewlab.ini`.
   `Installer\HarvestedFiles.wxs` references one, but that harvest file is not included in the
   WiX project. Fresh installs therefore depend on code fallbacks, and upgrades keep stripped
   local configs without a default-file repair path.

3. **P1 - Per-machine installer custom actions may touch the wrong user's config/profile.**
   The MSI is per-machine, but immediate VBScript actions read/write `%LOCALAPPDATA%` and HKCU.
   In elevated installs, backup/reset may target the elevated account rather than the actual
   ViewLab user. The final fix removes upgrade-time user-setting reset work entirely.

4. **P1 - Edge Masks popup writes keys the DLL never reads.**
   The UI loads/saves `horizontal_visual_mask_both`, `horizontal_outer_eye_mask`,
   `horizontal_inner_eye_mask`, `vertical_visual_mask_both`, `vertical_top_mask_only`, and
   `vertical_bottom_mask_only`. The DLL reads only `visual_mask_only` and
   `horizontal_visual_mask_only`, so the popup controls appear to have no native effect.

5. **P1 - `crop_outer_edges_only` is saved by the UI but ignored by the DLL.**
   The main UI persists `crop_outer_edges_only`, and docs describe it as a crop mode, but
   `LoadConfig()` does not read the key. The native log hardcodes `horizontal_outer_edges_only=1`
   and `EffectiveOuterEdgeHorizontalScale()` always applies the outer-edge-only model.

6. **P1 - Apex pin drag math is not the inverse of the rendered pin position.**
   `BeanMaskEditor.PinPositions` renders the apex pin at `centerY + OuterApexY * span`, but
   dragging writes `OuterApexY = (mouse.Y - pins.y0) / span`. A default centered pin drag maps to
   `0.5`, negative apex values are effectively unreachable by drag, and the pin can appear to
   jump or clamp instead of tracking the cursor.

7. **P1 - Profile "Use global visor settings" checkbox does not save as global.**
   `ProfileWindow.UseGlobal` is only set by the `Use Global Values` button. If the checkbox is
   checked and the user presses Save, `MainWindow` sees `UseGlobal == false` and writes a custom
   profile. The editor also remains interactive while the sliders are disabled, so dragging the
   preview can create custom values while the UI claims it is using global visor settings.

8. **P1 - One global release-draw flag can suppress a required late fallback.**
   `g_releaseDrewThisFrame` is set by edge guard, Technique B, or Direct C release drawing, then
   `xrEndFrame` skips all late drawing when the flag is true. If edge guard draws but Direct C
   has no usable eye layout yet, or one swapchain draws while another did not, the late visor
   fallback is skipped even though the visor path still needed it.

9. **P2 - Recommended render-size scaling is incorrectly gated on `fovMutable`.**
   `XRViewLab_xrEnumerateViewConfigurationViews()` skips recommended image-size changes when
   `XrViewConfigurationProperties::fovMutable` is false. That property describes mutable FOV,
   not whether an API layer can adjust app-visible recommended swapchain dimensions, so render
   savings can be silently disabled on runtimes with immutable FOV.

10. **P2 - D3D11 state restore is incomplete for complex app state.**
    The visor/edge-guard draw paths save and restore one RTV, one viewport, and vertex-buffer slot 0.
    Apps using multiple render targets, multiple viewports, or additional IA slots can have state
    collapsed after ViewLab draws, especially in the late `xrEndFrame` path.

11. **P2 - Open-inner AA does not feather top/bottom aperture bands.**
    `BuildOpenInnerEyeVisorVerts()` ignores `featherY`; the curved outer edge gets an alpha
    feather, but the full-width top and bottom black bands remain hard-edged.

12. **P2 - UI reads missing per-app `mask_inner_bridge_width` as 0.0 while native defaults to 0.5/global.**
    Older profiles missing this newer key keep the current global bridge width in native code,
    but the UI displays `0.0`. Opening and saving can persist a different bridge shape than the
    DLL would have used.

13. **P2 - Profile popup re-enables a global-only visor enable checkbox.**
   The XAML says visor enable is global-only and starts disabled, but `SetVisorSlidersEnabled`
   enables `VisorEnabledCheck` for custom profiles. `Save_Click` writes the value, while the DLL
   intentionally ignores per-app `mask_enabled`. This invites a UI promise the runtime will not keep.

14. **P2 - Profile global mode initializes the curve slider from stale per-app state.**
   When `visorSize <= 0` means "use global", most visor controls load global values, but
   `VisorCurveSlider` still uses `1.0 - maskCorner` from the app profile instead of
   `_globalMaskCorner`. Saving can accidentally turn an old per-app curve into a fresh override.

15. **P2 - Per-app `mask_horizontal` is decoded with a render-scale helper.**
   `LoadConfig()` reads `mask_horizontal` through `MillisToRenderScale`, whose minimum clamp is
   `0.1`, not the UI's `0.01` mask range. Legacy/visual-mask per-app values below 10% cannot
   round-trip correctly.

16. **P2 - Curve right-click reset disagrees with the default config.**
   The bundled config default is `mask_corner=0.5`, which maps to curve slider `0.5`, but
   `MaskSliderReset_RightClick` resets the curve slider to `0.75` and writes `mask_corner=0.25`.

17. **P3 - `build.ps1` copies native DLLs into publish output before rebuilding them.**
    The local publish/dev-test folder can receive stale native layer DLLs from a previous build.
    The MSI sources DLLs from the rebuilt Release paths, so the shipped MSI may be correct while
    local publish-folder testing is misleading.

18. **P3 - Legacy `visibility_mask_visor` path diverges from current visor geometry.**
    `ApplyVisorMask()` uses a symmetric closed superellipse and ignores open-inner mode, apex,
    inner-low/bridge controls, AA, and HD. If `visibility_mask_visor=1` with Technique off, the
    hidden visibility-mask visor no longer matches the UI or Direct C renderer.

19. **P3 - Canonical docs/contracts are stale or contradictory.**
    `docs\CONFIG.md` still labels bridge rise/peak/steepness as global-only and says AA/HD native
    wiring is Pass 3, while the code now reads/writes those paths. `CHANGELOG.md` has no 4.1.108
    entry for the pin-drag correction. Contract tests pass while missing the behavioural failures
    above: safe UI fallback, MSI default-ini packaging, edge-mask key wiring,
    `crop_outer_edges_only`, apex drag inverse math, profile use-global save semantics,
    global-only enable state, and release/late draw feature gating.

## Bug scan fix pass - 2026-07-10

Confident fixes implemented in-tree for findings 1, 2, 4-17, and 19:

- UI missing `mask_size` now falls back directly to `0.82`; curve reset is back to `0.5`.
- Default `xr-viewlab.ini` is included in publish/MSI output, and `build.ps1` copies native DLLs
  after MSBuild so publish-folder testing does not use stale layer binaries.
- Edge Masks "Both" controls write `visual_mask_only` / `horizontal_visual_mask_only`; unsupported
  one-sided controls are disabled until the DLL has matching behaviour.
- Native reads/logs `crop_outer_edges_only`, recommended-size scaling no longer depends on
  `fovMutable`, and per-app `mask_horizontal` uses mask-scale decoding.
- Preview apex pin dragging now uses the same centre-origin inverse as the rendered pin position.
- Profile "Use global visor settings" saves as global, disables preview pin edits in global mode,
  keeps unsupported per-app visor enable disabled, and uses safe bridge/curve fallbacks.
- Direct C/edge-guard late fallback flags are independent, open-inner AA feathers top/bottom bands,
  and D3D11 visor/edge draw state restore now covers all RTV slots and viewport slots.
- Contracts now cover the fixed behaviours above.

## Residual cleanup - 2026-07-11

The two previously documented residuals are fixed in-tree:

- Finding 3: the MSI no longer runs current-user work from VBScript, and ordinary upgrades now
  preserve all live user settings. A rejected intermediate design used a changing version marker
  and would have erased visor tuning after every update. The Start Menu component key path also
  moved from HKCU to HKLM.
- Finding 18: `visibility_mask_visor=1` is retired. The DLL logs and ignores the key so the
  legacy hidden-mesh reshaper can no longer diverge from Direct C visor geometry.

No known residual from the original 19-finding scan remains intentionally unfixed. Headset
validation is still required before pushing or publishing.

Additional cleanup from `docs/BUG_SCAN_2026-07-10.md` fixed in the same pass:

- Technique A no longer passes a null release-info pointer on wait failure.
- D3D11 rasterizer/blend/depth-state creation is checked before the renderer is marked initialized.
- Release-time draw paths hold COM references to swapchain textures while drawing outside the
  swapchain map lock, including the late fallback path, and draw entry points check for a live
  D3D11 context.
- Partner-eye boundary projection now uses bbox-relative visor extents instead of raw scale values.
- Large visor vertex buffers are heap-backed instead of render-thread stack arrays.
- Native config fallback paths remain absolute when module path lookup fails.
- Native config is now a stable game-start snapshot; unsafe mid-frame hot reload was removed in
  line with the UI's existing "restart the VR game" guidance.
- D3D11 draw hooks and renderer/session teardown now share a dedicated renderer lock, preventing
  the immediate context or state objects being released during a concurrent draw.
- UI/profile defaults now agree on `mask_corner=0.5` and `mask_rounded=1`.
- Profile window global/custom toggling restores the original custom values and refreshes preview.
- 32-bit layer files are installed from a 32-bit component under `ProgramFilesFolder`.
- `build.ps1` rejects non-Release packaging, checks native exit codes, and normalizes version parsing.

Remaining broader static-audit items not closed in this pass:

- `docs/BUG_SCAN_2026-07-10.md` P1.1/P1.2: a complete D3D11/config threading refactor is still
  pending. This pass reduced the biggest release-time texture lifetime risk with COM AddRef/Release
  and context guards, but did not convert the whole config/global renderer state to immutable
  snapshots or a dedicated renderer-state lock.
- Low-priority audit cleanups P3.5/P3.7/P3.8 remain documentation/cleanup-level items: redundant
  per-app fallback INI reads, legacy custom-install migration coverage, and intentionally hidden
  legacy bias/curve keys.

## Environment facts

- Test game: Pistol Whip, `D:\VR Games\Pistol Whip-working\Pistol Whip.exe`, via Virtual
  Desktop / VDXR, Quest 3. ReShade in that folder: Home = menu, PrintScreen = screenshot
  (screenshots land in `%USERPROFILE%\Documents\ReShade\Screenshots\`) — see `docs/VERIFICATION.md`.
- Games only query the visibility mask at session start — **restart the game** after toggling
  stencil settings.
- Layer registration: HKLM `SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit` (x64) and
  `WOW6432Node` (Win32). A stray 32-bit entry in the 64-bit hive was removed 2026-07-10.

## Latest verification

- `Tests\Verify-ViewLabContracts.ps1` passed on 2026-07-11 after the 4.1.113 build.
- `dotnet build xr-viewlab.csproj -c Release --no-restore` passed with 0 warnings / 0 errors on
  2026-07-11.
- `.\build.ps1` passed on 2026-07-11 with 0 warnings / 0 errors for WPF, x64 native, Win32 native,
  and WiX MSI:
   `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.113.msi`.
