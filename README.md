# OpenXR Vertical Tangent

OpenXR Vertical Tangent is a small OpenXR API layer for reducing vertical render height.

It changes the vertical FOV reported through OpenXR and reduces the recommended render height requested by the application. The goal is lower GPU render cost in VR, especially for sim racing where a narrow vertical view can still keep the useful driving area visible.

This build is tangent-only. It does not modify OpenXR overlays, capture paths, quad-layer positions, or black-bar rendering.

## Download

Download the latest MSI from the releases page:

[OpenXR Vertical Tangent Releases](https://github.com/Cooooked/openxr-verticaltangent/releases)

## Settings

The settings app has two modes.

### Total Mode

Use one value for the full vertical render share.

- `0.40` means 40% total render height, centered.
- `0.18` means 18% total render height, centered.

This is the default mode.

### Split Mode

Use separate top and bottom values when you want to move the rendered slice up or down.

- `0.20` top + `0.20` bottom = 40% total render height, centered.
- `0.09` top + `0.09` bottom = 18% total render height, centered.
- `0.00` top + `0.18` bottom = 18% total render height, shifted downward.

Default config file:

```ini
[Settings]
enabled=1
split_mode=0
vertical_tangent=0.400
top_tangent=0.200
bottom_tangent=0.200
```

## How It Works

The tool converts the selected mode to final top and bottom screen-share values:

- total mode: `top = vertical_tangent / 2`, `bottom = vertical_tangent / 2`
- split mode: `top = top_tangent`, `bottom = bottom_tangent`

It then scales the original OpenXR top and bottom FOV:

- `top scale = top * 2`
- `bottom scale = bottom * 2`

The layer hooks:

- `xrLocateViews` to scale the reported vertical FOV.
- `xrEnumerateViewConfigurationViews` to reduce `recommendedImageRectHeight` by the final total screen share.

For example, split mode with `0.09 / 0.09` gives `0.18` total render height and requests roughly 18% of the original recommended vertical image height.

## Notes

- Values are clamped between `0.000` and `1.000`.
- Total render height is clamped to at least `0.010`.
- A game must respect OpenXR recommended image sizes for the render-height saving to apply.
- The MSI is unsigned, so Windows SmartScreen may warn until the download gains reputation or the installer is code-signed.
