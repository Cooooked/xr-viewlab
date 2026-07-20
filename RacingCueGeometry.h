#pragma once
// Single-source geometry/opacity mappings for the iRacing edge cues, shared by the native renderer
// (dllmain.cpp) and the deterministic fixture (Tests/RenderPolicyFixtures.cpp). Extracting these makes
// the behavioral audit real: a test can prove that changing a control (width, strength, opacity, fade,
// severity) changes the computed geometry/alpha, rather than only that a key is read.
#include <algorithm>
#include <cmath>

namespace viewlab::racing {

constexpr int kSpotterBands = 8;

// Spotter glow: total band width in pixels grows with the configured width, clamped to a safe range.
inline float SpotterWidthPx(double spotterWidth, float viewW) {
    return std::clamp(static_cast<float>(spotterWidth) * viewW, 8.f, viewW * 0.35f);
}
// Base intensity is opacity x strength (both raise brightness).
inline float SpotterBase(double opacity, double strength) {
    return static_cast<float>(opacity * strength);
}
// Per-band alpha. inward = (i+0.5)/kSpotterBands. The left edge is brightest at the outer edge
// (1-inward), the right edge at the inner edge (inward); higher fade sharpens the falloff.
inline float SpotterBandAlpha(float base, float inward, double fade, bool left) {
    return base * powf(left ? 1.f - inward : inward, static_cast<float>(fade));
}

// Rear-closing top-centre glow: half-width grows with proximity (0..1).
inline float RearGlowHalfWidth(double width01, float viewW) {
    return static_cast<float>(0.10 + 0.30 * width01) * viewW * 0.5f;
}

// Grip-O-Bar lower bar: width grows with severity (0..1); colour band splits yellow/orange/red.
inline float GripBarWidth(double severity01, float viewW) {
    return static_cast<float>(0.10 + 0.35 * severity01) * viewW * 0.5f;
}
inline int GripSeverityBand(double severity01) {
    return severity01 < 0.34 ? 0 : severity01 < 0.67 ? 1 : 2;
}

// Flag border: thickness grows with the configured width, clamped; opacity passes straight through.
inline float FlagBorderThickness(double flagWidth, float minDim) {
    return std::clamp(static_cast<float>(flagWidth) * minDim, 2.f, minDim * 0.12f);
}

// Race-start green envelope: full strength during the configured hold, then a linear fade.
inline float RaceStartGreenEnvelope(double heldMs, double holdMs, double fadeMs) {
    if (heldMs <= holdMs) return 1.f;
    if (heldMs >= holdMs + fadeMs) return 0.f;
    return static_cast<float>(1.0 - (heldMs - holdMs) / fadeMs);
}
// Race-start border thickness grows with the configured fraction of the view, clamped.
inline float RaceStartBorderThickness(double thickness, float minDim) {
    return std::clamp(static_cast<float>(thickness) * minDim, 3.f, minDim * 0.12f);
}

} // namespace viewlab::racing
