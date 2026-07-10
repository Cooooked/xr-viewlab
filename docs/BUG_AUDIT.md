# Bug Audit Findings

Date: 2026-07-10

Scope: static audit of native layer, WPF UI/config, installer/build scripts, contract tests, and canonical docs. No fixes are included in this document.

## High Severity

1. `xrEndFrame` fallback draw can write swapchain images after release.
   - References: `dllmain.cpp:1737-1897`, `dllmain.cpp:2604-2671`
   - Why: the normal Direct C path draws during `xrReleaseSwapchainImage`, before forwarding to the runtime. The late fallback in `xrEndFrame` calls `DrawCapturedProjectionTextures()` when no release draw happened, but projection images have normally already been released by then. That risks writing to images the app no longer owns and can race runtime composition.
   - Confidence: high

2. MSI does not install `xr-viewlab.ini`, despite first-run migration and docs assuming it exists beside the app.
   - References: `Installer/Product.wxs:116-174`, `Installer/Installer.wixproj:23-25`, `Installer/HarvestedFiles.wxs:14-16`, `XRViewLab.UI/MainWindow.cs:175-181`, `docs/ARCHITECTURE.md:80-81`, `Tests/Verify-ViewLabContracts.ps1:22-28`
   - Why: `Product.wxs` installs the app, DLLs, manifests, registry entries, and ReShade payload, but not `xr-viewlab.ini`. `HarvestedFiles.wxs` contains an ini entry but is not compiled. Fresh installs may not have a packaged default ini for `EnsureConfigMigrated()` to copy.
   - Confidence: high

3. Edge Masks popup writes keys the DLL never reads.
   - References: `MainWindow.xaml:486-492`, `XRViewLab.UI/MainWindow.cs:63-68`, `XRViewLab.UI/MainWindow.cs:958-963`, `XRViewLab.UI/MainWindow.cs:1347-1352`, `dllmain.cpp:2136-2137`, `docs/CONFIG.md:21`
   - Why: the UI persists `horizontal_visual_mask_both`, `horizontal_outer_eye_mask`, `horizontal_inner_eye_mask`, `vertical_visual_mask_both`, `vertical_top_mask_only`, and `vertical_bottom_mask_only`. The DLL only reads `visual_mask_only` and `horizontal_visual_mask_only`, so these controls appear to have no native effect.
   - Confidence: high

4. `crop_outer_edges_only` is a UI/documented config key but appears ignored by the DLL.
   - References: `MainWindow.xaml:420`, `XRViewLab.UI/MainWindow.cs:60`, `XRViewLab.UI/MainWindow.cs:955`, `XRViewLab.UI/MainWindow.cs:1344`, `docs/CONFIG.md:19`, `dllmain.cpp:2132-2185`, `dllmain.cpp:2328`
   - Why: the UI saves `crop_outer_edges_only`, but `LoadConfig()` does not read it. The config log hardcodes `horizontal_outer_edges_only=1`, so the checkbox likely does not change runtime crop behavior.
   - Confidence: high

## Medium Severity

5. Recommended render-size scaling is incorrectly gated on `fovMutable`.
   - References: `dllmain.cpp:2732-2747`
   - Why: `xrEnumerateViewConfigurationViews` skips recommended image size changes when `XrViewConfigurationProperties::fovMutable` is false. That OpenXR property describes whether FOV can be changed, not whether an API layer can alter app-visible recommended swapchain dimensions. This may silently disable render-resolution savings on runtimes that report immutable FOV.
   - Confidence: medium

6. D3D11 state restore is incomplete for apps using multiple render targets, viewports, or IA slots.
   - References: `dllmain.cpp:1344-1400`, `dllmain.cpp:1473-1510`
   - Why: the visor and edge-guard draw paths save/restore only one RTV, one viewport, and vertex buffer slot 0. If the game had multiple render targets, multiple viewports, or other IA slots bound, ViewLab collapses that state after drawing and can corrupt later app rendering.
   - Confidence: medium-high

