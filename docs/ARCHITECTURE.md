# Architecture

> How ViewLab actually works, mapped to owning symbols. Grep the symbol; don't read the file.
> Update this document whenever a subsystem's shape changes.

## Two components, one contract

```
XRViewLab.UI (WPF, .NET 8)                    dllmain.cpp (native OpenXR implicit layer)
  MainWindow writes ──► %LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini ──► LoadConfig() reads
  ProfileWindow writes ─► HKCU\Software\cooooked\xr-viewlab\Apps\<exe> ─► ReadProfileDword()
```
There is no live IPC between UI and layer (except ReShade Remote, below). Crop/FOV/resolution and
per-app profiles remain a stable startup snapshot; restart the game after changing those settings.
Global visor controls are the deliberate exception: the UI commits `visor_live_revision` last, and
the layer reloads only visor geometry values at the locked `xrEndFrame` safe point. Mid-frame hot
reload remains forbidden because it raced native render hooks. The full key contract is
`docs/CONFIG.md`.

The performance HUD hooks `xrWaitFrame`, `xrBeginFrame`, and `xrEndFrame` without altering call
flow. `predictedDisplayPeriod` is the active runtime frame budget; QPC measures each matched frame
from `xrBeginFrame` entry through `xrEndFrame` return. CPU uses `GetSystemTimes` deltas. GPU Engine samples are
grouped by adapter LUID and engine ID, restricted to 3D engines, and the D3D11 render adapter is
preferred. A 60-frame rolling window supplies the missed-budget percentage, while a 120-sample
buffer draws frame-time deviation around the runtime target interval.

The live-state mapping is optional. When the settings app is not open, the native layer attempts
to reconnect at most once per second; it never opens the mapping once per submitted frame. When
mapped, it reads the compact generation-stamped snapshot at `xrEndFrame`, so enabled calibration
and live visor adjustments take effect without INI I/O on the render path.

## Native layer anatomy (dllmain.cpp — grep these symbols)

