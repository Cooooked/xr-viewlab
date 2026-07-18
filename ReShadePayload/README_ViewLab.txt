ViewLab ReShade Remote payload

This payload contains the patched ReShade OpenXR layer used by the Advanced: ReShade Remote panel.

Files:
- ReShade64.dll: patched ReShade OpenXR layer built from the repository's
  ReShadePayloadSource\build\Release output. See Docs\SOURCE_BUILD.md.
- ReShade64_XR.json: OpenXR implicit-layer manifest. The DLL path is relative to the manifest.

Deployment model:
- ViewLab installs these files to C:\ProgramData\ReShade.
- The manifest is registered under HKLM\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit with value 0.
- Games then load the ReShade OpenXR layer globally.
- ViewLab talks to the layer through Local\ReShadeXRControl.

Known limits:
- This is the layer/control payload only. Per-game shader/effect setup is still a separate flow.
- DiRT Rally 2.0 was the proven working target in the ReshadeAIO notes.
