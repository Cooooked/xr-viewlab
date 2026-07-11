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

function Assert-NotContains([string]$RelativePath, [string]$Pattern, [string]$Message) {
    $content = Read-RepoFile $RelativePath
    if ($content -match $Pattern) {
        throw "Contract failed: $Message`nFile: $RelativePath`nForbidden pattern: $Pattern"
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
Assert-IniValue 'mask_enabled' '0'
Assert-IniValue 'mask_size' '0.82'
Assert-IniValue 'mask_corner' '0.5'
Assert-IniValue 'visor_hd' '0'
Assert-IniValue 'visor_antialiasing' '1'
Assert-IniValue 'visibility_mask_visor' '0'
Assert-IniValue 'edge_smear_pixels' '2'
Assert-IniValue 'verbose_logging' '0'
Assert-IniValue 'mask_outer_apex_y' '0.0'
Assert-IniValue 'mask_inner_lower_y' '0.0'
Assert-IniValue 'mask_inner_bridge_width' '0.5'
Assert-IniValue 'mask_inner_bridge_rise' '0.0'
Assert-IniValue 'mask_inner_bridge_peak_x' '0.5'
Assert-IniValue 'mask_inner_bridge_steepness' '0.5'
Assert-IniValue 'stencil_outer_edges_only' '1'

# ---- Installer reset policy is explicit and complete -------------------------------
Assert-NotContains 'Installer\PreserveConfig.vbs' '%LOCALAPPDATA%' 'installer VBScript does not touch current-user LOCALAPPDATA'
Assert-NotContains 'Installer\PreserveConfig.vbs' 'H80000001' 'installer VBScript does not touch current-user HKCU'
Assert-Contains 'Installer\Product.wxs' 'File Id="DefaultConfigFile" Source="\.\.\\xr-viewlab\.ini" Name="xr-viewlab\.ini"' 'MSI packages the default config file'
Assert-Contains 'xr-viewlab.csproj' 'CopyToPublishDirectory="PreserveNewest"' 'published app carries xr-viewlab.ini for installer harvesting'
Assert-Contains 'build.ps1' '\$DefaultConfigSrc = Join-Path \$Root "xr-viewlab\.ini"' 'build copies default config into publish output'
Assert-Contains 'build.ps1' 'x64\\\$Configuration\\XR_APILAYER_cooooked_xrviewlab\.dll' 'build harvests freshly-built x64 native DLL'
Assert-NotContains 'Installer\Product.wxs' 'VisorResetVersion' 'ordinary MSI upgrades preserve user visor settings'
Assert-Contains 'Installer\Product.wxs' 'Root="HKLM" Key="Software\\cooooked\\xr-viewlab" Name="StartMenuShortcutInstalled"[^>]*KeyPath="yes"' 'per-machine Start Menu component uses HKLM key path'
Assert-NotContains 'Installer\Product.wxs' 'Custom Action="ResetVisorSettings"' 'installer does not run current-user visor reset from elevated MSI context'
Assert-NotContains 'Installer\Product.wxs' 'Custom Action="BackupConfig"' 'installer does not back up LOCALAPPDATA from elevated MSI context'
Assert-NotContains 'XRViewLab.UI\MainWindow.cs' 'ApplyPendingInstallVisorReset\(\);' 'UI startup does not reset user visor settings after upgrades'
Assert-NotContains 'dllmain.cpp' 'ApplyPendingInstallVisorReset\(\);' 'OpenXR runtime path never resets user visor settings'
Assert-Contains 'dllmain.cpp' 'if \(draw\.tex\) draw\.tex->AddRef\(\);' 'late fallback holds the swapchain texture alive outside the map lock'
Assert-Contains 'dllmain.cpp' 'p\.tex->Release\(\);' 'late fallback releases its swapchain texture reference'
Assert-Contains 'dllmain.cpp' 'cropOuterEdgesOnly && viewIndex == 0' 'outer-edge crop is applied only when its checkbox is enabled'
Assert-Contains 'XRViewLab.UI\ReShadeRemoteWindow.cs' 'EncodedCommand' 'ReShade payload installation uses robust PowerShell command encoding'
Assert-Contains 'XRViewLab.UI\ReShadeRemoteWindow.cs' 'IsBundledPayloadDeployed' 'ReShade Remote reports whether its custom payload actually deployed'
Assert-Contains 'XRViewLab.UI\ReShadeRemoteWindow.cs' 'Environment\.ProcessPath' 'payload lookup uses the real exe dir, not the single-file TEMP extraction dir'
Assert-NotContains 'dllmain.cpp' 'config: file changed, hot-reloading' 'native config remains stable for the running game'
Assert-Contains 'dllmain.cpp' 'visorAntialiasing [?] g_d3d11Mask\.bs : g_d3d11Mask\.bsOpaque' 'AA-off visor draws with the proven opaque (blend-disabled) pipeline'
Assert-Contains 'dllmain.cpp' 'std::recursive_mutex g_rendererMutex;' 'D3D11 renderer lifetime and immediate context have a dedicated lock'
Assert-Contains 'dllmain.cpp' 'XRViewLab_xrReleaseSwapchainImage[\s\S]*?rendererLock\(g_rendererMutex\)' 'release-time drawing holds the renderer lock'
Assert-Contains 'dllmain.cpp' 'XRViewLab_xrEndFrame[\s\S]*?rendererLock\(g_rendererMutex\)' 'late drawing holds the renderer lock'

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
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'CaptureMouse\(\);' 'preview captures mouse after pin hit'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'ReleaseMouseCapture\(\);' 'preview releases mouse capture on mouse-up'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'OnLostMouseCapture' 'preview clears drag state on lost capture'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'double centerY = \(pins\.y0 \+ pins\.y1\) \* 0\.5;' 'apex pin drag uses the same center-origin inverse as the rendered geometry'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'HitTestCore\(PointHitTestParameters hitTestParameters\)' 'preview is hit-testable across its full rectangle'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'OnPreviewMouseLeftButtonDown' 'preview receives mouse before parent scroll/title handlers'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'SetSliderValue\(MaskApexYSlider, MaskBeanEditor\.OuterApexY\);' 'main window syncs dragged editor apex back to sliders'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'SetSliderValue\(MaskInnerBridgeSlider, MaskBeanEditor\.InnerBridgeWidth\);' 'main window syncs dragged bridge pin back to sliders'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'VisorApexYSlider\.Value = MaskBeanEditor\.OuterApexY;' 'profile window syncs dragged editor apex back to sliders'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'VisorInnerBridgeSlider\.Value = MaskBeanEditor\.InnerBridgeWidth;' 'profile window syncs dragged bridge pin back to sliders'

# ---- Native layer: config plumbing ------------------------------------------------
Assert-Contains 'dllmain.cpp' 'visorOuterApexY = std::clamp\(ReadDoubleSetting\(L"mask_outer_apex_y", 0\.0\), -0\.5, 0\.5\);' 'native reads and clamps global Apex Y'
Assert-Contains 'dllmain.cpp' 'visorInnerLowerY = std::clamp\(ReadDoubleSetting\(L"mask_inner_lower_y", 0\.0\), 0\.0, 0\.333\);' 'native reads and clamps global Inner low'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_width", 0\.5\)' 'native reads bridge width'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_rise", 0\.0\)' 'native reads bridge rise'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_peak_x", 0\.5\)' 'native reads bridge peak x'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_steepness", 0\.5\)' 'native reads bridge steepness'
Assert-Contains 'dllmain.cpp' 'visorHD = ReadBoolSetting\(L"visor_hd", false\);' 'native reads HD visor setting'
Assert-Contains 'dllmain.cpp' 'visorAntialiasing = ReadBoolSetting\(L"visor_antialiasing", true\);' 'native reads AA visor setting'
Assert-Contains 'dllmain.cpp' 'cropOuterEdgesOnly = ReadBoolSetting\(L"crop_outer_edges_only", true\);' 'native reads crop outer edges mode'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_outer_apex_y", profileOuterApexY\);' 'native reads per-app Apex Y'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_inner_lower_y", profileInnerLowerY\);' 'native reads per-app Inner low'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_inner_bridge_rise", profileBridgeRise\);' 'native reads per-app bridge rise'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_inner_bridge_peak_x", profileBridgePeakX\);' 'native reads per-app bridge peak x'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_inner_bridge_steepness", profileBridgeSteepness\);' 'native reads per-app bridge steepness'
Assert-Contains 'dllmain.cpp' 'std::clamp\(SignedMillisToUnit\(profileOuterApexY, visorOuterApexY\), -0\.5, 0\.5\)' 'native clamps decoded per-app Apex Y'
Assert-Contains 'dllmain.cpp' 'visor_hd=%d visor_antialiasing=%d' 'native config log includes AA/HD controls'
Assert-Contains 'dllmain.cpp' 'visor_outer_apex_y=%.3f visor_inner_lower_y=%.3f' 'native config log includes new visor controls'
Assert-Contains 'dllmain.cpp' 'crop_outer_edges_only=%d' 'native config and verbose logs include actual crop mode'

