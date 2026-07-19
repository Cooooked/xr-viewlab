$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Assert-Close([double]$actual, [double]$expected, [double]$epsilon, [string]$name) {
    if ([Math]::Abs($actual - $expected) -gt $epsilon) {
        throw "$name expected $expected, got $actual"
    }
}

$width = 550.0
$height = 480.0
Assert-Close ($width / $height) (55.0 / 48.0) 0.0000001 'fixed binocular box aspect'

# Preview percentages map once into the centred box. Horizontal 0.8 is visibly 80%, not
# reinterpreted through eye overlap or cropped a second time.
$h = 0.8
$cropWidth = $width * $h
Assert-Close ($cropWidth / $width) 0.8 0.0000001 'preview horizontal 0.8 retained width'
Assert-Close (($width - $cropWidth) / 2.0) 55.0 0.0000001 'preview horizontal crop remains centred'

# Native outer-only crop keeps the inner boundary fixed while retaining exactly the requested
# fraction of the submitted signed tangent span.
$leftOuter = -1.1; $leftInner = 0.9
$croppedLeftOuter = $leftInner - ($leftInner - $leftOuter) * $h
Assert-Close (($leftInner - $croppedLeftOuter) / ($leftInner - $leftOuter)) 0.8 0.0000001 'native left-eye horizontal 0.8 retained span'
$rightInner = -0.9; $rightOuter = 1.1
$croppedRightOuter = $rightInner + ($rightOuter - $rightInner) * $h
Assert-Close (($croppedRightOuter - $rightInner) / ($rightOuter - $rightInner)) 0.8 0.0000001 'native right-eye horizontal 0.8 retained span'

# All preview layers use one geometric centre. Calibrated spans affect replica size only.
$fullHorizontalSpan=1.67819926235456
$fullVerticalSpan=2.39383678154919

$asymmetricEyes=@(
    @{ L=-1.10; R=.82; U=.95; D=-1.02; W=1900 },
    @{ L=-.78; R=1.16; U=1.03; D=-.91; W=2016 }
)
foreach($eye in $asymmetricEyes) {
    $pixelAtZero=(-$eye.L/($eye.R-$eye.L))*$eye.W
    $recovered=$eye.L+($pixelAtZero/$eye.W)*($eye.R-$eye.L)
    Assert-Close $recovered 0 0.000000001 'asymmetric tangent projection inverse'
    if ([Math]::Abs($pixelAtZero-$eye.W*.5) -lt 0.001) { throw 'asymmetric tangent zero incorrectly projected to pixel centre' }
}

$availableHeight=600.0
$fittedHeight=546.0/(55.0/48.0)
$geometricTop=($availableHeight-$fittedHeight)*.5
$opticalTop=$availableHeight*.403406273247302-$fittedHeight*.5
Assert-Close $fittedHeight $fittedHeight 0.0000001 'optical centre preserves preview height'
Assert-Close ($opticalTop-$geometricTop) ($availableHeight*(.403406273247302-.5)) 0.0000001 'optical centre is translation only'
$geometricCropHeight=$fittedHeight*.15
$opticalCropHeight=$fittedHeight*.15
Assert-Close $opticalCropHeight $geometricCropHeight 0.0000001 'optical centre preserves crop geometry'

function Preview-Crop([double]$topScale, [double]$bottomScale) {
    $topShare=[Math]::Min(1.0,[Math]::Max(0.0,$topScale))*.5
    $bottomShare=[Math]::Min(1.0,[Math]::Max(0.0,$bottomScale))*.5
    return @((0.5 - $topShare), ($topShare + $bottomShare))
}
$crop15 = Preview-Crop 0.15 0.15
Assert-Close $crop15[0] 0.425 0.0000001 'equal 15% crop remains centred'
Assert-Close $crop15[1] 0.15 0.0000001 '15% crop retained height'
$full = Preview-Crop 1 1; Assert-Close $full[0] 0 0.0000001 'full split top'; Assert-Close $full[1] 1 0.0000001 'full split height'
$topOnly = Preview-Crop 1 0; Assert-Close $topOnly[0] 0 0.0000001 'top only starts at top'; Assert-Close $topOnly[1] .5 0.0000001 'top only is top half'
$bottomOnly = Preview-Crop 0 1; Assert-Close $bottomOnly[0] .5 0.0000001 'bottom only starts at centre'; Assert-Close $bottomOnly[1] .5 0.0000001 'bottom only is bottom half'
$half = Preview-Crop .5 .5; Assert-Close $half[0] .25 0.0000001 'equal half controls start at quarter'; Assert-Close $half[1] .5 0.0000001 'equal half controls retain middle half'

