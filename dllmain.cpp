#include "pch.h"

namespace {
constexpr const char* LayerName = "XR_APILAYER_cooooked_xrviewlab";
constexpr const wchar_t* ConfigFileName = L"xr-viewlab.ini";
constexpr const wchar_t* AppRegistryRoot = L"Software\\cooooked\\xr-viewlab\\Apps";
constexpr double DefaultTotalTangent = 0.18;
constexpr double DefaultTopTangent = 0.09;
constexpr double DefaultBottomTangent = 0.09;
constexpr double DefaultHorizontalRenderWidth = 0.80;
constexpr double MinVerticalTangent = 0.0;
constexpr double MaxVerticalTangent = 1.0;
constexpr double MinRenderScale = 0.01;
constexpr double MinUevrRecommendedScale = 0.50;

std::filesystem::path layerDirectory;
std::ofstream logStream;
std::ofstream verboseLogStream;
std::mutex logMutex;
bool sessionLogged = false;

bool enabled = true;
bool cropOuterEdgesOnly = true;
// CROP (total_render_height / horizontal_render_width) controls how much the game
// actually RENDERS (FOV + recommended resolution) — keep generous to avoid LOD
// pop-in. MASK is a separate black visor border drawn via the OpenXR visibility
// mesh: it bounds the VISIBLE area (black beyond) without changing what is rendered,
// and costs no extra GPU (fewer pixels shaded). mask_* values are ABSOLUTE in the
// same units as CROP; the layer converts them to a fraction of the cropped view.
bool maskEnabled = false;
double maskVertical = 1.0;    // legacy absolute vtangent bound
double maskHorizontal = 1.0;  // legacy absolute hwidth bound
bool maskRounded = false;     // round the visible-area corners (pill/bean look)
double maskCorner = 1.0;      // corner radius as a fraction of the shorter half-extent
double maskTopBias = 0.0;     // -1..1, moves the top lip of the visor opening vertically
double maskBottomBias = 0.0;  // -1..1, moves the bottom lip of the visor opening vertically
double maskLeftBias = 0.0;    // -1..1, moves the left lip of the visor opening horizontally
double maskRightBias = 0.0;   // -1..1, moves the right lip of the visor opening horizontally
double maskTopCurve = 0.0;    // -1..1, bends the top lip for bridge/nose-like shapes
double maskBottomCurve = 0.0; // -1..1, bends the bottom lip
// Per-game render-resolution multiplier (supersampling). 1.0 = unchanged; e.g. 1.37 = 137%.
// Lets the user set quality per game instead of changing VD/VDXR resolution between games.
double renderScale = 1.0;
double visorSize = 1.0;
double visorWidth = 1.0;
double visorHeight = 1.0;
double visorCurve = 0.75;
double visorOffsetX = 0.0;
double visorOffsetY = 0.0;
// Visor shape controls. Formula-identical to the UI preview (BeanMaskEditor.cs) — any
// change here must be mirrored there. Values are authored in the UI's coordinate
// convention (y grows DOWN); the UI-to-NDC flip happens ONLY in ApexYFromConfigNdc.
double visorOuterApexY = 0.0;          // mask_outer_apex_y: -0.5..0.5, outer-curve apex offset
double visorInnerLowerY = 0.0;         // mask_inner_lower_y: 0..0.333, nose-divot band height (0 = off)
double visorInnerBridgeWidth = 0.5;    // mask_inner_bridge_width: 0..1, bezier handle width
double visorInnerBridgeRise = 0.0;     // mask_inner_bridge_rise: 0..0.5, endpoint tangent rise
double visorInnerBridgePeakX = 0.5;    // mask_inner_bridge_peak_x: 0..1, handle horizontal shift
double visorInnerBridgeSteepness = 0.5;// mask_inner_bridge_steepness: 0..1, handle length scale
constexpr bool visorHD = false;          // HD visor removed
constexpr bool visorAntialiasing = false; // anti-aliasing removed
// There is one product visor: a ViewLab-owned curved corner mask drawn into the
// game's D3D11 eye textures. The old alternate visor experiments were removed.
// Per-frame hook detail (xrLocateViews / xrEnumerateViewConfigurationViews /
// xrGetVisibilityMaskKHR) is gated behind this flag and, when on, routed to a
// SEPARATE ViewLab.verbose.log so the human-facing ViewLab.log (and the in-UI
// Log button) only ever shows meaningful init/config/error events. OFF by default.
bool verboseLogging = false;
bool splitMode = false;
bool foveatedCenterCompensation = true;   // permanently enabled
bool visualMaskOnly = false;
bool horizontalVisualMaskOnly = false;
bool outerEdgeVisibilityMaskOnly = true;  // permanently enabled
constexpr bool edgeSmearFix = false;       // edge-smear fix removed
constexpr bool lodPopInFix = false;        // LOD pop-in fix removed
int edgeSmearPixels = 2;                   // unused; kept to avoid changing DrawEdgeGuardToTexture signature
bool uevrLikeProcess = false;
double totalTangent = DefaultTotalTangent;
double topTangent = DefaultTopTangent;
double bottomTangent = DefaultBottomTangent;
double horizontalRenderWidth = DefaultHorizontalRenderWidth;
std::wstring currentAppKey;

PFN_xrGetInstanceProcAddr nextXrGetInstanceProcAddr = nullptr;
PFN_xrCreateSession nextXrCreateSession = nullptr;
PFN_xrDestroySession nextXrDestroySession = nullptr;
PFN_xrLocateViews nextXrLocateViews = nullptr;
PFN_xrEnumerateViewConfigurationViews nextXrEnumerateViewConfigurationViews = nullptr;
PFN_xrGetVisibilityMaskKHR nextXrGetVisibilityMaskKHR = nullptr;
PFN_xrEndFrame nextXrEndFrame = nullptr;
PFN_xrEnumerateSwapchainFormats nextXrEnumerateSwapchainFormats = nullptr;
PFN_xrCreateSwapchain nextXrCreateSwapchain = nullptr;
PFN_xrDestroySwapchain nextXrDestroySwapchain = nullptr;
PFN_xrEnumerateSwapchainImages nextXrEnumerateSwapchainImages = nullptr;
PFN_xrAcquireSwapchainImage nextXrAcquireSwapchainImage = nullptr;
PFN_xrWaitSwapchainImage nextXrWaitSwapchainImage = nullptr;
PFN_xrReleaseSwapchainImage nextXrReleaseSwapchainImage = nullptr;

std::atomic<uint32_t> locateViewsLogCount{0};
std::atomic<uint32_t> enumerateViewsLogCount{0};
std::atomic<uint32_t> enumerateSkipLogCount{0};
std::atomic<uint32_t> visibilityMaskLogCount{0};
std::atomic<uint32_t> visorMaskNoHookLogCount{0};
std::atomic<uint32_t> visorRendererLogCount{0};
std::atomic<bool> g_visibilityMaskEverCalled{false};

// One-shot diagnostic flags for the visor renderer. Each stage logs the
// first time it is hit (success or failure) so a single in-headset run reveals the
// first failing stage without spamming the log.
std::atomic<bool> g_diagEndFrameCalled{false};
std::atomic<bool> g_diagEndFrameGated{false};
std::atomic<bool> g_diagProjFound{false};
std::atomic<bool> g_diagNoProjLayer{false};
std::atomic<bool> g_diagDrawScNotTracked{false};
std::atomic<bool> g_diagDrawBadIndex{false};
std::atomic<bool> g_diagDrawNullTex{false};
std::atomic<bool> g_diagDrawNoVerts{false};
std::atomic<bool> g_diagDrawMapFailed{false};
std::atomic<bool> g_diagDrawRtvFailed{false};
std::atomic<bool> g_diagDrawOk{false};

std::atomic<bool> g_diagLodFullFov{false};
std::atomic<bool> g_diagVisorSkipsVisibilityEdgeFilter{false};
std::atomic<bool> g_diagEndFrameLateDraw{false};

// Per-game swapchain tracking for the D3D11 visor renderer.
struct EyeView {
    XrRect2Di rect{};       // sub-image rect within the texture for this eye
    uint32_t arraySlice = 0; // texture-array slice for this eye
    uint32_t viewIndex = 0;  // projection view index: 0 = left, 1 = right for primary stereo
    XrFovf fov{};
};
struct TrackedSwapchain {
    XrSession session = XR_NULL_HANDLE;
    uint32_t width = 0;
    uint32_t height = 0;
    uint32_t arraySize = 1;
    uint32_t sampleCount = 1;
    int64_t format = 0;
    XrSwapchainUsageFlags usageFlags = 0;
    std::vector<ID3D11Texture2D*> textures;  // runtime textures; not AddRef'd
    std::vector<ID3D11RenderTargetView*> rtvs; // runtime texture RTVs, indexed image * arraySize + slice
    uint32_t lastAcquiredIndex = UINT32_MAX;
    // Per-eye layout captured from the projection layer in xrEndFrame. Stable
    // across frames, so the previous frame's layout is used at release time.
    std::vector<EyeView> eyeViews;
};

std::mutex g_swapchainMutex;
// Serializes renderer lifetime and immediate-context use across OpenXR hooks. The D3D11
// immediate context and its state objects must not be released while another hook draws.
std::recursive_mutex g_rendererMutex;
std::unordered_map<XrSwapchain, TrackedSwapchain> g_swapchains;

// D3D11 shader-based mask renderer — draws the visor border directly into the
// game's own eye textures when the visibility-mask path is unavailable.
struct D3D11MaskState {
    XrSession session = XR_NULL_HANDLE;
    ID3D11Device* device = nullptr;
    ID3D11DeviceContext* context = nullptr;
    ID3D11VertexShader* vs = nullptr;
    ID3D11PixelShader* ps = nullptr;
    ID3D11InputLayout* layout = nullptr;
    ID3D11Buffer* vb = nullptr;
    ID3D11RasterizerState* rs = nullptr;
    ID3D11BlendState* bs = nullptr;        // SRC_ALPHA blend — used only when AA is on
    ID3D11BlendState* bsOpaque = nullptr;  // blend disabled — the historically proven visor pipeline
    ID3D11DepthStencilState* dss = nullptr;
    bool initialized = false;
    bool failed = false;
    // Latest eye views from xrLocateViews (for future per-eye FOV use).
    XrSpace locateSpace = XR_NULL_HANDLE;
    XrTime locateTime = 0;
    std::array<XrView, 2> latestViews{};
    std::array<XrView, 2> latestOriginalViews{};
    uint32_t latestViewCount = 0;
};

D3D11MaskState g_d3d11Mask;

void ReleaseTrackedSwapchainResources(TrackedSwapchain& ts) {
    for (ID3D11RenderTargetView* rtv : ts.rtvs) {
        if (rtv) rtv->Release();
    }
    ts.rtvs.clear();
}

std::filesystem::path LocalAppDataPath() {
    wchar_t localAppData[MAX_PATH]{};
    const DWORD length = GetEnvironmentVariableW(L"LOCALAPPDATA", localAppData, static_cast<DWORD>(std::size(localAppData)));
    if (length == 0 || length >= std::size(localAppData)) {
        return {};
    }

    return std::filesystem::path(localAppData);
}

std::filesystem::path UserDataDirectory() {
    const std::filesystem::path localAppData = LocalAppDataPath();
    if (localAppData.empty()) {
        return layerDirectory;
    }

    return localAppData / L"XR ViewLab";
}

std::filesystem::path LogPath() {
    return UserDataDirectory() / L"Logs" / L"ViewLab.log";
}

std::filesystem::path VerboseLogPath() {
    return UserDataDirectory() / L"Logs" / L"ViewLab.verbose.log";
}

void RotateLogIfNeeded(const std::filesystem::path& logPath) {
    std::error_code ec;
    if (!std::filesystem::exists(logPath, ec) || std::filesystem::file_size(logPath, ec) < 2 * 1024 * 1024) {
        return;
    }

    const std::filesystem::path oldPath = logPath.parent_path() / L"ViewLab.old.log";
    std::filesystem::remove(oldPath, ec);
    std::filesystem::rename(logPath, oldPath, ec);
}

void OpenLogIfNeeded() {
    if (logStream.is_open()) {
        return;
    }

    const std::filesystem::path logPath = LogPath();
    std::error_code ec;
    std::filesystem::create_directories(logPath.parent_path(), ec);
    RotateLogIfNeeded(logPath);
    logStream.open(logPath, std::ios_base::app);
}

void Log(const char* fmt, ...) {
    char buffer[1024]{};

    va_list args;
    va_start(args, fmt);
    _vsnprintf_s(buffer, sizeof(buffer), _TRUNCATE, fmt, args);
    va_end(args);

    SYSTEMTIME now{};
    GetLocalTime(&now);

    char line[1400]{};
    _snprintf_s(
        line,
        sizeof(line),
        _TRUNCATE,
        "%02u:%02u:%02u:%03u [%lu] | INFO  | %s",
        now.wHour,
        now.wMinute,
        now.wSecond,
        now.wMilliseconds,
        static_cast<unsigned long>(GetCurrentProcessId()),
        buffer);

    OutputDebugStringA(buffer);
    std::lock_guard<std::mutex> lock(logMutex);
    OpenLogIfNeeded();
    if (logStream.is_open()) {
        logStream << line;
        if (line[std::strlen(line) - 1] != '\n') {
            logStream << '\n';
        }
        logStream.flush();
    }
}

void OpenVerboseLogIfNeeded() {
    if (verboseLogStream.is_open()) {
        return;
    }

    const std::filesystem::path logPath = VerboseLogPath();
    std::error_code ec;
    std::filesystem::create_directories(logPath.parent_path(), ec);
    RotateLogIfNeeded(logPath);
    verboseLogStream.open(logPath, std::ios_base::app);
}

// Per-frame / high-frequency hook detail. No-op unless verbose_logging=1 in the
// INI. Writes to ViewLab.verbose.log only — never the human-facing ViewLab.log.
void LogVerbose(const char* fmt, ...) {
    if (!verboseLogging) {
        return;
    }

    char buffer[1024]{};

    va_list args;
    va_start(args, fmt);
    _vsnprintf_s(buffer, sizeof(buffer), _TRUNCATE, fmt, args);
    va_end(args);

    SYSTEMTIME now{};
    GetLocalTime(&now);

    char line[1400]{};
    _snprintf_s(
        line,
        sizeof(line),
        _TRUNCATE,
        "%02u:%02u:%02u:%03u [%lu] | VERB  | %s",
        now.wHour,
        now.wMinute,
        now.wSecond,
        now.wMilliseconds,
        static_cast<unsigned long>(GetCurrentProcessId()),
        buffer);

    std::lock_guard<std::mutex> lock(logMutex);
    OpenVerboseLogIfNeeded();
    if (verboseLogStream.is_open()) {
        verboseLogStream << line;
        if (line[std::strlen(line) - 1] != '\n') {
            verboseLogStream << '\n';
        }
        verboseLogStream.flush();
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
    if (layerDirectory.empty()) {
        const std::filesystem::path localAppData = LocalAppDataPath();
        if (!localAppData.empty()) {
            return localAppData / L"XR ViewLab" / ConfigFileName;
        }
        std::error_code ec;
        return std::filesystem::temp_directory_path(ec) / L"XR ViewLab" / ConfigFileName;
    }
    return layerDirectory / ConfigFileName;
}

std::filesystem::path UserConfigPath() {
    const std::filesystem::path userData = UserDataDirectory();
    if (userData.empty()) {
        return LegacyConfigPath();
    }

    return userData / ConfigFileName;
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

bool EndsWithInsensitive(const std::wstring& value, const wchar_t* suffix) {
    const size_t suffixLength = std::wcslen(suffix);
    if (value.size() < suffixLength) {
        return false;
    }

    return _wcsicmp(value.c_str() + value.size() - suffixLength, suffix) == 0;
}

bool ContainsInsensitive(const std::wstring& value, const wchar_t* pattern) {
    std::wstring loweredValue = value;
    std::wstring loweredPattern = pattern;
    std::transform(loweredValue.begin(), loweredValue.end(), loweredValue.begin(), [](wchar_t ch) { return static_cast<wchar_t>(std::towlower(ch)); });
    std::transform(loweredPattern.begin(), loweredPattern.end(), loweredPattern.begin(), [](wchar_t ch) { return static_cast<wchar_t>(std::towlower(ch)); });
    return loweredValue.find(loweredPattern) != std::wstring::npos;
}

bool DetectUevrLikeProcess(const std::wstring& processPath, const std::wstring& processName, const std::wstring& openXrAppName) {
    return EndsWithInsensitive(processName, L"-Win64-Shipping.exe") ||
           EndsWithInsensitive(processName, L"-WinGDK-Shipping.exe") ||
           EndsWithInsensitive(processName, L"-Windows-Shipping.exe") ||
           ContainsInsensitive(processPath, L"\\Engine\\Binaries\\Win64\\") ||
           ContainsInsensitive(openXrAppName, L"Unreal") ||
           ContainsInsensitive(openXrAppName, L"UEVR");
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

std::wstring CurrentUtcTimestamp() {
    SYSTEMTIME st{};
    GetSystemTime(&st);
    wchar_t buffer[32]{};
    swprintf_s(buffer, L"%04u-%02u-%02uT%02u:%02u:%02uZ",
        st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);
    return buffer;
}

void SetRegistryStringValue(HKEY key, const wchar_t* name, const std::wstring& value) {
    RegSetValueExW(key, name, 0, REG_SZ, reinterpret_cast<const BYTE*>(value.c_str()), static_cast<DWORD>((value.size() + 1) * sizeof(wchar_t)));
}

void SetRegistryDwordValue(HKEY key, const wchar_t* name, DWORD value) {
    RegSetValueExW(key, name, 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
}

void RememberApplication(const char* openXrAppName, const char* openXrEngineName, XrResult createResult, const char* runtimeName) {
    currentAppKey = SanitizeRegistryKey(CurrentProcessFileName());
    const std::wstring displayName = Utf8ToWide(openXrAppName);
    const std::wstring engineName = Utf8ToWide(openXrEngineName);
    const std::wstring runtime = Utf8ToWide(runtimeName);
    const std::wstring modulePath = CurrentProcessPath();
    uevrLikeProcess = DetectUevrLikeProcess(modulePath, CurrentProcessFileName(), displayName);

    HKEY key = nullptr;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, AppRegistryPath(currentAppKey).c_str(), 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_SET_VALUE, nullptr, &key, nullptr) != ERROR_SUCCESS) {
        Log("app profile: failed to create registry key for %ls\n", currentAppKey.c_str());
        return;
    }

    if (!displayName.empty()) {
        SetRegistryStringValue(key, L"display_name", displayName);
        SetRegistryStringValue(key, L"last_openxr_app_name", displayName);
    }
    if (!engineName.empty()) {
        SetRegistryStringValue(key, L"last_openxr_engine_name", engineName);
    }
    if (!modulePath.empty()) {
        SetRegistryStringValue(key, L"module", modulePath);
    }
    if (!runtime.empty()) {
        SetRegistryStringValue(key, L"last_runtime", runtime);
    }
    const std::wstring now = CurrentUtcTimestamp();
    SetRegistryStringValue(key, L"last_seen", now);
    SetRegistryStringValue(key, L"last_seen_utc", now);
    SetRegistryDwordValue(key, L"last_pid", static_cast<DWORD>(GetCurrentProcessId()));
    SetRegistryDwordValue(key, L"last_result", static_cast<DWORD>(createResult));
#if defined(_WIN64)
    SetRegistryStringValue(key, L"last_bitness", L"64-bit");
#else
    SetRegistryStringValue(key, L"last_bitness", L"32-bit");
#endif
    RegCloseKey(key);

    Log("app profile: active app_key=%ls display=%ls module=%ls result=%d runtime=%ls\n",
        currentAppKey.c_str(),
        displayName.empty() ? L"<unknown>" : displayName.c_str(),
        modulePath.empty() ? L"<unknown>" : modulePath.c_str(),
        createResult,
        runtime.empty() ? L"<unknown>" : runtime.c_str());
    if (uevrLikeProcess) {
        Log("app profile: UEVR-like Unreal process detected, enabling conservative runtime guards\n");
    }
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

bool ReadProfileDouble(const wchar_t* keyName, double& value) {
    if (currentAppKey.empty()) {
        return false;
    }

    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, AppRegistryPath(currentAppKey).c_str(), 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) {
        return false;
    }

    wchar_t buffer[64]{};
    DWORD type = 0;
    DWORD size = sizeof(buffer);
    const LONG result = RegQueryValueExW(key, keyName, nullptr, &type, reinterpret_cast<BYTE*>(buffer), &size);
    RegCloseKey(key);
    if (result != ERROR_SUCCESS || (type != REG_SZ && type != REG_EXPAND_SZ)) {
        return false;
    }

    wchar_t* end = nullptr;
    const double parsed = std::wcstod(buffer, &end);
    if (end == buffer || !std::isfinite(parsed)) {
        return false;
    }
    value = parsed;
    return true;
}

double MillisToRenderHeight(DWORD value, double fallback) {
    return std::clamp(static_cast<double>(value) / 1000.0, MinVerticalTangent, MaxVerticalTangent);
}

double MillisToRenderScale(DWORD value, double fallback) {
    return std::clamp(static_cast<double>(value) / 1000.0, MinRenderScale, 1.0);
}

double MillisToMaskScale(DWORD value, double fallback) {
    return std::clamp(static_cast<double>(value) / 1000.0, 0.01, 1.0);
}

double DwordToRenderScale(DWORD value, double fallback) {
    if (value == 0u) {
        return fallback;
    }
    const double divisor = value > 3000u ? 1000000.0 : 1000.0;
    return std::clamp(static_cast<double>(value) / divisor, 0.1, 3.0);
}

double SignedMillisToUnit(DWORD value, double fallback) {
    if (value > 2000u) {
        return fallback;
    }
    return std::clamp((static_cast<double>(value) / 1000.0) - 1.0, -1.0, 1.0);
}

double OpeningFromMask(double vertical, double horizontal, double cropVertical, double cropHorizontal) {
    const double v = std::clamp(vertical / std::clamp(cropVertical, 0.01, 1.0), 0.05, 1.0);
    const double h = std::clamp(horizontal / std::clamp(cropHorizontal, 0.01, 1.0), 0.05, 1.0);
    return std::clamp((v + h) * 0.5, 0.05, 1.0);
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

void ReleaseD3D11MaskRenderer() {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        for (auto& kv : g_swapchains) {
            ReleaseTrackedSwapchainResources(kv.second);
        }
        g_swapchains.clear();
    }
    if (g_d3d11Mask.vb)      { g_d3d11Mask.vb->Release();      g_d3d11Mask.vb = nullptr; }
    if (g_d3d11Mask.dss)     { g_d3d11Mask.dss->Release();     g_d3d11Mask.dss = nullptr; }
    if (g_d3d11Mask.bs)      { g_d3d11Mask.bs->Release();      g_d3d11Mask.bs = nullptr; }
    if (g_d3d11Mask.bsOpaque){ g_d3d11Mask.bsOpaque->Release(); g_d3d11Mask.bsOpaque = nullptr; }
    if (g_d3d11Mask.rs)      { g_d3d11Mask.rs->Release();      g_d3d11Mask.rs = nullptr; }
    if (g_d3d11Mask.layout)  { g_d3d11Mask.layout->Release();  g_d3d11Mask.layout = nullptr; }
    if (g_d3d11Mask.ps)      { g_d3d11Mask.ps->Release();      g_d3d11Mask.ps = nullptr; }
    if (g_d3d11Mask.vs)      { g_d3d11Mask.vs->Release();      g_d3d11Mask.vs = nullptr; }
    if (g_d3d11Mask.context) { g_d3d11Mask.context->Release(); g_d3d11Mask.context = nullptr; }
    if (g_d3d11Mask.device)  { g_d3d11Mask.device->Release();  g_d3d11Mask.device = nullptr; }
    g_d3d11Mask = D3D11MaskState{};
    g_visibilityMaskEverCalled.store(false);
}

const XrBaseInStructure* FindStructInChain(const void* next, XrStructureType type) {
    const XrBaseInStructure* cursor = reinterpret_cast<const XrBaseInStructure*>(next);
    while (cursor != nullptr) {
        if (cursor->type == type) {
            return cursor;
        }
        cursor = cursor->next;
    }
    return nullptr;
}

DXGI_FORMAT GetNonSRGBFormat(DXGI_FORMAT fmt) {
    switch (fmt) {
        case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:  return DXGI_FORMAT_B8G8R8A8_UNORM;
        case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:  return DXGI_FORMAT_R8G8B8A8_UNORM;
        case DXGI_FORMAT_BC1_UNORM_SRGB:       return DXGI_FORMAT_BC1_UNORM;
        case DXGI_FORMAT_BC2_UNORM_SRGB:       return DXGI_FORMAT_BC2_UNORM;
        case DXGI_FORMAT_BC3_UNORM_SRGB:       return DXGI_FORMAT_BC3_UNORM;
        // TYPELESS swapchain allocations cannot back an RTV directly; resolve to a
        // concrete UNORM view. Black writes are identical regardless of SRGB encoding.
        case DXGI_FORMAT_R8G8B8A8_TYPELESS:    return DXGI_FORMAT_R8G8B8A8_UNORM;
        case DXGI_FORMAT_B8G8R8A8_TYPELESS:    return DXGI_FORMAT_B8G8R8A8_UNORM;
        case DXGI_FORMAT_R10G10B10A2_TYPELESS: return DXGI_FORMAT_R10G10B10A2_UNORM;
        case DXGI_FORMAT_R16G16B16A16_TYPELESS:return DXGI_FORMAT_R16G16B16A16_UNORM;
        default: return fmt;
    }
}

struct VisorVertex {
    float x;
    float y;
    float alpha;
};

static const char kVisorVS[] =
    "struct VSIn { float2 pos : POSITION; float alpha : ALPHA; };"
    "struct VSOut { float4 pos : SV_POSITION; float alpha : ALPHA; };"
    "VSOut main(VSIn input) {"
    " VSOut output;"
    " output.pos = float4(input.pos.x, input.pos.y, 0.0f, 1.0f);"
    " output.alpha = input.alpha;"
    " return output; }";

static const char kVisorPS[] =
    "float4 main(float alpha : ALPHA) : SV_TARGET { return float4(0.0f, 0.0f, 0.0f, alpha); }";

bool InitD3D11MaskRenderer() {
    if (g_d3d11Mask.initialized) return true;
    if (g_d3d11Mask.failed)      return false;
    if (!g_d3d11Mask.device || !g_d3d11Mask.context) return false;

    typedef HRESULT(WINAPI* PFN_D3DCompile)(
        LPCVOID, SIZE_T, LPCSTR, const D3D_SHADER_MACRO*, ID3DInclude*,
        LPCSTR, LPCSTR, UINT, UINT, ID3DBlob**, ID3DBlob**);

    HMODULE dxcLib = LoadLibraryA("d3dcompiler_47.dll");
    if (!dxcLib) dxcLib = LoadLibraryA("d3dcompiler_46.dll");
    if (!dxcLib) {
        Log("visor: D3D shader compiler not found; visor unavailable for this game\n");
        g_d3d11Mask.failed = true;
        return false;
    }
    auto D3DCompile = reinterpret_cast<PFN_D3DCompile>(GetProcAddress(dxcLib, "D3DCompile"));
    if (!D3DCompile) {
        Log("d3d11 mask: D3DCompile entry point missing\n");
        FreeLibrary(dxcLib);
        g_d3d11Mask.failed = true;
        return false;
    }

    ID3DBlob* vsBlob = nullptr;
    ID3DBlob* psBlob = nullptr;
    ID3DBlob* errBlob = nullptr;

    HRESULT hr = D3DCompile(kVisorVS, std::strlen(kVisorVS), "VS", nullptr, nullptr,
                            "main", "vs_4_0", 0, 0, &vsBlob, &errBlob);
    if (FAILED(hr)) {
        const char* msg = errBlob ? static_cast<const char*>(errBlob->GetBufferPointer()) : "?";
        Log("d3d11 mask: VS compile failed hr=0x%08X %s\n", static_cast<unsigned>(hr), msg);
        if (errBlob) errBlob->Release();
        FreeLibrary(dxcLib);
        g_d3d11Mask.failed = true;
        return false;
    }
    if (errBlob) { errBlob->Release(); errBlob = nullptr; }

    hr = D3DCompile(kVisorPS, std::strlen(kVisorPS), "PS", nullptr, nullptr,
                    "main", "ps_4_0", 0, 0, &psBlob, &errBlob);
    if (FAILED(hr)) {
        const char* msg = errBlob ? static_cast<const char*>(errBlob->GetBufferPointer()) : "?";
        Log("d3d11 mask: PS compile failed hr=0x%08X %s\n", static_cast<unsigned>(hr), msg);
        if (errBlob) errBlob->Release();
        vsBlob->Release();
        FreeLibrary(dxcLib);
        g_d3d11Mask.failed = true;
        return false;
    }
    if (errBlob) { errBlob->Release(); errBlob = nullptr; }

    hr = g_d3d11Mask.device->CreateVertexShader(
        vsBlob->GetBufferPointer(), vsBlob->GetBufferSize(), nullptr, &g_d3d11Mask.vs);
    if (FAILED(hr)) {
        Log("d3d11 mask: CreateVertexShader hr=0x%08X\n", static_cast<unsigned>(hr));
        vsBlob->Release(); psBlob->Release();
        FreeLibrary(dxcLib);
        g_d3d11Mask.failed = true;
        return false;
    }

    hr = g_d3d11Mask.device->CreatePixelShader(
        psBlob->GetBufferPointer(), psBlob->GetBufferSize(), nullptr, &g_d3d11Mask.ps);
    psBlob->Release();
    if (FAILED(hr)) {
        Log("d3d11 mask: CreatePixelShader hr=0x%08X\n", static_cast<unsigned>(hr));
        vsBlob->Release();
        FreeLibrary(dxcLib);
        g_d3d11Mask.failed = true;
        return false;
    }

    D3D11_INPUT_ELEMENT_DESC elems[] = {
        {"POSITION", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0},
        {"ALPHA", 0, DXGI_FORMAT_R32_FLOAT, 0, 8, D3D11_INPUT_PER_VERTEX_DATA, 0},
    };
    hr = g_d3d11Mask.device->CreateInputLayout(
        elems, 2, vsBlob->GetBufferPointer(), vsBlob->GetBufferSize(), &g_d3d11Mask.layout);
    vsBlob->Release();
    FreeLibrary(dxcLib);
    if (FAILED(hr)) {
        Log("d3d11 mask: CreateInputLayout hr=0x%08X\n", static_cast<unsigned>(hr));
        g_d3d11Mask.failed = true;
        return false;
    }

    // Enough for high-segment direct masks and the overlay texture.
    D3D11_BUFFER_DESC vbd{};
    vbd.ByteWidth      = 4096 * sizeof(VisorVertex);
    vbd.Usage          = D3D11_USAGE_DYNAMIC;
    vbd.BindFlags      = D3D11_BIND_VERTEX_BUFFER;
    vbd.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
    if (FAILED(g_d3d11Mask.device->CreateBuffer(&vbd, nullptr, &g_d3d11Mask.vb))) {
        Log("d3d11 mask: vertex buffer create failed\n");
        g_d3d11Mask.failed = true;
        return false;
    }

    D3D11_RASTERIZER_DESC rsd{};
    rsd.FillMode        = D3D11_FILL_SOLID;
    rsd.CullMode        = D3D11_CULL_NONE;
    rsd.DepthClipEnable = TRUE;
    hr = g_d3d11Mask.device->CreateRasterizerState(&rsd, &g_d3d11Mask.rs);
    if (FAILED(hr) || !g_d3d11Mask.rs) {
        Log("d3d11 mask: rasterizer state create failed hr=0x%08X\n", static_cast<unsigned>(hr));
        ReleaseD3D11MaskRenderer();
        g_d3d11Mask.failed = true;
        return false;
    }

    D3D11_BLEND_DESC bld{};
    bld.RenderTarget[0].BlendEnable           = TRUE;
    bld.RenderTarget[0].SrcBlend              = D3D11_BLEND_SRC_ALPHA;
    bld.RenderTarget[0].DestBlend             = D3D11_BLEND_INV_SRC_ALPHA;
    bld.RenderTarget[0].BlendOp               = D3D11_BLEND_OP_ADD;
    bld.RenderTarget[0].SrcBlendAlpha         = D3D11_BLEND_ONE;
    bld.RenderTarget[0].DestBlendAlpha        = D3D11_BLEND_INV_SRC_ALPHA;
    bld.RenderTarget[0].BlendOpAlpha          = D3D11_BLEND_OP_ADD;
    bld.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
    hr = g_d3d11Mask.device->CreateBlendState(&bld, &g_d3d11Mask.bs);
    if (FAILED(hr) || !g_d3d11Mask.bs) {
        Log("d3d11 mask: blend state create failed hr=0x%08X\n", static_cast<unsigned>(hr));
        ReleaseD3D11MaskRenderer();
        g_d3d11Mask.failed = true;
        return false;
    }

    // Opaque pipeline for AA-off: blending disabled, exactly the draw path that was
    // last confirmed visible in-headset (4.1.103). The alpha-blended path above is
    // used ONLY when visor_antialiasing=1 draws feather strips.
    D3D11_BLEND_DESC bldOpaque{};
    bldOpaque.RenderTarget[0].BlendEnable           = FALSE;
    bldOpaque.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
    hr = g_d3d11Mask.device->CreateBlendState(&bldOpaque, &g_d3d11Mask.bsOpaque);
    if (FAILED(hr) || !g_d3d11Mask.bsOpaque) {
        Log("d3d11 mask: opaque blend state create failed hr=0x%08X\n", static_cast<unsigned>(hr));
        ReleaseD3D11MaskRenderer();
        g_d3d11Mask.failed = true;
        return false;
    }

    D3D11_DEPTH_STENCIL_DESC dsd{};
    dsd.DepthEnable   = FALSE;
    dsd.StencilEnable = FALSE;
    hr = g_d3d11Mask.device->CreateDepthStencilState(&dsd, &g_d3d11Mask.dss);
    if (FAILED(hr) || !g_d3d11Mask.dss) {
        Log("d3d11 mask: depth-stencil state create failed hr=0x%08X\n", static_cast<unsigned>(hr));
        ReleaseD3D11MaskRenderer();
        g_d3d11Mask.failed = true;
        return false;
    }

    g_d3d11Mask.initialized = true;
    Log("visor: D3D11 renderer initialized\n");
    return true;
}

// Curve slider -> superellipse exponent. Geometric interpolation from exp=32 (flat
// rounded-rect) to exp=2 (full lemon). MUST stay identical to the UI's
// BeanMaskEditor.CurveExponent: 32 * (2/32)^curve.
float VisorCurveExponent(double curve) {
    return 32.0f * std::pow(2.0f / 32.0f, static_cast<float>(std::clamp(curve, 0.0, 1.0)));
}

// The UI authors apex-y in screen coordinates (y grows DOWN); D3D NDC y grows UP.
// This is the ONLY place that flip happens — past inversion bugs came from flipping
// (or forgetting to flip) in individual geometry builders.
float ApexYFromConfigNdc(double configApexY) {
    return -static_cast<float>(std::clamp(configApexY, -0.5, 0.5));
}

uint32_t VisorCurveSegments(uint32_t requested = 96u) {
    return requested; // HD visor removed
}

void PushVertex(VisorVertex* vertsOut, uint32_t& v, uint32_t vertCapacity, float x, float y, float alpha = 1.0f) {
    if (v >= vertCapacity) return;
    vertsOut[v++] = VisorVertex{x, y, alpha};
}

void PushTri(
    VisorVertex* vertsOut, uint32_t& v, uint32_t vertCapacity,
    float ax, float ay, float bx, float by, float cx, float cy,
    float aa = 1.0f, float ba = 1.0f, float ca = 1.0f) {
    if (v + 3 > vertCapacity) return;
    PushVertex(vertsOut, v, vertCapacity, ax, ay, aa);
    PushVertex(vertsOut, v, vertCapacity, bx, by, ba);
    PushVertex(vertsOut, v, vertCapacity, cx, cy, ca);
}

void PushQuad(
    VisorVertex* vertsOut, uint32_t& v, uint32_t vertCapacity,
    float x0, float y0, float x1, float y1, float alpha = 1.0f) {
    if (x1 <= x0 || y1 <= y0 || v + 6 > vertCapacity) return;
    PushTri(vertsOut, v, vertCapacity, x0, y0, x1, y0, x1, y1, alpha, alpha, alpha);
    PushTri(vertsOut, v, vertCapacity, x0, y0, x1, y1, x0, y1, alpha, alpha, alpha);
}

// Builds visor border as a flat triangle list (unindexed) in the given NDC bbox.
// Output: x/y/alpha vertices. Returns vertex count (= triangle count * 3).
// Shape math identical to BeanMaskEditor.BuildGeometry.
uint32_t BuildVisorBorderVerts(
    float bboxMinX, float bboxMaxX, float bboxMinY, float bboxMaxY,
    float featherX, float featherY,
    VisorVertex* vertsOut, uint32_t vertCapacity) {
    const float bboxW = bboxMaxX - bboxMinX;
    const float bboxH = bboxMaxY - bboxMinY;
    if (bboxW < 0.001f || bboxH < 0.001f || vertCapacity < 6) return 0;

    // Hardcoded maximum corner coverage: opening fills the full bbox, mask extends to edges
    const float sw  = std::clamp(static_cast<float>(visorWidth),  0.01f, 1.0f);
    const float sh  = std::clamp(static_cast<float>(visorHeight), 0.01f, 1.0f);
    const float hw  = bboxW * 0.5f * sw;
    const float hh  = bboxH * 0.5f * sh;
    const float cx  = (bboxMinX + bboxMaxX) * 0.5f
                    + (bboxW * 0.5f - hw) * static_cast<float>(visorOffsetX);
    const float cy  = (bboxMinY + bboxMaxY) * 0.5f
                    + (bboxH * 0.5f - hh) * static_cast<float>(visorOffsetY);
    const float exp = VisorCurveExponent(visorCurve);
    // Apex offset, mirroring BeanMaskEditor.BuildGeometry (flip in ApexYFromConfigNdc).
    const float apexY = std::clamp(cy + ApexYFromConfigNdc(visorOuterApexY) * hh * 2.0f,
                                   cy - hh + 0.001f, cy + hh - 0.001f);

    const uint32_t segCount = (std::min)(VisorCurveSegments(), vertCapacity / (visorAntialiasing ? 12u : 6u));
    if (segCount < 4u) return 0;

    constexpr float kTwoPi = 6.28318530717958647692f;
    std::vector<std::pair<float, float>> inner(segCount);
    std::vector<std::pair<float, float>> feather(segCount);
    for (uint32_t i = 0; i < segCount; ++i) {
        const float angle = kTwoPi * static_cast<float>(i) / static_cast<float>(segCount);
        const float c  = std::cos(angle);
        const float s  = std::sin(angle);
        const float sc = (c < 0.0f ? -1.0f : 1.0f) * std::pow(std::abs(c), 2.0f / exp);
        const float syScale = s < 0.0f ? (apexY - (cy - hh)) : ((cy + hh) - apexY);
        const float ss = (s < 0.0f ? -1.0f : 1.0f) * std::pow(std::abs(s), 2.0f / exp) * syScale;
        const float x = std::clamp(cx + sc * hw, bboxMinX + 0.001f, bboxMaxX - 0.001f);
        const float y = std::clamp(apexY + ss, bboxMinY + 0.001f, bboxMaxY - 0.001f);
        inner[i] = {x, y};
        const float toCx = cx - x;
        const float toCy = apexY - y;
        const float len = std::sqrt(toCx * toCx + toCy * toCy);
        feather[i] = len > 0.00001f
            ? std::pair<float, float>{
                std::clamp(x + (toCx / len) * featherX, bboxMinX + 0.001f, bboxMaxX - 0.001f),
                std::clamp(y + (toCy / len) * featherY, bboxMinY + 0.001f, bboxMaxY - 0.001f)}
            : inner[i];
    }

    uint32_t v = 0;
    for (uint32_t i = 0; i < segCount; ++i) {
        const auto [px, py] = inner[i];
        const auto [qx, qy] = inner[(i + 1) % segCount];
        const float mid = kTwoPi * (static_cast<float>(i) + 0.5f) / static_cast<float>(segCount);
        if (std::abs(std::cos(mid)) > std::abs(std::sin(mid))) {
            const float ex = std::cos(mid) < 0.0f ? bboxMinX : bboxMaxX;
            PushTri(vertsOut, v, vertCapacity, ex,py, ex,qy, qx,qy);
            PushTri(vertsOut, v, vertCapacity, ex,py, qx,qy, px,py);
        } else {
            const float ey = std::sin(mid) < 0.0f ? bboxMinY : bboxMaxY;
            PushTri(vertsOut, v, vertCapacity, px,ey, qx,ey, qx,qy);
            PushTri(vertsOut, v, vertCapacity, px,ey, qx,qy, px,py);
        }
        if (visorAntialiasing && v + 6 <= vertCapacity) {
            const auto [fpX, fpY] = feather[i];
            const auto [fqX, fqY] = feather[(i + 1) % segCount];
            PushTri(vertsOut, v, vertCapacity, px, py, qx, qy, fqX, fqY, 1.0f, 1.0f, 0.0f);
            PushTri(vertsOut, v, vertCapacity, px, py, fqX, fqY, fpX, fpY, 1.0f, 0.0f, 0.0f);
        }
    }
    return v;
}

// Nose-divot fill: solid triangles between a cubic bezier and the opening's BOTTOM
// edge (NDC yBase), from the eye-rect centre to the inner edge. The bezier math is
// identical to BeanMaskEditor.AddNoseBridgeCurve (horizontal tangents at both ends;
// width/rise/peakX/steepness shape the handles), made sign-aware so the right eye
// mirrors correctly. Every emitted vertex is clamped into [yBase, yTop], so the divot
// is structurally bottom-only — it can never appear in the upper part of the lens.
void BuildNoseBridgeCurve(
    VisorVertex* vertsOut, uint32_t& v, uint32_t vertCapacity,
    float startX, float endX, float yBase, float yTop) {
    const float dx = endX - startX;
    if (std::abs(dx) < 0.0005f || yTop - yBase < 0.0005f) return;

    const float w  = static_cast<float>(std::clamp(visorInnerBridgeWidth, 0.0, 1.0));
    const float st = static_cast<float>(std::clamp(visorInnerBridgeSteepness, 0.0, 1.0));
    const float rs = static_cast<float>(std::clamp(visorInnerBridgeRise, 0.0, 0.5));
    const float px = static_cast<float>(std::clamp(visorInnerBridgePeakX, 0.0, 1.0));

    // Handle length proportional to the horizontal span, bridge width, and steepness
    // (same formula as the UI preview).
    const float handleLength = std::abs(dx) * (0.3f + w * 0.4f) * (0.5f + st * 0.5f);
    const float dir = dx > 0.0f ? 1.0f : -1.0f;
    const float dy  = yTop - yBase;

    const float p0x = startX,                                           p0y = yBase;
    const float p1x = startX + dir * handleLength * (0.5f + px * 0.5f), p1y = yBase + rs * dy;
    const float p2x = endX - dir * handleLength * (1.0f - px * 0.5f),   p2y = yTop - rs * dy;
    const float p3x = endX,                                             p3y = yTop;

    const float xLo = (std::min)(startX, endX);
    const float xHi = (std::max)(startX, endX);

    constexpr uint32_t segCount = 32;
    float prevX = p0x, prevY = p0y;
    for (uint32_t i = 1; i <= segCount; ++i) {
        const float t = static_cast<float>(i) / static_cast<float>(segCount);
        const float mt = 1.0f - t;
        const float mt2 = mt * mt, mt3 = mt2 * mt, t2 = t * t, t3 = t2 * t;
        float x = mt3 * p0x + 3.0f * mt2 * t * p1x + 3.0f * mt * t2 * p2x + t3 * p3x;
        float y = mt3 * p0y + 3.0f * mt2 * t * p1y + 3.0f * mt * t2 * p2y + t3 * p3y;
        x = std::clamp(x, xLo, xHi);
        y = std::clamp(y, yBase, yTop);
        // Trapezoid between this curve segment and the bottom edge.
        if (v + 6 <= vertCapacity) {
            PushTri(vertsOut, v, vertCapacity, prevX, yBase, prevX, prevY, x, y);
            PushTri(vertsOut, v, vertCapacity, prevX, yBase, x, y, x, yBase);
        }
        prevX = x; prevY = y;
    }
}

// Builds a per-eye visor mask that stays open toward the inner eye edge.
// Left eye: curved cap on the left/outer side, open toward the right/inner side.
// Right eye: mirrored. Top/bottom bands span the full eye rect so the corners are
// fully black instead of leaking rectangular game pixels around the curve.
uint32_t BuildOpenInnerEyeVisorVerts(
    float bboxMinX, float bboxMaxX, float bboxMinY, float bboxMaxY,
    bool outerLeft, float featherX, float featherY,
    VisorVertex* vertsOut, uint32_t vertCapacity) {
    const float bboxW = bboxMaxX - bboxMinX;
    const float bboxH = bboxMaxY - bboxMinY;
    if (bboxW < 0.001f || bboxH < 0.001f || vertCapacity < 18) return 0;

    // Hardcoded maximum corner coverage: opening fills the full bbox, mask extends to edges
    const float sw  = std::clamp(static_cast<float>(visorWidth),  0.01f, 1.0f);
    const float sh  = std::clamp(static_cast<float>(visorHeight), 0.01f, 1.0f);
    const float hw  = bboxW * 0.5f * sw;
    const float hh  = bboxH * 0.5f * sh;
    const float cx  = (bboxMinX + bboxMaxX) * 0.5f;
    const float cy  = (bboxMinY + bboxMaxY) * 0.5f;
    const float exp = VisorCurveExponent(visorCurve);
    const float y0 = std::clamp(cy - hh, bboxMinY + 0.001f, bboxMaxY - 0.001f);
    const float y1 = std::clamp(cy + hh, bboxMinY + 0.001f, bboxMaxY - 0.001f);
    if (y1 <= y0) return 0;
    // Outer-curve apex, offset by the user's apex-y (flip handled in ApexYFromConfigNdc).
    // Mirrors BeanMaskEditor.AddOpenInnerHalf: the curve's vertical scale is split at
    // the apex so the shape stays inside [y0, y1] at any apex position.
    const float apexY = std::clamp(cy + ApexYFromConfigNdc(visorOuterApexY) * (y1 - y0), y0 + 0.001f, y1 - 0.001f);

    uint32_t v = 0;
    auto quad = [&](float x0, float yq0, float x1, float yq1) {
        if (x1 <= x0 || yq1 <= yq0 || v + 6 > vertCapacity) return;
        PushQuad(vertsOut, v, vertCapacity, x0, yq0, x1, yq1);
    };

    // Hard-mask top and bottom across the whole submitted eye rect; this fixes the
    // visible-corner leak from the closed oval path.
    quad(bboxMinX, bboxMinY, bboxMaxX, y0);
    quad(bboxMinX, y1, bboxMaxX, bboxMaxY);
    if (visorAntialiasing && featherY > 0.0f) {
        const float topFeatherY = std::clamp(y0 + featherY, y0, y1);
        const float bottomFeatherY = std::clamp(y1 - featherY, y0, y1);
        if (topFeatherY > y0 && v + 6 <= vertCapacity) {
            PushTri(vertsOut, v, vertCapacity, bboxMinX, y0, bboxMaxX, y0, bboxMaxX, topFeatherY, 1.0f, 1.0f, 0.0f);
            PushTri(vertsOut, v, vertCapacity, bboxMinX, y0, bboxMaxX, topFeatherY, bboxMinX, topFeatherY, 1.0f, 0.0f, 0.0f);
        }
        if (bottomFeatherY < y1 && v + 6 <= vertCapacity) {
            PushTri(vertsOut, v, vertCapacity, bboxMinX, bottomFeatherY, bboxMaxX, bottomFeatherY, bboxMaxX, y1, 0.0f, 0.0f, 1.0f);
            PushTri(vertsOut, v, vertCapacity, bboxMinX, bottomFeatherY, bboxMaxX, y1, bboxMinX, y1, 0.0f, 1.0f, 1.0f);
        }
    }

    const uint32_t segCount = (std::min)(VisorCurveSegments(), (vertCapacity > v ? (vertCapacity - v) / (visorAntialiasing ? 12u : 6u) : 0u));
    if (segCount < 4u) return v;

    constexpr float kPi = 3.14159265358979323846f;
    std::vector<std::pair<float, float>> curve(segCount + 1);
    std::vector<std::pair<float, float>> feather(segCount + 1);
    for (uint32_t i = 0; i <= segCount; ++i) {
        const float t = -0.5f * kPi + kPi * static_cast<float>(i) / static_cast<float>(segCount);
        const float c = (std::max)(0.0f, std::cos(t));
        const float s = std::sin(t);
        const float cxOffset = std::pow(c, 2.0f / exp) * hw;
        // Vertical scale split at the apex: below-apex points scale to (apexY - y0),
        // above-apex points to (y1 - apexY). Identical to the UI preview.
        const float syScale = s < 0.0f ? (apexY - y0) : (y1 - apexY);
        const float sy = (s < 0.0f ? -1.0f : 1.0f) * std::pow(std::abs(s), 2.0f / exp) * syScale;
        const float x = outerLeft ? (cx - cxOffset) : (cx + cxOffset);
        const float y = apexY + sy;
        const float clampedX = std::clamp(x, bboxMinX + 0.001f, bboxMaxX - 0.001f);
        const float clampedY = std::clamp(y, bboxMinY + 0.001f, bboxMaxY - 0.001f);
        curve[i] = {clampedX, clampedY};
        const float inwardX = outerLeft ? clampedX + featherX : clampedX - featherX;
        feather[i] = {
            std::clamp(inwardX, bboxMinX + 0.001f, bboxMaxX - 0.001f),
            std::clamp(clampedY, bboxMinY + 0.001f, bboxMaxY - 0.001f)};
    }

    const float outerX = outerLeft ? bboxMinX : bboxMaxX;
    for (uint32_t i = 0; i < segCount && v + 6 <= vertCapacity; ++i) {
        const auto [x0c, y0c] = curve[i];
        const auto [x1c, y1c] = curve[i + 1];
        if (outerLeft) {
            PushTri(vertsOut, v, vertCapacity, outerX, y0c, x1c, y1c, x0c, y0c);
            PushTri(vertsOut, v, vertCapacity, outerX, y0c, outerX, y1c, x1c, y1c);
        } else {
            PushTri(vertsOut, v, vertCapacity, x0c, y0c, x1c, y1c, outerX, y0c);
            PushTri(vertsOut, v, vertCapacity, outerX, y0c, x1c, y1c, outerX, y1c);
        }
        if (visorAntialiasing && v + 6 <= vertCapacity) {
            const auto [fx0, fy0] = feather[i];
            const auto [fx1, fy1] = feather[i + 1];
            PushTri(vertsOut, v, vertCapacity, x0c, y0c, x1c, y1c, fx1, fy1, 1.0f, 1.0f, 0.0f);
            PushTri(vertsOut, v, vertCapacity, x0c, y0c, fx1, fy1, fx0, fy0, 1.0f, 0.0f, 0.0f);
        }
    }

    // Nose divot: fill between the bezier and the opening's BOTTOM edge (NDC y0),
    // from the eye-rect centre to the inner edge. Note: the old implementation used
    // y1 here — NDC y-up vs UI y-down — which put the divot at the TOP of the lens.
    if (visorInnerLowerY > 0.0) {
        const float innerX = outerLeft ? bboxMaxX : bboxMinX;
        const float bandTopY = std::clamp(y0 + static_cast<float>(visorInnerLowerY) * (y1 - y0), y0, y1);
        BuildNoseBridgeCurve(vertsOut, v, vertCapacity, cx, innerX, y0, bandTopY);
    }

    return v;
}

float FovLeftTan(const XrFovf& fov) { return static_cast<float>(std::tan(static_cast<double>(fov.angleLeft))); }
float FovRightTan(const XrFovf& fov) { return static_cast<float>(std::tan(static_cast<double>(fov.angleRight))); }
float FovDownTan(const XrFovf& fov) { return static_cast<float>(std::tan(static_cast<double>(fov.angleDown))); }
float FovUpTan(const XrFovf& fov) { return static_cast<float>(std::tan(static_cast<double>(fov.angleUp))); }

float TanToBboxX(float tx, const XrFovf& fov, float bboxMinX, float bboxMaxX) {
    const float l = FovLeftTan(fov);
    const float r = FovRightTan(fov);
    if (std::abs(r - l) < 0.00001f) return (bboxMinX + bboxMaxX) * 0.5f;
    const float ndc = ((tx - l) / (r - l)) * 2.0f - 1.0f;
    return bboxMinX + (ndc + 1.0f) * 0.5f * (bboxMaxX - bboxMinX);
}

float TanToBboxY(float ty, const XrFovf& fov, float bboxMinY, float bboxMaxY) {
    const float d = FovDownTan(fov);
    const float u = FovUpTan(fov);
    if (std::abs(u - d) < 0.00001f) return (bboxMinY + bboxMaxY) * 0.5f;
    const float ndc = ((ty - d) / (u - d)) * 2.0f - 1.0f;
    return bboxMinY + (ndc + 1.0f) * 0.5f * (bboxMaxY - bboxMinY);
}

float NdcToTanX(float x, const XrFovf& fov) {
    const float l = FovLeftTan(fov);
    const float r = FovRightTan(fov);
    return l + (x + 1.0f) * 0.5f * (r - l);
}

float NdcToTanY(float y, const XrFovf& fov) {
    const float d = FovDownTan(fov);
    const float u = FovUpTan(fov);
    return d + (y + 1.0f) * 0.5f * (u - d);
}

uint32_t BuildProjectedPartnerVisorVerts(
    float bboxMinX, float bboxMaxX, float bboxMinY, float bboxMaxY,
    bool drawLeftBoundary,
    const XrFovf& sourceFov, const XrFovf& targetFov,
    VisorVertex* vertsOut, uint32_t vertCapacity) {
    if (vertCapacity < 24) return 0;

    const float threshold = (bboxMaxX - bboxMinX) * 0.025f;
    const float sourceEdgeTan = drawLeftBoundary ? FovLeftTan(sourceFov) : FovRightTan(sourceFov);
    const float projectedEdgeX = TanToBboxX(sourceEdgeTan, targetFov, bboxMinX, bboxMaxX);
    if (drawLeftBoundary) {
        if (projectedEdgeX <= bboxMinX + threshold) return 0;
    } else {
        if (projectedEdgeX >= bboxMaxX - threshold) return 0;
    }

    // Hardcoded maximum corner coverage: opening fills the full bbox, mask extends to edges
    const float sw = std::clamp(static_cast<float>(visorWidth), 0.01f, 1.0f);
    const float sh = std::clamp(static_cast<float>(visorHeight), 0.01f, 1.0f);
    const float bboxW = bboxMaxX - bboxMinX;
    const float bboxH = bboxMaxY - bboxMinY;
    const float hw = bboxW * 0.5f * sw;
    const float hh = bboxH * 0.5f * sh;
    const float sourceCx = (bboxMinX + bboxMaxX) * 0.5f + (bboxW * 0.5f - hw) * static_cast<float>(visorOffsetX);
    const float sourceCy = (bboxMinY + bboxMaxY) * 0.5f + (bboxH * 0.5f - hh) * static_cast<float>(visorOffsetY);
    const float exp = VisorCurveExponent(visorCurve);
    const uint32_t segCount = VisorCurveSegments();
    constexpr float kPi = 3.14159265358979323846f;

    std::vector<std::pair<float, float>> curve(segCount + 1);
    for (uint32_t i = 0; i <= segCount; ++i) {
        const float t = -0.5f * kPi + kPi * static_cast<float>(i) / static_cast<float>(segCount);
        const float c = (std::max)(0.0f, std::cos(t));
        const float s = std::sin(t);
        const float cxOffset = std::pow(c, 2.0f / exp) * hw;
        const float sy = (s < 0.0f ? -1.0f : 1.0f) * std::pow(std::abs(s), 2.0f / exp) * hh;
        const float sourceX = std::clamp(sourceCx + (drawLeftBoundary ? -cxOffset : cxOffset), -1.0f, 1.0f);
        const float sourceY = std::clamp(sourceCy + sy, -1.0f, 1.0f);
        curve[i] = {
            std::clamp(TanToBboxX(NdcToTanX(sourceX, sourceFov), targetFov, bboxMinX, bboxMaxX), bboxMinX, bboxMaxX),
            std::clamp(TanToBboxY(NdcToTanY(sourceY, sourceFov), targetFov, bboxMinY, bboxMaxY), bboxMinY, bboxMaxY)};
    }

    uint32_t v = 0;
    for (uint32_t i = 0; i < segCount && v + 6 <= vertCapacity; ++i) {
        const auto [x0, y0] = curve[i];
        const auto [x1, y1] = curve[i + 1];
        if (std::abs(y1 - y0) < 0.00001f) continue;
        if (drawLeftBoundary) {
            if (x0 <= bboxMinX && x1 <= bboxMinX) continue;
            PushTri(vertsOut, v, vertCapacity, bboxMinX, y0, x1, y1, x0, y0);
            PushTri(vertsOut, v, vertCapacity, bboxMinX, y0, bboxMinX, y1, x1, y1);
        } else {
            if (x0 >= bboxMaxX && x1 >= bboxMaxX) continue;
            PushTri(vertsOut, v, vertCapacity, x0, y0, x1, y1, bboxMaxX, y0);
            PushTri(vertsOut, v, vertCapacity, bboxMaxX, y0, x1, y1, bboxMaxX, y1);
        }
    }
    return v;
}

// Draws the visor border directly into the given eye texture/slice/rect.
// Pure draw: no swapchain lookup, no locking — the caller resolves these and must
// call this on the render thread that owns the D3D11 immediate context. Called from
// XRViewLab_xrReleaseSwapchainImage, i.e. while the app still owns the image and
// immediately before the runtime takes it for composition (last legal write point).
void DrawVisorBorderToTexture(
    ID3D11Texture2D* tex, uint32_t arrSize, int64_t scFormat,
    const EyeView& eye, const std::vector<EyeView>& allViews,
    ID3D11RenderTargetView* cachedRtv = nullptr) {
    if (!g_d3d11Mask.initialized || !g_d3d11Mask.context || !tex) return;
    const XrRect2Di& rect = eye.rect;
    const uint32_t arrayIndex = eye.arraySlice;
    const uint32_t viewIndex = eye.viewIndex;

    D3D11_TEXTURE2D_DESC texDesc{};
    tex->GetDesc(&texDesc);
    if (texDesc.Width == 0 || texDesc.Height == 0 || rect.extent.width <= 0 || rect.extent.height <= 0) {
        return;
    }

    // Draw in full-texture NDC so we can cover the whole submitted eye rectangle,
    // including the corner areas that were outside the old rect-local oval draw.
    const float bboxMinX = std::clamp((2.0f * static_cast<float>(rect.offset.x) / static_cast<float>(texDesc.Width)) - 1.0f, -1.0f, 1.0f);
    const float bboxMaxX = std::clamp((2.0f * static_cast<float>(rect.offset.x + rect.extent.width) / static_cast<float>(texDesc.Width)) - 1.0f, -1.0f, 1.0f);
    const float bboxMaxY = std::clamp(1.0f - (2.0f * static_cast<float>(rect.offset.y) / static_cast<float>(texDesc.Height)), -1.0f, 1.0f);
    const float bboxMinY = std::clamp(1.0f - (2.0f * static_cast<float>(rect.offset.y + rect.extent.height) / static_cast<float>(texDesc.Height)), -1.0f, 1.0f);
    if (bboxMaxX <= bboxMinX || bboxMaxY <= bboxMinY) {
        return;
    }

    constexpr uint32_t kMaxVerts = 4096;
    std::vector<VisorVertex> verts(kMaxVerts);
    const bool outerLeft = viewIndex == 0;
    // The visor shape follows the same setting as the UI preview: "Stencil outer edges
    // only" ON = per-eye open-inner arch (inner/nose side stays visible), OFF = closed
    // bean that fully surrounds the opening (inner edge stenciled too).
    const bool openInnerShape = outerEdgeVisibilityMaskOnly;
    const float featherX = visorAntialiasing ? (3.0f / static_cast<float>(texDesc.Width)) : 0.0f;
    const float featherY = visorAntialiasing ? (3.0f / static_cast<float>(texDesc.Height)) : 0.0f;
    uint32_t vertCount = openInnerShape
        ? BuildOpenInnerEyeVisorVerts(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, outerLeft, featherX, featherY, verts.data(), kMaxVerts)
        : BuildVisorBorderVerts(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, featherX, featherY, verts.data(), kMaxVerts);
    // Projected partner-eye boundary: blacks out the inner-eye band the cropped partner
    // eye can no longer render. This is exactly the inner-edge stenciling the user turns
    // OFF with "Stencil outer edges only" — only draw it when that setting is unchecked.
    if (!openInnerShape && allViews.size() >= 2 && viewIndex < 2 && vertCount < kMaxVerts) {
        const EyeView& leftEye = allViews[0].viewIndex == 0 ? allViews[0] : allViews[1];
        const EyeView& rightEye = allViews[0].viewIndex == 1 ? allViews[0] : allViews[1];
        if (viewIndex == 0) {
            vertCount += BuildProjectedPartnerVisorVerts(
                bboxMinX, bboxMaxX, bboxMinY, bboxMaxY,
                false, rightEye.fov, eye.fov, verts.data() + vertCount, kMaxVerts - vertCount);
        } else {
            vertCount += BuildProjectedPartnerVisorVerts(
                bboxMinX, bboxMaxX, bboxMinY, bboxMaxY,
                true, leftEye.fov, eye.fov, verts.data() + vertCount, kMaxVerts - vertCount);
        }
    }
    if (vertCount == 0) {
        if (!g_diagDrawNoVerts.exchange(true))
            Log("d3d11 mask DIAG: FAIL border geometry produced 0 vertices\n");
        return;
    }

    D3D11_MAPPED_SUBRESOURCE mapped{};
    if (FAILED(g_d3d11Mask.context->Map(g_d3d11Mask.vb, 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped))) {
        if (!g_diagDrawMapFailed.exchange(true))
            Log("d3d11 mask DIAG: FAIL vertex buffer Map failed\n");
        return;
    }
    memcpy(mapped.pData, verts.data(), vertCount * sizeof(VisorVertex));
    g_d3d11Mask.context->Unmap(g_d3d11Mask.vb, 0);

    // Pick a concrete RTV format. Runtimes frequently allocate swapchain textures as
    // TYPELESS so they can be viewed as SRGB or UNORM; an RTV cannot use a TYPELESS
    // format. Prefer the app-requested swapchain format (mapped to non-SRGB) which is
    // always concrete and view-compatible with the underlying texture. Black writes are
    // identical under SRGB/UNORM so the non-SRGB view is safe.
    DXGI_FORMAT rtvFormat = GetNonSRGBFormat(static_cast<DXGI_FORMAT>(scFormat));
    if (rtvFormat == DXGI_FORMAT_UNKNOWN) {
        rtvFormat = GetNonSRGBFormat(texDesc.Format);
    }

    D3D11_RENDER_TARGET_VIEW_DESC rtvDesc{};
    rtvDesc.Format = rtvFormat;
    if (arrSize > 1) {
        rtvDesc.ViewDimension                  = D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
        rtvDesc.Texture2DArray.MipSlice        = 0;
        rtvDesc.Texture2DArray.FirstArraySlice = arrayIndex;
        rtvDesc.Texture2DArray.ArraySize       = 1;
    } else {
        rtvDesc.ViewDimension      = D3D11_RTV_DIMENSION_TEXTURE2D;
        rtvDesc.Texture2D.MipSlice = 0;
    }
    const bool ownsRtv = (cachedRtv == nullptr);
    ID3D11RenderTargetView* rtv = cachedRtv;
    if (!rtv) {
        const HRESULT rtvHr = g_d3d11Mask.device->CreateRenderTargetView(tex, &rtvDesc, &rtv);
        if (FAILED(rtvHr) || !rtv) {
            if (!g_diagDrawRtvFailed.exchange(true))
                Log("d3d11 mask DIAG: FAIL CreateRenderTargetView hr=0x%08X rtvFormat=%d texFormat=%d arrSize=%u slice=%u\n",
                    static_cast<unsigned>(rtvHr), static_cast<int>(rtvFormat),
                    static_cast<int>(texDesc.Format), arrSize, arrayIndex);
            return;
        }
    }

    D3D11_VIEWPORT vp{};
    vp.TopLeftX = 0.0f;
    vp.TopLeftY = 0.0f;
    vp.Width    = static_cast<float>(texDesc.Width);
    vp.Height   = static_cast<float>(texDesc.Height);
    vp.MaxDepth = 1.0f;

    // Save D3D11 state.
    ID3D11RenderTargetView* sRTVs[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT]{};
    ID3D11DepthStencilView* sDSV = nullptr;
    g_d3d11Mask.context->OMGetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, sRTVs, &sDSV);
    UINT sVPCount = D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE;
    D3D11_VIEWPORT sVPs[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{};
    g_d3d11Mask.context->RSGetViewports(&sVPCount, sVPs);
    ID3D11RasterizerState* sRS = nullptr;
    g_d3d11Mask.context->RSGetState(&sRS);
    ID3D11BlendState* sBS = nullptr; FLOAT sBF[4]{}; UINT sSM = 0;
    g_d3d11Mask.context->OMGetBlendState(&sBS, sBF, &sSM);
    ID3D11DepthStencilState* sDSS = nullptr; UINT sSRef = 0;
    g_d3d11Mask.context->OMGetDepthStencilState(&sDSS, &sSRef);
    ID3D11VertexShader* sVS = nullptr;
    g_d3d11Mask.context->VSGetShader(&sVS, nullptr, nullptr);
    ID3D11PixelShader* sPS = nullptr;
    g_d3d11Mask.context->PSGetShader(&sPS, nullptr, nullptr);
    ID3D11InputLayout* sLayout = nullptr;
    g_d3d11Mask.context->IAGetInputLayout(&sLayout);
    D3D11_PRIMITIVE_TOPOLOGY sTopo = D3D11_PRIMITIVE_TOPOLOGY_UNDEFINED;
    g_d3d11Mask.context->IAGetPrimitiveTopology(&sTopo);
    ID3D11Buffer* sVB = nullptr; UINT sStride = 0, sOff = 0;
    g_d3d11Mask.context->IAGetVertexBuffers(0, 1, &sVB, &sStride, &sOff);

    // Draw visor border.
    static const FLOAT kBF[] = {0.f, 0.f, 0.f, 0.f};
    UINT stride = sizeof(VisorVertex), offset = 0;
    g_d3d11Mask.context->OMSetRenderTargets(1, &rtv, nullptr);
    g_d3d11Mask.context->RSSetViewports(1, &vp);
    g_d3d11Mask.context->RSSetState(g_d3d11Mask.rs);
    g_d3d11Mask.context->OMSetBlendState(
        visorAntialiasing ? g_d3d11Mask.bs : g_d3d11Mask.bsOpaque, kBF, 0xFFFFFFFF);
    g_d3d11Mask.context->OMSetDepthStencilState(g_d3d11Mask.dss, 0);
    g_d3d11Mask.context->VSSetShader(g_d3d11Mask.vs, nullptr, 0);
    g_d3d11Mask.context->PSSetShader(g_d3d11Mask.ps, nullptr, 0);
    g_d3d11Mask.context->IASetInputLayout(g_d3d11Mask.layout);
    g_d3d11Mask.context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    g_d3d11Mask.context->IASetVertexBuffers(0, 1, &g_d3d11Mask.vb, &stride, &offset);
    g_d3d11Mask.context->Draw(vertCount, 0);

    if (!g_diagDrawOk.exchange(true))
        Log("d3d11 mask DIAG: OK draw executed verts=%u rtvFormat=%d texFormat=%d arrSize=%u slice=%u view=%u outer=%s "
            "rect=(%d,%d %dx%d) tex=%ux%u aa=%d hd=%d\n",
            vertCount, static_cast<int>(rtvFormat), static_cast<int>(texDesc.Format),
            arrSize, arrayIndex, viewIndex, outerLeft ? "left" : "right",
            rect.offset.x, rect.offset.y, rect.extent.width, rect.extent.height,
            texDesc.Width, texDesc.Height,
            visorAntialiasing ? 1 : 0, visorHD ? 1 : 0);

    // Restore D3D11 state.
    g_d3d11Mask.context->OMSetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, sRTVs, sDSV);
    if (sVPCount > 0) g_d3d11Mask.context->RSSetViewports(sVPCount, sVPs);
    g_d3d11Mask.context->RSSetState(sRS);
    g_d3d11Mask.context->OMSetBlendState(sBS, sBF, sSM);
    g_d3d11Mask.context->OMSetDepthStencilState(sDSS, sSRef);
    g_d3d11Mask.context->VSSetShader(sVS, nullptr, 0);
    g_d3d11Mask.context->PSSetShader(sPS, nullptr, 0);
    g_d3d11Mask.context->IASetInputLayout(sLayout);
    g_d3d11Mask.context->IASetPrimitiveTopology(sTopo);
    g_d3d11Mask.context->IASetVertexBuffers(0, 1, &sVB, &sStride, &sOff);

    if (ownsRtv) rtv->Release();
    for (ID3D11RenderTargetView* savedRtv : sRTVs) { if (savedRtv) savedRtv->Release(); }
    if (sDSV) sDSV->Release();
    if (sRS)  sRS->Release();  if (sBS)  sBS->Release();
    if (sDSS) sDSS->Release(); if (sVS)  sVS->Release();
    if (sPS)  sPS->Release();  if (sLayout) sLayout->Release();
    if (sVB)  sVB->Release();

    // Force submission so a separate-device runtime encoder (e.g. VDXR streaming)
    // sees the write before it consumes the released image.
    g_d3d11Mask.context->Flush();
}

void DrawEdgeGuardToTexture(
    ID3D11Texture2D* tex, uint32_t arrSize, int64_t scFormat,
    uint32_t arrayIndex, const XrRect2Di& rect, int pixels,
    ID3D11RenderTargetView* cachedRtv = nullptr) {
    if (!g_d3d11Mask.initialized || !g_d3d11Mask.context || !tex || pixels <= 0 || rect.extent.width <= 0 || rect.extent.height <= 0) return;

    D3D11_TEXTURE2D_DESC texDesc{};
    tex->GetDesc(&texDesc);
    if (texDesc.Width == 0 || texDesc.Height == 0) return;

    const float minX = std::clamp((2.0f * static_cast<float>(rect.offset.x) / static_cast<float>(texDesc.Width)) - 1.0f, -1.0f, 1.0f);
    const float maxX = std::clamp((2.0f * static_cast<float>(rect.offset.x + rect.extent.width) / static_cast<float>(texDesc.Width)) - 1.0f, -1.0f, 1.0f);
    const float maxY = std::clamp(1.0f - (2.0f * static_cast<float>(rect.offset.y) / static_cast<float>(texDesc.Height)), -1.0f, 1.0f);
    const float minY = std::clamp(1.0f - (2.0f * static_cast<float>(rect.offset.y + rect.extent.height) / static_cast<float>(texDesc.Height)), -1.0f, 1.0f);
    if (maxX <= minX || maxY <= minY) return;

    const float dx = (2.0f * static_cast<float>(pixels)) / static_cast<float>(texDesc.Width);
    const float dy = (2.0f * static_cast<float>(pixels)) / static_cast<float>(texDesc.Height);
    VisorVertex verts[24]{};
    uint32_t v = 0;
    auto tri = [&](float ax, float ay, float bx, float by, float cx, float cy) {
        PushTri(verts, v, 24, ax, ay, bx, by, cx, cy);
    };
    auto quad = [&](float x0, float y0, float x1, float y1) {
        if (x1 <= x0 || y1 <= y0 || v + 6 > 24) return;
        tri(x0, y0, x1, y0, x1, y1);
        tri(x0, y0, x1, y1, x0, y1);
    };
    quad(minX, minY, (std::min)(minX + dx, maxX), maxY);
    quad((std::max)(maxX - dx, minX), minY, maxX, maxY);
    quad(minX, minY, maxX, (std::min)(minY + dy, maxY));
    quad(minX, (std::max)(maxY - dy, minY), maxX, maxY);
    if (v == 0) return;

    D3D11_MAPPED_SUBRESOURCE mapped{};
    if (FAILED(g_d3d11Mask.context->Map(g_d3d11Mask.vb, 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped))) return;
    memcpy(mapped.pData, verts, v * sizeof(VisorVertex));
    g_d3d11Mask.context->Unmap(g_d3d11Mask.vb, 0);

    DXGI_FORMAT rtvFormat = GetNonSRGBFormat(static_cast<DXGI_FORMAT>(scFormat));
    if (rtvFormat == DXGI_FORMAT_UNKNOWN) rtvFormat = GetNonSRGBFormat(texDesc.Format);

    D3D11_RENDER_TARGET_VIEW_DESC rtvDesc{};
    rtvDesc.Format = rtvFormat;
    if (arrSize > 1) {
        rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
        rtvDesc.Texture2DArray.MipSlice = 0;
        rtvDesc.Texture2DArray.FirstArraySlice = arrayIndex;
        rtvDesc.Texture2DArray.ArraySize = 1;
    } else {
        rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2D;
        rtvDesc.Texture2D.MipSlice = 0;
    }
    const bool ownsRtv = (cachedRtv == nullptr);
    ID3D11RenderTargetView* rtv = cachedRtv;
    if (!rtv && (FAILED(g_d3d11Mask.device->CreateRenderTargetView(tex, &rtvDesc, &rtv)) || !rtv)) {
        return;
    }

    ID3D11RenderTargetView* sRTVs[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT]{};
    ID3D11DepthStencilView* sDSV = nullptr;
    g_d3d11Mask.context->OMGetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, sRTVs, &sDSV);
    UINT sVPCount = D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE;
    D3D11_VIEWPORT sVPs[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{};
    g_d3d11Mask.context->RSGetViewports(&sVPCount, sVPs);
    ID3D11RasterizerState* sRS = nullptr; g_d3d11Mask.context->RSGetState(&sRS);
    ID3D11BlendState* sBS = nullptr; FLOAT sBF[4]{}; UINT sSM = 0; g_d3d11Mask.context->OMGetBlendState(&sBS, sBF, &sSM);
    ID3D11DepthStencilState* sDSS = nullptr; UINT sSRef = 0; g_d3d11Mask.context->OMGetDepthStencilState(&sDSS, &sSRef);
    ID3D11VertexShader* sVS = nullptr; g_d3d11Mask.context->VSGetShader(&sVS, nullptr, nullptr);
    ID3D11PixelShader* sPS = nullptr; g_d3d11Mask.context->PSGetShader(&sPS, nullptr, nullptr);
    ID3D11InputLayout* sLayout = nullptr; g_d3d11Mask.context->IAGetInputLayout(&sLayout);
    D3D11_PRIMITIVE_TOPOLOGY sTopo = D3D11_PRIMITIVE_TOPOLOGY_UNDEFINED; g_d3d11Mask.context->IAGetPrimitiveTopology(&sTopo);
    ID3D11Buffer* sVB = nullptr; UINT sStride = 0, sOff = 0; g_d3d11Mask.context->IAGetVertexBuffers(0, 1, &sVB, &sStride, &sOff);

    D3D11_VIEWPORT vp{}; vp.Width = static_cast<float>(texDesc.Width); vp.Height = static_cast<float>(texDesc.Height); vp.MaxDepth = 1.0f;
    static const FLOAT kBF[] = {0.f, 0.f, 0.f, 0.f};
    UINT stride = sizeof(VisorVertex), offset = 0;
    g_d3d11Mask.context->OMSetRenderTargets(1, &rtv, nullptr);
    g_d3d11Mask.context->RSSetViewports(1, &vp);
    g_d3d11Mask.context->RSSetState(g_d3d11Mask.rs);
    g_d3d11Mask.context->OMSetBlendState(
        visorAntialiasing ? g_d3d11Mask.bs : g_d3d11Mask.bsOpaque, kBF, 0xFFFFFFFF);
    g_d3d11Mask.context->OMSetDepthStencilState(g_d3d11Mask.dss, 0);
    g_d3d11Mask.context->VSSetShader(g_d3d11Mask.vs, nullptr, 0);
    g_d3d11Mask.context->PSSetShader(g_d3d11Mask.ps, nullptr, 0);
    g_d3d11Mask.context->IASetInputLayout(g_d3d11Mask.layout);
    g_d3d11Mask.context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    g_d3d11Mask.context->IASetVertexBuffers(0, 1, &g_d3d11Mask.vb, &stride, &offset);
    g_d3d11Mask.context->Draw(v, 0);

    g_d3d11Mask.context->OMSetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, sRTVs, sDSV);
    if (sVPCount > 0) g_d3d11Mask.context->RSSetViewports(sVPCount, sVPs);
    g_d3d11Mask.context->RSSetState(sRS);
    g_d3d11Mask.context->OMSetBlendState(sBS, sBF, sSM);
    g_d3d11Mask.context->OMSetDepthStencilState(sDSS, sSRef);
    g_d3d11Mask.context->VSSetShader(sVS, nullptr, 0);
    g_d3d11Mask.context->PSSetShader(sPS, nullptr, 0);
    g_d3d11Mask.context->IASetInputLayout(sLayout);
    g_d3d11Mask.context->IASetPrimitiveTopology(sTopo);
    g_d3d11Mask.context->IASetVertexBuffers(0, 1, &sVB, &sStride, &sOff);

    if (ownsRtv) rtv->Release();
    for (ID3D11RenderTargetView* savedRtv : sRTVs) { if (savedRtv) savedRtv->Release(); }
    if (sDSV) sDSV->Release();
    if (sRS) sRS->Release(); if (sBS) sBS->Release();
    if (sDSS) sDSS->Release(); if (sVS) sVS->Release();
    if (sPS) sPS->Release(); if (sLayout) sLayout->Release();
    if (sVB) sVB->Release();
    g_d3d11Mask.context->Flush();
}

ID3D11RenderTargetView* CachedRtvFor(const TrackedSwapchain& ts, uint32_t imageIndex, uint32_t arraySlice);

void DrawCapturedProjectionTextures(bool drawEdgeGuard, bool drawVisor, const char* tag) {
    if ((!drawEdgeGuard && !drawVisor) || !g_d3d11Mask.initialized || !g_d3d11Mask.context) return;

    struct PendingDraw {
        ID3D11Texture2D* tex = nullptr;
        uint32_t arrSize = 1;
        int64_t format = 0;
        std::vector<EyeView> views;
        std::vector<ID3D11RenderTargetView*> rtvs;
    };

    std::vector<PendingDraw> pending;
    {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        for (const auto& kv : g_swapchains) {
            const TrackedSwapchain& ts = kv.second;
            if (ts.session != g_d3d11Mask.session ||
                ts.lastAcquiredIndex >= ts.textures.size() ||
                ts.eyeViews.empty()) {
                continue;
            }
            PendingDraw draw{
                ts.textures[ts.lastAcquiredIndex],
                ts.arraySize,
                ts.format,
                ts.eyeViews,
                {}};
            if (draw.tex) draw.tex->AddRef();
            draw.rtvs.reserve(draw.views.size());
            for (const EyeView& ev : draw.views) {
                draw.rtvs.push_back(CachedRtvFor(ts, ts.lastAcquiredIndex, ev.arraySlice));
            }
            pending.push_back(std::move(draw));
        }
    }

    size_t eyeCount = 0;
    for (const PendingDraw& p : pending) {
        if (p.tex == nullptr) continue;
        for (size_t i = 0; i < p.views.size(); ++i) {
            const EyeView& ev = p.views[i];
            if (drawEdgeGuard) {
                DrawEdgeGuardToTexture(p.tex, p.arrSize, p.format, ev.arraySlice, ev.rect, edgeSmearPixels,
                    i < p.rtvs.size() ? p.rtvs[i] : nullptr);
            }
            if (drawVisor) {
                DrawVisorBorderToTexture(p.tex, p.arrSize, p.format, ev, p.views,
                    i < p.rtvs.size() ? p.rtvs[i] : nullptr);
            }
            ++eyeCount;
        }
        for (ID3D11RenderTargetView* rtv : p.rtvs) {
            if (rtv) rtv->Release();
        }
        p.tex->Release();
    }

    if (eyeCount > 0 && !g_diagEndFrameLateDraw.exchange(true)) {
        Log("%s: late xrEndFrame draw executed (%zu eye view(s))\n",
            tag ? tag : "d3d11 mask", eyeCount);
    }
}

ID3D11RenderTargetView* CachedRtvFor(const TrackedSwapchain& ts, uint32_t imageIndex, uint32_t arraySlice) {
    if (ts.rtvs.empty() || ts.arraySize == 0) return nullptr;
    const size_t rtvIndex = static_cast<size_t>(imageIndex) * ts.arraySize + arraySlice;
    if (rtvIndex >= ts.rtvs.size() || ts.rtvs[rtvIndex] == nullptr) return nullptr;
    ts.rtvs[rtvIndex]->AddRef();
    return ts.rtvs[rtvIndex];
}

void RebuildCachedRtvs(TrackedSwapchain& ts) {
    for (ID3D11RenderTargetView* rtv : ts.rtvs) {
        if (rtv) rtv->Release();
    }
    ts.rtvs.clear();

    if (!g_d3d11Mask.device || ts.textures.empty() || ts.arraySize == 0) return;

    DXGI_FORMAT rtvFormat = GetNonSRGBFormat(static_cast<DXGI_FORMAT>(ts.format));
    if (rtvFormat == DXGI_FORMAT_UNKNOWN && ts.textures[0]) {
        D3D11_TEXTURE2D_DESC desc{};
        ts.textures[0]->GetDesc(&desc);
        rtvFormat = GetNonSRGBFormat(desc.Format);
    }
    if (rtvFormat == DXGI_FORMAT_UNKNOWN) return;

    ts.rtvs.resize(ts.textures.size() * ts.arraySize, nullptr);
    for (size_t imageIndex = 0; imageIndex < ts.textures.size(); ++imageIndex) {
        ID3D11Texture2D* tex = ts.textures[imageIndex];
        if (!tex) continue;
        for (uint32_t slice = 0; slice < ts.arraySize; ++slice) {
            D3D11_RENDER_TARGET_VIEW_DESC rtvDesc{};
            rtvDesc.Format = rtvFormat;
            if (ts.arraySize > 1) {
                rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
                rtvDesc.Texture2DArray.MipSlice = 0;
                rtvDesc.Texture2DArray.FirstArraySlice = slice;
                rtvDesc.Texture2DArray.ArraySize = 1;
            } else {
                rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2D;
                rtvDesc.Texture2D.MipSlice = 0;
            }
            ID3D11RenderTargetView* rtv = nullptr;
            if (SUCCEEDED(g_d3d11Mask.device->CreateRenderTargetView(tex, &rtvDesc, &rtv)) && rtv) {
                ts.rtvs[imageIndex * ts.arraySize + slice] = rtv;
            }
        }
    }
}

// ---- Swapchain tracking for the D3D11 visor ----

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrCreateSwapchain(
    XrSession session, const XrSwapchainCreateInfo* info, XrSwapchain* swapchain) {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    const XrResult r = nextXrCreateSwapchain(session, info, swapchain);
    if (r == XR_SUCCESS && swapchain && *swapchain != XR_NULL_HANDLE && info) {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        TrackedSwapchain& ts = g_swapchains[*swapchain];
        ts.session   = session;
        ts.width     = info->width;
        ts.height    = info->height;
        ts.arraySize = info->arraySize;
        ts.sampleCount = info->sampleCount;
        ts.format    = info->format;
        ts.usageFlags = info->usageFlags;
    }
    return r;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrEnumerateSwapchainImages(
    XrSwapchain swapchain, uint32_t capacity, uint32_t* count, XrSwapchainImageBaseHeader* images) {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    const XrResult r = nextXrEnumerateSwapchainImages(swapchain, capacity, count, images);
    if (r == XR_SUCCESS && images && count && *count > 0 &&
        images->type == XR_TYPE_SWAPCHAIN_IMAGE_D3D11_KHR) {
        auto* d3d = reinterpret_cast<XrSwapchainImageD3D11KHR*>(images);
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        auto it = g_swapchains.find(swapchain);
        if (it != g_swapchains.end()) {
            it->second.textures.resize(*count, nullptr);
            for (uint32_t i = 0; i < *count; ++i) it->second.textures[i] = d3d[i].texture;
            RebuildCachedRtvs(it->second);
        }
    }
    return r;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrAcquireSwapchainImage(
    XrSwapchain swapchain, const XrSwapchainImageAcquireInfo* info, uint32_t* imageIndex) {
    const XrResult r = nextXrAcquireSwapchainImage(swapchain, info, imageIndex);
    if (r == XR_SUCCESS && imageIndex) {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        auto it = g_swapchains.find(swapchain);
        if (it != g_swapchains.end()) it->second.lastAcquiredIndex = *imageIndex;
    }
    return r;
}

std::atomic<bool> g_diagReleaseSeen{false};
std::atomic<bool> g_diagReleaseNoLayout{false};
std::atomic<uint32_t> g_releaseDrawLogCount{0};
std::atomic<bool> g_releaseDrewEdgeGuardThisFrame{false};
std::atomic<bool> g_releaseDrewVisorThisFrame{false};

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrReleaseSwapchainImage(
    XrSwapchain swapchain, const XrSwapchainImageReleaseInfo* info) {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    // Draw the visor border NOW — the app has finished rendering this image (it is
    // releasing it) and the runtime has not yet consumed it. This is the last point
    // at which the app side legitimately owns the image, so the write cannot be
    // overwritten by the app and is guaranteed present when the runtime composites.
    // Independent of the visibility-mask path; that path must never suppress this.

    if (enabled && maskEnabled &&
        g_d3d11Mask.initialized && g_d3d11Mask.context) {
        ID3D11Texture2D* tex = nullptr;
        uint32_t arrSize = 1;
        int64_t  scFormat = 0;
        std::vector<EyeView> views;
        std::vector<ID3D11RenderTargetView*> rtvs;
        bool tracked = false, sessionOk = false;
        {
            std::lock_guard<std::mutex> lk(g_swapchainMutex);
            auto it = g_swapchains.find(swapchain);
            if (it != g_swapchains.end()) {
                tracked = true;
                const TrackedSwapchain& ts = it->second;
                sessionOk = (ts.session == g_d3d11Mask.session);
                if (sessionOk && ts.lastAcquiredIndex < ts.textures.size()) {
                    tex      = ts.textures[ts.lastAcquiredIndex];
                    if (tex) tex->AddRef();
                    arrSize  = ts.arraySize;
                    scFormat = ts.format;
                    views    = ts.eyeViews; // copy; layout captured in xrEndFrame
                    rtvs.reserve(views.size());
                    for (const EyeView& ev : views) {
                        rtvs.push_back(CachedRtvFor(ts, ts.lastAcquiredIndex, ev.arraySlice));
                    }
                }
            }
        }
        if (!g_diagReleaseSeen.exchange(true))
            Log("d3d11 mask DIAG: xrReleaseSwapchainImage reached (tracked=%d sessionMatch=%d eyeViews=%zu)\n",
                tracked ? 1 : 0, sessionOk ? 1 : 0, views.size());

        if (tracked && sessionOk && tex) {
            if (views.empty()) {
                if (!g_diagReleaseNoLayout.exchange(true))
                    Log("d3d11 mask DIAG: no eye layout yet for this swapchain (captured on first xrEndFrame; mask starts next frame)\n");
            } else {
                for (size_t i = 0; i < views.size(); ++i) {
                    DrawVisorBorderToTexture(tex, arrSize, scFormat, views[i], views,
                        i < rtvs.size() ? rtvs[i] : nullptr);
                }
                for (ID3D11RenderTargetView* rtv : rtvs) {
                    if (rtv) rtv->Release();
                }
                const uint32_t n = g_releaseDrawLogCount.fetch_add(1);
                if (n == 0)
                    Log("visor: draw executed (%zu eye view(s))\n", views.size());
                g_releaseDrewVisorThisFrame.store(true);
            }
        } else {
            for (ID3D11RenderTargetView* rtv : rtvs) {
                if (rtv) rtv->Release();
            }
        }
        if (tex) tex->Release();
    }
    return nextXrReleaseSwapchainImage(swapchain, info);
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrDestroySwapchain(XrSwapchain swapchain) {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        auto it = g_swapchains.find(swapchain);
        if (it != g_swapchains.end()) {
            ReleaseTrackedSwapchainResources(it->second);
            g_swapchains.erase(it);
        }
    }
    return nextXrDestroySwapchain(swapchain);
}

// ---- Head-locked RGBA layer helper (shared by test quad and technique-A visor) ----

// Creates a head-locked VIEW reference space + an RGBA swapchain of the given size.
void LoadConfig() {
    enabled = ReadBoolSetting(L"enabled", true);
    verboseLogging = ReadBoolSetting(L"verbose_logging", false);
    splitMode = ReadBoolSetting(L"split_mode", false);
    // foveated_center_compensation, crop_outer_edges_only, and stencil_outer_edges_only are
    // permanently enabled; config keys are ignored.
    visualMaskOnly = ReadBoolSetting(L"visual_mask_only", false);
    horizontalVisualMaskOnly = ReadBoolSetting(L"horizontal_visual_mask_only", false);
    // There is exactly ONE visor renderer (D3D11 draw into the game's eye textures).
    // The old hidden-mesh reshaper is gone: visibility_mask_visor=1 is retired. Old
    // inis may still carry the key; it is read only to warn, never to reshape.
    bool visibilityMaskVisor = ReadBoolSetting(L"visibility_mask_visor", false);
    if (visibilityMaskVisor) {
        Log("config: visibility_mask_visor=1 is retired and ignored; the visor draws into eye textures only\n");
    }
    visibilityMaskVisor = false;
    // edge_smear_fix, lod_popin_fix, and edge_smear_pixels are removed; edge-smear code is disabled.
    const double legacyTotal = ReadDoubleSetting(L"vertical_tangent", DefaultTotalTangent);
    totalTangent = std::clamp(
        ReadDoubleSetting(L"total_render_height", ReadDoubleSetting(L"total_share", legacyTotal)),
        MinVerticalTangent,
        MaxVerticalTangent);
    horizontalRenderWidth = std::clamp(
        ReadDoubleSetting(L"horizontal_render_width", DefaultHorizontalRenderWidth),
        MinRenderScale,
        1.0);

    maskEnabled = ReadBoolSetting(L"mask_enabled", false);
    maskVertical = std::clamp(ReadDoubleSetting(L"mask_vertical", 1.0), MinVerticalTangent, MaxVerticalTangent);
    maskHorizontal = std::clamp(ReadDoubleSetting(L"mask_horizontal", 1.0), 0.01, 1.0);
    maskRounded = ReadBoolSetting(L"mask_rounded", true);
    maskCorner = std::clamp(ReadDoubleSetting(L"mask_corner", 0.5), 0.0, 1.0);
    const double oldMaskOffsetY = std::clamp(ReadDoubleSetting(L"mask_offset_y", 0.0), -1.0, 1.0);
    maskTopBias = std::clamp(ReadDoubleSetting(L"mask_top_bias", oldMaskOffsetY), -1.0, 1.0);
    maskBottomBias = std::clamp(ReadDoubleSetting(L"mask_bottom_bias", oldMaskOffsetY), -1.0, 1.0);
    maskLeftBias = std::clamp(ReadDoubleSetting(L"mask_left_bias", 0.0), -1.0, 1.0);
    maskRightBias = std::clamp(ReadDoubleSetting(L"mask_right_bias", 0.0), -1.0, 1.0);
    maskTopCurve = std::clamp(ReadDoubleSetting(L"mask_top_curve", 0.0), -1.0, 1.0);
    maskBottomCurve = std::clamp(ReadDoubleSetting(L"mask_bottom_curve", 0.0), -1.0, 1.0);
    renderScale = std::clamp(ReadDoubleSetting(L"render_scale", 1.0), 0.1, 3.0);
    // HD visor and anti-aliasing are permanently disabled.

    // Visor shape controls (ranges identical to the UI sliders).
    visorOuterApexY = std::clamp(ReadDoubleSetting(L"mask_outer_apex_y", 0.0), -0.5, 0.5);
    visorInnerLowerY = std::clamp(ReadDoubleSetting(L"mask_inner_lower_y", 0.0), 0.0, 0.333);
    visorInnerBridgeWidth = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_width", 0.5), 0.0, 1.0);
    visorInnerBridgeRise = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_rise", 0.0), 0.0, 0.5);
    visorInnerBridgePeakX = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_peak_x", 0.5), 0.0, 1.0);
    visorInnerBridgeSteepness = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_steepness", 0.5), 0.0, 1.0);

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
    DWORD profileVisorSize = 0;
    DWORD profileVisorWidth = 0;
    DWORD profileVisorHeight = 0;
    if (ReadProfileDword(L"profile_enabled", profileEnabled) && profileEnabled != 0) {
        DWORD profileSplitMode = splitMode ? 1u : 0u;
        DWORD profileTotal = static_cast<DWORD>(std::lround(totalTangent * 1000.0));
        DWORD profileTop = static_cast<DWORD>(std::lround(topTangent * 1000.0));
        DWORD profileBottom = static_cast<DWORD>(std::lround(bottomTangent * 1000.0));
        DWORD profileHorizontal = static_cast<DWORD>(std::lround(horizontalRenderWidth * 1000.0));
        ReadProfileDword(L"split_mode", profileSplitMode);
        ReadProfileDword(L"total_render_height", profileTotal) || ReadProfileDword(L"total_share", profileTotal) || ReadProfileDword(L"vertical_tangent", profileTotal);
        ReadProfileDword(L"top_tangent", profileTop);
        ReadProfileDword(L"bottom_tangent", profileBottom);
        ReadProfileDword(L"horizontal_render_width", profileHorizontal);

        DWORD profileMaskVertical = static_cast<DWORD>(std::lround(maskVertical * 1000.0));
        DWORD profileMaskHorizontal = static_cast<DWORD>(std::lround(maskHorizontal * 1000.0));
        DWORD profileMaskRounded = maskRounded ? 1u : 0u;
        DWORD profileMaskCorner = static_cast<DWORD>(std::lround(maskCorner * 1000.0));
        DWORD profileMaskTopBias = static_cast<DWORD>(std::lround((std::clamp(maskTopBias, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskBottomBias = static_cast<DWORD>(std::lround((std::clamp(maskBottomBias, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskLeftBias = static_cast<DWORD>(std::lround((std::clamp(maskLeftBias, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskRightBias = static_cast<DWORD>(std::lround((std::clamp(maskRightBias, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskTopCurve = static_cast<DWORD>(std::lround((std::clamp(maskTopCurve, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskBottomCurve = static_cast<DWORD>(std::lround((std::clamp(maskBottomCurve, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskOffsetY = static_cast<DWORD>(std::lround(((maskTopBias + maskBottomBias) * 0.5 + 1.0) * 1000.0));
        // Visor ENABLE is global-only now: per-app profiles no longer carry/override mask_enabled.
        // (Per-app shape values below are still honored.) This stops stale per-app mask_enabled=0
        // from silently overriding the global visor toggle.
        ReadProfileDword(L"mask_vertical", profileMaskVertical);
        ReadProfileDword(L"mask_horizontal", profileMaskHorizontal);
        DWORD profileRenderScale = static_cast<DWORD>(std::lround(renderScale * 1000000.0));
        ReadProfileDword(L"mask_rounded", profileMaskRounded);
        ReadProfileDword(L"mask_corner", profileMaskCorner);
        ReadProfileDword(L"mask_offset_y", profileMaskOffsetY);
        ReadProfileDword(L"mask_top_bias", profileMaskTopBias);
        ReadProfileDword(L"mask_bottom_bias", profileMaskBottomBias);
        ReadProfileDword(L"mask_left_bias", profileMaskLeftBias);
        ReadProfileDword(L"mask_right_bias", profileMaskRightBias);
        ReadProfileDword(L"mask_top_curve", profileMaskTopCurve);
        ReadProfileDword(L"mask_bottom_curve", profileMaskBottomCurve);
        ReadProfileDword(L"visor_size", profileVisorSize);
        ReadProfileDword(L"visor_width", profileVisorWidth);
        ReadProfileDword(L"visor_height", profileVisorHeight);

        // Per-app visor shape overrides (ProfileWindow stores apex-y as signed millis
        // (v+1)*1000, inner-low/bridge-width as plain millis v*1000). Initialized from
        // the global values so a missing registry key keeps the global setting.
        DWORD profileOuterApexY = static_cast<DWORD>(std::lround((std::clamp(visorOuterApexY, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileInnerLowerY = static_cast<DWORD>(std::lround(visorInnerLowerY * 1000.0));
        DWORD profileBridgeWidth = static_cast<DWORD>(std::lround(visorInnerBridgeWidth * 1000.0));
        DWORD profileBridgeRise = static_cast<DWORD>(std::lround(visorInnerBridgeRise * 1000.0));
        DWORD profileBridgePeakX = static_cast<DWORD>(std::lround(visorInnerBridgePeakX * 1000.0));
        DWORD profileBridgeSteepness = static_cast<DWORD>(std::lround(visorInnerBridgeSteepness * 1000.0));
        ReadProfileDword(L"mask_outer_apex_y", profileOuterApexY);
        ReadProfileDword(L"mask_inner_lower_y", profileInnerLowerY);
        ReadProfileDword(L"mask_inner_bridge_width", profileBridgeWidth);
        ReadProfileDword(L"mask_inner_bridge_rise", profileBridgeRise);
        ReadProfileDword(L"mask_inner_bridge_peak_x", profileBridgePeakX);
        ReadProfileDword(L"mask_inner_bridge_steepness", profileBridgeSteepness);
        visorOuterApexY = std::clamp(SignedMillisToUnit(profileOuterApexY, visorOuterApexY), -0.5, 0.5);
        visorInnerLowerY = std::clamp(static_cast<double>(profileInnerLowerY) / 1000.0, 0.0, 0.333);
        visorInnerBridgeWidth = std::clamp(static_cast<double>(profileBridgeWidth) / 1000.0, 0.0, 1.0);
        visorInnerBridgeRise = std::clamp(static_cast<double>(profileBridgeRise) / 1000.0, 0.0, 0.5);
        visorInnerBridgePeakX = std::clamp(static_cast<double>(profileBridgePeakX) / 1000.0, 0.0, 1.0);
        visorInnerBridgeSteepness = std::clamp(static_cast<double>(profileBridgeSteepness) / 1000.0, 0.0, 1.0);
        if (!ReadProfileDouble(L"render_scale", renderScale)) {
            ReadProfileDword(L"render_scale", profileRenderScale);
            renderScale = DwordToRenderScale(profileRenderScale, renderScale);
        }
        renderScale = std::clamp(renderScale, 0.1, 3.0);
        // maskEnabled intentionally NOT overridden per-app — it stays at the global value read above.
        maskVertical = MillisToRenderHeight(profileMaskVertical, maskVertical);
        maskHorizontal = MillisToMaskScale(profileMaskHorizontal, maskHorizontal);
        maskRounded = profileMaskRounded != 0;
        maskCorner = std::clamp(static_cast<double>(profileMaskCorner) / 1000.0, 0.0, 1.0);
        const double oldProfileOffsetY = SignedMillisToUnit(profileMaskOffsetY, (maskTopBias + maskBottomBias) * 0.5);
        maskTopBias = SignedMillisToUnit(profileMaskTopBias, oldProfileOffsetY);
        maskBottomBias = SignedMillisToUnit(profileMaskBottomBias, oldProfileOffsetY);
        maskLeftBias = SignedMillisToUnit(profileMaskLeftBias, maskLeftBias);
        maskRightBias = SignedMillisToUnit(profileMaskRightBias, maskRightBias);
        maskTopCurve = SignedMillisToUnit(profileMaskTopCurve, maskTopCurve);
        maskBottomCurve = SignedMillisToUnit(profileMaskBottomCurve, maskBottomCurve);

        splitMode = profileSplitMode != 0;
        totalTangent = MillisToRenderHeight(profileTotal, totalTangent);
        horizontalRenderWidth = MillisToRenderScale(profileHorizontal, horizontalRenderWidth);
        if (splitMode) {
            topTangent = MillisToRenderHeight(profileTop, topTangent);
            bottomTangent = MillisToRenderHeight(profileBottom, bottomTangent);
            totalTangent = std::clamp(topTangent + bottomTangent, MinVerticalTangent, MaxVerticalTangent);
        } else {
            topTangent = totalTangent * 0.5;
            bottomTangent = totalTangent * 0.5;
        }
    }

    // Size now controls mask thickness (1.0 = full mask, 0.0 = no mask).
    // Default to full mask (1.0) for maximum effect.
    visorSize = std::clamp(ReadDoubleSetting(L"mask_size", 1.0), 0.05, 1.0);
    visorWidth = std::clamp(ReadDoubleSetting(L"mask_width_scale", 1.0), 0.25, 2.0);
    visorHeight = std::clamp(ReadDoubleSetting(L"mask_height_scale", 1.0), 0.25, 2.0);
    if (profileVisorSize > 0)
        visorSize = std::clamp(static_cast<double>(profileVisorSize) / 1000.0, 0.05, 1.0);
    if (profileVisorWidth > 0)
        visorWidth = std::clamp(static_cast<double>(profileVisorWidth) / 1000.0, 0.25, 2.0);
    if (profileVisorHeight > 0)
        visorHeight = std::clamp(static_cast<double>(profileVisorHeight) / 1000.0, 0.25, 2.0);
    visorCurve = std::clamp(1.0 - maskCorner, 0.0, 1.0);
    // X/Y visor positioning was removed from the product UI. Ignore stale global or
    // per-app bias values so old profiles cannot reintroduce binocular mismatch.
    visorOffsetX = 0.0;
    visorOffsetY = 0.0;

    Log("config: enabled=%d app=%ls mode=%s total_render_height=%.3f top_render_height=%.3f bottom_render_height=%.3f horizontal_render_width=%.3f top_scale=%.3f bottom_scale=%.3f foveated_center_compensation=%d visual_mask_only=%d horizontal_visual_mask_only=%d outer_edge_visibility_mask_only=%d crop_outer_edges_only=%d visor_enabled=%d visor_size=%.3f visor_width=%.3f visor_height=%.3f visor_curve=%.3f visor_offset_x=%.3f visor_offset_y=%.3f visor_outer_apex_y=%.3f visor_inner_lower_y=%.3f visor_inner_bridge_w/r/px/s=%.3f/%.3f/%.3f/%.3f render_scale=%.6f uevr_like=%d verbose_logging=%d\n",
        enabled ? 1 : 0,
        currentAppKey.empty() ? L"<global>" : currentAppKey.c_str(),
        splitMode ? "split" : "total",
        totalTangent,
        topTangent,
        bottomTangent,
        horizontalRenderWidth,
        std::clamp(topTangent * 2.0, 0.0, 1.0),
        std::clamp(bottomTangent * 2.0, 0.0, 1.0),
        foveatedCenterCompensation ? 1 : 0,
        visualMaskOnly ? 1 : 0,
        horizontalVisualMaskOnly ? 1 : 0,
        outerEdgeVisibilityMaskOnly ? 1 : 0,
        cropOuterEdgesOnly ? 1 : 0,
        maskEnabled ? 1 : 0,
        visorSize,
        visorWidth,
        visorHeight,
        visorCurve,
        visorOffsetX,
        visorOffsetY,
        visorOuterApexY,
        visorInnerLowerY,
        visorInnerBridgeWidth,
        visorInnerBridgeRise,
        visorInnerBridgePeakX,
        visorInnerBridgeSteepness,
        renderScale,
        uevrLikeProcess ? 1 : 0,
        verboseLogging ? 1 : 0);
}

void EnsureInitialized() {
    if (layerDirectory.empty()) {
        layerDirectory = GetLayerDirectory();
    }

    if (!sessionLogged) {
        sessionLogged = true;
        Log("------------------------------------------------------------\n");
        Log("XR ViewLab layer starting\n");
        Log("layer_dir=%ls\n", layerDirectory.empty() ? L"<unknown>" : layerDirectory.c_str());
        Log("config_path=%ls\n", ConfigPath().c_str());
        Log("log_path=%ls\n", LogPath().c_str());
        Log("process=%ls\n", CurrentProcessPath().c_str());
    }

    LoadConfig();
}

double EffectiveOuterEdgeHorizontalScale() {
    const double horizontalScale = std::clamp(horizontalRenderWidth, MinRenderScale, 1.0);
    if (!cropOuterEdgesOnly) {
        return horizontalScale;
    }
    // Horizontal crop preserves the inner eye edges and scales only the outer edges.
    // For the normal near-symmetric stereo frustum, the total per-eye FOV width is
    // therefore half unchanged inner FOV plus half scaled outer FOV.
    return std::clamp((1.0 + horizontalScale) * 0.5, MinRenderScale, 1.0);
}

double ConservativeRecommendedScale(double scale) {
    if (!uevrLikeProcess) {
        return scale;
    }

    return std::clamp(scale, MinUevrRecommendedScale, 1.0);
}

double EffectiveHorizontalScaleForView(uint32_t viewIndex, double requestedWidthScale) {
    if (viewIndex >= g_d3d11Mask.latestViewCount) {
        return ConservativeRecommendedScale(requestedWidthScale);
    }

    const XrFovf& original = g_d3d11Mask.latestOriginalViews[viewIndex].fov;
    const XrFovf& cropped = g_d3d11Mask.latestViews[viewIndex].fov;
    const double originalWidth =
        std::tan(static_cast<double>(original.angleRight)) -
        std::tan(static_cast<double>(original.angleLeft));
    const double croppedWidth =
        std::tan(static_cast<double>(cropped.angleRight)) -
        std::tan(static_cast<double>(cropped.angleLeft));
    if (!std::isfinite(originalWidth) || std::abs(originalWidth) < 0.00001 ||
        !std::isfinite(croppedWidth)) {
        return ConservativeRecommendedScale(requestedWidthScale);
    }
    return ConservativeRecommendedScale(std::clamp(croppedWidth / originalWidth, MinRenderScale, 1.0));
}

double EffectiveVerticalScaleForView(uint32_t viewIndex, double requestedHeightScale) {
    if (viewIndex >= g_d3d11Mask.latestViewCount) {
        return ConservativeRecommendedScale(requestedHeightScale);
    }

    const XrFovf& original = g_d3d11Mask.latestOriginalViews[viewIndex].fov;
    const XrFovf& cropped = g_d3d11Mask.latestViews[viewIndex].fov;
    const double originalHeight =
        std::tan(static_cast<double>(original.angleUp)) -
        std::tan(static_cast<double>(original.angleDown));
    const double croppedHeight =
        std::tan(static_cast<double>(cropped.angleUp)) -
        std::tan(static_cast<double>(cropped.angleDown));
    if (!std::isfinite(originalHeight) || std::abs(originalHeight) < 0.00001 ||
        !std::isfinite(croppedHeight)) {
        return ConservativeRecommendedScale(requestedHeightScale);
    }
    return ConservativeRecommendedScale(std::clamp(croppedHeight / originalHeight, MinRenderScale, 1.0));
}

void ApplyXRViewLabFov(uint32_t viewIndex, XrView& view, bool& compensated, float& pitchOffset) {
    const double topScale = std::clamp(topTangent * 2.0, 0.0, 1.0);
    const double bottomScale = std::clamp(bottomTangent * 2.0, 0.0, 1.0);
    const double horizontalScale = std::clamp(horizontalRenderWidth, MinRenderScale, 1.0);
    const double originalLeftTan = (std::max)(0.0, -std::tan(static_cast<double>(view.fov.angleLeft)));
    const double originalRightTan = (std::max)(0.0, std::tan(static_cast<double>(view.fov.angleRight)));
    double desiredLeftTan = originalLeftTan;
    double desiredRightTan = originalRightTan;
    if (cropOuterEdgesOnly && viewIndex == 0) {
        // Left eye: keep the inner/right edge stable and scale only the outer/left edge.
        desiredLeftTan = originalLeftTan * horizontalScale;
    } else if (cropOuterEdgesOnly && viewIndex == 1) {
        // Right eye: keep the inner/left edge stable and scale only the outer/right edge.
        desiredRightTan = originalRightTan * horizontalScale;
    } else {
        // Full crop scales both sides of each eye around its centre.
        desiredLeftTan = originalLeftTan * horizontalScale;
        desiredRightTan = originalRightTan * horizontalScale;
    }
    const double originalTopTan = (std::max)(0.0, std::tan(static_cast<double>(view.fov.angleUp)));
    const double originalBottomTan = (std::max)(0.0, -std::tan(static_cast<double>(view.fov.angleDown)));
    const double desiredTopTan = originalTopTan * topScale;
    const double desiredBottomTan = originalBottomTan * bottomScale;

    view.fov.angleLeft = -static_cast<float>(std::atan(desiredLeftTan));
    view.fov.angleRight = static_cast<float>(std::atan(desiredRightTan));

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

    if (viewLocateInfo != nullptr &&
        *viewCountOutput >= 2 &&
        viewCapacityInput >= 2) {
        g_d3d11Mask.locateSpace = viewLocateInfo->space;
        g_d3d11Mask.locateTime = viewLocateInfo->displayTime;
        g_d3d11Mask.latestViewCount = (std::min)(*viewCountOutput, 2u);
    }

    const uint32_t logCount = locateViewsLogCount.fetch_add(1);
    if (maskEnabled && visibilityMaskLogCount.load() == 0 && logCount == 300) {
        const uint32_t warnCount = visorMaskNoHookLogCount.fetch_add(1);
        if (warnCount == 0) {
            Log("visibility mask: rounded visor enabled, but xrGetVisibilityMaskKHR has not been called after 300 locate frames; this runtime path may not consume OpenXR visibility masks, so no in-HMD rounded mask will be visible\n");
        }
    }
    for (uint32_t i = 0; i < *viewCountOutput; ++i) {
        const XrFovf before = views[i].fov;
        const XrView originalView = views[i];
        bool compensated = false;
        float pitchOffset = 0.0f;
        ApplyXRViewLabFov(i, views[i], compensated, pitchOffset);
        if (i < g_d3d11Mask.latestViewCount) {
            g_d3d11Mask.latestOriginalViews[i] = originalView;
            g_d3d11Mask.latestViews[i] = views[i];
        }
        if (verboseLogging && (logCount < 20 || logCount % 300 == 0)) {
            LogVerbose("xrLocateViews[%u]: up %.5f -> %.5f down %.5f -> %.5f left %.5f -> %.5f right %.5f -> %.5f original_fov=(L %.5f R %.5f U %.5f D %.5f valid=%d) horizontal_render_width=%.3f crop_outer_edges_only=%d foveated_center=%d pitch_offset=%.5f\n",
                i,
                before.angleUp,
                views[i].fov.angleUp,
                before.angleDown,
                views[i].fov.angleDown,
                before.angleLeft,
                views[i].fov.angleLeft,
                before.angleRight,
                views[i].fov.angleRight,
                before.angleLeft,
                before.angleRight,
                before.angleUp,
                before.angleDown,
                i < g_d3d11Mask.latestViewCount ? 1 : 0,
                horizontalRenderWidth,
                cropOuterEdgesOnly ? 1 : 0,
                compensated ? 1 : 0,
                pitchOffset);
        }
    }

    if (logCount == 0) {
        Log("render: FOV adjustment active (per-frame detail in verbose log only)\n");
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrCreateSession(
    XrInstance instance,
    const XrSessionCreateInfo* createInfo,
    XrSession* session) {
    const XrResult result = nextXrCreateSession(instance, createInfo, session);
    if (result != XR_SUCCESS || createInfo == nullptr || session == nullptr || *session == XR_NULL_HANDLE) {
        return result;
    }

    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);

    ReleaseD3D11MaskRenderer();
    g_d3d11Mask.session = *session;
    const auto* d3d11Binding = reinterpret_cast<const XrGraphicsBindingD3D11KHR*>(
        FindStructInChain(createInfo->next, XR_TYPE_GRAPHICS_BINDING_D3D11_KHR));
    if (d3d11Binding != nullptr && d3d11Binding->device != nullptr) {
        g_d3d11Mask.device = d3d11Binding->device;
        g_d3d11Mask.device->AddRef();
        g_d3d11Mask.device->GetImmediateContext(&g_d3d11Mask.context);
        Log("visor: D3D11 session detected; initializing renderer\n");
        InitD3D11MaskRenderer();
    } else {
        Log("visor: this game does not use D3D11; ViewLab's visor is unavailable\n");
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrDestroySession(XrSession session) {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    if (session == g_d3d11Mask.session) {
        ReleaseD3D11MaskRenderer();
    }
    return nextXrDestroySession(session);
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrEndFrame(
    XrSession session,
    const XrFrameEndInfo* frameEndInfo) {
    if (nextXrEndFrame == nullptr || frameEndInfo == nullptr) {
        return nextXrEndFrame ? nextXrEndFrame(session, frameEndInfo) : XR_ERROR_RUNTIME_FAILURE;
    }

    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);

    if (!g_diagEndFrameCalled.exchange(true)) {
        Log("d3d11 mask DIAG: xrEndFrame hook called (enabled=%d maskEnabled=%d visMaskCalled=%d "
            "sessionMatch=%d d3d11Init=%d layerCount=%u)\n",
            enabled ? 1 : 0, maskEnabled ? 1 : 0, g_visibilityMaskEverCalled.load() ? 1 : 0,
            (session == g_d3d11Mask.session) ? 1 : 0, g_d3d11Mask.initialized ? 1 : 0,
            frameEndInfo->layerCount);
    }

    const XrFrameEndInfo* submitFrame = frameEndInfo;

    // Capture the per-eye layout (swapchain -> imageRect + array
    // slice) from the projection layer. The actual draw happens in
    // xrReleaseSwapchainImage, which runs earlier in the frame loop, so the layout
    // captured here is consumed by the NEXT frame's release. Layout is stable frame to
    // frame. Not gated on the visibility-mask path — the D3D11 visor runs regardless.
    if (enabled && maskEnabled &&
        g_d3d11Mask.initialized && session == g_d3d11Mask.session) {
        bool foundProj = false;
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        // Reset captured layouts so stale eyes don't linger if the app changes layers.
        for (auto& kv : g_swapchains) kv.second.eyeViews.clear();
        // Capture the app's original projection rect. Edge smear is handled by
        // writing guard pixels into that texture, not by changing submitted rects.
        for (uint32_t l = 0; l < frameEndInfo->layerCount; ++l) {
            const XrCompositionLayerBaseHeader* hdr = frameEndInfo->layers[l];
            if (hdr == nullptr || hdr->type != XR_TYPE_COMPOSITION_LAYER_PROJECTION) continue;
            foundProj = true;
            const auto* proj = reinterpret_cast<const XrCompositionLayerProjection*>(hdr);
            if (!g_diagProjFound.exchange(true)) {
                Log("d3d11 mask DIAG: projection layer found (layer=%u viewCount=%u)\n",
                    l, proj->viewCount);
            }
            for (uint32_t v = 0; v < proj->viewCount; ++v) {
                const XrCompositionLayerProjectionView& pv = proj->views[v];
                auto it = g_swapchains.find(pv.subImage.swapchain);
                if (it != g_swapchains.end()) {
                    it->second.eyeViews.push_back(
                        EyeView{pv.subImage.imageRect,
                                static_cast<uint32_t>(pv.subImage.imageArrayIndex),
                                v,
                                pv.fov});
                }
            }
        }
        if (!foundProj && !g_diagNoProjLayer.exchange(true)) {
            Log("d3d11 mask DIAG: FAIL no projection layer in submitted frame (layerCount=%u)\n",
                frameEndInfo->layerCount);
        }
    }

    if (enabled && g_d3d11Mask.initialized && session == g_d3d11Mask.session) {
        const bool drawLateEdgeGuard = edgeSmearFix;
        const bool drawLateVisor = maskEnabled;
        const bool releaseDrewEdgeGuard = g_releaseDrewEdgeGuardThisFrame.exchange(false);
        const bool releaseDrewVisor = g_releaseDrewVisorThisFrame.exchange(false);
        const bool needsLateEdgeGuard = drawLateEdgeGuard && !releaseDrewEdgeGuard;
        const bool needsLateVisor = drawLateVisor && !releaseDrewVisor;
        if (needsLateEdgeGuard || needsLateVisor) {
            DrawCapturedProjectionTextures(needsLateEdgeGuard, needsLateVisor,
                needsLateVisor ? "visor" : "edge-smear fix");
        }
    }

    return nextXrEndFrame(session, submitFrame);
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
            reinterpret_cast<PFN_xrVoidFunction*>(&getProperties)) == XR_SUCCESS &&
        getProperties != nullptr) {
        XrViewConfigurationProperties properties{XR_TYPE_VIEW_CONFIGURATION_PROPERTIES};
        const XrResult propertiesResult = getProperties(instance, systemId, viewConfigurationType, &properties);
        if ((propertiesResult != XR_SUCCESS || properties.fovMutable != XR_TRUE) && verboseLogging) {
            const uint32_t skipCount = enumerateSkipLogCount.fetch_add(1);
            if (skipCount < 10 || skipCount % 100 == 0) {
                LogVerbose("xrEnumerateViewConfigurationViews: continuing recommended-size scale, properties_result=%d fovMutable=%d\n",
                    propertiesResult,
                    properties.fovMutable == XR_TRUE ? 1 : 0);
            }
        }
    }

    const uint32_t logCount = enumerateViewsLogCount.fetch_add(1);
    const double requestedHeightScale = std::clamp(topTangent + bottomTangent, 0.01, 1.0);
    const double requestedWidthScale = EffectiveOuterEdgeHorizontalScale();
    for (uint32_t i = 0; i < *viewCountOutput; ++i) {
        const double perViewHeightScale = visualMaskOnly ? 1.0 : EffectiveVerticalScaleForView(i, requestedHeightScale);
        const double perViewWidthScale = horizontalVisualMaskOnly ? 1.0 : EffectiveHorizontalScaleForView(i, requestedWidthScale);
        const double renderHeightScale = perViewHeightScale * renderScale;
        const double renderWidthScale = perViewWidthScale * renderScale;
        const uint32_t beforeWidth = views[i].recommendedImageRectWidth;
        const uint32_t beforeHeight = views[i].recommendedImageRectHeight;
        views[i].recommendedImageRectWidth = std::max<uint32_t>(
            1,
            static_cast<uint32_t>(std::lround(static_cast<double>(views[i].recommendedImageRectWidth) * renderWidthScale)));
        views[i].recommendedImageRectHeight = std::max<uint32_t>(
            1,
            static_cast<uint32_t>(std::lround(static_cast<double>(views[i].recommendedImageRectHeight) * renderHeightScale)));
        if (i == 0 && logCount == 0) {
            Log("render: resolution scaled width=%.3f height=%.3f (per-view detail in verbose log only)\n",
                renderWidthScale,
                renderHeightScale);
        }
        if (verboseLogging && (logCount < 10 || logCount % 100 == 0)) {
            LogVerbose("xrEnumerateViewConfigurationViews[%u]: recommended width %u -> %u horizontal_render_width=%.3f effective_horizontal_width=%.3f horizontal_visual_mask_only=%d height %u -> %u total_render_height=%.3f effective_vertical_height=%.3f visual_mask_only=%d uevr_like=%d\n",
                i,
                beforeWidth,
                views[i].recommendedImageRectWidth,
                horizontalRenderWidth,
                perViewWidthScale,
                horizontalVisualMaskOnly ? 1 : 0,
                beforeHeight,
                views[i].recommendedImageRectHeight,
                requestedHeightScale,
                perViewHeightScale,
                visualMaskOnly ? 1 : 0,
                uevrLikeProcess ? 1 : 0);
        }
    }

    return result;
}

// Replace the runtime's hidden mesh with a rectangular black "visor" border that
// bounds the visible area. mask_vertical/mask_horizontal are absolute (CROP units);
// here they become a fraction of the cropped view (1.0 = no mask on that axis).
// The frame is 8 vertices / 24 indices, well within the runtime's lens-mesh capacity,
// so no buffer growth is needed. Centered rect for now; per-eye/feathered "bean"
// shaping follows. Returns false if there is nothing to mask or no room.
// Replaces the runtime hidden-area mesh with a visor-shaped border.
// Shape math identical to BeanMaskEditor.BuildGeometry and BuildVisorBorderVerts.
XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrGetVisibilityMaskKHR(
    XrSession session,
    XrViewConfigurationType viewConfigurationType,
    uint32_t viewIndex,
    XrVisibilityMaskTypeKHR visibilityMaskType,
    XrVisibilityMaskKHR* visibilityMask) {
    // Informational only. The game calling xrGetVisibilityMaskKHR does NOT disable the
    // native D3D11 visor — that path runs independently at xrReleaseSwapchainImage.
    if (!g_visibilityMaskEverCalled.exchange(true)) {
        Log("visibility mask: game calls xrGetVisibilityMaskKHR (informational; native D3D11 visor unaffected, runs independently)\n");
    }

    const XrResult result = nextXrGetVisibilityMaskKHR(session, viewConfigurationType, viewIndex, visibilityMaskType, visibilityMask);

    if (!enabled ||
        result != XR_SUCCESS ||
        viewConfigurationType != XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO ||
        viewIndex > 1 ||
        visibilityMaskType != XR_VISIBILITY_MASK_TYPE_HIDDEN_TRIANGLE_MESH_KHR ||
        visibilityMask == nullptr ||
        visibilityMask->vertices == nullptr ||
        visibilityMask->indices == nullptr ||
        visibilityMask->vertexCountOutput == 0 ||
        visibilityMask->indexCountOutput < 3 ||
        visibilityMask->vertexCapacityInput < visibilityMask->vertexCountOutput ||
        visibilityMask->indexCapacityInput < visibilityMask->indexCountOutput) {
        return result;
    }

    // Outer-edge stencil filter (UI checkbox "Stencil outer edges only"): keep only the
    // outer-half hidden triangles so the runtime's FOV stencil (e.g. Virtual Desktop's)
    // never stencils the inner-eye/top/bottom regions. This MUST also run when the native
    // D3D11 visor is active: the visor only draws pixels, but the game stencils whatever
    // this mask returns — an unfiltered mask blacks out the inner edges regardless of the
    // visor shape, with the runtime's curve instead of the user's apex-y curve.
    if (uevrLikeProcess) {
        if (!g_diagVisorSkipsVisibilityEdgeFilter.exchange(true)) {
            Log("visibility mask: outer-edge filtering OFF for UEVR-like process; runtime mask passed through unmodified\n");
        }
        return result;
    }

    const uint32_t beforeIndexCount = visibilityMask->indexCountOutput;
    uint32_t writeIndex = 0;
    for (uint32_t readIndex = 0; readIndex + 2 < beforeIndexCount; readIndex += 3) {
        const uint32_t i0 = visibilityMask->indices[readIndex];
        const uint32_t i1 = visibilityMask->indices[readIndex + 1];
        const uint32_t i2 = visibilityMask->indices[readIndex + 2];
        if (i0 >= visibilityMask->vertexCountOutput ||
            i1 >= visibilityMask->vertexCountOutput ||
            i2 >= visibilityMask->vertexCountOutput) {
            continue;
        }

        const float centerX =
            (visibilityMask->vertices[i0].x +
             visibilityMask->vertices[i1].x +
             visibilityMask->vertices[i2].x) / 3.0f;

        // Outer half only: left eye (view 0) keeps its left side, right eye (view 1)
        // keeps its right side. Inner-eye (nose-side) and centre triangles are dropped.
        const bool keepOuterEdge =
            (viewIndex == 0 && centerX <= 0.0f) ||
            (viewIndex == 1 && centerX >= 0.0f);
        if (!keepOuterEdge) {
            continue;
        }

        visibilityMask->indices[writeIndex++] = i0;
        visibilityMask->indices[writeIndex++] = i1;
        visibilityMask->indices[writeIndex++] = i2;
    }

    visibilityMask->indexCountOutput = writeIndex;

    const uint32_t logCount = visibilityMaskLogCount.fetch_add(1);
    if (logCount == 0) {
        Log("visibility mask: outer-edge filtering active (per-call detail in verbose log only)\n");
    }
    if (verboseLogging && (logCount < 20 || logCount % 100 == 0)) {
        LogVerbose("xrGetVisibilityMaskKHR: view=%u hidden indices %u -> %u outer_edge_only=1\n",
            viewIndex,
            beforeIndexCount,
            visibilityMask->indexCountOutput);
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
        Log("hook installed: xrLocateViews\n");
    } else if (std::strcmp(name, "xrCreateSession") == 0) {
        nextXrCreateSession = reinterpret_cast<PFN_xrCreateSession>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrCreateSession);
        Log("hook installed: xrCreateSession\n");
    } else if (std::strcmp(name, "xrDestroySession") == 0) {
        nextXrDestroySession = reinterpret_cast<PFN_xrDestroySession>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrDestroySession);
        Log("hook installed: xrDestroySession\n");
    } else if (std::strcmp(name, "xrEndFrame") == 0) {
        nextXrEndFrame = reinterpret_cast<PFN_xrEndFrame>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrEndFrame);
        Log("hook installed: xrEndFrame\n");
    } else if (std::strcmp(name, "xrEnumerateSwapchainFormats") == 0) {
        nextXrEnumerateSwapchainFormats = reinterpret_cast<PFN_xrEnumerateSwapchainFormats>(*function);
        Log("hook captured: xrEnumerateSwapchainFormats\n");
    } else if (std::strcmp(name, "xrCreateSwapchain") == 0) {
        nextXrCreateSwapchain = reinterpret_cast<PFN_xrCreateSwapchain>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrCreateSwapchain);
        Log("hook installed: xrCreateSwapchain\n");
    } else if (std::strcmp(name, "xrDestroySwapchain") == 0) {
        nextXrDestroySwapchain = reinterpret_cast<PFN_xrDestroySwapchain>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrDestroySwapchain);
        Log("hook installed: xrDestroySwapchain\n");
    } else if (std::strcmp(name, "xrEnumerateSwapchainImages") == 0) {
        nextXrEnumerateSwapchainImages = reinterpret_cast<PFN_xrEnumerateSwapchainImages>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrEnumerateSwapchainImages);
        Log("hook installed: xrEnumerateSwapchainImages\n");
    } else if (std::strcmp(name, "xrAcquireSwapchainImage") == 0) {
        nextXrAcquireSwapchainImage = reinterpret_cast<PFN_xrAcquireSwapchainImage>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrAcquireSwapchainImage);
        Log("hook installed: xrAcquireSwapchainImage\n");
    } else if (std::strcmp(name, "xrWaitSwapchainImage") == 0) {
        nextXrWaitSwapchainImage = reinterpret_cast<PFN_xrWaitSwapchainImage>(*function);
        Log("hook captured: xrWaitSwapchainImage\n");
    } else if (std::strcmp(name, "xrReleaseSwapchainImage") == 0) {
        nextXrReleaseSwapchainImage = reinterpret_cast<PFN_xrReleaseSwapchainImage>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrReleaseSwapchainImage);
        Log("hook installed: xrReleaseSwapchainImage\n");
    } else if (std::strcmp(name, "xrEnumerateViewConfigurationViews") == 0) {
        nextXrEnumerateViewConfigurationViews = reinterpret_cast<PFN_xrEnumerateViewConfigurationViews>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrEnumerateViewConfigurationViews);
        Log("hook installed: xrEnumerateViewConfigurationViews\n");
    } else if (std::strcmp(name, "xrGetVisibilityMaskKHR") == 0) {
        nextXrGetVisibilityMaskKHR = reinterpret_cast<PFN_xrGetVisibilityMaskKHR>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrGetVisibilityMaskKHR);
        Log("hook installed: xrGetVisibilityMaskKHR\n");
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
    const char* engineName = instanceCreateInfo ? instanceCreateInfo->applicationInfo.engineName : "<unknown>";
    std::string runtimeName;
    if (result == XR_SUCCESS && instance != nullptr && *instance != XR_NULL_HANDLE) {
        PFN_xrGetInstanceProperties getInstanceProperties = nullptr;
        if (nextXrGetInstanceProcAddr(
                *instance,
                "xrGetInstanceProperties",
                reinterpret_cast<PFN_xrVoidFunction*>(&getInstanceProperties)) == XR_SUCCESS &&
            getInstanceProperties != nullptr) {
            XrInstanceProperties properties{XR_TYPE_INSTANCE_PROPERTIES};
            if (getInstanceProperties(*instance, &properties) == XR_SUCCESS) {
                runtimeName = properties.runtimeName;
                Log("runtime: name=%s version=%u.%u.%u\n",
                    properties.runtimeName,
                    XR_VERSION_MAJOR(properties.runtimeVersion),
                    XR_VERSION_MINOR(properties.runtimeVersion),
                    XR_VERSION_PATCH(properties.runtimeVersion));
            }
        }
    }
    RememberApplication(appName, engineName, result, runtimeName.c_str());
    LoadConfig();
    if (instanceCreateInfo != nullptr) {
        Log("application: name=%s engine=%s api=%u.%u.%u extensions=%u\n",
            instanceCreateInfo->applicationInfo.applicationName,
            instanceCreateInfo->applicationInfo.engineName,
            XR_VERSION_MAJOR(instanceCreateInfo->applicationInfo.apiVersion),
            XR_VERSION_MINOR(instanceCreateInfo->applicationInfo.apiVersion),
            XR_VERSION_PATCH(instanceCreateInfo->applicationInfo.apiVersion),
            instanceCreateInfo->enabledExtensionCount);
        for (uint32_t i = 0; i < instanceCreateInfo->enabledExtensionCount; ++i) {
            Log("application extension[%u]=%s\n", i, instanceCreateInfo->enabledExtensionNames[i]);
        }
    }
    Log("xrCreateApiLayerInstance result=%d state=%s app=%s\n", result, enabled ? "enabled" : "bypassed", appName);
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
