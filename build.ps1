param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$DistDir = ""
)

$ErrorActionPreference = "Stop"
if ($Configuration -ne "Release") {
    throw "build.ps1 packages the MSI from Release paths; use -Configuration Release."
}

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($DistDir)) {
    $DistDir = Join-Path $Root "dist"
}
$DotnetProject = Join-Path $Root "xr-viewlab.csproj"
$LayerProject = Join-Path $Root "XRViewLabLayer.vcxproj"
$InstallerProject = Join-Path $Root "Installer\Installer.wixproj"
$MsiSource = Join-Path $Root "Installer\bin\$Configuration\xr-viewlab-setup.msi"
$TargetFramework = ([xml](Get-Content -Raw $DotnetProject)).Project.PropertyGroup.TargetFramework | Where-Object { $_ } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($TargetFramework)) {
    throw "TargetFramework was not found in $DotnetProject"
}
$PublishDir = Join-Path $Root "bin\$Configuration\$TargetFramework\win-x64\publish"
$ExpectedWixSource = "..\bin\$Configuration\$TargetFramework\win-x64\publish\xr-viewlab.exe"

# --- Auto-increment version (major.minor.build) ---
$assemblyInfo = Join-Path $Root "Properties\AssemblyInfo.cs"
$productWxs   = Join-Path $Root "Installer\Product.wxs"

$currentVersion = Select-String -Path $assemblyInfo -Pattern 'AssemblyInformationalVersion\("(\d+)\.(\d+)\.(\d+)(?:\.\d+)?(?:[-+][^"]*)?"\)' | Select-Object -First 1
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
$BuildStartedUtc = [DateTime]::UtcNow

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

function Invoke-Native {
    param(
        [Parameter(Mandatory=$true)][string]$FilePath,
        [Parameter(ValueFromRemainingArguments=$true)][string[]]$Arguments
    )
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE"
    }
}

Write-Host "Removing generated outputs that could otherwise satisfy packaging from a stale build..."
$GeneratedPaths = @(
    (Join-Path $Root "bin\$Configuration\$TargetFramework"),
    (Join-Path $Root "obj\$Configuration\$TargetFramework"),
    (Join-Path $Root "Installer\bin\$Configuration"),
    (Join-Path $Root "Installer\obj\$Configuration"),
    (Join-Path $Root "Installer\verify-build")
)
foreach ($GeneratedPath in $GeneratedPaths) {
    if (Test-Path $GeneratedPath) { Remove-Item -Recurse -Force $GeneratedPath }
}

