$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'OverlayInheritanceFixtures\OverlayInheritanceFixtures.csproj'
dotnet run --project $project -c Release
if ($LASTEXITCODE -ne 0) { throw "Overlay inheritance fixtures failed ($LASTEXITCODE)." }
