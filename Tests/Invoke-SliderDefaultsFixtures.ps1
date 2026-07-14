$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $PSScriptRoot 'SliderDefaultsFixtures\SliderDefaultsFixtures.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Slider defaults fixture run failed with exit code $LASTEXITCODE" }
