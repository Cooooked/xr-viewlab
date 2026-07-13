# ViewLab release validation

Candidate: **4.1.210** — 2026-07-13

This is the risk-based release gate, not an assertion that unperformed headset work somehow passed
through confidence alone. Automated and desktop evidence are complete for the candidate source.
The safety-critical game/headset matrix remains explicitly pending user observation.

| Area | Status | Evidence / required check |
|---|---|---|
| Repository contracts | Pass | `Verify-ViewLabContracts.ps1`, `Verify-PerformanceHud.ps1` and `Verify-TopmostSafety.ps1` pass. |
| WPF and broker | Pass | 4.1.210 Release publish completed with 0 warnings/errors. |
| Native x64 / Win32 | Pass | Both Release API layers linked with 0 warnings/errors. |
| Identity and installer | Pass | Signed identity MSIX and WiX MSI built; extracted app version, WPF/native/broker hashes, certificate and overlay markers matched fresh outputs. |
| Real Windows notification | Pass, installed broker | Windows listener reports `Allowed`; an independent packaged app's real toast reached the production mapping as one card (fresh repeat: card ID 2). Final headset appearance is pending. |
| Notification permission states | Pass, desktop | Ready/denied/revoked/unsupported/listener failure are explicit; the synthetic button is labelled as a presentation test. |
| iRacing provider fixtures | Pass | Every official spotter state, repeated/stale ticks, inactive/disconnect/reconnect, bad enum/offset, flags, lap validity/PB/delta/session, duplicates and quick stop/start pass. |
| Generic racing bridge | Pass, installed broker | Left/right/both/yellow/lap/clear commands used the production broker, mapping and card path in installed 4.1.204. |
| Bounded technical history | Pass | Corrupt/expired input, 14-day/512-record/512-KiB bounds, UTF-8 size, field limits, body-free schema and clear action pass. |
| Live/replay iRacing | **Pending user** | Use replay, AI or test drive; verify prompt side clearing, flag priority, lap cards and reconnect without completing a competitive race. |
| Pistol Whip regression | **Mandatory first; pending user** | Check visor, 64 px grid plus one radial pattern, all four graph modes, Trace Off/Always/Alarm-only, 12-widget single row at two scales, notification animation, all racing presentation buttons, stereo fusion, gameplay and clean exit. Stop before DiRT on any failure. |
| DiRT Rally 2 menu/car | **Pending user, test last** | Verify overlays above menu, transition to car, no duplicate scene, clean exit and no device/display disturbance. |
| Crop/render scale | **Pending user** | Symmetric vertical band, split top/bottom mode switch, horizontal crop, global/per-app switch and render scale. Preserve flat geometry and stereo fusion. |
| Visor/edge masks/calibration | **Pending user** | Pistol direct visor and calibration first; later DiRT Topmost visor/common-scene parity, intended edge boundaries, literal-pixel grid and shared-tangent radial geometry. |
| HUD and Trace | **Pending user** | Stereo fusion, colour parity, Off/Always/Alarm-only appearance, responsible-channel emphasis, hold and fade. |
| Spotter/flag/lap visuals | **Pending user** | Physical side, restrained coexistence, prompt stale clear, card placement and configurable appearance. |
| Device-loss containment | Contract pass; **headset pending** | One Topmost allocation attempt/session and fail-closed direct fallback are pinned. Abort on any stream/monitor/DWM disturbance. |
| Fallback behaviour | Contract pass; **runtime pending** | `overlay_force_direct=1` direct path and automatic failure fallback need Pistol Whip observation before DiRT. |
| Shutdown/reconnect | Partial pass | Provider cancellation/quick restart and broker persistence pass. Final game shutdown, runtime reconnect and installer upgrade/uninstall cycle remain pending. |

## Release decision

Do not publish 4.1.210 yet. Install it, run the ordered headset checks above, and add one compact row
per tested build/game to `VIEWLAB_VALIDATION_HISTORY.md`. If Pistol Whip or the desktop stream shows
any safety symptom, stop before DiRT Rally and preserve the logs listed in `TOPMOST_VALIDATION.md`.
