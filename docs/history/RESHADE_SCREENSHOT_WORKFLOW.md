# ReShade Screenshot Workflow for In-HMD Verification

## Purpose

Enable automated, programmatic capture and analysis of in-HMD visual features without requiring manual headset inspection. This workflow allows AI agents and tools to:
- Capture screenshots via ReShade during gameplay
- Locate and load screenshot files automatically
- Analyze visual output (visor shape, menu rendering, preset effects, etc.)
- Verify features work as intended in VR

## When to Use This Workflow

Any task that requires visual verification "in the headset" but the operator cannot see it:
- ✅ Verify visor mask shape/curvature (this release)
- ✅ Verify ReShade 3D in-HMD menu renders correctly
- ✅ Verify ReShade preset effects apply correctly
- ✅ Verify color/brightness/contrast tuning looks right
- ✅ Verify edge-smear, crop, or rendering artifacts
- ✅ Verify UI overlays/quads render in the correct position

## ReShade Configuration

### Screenshot Keybind Setup (One-Time)

1. **Locate ReShade config**: `%LOCALAPPDATA%\ReShade_Backup\ReShade.ini` or similar (if installed via ViewLab)
   - Alternative: search for `ReShade.ini` in Windows, or check ReShade documentation for config path
2. **Find or create the screenshot keybind**:
   ```ini
   [INPUT]
   KeyScreenshot=42       ; 42 = PrintScreen (standard)
   ; Or use: 61 = F12, or any DirectInput key code
   ```
3. **Set screenshot output path** (optional but recommended):
   ```ini
   [SCREENSHOT]
   ; ReShade default: %USERPROFILE%\Documents\ReShade\Screenshots\
   ; Or specify custom: C:\ReShadeScreenshots\
   ```

**Default behavior**: if not configured, screenshots save to `%USERPROFILE%\Documents\ReShade\Screenshots\`

### Verify ReShade is Active in ViewLab

- ViewLab UI must be running
- ReShade Remote section must show "Connected" or active status
- Pistol Whip (or target game) must be running with ReShade DLL injected
- Log should show `OK draw executed` or similar injection confirmation

## Automated Workflow — Step by Step

### Phase 1: Prepare Environment

```powershell
# 1. Install/verify ViewLab is running
Start-Process "F:\AI-Projects\ViewLab\dist\ViewLab-4.1.88.msi"  # If needed
# (Or launch ViewLab UI from Program Files)

# 2. Verify ReShadePayload is installed
#    In ViewLab UI: Advanced → ReShade Remote → Install ReShade Component
```

### Phase 2: Launch Game with ReShade Active

```powershell
# 3. Launch target game (e.g., Pistol Whip)
# Keep ViewLab UI running in background
```

**Wait for**:
- Game to fully load (audio + VR content visible)
- ReShade injection confirmed in log: `%LOCALAPPDATA%\XR ViewLab\Logs\ViewLab.log`
- Look for: `initialized`, `OK draw executed`, or ReShade heartbeat lines

### Phase 3: Capture Screenshot

```powershell
# 4. Programmatically trigger screenshot keybind
# Use input automation (e.g., computer-use MCP or keyboard emulation)
# Press: PrintScreen (key code 42) or F12 (key code 61)
# Wait 2-3 seconds for capture to complete
```

**For manual test**: press PrintScreen or F12 directly on keyboard while game is running

### Phase 4: Locate Screenshot File

```powershell
# 5. Find the screenshot
# Default location: %USERPROFILE%\Documents\ReShade\Screenshots\
$ScreenshotDir = Join-Path $env:USERPROFILE "Documents\ReShade\Screenshots"

# Alternative: check ReShade.ini for custom path
$ReShadeIni = Join-Path $env:LOCALAPPDATA "ReShade_Backup\ReShade.ini"
# Parse [SCREENSHOT] section for path

# List all screenshots, sorted by newest first
Get-ChildItem $ScreenshotDir -Filter "*.png" -ErrorAction SilentlyContinue | 
  Sort-Object LastWriteTime -Descending | 
  Select-Object -First 5 FullName, LastWriteTime
```

**Expected output**: list of `.png` files with timestamps; newest one is your screenshot

### Phase 5: Load and Analyze Screenshot

```powershell
# 6. Use Read tool to load the screenshot
# Path example: C:\Users\strif\Documents\ReShade\Screenshots\ReShade_2026-07-10_12-34-56.png

# Once loaded, pass to image-analysis agent:
# - Agent inspects visual elements (visor shape, menu position, color, etc.)
# - Agent confirms or reports deviations from expected behavior
```

## Screenshot File Naming

ReShade uses a standard naming convention:
```
ReShade_YYYY-MM-DD_HH-mm-ss.png
```

Example:
```
ReShade_2026-07-10_14-23-45.png
```

**To find the most recent screenshot**:
```powershell
Get-ChildItem $ScreenshotDir -Filter "ReShade_*.png" | 
  Sort-Object LastWriteTime -Descending | 
  Select-Object -First 1 | 
  ForEach-Object { Write-Host $_.FullName }
