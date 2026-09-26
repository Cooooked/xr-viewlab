using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace XRViewLab.UI;

// Fixed generic racing-state contract with dllmain.cpp. The iRacing provider is only one producer;
// native rendering never sees simulator-specific fields.
internal sealed class RacingStateService : IDisposable
{
    private const string Name = "Local\\XRViewLabRacingState";
    private const int Size = 76; // v4 adds independent close-range spotter proximity at offset 72
    private const uint Magic = 0x31524C56; // VLR1
    private readonly MemoryMappedFile _map;
    private readonly MemoryMappedViewAccessor _view;
    private uint _generation;
    private SpotterState _spotter;
    private SpotterState _testSpotter;
    private RacingFlagState _flag;
    private uint _flagColor;
    private RacingFlagState _testFlag;
    private uint _testFlagColor;
    private uint _presentationFlags;
    private uint _raceStartPhase; // 0 inactive, 1 waiting/red, 2 started/green (native owns hold+fade)
    private uint _testRaceStartPhase;
    private uint _rearClosing;    // packed: bit0 active, opacity<<8, width<<16, intensity<<24
    private uint _testRearClosing;
    private uint _grip;           // packed: bit0 active, dominance<<1, direction<<3, severity<<8
    private uint _testGrip;
    private uint _shift;          // packed: bit0 active, bit1 at shift point, bit2 over-rev, progress<<8
    private uint _testShift;
    private uint _spotterProximity, _testSpotterProximity;

    public RacingStateService() : this(Name) { }
    internal RacingStateService(string name)
    {
        _map = MemoryMappedFile.CreateOrOpen(name, Size, MemoryMappedFileAccess.ReadWrite);
        _view = _map.CreateViewAccessor(0, Size, MemoryMappedFileAccess.ReadWrite);
        _view.Write(0, Magic); _view.Write(4, 4u); _view.Write(8, (uint)Size);
        PublishState();
    }

    public void Publish(ViewLabEvent e, double lapDurationMs)
    {
        // An explicit test temporarily takes priority over live telemetry. Keep the live values
        // separately so clearing the test restores the current real cue immediately.
        if (e.ClearPresentationTests) _presentationFlags = 0;
        switch (e.Kind)
        {
            case ViewLabEventKind.SpotterGlow:
                if (e.IsPresentationTest) { _testSpotter = e.Spotter; _presentationFlags = e.Spotter != SpotterState.Clear ? _presentationFlags | 1u : _presentationFlags & ~1u; }
                else _spotter = e.Spotter;
                PublishState(); break;
            case ViewLabEventKind.FlagState:
                if (e.IsPresentationTest) { _testFlag = e.Flag; _testFlagColor = e.Color; _presentationFlags = e.Flag != RacingFlagState.Clear ? _presentationFlags | 2u : _presentationFlags & ~2u; }
                else { _flag = e.Flag; _flagColor = e.Color; }
                PublishState(); break;
            case ViewLabEventKind.RaceStart:
                if (e.IsPresentationTest) { _testRaceStartPhase = (uint)Math.Clamp((int)Math.Round(e.Value), 0, 2); _presentationFlags = _testRaceStartPhase != 0 ? _presentationFlags | 8u : _presentationFlags & ~8u; }
                else _raceStartPhase = (uint)Math.Clamp((int)Math.Round(e.Value), 0, 2);
                PublishState(); break;
            case ViewLabEventKind.RearClosing:
                if (e.IsPresentationTest) {
                    _testRearClosing = (uint)e.Value;
                    _presentationFlags = (_testRearClosing & 1u) != 0 ? _presentationFlags | 16u : _presentationFlags & ~16u;
                }
                else _rearClosing = (uint)e.Value;
                PublishState(); break;
            case ViewLabEventKind.SpotterProximity:
                if (e.IsPresentationTest) { _testSpotterProximity = (uint)Math.Clamp((int)e.Value, 0, 255); _presentationFlags = _testSpotterProximity != 0 ? _presentationFlags | 128u : _presentationFlags & ~128u; }
                else _spotterProximity = (uint)Math.Clamp((int)e.Value, 0, 255);
                PublishState(); break;
            case ViewLabEventKind.GripOBar:
                if (e.IsPresentationTest) { _testGrip = (uint)e.Value; _presentationFlags = (_testGrip & 1u) != 0 ? _presentationFlags | 32u : _presentationFlags & ~32u; }
                else _grip = (uint)e.Value;
                PublishState(); break;
            case ViewLabEventKind.ShiftLight:
                if (e.IsPresentationTest) { _testShift = (uint)e.Value; _presentationFlags = (_testShift & 1u) != 0 ? _presentationFlags | 64u : _presentationFlags & ~64u; }
                else _shift = (uint)e.Value;
                PublishState(); break;
            case ViewLabEventKind.LapTime:
                _presentationFlags = e.IsPresentationTest ? _presentationFlags | 4u : _presentationFlags & ~4u;
                uint flags = 1u | (e.IsValid ? 2u : 0u) | (e.IsPersonalBest ? 4u : 0u) |
                    (e.DeltaSeconds.HasValue ? 8u : 0u);
                _view.Write(28, flags); _view.Write(32, e.LapNumber);
                _view.Write(36, (float)e.Value); _view.Write(40, (float)(e.DeltaSeconds ?? 0));
                _view.Write(48, Environment.TickCount64 + (long)Math.Clamp(lapDurationMs, 1000, 15000));
                _view.Write(56, _presentationFlags);
                PublishGeneration();
                break;
        }
    }

    public void Clear()
    {
        _spotter = _testSpotter = SpotterState.Clear; _flag = _testFlag = RacingFlagState.Clear; _flagColor = _testFlagColor = 0; _presentationFlags = 0; _raceStartPhase = _testRaceStartPhase = 0; _rearClosing = _testRearClosing = 0; _grip = _testGrip = 0; _shift = _testShift = 0; _spotterProximity = _testSpotterProximity = 0;
        _view.Write(28, 0u); _view.Write(48, 0L); PublishState();
    }

    private void PublishState()
    {
        _view.Write(16, (uint)((_presentationFlags & 128u) != 0 ? SpotterState.Clear : (_presentationFlags & 1u) != 0 ? _testSpotter : _spotter));
        _view.Write(20, (uint)((_presentationFlags & 2u) != 0 ? _testFlag : _flag));
        _view.Write(24, (_presentationFlags & 2u) != 0 ? _testFlagColor : _flagColor);
        _view.Write(56, _presentationFlags);
        _view.Write(44, (_presentationFlags & 8u) != 0 ? _testRaceStartPhase : _raceStartPhase);
        _view.Write(60, (_presentationFlags & 16u) != 0 ? _testRearClosing : _rearClosing);
        _view.Write(64, (_presentationFlags & 32u) != 0 ? _testGrip : _grip);
        _view.Write(68, (_presentationFlags & 64u) != 0 ? _testShift : _shift);
        _view.Write(72, (_presentationFlags & 128u) != 0 ? _testSpotterProximity : _spotterProximity);
        PublishGeneration();
    }

    private void PublishGeneration()
    {
        Thread.MemoryBarrier(); _view.Write(12, unchecked(++_generation));
    }

    public void Dispose() { Clear(); _view.Dispose(); _map.Dispose(); }
}
