# Architecture

> How ViewLab actually works, mapped to owning symbols. Grep the symbol; don't read the file.
> Update this document whenever a subsystem's shape changes.

## Two components, one contract

```
XRViewLab.UI (WPF, .NET 8)                    dllmain.cpp (native OpenXR implicit layer)
  MainWindow writes ──► %LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini ──► LoadConfig() reads
  ProfileWindow writes ─► HKCU\Software\cooooked\xr-viewlab\Apps\<exe> ─► ReadProfileDword()
```
There is no live IPC between UI and layer (except ReShade Remote, below). Games read a stable
config snapshot at startup; restart the game after changing settings. Mid-frame hot reload was
removed because it raced native render hooks. The full key contract is `docs/CONFIG.md`.

## Native layer anatomy (dllmain.cpp — grep these symbols)

| Subsystem | Owning symbols |
|---|---|
| Layer bootstrap / hook install | `xrNegotiateLoaderApiLayerInterface`, `XRViewLab_xrGetInstanceProcAddr`, `EnsureInitialized` |
| Config read (ini) | `LoadConfig`, `ReadBoolSetting`, `ReadDoubleSetting`, `ReadStringSetting` |
| Config read (per-app registry) | `ReadProfileDword`, `ReadProfileDouble`, `SignedMillisToUnit` (encoding: signed = (v+1)*1000, unsigned = v*1000) |
| FOV / resolution crop (the perf feature) | `XRViewLab_xrLocateViews` (crops FOV tangents), `XRViewLab_xrEnumerateViewConfigurationViews` (reduces recommended size) |
| D3D11 mask renderer init | `InitD3D11MaskRenderer` (compiles shaders via d3dcompiler; FreeLibrary ordering is load-bearing — REGRESSIONS R1) |
| Visor draw entry (Technique C Direct) | `XRViewLab_xrReleaseSwapchainImage` → `DrawVisorBorderToTexture` |
| Visor geometry — open-inner arch | `BuildOpenInnerEyeVisorVerts` (per-eye, apex-aware superellipse cap + full-width top/bottom bands) |
| Visor geometry — closed bean | `BuildVisorBorderVerts` (apex-aware superellipse ring fill) |
| Visor geometry — nose divot | `BuildNoseBridgeCurve` (cubic bezier trapezoid fill, band-clamped to NDC bottom) |
| Visor geometry — partner-eye boundary | `BuildProjectedPartnerVisorVerts` (only in closed-bean mode) |
| Shape helpers | `VisorCurveExponent` (geometric 32·(2/32)^curve), `ApexYFromConfigNdc` (THE one y-flip) |
| Visibility-mask stencil filter | `XRViewLab_xrGetVisibilityMaskKHR` (`keepOuterEdge` filter; runs even when visor active) |
| Legacy visor paths (hidden, kept for reference) | Technique A `RenderVisorCutout`/`InitVisorQuad`, Technique B interception, `visibilityMaskVisor` reshaper `ApplyVisorMask` |
| Logging | `Log` → `%LOCALAPPDATA%\XR ViewLab\ViewLab.log`; config snapshot logged by `LoadConfig` |

### Coordinate conventions (bug factory — read before touching geometry)

- UI preview (`BeanMaskEditor.cs`) is in screen coords: **y grows DOWN**.
- Native draws in D3D NDC: **y grows UP** (+1 = top of the eye texture rect).
- The flip between them lives in exactly ONE native helper: `ApexYFromConfigNdc`. The nose divot
  anchors to NDC `y0` (bottom). Anchoring anything to `y1` "like the UI does" recreates the
  divot-on-top-of-lens regression (REGRESSIONS R2).
- Left eye (viewIndex 0): outer = left = −x, inner/nose = right = +x. Right eye mirrored.

### Geometry parity rule

`BeanMaskEditor.cs` is the reference spec. Native geometry (`BuildOpenInnerEyeVisorVerts`,
`BuildVisorBorderVerts`, `BuildNoseBridgeCurve`, `VisorCurveExponent`) must stay formula-identical
to the preview (`AddOpenInnerHalf`, `BuildGeometry`, `AddNoseBridgeCurve`, `CurveExponent`),
modulo the documented y-flip. The contract test pins parts of this.

### The stencil pipeline ("Stencil outer edges only")

