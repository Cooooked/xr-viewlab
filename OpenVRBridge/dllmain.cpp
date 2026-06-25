#include "pch.h"
#include "openvr.h"

#include <cstdarg>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <map>
#include <mutex>
#include <string>

namespace
{
constexpr const wchar_t *kMapName = L"Local\\CooookedReShadeOpenVROverlayBridge";
constexpr uint32_t kBridgeVersion = 1;

struct BridgeState
{
    uint32_t version;
    uint32_t processId;
    uint64_t serial;
    uint64_t overlayHandle;
    uint64_t textureHandle;
    int32_t textureType;
    int32_t colorSpace;
    float widthMeters;
    float alpha;
    uint32_t visible;
    uint32_t isDashboard;
    char key[256];
    char name[256];
};

struct OverlayState
{
    std::string key;
    std::string name;
    float widthMeters = 1.25f;
    float alpha = 1.0f;
    bool visible = false;
    bool dashboard = false;
    vr::Texture_t texture {};
};

HMODULE g_module = nullptr;
HMODULE g_realOpenVR = nullptr;
std::mutex g_mutex;
std::map<vr::VROverlayHandle_t, OverlayState> g_overlays;
vr::VROverlayHandle_t g_nextHandle = 100;
HANDLE g_mapping = nullptr;
BridgeState *g_bridge = nullptr;
bool g_realRuntimeActive = false;

using VRGetGenericInterfaceFn = void *(VR_CALLTYPE *)(const char *, vr::EVRInitError *);
using VRInitInternal2Fn = uint32_t(VR_CALLTYPE *)(vr::EVRInitError *, vr::EVRApplicationType, const char *);
using VRInitInternalFn = uint32_t(VR_CALLTYPE *)(vr::EVRInitError *, vr::EVRApplicationType);
using VRShutdownInternalFn = void(VR_CALLTYPE *)();
using VRBoolFn = bool(VR_CALLTYPE *)();
using VRErrorSymbolFn = const char *(VR_CALLTYPE *)(vr::EVRInitError);
using VRGetInitTokenFn = uint32_t(VR_CALLTYPE *)();

VRGetGenericInterfaceFn realVRGetGenericInterface = nullptr;
VRInitInternal2Fn realVRInitInternal2 = nullptr;
VRInitInternalFn realVRInitInternal = nullptr;
VRShutdownInternalFn realVRShutdownInternal = nullptr;
VRBoolFn realVRIsHmdPresent = nullptr;
VRBoolFn realVRIsRuntimeInstalled = nullptr;
VRErrorSymbolFn realVRGetVRInitErrorAsSymbol = nullptr;
VRErrorSymbolFn realVRGetVRInitErrorAsEnglishDescription = nullptr;
VRGetInitTokenFn realVRGetInitToken = nullptr;

std::wstring moduleDir()
{
    wchar_t path[MAX_PATH] {};
    GetModuleFileNameW(g_module, path, MAX_PATH);
    std::wstring result(path);
    const size_t slash = result.find_last_of(L"\\/");
    if (slash != std::wstring::npos)
        result.resize(slash);
    return result;
}

void logLine(const char *fmt, ...)
{
    char message[2048] {};
    va_list args;
    va_start(args, fmt);
    vsnprintf_s(message, sizeof(message), _TRUNCATE, fmt, args);
    va_end(args);

    SYSTEMTIME st {};
    GetLocalTime(&st);

    const std::wstring path = moduleDir() + L"\\openvr-reshade-overlay-bridge.log";
    std::ofstream out(path, std::ios::app);
    out << st.wHour << ":" << st.wMinute << ":" << st.wSecond << "." << st.wMilliseconds
        << " [" << GetCurrentProcessId() << "] " << message << "\n";
}

template <typename T>
T proc(const char *name)
{
    return g_realOpenVR ? reinterpret_cast<T>(GetProcAddress(g_realOpenVR, name)) : nullptr;
}

void loadRealOpenVR()
{
    if (g_realOpenVR)
        return;

    const std::wstring path = moduleDir() + L"\\openvr_api.real.dll";
    g_realOpenVR = LoadLibraryW(path.c_str());
    if (!g_realOpenVR)
    {
        logLine("No real OpenVR proxy DLL at openvr_api.real.dll err=%lu", GetLastError());
        return;
    }

    realVRGetGenericInterface = proc<VRGetGenericInterfaceFn>("VR_GetGenericInterface");
    realVRInitInternal2 = proc<VRInitInternal2Fn>("VR_InitInternal2");
    realVRInitInternal = proc<VRInitInternalFn>("VR_InitInternal");
    realVRShutdownInternal = proc<VRShutdownInternalFn>("VR_ShutdownInternal");
    realVRIsHmdPresent = proc<VRBoolFn>("VR_IsHmdPresent");
    realVRIsRuntimeInstalled = proc<VRBoolFn>("VR_IsRuntimeInstalled");
    realVRGetVRInitErrorAsSymbol = proc<VRErrorSymbolFn>("VR_GetVRInitErrorAsSymbol");
    realVRGetVRInitErrorAsEnglishDescription = proc<VRErrorSymbolFn>("VR_GetVRInitErrorAsEnglishDescription");
    realVRGetInitToken = proc<VRGetInitTokenFn>("VR_GetInitToken");
    logLine("Loaded real OpenVR proxy DLL");
}

void initBridge()
{
    if (g_bridge)
        return;

    g_mapping = CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0, sizeof(BridgeState), kMapName);
    if (!g_mapping)
    {
        logLine("CreateFileMapping failed err=%lu", GetLastError());
        return;
    }

