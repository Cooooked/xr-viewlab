param([int]$TimeoutSeconds = 10)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$projectDir = Join-Path $PSScriptRoot 'NotificationFixture'
$publishDir = Join-Path $projectDir 'bin\Release\net8.0-windows10.0.19041.0\win-x64\publish'
$packageDir = Join-Path $projectDir 'package-build'
$contentDir = Join-Path $packageDir 'content'
$packagePath = Join-Path $packageDir 'ViewLab.NotificationFixture.msix'
$pfxPath = Join-Path $root '.viewlab-signing\ViewLab.NotificationIdentity.pfx'
$passwordPath = Join-Path $root '.viewlab-signing\ViewLab.NotificationIdentity.password.txt'

if (!(Test-Path $pfxPath) -or !(Test-Path $passwordPath)) { throw 'Run .\build.ps1 first so the local notification signing identity exists.' }
$password = Get-Content -Raw $passwordPath
$sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin" -Directory |
    Where-Object Name -match '^\d+\.\d+\.\d+\.\d+$' | Sort-Object { [version]$_.Name } -Descending |
    Where-Object { Test-Path (Join-Path $_.FullName 'x64\makeappx.exe') } | Select-Object -First 1
if (!$sdk) { throw 'Windows SDK MakeAppx.exe was not found.' }
$makeAppx = Join-Path $sdk.FullName 'x64\makeappx.exe'
$signTool = Join-Path $sdk.FullName 'x64\signtool.exe'

Get-AppxPackage -Name 'cooooked.ViewLab.NotificationFixture' | Remove-AppxPackage -ErrorAction SilentlyContinue
if (Test-Path $packageDir) { Remove-Item $packageDir -Recurse -Force }
New-Item $contentDir -ItemType Directory -Force | Out-Null

& dotnet publish (Join-Path $projectDir 'ViewLab.NotificationFixture.csproj') -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { throw "Fixture publish failed: $LASTEXITCODE" }
Copy-Item (Join-Path $projectDir 'AppxManifest.xml') (Join-Path $contentDir 'AppxManifest.xml')
Copy-Item (Join-Path $root 'NotificationBroker\package-build\xr-viewlab.png') (Join-Path $publishDir 'xr-viewlab.png')
& $makeAppx pack /o /d $contentDir /nv /p $packagePath
if ($LASTEXITCODE -ne 0) { throw "Fixture package failed: $LASTEXITCODE" }
& $signTool sign /fd SHA256 /f $pfxPath /p $password $packagePath
if ($LASTEXITCODE -ne 0) { throw "Fixture signing failed: $LASTEXITCODE" }

Add-AppxPackage -Path $packagePath -ExternalLocation $publishDir
$map = [IO.MemoryMappedFiles.MemoryMappedFile]::OpenExisting('Local\XRViewLabNotifications')
$view = $map.CreateViewAccessor()
$marker = 'fixture-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss.fff')
$fixtureStatus = Join-Path $env:LOCALAPPDATA 'XR ViewLab\notification-fixture-status.json'
Remove-Item $fixtureStatus -Force -ErrorAction SilentlyContinue
try {
    & (Join-Path $publishDir 'ViewLab.NotificationFixture.exe') $marker
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $observed = $null
    do {
        Start-Sleep -Milliseconds 50
        $count = $view.ReadUInt32(8)
        $active = $view.ReadUInt32(16)
        if ($count -gt 0 -and $active -eq 1) {
            $observed = [pscustomobject]@{
                Marker = $marker
                CardCount = $count
                CardId = $view.ReadUInt32(20)
                Package = (Get-AppxPackage -Name 'cooooked.ViewLab.NotificationFixture').PackageFullName
            }
            break
        }
    } while ([DateTime]::UtcNow -lt $deadline)
    if (!$observed) {
        $fixtureDiagnostic = if (Test-Path $fixtureStatus) { Get-Content -Raw $fixtureStatus } else { 'fixture produced no diagnostic file' }
        throw "The packaged Windows notification did not reach ViewLab before timeout. Source diagnostic: $fixtureDiagnostic"
    }
    $observed
}
finally {
    $view.Dispose(); $map.Dispose()
    Get-AppxPackage -Name 'cooooked.ViewLab.NotificationFixture' | Remove-AppxPackage -ErrorAction SilentlyContinue
}
