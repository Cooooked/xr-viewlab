param(
    [string]$RepositoryRoot = 'F:\AI-Projects\ViewLab',
    [string]$ObsRoot = 'C:\Program Files\obs-studio'
)

$ErrorActionPreference = 'Stop'

if (Get-Process -Name obs64 -ErrorAction SilentlyContinue) {
    throw 'OBS Studio is running. Close it before installing ViewLab OBS plugins.'
}

$repositoryRootPath = [IO.Path]::GetFullPath($RepositoryRoot)
$obsRootPath = [IO.Path]::GetFullPath($ObsRoot)
$obsBinaryPath = [IO.Path]::GetFullPath((Join-Path $obsRootPath 'obs-plugins\64bit'))
$stabilizerDataPath = [IO.Path]::GetFullPath((Join-Path $obsRootPath 'data\obs-plugins\viewlab-stabilizer'))

if (-not $obsBinaryPath.StartsWith($obsRootPath, [StringComparison]::OrdinalIgnoreCase) -or
    -not $stabilizerDataPath.StartsWith($obsRootPath, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Refusing to install outside the selected OBS Studio directory.'
}

$enhancerSource = Join-Path $repositoryRootPath 'ViewLabEnhancerFilter\x64\Release\viewlab-enhancer.dll'
$stabilizerSource = Join-Path $repositoryRootPath 'ViewLabStabilizerFilter\x64\Release\viewlab-stabilizer.dll'
$openCvSource = Join-Path $repositoryRootPath 'ViewLabStabilizerFilter\deps\opencv\bin\opencv_world470.dll'
$effectSourcePath = Join-Path $repositoryRootPath 'ViewLabStabilizerFilter\upstream\LiveVisionKit\OBS\Data\effects'
$effectNames = @('fsr.effect', 'ffx_a_mod.h', 'ffx_fsr1_mod.h')

$requiredSources = @($enhancerSource, $stabilizerSource, $openCvSource)
$requiredSources += $effectNames | ForEach-Object { Join-Path $effectSourcePath $_ }
foreach ($sourcePath in $requiredSources) {
    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Required plugin file is missing: $sourcePath"
    }
}

$backupPath = Join-Path $repositoryRootPath ('backups\obs-pre-lvk-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
foreach ($fileName in @('viewlab-enhancer.dll', 'viewlab-stabilizer.dll')) {
    $installedPath = Join-Path $obsBinaryPath $fileName
    if (Test-Path -LiteralPath $installedPath -PathType Leaf) {
        Copy-Item -LiteralPath $installedPath -Destination (Join-Path $backupPath $fileName) -Force
    }
}

New-Item -ItemType Directory -Path (Join-Path $stabilizerDataPath 'effects') -Force | Out-Null
Copy-Item -LiteralPath $enhancerSource -Destination (Join-Path $obsBinaryPath 'viewlab-enhancer.dll') -Force
Copy-Item -LiteralPath $stabilizerSource -Destination (Join-Path $obsBinaryPath 'viewlab-stabilizer.dll') -Force
Copy-Item -LiteralPath $openCvSource -Destination (Join-Path $obsBinaryPath 'opencv_world470.dll') -Force
foreach ($effectName in $effectNames) {
    Copy-Item -LiteralPath (Join-Path $effectSourcePath $effectName) -Destination (Join-Path (Join-Path $stabilizerDataPath 'effects') $effectName) -Force
}

$resultPath = Join-Path $repositoryRootPath 'dist\obs-plugin-install-result.txt'
$hashes = Get-FileHash -Algorithm SHA256 -LiteralPath @(
    (Join-Path $obsBinaryPath 'viewlab-enhancer.dll'),
    (Join-Path $obsBinaryPath 'viewlab-stabilizer.dll'),
    (Join-Path $obsBinaryPath 'opencv_world470.dll')
)
$result = @(
    'VIEWLAB_OBS_PLUGIN_INSTALL_OK',
    "Backup=$backupPath"
) + ($hashes | ForEach-Object { "$($_.Path)=$($_.Hash)" })
[IO.File]::WriteAllLines($resultPath, $result)
