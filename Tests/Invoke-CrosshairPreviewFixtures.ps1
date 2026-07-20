$ErrorActionPreference = 'Stop'
dotnet run --project (Join-Path $PSScriptRoot 'CrosshairPreviewFixtures\CrosshairPreviewFixtures.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Crosshair preview fixture run failed with exit code $LASTEXITCODE" }
