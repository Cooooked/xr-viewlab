# Changelog

> Visor-mask status note (2026-07-01, late): layer submission is CONFIRMED working in-headset (a BOTH-eye
> debug quad drew). The mask was invisible because `visorSize` resolved to 1.0 (opening = whole view =
> zero-width border); fixed in 4.1.46. Technique A (quad overlay) is now the primary path and awaits
> in-headset confirmation. See `PROJECT_STATUS.md` / `HANDOFF.md`.

## 4.1.55 - 2026-07-02

- Optimized release-time D3D11 visor/edge drawing by caching runtime swapchain render-target views instead
  of creating an RTV for every eye draw.
- Removed heap allocations from the high-segment visor geometry builders by switching their fixed-size
  curve buffers to stack arrays.
- Verified the x64 native OpenXR layer build before the full installer build.
- Full installer build passed: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.55.msi`.

## 4.1.54 - 2026-07-02

- Preserved the real vertical/horizontal tangent crop path. The LOD-pop-in experiment is now
  diagnostic-only and no longer bypasses cropped FOV, avoiding the stretch-over-lens failure.
- Reworked the edge-smear experiment to draw black guard pixels into projection texture edges instead of
  changing submitted `imageRect` metadata.
- Technique A remains a real OpenXR composition-layer path, now drawing the same open-inner left/right
  visor artwork into a BOTH-eye alpha quad for VDXR compatibility.
- Technique B is narrowed to D3D11 colour non-MSAA swapchains and bypasses unsupported swapchains instead
  of intercepting them.
- Technique C now draws at release and late `xrEndFrame`, with higher-segment open-inner geometry and a
  projected partner-eye boundary for aggressive horizontal crop convergence.
- Removed visible X/Y visor sliders from the main UI and made the DLL ignore stale X/Y profile biases.
  Size, Width, Height, and Curve are the active shape controls.
- Full installer build passed: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.54.msi`.

## 4.1.47 — 2026-07-01

- Removed the debug test quad entirely (it proved layer submission; done).
- Technique A reworked to a single BOTH-eye head-locked quad (per-eye LEFT/RIGHT quads were not rendering
  on VDXR; the BOTH-eye quad did), FOV-sized, alpha-cutout (black ring, transparent hole).
- Technique C: added `context->Flush()` after the eye-texture draw so a streaming runtime's encoder
  (VDXR) sees the write; C remains unreliable on streaming runtimes (A is primary).

## 4.1.46 — 2026-07-01

- FIX (root cause of "no mask"): `visorSize` fallback for a missing `mask_size` was the legacy
  OpeningFromMask formula, which clamps to 1.0 for cropped views = full opening = invisible border.
  Fallback is now a visible 0.82. This is why neither technique showed a mask despite drawing correctly.

## 4.1.45 — 2026-07-01

- Added a UI **Technique** selector (radio: Quad (A) / Direct (C) / Off) in Render Options, writing
  `visor_technique`. Verified in-app (round-trips a↔c↔off to the ini). No more hand-editing the ini.

## 4.1.44 — 2026-07-01

- Implemented technique A (quad overlay) alongside technique C, selectable via `visor_technique`.
- Shared `CreateHeadLockedRgbaLayer` helper for the head-locked VIEW-space RGBA swapchain.

## 4.1.43 — 2026-07-01

- Visor ENABLE made global-only: per-app profiles no longer carry/override `mask_enabled` (they still
  tune shape). Fixes stale per-app `mask_enabled=0` silently overriding the global toggle. Per-app enable
  checkbox disabled in the profile popup.

## 4.1.42 — 2026-07-01

- Added a DEBUG head-locked blue test quad (`test_quad` config, default off): our own
  `XrCompositionLayerQuad` in a `VIEW` reference space at (0,0,−1), 0.4 m, cleared solid blue. Independent
  of the game textures and of `mask_enabled`. Purpose: prove OpenXR layer submission reaches the headset.
- Captured `xrCreateReferenceSpace` for the quad's head-locked space.

## 4.1.41 — 2026-07-01

- Removed the gate that disabled the native D3D11 visor whenever the game called
  `xrGetVisibilityMaskKHR` (this was why Unity/Pistol Whip never drew).
