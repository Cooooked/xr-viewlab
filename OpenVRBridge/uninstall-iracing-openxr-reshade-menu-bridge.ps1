$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$IRacingDir = "C:\Program Files (x86)\iRacing"
$LayerManifest = Join-Path (Join-Path $ProjectRoot "openxr-reshade-menu-layer") "XR_APILAYER_cooooked_reshade_menu_layer.json"
$LayerRegistryKey = "HKCU:\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit"

$TargetOpenVR = Join-Path $IRacingDir "openvr_api.dll"
$RealOpenVR = Join-Path $IRacingDir "openvr_api.real.dll"

if (Test-Path $RealOpenVR) {
    Remove-Item -LiteralPath $TargetOpenVR -Force -ErrorAction SilentlyContinue
    Rename-Item -LiteralPath $RealOpenVR -NewName "openvr_api.dll"
    Write-Host "Restored original iRacing openvr_api.dll"
}

if (Test-Path $LayerRegistryKey) {
    Remove-ItemProperty -Path $LayerRegistryKey -Name $LayerManifest -ErrorAction SilentlyContinue
    Write-Host "Disabled OpenXR menu layer:"
    Write-Host $LayerManifest
}

Write-Host "Uninstall complete."

