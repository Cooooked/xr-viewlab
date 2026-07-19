using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace XRViewLab.UI;

// Render-side settings for the desktop-notification mirror.
internal sealed class NotificationSettings
{
    public bool Enabled;
    public double X = 0.98, Y = 0.98, Scale = 1.0, Opacity = 1.0;
    public double DurationMs = 3000;
    public int MaxVisible = 3;
    public int Privacy;          // 0 full, 1 title only, 2 app only
    public int Theme;            // visual design: 0 classic, 1 compact banner, 2 minimal, 3 bold
    public int Palette;          // colours only: 0 graphite, 1 paper, 2 OLED, 3 amber, 4 mint
    public bool ShowIcon = true;
    public bool ShowImage = true;
    public bool AllowlistMode;   // false = blocklist, true = allowlist
    public string[] AppFilters = Array.Empty<string>();
}

// Mirrors supported Windows desktop notifications into the visor.
//
// Everything here runs in the settings-app process, entirely off the game's render thread:
//   * A WinRT UserNotificationListener supplies notification text and the source app icon.
//   * Each card is composited once (icon + title/sender + shortened body + optional thumbnail)
//     into a fixed-size straight-alpha RGBA bitmap on the UI dispatcher.
//   * A bounded background timer advances hold/expiry and writes compact lifecycle timestamps.
//     Pixels are written only when content changes, never for animation frames.
// The native layer performs no collection or decoding; it evaluates fade/slide from those timestamps
// at the game's render cadence.
internal sealed class NotificationService : IDisposable
{
    internal enum ServiceState { Ready, PermissionNotGranted, UnsupportedDeployment, ListenerInitializationFailure, InternalRendererFailure }
    // Must match dllmain.cpp NotifyBlock / NotifyCardBlock.
    private const string MapName = "Local\\XRViewLabNotifications";
    private const uint Magic = 0x314E4C56; // "VLN1"
    private const int MaxCards = 6;
    private const int CardW = 336;
    private const int CardH = 96;
    private const int CardPixels = CardW * CardH * 4;
    private const int CardMetaBytes = 32; // active,id,width,height,enterTick,leaveTick,stackIndex,contentSerial
    private const int CardStride = CardMetaBytes + CardPixels;
    private const int HeaderBytes = 16;
    private const int BlockSize = HeaderBytes + MaxCards * CardStride;

    private const int RiseMs = 250;
    private const int LeaveMs = 400;

    private sealed class Card
    {
        public uint Id;
        public string? SourceKey;
        public byte[] Rgba = Array.Empty<byte>();
        public int Width, Height;
        public uint ContentSerial;
        public long BornTick;      // Environment.TickCount64 at first show
        public long EligibleTick;  // desktop cards start only after racing-attention hold clears
        public long LeaveTick;     // when it started leaving; 0 = not yet
        public bool IsDesktop;
        public bool PixelsDirty = true;
    }

    private readonly Dispatcher _ui;
    private readonly object _lock = new();
    private readonly List<Card> _cards = new();
    private readonly Dictionary<string, uint> _seen = new(StringComparer.Ordinal); // package + listener id -> our card id
    private uint _nextId = 1;
    private uint _generation;

    private MemoryMappedFile? _map;
    private MemoryMappedViewAccessor? _view;
    private Timer? _timer;
    private UserNotificationListener? _listener;
    private volatile NotificationSettings _settings = new();
    private volatile bool _listenerReady;
    private bool _baselineComplete;
    private int _refreshActive;
    private int _refreshFailures;
    private volatile bool _listenerFaulted;
    private long _lastRefreshTick;
    private bool _disposed;
    private volatile bool _racingAttentionActive;

    public string Status { get; private set; } = "Notifications idle.";
    public ServiceState State { get; private set; } = ServiceState.UnsupportedDeployment;
    public event Action? StatusChanged;

    private void SetStatus(ServiceState state, string detail)
    {
        State = state; Status = $"{state}: {detail}"; StatusChanged?.Invoke();
    }

