# OpenXR Vertical Tangent

Small OpenXR API layer for reducing vertical FOV render cost with independent top and bottom screen-share values.

This build is tangent-only. The experimental overlay/black-bar work has been removed.

## Settings

Default config:

```ini
[Settings]
enabled=1
top_tangent=0.200
bottom_tangent=0.200
```

The values are final screen-share budgets:

- Default `top_tangent=0.200` and `bottom_tangent=0.200` gives `40%` total render height, centered.
- `top_tangent=0.090` and `bottom_tangent=0.090` gives `18%` total render height, centered.
- `top_tangent=0.000` and `bottom_tangent=0.180` gives `18%` total render height, shifted downward.

Internally the tool converts each side to a per-side FOV scale:

- `top scale = top_tangent * 2`
- `bottom scale = bottom_tangent * 2`

So `0.09 / 0.09` becomes `0.18 / 0.18` per-side scaling, while the requested render height is still `0.09 + 0.09 = 0.18`.

## What It Does

The layer hooks:

- `xrLocateViews` and scales the original top/bottom FOV independently.
- `xrEnumerateViewConfigurationViews` and scales requested render height by `top_tangent + bottom_tangent`.

It does not touch OpenXR overlay layers, quad positions, capture paths, or black-bar rendering.
