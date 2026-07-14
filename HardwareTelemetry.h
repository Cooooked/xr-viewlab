#pragma once

#include <array>
#include <cstdint>

namespace viewlab::telemetry {

enum class MetricId : uint8_t {
    CpuUtilisation, CpuPeakCore, CpuFrequency, GpuUtilisation,
    RamPressure, CommitPressure, VramPressure, SystemHeadroom,
    NetworkPing, NetworkLoss, NetworkJitter, NetworkStatus, Count
};

enum class Availability : uint8_t {
    Available, TemporarilyUnavailable, Unsupported, ProviderNotInstalled,
    PermissionUnavailable, Stale
};

struct MetricSample {
    double value = 0.0;       // primary display value
    double used = 0.0;        // optional used amount, GiB
    double capacity = 0.0;    // optional limit/budget, GiB
    uint64_t sampledAtMs = 0;
    Availability availability = Availability::TemporarilyUnavailable;
};

struct Snapshot {
    std::array<MetricSample, static_cast<size_t>(MetricId::Count)> metrics{};
    uint64_t generation = 0;
    uint64_t publishedAtMs = 0;
    uint32_t headroomCoverage = 0;
    double workerCpuMs = 0.0;
    uint64_t sampleCount = 0;
};

// Starts one bounded, allocation-free-after-initialisation collector. Safe to call repeatedly.
void Start();
void Stop();
void SetPreferredAdapterLuid(uint64_t luid);
void SetNetworkProbeTarget(uint32_t ipv4NetworkOrder);
void SetNetworkProbeEnabled(bool enabled);
bool TryGetSnapshot(Snapshot& snapshot); // never blocks the caller

} // namespace viewlab::telemetry
