#pragma once
#include <cstdint>
#include <limits>

namespace viewlab::bridge::stereo {
// This contract requires an upstream producer of both eye transforms and a
// translated shader. Finished OpenXR images do not satisfy it.
enum class Method { TwoPass, Instanced, GeometryBroadcast, SecondaryPosition };
enum class Rejection { None, MissingContract, SideEffects, ExistingGeometryStage, Overflow };
struct Contract {
    bool pairedViews = false;
    bool translatedShader = false;
    bool arrayColorAndDepth = false;
    bool sideEffects = false;
    bool existingGeometryStage = false;
};
struct Plan {
    Method method = Method::TwoPass;
    Rejection rejection = Rejection::MissingContract;
    std::uint32_t instances = 0;
};
inline Plan Select(const Contract& c, Method requested, std::uint32_t instances) noexcept {
    if (!c.pairedViews || !c.translatedShader || !c.arrayColorAndDepth)
        return {Method::TwoPass, Rejection::MissingContract, instances};
    if (c.sideEffects) return {Method::TwoPass, Rejection::SideEffects, instances};
    if (c.existingGeometryStage)
        return {Method::TwoPass, Rejection::ExistingGeometryStage, instances};
    if (requested == Method::Instanced && instances > (std::numeric_limits<std::uint32_t>::max)()/2)
        return {Method::TwoPass, Rejection::Overflow, instances};
    return {requested, Rejection::None, requested == Method::Instanced ? instances*2 : instances};
}
}
