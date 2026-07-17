$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'CalibrationReferenceFixtures\CalibrationReferenceFixtures.csproj'
dotnet run --project $project -c Release
if ($LASTEXITCODE -ne 0) { throw "Calibration reference fixtures failed ($LASTEXITCODE)." }
