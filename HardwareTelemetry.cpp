#include "pch.h"
#include "HardwareTelemetry.h"
#include "NetworkProbe.h"

#include <condition_variable>
#include <dxgi1_4.h>
#include <iphlpapi.h>
#include <icmpapi.h>
#include <powrprof.h>
#include <psapi.h>
#include <thread>
#pragma comment(lib, "dxgi.lib")
#pragma comment(lib, "powrprof.lib")
#pragma comment(lib, "psapi.lib")
#pragma comment(lib, "iphlpapi.lib")

namespace viewlab::telemetry { namespace {
struct ProcessorPowerInformation {
    ULONG Number, MaxMhz, CurrentMhz, MhzLimit, MaxIdleState, CurrentIdleState;
};
constexpr uint64_t kSamplePeriodMs = 250;
constexpr uint64_t kStaleAfterMs = 2000;
std::thread worker;
std::atomic<bool> stopping{false};
std::atomic<bool> running{false};
std::atomic<uint64_t> preferredLuid{0};
std::atomic<uint32_t> networkProbeTarget{0};
std::atomic<bool> networkProbeEnabled{false};
std::atomic<void(*)()> checkpointCallback{nullptr};
std::atomic<bool> checkpointRequested{false};
std::mutex wakeMutex, snapshotMutex, lifecycleMutex;
std::condition_variable wakeCondition;
Snapshot published{};

uint64_t FileTimeValue(const FILETIME& value) {
    ULARGE_INTEGER q{}; q.LowPart=value.dwLowDateTime; q.HighPart=value.dwHighDateTime; return q.QuadPart;
}
double Ema(double previous, double value, double alpha=.25) {
    if (!std::isfinite(value)) return previous;
    return previous > 0.0 ? previous*(1.0-alpha)+value*alpha : value;
}
void SetSample(Snapshot& s, MetricId id, double value, uint64_t now, double used=0, double capacity=0) {
    auto& m=s.metrics[static_cast<size_t>(id)];
    m.value=Ema(m.availability==Availability::Available?m.value:0.0,value);
    m.used=used; m.capacity=capacity; m.sampledAtMs=now; m.availability=Availability::Available;
}
void SetInstantSample(Snapshot& s, MetricId id, double value, uint64_t now) {
    auto& m=s.metrics[static_cast<size_t>(id)];m.value=value;m.used=0;m.capacity=0;m.sampledAtMs=now;m.availability=Availability::Available;
}

struct Providers {
    PDH_HQUERY gpuQuery=nullptr, cpuQuery=nullptr;
    PDH_HCOUNTER gpuCounter=nullptr, cpuCounter=nullptr;
    IDXGIFactory1* factory=nullptr;
    IDXGIAdapter3* adapter=nullptr;
    uint64_t adapterLuid=0;
    uint64_t lastIdle=0,lastKernel=0,lastUser=0;
    DWORD logicalProcessors=0;
    std::vector<ProcessorPowerInformation> power;
    std::array<uint8_t,65536> pdhBuffer{};
    HANDLE icmp=INVALID_HANDLE_VALUE;
    viewlab::network::Window networkWindow{};
    uint64_t lastNetworkProbeMs=0;

