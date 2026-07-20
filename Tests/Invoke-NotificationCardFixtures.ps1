$ErrorActionPreference = 'Stop'
dotnet run --project (Join-Path $PSScriptRoot 'NotificationCardFixtures\NotificationCardFixtures.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw "Notification card fixture run failed with exit code $LASTEXITCODE" }
