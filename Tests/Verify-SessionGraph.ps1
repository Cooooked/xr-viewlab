param([string]$Root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path)
$ErrorActionPreference='Stop'
$native=Get-Content (Join-Path $Root 'dllmain.cpp') -Raw
$library=Get-Content (Join-Path $Root 'XRViewLab.UI\PerformanceTraceLibraryWindow.xaml.cs') -Raw
$libraryXaml=Get-Content (Join-Path $Root 'XRViewLab.UI\PerformanceTraceLibraryWindow.xaml') -Raw
$diag=Get-Content (Join-Path $Root 'XRViewLab.UI\DiagMonWindow.xaml.cs') -Raw
function Need($text,$pattern,$message){if($text-notmatch$pattern){throw "Session Graph contract failed: $message"}}
Need $native 'session-%04u%02u%02u-' 'native trace sessions are not archived uniquely'
Need $native 'CreateHardLinkW\(latest\.c_str\(\),path\.c_str\(\)' 'latest.csv compatibility alias is absent'
Need $native 'g_latestTraceIsHardLink' 'copy fallback cannot remain current after append'
Need $library 'Directory\.EnumerateFiles\(_directory,"session-\*\.csv"\)' 'history browser does not enumerate archived sessions'
Need $libraryXaml 'SelectionMode="Extended"' 'multi-session selection is absent'
Need $libraryXaml 'Compare selected' 'user-selected comparison is absent'
Need $libraryXaml 'Delete selected' 'explicit history clearing is absent'
Need $diag 'new PerformanceTraceLibraryWindow' 'Session Graph is not owned by DiagMonster'
Write-Output 'Session Graph archival, browsing, comparison, compatibility alias and explicit deletion contracts passed.'
