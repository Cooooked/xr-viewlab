#include "../pch.h"
#include "../HardwareTelemetry.h"
#include <chrono>
#include <iostream>
#include <thread>

int main() {
    viewlab::telemetry::SetNetworkProbeTarget(inet_addr("1.1.1.1"));
    viewlab::telemetry::SetNetworkProbeEnabled(true);
    viewlab::telemetry::Start();
    std::this_thread::sleep_for(std::chrono::seconds(5));
    viewlab::telemetry::Snapshot snapshot{};
    const bool ok=viewlab::telemetry::TryGetSnapshot(snapshot);
    viewlab::telemetry::Stop();
    const auto& loss=snapshot.metrics[(size_t)viewlab::telemetry::MetricId::NetworkLoss];
    const auto& state=snapshot.metrics[(size_t)viewlab::telemetry::MetricId::NetworkStatus];
    if(!ok||snapshot.sampleCount<10||loss.availability!=viewlab::telemetry::Availability::Available||state.availability!=viewlab::telemetry::Availability::Available)return 2;
    std::cout<<"samples="<<snapshot.sampleCount<<" workerCpuMs="<<snapshot.workerCpuMs
             <<" wallMs=5000 cpuPercent="<<(snapshot.workerCpuMs/5000.0*100.0)
             <<" coverage="<<snapshot.headroomCoverage<<" networkLoss="<<loss.value
             <<" networkState="<<state.value<<"\n";
    return 0;
}
