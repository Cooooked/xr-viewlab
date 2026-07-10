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

# Copy ReShadePayload to publish directory for development/testing
$PublishDir = Join-Path $Root "bin\$Configuration\net8.0-windows\win-x64\publish"
$PayloadSrc = Join-Path $Root "ReShadePayload"
$PayloadDest = Join-Path $PublishDir "ReShadePayload"
if (Test-Path $PayloadSrc) {
    Remove-Item -Path $PayloadDest -Recurse -Force -ErrorAction SilentlyContinue
    Copy-Item -Path $PayloadSrc -Destination $PayloadDest -Recurse -Force
}

# Copy the bundled default config to publish output. The MSI also installs the repo copy
# directly, but local publish-folder runs use this legacy migration source.
$DefaultConfigSrc = Join-Path $Root "xr-viewlab.ini"
if (Test-Path $DefaultConfigSrc) {
    Copy-Item -Path $DefaultConfigSrc -Destination (Join-Path $PublishDir "xr-viewlab.ini") -Force
}

# Create ViewLab directory for native DLLs used by publish-folder testing.
$ViewLabDir = Join-Path $PublishDir "ViewLab"
New-Item -ItemType Directory -Path $ViewLabDir -Force | Out-Null

$MSBuild = Find-MSBuild
Write-Host "Using MSBuild: $MSBuild"

Write-Host "Building OpenXR API layer (x64)..."
& $MSBuild $LayerProject /p:Configuration=$Configuration /p:Platform=x64 /m
Write-Host "Building OpenXR API layer (Win32 / 32-bit for 32-bit games)..."
& $MSBuild $LayerProject /p:Configuration=$Configuration /p:Platform=Win32 /m

# Copy native DLLs to distribution/release folder
$Dll64Src = Join-Path $Root "x64\$Configuration\XR_APILAYER_cooooked_xrviewlab.dll"
$Dll32Src = Join-Path $Root "$Configuration\XR_APILAYER_cooooked_xrviewlab32.dll"
$Dll32FallbackSrc = Join-Path $Root "Release\XR_APILAYER_cooooked_xrviewlab32.dll"
if (!(Test-Path $Dll32Src) -and $Configuration -eq "Release") {
    $Dll32Src = $Dll32FallbackSrc
}

if (Test-Path $Dll64Src) {
    Copy-Item -Path $Dll64Src -Destination "$ViewLabDir\" -Force
}
if (Test-Path $Dll32Src) {
    Copy-Item -Path $Dll32Src -Destination "$ViewLabDir\" -Force
}

$DllDest = Join-Path $DistDir "ViewLab-dlls"
if ((Test-Path $Dll64Src) -or (Test-Path $Dll32Src)) {
    New-Item -ItemType Directory -Path $DllDest -Force | Out-Null
    if (Test-Path $Dll64Src) {
        Copy-Item -Path $Dll64Src -Destination "$DllDest\" -Force
        Write-Host "Copied x64 DLL to dist"
    }
    if (Test-Path $Dll32Src) {
        Copy-Item -Path $Dll32Src -Destination "$DllDest\" -Force
        Write-Host "Copied Win32 DLL to dist"
    }
}

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