7. Open-inner antialiasing does not feather the top/bottom aperture edges.
   - References: `dllmain.cpp:1049-1053`, `dllmain.cpp:1079-1082`, `dllmain.cpp:1121-1126`
   - Why: `BuildOpenInnerEyeVisorVerts()` explicitly ignores `featherY`. With stencil/open-inner mode enabled, the outer curved side gets an alpha feather strip, but the full-width top and bottom hard-mask bands remain aliased.
   - Confidence: high

8. Missing `mask_size` fallback differs between UI and DLL, allowing the UI to save an invisible visor size.
   - References: `XRViewLab.UI/MainWindow.cs:940`, `dllmain.cpp:2305-2308`, `docs/CONFIG.md:29`, `xr-viewlab.ini:24`
   - Why: the DLL falls back to `mask_size=0.82` if the key is missing, but the UI falls back to `OpeningFromMask(...)`, which can become `1.0` and produce a full opening. Opening and saving a damaged ini can overwrite the DLL's safe fallback with an invisible visor size.
   - Confidence: high

9. Per-app "Use global visor settings" can freeze current global visor values instead of preserving the global sentinel.
   - References: `ProfileWindow.xaml:148`, `XRViewLab.UI/ProfileWindow.cs:124-137`, `XRViewLab.UI/ProfileWindow.cs:332-339`, `XRViewLab.UI/MainWindow.cs:2155-2163`, `XRViewLab.UI/MainWindow.cs:2237-2244`, `dllmain.cpp:2308-2316`
   - Why: the DLL treats `visor_size == 0` as the per-app sentinel for global visor settings. The profile window loads global slider values when the checkbox is checked, and the save path writes positive `visor_size` values when saving a custom crop/render profile. Later global visor edits may not apply to that app.
   - Confidence: high

10. UI reads missing per-app `mask_inner_bridge_width` as `0.0`, while native/global defaults use `0.5`.
    - References: `XRViewLab.UI/MainWindow.cs:1876`, `dllmain.cpp:2182`, `dllmain.cpp:2258-2270`, `docs/CONFIG.md:33`, `xr-viewlab.ini:36`
    - Why: older profiles missing this newer key keep the current global bridge width in native code, but the UI displays `0.0`. Opening and saving can persist a different bridge shape than the DLL would have used.
    - Confidence: high

11. Bean mask editor drag math cannot naturally drag the outer apex above center.
    - References: `XRViewLab.UI/BeanMaskEditor.cs:359-367`, `XRViewLab.UI/BeanMaskEditor.cs:438-441`, `dllmain.cpp:887-892`, `dllmain.cpp:1068-1071`
    - Why: `PinPositions()` renders the apex as `centerY + OuterApexY * span`, but the drag handler decodes it as `(mouse.Y - y0) / span`. That maps the top of the shape to `0` instead of about `-0.5`, so dragging inside the visible shape cannot produce negative apex values even though the slider can.
    - Confidence: high

12. Optional visibility-mask visor path is not formula-identical to the Direct C/UI visor.
    - References: `dllmain.cpp:2800-2868`, `dllmain.cpp:2902-2915`
    - Why: `ApplyVisorMask()` claims parity, but it uses a symmetric closed superellipse and ignores restored shape controls such as apex-y, open-inner mode, inner-low bridge controls, AA, and HD. If `visibility_mask_visor=1` with `visor_technique=off`, visor geometry diverges from the UI and normal native renderer.
    - Confidence: high

13. `build.ps1` copies native DLLs into the publish folder before rebuilding those DLLs.
    - References: `build.ps1:73-97`, `build.ps1:99-105`, `docs/ARCHITECTURE.md:76-79`
    - Why: the publish/dev-test folder can receive stale DLLs from a previous build. The MSI later sources DLLs from rebuilt Release paths, so the MSI may be correct while local publish-folder testing is stale and misleading.
    - Confidence: high

