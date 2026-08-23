# ViewLab

ViewLab is an OpenXR API layer plus a Windows settings app for tuning what your VR headset renders,
drawing overlays inside the headset, and getting a clean capture of the result into OBS.

It started as a vertical render-height tool and still does that: it changes the FOV reported through
OpenXR and reduces the recommended render resolution, so the GPU draws fewer pixels. That is
different from cropping the picture afterwards — in compatible games ViewLab asks for less render
area *before* the frame is drawn.

Everything else grew around that: a visor mask, an in-headset performance HUD, notifications, racing
cues, a bundled ReShade payload, and ViewLab's own OBS capture path.

## Download

[ViewLab Releases](https://github.com/Cooooked/xr-viewlab/releases)

The MSI is unsigned, so Windows SmartScreen may warn until the download gains reputation.

## Features

### Render tuning

- **FOV and resolution crop.** Narrows the vertical (and optionally horizontal) render area reported
  to the game. Total mode uses one value; split mode takes separate top and bottom values so you can
  shift the rendered slice up or down.
- **Visor mask.** A black border drawn directly into the game's eye textures, with an adjustable
  shape — aperture width and size, apex height, inner/nose bridge, curve. It bounds what you *see*
  without changing what is rendered, and costs no extra GPU.
- **Per-application profiles.** Global settings plus per-game overrides, with per-overlay "use global
  values" inheritance.

### In-headset overlays

- Performance HUD (GPU/CPU/system/app metrics) and a performance trace with a session graph browser
- Clock and OpenXR session timer
- Desktop notifications and music track-change cards
- Sticky notes
- Crosshair
- iRacing cues — spotter glow, flag border, race start light, rear-closing pressure, grip bar
- OBS recording cue

Every overlay can be shown or hidden independently in the headset and in the capture.

### ViewLab Media Capture (OBS)

ViewLab's own OBS source. Because ViewLab is registered last in the OpenXR layer chain, it sees the
finished frame — so the capture includes things a generic mirror misses:

- the OpenXR scene
- ViewLab's own overlays
- ReShade
- other OpenXR overlay layers submitted above ViewLab, such as RaceLab and OpenKneeboard

Source properties: eye selection (left/right), a "Display overlay layers" toggle, four percentage
crop sliders, and a Reinitialize button.

Two companion OBS filters ship alongside it, installed from the app's **OBS** menu:

| Filter | Does |
|---|---|
| **ViewLab Enhancer** | Image adjustment only — sharpness, saturation, vibrance, contrast, brightness, gamma |
| **ViewLab Stabilizer** | Motion stabilization (shake, roll, zoom), built on [LiveVisionKit](https://github.com/Crowsinc/LiveVisionKit) |

> The OBS plugins share a versioned contract with the OpenXR layer. Update them together — after
> installing a new ViewLab build, reopen the **OBS** menu and use the install buttons, or the source
> will not connect.

### ReShade

A ReShade OpenXR payload is bundled, with a ReShade Remote popout in the app for controlling it
without taking the headset off.

## Settings

The layer is registered globally with OpenXR, because that is how implicit API layers are
discovered. Per-application enable/disable happens inside the layer: if an app is unchecked, the
layer bypasses itself for that app.

### Application list

Launch an OpenXR game once, then reopen ViewLab or press **Reload app list**.

- Checked: ViewLab is enabled for that application.
- Unchecked: ViewLab bypasses that application.
- **Use custom values for selected app**: saves a per-game profile instead of using the global values.

Useful for keeping different setups per game — a low narrow view for iRacing, a centred view for
DCS/MSFS, a taller view for SkyrimVR.

### View modes

**Total mode** uses one value for the full vertical render height (`0.40` = 40% total, centred).
This is the default.

**Split mode** uses separate top and bottom values:

- `0.20` top + `0.20` bottom = 40% total, centred
- `0.10` top + `0.10` bottom = 20% total, centred
- `0.00` top + `0.20` bottom = 20% total, shifted downward

Default config:

```ini
[Settings]
enabled=1
split_mode=0
total_render_height=0.400
top_tangent=0.200
bottom_tangent=0.200
```

Live config lives at `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini`.

### Layout modes

The settings window reflows by width.

| Mode | Width | Description |
|------|-------|-------------|
| **Mini** | < 360 px | Single column, sliders compress, footer items equally spaced |
| **Small** | 360–599 px | Single column, sliders full width with labels and hints |
| **Medium** | 600–899 px | Two columns — sliders + options left, apps table + ReShade menus right |
| **Large** | ≥ 900 px | Three columns — sliders left, apps table centre, options + ReShade menus right |

## Notes

- Values are clamped between `0.000` and `1.000`; total render height is clamped to at least `0.010`.
- A game must respect OpenXR recommended image sizes for the render-height saving to apply.
- ViewLab targets compatibility by capability, not by an allowlist — it never classifies a game as
  unsupported. Missing capabilities degrade individual features with explicit diagnostics.

## Current version

See [CHANGELOG.md](CHANGELOG.md) and the [releases page](https://github.com/Cooooked/xr-viewlab/releases).

## Lineage and thanks

ViewLab stands on work from the OpenXR community:

- [fommil/openxr-widescreen](https://github.com/fommil/openxr-widescreen), which continued and adapted the OpenXR FOV modifier idea.
- [mbucchia/_ARCHIVE_XR_APILAYER_NOVENDOR_fov_modifier](https://github.com/mbucchia/_ARCHIVE_XR_APILAYER_NOVENDOR_fov_modifier), the archived API-layer/FOV modifier foundation.
- [mbucchia/OpenXR-Toolkit](https://github.com/mbucchia/OpenXR-Toolkit), used as a reference for the companion-app style and per-application enable/profile behaviour.
- [Jabbah/OpenXR-Layer-OBSMirror](https://github.com/Jabbah/OpenXR-Layer-OBSMirror) (MIT), whose OBS mirror layer is the reference for ViewLab Media Capture — the overlay quad compositing and crop behaviour are derived from it.
- [Crowsinc/LiveVisionKit](https://github.com/Crowsinc/LiveVisionKit) (GPL-3.0), which powers the ViewLab Stabilizer filter.

ViewLab is its own tool, but it is very much part of that chain.
