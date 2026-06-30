param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$DistDir = ""
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($DistDir)) {
    $DistDir = Join-Path $Root "dist"
}
$DotnetProject = Join-Path $Root "xr-viewlab.csproj"
$LayerProject = Join-Path $Root "XRViewLabLayer.vcxproj"
$InstallerProject = Join-Path $Root "Installer\Installer.wixproj"
$MsiSource = Join-Path $Root "Installer\bin\$Configuration\xr-viewlab-setup.msi"

# --- Auto-increment version (major.minor.build) ---
$assemblyInfo = Join-Path $Root "Properties\AssemblyInfo.cs"
$productWxs   = Join-Path $Root "Installer\Product.wxs"

$currentVersion = Select-String -Path $assemblyInfo -Pattern 'AssemblyInformationalVersion\("(\d+)\.(\d+)\.(\d+)"\)' | Select-Object -First 1
if ($currentVersion -and $currentVersion.Matches[0].Groups.Count -ge 4) {
    $major = $currentVersion.Matches[0].Groups[1].Value
    $minor = $currentVersion.Matches[0].Groups[2].Value
    $build = [int]$currentVersion.Matches[0].Groups[3].Value + 1
    $newVersion = "$major.$minor.$build"
    $newVersion4 = "$newVersion.0"

    Write-Host "Bumping version to $newVersion"
    (Get-Content $assemblyInfo) -replace "$major\.\d+\.\d+\.0", $newVersion4 -replace "$major\.\d+\.\d+", $newVersion | Set-Content $assemblyInfo -Encoding UTF8
    (Get-Content $productWxs)   -replace "Version=`"$major\.\d+\.\d+`"", "Version=`"$newVersion`"" | Set-Content $productWxs -Encoding UTF8
}

# --- Build ---
Write-Host "ViewLab build root: $Root"

function Find-MSBuild {
    $vswhereCandidates = @(
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\Installer\vswhere.exe"
    )
    foreach ($vswhere in $vswhereCandidates) {
        if (Test-Path $vswhere) {
            $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
            if ($path -and (Test-Path $path)) {
                return $path
            }
        }
    }

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

Write-Host "Building WPF app..."
dotnet publish $DotnetProject -c $Configuration -r win-x64 --self-contained true /p:PublishSingleFile=true

$MSBuild = Find-MSBuild
Write-Host "Using MSBuild: $MSBuild"

Write-Host "Building OpenXR API layer (x64)..."
& $MSBuild $LayerProject /p:Configuration=$Configuration /p:Platform=x64 /m
Write-Host "Building OpenXR API layer (Win32 / 32-bit for 32-bit games)..."
& $MSBuild $LayerProject /p:Configuration=$Configuration /p:Platform=Win32 /m

Write-Host "Building MSI..."
$WixObjDir = Join-Path $Root "Installer\obj\$Configuration"
if (Test-Path $WixObjDir) { Remove-Item -Recurse -Force $WixObjDir }
& $MSBuild $InstallerProject /p:Configuration=$Configuration /p:Platform=$Platform /m

if (!(Test-Path $MsiSource)) {
    throw "MSI was not produced at $MsiSource"
}

New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
$versionLine = Select-String -Path $assemblyInfo -Pattern 'AssemblyInformationalVersion\("([^"]+)"\)' | Select-Object -First 1
$version = if ($versionLine -and $versionLine.Matches.Count -gt 0) { $versionLine.Matches[0].Groups[1].Value } else { "unknown" }
$MsiDest = Join-Path $DistDir "ViewLab-$version.msi"
Copy-Item -Path $MsiSource -Destination $MsiDest -Force

Write-Host "Built MSI:"
Write-Host $MsiDest
