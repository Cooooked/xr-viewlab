# DATAFLOW.md

## Overview
This document details the exact data paths for key operations in the ViewLab and ReShade system. It includes file and function names to help trace the flow of data and control signals.

---

## 1. Gameplay Mode Path
**Purpose**: Toggle between desktop effects (off) and XR/HMD effects (on) via a checkbox.

### Data Flow:
1. **UI Checkbox** (`MainWindow.xaml`): `IsChecked` bound to `ViewModel.GameplayMode`
2. **ViewModel** (`MainWindow.xaml.cs`): Setter updates `ReShadeControlService.GameplayMode`
3. **Service Write** (`ReShadeControlService.cs`): 
   - Method: `SetGameplayMode(bool enabled)`
   - Writes to shared memory: `sharedMemoryBlock->xr_mode = enabled ? 0 : 1;` 
     (Note: In the code, checked = 0 for Gameplay Mode, unchecked = 1 for Tuning Mode)
4. **Shared Memory**: `XRControlBlock` struct (defined in `shared_memory.h`)
   - Field: `int xr_mode`
5. **DLL Read** (`runtime.cpp`):
   - Function: `void EffectRenderer::UpdateInput()`
   - Reads `xr_mode` from shared memory
6. **Desktop Runtime Effect Suppression** (`runtime.cpp`):
   - Function: `bool EffectRenderer::ShouldRenderDesktop()`
   - Returns `false` if `xr_mode == 0` (Gameplay Mode)
7. **Update/Render Decision** (`runtime.cpp`):
   - Function: `void EffectRenderer::Render()`
   - Skips desktop effect rendering if `ShouldRenderDesktop()` returns `false`

### Files/Functions:
- `MainWindow.xaml`: UI checkbox
- `MainWindow.xaml.cs`: ViewModel setter
- `ReShadeControlService.cs::SetGameplayMode`
- `shared_memory.h`: `XRControlBlock::xr_mode`
- `runtime.cpp::EffectRenderer::UpdateInput`
- `runtime.cpp::EffectRenderer::ShouldRenderDesktop`
- `runtime.cpp::EffectRenderer::Render`

---

## 2. VR Quad Path
**Purpose**: Adjust the position, scale, and alpha of the VR overlay quad via sliders.

### Data Flow:
1. **UI Sliders** (`MainWindow.xaml`): Bound to `ViewModel.QuadPositionX`, etc.
2. **ViewModel** (`MainWindow.xaml.cs`): Setters update `ReShadeControlService.Quad*`
3. **Service Write** (`ReShadeControlService.cs`):
   - Methods: `SetQuadPosition`, `SetQuadScale`, `SetQuadAlpha`
   - Write to shared memory block: 
     - `sharedMemoryBlock->quad_position.x = value;`
     - etc.
4. **Shared Memory**: `XRControlBlock` struct
   - Fields: `QuadPosition` (float3), `QuadScale` (float), `QuadAlpha` (float)
5. **DLL Read** (`openxr_overlay.cpp` or `runtime_gui.cpp`):
   - Function: `void Overlay::UpdateTransform()`
   - Reads `quad_position`, `quad_scale`, `quad_alpha` from shared memory
6. **OpenXR Quad Transform** (`openxr_overlay.cpp`):
   - Function: `void Overlay::UpdateTransform()`
   - Builds transformation matrix from position/scale
7. **HMD Render** (`openxr_overlay.cpp`):
   - Function: `void Overlay::Render()`
   - Uses transformation matrix to position quad in HMD space

### Files/Functions:
- `MainWindow.xaml`: UI sliders
- `MainWindow.xaml.cs`: ViewModel setters
- `ReShadeControlService.cs::SetQuadPosition/Scale/Alpha`
- `shared_memory.h`: `XRControlBlock` (quad_position, quad_scale, quad_alpha)
- `openxr_overlay.cpp::Overlay::UpdateTransform`
- `openxr_overlay.cpp::Overlay::Render`

