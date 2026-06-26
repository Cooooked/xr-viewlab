#include "pch_vr.h"
#include "overlay.h"
#include "shared_memory.h"
#include "log.h"

namespace {

vr::VROverlayHandle_t g_overlayHandle = vr::k_ulOverlayHandleInvalid;

// Build a SteamVR HmdMatrix34_t from position + quaternion.
// Convention: right-hand, Y-up, -Z forward (same as the OpenXR quad fields).
vr::HmdMatrix34_t QuatPosToMatrix(float px, float py, float pz,
                                   float qx, float qy, float qz, float qw) {
    // Normalise just in case
    float len = sqrtf(qx*qx + qy*qy + qz*qz + qw*qw);
    if (len < 1e-6f) { qw = 1.0f; qx = qy = qz = 0.0f; }
    else { qx /= len; qy /= len; qz /= len; qw /= len; }

    // Row-major 3x4 (SteamVR stores columns 0-2 as axes, column 3 as translation)
    vr::HmdMatrix34_t m{};
    m.m[0][0] = 1 - 2*(qy*qy + qz*qz);  m.m[0][1] = 2*(qx*qy - qz*qw);  m.m[0][2] = 2*(qx*qz + qy*qw);  m.m[0][3] = px;
    m.m[1][0] = 2*(qx*qy + qz*qw);      m.m[1][1] = 1 - 2*(qx*qx + qz*qz); m.m[1][2] = 2*(qy*qz - qx*qw); m.m[1][3] = py;
    m.m[2][0] = 2*(qx*qz - qy*qw);      m.m[2][1] = 2*(qy*qz + qx*qw);  m.m[2][2] = 1 - 2*(qx*qx + qy*qy); m.m[2][3] = pz;
    return m;
}

} // namespace

void InitOverlay() {
    vr::IVROverlay* ovr = vr::VROverlay();
    if (!ovr) {
        VrLog("InitOverlay: IVROverlay not available");
        return;
    }

    vr::EVROverlayError err = ovr->CreateOverlay("viewlab.reshade.menu", "ViewLab ReShade Menu", &g_overlayHandle);
    if (err != vr::VROverlayError_None) {
        VrLog("InitOverlay: CreateOverlay failed (%d)", (int)err);
        g_overlayHandle = vr::k_ulOverlayHandleInvalid;
        return;
    }

    // Default position: 1 m in front in standing space
    vr::HmdMatrix34_t defaultTransform = QuatPosToMatrix(0, 0, -1.0f, 0, 0, 0, 1);
    ovr->SetOverlayTransformAbsolute(g_overlayHandle, vr::TrackingUniverseStanding, &defaultTransform);
    ovr->SetOverlayWidthInMeters(g_overlayHandle, 1.0f);
    ovr->SetOverlayAlpha(g_overlayHandle, 1.0f);

    VrLog("InitOverlay: overlay created (handle=%llu)", (unsigned long long)g_overlayHandle);
}

void UpdateOverlay(ID3D11Texture2D* menuTex) {
    if (g_overlayHandle == vr::k_ulOverlayHandleInvalid) return;

    vr::IVROverlay* ovr = vr::VROverlay();
    if (!ovr) return;

    const VRControlBlock* block = GetVRBlock();
    if (!block) return;

    // In-HMD overlay is driven by menu_visible + overlay being enabled (non-zero quad_width signals intent)
    const bool show = (block->menu_visible != 0) && (block->quad_width > 0.0f);

    if (!show || !menuTex) {
        ovr->HideOverlay(g_overlayHandle);
        return;
    }

    // Apply transform from shared memory
    vr::HmdMatrix34_t m = QuatPosToMatrix(
        block->quad_pos_x, block->quad_pos_y, block->quad_pos_z,
        block->quad_quat_x, block->quad_quat_y, block->quad_quat_z, block->quad_quat_w);
    ovr->SetOverlayTransformAbsolute(g_overlayHandle, vr::TrackingUniverseStanding, &m);
    ovr->SetOverlayWidthInMeters(g_overlayHandle, block->quad_width > 0.0f ? block->quad_width : 1.0f);
    ovr->SetOverlayAlpha(g_overlayHandle, block->quad_alpha > 0.0f ? block->quad_alpha : 1.0f);

    vr::Texture_t tex{};
    tex.handle     = menuTex;
    tex.eType      = vr::TextureType_DirectX;
    tex.eColorSpace = vr::ColorSpace_Auto;
    ovr->SetOverlayTexture(g_overlayHandle, &tex);
    ovr->ShowOverlay(g_overlayHandle);
}

void ShutdownOverlay() {
    if (g_overlayHandle == vr::k_ulOverlayHandleInvalid) return;
    vr::IVROverlay* ovr = vr::VROverlay();
    if (ovr) ovr->DestroyOverlay(g_overlayHandle);
    g_overlayHandle = vr::k_ulOverlayHandleInvalid;
}
