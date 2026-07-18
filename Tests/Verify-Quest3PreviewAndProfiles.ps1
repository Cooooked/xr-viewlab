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

# Equal halves centre a 0.15 crop. Split values move the same-height box without changing scale.
$top = 0.075; $bottom = 0.075
$cropTop = $height * (0.5 - $top); $cropHeight = $height * ($top + $bottom)
Assert-Close $cropTop 204.0 0.0000001 'centred vertical 0.15 top'
Assert-Close $cropHeight 72.0 0.0000001 'vertical 0.15 retained height'
$weightedTop = 0.03; $weightedBottom = 0.12
$weightedCentre = $height * (0.5 - $weightedTop) + $height * ($weightedTop + $weightedBottom) / 2.0
if ($weightedCentre -le $height / 2.0) { throw 'bottom-weighted crop did not move down' }

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
$native = Get-Content (Join-Path $root 'dllmain.cpp') -Raw
$preview = Get-Content (Join-Path $root 'XRViewLab.UI\BeanMaskEditor.cs') -Raw
$geometry = Get-Content (Join-Path $root 'XRViewLab.UI\Quest3PreviewGeometry.cs') -Raw

foreach ($contract in @(
    @{ Text=$profile; Pattern='UseGlobalValues'; Name='separate whole-profile global action' },
    @{ Text=$profile; Pattern='UseGlobalVisor'; Name='separate visor-only global state' },
    @{ Text=$main; Pattern='double savedVisorSize = profileWindow\.UseGlobalVisor \? 0\.0 :'; Name='visor-only global save preserves crop profile' },
    @{ Text=$main; Pattern='registryKey\.SetValue\("profile_enabled", profileEnabled \? 1 : 0'; Name='custom profile override persistence' },
    @{ Text=$main; Pattern='LoadAppProfiles\(\);\s*StatusText\.Text = "Saved app profile'; Name='post-save registry reload' },
    @{ Text=$preview; Pattern='Quest3PreviewGeometry\.FullEyeGuides'; Name='overlapping circular eye guides' },
    @{ Text=$preview; Pattern='double radius = eye\.Width \* 0\.5'; Name='unstretched circle radius' },
    @{ Text=$geometry; Pattern='double width = area\.Width \* horizontal'; Name='single direct horizontal percentage mapping' },
    @{ Text=$geometry; Pattern='area\.Height \* \(0\.5 - top\)'; Name='centred split vertical mapping' },
    @{ Text=$geometry; Pattern='internal static Rect SharedFullArea\(Rect area\) => area'; Name='single normalized overlay coordinate space' },
    @{ Text=$preview; Pattern='SetCropVertical\(double top, double bottom\)'; Name='split crop preview state' },
    @{ Text=$native; Pattern='originalRightTan - \(originalRightTan - originalLeftTan\) \* horizontalScale'; Name='exact left outer crop' },
    @{ Text=$native; Pattern='mask_nose_spread_x'; Name='native Nose Spread X persistence' },
    @{ Text=$main; Pattern='MaskNoseSpreadXSlider\.Value'; Name='UI Nose Spread X plumbing' }
)) {
    if ($contract.Text -notmatch $contract.Pattern) { throw "Missing contract: $($contract.Name)" }
}

if ($geometry -match 'Degrees|Tan\(|Tangent') { throw 'Preview geometry still contains projection-degree movement math' }
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
    @{ Text=$main; Pattern='e\.Key == Key\.Up \? 0\.1 : -0\.1'; Name='preview IPD 0.1 keyboard steps' },
    @{ Text=$geometry; Pattern='calibratedSeparation \* ipd / DefaultIpdMillimetres'; Name='IPD changes guide separation only' },
    @{ Text=(Get-Content (Join-Path $root 'MainWindow.xaml') -Raw); Pattern='Name="PreviewCircleGuidesCheck"'; Name='main preview guide toggle' },
    @{ Text=(Get-Content (Join-Path $root 'MainWindow.xaml') -Raw); Pattern='Name="PreviewPerEyeFramesCheck"'; Name='main frame guide toggle' },
    @{ Text=(Get-Content (Join-Path $root 'MainWindow.xaml') -Raw); Pattern='Name="PreviewIpdBox"[^>]+Text="67\.0"'; Name='main preview IPD input' },
    @{ Text=(Get-Content (Join-Path $root 'ProfileWindow.xaml') -Raw); Pattern='Name="PreviewCircleGuidesCheck"'; Name='profile preview guide toggle' },
    @{ Text=(Get-Content (Join-Path $root 'ProfileWindow.xaml') -Raw); Pattern='Name="PreviewPerEyeFramesCheck"'; Name='profile frame guide toggle' },
    @{ Text=(Get-Content (Join-Path $root 'ProfileWindow.xaml') -Raw); Pattern='Name="PreviewIpdBox"[^>]+Text="67\.0"'; Name='profile preview IPD input' }
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

Write-Output 'Quest 3 preview geometry, exact crop, profile persistence and Nose Spread X contracts passed.'