# ---- Native layer: geometry contracts ---------------------------------------------
Assert-Contains 'dllmain.cpp' 'const bool openInnerShape = outerEdgeVisibilityMaskOnly;' 'native visor geometry follows the same open-inner setting as preview'
Assert-Contains 'dllmain.cpp' 'BuildVisorBorderVerts\(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, featherX, featherY, verts\.data\(\), kMaxVerts\)' 'native has closed-bean visor geometry path'
Assert-Contains 'dllmain.cpp' 'BuildOpenInnerEyeVisorVerts\(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, outerLeft, featherX, featherY, verts\.data\(\), kMaxVerts\)' 'native has open-inner visor geometry path'
Assert-Contains 'dllmain.cpp' '32\.0f \* std::pow\(2\.0f / 32\.0f' 'native uses the same geometric curve exponent as the preview'
Assert-Contains 'dllmain.cpp' 'float ApexYFromConfigNdc\(double configApexY\)' 'UI-to-NDC y flip lives in exactly one helper'
Assert-Contains 'dllmain.cpp' 'BuildNoseBridgeCurve\(vertsOut, v, vertCapacity, cx, innerX, y0, bandTopY\);' 'nose divot is anchored to the NDC BOTTOM edge (y0), never y1'
Assert-Contains 'dllmain.cpp' 'y = std::clamp\(y, yBase, yTop\);' 'nose divot vertices are band-clamped (structurally bottom-only)'
Assert-Contains 'dllmain.cpp' 'struct VisorVertex' 'native visor vertices carry alpha for feathered AA'
Assert-Contains 'dllmain.cpp' 'D3D11_BLEND_SRC_ALPHA' 'native visor draw uses SRC_ALPHA blending'
Assert-Contains 'dllmain.cpp' 'VisorCurveSegments' 'native HD setting controls curve tessellation'
Assert-Contains 'dllmain.cpp' 'const float topFeatherY = std::clamp\(y0 \+ featherY, y0, y1\);' 'open-inner visor path uses vertical AA feathering'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'double dir = dx > 0\.0 \? 1\.0 : -1\.0;' 'preview nose bridge handles mirror correctly'

