$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $PSScriptRoot 'MediaSessionFixtures\MediaSessionFixtures.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Media session fixture run failed with exit code $LASTEXITCODE" }
