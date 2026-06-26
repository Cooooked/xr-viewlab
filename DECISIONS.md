# DECISIONS.md

## Overview
This document records intentional design decisions made during the development of ViewLab and the modified ReShade DLL. These decisions are critical for maintaining system stability and guiding future modifications.

---

## 1. Gameplay Mode Configuration
- **Decision**: Gameplay Mode is a launch-time configuration (`xr_mode = 0`) rather than a live toggle.
- **Rationale**: Prevents accidental activation of desktop effects during VR sessions. Ensures consistent behavior across sessions.

## 2. Shared-Memory Struct Stability
- **Decision**: Shared-memory structs (`XRControlBlock`) must remain stable across ViewLab and ReShade DLL versions.
- **Rationale**: Breaking changes would require simultaneous updates to both applications, risking deployment failures.

## 3. Edge-Triggered Command Semantics
- **Decision**: Shared-memory commands (e.g., menu toggle) use edge-triggered semantics (rising edge only).
- **Rationale**: Prevents repeated execution of commands while a state is held, avoiding unintended behavior.

## 4. Runtime Separation
- **Decision**: Desktop and XR/HMD rendering paths are strictly separated.
- **Rationale**: Ensures optimal performance and correct behavior for each runtime mode.

## 5. Loading/Progress Bar Gating
- **Decision**: `render_effects()` must respect Gameplay/Tuning gating even during loading.
- **Rationale**: Prevents desktop effects from appearing in Gameplay Mode during loading.

## 6. INI Config Format Preservation
- **Decision**: INI file syntax must remain consistent between ViewLab and ReShade.
- **Rationale**: Avoids parsing errors and ensures backward compatibility.

## 7. Versioned Deployment
- **Decision**: MSI installer must enforce versioned DLL deployment.
- **Rationale**: Prevents version mismatches between ViewLab and ReShade DLL.

*End of DECISIONS.md.*