#include "pch.h"

namespace {
constexpr const char* LayerName = "XR_APILAYER_cooooked_xrviewlab";
constexpr const wchar_t* ConfigFileName = L"xr-viewlab.ini";
constexpr const wchar_t* AppRegistryRoot = L"Software\\cooooked\\xr-viewlab\\Apps";
constexpr double DefaultTotalTangent = 0.18;
constexpr double DefaultTopTangent = 0.09;
constexpr double DefaultBottomTangent = 0.09;
constexpr double MinVerticalTangent = 0.0;
constexpr double MaxVerticalTangent = 1.0;

std::filesystem::path layerDirectory;
std::ofstream logStream;

bool enabled = true;
bool splitMode = false;
bool foveatedCenterCompensation = false;
double totalTangent = DefaultTotalTangent;
double topTangent = DefaultTopTangent;
double bottomTangent = DefaultBottomTangent;
std::wstring currentAppKey;

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

std::filesystem::path LegacyConfigPath() {
    return layerDirectory / ConfigFileName;
}

std::filesystem::path UserConfigPath() {
    wchar_t localAppData[MAX_PATH]{};
    const DWORD length = GetEnvironmentVariableW(L"LOCALAPPDATA", localAppData, static_cast<DWORD>(std::size(localAppData)));
    if (length == 0 || length >= std::size(localAppData)) {
        return LegacyConfigPath();
    }

    return std::filesystem::path(localAppData) / L"XR ViewLab" / ConfigFileName;
}

std::filesystem::path ConfigPath() {
    const std::filesystem::path userConfig = UserConfigPath();
    std::error_code ec;
    if (std::filesystem::exists(userConfig, ec)) {
        return userConfig;
    }

    return LegacyConfigPath();
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

std::wstring CurrentProcessPath() {
    wchar_t path[MAX_PATH]{};
    if (GetModuleFileNameW(nullptr, path, static_cast<DWORD>(std::size(path))) == 0) {
        return {};
    }

    return path;
}

std::wstring CurrentProcessFileName() {
    const std::filesystem::path path(CurrentProcessPath());
    const std::wstring name = path.filename().wstring();
    return name.empty() ? L"Unknown.exe" : name;
}

std::wstring SanitizeRegistryKey(std::wstring value) {
    if (value.empty()) {
        return L"Unknown.exe";
    }

    for (wchar_t& ch : value) {
        if (ch == L'\\' || ch == L'/' || ch == L':' || ch == L'*' || ch == L'?' || ch == L'"' || ch == L'<' || ch == L'>' || ch == L'|') {
            ch = L'_';
        }
    }

    return value;
}

std::wstring Utf8ToWide(const char* value) {
    if (value == nullptr || value[0] == '\0') {
        return {};
    }

    const int length = MultiByteToWideChar(CP_UTF8, 0, value, -1, nullptr, 0);
    if (length <= 1) {
        return {};
    }

    std::wstring result(static_cast<size_t>(length - 1), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, value, -1, result.data(), length);
    return result;
}

std::wstring AppRegistryPath(const std::wstring& appKey) {
    return std::wstring(AppRegistryRoot) + L"\\" + appKey;
}

void RememberApplication(const char* openXrAppName) {
    currentAppKey = SanitizeRegistryKey(CurrentProcessFileName());

    HKEY key = nullptr;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, AppRegistryPath(currentAppKey).c_str(), 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_SET_VALUE, nullptr, &key, nullptr) != ERROR_SUCCESS) {
        Log("app profile: failed to create registry key for %ls\n", currentAppKey.c_str());
        return;
    }

    const std::wstring displayName = Utf8ToWide(openXrAppName);
    const std::wstring modulePath = CurrentProcessPath();
    if (!displayName.empty()) {
        RegSetValueExW(key, L"display_name", 0, REG_SZ, reinterpret_cast<const BYTE*>(displayName.c_str()), static_cast<DWORD>((displayName.size() + 1) * sizeof(wchar_t)));
    }
    if (!modulePath.empty()) {
        RegSetValueExW(key, L"module", 0, REG_SZ, reinterpret_cast<const BYTE*>(modulePath.c_str()), static_cast<DWORD>((modulePath.size() + 1) * sizeof(wchar_t)));
    }
    RegCloseKey(key);

    Log("app profile: active app_key=%ls display=%ls module=%ls\n",
        currentAppKey.c_str(),
        displayName.empty() ? L"<unknown>" : displayName.c_str(),
        modulePath.empty() ? L"<unknown>" : modulePath.c_str());
}

