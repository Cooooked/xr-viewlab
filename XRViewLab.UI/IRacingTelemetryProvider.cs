using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;

namespace XRViewLab.UI;

// Dependency-free reader for the published iRacing SDK shared-memory contract. Raw simulator
// values stop here; every consumer receives only generic ViewLab events.
internal sealed class IRacingTelemetryProvider : IViewLabEventProvider
{
    internal const string DefaultMapName = "Local\\IRSDKMemMapFileName";
    private const int HeaderSize = 112;
    private const int VarHeaderSize = 144;
    private const int MaxVariables = 4096;
    private const int MaxBuffers = 4;
    private const int StaleAfterMs = 750;
    private readonly string _mapName;
    private readonly object _gate = new();
    private Thread? _thread;
    private CancellationTokenSource? _stop;
    private MemoryMappedFile? _map;
    private MemoryMappedViewAccessor? _view;
    private readonly Dictionary<string, Variable> _variables = new(StringComparer.OrdinalIgnoreCase);
    private int _bufferLength;
    private int _lastTick = int.MinValue;
    private long _lastTickAt;
    private long _rateStarted;
    private int _freshSamples;
    private SpotterState _spotter = (SpotterState)(-1);
    private RacingFlagState _flag = (RacingFlagState)(-1);
    private int _lastLap = -1;
    private double _personalBest = double.NaN;
    private string? _sessionId;
    private bool _fuelWarningFired;
    private double _fuelWarningThresholdPct = 0.10;
    private uint _prevRawFlags;
    private bool _sawRaceWaiting;
    private bool _raceStartLatched;
    private int _raceStartPhase = -1;

    // Settable by the broker from its own settings poll; not part of the SDK layout. Clamped to a
    // sane range so a malformed ini value can't disable the warning (0) or fire it constantly (1).
    public double FuelWarningThresholdPct
    {
        get => Volatile.Read(ref _fuelWarningThresholdPct);
        set => Volatile.Write(ref _fuelWarningThresholdPct, Math.Clamp(value, 0.01, 0.5));
    }

    private readonly record struct Variable(int Type, int Offset, int Count);

    public IRacingTelemetryProvider() : this(DefaultMapName) { }
    internal IRacingTelemetryProvider(string mapName) => _mapName = mapName;

    public string Name => "iRacing SDK";
    public bool IsConnected { get; private set; }
    public string Status { get; private set; } = "Disconnected";
    public string Diagnostics { get; private set; } = "No telemetry received.";
    public event EventHandler<ViewLabEvent>? EventPublished;
    public event Action? DiagnosticsChanged;

    public void Start()
    {
        lock (_gate)
        {
            if (_thread is { IsAlive: true }) return;
            _stop?.Dispose();
            _stop = new CancellationTokenSource();
            CancellationToken token = _stop.Token;
            _thread = new Thread(() => Run(token)) { IsBackground = true, Name = "ViewLab iRacing telemetry" };
            _thread.Start();
        }
    }

    public void Stop()
    {
        Thread? worker;
        lock (_gate) { _stop?.Cancel(); worker = _thread; }
        if (worker != null && worker != Thread.CurrentThread && !worker.Join(1500))
            SetStatus("Stopping", "Telemetry worker did not stop within 1.5 seconds; it remains cancellation-bound.");
        lock (_gate) { if (_thread == worker && (worker == null || !worker.IsAlive)) _thread = null; }
        Disconnect("Disconnected", publishClear: true);
    }

    private void Run(CancellationToken token)
    {
        _rateStarted = Environment.TickCount64;
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_view == null && !Connect())
                    {
                        SetStatus("Disconnected", "Waiting for the iRacing SDK mapping.");
                        if (token.WaitHandle.WaitOne(500)) break;
                        continue;
                    }

                    if ((_view!.ReadInt32(4) & 1) == 0)
                    {
                        IsConnected = false;
                        ClearPresentationState();
                        SetStatus("Connected (inactive)", "SDK mapping exists but telemetry is not active.");
                        if (token.WaitHandle.WaitOne(100)) break;
                        continue;
                    }

