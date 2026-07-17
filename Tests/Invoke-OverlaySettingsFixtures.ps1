$ErrorActionPreference='Stop'
dotnet run --project (Join-Path $PSScriptRoot 'OverlaySettingsFixtures\OverlaySettingsFixtures.csproj') -c Release
if($LASTEXITCODE-ne0){throw "Overlay settings fixtures failed ($LASTEXITCODE)."}