    public NotificationService(Dispatcher uiDispatcher)
    {
        _ui = uiDispatcher;
        try
        {
            _map = MemoryMappedFile.CreateOrOpen(MapName, BlockSize, MemoryMappedFileAccess.ReadWrite);
            _view = _map.CreateViewAccessor(0, BlockSize, MemoryMappedFileAccess.ReadWrite);
            _view.Write(0, Magic); _view.Write(4, 2u); // version 2: native evaluates animation from timestamps
        }
        catch (Exception ex) { SetStatus(ServiceState.InternalRendererFailure, ex.GetType().Name + ": " + ex.Message); }
    }

    // Called whenever settings change. Starts/stops the listener and the animation timer.
    public void Update(NotificationSettings settings, bool requestAccess = false)
    {
        _settings = settings;
        if (settings.Enabled)
        {
            EnsureListener(requestAccess);
            if (_timer == null) _timer = new Timer(_ => Tick(), null, 0, 50);
        }
        else
        {
            _timer?.Dispose(); _timer = null;
            lock (_lock) { _cards.Clear(); _seen.Clear(); _baselineComplete = false; }
            WriteBlock(); // publish an empty block so nothing renders
        }
    }

    private async void EnsureListener(bool requestAccess)
    {
        if (requestAccess) _listenerFaulted = false;
        if (_listenerFaulted) return;
        if (_listenerReady || _listener != null) return;
        try
        {
            _listener = UserNotificationListener.Current;
            var access = _listener.GetAccessStatus();
            if (access != UserNotificationListenerAccessStatus.Allowed && requestAccess)
                access = await _listener.RequestAccessAsync();
            if (access != UserNotificationListenerAccessStatus.Allowed)
            {
                SetStatus(ServiceState.PermissionNotGranted, $"UserNotificationListenerAccessStatus={access}. Open ViewLab and enable notifications to request Windows permission.");
                _listener = null;
                return;
            }
            _listener.NotificationChanged += (s, a) => { _ = RefreshAsync(); };
            _listenerReady = true;
            SetStatus(ServiceState.Ready, $"UserNotificationListenerAccessStatus={access}; listener active.");
            await RefreshAsync(); // first successful snapshot becomes the stale-notification baseline
        }
        catch (Exception ex)
        {
            // Unpackaged Win32 apps frequently cannot use the listener; fail soft.
            int hr = ex.HResult;
            var state = hr == unchecked((int)0x80070490) || hr == unchecked((int)0x80040154)
                ? ServiceState.UnsupportedDeployment : ServiceState.ListenerInitializationFailure;
            SetStatus(state, $"{ex.GetType().Name} (0x{hr:X8}): {ex.Message}. Notification broker package identity is unavailable or damaged.");
            _listener = null;
        }
    }

