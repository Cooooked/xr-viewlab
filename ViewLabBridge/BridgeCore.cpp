#include "BridgeCore.h"

#include <algorithm>
#include <cmath>

namespace viewlab::bridge {

OverlayBackend SelectOverlayBackend(const RuntimeCapabilities& capabilities) noexcept {
    if (!capabilities.hasPrimaryProjection && !capabilities.hasCompositionLayers)
        return OverlayBackend::FeatureDisabled;

    // A direct write is the least intrusive path until another submitted layer can cover it.
    if (capabilities.hasPrimaryProjection && !capabilities.hasDistinctCompositionLayer &&
        capabilities.canWriteEyeTexture)
        return OverlayBackend::DirectEyeTexture;

    // Preserve the established stereo carrier when a current projection supplies the space, poses,
    // FOVs and dimensions needed to construct it. A generic core quad remains a later capability,
    // not a substitute merely because xrEndFrame accepts it: acceptance does not prove visibility.
    if (capabilities.supportsAdditionalProjectionLayers)
        return OverlayBackend::SeparateProjection;

    if (capabilities.supportsQuadLayers) return OverlayBackend::HeadLockedQuad;

    if (capabilities.canWriteEyeTexture) return OverlayBackend::DirectEyeTexture;
    return OverlayBackend::FeatureDisabled;
}

FeaturePresentationPlan SelectFeaturePresentationPlan(const RuntimeCapabilities& capabilities) noexcept {
    FeaturePresentationPlan plan{};
    if (!capabilities.hasPrimaryProjection && !capabilities.hasCompositionLayers) return plan;

    // Ordered presentation is additive. API success cannot prove compositor visibility, and black
    // visor pixels are idempotent when both the direct and ordered paths are visible.
    plan.drawDirectVisor = capabilities.hasPrimaryProjection && capabilities.canWriteEyeTexture;
    // Ordered presentation is additive until actual headset evidence proves it can replace a
    // working path. A distinct layer may cover direct pixels, but disabling the shared direct
    // renderer here made every common feature disappear together whenever the ordered layer was
    // absent for even one topology frame.
    plan.drawDirectCommonFeatures = capabilities.hasPrimaryProjection && capabilities.canWriteEyeTexture;
    if (capabilities.hasPrimaryProjection && !capabilities.hasDistinctCompositionLayer) return plan;
    if (capabilities.supportsAdditionalProjectionLayers)
        plan.orderedBackend = OverlayBackend::SeparateProjection;
    else if (capabilities.supportsQuadLayers)
        plan.orderedBackend = OverlayBackend::HeadLockedQuad;
    if (plan.orderedBackend != OverlayBackend::FeatureDisabled && capabilities.orderedPresentationReady) {
        plan.drawDirectVisor = false;
        plan.drawDirectCommonFeatures = false;
    }
    return plan;
}

PixelRect MapTextureBounds(TextureBounds bounds, std::uint32_t width, std::uint32_t height) noexcept {
    auto finiteOr = [](float value, float fallback) { return std::isfinite(value) ? value : fallback; };
    bounds.uMin = std::clamp(finiteOr(bounds.uMin, 0.0f), 0.0f, 1.0f);
    bounds.vMin = std::clamp(finiteOr(bounds.vMin, 0.0f), 0.0f, 1.0f);
    bounds.uMax = std::clamp(finiteOr(bounds.uMax, 1.0f), 0.0f, 1.0f);
    bounds.vMax = std::clamp(finiteOr(bounds.vMax, 1.0f), 0.0f, 1.0f);
    if (bounds.uMax < bounds.uMin) std::swap(bounds.uMin, bounds.uMax);
    if (bounds.vMax < bounds.vMin) std::swap(bounds.vMin, bounds.vMax);

    const auto left = static_cast<std::int32_t>(std::lround(bounds.uMin * width));
    const auto top = static_cast<std::int32_t>(std::lround(bounds.vMin * height));
    const auto right = static_cast<std::int32_t>(std::lround(bounds.uMax * width));
    const auto bottom = static_cast<std::int32_t>(std::lround(bounds.vMax * height));
    return {left, top, (std::max)(0, right - left), (std::max)(0, bottom - top)};
}

const char* OverlayBackendName(OverlayBackend backend) noexcept {
    switch (backend) {
    case OverlayBackend::DirectEyeTexture: return "direct-eye-texture";
    case OverlayBackend::HeadLockedQuad: return "head-locked-quad";
    case OverlayBackend::SeparateProjection: return "separate-projection";
    default: return "feature-disabled";
    }
}

} // namespace viewlab::bridge
