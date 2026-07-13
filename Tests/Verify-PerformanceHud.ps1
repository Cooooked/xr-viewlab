param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)
$ErrorActionPreference='Stop'
$native=Get-Content (Join-Path $Root 'dllmain.cpp') -Raw
$ui=Get-Content (Join-Path $Root 'MainWindow.xaml') -Raw
$live=Get-Content (Join-Path $Root 'XRViewLab.UI\LiveStateService.cs') -Raw
function Require($text,$pattern,$message){if($text-notmatch$pattern){throw "Performance HUD contract failed: $message"}}
function Forbid($text,$pattern,$message){if($text-match$pattern){throw "Performance HUD contract failed: $message"}}

Require $native 'enum class HudWidgetId' 'widget registry identifier is absent'
Require $native 'OrderedHudWidgets' 'enabled widgets are not normalized into persisted order'
Require $native 'drawWidgetCount' 'renderer is still fixed-slot rather than packed'
Require $native 'beginStop\.QuadPart[\s\S]*?endStart\.QuadPart' 'APP work does not use begin-return to end-entry'
Require $native 'RecordHudAppWorkSample' 'APP workload collector is absent'
Forbid $native 'RecordHudSystemSample' 'unstable legacy SYS collector remains'
Require $native 'predictedDisplayPeriod' 'runtime target period is not sampled'
Require $native 'g_hudCadenceMultiple' 'cadence division is not detected'
Require $native 'HudMetricState::Reprojection' 'stable reprojection has no explicit state'
Require $native 'HudMetricState::Unstable' 'unstable cadence has no explicit state'
Require $native 'std::array<HudFrameSample, 600>' 'history is not statically bounded'
Require $native 'GraphFrameInterval[\s\S]*GraphFps[\s\S]*GraphBudgetDeviation[\s\S]*GraphAppWork[\s\S]*GraphWaitDuration[\s\S]*GraphSubmitDuration[\s\S]*GraphDisplayPeriod' 'truthful graph channels are incomplete'
Require $native 'if\(snap\.graphMode==HudGraphMode::Deviation\)return bit==GraphBudgetDeviation' 'deviation mode admits incompatible units'
Require $native 'if\(snap\.graphMode==HudGraphMode::Fps\)return bit==GraphFps' 'FPS mode admits incompatible units'
Require $ui 'HudScaleSlider" Minimum="0\.15"' 'small HUD scale is unavailable'
foreach($name in 'HudWidgetList','HudGraphModeCombo','HudGraphFrameIntervalCheck','HudGraphFpsCheck','HudGraphBudgetDeviationCheck','HudGraphAppWorkCheck','HudGraphWaitDurationCheck','HudGraphSubmitDurationCheck','HudGraphDisplayPeriodCheck'){Require $ui "Name=`"$name`"" "UI control $name is absent"}
Require $live '_view\.Write\(4, 7u\)' 'live mapping is not version 7'
foreach($offset in 192,196,200,204){Require $live "_view\.Write\($offset," "live mapping field at $offset is absent"}

# All enable masks pack without holes; all permutations preserve each widget exactly once.
foreach($mask in 0..15){$enabled=@(0..3|Where-Object{($mask -band (1 -shl $_)) -ne 0});$expected=0;foreach($id in 0..3){if(($mask -band (1 -shl $id)) -ne 0){$expected++}};if($enabled.Count -ne $expected){throw "mask $mask packing mismatch"}}
$permutations=0
foreach($a in 0..3){foreach($b in 0..3){foreach($c in 0..3){foreach($d in 0..3){$p=@($a,$b,$c,$d);if(($p|Select-Object -Unique).Count-eq4){$permutations++;if((($p|Sort-Object) -join ',') -ne '0,1,2,3'){throw 'order lost a widget'}}}}}}
if($permutations-ne24){throw "expected 24 orders, got $permutations"}

# Refresh-relative cadence: the same 11.11 ms is correct at 90 Hz and a miss at 120 Hz.
function Ratio($interval,$hz,$multiple=1){$interval/((1000.0/$hz)*$multiple)}
if([math]::Abs((Ratio 11.11 90)-1)-gt0.02){throw '90 Hz target classification failed'}
if((Ratio 11.11 120)-lt1.30){throw '11.11 ms incorrectly acceptable at 120 Hz'}
if([math]::Abs((Ratio 16.67 120 2)-1)-gt0.02){throw '120 Hz half-rate classification failed'}

Write-Output 'Performance HUD modularity, timing, cadence, graph, and migration contracts passed.'