    g_bridge = static_cast<BridgeState *>(MapViewOfFile(g_mapping, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(BridgeState)));
    if (!g_bridge)
    {
        logLine("MapViewOfFile failed err=%lu", GetLastError());
        CloseHandle(g_mapping);
        g_mapping = nullptr;
        return;
    }

    ZeroMemory(g_bridge, sizeof(BridgeState));
    g_bridge->version = kBridgeVersion;
    g_bridge->processId = GetCurrentProcessId();
    logLine("Bridge shared memory ready");
}

void publishOverlay(vr::VROverlayHandle_t handle)
{
    if (!g_bridge)
        return;

    const auto it = g_overlays.find(handle);
    if (it == g_overlays.end())
        return;

    const OverlayState &overlay = it->second;
    g_bridge->version = kBridgeVersion;
    g_bridge->processId = GetCurrentProcessId();
    g_bridge->serial++;
    g_bridge->overlayHandle = handle;
    g_bridge->textureHandle = reinterpret_cast<uint64_t>(overlay.texture.handle);
    g_bridge->textureType = static_cast<int32_t>(overlay.texture.eType);
    g_bridge->colorSpace = static_cast<int32_t>(overlay.texture.eColorSpace);
    g_bridge->widthMeters = overlay.widthMeters;
    g_bridge->alpha = overlay.alpha;
    g_bridge->visible = overlay.visible ? 1u : 0u;
    g_bridge->isDashboard = overlay.dashboard ? 1u : 0u;
    strncpy_s(g_bridge->key, overlay.key.c_str(), _TRUNCATE);
    strncpy_s(g_bridge->name, overlay.name.c_str(), _TRUNCATE);
}

vr::EVROverlayError ok(vr::VROverlayHandle_t handle = vr::k_ulOverlayHandleInvalid)
{
    if (handle != vr::k_ulOverlayHandleInvalid)
        publishOverlay(handle);
    return vr::VROverlayError_None;
}

vr::EVROverlayError unknown()
{
    return vr::VROverlayError_UnknownOverlay;
}

vr::VROverlayHandle_t createOverlay(const char *key, const char *name, bool dashboard)
{
    std::lock_guard<std::mutex> lock(g_mutex);
    const vr::VROverlayHandle_t handle = g_nextHandle++;
    OverlayState state;
    state.key = key ? key : "";
    state.name = name ? name : "";
    state.dashboard = dashboard;
    g_overlays[handle] = state;
    publishOverlay(handle);
    return handle;
}

