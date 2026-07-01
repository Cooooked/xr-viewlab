# Build XR ViewLab

Active source folder:

`F:\AI-Projects\ViewLab`

## Build UI

```powershell
dotnet publish ".\xr-viewlab.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## Build Native OpenXR Layer

Use `.\build.ps1`; it finds MSBuild automatically.

## Build Installer

Use `.\build.ps1`; it builds the MSI automatically.

Copy final MSI builds to:

`F:\AI-Projects\ViewLab\dist`

After implementation work, run the full installer build and give the user the exact runnable MSI path,
including file name, in a plain text block suitable for Windows Run. Example:

```text
F:\AI-Projects\ViewLab\dist\ViewLab-4.1.55.msi
```
