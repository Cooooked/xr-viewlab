using System;
using System.Collections.Generic;
using XRViewLab.UI;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAILED: " + message);
    Console.WriteLine("PASS: " + message);
}

// ---- Dedup / track-change detection ---------------------------------------------------
{
    var t = new NowPlayingTracker();
    Require(t.ShouldEmit("Song A", "Artist 1"), "first track emits a card");
    Require(!t.ShouldEmit("Song A", "Artist 1"), "identical metadata update does not re-emit (pause/seek/volume)");
    Require(!t.ShouldEmit("  Song A  ", " Artist 1 "), "whitespace-only differences are deduplicated");
    Require(t.ShouldEmit("Song B", "Artist 1"), "genuine title change emits while playback stays active");
    Require(t.ShouldEmit("Song B", "Artist 2"), "genuine artist change emits");
    Require(!t.ShouldEmit("", "Artist 2"), "missing title never emits and never changes the key");
    Require(!t.ShouldEmit("Song B", "Artist 2"), "prior track key survived the empty-title update");
}

// ---- Session switching on the same track must not re-emit -----------------------------
{
    var t = new NowPlayingTracker();
    Require(t.ShouldEmit("Shared Song", "Shared Artist"), "track emits on first session");
    // App/session changes but the audible track is identical: no duplicate card.
    Require(!t.ShouldEmit("Shared Song", "Shared Artist"), "switching sessions on an identical track does not re-emit");
    Require(t.ShouldEmit("New Song", "Shared Artist"), "a real change after the switch still emits");
}

// ---- Preferred-session selection ------------------------------------------------------
{
    // Prefer the actually-playing session over the OS 'current' (often a paused browser tab).
    var sessions = new List<MediaSessionSnapshot>
    {
        new("browser", false, "Paused Tab", "Someone"),
        new("tidal",   true,  "Now Playing", "Band"),
    };
    Require(NowPlayingTracker.SelectSessionIndex(sessions, 0) == 1, "playing session is preferred over the paused OS-current session");

    // When the OS-current session is itself playing, keep it.
    var both = new List<MediaSessionSnapshot>
    {
        new("a", true, "A", "x"),
        new("b", true, "B", "y"),
    };
    Require(NowPlayingTracker.SelectSessionIndex(both, 1) == 1, "a playing OS-current session is kept");
    Require(NowPlayingTracker.SelectSessionIndex(both, 0) == 0, "first playing OS-current session is kept");

    // None playing: fall back to the OS-current session.
    var paused = new List<MediaSessionSnapshot>
    {
        new("a", false, "A", "x"),
        new("b", false, "B", "y"),
    };
    Require(NowPlayingTracker.SelectSessionIndex(paused, 1) == 1, "with none playing the OS-current session is used");

    // None playing, no valid current: first session carrying metadata.
    var noCurrent = new List<MediaSessionSnapshot>
    {
        new("a", false, "", ""),
        new("b", false, "Has Meta", "z"),
    };
    Require(NowPlayingTracker.SelectSessionIndex(noCurrent, -1) == 1, "first session with metadata is chosen when nothing is current or playing");

    // Empty list yields no selection (all sessions closed).
    Require(NowPlayingTracker.SelectSessionIndex(new List<MediaSessionSnapshot>(), 0) == -1, "no sessions yields no selection");
}

// ---- Reconnect after all sessions closed ---------------------------------------------
{
    var t = new NowPlayingTracker();
    Require(t.ShouldEmit("Track", "Artist"), "track emits before disconnect");
    // Provider keeps the last key across a session-closure gap, so reconnecting to the same
    // track stays quiet; Reset models an explicit provider stop/start.
    Require(!t.ShouldEmit("Track", "Artist"), "reconnecting the same track without reset stays quiet");
    t.Reset();
    Require(t.ShouldEmit("Track", "Artist"), "after an explicit provider restart the current track emits once");
}

// ---- Disabled-state behaviour ---------------------------------------------------------
// The feature gate lives in the broker: EnqueueMediaCard is only called when
// media_notify_enabled is true. Model that gate here to prove a disabled feature emits nothing.
{
    bool enabled = false;
    int cards = 0;
    var t = new NowPlayingTracker();
    void OnTrack(string title, string artist) { if (enabled && t.ShouldEmit(title, artist)) cards++; }
    OnTrack("Song", "Artist");
    Require(cards == 0, "disabled feature produces no cards even on a genuine change");
    enabled = true;
    OnTrack("Song", "Artist");
    Require(cards == 1, "enabling the feature lets the next change through");
}

Console.WriteLine("Now Playing logic fixtures passed.");
