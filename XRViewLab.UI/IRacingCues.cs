using System;

namespace XRViewLab.UI;

// Shared, deterministic state machines and mapping functions for the new iRacing cues. Kept free of
// drawing, WPF and wall-clock time (callers pass dt) so the same logic drives the desktop preview and
// the native runtime and can be exercised by deterministic fixtures. Live-driving thresholds still need
// in-car confirmation, but the logic, transitions, hysteresis and mappings are validated here.

// ---------------------------------------------------------------------------------------------------
// A. Rear-Closing Pressure Cue (item 4). A narrow top-centre glow warns of the nearest car behind when
// it is rapidly gaining. Distance drives glow width; closing speed drives intensity. The cue clears once
// side overlap begins so the existing left/right spotter takes over; it never infers an approach side.
// ---------------------------------------------------------------------------------------------------
internal sealed class RearClosingCue
{
    // Tunables (defaults chosen conservatively; live driving may adjust).
    public double MaxDistanceMeters = 40.0;   // beyond this, no cue
    public double MinDistanceMeters = 3.0;    // at/below this, full width
    public double ActivateClosingMps = 1.5;   // closing speed to switch on (hysteresis high)
    public double DeactivateClosingMps = 0.6;  // closing speed to switch off (hysteresis low)
    public double CriticalClosingMps = 6.0;    // closing speed mapped to full intensity
    public double Smoothing = 0.35;            // EMA factor for closing speed [0..1]
    public double FadePerSecond = 3.0;         // opacity fade rate when deactivating
    public double MinPersistenceSeconds = 0.4; // minimum on-time once activated

    private double _prevDistance = double.NaN;
    private int _prevCarId = -1;
    private double _smoothedClosing;
    private bool _active;
    private double _onTime;
    private double _opacity;   // 0..1 output envelope

    public bool Active => _active;
    public double Opacity => _opacity;                       // fade envelope
    public double GlowWidth { get; private set; }            // 0..1 (distance-driven)
    public double Intensity { get; private set; }            // 0..1 (closing-speed-driven)

    public void Reset()
    {
        _prevDistance = double.NaN; _prevCarId = -1; _smoothedClosing = 0;
        _active = false; _onTime = 0; _opacity = 0; GlowWidth = 0; Intensity = 0;
    }

    // rearDistance: metres to the nearest valid car behind (NaN/<=0 or !valid = none).
    // sideOverlap: the SDK reports the car has drawn alongside (spotter takes over).
    public void Update(double rearDistance, int carId, bool sideOverlap, bool valid, double dt)
    {
        dt = Math.Clamp(dt, 0.0, 0.5);
        bool hasCar = valid && !double.IsNaN(rearDistance) && rearDistance > 0 && rearDistance <= MaxDistanceMeters;

        // A target identity change (a different car becomes nearest) must not produce a false closing
        // spike from the discontinuous distance jump; reset the derivative baseline instead.
        if (!hasCar || sideOverlap || carId != _prevCarId)
        {
            _smoothedClosing = 0;
            _prevDistance = hasCar ? rearDistance : double.NaN;
            _prevCarId = hasCar ? carId : -1;
        }
        else if (dt > 0 && !double.IsNaN(_prevDistance))
        {
            double closing = (_prevDistance - rearDistance) / dt; // positive = getting closer
            if (Math.Abs(closing) < 200.0) // reject absurd discontinuities
                _smoothedClosing += (closing - _smoothedClosing) * Math.Clamp(Smoothing, 0.05, 1.0);
            _prevDistance = rearDistance;
        }

        bool want = hasCar && !sideOverlap;
        if (want)
        {
            if (!_active && _smoothedClosing >= ActivateClosingMps) { _active = true; _onTime = 0; }
            else if (_active && _smoothedClosing <= DeactivateClosingMps && _onTime >= MinPersistenceSeconds) _active = false;
        }
        else _active = false;

        if (_active)
        {
            _onTime += dt;
            _opacity = Math.Min(1.0, _opacity + FadePerSecond * dt);
            double t = Math.Clamp((MaxDistanceMeters - rearDistance) / Math.Max(0.001, MaxDistanceMeters - MinDistanceMeters), 0.0, 1.0);
            GlowWidth = t;
            Intensity = Math.Clamp(_smoothedClosing / Math.Max(0.001, CriticalClosingMps), 0.0, 1.0);
        }
        else
        {
            _opacity = Math.Max(0.0, _opacity - FadePerSecond * dt);
            if (_opacity <= 0) { GlowWidth = 0; Intensity = 0; }
        }
    }
}

// ---------------------------------------------------------------------------------------------------
// C. Race-Start Light (item 6). Red while officially waiting, one green when the official start occurs,
// green fades after a configurable duration, resets for the next grid/session. Never triggers from
// movement, replay, reconnect, garage or a telemetry tick reset.
// ---------------------------------------------------------------------------------------------------
internal enum RaceStartState { Inactive, WaitingRed, GreenActive, GreenFading }

