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
}

internal readonly struct ViewLabEvent
{
    public ViewLabEventKind Kind { get; init; }
    public string? Title { get; init; }
    public string? Body { get; init; }
    public double Value { get; init; }   // generic scalar (e.g. spotter side/intensity)
    public uint Color { get; init; }      // 0xRRGGBB where meaningful (e.g. flag colour)
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
