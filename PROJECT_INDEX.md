# PROJECT_INDEX.md

## Overview
A concise index of critical files, their purposes, and safety considerations for both the ViewLab WPF application and the modified ReShade DLL. This index is intended to help future AI agents quickly locate and understand key components without making unsafe modifications.

---

### ViewLab WPF Application

| Path | Purpose | Main Classes / Functions | Features Involved | Safe‑to‑Edit Notes | Danger Notes |
|------|---------|--------------------------|-------------------|--------------------|--------------|
| `MainWindow.xaml` | UI layout for the main configuration window | `MainWindow` (partial class) | All UI interactions | Can be edited for UI tweaks only | Changing layout may affect binding logic |
| `MainWindow.xaml.cs` | Code‑behind for `MainWindow.xaml` | `MainWindow` class | UI event handling | Safe for UI logic adjustments | Modifying core logic can break data flow |
| `ReShadeControlService.cs` | Service that manages ReShade settings via shared memory | `ReShadeControlService` | Config loading/saving, shared‑memory updates | Safe for service method updates | Directly affects ReShade behavior |
| `build.ps1` | PowerShell build script for MSI packaging | `Build-ViewLab` function | Build pipeline, versioning | Can add build steps | Changing build order may break CI |
| `Product.wxs` | WiX installer XML defining MSI package | N/A (XML) | Installer UI, file selection | Safe for version bump updates | Incorrect XML breaks installer |
| `AssemblyInfo.cs` | Assembly metadata (version, GUID) | N/A | Versioning, COM registration | Safe for version increment | Changing GUID breaks strong naming |
| `runtime.cpp` | Native runtime entry point for ReShade integration | `RuntimeInit` | DLL loading, shared‑memory mapping | Safe for minor instrumentation | Modifying runtime logic can crash the app |
| `runtime_gui.cpp` | GUI bridge between ViewLab and ReShade runtime | `GuiBridge::Update` | UI updates from shared memory | Safe for UI refresh tweaks | Affects real‑time rendering |
| `openxr_overlay.hpp` | Header for OpenXR overlay implementation | `OpenXROverlay` class | VR menu rendering, overlay control | Safe for adding overlay flags | Changing overlay logic may break VR |
| `openxr_overlay_preview.cpp` | Preview implementation for OpenXR overlay | `OverlayPreview::Render` | Real‑time preview of overlays | Safe for UI preview adjustments | May affect performance |
| `openxr_hooks_swapchain.cpp` | Hooks for swapchain handling in OpenXR | `SwapchainHook::Present` | Frame presentation, timing | Safe for minor hook adjustments | Incorrect hook can cause rendering artifacts |
| `openxr_impl_swapchain.cpp` | Implementation of OpenXR swapchain interface | `ImplSwapchain::Acquire` | Low‑level swapchain management | Safe for performance tweaks | May cause frame loss |
| `addon.cpp` | Generic add‑on entry point for ReShade extensions | `AddonMain` | Custom effect registration | Safe for adding add‑ons | Modifying add‑on registration can break loading |

---

### Modified ReShade DLL

| Path | Purpose | Main Classes / Functions | Features Involved | Safe‑to‑Edit Notes | Danger Notes |
|------|---------|--------------------------|-------------------|--------------------|--------------|
| `runtime.cpp` | Core runtime logic for ReShade effects | `EffectRenderer`, `Init` | Effect loading, runtime sync | Safe for adding logging | Changing effect pipeline can break rendering |
| `runtime_gui.cpp` | GUI handling for ReShade overlay | `Gui::DrawOverlay` | VR overlay/menu rendering | Safe for UI text changes | Affects overlay visibility |
| `shared_memory.cpp` | Shared‑memory communication with ViewLab | `SharedMemory::Read` | Remote control commands | Safe for read‑only extensions | Writing to shared memory can corrupt state |
| `openxr_overlay.hpp` (also used by ViewLab) | Declares overlay structures | `OverlayDesc` struct | Describes overlay geometry | Safe for adding fields | Changing struct layout breaks ViewLab |
| `openxr_impl_swapchain.cpp` | Swapchain implementation for XR | `XrSwapchain::AcquireImage` | Frame acquisition | Safe for performance tweaks | May cause tearing |
| `addon.cpp` | Add‑on manager for custom shaders | `AddonManager::Load` | Effect module registration | Safe for adding new add‑ons | Modifying registration can crash DLL |

---

### Cross‑Component References
- **Shared‑Memory Block**: Defined in `shared_memory.cpp` (ViewLab) and consumed by `runtime.cpp` (ReShade DLL).  
- **Config Files**: INI files located in `config/` are read by both ViewLab (`ConfigService`) and ReShade (`ConfigLoader`).  
- **Build Scripts**: `build.ps1` orchestrates MSI creation and DLL signing; changes must preserve versioning.

---

### Safety Checklist for Editors
- **Never rename** any of the listed files or classes without explicit instruction.  
- **Do not modify shared‑memory structs** unless a documented change is required.  
- **Avoid editing generated code** (e.g., files marked as generated in comments).  
- **Preserve build scripts** – only add comments or version bumps.  
- **When in doubt**, add a note in `KNOWN_ISSUES.md` before making any change.  

*End of index.*