    void Init() {
        SYSTEM_INFO info{}; GetSystemInfo(&info); logicalProcessors=(std::max<DWORD>)(1,info.dwNumberOfProcessors);
        power.resize(logicalProcessors);
        if(PdhOpenQueryW(nullptr,0,&gpuQuery)==ERROR_SUCCESS) {
            if(PdhAddEnglishCounterW(gpuQuery,L"\\GPU Engine(*)\\Utilization Percentage",0,&gpuCounter)!=ERROR_SUCCESS) gpuCounter=nullptr;
            PdhCollectQueryData(gpuQuery);
        }
        if(PdhOpenQueryW(nullptr,0,&cpuQuery)==ERROR_SUCCESS) {
            if(PdhAddEnglishCounterW(cpuQuery,L"\\Processor Information(*)\\% Processor Utility",0,&cpuCounter)!=ERROR_SUCCESS) cpuCounter=nullptr;
            PdhCollectQueryData(cpuQuery);
        }
        CreateDXGIFactory1(__uuidof(IDXGIFactory1),reinterpret_cast<void**>(&factory));
        icmp=IcmpCreateFile();
    }
    void Shutdown() {
        if(adapter)adapter->Release(); adapter=nullptr;
        if(factory)factory->Release(); factory=nullptr;
        if(gpuQuery)PdhCloseQuery(gpuQuery); gpuQuery=nullptr; gpuCounter=nullptr;
        if(cpuQuery)PdhCloseQuery(cpuQuery); cpuQuery=nullptr; cpuCounter=nullptr;
        if(icmp!=INVALID_HANDLE_VALUE)IcmpCloseHandle(icmp);icmp=INVALID_HANDLE_VALUE;
    }
    bool SelectAdapter(uint64_t luid) {
        if(adapter && adapterLuid==luid)return true;
        if(adapter){adapter->Release();adapter=nullptr;} adapterLuid=0;
        if(!factory)return false;
        IDXGIAdapter1* candidate=nullptr;
        for(UINT i=0;factory->EnumAdapters1(i,&candidate)!=DXGI_ERROR_NOT_FOUND;++i) {
            DXGI_ADAPTER_DESC1 d{}; candidate->GetDesc1(&d);
            const uint64_t candidateLuid=(static_cast<uint64_t>(static_cast<uint32_t>(d.AdapterLuid.HighPart))<<32)|d.AdapterLuid.LowPart;
            if((luid && candidateLuid==luid)||(!luid && !(d.Flags&DXGI_ADAPTER_FLAG_SOFTWARE))) {
                candidate->QueryInterface(__uuidof(IDXGIAdapter3),reinterpret_cast<void**>(&adapter));
                adapterLuid=candidateLuid; candidate->Release(); return adapter!=nullptr;
            }
            candidate->Release(); candidate=nullptr;
        }
        return false;
    }
};

bool ParseGpuName(const wchar_t* name,uint64_t& luid,uint32_t& engine) {
    unsigned long pid=0,high=0,low=0,phys=0,eng=0; wchar_t type[64]{};
    if(!name||swscanf_s(name,L"pid_%lu_luid_0x%lx_0x%lx_phys_%lu_eng_%lu_engtype_%63s",&pid,&high,&low,&phys,&eng,type,(unsigned)_countof(type))!=6||_wcsicmp(type,L"3D"))return false;
    luid=(static_cast<uint64_t>(high)<<32)|low; engine=eng; return true;
}
double ReadGpu(Providers& p,uint64_t wanted,uint64_t& selected) {
    selected=0; if(!p.gpuCounter||PdhCollectQueryData(p.gpuQuery)!=ERROR_SUCCESS)return -1;
    DWORD count=0,size=0; if(PdhGetFormattedCounterArrayW(p.gpuCounter,PDH_FMT_DOUBLE,&size,&count,nullptr)!=PDH_MORE_DATA||size>p.pdhBuffer.size())return -1;
    auto* values=reinterpret_cast<PDH_FMT_COUNTERVALUE_ITEM_W*>(p.pdhBuffer.data());
    if(PdhGetFormattedCounterArrayW(p.gpuCounter,PDH_FMT_DOUBLE,&size,&count,values)!=ERROR_SUCCESS)return -1;
    struct Engine {uint64_t luid=0;uint32_t id=0;double value=0;};std::array<Engine,256> engines{};size_t engineCount=0;
    for(DWORD i=0;i<count;++i){uint64_t l=0;uint32_t e=0;if(values[i].FmtValue.CStatus!=ERROR_SUCCESS||!ParseGpuName(values[i].szName,l,e))continue;size_t slot=0;for(;slot<engineCount;++slot)if(engines[slot].luid==l&&engines[slot].id==e)break;if(slot==engineCount){if(engineCount==engines.size())continue;engines[engineCount++]={l,e,0};}engines[slot].value+=(std::max)(0.0,values[i].FmtValue.doubleValue);}
    struct Adapter {uint64_t luid=0;double value=0;};std::array<Adapter,32> adapters{};size_t adapterCount=0;
    for(size_t i=0;i<engineCount;++i){size_t slot=0;for(;slot<adapterCount;++slot)if(adapters[slot].luid==engines[i].luid)break;if(slot==adapterCount){if(adapterCount==adapters.size())continue;adapters[adapterCount++]={engines[i].luid,0};}adapters[slot].value=(std::max)(adapters[slot].value,std::clamp(engines[i].value,0.0,100.0));}
    if(!adapterCount)return -1;size_t best=0;for(size_t i=0;i<adapterCount;++i){if(wanted&&adapters[i].luid==wanted){best=i;break;}if(!wanted&&adapters[i].value>adapters[best].value)best=i;}selected=adapters[best].luid;return adapters[best].value;
}
double ReadPeakCore(Providers& p) {
    if(!p.cpuCounter||PdhCollectQueryData(p.cpuQuery)!=ERROR_SUCCESS)return -1;
    DWORD count=0,size=0;if(PdhGetFormattedCounterArrayW(p.cpuCounter,PDH_FMT_DOUBLE,&size,&count,nullptr)!=PDH_MORE_DATA||size>p.pdhBuffer.size())return -1;
    auto* values=reinterpret_cast<PDH_FMT_COUNTERVALUE_ITEM_W*>(p.pdhBuffer.data());if(PdhGetFormattedCounterArrayW(p.cpuCounter,PDH_FMT_DOUBLE,&size,&count,values)!=ERROR_SUCCESS)return -1;
    double peak=-1;for(DWORD i=0;i<count;++i)if(values[i].FmtValue.CStatus==ERROR_SUCCESS&&values[i].szName&&wcsstr(values[i].szName,L"_Total")==nullptr)peak=(std::max)(peak,std::clamp(values[i].FmtValue.doubleValue,0.0,100.0));return peak;
}
double MemoryPressure(double percent,double start,double full){return std::clamp((percent-start)/(full-start),0.0,1.0);}

void Run() {
    Providers p; p.Init(); Snapshot next{}; uint64_t tick=0; FILETIME created{},exited{},kernel0{},user0{}; GetThreadTimes(GetCurrentThread(),&created,&exited,&kernel0,&user0);
    while(!stopping.load(std::memory_order_acquire)) {
        const uint64_t now=GetTickCount64(); ++tick;
        FILETIME idle{},kernel{},user{};
        if(GetSystemTimes(&idle,&kernel,&user)){auto i=FileTimeValue(idle),k=FileTimeValue(kernel),u=FileTimeValue(user);if(p.lastKernel&&k>=p.lastKernel&&u>=p.lastUser&&i>=p.lastIdle){double total=double(k-p.lastKernel+u-p.lastUser);if(total>0)SetSample(next,MetricId::CpuUtilisation,std::clamp(100.0*(1.0-double(i-p.lastIdle)/total),0.0,100.0),now);}p.lastIdle=i;p.lastKernel=k;p.lastUser=u;}
        double peak=ReadPeakCore(p);if(peak>=0)SetSample(next,MetricId::CpuPeakCore,peak,now);
        if(!p.power.empty()&&CallNtPowerInformation(ProcessorInformation,nullptr,0,p.power.data(),(ULONG)(p.power.size()*sizeof(ProcessorPowerInformation)))==0){double weighted=0,weight=0;for(auto&q:p.power){weighted+=q.CurrentMhz;weight+=1;}if(weight)SetSample(next,MetricId::CpuFrequency,weighted/weight,now);}
        uint64_t selected=0;double gpu=ReadGpu(p,preferredLuid.load(),selected);if(gpu>=0)SetSample(next,MetricId::GpuUtilisation,gpu,now);
        if((tick&1)==0){
            MEMORYSTATUSEX mem{sizeof(mem)};if(GlobalMemoryStatusEx(&mem)){double total=mem.ullTotalPhys/1073741824.0,used=(mem.ullTotalPhys-mem.ullAvailPhys)/1073741824.0;SetSample(next,MetricId::RamPressure,100.0*used/total,now,used,total);}
            PERFORMANCE_INFORMATION perf{sizeof(perf)};if(GetPerformanceInfo(&perf,sizeof(perf))&&perf.CommitLimit){double used=double(perf.CommitTotal)*double(perf.PageSize)/1073741824.0,limit=double(perf.CommitLimit)*double(perf.PageSize)/1073741824.0;SetSample(next,MetricId::CommitPressure,100.0*used/limit,now,used,limit);}
            uint64_t luid=preferredLuid.load();if(!luid)luid=selected;if(p.SelectAdapter(luid)){DXGI_QUERY_VIDEO_MEMORY_INFO info{};if(SUCCEEDED(p.adapter->QueryVideoMemoryInfo(0,DXGI_MEMORY_SEGMENT_GROUP_LOCAL,&info))&&info.Budget){double used=info.CurrentUsage/1073741824.0,budget=info.Budget/1073741824.0;SetSample(next,MetricId::VramPressure,100.0*used/budget,now,used,budget);}}
        }
        if(networkProbeEnabled.load(std::memory_order_acquire) && p.icmp!=INVALID_HANDLE_VALUE && now-p.lastNetworkProbeMs>=1000) {
            p.lastNetworkProbeMs=now;
            const IPAddr target=networkProbeTarget.load(std::memory_order_acquire);
            if(target) {
                char payload[16]="ViewLab network";
                std::array<uint8_t,sizeof(ICMP_ECHO_REPLY)+64> reply{};
                const DWORD replies=IcmpSendEcho(p.icmp,target,payload,(WORD)sizeof(payload),nullptr,reply.data(),(DWORD)reply.size(),250);
                double rtt=0.0; if(replies){auto* echo=reinterpret_cast<ICMP_ECHO_REPLY*>(reply.data());rtt=echo->RoundTripTime;}
                const auto stats=p.networkWindow.Record(replies!=0,rtt);
                if(replies)SetSample(next,MetricId::NetworkPing,stats.pingMs,now);
                SetSample(next,MetricId::NetworkLoss,stats.lossPercent,now);
                if(stats.successfulSamples>=2)SetSample(next,MetricId::NetworkJitter,stats.jitterMs,now);
                SetInstantSample(next,MetricId::NetworkStatus,static_cast<double>(stats.state),now);
            }
        }
        std::array<double,6> pressures{};size_t n=0;auto add=[&](MetricId id,bool memory){auto&m=next.metrics[(size_t)id];if(m.availability==Availability::Available&&now-m.sampledAtMs<=kStaleAfterMs)pressures[n++]=memory?MemoryPressure(m.value,70,95):std::clamp(m.value/100.0,0.0,1.0);};
        add(MetricId::CpuUtilisation,false);add(MetricId::CpuPeakCore,false);add(MetricId::GpuUtilisation,false);add(MetricId::RamPressure,true);add(MetricId::CommitPressure,true);add(MetricId::VramPressure,true);
        if(n){double strongest=*std::max_element(pressures.begin(),pressures.begin()+n);SetSample(next,MetricId::SystemHeadroom,100.0*(1.0-strongest),now);next.headroomCoverage=(uint32_t)n;}
        for(auto&m:next.metrics)if(m.availability==Availability::Available&&now-m.sampledAtMs>kStaleAfterMs)m.availability=Availability::Stale;
        FILETIME k1{},u1{};GetThreadTimes(GetCurrentThread(),&created,&exited,&k1,&u1);next.workerCpuMs=double((FileTimeValue(k1)-FileTimeValue(kernel0))+(FileTimeValue(u1)-FileTimeValue(user0)))/10000.0;next.sampleCount=tick;next.generation++;next.publishedAtMs=now;
        {std::lock_guard<std::mutex> lock(snapshotMutex);published=next;}
        if((tick&3u)==0||checkpointRequested.exchange(false,std::memory_order_acq_rel))if(auto callback=checkpointCallback.load(std::memory_order_acquire))callback();
        std::unique_lock<std::mutex> waitLock(wakeMutex);wakeCondition.wait_for(waitLock,std::chrono::milliseconds(kSamplePeriodMs),[]{return stopping.load()||checkpointRequested.load();});
    }
    if(auto callback=checkpointCallback.load(std::memory_order_acquire))callback();
    p.Shutdown(); running.store(false,std::memory_order_release);
}
}

void Start(){std::lock_guard<std::mutex> lock(lifecycleMutex);if(running.exchange(true))return;stopping.store(false);worker=std::thread(Run);}
void Stop(){std::lock_guard<std::mutex> lock(lifecycleMutex);if(!running.load()&&!worker.joinable())return;stopping.store(true);wakeCondition.notify_all();if(worker.joinable())worker.join();}
bool Running(){return running.load(std::memory_order_acquire);}
void SetPreferredAdapterLuid(uint64_t luid){preferredLuid.store(luid,std::memory_order_release);}
void SetNetworkProbeTarget(uint32_t ipv4NetworkOrder){networkProbeTarget.store(ipv4NetworkOrder,std::memory_order_release);}
void SetNetworkProbeEnabled(bool enabled){networkProbeEnabled.store(enabled,std::memory_order_release);}
void SetCheckpointCallback(void (*callback)()){checkpointCallback.store(callback,std::memory_order_release);}
void RequestCheckpoint(){checkpointRequested.store(true,std::memory_order_release);wakeCondition.notify_all();}
bool TryGetSnapshot(Snapshot& snapshot){if(!snapshotMutex.try_lock())return false;snapshot=published;snapshotMutex.unlock();return snapshot.generation!=0;}
}
