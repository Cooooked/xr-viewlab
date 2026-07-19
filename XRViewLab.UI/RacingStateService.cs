using System;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace XRViewLab.UI;

// Fixed generic racing-state contract with dllmain.cpp. The iRacing provider is only one producer;
// native rendering never sees simulator-specific fields.
internal sealed class RacingStateService : IDisposable
{
    private const string Name = "Local\\XRViewLabRacingState";
    private const int Size = 64;
    private const uint Magic = 0x31524C56; // VLR1
    private readonly MemoryMappedFile _map;
    private readonly MemoryMappedViewAccessor _view;
    private uint _generation;
    private SpotterState _spotter;
    private RacingFlagState _flag;
    private uint _flagColor;
    private uint _presentationFlags;
    private uint _raceStartPhase; // 0 inactive, 1 waiting/red, 2 started/green (native owns hold+fade)
    private uint _rearClosing;    // packed: bit0 active, opacity<<8, width<<16, intensity<<24

    public RacingStateService() : this(Name) { }
    internal RacingStateService(string name)
    {
        _map = MemoryMappedFile.CreateOrOpen(name, Size, MemoryMappedFileAccess.ReadWrite);
        _view = _map.CreateViewAccessor(0, Size, MemoryMappedFileAccess.ReadWrite);
        _view.Write(0, Magic); _view.Write(4, 1u); _view.Write(8, (uint)Size);
        PublishState();
    }

    public void Publish(ViewLabEvent e, double lapDurationMs)
    {
        if (e.ClearPresentationTests) _presentationFlags = 0;
        switch (e.Kind)
        {
            case ViewLabEventKind.SpotterGlow:
                _spotter = e.Spotter;
                _presentationFlags = e.IsPresentationTest && e.Spotter != SpotterState.Clear ? _presentationFlags | 1u : _presentationFlags & ~1u;
                PublishState(); break;
            case ViewLabEventKind.FlagState:
                _flag = e.Flag; _flagColor = e.Color;
                _presentationFlags = e.IsPresentationTest && e.Flag != RacingFlagState.Clear ? _presentationFlags | 2u : _presentationFlags & ~2u;
                PublishState(); break;
            case ViewLabEventKind.RaceStart:
                _raceStartPhase = (uint)Math.Clamp((int)Math.Round(e.Value), 0, 2);
                _presentationFlags = e.IsPresentationTest && _raceStartPhase != 0 ? _presentationFlags | 8u : _presentationFlags & ~8u;
                PublishState(); break;
            case ViewLabEventKind.RearClosing:
                _rearClosing = (uint)e.Value;
                _presentationFlags = e.IsPresentationTest && (_rearClosing & 1u) != 0 ? _presentationFlags | 16u : _presentationFlags & ~16u;
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
        _spotter = SpotterState.Clear; _flag = RacingFlagState.Clear; _flagColor = 0; _presentationFlags = 0; _raceStartPhase = 0; _rearClosing = 0;
        _view.Write(28, 0u); _view.Write(48, 0L); PublishState();
    }

    private void PublishState()
    {
        _view.Write(16, (uint)_spotter); _view.Write(20, (uint)_flag); _view.Write(24, _flagColor); _view.Write(56, _presentationFlags);
        _view.Write(44, _raceStartPhase); // reserved0: race-start phase (no version bump; old layers ignore it)
        _view.Write(60, _rearClosing);    // reserved1: packed rear-closing state
        PublishGeneration();
    }

    private void PublishGeneration()
    {
        Thread.MemoryBarrier(); _view.Write(12, unchecked(++_generation));
    }

    public void Dispose() { Clear(); _view.Dispose(); _map.Dispose(); }
}
