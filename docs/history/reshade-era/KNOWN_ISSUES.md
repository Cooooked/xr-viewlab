# KNOWN_ISSUES.md

## Overview
This document lists known issues and fragile areas in the ViewLab and ReShade system. These are documented for awareness but not fixed during this stabilization session.

---

## 1. VR Quad Echo/Repeating/Jumping/Growing Bug
- **Description**: When adjusting VR Quad position/scale/alpha rapidly, the quad may exhibit visual artifacts such as echoing, repeating, jumping, or growing uncontrollably.
- **Reproduction**: Move sliders quickly or set extreme values.
- **Impact**: Degrades user experience in VR.
- **Notes**: Likely related to shared-memory update rate or interpolation in the DLL.

## 2. VR Quad Default/Save/Reset Baseline Confusion
- **Description**: The behavior of "Reset" and "Save Default" buttons is unclear to users, leading to unexpected baseline changes.
- **Reproduction**: Use Save Default, then Reset, then adjust again.
- **Impact**: Users may lose preferred settings.
- **Notes**: The saved baseline is stored in shared memory; there is no persistent storage.

## 3. Directional Pad Y-Axis Inversion
- **Description**: When using a directional pad (if implemented) to control VR Quad, the Y-axis may be inverted.
- **Reproduction**: Press up on D-pad to move quad down.
- **Impact**: Counter-intuitive control.
- **Notes**: May be due to coordinate system differences between input and rendering.

## 4. Centre Dot Unclear Functionality
- **Description**: The purpose of the centre dot in the VR Quad UI is not documented or clear.
- **Reproduction**: Observe the UI; no tooltip or documentation.
- **Impact**: Users may ignore or misuse the control.
- **Notes**: Possibly a leftover from an earlier design.

## 5. Close Button Needs to Become VR Menu Toggle
- **Description**: The close button in the UI currently closes the popup but should toggle the in-HMD ReShade menu.
- **Reproduction**: Click the close button; the popup closes but the menu does not toggle.
- **Impact**: Inconsistent user expectation.
- **Notes**: Requires changing the button's command to an edge-triggered menu toggle.

## 6. Shared-Memory Command Fields Need Edge-Triggered Semantics
- **Description**: Command fields in the shared-memory block are currently level-triggered, causing repeated execution if the state is held.
- **Reproduction**: Set a command ID and hold it; observe repeated actions.
- **Impact**: Unintended multiple triggers (e.g., menu toggling repeatedly).
- **Notes**: Fix requires detecting rising edge only.

## 7. Loading/Progress Bar Can Accidentally Re-enable Desktop Effects
- **Description**: During loading, if the loading bar fix is not properly gated, desktop effects may be enabled in Gameplay Mode.
- **Reproduction**: Launch with Gameplay Mode enabled and observe desktop effects during loading.
- **Impact**: Visual artifacts or performance issues in VR.
- **Notes**: Already partially fixed; ensure `render_effects()` respects Gameplay/Tuning gating.

## 8. Same-Version MSI Replacement Issues
- **Description**: Installing an MSI with the same version as the current installation may not replace files correctly.
- **Reproduction**: Build and install an MSI with same version; some files may not update.
- **Impact**: Users may run outdated binaries.
- **Notes**: Requires changing the product code or using a patch strategy.

## 9. Stale DLL Deployment Risks
- **Description**: If the ViewLab application is updated but the ReShade DLL is not redeployed, version mismatches can occur.
- **Reproduction**: Update ViewLab without copying the new DLL to the deployment folder.
- **Impact**: Crashes or undefined behavior due to API mismatches.
- **Notes**: Deployment scripts must enforce DLL update.

## 10. Missing Git Repos or Backup Gaps
- **Description**: The ReshadeAI folder (containing ReShade source) was not previously a Git repository, risking loss of history.
- **Reproduction**: Check for .git folder in ReshadeAI.
- **Impact**: Difficulty in tracking changes and recovering from errors.
- **Notes**: Now initialized as a Git repository (see backup task).

*End of KNOWN_ISSUES.md.*