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
$BrokerProject = Join-Path $Root "NotificationBroker\ViewLab.NotificationBroker.csproj"
$LayerProject = Join-Path $Root "XRViewLabLayer.vcxproj"
$InstallerProject = Join-Path $Root "Installer\Installer.wixproj"
$MsiSource = Join-Path $Root "Installer\bin\$Configuration\xr-viewlab-setup.msi"
$TargetFramework = ([xml](Get-Content -Raw $DotnetProject)).Project.PropertyGroup.TargetFramework | Where-Object { $_ } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($TargetFramework)) {
    throw "TargetFramework was not found in $DotnetProject"
}
$PublishDir = Join-Path $Root "bin\$Configuration\$TargetFramework\win-x64\publish"
$BrokerTargetFramework = ([xml](Get-Content -Raw $BrokerProject)).Project.PropertyGroup.TargetFramework | Where-Object { $_ } | Select-Object -First 1
$BrokerPublishDir = Join-Path $Root "NotificationBroker\bin\$Configuration\$BrokerTargetFramework\win-x64\publish"
$BrokerPackageBuildDir = Join-Path $Root "NotificationBroker\package-build"
$BrokerPackageContentDir = Join-Path $BrokerPackageBuildDir "content"
$BrokerPackagePath = Join-Path $BrokerPackageBuildDir "ViewLab.NotificationIdentity.msix"
$BrokerCertificatePath = Join-Path $BrokerPackageBuildDir "ViewLab.NotificationIdentity.cer"
$BrokerLogoPath = Join-Path $BrokerPackageBuildDir "xr-viewlab.png"
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
    (Join-Path $Root "NotificationBroker\bin\$Configuration")
    (Join-Path $Root "NotificationBroker\obj\$Configuration")
    $BrokerPackageBuildDir
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

Write-Host "Building medium-integrity notification broker..."
Invoke-Native dotnet publish $BrokerProject -c $Configuration -r win-x64 --self-contained true /p:PublishSingleFile=true

Write-Host "Building signed notification identity package..."
$WindowsSdkBinRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
$WindowsSdkVersionDir = Get-ChildItem $WindowsSdkBinRoot -Directory -ErrorAction Stop |
    Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
    Sort-Object { [version]$_.Name } -Descending |
    Where-Object { Test-Path (Join-Path $_.FullName "x64\makeappx.exe") } |
    Select-Object -First 1
if (!$WindowsSdkVersionDir) { throw "Windows SDK MakeAppx.exe was not found." }
$MakeAppx = Join-Path $WindowsSdkVersionDir.FullName "x64\makeappx.exe"
$SignTool = Join-Path $WindowsSdkVersionDir.FullName "x64\signtool.exe"

