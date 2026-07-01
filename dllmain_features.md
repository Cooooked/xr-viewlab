# DLL Feature And Config Reference

This documents what `dllmain.cpp` reads today.

## 4.1.55 Notes

- Vertical/horizontal crop remains active in `xrLocateViews`; no visor or experimental option disables
  the GPU-saving crop path.
- Release-time D3D11 visor/edge drawing caches runtime swapchain RTVs per tracked image/slice instead of
  creating render-target views for every eye draw.
- High-segment visor geometry builders use fixed stack arrays for curve points instead of heap vectors.
- `edge_smear_fix` draws black guard pixels into projection texture edges. It does not inset submitted
  `imageRect` values.
- `lod_popin_fix` is diagnostic-only in 4.1.55 and preserves normal ViewLab crop.
- `visor_technique` supports `a`, `b`, `c`, and `off`. A is a BOTH-eye OpenXR quad overlay with
  open-inner left/right artwork; B is D3D11 colour non-MSAA swapchain interception; C is release-time plus
  late-`xrEndFrame` direct write.
- X/Y visor offsets are no longer product controls. The UI writes zero and the DLL ignores stale
  global/per-app X/Y bias values.

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
- `edge_smear_fix` - experimental, default `0`. When enabled, the layer draws black guard pixels into
  projection texture edges at release/late-end-frame.
- `edge_smear_pixels` - optional tuning value for `edge_smear_fix`; default `2`, clamped `1..16`.
- `lod_popin_fix` - experimental, default `0`. Diagnostic-only in 4.1.55; it logs that the unsafe
  full-FOV/stretch path is disabled and preserves the normal ViewLab crop.

### Visor mask keys (native D3D11 direct-write)

The native visor draws a black border outside a kidney/superellipse opening directly into the game's eye
textures at `xrReleaseSwapchainImage`. Shared superellipse math matches `BeanMaskEditor` in the UI.

- `mask_enabled` - `1` enables the visor mask draw. **Gates the D3D11 draw — if 0, nothing draws.**
- `mask_size` - opening size (0.05-1.0).
- `mask_width_scale` / `mask_height_scale` - opening width/height scale.
- `mask_corner` - legacy corner value; runtime converts `visorCurve = 1.0 - mask_corner`.
- `mask_left_bias` / `mask_right_bias` - legacy X offset values. Ignored by the DLL in 4.1.55.
- `mask_top_bias` / `mask_bottom_bias` - legacy Y offset values. Ignored by the DLL in 4.1.55.
- `mask_top_curve` / `mask_bottom_curve` / `mask_offset_y` / `mask_rounded` - legacy, still read.
- `visibility_mask_visor` - OPTIONAL, default `0`. Enables the SEPARATE legacy path that reshapes the
  runtime hidden-area mesh into the visor via `xrGetVisibilityMaskKHR`. Never suppresses the D3D11 path.
- `test_quad` - REMOVED in 4.1.47 (it was a debug blue quad that proved layer submission works; job done).
  The config key is ignored if present.
- `visor_technique` - selects the masking mechanism (gated by `mask_enabled`; also selectable via the UI
  Technique radio). Values:
  Current 4.1.55 values: `a`/`quad` = OpenXR quad overlay, `b`/`intercept`/`interception` =
  app-facing swapchain interception/composite, `c` = direct write, `off` = no visor.
  Authoritative 4.1.55 detail: A draws open-inner left/right artwork into a BOTH-eye alpha quad; B only
  intercepts D3D11 colour non-MSAA swapchains and bypasses unsupported ones; C draws both at release and
  late `xrEndFrame`. Treat older C/VDXR notes below as historical caution, not current implementation.
  - `a` / `quad` — head-locked alpha-cutout visor as our OWN composition layer. Currently a single
    BOTH-eye `XrCompositionLayerQuad`, FOV-sized (per-eye LEFT/RIGHT quads did NOT render on VDXR).
    PRIMARY path. Independent of the game's swapchains.
  - `c` (default) — direct write of the border into the game's eye texture at `xrReleaseSwapchainImage`
    (+ a `Flush()`). Draws correctly (logs `OK draw executed`) but does NOT appear on VDXR/streaming
    runtimes. Use for native runtimes only.
  - `off` — no visor (crop only).
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
- `mask_size`, `mask_width_scale`, `mask_height_scale`, `mask_corner`, `mask_*_bias`, `mask_*_curve` -
  per-app visor shape values (legacy-bridged into the visor model).

## Hooks

- `xrLocateViews` - edits reported FOV tangents; also drives config hot-reload.
- `xrEnumerateViewConfigurationViews` - edits recommended render dimensions unless visual-mask-only mode is active.
- `xrGetVisibilityMaskKHR` - outer-edge stencil filtering; OPTIONAL visor-mesh reshape if `visibility_mask_visor=1`. Informational only otherwise; does NOT disable the D3D11 visor.
- `xrCreateSession` / `xrDestroySession` - capture the D3D11 device/context; init/teardown the mask renderer.
- `xrCreateSwapchain` / `xrEnumerateSwapchainImages` / `xrAcquireSwapchainImage` / `xrDestroySwapchain` -
  track eye swapchains (textures, dimensions, array size, format, last-acquired index).
- `xrReleaseSwapchainImage` - draws edge guard bands, B compositing, and C direct-write visor into the
  just-rendered eye image before forwarding the release.
- `xrEndFrame` - captures per-eye layout (imageRect + array slice + FOV), performs a late C/edge-guard
  draw for OpenComposite timing paths, and appends the Technique A quad layer when selected.
- `xrCreateReferenceSpace` - captured/used for Technique A's head-locked VIEW space.
- `xrCreateApiLayerInstance` - initializes layer state and logs app/extensions.

## Diagnostics

One-shot `DIAG` lines in `ViewLab.log` trace each stage (hook called, projection layer found, swapchain
tracked, release reached, RTV created, draw executed, or the specific FAIL). Set `verbose_logging=1` for
per-frame detail in `ViewLab.verbose.log`.

## ReShade MENU Keys

The DLL currently does not read these keys:

- `reshade_hmd_menu`
- `reshade_desktop_duplicate`
- `reshade_3d_menu`

Those are WPF UI state keys for the ReShade MENU section. If the native layer later owns ReShade menu drawing, add the reads here and update this file in the same commit.
