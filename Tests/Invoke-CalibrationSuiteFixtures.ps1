$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'CalibrationSuiteFixtures\CalibrationSuiteFixtures.csproj'
dotnet run --project $project -c Release
if ($LASTEXITCODE -ne 0) { throw "Calibration suite fixtures failed ($LASTEXITCODE)." }
