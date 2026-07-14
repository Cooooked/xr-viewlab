$ErrorActionPreference='Stop'
dotnet run --project (Join-Path $PSScriptRoot 'PerformanceTraceFixtures\PerformanceTraceFixtures.csproj') -c Release
if($LASTEXITCODE-ne0){throw "Performance trace fixtures failed: $LASTEXITCODE"}
