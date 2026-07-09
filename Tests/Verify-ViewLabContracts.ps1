param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Read-RepoFile([string]$RelativePath) {
    $path = Join-Path $Root $RelativePath
    if (!(Test-Path -LiteralPath $path)) {
        throw "Missing required file: $RelativePath"
    }
    Get-Content -LiteralPath $path -Raw
}

function Assert-Contains([string]$RelativePath, [string]$Pattern, [string]$Message) {
    $content = Read-RepoFile $RelativePath
    if ($content -notmatch $Pattern) {
        throw "Contract failed: $Message`nFile: $RelativePath`nPattern: $Pattern"
    }
}

function Assert-IniValue([string]$Key, [string]$Value) {
    $ini = Read-RepoFile 'xr-viewlab.ini'
    $pattern = "(?m)^$([regex]::Escape($Key))=$([regex]::Escape($Value))\r?$"
    if ($ini -notmatch $pattern) {
        throw "Contract failed: expected xr-viewlab.ini to contain $Key=$Value"
    }
}

# ---- Default ini carries the visor shape keys -------------------------------------
Assert-IniValue 'mask_outer_apex_y' '0.0'
Assert-IniValue 'mask_inner_lower_y' '0.0'
Assert-IniValue 'mask_inner_bridge_width' '0.5'
Assert-IniValue 'stencil_outer_edges_only' '1'

# ---- UI sliders exist with the agreed ranges --------------------------------------
Assert-Contains 'MainWindow.xaml' 'Name="MaskApexYSlider"[^>]*Minimum="-0\.5"[^>]*Maximum="0\.5"' 'main window Apex Y slider range'
Assert-Contains 'MainWindow.xaml' 'Name="MaskInnerLowerSlider"[^>]*Minimum="0"[^>]*Maximum="0\.333"' 'main window Inner low slider range'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorApexYSlider"[^>]*Minimum="-0\.5"[^>]*Maximum="0\.5"' 'profile Apex Y slider range'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorInnerLowerSlider"[^>]*Minimum="0"[^>]*Maximum="0\.333"' 'profile Inner low slider range'

Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'private const string MaskOuterApexYKey = "mask_outer_apex_y";' 'main window has Apex Y config key'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'private const string MaskInnerLowerYKey = "mask_inner_lower_y";' 'main window has Inner low config key'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'private const string StencilOuterEdgesKey = "stencil_outer_edges_only";' 'main window writes the stencil key the DLL reads'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'MaskBeanEditor\.OpenInnerPreview = StencilOuterEdgesCheck' 'main preview follows the stencil/open-inner checkbox'

# ---- Preview exposes the shape controls -------------------------------------------
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'public double OuterApexY' 'preview exposes Apex Y'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'public double InnerLowerY' 'preview exposes Inner low'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'internal static double CurveExponent\(double curve\) => 32\.0 \* Math\.Pow\(2\.0 / 32\.0' 'preview uses geometric curve exponent'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'DrawPin\(dc, pins\.outerApex' 'preview draws outer apex pin'

# ---- Native layer: config plumbing ------------------------------------------------
Assert-Contains 'dllmain.cpp' 'visorOuterApexY = std::clamp\(ReadDoubleSetting\(L"mask_outer_apex_y", 0\.0\), -0\.5, 0\.5\);' 'native reads and clamps global Apex Y'
Assert-Contains 'dllmain.cpp' 'visorInnerLowerY = std::clamp\(ReadDoubleSetting\(L"mask_inner_lower_y", 0\.0\), 0\.0, 0\.333\);' 'native reads and clamps global Inner low'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_width", 0\.5\)' 'native reads bridge width'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_rise", 0\.0\)' 'native reads bridge rise'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_peak_x", 0\.5\)' 'native reads bridge peak x'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_steepness", 0\.5\)' 'native reads bridge steepness'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_outer_apex_y", profileOuterApexY\);' 'native reads per-app Apex Y'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_inner_lower_y", profileInnerLowerY\);' 'native reads per-app Inner low'
Assert-Contains 'dllmain.cpp' 'std::clamp\(SignedMillisToUnit\(profileOuterApexY, visorOuterApexY\), -0\.5, 0\.5\)' 'native clamps decoded per-app Apex Y'
Assert-Contains 'dllmain.cpp' 'visor_outer_apex_y=%.3f visor_inner_lower_y=%.3f' 'native config log includes new visor controls'

# ---- Native layer: geometry contracts ---------------------------------------------
Assert-Contains 'dllmain.cpp' 'const bool openInnerShape = outerEdgeVisibilityMaskOnly;' 'native Direct C geometry follows the same open-inner setting as preview'
Assert-Contains 'dllmain.cpp' 'BuildVisorBorderVerts\(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, verts, kMaxVerts\)' 'native has closed-bean Direct C geometry path'
Assert-Contains 'dllmain.cpp' 'BuildOpenInnerEyeVisorVerts\(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, outerLeft, verts, kMaxVerts\)' 'native has open-inner Direct C geometry path'
Assert-Contains 'dllmain.cpp' '32\.0f \* std::pow\(2\.0f / 32\.0f' 'native uses the same geometric curve exponent as the preview'
Assert-Contains 'dllmain.cpp' 'float ApexYFromConfigNdc\(double configApexY\)' 'UI-to-NDC y flip lives in exactly one helper'
Assert-Contains 'dllmain.cpp' 'BuildNoseBridgeCurve\(vertsOut, v, vertCapacity, cx, innerX, y0, bandTopY\);' 'nose divot is anchored to the NDC BOTTOM edge (y0), never y1'
Assert-Contains 'dllmain.cpp' 'y = std::clamp\(y, yBase, yTop\);' 'nose divot vertices are band-clamped (structurally bottom-only)'

# ---- Native layer: stencil fixes must survive (4.1.102/4.1.103) --------------------
Assert-Contains 'dllmain.cpp' 'ReadBoolSetting\(L"stencil_outer_edges_only",' 'DLL reads the checkbox key with legacy fallback'
Assert-Contains 'dllmain.cpp' 'const bool keepOuterEdge =' 'visibility-mask filter keeps outer-edge triangles'
Assert-Contains 'dllmain.cpp' 'if \(!openInnerShape && allViews\.size\(\) >= 2' 'partner-eye boundary only drawn when stencil checkbox is off'

# Pass 4 (pending rewrite): edge-smear FOV diagnostics contracts removed with the
# 4.1.55 revert. Restore these asserts when the diagnostics are reimplemented:
#   'outer_apex_y=%.3f inner_lower_y=%.3f' in verbose edge-smear contract log
#   'original_fov=\(L %.5f R %.5f U %.5f D %.5f valid=%d\)'
#   'latestOriginalViews\[i\] = originalViews\[i\];'
#   'EffectiveHorizontalScaleForView\(i, requestedWidthScale\)'
#   'EffectiveVerticalScaleForView\(i, requestedHeightScale\)'
#   'edge-smear fix: using FOV-weighted recommended image scale'

Write-Host 'ViewLab contract verification passed.'
