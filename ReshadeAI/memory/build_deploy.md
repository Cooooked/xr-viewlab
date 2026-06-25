# Build & Deploy

## Prerequisites

- Visual Studio 2022 (any edition, Build Tools is fine)
- CMake (bundled with VS at `C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe`)
- Windows SDK 10.0.26100.0 (for fxc.exe if shaders need recompiling)
- Admin rights for deploy step

## Build

```powershell
cd F:\ReshadeAI\reshade
cmake --build build --config Release --target ReShade
```

Output: `F:\ReshadeAI\reshade\build\Release\ReShade64.dll`

If CMake cache is missing (first time or after deleting build/):
```powershell
cmake -B build -G "Visual Studio 17 2022" -A x64
cmake --build build --config Release --target ReShade
```

**Check for errors:**
```powershell
cmake --build build --config Release --target ReShade 2>&1 | Select-String "(error C|error LNK|FAILED)"
```

## Deploy

**ALWAYS kill the game first. DLL is locked while game is running — deploy will silently fail.**

```powershell
# Kill game
Get-Process | Where-Object {$_.Name -match "Pistol|Whip|iRacing"} | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep 2

# Deploy (admin required — Claude can run this directly in admin mode)
Copy-Item "F:\ReshadeAI\reshade\build\Release\ReShade64.dll" "C:\ProgramData\ReShade\ReShade64.dll" -Force
```

If not in admin session:
```powershell
Start-Process -FilePath powershell.exe -Verb RunAs -ArgumentList `
  "-Command `"Copy-Item 'F:\ReshadeAI\reshade\build\Release\ReShade64.dll' 'C:\ProgramData\ReShade\ReShade64.dll' -Force`"" `
  -Wait
```

## Test (Pistol Whip)

```powershell
& "D:\VR Games\Pistol Whip-working\Pistol Whip.exe"
```

Wait ~15 seconds for full init. Put on headset. Press Home to toggle overlay.

## Check Logs

```powershell
# All overlay-related lines
Get-Content "D:\VR Games\Pistol Whip-working\ReShade.log" -Tail 100 | Select-String "\[OVERLAY\]"

# Last 50 lines (general)
Get-Content "D:\VR Games\Pistol Whip-working\ReShade.log" -Tail 50

# iRacing
Get-Content "C:\Program Files (x86)\iRacing\ReShade.log" -Tail 100 | Select-String "\[OVERLAY\]"
```

## One-Liner: Build + Deploy

```powershell
cd F:\ReshadeAI\reshade
cmake --build build --config Release --target ReShade 2>&1 | Select-String "(error|FAILED)" | Select-Object -Last 5
Get-Process | Where-Object {$_.Name -match "Pistol|Whip"} | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep 2
Copy-Item "build\Release\ReShade64.dll" "C:\ProgramData\ReShade\ReShade64.dll" -Force
Write-Host "Deployed"
```

## Shader Recompilation (only if .cso files are missing)

```powershell
$fxc = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\fxc.exe"
$shaders = @(
    @{src="copy_ps.hlsl";          ps="ps_4_0"},
    @{src="fullscreen_vs.hlsl";    ps="vs_4_0"},
    @{src="imgui_ps_3_0.hlsl";     ps="ps_3_0"},
    @{src="imgui_ps_4_0.hlsl";     ps="ps_4_0"},
    @{src="imgui_vs_3_0.hlsl";     ps="vs_3_0"},
    @{src="imgui_vs_4_0.hlsl";     ps="vs_4_0"},
    @{src="mipmap_cs_5_0.hlsl";    ps="cs_5_0"}
)
foreach ($s in $shaders) {
    $out = $s.src -replace "\.hlsl$", ".cso"
    & $fxc /Fo "res/shaders/$out" /T $s.ps "res/shaders/$($s.src)"
}
```
