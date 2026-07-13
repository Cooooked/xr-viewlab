# Performance HUD validation

## Deterministic and desktop checks

- Run `Tests/Verify-ViewLabContracts.ps1` and the HUD-specific contract tests.
- Verify old INI files with no modular keys load CPU/GPU/APP/VR in that order.
- Exercise all 16 widget-enable masks; enabled widgets must pack contiguously and mask 0 draws none.
- Exercise all 24 widget-order permutations and verify the immutable snapshot carries the same order
  to both eyes.
- Check HUD scale at minimum, 0.5, 1.0, and 3.0 with one through four widgets; no icon/text overlap.
- Exercise every graph mode/channel combination; incompatible units must not share a scale.
- Confirm history never exceeds 600 samples and unavailable channels draw no fabricated zero line.

## Refresh and cadence matrix

| Runtime target | Input cadence | Expected state |
|---|---|---|
| 72 Hz / 13.89 ms | 13.8–14.0 ms | On target |
| 80 Hz / 12.50 ms | 12.4–12.6 ms | On target |
| 90 Hz / 11.11 ms | 11.0–11.2 ms | On target |
| 120 Hz / 8.33 ms | 8.2–8.4 ms | On target |
| 120 Hz / 8.33 ms | 11.0–11.2 ms | Missed/unstable, never green |
| 120 Hz / 8.33 ms | 16.5–16.8 ms sustained | Stable 1/2 reprojection |
| 90 Hz / 11.11 ms | 22.1–22.4 ms sustained | Stable 1/2 reprojection |

For each row, verify state entry requires sustained samples, harmless jitter does not flicker, and
recovery uses lower thresholds plus hold time.

## Manual headset order

1. Pistol Whip, Topmost off: verify CPU/GPU behaviour, APP stability, VR target state, every widget
   toggle, packing, order, minimum scale, and graph channels.
2. Repeat a short static-menu test with Topmost on. Abort on any Topmost safety log, stream flicker,
   repeated allocation, or device-loss message; do not retry in that session.
3. DiRT Rally menus only: verify stereo fusion, identical values/history in both eyes, and normal
   shutdown. Preserve ViewLab, VDXR, and ReShade logs.
4. The DiRT menu-to-car transition remains the final user-authorised test. Abort on stream freeze,
   monitor reconnect, DWM flicker, `XR_ERROR_RUNTIME_FAILURE`, or `d3d11 safety` logging.

CPU and GPU should match the previous build under equivalent load. APP should remain comparatively
stable in a stable scene and must not alarm from time spent inside runtime submission. No automated
VR launch is permitted.
