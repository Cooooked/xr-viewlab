# Code Map — Changed Files

All under `F:\ReshadeAI\reshade\source\` unless noted.

---

## openxr/openxr_overlay.hpp — Central public API

Constants: `OVERLAY_WIDTH=500`, `OVERLAY_HEIGHT=500`, `XR_MODE_GAMEPLAY=0`, `XR_MODE_TUNING=1`, `XR_CONTROL_SHMEM_NAME`

### XRControlBlock fields
```cpp
struct XRControlBlock {
    // header
    uint32_t version;            // must be 1
    uint32_t size;               // sizeof(XRControlBlock) layout guard
    // mode
    uint32_t xr_mode;            // 0=Gameplay, 1=Tuning
    uint32_t revision;           // increment on every write
    // preview window
    uint32_t win_headless;       // 1 = WS_POPUP (no title bar/border)
    uint32_t win_always_on_top;  // 1 = HWND_TOPMOST
    uint32_t win_snap_cursor;    // 1 = follow cursor; LMB click places it
    uint32_t win_reserved;
    // VR quad edit mode
    uint32_t quad_edit_mode;     // 0=none, 1=Reposition (blue), 2=Transform (orange)
    uint32_t quad_reserved;
    // VR quad transform
    float quad_pos_x, quad_pos_y, quad_pos_z;   // NaN pos_z = use loaded defaults
    float quad_quat_x, quad_quat_y, quad_quat_z, quad_quat_w;
    float quad_width, quad_height;
    float quad_alpha;            // 0.0-1.0 (ImGui wiring TBD)
};
```

### All declarations
```cpp
bool get_active_overlay_resources(resource*, resource_view*);
void set_overlay_visible_active(bool);
bool is_overlay_visible_active();
bool get_overlay_transform_active(overlay_transform*);
void set_overlay_transform_active(const overlay_transform&);
void preview_update(const void*, int, int, int);
bool preview_get_mouse_state(float*, float*, bool[3], float*);
bool preview_consume_edit_delta(lmb_dx,lmb_dy, rmb_dx,rmb_dy, mmb_dx,mmb_dy, wheel);
void control_init(); void control_shutdown();
bool control_is_tuning();
XRControlBlock* control_block();  // live ptr; nullptr if not connected
```

---

## openxr/openxr_hooks_swapchain.cpp — Main overlay logic

### Global quad transform persistence (session 3)
```cpp
static constexpr wchar_t QUAD_TRANSFORM_FILE[] = L"C:\\ProgramData\\ReShade\\openxr_quad_transform.ini";
struct quad_transform_t { float pos_x,pos_y,pos_z, quat_x,quat_y,quat_z,quat_w, width,height,alpha; };
static quad_transform_t s_quad_xform;  // OKB Kneeboard 8 defaults, overwritten by file
void quad_transform_load();  // ini -> s_quad_xform
void quad_transform_save();  // s_quad_xform -> ini (called on edit mode exit)
```

### xrCreateSession additions
1. `quad_transform_load()` — reads ini or creates with defaults
2. Apply `s_quad_xform` to session struct (overlay_offset_x/y, distance, quat, size)
3. Write to ctrl block (clears NaN so ViewLab can read initial position)

### openxr_session struct (key fields)
```cpp
bool  overlay_visible    = false;
float overlay_quad_width = 0.32f, overlay_quad_height = 0.30f;
float overlay_distance   = 0.81f;
float overlay_offset_x   = -0.32f, overlay_offset_y = -0.16f;
float overlay_quat_x=-0.0778f, overlay_quat_y=0.1301f,
      overlay_quat_z=0.0102f,  overlay_quat_w=0.9884f;
```
All overwritten at session start from s_quad_xform.

### xrEndFrame quad logic
1. Resolve: ctrl_has_pos (ctrl != NaN) ? ctrl values : session struct (file-loaded)
2. Apply preview window drag deltas (mode 1=reposition LMB=XY RMB=Z, mode 2=transform LMB=scale RMB=rotY scroll=alpha)
3. Write-back to ctrl + s_quad_xform
4. Detect edit mode exit (nonzero→0) → quad_transform_save()
5. Submit: `pose.position = {pos_x, pos_y, -pos_z}` (negate Z for OpenXR)

### Staging readback (always, no mode gate)
copy overlay_texture → staging_texture → preview_update()

---

## openxr/openxr_overlay_preview.cpp — Preview window + shared memory

### New preview_data fields
```cpp
bool snap_cursor_active;
bool lmb/rmb/mmb_dragging;
int  drag_last_x, drag_last_y;
float lmb_dx/dy, rmb_dx/dy, mmb_dx/dy;
```

### WM_NCHITTEST
Headless + no edit mode → HTCAPTION (window draggable); else HTCLIENT

### WM_TIMER (33ms)
Applies ctrl flags: headless style toggle, topmost, snap-cursor follow

### WM_LBUTTONDOWN
snap_cursor_active → places window + clears ctrl->win_snap_cursor

### WM_PAINT border
edit_mode==1: 6px blue border; edit_mode==2: 6px orange border

### Shared memory — control_init() defaults
```cpp
version=1, size=sizeof(XRControlBlock), xr_mode=Gameplay
win_headless/topmost/snap=0, quad_edit_mode=0
quad_pos_z=NaN (signals "use file/session defaults")
quad_alpha=1.0, quad_width=0.32, quad_height=0.30
```

---

## runtime_gui.cpp — Splash + Gameplay mode

```cpp
const bool show_splash_window = _show_splash && (
    is_loading() ||
    (!desktop_gameplay_mode && _reload_count<=1 && <5sec) ||
    (!desktop_gameplay_mode && !_is_vr && !_show_overlay && _tutorial_index==0 && _input!=nullptr));
//  !_is_vr added session 3: VR runtime _show_overlay never set via Home key
```

---

## runtime.cpp — Effect gate

```cpp
if (_is_vr || reshade::openxr::control_is_tuning())
    update_effects();
```

---

## Deployment

```
C:\ProgramData\ReShade\ReShade64.dll
C:\ProgramData\ReShade\ReShade64_XR.json       (static, never changes)
C:\ProgramData\ReShade\openxr_quad_transform.ini  (created on first run)
```

Build: `F:\ReshadeAI\reshade\build\Release\ReShade64.dll`

## Backups

`F:\ReshadeAI\backups\2026-06-25_build2\` — DLL + source pre-persistence (known working)
