param([switch]$NoVersionBump, [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$versionPath = Join-Path $PSScriptRoot 'version.json'
$state = Get-Content $versionPath -Raw | ConvertFrom-Json
$previous = [version]$state.version
$version = if ($state.built -and !$NoVersionBump) { '{0}.{1}.0' -f $previous.Major, ($previous.Minor + 1) } else { $state.version }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$staging = Join-Path $output ('.build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $staging | Out-Null
Write-Output "Building iRacing DynLOD $version"
dotnet publish (Join-Path $PSScriptRoot 'DynLOD.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=None "-p:Version=$version" "-p:AssemblyVersion=$version.0" "-p:FileVersion=$version.0" -o $staging
if ($LASTEXITCODE -ne 0) { throw 'Publish failed; version unchanged.' }
$exe = Join-Path $staging 'iRacing-DynLOD.exe'
$fixture = Join-Path $PSScriptRoot 'Tests\renderer-fixture.ini'
$test = Start-Process -FilePath $exe -ArgumentList "--self-test --fixture `"$fixture`"" -WindowStyle Hidden -PassThru -Wait
if ($test.ExitCode -ne 0) { throw (Get-Content (Join-Path $staging 'self-test.txt') -Raw) }
Write-Output (Get-Content (Join-Path $staging 'self-test.txt') -Raw)
$package = Join-Path $staging 'package'
New-Item -ItemType Directory -Force $package | Out-Null
Copy-Item $exe $package
Copy-Item (Join-Path $PSScriptRoot 'README.md'), (Join-Path $PSScriptRoot 'LICENSE'), (Join-Path $PSScriptRoot 'NOTICE') $package
$zip = Join-Path $output "iRacing-DynLOD-$version-win-x64.zip"
Compress-Archive -Path (Join-Path $package '*') -DestinationPath $zip -Force
$checksum = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content ($zip + '.sha256') "$checksum  $([IO.Path]::GetFileName($zip))" -Encoding ascii
$destination = Join-Path $output 'iRacing-DynLOD.exe'
if (Test-Path $destination) { Move-Item -LiteralPath $destination -Destination (Join-Path $staging 'previous.exe') }
Copy-Item $exe $destination
@{ version = $version; built = $true } | ConvertTo-Json | Set-Content $versionPath
Write-Output "Ready: $destination"
Write-Output "Release: $zip"
