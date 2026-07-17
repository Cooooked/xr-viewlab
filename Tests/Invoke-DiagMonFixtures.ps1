$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $PSScriptRoot 'DiagMonFixtures\DiagMonFixtures.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "DiagMon fixtures failed with exit code $LASTEXITCODE" }
