#pragma once

#include <array>
#include <algorithm>
#include <cmath>
#include <cstddef>
#include <cstdint>

namespace viewlab::network {

enum class State : uint8_t { Stable=0, Unstable=1, Disconnected=2 };

struct Stats {
    double pingMs = 0.0;
    double lossPercent = 0.0;
    double jitterMs = 0.0;
    State state = State::Stable;
    uint32_t sampleCount = 0;
    uint32_t successfulSamples = 0;
    uint32_t consecutiveFailures = 0;
};

class Window {
public:
    Stats Record(bool success, double roundTripMs) {
        const size_t slot = next_++ % outcomes_.size();
        outcomes_[slot] = success ? 1u : 0u;
        if (count_ < outcomes_.size()) ++count_;
        if (success) {
            roundTripMs = std::clamp(roundTripMs, 0.0, 5000.0);
            if (havePrevious_) {
                jitter_[jitterNext_++ % jitter_.size()] = std::abs(roundTripMs - previousRtt_);
                if (jitterCount_ < jitter_.size()) ++jitterCount_;
            }
            previousRtt_ = roundTripMs; havePrevious_ = true; lastPingMs_ = roundTripMs;
            ++successfulSamples_;
            consecutiveFailures_ = 0;
        } else {
            ++consecutiveFailures_;
        }
        uint32_t successes = 0; for (size_t i=0;i<count_;++i) successes += outcomes_[i];
        double jitter = 0.0; for (size_t i=0;i<jitterCount_;++i) jitter += jitter_[i];
        Stats result{};
        result.pingMs = lastPingMs_;
        result.lossPercent = count_ ? 100.0 * double(count_ - successes) / double(count_) : 0.0;
        result.jitterMs = jitterCount_ ? jitter / double(jitterCount_) : 0.0;
        result.sampleCount = static_cast<uint32_t>(count_);
        result.successfulSamples = successfulSamples_;
        result.consecutiveFailures = consecutiveFailures_;
        result.state = consecutiveFailures_ >= 3 ? State::Disconnected
            : (result.lossPercent >= 5.0 || result.jitterMs >= 30.0 || (success && roundTripMs >= 150.0)
                ? State::Unstable : State::Stable);
        return result;
    }

private:
    std::array<uint8_t,20> outcomes_{};
    std::array<double,19> jitter_{};
    size_t next_=0,count_=0,jitterNext_=0,jitterCount_=0;
    uint32_t consecutiveFailures_=0,successfulSamples_=0;
    double previousRtt_=0.0,lastPingMs_=0.0;
    bool havePrevious_=false;
};

} // namespace viewlab::network