bool ReadProfileDword(const wchar_t* keyName, DWORD& value) {
    if (currentAppKey.empty()) {
        return false;
    }

    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, AppRegistryPath(currentAppKey).c_str(), 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) {
        return false;
    }

    DWORD type = 0;
    DWORD size = sizeof(value);
    const LONG result = RegQueryValueExW(key, keyName, nullptr, &type, reinterpret_cast<BYTE*>(&value), &size);
    RegCloseKey(key);
    return result == ERROR_SUCCESS && type == REG_DWORD;
}

double MillisToRenderHeight(DWORD value, double fallback) {
    return std::clamp(static_cast<double>(value) / 1000.0, MinVerticalTangent, MaxVerticalTangent);
}

XrQuaternionf NormalizeQuaternion(const XrQuaternionf& value) {
    const float length = std::sqrt(
        value.x * value.x +
        value.y * value.y +
        value.z * value.z +
        value.w * value.w);
    if (length <= 0.0f || !std::isfinite(length)) {
        return XrQuaternionf{0.0f, 0.0f, 0.0f, 1.0f};
    }

    return XrQuaternionf{
        value.x / length,
        value.y / length,
        value.z / length,
        value.w / length};
}

XrQuaternionf MultiplyQuaternion(const XrQuaternionf& a, const XrQuaternionf& b) {
    return NormalizeQuaternion(XrQuaternionf{
        a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
        a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
        a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
        a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z});
}

XrQuaternionf PitchQuaternion(float radians) {
    const float halfAngle = radians * 0.5f;
    return XrQuaternionf{std::sin(halfAngle), 0.0f, 0.0f, std::cos(halfAngle)};
}

void LoadConfig() {
    enabled = ReadBoolSetting(L"enabled", true);
    splitMode = ReadBoolSetting(L"split_mode", false);
    foveatedCenterCompensation = ReadBoolSetting(L"foveated_center_compensation", false);
    const double legacyTotal = ReadDoubleSetting(L"vertical_tangent", DefaultTotalTangent);
    totalTangent = std::clamp(
        ReadDoubleSetting(L"total_render_height", ReadDoubleSetting(L"total_share", legacyTotal)),
        MinVerticalTangent,
        MaxVerticalTangent);

    if (splitMode) {
        topTangent = std::clamp(
            ReadDoubleSetting(L"top_tangent", totalTangent * 0.5),
            MinVerticalTangent,
            MaxVerticalTangent);
        bottomTangent = std::clamp(
            ReadDoubleSetting(L"bottom_tangent", totalTangent * 0.5),
            MinVerticalTangent,
            MaxVerticalTangent);
        totalTangent = std::clamp(topTangent + bottomTangent, MinVerticalTangent, MaxVerticalTangent);
    } else {
        topTangent = totalTangent * 0.5;
        bottomTangent = totalTangent * 0.5;
    }

    DWORD appEnabled = 1;
    if (ReadProfileDword(L"app_enabled", appEnabled) && appEnabled == 0) {
        enabled = false;
    }

    DWORD profileEnabled = 0;
    if (ReadProfileDword(L"profile_enabled", profileEnabled) && profileEnabled != 0) {
        DWORD profileSplitMode = splitMode ? 1u : 0u;
        DWORD profileTotal = static_cast<DWORD>(std::lround(totalTangent * 1000.0));
        DWORD profileTop = static_cast<DWORD>(std::lround(topTangent * 1000.0));
        DWORD profileBottom = static_cast<DWORD>(std::lround(bottomTangent * 1000.0));
        ReadProfileDword(L"split_mode", profileSplitMode);
        ReadProfileDword(L"total_render_height", profileTotal) || ReadProfileDword(L"total_share", profileTotal) || ReadProfileDword(L"vertical_tangent", profileTotal);
        ReadProfileDword(L"top_tangent", profileTop);
        ReadProfileDword(L"bottom_tangent", profileBottom);

        splitMode = profileSplitMode != 0;
        totalTangent = MillisToRenderHeight(profileTotal, totalTangent);
        if (splitMode) {
            topTangent = MillisToRenderHeight(profileTop, topTangent);
            bottomTangent = MillisToRenderHeight(profileBottom, bottomTangent);
            totalTangent = std::clamp(topTangent + bottomTangent, MinVerticalTangent, MaxVerticalTangent);
        } else {
            topTangent = totalTangent * 0.5;
            bottomTangent = totalTangent * 0.5;
        }
    }

    Log("config: enabled=%d app=%ls mode=%s total_render_height=%.3f top_render_height=%.3f bottom_render_height=%.3f top_scale=%.3f bottom_scale=%.3f foveated_center_compensation=%d\n",
        enabled ? 1 : 0,
        currentAppKey.empty() ? L"<global>" : currentAppKey.c_str(),
        splitMode ? "split" : "total",
        totalTangent,
        topTangent,
        bottomTangent,
        std::clamp(topTangent * 2.0, 0.0, 1.0),
        std::clamp(bottomTangent * 2.0, 0.0, 1.0),
        foveatedCenterCompensation ? 1 : 0);
}