# Saved widget positions are full-lens normalized in INI, live state, profile registry and native
# OverlayPlacement::FullLens. The crop clips that coordinate system; it does not redefine it.
function FullLens-Anchor([double]$x, [double]$y) { return @(($x*$width),($y*$height)) }
$fixtures = @(
    @{ Name='HUD'; X=.216; Y=.053; Scale=.319 },
    @{ Name='Performance Trace'; X=.159; Y=.363; Scale=.767 },
    @{ Name='Clock'; X=.212; Y=.870; Scale=.361 }
)
foreach($fixture in $fixtures) {
    $previewAnchor=FullLens-Anchor $fixture.X $fixture.Y
    $runtimeX=$fixture.X*$width
    $runtimeY=$fixture.Y*$height
    Assert-Close $previewAnchor[0] $runtimeX 0.0000001 "$($fixture.Name) preview/runtime X"
    Assert-Close $previewAnchor[1] $runtimeY 0.0000001 "$($fixture.Name) preview/runtime Y"
}

# Actual placement rectangles on the 550x480 reference, using the same tangent-space footprint
# equations as native. Comparing in the full-lens frame removes eye-buffer resolution from the result.
$pixelsPerTangent=[Math]::Min($width/$fullHorizontalSpan,$height/$fullVerticalSpan)
$hudUnit=$pixelsPerTangent*(2.0/1080.0)*64.0*.319
$hudRadius=$hudUnit*.48; $hudGap=$hudUnit*(.25+3*.018)
$hudRect=@(118.8,25.44,($hudRadius*2*11+$hudGap*10),($hudRadius*2+$hudUnit*.08+$hudUnit*.045*7))
$traceRect=@(87.45,174.24,($width*.184*.767),($pixelsPerTangent*(2.0/1080.0)*64*.55*.767))
$clockGlyphX=$width/$fullHorizontalSpan*(2.0/1080.0)*2.1*.361
$clockGlyphY=$height/$fullVerticalSpan*(2.0/1080.0)*2.1*.361
$clockW=77.75*$clockGlyphX;$clockH=23.05*$clockGlyphY
$clockRect=@((.212*$width-$clockW/2),(.87*$height-$clockH/2),$clockW,$clockH)
$expectedRects=@(
    @('HUD',$hudRect,@(118.8,25.44,103.100912083,10.2721864612)),
    @('Performance Trace',$traceRect,@(87.45,174.24,77.6204,10.025152075)),
    @('Clock',$clockRect,@(98.713626207,414.355704736,35.772747586,6.488590528))
)
foreach($entry in $expectedRects) { for($i=0;$i-lt 4;$i++) { Assert-Close $entry[1][$i] $entry[2][$i] .000001 "$($entry[0]) runtime/preview rectangle component $i" } }

# Drawing and dragging are exact inverses over the full preview. At 15% crop a 7.2 px drag
# changes Y by .015, not the broken crop-relative .100 (6.67 times too large).
$startX=.216; $startY=.363; $dx=11.0; $dy=7.2
$drawX=$startX*$width; $drawY=$startY*$height
$movedX=$startX+$dx/$width; $movedY=$startY+$dy/$height
Assert-Close ($movedX*$width-$drawX) $dx 0.0000001 'forward/inverse X round trip'
Assert-Close ($movedY*$height-$drawY) $dy 0.0000001 'forward/inverse Y round trip'
Assert-Close ($movedY-$startY) .015 0.0000001 'small vertical drag remains proportional'

