// Single-source model of ViewLab's overlay-over-visor compositing, so the magenta-edge contract
// can be proven deterministically on the CPU without a live D3D pipeline. This mirrors exactly what
// the runtime does: the visor is drawn OPAQUE in the configured colour (kVisorPS returns
// float4(visorColor.rgb, 1.0)); overlay cards are then drawn on top with straight-alpha "over"
// blending (SrcAlpha, InvSrcAlpha) and the textured PS output float4(rgb, a*tint.a). The card's
// transparent padding is stored as (0,0,0,0) so it contributes no colour.
//
// The point being proven: ViewLab introduces NO magenta contamination. A fully transparent overlay
// texel leaves the magenta visor exactly as-is; a fully opaque overlay texel fully replaces it. Any
// magenta visible in a partially-covered anti-aliased edge texel is the visor legitimately showing
// through the overlay's own coverage fraction (1 - alpha), not colour bleeding out of the overlay.
#pragma once
#include <cstdint>
#include <algorithm>

namespace viewlab::composite {

struct Rgba { float r, g, b, a; };

// Straight-alpha "over": src drawn onto an opaque destination. Matches OMSetBlendState
// (SrcAlpha / InvSrcAlpha) with the textured PS emitting straight alpha.
inline Rgba OverStraight(const Rgba& src, const Rgba& dstOpaque) {
    const float a = std::clamp(src.a, 0.f, 1.f);
    return {
        src.r * a + dstOpaque.r * (1.f - a),
        src.g * a + dstOpaque.g * (1.f - a),
        src.b * a + dstOpaque.b * (1.f - a),
        1.f // destination stays opaque
    };
}

// Round-trip the UI's premultiplied-BGRA -> straight-RGBA conversion for one 8-bit texel, with the
// rule that a fully transparent texel is emitted as (0,0,0,0).
inline void UnpremultiplyToStraight(uint8_t& r, uint8_t& g, uint8_t& b, uint8_t a) {
    if (a == 0) { r = g = b = 0; return; }
    r = static_cast<uint8_t>(std::min(255, r * 255 / a));
    g = static_cast<uint8_t>(std::min(255, g * 255 / a));
    b = static_cast<uint8_t>(std::min(255, b * 255 / a));
}

} // namespace viewlab::composite
