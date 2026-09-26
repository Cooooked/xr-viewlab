#pragma once

#include <array>
#include <cstdint>
#include <cstdio>

namespace viewlab::clock_widget {

struct Text {
    std::array<char, 9> local{};    // HH:MM or HH:MM AM
    std::array<char, 9> session{};  // HH:MM:SS, capped at 99:59:59
};

inline Text Format(uint32_t localHour, uint32_t localMinute, uint64_t elapsedMilliseconds, bool twentyFourHour = true) {
    Text result{};
    localHour %= 24;
    localMinute %= 60;
    const uint64_t totalSeconds = elapsedMilliseconds / 1000;
    const uint32_t hours = static_cast<uint32_t>((totalSeconds / 3600) > 99 ? 99 : totalSeconds / 3600);
    const uint32_t minutes = hours == 99 && totalSeconds >= 100ull * 3600
        ? 59 : static_cast<uint32_t>((totalSeconds / 60) % 60);
    const uint32_t seconds = hours == 99 && totalSeconds >= 100ull * 3600
        ? 59 : static_cast<uint32_t>(totalSeconds % 60);
    const uint32_t displayHour = twentyFourHour ? localHour : (localHour % 12 == 0 ? 12 : localHour % 12);
    if (twentyFourHour) std::snprintf(result.local.data(), result.local.size(), "%02u:%02u", displayHour, localMinute);
    else std::snprintf(result.local.data(), result.local.size(), "%02u:%02u %s", displayHour, localMinute, localHour < 12 ? "AM" : "PM");
    std::snprintf(result.session.data(), result.session.size(), "%02u:%02u:%02u", hours, minutes, seconds);
    return result;
}

// All durations advance from a monotonic tick. Target time is resolved once from the local
// time at reset/session start, with a same-minute target scheduled for tomorrow.
inline uint32_t SecondsUntilLocalTarget(uint32_t nowSeconds, uint32_t targetSeconds) {
    nowSeconds %= 86400; targetSeconds %= 86400;
    const uint32_t delta = (targetSeconds + 86400 - nowSeconds) % 86400;
    return delta == 0 ? 86400 : delta;
}

struct TimerState {
    uint64_t startedTick = 0;
    uint64_t accumulatedMs = 0;
    uint64_t durationMs = 0;
    bool running = false;

    void Reset(uint64_t now, uint64_t duration, bool start = true) {
        startedTick = now; accumulatedMs = 0; durationMs = duration; running = start;
    }
    void Pause(uint64_t now) {
        if (running) { accumulatedMs += now >= startedTick ? now - startedTick : 0; running = false; }
    }
    void Resume(uint64_t now) { if (!running) { startedTick = now; running = true; } }
    uint64_t Elapsed(uint64_t now) const { return accumulatedMs + (running && now >= startedTick ? now - startedTick : 0); }
    uint64_t Remaining(uint64_t now) const { const auto elapsed = Elapsed(now); return elapsed >= durationMs ? 0 : durationMs - elapsed; }
};

} // namespace viewlab::clock_widget
