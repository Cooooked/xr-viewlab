#pragma once

#include <cstdint>

#include "../../ThirdParty/OpenVR/openvr.h"

namespace viewlab::bridge::legacy {

enum class BackendState : std::uint8_t {
    Uninitialised,
    LoaderUnavailable,
    InstanceUnavailable,
    HeadsetUnavailable,
    Ready,
};

struct BackendStatus {
    BackendState state = BackendState::Uninitialised;
    std::int32_t xrResult = 0;
    char runtimeName[128]{};
    char systemName[256]{};
};

// Owns the OpenXR bootstrap lifetime for the legacy ABI. OpenVR-facing code delegates here rather
// than learning about loaders, runtimes or system selection. Graphics/session/compositor ownership
// will extend this boundary; the public ViewLab layer remains independent.
class OpenXrBackend final {
public:
    static OpenXrBackend& Instance() noexcept;

    vr::EVRInitError Initialise(vr::EVRApplicationType applicationType,
                                const char* startupInfo) noexcept;
    void Cleanup() noexcept;
    bool IsHeadsetPresent() noexcept;
    BackendStatus Status() const noexcept;

private:
    OpenXrBackend() = default;
    OpenXrBackend(const OpenXrBackend&) = delete;
    OpenXrBackend& operator=(const OpenXrBackend&) = delete;

    struct Implementation;
    Implementation* implementation_ = nullptr;
};

const char* BackendStateName(BackendState state) noexcept;

} // namespace viewlab::bridge::legacy