$WixText = Get-Content -Raw $productWxs
if ($WixText -notmatch [regex]::Escape("Source=`"$ExpectedWixSource`"")) {
    throw "WiX SettingsApp source must match the project TargetFramework: $ExpectedWixSource"
}

Write-Host "Building WPF app..."
Invoke-Native dotnet publish $DotnetProject -c $Configuration -r win-x64 --self-contained true /p:PublishSingleFile=true

# Copy ReShadePayload to publish directory for development/testing
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
$Dll64Expected = Join-Path $Root "x64\$Configuration\XR_APILAYER_cooooked_xrviewlab.dll"
$Dll32Expected = Join-Path $Root "$Configuration\XR_APILAYER_cooooked_xrviewlab32.dll"
foreach ($NativeOutput in @($Dll64Expected, $Dll32Expected)) {
    if (Test-Path $NativeOutput) { Remove-Item -Force $NativeOutput }
}
Invoke-Native $MSBuild $LayerProject /p:Configuration=$Configuration /p:Platform=x64 /m
Write-Host "Building OpenXR API layer (Win32 / 32-bit for 32-bit games)..."
Invoke-Native $MSBuild $LayerProject /p:Configuration=$Configuration /p:Platform=Win32 /m

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
Invoke-Native $MSBuild $InstallerProject /p:Configuration=$Configuration /p:Platform=$Platform /m

if (!(Test-Path $MsiSource)) {
    throw "MSI was not produced at $MsiSource"
}

New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
$versionLine = Select-String -Path $assemblyInfo -Pattern 'AssemblyInformationalVersion\("(\d+\.\d+\.\d+)(?:\.\d+)?(?:[-+][^"]*)?"\)' | Select-Object -First 1
$version = if ($versionLine -and $versionLine.Matches.Count -gt 0) { $versionLine.Matches[0].Groups[1].Value } else { "unknown" }
$PublishExe = Join-Path $PublishDir "xr-viewlab.exe"
if (!(Test-Path $PublishExe)) { throw "Published executable missing: $PublishExe" }
$PublishVersion = (Get-Item $PublishExe).VersionInfo.ProductVersion
if ($PublishVersion -ne $version) {
    throw "Published executable version $PublishVersion does not match build version $version"
}
foreach ($FreshOutput in @($PublishExe, $Dll64Src, $Dll32Src, $MsiSource)) {
    if (!(Test-Path $FreshOutput)) { throw "Required fresh build output missing: $FreshOutput" }
    if ((Get-Item $FreshOutput).LastWriteTimeUtc -lt $BuildStartedUtc) {
        throw "Build output predates this build and may be stale: $FreshOutput"
    }
}

# Administrative extraction proves what the MSI actually contains, independently of WiX source intent.
$VerifyDir = Join-Path $Root "Installer\verify-build"
New-Item -ItemType Directory -Path $VerifyDir -Force | Out-Null
Invoke-Native msiexec.exe /a $MsiSource /qn "TARGETDIR=$VerifyDir"
$PayloadExe = $null
$ExtractDeadline = [DateTime]::UtcNow.AddSeconds(30)
do {
    $PayloadExe = Get-ChildItem $VerifyDir -Recurse -Filter xr-viewlab.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if (!$PayloadExe -or $PayloadExe.Length -ne (Get-Item $PublishExe).Length) {
        $PayloadExe = $null
        Start-Sleep -Milliseconds 200
        continue
    }
    $PayloadVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($PayloadExe.FullName).ProductVersion
    if ([string]::IsNullOrWhiteSpace($PayloadVersion)) {
        $PayloadExe = $null
        Start-Sleep -Milliseconds 200
    }
} while (!$PayloadExe -and [DateTime]::UtcNow -lt $ExtractDeadline)
if (!$PayloadExe) { throw "MSI validation could not find xr-viewlab.exe in the extracted payload" }
$PayloadExe = Get-Item $PayloadExe.FullName
$PayloadVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($PayloadExe.FullName).ProductVersion
if ($PayloadVersion -ne $version) {
    throw "MSI payload executable version $PayloadVersion does not match build version $version"
}
if ((Get-FileHash $PayloadExe.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $PublishExe -Algorithm SHA256).Hash) {
    throw "MSI payload executable is not byte-for-byte identical to the fresh publish output"
}
$PayloadDll64 = Get-ChildItem $VerifyDir -Recurse -Filter XR_APILAYER_cooooked_xrviewlab.dll | Select-Object -First 1
$PayloadDll32 = Get-ChildItem $VerifyDir -Recurse -Filter XR_APILAYER_cooooked_xrviewlab32.dll | Select-Object -First 1
if (!$PayloadDll64 -or (Get-FileHash $PayloadDll64.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $Dll64Src -Algorithm SHA256).Hash) {
    throw "MSI x64 layer payload does not match the fresh native build"
}
if (!$PayloadDll32 -or (Get-FileHash $PayloadDll32.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $Dll32Src -Algorithm SHA256).Hash) {
    throw "MSI Win32 layer payload does not match the fresh native build"
}

# These generated members/types prove the compiled application includes the Overlays XAML and all
# four overlay feature implementations, rather than merely carrying the expected version resource.
$PayloadText = [Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($PayloadExe.FullName))
$RequiredOverlayMarkers = @(
    'OverlaysButton_Click',
    'BoundaryDrag_End',
    'CrosshairEnabledCheck',
    'NotificationService',
    'IRacingEnabledCheck'
)
foreach ($Marker in $RequiredOverlayMarkers) {
    if (!$PayloadText.Contains($Marker)) { throw "MSI payload is missing compiled overlay marker: $Marker" }
}

$MsiDest = Join-Path $DistDir "ViewLab-$version.msi"
Copy-Item -Path $MsiSource -Destination $MsiDest -Force

Write-Host "Validated MSI payload: app $PayloadVersion; fresh WPF/native hashes match; Overlays markers present."
Write-Host "Built MSI:"
Write-Host $MsiDest
