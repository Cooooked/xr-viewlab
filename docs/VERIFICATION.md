# Verification — how to test ViewLab behavior

> Canonical test procedures. Update when the test environment or workflow changes.

## Levels of verification (cheapest first)

1. **Contract test:** `Tests\Verify-ViewLabContracts.ps1` — pins UI↔DLL key wiring, geometry
   parity anchors, regression fixes. Must pass before every commit.
2. **Build:** `.\build.ps1` must finish with 0 warnings / 0 errors.
3. **Log verification:** launch the game, read `%LOCALAPPDATA%\XR ViewLab\ViewLab.log` — the
   `config:` line shows every value the DLL actually loaded; `OK draw executed` lines confirm
   the visor pipeline runs. Proves plumbing, NOT visuals.
4. **Manual desktop verification:** exercise settings, permissions, lifecycle and diagnostics
   without claiming headset output.
5. **In-headset verification:** the only proof of visual correctness. Record the game, runtime,
   affected features and result in `VIEWLAB_VALIDATION_HISTORY.md`. A screenshot proves submitted
   pixels; a human headset observation proves perceived stereo/lens behaviour.

Verification is risk-based. Every implementation change gets contracts and a build. Test the
affected subsystem in the relevant runtime. Reserve the broader Pistol Whip + DiRT Rally matrix for
release candidates and changes to shared projection, rendering, device lifetime, installer or
profile state. Do not treat an omitted test as a failure, nor a narrow pass as global acceptance.

## Test environment (do not guess alternate paths)

- **Game:** Pistol Whip — `D:\VR Games\Pistol Whip-working\Pistol Whip.exe`
- **Runtime:** Virtual Desktop / VDXR, Quest 3 headset
- **ReShade in game folder:** `D:\VR Games\Pistol Whip-working\ReShade.ini` —
  Home (36) = overlay menu, PrintScreen (42) = screenshot
- **Screenshots land in:** `%USERPROFILE%\Documents\ReShade\Screenshots\`
- Secondary title for OpenComposite/OpenVR timing paths: DiRT Rally 2.

## Rules

- Check headset/runtime state BEFORE launching a game. `XR_ERROR_FORM_FACTOR_UNAVAILABLE` in the
  log = headset not streaming — stop and tell the user. Do not spam game launches.
- Games query the visibility mask and config once at session start — **restart the game** after
  changing stencil settings or installing a new build.
- Toggling ViewLab on/off: use the big ENABLED/DISABLED button in the ViewLab UI (it handles
  both registry hives). Don't hand-edit layer registration.
- MSI install may reset live ini keys (REGRESSIONS R6) — re-check settings in the UI after install.
- Recording a headset result should take one row, not a small coronation. Use the template in
  `VIEWLAB_VALIDATION_HISTORY.md`.

## In-HMD screenshot workflow (agent-driven visual verification)

1. Build + install the MSI; confirm ViewLab UI shows ENABLED.
2. Launch Pistol Whip via Virtual Desktop; wait for the game to fully load.
3. Confirm in `ViewLab.log`: fresh `config:` line with expected values, then `OK draw executed`.
4. Trigger a screenshot: press PrintScreen (or drive it via input automation).
5. Read the newest file in `%USERPROFILE%\Documents\ReShade\Screenshots\` and inspect the eye
   image: visor shape, edges, stencil state.
6. Judge against the expectation (e.g. divot bottom-inner only; smooth edges; arch vs bean).
Screenshots capture ONE eye's submitted image — good for shape/stencil checks; lens-warp
artifacts (edge smear, blockiness magnification) still need a human in the headset.
## Forefront diagnostic capture

Use this only with a build that contains the `forefront diag: VIEWLAB_LOADED` marker.

1. Close Forefront and ViewLab, then launch ViewLab and start Forefront normally through Steam/VD.
2. If Forefront launches, wait until the VR scene has attempted to start; then exit it and reload the
   app list in ViewLab.
3. Send `%LOCALAPPDATA%\XR ViewLab\ViewLab.log` plus the exact text (or a screenshot) of any EAC
   warning. Do not edit the log.

Interpretation: no `VIEWLAB_LOADED` marker means the OpenXR layer never entered the actual Forefront
process; the marker plus `xrCreateApiLayerInstance result` identifies layer-chain failure; the marker
plus `render: FOV adjustment active` means ViewLab's crop hook ran and the remaining issue is visual
or runtime-side.
# Overlay functionality questionnaire — 2026-07-13

1. Which exact ViewLab build was most recently tested in Dirt Rally 2, and what happened?
2. At 100% horizontal/vertical, where is the crosshair relative to perceived straight ahead?
3. What visual reference is being used as the true lens centre?
4. With one eye closed at a time, is the crosshair correct in each eye individually?
5. With both eyes open, is the crosshair fused, doubled, blurry, or at an uncomfortable depth?
6. When horizontal render amount changes, how do crosshair position and apparent size change per eye?
7. With 0% top and 20% bottom, should pinning place the target on the edge or keep the whole symbol inset?
8. Should user calibration offset move the full-lens target before pinning, or move the already-pinned symbol?
9. Should radial spokes share the crosshair calibration offset, or remain the uncalibrated ViewLab centre reference?
10. Is the Performance HUD deliberately monocular in the left eye, or intended on the left side but binocularly fused?
11. Is HUD duplication two crisp copies, a small displaced double, stale trails, or differing content?
12. When does duplication begin, and does toggling the HUD clear it?
13. Should Performance Trace be left-eye-only or binocular, and should telemetry collection remain shared?
14. Should Render Area `(0.5, 0.5)` mean submitted-rectangle pixel centre or selected tangent-region angular centre?
15. Which overlays must fuse binocularly, and which must remain literal per-eye texture diagnostics?
16. Should calibration dimensions remain exact texture pixels, scale with render area, or keep constant angular size?
17. Do live crop changes recreate Dirt Rally swapchains, update FOV only, or require a restart before resolution changes?
18. What exact OpenComposite installation path, headset, and active OpenXR runtime are used?
19. Which other API layers remain enabled when duplication occurs?
20. Which should be validated first: crosshair/pinning, HUD duplication, HUD/Trace separation, or calibration stability?