internal sealed class RaceStartLight
{
    public double GreenHoldSeconds = 3.0;
    public double FadePerSecond = 1.5;

    private RaceStartState _state = RaceStartState.Inactive;
    private int _sessionNum = -1;
    private double _greenTime;
    private double _opacity;
    private bool _greenLatched;   // green already shown this session — do not replay

    public RaceStartState State => _state;
    public double Opacity => _opacity;
    public bool IsRed => _state == RaceStartState.WaitingRed;
    public bool IsGreen => _state is RaceStartState.GreenActive or RaceStartState.GreenFading;

    // officialWaiting: pre-start (grid/pace) per session flags. officialStart: the authoritative green.
    // Guards: replay/garage/disconnect force Inactive; a session-number change resets the latch.
    public void Update(bool officialWaiting, bool officialStart, int sessionNum, bool isReplay, bool isGarage, bool connected, double dt)
    {
        dt = Math.Clamp(dt, 0.0, 0.5);

        if (sessionNum != _sessionNum) { _sessionNum = sessionNum; _greenLatched = false; _state = RaceStartState.Inactive; _opacity = 0; }
        if (!connected || isReplay || isGarage)
        {
            _state = RaceStartState.Inactive; _opacity = 0; return; // do not trigger from these transitions
        }

        switch (_state)
        {
            case RaceStartState.Inactive:
                if (officialWaiting && !_greenLatched) { _state = RaceStartState.WaitingRed; _opacity = 1; }
                else if (officialStart && !_greenLatched) { _state = RaceStartState.GreenActive; _greenLatched = true; _greenTime = 0; _opacity = 1; }
                break;
            case RaceStartState.WaitingRed:
                _opacity = 1;
                if (officialStart) { _state = RaceStartState.GreenActive; _greenLatched = true; _greenTime = 0; }
                break;
            case RaceStartState.GreenActive:
                _opacity = 1; _greenTime += dt;
                if (_greenTime >= GreenHoldSeconds) _state = RaceStartState.GreenFading;
                break;
            case RaceStartState.GreenFading:
                _opacity = Math.Max(0.0, _opacity - FadePerSecond * dt);
                if (_opacity <= 0) _state = RaceStartState.Inactive;
                break;
        }
    }

    public void Reset() { _state = RaceStartState.Inactive; _sessionNum = -1; _opacity = 0; _greenLatched = false; }
}

// ---------------------------------------------------------------------------------------------------
// D. Grip-O-Bar (item 7). A lower-peripheral whole-car directional grip-loss indicator. Following the
// MAIRA Grip-O-Meter idea, it compares the actual yaw response with the expected yaw response for the
// current steering and speed (a calibrated single-track/bicycle estimate) and combines that with lateral
// slip. It reports WHOLE-CAR slip direction and dominance only — never an individual tyre.
// ---------------------------------------------------------------------------------------------------
internal enum GripDominance { Neutral, Understeer, Oversteer, Sideslip }

// Versioned per-car calibration. YawGain approximates expected yaw-rate per (steering * speed).
internal sealed class GripCarCalibration
{
    public const int CurrentSchema = 1;
    public int Schema = CurrentSchema;
    public string CarId = "";
    public double YawGain = 0.0;        // rad/s of yaw per (rad steering * m/s) — 0 = uncalibrated
    public int Samples = 0;
    public bool IsCalibrated => Samples >= 40 && YawGain > 1e-4;
}

internal sealed class GripOMeter
{
    public double DeadZone = 0.04;         // deviation below this is neutral
    public double SevereThreshold = 0.35;  // combined magnitude mapped to full severity
    public double Smoothing = 0.3;
    public double MinSpeedMps = 8.0;       // below this, suppressed (low-speed noise)
    public double Hysteresis = 0.02;

    private double _smoothed;
    private int _dir; // -1 left, +1 right, 0 none
    public double Severity { get; private set; } // 0..1
    public int Direction => _dir;                 // -1 left, +1 right
    public GripDominance Dominance { get; private set; } = GripDominance.Neutral;

    public void Reset() { _smoothed = 0; _dir = 0; Severity = 0; Dominance = GripDominance.Neutral; }

    // Bounded per-car calibration: accumulate the yaw/(steer*speed) ratio from clean, higher-speed,
    // steady samples only. Rejects pit/low-speed/invalid/spin/reset samples (caller pre-filters `clean`).
    public static void AccumulateCalibration(GripCarCalibration cal, double steering, double speed, double yawRate, bool clean)
    {
        if (!clean || speed < 12.0 || Math.Abs(steering) < 0.03) return;
        double denom = steering * speed;
        if (Math.Abs(denom) < 1e-3) return;
        double ratio = yawRate / denom;
        if (!double.IsFinite(ratio) || ratio <= 0 || ratio > 5.0) return; // reject spins/garbage
        if (cal.Samples >= 5000) return; // bounded collection
        cal.YawGain = (cal.YawGain * cal.Samples + ratio) / (cal.Samples + 1);
        cal.Samples++;
    }

