# ReShade User Flow

> Target user: someone who has never heard of ReShade and just wants their VR game to look better.

## Step-by-step

1. **Install ViewLab**
   - Download and run the ViewLab MSI.

2. **Open ViewLab**
   - The main window shows crop/visor settings and a **ReShade Remote** section.

3. **Open ReShade Remote**
   - Click the **Remote** button.
   - ReShade Remote checks whether the ViewLab ReShade payload is deployed.

4. **Install the ViewLab ReShade payload**
   - If the payload is not installed, ReShade Remote shows **Install component**.
   - Click it. ViewLab copies the bundled `ReShade64.dll` + `ReShade64_XR.json` to `C:\ProgramData\ReShade` and registers the OpenXR implicit layer.
   - If a stock ReShade 6.7.3 installation is detected, ViewLab warns and replaces/overrides its DLL so the ViewLab payload works. The uninstall path reverses this.

5. **Launch the VR game normally**
   - Use Steam, Virtual Desktop, or your usual launcher.
   - ViewLab does not launch games.

6. **ReShade Remote detects the game**
   - Once the game is running and the ReShade layer is loaded, ReShade Remote shows **Connected**.
   - Controls unlock.

7. **Effects are applied automatically**
   - ReShade loads the active preset (currently the bundled ASCII test preset).
   - The user does not need to toggle effects on every launch; ReShade applies whatever the preset/config says.

8. **Optional: adjust live controls**
   - **Mode**: switch between **Gameplay** and **Tuning**.
     - **Gameplay** (`xr_mode = 0`): desktop effects are suppressed; only the XR/HMD effect runtime is active. Use this for normal play.
     - **Tuning** (`xr_mode = 1`): desktop effects are enabled so you can preview and tweak parameters. Use this when adjusting the look.
   - **Show menu / overlay**: show or hide the ReShade menu (desktop mirror / in-HMD overlay).
   - **Borderless / Always on top**: control how the desktop mirror window behaves.
   - **Reposition / Transform**: move, resize, rotate, or change opacity of the in-HMD menu quad.

9. **Optional: switch presets**
   - Future: choose between bundled presets (ASCII test, HDR/Bloom, custom).
   - Enable/disable effects entirely.

10. **Close the game or ViewLab**
    - The ReShade layer is global; it stays active for other games until ViewLab's **Uninstall component** is used.

## Uninstall / revert

- Click **Uninstall component** in ReShade Remote.
- ViewLab removes the global OpenXR layer and restores the backed-up stock ReShade files if it replaced them.

## Notes

- The current payload is a patched ReShade OpenXR layer. It does **not** require the user to download or run a separate ReShade 6.7.3 installer.
- `ReShade.ini` / `ReShadeVR.ini` / `ReShadeVR2.ini` are created per-game on first launch.
