# XR ViewLab — Source & Backup Reference

## Active Source

```
F:\AI-Projects\ViewLab\
```

Current version: **4.1.7** — see `CHANGELOG.md` and `PROJECT_GOAL.md`.

---

## Backup Sources

### In-tree backups (`backups/`)

| Backup | Date | Contents |
|--------|------|----------|
| `backups/2026-06-25_1350` | 2026-06-25 | Pre-v4.1.7 snapshot |
| `backups/2026-06-25_v4.1` | 2026-06-25 | v4.1 codebase |
| `backups/2026-06-25_v4.1-final` | 2026-06-25 | v4.1-final codebase |

### Off-tree backup archive (`F:\viewlab-old-donotreference\`)

Do not reference this path from code. Use only for manual recovery.

| Path | Contents |
|------|----------|
| `MenuLayer\` | Standalone OpenXR API layer ReShade menu DLL source |
| `reference\recovered-reshade-menu-backend\` | Recovered OpenVR bridge + ReShade menu backend code |
| `Payloads\ReShade-OpenXR-Minimal_2026-06-23_133340\` | Minimal ReShade payload (DLL + configs) |
| `Backups\ReShade-OpenXR-Working_2026-06-23_133224\` | Full working ReShade OpenXR integration (source + built DLL + configs) |
| `RESHADE_OPENXR_WORKING_SUMMARY.md` | Detailed docs for the working ReShade OpenXR build |
| `XR_APILAYER_cooooked_reshade_menu_layer.dll` | Compiled ReShade menu OpenXR layer DLL |
| `XR_APILAYER_cooooked_reshade_menu_layer.json` | Manifest for above |
| `old-msis\` | Previous MSI installer builds |
| `docs\` | Older documentation |

### Reference materials (`reference/`)

| File | Contents |
|------|----------|
| `reference/OpenKneeboard-OpenXR-rendering-notes.md` | OpenXR 3D quad rendering reference (OKB patterns) |

---

## ReShade Mod Source

The working ReShade OpenXR integration is backed up at:

```
F:\viewlab-old-donotreference\Backups\ReShade-OpenXR-Working_2026-06-23_133224\
├── ReShadeSource/           (1.1 GB — full ReShade 6.7.3.7 source tree with detour hooks)
├── ReShade64.dll            (Built 64-bit Release, 5.3 MB)
├── ReShade64_XR.json        (OpenXR layer manifest)
├── ReShade.log              (Verified test run log)
├── ReShade.ini / ReShadeVR.ini / ASCII.ini  (Configs)
└── BUILD_NOTES.md
```

### Build from source

```powershell
cd "F:\viewlab-old-donotreference\Backups\ReShade-OpenXR-Working_2026-06-23_133224\ReShadeSource"
MSBuild.exe ReShade.sln /p:Configuration=Release /p:Platform="64-bit" /t:Rebuild
# Output: bin\x64\Release\ReShade64.dll
```

### Key modified source files (ReShade detour hooks)

- `source/openxr/openxr_hooks_instance.cpp` — hook installation on runtime dispatch table
- `source/openxr/openxr_hooks_swapchain.cpp` — xrCreateSession hook
- `source/openxr/openxr.cpp` — xrGetInstanceProcAddr dispatcher

### Standalone ReShade menu layer

```
F:\viewlab-old-donotreference\MenuLayer\
├── dllmain.cpp              (Standalone ReShade menu OpenXR layer)
├── MenuLayer.vcxproj
├── bin\                     (Build output)
└── obj\
```

### Minimal payload (for distribution)

```
F:\viewlab-old-donotreference\Payloads\ReShade-OpenXR-Minimal_2026-06-23_133340\
```

---

## Build Pipeline

```powershell
cd F:\AI-Projects\ViewLab
Set-ExecutionPolicy -Scope Process Bypass -Force
.\build.ps1
```

Produces:
- `bin\Release\net8.0-windows\win-x64\publish\xr-viewlab.exe` (WPF app, single-file)
- `x64\Release\XR_APILAYER_cooooked_xrviewlab.dll` (OpenXR layer)
- `dist\XR-ViewLab-<version>.msi` (installer)

---

## Version History

| Version | Date | Key Changes |
|---------|------|-------------|
| 4.1.7 | 2026-06-25 | Removed stale OpenVRBridge/ReShadePayload/ReshadeAI files. Fixed installer. |
| 4.1.6 | 2026-06-25 | OpenXR ReShade shared memory controls. Heartbeat fix. Gameplay/desktop menu split. |
| 4.1.0 | 2026-06-25 | Responsive layout. Visual Masks flyout. XR type column. Auto-save. |
| 4.0.x | earlier | See git history. |

---

## How to Rebuild Everything

### ViewLab
```powershell
cd F:\AI-Projects\ViewLab
.\build.ps1
```
Fresh copy: clone `https://github.com/Cooooked/xr-viewlab.git`, run `build.ps1`.

### ReShade mod (from backup)
```powershell
cd "F:\viewlab-old-donotreference\Backups\ReShade-OpenXR-Working_2026-06-23_133224\ReShadeSource"
MSBuild.exe ReShade.sln /p:Configuration=Release /p:Platform="64-bit" /t:Rebuild
```

### Standalone menu layer
```powershell
cd "F:\viewlab-old-donotreference\MenuLayer"
MSBuild.exe MenuLayer.vcxproj /p:Configuration=Release /p:Platform=x64
```