    // steering: rad (sign = intended direction). speed: m/s. yawRate: rad/s (actual).
    // lateralVel/forwardVel: m/s in car frame. valid: caller pre-filtered (not pit/spin/reset/stale).
    public void Update(GripCarCalibration cal, double steering, double speed, double yawRate,
        double lateralVel, double forwardVel, bool valid, double dt)
    {
        dt = Math.Clamp(dt, 0.0, 0.5);
        if (!valid || speed < MinSpeedMps || !cal.IsCalibrated)
        {
            _smoothed += (0 - _smoothed) * Math.Clamp(Smoothing, 0.05, 1.0);
            ApplyEnvelope();
            return;
        }

        double expectedYaw = cal.YawGain * steering * speed;   // what the car "should" be rotating
        double yawDeviation = yawRate - expectedYaw;            // + = rotating more than expected (oversteer-ish)
        double lateralSlip = forwardVel > 1.0 ? lateralVel / forwardVel : 0.0;

        // Combined whole-car slip signal. Sign convention: positive = car sliding to the right.
        double combined = 0.6 * yawDeviation + 0.4 * lateralSlip;
        _smoothed += (combined - _smoothed) * Math.Clamp(Smoothing, 0.05, 1.0);

        // Dominance: understeer when the car rotates far LESS than commanded, oversteer when far MORE,
        // sideslip when lateral translation dominates the yaw error.
        double under = expectedYaw - yawRate; // + = under-rotating relative to command
        if (Math.Abs(lateralSlip) > Math.Abs(yawDeviation) * 1.5 && Math.Abs(lateralSlip) > DeadZone)
            Dominance = GripDominance.Sideslip;
        else if (Math.Sign(steering) != 0 && under * Math.Sign(steering) > DeadZone)
            Dominance = GripDominance.Understeer;
        else if (Math.Abs(yawDeviation) > DeadZone)
            Dominance = GripDominance.Oversteer;
        else Dominance = GripDominance.Neutral;

        ApplyEnvelope();
    }

    private void ApplyEnvelope()
    {
        double mag = Math.Abs(_smoothed);
        double onThresh = DeadZone + Hysteresis, offThresh = Math.Max(0, DeadZone - Hysteresis);
        if (_dir == 0)
        {
            if (mag >= onThresh) _dir = _smoothed >= 0 ? 1 : -1;
        }
        else
        {
            if (mag <= offThresh) { _dir = 0; Dominance = GripDominance.Neutral; }
            else if (Math.Sign(_smoothed) != 0 && Math.Sign(_smoothed) != _dir && mag >= onThresh) _dir = _smoothed >= 0 ? 1 : -1;
        }
        Severity = _dir == 0 ? 0 : Math.Clamp((mag - DeadZone) / Math.Max(0.001, SevereThreshold - DeadZone), 0.0, 1.0);
    }

    // Shared severity -> colour band used by BOTH preview and runtime. 0=yellow,1=orange,2=red.
    public static int SeverityBand(double severity) => severity < 0.34 ? 0 : severity < 0.67 ? 1 : 2;
}

// Pure, testable mapping from iRacing SessionFlags to the latched race-start phase (item 5). Extracted
// from the provider so the standing/rolling/join-in-progress guards are deterministically verifiable.
// Returns 0 inactive, 1 waiting/red, 2 started/green. `sawWaiting` and `latched` are per-session state
// the caller threads through samples and resets on session change.
internal static class RaceStartFlags
{
    public const uint StartReady = 0x20000000u, StartSet = 0x40000000u, StartGo = 0x80000000u, Green = 0x00000004u;

    public static int Phase(uint rawFlags, uint prevRawFlags, ref bool sawWaiting, ref bool latched)
    {
        bool waiting = (rawFlags & (StartReady | StartSet)) != 0 && (rawFlags & StartGo) == 0;
        if (waiting) sawWaiting = true;
        bool goRising = (rawFlags & StartGo) != 0 && (prevRawFlags & StartGo) == 0;
        bool greenRising = (rawFlags & Green) != 0 && (prevRawFlags & Green) == 0;
        // Authoritative start = rising edge of startGo (standing) or of green once a waiting phase was
        // observed (rolling). Joining a race already green has no edge and no waiting, so it never fires.
        if (!latched && (goRising || (greenRising && sawWaiting))) latched = true;
        return latched ? 2 : waiting ? 1 : 0;
    }
}
