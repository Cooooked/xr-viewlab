# Changelog

## 4.1.0 — 2026-06-25

### UI

- Responsive layout: compact (<360 px), single (360–599 px), dual (≥600 px), triple (≥900 px) column modes.
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

### Build

- Version bumped to 4.1.0 in AssemblyInfo.cs and Product.wxs.

---

## 4.0.x and earlier

See git history.
