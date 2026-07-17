#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include "../../ViewLabBridge/LegacyRuntime/ClientCoreAbi.h"

#include <cstring>
#include <iostream>

namespace {
using Factory = void* (*)(const char*, int*);

int Fail(const char* message) {
    std::cerr << "FAIL: " << message << '\n';
    return 1;
}
}

int main(int argc, char** argv) {
    if (argc != 2) return Fail("expected the bridge DLL path");
    HMODULE module = LoadLibraryA(argv[1]);
    if (!module) return Fail("bridge DLL did not load");
    auto factory = reinterpret_cast<Factory>(GetProcAddress(module, "VRClientCoreFactory"));
    if (!factory) return Fail("VRClientCoreFactory export is missing");

    int factoryError = -1;
    auto* core = static_cast<viewlab::bridge::legacy::IClientCore003*>(
        factory(viewlab::bridge::legacy::ClientCore003Version, &factoryError));
    if (!core || factoryError != static_cast<int>(vr::VRInitError_None))
        return Fail("IVRClientCore_003 factory contract failed");
    if (core->IsInterfaceVersionValid(viewlab::bridge::legacy::ClientCore003Version) !=
        vr::VRInitError_None)
        return Fail("bootstrap interface validation failed");
    if (core->IsInterfaceVersionValid("IVRNotARealInterface_999") == vr::VRInitError_None)
        return Fail("unknown interface was accepted");

    const bool headsetPresent = core->BIsHmdPresent();
    const vr::EVRInitError init = core->Init(vr::VRApplication_Scene, "fixture");
    if (headsetPresent && init != vr::VRInitError_None)
        return Fail("headset probe succeeded but initialisation failed");
    const char* english = core->GetEnglishStringForHmdError(init);
    const char* symbol = core->GetIDForVRInitError(init);
    if (!english || !*english || !symbol || !*symbol) return Fail("error diagnostics are empty");
    if (init == vr::VRInitError_None) core->Cleanup();

    std::cout << "PASS: factory=IVRClientCore_003 headset=" << (headsetPresent ? "present" : "absent")
              << " init=" << static_cast<int>(init) << " " << symbol << '\n';
    FreeLibrary(module);
    return 0;
}
