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

} // namespace viewlab::clock_widget
