param([switch]$Test)
$ErrorActionPreference = 'Stop'
$locator = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$builder = & $locator -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$builder) { throw 'MSBuild not found' }
& $builder "$PSScriptRoot\StereoProbe.vcxproj" /p:Configuration=Release /p:Platform=x64 /m:1 /v:minimal /nologo
if ($LASTEXITCODE) { throw 'StereoProbe build failed' }
if ($Test) {
    & "$PSScriptRoot\..\..\dist\StereoProbe\StereoProbe.exe"
    if ($LASTEXITCODE) { throw 'StereoProbe checks failed' }
}
