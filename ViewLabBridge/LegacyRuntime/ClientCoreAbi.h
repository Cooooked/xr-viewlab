#pragma once

#include "../../ThirdParty/OpenVR/openvr.h"

namespace viewlab::bridge::legacy {

// vrclient's private bootstrap interfaces are intentionally isolated here. Their vtable contracts
// follow the stable IVRClientCore_002/003 definitions used by Valve's client loader and by the
// OpenComposite baseline. Product code outside LegacyRuntime must not depend on these types.
class IClientCore003 {
public:
    virtual vr::EVRInitError Init(vr::EVRApplicationType applicationType,
                                  const char* startupInfo) = 0;
    virtual void Cleanup() = 0;
    virtual vr::EVRInitError IsInterfaceVersionValid(const char* interfaceVersion) = 0;
    virtual void* GetGenericInterface(const char* nameAndVersion, vr::EVRInitError* error) = 0;
    virtual bool BIsHmdPresent() = 0;
    virtual const char* GetEnglishStringForHmdError(vr::EVRInitError error) = 0;
    virtual const char* GetIDForVRInitError(vr::EVRInitError error) = 0;
};

class IClientCore002 {
public:
    virtual vr::EVRInitError Init(vr::EVRApplicationType applicationType) = 0;
    virtual void Cleanup() = 0;
    virtual vr::EVRInitError IsInterfaceVersionValid(const char* interfaceVersion) = 0;
    virtual void* GetGenericInterface(const char* nameAndVersion, vr::EVRInitError* error) = 0;
    virtual bool BIsHmdPresent() = 0;
    virtual const char* GetEnglishStringForHmdError(vr::EVRInitError error) = 0;
    virtual const char* GetIDForVRInitError(vr::EVRInitError error) = 0;
};

inline constexpr char ClientCore003Version[] = "IVRClientCore_003";
inline constexpr char ClientCore002Version[] = "IVRClientCore_002";

} // namespace viewlab::bridge::legacy

extern "C" __declspec(dllexport) void* VRClientCoreFactory(const char* interfaceName,
                                                            int* returnCode);
