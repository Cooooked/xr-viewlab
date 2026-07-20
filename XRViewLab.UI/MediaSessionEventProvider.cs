using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace XRViewLab.UI;

internal readonly record struct MediaTrackInfo(string Title, string Artist, BitmapSource? Artwork);

// Watches ALL OS media sessions (Windows System Media Transport Controls — the same source
// Windows itself uses for the volume-flyout media card) and raises TrackChanged only when the
// title/artist of the preferred session actually changes. Track changes are metadata updates on
// these sessions, not Windows toast notifications — players like Tidal, Spotify or a browser
// YouTube Music tab never need to raise a toast for this card to work.
//
// Why all sessions rather than GetCurrentSession() alone: Windows' "current" session is often a
// paused tab or the last-focused app while a different app is audibly playing. Selection and
// dedup live in NowPlayingTracker (WPF/WinRT-free, deterministically tested): prefer the playing
// session, and emit only on a genuine title/artist change — pause/seek/volume updates and
// switching sessions on an identical track never repeat the visor card. A machine with no
// SMTC-reporting player simply never raises the event; there is no polling fallback.
internal sealed class MediaSessionEventProvider : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly List<GlobalSystemMediaTransportControlsSession> _attached = new();
    private readonly NowPlayingTracker _tracker = new();
    private int _refreshActive;
    private bool _refreshQueued;
    private bool _started;
    private bool _disposed;

    public event Action<MediaTrackInfo>? TrackChanged;
    public string Status { get; private set; } = "Media session watcher idle.";

    public async void Start()
    {
        if (_started) return;
        _started = true;
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.SessionsChanged += OnSessionsChanged;
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            AttachSessions();
            Status = "Media session watcher active.";
            _ = RefreshAsync();
        }
        catch (Exception ex)
        {
            Status = $"Media session watcher unavailable: {ex.GetType().Name}: {ex.Message}";
            _started = false;
        }
    }

    public void Stop()
    {
        DetachSessions();
        if (_manager != null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }
        _manager = null;
        _tracker.Reset();
        _started = false;
        Status = "Media session watcher idle.";
    }

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) { AttachSessions(); _ = RefreshAsync(); }
    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => _ = RefreshAsync();
    private void OnPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => _ = RefreshAsync();
    private void OnPlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) => _ = RefreshAsync();

    // (Re)subscribe to every live session. Sessions come and go as apps open/close media; a
    // closed session's handlers are dropped with it and the next change re-enumerates cleanly.
    private void AttachSessions()
    {
        DetachSessions();
        var manager = _manager;
        if (manager == null) return;
        try
        {
            foreach (var session in manager.GetSessions())
            {
                session.MediaPropertiesChanged += OnPropertiesChanged;
                session.PlaybackInfoChanged += OnPlaybackChanged;
                _attached.Add(session);
            }
        }
        catch { /* a session list torn mid-enumeration re-attaches on the next SessionsChanged */ }
    }

    private void DetachSessions()
    {
        foreach (var session in _attached)
        {
            try { session.MediaPropertiesChanged -= OnPropertiesChanged; session.PlaybackInfoChanged -= OnPlaybackChanged; } catch { }
        }
        _attached.Clear();
    }

    // Coalescing refresh: overlapping SMTC events run one snapshot pass and queue at most one more,
    // so the final state is always evaluated without unbounded re-entrancy.
    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _refreshActive, 1) != 0) { _refreshQueued = true; return; }
        try
        {
            do
            {
                _refreshQueued = false;
                await RefreshOnceAsync();
            } while (_refreshQueued);
        }
        finally { Interlocked.Exchange(ref _refreshActive, 0); }
    }

    private async Task RefreshOnceAsync()
    {
        var manager = _manager;
        if (manager == null) return;
        try
        {
            IReadOnlyList<GlobalSystemMediaTransportControlsSession> sessions = manager.GetSessions();
            if (sessions.Count == 0) return; // all sessions closed: keep the last key so a reconnect of the same track stays quiet
            string? currentId = null;
            try { currentId = manager.GetCurrentSession()?.SourceAppUserModelId; } catch { }

            var snapshots = new List<MediaSessionSnapshot>(sessions.Count);
            var props = new List<GlobalSystemMediaTransportControlsSessionMediaProperties?>(sessions.Count);
            int currentIndex = -1;
            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                string id = Safe(() => s.SourceAppUserModelId) ?? string.Empty;
                if (currentIndex < 0 && currentId != null && id == currentId) currentIndex = i;
                bool playing = Safe(() => s.GetPlaybackInfo()?.PlaybackStatus) == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                GlobalSystemMediaTransportControlsSessionMediaProperties? p = null;
                try { p = await s.TryGetMediaPropertiesAsync(); } catch { }
                props.Add(p);
                snapshots.Add(new MediaSessionSnapshot(id, playing, p?.Title ?? string.Empty, p?.Artist ?? string.Empty));
            }

            int pick = NowPlayingTracker.SelectSessionIndex(snapshots, currentIndex);
            if (pick < 0) return;
            var chosen = snapshots[pick];
            if (!_tracker.ShouldEmit(chosen.Title, chosen.Artist)) return;

            BitmapSource? artwork = props[pick]?.Thumbnail != null ? await TryLoadThumbnailAsync(props[pick]!.Thumbnail) : null;
            TrackChanged?.Invoke(new MediaTrackInfo(chosen.Title, chosen.Artist, artwork));
        }
        catch { /* a transient SMTC read failure just skips this update; the next change retries */ }
    }

    private static T? Safe<T>(Func<T?> f) { try { return f(); } catch { return default; } }

    private static async Task<BitmapSource?> TryLoadThumbnailAsync(IRandomAccessStreamReference reference)
    {
        try
        {
            using IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync();
            using var net = stream.AsStreamForRead();
            var ms = new MemoryStream();
            await net.CopyToAsync(ms);
            ms.Position = 0;
            var bmp = new BitmapImage();
            bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = ms; bmp.EndInit(); bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        Stop();
    }
}
