#include "ClientCoreAbi.h"
#include "OpenXrBackend.h"

#include <cstring>

namespace viewlab::bridge::legacy {
namespace {

bool IsBootstrapInterface(const char* version) noexcept {
    return version && (std::strcmp(version,ClientCore003Version)==0 ||
                       std::strcmp(version,ClientCore002Version)==0);
}

vr::EVRInitError ValidateInterface(const char* version) noexcept {
    return IsBootstrapInterface(version) ? vr::VRInitError_None
                                         : vr::VRInitError_Init_InvalidInterface;
}

void* MissingInterface(vr::EVRInitError* error) noexcept {
    if(error) *error=vr::VRInitError_Init_InterfaceNotFound;
    return nullptr;
}

const char* ErrorEnglish(vr::EVRInitError error) noexcept {
    switch(error) {
    case vr::VRInitError_None: return "No error";
    case vr::VRInitError_Init_HmdNotFound: return "The active OpenXR runtime cannot currently find a headset";
    case vr::VRInitError_Init_InstallationNotFound: return "The OpenXR loader or active runtime is unavailable";
    case vr::VRInitError_Init_InterfaceNotFound: return "Requested legacy interface is not implemented";
    case vr::VRInitError_Init_InvalidInterface: return "Requested legacy interface version is invalid";
    default: return "ViewLab Bridge initialization error";
    }
}

const char* ErrorSymbol(vr::EVRInitError error) noexcept {
    switch(error) {
    case vr::VRInitError_None: return "VRInitError_None";
    case vr::VRInitError_Init_HmdNotFound: return "VRInitError_Init_HmdNotFound";
    case vr::VRInitError_Init_InstallationNotFound: return "VRInitError_Init_InstallationNotFound";
    case vr::VRInitError_Init_InterfaceNotFound: return "VRInitError_Init_InterfaceNotFound";
    case vr::VRInitError_Init_InvalidInterface: return "VRInitError_Init_InvalidInterface";
    default: return "VRInitError_Unknown";
    }
}

class ClientCore003 final : public IClientCore003 {
public:
    vr::EVRInitError Init(vr::EVRApplicationType applicationType, const char* startupInfo) override {
        return OpenXrBackend::Instance().Initialise(applicationType, startupInfo);
    }
    void Cleanup() override { OpenXrBackend::Instance().Cleanup(); }
    vr::EVRInitError IsInterfaceVersionValid(const char* version) override { return ValidateInterface(version); }
    void* GetGenericInterface(const char*, vr::EVRInitError* error) override { return MissingInterface(error); }
    bool BIsHmdPresent() override { return OpenXrBackend::Instance().IsHeadsetPresent(); }
    const char* GetEnglishStringForHmdError(vr::EVRInitError error) override { return ErrorEnglish(error); }
    const char* GetIDForVRInitError(vr::EVRInitError error) override { return ErrorSymbol(error); }
};

class ClientCore002 final : public IClientCore002 {
public:
    vr::EVRInitError Init(vr::EVRApplicationType applicationType) override {
        return OpenXrBackend::Instance().Initialise(applicationType, nullptr);
    }
    void Cleanup() override { OpenXrBackend::Instance().Cleanup(); }
    vr::EVRInitError IsInterfaceVersionValid(const char* version) override { return ValidateInterface(version); }
    void* GetGenericInterface(const char*, vr::EVRInitError* error) override { return MissingInterface(error); }
    bool BIsHmdPresent() override { return OpenXrBackend::Instance().IsHeadsetPresent(); }
    const char* GetEnglishStringForHmdError(vr::EVRInitError error) override { return ErrorEnglish(error); }
    const char* GetIDForVRInitError(vr::EVRInitError error) override { return ErrorSymbol(error); }
};

ClientCore003 g_clientCore003;
ClientCore002 g_clientCore002;

} // namespace
} // namespace viewlab::bridge::legacy

extern "C" __declspec(dllexport) void* VRClientCoreFactory(const char* interfaceName,
                                                            int* returnCode) {
    using namespace viewlab::bridge::legacy;
    if(returnCode) *returnCode=static_cast<int>(vr::VRInitError_Init_InvalidInterface);
    if(!interfaceName) return nullptr;
    if(std::strcmp(interfaceName,ClientCore003Version)==0) {
        if(returnCode) *returnCode=static_cast<int>(vr::VRInitError_None);
        return &g_clientCore003;
    }
    if(std::strcmp(interfaceName,ClientCore002Version)==0) {
        if(returnCode) *returnCode=static_cast<int>(vr::VRInitError_None);
        return &g_clientCore002;
    }
    return nullptr;
}
