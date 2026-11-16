#pragma once
namespace viewlab::panels {
// Thread-local and nest-safe: unrelated/forwarded swapchains never inherit ownership.
class NativePanelCreationScope {
    inline static thread_local unsigned depth = 0;
public:
    NativePanelCreationScope() noexcept { ++depth; }
    ~NativePanelCreationScope() { --depth; }
    NativePanelCreationScope(const NativePanelCreationScope&) = delete;
    NativePanelCreationScope& operator=(const NativePanelCreationScope&) = delete;
    static bool Active() noexcept { return depth != 0; }
};
}
