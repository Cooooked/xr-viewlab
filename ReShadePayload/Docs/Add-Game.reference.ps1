<#
.SYNOPSIS
  Prepare a VR game folder to use the global ReShade OpenXR layer.
.DESCRIPTION
  Injection is handled by the GLOBAL OpenXR implicit layer (deployed by Deploy-ReShade.ps1), so this
  does NOT drop a dll in the game folder. It only:
    - removes conflicting game-folder injectors (dxgi.dll / d3d11.dll ReShade) that would double-load
    - seeds reshade-shaders + a minimal ReShade.ini (effect search paths + Home menu key)
    - ensures the XR layer is registered & enabled in the registry
  Effects are configured per-game in ReShadeVR.ini (created on first launch). The in-HMD menu quad
  position is global: C:\ProgramData\ReShade\openxr_quad_transform.ini
.EXAMPLE
  .\Add-Game.ps1 -GameDir "D:\VR Games\Some VR Game"
#>
param(
  [Parameter(Mandatory)] [string]$GameDir,
  [string]$ShaderSource = "D:\VR Games\DiRT Rally 2.0 - RESHADE working\reshade-shaders",
  [string]$LayerJson    = "C:\ProgramData\ReShade\ReShade64_XR.json"
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path $GameDir)) { throw "GameDir not found: $GameDir" }

# 1) Remove conflicting in-folder injectors (the global XR layer does the injection)
foreach ($n in 'dxgi.dll','d3d11.dll','d3d12.dll','opengl32.dll') {
  $f = Join-Path $GameDir $n
  if (Test-Path $f) { Move-Item $f "$f.bak" -Force; Write-Host "Disabled conflicting $n -> $n.bak" -ForegroundColor Yellow }
}

# 2) Seed shaders
$destShaders = Join-Path $GameDir "reshade-shaders"
if (-not (Test-Path $destShaders) -and (Test-Path $ShaderSource)) {
  Copy-Item $ShaderSource $destShaders -Recurse -Force
  Write-Host "Copied reshade-shaders" -ForegroundColor Green
}

# 3) Seed a minimal ReShade.ini if none exists
$ini = Join-Path $GameDir "ReShade.ini"
if (-not (Test-Path $ini)) {
@"
[GENERAL]
EffectSearchPaths=.\reshade-shaders\Shaders\**
TextureSearchPaths=.\reshade-shaders\Textures\**
PresetPath=.\ReShadePreset.ini

[INPUT]
KeyOverlay=36,0,0,0
"@ | Set-Content -Path $ini -Encoding UTF8
  Write-Host "Wrote minimal ReShade.ini (Home = menu)" -ForegroundColor Green
}

# 4) Ensure the global XR layer is registered & enabled (0 = enabled)
$key = 'HKLM:\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit'
$val = (Get-ItemProperty -Path $key -ErrorAction SilentlyContinue).$LayerJson
if ($null -eq $val) {
  New-ItemProperty -Path $key -Name $LayerJson -PropertyType DWord -Value 0 -Force | Out-Null
  Write-Host "Registered ReShade XR layer" -ForegroundColor Green
} elseif ($val -ne 0) {
  Set-ItemProperty -Path $key -Name $LayerJson -Value 0
  Write-Host "Enabled ReShade XR layer (was disabled)" -ForegroundColor Green
} else {
  Write-Host "ReShade XR layer already enabled" -ForegroundColor DarkGray
}
Write-Host "`nDone. Launch the game; press Home in-HMD for the menu. Set effects in ReShadeVR.ini." -ForegroundColor Cyan