                    EnsureLayout();
                    (int tick, int offset) = FindNewestBuffer();
                    long now = Environment.TickCount64;
                    if (tick == _lastTick)
                    {
                        if (_lastTickAt != 0 && now - _lastTickAt >= StaleAfterMs && Status != "Stale")
                        {
                            IsConnected = false;
                            ClearPresentationState();
                            SetStatus("Stale", $"SDK tick {tick} has not advanced for {StaleAfterMs} ms.");
                        }
                    }
                    else
                    {
                        _lastTick = tick;
                        _lastTickAt = now;
                        ProcessSample(offset);
                        IsConnected = true;
                        _freshSamples++;
                        if (now - _rateStarted >= 1000)
                        {
                            double hz = _freshSamples * 1000.0 / Math.Max(1, now - _rateStarted);
                            _freshSamples = 0; _rateStarted = now;
                            SetStatus("Telemetry active", $"{hz:0.0} fresh ticks/s; {Diagnostics}");
                        }
                    }
                    if (token.WaitHandle.WaitOne(8)) break;
                }
                catch (Exception ex)
                {
                    Disconnect($"Disconnected: {ex.GetType().Name}", publishClear: true);
                    SetStatus("Disconnected", $"SDK read failed: {ex.Message}");
                    if (token.WaitHandle.WaitOne(500)) break;
                }
            }
        }
        finally
        {
            Disconnect("Disconnected", publishClear: true);
            lock (_gate) { if (_thread == Thread.CurrentThread) _thread = null; }
        }
    }

    private bool Connect()
    {
        try
        {
            _map = MemoryMappedFile.OpenExisting(_mapName, MemoryMappedFileRights.Read);
            _view = _map.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            if (_view.Capacity < HeaderSize) throw new InvalidDataException("SDK mapping is smaller than its header.");
            _variables.Clear(); _bufferLength = 0; _lastTick = int.MinValue; _lastTickAt = 0;
            ResetSessionState();
            SetStatus("Connected", "SDK shared memory opened; validating layout.");
            return true;
        }
        catch (FileNotFoundException) { return false; }
    }

    private void EnsureLayout()
    {
        int variableCount = _view!.ReadInt32(24);
        int variableOffset = _view.ReadInt32(28);
        int bufferCount = _view.ReadInt32(32);
        int bufferLength = _view.ReadInt32(36);
        if (variableCount is < 1 or > MaxVariables) throw new InvalidDataException($"Invalid SDK variable count {variableCount}.");
        if (bufferCount is < 1 or > MaxBuffers) throw new InvalidDataException($"Invalid SDK buffer count {bufferCount}.");
        if (bufferLength <= 0 || bufferLength > _view.Capacity) throw new InvalidDataException($"Invalid SDK buffer length {bufferLength}.");
        long headersEnd = (long)variableOffset + (long)variableCount * VarHeaderSize;
        if (variableOffset < HeaderSize || headersEnd > _view.Capacity) throw new InvalidDataException("SDK variable headers are outside the mapping.");
        if (_variables.Count != 0 && _bufferLength == bufferLength) return;

        _variables.Clear(); _bufferLength = bufferLength;
        for (int i = 0; i < variableCount; i++)
        {
            long p = variableOffset + (long)i * VarHeaderSize;
            int type = _view.ReadInt32(p), dataOffset = _view.ReadInt32(p + 4), count = _view.ReadInt32(p + 8);
            int elementSize = type switch { 0 or 1 => 1, 2 or 3 or 4 => 4, 5 => 8, _ => 0 };
            if (elementSize == 0 || count < 1 || dataOffset < 0 || (long)dataOffset + (long)elementSize * count > bufferLength) continue;
            byte[] name = new byte[32]; _view.ReadArray(p + 16, name, 0, name.Length);
            string decoded = Encoding.ASCII.GetString(name).TrimEnd('\0');
            if (decoded.Length != 0) _variables[decoded] = new Variable(type, dataOffset, count);
        }
        foreach (string required in new[] { "CarLeftRight", "LapCompleted", "LapLastLapTime", "SessionFlags" })
            if (!_variables.ContainsKey(required)) throw new InvalidDataException($"Required SDK variable '{required}' is missing or invalid.");
    }

    private (int Tick, int Offset) FindNewestBuffer()
    {
        int count = _view!.ReadInt32(32), newestTick = int.MinValue, newestOffset = -1;
        for (int i = 0; i < count; i++)
        {
            long p = 48 + i * 16L;
            int tick = _view.ReadInt32(p), offset = _view.ReadInt32(p + 4);
            if (offset < HeaderSize || (long)offset + _bufferLength > _view.Capacity) continue;
            if (tick > newestTick) { newestTick = tick; newestOffset = offset; }
        }
        if (newestOffset < 0) throw new InvalidDataException("No SDK data buffer has a valid offset.");
        return (newestTick, newestOffset);
    }

    private double ReadValue(string name, int buffer)
    {
        if (!_variables.TryGetValue(name, out Variable v)) return double.NaN;
        long p = buffer + v.Offset;
        return v.Type switch
        {
            0 => _view!.ReadByte(p), 1 => _view!.ReadByte(p) != 0 ? 1 : 0,
            2 => _view!.ReadInt32(p), 3 => _view!.ReadUInt32(p),
            4 => _view!.ReadSingle(p), 5 => _view!.ReadDouble(p), _ => double.NaN
        };
    }

    private void ProcessSample(int buffer)
    {
        int rawSpotter = CheckedInt("CarLeftRight", buffer);
        if (rawSpotter is < 0 or > 6) throw new InvalidDataException($"Invalid CarLeftRight enum value {rawSpotter}.");
        int lap = CheckedInt("LapCompleted", buffer);
        double lastLapTime = ReadValue("LapLastLapTime", buffer);
        uint rawFlags = CheckedUInt("SessionFlags", buffer);
        int sessionNumber = OptionalInt("SessionNum", buffer, 0);
        int sessionUnique = OptionalInt("SessionUniqueID", buffer, 0);
        string sessionId = $"{sessionUnique}:{sessionNumber}";
        int bestLapNumber = OptionalInt("LapBestLap", buffer, -1);
        double reportedBest = ReadValue("LapBestLapTime", buffer);

        if (_sessionId != sessionId || (_lastLap >= 0 && lap < _lastLap))
        {
            ClearPresentationState();
            ResetSessionState();
            _sessionId = sessionId;
        }

        SpotterState spotter = NormalizeSpotter(rawSpotter);
        if (spotter != _spotter) { _spotter = spotter; PublishSpotter(spotter); }

        RacingFlagState flag = NormalizeFlag(rawFlags);
        if (flag != _flag) { _flag = flag; PublishFlag(flag); }

        // Race-start border light. iRacing SessionFlags start bits: startReady 0x20000000,
        // startSet 0x40000000, startGo 0x80000000; green 0x00000004. Waiting = ready/set (not go);
        // the authoritative start is the rising edge of startGo (standing) or of green once we have
        // actually observed the waiting phase (rolling). Joining a race already in green never fires
        // because there is no waiting phase and no rising edge from a fresh connection.
        int phase = RaceStartFlags.Phase(rawFlags, _prevRawFlags, ref _sawRaceWaiting, ref _raceStartLatched);
        if (phase != _raceStartPhase)
        {
            _raceStartPhase = phase;
            Publish(new ViewLabEvent { Kind = ViewLabEventKind.RaceStart, Value = phase, SessionId = sessionId, TimestampUtc = DateTimeOffset.UtcNow });
        }
        _prevRawFlags = rawFlags;

        if (_lastLap >= 0 && lap > _lastLap)
        {
            bool valid = double.IsFinite(lastLapTime) && lastLapTime > 0;
            bool personalBest = valid && (bestLapNumber == lap || !double.IsFinite(_personalBest) || lastLapTime < _personalBest - 0.0005);
            double? delta = valid && double.IsFinite(_personalBest) ? lastLapTime - _personalBest : null;
            Publish(new ViewLabEvent
            {
                Kind = ViewLabEventKind.LapTime, LapNumber = lap, IsValid = valid,
                IsPersonalBest = personalBest, IsSessionBest = null, DeltaSeconds = delta,
                SessionId = sessionId, Value = valid ? lastLapTime : double.NaN,
                Title = $"Lap {lap}", Body = valid ? FormatLap(lastLapTime) : "Invalid lap",
                TimestampUtc = DateTimeOffset.UtcNow
            });
        }
        if (double.IsFinite(reportedBest) && reportedBest > 0) _personalBest = reportedBest;
        else if (double.IsFinite(lastLapTime) && lastLapTime > 0 && (!double.IsFinite(_personalBest) || lastLapTime < _personalBest)) _personalBest = lastLapTime;
        _lastLap = lap;

        // FuelLevelPct is optional — absent on some cars/older SDK builds. Its absence means "no
        // fuel data available," not zero fuel, so the warning simply never fires in that case.
        double fuelPct = ReadValue("FuelLevelPct", buffer);
        if (double.IsFinite(fuelPct))
        {
            double threshold = FuelWarningThresholdPct;
            if (fuelPct < threshold && !_fuelWarningFired)
            {
                _fuelWarningFired = true;
                Publish(new ViewLabEvent { Kind = ViewLabEventKind.FuelWarning, Value = fuelPct, SessionId = sessionId,
                    Title = "Low fuel", Body = $"{Math.Round(fuelPct * 100)}% remaining", TimestampUtc = DateTimeOffset.UtcNow });
            }
            else if (fuelPct > threshold * 1.5) _fuelWarningFired = false; // hysteresis: refuelling clears the warning
        }

        Diagnostics = $"tick={_lastTick}, session={sessionId}, CarLeftRight={rawSpotter}/{spotter}, LapCompleted={lap}, SessionFlags=0x{rawFlags:X}/{flag}";
    }

    private int CheckedInt(string name, int buffer)
    {
        double value = ReadValue(name, buffer);
        if (!double.IsFinite(value) || value < int.MinValue || value > int.MaxValue) throw new InvalidDataException($"Invalid {name} value.");
        return (int)value;
    }
    private uint CheckedUInt(string name, int buffer)
    {
        double value = ReadValue(name, buffer);
        if (!double.IsFinite(value) || value < uint.MinValue || value > uint.MaxValue) throw new InvalidDataException($"Invalid {name} value.");
        return (uint)value;
    }
    private int OptionalInt(string name, int buffer, int fallback)
    {
        double value = ReadValue(name, buffer);
        return double.IsFinite(value) && value >= int.MinValue && value <= int.MaxValue ? (int)value : fallback;
    }

    internal static SpotterState NormalizeSpotter(int raw) => raw switch
    {
        0 or 1 => SpotterState.Clear, 2 => SpotterState.CarLeft, 3 => SpotterState.CarRight,
        4 => SpotterState.CarsBothSides, 5 => SpotterState.TwoCarsLeft, 6 => SpotterState.TwoCarsRight,
        _ => SpotterState.Clear
    };

    internal static RacingFlagState NormalizeFlag(uint raw)
    {
        if ((raw & 0x00020000) != 0) return RacingFlagState.Disqualified;
        if ((raw & 0x00010000) != 0) return RacingFlagState.Black;
        if ((raw & 0x00000010) != 0) return RacingFlagState.Red;
        if ((raw & (0x00000008 | 0x00000100 | 0x00004000 | 0x00008000)) != 0) return RacingFlagState.Yellow;
        if ((raw & 0x00000020) != 0) return RacingFlagState.Blue;
        if ((raw & 0x00000040) != 0) return RacingFlagState.Debris;
        if ((raw & 0x00000002) != 0) return RacingFlagState.White;
        if ((raw & 0x00000001) != 0) return RacingFlagState.Checkered;
        if ((raw & 0x00000004) != 0) return RacingFlagState.Green;
        return RacingFlagState.Clear;
    }

    private void PublishSpotter(SpotterState state, bool presentationTest = false, bool clearPresentationTests = false)
    {
        double side = state is SpotterState.CarLeft or SpotterState.TwoCarsLeft ? -1 : state is SpotterState.CarRight or SpotterState.TwoCarsRight ? 1 : state == SpotterState.CarsBothSides ? 2 : 0;
        double count = state is SpotterState.TwoCarsLeft or SpotterState.TwoCarsRight ? 2 : state == SpotterState.CarsBothSides ? 2 : state == SpotterState.Clear ? 0 : 1;
        Publish(new ViewLabEvent { Kind = ViewLabEventKind.SpotterGlow, Spotter = state, Title = state.ToString(), Value = side, SecondaryValue = count,
            IsPresentationTest = presentationTest, ClearPresentationTests = clearPresentationTests, TimestampUtc = DateTimeOffset.UtcNow });
    }

    private void PublishFlag(RacingFlagState state, bool presentationTest = false, bool clearPresentationTests = false)
    {
        uint color = state switch
        {
            RacingFlagState.Green => 0x00D060, RacingFlagState.Blue => 0x168CFF, RacingFlagState.White => 0xFFFFFF,
            RacingFlagState.Yellow => 0xFFD000, RacingFlagState.Debris => 0xFF8000, RacingFlagState.Red => 0xFF2020,
            RacingFlagState.Black => 0x202020, RacingFlagState.Disqualified => 0xFF2020, RacingFlagState.Checkered => 0xFFFFFF, _ => 0
        };
        Publish(new ViewLabEvent { Kind = ViewLabEventKind.FlagState, Flag = state, Title = state.ToString(), Color = color,
            IsPresentationTest = presentationTest, ClearPresentationTests = clearPresentationTests, TimestampUtc = DateTimeOffset.UtcNow });
    }

    private void ClearPresentationState()
    {
        if (_spotter > SpotterState.Clear) { _spotter = SpotterState.Clear; PublishSpotter(SpotterState.Clear); }
        if (_flag > RacingFlagState.Clear) { _flag = RacingFlagState.Clear; PublishFlag(RacingFlagState.Clear); }
    }

    private void ResetSessionState()
    {
        _lastLap = -1; _personalBest = double.NaN; _sessionId = null;
        _spotter = (SpotterState)(-1); _flag = (RacingFlagState)(-1);
        _fuelWarningFired = false;
        _prevRawFlags = 0; _sawRaceWaiting = false; _raceStartLatched = false; _raceStartPhase = -1;
    }

    private static string FormatLap(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"m\:ss\.fff");
    private void Publish(ViewLabEvent e) => EventPublished?.Invoke(this, e);

    public void Simulate(string kind)
    {
        switch (kind)
        {
            case "Left": PublishSpotter(SpotterState.CarLeft, presentationTest: true); break;
            case "Right": PublishSpotter(SpotterState.CarRight, presentationTest: true); break;
            case "Both": PublishSpotter(SpotterState.CarsBothSides, presentationTest: true); break;
            case "TwoLeft": PublishSpotter(SpotterState.TwoCarsLeft, presentationTest: true); break;
            case "TwoRight": PublishSpotter(SpotterState.TwoCarsRight, presentationTest: true); break;
            case "Clear":
                PublishSpotter(SpotterState.Clear, presentationTest: true, clearPresentationTests: true);
                PublishFlag(RacingFlagState.Clear, presentationTest: true, clearPresentationTests: true);
                break;
            case "Lap": Publish(new ViewLabEvent { Kind = ViewLabEventKind.LapTime, LapNumber = 12, IsValid = true, IsPersonalBest = true,
                IsPresentationTest = true, SessionId = "fixture:0", Title = "Lap 12", Body = "1:34.221", Value = 94.221, TimestampUtc = DateTimeOffset.UtcNow }); break;
            case "Yellow": PublishFlag(RacingFlagState.Yellow, presentationTest: true); break;
            case "Blue": PublishFlag(RacingFlagState.Blue, presentationTest: true); break;
            case "LowFuel": Publish(new ViewLabEvent { Kind = ViewLabEventKind.FuelWarning, Value = 0.08, SessionId = "fixture:0",
                IsPresentationTest = true, Title = "Low fuel", Body = "8% remaining", TimestampUtc = DateTimeOffset.UtcNow }); break;
        }
        Diagnostics = "Simulated through generic event path: " + kind;
        SetStatus("Test presentation", Diagnostics);
    }

    private void SetStatus(string status, string detail)
    {
        if (Status == status && Diagnostics == detail) return;
        Status = status; Diagnostics = detail; DiagnosticsChanged?.Invoke();
    }
    private void Disconnect(string status, bool publishClear)
    {
        IsConnected = false;
        if (publishClear) ClearPresentationState();
        _view?.Dispose(); _map?.Dispose(); _view = null; _map = null; _variables.Clear(); _bufferLength = 0;
        _lastTick = int.MinValue; _lastTickAt = 0; ResetSessionState();
        if (Status != status) SetStatus(status, "No active SDK mapping.");
    }
    public void Dispose() { Stop(); _stop?.Dispose(); }
}
