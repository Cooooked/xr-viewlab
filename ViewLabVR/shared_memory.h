#pragma once
#include <cstdint>
#include <windows.h>

// Named mapping: separate namespace from OpenXR's "Local\ReShadeXRControl"
static constexpr const wchar_t* VRControlMappingName = L"Local\\ReShadeVRControl";

// Must match ReShadeVRControlService.cs VRControlBlock exactly.
// Both sides use Pack=4 / #pragma pack(push,4).
#pragma pack(push, 4)
struct VRControlBlock {
    uint32_t version;               // 1
    uint32_t size;                  // sizeof(VRControlBlock)
    uint32_t vr_mode;               // 0=Gameplay, 1=Tuning
    uint32_t revision;              // incremented by writer on each change
    uint32_t win_headless;          // 0/1
    uint32_t win_always_on_top;     // 0/1
    uint32_t win_snap_cursor;       // reserved
    uint32_t menu_visible;          // Desktop VR Menu shown 0/1
    uint32_t quad_edit_mode;        // 0=off, 1=reposition, 2=transform
    uint32_t heartbeat;             // incremented every second by proxy; ViewLab detects disconnect
    float    quad_pos_x;
    float    quad_pos_y;
    float    quad_pos_z;
    float    quad_quat_x;
    float    quad_quat_y;
    float    quad_quat_z;
    float    quad_quat_w;
    float    quad_width;            // overlay width in meters
    float    quad_height;           // overlay height in meters
    float    quad_alpha;            // 0.0-1.0
    // Desktop VR Menu shared texture (Phase 4)
    uint64_t menu_texture_handle;   // DXGI NT shared handle (0 = none)
    uint32_t menu_texture_width;
    uint32_t menu_texture_height;
    uint32_t menu_texture_revision; // incremented each frame when menu is updated
    // Mouse input forwarding ViewLab -> game (Phase 5)
    float    mouse_x;               // normalised 0-1 within menu texture
    float    mouse_y;
    uint32_t mouse_buttons;         // bit 0 = LMB, bit 1 = RMB
    uint32_t mouse_revision;        // incremented on each mouse event
};
#pragma pack(pop)

// Proxy DLL calls this once on attach to create and own the control mapping.
bool InitSharedMemory();
void ShutdownSharedMemory();
VRControlBlock* GetVRBlock();

// Pixel buffer mapping for VrMenuWindow (Local\ViewLabVRMenuPixels).
// The proxy DLL writes BGRA32 pixels here; ViewLab reads them directly into WriteableBitmap.
static constexpr const wchar_t* VRMenuPixelMappingName = L"Local\\ViewLabVRMenuPixels";

bool  InitPixelMapping(uint32_t width, uint32_t height);
void  ShutdownPixelMapping();
void* GetPixelBuffer();   // returns pointer to the BGRA32 pixel bytes, or nullptr
