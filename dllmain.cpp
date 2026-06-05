#include "pch.h"

namespace {
constexpr const char* LayerName = "XR_APILAYER_cooooked_verticaltangent";
constexpr const wchar_t* ConfigFileName = L"openxr-verticaltangent.ini";
constexpr double DefaultTopTangent = 0.09;
constexpr double DefaultBottomTangent = 0.09;
constexpr double MinVerticalTangent = 0.0;
constexpr double MaxVerticalTangent = 1.0;

std::filesystem::path layerDirectory;
std::ofstream logStream;

bool enabled = true;
double topTangent = DefaultTopTangent;
double bottomTangent = DefaultBottomTangent;

PFN_xrGetInstanceProcAddr nextXrGetInstanceProcAddr = nullptr;
PFN_xrLocateViews nextXrLocateViews = nullptr;
PFN_xrEnumerateViewConfigurationViews nextXrEnumerateViewConfigurationViews = nullptr;

std::atomic<uint32_t> locateViewsLogCount{0};
std::atomic<uint32_t> enumerateViewsLogCount{0};

void Log(const char* fmt, ...) {
    char buffer[1024]{};

    va_list args;
    va_start(args, fmt);
    _vsnprintf_s(buffer, sizeof(buffer), _TRUNCATE, fmt, args);
    va_end(args);

    OutputDebugStringA(buffer);
    if (logStream.is_open()) {
        logStream << buffer;
        logStream.flush();
    }
}

std::filesystem::path GetLayerDirectory() {
    HMODULE module = nullptr;
    if (!GetModuleHandleExA(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&GetLayerDirectory),
            &module)) {
        return {};
    }

    wchar_t path[MAX_PATH]{};
    if (GetModuleFileNameW(module, path, static_cast<DWORD>(std::size(path))) == 0) {
        return {};
    }

    return std::filesystem::path(path).parent_path();
}

std::filesystem::path ConfigPath() {
    return layerDirectory / ConfigFileName;
}

bool ReadBoolSetting(const wchar_t* key, bool fallback) {
    wchar_t buffer[32]{};
    GetPrivateProfileStringW(L"Settings", key, fallback ? L"1" : L"0", buffer, static_cast<DWORD>(std::size(buffer)), ConfigPath().c_str());

    return _wcsicmp(buffer, L"1") == 0 ||
           _wcsicmp(buffer, L"true") == 0 ||
           _wcsicmp(buffer, L"yes") == 0 ||
           _wcsicmp(buffer, L"on") == 0;
}

double ReadDoubleSetting(const wchar_t* key, double fallback) {
    wchar_t buffer[64]{};
    GetPrivateProfileStringW(L"Settings", key, L"", buffer, static_cast<DWORD>(std::size(buffer)), ConfigPath().c_str());

    wchar_t* end = nullptr;
    const double value = std::wcstod(buffer, &end);
    if (end == buffer || !std::isfinite(value)) {
        return fallback;
    }

    return value;
}

void LoadConfig() {
    enabled = ReadBoolSetting(L"enabled", true);
    const double legacyTotal = ReadDoubleSetting(L"vertical_tangent", DefaultTopTangent + DefaultBottomTangent);
    topTangent = std::clamp(
        ReadDoubleSetting(L"top_tangent", legacyTotal * 0.5),
        MinVerticalTangent,
        MaxVerticalTangent);
    bottomTangent = std::clamp(
        ReadDoubleSetting(L"bottom_tangent", legacyTotal * 0.5),
        MinVerticalTangent,
        MaxVerticalTangent);

    Log("config: enabled=%d top_tangent=%.3f bottom_tangent=%.3f total_tangent=%.3f\n",
        enabled ? 1 : 0,
        topTangent,
        bottomTangent,
        topTangent + bottomTangent);
}

void EnsureInitialized() {
    if (layerDirectory.empty()) {
        layerDirectory = GetLayerDirectory();
    }

    if (!logStream.is_open() && !layerDirectory.empty()) {
        logStream.open(layerDirectory / L"openxr-verticaltangent.log", std::ios_base::app);
    }

    LoadConfig();
}

void ApplyVerticalTangentSplit(XrFovf& fov) {
    fov.angleUp = static_cast<float>(std::atan(topTangent));
    fov.angleDown = -static_cast<float>(std::atan(bottomTangent));
}

