using System;
using XRViewLab.UI;

// Deterministic simulations for the new iRacing cue state machines (items 4, 6, 7). These prove the
// transitions, hysteresis, target-change protection, calibration and severity/direction mapping without
// a live iRacing connection. Live-driving threshold tuning remains to confirm in-car.

int failures = 0;
void Check(bool ok, string label) { Console.WriteLine((ok ? "PASS " : "FAIL ") + label); if (!ok) failures++; }
const double dt = 1.0 / 60.0;

// ---- A. Rear-Closing Pressure Cue --------------------------------------------------------------
{
    // No car behind -> inactive.
    var cue = new RearClosingCue();
    for (int i = 0; i < 30; i++) cue.Update(double.NaN, -1, false, false, dt);
    Check(!cue.Active && cue.Opacity == 0, "rear cue: no car -> inactive");

    // Car behind but holding station (not closing) -> stays inactive.
    cue.Reset();
    for (int i = 0; i < 60; i++) cue.Update(20.0, 7, false, true, dt);
    Check(!cue.Active, "rear cue: steady car behind (no closing) -> inactive");

    // Car rapidly closing 25m -> 10m over ~1s -> activates, glow width grows with proximity.
    cue.Reset();
    double d = 25.0; bool becameActive = false; double widthAtClose = 0, peakIntensity = 0;
    for (int i = 0; i < 90; i++) { d = Math.Max(8.0, d - 0.25); cue.Update(d, 7, false, true, dt); if (cue.Active) { becameActive = true; widthAtClose = cue.GlowWidth; peakIntensity = Math.Max(peakIntensity, cue.Intensity); } }
    Check(becameActive, "rear cue: rapid closing activates");
    Check(peakIntensity > 0.2, "rear cue: closing speed drives intensity");
    Check(widthAtClose > 0.3, "rear cue: closer car -> wider glow");

    // Side overlap -> hands over to spotter (cue clears).
    cue.Update(8.0, 7, true, true, dt);
    Check(!cue.Active, "rear cue: side overlap clears the cue (spotter takes over)");

    // Target identity change must not spike closing speed: nearest car swaps from far to near instantly.
    cue.Reset();
    for (int i = 0; i < 20; i++) cue.Update(30.0, 7, false, true, dt);
    cue.Update(5.0, 9, false, true, dt); // different carId, big distance jump
    Check(!cue.Active, "rear cue: nearest-car change does not produce a false closing spike");
}

// ---- C. Race-Start Light -----------------------------------------------------------------------
{
    var light = new RaceStartLight { GreenHoldSeconds = 0.5 };
    // Waiting -> red.
    light.Update(officialWaiting: true, officialStart: false, sessionNum: 1, isReplay: false, isGarage: false, connected: true, dt);
    Check(light.IsRed, "race start: waiting -> red");

    // Official start -> green, latched.
    light.Update(false, true, 1, false, false, true, dt);
    Check(light.IsGreen, "race start: official start -> green");

    // Green holds then fades to inactive.
    for (int i = 0; i < 120; i++) light.Update(false, false, 1, false, false, true, dt);
    Check(light.State == RaceStartState.Inactive && light.Opacity == 0, "race start: green fades to inactive after hold");

    // Green does not replay in the same session even if 'start' re-asserts.
    light.Update(false, true, 1, false, false, true, dt);
    Check(!light.IsGreen, "race start: green does not replay within the same session");

    // Replay / garage / disconnect never trigger green.
    var l2 = new RaceStartLight();
    l2.Update(false, true, 5, isReplay: true, isGarage: false, connected: true, dt);
    Check(!l2.IsGreen, "race start: replay does not trigger green");
    l2.Update(false, true, 5, false, true, true, dt);
    Check(!l2.IsGreen, "race start: garage does not trigger green");
    l2.Update(false, true, 5, false, false, connected: false, dt);
    Check(!l2.IsGreen, "race start: disconnect does not trigger green");

    // New session resets the latch so the next grid can arm again.
    var l3 = new RaceStartLight { GreenHoldSeconds = 0.1 };
    l3.Update(false, true, 1, false, false, true, dt);
    for (int i = 0; i < 30; i++) l3.Update(false, false, 1, false, false, true, dt);
    l3.Update(true, false, 2, false, false, true, dt); // session 2 waiting
    Check(l3.IsRed, "race start: new session re-arms (red again)");
}

