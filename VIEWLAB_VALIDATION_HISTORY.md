# ViewLab Validation History

This is the compact evidence ledger for recent ViewLab validation. It separates compilation,
automated contracts, desktop/runtime observation, and human headset observation. A missing entry is
missing documentation, not proof that nobody tested the build.

The historical record before 2026-07-13 is necessarily grouped where conversation and notes did
not preserve the exact build number for every observation. Do not invent finer precision later.

## Evidence levels

| Level | Meaning |
|---|---|
| Contracts | Deterministic repository tests passed; no visual claim |
| Build | WPF, native layers, and/or MSI compiled; no runtime claim |
| Desktop/runtime | Process, logs, payload, screenshots, or failure behaviour observed outside the lenses |
| Headset | A person observed the named behaviour in the headset |
| Game-specific | Named game/runtime path was exercised; result applies only to that path and tested configuration |

## Known recent record

| Build(s) | Date | Game/runtime | Evidence | Major coverage | Result and known failures |
|---|---|---|---|---|---|
| 4.1.103 | 2026-07-10 | Pistol Whip / Virtual Desktop VDXR / Quest 3 | Headset, game-specific | Direct visor, stencil inner-eye filter | Confirmed working reference for the stencil repair. This is a narrow reference, not the last later headset test. |
| 4.1.105–4.1.134 | 2026-07-10–11 | Pistol Whip / VDXR | Contracts, builds, desktop/runtime, repeated headset observations | Crop, visor geometry, crop-edge artefacts, calibration captures, fixed-foveation diagnosis | Extensive iterative testing occurred. Exact build-to-observation mapping is incomplete. The crop-edge quality issue was localised to Virtual Desktop fixed-foveated streaming; experimental crop fixes were removed. |
| 4.1.112–4.1.115 | 2026-07-11 | Pistol Whip and DiRT Rally 2 | Desktop/runtime plus headset observations recorded across notes | Direct visor launch, curve range, shader lifetime crash repair, vertical orientation | Both games launched and drew the visor in recorded live checks. Individual shape refinements were not all accepted in the same build. |
| 4.1.134 vicinity | 2026-07-11 | Pistol Whip / VDXR | Headset screenshots and human headset comparison | Crop boundary, edge guard, fixed-foveation modes | Guard sharply reduced the measured capture ramp, but the user reported little perceptual headset benefit; the dominant issue was downstream streaming quality. |
| 4.1.143–4.1.148 | 2026-07-12 | Pistol Whip / VDXR | Build, desktop/runtime and manual headset use | Runtime recovery, visor, crop, fixed-reference binocular preview | Pistol Whip was restored and running. Documentation did not consistently attach each headset observation to an exact MSI. 4.1.148 was published at the user's direction. |
| 4.1.159–4.1.185 | 2026-07-12–13 | Pistol Whip / VDXR; DiRT Rally 2 / OpenComposite | Contracts, builds and repeated manual headset testing reported by the user | Direct/Topmost overlays, DiRT menu-to-car transition, crop, visor, crosshair, calibration, HUD, trace, stereo fusion | Testing exposed and then verified repairs for binocular “two stickers” and tilted crop. Topmost was observed above DiRT's menu layer. Calibration scale/geometry and Topmost colour issues were also identified during this range. Exact coverage per build is incompletely documented. |
| 4.1.187 incident range | 2026-07-13 | DiRT Rally 2 / OpenComposite | Desktop/runtime diagnostics and headset/game-session incident | Topmost menu-to-car transition, device loss, shutdown | Unsafe Topmost swapchain churn and 193 retry attempts accompanied a D3D device/display-stack failure. This is a failed validation, not a successful build claim. See `INCIDENT_REPORT.md`. |
| 4.1.191–4.1.194 | 2026-07-13 | Repository and packaged payload | Contracts, build, MSI extraction; manual headset testing occurred during the broader recent cycle | Topmost fail-closed work, unified coordinates, calibration repair, modular HUD/trace, hardware telemetry | Automated suites and packaging passed. The exact post-safety-repair headset matrix was not recorded well enough to claim every new behaviour accepted in 4.1.194. Hardware telemetry overhead/provider checks still require targeted headset comparison. |
| 4.1.195 | 2026-07-13 | Integrated `master` baseline | Contracts and full build | WPF, x64 layer, Win32 layer, WiX MSI, packaged payload hashes | Passed with zero warnings/errors. No new headset test yet; behaviour matches integrated 4.1.194 source apart from version metadata and documentation. |
| 4.1.202 | 2026-07-13 | Windows 11 desktop / packaged notification broker | Contracts, build, installer, desktop/runtime | Sparse identity registration, medium-integrity broker lifetime, listener permission, real external Windows toast, production card mapping | Passed: signature valid, package status OK, listener Allowed, broker remained alive without settings, and a separately packaged fixture reached `Local\XRViewLabNotifications` as exactly one card (ID 1). In-headset card rendering not yet checked. |
| 4.1.203 | 2026-07-13 | Recorded iRacing SDK shared-memory fixture | Contracts, full build, deterministic runtime fixture | Official spotter enums, tick dedupe/stale/inactive, reconnect, quick restart, invalid enum/offset, flag priority, lap validity/PB/delta/session reset | Passed every fixture plus WPF, broker, x64/Win32 native and MSI payload validation with zero warnings/errors. Live/replay iRacing and headset presentation not yet tested. |
| 4.1.204 | 2026-07-13 | Installed packaged broker / generic racing bridge | Full build, MSI install, desktop/runtime | Independent broker pipe, spotter/flag/lap/clear simulation, racing mapping, lap textured-card queue | Passed: installed broker PID 17096 published expected left/right/both/yellow/lap/clear states; lap simulation produced one active production card. No headset or live iRacing test yet. |
| 4.1.205 | 2026-07-13 | Packaged racing presentation candidate | Contracts, iRacing fixtures, full build/MSI payload | WPF controls, broker, generic mapping, native x64/Win32 spotter/flag/card consumers | Passed with zero warnings/errors; aligns notification header field order. Behavioural deployment proof remains the installed 4.1.204 run; headset visuals still pending. |
| 4.1.206 | 2026-07-13 | Racing attention policy | Contracts, broker build, full build/MSI payload | Desktop-card bounded hold/expiry during spatial and safety-flag cues | Passed with zero warnings/errors. Policy timing and simultaneous headset appearance remain unverified visually. |
| 4.1.207 | 2026-07-13 | Performance Trace visibility modes | Contracts, full build/MSI payload | Off/always/alarm-only state, hold/fade and responsible-channel emphasis | Passed WPF, broker, x64/Win32 and MSI validation with zero warnings/errors. Alarm transition appearance needs headset/runtime timing verification. |
| 4.1.208 | 2026-07-13 | Automatic Topmost candidate | Contracts, full build/MSI payload | Default automatic policy, first-frame direct arm, duplicate suppression, diagnostic force-direct key, existing fail-closed latch | Passed with zero warnings/errors. Safety-critical Pistol Whip then DiRT headset/device-loss matrix is deliberately still pending. |
| 4.1.209 | 2026-07-13 | Repository, Windows 11 desktop and installed packaged broker | Contracts, fixtures, full build/MSI payload, desktop/runtime | Bounded history, truthful status labels, all iRacing fixtures, Topmost lifecycle simulation, real external Windows toast | All deterministic suites and the full build passed with zero warnings/errors. A fresh independent packaged toast reached the installed production broker as one card (ID 2). The installed proof remains 4.1.204-era broker code; 4.1.209 headset/game presentation and the ordered Topmost safety matrix remain pending. |

## Recording the next test

Add one row. One sentence in the last column is enough.

```text
| 4.1.xxx | YYYY-MM-DD | Game / runtime / headset | Headset + game-specific | Features actually checked | Pass/fail, exact symptom, and anything not checked |
```

Use risk-based scope:

- Run contracts for every implementation commit.
- Build after implementation changes.
- Test the affected subsystem in its relevant game/runtime.
- Perform the broader Pistol Whip and DiRT Rally regression matrix for a release candidate or a
  change to shared projection, rendering, device lifetime, installer, or profile state.
- Record “not tested” for omitted areas rather than converting a narrow pass into a global one.