# Save/restart fixture: production writes 3 decimals and reloads invariant doubles. Values already
# at that precision must survive a real file round trip without relocation or migration.
$tempFile=[IO.Path]::GetTempFileName()
try {
    $lines=$fixtures|ForEach-Object { "$($_.Name)=$($_.X.ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)),$($_.Y.ToString('0.###',[Globalization.CultureInfo]::InvariantCulture)),$($_.Scale.ToString('0.###',[Globalization.CultureInfo]::InvariantCulture))" }
    [IO.File]::WriteAllLines($tempFile,$lines)
    $reloaded=[IO.File]::ReadAllLines($tempFile)
    for($i=0;$i-lt $fixtures.Count;$i++) {
        $parts=$reloaded[$i].Split('=')[1].Split(',')
        Assert-Close ([double]::Parse($parts[0],[Globalization.CultureInfo]::InvariantCulture)) $fixtures[$i].X 0.0000001 "$($fixtures[$i].Name) restart X"
        Assert-Close ([double]::Parse($parts[1],[Globalization.CultureInfo]::InvariantCulture)) $fixtures[$i].Y 0.0000001 "$($fixtures[$i].Name) restart Y"
        Assert-Close ([double]::Parse($parts[2],[Globalization.CultureInfo]::InvariantCulture)) $fixtures[$i].Scale 0.0000001 "$($fixtures[$i].Name) restart scale"
    }
} finally { Remove-Item -LiteralPath $tempFile -Force }

# Crosshair shares the preview centre and applies the existing normalized display offset.
$crosshairY=.5+.042*.5
Assert-Close $crosshairY 0.521 0.0000001 'crosshair shared-centre preview Y'

# Eye guides are true pixel circles. Their combined useful width is 0.85 and their height is
# 0.90, placing a centred 0.8 crop only 2.5% of the box width inside the dotted outer edge.
$diameter = $height * 0.90
$guideUnion = $width * 0.85
$centreSeparation = $guideUnion - $diameter
if ($centreSeparation -ge $diameter) { throw 'eye guides do not overlap' }
Assert-Close (($guideUnion - $cropWidth) / 2.0 / $width) 0.025 0.0000001 '0.8 crop aligns near dotted usable edge'

# Per-eye frame mode preserves the actual 2064:2208 eye aspect and the two rectangles overlap
# while their union remains the same combined outer frame.
$eyeAspect = 2064.0 / 2208.0
$eyeFrameWidth = $height * $eyeAspect
$eyeFrameSeparation = $width - $eyeFrameWidth
Assert-Close ($eyeFrameWidth / $height) $eyeAspect 0.0000001 'per-eye frame aspect'
if ($eyeFrameSeparation -ge $eyeFrameWidth) { throw 'per-eye frame guides do not overlap' }
Assert-Close ($eyeFrameWidth + $eyeFrameSeparation) $width 0.0000001 'per-eye frame union'
$adjustedSeparation = $eyeFrameSeparation * 70.0 / 67.0
if ($adjustedSeparation -le $eyeFrameSeparation) { throw 'higher preview IPD did not reduce eye-guide overlap' }
Assert-Close ($eyeFrameSeparation * 67.0 / 67.0) $eyeFrameSeparation 0.0000001 '67.0 preview IPD calibration'

$main = Get-Content (Join-Path $root 'XRViewLab.UI\MainWindow.cs') -Raw
$profile = Get-Content (Join-Path $root 'XRViewLab.UI\ProfileWindow.cs') -Raw
$profileXaml = Get-Content (Join-Path $root 'ProfileWindow.xaml') -Raw
$native = Get-Content (Join-Path $root 'dllmain.cpp') -Raw
$preview = Get-Content (Join-Path $root 'XRViewLab.UI\BeanMaskEditor.cs') -Raw
$geometry = Get-Content (Join-Path $root 'XRViewLab.UI\Quest3PreviewGeometry.cs') -Raw