// ---- D. Grip-O-Bar -----------------------------------------------------------------------------
{
    // Calibration: feed clean steady samples with a known yaw gain.
    var cal = new GripCarCalibration { CarId = "gt3" };
    for (int i = 0; i < 200; i++) GripOMeter.AccumulateCalibration(cal, steering: 0.1, speed: 40.0, yawRate: 0.1 * 40.0 * 0.02, clean: true);
    Check(cal.IsCalibrated, "grip: accumulates a usable per-car calibration from clean samples");
    Check(Math.Abs(cal.YawGain - 0.02) < 1e-3, "grip: calibrated yaw gain matches the injected model");
    Check(cal.Schema == GripCarCalibration.CurrentSchema, "grip: calibration carries the versioned schema");

    var grip = new GripOMeter();

    // Neutral: actual yaw matches expected -> no direction.
    for (int i = 0; i < 30; i++) grip.Update(cal, 0.1, 40.0, 0.02 * 0.1 * 40.0, 0.0, 40.0, true, dt);
    Check(grip.Direction == 0 && grip.Severity == 0, "grip: matched yaw -> neutral");

    // Oversteer: rotating far MORE than commanded, car sliding right -> right side, rising severity.
    var over = new GripOMeter();
    for (int i = 0; i < 40; i++) over.Update(cal, 0.1, 40.0, yawRate: 0.02 * 0.1 * 40.0 + 0.4, lateralVel: 1.0, forwardVel: 40.0, true, dt);
    Check(over.Direction == 1, "grip: oversteer sliding right -> right direction");
    Check(over.Severity > 0.3, "grip: larger yaw deviation -> higher severity");
    Check(over.Dominance == GripDominance.Oversteer, "grip: classifies oversteer-dominant");

    // Understeer: rotating far LESS than commanded.
    var under = new GripOMeter();
    for (int i = 0; i < 40; i++) under.Update(cal, 0.3, 40.0, yawRate: 0.02 * 0.3 * 40.0 - 0.4, lateralVel: 0.0, forwardVel: 40.0, true, dt);
    Check(under.Dominance == GripDominance.Understeer, "grip: classifies understeer-dominant");

    // Sideslip: lateral translation dominates.
    var side = new GripOMeter();
    for (int i = 0; i < 40; i++) side.Update(cal, 0.02, 40.0, yawRate: 0.02 * 0.02 * 40.0, lateralVel: 6.0, forwardVel: 40.0, true, dt);
    Check(side.Dominance == GripDominance.Sideslip, "grip: classifies sideslip-dominant");

    // Low speed suppresses the cue.
    var slow = new GripOMeter();
    for (int i = 0; i < 40; i++) slow.Update(cal, 0.3, 3.0, yawRate: 5.0, lateralVel: 3.0, forwardVel: 3.0, true, dt);
    Check(slow.Direction == 0, "grip: low speed suppresses the cue");

    // Missing calibration -> no output even with large slip.
    var uncal = new GripCarCalibration { CarId = "new" };
    var g2 = new GripOMeter();
    for (int i = 0; i < 40; i++) g2.Update(uncal, 0.3, 40.0, 2.0, 4.0, 40.0, true, dt);
    Check(g2.Direction == 0 && g2.Severity == 0, "grip: missing calibration -> inactive");

    // Severity band mapping shared by preview and runtime.
    Check(GripOMeter.SeverityBand(0.1) == 0 && GripOMeter.SeverityBand(0.5) == 1 && GripOMeter.SeverityBand(0.9) == 2,
        "grip: severity->band mapping is yellow/orange/red");
}

Console.WriteLine(failures == 0 ? "All iRacing cue fixtures passed." : $"{failures} fixture(s) failed.");
return failures == 0 ? 0 : 1;