XRAPI_ATTR XrResult XRAPI_CALL VerticalTangent_xrLocateViews(
    XrSession session,
    const XrViewLocateInfo* viewLocateInfo,
    XrViewState* viewState,
    uint32_t viewCapacityInput,
    uint32_t* viewCountOutput,
    XrView* views) {
    const XrResult result = nextXrLocateViews(session, viewLocateInfo, viewState, viewCapacityInput, viewCountOutput, views);

    if (!enabled ||
        result != XR_SUCCESS ||
        viewLocateInfo == nullptr ||
        viewCountOutput == nullptr ||
        views == nullptr ||
        viewLocateInfo->viewConfigurationType != XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO) {
        return result;
    }

    const uint32_t logCount = locateViewsLogCount.fetch_add(1);
    for (uint32_t i = 0; i < *viewCountOutput; ++i) {
        const XrFovf before = views[i].fov;
        ApplyVerticalTangentSplit(views[i].fov);
        if (logCount < 20 || logCount % 300 == 0) {
            Log("xrLocateViews[%u]: up %.5f -> %.5f down %.5f -> %.5f left %.5f right %.5f\n",
                i,
                before.angleUp,
                views[i].fov.angleUp,
                before.angleDown,
                views[i].fov.angleDown,
                views[i].fov.angleLeft,
                views[i].fov.angleRight);
        }
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL VerticalTangent_xrEnumerateViewConfigurationViews(
    XrInstance instance,
    XrSystemId systemId,
    XrViewConfigurationType viewConfigurationType,
    uint32_t viewCapacityInput,
    uint32_t* viewCountOutput,
    XrViewConfigurationView* views) {
    const XrResult result = nextXrEnumerateViewConfigurationViews(instance, systemId, viewConfigurationType, viewCapacityInput, viewCountOutput, views);

    if (!enabled ||
        result != XR_SUCCESS ||
        viewConfigurationType != XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO ||
        viewCountOutput == nullptr ||
        views == nullptr) {
        return result;
    }

    PFN_xrGetViewConfigurationProperties getProperties = nullptr;
    if (nextXrGetInstanceProcAddr(
            instance,
            "xrGetViewConfigurationProperties",
            reinterpret_cast<PFN_xrVoidFunction*>(&getProperties)) != XR_SUCCESS ||
        getProperties == nullptr) {
        return result;
    }

    XrViewConfigurationProperties properties{XR_TYPE_VIEW_CONFIGURATION_PROPERTIES};
    if (getProperties(instance, systemId, viewConfigurationType, &properties) != XR_SUCCESS ||
        properties.fovMutable != XR_TRUE) {
        return result;
    }

    const uint32_t logCount = enumerateViewsLogCount.fetch_add(1);
    const double renderHeightScale = std::clamp(topTangent + bottomTangent, 0.01, 1.0);
    for (uint32_t i = 0; i < *viewCountOutput; ++i) {
        const uint32_t beforeHeight = views[i].recommendedImageRectHeight;
        views[i].recommendedImageRectHeight = std::max<uint32_t>(
            1,
            static_cast<uint32_t>(std::lround(static_cast<double>(views[i].recommendedImageRectHeight) * renderHeightScale)));
        if (logCount < 10 || logCount % 100 == 0) {
            Log("xrEnumerateViewConfigurationViews[%u]: recommended height %u -> %u total_tangent=%.3f\n",
                i,
                beforeHeight,
                views[i].recommendedImageRectHeight,
                renderHeightScale);
        }
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL VerticalTangent_xrGetInstanceProcAddr(
    XrInstance instance,
    const char* name,
    PFN_xrVoidFunction* function) {
    const XrResult result = nextXrGetInstanceProcAddr(instance, name, function);

    if (result != XR_SUCCESS || name == nullptr || function == nullptr || *function == nullptr) {
        return result;
    }

    if (std::strcmp(name, "xrLocateViews") == 0) {
        nextXrLocateViews = reinterpret_cast<PFN_xrLocateViews>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(VerticalTangent_xrLocateViews);
    } else if (std::strcmp(name, "xrEnumerateViewConfigurationViews") == 0) {
        nextXrEnumerateViewConfigurationViews = reinterpret_cast<PFN_xrEnumerateViewConfigurationViews>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(VerticalTangent_xrEnumerateViewConfigurationViews);
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL VerticalTangent_xrCreateApiLayerInstance(
    const XrInstanceCreateInfo* instanceCreateInfo,
    const XrApiLayerCreateInfo* apiLayerInfo,
    XrInstance* instance) {
    if (!apiLayerInfo ||
        apiLayerInfo->structType != XR_LOADER_INTERFACE_STRUCT_API_LAYER_CREATE_INFO ||
        apiLayerInfo->structVersion != XR_API_LAYER_CREATE_INFO_STRUCT_VERSION ||
        apiLayerInfo->structSize != sizeof(XrApiLayerCreateInfo) ||
        !apiLayerInfo->nextInfo ||
        apiLayerInfo->nextInfo->structType != XR_LOADER_INTERFACE_STRUCT_API_LAYER_NEXT_INFO ||
        apiLayerInfo->nextInfo->structVersion != XR_API_LAYER_NEXT_INFO_STRUCT_VERSION ||
        apiLayerInfo->nextInfo->structSize != sizeof(XrApiLayerNextInfo) ||
        apiLayerInfo->nextInfo->layerName == nullptr ||
        std::strcmp(apiLayerInfo->nextInfo->layerName, LayerName) != 0 ||
        !apiLayerInfo->nextInfo->nextGetInstanceProcAddr ||
        !apiLayerInfo->nextInfo->nextCreateApiLayerInstance) {
        Log("xrCreateApiLayerInstance validation failed\n");
        return XR_ERROR_INITIALIZATION_FAILED;
    }

    nextXrGetInstanceProcAddr = apiLayerInfo->nextInfo->nextGetInstanceProcAddr;

    XrApiLayerCreateInfo chainedApiLayerInfo = *apiLayerInfo;
    chainedApiLayerInfo.nextInfo = apiLayerInfo->nextInfo->next;

    const XrResult result = apiLayerInfo->nextInfo->nextCreateApiLayerInstance(instanceCreateInfo, &chainedApiLayerInfo, instance);
    const char* appName = instanceCreateInfo ? instanceCreateInfo->applicationInfo.applicationName : "<unknown>";
    Log("%s for %s\n", enabled ? "enabled" : "disabled", appName);
    return result;
}
}

extern "C" {
XrResult __declspec(dllexport) XRAPI_CALL cooooked_verticaltangent_overlaytest_xrNegotiateLoaderApiLayerInterface(
    const XrNegotiateLoaderInfo* loaderInfo,
    const char* apiLayerName,
    XrNegotiateApiLayerRequest* apiLayerRequest) {
    EnsureInitialized();

    if (apiLayerName == nullptr || std::strcmp(apiLayerName, LayerName) != 0) {
        Log("invalid apiLayerName: %s\n", apiLayerName ? apiLayerName : "<null>");
        return XR_ERROR_INITIALIZATION_FAILED;
    }

    if (!loaderInfo ||
        !apiLayerRequest ||
        loaderInfo->structType != XR_LOADER_INTERFACE_STRUCT_LOADER_INFO ||
        loaderInfo->structVersion != XR_LOADER_INFO_STRUCT_VERSION ||
        loaderInfo->structSize != sizeof(XrNegotiateLoaderInfo) ||
        apiLayerRequest->structType != XR_LOADER_INTERFACE_STRUCT_API_LAYER_REQUEST ||
        apiLayerRequest->structVersion != XR_API_LAYER_INFO_STRUCT_VERSION ||
        apiLayerRequest->structSize != sizeof(XrNegotiateApiLayerRequest) ||
        loaderInfo->minInterfaceVersion > XR_CURRENT_LOADER_API_LAYER_VERSION ||
        loaderInfo->maxInterfaceVersion < XR_CURRENT_LOADER_API_LAYER_VERSION ||
        loaderInfo->maxApiVersion < XR_CURRENT_API_VERSION ||
        loaderInfo->minApiVersion > XR_CURRENT_API_VERSION) {
        Log("xrNegotiateLoaderApiLayerInterface validation failed\n");
        return XR_ERROR_INITIALIZATION_FAILED;
    }

    apiLayerRequest->layerInterfaceVersion = XR_CURRENT_LOADER_API_LAYER_VERSION;
    apiLayerRequest->layerApiVersion = XR_CURRENT_API_VERSION;
    apiLayerRequest->getInstanceProcAddr = reinterpret_cast<PFN_xrGetInstanceProcAddr>(VerticalTangent_xrGetInstanceProcAddr);
    apiLayerRequest->createApiLayerInstance = reinterpret_cast<PFN_xrCreateApiLayerInstance>(VerticalTangent_xrCreateApiLayerInstance);
    return XR_SUCCESS;
}
}