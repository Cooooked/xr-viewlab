using System;
using System.Collections.Generic;

namespace XRViewLab.UI;

// One session's observable state, decoupled from WinRT so selection and dedup are
// deterministically testable without a live SMTC stack.
internal readonly record struct MediaSessionSnapshot(string SessionId, bool IsPlaying, string Title, string Artist);

// Pure Now Playing decision logic:
//   * Selection: prefer the session that is actually PLAYING (Windows' "current" session is
//     frequently a paused browser tab while another app is audible). Among playing sessions the
//     OS-current one wins; with none playing the OS-current one is used; otherwise the first
//     session with any metadata.
//   * Dedup: a card is emitted only when the trimmed title+artist key genuinely changes.
//     Pause/seek/volume/position updates repeat identical metadata and must not re-emit, and a
//     session switch to the very same track must not re-emit either (the key is app-agnostic).
internal sealed class NowPlayingTracker
{
    private string? _lastKey;

    public static int SelectSessionIndex(IReadOnlyList<MediaSessionSnapshot> sessions, int currentIndex)
    {
        if (sessions == null || sessions.Count == 0) return -1;
        bool currentValid = currentIndex >= 0 && currentIndex < sessions.Count;
        if (currentValid && sessions[currentIndex].IsPlaying) return currentIndex;
        for (int i = 0; i < sessions.Count; i++) if (sessions[i].IsPlaying) return i;
        if (currentValid) return currentIndex;
        for (int i = 0; i < sessions.Count; i++) if (!string.IsNullOrWhiteSpace(sessions[i].Title)) return i;
        return 0;
    }

    // True exactly when this metadata should produce a card; updates the dedup key when it does.
    public bool ShouldEmit(string title, string artist)
    {
        if (string.IsNullOrWhiteSpace(title)) return false; // no metadata: never a card, never a key change
        string key = TrackKey(title, artist);
        if (key == _lastKey) return false;
        _lastKey = key;
        return true;
    }

    public void Reset() => _lastKey = null;

    internal static string TrackKey(string title, string artist) => (title ?? string.Empty).Trim() + "␟" + (artist ?? string.Empty).Trim();
}
