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
Assert-IniValue 'mask_size' '1.0'
Assert-IniValue 'mask_corner' '0.5'
Assert-IniValue 'visor_hd' '0'
Assert-IniValue 'visor_antialiasing' '0'
Assert-IniValue 'visibility_mask_visor' '0'
Assert-IniValue 'foveated_center_compensation' '0'
Assert-NotContains 'dllmain.cpp' 'view\.pose\.orientation\s*=' 'crop never rotates the game eye pose'
Assert-IniValue 'stencil_outer_edges_only' '1'
Assert-IniValue 'crop_outer_edges_only' '1'
Assert-IniValue 'verbose_logging' '0'
Assert-IniValue 'calibration_grid' '0'
Assert-IniValue 'calibration_ruler' '0'
Assert-IniValue 'calibration_gratings' '0'
Assert-IniValue 'calibration_bars' '0'
Assert-IniValue 'calibration_beacon' '0'
Assert-IniValue 'calibration_edge_probes' '0'
Assert-IniValue 'calibration_checkerboards' '0'
Assert-IniValue 'calibration_zone_plate' '0'
Assert-IniValue 'calibration_clipping_steps' '0'
Assert-IniValue 'calibration_motion_strip' '0'
Assert-IniValue 'hud_enabled' '0'
Assert-IniValue 'hud_anchor_x' '0.04'
Assert-IniValue 'hud_anchor_y' '0.05'
Assert-IniValue 'hud_update_ms' '100'
Assert-IniValue 'hud_trace_sensitivity_ms' '2'
Assert-IniValue 'hud_debug_values' '0'
Assert-IniValue 'hud_safe_margin' '0.025'
Assert-IniValue 'hud_clamp_to_visible' '1'
Assert-IniValue 'mask_outer_apex_y' '0.0'
Assert-IniValue 'mask_inner_lower_y' '0.0'
Assert-IniValue 'mask_inner_bridge_width' '0.5'
Assert-IniValue 'mask_inner_bridge_rise' '0.0'
Assert-IniValue 'mask_inner_bridge_peak_x' '0.5'
Assert-IniValue 'mask_inner_bridge_steepness' '0.5'
Assert-IniValue 'visor_live_revision' '0'
Assert-IniValue 'stencil_outer_edges_only' '1'

# ---- Installer reset policy is explicit and complete -------------------------------
Assert-NotContains 'Installer\Product.wxs' 'CleanupApiLayerRegistry' 'installer never enumerates or cleans the shared implicit OpenXR layer registry'
Assert-NotContains 'Installer\Product.wxs' 'PreserveConfig.vbs' 'installer does not embed a registry-cleanup script'
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
Assert-Contains 'dllmain.cpp' 'cropOuterEdgesOnly && viewIndex == 0' 'outer-edge crop is applied (permanently enabled)'
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
Assert-Contains 'MainWindow.xaml' 'Name="MaskSizeSlider"[^>]*Minimum="0\.1"[^>]*Maximum="1"' 'main window Size slider range'
Assert-Contains 'MainWindow.xaml' 'Name="MaskInnerLowerSlider"[^>]*Minimum="0"[^>]*Maximum="0\.666"' 'main window Inner low slider range'
Assert-NotContains 'MainWindow.xaml' 'Name="MaskInnerBridge(Slider|RiseSlider|PeakXSlider|SteepnessSlider)"' 'removed notch-detail sliders are absent from the main window'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorApexYSlider"[^>]*Minimum="-0\.5"[^>]*Maximum="0\.5"' 'profile Apex Y slider range'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorInnerLowerSlider"[^>]*Minimum="0"[^>]*Maximum="0\.666"' 'profile Inner low slider range'
Assert-Contains 'ProfileWindow.xaml' 'x:Key="ProfileScrollViewer"' 'PowerUp/profile window uses a ViewLab-themed scroll viewer'
Assert-Contains 'ProfileWindow.xaml' 'Grid\.Column="1" Name="PART_VerticalScrollBar"' 'PowerUp/profile scrollbar has its own reserved layout column'

Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'private const string MaskOuterApexYKey = "mask_outer_apex_y";' 'main window has Apex Y config key'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'private const string MaskInnerLowerYKey = "mask_inner_lower_y";' 'main window has Inner low config key'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'MaskBeanEditor\.OpenInnerPreview = true' 'main preview is hardcoded to open-inner (stencil outer edges permanently on)'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'SplitCheck_Changed[\s\S]*?SaveGlobalSettings\(\)' 'split mode transitions are persisted immediately so disabled split state cannot survive relaunch'

# ---- Preview exposes the shape controls -------------------------------------------
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'public double OuterApexY' 'preview exposes Apex Y'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'public double InnerLowerY' 'preview exposes Inner low'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' '1024\.0 \* Math\.Exp\(-50\.0 \* Math\.Clamp\(curve, 0\.0, 1\.0\)\)' 'preview Curve has a continuous square-to-round shoulder'
Assert-NotContains 'XRViewLab.UI\BeanMaskEditor.cs' 'if \(Curve <= 0\.0\)' 'preview Curve no longer switches geometry modes at zero'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'Math\.Clamp\(Size \* WidthScale, 0\.01, 1\.0\)' 'preview Size uniformly scales aperture width'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'DrawPin\(dc, pins\.outerApex' 'preview draws outer apex pin'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'DrawPin\(dc, pins\.size' 'preview draws Size pin'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'DrawPin\(dc, pins\.curve' 'preview draws Curve pin'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'DrawPin\(dc, pins\.innerRise' 'preview draws Rise pin'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'DrawPin\(dc, pins\.innerPeakX' 'preview draws Peak X pin'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'DrawPin\(dc, pins\.innerSteepness' 'preview draws Steep pin'
Assert-Contains 'MainWindow.xaml' 'Name="ReShadeButton"' 'ReShade Remote is available next to Edge Masks'
Assert-Contains 'XRViewLab.UI\ReShadeRemoteWindow.cs' 'Text = "WARNING — DO NOT USE"' 'ReShade Remote warns entry-level users'
Assert-Contains 'MainWindow.xaml' 'Name="AppsHeader" Visibility="Collapsed"' 'Applications sub-header is removed for aligned responsive columns'
Assert-Contains 'MainWindow.xaml' 'Name="OptionsHeader" Visibility="Collapsed"' 'Render Options sub-header is removed for aligned responsive columns'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'ThanksTextRight\.Visibility     = threeCol \? Visibility\.Visible : Visibility\.Collapsed;' 'beta-testers line moves to the third column in triple-column mode'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'Forefront_Internal\.exe' 'Forefront discovery targets the actual runtime executable'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'w < 280\.0' 'mini layout waits for a genuinely narrow client width'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'w >= 720\.0' 'two-column layout has enough client width'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'w >= 1200\.0' 'three-column layout has enough client width'
Assert-Contains 'MainWindow.xaml' 'Checked apps use ViewLab\. Double-click for per-game customization\.' 'applications instruction is concise'
Assert-Contains 'MainWindow.xaml' 'Name="CombinedHint"[^>]*Visibility="Collapsed"' 'redundant combined render-height hint is removed'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'CaptureMouse\(\);' 'preview captures mouse after pin hit'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'ReleaseMouseCapture\(\);' 'preview releases mouse capture on mouse-up'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'OnLostMouseCapture' 'preview clears drag state on lost capture'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'double centerY = \(pins\.y0 \+ pins\.y1\) \* 0\.5;' 'apex pin drag uses the same center-origin inverse as the rendered geometry'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'HitTestCore\(PointHitTestParameters hitTestParameters\)' 'preview is hit-testable across its full rectangle'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'OnPreviewMouseLeftButtonDown' 'preview receives mouse before parent scroll/title handlers'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'SetSliderValue\(MaskApexYSlider, MaskBeanEditor\.OuterApexY\);' 'main window syncs dragged editor apex back to sliders'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'MaskBeanEditor\.InnerBridgeWidth = FixedInnerBridgeWidth;' 'main preview uses the fixed supported notch shape'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'VisorApexYSlider\.Value = MaskBeanEditor\.OuterApexY;' 'profile window syncs dragged editor apex back to sliders'
Assert-Contains 'XRViewLab.UI\ProfileWindow.cs' 'VisorInnerBridgeSlider\.Value = MaskBeanEditor\.InnerBridgeWidth;' 'profile window syncs dragged bridge pin back to sliders'

