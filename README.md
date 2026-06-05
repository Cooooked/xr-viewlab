# OpenXR Vertical Tangent

Small OpenXR API layer for reducing vertical FOV render cost with independent top and bottom tangent values.

This build is tangent-only. The experimental overlay/black-bar work has been removed.

## Download

Grab the MSI from `dist/OpenXR-Vertical-Tangent-2.1.0.msi`.

## Settings

Default config:

```ini
[Settings]
enabled=1
top_tangent=0.090
bottom_tangent=0.090
```

The values are final per-side tangent budgets:

- `top_tangent=0.090` and `bottom_tangent=0.090` gives a centered total vertical tangent of `0.180`.
- `top_tangent=0.000` and `bottom_tangent=0.180` gives the same total vertical tangent, shifted downward.

Values are clamped from `0.000` to `1.000`.

## What It Does

The layer hooks:

- `xrLocateViews` and sets:
  - `angleUp = atan(top_tangent)`
  - `angleDown = -atan(bottom_tangent)`
- `xrEnumerateViewConfigurationViews` and scales recommended render height by `top_tangent + bottom_tangent`.

It does not touch OpenXR overlay layers, quad positions, capture paths, or black-bar rendering.