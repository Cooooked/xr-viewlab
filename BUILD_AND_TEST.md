# BUILD_AND_TEST.md

## Overview
This document describes the complete build and testing workflow for both the ViewLab WPF application and the modified ReShade DLL. It is intended to enable reproducible builds, correct deployment, and verification of functionality.

---

## 1. Prerequisites
- **Operating System**: Windows 10 (64‑bit)
- **Development Tools**:
  - Visual Studio 2022 (Desktop development with C++)
  - CMake 3.20+ (for ReShade dependencies)
  - PowerShell 5.1+ (for build scripts)
  - Git (for source control)
- **Dependencies**:
  - OpenXR SDK (version matching system)
  - DirectX End‑User Runtime
  - WiX Toolset (for MSI packaging)

---

## 2. Building ViewLab (WPF Application)

### 2.1. Build Script (`build.ps1`)
The PowerShell script orchestrates the entire build pipeline:

| Step | Command | Description |
|------|---------|-------------|
| **Clean** | `Remove-Item -Recurse -Force bin obj` | Removes previous build artefacts |
| **Restore NuGet** | `dotnet restore` (if any) | Restores .NET packages |
| **Build Solution** | `msbuild ViewLab.sln /p:Configuration=Release` | Compiles all projects |
| **Run Unit Tests** | `dotnet test` (if tests exist) | Executes unit test suite |
| **Create MSI** | `candle Product.wxs && light -o ViewLab.msi Product.wixobj` | Generates Windows Installer package |
| **Sign Executables** | `signtool sign /a /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 <exe>` | Applies code signing (if configured) |
| **Deploy** | `Copy-Item -Recurse -Force Release\* <deploy_dir>` | Copies binaries to deployment folder |

### 2.2. Version Bumping
- **AssemblyInfo.cs**: Increment `AssemblyVersion` and `AssemblyFileVersion`.
- **Product.wxs**: Update `PackageId` and `Version` attributes.
- **Git Tag**: `git tag -a v<major>.<minor>.<patch> -m "Release <version>"` (optional).

### 2.3. Build Output Paths
- **Executable**: `Release\ViewLab.exe`
- **DLLs**: `Release\*.dll` (including the modified ReShade DLL)
- **MSI Installer**: `Release\ViewLab.msi`
- **Logs**: `Logs\build_*.log`

---

## 3. Building the Modified ReShade DLL

### 3.1. Build Steps
1. **Open CMake** in `ReshadeAI/reshade` directory.
2. **Configure** with:
   ```cmake
   cmake -A x64 -DCMAKE_BUILD_TYPE=Release -DRESINSTALL=OFF -DRESHEADLESS=ON
   ```
3. **Build** using:
   ```powershell
   msbuild ReShade.sln /p:Configuration=Release /m
   ```
4. **Post‑Build** steps:
   - Copy `ReShade64.dll` to `Deploy\ReShade.dll`
   - Strip symbols (optional): `strip -s ReShade64.dll`

### 3.2. Output Path
- **DLL**: `Release\ReShade64.dll` (or `Release\ReShade.dll` depending on build config)

### 3.3. Deployment
- **Copy** the built DLL to the ViewLab output directory (`Release\`).
- **Verify** that the DLL is the one signed with the correct version.

---

## 4. Testing Procedures

### 4.1. General Testing Checklist
- Verify that the application launches without errors.
- Confirm that the shared‑memory block is created and accessible.
- Ensure that the MSI installer registers the shared‑memory mapping correctly.
- Check that logs (`Logs\`) contain no critical errors.

### 4.2. Gameplay Mode Test
1. Launch ViewLab.
2. Check the **Gameplay Mode** checkbox.
3. Verify that:
   - Desktop effects are suppressed.
   - XR/HMD rendering continues.
   - No desktop effects appear in the headset view.
4. Observe the **Loading/Progress Bar** behavior:
   - Ensure the bar completes.
   - Confirm that `update_effects()` populates `_techniques`.
   - Verify that `render_effects()` respects Gameplay/Tuning gating.

### 4.3. Tuning Mode Test
1. Enable **Tuning Mode**.
2. Adjust effect parameters via UI sliders.
3. Verify that changes are reflected instantly in the preview.
4. Ensure that invalid parameter values are clamped or rejected.

### 4.4. VR Quad Controls Test
1. Modify **Position**, **Scale**, and **Alpha** sliders.
2. Observe the VR overlay quad for correct transformation.
3. Test **Reset** and **Save Default** buttons:
   - Reset restores saved baseline.
   - Save Default overwrites the stored baseline.

### 4.5. OpenXR/ReShade Menu Toggle Test
1. Click the **Menu** button.
2. Verify that the in‑HMD ReShade menu opens/closes.
3. Ensure edge‑triggered semantics prevent spamming.

### 4.6. Visual Masks Test
1. Select a mask from the dropdown.
2. Confirm that the mask texture applies correctly.
4. Test with an invalid mask path to verify graceful fallback.

### 4.7. Effect Runtime Sync Test
1. Switch between Gameplay and Tuning modes rapidly.
2. Verify that desktop effects are fully suppressed in Gameplay mode.
3. Confirm that no stray desktop effects remain after switching.

### 4.8. Loading/Progress Bar Test (Known Bug Fix)
1. Observe the loading bar during startup.
2. Ensure it completes and the UI updates correctly.
3. Verify that after the fix, `render_effects()` does not enable desktop effects in Gameplay mode.

### 4.9. Logging Verification
- Check `Logs\build_*.log` for errors.
- Confirm that any warnings are non‑critical.

---

## 5. Verifying Correct DLL Deployment
1. **File Hash Check**: Compare the hash of the deployed `ReShade64.dll` with the hash stored in `Deploy\hashes.txt`.
2. **Version Check**: Ensure the version embedded in the DLL matches the version in `Product.wxs`.
3. **Dependency Check**: Run `dependency walker` on the DLL to ensure no missing dependencies.

---

## 6. Avoiding Stale Builds
- **Clean** the build directory before each build (`git clean -fdx` or manually delete `bin/obj`).
- **Invalidate** the MSI version by bumping the version number.
- **Do not** copy old DLLs into the deployment folder; always use the freshly built artifact.

---

## 7. Continuous Integration (Optional)
- Set up a GitHub Actions workflow that runs `build.ps1` on each push to `master`.
- Publish the MSI as an artifact.
- Run automated tests on the built binaries.

*End of BUILD_AND_TEST.md.*