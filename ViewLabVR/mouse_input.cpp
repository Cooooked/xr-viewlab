#include "pch_vr.h"
#include "mouse_input.h"
#include "shared_memory.h"
#include "log.h"

namespace {

std::thread       g_mouseThread;
std::atomic<bool> g_mouseShutdown{false};
uint32_t          g_lastMouseRevision = 0;

// Find ReShade's ImGui host window inside the current process.
HWND FindReShadeWindow() {
    struct FindCtx { DWORD pid; HWND result; };
    FindCtx ctx{ GetCurrentProcessId(), nullptr };

    EnumWindows([](HWND hwnd, LPARAM lp) -> BOOL {
        auto* ctx = reinterpret_cast<FindCtx*>(lp);
        DWORD pid = 0;
        GetWindowThreadProcessId(hwnd, &pid);
        if (pid != ctx->pid) return TRUE;

        char cls[128]{};
        GetClassNameA(hwnd, cls, sizeof(cls));
        if (strcmp(cls, "ReShade") == 0) {
            ctx->result = hwnd;
            return FALSE;
        }
        return TRUE;
    }, reinterpret_cast<LPARAM>(&ctx));

    return ctx.result;
}

void ForwardMouseInput(HWND reshadeWnd, float nx, float ny, uint32_t buttons) {
    RECT rc{};
    GetClientRect(reshadeWnd, &rc);
    const int px = static_cast<int>(nx * (rc.right  - rc.left));
    const int py = static_cast<int>(ny * (rc.bottom - rc.top));
    const LPARAM lp = MAKELPARAM(px, py);

    PostMessageA(reshadeWnd, WM_MOUSEMOVE, 0, lp);

    if (buttons & 1)
        PostMessageA(reshadeWnd, WM_LBUTTONDOWN, MK_LBUTTON, lp);
    else
        PostMessageA(reshadeWnd, WM_LBUTTONUP, 0, lp);

    if (buttons & 2)
        PostMessageA(reshadeWnd, WM_RBUTTONDOWN, MK_RBUTTON, lp);
    else
        PostMessageA(reshadeWnd, WM_RBUTTONUP, 0, lp);
}

void MousePollThread() {
    HWND reshadeWnd = nullptr;

    while (!g_mouseShutdown.load(std::memory_order_relaxed)) {
        std::this_thread::sleep_for(std::chrono::milliseconds(8));

        const VRControlBlock* block = GetVRBlock();
        if (!block || block->menu_visible == 0) continue;

        const uint32_t rev = block->mouse_revision;
        if (rev == g_lastMouseRevision) continue;
        g_lastMouseRevision = rev;

        // Re-find window lazily
        if (!reshadeWnd || !IsWindow(reshadeWnd))
            reshadeWnd = FindReShadeWindow();
        if (!reshadeWnd) continue;

        ForwardMouseInput(reshadeWnd, block->mouse_x, block->mouse_y, block->mouse_buttons);
    }
}

} // namespace

void InitMouseInput() {
    g_mouseShutdown = false;
    g_mouseThread = std::thread(MousePollThread);
}

void ShutdownMouseInput() {
    g_mouseShutdown = true;
    if (g_mouseThread.joinable()) g_mouseThread.join();
}
