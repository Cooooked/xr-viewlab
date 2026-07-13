$ErrorActionPreference = 'Stop'
$candidates = @(
    'D:\VSBuildTools\MSBuild\Current\Bin\MSBuild.exe',
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
)
$msbuild = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (!$msbuild) { throw 'MSBuild.exe was not found.' }
& $msbuild (Join-Path $PSScriptRoot 'RenderPolicyFixtures.vcxproj') /p:Configuration=Release /p:Platform=x64 /m
if ($LASTEXITCODE -ne 0) { throw "Render-policy fixture build failed: $LASTEXITCODE" }
& (Join-Path $PSScriptRoot 'x64\Release\RenderPolicyFixtures.exe')
if ($LASTEXITCODE -ne 0) { throw "Render-policy fixture run failed: $LASTEXITCODE" }
