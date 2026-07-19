$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$baselinePath = Join-Path $root 'config\factory-baseline-v4.1.255.json'
$baseline = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
if ($baseline.baselineVersion -ne '4.1.255' -or $baseline.migrationMarker -ne 'FactoryBaselineAppliedVersion') {
    throw 'Factory baseline version or migration marker changed unexpectedly.'
}

$iniValues = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $root 'xr-viewlab.ini')) {
    if ($line -match '^([^;#][^=]*)=(.*)$') { $iniValues[$matches[1].Trim()] = $matches[2] }
}
foreach ($property in $baseline.iniSettings.PSObject.Properties) {
    if (-not $iniValues.ContainsKey($property.Name)) { throw "Default INI is missing baseline key $($property.Name)." }
    $expected = [string]$property.Value; $actual = [string]$iniValues[$property.Name]
    $expectedNumber = 0.0; $actualNumber = 0.0
    $numeric = [double]::TryParse($expected, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$expectedNumber) -and
        [double]::TryParse($actual, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$actualNumber)
    if (($numeric -and [Math]::Abs($expectedNumber - $actualNumber) -gt 1e-12) -or (-not $numeric -and $expected -cne $actual)) {
        throw "Default INI differs from baseline for $($property.Name): expected '$expected', got '$actual'."
    }
}

$main = Get-Content -LiteralPath (Join-Path $root 'XRViewLab.UI\MainWindow.cs') -Raw
if ($main -notmatch 'FactoryBaseline\.MigrationMarker' -or $main -notmatch 'FactoryBaseline\.IniSettings') { throw 'Factory migration is not wired.' }
if ($main -match 'FactoryBaseline[\s\S]{0,800}Apps\\') { throw 'Factory migration must not touch the per-app registry subtree.' }
$service = Get-Content -LiteralPath (Join-Path $root 'XRViewLab.UI\ReShadeControlService.cs') -Raw
foreach ($key in @('xr_mode','menu_visible','win_headless','win_always_on_top')) {
    if ($service -notmatch [regex]::Escape('reshade_remote_' + $key)) { throw "ReShade preference $key is not persisted." }
}
Write-Host "Factory baseline verified: $(@($baseline.iniSettings.PSObject.Properties).Count) INI keys and four ReShade Remote preferences."