| Subsystem | Owning symbols |
|---|---|
| Layer bootstrap / hook install | `xrNegotiateLoaderApiLayerInterface`, `XRViewLab_xrGetInstanceProcAddr`, `EnsureInitialized` |
| Config read (ini) | `LoadConfig`, `ReadBoolSetting`, `ReadDoubleSetting`, `ReadStringSetting` |
| Config read (per-app registry) | `ReadProfileDword`, `ReadProfileDouble`, `SignedMillisToUnit` (encoding: signed = (v+1)*1000, unsigned = v*1000) |
| FOV / resolution crop (the perf feature) | `XRViewLab_xrLocateViews` (crops FOV tangents), `XRViewLab_xrEnumerateViewConfigurationViews` (reduces recommended size) |
| Crop-boundary diagnostics | `LocateViewsSnapshot`, `StoreLocateViewsSnapshot`, `TakeLocateViewsSnapshot`, `ValidateSubImage` (read-only exact session/display-time submission comparison) |
| D3D11 mask renderer init | `InitD3D11MaskRenderer` (compiles shaders via d3dcompiler; FreeLibrary ordering is load-bearing — REGRESSIONS R1) |
| Visor draw entry (Technique C Direct) | `XRViewLab_xrReleaseSwapchainImage` → `DrawVisorBorderToTexture` |
| Calibration diagnostics | `DrawCalibrationPatternsToTexture`, `DrawCalibrationGridToTexture`, `DrawCalibrationOverlayToTexture`, `AnyCalibrationPattern`, `g_calibrationFrameSerial`; ten optional patterns draw after the visor. Pixel-measurement tools use literal submitted-texture pixels and the complete eye rectangle; the 64 px grid is anchored at the fused centre without resizing; radial geometry is circular in shared tangent space. See `docs/CALIBRATION.md`. |
| Overlay coordinates | `OverlayCoordinateResolver`, `OverlayPlacement`; builds shared selected/full-lens tangent bounds from both eyes, chooses one visor-space target, then projects it independently through each eye's read-only FOV and destination viewport. Render Area and Lens Pinned therefore produce different eye pixels for one fused target. No game projection structure is modified. |
| FOV crop | `ApplyXRViewLabFov`; scales horizontal and vertical FOV tangents only. Asymmetric split crop never rotates or otherwise mutates `XrView.pose`; the former foveated-centre pose compensation is retired. |
| Experimental topmost overlays | `EnsureTopmostLayer`, `RenderTopmostLayer`, `BlockTopmostLayer`, `DestroyTopmostLayer`, `TopmostSubmission`; owns one transparent two-array-slice OpenXR projection swapchain per session, sized from stable game-texture capacity. Allocation is attempted once; any capacity/runtime/D3D failure latches the backend off until a new session and restores direct rendering. RTVs use the runtime resource's typed format; the premultiplied target is appended after application layers at `xrEndFrame`. |
| Native performance HUD | `HardwareTelemetry.cpp/.h` own a bounded Windows/PDH/DXGI worker and immutable snapshot; `HudWidgetId`, `kHudWidgetRegistry`, `UpdateHudMetrics`, `HudDrawSnapshot`, and `DrawCalibrationOverlayToTexture` consume it. Twelve ordered widgets wrap without gaps. APP/VR/FPS/frame interval remain per-frame OpenXR timing; hardware reads never run on the XR/D3D thread. Both eyes reuse one draw snapshot. See `HARDWARE_TELEMETRY_ARCHITECTURE.md` and `SYSTEM_HEADROOM_MODEL.md`. |
| Visor overlays (boundary/crosshair/notifications/racing events) | `DrawViewLabOverlaysToTexture`, `EnsureNotifyCardTexture`, `ConsumeRacingState`, `AnyViewLabOverlay`, `BoundaryFlashActive`, `kOverlayPS`, `kTexturedPS`; flat overlays use an explicit constant-colour shader, while cards use textured quads. `CrosshairPreview` and `BeanMaskEditor.DrawCrosshair` mirror native geometry. The signed medium-integrity broker owns global Windows consent/notifications plus the sole optional `IRacingTelemetryProvider` worker independently of settings/game lifetime. `RacingStateService` publishes exact generic spotter/flag/lap semantics through a 64-byte generation-safe mapping; native renders spatial glow/border and has no iRacing dependency. `NotificationService.SetRacingAttention` holds only desktop cards for at most five seconds during safety cues; lap cards and spatial cues retain their channels. |
| Visor geometry — open-inner arch | `BuildOpenInnerEyeVisorVerts` (per-eye, apex-aware superellipse cap + full-width top/bottom bands; exact rectangle at Curve 0) |
| Visor geometry — closed bean | `BuildVisorBorderVerts` (apex-aware superellipse ring fill; exact rectangle at Curve 0) |
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

`mask_size` uniformly scales only the exposed aperture. Its compatibility default is `1.0`, which
preserves the full existing opening; lowering it hides an outer band without changing crop
tangents, submitted FOV, or recommended render resolution. Peak X is mirrored by passing the
same sign-aware Bezier formula to each eye; its extended `-0.5..1` range is intentionally stronger
on the left end.

The main visor card keeps the Edge Masks and ReShade Remote pop-outs together above the visor
toggle. `BeanMaskEditor` exposes a draggable preview pin for every shape slider, so slider and
preview editing remain bidirectionally synchronised.

Responsive layout omits the Applications and Render Options sub-headers so the three primary cards
start on the same line in dual- and triple-column layouts. The beta-testers line remains visible
at every width; in triple-column mode it sits directly below the third-column card rather than
extending the applications column.

The responsive client-width cutovers are 280px (mini), 720px (two columns), and 1200px (three
columns). They intentionally favour fewer usable columns over three compressed cards.

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

`ProfileWindow.xaml` owns the PowerUp/profile popup chrome, including its dedicated ViewLab-themed
scroll viewer. The scrollbar occupies a separate grid column, never an overlay on visor controls.

## Overlay coordinate contract

ViewLab deliberately uses several coordinate spaces; treating them as interchangeable creates
stereo disparity:

