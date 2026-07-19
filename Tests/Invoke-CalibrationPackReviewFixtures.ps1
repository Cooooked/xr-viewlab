$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'CalibrationPackReviewFixtures\CalibrationPackReviewFixtures.csproj'
dotnet run --project $project -c Release
if ($LASTEXITCODE -ne 0) { throw "Calibration pack-review fixtures failed ($LASTEXITCODE)." }
