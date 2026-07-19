$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'IRacingCuesFixtures\IRacingCuesFixtures.csproj'
dotnet run --project $project -c Release
if ($LASTEXITCODE -ne 0) { throw "iRacing cue fixtures failed ($LASTEXITCODE)." }
