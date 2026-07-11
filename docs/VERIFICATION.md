# Verification — how to test ViewLab behavior

> Canonical test procedures. Update when the test environment or workflow changes.

## Levels of verification (cheapest first)

1. **Contract test:** `Tests\Verify-ViewLabContracts.ps1` — pins UI↔DLL key wiring, geometry
   parity anchors, regression fixes. Must pass before every commit.
2. **Build:** `.\build.ps1` must finish with 0 warnings / 0 errors.
3. **Log verification:** launch the game, read `%LOCALAPPDATA%\XR ViewLab\ViewLab.log` — the
   `config:` line shows every value the DLL actually loaded; `OK draw executed` lines confirm
   the visor pipeline runs. Proves plumbing, NOT visuals.
4. **In-headset verification:** the only proof of visual correctness. The user does this, or use
   the ReShade screenshot workflow below.

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
