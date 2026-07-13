using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;
using XRViewLab.UI;

const int Capacity = 8192, Headers = 256, Buffer = 4096, BufferLength = 128;
string mapName = "Local\\ViewLabIRacingFixture_" + Environment.ProcessId;
using var map = MemoryMappedFile.CreateNew(mapName, Capacity, MemoryMappedFileAccess.ReadWrite);
using var view = map.CreateViewAccessor(0, Capacity, MemoryMappedFileAccess.ReadWrite);
var variables = new (string Name, int Type, int Offset)[]
{
    ("CarLeftRight", 2, 0), ("LapCompleted", 2, 4), ("LapLastLapTime", 4, 8),
    ("SessionFlags", 3, 12), ("SessionNum", 2, 16), ("SessionUniqueID", 2, 20),
    ("LapBestLap", 2, 24), ("LapBestLapTime", 4, 28),
};

view.Write(0, 2); view.Write(4, 1); view.Write(8, 60);
view.Write(24, variables.Length); view.Write(28, Headers); view.Write(32, 1); view.Write(36, BufferLength);
view.Write(48, 0); view.Write(52, Buffer);
for (int i = 0; i < variables.Length; i++)
{
    long p = Headers + i * 144L;
    view.Write(p, variables[i].Type); view.Write(p + 4, variables[i].Offset); view.Write(p + 8, 1);
    byte[] name = new byte[32]; Encoding.ASCII.GetBytes(variables[i].Name).CopyTo(name, 0);
    view.WriteArray(p + 16, name, 0, name.Length);
}

int tick = 0;
void Sample(int spotter = 1, int lap = 0, float lapTime = -1, uint flags = 0, int session = 1, int unique = 100, int bestLap = -1, float bestTime = -1)
{
    view.Write(Buffer + 0, spotter); view.Write(Buffer + 4, lap); view.Write(Buffer + 8, lapTime);
    view.Write(Buffer + 12, flags); view.Write(Buffer + 16, session); view.Write(Buffer + 20, unique);
    view.Write(Buffer + 24, bestLap); view.Write(Buffer + 28, bestTime); view.Flush();
    view.Write(48, ++tick); view.Flush();
}

var events = new ConcurrentQueue<ViewLabEvent>();
string racingMapName = "Local\\ViewLabRacingStateFixture_" + Environment.ProcessId;
using var racingState = new RacingStateService(racingMapName);
using var racingMap = MemoryMappedFile.OpenExisting(racingMapName, MemoryMappedFileRights.Read);
using var racingView = racingMap.CreateViewAccessor(0, 64, MemoryMappedFileAccess.Read);
using var provider = new IRacingTelemetryProvider(mapName);
provider.EventPublished += (_, e) => { events.Enqueue(e); racingState.Publish(e, 4500); };
provider.Start();
Sample();
WaitUntil(() => provider.IsConnected, "provider connects to fixture mapping");
Drain();

var official = new[]
{
    SpotterState.Clear, SpotterState.Clear, SpotterState.CarLeft, SpotterState.CarRight,
    SpotterState.CarsBothSides, SpotterState.TwoCarsLeft, SpotterState.TwoCarsRight
};
for (int raw = 0; raw <= 6; raw++)
    Assert(IRacingTelemetryProvider.NormalizeSpotter(raw) == official[raw], $"official spotter enum {raw} maps to {official[raw]}");

foreach (int raw in new[] { 2, 3, 4, 5, 6, 1 })
{
    Sample(spotter: raw);
    ViewLabEvent e = WaitEvent(x => x.Kind == ViewLabEventKind.SpotterGlow, $"spotter raw {raw}");
    Assert(e.Spotter == official[raw], $"spotter raw {raw} reaches generic event as {official[raw]}");
    WaitUntil(() => racingView.ReadUInt32(16) == (uint)official[raw], $"spotter raw {raw} reaches native racing-state contract");
    Drain();
}
Sample(spotter: 0); Thread.Sleep(40);
Assert(!events.Any(e => e.Kind == ViewLabEventKind.SpotterGlow), "equivalent Off/Clear does not storm");

int beforeRepeatedTick = events.Count;
Thread.Sleep(80);
Assert(events.Count == beforeRepeatedTick, "repeated SDK tick emits no duplicate events");

Sample(spotter: 2);
WaitEvent(e => e.Kind == ViewLabEventKind.SpotterGlow && e.Spotter == SpotterState.CarLeft, "left before stale");
WaitUntil(() => provider.Status == "Stale", "unchanged tick becomes stale", 1800);
WaitEvent(e => e.Kind == ViewLabEventKind.SpotterGlow && e.Spotter == SpotterState.Clear, "stale clears spotter");
WaitUntil(() => racingView.ReadUInt32(16) == 0, "stale clears native racing-state contract");
Drain();

Sample(spotter: 3);
WaitUntil(() => provider.IsConnected, "fresh tick recovers stale provider");
WaitEvent(e => e.Kind == ViewLabEventKind.SpotterGlow && e.Spotter == SpotterState.CarRight, "recovery republishes authoritative state");
Drain();

