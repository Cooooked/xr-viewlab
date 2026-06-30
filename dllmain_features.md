# DLL Feature And Config Reference

This documents what `dllmain.cpp` reads today.

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

### Visor mask keys (native D3D11 direct-write)

The native visor draws a black border outside a kidney/superellipse opening directly into the game's eye
textures at `xrReleaseSwapchainImage`. Shared superellipse math matches `BeanMaskEditor` in the UI.

- `mask_enabled` - `1` enables the visor mask draw. **Gates the D3D11 draw — if 0, nothing draws.**
- `mask_size` - opening size (0.05-1.0).
- `mask_width_scale` / `mask_height_scale` - opening width/height scale.
- `mask_corner` - legacy corner value; runtime converts `visorCurve = 1.0 - mask_corner`.
- `mask_left_bias` / `mask_right_bias` - X offset of the opening (legacy bridge to `visorOffsetX`).
- `mask_top_bias` / `mask_bottom_bias` - Y offset (legacy bridge to `visorOffsetY`).
- `mask_top_curve` / `mask_bottom_curve` / `mask_offset_y` / `mask_rounded` - legacy, still read.
- `visibility_mask_visor` - OPTIONAL, default `0`. Enables the SEPARATE legacy path that reshapes the
  runtime hidden-area mesh into the visor via `xrGetVisibilityMaskKHR`. Never suppresses the D3D11 path.
- `test_quad` - DEBUG, default `0`. Draws a solid head-locked blue `XrCompositionLayerQuad` 1 m in front
  of the face as its own layer (independent of game textures and of `mask_enabled`). Used to prove that
  OpenXR layer submission reaches the headset.

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
- `mask_enabled` - per-app visor enable (nonzero). The DLL reads this for the active app's profile.
- `mask_size`, `mask_width_scale`, `mask_height_scale`, `mask_corner`, `mask_*_bias`, `mask_*_curve` -
  per-app visor shape values (legacy-bridged into the visor model).

## Hooks

- `xrLocateViews` - edits reported FOV tangents; also drives config hot-reload.
- `xrEnumerateViewConfigurationViews` - edits recommended render dimensions unless visual-mask-only mode is active.
- `xrGetVisibilityMaskKHR` - outer-edge stencil filtering; OPTIONAL visor-mesh reshape if `visibility_mask_visor=1`. Informational only otherwise; does NOT disable the D3D11 visor.
- `xrCreateSession` / `xrDestroySession` - capture the D3D11 device/context; init/teardown the mask renderer.
- `xrCreateSwapchain` / `xrEnumerateSwapchainImages` / `xrAcquireSwapchainImage` / `xrDestroySwapchain` -
  track eye swapchains (textures, dimensions, array size, format, last-acquired index).
- `xrReleaseSwapchainImage` - **draws the visor border** into the just-rendered eye image, then forwards
  the release. This is the primary visor draw point (last legal app-side write before composition).
- `xrEndFrame` - captures per-eye layout (imageRect + array slice) from the projection layer for the next
  frame's release-time draw; appends the debug test quad when `test_quad=1`.
- `xrCreateReferenceSpace` - captured (used to create the test quad's head-locked VIEW space).
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
