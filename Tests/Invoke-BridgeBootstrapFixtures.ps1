$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'MSBuild.exe not found' }
$bridgeProject = Join-Path $root 'ViewLabBridge\LegacyRuntime\ViewLabLegacyRuntime.vcxproj'
$fixtureProject = Join-Path $PSScriptRoot 'BridgeBootstrapFixtures\BridgeBootstrapFixtures.vcxproj'
& $msbuild $bridgeProject /p:Configuration=Release /p:Platform=x64 /m
if ($LASTEXITCODE) { throw "x64 bridge build failed: $LASTEXITCODE" }
& $msbuild $bridgeProject /p:Configuration=Release /p:Platform=Win32 /m
if ($LASTEXITCODE) { throw "Win32 bridge build failed: $LASTEXITCODE" }
& $msbuild $fixtureProject /p:Configuration=Release /p:Platform=x64 /m
if ($LASTEXITCODE) { throw "bridge fixture build failed: $LASTEXITCODE" }
$fixture = Join-Path $PSScriptRoot 'BridgeBootstrapFixtures\x64\Release\BridgeBootstrapFixtures.exe'
$bridge = Join-Path $root 'ViewLabBridge\LegacyRuntime\bin\x64\Release\vrclient_x64.dll'
& $fixture $bridge
if ($LASTEXITCODE) { throw "bridge fixture failed: $LASTEXITCODE" }
$x86 = Join-Path $root 'ViewLabBridge\LegacyRuntime\bin\Win32\Release\vrclient.dll'
if (!(Test-Path $x86)) { throw 'Win32 bridge output missing' }
Write-Host 'ViewLab Bridge bootstrap fixtures passed (x64 ABI/runtime probe + Win32 build).'
