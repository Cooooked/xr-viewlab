#include <windows.h>

#include "OpenXrBackend.h"

#include <openxr/openxr.h>

#include <algorithm>
#include <cstdio>
#include <cstring>
#include <mutex>
#include <new>

namespace viewlab::bridge::legacy {
namespace {

void CopyText(char* destination, std::size_t capacity, const char* source) noexcept {
    if (!destination || capacity == 0) return;
    destination[0] = '\0';
    if (!source) return;
    strncpy_s(destination, capacity, source, _TRUNCATE);
}

void LogStatus(const BackendStatus& status, const char* operation) noexcept {
    wchar_t localAppData[MAX_PATH]{};
    if (!GetEnvironmentVariableW(L"LOCALAPPDATA", localAppData, MAX_PATH)) return;
    wchar_t directory[MAX_PATH]{};
    if (swprintf_s(directory, L"%s\\XR ViewLab", localAppData) <= 0) return;
    CreateDirectoryW(directory, nullptr);
    wchar_t path[MAX_PATH]{};
    if (swprintf_s(path, L"%s\\ViewLabBridge.log", directory) <= 0) return;
    FILE* file = nullptr;
    if (_wfopen_s(&file, path, L"a, ccs=UTF-8") != 0 || !file) return;
    SYSTEMTIME now{};
    GetLocalTime(&now);
    fwprintf(file, L"%04u-%02u-%02u %02u:%02u:%02u operation=%hs state=%hs xr=%d runtime=%hs system=%hs\n",
             now.wYear, now.wMonth, now.wDay, now.wHour, now.wMinute, now.wSecond,
             operation ? operation : "unknown", BackendStateName(status.state), status.xrResult,
             status.runtimeName[0] ? status.runtimeName : "unknown",
             status.systemName[0] ? status.systemName : "unknown");
    fclose(file);
}

vr::EVRInitError MapStatus(const BackendStatus& status) noexcept {
    switch (status.state) {
    case BackendState::Ready: return vr::VRInitError_None;
    case BackendState::LoaderUnavailable: return vr::VRInitError_Init_InstallationNotFound;
    case BackendState::HeadsetUnavailable: return vr::VRInitError_Init_HmdNotFound;
    default: return vr::VRInitError_Init_Internal;
    }
}

} // namespace

struct OpenXrBackend::Implementation {
    mutable std::mutex mutex;
    HMODULE loader = nullptr;
    XrInstance instance = XR_NULL_HANDLE;
    XrSystemId systemId = XR_NULL_SYSTEM_ID;
    PFN_xrGetInstanceProcAddr getInstanceProcAddr = nullptr;
    PFN_xrDestroyInstance destroyInstance = nullptr;
    BackendStatus status{};
    std::uint32_t clients = 0;

    void DestroyLocked() noexcept {
        if (instance != XR_NULL_HANDLE && destroyInstance) destroyInstance(instance);
        instance = XR_NULL_HANDLE;
        systemId = XR_NULL_SYSTEM_ID;
        destroyInstance = nullptr;
        getInstanceProcAddr = nullptr;
        if (loader) FreeLibrary(loader);
        loader = nullptr;
        status = {};
    }

