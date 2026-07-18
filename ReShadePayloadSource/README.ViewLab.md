# ViewLab ReShade payload source

This directory is the canonical, buildable source for ViewLab's bundled
`ReShadePayload/ReShade64.dll`. It is a recovered working tree based on upstream ReShade,
not a stock ReShade checkout.

## Recovery provenance

- Recovered on: 2026-07-18
- Original working tree: `F:\AI-Projects\ReshadeAI\reshade`
- Original remote: `https://github.com/crosire/reshade`
- Base commit: `4a50d1eddace85734871d91792ff214f13f66c01`
- The original tree had ViewLab changes in `CMakeLists.txt`, OpenVR/OpenXR hooks,
  `runtime.cpp`, `runtime.hpp`, `runtime_gui.cpp`, and `runtime_gui_vr.cpp`, plus the
  untracked ViewLab files `source/openxr/openxr_overlay.hpp` and
  `source/openxr/openxr_overlay_preview.cpp`.
- The original `build/Release/ReShade64.dll` SHA-256 was
  `2307754A416C9F73CA9DD84BBC8C418FB67B325B5CFBC2F8F08D8AADF1590EC1`, exactly matching
  the DLL bundled by ViewLab before recovery.

The upstream `.git` directory and old build products were deliberately not nested inside ViewLab.
The original path, remote, base commit and dirty-file inventory above preserve the recoverable Git
provenance while this repository remains the single canonical workspace.

## ViewLab-specific modifications

The fork adds:

- the `Local\ReShadeXRControl` shared-memory contract used by ReShade Remote;
- a Home-key (`KeyOverlay=36`) controlled menu rendered into a dedicated OpenXR quad;
- persisted quad position, orientation, size and opacity;
- an OpenComposite-aware OpenXR route that avoids the SteamVR dashboard-overlay path;
- a focusable desktop mirror of the same ImGui menu surface, including mouse, wheel and keyboard input;
- ViewLab live heartbeat and edit-mode state;
- a two-buffer GPU readback path and persistent preview window lifecycle, avoiding synchronous
  same-frame readback stalls, needless surface churn and white background flashes.

## Build

The CMake project target `ReShade` builds the payload. From this directory:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe' -S . -B build -G 'Visual Studio 18 2026' -A x64
& 'C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe' --build build --config Release --target ReShade -- /m
```

Output: `ReShadePayloadSource\build\Release\ReShade64.dll`.

After focused validation, copy that DLL to `ReShadePayload\ReShade64.dll`. ViewLab's `build.ps1`
copies `ReShadePayload` into the WPF publish tree, and `Installer/Product.wxs` packages that DLL and
`ReShade64_XR.json` under the installed `ReShadePayload` directory. The installer itself is not
required to rebuild or validate this payload.