- Moved the visor draw from `xrEndFrame` to `xrReleaseSwapchainImage` — the lifecycle-correct point where
  the app has finished rendering the eye and the runtime has not yet consumed it. `xrEndFrame` now only
  captures the per-eye layout (imageRect + array slice) for the next frame's release-time draw.
- Made the visibility-mask mesh-reshape optional (`visibility_mask_visor`, default off); it never
  suppresses the D3D11 path. Outer-edge visibility-mask filtering preserved.
- Installed `xrReleaseSwapchainImage` hook (was only captured before).

## 4.1.40 — 2026-07-01

- Main window: removed the large blank gap below VIEWLAB ENABLED. Col 0 is now a single top-aligned
  `LeftColumnPanel` StackPanel, mirroring cols 2/4, so the tall RowSpan side panels can't inject overflow
  between the left cards.
- App Profile popup: content wrapped in a vertical `ScrollViewer`; bean editor restored in the visual
  tree; visor sliders interactive when "Use global" is unchecked; opens without crashing.
- Typeless-safe RTV format for the eye-texture write (use app-requested swapchain format mapped to non-SRGB).
- Added extensive one-shot `DIAG` logging across the whole D3D11 mask path.

## 4.1.39 — 2026-06-30/07-01

- Removed the old ViewLab-owned `XrCompositionLayerProjection` "orb" visor renderer.
- Implemented native D3D11 direct-write of the kidney/superellipse visor border into the game's existing
  eye textures; added swapchain tracking hooks (create/enumerate-images/acquire/destroy).
- Restored the ProfileWindow bean editor and interactive per-app visor sliders.

## 4.1.7 — 2026-06-25

- Removed stale `OpenVRBridge/`, `ReShadePayload/` binaries, and `ReshadeAI/` agent files from repo.
- Fixed installer `Product.wxs` to not reference deleted ReShadePayload files.
- Created `SOURCE_BACKUP.md` consolidating all backup sources and rebuild instructions.
- Published GitHub release v4.1.7 with MSI.

## 4.1.0 — 2026-06-25

### UI

- Responsive layout: four window-width scale modes — see Layout Modes below.
- Triple-column mode: left = render sliders, middle = applications table, right = Render Options + ReShade MENU sections.
- Right column uses an independent StackPanel (not shared Grid rows), eliminating the ghost height gap that previously appeared below Render Options.
- VIEWLAB ENABLED / VIEWLAB DISABLED badge replaces the checkbox. Animated red/green border, background, and text colour transition (0.25 s).
- Visual Masks moved into a non-layout-participating Popup flyout behind a "Visual Masks ▾" button. Popup has no measured parent so it never reserves blank space when closed.
- Removed "RENDER" section subheader.
- Removed "Horizontal visual mask" and "Vertical visual mask" inline subheaders from the main card.
- OpenXR and OpenVR ReShade MENU sections retain their titles but all entries replaced with "Coming soon" stubs.
- Install ReShade button and "ReShade Available" pill badge removed.

### Applications table

- XR type column added (OpenXR / OpenVR, detected via OpenComposite_ display-name prefix).
- Junk-app blacklist: steam, steamvr, steamtours, racelab_vr, vrmonitor, vrserver, vrdashboard, openvr_overlay, xr_composition_layer_override, pivotpoint and variants filtered from the app list.

### Settings

- Sliders and value boxes auto-save on change; Save button removed.
- Settings write via `WritePrivateProfileString` unchanged; INI path unchanged.

### Layout Modes

| Mode | Width | Content |
|------|-------|---------|
| **Mini** | < 360 px | Single column, sliders compress to full width, footer items equally spaced |
| **Small** | 360–599 px | Single column, sliders full width with labels and hints |
| **Medium** | 600–899 px | Two columns — left: render sliders + options; right: applications table + ReShade menus |
| **Large** | ≥ 900 px | Three columns — left: render sliders; middle: applications table; right: Render Options + ReShade menus |

### Build

- Version bumped to 4.1.0 in AssemblyInfo.cs and Product.wxs.

---

## 4.0.x and earlier

See git history.
