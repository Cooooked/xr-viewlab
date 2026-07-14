#include "../RenderPolicy.h"
#include "../ClockWidget.h"
#include <cmath>
#include <cstdlib>
#include <iostream>

static void Check(bool value, const char* message) {
    if (!value) { std::cerr << "FAIL: " << message << '\n'; std::exit(1); }
    std::cout << "PASS: " << message << '\n';
}

int main() {
    using viewlab::clock_widget::Format;
    Check(Format(7, 5, 0).local == std::array<char, 6>{'0','7',':','0','5','\0'}, "clock uses fixed 24-hour local time");
    Check(Format(23, 59, 3723000).session == std::array<char, 9>{'0','1',':','0','2',':','0','3','\0'}, "session timer formats monotonic elapsed time");
    Check(Format(0, 0, 500000000).session == std::array<char, 9>{'9','9',':','5','9',':','5','9','\0'}, "session timer has a stable display cap");
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
    return 0;
}
