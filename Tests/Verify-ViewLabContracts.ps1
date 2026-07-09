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
    $pattern = "(?m)^$([regex]::Escape($Key))=$([regex]::Escape($Value))$"
    if ($ini -notmatch $pattern) {
        throw "Contract failed: expected xr-viewlab.ini to contain $Key=$Value"
    }
}

Assert-IniValue 'mask_outer_apex_y' '0'
Assert-IniValue 'mask_inner_lower_y' '0'
Assert-IniValue 'visor_technique' 'c'

Assert-Contains 'MainWindow.xaml' 'Name="MaskOuterApexSlider"[^>]*Minimum="-0\.5"[^>]*Maximum="0\.5"' 'main window Apex Y slider range'
Assert-Contains 'MainWindow.xaml' 'Name="MaskInnerLowerSlider"[^>]*Minimum="0"[^>]*Maximum="0\.333"' 'main window Inner low slider range'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorOuterApexSlider"[^>]*Minimum="-0\.5"[^>]*Maximum="0\.5"' 'profile Apex Y slider range'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorInnerLowerSlider"[^>]*Minimum="0"[^>]*Maximum="0\.333"' 'profile Inner low slider range'

Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'private const string MaskOuterApexYKey = "mask_outer_apex_y";' 'main window has Apex Y config key'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'private const string MaskInnerLowerYKey = "mask_inner_lower_y";' 'main window has Inner low config key'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'WritePrivateProfileString\("Settings", MaskOuterApexYKey, FormatStorageScale\(MaskOuterApexSlider\.Value\), ConfigPath\)' 'main window saves Apex Y'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'WritePrivateProfileString\("Settings", MaskInnerLowerYKey, FormatStorageScale\(MaskInnerLowerSlider\.Value\), ConfigPath\)' 'main window saves Inner low'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'MaskBeanEditor\.OpenInnerPreview = openInner;' 'main preview follows stencil/open-inner mode'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'MaskInnerLowerSlider\.IsEnabled = openInner;' 'main Inner low slider is active only when open-inner mode is visible'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'StencilOuterEdgesCheck\?\.IsChecked == true\)' 'profile window receives the current open-inner mode'

Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'bool openInnerPreview' 'profile constructor accepts open-inner preview mode'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'MaskBeanEditor\.OpenInnerPreview = _openInnerPreview;' 'profile preview uses passed open-inner mode'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'VisorInnerLowerSlider\.IsEnabled = enabled && _openInnerPreview;' 'profile Inner low slider is active only when open-inner mode is visible'

Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'public double OuterApexY' 'preview exposes Apex Y'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'public double InnerLowerY' 'preview exposes Inner low'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'DrawPin\(dc, pins\.outerApex' 'preview draws outer apex pin'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'OpenInnerPreview && DistanceSquared\(p, pins\.innerLower\)' 'preview only drags inner-low pin in open-inner mode'

Assert-Contains 'dllmain.cpp' 'visorOuterApexY = std::clamp\(ReadDoubleSetting\(L"mask_outer_apex_y", 0\.0\), -0\.5, 0\.5\);' 'native reads and clamps global Apex Y'
Assert-Contains 'dllmain.cpp' 'visorInnerLowerY = std::clamp\(ReadDoubleSetting\(L"mask_inner_lower_y", 0\.0\), 0\.0, 0\.333\);' 'native reads and clamps global Inner low'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_outer_apex_y", profileOuterApexY\);' 'native reads per-app Apex Y'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_inner_lower_y", profileInnerLowerY\);' 'native reads per-app Inner low'
Assert-Contains 'dllmain.cpp' 'std::clamp\(SignedMillisToUnit\(profileOuterApexY, visorOuterApexY\), -0\.5, 0\.5\)' 'native clamps decoded per-app Apex Y'
Assert-Contains 'dllmain.cpp' 'const bool openInnerShape = outerEdgeVisibilityMaskOnly;' 'native Direct C geometry follows the same open-inner setting as preview'
Assert-Contains 'dllmain.cpp' 'BuildVisorBorderVerts\(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, verts, kMaxVerts\)' 'native has closed-bean Direct C geometry path'
Assert-Contains 'dllmain.cpp' 'BuildOpenInnerEyeVisorVerts\(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, outerLeft, verts, kMaxVerts\)' 'native has open-inner Direct C geometry path'
Assert-Contains 'dllmain.cpp' 'visor_outer_apex_y=%.3f visor_inner_lower_y=%.3f' 'native config log includes new visor controls'
Assert-Contains 'dllmain.cpp' 'outer_apex_y=%.3f inner_lower_y=%.3f' 'verbose edge-smear diagnostics include new visor controls'
Assert-Contains 'dllmain.cpp' 'original_fov=\(L %.5f R %.5f U %.5f D %.5f valid=%d\)' 'verbose edge-smear diagnostics include original runtime FOV'
Assert-Contains 'dllmain.cpp' 'latestOriginalViews\[i\] = originalViews\[i\];' 'edge-smear path stores original runtime FOV before ViewLab crop'
Assert-Contains 'dllmain.cpp' 'EffectiveHorizontalScaleForView\(i, requestedWidthScale\)' 'recommended width can use FOV-weighted crop scale'
Assert-Contains 'dllmain.cpp' 'EffectiveVerticalScaleForView\(i, requestedHeightScale\)' 'recommended height can use FOV-weighted crop scale'
Assert-Contains 'dllmain.cpp' 'edge-smear fix: using FOV-weighted recommended image scale' 'native logs FOV-weighted recommended-size correction once'

Write-Host 'ViewLab contract verification passed.'