void EnsureInitialized() {
    if (layerDirectory.empty()) {
        layerDirectory = GetLayerDirectory();
    }

    if (!logStream.is_open() && !layerDirectory.empty()) {
        logStream.open(layerDirectory / L"xr-viewlab.log", std::ios_base::app);
    }

    LoadConfig();
}

void ApplyXRViewLabFov(XrView& view, bool& compensated, float& pitchOffset) {
    const double topScale = std::clamp(topTangent * 2.0, 0.0, 1.0);
    const double bottomScale = std::clamp(bottomTangent * 2.0, 0.0, 1.0);
    const double originalTopTan = (std::max)(0.0, std::tan(static_cast<double>(view.fov.angleUp)));
    const double originalBottomTan = (std::max)(0.0, -std::tan(static_cast<double>(view.fov.angleDown)));
    const double desiredTopTan = originalTopTan * topScale;
    const double desiredBottomTan = originalBottomTan * bottomScale;

    compensated = false;
    pitchOffset = 0.0f;
    if (foveatedCenterCompensation &&
        splitMode &&
        std::abs(desiredTopTan - desiredBottomTan) > 0.00001) {
        const double symmetricHalfTan = (std::max)(0.00001, (desiredTopTan + desiredBottomTan) * 0.5);
        const double centerTan = (desiredTopTan - desiredBottomTan) * 0.5;
        pitchOffset = static_cast<float>(std::atan(centerTan));
        view.fov.angleUp = static_cast<float>(std::atan(symmetricHalfTan));
        view.fov.angleDown = -static_cast<float>(std::atan(symmetricHalfTan));
        view.pose.orientation = MultiplyQuaternion(view.pose.orientation, PitchQuaternion(pitchOffset));
        compensated = true;
        return;
    }

    view.fov.angleUp = static_cast<float>(std::atan(desiredTopTan));
    view.fov.angleDown = -static_cast<float>(std::atan(desiredBottomTan));
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrLocateViews(
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
        bool compensated = false;
        float pitchOffset = 0.0f;
        ApplyXRViewLabFov(views[i], compensated, pitchOffset);
        if (logCount < 20 || logCount % 300 == 0) {
            Log("xrLocateViews[%u]: up %.5f -> %.5f down %.5f -> %.5f left %.5f right %.5f foveated_center=%d pitch_offset=%.5f\n",
                i,
                before.angleUp,
                views[i].fov.angleUp,
                before.angleDown,
                views[i].fov.angleDown,
                views[i].fov.angleLeft,
                views[i].fov.angleRight,
                compensated ? 1 : 0,
                pitchOffset);
        }
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrEnumerateViewConfigurationViews(
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
            Log("xrEnumerateViewConfigurationViews[%u]: recommended height %u -> %u total_render_height=%.3f\n",
                i,
                beforeHeight,
                views[i].recommendedImageRectHeight,
                renderHeightScale);
        }
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrGetInstanceProcAddr(
    XrInstance instance,
    const char* name,
    PFN_xrVoidFunction* function) {
    const XrResult result = nextXrGetInstanceProcAddr(instance, name, function);

    if (result != XR_SUCCESS || name == nullptr || function == nullptr || *function == nullptr) {
        return result;
    }

    if (std::strcmp(name, "xrLocateViews") == 0) {
        nextXrLocateViews = reinterpret_cast<PFN_xrLocateViews>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrLocateViews);
    } else if (std::strcmp(name, "xrEnumerateViewConfigurationViews") == 0) {
        nextXrEnumerateViewConfigurationViews = reinterpret_cast<PFN_xrEnumerateViewConfigurationViews>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrEnumerateViewConfigurationViews);
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrCreateApiLayerInstance(
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
    RememberApplication(appName);
    LoadConfig();
    Log("%s for %s\n", enabled ? "enabled" : "disabled", appName);
    return result;
}
}

extern "C" {
XrResult __declspec(dllexport) XRAPI_CALL cooooked_xrviewlab_xrNegotiateLoaderApiLayerInterface(
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
    apiLayerRequest->getInstanceProcAddr = reinterpret_cast<PFN_xrGetInstanceProcAddr>(XRViewLab_xrGetInstanceProcAddr);
    apiLayerRequest->createApiLayerInstance = reinterpret_cast<PFN_xrCreateApiLayerInstance>(XRViewLab_xrCreateApiLayerInstance);
    return XR_SUCCESS;
}
}
