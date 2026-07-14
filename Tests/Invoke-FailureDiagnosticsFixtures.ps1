$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $PSScriptRoot 'FailureDiagnosticsFixtures\FailureDiagnosticsFixtures.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Failure diagnostics fixture run failed with exit code $LASTEXITCODE" }