Three cooperating mechanisms, all keyed to ini `stencil_outer_edges_only` (DLL global
`outerEdgeVisibilityMaskOnly`, legacy fallback key `outer_edge_visibility_mask_only`):
1. `XRViewLab_xrGetVisibilityMaskKHR` filters the runtime's hidden-area mesh (e.g. Virtual
   Desktop's FOV stencil) so the GAME only stencils each eye's outer half. Must run even when
   the visor is active — the game stencils whatever this returns (DECISIONS D8).
2. `DrawVisorBorderToTexture` picks visor geometry: ON → open-inner arch, OFF → closed bean
   (`openInnerShape = outerEdgeVisibilityMaskOnly`) — same switch the preview uses.
3. The partner-eye boundary (`BuildProjectedPartnerVisorVerts`) only draws in closed-bean mode;
   in arch mode it would black the nose side (DECISIONS D9).
Games query the visibility mask once per session → restart the game to see stencil changes.

### Horizontal crop modes

`crop_outer_edges_only=1` keeps each eye's inner/nose-side FOV edge fixed and reduces only its
outer edge. With it off, `XRViewLab_xrLocateViews` scales both horizontal edges around each eye's
centre. Recommended render width follows the same selected mode. This must remain a real toggle,
not merely a saved preference.

## WPF app anatomy

| Subsystem | Where |
|---|---|
| Settings load/save | `MainWindow.cs` `LoadSettings`/`SaveSettings` + `WritePrivateProfileString`; key constants at top of class |
| Visor preview + pins | `BeanMaskEditor.cs` (`OnRender`, `PinPositions`, mouse handlers) |
| App list / per-app profiles | `MainWindow.cs` app-table region, `AppProfile.cs`, `ProfileWindow.cs`; registry encode helpers `ToMillis`/`ToSignedMillis`/`FromMillis`/`FromSignedMillis` |
| ReShade Remote | `ReShadeRemoteWindow.cs` + `ReShadeControlService.cs`: shared-memory control block + `openxr_quad_transform.ini` under ProgramData, controls the bundled payload in `ReShadePayload/` (payload internals: `ReShadePayload/Docs/`) |
| Update check | `UpdateRelease.cs` + GitHub releases endpoint in `MainWindow.cs` |

## Build & packaging

`build.ps1` (the only build entry): bumps version in `Properties/AssemblyInfo.cs` +
`Installer/Product.wxs` → `dotnet publish` WPF (win-x64 self-contained single-file) → copies the
repo default `xr-viewlab.ini` into publish output → MSBuild native layer x64 then Win32 → copies
the freshly-built layer DLLs to dist + publish dir → WiX MSI → `dist\ViewLab-<version>.msi`.
Manual builds must bump versions by hand to avoid double-bumps. MSI registers both layer
manifests under HKLM Khronos ApiLayers (x64 + WOW6432Node), and installs the app, fresh-install
default ini, and ReShadePayload. Upgrades leave the live config in `%LOCALAPPDATA%` and per-app
HKCU profiles untouched.

## 2026-07-10 visor quality / robustness update

- The D3D11 visor vertex format is now `{x, y, alpha}`. `visor_antialiasing=1` adds feather
  strips on the aperture boundary and draws with `SRC_ALPHA`; `visor_hd=1` doubles curve
  tessellation from 96 to 192 segments.
- Direct C draws at `xrReleaseSwapchainImage` using cached per-image/per-slice RTVs. The
  `xrEndFrame` draw is fallback-only when release-time drawing did not run for that frame, with
  independent release flags for edge guard and Direct C visor paths.
- `g_rendererMutex` serializes D3D11 immediate-context use and renderer/session teardown across
  OpenXR hooks; `g_swapchainMutex` remains responsible for the swapchain map itself.
- `XRViewLab_xrLocateViews` stores original and cropped FOVs for diagnostics; recommended-size
  logging uses per-view effective horizontal/vertical scale helpers when available.
- MSI upgrades preserve the current user's visor and per-app settings. The OpenXR layer is
  read-only with respect to user configuration; it never performs installer cleanup in a game.
- `visibility_mask_visor` is retired. The old hidden-mesh visor path is ignored because it cannot
  represent the current Direct C shape, AA, or HD contract.