$SigningDir = Join-Path $Root ".viewlab-signing"
New-Item -ItemType Directory -Path $SigningDir -Force | Out-Null
$PfxPath = $env:VIEWLAB_MSIX_PFX
$PfxPassword = $env:VIEWLAB_MSIX_PFX_PASSWORD
if ([string]::IsNullOrWhiteSpace($PfxPath)) {
    $PfxPath = Join-Path $SigningDir "ViewLab.NotificationIdentity.pfx"
    $PasswordPath = Join-Path $SigningDir "ViewLab.NotificationIdentity.password.txt"
    if (!(Test-Path $PfxPath) -or !(Test-Path $PasswordPath)) {
        $randomBytes = New-Object byte[] 32
        $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($randomBytes) } finally { $rng.Dispose() }
        $PfxPassword = [Convert]::ToBase64String($randomBytes)
        Set-Content -Path $PasswordPath -Value $PfxPassword -NoNewline -Encoding UTF8
        $secure = ConvertTo-SecureString $PfxPassword -AsPlainText -Force
        $certificate = New-SelfSignedCertificate -Type Custom -Subject "CN=cooooked ViewLab" `
            -FriendlyName "ViewLab local MSIX signing" -KeyUsage DigitalSignature `
            -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter ([DateTime]::UtcNow.AddYears(3)) `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")
        Export-PfxCertificate -Cert $certificate -FilePath $PfxPath -Password $secure | Out-Null
    } else {
        $PfxPassword = Get-Content -Raw $PasswordPath
    }
}
if (!(Test-Path $PfxPath) -or [string]::IsNullOrWhiteSpace($PfxPassword)) {
    throw "MSIX signing requires VIEWLAB_MSIX_PFX + VIEWLAB_MSIX_PFX_PASSWORD or the local development certificate."
}

New-Item -ItemType Directory -Path $BrokerPackageContentDir -Force | Out-Null
$securePfxPassword = ConvertTo-SecureString $PfxPassword -AsPlainText -Force
$PfxData = Get-PfxData -FilePath $PfxPath -Password $securePfxPassword
Export-Certificate -Cert $PfxData.EndEntityCertificates[0] -FilePath $BrokerCertificatePath -Force | Out-Null
Add-Type -AssemblyName PresentationCore
$iconStream = [IO.File]::OpenRead((Join-Path $Root "app.ico"))
try {
    $decoder = [Windows.Media.Imaging.BitmapDecoder]::Create($iconStream, [Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat, [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add($decoder.Frames[0])
    $logoStream = [IO.File]::Create($BrokerLogoPath)
    try { $encoder.Save($logoStream) } finally { $logoStream.Dispose() }
} finally { $iconStream.Dispose() }
$ManifestTemplate = Get-Content -Raw (Join-Path $Root "NotificationBroker\AppxManifest.xml.template")
$PackageManifest = $ManifestTemplate.Replace("__VERSION__", $newVersion)
Set-Content -Path (Join-Path $BrokerPackageContentDir "AppxManifest.xml") -Value $PackageManifest -Encoding UTF8
Invoke-Native $MakeAppx pack /o /d $BrokerPackageContentDir /nv /p $BrokerPackagePath
Invoke-Native $SignTool sign /fd SHA256 /f $PfxPath /p $PfxPassword $BrokerPackagePath

$BrokerExe = Join-Path $BrokerPublishDir "ViewLab.NotificationBroker.exe"
if (!(Test-Path $BrokerExe)) { throw "Notification broker executable missing: $BrokerExe" }
Copy-Item $BrokerPackagePath (Join-Path $BrokerPublishDir "ViewLab.NotificationIdentity.msix") -Force
Copy-Item $BrokerCertificatePath (Join-Path $BrokerPublishDir "ViewLab.NotificationIdentity.cer") -Force
Copy-Item $BrokerLogoPath (Join-Path $BrokerPublishDir "xr-viewlab.png") -Force

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

Write-Host "Building ViewLab Mirror OBS plugin (x64)..."
$ObsPluginProject = Join-Path $Root "ViewLabMirrorPlugin\ViewLabMirrorPlugin.vcxproj"
$ObsPluginDll = Join-Path $Root "ViewLabMirrorPlugin\x64\$Configuration\viewlab-mirror.dll"
if (Test-Path $ObsPluginDll) { Remove-Item -Force $ObsPluginDll }
Invoke-Native $MSBuild $ObsPluginProject /p:Configuration=$Configuration /p:Platform=x64 /m
if (!(Test-Path $ObsPluginDll)) { throw "ViewLab Mirror OBS plugin was not produced at $ObsPluginDll" }
$ObsPluginPublishDir = Join-Path $PublishDir "ObsPlugin"
New-Item -ItemType Directory -Path $ObsPluginPublishDir -Force | Out-Null
Copy-Item -Path $ObsPluginDll -Destination "$ObsPluginPublishDir\" -Force
Copy-Item -Path (Join-Path $Root "ViewLabMirrorPlugin\LICENSE") -Destination (Join-Path $ObsPluginPublishDir "LICENSE.txt") -Force
Copy-Item -Path (Join-Path $Root "ViewLabMirrorPlugin\README.md") -Destination "$ObsPluginPublishDir\" -Force

Write-Host "Building ViewLab Stabilizer OBS filter (x64)..."
$StabPluginProject = Join-Path $Root "ViewLabStabilizerFilter\ViewLabStabilizerFilter.vcxproj"
$StabPluginDll = Join-Path $Root "ViewLabStabilizerFilter\x64\$Configuration\viewlab-stabilizer.dll"
if (Test-Path $StabPluginDll) { Remove-Item -Force $StabPluginDll }
Invoke-Native $MSBuild $StabPluginProject /p:Configuration=$Configuration /p:Platform=x64 /m
if (!(Test-Path $StabPluginDll)) { throw "ViewLab Stabilizer OBS filter was not produced at $StabPluginDll" }
Copy-Item -Path $StabPluginDll -Destination "$ObsPluginPublishDir\" -Force
Copy-Item -Path (Join-Path $Root "ViewLabStabilizerFilter\LICENSE") -Destination (Join-Path $ObsPluginPublishDir "LICENSE-stabilizer.txt") -Force
Copy-Item -Path (Join-Path $Root "ViewLabStabilizerFilter\README.md") -Destination (Join-Path $ObsPluginPublishDir "README-stabilizer.md") -Force

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
$PresentMonSource = Join-Path $Root "ThirdParty\PresentMon\PresentMon-2.4.1-x64.exe"
$PresentMonLicenseSource = Join-Path $Root "ThirdParty\PresentMon\LICENSE.txt"
$ExpectedPresentMonHash = 'D74183E7AE630F72CD3690BE0373ECBFDC6CBB86578148AAB8FA2A7166068F34'
if (!(Test-Path $PresentMonSource) -or (Get-FileHash $PresentMonSource -Algorithm SHA256).Hash -ne $ExpectedPresentMonHash) {
    throw "Pinned PresentMon 2.4.1 source is missing or has an unexpected hash"
}
if (!(Test-Path $PresentMonLicenseSource)) { throw "PresentMon MIT notice is missing" }
$PublishVersion = (Get-Item $PublishExe).VersionInfo.ProductVersion
if ($PublishVersion -ne $version) {
    throw "Published executable version $PublishVersion does not match build version $version"
}
foreach ($FreshOutput in @($PublishExe, $BrokerExe, $BrokerPackagePath, $BrokerCertificatePath, $Dll64Src, $Dll32Src, $ObsPluginDll, $StabPluginDll, $MsiSource)) {
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
$PayloadBroker = Get-ChildItem $VerifyDir -Recurse -Filter ViewLab.NotificationBroker.exe | Select-Object -First 1
$PayloadIdentityPackage = Get-ChildItem $VerifyDir -Recurse -Filter ViewLab.NotificationIdentity.msix | Select-Object -First 1
$PayloadIdentityCertificate = Get-ChildItem $VerifyDir -Recurse -Filter ViewLab.NotificationIdentity.cer | Select-Object -First 1
if (!$PayloadBroker -or (Get-FileHash $PayloadBroker.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $BrokerExe -Algorithm SHA256).Hash) {
    throw "MSI notification broker payload does not match the fresh broker build"
}
if (!$PayloadIdentityPackage -or (Get-FileHash $PayloadIdentityPackage.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $BrokerPackagePath -Algorithm SHA256).Hash) {
    throw "MSI notification identity package does not match the freshly signed package"
}
if (!$PayloadIdentityCertificate -or (Get-FileHash $PayloadIdentityCertificate.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $BrokerCertificatePath -Algorithm SHA256).Hash) {
    throw "MSI notification identity certificate does not match the package signer certificate"
}
$PayloadPresentMon = Get-ChildItem $VerifyDir -Recurse -Filter PresentMon-2.4.1-x64.exe | Select-Object -First 1
$PayloadPresentMonLicense = Get-ChildItem $VerifyDir -Recurse -Filter PresentMon-LICENSE.txt | Select-Object -First 1
if (!$PayloadPresentMon -or (Get-FileHash $PayloadPresentMon.FullName -Algorithm SHA256).Hash -ne $ExpectedPresentMonHash) {
    throw "MSI pinned PresentMon payload is missing or has an unexpected hash"
}
if (!$PayloadPresentMonLicense -or (Get-FileHash $PayloadPresentMonLicense.FullName -Algorithm SHA256).Hash -ne (Get-FileHash $PresentMonLicenseSource -Algorithm SHA256).Hash) {
    throw "MSI PresentMon MIT notice is missing or does not match the repository source"
}
$PackageSignature = Get-AuthenticodeSignature $BrokerPackagePath
$PublicCertificate = New-Object Security.Cryptography.X509Certificates.X509Certificate2($BrokerCertificatePath)
if (!$PackageSignature.SignerCertificate -or $PackageSignature.SignerCertificate.Thumbprint -ne $PublicCertificate.Thumbprint) {
    throw "Notification identity package signer does not match the MSI public certificate"
}

# These generated members/types prove the compiled application includes the Overlays XAML and all
# four overlay feature implementations, rather than merely carrying the expected version resource.
$PayloadText = [Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($PayloadExe.FullName))
$RequiredOverlayMarkers = @(
    'OverlaysButton_Click',
    'BoundaryDrag_End',
    'CrosshairEnabledCheck',
    'NotificationBrokerClient',
    'IRacingEnabledCheck'
)
foreach ($Marker in $RequiredOverlayMarkers) {
    if (!$PayloadText.Contains($Marker)) { throw "MSI payload is missing compiled overlay marker: $Marker" }
}

$MsiDest = Join-Path $DistDir "ViewLab-$version.msi"
Copy-Item -Path $MsiSource -Destination $MsiDest -Force

Write-Host "Validated MSI payload: app $PayloadVersion; fresh WPF/native/broker hashes, pinned PresentMon + MIT notice, signed identity certificate and Overlays markers match."
Write-Host "Built MSI:"
Write-Host $MsiDest