# ---- Native layer: stencil fixes must survive (4.1.102/4.1.103) --------------------
Assert-Contains 'dllmain.cpp' 'ReadBoolSetting\(L"stencil_outer_edges_only",' 'DLL reads the checkbox key with legacy fallback'
Assert-Contains 'dllmain.cpp' 'const bool keepOuterEdge =' 'visibility-mask filter keeps outer-edge triangles'
Assert-Contains 'dllmain.cpp' 'if \(!openInnerShape && allViews\.size\(\) >= 2' 'partner-eye boundary only drawn when stencil checkbox is off'
Assert-Contains 'dllmain.cpp' 'visibility_mask_visor=1 is retired' 'legacy visibility-mask visor path is explicitly ignored'
Assert-Contains 'dllmain.cpp' 'visibilityMaskVisor = false;' 'legacy visibility-mask visor cannot reshape current visor geometry'

# ---- Per-app profile shape coverage -----------------------------------------------
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.SetValue\("mask_inner_bridge_rise", ToMillis\(visorInnerBridgeRise, 0\.5\)' 'UI writes per-app bridge rise'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.SetValue\("mask_inner_bridge_peak_x", ToMillis\(visorInnerBridgePeakX\)' 'UI writes per-app bridge peak x'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.SetValue\("mask_inner_bridge_steepness", ToMillis\(visorInnerBridgeSteepness\)' 'UI writes per-app bridge steepness'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.DeleteValue\("mask_enabled", throwOnMissingValue: false\);' 'UI does not write unsupported per-app visor enable override'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.DeleteValue\("mask_inner_bridge_rise"' 'UI reset deletes per-app bridge rise'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.DeleteValue\("mask_inner_bridge_peak_x"' 'UI reset deletes per-app bridge peak x'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.DeleteValue\("mask_inner_bridge_steepness"' 'UI reset deletes per-app bridge steepness'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'ReadScaleSetting\(MaskSizeKey, 0\.82\)' 'missing mask_size falls back directly to the safe 0.82 default'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'MaskRoundnessSlider\.Value = 0\.5;' 'curve right-click reset returns to the safe default'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'private const string HorizVisualMaskBothKey = "horizontal_visual_mask_only";' 'horizontal Edge Masks checkbox writes the DLL key'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'private const string VertVisualMaskBothKey = "visual_mask_only";' 'vertical Edge Masks checkbox writes the DLL key'
Assert-Contains 'MainWindow.xaml' 'Name="HorizOuterEyeMaskCheck"[^>]*IsEnabled="False"' 'unsupported one-sided horizontal edge mask control is disabled'
Assert-Contains 'MainWindow.xaml' 'Name="VertTopMaskCheck"[^>]*IsEnabled="False"' 'unsupported one-sided vertical edge mask control is disabled'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'UseGlobal = UseGlobalVisorCheck\.IsChecked == true;' 'profile save preserves use-global when selected'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' '_initialized = true;' 'profile window enables event handlers after manual initialization'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'LoadCustomVisorValues\(\);' 'profile use-global toggle can restore original custom values'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'SyncMaskEditorFromSliders\(\);' 'profile use-global toggle updates preview'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'VisorEnabledCheck\.IsEnabled = false;' 'profile editor disables unsupported per-app visor enable'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'MaskBeanEditor\.IsEnabled = enabled;' 'profile preview pins disable in use-global mode'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'FromMillis\(appKey\.GetValue\("mask_inner_bridge_width"\), 0\.5\)' 'missing per-app bridge width falls back to safe centered default'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'MaskRoundedCheck\.IsChecked = ReadBoolSetting\(MaskRoundedKey, fallback: true\);' 'main window loads mask_rounded instead of hardcoding it'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'WritePrivateProfileString\("Settings", MaskRoundedKey, MaskRoundedCheck\.IsChecked == true \? "1" : "0", ConfigPath\);' 'main window saves mask_rounded from the roundness checkbox'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'double fallbackMaskCorner = ReadScaleSetting\(MaskCornerKey, 0\.5\);' 'per-app profile fallback uses canonical mask_corner default'
Assert-Contains 'XRViewLab.UI\AppProfile.cs' 'return \$"\{Top:0\.###\};\{Bottom:0\.###\};\{Horizontal:0\.###\};\{RenderScale \* 100\.0:0\.####\}%";' 'app summary does not claim unsupported per-app visor enable'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorInnerBridgeRiseSlider"[^>]*Minimum="0"[^>]*Maximum="0\.5"' 'profile bridge rise slider range'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorInnerBridgePeakXSlider"[^>]*Minimum="0"[^>]*Maximum="1"' 'profile bridge peak x slider range'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorInnerBridgeSteepnessSlider"[^>]*Minimum="0"[^>]*Maximum="1"' 'profile bridge steepness slider range'

# ---- Pass 4 robustness contracts ---------------------------------------------------
Assert-Contains 'dllmain.cpp' 'CachedRtvFor\(const TrackedSwapchain& ts, uint32_t imageIndex, uint32_t arraySlice\)' 'RTVs are cached per swapchain image/slice'
Assert-Contains 'dllmain.cpp' 'g_releaseDrewEdgeGuardThisFrame\.exchange\(false\)' 'late edge-guard draw is fallback-only after release-time edge draw'
Assert-Contains 'dllmain.cpp' 'g_releaseDrewVisorThisFrame\.exchange\(false\)' 'late visor draw is fallback-only after release-time visor draw'
Assert-Contains 'dllmain.cpp' 'DrawCapturedProjectionTextures\(needsLateEdgeGuard, needsLateVisor' 'late fallback keeps edge guard and visor release flags independent'
Assert-Contains 'dllmain.cpp' 'MillisToMaskScale\(profileMaskHorizontal, maskHorizontal\)' 'per-app mask_horizontal uses mask-scale fallback, not render-scale clamp'
Assert-Contains 'dllmain.cpp' 'if \(!cropOuterEdgesOnly\) \{' 'horizontal crop mode can opt out of outer-edge-only scaling'
Assert-Contains 'dllmain.cpp' 'D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT' 'D3D11 visor draws restore all render-target slots'
Assert-Contains 'dllmain.cpp' 'CreateRasterizerState\(&rsd, &g_d3d11Mask\.rs\)' 'D3D11 rasterizer state creation is checked'
Assert-Contains 'dllmain.cpp' 'CreateBlendState\(&bld, &g_d3d11Mask\.bs\)' 'D3D11 blend state creation is checked'
Assert-Contains 'dllmain.cpp' 'CreateDepthStencilState\(&dsd, &g_d3d11Mask\.dss\)' 'D3D11 depth state creation is checked'
Assert-Contains 'dllmain.cpp' 'if \(tex\) tex->AddRef\(\);' 'release-time draw holds texture refs outside swapchain mutex'
Assert-Contains 'dllmain.cpp' 'std::vector<VisorVertex> verts\(kMaxVerts\);' 'large visor vertex buffers are heap-backed'
Assert-Contains 'dllmain.cpp' 'return localAppData / L"XR ViewLab" / ConfigFileName;' 'config path fallback stays absolute'
Assert-Contains 'Installer\Product.wxs' 'Directory Id="ProgramFilesFolder"' 'installer has a 32-bit Program Files tree'
Assert-Contains 'Installer\Product.wxs' 'Component Id="LayerFiles32"[^>]*Win64="no"' '32-bit layer files live in a 32-bit component'
Assert-Contains 'build.ps1' 'throw "build\.ps1 packages the MSI from Release paths; use -Configuration Release\."' 'build rejects mixed non-Release MSI packaging'
Assert-Contains 'build.ps1' 'function Invoke-Native' 'build checks native command exit codes'
Assert-Contains 'build.ps1' 'AssemblyInformationalVersion\\\("\(\\d\+\\\.\\d\+\\\.\\d\+\)\(\?:\\\.\\d\+\)\?' 'build version parsing accepts 3/4 component versions'
Assert-Contains 'dllmain.cpp' 'ViewLab\.verbose\.log' 'verbose hook logging uses the separate verbose log'
Assert-Contains 'dllmain.cpp' 'original_fov=\(L %.5f R %.5f U %.5f D %.5f valid=%d\)' 'edge-smear diagnostics include original FOV'
Assert-Contains 'dllmain.cpp' 'EffectiveHorizontalScaleForView\(i, requestedWidthScale\)' 'edge-smear diagnostics use per-view horizontal scale'
Assert-Contains 'dllmain.cpp' 'EffectiveVerticalScaleForView\(i, requestedHeightScale\)' 'edge-smear diagnostics use per-view vertical scale'
Assert-Contains 'dllmain.cpp' 'continuing recommended-size scale, properties_result=%d fovMutable=%d' 'recommended image-size scaling continues even when fovMutable reporting is unavailable'

Write-Host 'ViewLab contract verification passed.'
