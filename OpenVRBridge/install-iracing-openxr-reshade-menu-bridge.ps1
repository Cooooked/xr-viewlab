$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$IRacingDir = "C:\Program Files (x86)\iRacing"
$BridgeDll = Join-Path $PSScriptRoot "x64\Release\openvr_api.dll"
$LayerDir = Join-Path $ProjectRoot "openxr-reshade-menu-layer"
$LayerManifest = Join-Path $LayerDir "XR_APILAYER_cooooked_reshade_menu_layer.json"
$LayerRegistryKey = "HKCU:\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit"

$TargetOpenVR = Join-Path $IRacingDir "openvr_api.dll"
$RealOpenVR = Join-Path $IRacingDir "openvr_api.real.dll"

if (-not (Test-Path $IRacingDir)) {
    throw "Missing iRacing folder: $IRacingDir"
}
if (-not (Test-Path $BridgeDll)) {
    throw "Missing bridge DLL. Build first: $BridgeDll"
}
if (-not (Test-Path $LayerManifest)) {
    throw "Missing OpenXR layer manifest: $LayerManifest"
}
if (-not (Test-Path $TargetOpenVR) -and -not (Test-Path $RealOpenVR)) {
    throw "No iRacing openvr_api.dll found to proxy."
}

if (-not (Test-Path $RealOpenVR)) {
    Rename-Item -LiteralPath $TargetOpenVR -NewName "openvr_api.real.dll"
    Write-Host "Backed up iRacing openvr_api.dll -> openvr_api.real.dll"
} else {
    Write-Host "Real OpenVR backup already exists."
}

Copy-Item -LiteralPath $BridgeDll -Destination $TargetOpenVR -Force
Write-Host "Installed OpenVR overlay bridge:"
Write-Host $TargetOpenVR

New-Item -Path $LayerRegistryKey -Force | Out-Null
New-ItemProperty -Path $LayerRegistryKey -Name $LayerManifest -PropertyType DWord -Value 0 -Force | Out-Null
Write-Host "Enabled OpenXR menu layer:"
Write-Host $LayerManifest

Write-Host ""
Write-Host "Done. Launch iRacing in OpenXR, then press Home."

