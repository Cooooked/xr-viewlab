# DLL Feature And Config Reference

This documents what `dllmain.cpp` reads today.

## 4.1.68 Notes

- Mask edges are antialiased via 4-pass jittered supersampling, not geometry/tessellation: each
  `DrawVisorBorderToTexture` call now draws the same triangles 4 times with a small sub-pixel offset
  (from a `JitterCB` cbuffer bound to both VS and PS, register b0) and 1/4 alpha each, blended
  (`SrcBlend=SRC_ALPHA, DestBlend=INV_SRC_ALPHA`, blend was previously disabled). Do not disable
  blending or drop back to a single pass without re-adding some other AA method — the mask geometry
  itself is still hard-edged; the smoothing comes entirely from the multi-pass accumulation.
- `RenderVisorCutout` (Technique A, hidden/historical) shares the same VS/PS and therefore also needs
  `JitterCB` bound — it uses a single neutral pass (offset=0, alpha=1) to preserve its old fully-opaque
  border, not the 4-pass AA (that path isn't part of the antialiasing fix).
- Inner-low (NRI) nose-bridge is now a **two-segment** curve (superseding the single quarter-ellipse
  from 4.1.66/67): `BuildNoseBridgeCurve(startX,startY,endX,endY,bridgeWidth,exp,...)` builds segment A
  (rise, from the crescent's bottom pinch point to a junction J) and segment B (flat bridge, from J to
  the nose edge). J's position along the start->end line is controlled by `mask_inner_bridge_width`
  (0..1, ini + per-app registry, default 0.5): 0 = J at the start (no bridge, sharp corner like the old
  behavior), 1 = J at the end (long flat bridge, short/early rise). Both segments always meet with a
  purely vertical tangent at J and segment B always ends with a purely horizontal tangent at the nose
  edge, by construction of the superellipse parametrization — do not "fix" a perceived kink by adding
  manual continuity code, the join is already C1 for any J. Implemented identically in
  `BuildOpenInnerEyeVisorVerts` (DLL) and `BeanMaskEditor.AddNoseBridgeCurve` (UI).

## 4.1.67 Notes

- Curve mapping (superseding 4.1.66): slider (0..1) -> exponent is a **geometric** interpolation
  `32 * (2/32)^curve`, i.e. exp=32 at curve=0 (flat) down to exp=2 at curve=1 (full lemon/banana
  extreme). curve=0.5 lands at exp=8. Shared between `VisorCurveExponent()` (DLL) and
  `BeanMaskEditor.CurveExponent` (UI) — must stay identical.
- Inner-low (NRI) nose-bridge corner is a quarter-superellipse from the crescent's bottom pinch point
  to the nose edge at the top of the inner-low band, using the same exponent as the outer curve:
  `x = cx + (innerX-cx)*sin(t)^(2/exp)`, `y = y0 + (bandTopY-y0)*(1-cos(t)^(2/exp))` for
  `t in [0, pi/2]`. This moves toward the nose edge first (while pinned near y0) and rises last —
  "right, then up". Do not go back to easing X and Y independently; that reintroduces an "up, then
  sideways" spike. Implemented identically in `BuildOpenInnerEyeVisorVerts` (DLL) and
  `BeanMaskEditor.AddOpenInnerHalf` (UI).

## 4.1.66 Notes

- Fixed the launch crash in games without d3dcompiler_47 already loaded (e.g. Pistol Whip):
  `InitD3D11MaskRenderer` no longer frees d3dcompiler while its `ID3DBlob`s are still alive.
- Y convention: config `mask_outer_apex_y` / `mask_inner_lower_y` are authored in the UI preview's
  y-DOWN space (positive apex = visually lower; inner-low band = visual bottom). Native geometry is
  built in y-UP D3D NDC; the flip happens exactly once, in `ApexYFromConfigNdc()`. Do not add
  another sign flip anywhere else.
- Curve mapping: slider (0..1) -> superellipse exponent is `32 - 25*curve` (max slider = exp 7),
  shared between `VisorCurveExponent()` in the DLL and `BeanMaskEditor.CurveExponent` in the UI.
  These two must stay identical.
- `hook installed/captured` lines log once per process (first resolution only).

## 4.1.64 Notes

- Vertical/horizontal crop remains active in `xrLocateViews`; no visor or experimental option disables
  the GPU-saving crop path.
- Release-time D3D11 visor/edge drawing caches runtime swapchain RTVs per tracked image/slice instead of
  creating render-target views for every eye draw.
- High-segment visor geometry builders use fixed stack arrays for curve points instead of heap vectors.
- `edge_smear_fix` applies four gated compositor-contract mitigations: submitted projection-layer FOV is
  matched to the cropped FOV returned by ViewLab, invalid/out-of-bounds submitted `subImage.imageRect`
  values are clamped to tracked swapchain bounds, recommended image dimensions are aligned to 4 pixels
  where possible, and recommended size scaling can use original runtime FOV when known. It does not draw
  guard pixels, blur, overscan, or black strip cover-ups.
- `edge_smear_fix` FOV comparison diagnostics are verbose-only and rate-limited.
- `edge_smear_fix` verbose diagnostics also include original runtime FOV, `subImage.imageRect`, tracked
  swapchain size/format, latest recommended image size, crop settings, visibility-mask state, and visor
  state.
- `crop_outer_edges_only`, `visual_mask_only`, and `horizontal_visual_mask_only` are aligned between UI
  and DLL. Only the "Both sides" Edge Masks controls map to those DLL keys; one-sided controls are
  disabled until a real one-sided runtime path exists.
- `lod_popin_fix` is diagnostic-only in 4.1.64 and preserves normal ViewLab crop.
- `visor_technique` is forced to C in 4.1.64. A/B source paths remain present but are hidden/bypassed for
  product use. Direct C writes at release time, with late-`xrEndFrame` as a fallback only when the release
  draw did not cover the swapchain in the current frame. `mask_enabled` is the off switch.
- Verbose edge-smear contract diagnostics emit a rate-limited baseline even when `edge_smear_fix=0`, and
  include release/late-draw provenance plus `render_scale`.
- Width/height visor scale and X/Y visor offsets are no longer product controls. The UI writes neutral
  values and the DLL ignores stale global/per-app values.
- 4.1.64 Direct C shape controls: `mask_outer_apex_y` moves the outer curve apex up/down, and
  `mask_inner_lower_y` adds a bottom-only inner-eye stencil curve when open-inner mode is active. These
  are global and per-app profile values, are draggable in the UI preview, and are included in verbose
  edge-smear contract diagnostics.
- Edge-smear/render-stretch is still unresolved. Do not infer success from clean logs or a build.

## Config File

Primary config filename:

`xr-viewlab.ini`

The layer reads from the installed/process config path resolved by `ConfigPath()` in `dllmain.cpp`.

## Global INI Keys

All keys are under `[Settings]`.

- `enabled` - `1` enables the layer globally; `0` bypasses it.
- `split_mode` - `1` uses separate `top_tangent` and `bottom_tangent`; `0` uses centered total mode.
- `total_render_height` - total vertical render height, `0.18` means 18%.
- `total_share` - legacy total vertical value, still read as fallback.
- `vertical_tangent` - legacy total vertical value, still read as fallback.
- `top_tangent` - top vertical value in split mode.
- `bottom_tangent` - bottom vertical value in split mode.
- `horizontal_render_width` - horizontal render width, `0.80` means 80%.
- `foveated_center_compensation` - adjusts reported vertical FOV so foveated rendering is centered on the rendered vertical slice.
- `visual_mask_only` - keeps full recommended vertical render size while reporting/masking vertical view.
- `horizontal_visual_mask_only` - keeps full recommended horizontal render size while reporting/masking horizontal view.
- `outer_edge_visibility_mask_only` - filters visibility-mask triangles so only outer eye edges are affected.
- `crop_outer_edges_only` - default `1`. When `1`, horizontal crop preserves each eye's inner edge and
  crops only the outer edge. When `0`, horizontal crop is symmetric.
- `horizontal_visual_mask_both` - legacy UI popup key; read as a fallback for
  `horizontal_visual_mask_only`.
- `vertical_visual_mask_both` - legacy UI popup key; read as a fallback for `visual_mask_only`.
- `horizontal_outer_eye_mask` / `horizontal_inner_eye_mask` / `vertical_top_mask_only` /
  `vertical_bottom_mask_only` - unsupported one-sided legacy keys. Ignored by the DLL and cleared by the
  UI.
- `edge_smear_fix` - experimental, default `0`. When enabled, the layer patches projection-layer FOV at
  `xrEndFrame` so the runtime presents the same cropped angular region that `xrLocateViews` returned,
  preserves the runtime visibility mask instead of applying `outer_edge_visibility_mask_only`, clamps only
  invalid/out-of-bounds submitted image rects to swapchain bounds, aligns recommended dimensions to 4
  pixels, and can use original runtime FOV to weight recommended-size scaling.
- `lod_popin_fix` - experimental, default `0`. Diagnostic-only in 4.1.64; it logs that the unsafe
  full-FOV/stretch path is disabled and preserves the normal ViewLab crop.
- `render_scale` - per-game render-resolution multiplier, default `1.0` (range 0.1-3.0). Read
  globally as a hand-edit escape hatch; the product control is the per-app registry value.
- `verbose_logging` - default `0`. Routes per-frame hook detail to `ViewLab.verbose.log`; the
  human-facing `ViewLab.log` only gets one-shot/state-change lines.

### Visor mask keys (native D3D11 direct-write)

The native visor draws a black border outside a kidney/superellipse opening directly into the game's eye
textures at `xrReleaseSwapchainImage`. Shared superellipse math matches `BeanMaskEditor` in the UI.

- `mask_enabled` - `1` enables the visor mask draw. **Gates the D3D11 draw — if 0, nothing draws.**
- `mask_size` - opening size (0.05-1.0).
- `mask_width_scale` / `mask_height_scale` - legacy opening width/height scale. Ignored by the DLL in
  4.1.64; visor aperture follows the submitted crop rect with neutral scale.
- `mask_corner` - legacy corner value; runtime converts `visorCurve = 1.0 - mask_corner`.
- `mask_outer_apex_y` - experimental Direct C open-inner outer-curve apex offset, `-0.5` to `0.5`.
- `mask_inner_lower_y` - experimental Direct C bottom-only inner-eye stencil amount, `0.0` to `0.333`.
- `mask_left_bias` / `mask_right_bias` / `mask_top_bias` / `mask_bottom_bias` /
  `mask_top_curve` / `mask_bottom_curve` / `mask_offset_y` / `mask_rounded` /
  `mask_vertical` / `mask_horizontal` - legacy, no longer read. They fed no render path;
  the UI may still write some of them for back-compat, the DLL ignores them.
- `visibility_mask_visor` - legacy/diagnostic only in 4.1.64. The native Direct C visor path skips the
  legacy visibility-mask reshaper to avoid double boundaries, so this path is effectively unreachable
  while the product visor is active.
- `test_quad` - REMOVED in 4.1.47 (it was a debug blue quad that proved layer submission works; job done).
  The config key is ignored if present.
- `visor_technique` - legacy mechanism selector. Current 4.1.64 runtime forces Direct C regardless of old
  values. `a`/`quad` and `b`/`intercept` remain in source as hidden historical paths. `c` draws the visor
  into the game's eye texture at `xrReleaseSwapchainImage`; late `xrEndFrame` draw is fallback-only.
  NOTE: `mask_size` fallback is 0.82 (a visible opening). A missing `mask_size` must never resolve to 1.0
  (= full opening = invisible mask), which was the root cause of "no mask" through 4.1.45.
  NOTE: visor ENABLE is GLOBAL-only. Per-app profiles no longer carry/override `mask_enabled`; they still
  tune the visor shape (size/width/height/curve/offsets).

## App Registry Keys

Per-app settings live under:

`HKCU\Software\cooooked\xr-viewlab\Apps\<app-key>`

The DLL reads:

- `app_enabled` - `0` bypasses the layer for that app.
- `profile_enabled` - nonzero means use custom app values.
- `split_mode` - app profile split mode.
- `total_render_height` - app profile total vertical value, stored as 0-1000 integer.
- `total_share` - legacy app profile total value.
- `vertical_tangent` - legacy app profile total value.
- `top_tangent` - app top value, stored as 0-1000 integer.
- `bottom_tangent` - app bottom value, stored as 0-1000 integer.
- `horizontal_render_width` - app horizontal value, stored as 0-1000 integer.
- `mask_enabled` - legacy per-app visor enable. Ignored for enable in current builds; visor enable is
  global-only.
- `visor_size`, `mask_corner`, `mask_outer_apex_y`, `mask_inner_lower_y` - per-app visor shape
  values. `visor_width`, `visor_height`, `mask_vertical`, `mask_horizontal`, `mask_rounded`,
  `mask_offset_y`, `mask_*_bias`, and `mask_*_curve` are written by the UI for back-compat but
  ignored (not read) by the DLL.

## Hooks

- `xrLocateViews` - edits reported FOV tangents; also drives config hot-reload.
- `xrEnumerateViewConfigurationViews` - edits recommended render dimensions unless visual-mask-only mode is active.
- `xrGetVisibilityMaskKHR` - outer-edge stencil filtering; OPTIONAL visor-mesh reshape if `visibility_mask_visor=1`. Informational only otherwise; does NOT disable the D3D11 visor.
- `xrCreateSession` / `xrDestroySession` - capture the D3D11 device/context; init/teardown the mask renderer.
- `xrCreateSwapchain` / `xrEnumerateSwapchainImages` / `xrAcquireSwapchainImage` / `xrDestroySwapchain` -
  track eye swapchains (textures, dimensions, array size, format, last-acquired index).
- `xrReleaseSwapchainImage` - performs B compositing and C direct-write visor into the just-rendered eye
  image before forwarding the release; Direct C marks the swapchain as covered for this frame.
- `xrEndFrame` - patches projection-layer FOV when `edge_smear_fix=1`, clamps only invalid/out-of-bounds
  projection image rects to tracked swapchain bounds, logs verbose baseline contract diagnostics when
  enabled, captures per-eye layout (imageRect + array slice + FOV), performs fallback late C drawing only
  for swapchains not already covered at release, and appends the hidden Technique A quad layer only if
  that source path is deliberately selected in code.
- `xrCreateReferenceSpace` - captured/used for Technique A's head-locked VIEW space.
- `xrCreateApiLayerInstance` - initializes layer state and logs app/extensions.

## Diagnostics

One-shot `DIAG` lines in `ViewLab.log` trace each stage (hook called, projection layer found, swapchain
tracked, release reached, RTV created, draw executed, or the specific FAIL). Set `verbose_logging=1` for
per-frame detail in `ViewLab.verbose.log`.

## ReShade MENU Keys

There are no ReShade keys in `xr-viewlab.ini` anymore. The old `reshade_hmd_menu`,
`reshade_desktop_duplicate`, and `reshade_3d_menu` keys were fossils: no code read or wrote them,
so they were removed from the default ini and the dead UI constants were deleted. ReShade Remote
state lives in the `Advanced: ReShade Remote` popout (shared memory + `openxr_quad_transform.ini`
under ProgramData). Note: the UI reads an optional `gameplay_mode` ini key (never written by the
UI; hand-edit only) to pick the ReShade launch xr_mode.
