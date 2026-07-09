<#
.SYNOPSIS
  Build the patched ReShade and deploy it as the global OpenXR implicit layer.
.DESCRIPTION
  Builds source from F:\AI-Projects\ReshadeAI\reshade, then copies ReShade64.dll to
  C:\ProgramData\ReShade\ReShade64.dll (the DLL referenced by the registered XR layer JSON).
  Requires admin to write ProgramData. Kills running VR games first (DLL is locked while loaded).
.NOTES
  The XR layer registration (ReShade64_XR.json under HKLM\...\OpenXR\1\ApiLayers\Implicit, value 0)
  is assumed already present. Run Register-Layer (see Add-Game.ps1 notes) if it is missing.
#>
param(
  [string]$ReshadeSrc = "F:\AI-Projects\ReshadeAI\reshade",
  [switch]$SkipBuild
)
$ErrorActionPreference = "Stop"
$cmake = "C:\Program Files\Microsoft Visual Studio\18\Insiders\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
$dll   = Join-Path $ReshadeSrc "build\Release\ReShade64.dll"
$dest  = "C:\ProgramData\ReShade\ReShade64.dll"

if (-not $SkipBuild) {
  Write-Host "Building ReShade..." -ForegroundColor Cyan
  & $cmake --build (Join-Path $ReshadeSrc "build") --config Release --target ReShade 2>&1 |
    Select-String "(error C|error LNK|FAILED|-> .*ReShade64.dll)"
}
if (-not (Test-Path $dll)) { throw "Build output not found: $dll" }

# Kill any VR game holding the layer DLL
Get-Process -ErrorAction SilentlyContinue |
  Where-Object { $_.Name -match '(?i)dirtrally|PistolWhip|Pistol Whip|iRacing|acs|TetrisEffect|GolfIt|Pinball' } |
  Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800

# Back up current deployed DLL once
if ((Test-Path $dest) -and -not (Test-Path "$dest.prev.bak")) { Copy-Item $dest "$dest.prev.bak" -Force }

Copy-Item $dll $dest -Force
$i = Get-Item $dest
Write-Host ("Deployed -> {0}  ({1:N0} KB, {2})" -f $dest, ($i.Length/1KB), $i.LastWriteTime) -ForegroundColor Green