view.Write(4, 0); view.Flush();
WaitUntil(() => provider.Status == "Connected (inactive)", "inactive SDK state is distinct");
WaitEvent(e => e.Kind == ViewLabEventKind.SpotterGlow && e.Spotter == SpotterState.Clear, "inactive SDK clears spotter");
view.Write(4, 1); Sample(spotter: 5);
WaitUntil(() => provider.IsConnected, "inactive mapping reconnects");
WaitEvent(e => e.Kind == ViewLabEventKind.SpotterGlow && e.Spotter == SpotterState.TwoCarsLeft, "reconnect state");
Drain();

var flags = new (uint Raw, RacingFlagState Expected)[]
{
    (0x4, RacingFlagState.Green), (0x20, RacingFlagState.Blue), (0x2, RacingFlagState.White),
    (0x8, RacingFlagState.Yellow), (0x40, RacingFlagState.Debris), (0x10, RacingFlagState.Red),
    (0x10000, RacingFlagState.Black), (0x20000, RacingFlagState.Disqualified), (0x1, RacingFlagState.Checkered),
    (0x28, RacingFlagState.Yellow), (0, RacingFlagState.Clear),
};
foreach ((uint raw, RacingFlagState expected) in flags)
{
    Sample(flags: raw);
    ViewLabEvent e = WaitEvent(x => x.Kind == ViewLabEventKind.FlagState, $"flag 0x{raw:X}");
    Assert(e.Flag == expected, $"flag 0x{raw:X} maps/prioritises as {expected}"); Drain();
    WaitUntil(() => racingView.ReadUInt32(20) == (uint)expected, $"flag {expected} reaches native racing-state contract");
}

Sample(lap: 10, lapTime: 95, bestLap: 9, bestTime: 94);
Drain();
Sample(lap: 11, lapTime: 93, bestLap: 11, bestTime: 93);
ViewLabEvent lap = WaitEvent(e => e.Kind == ViewLabEventKind.LapTime, "valid lap completion");
Assert(lap.IsValid && lap.IsPersonalBest && lap.LapNumber == 11 && lap.SessionId == "100:1", "valid PB lap semantics");
WaitUntil(() => racingView.ReadInt32(32) == 11 && (racingView.ReadUInt32(28) & 7) == 7, "lap result reaches native racing-state contract");
Sample(lap: 11, lapTime: 93, bestLap: 11, bestTime: 93); Thread.Sleep(40);
Assert(!events.Any(e => e.Kind == ViewLabEventKind.LapTime), "duplicate lap value is suppressed");
Sample(lap: 12, lapTime: -1, bestLap: 11, bestTime: 93);
lap = WaitEvent(e => e.Kind == ViewLabEventKind.LapTime, "invalid completed lap");
Assert(!lap.IsValid && lap.Body == "Invalid lap", "invalid lap is explicit and does not fabricate timing");
Sample(lap: 1, lapTime: 90, session: 2, unique: 101, bestLap: 1, bestTime: 90); Thread.Sleep(40);
Assert(!events.Any(e => e.Kind == ViewLabEventKind.LapTime), "session change resets lap baseline without phantom card");
Sample(lap: 2, lapTime: 89, session: 2, unique: 101, bestLap: 2, bestTime: 89);
lap = WaitEvent(e => e.Kind == ViewLabEventKind.LapTime, "new-session lap");
Assert(lap.SessionId == "101:2" && lap.LapNumber == 2, "new session identity is authoritative"); Drain();

Sample(spotter: 99);
WaitUntil(() => provider.Status == "Disconnected" && provider.Diagnostics.Contains("Invalid CarLeftRight"), "invalid enum fails closed");
view.Write(52, Capacity - 8); view.Flush(); view.Write(48, ++tick); view.Flush();
Thread.Sleep(550);
Assert(provider.Status == "Disconnected" && provider.Diagnostics.Contains("valid offset"), "invalid buffer offset is rejected");
view.Write(52, Buffer); view.Flush(); Sample(spotter: 1);
WaitUntil(() => provider.IsConnected, "provider reconnects after repaired layout");

provider.Stop();
Assert(!provider.IsConnected && provider.Status == "Disconnected", "stop is interruptible and clears connection");
provider.Start(); Sample(spotter: 6);
WaitUntil(() => provider.IsConnected, "quick stop/start creates one fresh worker");
WaitEvent(e => e.Kind == ViewLabEventKind.SpotterGlow && e.Spotter == SpotterState.TwoCarsRight, "quick restart event path");
provider.Stop();

Console.WriteLine("iRacing shared-memory fixtures passed: official spotter enum, tick dedupe, stale/inactive clearing, reconnect, layout/value rejection, flag priority, lap/session semantics, quick stop/start.");

void Drain() { while (events.TryDequeue(out _)) { } }
ViewLabEvent WaitEvent(Func<ViewLabEvent, bool> predicate, string description, int timeout = 1500)
{
    ViewLabEvent found = default;
    WaitUntil(() =>
    {
        while (events.TryDequeue(out ViewLabEvent candidate)) if (predicate(candidate)) { found = candidate; return true; }
        return false;
    }, description, timeout);
    return found;
}
void WaitUntil(Func<bool> predicate, string description, int timeout = 1500)
{
    var sw = Stopwatch.StartNew();
    while (sw.ElapsedMilliseconds < timeout) { if (predicate()) { Console.WriteLine("PASS " + description); return; } Thread.Sleep(5); }
    throw new InvalidOperationException("Timed out: " + description + $" (status={provider.Status}; diagnostics={provider.Diagnostics})");
}
void Assert(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("Assertion failed: " + description);
    Console.WriteLine("PASS " + description);
}