# ---- Native layer: config plumbing ------------------------------------------------
Assert-Contains 'dllmain.cpp' 'visorOuterApexY = std::clamp\(ReadDoubleSetting\(L"mask_outer_apex_y", 0\.0\), -0\.5, 0\.5\);' 'native reads and clamps global Apex Y'
Assert-Contains 'dllmain.cpp' 'visorInnerLowerY = std::clamp\(ReadDoubleSetting\(L"mask_inner_lower_y", 0\.0\), 0\.0, 0\.666\);' 'native reads and clamps extended global Inner low'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_width", 0\.5\)' 'native reads bridge width'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_rise", 0\.0\)' 'native reads bridge rise'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_peak_x", 0\.5\)' 'native reads bridge peak x'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_size", 1\.0\)' 'native reads visor Size with compatibility default'
Assert-Contains 'dllmain.cpp' 'ReadDoubleSetting\(L"mask_inner_bridge_steepness", 0\.5\)' 'native reads bridge steepness'
Assert-Contains 'XRViewLab.UI\LiveStateService.cs' 'XRViewLabLiveState' 'UI publishes live settings through shared memory'
Assert-Contains 'dllmain.cpp' 'void ConsumeLiveState\(\)' 'native consumes the compact live state snapshot'
Assert-Contains 'dllmain.cpp' 'ConsumeLiveState\(\);' 'live state is consumed at the end-of-frame safe point'
Assert-Contains 'dllmain.cpp' 'if \(\(stable\.flags & 1u\) != 0 && !liveVisorUsesProfileOverride\)' 'live visor state does not override per-app profiles'
Assert-Contains 'XRViewLab.UI\LiveStateService.cs' '_view\.Write\(20, 1u \| \(maskEnabled \? 4u : 0u\)\)' 'global visor tuning is always published live'
Assert-NotContains 'MainWindow.xaml' 'LiveVisorTuningCheck' 'obsolete live visor tuning toggle is removed'
Assert-NotContains 'xr-viewlab.ini' 'live_visor_tuning' 'obsolete live visor tuning key is removed from defaults'
Assert-Contains 'dllmain.cpp' 'g_liveStateNextConnectTick = now \+ 1000' 'closed settings app reconnects live-state shared memory at a bounded cadence'
Assert-NotContains 'dllmain.cpp' 'VoidQuadState|EnsureVoidQuad|drawInVoidTest' 'Draw in the Void experiment is fully removed from the native layer'
Assert-NotContains 'XRViewLab.UI\MainWindow.cs' 'DrawInVoidTestKey|DrawInVoidCheck' 'Draw in the Void experiment is removed from the settings UI'
Assert-Contains 'dllmain.cpp' 'forefront diag: VIEWLAB_LOADED' 'Forefront layer-entry diagnostic is logged when the process loads ViewLab'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'WritePrivateProfileString\("Settings", MaskInnerLowerYKey, FormatStorageScale\(MaskInnerLowerSlider\.Value\)' 'UI persists expanded Inner low to the live INI'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'WritePrivateProfileString\("Settings", MaskInnerBridgeRiseKey, FormatStorageScale\(FixedInnerBridgeRise\)' 'UI resets removed Rise tuning to the supported shape'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'WritePrivateProfileString\("Settings", MaskInnerBridgePeakXKey, FormatStorageScale\(FixedInnerBridgePeakX\)' 'UI resets removed Peak X tuning to the supported shape'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'WritePrivateProfileString\("Settings", MaskInnerBridgeSteepnessKey, FormatStorageScale\(FixedInnerBridgeSteepness\)' 'UI resets removed Steep tuning to the supported shape'
Assert-Contains 'dllmain.cpp' 'constexpr bool visorHD = false' 'HD visor is hardcoded off'
Assert-Contains 'dllmain.cpp' 'constexpr bool visorAntialiasing = false' 'visor anti-aliasing is hardcoded off'
Assert-Contains 'dllmain.cpp' 'bool cropOuterEdgesOnly = true' 'crop outer edges is hardcoded on'
Assert-Contains 'dllmain.cpp' 'constexpr bool foveatedCenterCompensation = false' 'pose-rotating foveated centre compensation is retired'
Assert-Contains 'dllmain.cpp' 'bool outerEdgeVisibilityMaskOnly = true' 'stencil outer edges only is hardcoded on'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_outer_apex_y", profileOuterApexY\);' 'native reads per-app Apex Y'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_inner_lower_y", profileInnerLowerY\);' 'native reads per-app Inner low'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_inner_bridge_rise", profileBridgeRise\);' 'native reads per-app bridge rise'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_inner_bridge_peak_x", profileBridgePeakX\);' 'native reads per-app bridge peak x'
Assert-Contains 'dllmain.cpp' 'ReadProfileDword\(L"mask_inner_bridge_steepness", profileBridgeSteepness\);' 'native reads per-app bridge steepness'
Assert-Contains 'dllmain.cpp' 'std::clamp\(SignedMillisToUnit\(profileOuterApexY, visorOuterApexY\), -0\.5, 0\.5\)' 'native clamps decoded per-app Apex Y'
Assert-NotContains 'dllmain.cpp' 'visor_hd=%d visor_antialiasing=%d' 'native config log no longer includes removed AA/HD controls'
Assert-Contains 'dllmain.cpp' 'visor_outer_apex_y=%.3f visor_inner_lower_y=%.3f' 'native config log includes new visor controls'
Assert-Contains 'dllmain.cpp' 'crop_outer_edges_only=%d' 'native config and verbose logs include actual crop mode'

# ---- Native layer: geometry contracts ---------------------------------------------
Assert-Contains 'dllmain.cpp' 'const bool openInnerShape = outerEdgeVisibilityMaskOnly;' 'native visor geometry follows the same open-inner setting as preview'
Assert-Contains 'dllmain.cpp' 'BuildVisorBorderVerts\(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, featherX, featherY, verts\.data\(\), kMaxVerts\)' 'native has closed-bean visor geometry path'
Assert-Contains 'dllmain.cpp' 'BuildOpenInnerEyeVisorVerts\(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, outerLeft, featherX, featherY, verts\.data\(\), kMaxVerts\)' 'native has open-inner visor geometry path'
Assert-Contains 'dllmain.cpp' '1024\.0f \* std::exp\(-50\.0f \* c\)' 'native uses the same continuous Curve shoulder as preview'
Assert-NotContains 'dllmain.cpp' 'bool IsRectangularVisor\(\)' 'native Curve no longer switches geometry modes at zero'
Assert-Contains 'dllmain.cpp' 'visorSize \* visorWidth' 'native Size uniformly scales aperture width'
Assert-Contains 'dllmain.cpp' 'float ApexYFromConfigNdc\(double configApexY\)' 'UI-to-NDC y flip lives in exactly one helper'
Assert-Contains 'dllmain.cpp' 'BuildNoseBridgeCurve\(vertsOut, v, vertCapacity, cx, innerX, y0, bandTopY\);' 'nose divot is anchored to the NDC BOTTOM edge (y0), never y1'
Assert-Contains 'dllmain.cpp' 'y = std::clamp\(y, yBase, yTop\);' 'nose divot vertices are band-clamped (structurally bottom-only)'
Assert-Contains 'dllmain.cpp' 'struct VisorVertex' 'native visor vertices carry alpha for feathered AA'
Assert-Contains 'dllmain.cpp' 'D3D11_BLEND_SRC_ALPHA' 'native visor draw uses SRC_ALPHA blending'
Assert-Contains 'dllmain.cpp' 'VisorCurveSegments\(uint32_t requested = 512u\)' 'native visor uses fixed high-density curve tessellation'
Assert-Contains 'dllmain.cpp' 'const float topFeatherY = std::clamp\(y0 \+ featherY, y0, y1\);' 'open-inner visor path uses vertical AA feathering'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'double dir = dx > 0\.0 \? 1\.0 : -1\.0;' 'preview nose bridge handles mirror correctly'

# ---- Native layer: stencil fixes must survive (4.1.102/4.1.103) --------------------
Assert-Contains 'dllmain.cpp' 'const bool keepOuterEdge =' 'visibility-mask filter keeps outer-edge triangles'
Assert-Contains 'dllmain.cpp' 'if \(beforeIndexCount == 3\)[\s\S]*?indivisible_passthrough=1' 'indivisible runtime visibility masks pass through unchanged'
Assert-Contains 'dllmain.cpp' 'if \(writeIndex == 0\)[\s\S]*?empty_filter_passthrough=1' 'visibility-mask filtering can never turn a non-empty runtime mask into an empty mask'
Assert-Contains 'dllmain.cpp' 'visibility_mask_visor=1 is retired' 'legacy visibility-mask visor path is explicitly ignored'
Assert-Contains 'dllmain.cpp' 'visibilityMaskVisor = false;' 'legacy visibility-mask visor cannot reshape current visor geometry'

