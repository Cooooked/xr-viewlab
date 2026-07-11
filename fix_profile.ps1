# Restore 64-bit OpenXR implicit API layers wiped by ReShade Remote "Install component"
# (New-Item -Force recreated the Implicit key). Run as admin.
$key = 'HKLM:\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit'
$layers = @{
    'C:\Program Files\xr-viewlab\XR_APILAYER_cooooked_xrviewlab.json'          = 0
    'C:\ProgramData\ReShade\ReShade64_XR.json'                                 = 0
    'C:\Program Files\OpenKneeboard\bin\OpenKneeboard-OpenXR.json'             = 1
    'C:\Program Files\OpenXR-OBSMirror\v0.2.5\XR_APILAYER_NOVENDOR_OBSMirror.json' = 0
    'C:\Program Files\OpenXR-Toolkit\XR_APILAYER_MBUCCHIA_toolkit.json'        = 0
    'C:\Program Files\Racelab VR\XR_APILAYER_app_racelab_Overlay64.json'       = 0
    'C:\Program Files\Virtual Desktop Streamer\openxr-oculus-compatibility.json' = 0
    'C:\Program Files\XRFrameTools\lib\XR_APILAYER_FREDEMMOTT_core_metrics64.json'  = 0
    'C:\Program Files\XRFrameTools\lib\XR_APILAYER_FREDEMMOTT_d3d11_metrics64.json' = 0
    'C:\Program Files\XRFrameTools\lib\XR_APILAYER_FREDEMMOTT_nvapi_metrics64.json' = 0
}
foreach ($l in $layers.GetEnumerator()) {
    Set-ItemProperty $key -Name $l.Key -Value $l.Value -Type DWord
}
Get-ItemProperty $key | Format-List
