$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $PSScriptRoot 'IRacingFixtures\ViewLab.IRacingFixtures.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "iRacing fixture run failed with exit code $LASTEXITCODE" }
