// ViewLab Mirror — shared frame-transfer contract between the ViewLab OpenXR layer
// (producer) and the ViewLab Mirror OBS source plugin (consumer).
//
// Planned route (documented for the skeleton pass):
//   1. The ViewLab layer creates the "Local\\XRViewLabMirrorSurface" control block and a
//      triple-buffered ring of D3D11 shared textures (D3D11_RESOURCE_MISC_SHARED).
//   2. Once per OpenXR frame — immediately after xrBeginFrame returns down the layer chain,
//      the same capture point proven for the OpenXR-OBSMirror route — the layer composites
//      the submitted left-eye (or selected/both-eye) image plus every checkbox-selected
//      ViewLab feature into the ring texture at (frameNumber + 1) % 3, then publishes
//      displayIndex and increments frameNumber. Because ViewLab owns the producer, the
//      capture point can never overwrite ViewLab's own drawing (the defect the third-party
//      mirror route suffers from).
//   3. The OBS source opens the block read-only, opens the shared textures on OBS's own
//      D3D11 device, and each render draws the texture at displayIndex.
// The consumer treats a stale heartbeatTick (> 2 s) as "no VR session" and renders nothing.
#pragma once
#include <stdint.h>

#define VIEWLAB_MIRROR_SURFACE_NAME L"Local\\XRViewLabMirrorSurface"
#define VIEWLAB_MIRROR_MAGIC 0x534D4C56u /* 'VLMS' */
#define VIEWLAB_MIRROR_VERSION 1u

#pragma pack(push, 4)
typedef struct ViewLabMirrorSurface {
    uint32_t magic;           /* VIEWLAB_MIRROR_MAGIC */
    uint32_t version;         /* VIEWLAB_MIRROR_VERSION */
    uint32_t frameNumber;     /* producer increments after each completed ring copy */
    uint32_t displayIndex;    /* index (0..2) of the last completed ring texture */
    uint32_t width, height;   /* dimensions of every ring texture */
    uint32_t format;          /* DXGI_FORMAT of the ring textures */
    uint32_t eyeMode;         /* 0 left eye, 1 right eye, 2 side-by-side stereo */
    uint64_t heartbeatTick;   /* producer GetTickCount64 while a session submits frames */
    uint64_t sharedHandle[3]; /* legacy D3D11 shared handles of the ring textures */
} ViewLabMirrorSurface;
#pragma pack(pop)
