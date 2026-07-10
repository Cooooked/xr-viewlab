# Handoff: Verify ViewLab 4.1.88 Visor Shape via ReShade Screenshot

## Mission

Verify that ViewLab 4.1.88's visor mask shape and curvature are correct **in-HMD** by:
1. Installing ReShadePayload and Pistol Whip with ViewLab active
2. Capturing a screenshot via ReShade during gameplay
3. Loading the screenshot and analyzing it for correct visor geometry

**Success = You have a screenshot file loaded that shows the in-HMD visor, which can then be passed to an image-analysis agent.**

## Context — Visor Shape Expectations

The visor should be a **smooth kidney-bean shape** with these properties:

- **Outer boundary**: smooth curved edge (no pixelation/blockiness)
- **Inner boundary** (lower nose-bridge): curved lower edge with smooth rise to the bridge
- **Bridge geometry**: cubic Bézier curve with **horizontal tangents at both ends**
  - Should look like: "flat bottom → gentle smooth rise → flat bridge"
  - NOT a sharp point or rectangular shelf
- **Opacity**: solid black interior (fully opaque, alpha=1.0)
- **Symmetry**: left and right sides mirror perfectly
- **Overall**: organic, natural-looking; never sharp-cornered or rectangular

Current default settings (user's working config):
- Render crop: 0.2 vertical / 0.8 horizontal
- Foveated center: ON
- Crop outer edges: ON

## Workflow Overview

Follow **RESHADE_SCREENSHOT_WORKFLOW.md** in the project root for the complete automated workflow. TL;DR:

1. **Prepare**: ViewLab 4.1.88 running, ReShadePayload installed
2. **Launch**: Pistol Whip with ReShade injection confirmed
3. **Capture**: Press PrintScreen or F12 to screenshot
4. **Locate**: Find screenshot in `%USERPROFILE%\Documents\ReShade\Screenshots\`
5. **Analyze**: Load screenshot with Read tool, pass to image-analysis agent

## Step-by-Step

### 1. Ensure ViewLab 4.1.88 is Installed

```
F:\AI-Projects\ViewLab\dist\ViewLab-4.1.88.msi
```

If not installed, run the MSI. Then launch ViewLab UI (should be in Start menu or Program Files).

### 2. Install ReShadePayload

In ViewLab UI:
- Scroll to **Advanced: ReShade Remote** (bottom-right area)
- Click **Install ReShade Component** button (red/elevated)
- Wait ~5 seconds; ReShade Remote status should change to "Connected" or show active status
- Check log: `%LOCALAPPDATA%\XR ViewLab\Logs\ViewLab.log` for confirmation

### 3. Launch Pistol Whip

Keep ViewLab UI running. Launch Pistol Whip via Steam or direct exe.

**Wait for**:
- Game fully loaded (audio + VR content)
- Check `ViewLab.log` for injection lines:
  - `initialized` (mask renderer started)
  - `OK draw executed` (visor drawing each frame)
  - No `FAIL` or crash lines

### 4. Capture Screenshot

Use keyboard automation (computer-use MCP or similar) to press **PrintScreen** (key code 42) or **F12** (key code 61).

Wait 2–3 seconds for capture to complete.

### 5. Locate Screenshot File

Screenshots save to: `%USERPROFILE%\Documents\ReShade\Screenshots\`

Filename format: `ReShade_YYYY-MM-DD_HH-mm-ss.png`

**PowerShell to find latest:**
```powershell
$ScreenshotDir = Join-Path $env:USERPROFILE "Documents\ReShade\Screenshots"
Get-ChildItem $ScreenshotDir -Filter "*.png" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
```

### 6. Load Screenshot

Use the **Read** tool to load the screenshot PNG file. Example:
```
Read: C:\Users\strif\Documents\ReShade\Screenshots\ReShade_2026-07-10_14-23-45.png
```

### 7. Analyze Screenshot

Once loaded, the screenshot is visible. You can now:
- **Describe the visor shape** yourself (quick check)
- **Pass the image to an image-analysis agent** for detailed verification

Look for the expectations listed in "Context" above.

## Critical Notes

- **Keep ViewLab UI running** during the entire test; the config service must be active
- **Monitor `ViewLab.log`** for injection status; `OK draw executed` = visor is drawing
- **ReShade must inject successfully** or the screenshot will just show the game without any ReShade features
- **Default screenshot keybind is PrintScreen**; if it doesn't work, check ReShade.ini for custom keybind

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Screenshot doesn't capture | Press PrintScreen/F12 multiple times; verify ReShade injected (check ViewLab.log) |
| ReShade won't inject | Restart Pistol Whip + ViewLab UI; check `C:\ProgramData\ReShade\` has payload files |
| ViewLab crashes | Check `ViewLab.log` for `FAIL` lines; reinstall MSI if needed |
| Screenshot directory doesn't exist | Run ReShade at least once to create `Documents\ReShade\Screenshots\`; or check ReShade.ini for custom path |

## Success Criteria

✅ **You have successfully completed this task when**:
- Screenshot file exists and is readable on disk
- Screenshot is loaded via Read tool (you can see it as an image)
- Screenshot shows in-HMD Pistol Whip view with visor visible
- Screenshot can be analyzed by an image-analysis agent for visor shape verification

**Do NOT stop until you have a loadable screenshot image that can be passed to an analyzer.**

## References

- **Full workflow doc**: `F:\AI-Projects\ViewLab\RESHADE_SCREENSHOT_WORKFLOW.md`
- **ViewLab logs**: `%LOCALAPPDATA%\XR ViewLab\Logs\ViewLab.log`
- **ViewLab config**: `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini`
- **Latest installer**: https://github.com/Cooooked/xr-viewlab/releases/tag/v4.1.88

---

Once you have the screenshot loaded, report back with the file path and a description of what you see. An image-analysis agent can then evaluate whether the visor geometry is correct.

