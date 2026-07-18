$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'ReShadePayloadSource'
$builtDll = Join-Path $source 'build\Release\ReShade64.dll'
$bundledDll = Join-Path $root 'ReShadePayload\ReShade64.dll'

function Require-Text([string]$Path, [string]$Pattern, [string]$Description) {
    if (!(Test-Path -LiteralPath $Path)) { throw "Missing $Description file: $Path" }
    if (!(Select-String -LiteralPath $Path -Pattern $Pattern -Quiet)) {
        throw "Missing $Description marker '$Pattern' in $Path"
    }
}

Require-Text (Join-Path $source 'source\openxr\openxr_overlay.hpp') 'Local\\\\ReShadeXRControl' 'shared-memory contract'
Require-Text (Join-Path $source 'source\openxr\openxr_hooks_swapchain.cpp') 'staging_textures\[2\]' 'double-buffered preview readback'
Require-Text (Join-Path $source 'source\openxr\openxr_overlay_preview.cpp') 'WM_ERASEBKGND' 'flicker-free preview paint'
Require-Text (Join-Path $source 'source\openxr\openxr_overlay_preview.cpp') 'preview_consume_keyboard_input' 'desktop keyboard bridge'
Require-Text (Join-Path $source 'source\runtime_gui_vr.cpp') 'AddInputCharacterUTF16' 'ImGui keyboard input'
Require-Text (Join-Path $source 'README.ViewLab.md') '4a50d1eddace85734871d91792ff214f13f66c01' 'source provenance'

if (!(Test-Path -LiteralPath $builtDll)) { throw "Focused payload build output is missing: $builtDll" }
if (!(Test-Path -LiteralPath $bundledDll)) { throw "Bundled payload is missing: $bundledDll" }
$builtHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $builtDll).Hash
$bundledHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $bundledDll).Hash
if ($builtHash -ne $bundledHash) {
    throw "Bundled payload hash $bundledHash does not match focused build $builtHash"
}

$binaryText = [Text.Encoding]::Unicode.GetString([IO.File]::ReadAllBytes($builtDll))
foreach ($marker in @('Local\ReShadeXRControl', 'ReShadeVRPreview', 'openxr_quad_transform.ini')) {
    if (!$binaryText.Contains($marker)) { throw "Focused payload output is missing binary marker '$marker'" }
}

Write-Host "PASS: canonical ViewLab ReShade source markers and payload hash parity ($builtHash)"
