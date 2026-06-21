# XR ViewLab

XR ViewLab is a small OpenXR API layer for tuning VR view/render behaviour.

The current build focuses on vertical render-height control. It changes the vertical FOV reported through OpenXR and reduces the recommended render height requested by the application. The goal is lower GPU render cost in VR, especially for sims where a narrower vertical view can still keep the useful cockpit/driving/flying area visible.

This is different from a simple visible FOV crop. In compatible games, XR ViewLab asks OpenXR for less vertical render area before the frame is drawn, so the GPU has fewer pixels to render.

## Download

Download the latest MSI from the releases page:

[XR ViewLab Releases](https://github.com/Cooooked/xr-viewlab/releases)

## Settings

The settings app has global settings and optional per-application profiles.

The layer is still registered globally with OpenXR, because that is how implicit API layers are discovered. Per-application enable/disable is handled inside the layer: if an app is unchecked in XR ViewLab, the layer bypasses itself for that app.

### Application List

Launch an OpenXR game once, then reopen XR ViewLab or press **Reload app list**.

- Checked app: XR ViewLab is enabled for that application.
- Unchecked app: XR ViewLab bypasses that application.
- **Use custom values for selected app**: saves a per-game profile instead of using the global values.

This is useful for keeping different view setups per game, for example a low narrow view for iRacing, a centered view for DCS/MSFS, or a taller view for SkyrimVR.

### View Modes

Global settings and app profiles both support the same two view modes.

### Total Mode

Use one value for the full vertical render height.

- `0.40` means 40% total render height, centered.
- `0.20` means 20% total render height, centered.

This is the default mode.

### Split Mode

Use separate top and bottom values when you want to move the rendered slice up or down.

- `0.20` top + `0.20` bottom = 40% total render height, centered.
- `0.10` top + `0.10` bottom = 20% total render height, centered.
- `0.00` top + `0.20` bottom = 20% total render height, shifted downward.

Default config file:

```ini
[Settings]
enabled=1
split_mode=0
total_render_height=0.400
top_tangent=0.200
bottom_tangent=0.200
```

## What It Does

XR ViewLab narrows the vertical area the game asks OpenXR to render.

This is not just hiding part of the picture after the game has already rendered it. In compatible games, XR ViewLab asks for fewer vertical pixels up front, which can reduce GPU render time.

For example:

- Total mode `0.20` asks for about 20% vertical render height, centered.
- Split mode `0.10` top + `0.10` bottom does the same thing.
- Split mode `0.00` top + `0.20` bottom keeps the same size but moves the view downward.

## Notes

- Values are clamped between `0.000` and `1.000`.
- Total render height is clamped to at least `0.010`.
- A game must respect OpenXR recommended image sizes for the render-height saving to apply.
- The MSI is unsigned, so Windows SmartScreen may warn until the download gains reputation or the installer is code-signed.

## Lineage And Thanks

XR ViewLab stands on work from the OpenXR community:

- [fommil/openxr-widescreen](https://github.com/fommil/openxr-widescreen), which continued and adapted the OpenXR FOV modifier idea.
- [mbucchia/_ARCHIVE_XR_APILAYER_NOVENDOR_fov_modifier](https://github.com/mbucchia/_ARCHIVE_XR_APILAYER_NOVENDOR_fov_modifier), the archived API-layer/FOV modifier foundation.
- [mbucchia/OpenXR-Toolkit](https://github.com/mbucchia/OpenXR-Toolkit), used as a reference for the companion-app style and per-application enable/profile behaviour.

XR ViewLab is its own tool, but it is very much part of that chain.
