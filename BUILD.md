# Build XR ViewLab

Active source folder:

`F:\ViewLab`

## Build UI

```powershell
dotnet publish ".\xr-viewlab.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Build Native OpenXR Layer

Use `.\build.ps1`; it finds MSBuild automatically.

## Build Installer

Use `.\build.ps1`; it builds the MSI automatically.

Copy final MSI builds to:

`F:\ViewLab\dist`
