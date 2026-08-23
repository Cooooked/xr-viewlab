# Topmost safety validation

Do not begin with DiRT Rally's in-car transition.

## Test order

1. Install the new MSI after reboot. Automatic Topmost is normal. Open and close ViewLab five times; confirm no
   WPF crash and no orphaned `xr-viewlab.exe`.
2. For the explicit fallback baseline, set `overlay_force_direct=1`, run Pistol Whip, enter and leave one menu, then exit normally. Confirm the direct
   visor/HUD path and clean shutdown.
3. Clear `overlay_force_direct`, run Pistol Whip in a static menu, observe automatic Topmost for 60 seconds, then exit.
   Confirm one `topmost overlays: swapchain ready` entry and no safety/error entry.
4. Reconnect Virtual Desktop before starting a game. Repeat step 3. Do not continue if the stream,
   monitor, or desktop flickers.
5. Only after steps 1–4 pass, launch DiRT Rally and remain in menus. Verify automatic Topmost overlays.
6. The menu-to-car transition is the final test and requires the user to choose to proceed. Close
   nonessential applications first and keep the physical desktop visible.

## Abort conditions

Immediately stop the game test if the log contains `topmost safety: disabled for session`,
`d3d11 safety: device removed`, any repeated swapchain creation, an `XR_ERROR_RUNTIME_FAILURE`, a
stream freeze, monitor reconnect, black application window, or DWM flicker. Do not toggle Topmost to
retry in the same session; the build deliberately refuses that retry.

## Evidence to retain

Copy `%LOCALAPPDATA%\XR ViewLab\ViewLab.log`, the Virtual Desktop OpenXR log, the game's ReShade log,
and Application/System events covering five minutes before and after the test. A healthy session
must contain no more than one Topmost swapchain creation per session.

## Recovery

If only the game fails, close it and Virtual Desktop Streamer normally. If DWM or monitors become
unstable, stop testing and reboot; do not relaunch the game. Set `overlay_force_direct=1` and preserve all
logs before reopening ViewLab.
