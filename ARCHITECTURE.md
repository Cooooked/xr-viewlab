# ARCHITECTURE.md

## System Overview
ViewLab is a Windows WPF application that controls and configures a modified ReShade/OpenXR setup. It acts as the central interface for managing ReShade effects through shared-memory communication and OpenXR integration. The modified ReShade DLL provides the rendering backend with custom features for VR/desktop environments.

## Core Components
### ViewLab WPF Application
- **UI Layer**: MainWindow.xaml/MainWindow.xaml.cs
  - Handles user interface for configuration and controls
  - Manages VR Quad position/scale/alpha controls
  - Implements OpenXR menu toggles
- **Config Management**: config/INI files
  - Stores user preferences and runtime settings
- **Shared-Memory Interface**: shared_memory.cpp/h
  - Facilitates communication with ReShade DLL
  - Exposes structs for real-time control
- **OpenXR Integration**: XRViewLab.UI components
  - Manages OpenXR session and swapchain
  - Handles VR overlay rendering

### Modified ReShade DLL
- **Runtime System**: runtime.cpp/gui.cpp
  - Core effect rendering engine
  - Implements desktop/HMD runtime separation
  - Manages effect runtime sync
- **Shared-Memory Communication**: shared_memory.cpp/h
  - Receives commands from ViewLab
  - Updates effect parameters in real-time
- **OpenXR Implementation**: openxr_hooks_swapchain.cpp/overlay.hpp
  - Provides XR-specific rendering hooks
  - Manages VR overlay lifecycle

## Communication Flow
1. **User Input** → ViewLab UI
2. UI updates shared-memory structs
3. ReShade DLL reads shared memory
4. ReShade applies effects based on config
5. Output rendered through OpenXR/HMD or desktop

## Architecture Diagram
```
ViewLab
  → config/INI
  → shared_memory (structs)
  → ReShade DLL
    → desktop runtime
    → XR/HMD runtime
    → Gameplay/Tuning modes
    → VR Quad controls
    → OpenXR menu
    → Visual Masks
    → Effect Runtime Sync
    → Loading/progress bar
```

## Key Decisions
- Gameplay Mode is a launch-time config (`xr_mode=0`) that disables desktop effects
- VR Quad controls are WPF-based, not controller-driven
- Shared-memory structs must remain stable for cross-process communication
- OpenXR menu toggle uses edge-triggered semantics to prevent spam

## Safety Considerations
- Never modify shared-memory struct layouts
- Avoid changing OpenXR swapchain implementation
- Do not alter ReShade's effect rendering pipeline
- Preserve INI config format for both applications
