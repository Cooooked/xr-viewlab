param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$DistDir = "F:\ViewLab\dist"
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$DotnetProject = Join-Path $Root "xr-viewlab.csproj"
$LayerProject = Join-Path $Root "XRViewLabLayer.vcxproj"
$InstallerProject = Join-Path $Root "Installer\Installer.wixproj"
$MsiSource = Join-Path $Root "Installer\bin\$Configuration\xr-viewlab-setup.msi"

function Find-MSBuild {
    $candidates = @(
        "D:\VSBuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path $candidate)) {
            return $candidate
        }
    }

    $cmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }

    throw "MSBuild.exe not found. Install Visual Studio Build Tools."
}

Write-Host "XR ViewLab build root: $Root"
Write-Host "Building WPF app..."
dotnet publish $DotnetProject -c $Configuration -r win-x64 --self-contained true /p:PublishSingleFile=true

$MSBuild = Find-MSBuild
Write-Host "Using MSBuild: $MSBuild"

Write-Host "Building OpenXR API layer..."
& $MSBuild $LayerProject /p:Configuration=$Configuration /p:Platform=$Platform /m

Write-Host "Building MSI..."
& $MSBuild $InstallerProject /p:Configuration=$Configuration /p:Platform=$Platform /m

if (!(Test-Path $MsiSource)) {
    throw "MSI was not produced at $MsiSource"
}

New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
$versionLine = Select-String -Path (Join-Path $Root "Properties\AssemblyInfo.cs") -Pattern 'AssemblyInformationalVersion\("([^"]+)"\)' | Select-Object -First 1
$version = if ($versionLine -and $versionLine.Matches.Count -gt 0) { $versionLine.Matches[0].Groups[1].Value } else { "unknown" }
$MsiDest = Join-Path $DistDir "XR-ViewLab-$version.msi"
Copy-Item -Path $MsiSource -Destination $MsiDest -Force

Write-Host "Built MSI:"
Write-Host $MsiDest
