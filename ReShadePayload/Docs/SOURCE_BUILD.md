# Bundled ReShade payload source and build

The canonical modified ReShade source is `ReShadePayloadSource/` at the ViewLab repository root.
Its provenance, ViewLab modifications, exact CMake commands and output path are documented in
`ReShadePayloadSource/README.ViewLab.md`.

The project target is `ReShade`; its x64 Release output is
`ReShadePayloadSource/build/Release/ReShade64.dll`. A validated output is copied to
`ReShadePayload/ReShade64.dll`.

`build.ps1` copies the complete `ReShadePayload/` directory into the WPF publish output.
`Installer/Product.wxs` then packages `ReShade64.dll`, `ReShade64_XR.json`, the payload readme and
reference documentation. Runtime installation and layer registration are controlled separately by
ViewLab's ReShade Remote service.