foreach ($contract in @(
    @{ Text=$profile; Pattern='UseGlobalValues'; Name='separate whole-profile global action' },
    @{ Text=$profile; Pattern='UseGlobalVisor'; Name='separate visor-only global state' },
    @{ Text=$main; Pattern='double savedVisorSize = profileWindow\.UseGlobalVisor \? 0\.0 :'; Name='visor-only global save preserves crop profile' },
    @{ Text=$main; Pattern='registryKey\.SetValue\("profile_enabled", profileEnabled \? 1 : 0'; Name='custom profile override persistence' },
    @{ Text=$main; Pattern='registryKey\.SetValue\("mask_enabled", maskEnabled \? 1 : 0'; Name='per-app visor enable persistence' },
    @{ Text=$main; Pattern='appKey\.GetValue\("mask_enabled", fallbackMaskEnabled \? 1 : 0\)'; Name='per-app visor enable reload' },
    @{ Text=$main; Pattern='registryKey\.SetValue\("visor_width", ToMillis\(visorWidth\)'; Name='per-app visor width persistence' },
    @{ Text=$main; Pattern='registryKey\.SetValue\("visor_height", ToMillis\(visorHeight\)'; Name='per-app visor height persistence' },
    @{ Text=$main; Pattern='VisorWidth = visorWidth'; Name='per-app visor width reload' },
    @{ Text=$main; Pattern='VisorHeight = visorHeight'; Name='per-app visor height reload' },
    @{ Text=$native; Pattern='ReadProfileDword\(L"mask_enabled", profileMaskEnabled\)'; Name='native per-app visor enable load' },
    @{ Text=$native; Pattern='if \(profileVisorSize > 0\) \{\s*ReadProfileDword\(L"mask_enabled"'; Name='native enable override is gated by the custom visor sentinel' },
    @{ Text=$main; Pattern='registryKey\.DeleteValue\("mask_enabled", throwOnMissingValue: false\);\s*registryKey\.DeleteValue\("mask_rounded"'; Name='use-global visor clears custom enable and shape state' },
    @{ Text=$profile; Pattern='bool hasCustomVisor = visorSize > 0\.0;'; Name='profile distinguishes saved custom visor state' },
    @{ Text=$profile; Pattern='_customVisorSize = hasCustomVisor \?.+?: _globalVisorSize;'; Name='new custom visor starts from current global shape' },
    @{ Text=$profile; Pattern='MaskBeanEditor\.SetVisorVisible\(VisorEnabledCheck\?\.IsChecked == true\)'; Name='per-app active preview visibility' },
    @{ Text=$profile; Pattern='MaskBeanEditor\.WidthScale = VisorWidthSlider'; Name='per-app preview width parity' },
    @{ Text=$profile; Pattern='MaskBeanEditor\.HeightScale = VisorHeightSlider'; Name='per-app preview height parity' },
    @{ Text=$profileXaml; Pattern='Name="VisorEnabledCheck" Content="Enable visor mask"'; Name='editable per-app enable control' },
    @{ Text=$profileXaml; Pattern='Name="VisorWidthSlider"[^>]+Minimum="0\.25"[^>]+Maximum="2"'; Name='per-app Width range parity' },
    @{ Text=$profileXaml; Pattern='Name="VisorHeightSlider"[^>]+Minimum="0\.25"[^>]+Maximum="2"'; Name='per-app Height range parity' },
    @{ Text=$main; Pattern='LoadAppProfiles\(\);\s*StatusText\.Text = "Saved app profile'; Name='post-save registry reload' },
    @{ Text=$preview; Pattern='Quest3PreviewGeometry\.FullEyeGuides'; Name='overlapping circular eye guides' },
    @{ Text=$preview; Pattern='double radius = eye\.Width \* 0\.5'; Name='unstretched circle radius' },
    @{ Text=$geometry; Pattern='double width = area\.Width \* horizontal'; Name='single direct horizontal percentage mapping' },
    @{ Text=$geometry; Pattern='vertical >= 1\.0 - 0\.000001 \? area\.Height'; Name='full vertical preview uses exact frame height' },
    @{ Text=$geometry; Pattern='vertical >= 1\.0 - 0\.000001 \? area\.Top'; Name='full vertical preview uses exact frame top' },
    @{ Text=$geometry; Pattern='centre - height \* 0\.5'; Name='centred split vertical mapping' },
    @{ Text=$geometry; Pattern='internal static Rect SharedFullArea\(Rect area\) => area'; Name='single normalized overlay coordinate space' },
    @{ Text=$geometry; Pattern='InvertFullLens\(Rect area, Point point\)'; Name='coordinate inspector uses the shared inverse transform' },
    @{ Text=$preview; Pattern='DrawCoordinateInspector\(dc, area\)[\s\S]*InvertFullLens\(area, point\)[\s\S]*InvertFullLens\(crop, point\)'; Name='live full-lens and crop coordinate inspector' },
    @{ Text=$preview; Pattern='ResolveFullLens\(area,item\.X,item\.Y\+WidgetPreviewShimY\)[\s\S]*ApplyFullLensDrag\(overlayArea'; Name='widget placement (with permanent shim) and delta-based dragging share the full-lens conversion' },
    @{ Text=$preview; Pattern='DrawCrosshair\(dc, area\)[\s\S]*ResolveCentredOffset\(sizeReference,_crosshairOffsetX,_crosshairOffsetY\)'; Name='crosshair uses the shared visual centre' },
    @{ Text=$preview; Pattern='OnPreviewMouseRightButtonDown[\s\S]*IsOverPreviewWidget[\s\S]*ResetViewToStartupFit'; Name='empty preview right-click resets shared view navigation' },
    @{ Text=$preview; Pattern='ResetViewToStartupFit\(\)[\s\S]*_viewZoom=1\.0;_viewPan=new Vector\(\)'; Name='view reset reuses startup identity fit' },
    @{ Text=$geometry; Pattern='FullVerticalTangentSpan = 2\.39383678154919'; Name='replica size calibration is position-independent' },
    @{ Text=$geometry; Pattern='TangentReferencePixelsUniform'; Name='runtime-equivalent tangent footprint scale' },
    @{ Text=(Get-Content (Join-Path $root 'XRViewLab.UI\OverlayPreviewModels.cs') -Raw); Pattern='TangentReferencePixelsUniform\(area, 128\.0\)'; Name='HUD preview uses native tangent unit' },
    @{ Text=$preview; Pattern='RuntimeCropRect\(area, CropHorizontal, _cropTopScale, _cropBottomScale\)'; Name='crop guide uses the shared half-lens split state' },
    @{ Text=$geometry; Pattern='double top = 0\.5 \* \(1\.0 - topScale\)'; Name='crop guide remains geometrically centred (top half)' },
    @{ Text=$geometry; Pattern='double bottom = 0\.5 \+ 0\.5 \* bottomScale'; Name='crop guide remains geometrically centred (bottom half)' },
    @{ Text=$profile; Pattern='canonical feature settings win when both exist'; Name='per-app preview matches native overlay override precedence' },
    @{ Text=$profile; Pattern='definition\.XKey[\s\S]*definition\.YKey[\s\S]*definition\.ScaleKey'; Name='per-app preview resolves canonical position and scale' },
    @{ Text=$profile; Pattern='_overlayOverrides\.Set\(e\.Id, definition\.XKey[\s\S]*_overlayOverrides\.Set\(e\.Id, definition\.YKey'; Name='profile drag keeps canonical runtime position synchronized' },
    @{ Text=$preview; Pattern='SetCropVertical\(double top, double bottom\)'; Name='split crop preview state' },
    @{ Text=$geometry; Pattern='0\.5 \* \(1\.0 - topScale\)'; Name='top split control maps to the top half' },
    @{ Text=$geometry; Pattern='0\.5 \+ 0\.5 \* bottomScale'; Name='bottom split control maps to the bottom half' },
    @{ Text=$main; Pattern='"iRACING TELEMETRY"[\s\S]*OverlayPreviewAnchor\.RenderEdge'; Name='iRacing preview uses the post-crop render boundary' },
    @{ Text=$preview; Pattern='Anchor==OverlayPreviewAnchor\.RenderEdge[\s\S]*PreviewCropRect\(area\)'; Name='render-edge preview geometry follows current crop' },
    @{ Text=$main; Pattern='"OBS RECORDING CUE"[\s\S]*OverlayPreviewAnchor\.RecordingRenderEdge'; Name='main OBS cue preview uses its post-crop anchor' },
    @{ Text=$profile; Pattern='"OBS RECORDING CUE"[\s\S]*OverlayPreviewAnchor\.RecordingRenderEdge'; Name='profile OBS cue preview uses its post-crop anchor' },
    @{ Text=$preview; Pattern='Anchor==OverlayPreviewAnchor\.RecordingRenderEdge[\s\S]*OverlayPreviewGeometry\(item,crop'; Name='OBS cue border uses exact crop bounds' },
    @{ Text=$preview; Pattern='RecordingRenderEdge[\s\S]*rect\.Bottom-15/_viewZoom'; Name='OBS cue label is bottom-left' },
    @{ Text=$main; Pattern='ReadScaleSetting\("top_tangent", num \* 0\.5\) \* 2\.0'; Name='stored top share converts to half-relative UI value' },
    @{ Text=$main; Pattern='FormatStorageScale\(value2 \* 0\.5\)'; Name='half-relative top UI value converts back to stored full-lens share' },
    @{ Text=$profile; Pattern='TopValue = value \* 0\.5'; Name='profile top control converts back to stored full-lens share' },
    @{ Text=$native; Pattern='originalRightTan - \(originalRightTan - originalLeftTan\) \* horizontalScale'; Name='exact left outer crop' },
    @{ Text=$native; Pattern='mask_nose_spread_x'; Name='native Nose Spread X persistence' },
    @{ Text=$main; Pattern='MaskNoseSpreadXSlider\.Value'; Name='UI Nose Spread X plumbing' }
)) {
    if ($contract.Text -notmatch $contract.Pattern) { throw "Missing contract: $($contract.Name)" }
}