14. `build.ps1 -Configuration Debug` produces a mixed artifact because Release paths are hardcoded.
    - References: `build.ps1:1-4`, `build.ps1:74`, `build.ps1:77`, `build.ps1:90-91`, `build.ps1:108-109`, `Installer/Product.wxs:117`, `Installer/Product.wxs:119`, `Installer/Product.wxs:131`
    - Why: the script accepts a configuration parameter, but publish path, native DLL paths, and MSI source paths are hardcoded to Release. A Debug build can package Release components.
    - Confidence: high

15. Installer config reset actions may target the elevated/per-machine account rather than the actual user.
    - References: `Installer/Product.wxs:8`, `Installer/Product.wxs:35-50`, `Installer/Product.wxs:107-110`, `Installer/PreserveConfig.vbs:11-16`, `Installer/PreserveConfig.vbs:68`, `Installer/PreserveConfig.vbs:149-163`
    - Why: the MSI is per-machine, while immediate custom actions touch `%LOCALAPPDATA%` and HKCU. In elevated installs, those locations can resolve to the installing/elevated account instead of the ViewLab user's live settings and profiles.
    - Confidence: medium

16. Canonical state is internally contradictory about the current version and active work.
    - References: `STATE.md:7`, `STATE.md:22-41`, `STATE.md:77-87`, `Properties/AssemblyInfo.cs:8-14`, `Installer/Product.wxs:4-5`
    - Why: `STATE.md` says current version is `4.1.105` and passes 2/3/4 are pending, then later says passes 2/3/4 are implemented and built as `4.1.107`. Source/package versions are `4.1.108`. Agents following the repo memory can test or ship the wrong artifact.
    - Confidence: high

17. Contract tests validate source strings but miss packaging-critical invariants.
    - References: `Tests/Verify-ViewLabContracts.ps1:15-28`, `Tests/Verify-ViewLabContracts.ps1:47-51`, `Installer/Product.wxs:116-174`, `Installer/Installer.wixproj:23-25`
    - Why: the test checks repo `xr-viewlab.ini` and selected reset strings, but not whether the actual MSI includes the ini, whether WiX compiles all required sources, or whether build ordering puts fresh DLLs in publish output. Packaging regressions can pass contracts.
    - Confidence: high

## Low Severity

18. Failed D3D11 renderer initialization leaks partially-created objects on some error paths.
    - References: `dllmain.cpp:806-850`
    - Why: after shader/layout creation succeeds, later failures such as vertex-buffer creation set `g_d3d11Mask.failed=true` and return without releasing already-created COM objects. This is mainly a partial-init failure leak, not the normal runtime path.
    - Confidence: medium

19. `docs/CONFIG.md` render-scale registry encoding is stale.
    - References: `docs/CONFIG.md:8-9`, `docs/CONFIG.md:22`, `XRViewLab.UI/MainWindow.cs:2225`, `dllmain.cpp:2274-2276`
    - Why: docs say per-app `render_scale` is a DWORD encoded as `v * 1,000,000`, but the UI writes a registry string and the DLL reads the string first with a DWORD fallback. Runtime behavior is likely okay, but the canonical contract is inaccurate.
    - Confidence: high

20. `docs/CONFIG.md` contradicts implementation for per-app bridge shape keys.
    - References: `docs/CONFIG.md:34-36`, `docs/CONFIG.md:75-77`, `dllmain.cpp:2264-2267`, `Tests/Verify-ViewLabContracts.ps1:115-117`
    - Why: the visor table says bridge rise, peak-x, and steepness are global-only, while the update section and implementation say per-app profiles carry all six shape keys.
    - Confidence: high

21. `docs/CONFIG.md` has stale notes for `visor_hd` and `visor_antialiasing`.
    - References: `docs/CONFIG.md:39-40`, `docs/CONFIG.md:73-74`, `dllmain.cpp:2176-2177`, `dllmain.cpp:894-895`, `dllmain.cpp:1084-1126`
    - Why: the table says native wiring is still Pass 3, while later docs and code show native HD tessellation and AA feathering are implemented.
    - Confidence: high
