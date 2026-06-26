#include "pch_vr.h"
#include "shared_memory.h"
#include "log.h"

namespace {

HANDLE            g_hMap  = nullptr;
VRControlBlock*   g_block = nullptr;
std::thread       g_heartbeatThread;
std::atomic<bool> g_shutdown{false};

void HeartbeatThread() {
    while (!g_shutdown.load(std::memory_order_relaxed)) {
        if (g_block) {
            // Atomic-ish: single 32-bit write is effectively atomic on x86/x64.
            g_block->heartbeat++;
        }
        std::this_thread::sleep_for(std::chrono::seconds(1));
    }
}

} // namespace

bool InitSharedMemory() {
    const DWORD sz = static_cast<DWORD>(sizeof(VRControlBlock));
    g_hMap = CreateFileMappingW(
        INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, sz, VRControlMappingName);

    if (!g_hMap) {
        VrLog("InitSharedMemory: CreateFileMappingW failed (%lu)", GetLastError());
        return false;
    }

    // ERROR_ALREADY_EXISTS is acceptable — we re-use the existing mapping.
    const bool existed = (GetLastError() == ERROR_ALREADY_EXISTS);

    g_block = static_cast<VRControlBlock*>(
        MapViewOfFile(g_hMap, FILE_MAP_ALL_ACCESS, 0, 0, sz));

    if (!g_block) {
        VrLog("InitSharedMemory: MapViewOfFile failed (%lu)", GetLastError());
        CloseHandle(g_hMap);
        g_hMap = nullptr;
        return false;
    }

    if (!existed) {
        // Fresh mapping — zero and initialise.
        ZeroMemory(g_block, sz);
        g_block->version          = 1;
        g_block->size             = sz;
        g_block->quad_quat_w      = 1.0f;   // identity quaternion
        g_block->quad_width       = 1.0f;
        g_block->quad_height      = 0.5625f; // 16:9 default
        g_block->quad_alpha       = 1.0f;
        g_block->quad_pos_z       = -1.0f;  // 1 m in front
        g_block->menu_texture_handle = 0;
    }

    VrLog("InitSharedMemory: OK (existed=%d, size=%u)", (int)existed, sz);

    g_shutdown = false;
    g_heartbeatThread = std::thread(HeartbeatThread);
    return true;
}

void ShutdownSharedMemory() {
    g_shutdown = true;
    if (g_heartbeatThread.joinable()) g_heartbeatThread.join();
    if (g_block) { UnmapViewOfFile(g_block); g_block = nullptr; }
    if (g_hMap)  { CloseHandle(g_hMap);      g_hMap  = nullptr; }
}

VRControlBlock* GetVRBlock() { return g_block; }

// ---------------------------------------------------------------------------
// Pixel buffer mapping for VrMenuWindow
// ---------------------------------------------------------------------------
namespace {
HANDLE g_hPixelMap  = nullptr;
void*  g_pixelBuf   = nullptr;
uint32_t g_pixelW   = 0;
uint32_t g_pixelH   = 0;
} // namespace

bool InitPixelMapping(uint32_t width, uint32_t height) {
    ShutdownPixelMapping();
    const DWORD bytes = width * height * 4;
    g_hPixelMap = CreateFileMappingW(
        INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, bytes, VRMenuPixelMappingName);
    if (!g_hPixelMap) {
        VrLog("InitPixelMapping: CreateFileMappingW failed (%lu)", GetLastError());
        return false;
    }
    g_pixelBuf = MapViewOfFile(g_hPixelMap, FILE_MAP_ALL_ACCESS, 0, 0, bytes);
    if (!g_pixelBuf) {
        VrLog("InitPixelMapping: MapViewOfFile failed (%lu)", GetLastError());
        CloseHandle(g_hPixelMap); g_hPixelMap = nullptr;
        return false;
    }
    g_pixelW = width; g_pixelH = height;
    VrLog("InitPixelMapping: OK (%u x %u)", width, height);
    return true;
}

void ShutdownPixelMapping() {
    if (g_pixelBuf)   { UnmapViewOfFile(g_pixelBuf); g_pixelBuf = nullptr; }
    if (g_hPixelMap)  { CloseHandle(g_hPixelMap);    g_hPixelMap = nullptr; }
    g_pixelW = g_pixelH = 0;
}

void* GetPixelBuffer() { return g_pixelBuf; }
