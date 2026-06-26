Place the following files from the OpenVR SDK here:
  openvr_api.h    — from https://github.com/ValveSoftware/openvr/tree/master/headers
  openvr_api.lib  — x64 import library from https://github.com/ValveSoftware/openvr/tree/master/lib/win64

The .vcxproj will error at build time if openvr_api.h is missing.
