param([string]$Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path)
$ErrorActionPreference='Stop'
$model=Get-Content (Join-Path $Root 'XRViewLab.UI\OverlaySettingsModels.cs') -Raw
$ui=Get-Content (Join-Path $Root 'XRViewLab.UI\MainWindow.cs') -Raw
$xaml=Get-Content (Join-Path $Root 'MainWindow.xaml') -Raw
$native=Get-Content (Join-Path $Root 'dllmain.cpp') -Raw
$ini=Get-Content (Join-Path $Root 'xr-viewlab.ini') -Raw
function Need($text,$pattern,$message){if($text-notmatch$pattern){throw "Overlay settings contract failed: $message"}}
function Forbid($text,$pattern,$message){if($text-match$pattern){throw "Overlay settings contract failed: $message"}}

foreach($id in 'clock','hud','trace','sticky','crosshair','notifications'){Need $model "\[`"$id`"\]" "catalogue entry $id is absent"}
foreach($control in 'ClockWidgetToggleKeyCombo','HudToggleKeyCombo','HudTraceToggleKeyCombo','StickyNoteToggleKeyCombo','CrosshairToggleKeyCombo','NotifyToggleKeyCombo','HudOpacitySlider','HudTraceOpacitySlider') { Need $xaml "Name=`"$control`"" "control $control is absent" }
foreach($tag in 'clock','hud','trace','crosshair','notifications'){Need $xaml "Tag=`"$tag`"[^>]+OverlayResetPosition_Click" "reset-position control $tag is absent"}
Need $xaml 'Click="StickyNoteReset_Click"' 'per-note reset-position control is absent'
Need $ui 'LoadCommonOverlaySettings\(\)' 'common load path is absent'
Need $ui 'SaveCommonOverlaySettings\(string id\)' 'common save path is absent'
Need $ui 'LegacyToggleKey' 'legacy hotkey migration is absent'
Forbid $ui 'SaveClockWidgetSettings|CrosshairPosition_Reset' 'superseded feature-private common handler remains'
foreach($key in 'overlay_hud_toggle_vk','overlay_trace_toggle_vk','overlay_clock_toggle_vk','overlay_sticky_note_toggle_vk','overlay_crosshair_toggle_vk','overlay_notifications_toggle_vk'){Need $native $key "native key $key is absent";Need $ini "(?m)^$key=" "default $key is absent"}
Need $native 'hud_trace_opacity' 'independent trace opacity is absent'
Need $ini '(?m)^hud_trace_opacity=' 'trace opacity default is absent'
foreach($control in 'ClockSessionTimerCheck','Clock24HourCheck','ClockThemeCombo','NotifyThemeCombo','StickyNotesList'){Need $xaml "Name=`"$control`"" "polish control $control is absent"}
Need $xaml 'IsChecked="\{Binding UseSymbol' 'per-widget symbol selection is absent'
Need $ui 'hud_widget_\{widget.Id\}_symbol' 'per-widget symbol persistence is absent'
Need $native 'hudWidgetSymbolMask=stable.flags' 'per-widget symbol mode is not live'
Need $native 'clockSessionTimerEnabled=\(stable.clockFlags' 'clock timer lane is not live'
Need $native 'snapshot\.version != 8' 'live-state v8 is not consumed'
Need $ui 'StickyNoteLiveStateService' 'sticky collection live publisher is absent'
Need $native 'Local\\\\XRViewLabStickyNotes' 'native sticky collection consumer is absent'
Need $ini '(?m)^notify_theme=0\r?$' 'notification theme default is absent'
Need $ui 'RefreshMaskOverlayPreview' 'visor preview does not receive the shared overlay catalogue state'
$preview=Get-Content (Join-Path $Root 'XRViewLab.UI\BeanMaskEditor.cs') -Raw
$previewModel=Get-Content (Join-Path $Root 'XRViewLab.UI\OverlayPreviewModels.cs') -Raw
Need $preview 'DrawOverlayPreviews' 'visor preview does not draw overlay placement indicators'
Need $preview '8/_viewZoom' 'preview labels do not retain a readable screen-space size while zooming'
Need $preview 'OverlayPreviewHandle\.Move' 'preview nodes have no shared move handle'
Need $preview 'OverlayPreviewHandle\.Scale' 'preview nodes have no shared scale handle'
Need $preview 'OverlayPreviewChanged\?\.Invoke' 'preview edits are not emitted through one generic placement event'
Need $ui 'MaskBeanEditor_OverlayPreviewChanged' 'preview edits are not connected to the common settings path'
Need $ui 'SaveCommonOverlaySettings\(e\.Id\);PublishLiveState\(\)' 'preview placement does not persist and publish through the common overlay path'
Need $previewModel 'OverlayPreviewReplicaLayout' 'preview replicas have no central footprint resolver'
Need $previewModel 'Quest3PreviewGeometry\.ReferencePixelsToX' 'preview width does not use the shared normalized reference scale'
Need $previewModel 'Quest3PreviewGeometry\.ReferencePixelsToY' 'preview height does not use the shared normalized reference scale'
Need $previewModel 'return new Size\(width \* fit, height \* fit\)' 'preview bounds fitting does not preserve aspect ratio'
Need $preview 'QUEST 3 \{PreviewIpdMillimetres:0\.0\} mm  FULL BINOCULAR' 'full binocular crop reference is absent'
Need $preview 'DashStyle=new DashStyle' 'full binocular crop reference is not dotted'
Need $preview 'DrawOverlayPreviews\(dc, area\)' 'overlay preview placement still inherits the crop rectangle'
Need $preview 'DrawCrosshair\(dc, area\)' 'crosshair preview placement still inherits the crop rectangle'
Need $preview 'cropSupportsMaskGeometry' 'an extreme crop can still abort the complete overlay preview'
Need $preview 'SetVisorVisible' 'preview visor outline is not bound to feature visibility'
Need $preview '_visorVisible\?Color\.FromRgb\(224,42,53\):Color\.FromArgb' 'disabled visor preview does not fade to grey'
Need $preview 'DrawEyeGuide\(dc, fullEyes\.Left' 'left-eye visibility guide is absent'
Need $preview 'DrawEyeGuide\(dc, fullEyes\.Right' 'right-eye visibility guide is absent'
Need $preview 'FullBinocularOval' 'binocular oval guide mode is absent'
Need $xaml 'Name="PreviewCircleGuidesCheck"' 'preview oval/circle toggle is absent'
Need $xaml 'Name="PreviewPerEyeFramesCheck"' 'combined/per-eye frame toggle is absent'
Need $xaml 'Name="PreviewIpdBox"' 'preview IPD input is absent'
Need $xaml 'Name="ClockWidgetScaleSlider" Minimum="0\.1" Maximum="2" Value="1"' 'Clock + Timer minimum/default/maximum scale contract changed incorrectly'
Need $xaml 'Name="NotifyScaleSlider" Minimum="0\.1" Maximum="3" Value="1"' 'notification minimum/default/maximum scale contract changed incorrectly'
Need $ui 'ClockWidgetScaleSlider\.Minimum,ClockWidgetScaleSlider\.Maximum' 'Clock + Timer preview pin does not share slider scale bounds'
Need $ui 'NotifyScaleSlider\.Minimum,NotifyScaleSlider\.Maximum' 'notification preview pin does not share slider scale bounds'
Need $native 'stable\.notifyScale, 0\.1, 3\.0' 'notification live minimum scale differs from UI'
Need $native 'stable\.clockScale,\.1,2\.0' 'Clock + Timer live minimum scale differs from UI'
Need $native 'ReadDoubleSetting\(L"notify_scale", 1\.0\), 0\.1, 3\.0' 'notification startup minimum scale differs from UI'
Need $native 'ReadDoubleSetting\(L"clock_widget_scale", 1\.0\), 0\.10, 2\.0' 'Clock + Timer startup minimum scale differs from UI'
Need $xaml 'Name="HudScaleSlider" Minimum="0\.15" Maximum="3" Value="1"' 'HUD scale bounds changed outside scope'
Need $xaml 'Name="HudTraceScaleSlider" Minimum="0\.25" Maximum="3" Value="1"' 'Trace scale bounds changed outside scope'
Need $ui 'MaskBeanEditor\.SetVisorVisible\(MaskEnabledCheck\?\.IsChecked==true\)' 'visor enable state is not forwarded to the preview'
Need $native 'traceAvailableW \* traceScale' 'Performance Trace scale does not scale width with height'
Need $native 'traceUnit = std::clamp\(angularRefPx \* 64\.f' 'Performance Trace height remains coupled to HUD scale'
Need $native 'anchorPxX = \[&\]\(double frac\) \{ return coordinates\.Resolve\(OverlayPlacement::FullLens' 'HUD/trace anchors still use cropped render coordinates'
Need $native 'anchorX=\[&\]\(double f\)\{ return coordinates\.Resolve\(OverlayPlacement::FullLens' 'ordinary overlay anchors still use cropped render coordinates'
Need $native 'fullMinDim.*obR-obL,obB-obT' 'HUD scale limits still derive from cropped dimensions'
Need $native 'ResolveSharedTangent\(crosshairTanX,crosshairTanY,false\)' 'crosshair position is still clamped into the crop'
Need $native '\(fullRight-fullLeft\)\*\.6f' 'notification size cap still derives from crop width'
Forbid $xaml 'Name="MaskInnerBridge(Slider|RiseSlider|PeakXSlider|SteepnessSlider)"' 'removed notch-detail sliders remain in the main UI'
Write-Output 'Shared overlay configuration, migration, hotkey and persistence contracts passed.'
