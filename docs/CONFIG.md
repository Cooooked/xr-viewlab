# Config contract — every key, both sides

> Canonical reference for the UI ↔ DLL configuration contract. Update in the same commit as any
> key change. A key that exists on only one side is a bug (see REGRESSIONS R3).

Live ini: `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini` (section `[Settings]`; UI writes via
`WritePrivateProfileString`, DLL reads in `LoadConfig`). Bundled defaults: repo `xr-viewlab.ini`.
Per-app registry: `HKCU\Software\cooooked\xr-viewlab\Apps\<exe>` — DWORD encodings:
**millis** = round(v·1000); **signed millis** = round((v+1)·1000); render_scale = v·1,000,000.

## Render crop (core perf feature)

| ini key | range/default | DLL global | per-app | Notes |
|---|---|---|---|---|
| `enabled` | 1 | `enabled` | `app_enabled`, `profile_enabled` | master switch |
| `total_render_height` | 0..1 / 0.18 | `totalTangent` | millis | legacy fallbacks `total_share`, `vertical_tangent` |
| `split_mode` + `top_tangent` / `bottom_tangent` | 0.09/0.09 | `topTangent`/`bottomTangent` | millis | split top/bottom crop |
| `horizontal_render_width` | 0..1 / 0.80 | `horizontalRenderWidth` | millis | |
| `crop_outer_edges_only` | 1 | `cropOuterEdgesOnly` | — | horizontal crop takes from outer edges only when on; full-width symmetric horizontal crop when off |
| `foveated_center_compensation` | 0 | `foveatedCenterCompensation` | dword | |
| `visual_mask_only`, `horizontal_visual_mask_only` | 0 | same names | — | mask instead of crop (loses GPU savings); Edge Masks popup "Both" controls write these keys |
| `render_scale` | 0.1..3 / 1.0 | `renderScale` | ×1e6 dword | per-game supersampling |

## Visor mask

| ini key | range/default | DLL global | per-app | UI control |
|---|---|---|---|---|
| `mask_enabled` | 0 | `maskEnabled` | global-only (per-app enable deliberately ignored — DECISIONS D6) | Visor mask checkbox |
| `mask_size` | 0.05..1 / **0.82 fallback** | `visorSize` | `visor_size` millis | Size slider (single source of truth — REGRESSIONS R4) |
| `mask_corner` | 0..1 | `visorCurve = 1 − maskCorner` | `mask_corner` millis | Curve slider (stored inverted) |
| `mask_outer_apex_y` | −0.5..0.5 / 0 | `visorOuterApexY` | signed millis | Apex Y slider + red pin |
| `mask_inner_lower_y` | 0..0.333 / 0 | `visorInnerLowerY` | millis | Inner low slider + orange pin |
| `mask_inner_bridge_width` | 0..1 / 0.5 | `visorInnerBridgeWidth` | millis | Bridge slider + blue pin |
| `mask_inner_bridge_rise` | 0..0.5 / 0 | `visorInnerBridgeRise` | millis | Rise slider |
| `mask_inner_bridge_peak_x` | 0..1 / 0.5 | `visorInnerBridgePeakX` | millis | Peak X slider |
| `mask_inner_bridge_steepness` | 0..1 / 0.5 | `visorInnerBridgeSteepness` | millis | Steepness slider |
| `mask_width_scale` / `mask_height_scale` | 1.0 | `visorWidth`/`visorHeight` | `visor_width`/`visor_height` | neutralized in product (kept 1.0) |
| `visor_technique` | `c` | `visorTechnique` | — | a/b hidden; DirectWrite is the product path |
| `visor_hd` | 0 | `visorHD` | — | HD visor checkbox; doubles curve tessellation |
| `visor_antialiasing` | 1 | `visorAntialiasing` | — | Anti-aliasing checkbox; controls aperture feather strips |

## Stencil / visibility mask

| ini key | default | DLL global | Notes |
|---|---|---|---|
| `stencil_outer_edges_only` | 1 | `outerEdgeVisibilityMaskOnly` | THE checkbox key; legacy fallback `outer_edge_visibility_mask_only`. Drives the 3-part stencil pipeline (ARCHITECTURE) |
| `visibility_mask_visor` | 0 | `visibilityMaskVisor` | legacy hidden-mesh reshaper, only when visor off |

## Diagnostics / misc

| ini key | default | DLL global |
|---|---|---|
| `verbose_logging` | 0 | `verboseLogging` |
| `edge_smear_fix`, `edge_smear_pixels` | 0 / 2 | `edgeSmearFix`, `edgeSmearPixels` |
| `lod_popin_fix` | 0 | `lodPopInFix` — diagnostic-only; the full-FOV path it once enabled causes stretch and must stay disabled (DECISIONS D5) |

Dead keys (removed 4.1.65, do not resurrect): `mask_vertical`/`mask_horizontal` as opening source,
`mask_rounded`, `mask_offset_y`, `mask_*_bias`, `mask_*_curve` as visor inputs, `reshade_hmd_menu`,
`reshade_desktop_duplicate`, `reshade_3d_menu`. Some still exist as read-fallbacks in the current
4.1.55-derived `LoadConfig`; treat them as legacy compat only.

UI-only keys: window size/column layout keys, `stencil_outer_edges_only` also gates preview mode +
inner-low slider enablement. ReShade Remote state lives in ProgramData (shared-memory block +
`openxr_quad_transform.ini`), not in this ini.

## 2026-07-10 implementation update

- MSI install/upgrade intentionally backs up the live ini to `xr-viewlab.bak.ini`, then resets
  visor keys to safe defaults. Crop and render profile values are preserved.
- The bundled `xr-viewlab.ini` must include every product key with safe defaults, including
  `mask_enabled=0`, `mask_size=0.82`, `mask_corner=0.5`, `visor_hd=0`, and
  `visor_antialiasing=1`.
- `visor_hd` is now native: it doubles visor curve tessellation. `visor_antialiasing` is now
  native: it enables a per-vertex alpha feather strip on the visor aperture edge.
- Per-app profiles now carry all six shape keys: `mask_outer_apex_y`, `mask_inner_lower_y`,
  `mask_inner_bridge_width`, `mask_inner_bridge_rise`, `mask_inner_bridge_peak_x`, and
  `mask_inner_bridge_steepness`. Visor enable remains global-only and the UI deletes any stale
  per-app `mask_enabled` override when saving a custom profile.
- Missing `mask_size` falls back directly to `0.82` in both UI and DLL. Do not reintroduce
  legacy `mask_vertical`/`mask_horizontal` opening derivations as a fallback.
