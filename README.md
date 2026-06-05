# OpenXR Vertical Tangent

Small OpenXR API layer for reducing vertical FOV render cost with either one total screen-share value or independent top and bottom screen-share values.

This build is tangent-only. The experimental overlay/black-bar work has been removed.

## Settings

Default config:

```ini
[Settings]
enabled=1
split_mode=0
vertical_tangent=0.400
top_tangent=0.200
bottom_tangent=0.200
```

`split_mode=0` uses the single total value:

- `vertical_tangent=0.180` gives `18%` total render height, centered.
- Default `vertical_tangent=0.400` gives `40%` total render height, centered.

`split_mode=1` uses the separate top and bottom values:

- `top_tangent=0.200` and `bottom_tangent=0.200` gives `40%` total render height, centered.
- `top_tangent=0.090` and `bottom_tangent=0.090` gives `18%` total render height, centered.
- `top_tangent=0.000` and `bottom_tangent=0.180` gives `18%` total render height, shifted downward.

Internally the tool always converts to top and bottom values first:

- total mode: `top = vertical_tangent / 2`, `bottom = vertical_tangent / 2`
- split mode: `top = top_tangent`, `bottom = bottom_tangent`

Then it converts each side to a per-side FOV scale:

- `top scale = top * 2`
- `bottom scale = bottom * 2`

So split `0.09 / 0.09` becomes `0.18 / 0.18` per-side scaling, while the requested render height is still `0.09 + 0.09 = 0.18`.

## What It Does

The layer hooks:

- `xrLocateViews` and scales the original top/bottom FOV independently.
- `xrEnumerateViewConfigurationViews` and scales requested render height by the final total screen share.

It does not touch OpenXR overlay layers, quad positions, capture paths, or black-bar rendering.
