$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $PSScriptRoot 'HistoryFixtures\ViewLab.HistoryFixtures.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "History fixture run failed with exit code $LASTEXITCODE" }
