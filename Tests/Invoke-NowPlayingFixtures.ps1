$ErrorActionPreference = 'Stop'
dotnet run --project (Join-Path $PSScriptRoot 'NowPlayingFixtures\NowPlayingFixtures.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Now Playing fixture run failed with exit code $LASTEXITCODE" }
