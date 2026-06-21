# Build XR ViewLab

Active source folder:

`F:\ViewLab`

## Build UI

```powershell
dotnet publish "F:\ViewLab\xr-viewlab.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Build Native OpenXR Layer

```powershell
& "D:\VSBuildTools\MSBuild\Current\Bin\MSBuild.exe" "F:\ViewLab\XRViewLabLayer.vcxproj" /p:Configuration=Release /p:Platform=x64 /m
```

## Build Installer

```powershell
& "D:\VSBuildTools\MSBuild\Current\Bin\MSBuild.exe" "F:\ViewLab\Installer\Installer.wixproj" /p:Configuration=Release /p:Platform=x64 /m
```

Copy final MSI builds to:

`F:\ViewLab\dist`
