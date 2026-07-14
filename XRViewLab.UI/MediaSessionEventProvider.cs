using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace XRViewLab.UI;

internal readonly record struct MediaTrackInfo(string Title, string Artist, BitmapSource? Artwork);

// Watches the OS-wide "Now Playing" session (Windows System Media Transport Controls — the same
// source Windows itself uses for the volume-flyout media card) and raises TrackChanged only when
// the title/artist actually changes, so pause/seek/volume events on an unchanged track don't
// repeat the visor card. A machine with no SMTC-reporting player simply never raises the event —
// there is no polling fallback and no error surfaced to the user for that case.
internal sealed class MediaSessionEventProvider : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string? _lastKey;
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
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            AttachCurrentSession();
            Status = "Media session watcher active.";
        }
        catch (Exception ex)
        {
            Status = $"Media session watcher unavailable: {ex.GetType().Name}: {ex.Message}";
            _started = false;
        }
    }

    public void Stop()
    {
        if (_session != null) { _session.MediaPropertiesChanged -= OnPropertiesChanged; _session = null; }
        if (_manager != null) _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        _manager = null;
        _lastKey = null;
        _started = false;
        Status = "Media session watcher idle.";
    }

    private void AttachCurrentSession()
    {
        if (_session != null) _session.MediaPropertiesChanged -= OnPropertiesChanged;
        _session = _manager?.GetCurrentSession();
        if (_session != null)
        {
            _session.MediaPropertiesChanged += OnPropertiesChanged;
            _ = RefreshAsync();
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args) => AttachCurrentSession();

    private void OnPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        var session = _session;
        if (session == null) return;
        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            string title = props.Title ?? string.Empty;
            string artist = props.Artist ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title)) return; // nothing playing, or the app reports no metadata
            string key = TrackKey(title, artist);
            if (key == _lastKey) return;
            _lastKey = key;

            BitmapSource? artwork = props.Thumbnail != null ? await TryLoadThumbnailAsync(props.Thumbnail) : null;
            TrackChanged?.Invoke(new MediaTrackInfo(title, artist, artwork));
        }
        catch { /* a transient SMTC read failure just skips this update; the next change retries */ }
    }

    // Exposed for fixture testing of the dedup rule without a live SMTC session.
    internal static string TrackKey(string title, string artist) => title.Trim() + "␟" + artist.Trim();

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
