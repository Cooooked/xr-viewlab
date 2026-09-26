# Compiles Shaders\ColourGrade.hlsl into byte-code headers included by dllmain.cpp. Run after editing the HLSL
# (build.ps1 runs it too). Uses fxc from the Windows SDK found via the KitsRoot10 registry value.
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$kits = (Get-ItemProperty 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots' -ErrorAction SilentlyContinue).KitsRoot10
if (-not $kits) { $kits = "${env:ProgramFiles(x86)}\Windows Kits\10" }
$fxc = Get-ChildItem (Join-Path $kits 'bin') -Directory | Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending | ForEach-Object { Join-Path $_.FullName 'x64\fxc.exe' } |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $fxc) { throw 'fxc.exe not found in the Windows SDK' }
$src = Join-Path $here 'ColourGrade.hlsl'
foreach ($e in @(
    @{ Entry = 'VSMain'; Profile = 'vs_4_0'; Var = 'g_ColourGradeVS' },
    @{ Entry = 'PSMath'; Profile = 'ps_4_0'; Var = 'g_ColourGradePSMath' },
    @{ Entry = 'PSLut'; Profile = 'ps_4_0'; Var = 'g_ColourGradePSLut' },
    @{ Entry = 'PSBake'; Profile = 'ps_4_0'; Var = 'g_ColourGradePSBake' })) {
    $out = Join-Path $here ("ColourGrade_{0}.h" -f $e.Entry)
    & $fxc /nologo /O3 /T $e.Profile /E $e.Entry /Vn $e.Var /Fh $out $src | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "fxc failed for $($e.Entry)" }
}
Write-Host 'Colour grade shaders compiled.'
