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
| `crop_outer_edges_only` | 1 | `cropOuterEdgesOnly` | — | **Permanently enabled** — config key ignored. Horizontal crop takes from outer edges only. |
| `foveated_center_compensation` | 1 | `foveatedCenterCompensation` | dword | **Permanently enabled** — config key ignored. |
| `visual_mask_only`, `horizontal_visual_mask_only` | 0 | same names | — | mask instead of crop (loses GPU savings); Edge Masks popup "Both" controls write these keys |
| `render_scale` | 0.1..3 / 1.0 | `renderScale` | ×1e6 dword | per-game supersampling |

## Visor mask

| ini key | range/default | DLL global | per-app | UI control |
|---|---|---|---|---|
| `mask_enabled` | 0 | `maskEnabled` | global-only (per-app enable deliberately ignored — DECISIONS D6) | Visor mask checkbox |
| `mask_size` | 0.1..1 / **1.0** | `visorSize` | `visor_size` millis | Uniform visor-opening scale. `1.0` preserves the existing full opening; smaller values hide an outer band without changing crop/FOV/resolution. |
| `mask_corner` | 0..1 | `visorCurve = 1 − maskCorner` | `mask_corner` millis | Curve slider (stored inverted). Near zero stays visually square through one continuous curve; Inner low remains active. |
| `mask_outer_apex_y` | −0.5..0.5 / 0 | `visorOuterApexY` | signed millis | Apex Y slider + red pin |
| `mask_inner_lower_y` | 0..0.666 / 0 | `visorInnerLowerY` | millis | Inner low slider + orange pin |
| `mask_inner_bridge_width` | 0..1 / 0.5 | `visorInnerBridgeWidth` | millis | Bridge slider + blue pin |
| `mask_inner_bridge_rise` | −0.5..1 / 0 | `visorInnerBridgeRise` | legacy millis plus extended marker encoding for per-app profiles | Rise slider |
| `mask_inner_bridge_peak_x` | −1..2 / 0.5 | `visorInnerBridgePeakX` | legacy millis 0..1000; extended marker encoding for per-app profiles | Peak X slider; moving left carries the peak farther from the binocular centre, mirrored by eye. |
| `mask_inner_bridge_steepness` | −1..2 / 0.5 | `visorInnerBridgeSteepness` | legacy millis plus extended marker encoding for per-app profiles | Steepness slider |
| `visor_live_revision` | monotonic timestamp | `liveVisorRevision` | — | Internal commit marker. The UI writes it last after global visor controls, allowing a safe live visor-only refresh at `xrEndFrame`. |
| `mask_width_scale` / `mask_height_scale` | 1.0 | `visorWidth`/`visorHeight` | `visor_width`/`visor_height` | Fixed at 1.0; the visor mask always fills the crop opening and only affects corners. |
| `visor_technique` | `c` | `visorTechnique` | — | a/b hidden; DirectWrite is the product path |
| `visor_hd` | 0 | — | — | **Removed** — code disabled; key ignored. |
| `visor_antialiasing` | 0 | — | — | **Removed** — code disabled; key ignored. |

## Stencil / visibility mask

| ini key | default | DLL global | Notes |
|---|---|---|---|
| `stencil_outer_edges_only` | 1 | `outerEdgeVisibilityMaskOnly` | **Permanently enabled** — config keys ignored. Legacy fallback `outer_edge_visibility_mask_only` also ignored. Drives the 3-part stencil pipeline (ARCHITECTURE) |
| `visibility_mask_visor` | 0 | ignored/retired | retired legacy hidden-mesh reshaper; `1` is logged and ignored |

## Diagnostics / misc

| ini key | default | DLL global |
|---|---|---|
| `verbose_logging` | 0 | `verboseLogging` |
| `crop_experiment_mode` | — | — | **Removed 2026-07-11** (experimental crop-fix purge) — code and UI gone; key ignored. Root cause of the edge smear was VD fixed foveated encoding: `docs/FIXED_FOVEATION.md`. |
| `edge_smear_fix`, `edge_smear_pixels` | — | — | **Removed 2026-07-11** — edge-guard code deleted; keys ignored. |
| `lod_popin_fix` | 0 | — | **Removed** — code disabled; key ignored. |

Dead keys (removed 4.1.65, do not resurrect): `mask_vertical`/`mask_horizontal` as opening source,
`mask_rounded`, `mask_offset_y`, `mask_*_bias`, `mask_*_curve` as visor inputs, `reshade_hmd_menu`,
`reshade_desktop_duplicate`, `reshade_3d_menu`. Some still exist as read-fallbacks in the current
4.1.55-derived `LoadConfig`; treat them as legacy compat only.

UI-only keys: window size/column layout keys, `stencil_outer_edges_only` also gates preview mode +
inner-low slider enablement. ReShade Remote state lives in ProgramData (shared-memory block +
`openxr_quad_transform.ini`), not in this ini.

## 2026-07-10 implementation update

- MSI install/upgrade preserves the live `%LOCALAPPDATA%` configuration and per-app registry
  profiles. The packaged ini is a fresh-install template only; neither the installer nor the
  OpenXR layer resets user tuning during an ordinary upgrade.
- The bundled `xr-viewlab.ini` must include every product key with safe defaults, including
  `mask_enabled=0`, `mask_size=1.0`, `mask_corner=0.5`, `visor_hd=0`, and
  `visor_antialiasing=0`.
- `mask_size=1` is the compatibility default and preserves the previous full opening. Reducing it
  only masks more of the already-rendered image; it never changes crop tangents, submitted FOV,
  or recommended render resolution.
- `visor_hd` is now native: it doubles visor curve tessellation. `visor_antialiasing` is now
  native: it enables a per-vertex alpha feather strip on the visor aperture edge.
- Per-app profiles now carry all six shape keys: `mask_outer_apex_y`, `mask_inner_lower_y`,
  `mask_inner_bridge_width`, `mask_inner_bridge_rise`, `mask_inner_bridge_peak_x`, and
  `mask_inner_bridge_steepness`. Visor enable remains global-only and the UI deletes any stale
  per-app `mask_enabled` override when saving a custom profile.
- Missing `mask_size` falls back directly to `1.0` in both UI and DLL. Do not reintroduce
  legacy `mask_vertical`/`mask_horizontal` opening derivations as a fallback.
- `visibility_mask_visor` no longer changes runtime geometry. The Direct C visor is the product
  path; the old hidden-mesh reshaper cannot represent current shape/AA/HD behaviour.
