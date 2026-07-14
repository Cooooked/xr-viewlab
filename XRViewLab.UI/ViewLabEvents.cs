using System;

namespace XRViewLab.UI;

// Generic ViewLab overlay-event contract. Providers normalize simulator-specific values here;
// the native renderer never references iRacing SDK fields.
// generic, renderer-agnostic events without the visor renderer ever referencing iRacing.
//
// A later telemetry provider implements IViewLabEventProvider and raises EventPublished with a
// ViewLabEvent. A thin consumer (to be added when the feature ships) translates those generic
// events into the same shared-memory overlay primitives the notification/crosshair paths already
// use — the renderer stays decoupled from the data source.

internal enum ViewLabEventKind
{
    // Lap-time popup: a transient card (Title = "Lap 12", Body = "1:34.221").
    LapTime,
    // Peripheral spotter glow: Value in [-1,1] left/right intensity; drives an edge glow.
    SpotterGlow,
    // Flag-state border: Color carries the flag colour; a full-frame border tint.
    FlagState,
    // Low-fuel warning card: a transient card (Title = "Low fuel", Body = "8% remaining"). Fires
    // once per threshold crossing, not every sample — Value carries the fuel fraction (0..1).
    FuelWarning,
}

internal enum SpotterState
{
    Clear = 0,
    CarLeft = 1,
    CarRight = 2,
    CarsBothSides = 3,
    TwoCarsLeft = 4,
    TwoCarsRight = 5,
}

internal enum RacingFlagState
{
    Clear = 0,
    Green,
    Blue,
    White,
    Yellow,
    Debris,
    Red,
    Black,
    Disqualified,
    Checkered,
}

internal readonly struct ViewLabEvent
{
    public ViewLabEventKind Kind { get; init; }
    public string? Title { get; init; }
    public string? Body { get; init; }
    public double Value { get; init; }   // generic scalar (e.g. spotter side/intensity)
    public double SecondaryValue { get; init; }
    public uint Color { get; init; }      // 0xRRGGBB where meaningful (e.g. flag colour)
    public SpotterState Spotter { get; init; }
    public RacingFlagState Flag { get; init; }
    public int LapNumber { get; init; }
    public bool IsValid { get; init; }
    public bool IsPersonalBest { get; init; }
    public bool IsPresentationTest { get; init; }
    public bool ClearPresentationTests { get; init; }
    public bool? IsSessionBest { get; init; }
    public double? DeltaSeconds { get; init; }
    public string? SessionId { get; init; }
    public DateTimeOffset TimestampUtc { get; init; }
}

// Implemented by future telemetry providers (iRacing first). The provider owns its own polling
// thread/IPC and must never touch the render path directly.
internal interface IViewLabEventProvider : IDisposable
{
    string Name { get; }
    bool IsConnected { get; }
    event EventHandler<ViewLabEvent>? EventPublished;
    void Start();
    void Stop();
}