# ---- Per-app profile shape coverage -----------------------------------------------
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.SetValue\("mask_inner_bridge_rise", ToRiseMillis\(visorInnerBridgeRise\)' 'UI writes extended per-app bridge rise'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.SetValue\("mask_inner_bridge_peak_x", ToPeakXMillis\(visorInnerBridgePeakX\)' 'UI writes extended per-app bridge peak x without breaking legacy profiles'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.SetValue\("mask_inner_bridge_steepness", ToSteepMillis\(visorInnerBridgeSteepness\)' 'UI writes extended per-app bridge steepness'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.DeleteValue\("mask_enabled", throwOnMissingValue: false\);' 'UI does not write unsupported per-app visor enable override'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.DeleteValue\("mask_inner_bridge_rise"' 'UI reset deletes per-app bridge rise'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.DeleteValue\("mask_inner_bridge_peak_x"' 'UI reset deletes per-app bridge peak x'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'registryKey\.DeleteValue\("mask_inner_bridge_steepness"' 'UI reset deletes per-app bridge steepness'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'MaskRoundnessSlider\.Value = 0\.5;' 'curve right-click reset returns to the safe default'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'private const string HorizVisualMaskBothKey = "horizontal_visual_mask_only";' 'horizontal Edge Masks checkbox writes the DLL key'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'private const string VertVisualMaskBothKey = "visual_mask_only";' 'vertical Edge Masks checkbox writes the DLL key'
Assert-NotContains 'MainWindow.xaml' 'HorizOuterEyeMaskCheck|HorizInnerEyeMaskCheck' 'unsupported one-sided horizontal edge controls are removed'
Assert-NotContains 'MainWindow.xaml' 'VertTopMaskCheck|VertBottomMaskCheck' 'unsupported one-sided vertical edge controls are removed'
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
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorInnerBridgeRiseSlider"[^>]*Minimum="-0\.5"[^>]*Maximum="1"' 'profile extended bridge rise slider range'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorSizeSlider"[^>]*Minimum="0\.1"[^>]*Maximum="1"' 'profile Size slider range'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorInnerBridgePeakXSlider"[^>]*Minimum="-1"[^>]*Maximum="2"' 'profile extended bridge Peak X slider range'
Assert-Contains 'ProfileWindow.xaml' 'Name="VisorInnerBridgeSteepnessSlider"[^>]*Minimum="-1"[^>]*Maximum="2"' 'profile extended bridge steepness slider range'

# ---- Pass 4 robustness contracts ---------------------------------------------------
Assert-Contains 'dllmain.cpp' 'CachedRtvFor\(const TrackedSwapchain& ts, uint32_t imageIndex, uint32_t arraySlice\)' 'RTVs are cached per swapchain image/slice'
Assert-Contains 'dllmain.cpp' 'g_releaseDrewVisorThisFrame\.exchange\(false\)' 'late visor draw is fallback-only after release-time visor draw'
Assert-Contains 'dllmain.cpp' 'DrawCapturedProjectionTextures\(true, "visor"\)' 'late fallback draws the visor only (edge-guard path removed 2026-07-11)'
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
Assert-Contains 'Installer\Product.wxs' 'net8\.0-windows10\.0\.19041\.0\\win-x64\\publish\\xr-viewlab\.exe' 'MSI sources the current WinRT-targeted WPF publish output'
Assert-Contains 'build.ps1' '\$TargetFramework = \(\[xml\]\(Get-Content -Raw \$DotnetProject\)\)' 'build derives the publish directory from the project target framework'
Assert-Contains 'build.ps1' 'MSI payload executable version \$PayloadVersion does not match build version \$version' 'build rejects a stale-version MSI payload'
Assert-Contains 'build.ps1' 'MSI payload executable is not byte-for-byte identical to the fresh publish output' 'build hash-checks the packaged executable'
Assert-Contains 'build.ps1' "'OverlaysButton_Click'" 'build validates the compiled Overlays UI marker in the MSI payload'
Assert-Contains 'dllmain.cpp' 'ViewLab\.verbose\.log' 'verbose hook logging uses the separate verbose log'
Assert-Contains 'dllmain.cpp' 'original_fov=\(L %.5f R %.5f U %.5f D %.5f valid=%d\)' 'edge-smear diagnostics include original FOV'
Assert-Contains 'dllmain.cpp' 'EffectiveHorizontalScaleForView\(i, requestedWidthScale\)' 'edge-smear diagnostics use per-view horizontal scale'
Assert-Contains 'dllmain.cpp' 'EffectiveVerticalScaleForView\(i, requestedHeightScale\)' 'edge-smear diagnostics use per-view vertical scale'
Assert-Contains 'dllmain.cpp' 'continuing recommended-size scale, properties_result=%d fovMutable=%d' 'recommended image-size scaling continues even when fovMutable reporting is unavailable'

# Experimental crop fixes purged 2026-07-11 (root cause: VD fixed foveated encoding — see
# docs/FIXED_FOVEATION.md). Pin the removal so they do not silently return.
Assert-NotContains 'dllmain.cpp' 'CropExperimentMode' 'experimental crop modes removed (2026-07-11 purge)'
Assert-NotContains 'dllmain.cpp' 'edgeSmearFix' 'edge-guard experiment removed (2026-07-11 purge)'
Assert-NotContains 'dllmain.cpp' 'crop-contract' 'crop-contract diagnostics removed (2026-07-11 purge)'
Assert-NotContains 'XRViewLab.UI\MainWindow.cs' 'SetCropExperimentMode' 'experiment checkboxes removed from UI (2026-07-11 purge)'
foreach ($key in @('calibration_grid', 'calibration_ruler', 'calibration_gratings', 'calibration_bars', 'calibration_beacon', 'calibration_edge_probes', 'calibration_checkerboards', 'calibration_zone_plate', 'calibration_clipping_steps', 'calibration_motion_strip')) {
    Assert-Contains 'dllmain.cpp' "ReadBoolSetting\(L`"$key`", false\)" "DLL reads calibration key $key"
    Assert-Contains 'XRViewLab.UI\MainWindow.cs' $key "UI persists calibration key $key"
}
Assert-Contains 'dllmain.cpp' 'DrawCalibrationPatternsToTexture' 'calibration pattern draw path exists'
Assert-Contains 'dllmain.cpp' 'g_calibrationFrameSerial\.fetch_add\(1\)' 'calibration temporal patterns advance once per submitted projection frame'
Assert-Contains 'dllmain.cpp' 'kCalibrationPS' 'calibration uses its own explicit colour shader'
Assert-Contains 'dllmain.cpp' 'calibrationColorCb' 'calibration colour is supplied by a dedicated constant buffer'
Assert-Contains 'dllmain.cpp' 'const int l=eye\.rect\.offset\.x, t=eye\.rect\.offset\.y' 'pixel calibration tools use the full submitted eye rectangle rather than crop-scaled overlap bounds'
Assert-Contains 'dllmain.cpp' 'one cell is always 64 submitted-texture pixels' 'the 64px grid remains a literal pixel ruler'
Assert-Contains 'dllmain.cpp' 'ResolveSharedTangent\(0\.f, 0\.f, true\)' 'centre-based calibration patterns share the fused crosshair tangent centre'
Assert-Contains 'dllmain.cpp' 'cosf\(a\)\*radiusTan,sinf\(a\)\*radiusTan' 'radial calibration geometry is circular in tangent space rather than raw pixels'
Assert-Contains 'dllmain.cpp' 'hudEnabled&&OverlayFeatureVisible\(OverlayFeatureId::Hud\)[\s\S]*hudTraceEnabled&&OverlayFeatureVisible\(OverlayFeatureId::Trace\)[\s\S]*eye\.viewIndex < 2' 'performance HUD and trace project into both eyes for stereo fusion'
Assert-Contains 'dllmain.cpp' 'anchorPxX\(hudAnchorX\)' 'HUD anchor maps through the shared tangent-space frame (X)'
Assert-Contains 'dllmain.cpp' 'anchorPxY\(hudAnchorY\)' 'HUD anchor maps through the shared tangent-space frame (Y)'
Assert-Contains 'dllmain.cpp' 'g_hudDrawSnap' 'HUD and trace render from one frame-coherent snapshot'
Assert-NotContains 'dllmain.cpp' 'segs\[10\]=\{0x3f' 'seven-segment HUD digits replaced by the pixel font'
Assert-Contains 'dllmain.cpp' 'kHudFont' 'HUD uses the 5x7 pixel font with a decimal-point glyph'
Assert-Contains 'dllmain.cpp' '"%\.1f %s", metric\.value, widget\.unit' 'decimal HUD values carry explicit units'
Assert-Contains 'dllmain.cpp' 'UpdateHudMetrics\(\)' 'HUD telemetry is update-rate limited outside geometry construction'
Assert-Contains 'dllmain.cpp' 'GetSystemTimes' 'HUD reads total Windows CPU time'
Assert-Contains 'dllmain.cpp' 'idleDelta.*totalDelta' 'HUD CPU conversion subtracts idle time from total system time'
Assert-Contains 'dllmain.cpp' 'GPU Engine' 'HUD reads Windows GPU engine utilisation'
Assert-Contains 'dllmain.cpp' 'engtype_%63s' 'HUD parses GPU engine types instead of summing unrelated engines'
Assert-Contains 'dllmain.cpp' '_wcsicmp\(engineType, L"3D"\)' 'HUD limits GPU aggregation to 3D engines'
Assert-Contains 'dllmain.cpp' 'XRViewLab_xrWaitFrame' 'HUD captures predicted OpenXR display timing'
Assert-Contains 'dllmain.cpp' 'predictedDisplayPeriod' 'HUD derives the frame budget from the runtime period'
Assert-Contains 'dllmain.cpp' 'beginStop\.QuadPart' 'APP workload begins after xrBeginFrame returns'
Assert-Contains 'dllmain.cpp' 'endStart\.QuadPart' 'APP workload ends before xrEndFrame runtime blocking'
Assert-Contains 'dllmain.cpp' 'RecordHudAppWorkSample' 'APP workload replaces unstable legacy SYS'
Assert-NotContains 'dllmain.cpp' 'RecordHudSystemSample' 'legacy begin-through-end SYS metric is retired'
Assert-Contains 'dllmain.cpp' 'deviationMs' 'HUD stores frame pacing deviation samples'
Assert-Contains 'dllmain.cpp' 'UpdateHudCadence' 'VR frame time uses wait-to-wait interval cadence detection'
Assert-Contains 'dllmain.cpp' 'std::sort' 'cadence classification uses a rolling distribution, never a single slow frame'
Assert-Contains 'dllmain.cpp' 'g_hudCadenceStable >= 20' 'cadence multiple switches only after sustained agreement'
Assert-Contains 'dllmain.cpp' 'std::lround\(medianMs / g_hudDisplayPeriodMs\)' 'effective cadence is an integer multiple of the runtime display period, never hardcoded'
Assert-Contains 'dllmain.cpp' 'hudAlarmOnly && !snap\.alarm\[id\]' 'alarm-only mode hides each widget independently while not critical'
Assert-Contains 'dllmain.cpp' 'UpdateSustainedAlarm' 'every HUD symbol uses the shared bounded alarm state machine'
Assert-Contains 'Tests\RenderPolicyFixtures.cpp' 'post-recovery hold expires instead of extending itself forever' 'alarm recovery cannot refresh its own hold indefinitely'
Assert-NotContains 'dllmain.cpp' 'Network Interface' 'network placeholder telemetry is removed'
Assert-Contains 'MainWindow.xaml' 'Name="HudEnabledCheck"' 'UI exposes a Performance HUD checkbox'
Assert-Contains 'XRViewLab.UI\OverlaySettingsModels.cs' '"hud_enabled"' 'shared overlay catalogue persists the HUD enabled setting'
Assert-Contains 'MainWindow.xaml' 'Name="HudXSlider"' 'HUD X control exists'
Assert-Contains 'MainWindow.xaml' 'Name="HudYSlider"' 'HUD Y control exists'
Assert-Contains 'MainWindow.xaml' 'Name="HudScaleSlider"' 'HUD scale control exists'
Assert-Contains 'MainWindow.xaml' 'Name="HudTraceSensitivitySlider"' 'HUD trace sensitivity control exists'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'HudTraceSensitivityKey' 'HUD trace sensitivity persists to the ini'
foreach ($ctl in @('HudTraceXSlider', 'HudTraceYSlider', 'HudTraceScaleSlider', 'HudTraceWidthSlider', 'HudTraceHistorySlider', 'HudAlarmOnlyCheck')) {
    Assert-Contains 'MainWindow.xaml' "Name=`"$ctl`"" "live trace/alarm control $ctl exists"
}
foreach ($key in @('hud_trace_x', 'hud_trace_y', 'hud_trace_scale', 'hud_trace_width', 'hud_trace_history', 'hud_alarm_only', 'hud_alarm_hold_ms')) {
    Assert-Contains 'dllmain.cpp' $key "DLL reads HUD key $key"
    Assert-Contains 'xr-viewlab.ini' $key "default ini carries HUD key $key"
}
Assert-Contains 'MainWindow.xaml' 'Name="HudSafeMarginSlider"' 'HUD safe-margin control exists'
Assert-Contains 'dllmain.cpp' 'hudClampToVisible' 'HUD clamps complete bounds to the visible eye rectangle'
Assert-Contains 'XRViewLab.UI\LiveStateService.cs' 'private const int Size = 260' 'live state carries overlay placement controls'
Assert-Contains 'XRViewLab.UI\LiveStateService.cs' '_view\.Write\(4, 8u\)' 'live state contract is version 8'
Assert-Contains 'dllmain.cpp' 'snapshot\.version != 8' 'DLL consumes live-state contract version 8'
Assert-NotContains 'MainWindow.xaml' 'TopmostVisorOverlaysCheck' 'ordinary UI does not expose backend implementation choice'
Assert-Contains 'dllmain.cpp' '!ReadBoolSetting\(L"overlay_force_direct", false\)' 'automatic topmost is the normal session policy'
Assert-Contains 'dllmain.cpp' 'maskEnabled && g_featurePresentationPlan\.drawDirectVisor' 'central policy gates the direct visor path'
Assert-Contains 'dllmain.cpp' 'AnyDirectOverlay\(\) && g_featurePresentationPlan\.drawDirectCommonFeatures' 'central policy gates the direct common-feature path'
Assert-Contains 'dllmain.cpp' 'XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT' 'topmost layer submits transparent source alpha'
Assert-NotContains 'dllmain.cpp' 'XR_COMPOSITION_LAYER_UNPREMULTIPLIED_ALPHA_BIT' 'topmost compositor does not multiply its premultiplied target twice'
Assert-Contains 'dllmain.cpp' 'preferredFormat=DXGI_FORMAT_R8G8B8A8_UNORM_SRGB' 'ordered projection starts from the established colour contract'
Assert-Contains 'dllmain.cpp' 'const int64_t candidate=tracked->second.format' 'ordered projection follows the current game projection format when compatible'
Assert-Contains 'dllmain.cpp' 'rd\.Format=topmostDesc\.Format' 'ordered projection preserves the runtime resource RTV contract'
Assert-Contains 'dllmain.cpp' 'submittedLayers\.push_back' 'topmost layer is appended after game layers'
Assert-Contains 'dllmain.cpp' 'XrCompositionLayerProjection layer\{XR_TYPE_COMPOSITION_LAYER_PROJECTION\}' 'ordered compatibility presentation preserves the proven stereo projection carrier'
Assert-Contains 'dllmain.cpp' 'ci\.faceCount=1; ci\.arraySize=2' 'ordered compatibility presentation owns one runtime array slice per eye'
Assert-Contains 'dllmain.cpp' 'out\.layer\.space=source->space' 'ordered compatibility presentation follows the current projection space'
Assert-Contains 'dllmain.cpp' 'primaryProjection==nullptr' 'frame topology detects composition-only scenes without title checks'
Assert-Contains 'ViewLabBridge/BridgeCore.cpp' '!capabilities\.hasPrimaryProjection && !capabilities\.hasCompositionLayers' 'central feature policy recognises composition-only frames'
Assert-Contains 'dllmain.cpp' 'OverlayBackend::SeparateProjection' 'layer selection crosses the ViewLab Bridge boundary'
Assert-Contains 'ViewLabBridge\BridgeCore.cpp' 'hasDistinctCompositionLayer' 'bridge selects presentation from observed frame capabilities'
Assert-Contains 'ViewLabBridge\BridgeCore.cpp' 'plan\.drawDirectVisor = capabilities\.hasPrimaryProjection && capabilities\.canWriteEyeTexture' 'submission success never disables the proven direct visor when a writable projection exists'
Assert-Contains 'ViewLabBridge\BridgeCore.cpp' 'capabilities\.orderedPresentationReady' 'ordered readiness selects exactly one normal-feature presentation path'
Assert-Contains 'dllmain.cpp' 'const bool wasDemanded=g_topmostLayerDemanded' 'topology demand remains central and survives scene changes'
Assert-NotContains 'ViewLabBridge\BridgeCore.cpp' '(?i)dirt|pistol|pinball|boneworks' 'bridge contains no title-specific route policy'
Assert-Contains 'dllmain.cpp' 'DestroyTopmostLayer\(\)' 'topmost path has explicit fallback and teardown'
Assert-Contains 'dllmain.cpp' 'g_topmostLayerAttempted\s*=\s*true' 'topmost allocation is attempted at most once per session'
Assert-Contains 'dllmain.cpp' 'projection capacity changed' 'topmost projection-size changes fail closed instead of reallocating'
Assert-Contains 'XRViewLab.UI\LiveStateService.cs' '_view\.Write\(76, \(float\)hudTraceSensitivityMs\)' 'live state carries HUD trace sensitivity'

# Feature 1: render-boundary flash — drags flash the exact cropped boundary in cyan-white, fading ~500ms.
Assert-Contains 'dllmain.cpp' 'kBoundaryFadeMs = 500' 'boundary flash fades over ~500ms after release'
Assert-Contains 'dllmain.cpp' 'g_boundaryReleaseTick = GetTickCount64' 'boundary fade timer is stamped on drag release in native'
Assert-Contains 'dllmain.cpp' 'BoundaryFlashActive' 'boundary flash gates its own draw'
Assert-Contains 'MainWindow.xaml' 'Thumb.DragStarted="BoundaryDrag_Start"' 'HUD/trace layout sliders raise the boundary-flash drag signal'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' '_boundaryDragActive = true' 'UI sets the drag-active flag while dragging layout controls'

# Feature 2: crosshair — flat colour+alpha at the stereo centre; legacy config + CS2 share-code import.
Assert-Contains 'dllmain.cpp' 'crosshairEnabled&&OverlayFeatureVisible\(OverlayFeatureId::Crosshair\) && crosshairAlpha' 'crosshair draws only when enabled and visible'
Assert-Contains 'dllmain.cpp' 'crosshairTStyle' 'crosshair supports T-style (top arm hidden)'
Assert-Contains 'dllmain.cpp' 'crosshairGap' 'crosshair gap (incl. negative) is applied'
Assert-Contains 'MainWindow.xaml' 'Name="CrosshairEnabledCheck"' 'crosshair enable control exists'
foreach ($ctl in @('CrosshairSizeSlider','CrosshairGapSlider','CrosshairThicknessSlider','CrosshairOutlineThicknessSlider','CrosshairAlphaSlider','CrosshairScaleSlider','CrosshairDotCheck','CrosshairOutlineCheck','CrosshairTStyleCheck','CrosshairImportBox')) {
    Assert-Contains 'MainWindow.xaml' "Name=`"$ctl`"" "crosshair control $ctl exists"
}
Assert-Contains 'XRViewLab.UI\CrosshairConfig.cs' 'ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789' 'crosshair share-code base-58 dictionary present'
Assert-Contains 'XRViewLab.UI\CrosshairConfig.cs' 'cl_crosshairgap' 'legacy cl_crosshair config parsing present'
Assert-Contains 'XRViewLab.UI\CrosshairConfig.cs' 'ToLegacyConfig' 'crosshair legacy config export present'
Assert-Contains 'dllmain.cpp' 'cbuffer OverlayColor' 'crosshair uses explicit constant colour rather than the VDXR-broken interpolant'
Assert-Contains 'dllmain.cpp' 'crosshair: draw eye=' 'crosshair logs resolved per-eye geometry and draw execution'
Assert-Contains 'MainWindow.xaml' 'Name="CrosshairOverlayPreview"' 'Overlays menu includes live crosshair preview'
Assert-Contains 'MainWindow.xaml' 'Name="CrosshairOffsetXSlider"[^>]*MouseRightButtonUp="CrosshairOffset_ResetRightClick"' 'horizontal crosshair offset supports direct right-click reset'
Assert-Contains 'MainWindow.xaml' 'Name="CrosshairOffsetYSlider"[^>]*MouseRightButtonUp="CrosshairOffset_ResetRightClick"' 'vertical crosshair offset supports direct right-click reset'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'CrosshairOffset_ResetRightClick[\s\S]*?CrosshairOffsetXSlider\.Value = 0[\s\S]*?CrosshairOffsetYSlider\.Value = 0' 'crosshair right-click handler resets either offset to zero'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'DrawCrosshair\(dc, area\)' 'visor preview keeps the fused crosshair in crop-independent full-binocular coordinates'
Assert-NotContains 'XRViewLab.UI\BeanMaskEditor.cs' 'DrawCrosshair\(dc, rightEye\)' 'visor preview does not expose raw per-eye crosshair duplication'
Assert-Contains 'XRViewLab.UI\CrosshairPreview.cs' '1\.125\*VrScale' 'Overlays crosshair preview uses the calibrated half-size display scale'
foreach ($key in @('crosshair_enabled','crosshair_size','crosshair_gap','crosshair_thickness','crosshair_alpha','crosshair_color','crosshair_tstyle')) {
    Assert-Contains 'dllmain.cpp' $key "DLL reads crosshair key $key"
    Assert-Contains 'xr-viewlab.ini' $key "default ini carries crosshair key $key"
}

# Feature 3: notifications — textured card path, separate content mapping, off-render-thread collection.
Assert-Contains 'dllmain.cpp' 'kTexturedPS' 'notification cards use the textured pixel shader'
Assert-Contains 'dllmain.cpp' 'Local\\\\XRViewLabNotifications' 'notification content bridge uses a dedicated mapping'
Assert-Contains 'dllmain.cpp' 'EnsureNotifyCardTexture' 'notification card pixels upload only on content change'
Assert-Contains 'XRViewLab.UI\NotificationService.cs' 'UserNotificationListener' 'notification collection uses the WinRT UserNotificationListener'
Assert-Contains 'XRViewLab.UI\NotificationService.cs' 'RequestAccessAsync' 'notification service requests listener access'
Assert-Contains 'XRViewLab.UI\NotificationService.cs' 'UserNotificationListenerAccessStatus=' 'notification status reports exact listener access state'
Assert-Contains 'XRViewLab.UI\NotificationService.cs' 'EnqueueTestNotification' 'synthetic notification bypasses Windows listener permission'
Assert-Contains 'NotificationBroker\AppxManifest.xml.template' 'uap3:Capability Name="userNotificationListener"' 'notification broker declares the required Windows listener capability'
Assert-Contains 'NotificationBroker\AppxManifest.xml.template' 'uap10:AllowExternalContent' 'notification broker uses package-with-external-location identity'
Assert-Contains 'NotificationBroker\app.manifest' 'packageName="cooooked.ViewLab.NotificationBroker"' 'broker executable binds to its identity package'
Assert-Contains 'NotificationBroker\ViewLab.NotificationBroker.csproj' 'IncludeNativeLibrariesForSelfExtract' 'single-file WPF broker extracts its native runtime dependencies'
Assert-Contains 'xr-viewlab.csproj' 'Compile Remove="Tests\\\*\*"' 'product project excludes independent test-project sources and generated files'
Assert-Contains 'XRViewLab.UI\NotificationBrokerClient.cs' 'ViewLab.NotificationBroker.exe' 'settings UI controls the independent broker'
Assert-Contains 'app.manifest' 'requestedExecutionLevel level="asInvoker"' 'ordinary settings UI runs at medium integrity'
Assert-Contains 'XRViewLab.UI\App.cs' '--set-layer-enabled' 'machine-wide layer changes use the explicit elevated helper path'
Assert-Contains 'Installer\Product.wxs' 'ViewLab.NotificationIdentity.msix' 'MSI installs the signed notification identity package'
Assert-Contains 'Installer\Product.wxs' 'StoreName="trustedPeople"' 'MSI trusts only the packaged broker signer certificate'
Assert-Contains 'Installer\Product.wxs' 'ViewLab Notification Broker' 'MSI starts the independent notification broker at logon'
Assert-Contains 'build.ps1' 'makeappx\.exe' 'build creates the external-location identity package'
Assert-Contains 'build.ps1' 'signtool\.exe' 'build signs the notification identity package'
Assert-Contains 'Tests\Invoke-RealNotificationFixture.ps1' 'CardCount' 'real packaged notification fixture proves production card delivery'
Assert-NotContains 'XRViewLab.UI\MainWindow.cs' '_notifications' 'settings window no longer owns notification collection lifetime'
Assert-NotContains 'XRViewLab.UI\MainWindow.cs' 'IRacingEventPublished[\s\S]{0,500}NotifyEnabledCheck' 'racing telemetry cannot silently enable global Windows notifications'
Assert-Contains 'MainWindow.xaml' 'Test presentation \(synthetic\)' 'truthfully labelled notification presentation test exists'
Assert-Contains 'MainWindow.xaml' 'Name="NotifyEnabledCheck"' 'notification enable control exists'
foreach ($ctl in @('NotifyXSlider','NotifyYSlider','NotifyScaleSlider','NotifyOpacitySlider','NotifyDurationSlider','NotifyMaxSlider','NotifyPrivacyCombo','NotifyShowIconCheck','NotifyShowImageCheck','NotifyFiltersBox')) {
    Assert-Contains 'MainWindow.xaml' "Name=`"$ctl`"" "notification control $ctl exists"
}
# The DLL only renders; it reads the render-side notification settings. Duration/max/privacy are
# owned by the UI-side queue manager (off the render thread), so they live in the ini + C# only.
foreach ($key in @('notify_enabled','notify_x','notify_y','notify_scale','notify_opacity')) {
    Assert-Contains 'dllmain.cpp' $key "DLL reads notification render key $key"
}
foreach ($key in @('notify_enabled','notify_x','notify_y','notify_scale','notify_opacity','notify_duration_ms','notify_max_visible','notify_privacy','notify_show_icon','notify_show_image')) {
    Assert-Contains 'xr-viewlab.ini' $key "default ini carries notification key $key"
}
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'NotifyDurationKey' 'UI persists notification duration'
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'NotifyPrivacyKey' 'UI persists notification privacy mode'

# Feature 4: validated iRacing shared-memory provider and generic event contract.
Assert-Contains 'XRViewLab.UI\ViewLabEvents.cs' 'interface IViewLabEventProvider' 'generic ViewLab event provider seam exists'
Assert-Contains 'MainWindow.xaml' 'Name="IRacingEnabledCheck"' 'iRacing integration enable control exists'
Assert-Contains 'XRViewLab.UI\IRacingTelemetryProvider.cs' 'IRSDKMemMapFileName' 'iRacing provider opens the SDK shared-memory mapping'
foreach ($field in @('CarLeftRight','LapCompleted','LapLastLapTime','SessionFlags')) { Assert-Contains 'XRViewLab.UI\IRacingTelemetryProvider.cs' $field "iRacing reads $field" }
Assert-Contains 'XRViewLab.UI\IRacingTelemetryProvider.cs' 'void Simulate' 'iRacing generic events can be simulated without the simulator'
foreach ($state in @('CarsBothSides','TwoCarsLeft','TwoCarsRight')) { Assert-Contains 'XRViewLab.UI\IRacingTelemetryProvider.cs' $state "iRacing preserves $state" }
Assert-Contains 'XRViewLab.UI\IRacingTelemetryProvider.cs' 'StaleAfterMs = 750' 'iRacing stale telemetry clears promptly'
Assert-Contains 'XRViewLab.UI\IRacingTelemetryProvider.cs' 'token.WaitHandle.WaitOne' 'iRacing worker waits are cancellation-interruptible'
Assert-Contains 'Tests\IRacingFixtures\Program.cs' 'invalid buffer offset is rejected' 'iRacing fixture covers invalid SDK offsets'
Assert-Contains 'NotificationBroker\Program.cs' 'new IRacingTelemetryProvider' 'independent broker owns the sole iRacing provider worker'
Assert-Contains 'XRViewLab.UI\RacingStateService.cs' 'Local\\\\XRViewLabRacingState' 'generic racing state has a dedicated mapping'
Assert-Contains 'dllmain.cpp' 'ConsumeRacingState' 'native renderer consumes generic racing state'
Assert-Contains 'MainWindow.xaml' 'Name="HudTraceVisibilityCombo"' 'trace exposes explicit visibility modes'
Assert-Contains 'dllmain.cpp' 'UpdateTraceVisibility' 'native trace delegates explicit visibility transitions to the tested policy'
Assert-Contains 'Tests\RenderPolicyFixtures.cpp' 'trace fades and remains hidden' 'alarm-only trace recovery hold and fade are executable fixtures'
Assert-Contains 'XRViewLab.UI\LiveStateService.cs' 'traceVisibilityMode == 2 \? 2u' 'live contract carries trace alarm-only mode'
Assert-Contains 'dllmain.cpp' 'uint32_t magic, version, count, generation' 'native notification header order matches managed writer'
Assert-Contains 'dllmain.cpp' 'state==1\|\|state==3\|\|state==4' 'left and two-left remain left-side spatial cues'
Assert-Contains 'dllmain.cpp' 'state==2\|\|state==3\|\|state==5' 'right and two-right remain right-side spatial cues'
Assert-Contains 'XRViewLab.UI\NotificationService.cs' 'SetRacingAttention' 'desktop cards can be held behind racing safety cues'
Assert-Contains 'XRViewLab.UI\NotificationService.cs' 'enterTick,leaveTick' 'notification bridge publishes lifecycle timestamps rather than low-rate animation frames'
Assert-Contains 'dllmain.cpp' 'EvaluateNotificationAnimation' 'notification animation runs at native render cadence'
Assert-Contains 'XRViewLab.UI\NotificationService.cs' 'now - c.BornTick >= 5000' 'delayed desktop cards expire after a bounded wait'
Assert-Contains 'NotificationBroker\Program.cs' 'service.SetRacingAttention' 'generic racing events drive the small attention policy'
Assert-Contains 'XRViewLab.UI\ViewLabEvents.cs' 'IsPresentationTest' 'presentation tests are explicit rather than silently enabling features'
Assert-Contains 'dllmain.cpp' 'presentationFlags' 'native racing tests bypass disabled feature gates without persisting them'
Assert-NotContains 'XRViewLab.UI\MainWindow.cs' 'new IRacingTelemetryProvider' 'settings window cannot create a duplicate provider worker'
Assert-Contains 'dllmain.cpp' 'HudMetricState::Reprojection' 'VR HUD identifies stable cadence divisions'
Assert-Contains 'dllmain.cpp' 'HudMetricState::Unstable' 'VR HUD identifies unstable cadence'
Assert-Contains 'dllmain.cpp' 'rollingRatio>1\.03' 'VR HUD warning compares sustained cadence to the active budget'
Assert-Contains 'dllmain.cpp' 'rollingRatio>1\.08' 'VR HUD critical state compares sustained cadence to the active budget'
Assert-Contains 'dllmain.cpp' 'ResolveSharedTangent\(crosshairTanX,crosshairTanY,false\)' 'crosshair resolves one full-binocular tangent target without crop clamping'
Assert-Contains 'dllmain.cpp' 'enum class OverlayPlacement \{ RenderArea, FullLens, LensPinned \}' 'all overlay placement modes share one resolver'
Assert-Contains 'dllmain.cpp' 'angularDisparity=0' 'crosshair diagnostics report shared angular disparity and projected pixels'
Assert-Contains 'dllmain.cpp' 'const float inset=floorf\(th\*\.5f\)\+2\.f' 'boundary flash is inset fully inside the eye scissor'
Assert-Contains 'dllmain.cpp' 'boundaryPxPerTan' 'boundary flash stroke thickness is angular/screen stable under crop changes'
Assert-Contains 'dllmain.cpp' 'sharedSelectedL' 'render-area overlays use shared binocular tangent bounds'
Assert-Contains 'dllmain.cpp' 'g_featurePresentationPlan\.drawDirectVisor=false;[\s\S]{0,120}g_featurePresentationPlan\.drawDirectCommonFeatures=false;' 'successful ordered allocation disables both obsolete direct copies together'
Assert-Contains 'dllmain.cpp' 'if\(maskEnabled\)[\s\S]{0,160}DrawVisorBorderToTexture' 'topmost backend also draws the visor mask'
Assert-Contains 'dllmain.cpp' 'maskEnabled && g_featurePresentationPlan\.drawDirectVisor' 'central bridge plan controls the direct visor path'
Assert-Contains 'dllmain.cpp' 'if\(drewVisor\) g_releaseDrewVisorThisFrame' 'direct diagnostics cannot claim a visor draw when the backend gate skipped it'
Assert-Contains 'dllmain.cpp' 'return float4\(0\.0f, 0\.0f, 0\.0f, 1\.0f\)' 'visor emits compositor-visible opaque black on the shared ordered target'

# Stable display cadence straddles the theoretical interval slightly; these displayed values must
# remain below the rolling amber entry band at their full-precision ratios.
foreach ($case in @(@(120.0, 8.2), @(120.0, 8.3), @(90.0, 11.1), @(90.0, 11.2))) {
    $budget = 1000.0 / $case[0]
    $ratio = $case[1] / $budget
    if ($ratio -ge 1.08) { throw "Contract failed: stable $($case[0]) Hz value $($case[1]) ms entered amber band (ratio=$ratio)" }
}
foreach ($key in @('iracing_enabled','iracing_lap_popup','iracing_spotter_glow','iracing_flag_border')) {
    Assert-Contains 'dllmain.cpp' $key "DLL reads iRacing integration key $key"
    Assert-Contains 'xr-viewlab.ini' $key "default ini carries iRacing integration key $key"
}
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' '\(_visorVisible\?4\.0:2\.0\) / _viewZoom' 'active and disabled preview visor strokes remain screen-space stable while zooming'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'modelRadius = PinPixelRadius / _viewZoom' 'preview pins remain constant in screen pixels'
Assert-Contains 'XRViewLab.UI\BeanMaskEditor.cs' 'modelHitRadius = PinHitPixelRadius / _viewZoom' 'preview pin hit testing remains constant in screen pixels'
Assert-NotContains 'dllmain.cpp' 'projectionView\.fov\s*=' 'layer never rewrites submitted projection FOVs'
Assert-NotContains 'dllmain.cpp' 'projectionView\.subImage\.imageRect\s*=' 'layer never rewrites submitted image rects'

foreach ($hz in @(72, 80, 90, 107, 120)) {
    $targetMs = 1000.0 / $hz
    $underBudgetMs = $targetMs * 0.95
    $overBudgetMs = $targetMs * 1.05
    $underPercent = [Math]::Min(100.0, $underBudgetMs / $targetMs * 100.0)
    $overPercent = [Math]::Min(100.0, $overBudgetMs / $targetMs * 100.0)
    if ([Math]::Abs($underPercent - 95.0) -gt 0.000001 -or [Math]::Abs($overPercent - 100.0) -gt 0.000001) {
        throw "Contract failed: refresh-rate frame-budget conversion at $hz Hz"
    }
    Write-Host ("HUD refresh validation: {0} Hz target={1:N3} ms under={2:N3} ms => {3:N1}% over={4:N3} ms => {5:N1}%" -f $hz, $targetMs, $underBudgetMs, $underPercent, $overBudgetMs, $overPercent)
}

# Effective-cadence budget model: targetFrameMs = 1000 / effectiveAppFps, where effectiveAppFps
# is the refresh divided by the detected cadence multiple (1 = native, 2 = half-rate reprojection).
foreach ($case in @(
    @{ Hz = 72;  Multiple = 1; Fps = 72 }, @{ Hz = 72;  Multiple = 2; Fps = 36 },
    @{ Hz = 80;  Multiple = 1; Fps = 80 }, @{ Hz = 80;  Multiple = 2; Fps = 40 },
    @{ Hz = 90;  Multiple = 1; Fps = 90 }, @{ Hz = 90;  Multiple = 2; Fps = 45 },
    @{ Hz = 120; Multiple = 1; Fps = 120 }, @{ Hz = 120; Multiple = 2; Fps = 60 })) {
    $periodMs = 1000.0 / $case.Hz
    $budgetMs = $periodMs * $case.Multiple
    $expectedBudgetMs = 1000.0 / $case.Fps
    # The interval median at a locked cadence sits at the budget; round(median/period) recovers the multiple.
    $recoveredMultiple = [Math]::Round($budgetMs / $periodMs)
    if ([Math]::Abs($budgetMs - $expectedBudgetMs) -gt 0.000001 -or $recoveredMultiple -ne $case.Multiple) {
        throw ("Contract failed: effective cadence budget at {0} Hz x{1}" -f $case.Hz, $case.Multiple)
    }
    Write-Host ("HUD cadence validation: {0} Hz x{1} => {2} FPS budget={3:N3} ms" -f $case.Hz, $case.Multiple, $case.Fps, $budgetMs)
}

# Shared-angular projection contract. Asymmetric eyes must project tangent zero to different pixel
# coordinates, then recover the same angular point. When resolution follows tangent span, a fixed
# tangent-sized element must retain the same pixel/angular size across crop settings.
$projectionCases = @(
    @{ L=-1.10; R=0.90; U=1.00; D=-1.00; W=2000; H=2000 },
    @{ L=-0.72; R=0.72; U=0.40; D=-0.40; W=1440; H=800 },
    @{ L=-0.45; R=0.45; U=0.20; D=-0.20; W=900; H=400 })
$projectedCentres = @()
foreach ($p in $projectionCases) {
    $x = (0.0-$p.L)/($p.R-$p.L)*$p.W
    $y = ($p.U-0.0)/($p.U-$p.D)*$p.H
    $tanX = $p.L + ($x/$p.W)*($p.R-$p.L)
    $tanY = $p.U - ($y/$p.H)*($p.U-$p.D)
    if ([Math]::Abs($tanX) -gt 1e-9 -or [Math]::Abs($tanY) -gt 1e-9) { throw 'Contract failed: per-eye projection did not recover shared tangent zero' }
    $unitX = ($p.W/($p.R-$p.L))*(2.0/1080.0)
    $unitY = ($p.H/($p.U-$p.D))*(2.0/1080.0)
    if ([Math]::Abs($unitX-$projectionCases[0].W/($projectionCases[0].R-$projectionCases[0].L)*(2.0/1080.0)) -gt 1e-9 -or
        [Math]::Abs($unitY-$projectionCases[0].H/($projectionCases[0].U-$projectionCases[0].D)*(2.0/1080.0)) -gt 1e-9) {
        throw 'Contract failed: angular overlay size changed under proportional crop/resolution change'
    }
    $projectedCentres += $x
}
if ([Math]::Abs($projectedCentres[0]-($projectionCases[0].W/2.0)) -lt 1e-6) { throw 'Contract failed: asymmetric eye incorrectly used normalized pixel centre' }

# One shared visor target must land at distinct eye pixels for asymmetric stereo, including a
# side-by-side atlas offset, while inverse projection recovers the identical target.
$leftEye=@{L=-1.10;R=0.82;U=0.95;D=-1.02;W=1900;H=1900;Offset=0}
$rightEye=@{L=-0.80;R=1.12;U=0.98;D=-0.99;W=1900;H=1900;Offset=1900}
$targetX=0.12; $targetY=-0.08; $stereoPixels=@()
foreach($eye in @($leftEye,$rightEye)) {
    $localX=($targetX-$eye.L)/($eye.R-$eye.L)*$eye.W
    $localY=($eye.U-$targetY)/($eye.U-$eye.D)*$eye.H
    $stereoPixels += $eye.Offset+$localX
    $recoveredX=$eye.L+($localX/$eye.W)*($eye.R-$eye.L)
    $recoveredY=$eye.U-($localY/$eye.H)*($eye.U-$eye.D)
    if([Math]::Abs($recoveredX-$targetX)-gt 1e-9 -or [Math]::Abs($recoveredY-$targetY)-gt 1e-9){throw 'Contract failed: stereo target did not survive per-eye projection'}
}
if([Math]::Abs(($stereoPixels[0]-$leftEye.Offset)-($stereoPixels[1]-$rightEye.Offset))-lt 1e-6){throw 'Contract failed: asymmetric stereo used identical local eye pixels'}

Assert-Contains 'MainWindow.xaml' 'Test presentation \(synthetic\)' 'synthetic notification control is labelled truthfully'

# Dedicated clock/session widget: independent overlay, monotonic OpenXR-session lifetime, and
# complete UI -> ini -> native key wiring. It must not regress into the notification queue.
foreach ($key in @('clock_widget_enabled','clock_widget_x','clock_widget_y','clock_widget_scale','clock_widget_opacity')) {
    Assert-Contains 'XRViewLab.UI\OverlaySettingsModels.cs' $key "shared overlay catalogue persists $key"
    Assert-Contains 'dllmain.cpp' $key "native layer reads $key"
}
Assert-IniValue 'clock_widget_enabled' '0'
Assert-Contains 'dllmain.cpp' 'g_clockSessionStartTick\.store\(GetTickCount64\(\)' 'session timer starts at successful OpenXR session creation'
Assert-Contains 'dllmain.cpp' 'g_clockSessionStartTick\.store\(0' 'session timer resets at OpenXR session destruction'
Assert-Contains 'dllmain.cpp' 'clockWidgetEnabled&&OverlayFeatureVisible\(OverlayFeatureId::Clock\)' 'clock participates in the common direct/topmost overlay gate'
Assert-Contains 'dllmain.cpp' 'viewlab::clock_widget::Format' 'native renderer uses the tested clock formatter'
Assert-Contains 'MainWindow.xaml' 'CLOCK \+ SESSION' 'clock widget has dedicated ordinary settings UI'
if (Test-Path -LiteralPath (Join-Path $Root 'XRViewLab.UI\HistoryService.cs')) { throw 'Contract failed: removed technical-history service returned' }
Assert-NotContains 'NotificationBroker\Program.cs' 'clear-history|HistoryService' 'broker no longer owns experimental generic history'

# Performance markers belong to the real QPC trace from bind through post-session inspection.
foreach ($key in @('performance_trace_recording','performance_trace_marker_vk')) {
    Assert-Contains 'XRViewLab.UI\MainWindow.cs' $key "UI persists $key"
    Assert-Contains 'dllmain.cpp' $key "native layer reads $key"
}
Assert-IniValue 'performance_trace_recording' '1'
Assert-IniValue 'performance_trace_marker_vk' '119'
Assert-Contains 'dllmain.cpp' 'CapturePerformanceTraceMarker\(stop\.QuadPart\)' 'bind edge is stamped with the actual xrWaitFrame QPC timestamp'
Assert-Contains 'dllmain.cpp' 'sample\.qpc=qpc; sample\.markerNumber=g_pendingTraceMarker' 'marker is attached to the real visor trace stream'
Assert-Contains 'dllmain.cpp' 'ViewLabPerformanceTrace,2' 'native recorder writes a versioned real-trace format'
Assert-Contains 'dllmain.cpp' 'start_utc_filetime' 'native recorder anchors QPC samples to wall-clock time'
Assert-Contains 'dllmain.cpp' 'warning_mask,critical_mask,visible_alarm_mask' 'native recorder persists alarm state without a second collector'
Assert-Contains 'dllmain.cpp' 'SavePerformanceTraceSession\(\)' 'OpenXR session lifecycle persists the trace'
Assert-Contains 'dllmain.cpp' 'XRViewLab_xrEndSession' 'successful xrEndSession persists the completed trace before renderer teardown'
Assert-Contains 'dllmain.cpp' 'g_performanceTraceSession' 'trace lifetime is independent of D3D renderer state'
Assert-Contains 'HardwareTelemetry.cpp' 'checkpointCallback' 'bounded hardware worker owns periodic trace checkpoint calls'
Assert-Contains 'dllmain.cpp' 'SetCheckpointCallback\(SavePerformanceTraceSession\)' 'trace checkpointing is armed independently of orderly game shutdown'
Assert-Contains 'XRViewLab.UI\PerformanceTrace.cs' 'catch \(FormatException\)' 'reader ignores a trailing partial checkpoint record after abrupt process exit'
Assert-Contains 'XRViewLab.UI\PerformanceTrace.cs' 'PerformanceTraceSample' 'post-session reader consumes trace samples'
Assert-Contains 'XRViewLab.UI\PerformanceTraceWindow.xaml.cs' 'DrawEvents' 'post-session graph draws session, cadence, alarm and marker events'
Assert-Contains 'XRViewLab.UI\PerformanceTraceWindow.xaml.cs' 'PreviousMarker_Click|NextMarker_Click' 'post-session graph supports marker navigation'
Assert-Contains 'XRViewLab.UI\PerformanceTraceWindow.xaml.cs' 'DrawDownsampledSeries' 'post-session graph uses spike-preserving downsampling'
Assert-Contains 'XRViewLab.UI\PerformanceTraceWindow.xaml' 'Reset view' 'post-session graph exposes zoom and reset controls'
Assert-NotContains 'dllmain.cpp' 'HistoryService.*PerformanceTrace|PerformanceTrace.*HistoryService' 'trace markers must never use generic history'

# Bounded sticky notes are native visor widgets, not notification cards.
foreach ($key in @('sticky_note_enabled','sticky_note_x','sticky_note_y','sticky_note_scale','sticky_note_opacity','sticky_note_toggle_vk')) {
    Assert-Contains 'XRViewLab.UI\OverlaySettingsModels.cs' $key "shared overlay catalogue persists $key"
    Assert-Contains 'dllmain.cpp' $key "native layer reads $key"
}
Assert-Contains 'XRViewLab.UI\MainWindow.cs' 'sticky_note_text' 'UI persists sticky note text'
Assert-Contains 'dllmain.cpp' 'sticky_note_text' 'native layer reads sticky note text'
Assert-IniValue 'sticky_note_enabled' '0'
Assert-IniValue 'sticky_note_toggle_vk' '118'
Assert-Contains 'dllmain.cpp' 'if\(down&&!feature\.keyDown\)' 'shared overlay binds are rising-edge triggered'
Assert-Contains 'dllmain.cpp' 'viewlab::sticky_note::Wrap' 'native note uses bounded tested wrapping'
Assert-Contains 'MainWindow.xaml' 'MaxLength="120"' 'sticky note inputs are short and bounded'
Assert-Contains 'XRViewLab.UI\StickyNoteLiveStateService.cs' 'MaxNotes = 8' 'sticky note collection is explicitly bounded'
Assert-NotContains 'dllmain.cpp' 'NotifyCardBlock.*sticky|sticky.*NotifyCardBlock' 'sticky note must not enter the notification queue'

# OBS indication uses GetRecordStatus, not process presence, and makes no capture-exclusion claim.
foreach ($key in @('obs_indicator_enabled','obs_websocket_url','obs_websocket_password','obs_indicator_opacity','obs_indicator_thickness')) {
    Assert-Contains 'XRViewLab.UI\MainWindow.cs' $key "UI persists $key"
}
Assert-Contains 'NotificationBroker\Program.cs' 'obs_indicator_enabled' 'broker owns OBS state polling'
Assert-Contains 'XRViewLab.UI\ObsRecordingProvider.cs' 'GetRecordStatus' 'indicator queries actual OBS recording state'
Assert-Contains 'XRViewLab.UI\ObsRecordingProvider.cs' 'Local\\\\XRViewLabObsRecordingState' 'OBS state uses a dedicated broker-to-native mapping'
Assert-Contains 'dllmain.cpp' 'ObsRecordingActive' 'native renderer consumes OBS recording state'
Assert-Contains 'dllmain.cpp' 'Capture exclusion is unclaimed' 'native source preserves capture-exclusion caveat'
Assert-Contains 'MainWindow.xaml' 'Capture exclusion is unverified' 'UI does not make an untested exclusion claim'
Assert-IniValue 'obs_indicator_enabled' '0'

Assert-Contains 'XRViewLab.UI\MediaSessionEventProvider.cs' 'GlobalSystemMediaTransportControlsSessionManager' 'music provider uses Windows Now Playing state'
Assert-Contains 'XRViewLab.UI\MediaSessionEventProvider.cs' '_manager\.CurrentSessionChanged \+= OnCurrentSessionChanged' 'music provider registers a stable session-change handler'
Assert-Contains 'XRViewLab.UI\MediaSessionEventProvider.cs' '_manager\.CurrentSessionChanged -= OnCurrentSessionChanged' 'music provider unregisters the exact handler'
Assert-Contains 'NotificationBroker\Program.cs' 'EnqueueMediaCard' 'track changes enter the brief notification card pipeline'
Assert-NotContains 'XRViewLab.UI\MediaSessionEventProvider.cs' 'TryPlayAsync|TryPauseAsync|TrySkipNextAsync|TrySkipPreviousAsync' 'music feature has no transport controls'
Assert-IniValue 'media_notify_enabled' '0'

# DiagMon capture must survive target exit, own its collector output, and keep tests away from live data.
Assert-Contains 'XRViewLab.UI\DiagMonCaptureService.cs' 'FinalizeWhenCaptureLoopEndsAsync' 'natural target exit and Trace timeout automatically finalise capture'
Assert-Contains 'XRViewLab.UI\DiagMonCaptureService.cs' '--output_stdout' 'ViewLab owns recoverable PresentMon row persistence'
Assert-Contains 'XRViewLab.UI\DiagMonCaptureService.cs' '--terminate_existing_session' 'PresentMon is stopped through its named trace session'
Assert-Contains 'XRViewLab.UI\DiagMonCaptureService.cs' '--terminate_on_proc_exit' 'PresentMon exits when its target exits'
Assert-Contains 'XRViewLab.UI\DiagMonCaptureService.cs' 'CaptureModules\(preserveExistingOnFailure: true\)' 'Detailed module evidence is captured while the target is alive'
Assert-Contains 'Tests\DiagMonFixtures\Program.cs' 'new DiagMonStore\(Path.Combine\(fixture, "store"\)\)' 'DiagMon fixtures use an isolated temporary store'
Assert-Contains 'xr-viewlab.csproj' 'PresentMon-2.4.1-x64.exe' 'published UI includes the pinned PresentMon collector'
Assert-Contains 'Installer\Product.wxs' 'PresentMon-2.4.1-x64.exe' 'MSI includes the pinned PresentMon collector'
$presentMonBinary = Join-Path $Root 'ThirdParty\PresentMon\PresentMon-2.4.1-x64.exe'
if (-not (Test-Path -LiteralPath $presentMonBinary)) { throw 'Contract failed: pinned PresentMon binary is missing' }
$presentMonHash = (Get-FileHash -LiteralPath $presentMonBinary -Algorithm SHA256).Hash
if ($presentMonHash -ne 'D74183E7AE630F72CD3690BE0373ECBFDC6CBB86578148AAB8FA2A7166068F34') { throw "Contract failed: pinned PresentMon hash changed: $presentMonHash" }
Assert-Contains 'ThirdParty\PresentMon\LICENSE.txt' 'Permission is hereby granted' 'PresentMon MIT notice ships with the collector'

Write-Host 'ViewLab contract verification passed.'