    public void EnqueueTestNotification()
    {
        if (_view == null) { SetStatus(ServiceState.InternalRendererFailure, "Shared-memory card bridge was not created."); return; }
        var s = _settings;
        if (_timer == null) _timer = new Timer(_ => Tick(), null, 0, 50);
        _ui.Invoke(() =>
        {
            var icon = new DrawingVisual();
            using (var dc = icon.RenderOpen())
            {
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(35,190,170)), null, new Rect(0,0,48,48), 9,9);
                var ft=new FormattedText("V",System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),28,Brushes.White,1.0);
                dc.DrawText(ft,new Point(14,6));
            }
            var bmp=new RenderTargetBitmap(48,48,96,96,PixelFormats.Pbgra32); bmp.Render(icon); bmp.Freeze();
            AddComposedCard(_nextId++, "ViewLab", "Test notification", "Internal queue, shared memory, texture upload, stereo placement and expiry are active.", bmp, s);
        });
        Status = $"{State}: Synthetic notification queued successfully; Windows listener permission is not required for this test.";
        StatusChanged?.Invoke();
    }

    public void EnqueueEvent(ViewLabEvent e)
    {
        if (_view == null) return;
        if (_timer == null) _timer = new Timer(_ => Tick(), null, 0, 50);
        var s=_settings;
        _ui.Invoke(() => AddComposedCard(_nextId++, "ViewLab event", e.Title ?? e.Kind.ToString(), e.Body ?? $"Value {e.Value:0.###}", null, s));
    }

    // Reuses the same shared-memory card pipeline as desktop-notification mirroring: a track
    // change becomes a card with the track title, artist as the body line, and cover art in the
    // icon slot. No transport controls are exposed — this is a passive "now playing" card only.
    public void EnqueueMediaCard(string title, string artist, BitmapSource? artwork)
    {
        if (_view == null) return;
        if (_timer == null) _timer = new Timer(_ => Tick(), null, 0, 50);
        var s = _settings;
        _ui.Invoke(() => AddComposedCard(_nextId++, "Now playing", title, artist, artwork, s));
    }

    private async System.Threading.Tasks.Task RefreshAsync()
    {
        if (_listener == null || Interlocked.Exchange(ref _refreshActive, 1) != 0) return;
        try
        {
        IReadOnlyList<UserNotification> current;
        try { current = await _listener.GetNotificationsAsync(NotificationKinds.Toast); }
        catch (Exception ex)
        {
            if (++_refreshFailures >= 5)
            {
                _listenerFaulted = true;
                _listenerReady = false;
            }
            SetStatus(ServiceState.ListenerInitializationFailure, $"Notification refresh failed: {ex.GetType().Name} (0x{ex.HResult:X8}).");
            return;
        }

        var settings = _settings;
        var currentKeys = new HashSet<string>(current.Select(NotificationKey), StringComparer.Ordinal);
        lock (_lock)
        {
            if (!_baselineComplete)
            {
                foreach (string key in currentKeys) _seen[key] = 0;
                _baselineComplete = true;
                _lastRefreshTick = Environment.TickCount64;
                return;
            }

            foreach (string missing in _seen.Keys.Where(k => !currentKeys.Contains(k)).ToArray())
                _seen.Remove(missing);
            _cards.RemoveAll(c => c.SourceKey != null && !currentKeys.Contains(c.SourceKey));
        }
        foreach (var n in current)
        {
            string key = NotificationKey(n);
            lock (_lock) { if (_seen.ContainsKey(key)) continue; }

            string appName = Safe(() => n.AppInfo?.DisplayInfo?.DisplayName) ?? "Notification";
            if (!PassesFilter(appName, settings)) { lock (_lock) { _seen[key] = 0; } continue; }

            string title = appName, body = string.Empty;
            try
            {
                var binding = n.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
                var texts = binding?.GetTextElements();
                if (texts != null && texts.Count > 0)
                {
                    title = texts[0].Text;
                    body = string.Join("  ", texts.Skip(1).Select(t => t.Text));
                }
            }
            catch { /* keep app-name fallback */ }

            // Privacy shaping.
            if (settings.Privacy == 2) { title = appName; body = string.Empty; }
            else if (settings.Privacy == 1) { body = string.Empty; }

            BitmapSource? icon = null;
            if (settings.ShowIcon) icon = await TryLoadLogoAsync(n);

            uint id;
            lock (_lock) { id = _nextId++; _seen[key] = id; }
            // Composition touches WPF imaging → marshal to the UI dispatcher.
            _ui.Invoke(() => AddComposedCard(id, appName, title, body, icon, settings, key));
        }
        _refreshFailures = 0;
        _lastRefreshTick = Environment.TickCount64;
        }
        catch (Exception ex)
        {
            SetStatus(ServiceState.ListenerInitializationFailure, $"Notification processing failed: {ex.GetType().Name} (0x{ex.HResult:X8}).");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshActive, 0);
        }
    }

    public void SetRacingAttention(bool active)
    {
        if (_racingAttentionActive == active) return;
        _racingAttentionActive = active;
        if (!active)
        {
            long now = Environment.TickCount64;
            lock (_lock)
                foreach (Card card in _cards.Where(c => c.IsDesktop && c.EligibleTick == 0))
                    card.EligibleTick = now;
        }
        WriteBlock();
    }

    private static string NotificationKey(UserNotification n)
    {
        string family = Safe(() => n.AppInfo?.PackageFamilyName) ?? Safe(() => n.AppInfo?.AppUserModelId) ?? "unknown";
        return family + ":" + n.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool PassesFilter(string appName, NotificationSettings s)
    {
        if (s.AppFilters.Length == 0) return true; // no filters configured: allow all apps
        bool listed = s.AppFilters.Any(f => appName.Contains(f, StringComparison.OrdinalIgnoreCase));
        return s.AllowlistMode ? listed : !listed; // allowlist: only listed; blocklist: all but listed
    }

    private async System.Threading.Tasks.Task<BitmapSource?> TryLoadLogoAsync(UserNotification n)
    {
        try
        {
            var logo = n.AppInfo?.DisplayInfo?.GetLogo(new Windows.Foundation.Size(48, 48));
            if (logo == null) return null;
            using var stream = await logo.OpenReadAsync();
            using var net = stream.AsStreamForRead();
            var ms = new MemoryStream();
            await net.CopyToAsync(ms);
            ms.Position = 0;
            var bmp = new BitmapImage();
            bmp.BeginInit(); bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.StreamSource = ms; bmp.EndInit(); bmp.Freeze();
            return bmp;
        }
        catch (Exception ex) { SetStatus(ServiceState.ListenerInitializationFailure, $"Logo decode failed: {ex.GetType().Name}: {ex.Message}"); return null; }
    }

    // Composite one card to straight-alpha RGBA and enqueue it. Runs on the UI dispatcher.
    private void AddComposedCard(uint id, string appName, string title, string body, BitmapSource? icon, NotificationSettings s, string? sourceKey = null)
    {
        var (rgba, w, h) = ComposeCard(appName, title, body, icon, s);
        long now = Environment.TickCount64;
        bool desktop = sourceKey != null;
        var card = new Card { Id = id, SourceKey = sourceKey, Rgba = rgba, Width = w, Height = h, ContentSerial = id,
            BornTick = now, EligibleTick = desktop && _racingAttentionActive ? 0 : now, IsDesktop = desktop, PixelsDirty = true };
        lock (_lock)
        {
            _cards.Add(card);
            // Age out anything beyond a generous backlog cap so memory stays bounded.
            while (_cards.Count > MaxCards * 2) _cards.RemoveAt(0);
        }
        WriteBlock();
    }

    private static (byte[] rgba, int w, int h) ComposeCard(string appName, string title, string body, BitmapSource? icon, NotificationSettings s)
    {
        // Palette controls colours only; Theme is the actual card design (dimensions, corner
        // shape, icon treatment, typography, accent placement, line counts). Every design
        // stays within the fixed CardW x CardH shared-memory slot.
        var palette = Math.Clamp(s.Palette,0,4) switch
        {
            1 => (Color.FromArgb(246,242,239,231),Color.FromRgb(196,128,55),Color.FromRgb(35,31,27),Color.FromRgb(91,82,72),Color.FromArgb(42,35,31,27)),
            2 => (Color.FromArgb(252,0,0,0),Color.FromRgb(255,255,255),Color.FromRgb(255,255,255),Color.FromRgb(176,176,176),Color.FromArgb(55,255,255,255)),
            3 => (Color.FromArgb(244,31,24,15),Color.FromRgb(238,170,64),Color.FromRgb(255,239,206),Color.FromRgb(194,166,121),Color.FromArgb(42,238,170,64)),
            4 => (Color.FromArgb(244,14,27,23),Color.FromRgb(73,201,151),Color.FromRgb(231,250,241),Color.FromRgb(145,187,169),Color.FromArgb(42,73,201,151)),
            _ => (Color.FromArgb(238,20,21,25),Color.FromRgb(60,190,170),Color.FromRgb(244,246,249),Color.FromRgb(142,150,162),Color.FromArgb(28,255,255,255))
        };
        int design = Math.Clamp(s.Theme, 0, 3);
        (int w, int h) = design switch { 1 => (CardW, 48), 2 => (300, 64), _ => (CardW, CardH) };
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var panel = new Rect(0, 0, w, h);
            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
            var boldType = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var bodyType = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var titleBrush = new SolidColorBrush(palette.Item3);
            var bodyBrush = new SolidColorBrush(palette.Item4);
            var culture = System.Globalization.CultureInfo.CurrentUICulture;

            if (design == 1)
            {
                // Compact banner: one thin row, small icon, title and body inline, accent
                // underline along the bottom edge instead of a leading bar.
                dc.PushClip(new RectangleGeometry(panel, 6, 6));
                dc.DrawRectangle(new SolidColorBrush(palette.Item1), null, panel);
                dc.DrawRectangle(new SolidColorBrush(palette.Item2), null, new Rect(0, h - 3, w, 3));
                dc.Pop();
                dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(palette.Item5), 1), new Rect(0.5, 0.5, w - 1, h - 1), 6, 6);
                double x = 12;
                if (icon != null)
                {
                    const double iconSize = 26;
                    double iconY = (h - 3 - iconSize) / 2;
                    dc.PushClip(new RectangleGeometry(new Rect(x, iconY, iconSize, iconSize), 5, 5));
                    dc.DrawImage(icon, new Rect(x, iconY, iconSize, iconSize));
                    dc.Pop();
                    x += iconSize + 10;
                }
                var bannerTitle = new FormattedText(Shorten(title, 26), culture, FlowDirection.LeftToRight,
                    typeface, 13.5, titleBrush, 1.0) { MaxLineCount = 1, Trimming = System.Windows.TextTrimming.CharacterEllipsis };
                dc.DrawText(bannerTitle, new Point(x, (h - 3 - bannerTitle.Height) / 2));
                double bodyX = x + Math.Min(bannerTitle.Width, bannerTitle.MaxTextWidth <= 0 ? bannerTitle.Width : bannerTitle.MaxTextWidth) + 10;
                if (!string.IsNullOrEmpty(body) && bodyX < w - 40)
                {
                    var bannerBody = new FormattedText(Shorten(body, 60), culture, FlowDirection.LeftToRight,
                        bodyType, 12, bodyBrush, 1.0) { MaxTextWidth = w - bodyX - 12, MaxLineCount = 1, Trimming = System.Windows.TextTrimming.CharacterEllipsis };
                    dc.DrawText(bannerBody, new Point(bodyX, (h - 3 - bannerBody.Height) / 2));
                }
            }
            else if (design == 2)
            {
                // Minimal: square corners, no icon, no filled accent bar — a hairline frame
                // with a short accent tick, lighter type and tighter spacing.
                dc.DrawRectangle(new SolidColorBrush(palette.Item1), null, panel);
                dc.DrawRectangle(null, new Pen(new SolidColorBrush(palette.Item5), 1), new Rect(0.5, 0.5, w - 1, h - 1));
                dc.DrawRectangle(new SolidColorBrush(palette.Item2), null, new Rect(0, 10, 2, 16));
                var minTitle = new FormattedText(Shorten(title, 40), culture, FlowDirection.LeftToRight,
                    bodyType, 13, titleBrush, 1.0) { MaxTextWidth = w - 26, MaxLineCount = 1, Trimming = System.Windows.TextTrimming.CharacterEllipsis };
                dc.DrawText(minTitle, new Point(14, 10));
                if (!string.IsNullOrEmpty(body))
                {
                    var minBody = new FormattedText(Shorten(body, 90), culture, FlowDirection.LeftToRight,
                        bodyType, 11, bodyBrush, 1.0) { MaxTextWidth = w - 26, MaxLineCount = 2, Trimming = System.Windows.TextTrimming.CharacterEllipsis, LineHeight = 13.5 };
                    dc.DrawText(minBody, new Point(14, 30));
                }
            }
            else if (design == 3)
            {
                // Bold: strong top accent band with the app name inside it, large icon, bold
                // title, single body line and generous padding.
                dc.PushClip(new RectangleGeometry(panel, 14, 14));
                dc.DrawRectangle(new SolidColorBrush(palette.Item1), null, panel);
                dc.DrawRectangle(new SolidColorBrush(palette.Item2), null, new Rect(0, 0, w, 20));
                dc.Pop();
                dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(palette.Item5), 1), new Rect(0.5, 0.5, w - 1, h - 1), 14, 14);
                var band = new FormattedText(Shorten(appName, 32).ToUpperInvariant(), culture, FlowDirection.LeftToRight,
                    boldType, 10.5, new SolidColorBrush(palette.Item1), 1.0) { MaxTextWidth = w - 32, MaxLineCount = 1, Trimming = System.Windows.TextTrimming.CharacterEllipsis };
                dc.DrawText(band, new Point(16, 4));
                double x = 16;
                if (icon != null)
                {
                    const double iconSize = 52;
                    double iconY = 20 + (h - 20 - iconSize) / 2;
                    dc.PushClip(new RectangleGeometry(new Rect(x, iconY, iconSize, iconSize), 8, 8));
                    dc.DrawImage(icon, new Rect(x, iconY, iconSize, iconSize));
                    dc.Pop();
                    x += iconSize + 14;
                }
                var boldTitle = new FormattedText(Shorten(title, 34), culture, FlowDirection.LeftToRight,
                    boldType, 17, titleBrush, 1.0) { MaxTextWidth = w - x - 16, MaxLineCount = 1, Trimming = System.Windows.TextTrimming.CharacterEllipsis };
                dc.DrawText(boldTitle, new Point(x, 32));
                if (!string.IsNullOrEmpty(body))
                {
                    var boldBody = new FormattedText(Shorten(body, 70), culture, FlowDirection.LeftToRight,
                        bodyType, 12.5, bodyBrush, 1.0) { MaxTextWidth = w - x - 16, MaxLineCount = 1, Trimming = System.Windows.TextTrimming.CharacterEllipsis };
                    dc.DrawText(boldBody, new Point(x, 60));
                }
            }
            else
            {
                // Classic: dim neutral panel, slim leading accent, icon plus stacked
                // title/body — unchanged from the historical card.
                dc.PushClip(new RectangleGeometry(panel, 10, 10));
                dc.DrawRectangle(new SolidColorBrush(palette.Item1), null, panel);
                dc.DrawRectangle(new SolidColorBrush(palette.Item2), null, new Rect(0, 0, 3, h));
                dc.Pop();
                dc.DrawRoundedRectangle(null, new Pen(new SolidColorBrush(palette.Item5), 1), new Rect(0.5, 0.5, w - 1, h - 1), 10, 10);

                const double iconSize = 44;
                double x = 16;
                if (icon != null)
                {
                    double iconY = (h - iconSize) / 2;
                    dc.PushClip(new RectangleGeometry(new Rect(x, iconY, iconSize, iconSize), 6, 6));
                    dc.DrawImage(icon, new Rect(x, iconY, iconSize, iconSize));
                    dc.Pop();
                    x += iconSize + 12;
                }

                var t = new FormattedText(Shorten(title, 42), culture,
                    FlowDirection.LeftToRight, typeface, 15.5, titleBrush, 1.0) { MaxTextWidth = w - x - 14, MaxLineCount = 1, Trimming = System.Windows.TextTrimming.CharacterEllipsis };
                dc.DrawText(t, new Point(x, 22));

                if (!string.IsNullOrEmpty(body))
                {
                    var b = new FormattedText(Shorten(body, 120), culture,
                        FlowDirection.LeftToRight, bodyType, 12, bodyBrush, 1.0) { MaxTextWidth = w - x - 14, MaxLineCount = 2, Trimming = System.Windows.TextTrimming.CharacterEllipsis, LineHeight = 15 };
                    dc.DrawText(b, new Point(x, 46));
                }
            }
        }

        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var pbgra = new byte[w * h * 4];
        rtb.CopyPixels(pbgra, w * 4, 0);

        // Convert premultiplied BGRA -> straight RGBA for the textured pixel shader.
        var rgba = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            byte bl = pbgra[i * 4 + 0], gr = pbgra[i * 4 + 1], re = pbgra[i * 4 + 2], al = pbgra[i * 4 + 3];
            if (al > 0)
            {
                re = (byte)Math.Min(255, re * 255 / al);
                gr = (byte)Math.Min(255, gr * 255 / al);
                bl = (byte)Math.Min(255, bl * 255 / al);
            }
            rgba[i * 4 + 0] = re; rgba[i * 4 + 1] = gr; rgba[i * 4 + 2] = bl; rgba[i * 4 + 3] = al;
        }
        return (rgba, w, h);
    }

    private static string Shorten(string s, int max)
    {
        s = (s ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }

    // Advance queue lifecycle. Native evaluates the timestamped animation every rendered frame.
    private void Tick()
    {
        var s = _settings;
        long now = Environment.TickCount64;
        if (_listenerReady && now - Interlocked.Read(ref _lastRefreshTick) >= 2000)
            _ = RefreshAsync(); // bounded safety poll in case NotificationChanged is missed
        bool changed = false;
        lock (_lock)
        {
            int max = Math.Clamp(s.MaxVisible, 1, MaxCards);
            // The newest `max` cards are visible; older ones wait (independent expiry).
            var ordered = _cards.OrderBy(c => c.BornTick).ToList();
            var toRemove = new List<Card>();
            foreach (var c in ordered)
            {
                if (c.IsDesktop && _racingAttentionActive)
                {
                    if (now - c.BornTick >= 5000) toRemove.Add(c);
                    continue;
                }
                if (c.EligibleTick == 0) { c.EligibleTick = now; changed = true; }
                long age = now - c.EligibleTick;
                if (c.LeaveTick == 0 && age >= RiseMs + (long)s.DurationMs) { c.LeaveTick = now; changed = true; }
                if (c.LeaveTick != 0 && now - c.LeaveTick >= LeaveMs)
                {
                    toRemove.Add(c);
                }
            }
            foreach (var c in toRemove) { _cards.Remove(c); changed = true; }
        }
        if (changed) WriteBlock();
    }

    private void WriteBlock()
    {
        if (_view == null) return;
        var s = _settings;
        List<Card> visible;
        lock (_lock)
        {
            int max = Math.Clamp(s.MaxVisible, 1, MaxCards);
            var ordered = _cards.Where(c => !(c.IsDesktop && _racingAttentionActive) && c.EligibleTick != 0).OrderBy(c => c.EligibleTick).ToList();
            visible = ordered.Skip(Math.Max(0, ordered.Count - max)).ToList();
        }

        // Clear all slot 'active' flags first.
        for (int i = 0; i < MaxCards; i++) _view.Write(HeaderBytes + i * CardStride + 0, 0u);

        uint slot = 0;
        foreach (var c in visible.OrderByDescending(c => c.BornTick)) // newest at bottom (stackIndex 0)
        {
            if (slot >= MaxCards) break;
            int baseOff = HeaderBytes + (int)slot * CardStride;
            _view.Write(baseOff + 0, 1u);
            _view.Write(baseOff + 4, c.Id);
            _view.Write(baseOff + 8, (uint)c.Width);
            _view.Write(baseOff + 12, (uint)c.Height);
            _view.Write(baseOff + 16, unchecked((uint)c.EligibleTick));
            _view.Write(baseOff + 20, unchecked((uint)c.LeaveTick));
            _view.Write(baseOff + 24, slot); // stackIndex
            _view.Write(baseOff + 28, c.ContentSerial);
            if (c.PixelsDirty && c.Rgba.Length == CardPixels)
            {
                _view.WriteArray(baseOff + CardMetaBytes, c.Rgba, 0, CardPixels);
                c.PixelsDirty = false;
            }
            slot++;
        }
        _view.Write(8, (uint)visible.Count);
        Thread.MemoryBarrier();
        _view.Write(12, unchecked(++_generation));
    }

    private static T? Safe<T>(Func<T?> f) { try { return f(); } catch { return default; } }

    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        _timer?.Dispose();
        _view?.Dispose();
        _map?.Dispose();
    }
}