    BackendStatus BootstrapLocked(bool retain) noexcept {
        if (status.state == BackendState::Ready && instance != XR_NULL_HANDLE) return status;
        DestroyLocked();

        loader = LoadLibraryExW(L"openxr_loader.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
        if (!loader) loader = LoadLibraryW(L"openxr_loader.dll");
        if (!loader) {
            status.state = BackendState::LoaderUnavailable;
            status.xrResult = static_cast<std::int32_t>(XR_ERROR_RUNTIME_UNAVAILABLE);
            return status;
        }

        getInstanceProcAddr = reinterpret_cast<PFN_xrGetInstanceProcAddr>(
            GetProcAddress(loader, "xrGetInstanceProcAddr"));
        auto createInstance = reinterpret_cast<PFN_xrCreateInstance>(
            GetProcAddress(loader, "xrCreateInstance"));
        if (!getInstanceProcAddr || !createInstance) {
            status.state = BackendState::LoaderUnavailable;
            status.xrResult = static_cast<std::int32_t>(XR_ERROR_FUNCTION_UNSUPPORTED);
            const BackendStatus resultStatus = status;
            if (!retain) DestroyLocked();
            return resultStatus;
        }

        XrInstanceCreateInfo createInfo{XR_TYPE_INSTANCE_CREATE_INFO};
        CopyText(createInfo.applicationInfo.applicationName,
                 sizeof(createInfo.applicationInfo.applicationName), "ViewLab Legacy Client");
        createInfo.applicationInfo.applicationVersion = 1;
        CopyText(createInfo.applicationInfo.engineName,
                 sizeof(createInfo.applicationInfo.engineName), "ViewLab Bridge");
        createInfo.applicationInfo.engineVersion = 1;
        createInfo.applicationInfo.apiVersion = XR_CURRENT_API_VERSION;
        XrResult result = createInstance(&createInfo, &instance);
        if (XR_FAILED(result)) {
            status.state = BackendState::InstanceUnavailable;
            status.xrResult = static_cast<std::int32_t>(result);
            const BackendStatus resultStatus = status;
            if (!retain) DestroyLocked();
            return resultStatus;
        }

        auto getSystem = static_cast<PFN_xrGetSystem>(nullptr);
        auto getInstanceProperties = static_cast<PFN_xrGetInstanceProperties>(nullptr);
        auto getSystemProperties = static_cast<PFN_xrGetSystemProperties>(nullptr);
        getInstanceProcAddr(instance, "xrDestroyInstance",
                            reinterpret_cast<PFN_xrVoidFunction*>(&destroyInstance));
        getInstanceProcAddr(instance, "xrGetSystem", reinterpret_cast<PFN_xrVoidFunction*>(&getSystem));
        getInstanceProcAddr(instance, "xrGetInstanceProperties",
                            reinterpret_cast<PFN_xrVoidFunction*>(&getInstanceProperties));
        getInstanceProcAddr(instance, "xrGetSystemProperties",
                            reinterpret_cast<PFN_xrVoidFunction*>(&getSystemProperties));
        if (!destroyInstance || !getSystem) {
            status.state = BackendState::InstanceUnavailable;
            status.xrResult = static_cast<std::int32_t>(XR_ERROR_FUNCTION_UNSUPPORTED);
            const BackendStatus resultStatus = status;
            if (!retain) DestroyLocked();
            return resultStatus;
        }

        if (getInstanceProperties) {
            XrInstanceProperties properties{XR_TYPE_INSTANCE_PROPERTIES};
            if (XR_SUCCEEDED(getInstanceProperties(instance, &properties)))
                CopyText(status.runtimeName, sizeof(status.runtimeName), properties.runtimeName);
        }

        const XrSystemGetInfo systemInfo{XR_TYPE_SYSTEM_GET_INFO, nullptr,
                                         XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY};
        result = getSystem(instance, &systemInfo, &systemId);
        if (XR_FAILED(result)) {
            status.state = result == XR_ERROR_FORM_FACTOR_UNAVAILABLE
                               ? BackendState::HeadsetUnavailable
                               : BackendState::InstanceUnavailable;
            status.xrResult = static_cast<std::int32_t>(result);
            const BackendStatus resultStatus = status;
            if (!retain) DestroyLocked();
            return resultStatus;
        }

        if (getSystemProperties) {
            XrSystemProperties properties{XR_TYPE_SYSTEM_PROPERTIES};
            if (XR_SUCCEEDED(getSystemProperties(instance, systemId, &properties)))
                CopyText(status.systemName, sizeof(status.systemName), properties.systemName);
        }
        status.state = BackendState::Ready;
        status.xrResult = static_cast<std::int32_t>(XR_SUCCESS);
        const BackendStatus resultStatus = status;
        if (!retain) DestroyLocked();
        return resultStatus;
    }
};

OpenXrBackend& OpenXrBackend::Instance() noexcept {
    static OpenXrBackend backend;
    return backend;
}

vr::EVRInitError OpenXrBackend::Initialise(vr::EVRApplicationType, const char*) noexcept {
    if (!implementation_) implementation_ = new (std::nothrow) Implementation();
    if (!implementation_) return vr::VRInitError_Init_Internal;
    std::lock_guard<std::mutex> lock(implementation_->mutex);
    const BackendStatus status = implementation_->BootstrapLocked(true);
    if (status.state == BackendState::Ready) ++implementation_->clients;
    LogStatus(status, "initialise");
    return MapStatus(status);
}

void OpenXrBackend::Cleanup() noexcept {
    if (!implementation_) return;
    std::lock_guard<std::mutex> lock(implementation_->mutex);
    if (implementation_->clients > 0) --implementation_->clients;
    if (implementation_->clients == 0) implementation_->DestroyLocked();
}

bool OpenXrBackend::IsHeadsetPresent() noexcept {
    if (!implementation_) implementation_ = new (std::nothrow) Implementation();
    if (!implementation_) return false;
    std::lock_guard<std::mutex> lock(implementation_->mutex);
    const bool retain = implementation_->clients > 0;
    const BackendStatus status = implementation_->BootstrapLocked(retain);
    LogStatus(status, "headset-probe");
    return status.state == BackendState::Ready;
}

BackendStatus OpenXrBackend::Status() const noexcept {
    if (!implementation_) return {};
    std::lock_guard<std::mutex> lock(implementation_->mutex);
    return implementation_->status;
}

const char* BackendStateName(BackendState state) noexcept {
    switch (state) {
    case BackendState::LoaderUnavailable: return "loader-unavailable";
    case BackendState::InstanceUnavailable: return "instance-unavailable";
    case BackendState::HeadsetUnavailable: return "headset-unavailable";
    case BackendState::Ready: return "ready";
    default: return "uninitialised";
    }
}

} // namespace viewlab::bridge::legacy