class ReShadeOverlay final : public vr::IVROverlay
{
public:
    vr::EVROverlayError FindOverlay(const char *key, vr::VROverlayHandle_t *handle) override
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        for (const auto &entry : g_overlays)
        {
            if (entry.second.key == (key ? key : ""))
            {
                if (handle) *handle = entry.first;
                logLine("FindOverlay key='%s' -> %llu", key ? key : "", static_cast<unsigned long long>(entry.first));
                return vr::VROverlayError_None;
            }
        }
        logLine("FindOverlay key='%s' -> missing", key ? key : "");
        return vr::VROverlayError_UnknownOverlay;
    }

    vr::EVROverlayError CreateOverlay(const char *key, const char *name, vr::VROverlayHandle_t *handle) override
    {
        const auto h = createOverlay(key, name, false);
        if (handle) *handle = h;
        logLine("CreateOverlay key='%s' name='%s' -> %llu", key ? key : "", name ? name : "", static_cast<unsigned long long>(h));
        return ok(h);
    }

    vr::EVROverlayError CreateSubviewOverlay(vr::VROverlayHandle_t, const char *key, const char *name, vr::VROverlayHandle_t *handle) override
    {
        const auto h = createOverlay(key, name, false);
        if (handle) *handle = h;
        logLine("CreateSubviewOverlay key='%s' name='%s' -> %llu", key ? key : "", name ? name : "", static_cast<unsigned long long>(h));
        return ok(h);
    }

    vr::EVROverlayError DestroyOverlay(vr::VROverlayHandle_t handle) override { std::lock_guard<std::mutex> lock(g_mutex); g_overlays.erase(handle); logLine("DestroyOverlay %llu", static_cast<unsigned long long>(handle)); return vr::VROverlayError_None; }
    uint32_t GetOverlayKey(vr::VROverlayHandle_t handle, char *value, uint32_t size, vr::EVROverlayError *error = 0L) override { return copyString(handle, value, size, error, true); }
    uint32_t GetOverlayName(vr::VROverlayHandle_t handle, char *value, uint32_t size, vr::EVROverlayError *error = 0L) override { return copyString(handle, value, size, error, false); }
    vr::EVROverlayError SetOverlayName(vr::VROverlayHandle_t handle, const char *name) override { auto *o = find(handle); if (!o) return unknown(); o->name = name ? name : ""; logLine("SetOverlayName %llu '%s'", static_cast<unsigned long long>(handle), o->name.c_str()); return ok(handle); }
    vr::EVROverlayError GetOverlayImageData(vr::VROverlayHandle_t, void *, uint32_t, uint32_t *, uint32_t *) override { return vr::VROverlayError_RequestFailed; }
    const char *GetOverlayErrorNameFromEnum(vr::EVROverlayError error) override { return error == vr::VROverlayError_None ? "VROverlayError_None" : "VROverlayError"; }
    vr::EVROverlayError SetOverlayRenderingPid(vr::VROverlayHandle_t handle, uint32_t pid) override { logLine("SetOverlayRenderingPid %llu pid=%u", static_cast<unsigned long long>(handle), pid); return ok(handle); }
    uint32_t GetOverlayRenderingPid(vr::VROverlayHandle_t) override { return GetCurrentProcessId(); }
    vr::EVROverlayError SetOverlayFlag(vr::VROverlayHandle_t handle, vr::VROverlayFlags flag, bool enabled) override { logLine("SetOverlayFlag %llu flag=%d enabled=%d", static_cast<unsigned long long>(handle), flag, enabled ? 1 : 0); return ok(handle); }
    vr::EVROverlayError GetOverlayFlag(vr::VROverlayHandle_t, vr::VROverlayFlags, bool *enabled) override { if (enabled) *enabled = false; return vr::VROverlayError_None; }
    vr::EVROverlayError GetOverlayFlags(vr::VROverlayHandle_t, uint32_t *flags) override { if (flags) *flags = 0; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayColor(vr::VROverlayHandle_t handle, float r, float g, float b) override { logLine("SetOverlayColor %llu %.3f %.3f %.3f", static_cast<unsigned long long>(handle), r, g, b); return ok(handle); }
    vr::EVROverlayError GetOverlayColor(vr::VROverlayHandle_t, float *r, float *g, float *b) override { if (r) *r = 1; if (g) *g = 1; if (b) *b = 1; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayAlpha(vr::VROverlayHandle_t handle, float alpha) override { auto *o = find(handle); if (!o) return unknown(); o->alpha = alpha; logLine("SetOverlayAlpha %llu %.3f", static_cast<unsigned long long>(handle), alpha); return ok(handle); }
    vr::EVROverlayError GetOverlayAlpha(vr::VROverlayHandle_t handle, float *alpha) override { auto *o = find(handle); if (!o) return unknown(); if (alpha) *alpha = o->alpha; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayTexelAspect(vr::VROverlayHandle_t handle, float aspect) override { logLine("SetOverlayTexelAspect %llu %.3f", static_cast<unsigned long long>(handle), aspect); return ok(handle); }
    vr::EVROverlayError GetOverlayTexelAspect(vr::VROverlayHandle_t, float *aspect) override { if (aspect) *aspect = 1.0f; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlaySortOrder(vr::VROverlayHandle_t handle, uint32_t order) override { logLine("SetOverlaySortOrder %llu %u", static_cast<unsigned long long>(handle), order); return ok(handle); }
    vr::EVROverlayError GetOverlaySortOrder(vr::VROverlayHandle_t, uint32_t *order) override { if (order) *order = 0; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayWidthInMeters(vr::VROverlayHandle_t handle, float width) override { auto *o = find(handle); if (!o) return unknown(); o->widthMeters = width; logLine("SetOverlayWidthInMeters %llu %.3f", static_cast<unsigned long long>(handle), width); return ok(handle); }
    vr::EVROverlayError GetOverlayWidthInMeters(vr::VROverlayHandle_t handle, float *width) override { auto *o = find(handle); if (!o) return unknown(); if (width) *width = o->widthMeters; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayCurvature(vr::VROverlayHandle_t handle, float curv) override { logLine("SetOverlayCurvature %llu %.3f", static_cast<unsigned long long>(handle), curv); return ok(handle); }
    vr::EVROverlayError GetOverlayCurvature(vr::VROverlayHandle_t, float *curv) override { if (curv) *curv = 0; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayPreCurvePitch(vr::VROverlayHandle_t handle, float radians) override { logLine("SetOverlayPreCurvePitch %llu %.3f", static_cast<unsigned long long>(handle), radians); return ok(handle); }
    vr::EVROverlayError GetOverlayPreCurvePitch(vr::VROverlayHandle_t, float *radians) override { if (radians) *radians = 0; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayTextureColorSpace(vr::VROverlayHandle_t handle, vr::EColorSpace cs) override { logLine("SetOverlayTextureColorSpace %llu %d", static_cast<unsigned long long>(handle), cs); return ok(handle); }
    vr::EVROverlayError GetOverlayTextureColorSpace(vr::VROverlayHandle_t, vr::EColorSpace *cs) override { if (cs) *cs = vr::ColorSpace_Auto; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayTextureBounds(vr::VROverlayHandle_t handle, const vr::VRTextureBounds_t *bounds) override { if (bounds) logLine("SetOverlayTextureBounds %llu %.3f %.3f %.3f %.3f", static_cast<unsigned long long>(handle), bounds->uMin, bounds->vMin, bounds->uMax, bounds->vMax); return ok(handle); }
    vr::EVROverlayError GetOverlayTextureBounds(vr::VROverlayHandle_t, vr::VRTextureBounds_t *bounds) override { if (bounds) { bounds->uMin = 0; bounds->vMin = 0; bounds->uMax = 1; bounds->vMax = 1; } return vr::VROverlayError_None; }
    vr::EVROverlayError GetOverlayTransformType(vr::VROverlayHandle_t, vr::VROverlayTransformType *type) override { if (type) *type = vr::VROverlayTransform_Absolute; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayTransformAbsolute(vr::VROverlayHandle_t handle, vr::ETrackingUniverseOrigin, const vr::HmdMatrix34_t *) override { logLine("SetOverlayTransformAbsolute %llu", static_cast<unsigned long long>(handle)); return ok(handle); }
    vr::EVROverlayError GetOverlayTransformAbsolute(vr::VROverlayHandle_t, vr::ETrackingUniverseOrigin *origin, vr::HmdMatrix34_t *mat) override { if (origin) *origin = vr::TrackingUniverseStanding; identity(mat); return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayTransformTrackedDeviceRelative(vr::VROverlayHandle_t handle, vr::TrackedDeviceIndex_t, const vr::HmdMatrix34_t *) override { logLine("SetOverlayTransformTrackedDeviceRelative %llu", static_cast<unsigned long long>(handle)); return ok(handle); }
    vr::EVROverlayError GetOverlayTransformTrackedDeviceRelative(vr::VROverlayHandle_t, vr::TrackedDeviceIndex_t *device, vr::HmdMatrix34_t *mat) override { if (device) *device = 0; identity(mat); return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayTransformTrackedDeviceComponent(vr::VROverlayHandle_t handle, vr::TrackedDeviceIndex_t, const char *) override { logLine("SetOverlayTransformTrackedDeviceComponent %llu", static_cast<unsigned long long>(handle)); return ok(handle); }
    vr::EVROverlayError GetOverlayTransformTrackedDeviceComponent(vr::VROverlayHandle_t, vr::TrackedDeviceIndex_t *device, char *component, uint32_t size) override { if (device) *device = 0; if (component && size) component[0] = 0; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayTransformCursor(vr::VROverlayHandle_t handle, const vr::HmdVector2_t *) override { return ok(handle); }
    vr::EVROverlayError GetOverlayTransformCursor(vr::VROverlayHandle_t, vr::HmdVector2_t *hotspot) override { if (hotspot) { hotspot->v[0] = 0; hotspot->v[1] = 0; } return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayTransformProjection(vr::VROverlayHandle_t handle, vr::ETrackingUniverseOrigin, const vr::HmdMatrix34_t *, const vr::VROverlayProjection_t *, vr::EVREye) override { return ok(handle); }
    vr::EVROverlayError SetSubviewPosition(vr::VROverlayHandle_t handle, float x, float y) override { logLine("SetSubviewPosition %llu %.1f %.1f", static_cast<unsigned long long>(handle), x, y); return ok(handle); }
    vr::EVROverlayError ShowOverlay(vr::VROverlayHandle_t handle) override { auto *o = find(handle); if (!o) return unknown(); o->visible = true; logLine("ShowOverlay %llu", static_cast<unsigned long long>(handle)); return ok(handle); }
    vr::EVROverlayError HideOverlay(vr::VROverlayHandle_t handle) override { auto *o = find(handle); if (!o) return unknown(); o->visible = false; logLine("HideOverlay %llu", static_cast<unsigned long long>(handle)); return ok(handle); }
    bool IsOverlayVisible(vr::VROverlayHandle_t handle) override { auto *o = find(handle); return o && o->visible; }
    vr::EVROverlayError GetTransformForOverlayCoordinates(vr::VROverlayHandle_t, vr::ETrackingUniverseOrigin, vr::HmdVector2_t, vr::HmdMatrix34_t *mat) override { identity(mat); return vr::VROverlayError_None; }
    vr::EVROverlayError WaitFrameSync(uint32_t) override { return vr::VROverlayError_None; }
    bool PollNextOverlayEvent(vr::VROverlayHandle_t, vr::VREvent_t *, uint32_t) override { return false; }
    vr::EVROverlayError GetOverlayInputMethod(vr::VROverlayHandle_t, vr::VROverlayInputMethod *method) override { if (method) *method = vr::VROverlayInputMethod_None; return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayInputMethod(vr::VROverlayHandle_t handle, vr::VROverlayInputMethod method) override { logLine("SetOverlayInputMethod %llu %d", static_cast<unsigned long long>(handle), method); return ok(handle); }
    vr::EVROverlayError GetOverlayMouseScale(vr::VROverlayHandle_t, vr::HmdVector2_t *scale) override { if (scale) { scale->v[0] = 1024; scale->v[1] = 768; } return vr::VROverlayError_None; }
    vr::EVROverlayError SetOverlayMouseScale(vr::VROverlayHandle_t handle, const vr::HmdVector2_t *scale) override { if (scale) logLine("SetOverlayMouseScale %llu %.1f %.1f", static_cast<unsigned long long>(handle), scale->v[0], scale->v[1]); return ok(handle); }
    bool ComputeOverlayIntersection(vr::VROverlayHandle_t, const vr::VROverlayIntersectionParams_t *, vr::VROverlayIntersectionResults_t *) override { return false; }
    bool IsHoverTargetOverlay(vr::VROverlayHandle_t) override { return false; }
    vr::EVROverlayError SetOverlayIntersectionMask(vr::VROverlayHandle_t handle, vr::VROverlayIntersectionMaskPrimitive_t *, uint32_t, uint32_t) override { return ok(handle); }
    vr::EVROverlayError TriggerLaserMouseHapticVibration(vr::VROverlayHandle_t handle, float, float, float) override { return ok(handle); }
    vr::EVROverlayError SetOverlayCursor(vr::VROverlayHandle_t handle, vr::VROverlayHandle_t) override { return ok(handle); }
    vr::EVROverlayError SetOverlayCursorPositionOverride(vr::VROverlayHandle_t handle, const vr::HmdVector2_t *) override { return ok(handle); }
    vr::EVROverlayError ClearOverlayCursorPositionOverride(vr::VROverlayHandle_t handle) override { return ok(handle); }

    vr::EVROverlayError SetOverlayTexture(vr::VROverlayHandle_t handle, const vr::Texture_t *texture) override
    {
        auto *o = find(handle);
        if (!o || !texture)
            return unknown();
        o->texture = *texture;
        o->visible = true;
        logLine("SetOverlayTexture %llu texture=%p type=%d color=%d", static_cast<unsigned long long>(handle), texture->handle, texture->eType, texture->eColorSpace);
        return ok(handle);
    }

    vr::EVROverlayError ClearOverlayTexture(vr::VROverlayHandle_t handle) override { auto *o = find(handle); if (!o) return unknown(); ZeroMemory(&o->texture, sizeof(o->texture)); logLine("ClearOverlayTexture %llu", static_cast<unsigned long long>(handle)); return ok(handle); }
    vr::EVROverlayError SetOverlayRaw(vr::VROverlayHandle_t handle, void *, uint32_t width, uint32_t height, uint32_t bpp) override { logLine("SetOverlayRaw %llu %ux%u bpp=%u", static_cast<unsigned long long>(handle), width, height, bpp); return ok(handle); }
    vr::EVROverlayError SetOverlayFromFile(vr::VROverlayHandle_t handle, const char *path) override { logLine("SetOverlayFromFile %llu '%s'", static_cast<unsigned long long>(handle), path ? path : ""); return ok(handle); }
    vr::EVROverlayError GetOverlayTexture(vr::VROverlayHandle_t handle, void **native, void *, uint32_t *width, uint32_t *height, uint32_t *format, vr::ETextureType *type, vr::EColorSpace *color, vr::VRTextureBounds_t *bounds) override { auto *o = find(handle); if (!o) return unknown(); if (native) *native = o->texture.handle; if (width) *width = 1024; if (height) *height = 768; if (format) *format = 29; if (type) *type = o->texture.eType; if (color) *color = o->texture.eColorSpace; if (bounds) { bounds->uMin = 0; bounds->vMin = 0; bounds->uMax = 1; bounds->vMax = 1; } return vr::VROverlayError_None; }
    vr::EVROverlayError ReleaseNativeOverlayHandle(vr::VROverlayHandle_t, void *) override { return vr::VROverlayError_None; }
    vr::EVROverlayError GetOverlayTextureSize(vr::VROverlayHandle_t, uint32_t *width, uint32_t *height) override { if (width) *width = 1024; if (height) *height = 768; return vr::VROverlayError_None; }

    vr::EVROverlayError CreateDashboardOverlay(const char *key, const char *name, vr::VROverlayHandle_t *mainHandle, vr::VROverlayHandle_t *thumbHandle) override
    {
        const auto main = createOverlay(key, name, true);
        const auto thumb = createOverlay((std::string(key ? key : "") + ".thumbnail").c_str(), name, true);
        if (mainHandle) *mainHandle = main;
        if (thumbHandle) *thumbHandle = thumb;
        logLine("CreateDashboardOverlay key='%s' name='%s' -> main=%llu thumb=%llu", key ? key : "", name ? name : "", static_cast<unsigned long long>(main), static_cast<unsigned long long>(thumb));
        return ok(main);
    }

    bool IsDashboardVisible() override { return true; }
    bool IsActiveDashboardOverlay(vr::VROverlayHandle_t handle) override { auto *o = find(handle); return o && o->dashboard; }
    vr::EVROverlayError SetDashboardOverlaySceneProcess(vr::VROverlayHandle_t handle, uint32_t pid) override { logLine("SetDashboardOverlaySceneProcess %llu pid=%u", static_cast<unsigned long long>(handle), pid); return ok(handle); }
    vr::EVROverlayError GetDashboardOverlaySceneProcess(vr::VROverlayHandle_t, uint32_t *pid) override { if (pid) *pid = GetCurrentProcessId(); return vr::VROverlayError_None; }
    void ShowDashboard(const char *overlayToShow) override { logLine("ShowDashboard '%s'", overlayToShow ? overlayToShow : ""); }
    vr::TrackedDeviceIndex_t GetPrimaryDashboardDevice() override { return 0; }
    vr::EVROverlayError ShowKeyboard(vr::EGamepadTextInputMode, vr::EGamepadTextInputLineMode, uint32_t, const char *, uint32_t, const char *, uint64_t) override { return vr::VROverlayError_RequestFailed; }
    vr::EVROverlayError ShowKeyboardForOverlay(vr::VROverlayHandle_t, vr::EGamepadTextInputMode, vr::EGamepadTextInputLineMode, uint32_t, const char *, uint32_t, const char *, uint64_t) override { return vr::VROverlayError_RequestFailed; }
    uint32_t GetKeyboardText(char *text, uint32_t size) override { if (text && size) text[0] = 0; return 0; }
    void HideKeyboard() override {}
    void SetKeyboardTransformAbsolute(vr::ETrackingUniverseOrigin, const vr::HmdMatrix34_t *) override {}
    void SetKeyboardPositionForOverlay(vr::VROverlayHandle_t, vr::HmdRect2_t) override {}
    vr::VRMessageOverlayResponse ShowMessageOverlay(const char *, const char *, const char *, const char *, const char *, const char *) override { return vr::VRMessageOverlayResponse_ButtonPress_0; }
    void CloseMessageOverlay() override {}

private:
    OverlayState *find(vr::VROverlayHandle_t handle)
    {
        std::lock_guard<std::mutex> lock(g_mutex);
        const auto it = g_overlays.find(handle);
        return it == g_overlays.end() ? nullptr : &it->second;
    }

    uint32_t copyString(vr::VROverlayHandle_t handle, char *value, uint32_t size, vr::EVROverlayError *error, bool key)
    {
        auto *o = find(handle);
        if (!o)
        {
            if (error) *error = vr::VROverlayError_UnknownOverlay;
            return 0;
        }
        const std::string &src = key ? o->key : o->name;
        if (error) *error = vr::VROverlayError_None;
        if (value && size)
            strncpy_s(value, size, src.c_str(), _TRUNCATE);
        return static_cast<uint32_t>(src.size() + 1);
    }

    void identity(vr::HmdMatrix34_t *mat)
    {
        if (!mat)
            return;
        ZeroMemory(mat, sizeof(*mat));
        mat->m[0][0] = 1.0f;
        mat->m[1][1] = 1.0f;
        mat->m[2][2] = 1.0f;
        mat->m[2][3] = -1.25f;
    }
};

ReShadeOverlay g_overlay;
}

extern "C" __declspec(dllexport) void *VR_CALLTYPE VR_GetGenericInterface(const char *version, vr::EVRInitError *error)
{
    initBridge();
    loadRealOpenVR();
    logLine("VR_GetGenericInterface '%s'", version ? version : "");
    if (g_realRuntimeActive && realVRGetGenericInterface)
    {
        void *realInterface = realVRGetGenericInterface(version, error);
        if (realInterface)
        {
            logLine(" -> proxied real interface");
            return realInterface;
        }
    }
    if (version && strcmp(version, vr::IVROverlay_Version) == 0)
    {
        if (error) *error = vr::VRInitError_None;
        logLine(" -> fake IVROverlay bridge");
        return &g_overlay;
    }
    if (error) *error = vr::VRInitError_Init_InterfaceNotFound;
    return nullptr;
}

extern "C" __declspec(dllexport) uint32_t VR_CALLTYPE VR_InitInternal2(vr::EVRInitError *error, vr::EVRApplicationType appType, const char *startupInfo)
{
    initBridge();
    loadRealOpenVR();
    logLine("VR_InitInternal2 appType=%d startup='%s'", appType, startupInfo ? startupInfo : "");
    if (realVRInitInternal2)
    {
        vr::EVRInitError realError = vr::VRInitError_Unknown;
        const uint32_t token = realVRInitInternal2(&realError, appType, startupInfo);
        if (realError == vr::VRInitError_None)
        {
            g_realRuntimeActive = true;
            if (error) *error = realError;
            logLine(" -> real OpenVR init ok token=%u", token);
            return token;
        }
        logLine(" -> real OpenVR init failed err=%d, falling back to fake overlay", realError);
    }
    if (error) *error = vr::VRInitError_None;
    return 1;
}

extern "C" __declspec(dllexport) uint32_t VR_CALLTYPE VR_InitInternal(vr::EVRInitError *error, vr::EVRApplicationType appType)
{
    return VR_InitInternal2(error, appType, nullptr);
}

extern "C" __declspec(dllexport) void VR_CALLTYPE VR_ShutdownInternal()
{
    logLine("VR_ShutdownInternal");
    if (g_realRuntimeActive && realVRShutdownInternal)
        realVRShutdownInternal();
    g_realRuntimeActive = false;
}

extern "C" __declspec(dllexport) bool VR_CALLTYPE VR_IsHmdPresent()
{
    loadRealOpenVR();
    logLine("VR_IsHmdPresent");
    if (realVRIsHmdPresent)
        return realVRIsHmdPresent();
    return true;
}

extern "C" __declspec(dllexport) bool VR_CALLTYPE VR_IsRuntimeInstalled()
{
    loadRealOpenVR();
    logLine("VR_IsRuntimeInstalled");
    if (realVRIsRuntimeInstalled)
        return realVRIsRuntimeInstalled();
    return true;
}

extern "C" __declspec(dllexport) const char *VR_CALLTYPE VR_GetVRInitErrorAsSymbol(vr::EVRInitError error)
{
    loadRealOpenVR();
    if (realVRGetVRInitErrorAsSymbol)
        return realVRGetVRInitErrorAsSymbol(error);
    return error == vr::VRInitError_None ? "VRInitError_None" : "VRInitError";
}

extern "C" __declspec(dllexport) const char *VR_CALLTYPE VR_GetVRInitErrorAsEnglishDescription(vr::EVRInitError error)
{
    loadRealOpenVR();
    if (realVRGetVRInitErrorAsEnglishDescription)
        return realVRGetVRInitErrorAsEnglishDescription(error);
    return error == vr::VRInitError_None ? "No error" : "OpenVR bridge did not provide this interface";
}

extern "C" __declspec(dllexport) uint32_t VR_CALLTYPE VR_GetInitToken()
{
    loadRealOpenVR();
    if (g_realRuntimeActive && realVRGetInitToken)
        return realVRGetInitToken();
    return 1;
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = module;
        DisableThreadLibraryCalls(module);
        initBridge();
        logLine("Loaded openvr_api bridge");
    }
    else if (reason == DLL_PROCESS_DETACH)
    {
        logLine("Unloaded openvr_api bridge");
        if (g_bridge)
        {
            UnmapViewOfFile(g_bridge);
            g_bridge = nullptr;
        }
        if (g_mapping)
        {
            CloseHandle(g_mapping);
            g_mapping = nullptr;
        }
        if (g_realOpenVR)
        {
            FreeLibrary(g_realOpenVR);
            g_realOpenVR = nullptr;
        }
    }
    return TRUE;
}
