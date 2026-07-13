#include "../pch.h"
#include "../HardwareTelemetry.h"
#include <chrono>
#include <iostream>
#include <thread>

int main() {
    viewlab::telemetry::Start();
    std::this_thread::sleep_for(std::chrono::seconds(5));
    viewlab::telemetry::Snapshot snapshot{};
    const bool ok=viewlab::telemetry::TryGetSnapshot(snapshot);
    viewlab::telemetry::Stop();
    if(!ok||snapshot.sampleCount<10)return 2;
    std::cout<<"samples="<<snapshot.sampleCount<<" workerCpuMs="<<snapshot.workerCpuMs
             <<" wallMs=5000 cpuPercent="<<(snapshot.workerCpuMs/5000.0*100.0)
             <<" coverage="<<snapshot.headroomCoverage<<"\n";
    return 0;
}
