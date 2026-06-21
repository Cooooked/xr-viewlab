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

## Hooks

- `xrLocateViews` - edits reported FOV tangents.
- `xrEnumerateViewConfigurationViews` - edits recommended render dimensions unless visual-mask-only mode is active.
- `xrGetVisibilityMaskKHR` - filters visibility mask triangles for outer-edge stencil behaviour.
- `xrCreateApiLayerInstance` - initializes layer state and logs app/extensions.

## ReShade MENU Keys

The DLL currently does not read these keys:

- `reshade_hmd_menu`
- `reshade_desktop_duplicate`
- `reshade_3d_menu`

Those are WPF UI state keys for the ReShade MENU section. If the native layer later owns ReShade menu drawing, add the reads here and update this file in the same commit.
