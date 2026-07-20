#include "../RenderPolicy.h"
#include "../RacingCueGeometry.h"
#include "../ProjectionTopology.h"
#include "../ClockWidget.h"
#include "../NetworkProbe.h"
#include "../StickyNote.h"
#include "../ViewLabBridge/BridgeCore.h"
#include <cmath>
#include <cstdlib>
#include <iostream>

static void Check(bool value, const char* message) {
    if (!value) { std::cerr << "FAIL: " << message << '\n'; std::exit(1); }
    std::cout << "PASS: " << message << '\n';
}

int main() {
    struct TopologyView { unsigned viewIndex, arraySlice; int x, width; };
    using Topology = viewlab::projection::FrameTopology<unsigned, TopologyView>;
    const auto verifyTopology = [](const Topology& topology, unsigned firstSwapchain,
        size_t firstTargets, unsigned secondSwapchain, size_t secondTargets, const char* message) {
        const auto all = topology.AllViews();
        const auto first = topology.TargetsFor(firstSwapchain);
        const auto second = topology.TargetsFor(secondSwapchain);
        Check(all.size() == 2 && all[0].viewIndex == 0 && all[1].viewIndex == 1 &&
            first.size() == firstTargets && second.size() == secondTargets, message);
    };
    verifyTopology({{{10,{0,0,0,100}},{10,{1,1,0,100}}}},10,2,20,0,
        "array-slice stereo retains one complete projection context");
    verifyTopology({{{10,{0,0,0,100}},{20,{1,0,0,100}}}},10,1,20,1,
        "split swapchains retain both views in shared projection context");
    verifyTopology({{{10,{0,0,0,100}},{10,{1,0,100,100}}}},10,2,20,0,
        "atlas rectangles remain independent targets in one projection context");
    verifyTopology({{{10,{0,2,20,80}},{20,{1,5,40,60}}}},10,1,20,1,
        "mixed swapchain slice and rectangle packing preserves submitted views");
    const Topology overlapping{{{10,{0,0,0,100}},{10,{1,0,0,100}}}};
    const auto overlapTargets = overlapping.TargetsFor(10);
    Check(overlapTargets.size() == 2 && overlapTargets[0].viewIndex == 0 && overlapTargets[1].viewIndex == 1,
        "overlapping subimages preserve OpenXR view-array order");
    Check(overlapping.TargetsFor(20).empty() && overlapping.AllViews().size() == 2,
        "release order and unrelated swapchains cannot truncate projection context");
    using viewlab::clock_widget::Format;
    Check(Format(7, 5, 0).local == std::array<char, 9>{'0','7',':','0','5','\0','\0','\0','\0'}, "clock uses fixed 24-hour local time");
    Check(Format(19, 5, 0, false).local == std::array<char, 9>{'0','7',':','0','5',' ','P','M','\0'}, "clock supports an explicit 12-hour display");
    Check(Format(23, 59, 3723000).session == std::array<char, 9>{'0','1',':','0','2',':','0','3','\0'}, "session timer formats monotonic elapsed time");
    Check(Format(0, 0, 500000000).session == std::array<char, 9>{'9','9',':','5','9',':','5','9','\0'}, "session timer has a stable display cap");
    const auto note=viewlab::sticky_note::Wrap(L"bring fuel and check the very long setup note",12);
    Check(note.count>=2&&note.lines[0]=="BRING FUEL", "sticky note wraps words and normalizes case");
    const auto clipped=viewlab::sticky_note::Wrap(std::wstring(140,L'X'),10);
    Check(clipped.count==4&&clipped.lines[3].substr(clipped.lines[3].size()-3)=="...", "sticky note is bounded to four lines");
    viewlab::network::Window network;
    auto net = network.Record(true, 20); net = network.Record(true, 30);
    Check(net.pingMs == 30 && net.lossPercent == 0 && net.jitterMs == 10, "network probe reports RTT, loss and jitter truthfully");
    net = network.Record(false, 0);
    Check(net.lossPercent > 0 && net.state != viewlab::network::State::Disconnected, "one missed probe is loss, not a disconnect");
    network.Record(false, 0); net = network.Record(false, 0);
    Check(net.state == viewlab::network::State::Disconnected, "three consecutive misses produce a disconnect warning");
    using namespace viewlab::policy;
    Check(EffectiveGraphChannels(0, GraphFps) == GraphBudgetDeviation, "deviation mode cannot become empty");
    Check(EffectiveGraphChannels(1, GraphBudgetDeviation) == GraphFrameInterval, "milliseconds mode gains a valid channel");
    Check(EffectiveGraphChannels(2, GraphBudgetDeviation) == GraphFps, "FPS mode gains its FPS channel");
    Check(EffectiveGraphChannels(3, GraphFps) == GraphFrameInterval, "budget-percent mode gains frame interval");

    TraceVisibilityState trace{};
    Check(UpdateTraceVisibility(trace, 1, false, 1000, 1500) == 1.f, "always mode is immediately visible");
    Check(UpdateTraceVisibility(trace, 2, false, 1001, 1500) == 0.f, "healthy alarm-only mode hides immediately");
    Check(UpdateTraceVisibility(trace, 2, true, 1100, 1500) == 1.f, "alarm shows trace");
    Check(UpdateTraceVisibility(trace, 2, false, 2000, 1500) == 1.f, "recovery hold retains trace");
    UpdateTraceVisibility(trace, 2, false, 2700, 1500);
    Check(UpdateTraceVisibility(trace, 2, false, 3200, 1500) == 0.f, "trace fades and remains hidden");
    Check(UpdateTraceVisibility(trace, 0, true, 3300, 1500) == 0.f, "off overrides alarm");

    SustainedAlarmState alarm{};
    UpdateSustainedAlarm(alarm, 2, 2, 0, 750, 750, 1500);
    Check(!alarm.visible, "initial critical input still requires sustained entry");
    alarm = SustainedAlarmState{};
    UpdateSustainedAlarm(alarm, 0, 0, 0, 750, 750, 1500);
    UpdateSustainedAlarm(alarm, 2, 2, 100, 750, 750, 1500);
    UpdateSustainedAlarm(alarm, 2, 2, 700, 750, 750, 1500);
    Check(!alarm.visible, "brief critical input does not raise an alarm");
    UpdateSustainedAlarm(alarm, 2, 2, 850, 750, 750, 1500);
    Check(alarm.visible, "sustained critical input raises an alarm");
    UpdateSustainedAlarm(alarm, 1, 1, 900, 750, 750, 1500);
    UpdateSustainedAlarm(alarm, 0, 0, 1500, 750, 750, 1500);
    UpdateSustainedAlarm(alarm, 1, 1, 1650, 750, 750, 1500);
    Check(alarm.visible && alarm.stableSeverity < 2, "recovery can de-escalate while lower states fluctuate");
    UpdateSustainedAlarm(alarm, 0, 0, 2399, 750, 750, 1500);
    Check(alarm.visible, "post-recovery hold remains bounded from the first healthy input");
    UpdateSustainedAlarm(alarm, 0, 0, 2400, 750, 750, 1500);
    Check(!alarm.visible, "post-recovery hold expires instead of extending itself forever");

    const auto small = SingleRowHudLayout(12, 10.f, 20.f, .018f);
    const auto large = SingleRowHudLayout(12, 20.f, 40.f, .018f);
    Check(small.widgetsPerRow == 12 && small.rowCount == 1, "twelve widgets remain one row");
    Check(std::fabs(large.gap / small.gap - 2.f) < .0001f, "HUD spacing scales with the complete widget");
    Check(std::fabs(large.width / small.width - 2.f) < .0001f, "HUD row bounds scale proportionally");

    auto enter = EvaluateNotificationAnimation(1000, 1000, 0);
    auto mid = EvaluateNotificationAnimation(1125, 1000, 0);
    auto end = EvaluateNotificationAnimation(1250, 1000, 0);
    Check(enter.alpha == 0.f && mid.alpha > 0.49f && mid.alpha < 0.51f && end.alpha == 1.f,
        "notification entry evaluates continuously from timestamps");
    auto leave = EvaluateNotificationAnimation(2200, 1000, 2000);
    Check(leave.alpha > 0.49f && leave.alpha < 0.51f, "notification exit is independent of broker polling cadence");

    Check(!LatchTopmostDemand(false, false), "single projection retains direct backend");
    Check(LatchTopmostDemand(false, true), "distinct application layer demands Topmost");
    Check(LatchTopmostDemand(true, false), "Topmost demand remains latched for the session");
    using namespace viewlab::bridge;
    RuntimeCapabilities plain{GraphicsApi::D3D11,true,true,false,true,true,false};
    Check(SelectOverlayBackend(plain)==OverlayBackend::DirectEyeTexture,
        "plain projection keeps direct eye-texture presentation");
    auto featurePlan=SelectFeaturePresentationPlan(plain);
    Check(featurePlan.drawDirectVisor==featurePlan.drawDirectCommonFeatures,
        "direct visor and direct common-feature flags are always paired");
    Check(featurePlan.drawDirectVisor&&featurePlan.drawDirectCommonFeatures&&
        featurePlan.orderedBackend==OverlayBackend::FeatureDisabled,
        "plain frame preserves the proven direct ViewLab path");
    plain.hasDistinctCompositionLayer=true;
    plain.supportsAdditionalProjectionLayers=true;
    Check(SelectOverlayBackend(plain)==OverlayBackend::SeparateProjection,
        "a distinct compositor layer preserves the proven stereo projection backend");
    featurePlan=SelectFeaturePresentationPlan(plain);
    Check(featurePlan.drawDirectVisor==featurePlan.drawDirectCommonFeatures,
        "direct visor and direct common-feature flags stay paired during allocation transition");
    Check(featurePlan.drawDirectVisor&&featurePlan.drawDirectCommonFeatures&&
        featurePlan.orderedBackend==OverlayBackend::SeparateProjection,
        "allocation transition keeps the working renderer until ordered presentation is ready");
    plain.orderedPresentationReady=true;
    featurePlan=SelectFeaturePresentationPlan(plain);
    Check(featurePlan.drawDirectVisor==featurePlan.drawDirectCommonFeatures,
        "direct visor and direct common-feature flags stay paired when ordered is ready");
    Check(!featurePlan.drawDirectVisor&&!featurePlan.drawDirectCommonFeatures&&
        featurePlan.orderedBackend==OverlayBackend::SeparateProjection,
        "ready ordered presentation disables the obsolete duplicate renderer for every feature");
    RuntimeCapabilities menu{GraphicsApi::D3D11,false,true,true,false,false,false};
    featurePlan=SelectFeaturePresentationPlan(menu);
    Check(featurePlan.drawDirectVisor==featurePlan.drawDirectCommonFeatures,
        "direct visor and direct common-feature flags stay paired for composition-only frames");
    Check(!featurePlan.drawDirectVisor&&!featurePlan.drawDirectCommonFeatures&&
        featurePlan.orderedBackend==OverlayBackend::FeatureDisabled,
        "composition-only frame does not claim an unproven visible carrier");
    menu.hasPrimaryProjection=true;
    menu.canWriteEyeTexture=true;
    menu.supportsAdditionalProjectionLayers=true;
    featurePlan=SelectFeaturePresentationPlan(menu);
    Check(featurePlan.drawDirectVisor==featurePlan.drawDirectCommonFeatures,
        "direct visor and direct common-feature flags stay paired when projection returns");
    Check(featurePlan.drawDirectVisor&&featurePlan.drawDirectCommonFeatures&&
        featurePlan.orderedBackend==OverlayBackend::SeparateProjection,
        "the next observed projection restores direct and ordered common-feature presentation");
    menu.orderedPresentationReady=true;
    featurePlan=SelectFeaturePresentationPlan(menu);
    Check(featurePlan.drawDirectVisor==featurePlan.drawDirectCommonFeatures,
        "direct visor and direct common-feature flags stay paired for ready ordered carrier");
    Check(!featurePlan.drawDirectVisor&&!featurePlan.drawDirectCommonFeatures&&
        featurePlan.orderedBackend==OverlayBackend::SeparateProjection,
        "a ready ordered carrier is the sole normal-feature presentation path");
    bool orderedDemand=false;
    orderedDemand=LatchTopmostDemand(orderedDemand,false); // projection-only gameplay
    orderedDemand=LatchTopmostDemand(orderedDemand,true);  // composition-only menu
    orderedDemand=LatchTopmostDemand(orderedDemand,false); // projection returns
    Check(orderedDemand,"projection-menu-projection topology shift keeps ordered presentation latched");
    plain.supportsAdditionalProjectionLayers=false;
    plain.supportsQuadLayers=false;
    Check(SelectOverlayBackend(plain)==OverlayBackend::DirectEyeTexture,
        "missing ordered capability falls back without disabling ViewLab");
    const PixelRect mapped=MapTextureBounds({.25f,.1f,.75f,.9f},2000,1000);
    Check(mapped.x==500&&mapped.y==100&&mapped.width==1000&&mapped.height==800,
        "legacy texture bounds map to current submitted texture dimensions");

    // Behavioral audit (item 19): prove each iRacing cue control actually changes computed geometry/alpha,
    // through the same single-source header the native renderer uses.
    using namespace viewlab::racing;
    const float vw = 2000.f;
    Check(SpotterWidthPx(0.20, vw) > SpotterWidthPx(0.10, vw), "spotter: increasing width widens the glow");
    Check(SpotterWidthPx(1.0, vw) <= vw * 0.35f + 0.01f, "spotter: width is clamped to a safe maximum");
    Check(SpotterBase(0.9, 1.0) > SpotterBase(0.4, 1.0), "spotter: increasing opacity raises base intensity");
    Check(SpotterBase(0.6, 1.5) > SpotterBase(0.6, 1.0), "spotter: increasing strength raises base intensity");
    const float inwardInner = 0.5f / kSpotterBands; // brightest band for the left edge (near outer edge)
    Check(SpotterBandAlpha(1.f, inwardInner, 4.0, true) < SpotterBandAlpha(1.f, inwardInner, 1.0, true),
        "spotter: higher fade sharpens the falloff (lower alpha away from the peak)");
    Check(std::abs(SpotterBandAlpha(1.f, 0.3f, 2.0, true) - SpotterBandAlpha(1.f, 0.7f, 2.0, false)) < 1e-6f,
        "spotter: left and right edges are mirror images");
    Check(RearGlowHalfWidth(0.9, vw) > RearGlowHalfWidth(0.2, vw), "rear-closing: closer car widens the top-centre glow");
    Check(GripBarWidth(0.9, vw) > GripBarWidth(0.2, vw), "grip: higher severity widens the bar");
    Check(GripSeverityBand(0.1) == 0 && GripSeverityBand(0.5) == 1 && GripSeverityBand(0.9) == 2,
        "grip: severity maps to yellow/orange/red bands");
    Check(FlagBorderThickness(0.10, 1000.f) > FlagBorderThickness(0.02, 1000.f), "flag: increasing width thickens the border");
    Check(FlagBorderThickness(1.0, 1000.f) <= 1000.f * 0.12f + 0.01f, "flag: border thickness is clamped");
    return 0;
}
