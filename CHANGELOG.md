# Changelog

> Visor-mask status note (2026-07-01): the native visor mask is implemented as a D3D11 direct-write into
> the game's eye textures at `xrReleaseSwapchainImage`. It has NOT been confirmed visible in-headset.
> See `PROJECT_STATUS.md` / `HANDOFF.md` for the live debugging state.

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
