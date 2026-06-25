# ViewLab — AI Handoff Notes

**Written by:** ReShade DLL AI (Claude, 2026-06-25)
**For:** The AI that will build the ViewLab companion app
**Project root:** `F:\ReshadeAI\`

---

## What ViewLab Is

A companion desktop app that acts as a remote control for the ReShade OpenXR overlay.
ReShade is a graphics injector loaded into every OpenXR game as an implicit API layer.
ViewLab sits outside the game process and communicates via Windows shared memory.

---

## The Golden Rule

**ViewLab is the remote control. ReShade DLL is the machinery.**

ViewLab writes desired state to shared memory.
ReShade reads it and acts on it each frame.
ViewLab does NOT render anything into the game — only sends commands.

---

## Shared Memory Interface

### Mapping name
```
Local\ReShadeXRControl
```

### How to open it
```cpp
HANDLE h = OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, L"Local\\ReShadeXRControl");
// Returns NULL if ReShade hasn't started yet (game not running).
void *ptr = MapViewOfFile(h, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(XRControlBlock));
XRControlBlock *ctrl = static_cast<XRControlBlock*>(ptr);
```

### Layout guard — ALWAYS check before reading/writing
```cpp
if (ctrl->version != 1 || ctrl->size != sizeof(XRControlBlock))
    // Version mismatch — ReShade DLL is a different build. Show error to user.
```

### Full struct (must match `source/openxr/openxr_overlay.hpp` exactly)
```cpp
struct XRControlBlock
{
    // header — never reorder
    uint32_t version;            // must equal 1
    uint32_t size;               // must equal sizeof(XRControlBlock)

    // mode
    uint32_t xr_mode;            // 0=Gameplay, 1=Tuning
    uint32_t revision;           // increment by 1 on every write you make

    // preview window
    uint32_t win_headless;       // 1 = no title bar / border / buttons (WS_POPUP)
    uint32_t win_always_on_top;  // 1 = HWND_TOPMOST
    uint32_t win_snap_cursor;    // write 1 = window follows cursor; LMB click places it
                                 // ReShade clears this back to 0 when placed
    uint32_t win_reserved;       // write 0

    // VR quad edit mode
    uint32_t quad_edit_mode;     // 0=none, 1=Reposition (blue border), 2=Transform (orange border)
    uint32_t quad_reserved;      // write 0