- **Per-eye texture pixels:** the submitted `XrSwapchainSubImage.imageRect`. Visor geometry,
  boundary clipping, and calibration diagnostics ultimately rasterize here.
- **Per-eye cropped tangent space:** `tan(angleLeft/Right/Up/Down)` from each submitted projection
  view. `x = left + (tanX-selfLeft)/(selfRight-selfLeft)*width`; Y is the corresponding inverted
  mapping. Each eye has different bounds and therefore usually different pixel coordinates for the
  same angular point.
- **Shared binocular-overlap space:** the intersection of both eyes' cropped tangent bounds. HUD,
  trace, and notification X/Y fractions select one shared tangent point inside this intersection,
  which is then projected independently into each eye.
- **Calibrated angular space:** the crosshair uses shared tangent `(0,0)` (parallel straight-ahead
  rays, zero unintended angular disparity). Its CS pixels are fixed tangent spans (`2/1080`) and
  become eye pixels through that eye's pixels-per-tangent density. Crop changes therefore change
  clipping/density, not convergence or angular size.
- **Combined visor-preview space:** a user-facing fused representation of both eyes. It shows one
  resolved crosshair at the complete crop rectangle's visual centre; it does not display the two
  unequal native per-eye pixel projections.

The performance HUD and frame-trace positions are shared-overlap tangent points. Their base size is
angular; trace width remains intentionally relative to the available visible overlap because it is
a user-facing width control. Notification cards use one tangent width/height and each eye's own
X/Y density. The render-boundary flash is the exception: it is a per-eye inner outline of the final
submitted rectangle, with angularly stable stroke thickness and inset beyond half its stroke so
scissoring cannot remove it.

Calibration grid, ruler, gratings, colour bars, beacon, edge probes, checkerboards, zone plate,
clipping steps, and motion strip are explicitly **per-eye texture diagnostics**, not fused visor
content. Their job is to reveal downstream scaling/encoding, so their pixel-exact geometry remains
crop-relative by design. The visor mask likewise follows each submitted eye rectangle and its
existing shape contract; it is not repositioned as a binocular overlay.

## Build & packaging

`build.ps1` (the only build entry): bumps version in `Properties/AssemblyInfo.cs` +
`Installer/Product.wxs` → `dotnet publish` WPF (win-x64 self-contained single-file) → copies the
repo default `xr-viewlab.ini` into publish output → MSBuild native layer x64 then Win32 → copies
the freshly-built layer DLLs to dist + publish dir → WiX MSI → `dist\ViewLab-<version>.msi`.
The publish path is derived from `xr-viewlab.csproj`'s `TargetFramework`; generated WPF, native,
and WiX outputs are removed before their respective builds. After WiX completes, the script
administratively extracts the MSI and fails unless its app version and WPF/native/broker/identity
hashes match the fresh outputs and the compiled app contains the Overlays/boundary/crosshair/notification/iRacing
markers. This prevents a target-framework change from leaving WiX pointed at an older publish tree.

Global visor edits use the same generation-stamped `Local\XRViewLabLiveState` snapshot as HUD and
overlay controls and are always live while the UI is open. Unchanged snapshots cost only the fixed
generation check; there is no per-frame INI parse. Per-app visor overrides retain precedence.
Manual builds must bump versions by hand to avoid double-bumps. MSI registers both layer
manifests under HKLM Khronos ApiLayers (x64 + WOW6432Node), and installs the app, fresh-install
default ini, and ReShadePayload. Upgrades leave the live config in `%LOCALAPPDATA%` and per-app
HKCU profiles untouched.

## 2026-07-11 visor quality / robustness update

- Crop-boundary diagnosis is read-only: a bounded mutex-protected queue correlates primary-stereo
  `xrLocateViews` results to `xrEndFrame` only when both session and display time match. Verbose
  logs compare tangent-space FOVs and submitted sub-image bounds; no submitted data is rewritten.

- The D3D11 visor vertex format is `{x, y, alpha}`. The current opaque path uses fixed
  512-segment curve tessellation, so curved visor boundaries remain smooth at HMD eye-texture
  resolutions without restoring the removed HD or anti-aliasing controls.
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