```

## Analysis Template — What to Look For

Once screenshot is loaded, use an image-analysis agent to evaluate:

### Visor Mask Verification
- **Shape**: symmetric kidney bean, not rectangular or blocky
- **Outer edge**: smooth curve, antialiased, no hard corners
- **Inner edge**: curved lower boundary (nose bridge), smooth rise, no sharp points
- **Bridge tangents**: horizontal entry at the top edge, not vertical wall
- **Opacity**: solid black interior, not semi-transparent grey
- **Symmetry**: left and right halves mirror each other

### ReShade Menu Verification
- **Visibility**: menu renders in correct 3D space (in-HMD, not clipped/invisible)
- **Readability**: text is legible, colors are correct
- **Position**: menu sits at expected depth/offset relative to player
- **Interaction state**: menu responds to controller input (if applicable)

### Preset/Effect Verification
- **Color grading**: expected tint, saturation, brightness applied
- **Distortion/blur**: any shader effects visible and correctly applied
- **Artifacts**: no flickering, tearing, or rendering errors

## Troubleshooting

### Screenshot not captured
**Problem**: PrintScreen pressed but no file appears
- **Check**: ReShade is actually injected (look in game for ReShade overlay toggle key, typically Home or Shift+F2)
- **Check**: screenshot directory exists and is writable
- **Try**: press the ReShade overlay key first to confirm injection, then try screenshot
- **Try**: check ReShade.ini to confirm keybind is not disabled or mapped to a different key

### Screenshot directory not found
**Problem**: default path doesn't exist or screenshots save elsewhere
- **Check**: ReShade.ini for custom screenshot path
- **Check**: run ReShade at least once with a screenshot to create the directory
- **Fallback**: search for `ReShade_*.png` files on disk with `Get-ChildItem -Recurse`

### ReShade injection fails silently
**Problem**: game runs but ReShade is not active
- **Check**: `%LOCALAPPDATA%\XR ViewLab\Logs\ViewLab.log` for injection errors
- **Check**: `C:\ProgramData\ReShade` has the payload files (ReShade64.dll, etc.)
- **Try**: restart Pistol Whip and ViewLab UI
- **Try**: verify ReShade Remote in ViewLab shows "Connected" status

## Example: Complete Automated Workflow (Pseudocode)

```powershell
# 1. Ensure ViewLab is running
if (-not (Get-Process "XRViewLab" -ErrorAction SilentlyContinue)) {
    Start-Process "C:\Program Files\cooooked\XR ViewLab\XRViewLab.UI.exe"
    Start-Sleep -Seconds 3
}

# 2. Ensure ReShadePayload is installed
# (Can be verified by checking C:\ProgramData\ReShade\ReShade64.dll exists)
if (-not (Test-Path "C:\ProgramData\ReShade\ReShade64.dll")) {
    Write-Host "ReShadePayload not installed. Please install via ViewLab UI."
    exit 1
}

# 3. Launch Pistol Whip
& "C:\Program Files (x86)\Steam\steamapps\common\Pistol Whip\PistolWhip.exe"
Start-Sleep -Seconds 10  # Wait for game to fully load

# 4. Trigger screenshot (via keyboard automation)
# [Simulate PrintScreen key press]
[System.Windows.Forms.SendKeys]::SendWait("{PRTSC}")
Start-Sleep -Seconds 3

# 5. Find latest screenshot
$ScreenshotDir = Join-Path $env:USERPROFILE "Documents\ReShade\Screenshots"
$LatestScreenshot = Get-ChildItem $ScreenshotDir -Filter "*.png" -ErrorAction Stop | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 1 -ExpandProperty FullName

Write-Host "Screenshot captured: $LatestScreenshot"

# 6. Load screenshot and pass to analysis agent
# (Agent will use Read tool to load image, then analyze)
```

## Integration Points

### For Future AI Agents
Include this workflow prompt snippet:
```
If you need to verify in-HMD visual features:
1. Refer to RESHADE_SCREENSHOT_WORKFLOW.md in the project root
2. Follow Phase 1–5 (Prepare → Launch → Capture → Locate → Analyze)
3. Use computer-use MCP to press screenshot keybind if needed
4. Use Read tool to load the PNG from the screenshot directory
5. Pass image to image-analysis agent for detailed verification
```

### For Scripts/Tools
Create a reusable PowerShell function:
```powershell
function Invoke-ReShadeScreenshot {
    param([string]$GamePath, [int]$WaitSeconds = 10)
    
    # Launch game
    & $GamePath
    Start-Sleep -Seconds $WaitSeconds
    
    # Trigger screenshot
    # (Use keyboard automation here)
    
    # Return latest screenshot path
    $ScreenshotDir = Join-Path $env:USERPROFILE "Documents\ReShade\Screenshots"
    Get-ChildItem $ScreenshotDir -Filter "*.png" | 
        Sort-Object LastWriteTime -Descending | 
        Select-Object -First 1 -ExpandProperty FullName
}
```

## References

- ReShade official docs: https://reshade.me/
- ViewLab ReShade Remote: `%LOCALAPPDATA%\XR ViewLab\` → ReShade Remote section
- ViewLab logs: `%LOCALAPPDATA%\XR ViewLab\Logs\ViewLab.log`
- ReShadePayload location: `F:\AI-Projects\ViewLab\ReShadePayload\`