if ($preview -match 'ResolveRenderArea|ApplyRenderAreaDrag') { throw 'ordinary widget placement regressed to crop-relative coordinates' }
$circleBounds = [regex]::Matches($geometry, 'new Rect\([^\r\n]+diameter, diameter\)')
if ($circleBounds.Count -ne 2) { throw "Expected two square eye-guide bounds, found $($circleBounds.Count)" }
foreach ($contract in @(
    @{ Text=$geometry; Pattern='FullBinocularOval'; Name='binocular oval guide geometry' },
    @{ Text=$geometry; Pattern='FullEyeFrames'; Name='overlapping per-eye frame geometry' },
    @{ Text=$preview; Pattern='if \(UseCircularEyeGuides\)'; Name='oval/circle guide mode switch' },
    @{ Text=$preview; Pattern='if \(UsePerEyeFrameGuides\)'; Name='combined/per-eye frame mode switch' },
    @{ Text=$main; Pattern='PreviewCircleGuidesKey = "preview_circle_guides"'; Name='persisted preview guide preference' },
    @{ Text=$main; Pattern='PreviewPerEyeFramesKey = "preview_per_eye_frames"'; Name='persisted frame guide preference' },
    @{ Text=$main; Pattern='PreviewIpdKey = "preview_ipd_mm"'; Name='persisted preview IPD preference' },
    @{ Text=$main; Pattern='PreviewOpticalCentreKey = "preview_optical_centre"'; Name='persisted optical-centre preview preference' },
    @{ Text=$main; Pattern='PreviewOpticalCentreCheck\.IsChecked = ReadBoolSetting\(PreviewOpticalCentreKey, false\)'; Name='geometric-centre default' },
    @{ Text=$main; Pattern='StepPreviewIpd\(e\.Key == Key\.Up \? 0\.1 : -0\.1\)'; Name='preview IPD 0.1 keyboard steps' },
    @{ Text=$main; Pattern='PreviewIpdUp_Click[^\r\n]+StepPreviewIpd\(0\.1\)'; Name='main visible IPD increment action' },
    @{ Text=$main; Pattern='PreviewIpdDown_Click[^\r\n]+StepPreviewIpd\(-0\.1\)'; Name='main visible IPD decrement action' },
    @{ Text=$profile; Pattern='MaskBeanEditor\.PreviewIpdMillimetres = _previewIpdMillimetres'; Name='profile preview consumes the shared IPD calibration' },
    @{ Text=$profile; Pattern='MaskBeanEditor\.UseCircularEyeGuides = _useCircularEyeGuides'; Name='profile preview consumes the shared eye-guide mode' },
    @{ Text=$profile; Pattern='MaskBeanEditor\.UsePerEyeFrameGuides = _usePerEyeFrameGuides'; Name='profile preview consumes the shared frame-guide mode' },
    @{ Text=$profile; Pattern='MaskBeanEditor\.UseOpticalPreviewCentre = _useOpticalPreviewCentre'; Name='profile preview consumes the shared centre mode' },
    @{ Text=$preview; Pattern='_useOpticalPreviewCentre[\s\S]*FitAreaAtCentre\(RenderSize, Quest3PreviewGeometry\.OpticalPreviewCentreY\)[\s\S]*FitArea\(RenderSize\)'; Name='one complete preview area switches centres together' },
    @{ Text=$geometry; Pattern='calibratedSeparation \* ipd / DefaultIpdMillimetres'; Name='IPD changes guide separation only' },
    @{ Text=(Get-Content (Join-Path $root 'MainWindow.xaml') -Raw); Pattern='Name="PreviewCircleGuidesCheck"'; Name='main preview guide toggle' },
    @{ Text=(Get-Content (Join-Path $root 'MainWindow.xaml') -Raw); Pattern='Name="PreviewPerEyeFramesCheck"'; Name='main frame guide toggle' },
    @{ Text=(Get-Content (Join-Path $root 'MainWindow.xaml') -Raw); Pattern='Name="PreviewOpticalCentreCheck"[^>]+preview only'; Name='main optical-centre preview-only toggle' },
    @{ Text=(Get-Content (Join-Path $root 'MainWindow.xaml') -Raw); Pattern='Name="PreviewIpdBox"[^>]+Text="67\.0"'; Name='main preview IPD input' },
    @{ Text=(Get-Content (Join-Path $root 'MainWindow.xaml') -Raw); Pattern='<RepeatButton[^>]+Click="PreviewIpdUp_Click"'; Name='main visible IPD up arrow' },
    @{ Text=(Get-Content (Join-Path $root 'MainWindow.xaml') -Raw); Pattern='<RepeatButton[^>]+Click="PreviewIpdDown_Click"'; Name='main visible IPD down arrow' },
    @{ Text=$profile; Pattern='_previewIpdMillimetres = Math\.Round\(Math\.Clamp\(previewIpdMillimetres'; Name='profile preview clamps the shared IPD calibration' }
)) {
    if ($contract.Text -notmatch $contract.Pattern) { throw "Missing contract: $($contract.Name)" }
}

# The calibration-map layers are deliberately ordered: crop coverage, full-frame reference,
# periphery guide, final visor, then desktop overlay replicas.
$layerTokens = @('dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(34', 'dc.DrawRectangle(null,fullReferencePen,area)',
    'if (UseCircularEyeGuides)', 'dc.DrawGeometry(null', 'DrawOverlayPreviews(dc, area)')
$last = -1
foreach ($token in $layerTokens) {
    $next = $preview.IndexOf($token, [StringComparison]::Ordinal)
    if ($next -le $last) { throw "Preview calibration layer order changed at: $token" }
    $last = $next
}

Write-Output 'Quest 3 preview/runtime coordinates, exact inverse dragging, restart persistence, split crop, profiles and crosshair contracts passed.'