---

## 3. Loading/Progress Path
**Purpose**: Show progress while loading effects and techniques at startup.

### Data Flow:
1. **Runtime Loading State** (`runtime.cpp`):
   - Function: `void EffectRenderer::LoadingBarUpdate(float progress)`
   - Called during effect loading to report progress
2. **Update Effects** (`runtime.cpp`):
   - Function: `void EffectRenderer::UpdateEffects()`
   - Populates `_techniques` array with loaded techniques
3. **Techniques Populated** (`runtime.cpp`):
   - Variable: `std::vector<Technique> _techniques`
   - Filled by `UpdateEffects()`
4. **Render Effects** (`runtime.cpp`):
   - Function: `void EffectRenderer::RenderEffects()`
   - Iterates over `_techniques` to render each
5. **Desktop Runtime Gating** (`runtime.cpp`):
   - Function: `bool EffectRenderer::ShouldRenderDesktop()`
   - Gates `RenderEffects()` call based on `xr_mode` (Gameplay/Tuning)

### Known Bug/Fix:
- **Loading-bar fix**: Previously, `update_effects()` was not called during loading, leaving `_techniques` empty.
  - Fix: Allow `update_effects()` during loading to populate `_techniques`.
- **Gating issue**: After the fix, `render_effects()` was called during loading but without Gameplay/Tuning gating, causing desktop effects to appear in Gameplay Mode.
  - Fix: Added gating in `render_effects()` to check `ShouldRenderDesktop()`.

### Files/Functions:
- `runtime.cpp::EffectRenderer::LoadingBarUpdate`
- `runtime.cpp::EffectRenderer::UpdateEffects`
- `runtime.cpp::EffectRenderer::_techniques` (member)
- `runtime.cpp::EffectRenderer::RenderEffects`
- `runtime.cpp::EffectRenderer::ShouldRenderDesktop`

---

## 4. Shared-Memory Path
**Purpose**: ViewLab sends commands and state to the ReShade DLL via a memory-mapped file.

### Data Flow:
1. **ViewLab Service** (`ReShadeControlService.cs`):
   - Function: `void UpdateControlBlock()`
   - Writes entire `XRControlBlock` struct to shared memory
2. **Mapping Name**: Defined in `shared_memory.h` as `Local\\ViewLabSharedMemory`
3. **Struct Fields** (`XRControlBlock` in `shared_memory.h`):
   - `int xr_mode` (Gameplay/Tuning)
   - `float3 quad_position`
   - `float quad_scale`
   - `float quad_alpha`
   - `int command_id` (for edge-triggered commands)
   - `float command_payload` (generic payload)
   - ... (other fields as defined)
4. **DLL Control Functions** (`runtime.cpp`):
   - Function: `void EffectRenderer::UpdateInput()`
   - Reads the shared memory block into a local copy
   - Processes `command_id` edge-triggered (only act on change)
   - Applies state changes (xr_mode, quad_*, etc.)
5. **Consumers** (`runtime.cpp`):
   - Desktop runtime: Uses `xr_mode` to gate effects
   - VR Quad: Uses `quad_position/scale/alpha` for transform
   - Menu toggle: Uses `command_id` to toggle overlay

### Files/Functions:
- `ReShadeControlService.cs::UpdateControlBlock`
- `shared_memory.h`: `XRControlBlock` struct and mapping name
- `runtime.cpp::EffectRenderer::UpdateInput`
- `runtime.cpp::EffectRenderer::ApplyGameplayMode` (example consumer)
- `runtime.cpp::EffectRenderer::UpdateQuadTransform` (example consumer)

---

## Safety Notes
- **Shared-memory struct**: Must not be changed without updating both ViewLab and DLL simultaneously.
- **Edge-triggered commands**: Only act on transition (e.g., rising edge) to avoid spamming.
- **Versioning**: If the struct changes, a version field must be added and handled.

*End of DATAFLOW.md.*