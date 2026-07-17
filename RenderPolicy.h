#pragma once

#include <algorithm>
#include <cstddef>
#include <cstdint>

namespace viewlab::policy {

constexpr uint32_t GraphFrameInterval = 1u << 0;
constexpr uint32_t GraphFps = 1u << 1;
constexpr uint32_t GraphBudgetDeviation = 1u << 2;
constexpr uint32_t GraphAppWork = 1u << 3;
constexpr uint32_t GraphWaitDuration = 1u << 4;
constexpr uint32_t GraphSubmitDuration = 1u << 5;
constexpr uint32_t GraphDisplayPeriod = 1u << 6;

inline uint32_t CompatibleGraphChannels(uint32_t mode) {
    switch (mode) {
        case 0: return GraphBudgetDeviation;
        case 1: return GraphFrameInterval | GraphAppWork | GraphWaitDuration | GraphSubmitDuration | GraphDisplayPeriod;
        case 2: return GraphFps;
        case 3: return GraphFrameInterval | GraphAppWork;
        default: return 0;
    }
}

inline uint32_t DefaultGraphChannel(uint32_t mode) {
    switch (mode) {
        case 0: return GraphBudgetDeviation;
        case 1: return GraphFrameInterval;
        case 2: return GraphFps;
        case 3: return GraphFrameInterval;
        default: return 0;
    }
}

inline uint32_t EffectiveGraphChannels(uint32_t mode, uint32_t selected) {
    const uint32_t compatible = selected & CompatibleGraphChannels(mode);
    return compatible != 0 ? compatible : DefaultGraphChannel(mode);
}

struct TraceVisibilityState {
    float alpha = 0.f;
    uint64_t holdUntil = 0;
    uint64_t fadeStart = 0;
    uint32_t mode = 0;
};

struct SustainedAlarmState {
    int stableState = -1;
    int stableSeverity = -1;
    int pendingDirection = 0;
    int pendingState = -1;
    int pendingSeverity = -1;
    uint64_t pendingSince = 0;
    uint64_t holdUntil = 0;
    bool visible = false;
};

inline void UpdateSustainedAlarm(SustainedAlarmState& state, int desiredState, int desiredSeverity,
    uint64_t now, uint64_t entryMs, uint64_t recoveryMs, uint64_t holdMs) {
    if (desiredSeverity < 0) {
        state = SustainedAlarmState{};
        state.stableState = desiredState;
        state.stableSeverity = desiredSeverity;
        return;
    }
    if (state.stableSeverity < 0) {
        state.stableState = desiredSeverity >= 2 ? 0 : desiredState;
        state.stableSeverity = desiredSeverity >= 2 ? 0 : desiredSeverity;
        state.pendingState = desiredState;
        state.pendingSeverity = desiredSeverity;
        if (desiredSeverity >= 2) {
            state.pendingDirection = 1;
            state.pendingSince = now;
        }
    } else if (desiredSeverity == state.stableSeverity) {
        state.stableState = desiredState;
        state.pendingDirection = 0;
        state.pendingSince = 0;
    } else {
        const int direction = desiredSeverity > state.stableSeverity ? 1 : -1;
        if (state.pendingDirection != direction) {
            state.pendingDirection = direction;
            state.pendingSince = now;
        }
        state.pendingState = desiredState;
        state.pendingSeverity = desiredSeverity;
        const uint64_t required = direction > 0 ? entryMs : recoveryMs;
        if (required == 0 || now - state.pendingSince >= required) {
            state.stableState = state.pendingState;
            state.stableSeverity = state.pendingSeverity;
            state.pendingDirection = 0;
            state.pendingSince = 0;
        }
    }

    const bool desiredCritical = desiredSeverity >= 2;
    const bool stableCritical = state.stableSeverity >= 2;
    if (desiredCritical) {
        state.holdUntil = 0;
    } else if (state.visible && state.holdUntil == 0) {
        state.holdUntil = now + holdMs;
    }
    if (stableCritical) {
        state.visible = true;
    } else if (state.visible && (state.holdUntil == 0 || now >= state.holdUntil)) {
        state.visible = false;
        state.holdUntil = 0;
    }
}

inline float UpdateTraceVisibility(TraceVisibilityState& state, uint32_t mode, bool trouble,
    uint64_t now, uint64_t holdMs, uint64_t fadeMs = 500) {
    const bool changed = state.mode != mode;
    state.mode = mode;
    if (mode == 0) {
        state.alpha = 0.f; state.holdUntil = 0; state.fadeStart = 0; return state.alpha;
    }
    if (mode == 1) {
        state.alpha = 1.f; state.holdUntil = 0; state.fadeStart = 0; return state.alpha;
    }
    if (changed) {
        state.alpha = 0.f; state.holdUntil = 0; state.fadeStart = 0;
    }
    if (trouble) {
        state.alpha = 1.f; state.holdUntil = now + holdMs; state.fadeStart = 0; return state.alpha;
    }
    if (now < state.holdUntil) {
        state.alpha = 1.f; return state.alpha;
    }
    if (state.alpha <= 0.f) return state.alpha = 0.f;
    if (state.fadeStart == 0) state.fadeStart = now;
    const float elapsed = static_cast<float>(now - state.fadeStart);
    state.alpha = fadeMs == 0 ? 0.f : std::clamp(1.f - elapsed / static_cast<float>(fadeMs), 0.f, 1.f);
    return state.alpha;
}

struct HudRowLayout {
    size_t widgetsPerRow = 0;
    size_t rowCount = 0;
    float gap = 0.f;
    float width = 0.f;
};

inline HudRowLayout SingleRowHudLayout(size_t count, float radius, float unit, float spacing) {
    HudRowLayout result{};
    result.widgetsPerRow = count;
    result.rowCount = count == 0 ? 0 : 1;
    result.gap = unit * (0.25f + 3.f * std::clamp(spacing, 0.f, 0.10f));
    result.width = count == 0 ? 0.f : radius * 2.f * static_cast<float>(count) + result.gap * static_cast<float>(count - 1);
    return result;
}

struct NotificationAnimation { float alpha = 0.f; float slide = 1.f; };

inline NotificationAnimation EvaluateNotificationAnimation(uint32_t now, uint32_t enterTick,
    uint32_t leaveTick, uint32_t riseMs = 250, uint32_t leaveMs = 400) {
    NotificationAnimation result{1.f, 0.f};
    if (leaveTick != 0) {
        const float p = leaveMs == 0 ? 1.f : std::clamp(static_cast<float>(now - leaveTick) / static_cast<float>(leaveMs), 0.f, 1.f);
        result.alpha = 1.f - p; result.slide = p; return result;
    }
    const float p = riseMs == 0 ? 1.f : std::clamp(static_cast<float>(now - enterTick) / static_cast<float>(riseMs), 0.f, 1.f);
    result.alpha = p; result.slide = 1.f - p; return result;
}

inline bool LatchTopmostDemand(bool alreadyDemanded, bool distinctApplicationLayer) {
    return alreadyDemanded || distinctApplicationLayer;
}

} // namespace viewlab::policy