    // VR quad transform
    // ReShade reads these to position the HMD menu.
    // ReShade also writes back live values here every frame during edit mode.
    // On startup, ReShade fills these from the INI file.
    float quad_pos_x;    // stage-space X metres (left/right)
    float quad_pos_y;    // stage-space Y metres (up/down)
    float quad_pos_z;    // stage-space Z metres (positive = forward, ReShade negates for OpenXR)
    float quad_quat_x;   // orientation quaternion X
    float quad_quat_y;   // orientation quaternion Y
    float quad_quat_z;   // orientation quaternion Z
    float quad_quat_w;   // orientation quaternion W
    float quad_width;    // metres
    float quad_height;   // metres
    float quad_alpha;    // 0.0=transparent, 1.0=opaque (ImGui wiring not yet done in ReShade)
};
```

### Writing safely
Always increment `revision` after ANY write. ReShade uses this as a cheap "did anything change" signal.
```cpp
ctrl->xr_mode = 1;
ctrl->revision++;
```

---

## What ReShade Does Automatically (ViewLab doesn't need to touch)

- Renders the ReShade ImGui menu into an OpenXR quad layer
- Home key toggles the HMD menu on/off
- Staging readback — mirrors HMD texture to the desktop preview window
- Loads transform from INI on game start
- Saves transform to INI when `quad_edit_mode` goes nonzero → 0
- Draws blue/orange border in the preview window based on `quad_edit_mode`
- Applies mouse drag from preview window to quad position/rotation/scale (see below)
- Applies `win_headless`, `win_always_on_top`, `win_snap_cursor` to the preview window

---

## What ViewLab Must Do

### UI controls to expose

| Control | What it writes |
|---------|---------------|
| Tuning mode checkbox | `xr_mode = 0 or 1` |
| Headless toggle | `win_headless = 0 or 1` |
| Always on top toggle | `win_always_on_top = 0 or 1` |
| Reposition desktop window button | `win_snap_cursor = 1` (ReShade clears when placed) |
| Reposition HMD menu button | `quad_edit_mode = 1` (set back to 0 to confirm) |
| Transform HMD menu button | `quad_edit_mode = 2` (set back to 0 to confirm) |

### Status display
- Poll `ctrl->revision` at ~10Hz. When it changes, re-read the struct.
- Read `ctrl->quad_pos_*` etc to display live position (ReShade writes these back during edit).
- If `OpenFileMapping` returns NULL: "Waiting for game..." state.

### Saving user preferences
ViewLab should save its own UI state (which toggles are on, window position) separately in its own config file. Do NOT rely on the shared memory surviving between sessions — it's recreated each game launch.

---

## Edit Mode Flow

### Reposition (move menu in HMD)
1. User clicks "Reposition HMD Menu" in ViewLab
2. ViewLab writes `quad_edit_mode = 1; revision++`
3. ReShade preview window gets a **blue border**
4. User drags in the preview window:
   - LMB drag → moves menu left/right/up/down
   - RMB drag → moves menu forward/back (Z depth)
5. User clicks "Confirm" in ViewLab
6. ViewLab writes `quad_edit_mode = 0; revision++`
7. ReShade detects the 1→0 transition and saves to `openxr_quad_transform.ini`
8. Position persists across all future game launches

### Transform (scale/rotate menu)
1. ViewLab writes `quad_edit_mode = 2; revision++`
2. Preview window gets **orange border**
3. User drags:
   - LMB drag → scales width/height
   - RMB drag → rotates around Y axis (yaw)
   - Scroll wheel → changes alpha (opacity — wiring TBD in ReShade render side)
   - MMB drag → horizontal curve (not yet implemented)
4. Confirm: `quad_edit_mode = 0; revision++` → saves

---

## Transform INI File

ReShade maintains this file itself. ViewLab can read it for display or to offer a "reset to defaults" button.

```
C:\ProgramData\ReShade\openxr_quad_transform.ini
```

```ini
[Transform]
pos_x=-0.320000      ; metres, stage-space
pos_y=-0.160000
pos_z=0.810000
quat_x=-0.077800
quat_y=0.130100
quat_z=0.010200
quat_w=0.988400
width=0.320000
height=0.300000
alpha=1.000000
```

Default values are OKB Kneeboard 8 position (sitting sim-racer kneeboard).

---

## Connection Lifecycle

```
Game not running → OpenFileMapping returns NULL → show "Waiting for game"
Game starts      → ReShade creates mapping, writes version=1 and initial defaults
ViewLab opens    → OpenFileMapping succeeds → check version/size → ready
Game exits       → mapping destroyed → go back to "Waiting for game"
```

Poll for the mapping every second. When it appears, do the version check, then start polling revision at 10Hz.

---

## Known Limitations / TBD in ReShade

- **Quad alpha** (`ctrl->quad_alpha`): field exists and is tracked, but the ImGui render pipeline doesn't yet multiply by it. Setting it has no visible effect until that's wired up.
- **MMB drag (horizontal curve)**: cylindrical projection not implemented in ReShade yet. ReShade accumulates the drag delta but doesn't apply it.
- **Mouse → ImGui passthrough**: clicking in the preview window doesn't interact with the ReShade menu. Preview is read-only for the user right now.
- **OKB position may not appear correct in all games**: if it looks wrong, use Reposition mode to drag to desired spot; it saves automatically.

---

## ReShade Source Location

```
F:\ReshadeAI\reshade\source\openxr\openxr_overlay.hpp          — XRControlBlock definition
F:\ReshadeAI\reshade\source\openxr\openxr_overlay_preview.cpp  — window control + shared mem
F:\ReshadeAI\reshade\source\openxr\openxr_hooks_swapchain.cpp  — quad transform + edit logic
```

If the XRControlBlock struct changes (new fields added), the `size` field will no longer match and `control_block()` will return nullptr, effectively disabling the interface. Both sides must be rebuilt together.

---

## Quick Test Without ViewLab

Write a small utility that opens the mapping and sets fields manually:
```cpp
HANDLE h = OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, L"Local\\ReShadeXRControl");
auto *ctrl = (XRControlBlock*)MapViewOfFile(h, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(XRControlBlock));
ctrl->xr_mode = 1;   // enable Tuning mode
ctrl->revision++;
```
