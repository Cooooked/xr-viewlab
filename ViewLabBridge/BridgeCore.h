#pragma once

#include <cstdint>

namespace viewlab::bridge {

// Product-facing runtime facts.  OpenVR/OpenXR implementation details remain behind this
// boundary so the layer and eventual loader do not grow translation-specific branches.
enum class GraphicsApi : std::uint8_t {
    Unknown,
    D3D11,
    D3D12,
    Vulkan,
    OpenGL,
};

enum class OverlayBackend : std::uint8_t {
    DirectEyeTexture,
    HeadLockedQuad,
    SeparateProjection,
    FeatureDisabled,
};

struct RuntimeCapabilities {
    GraphicsApi graphicsApi = GraphicsApi::Unknown;
    bool hasPrimaryProjection = false;
    bool hasCompositionLayers = false;
    bool hasDistinctCompositionLayer = false;
    bool canWriteEyeTexture = false;
    bool supportsQuadLayers = false;
    bool supportsAdditionalProjectionLayers = false;
    bool orderedPresentationReady = false;
};

struct FeaturePresentationPlan {
    bool drawDirectVisor = false;
    bool drawDirectCommonFeatures = false;
    OverlayBackend orderedBackend = OverlayBackend::FeatureDisabled;
};

struct TextureBounds {
    float uMin = 0.0f;
    float vMin = 0.0f;
    float uMax = 1.0f;
    float vMax = 1.0f;
};

struct PixelRect {
    std::int32_t x = 0;
    std::int32_t y = 0;
    std::int32_t width = 0;
    std::int32_t height = 0;
};

OverlayBackend SelectOverlayBackend(const RuntimeCapabilities& capabilities) noexcept;
FeaturePresentationPlan SelectFeaturePresentationPlan(const RuntimeCapabilities& capabilities) noexcept;
PixelRect MapTextureBounds(TextureBounds bounds, std::uint32_t width, std::uint32_t height) noexcept;
const char* OverlayBackendName(OverlayBackend backend) noexcept;

} // namespace viewlab::bridge
