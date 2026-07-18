using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace XRViewLab.UI;

// Pop-out ReShade control panel (formerly the standalone ReshadeAIORemote). Opened from a button in
// ViewLab. Drives the DLL live via ReShadeControlService (Local\ReShadeXRControl). Red ViewLab theme.
public sealed class ReShadeRemoteWindow : Window
{
    public delegate void Mod(ref ReShadeControlService.XRControlBlock b);

    static Brush B(string hex) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)!);
    static readonly Brush Shell = B("#171819"), Panel = B("#101112"), Border_ = B("#2B2D31");
    static readonly Brush Muted = B("#9A9A9A"), Head = B("#C9C9C9"), Text = B("#E8E8E8");
    static readonly Brush Red = B("#C90012"), RedBorder = B("#EC3038"), RedHover = B("#E11421");
    static readonly Brush Green = B("#7FB069"), Amber = B("#E0A33A");

    const string QuadIni = @"C:\ProgramData\ReShade\openxr_quad_transform.ini";
    const string PayloadDirectory = @"C:\ProgramData\ReShade";
    const string PayloadDll = @"C:\ProgramData\ReShade\ReShade64.dll";
    const string PayloadManifest = @"C:\ProgramData\ReShade\ReShade64_XR.json";
    const string LayerRegistryPath = @"HKLM:\SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit";

    readonly ReShadeControlService _svc = new();
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    bool _applying;
    uint _lastHb; DateTime _lastHbChange = DateTime.MinValue; bool _handshakeBaselineSet;
    uint _lastAppliedRevision = uint.MaxValue;

    TextBlock _status = null!, _setup = null!;
    Border _gameplay = null!, _tuning = null!, _btnInstall = null!, _btnEnable = null!, _btnReposition = null!, _btnTransform = null!;
    CheckBox _menu = null!, _headless = null!, _onTop = null!;
    Window? _reposWin, _transWin;

    public ReShadeRemoteWindow()
    {
        Title = "Advanced: ReShade Remote";
        Width = 340;
        SizeToContent = SizeToContent.Height;
        MaxHeight = Math.Max(320, SystemParameters.WorkArea.Height - 32);
        WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; Foreground = Text;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;
        Content = BuildRoot();
        Loaded += (_, _) => { AttachControlChannel(); _timer.Tick += OnTick; _timer.Start(); OnTick(null, EventArgs.Empty); };
        Closed += (_, _) => { _timer.Stop(); _reposWin?.Close(); _transWin?.Close(); _svc.Dispose(); };
    }

    void Mutate(Mod m)
    {
        if (!_svc.Connected) return;
        var b = _svc.ReadBlock();
        m(ref b);
        _svc.WriteBlock(ref b);
    }

    void AttachControlChannel()
    {
        if (!_svc.TryConnect()) return;
        _lastHb = _svc.ReadBlock().heartbeat;
        _lastHbChange = DateTime.MinValue;
        _handshakeBaselineSet = true;
    }

    UIElement BuildRoot()
    {
        var root = new Border { CornerRadius = new CornerRadius(10), Background = Shell, BorderBrush = Border_, BorderThickness = new Thickness(1) };
        var dock = new DockPanel();
        dock.Children.Add(TitleBar());
        var body = new StackPanel { Margin = new Thickness(16, 2, 16, 14) };

        _status = new TextBlock { Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 0, 0, 8) };
        body.Children.Add(_status);
        _setup = new TextBlock { Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
        body.Children.Add(_setup);

        body.Children.Add(Header("SETUP"));
        _btnInstall = RedButton("Install ReShade", ToggleInstallPayload);
        _btnEnable = RedButton("Enable ReShade", TogglePayloadEnabled);
        body.Children.Add(Row(_btnInstall, _btnEnable));

        body.Children.Add(Header("MODE"));
        _gameplay = Chip("Gameplay", () => SetMode(0));
        _tuning = Chip("Tuning", () => SetMode(1));
        body.Children.Add(Row(_gameplay, _tuning));

        body.Children.Add(Header("DESKTOP MENU"));
        _menu = Check("Show desktop menu / overlay", v => Mutate((ref ReShadeControlService.XRControlBlock b) => b.menu_visible = v));
        body.Children.Add(_menu);

        body.Children.Add(Header("DESKTOP MENU WINDOW"));
        _headless = Check("Borderless", v => Mutate((ref ReShadeControlService.XRControlBlock b) => b.win_headless = v));
        _onTop = Check("Always on top", v => Mutate((ref ReShadeControlService.XRControlBlock b) => b.win_always_on_top = v));
        body.Children.Add(_headless);
        body.Children.Add(_onTop);

        body.Children.Add(Header("IN-HMD MENU QUAD"));
        _btnReposition = RedButton("Reposition", ToggleRepos);
        _btnTransform = RedButton("Transform", ToggleTrans);
        body.Children.Add(Row(_btnReposition, _btnTransform));

        dock.Children.Add(new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        });
        root.Child = dock;
        return root;
    }

    UIElement TitleBar()
    {
        var bar = new Grid { Height = 42, Background = Brushes.Transparent };
        bar.ColumnDefinitions.Add(new ColumnDefinition());
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        DockPanel.SetDock(bar, Dock.Top);
        var t = new TextBlock { Text = "ADVANCED  RESHADE  REMOTE", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Text, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        Grid.SetColumn(t, 0);
        var help = HelpIcon(OpenReShadeHelp);
        Grid.SetColumn(help, 1);
        var x = new TextBlock { Text = "X", FontSize = 14, Foreground = Muted, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center, Padding = new Thickness(14, 8, 16, 8) };
        x.MouseLeftButtonDown += (_, e) => { e.Handled = true; Close(); };
        x.MouseEnter += (_, _) => x.Foreground = Red; x.MouseLeave += (_, _) => x.Foreground = Muted;
        Grid.SetColumn(x, 2);
        bar.Children.Add(t); bar.Children.Add(help); bar.Children.Add(x);
        bar.MouseLeftButtonDown += (_, e) => { if (!ReferenceEquals(e.OriginalSource, x) && !help.IsMouseOver && e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } } };
        return bar;
    }

    Border HelpIcon(Action onClick)
    {
        var icon = new Border { Width = 25, Height = 25, CornerRadius = new CornerRadius(13), BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.5), Background = Brushes.Transparent, Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center, ToolTip = "About Advanced ReShade" };
        icon.Child = new TextBlock { Text = "?", Foreground = Brushes.White, FontWeight = FontWeights.Bold,
            FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        icon.MouseEnter += (_, _) => icon.Background = B("#34363B");
        icon.MouseLeave += (_, _) => icon.Background = Brushes.Transparent;
        icon.MouseLeftButtonDown += (_, e) => { e.Handled = true; icon.Background = Red; };
        icon.MouseLeftButtonUp += (_, e) => { e.Handled = true; icon.Background = B("#34363B"); onClick(); };
        return icon;
    }

    void OpenReShadeHelp() => BuiltInHelpWindow.Show(this, "About Advanced ReShade", BuiltInHelpWindow.ReShadeSections);

    UIElement Header(string s) => new TextBlock { Text = s, Foreground = Head, FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 11, 0, 5) };

    static UIElement Row(params UIElement[] items)
    {
        var g = new Grid();
        for (int i = 0; i < items.Length; i++)
        {
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var e = (FrameworkElement)items[i];
            e.Margin = new Thickness(i == 0 ? 0 : 3, 0, i == items.Length - 1 ? 0 : 3, 0);
            Grid.SetColumn(e, i); g.Children.Add(e);
        }
        return g;
    }

    Border Chip(string text, Action onClick)
    {
        var c = new Border { Background = Panel, BorderBrush = Border_, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Cursor = Cursors.Hand, Margin = new Thickness(0, 6, 0, 0) };
        c.Child = new TextBlock { Text = text, Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(0, 8, 0, 8) };
        c.MouseLeftButtonUp += (_, _) => onClick();
        c.MouseEnter += (_, _) => { if (c.Tag as string != "on") c.BorderBrush = B("#3A3D42"); };
        c.MouseLeave += (_, _) => { if (c.Tag as string != "on") c.BorderBrush = Border_; };
        return c;
    }

    static void SetChip(Border c, bool on)
    {
        c.Tag = on ? "on" : null;
        c.Background = on ? B("#C90012") : B("#101112");
        c.BorderBrush = on ? B("#EC3038") : B("#2B2D31");
        var tb = (TextBlock)c.Child; tb.Foreground = on ? B("#FFFFFF") : B("#9A9A9A"); tb.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
    }

    Border RedButton(string text, Action onClick)
    {
        var b = new Border { Background = Red, BorderBrush = RedBorder, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Cursor = Cursors.Hand, Margin = new Thickness(0, 6, 0, 0) };
        b.Child = new TextBlock { Text = text, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(0, 8, 0, 8) };
        b.MouseLeftButtonUp += (_, _) => { if (b.IsEnabled) onClick(); };
        b.MouseEnter += (_, _) => { if (b.IsEnabled) b.Background = RedHover; };
        b.MouseLeave += (_, _) => { if (b.IsEnabled) b.Background = Red; };
        return b;
    }

    static void SetButtonEnabled(Border b, bool enabled)
    {
        b.IsEnabled = enabled;
        b.Opacity = enabled ? 1.0 : 0.45;
        b.Cursor = enabled ? Cursors.Hand : Cursors.Arrow;
    }

    Border PadButton(string text, Action onClick)
    {
        var b = new Border { Background = B("#1B1C1E"), BorderBrush = Border_, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Cursor = Cursors.Hand, Margin = new Thickness(3) };
        b.Child = new TextBlock { Text = text, Foreground = Text, HorizontalAlignment = HorizontalAlignment.Center, Padding = new Thickness(0, 7, 0, 7), FontSize = 13 };
        b.MouseLeftButtonUp += (_, _) => onClick();
        b.MouseEnter += (_, _) => b.Background = B("#C90012"); b.MouseLeave += (_, _) => b.Background = B("#1B1C1E");
        return b;
    }

    CheckBox Check(string text, Action<uint> onToggle)
    {
        var cb = new CheckBox { Content = text, Foreground = Text, Margin = new Thickness(0, 7, 0, 0), VerticalContentAlignment = VerticalAlignment.Center };
        cb.Checked += (_, _) => { if (!_applying) onToggle(1); };
        cb.Unchecked += (_, _) => { if (!_applying) onToggle(0); };
        return cb;
    }

    void SetMode(int mode)
    {
        Mutate((ref ReShadeControlService.XRControlBlock b) => b.xr_mode = (uint)mode);
        SetChip(_gameplay, mode == 0); SetChip(_tuning, mode == 1);
    }

    void EnsureConcrete(ref ReShadeControlService.XRControlBlock b)
    {
        if (!float.IsNaN(b.quad_pos_z)) return;
        var s = ReadIniSeed();
        b.quad_pos_x = s.px; b.quad_pos_y = s.py; b.quad_pos_z = s.pz;
        b.quad_quat_x = s.qx; b.quad_quat_y = s.qy; b.quad_quat_z = s.qz; b.quad_quat_w = s.qw;
        b.quad_width = s.w; b.quad_height = s.h; b.quad_alpha = s.a;
    }

    (float px, float py, float pz, float qx, float qy, float qz, float qw, float w, float h, float a) ReadIniSeed()
    {
        float px = -0.32f, py = -0.16f, pz = 0.81f, qx = 0, qy = 0, qz = 0, qw = 1, w = 0.32f, h = 0.30f, a = 1f;
        try
        {
            if (File.Exists(QuadIni))
                foreach (var line in File.ReadAllLines(QuadIni))
                {
                    var i = line.IndexOf('='); if (i <= 0) continue;
                    var k = line[..i].Trim(); var v = line[(i + 1)..].Trim();
                    if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)) continue;
                    switch (k) { case "pos_x": px = f; break; case "pos_y": py = f; break; case "pos_z": pz = f; break;
                        case "quat_x": qx = f; break; case "quat_y": qy = f; break; case "quat_z": qz = f; break; case "quat_w": qw = f; break;
                        case "width": w = f; break; case "height": h = f; break; case "alpha": a = f; break; }
                }
        }
        catch { }
        return (px, py, pz, qx, qy, qz, qw, w, h, a);
    }

    void NudgePos(float dx, float dy, float dz) => Mutate((ref ReShadeControlService.XRControlBlock b) => { EnsureConcrete(ref b); b.quad_pos_x += dx; b.quad_pos_y += dy; b.quad_pos_z += dz; });
    void ScaleQuad(float f) => Mutate((ref ReShadeControlService.XRControlBlock b) => { EnsureConcrete(ref b); b.quad_width *= f; b.quad_height *= f; });
    void AlphaQuad(float d) => Mutate((ref ReShadeControlService.XRControlBlock b) => { EnsureConcrete(ref b); b.quad_alpha = Math.Clamp(b.quad_alpha + d, 0f, 1f); });
    void RotateQuad(char axis, float deg) => Mutate((ref ReShadeControlService.XRControlBlock b) =>
    {
        EnsureConcrete(ref b);
        double r = deg * Math.PI / 180.0, s = Math.Sin(r / 2), c = Math.Cos(r / 2);
        double dx = axis == 'x' ? s : 0, dy = axis == 'y' ? s : 0, dz = axis == 'z' ? s : 0, dw = c;
        double x = b.quad_quat_x, y = b.quad_quat_y, z = b.quad_quat_z, w = b.quad_quat_w;
        double nx = w * dx + x * dw + y * dz - z * dy;
        double ny = w * dy - x * dz + y * dw + z * dx;
        double nz = w * dz + x * dy - y * dx + z * dw;
        double nw = w * dw - x * dx - y * dy - z * dz;
        double n = Math.Sqrt(nx * nx + ny * ny + nz * nz + nw * nw); if (n < 1e-9) n = 1;
        b.quad_quat_x = (float)(nx / n); b.quad_quat_y = (float)(ny / n); b.quad_quat_z = (float)(nz / n); b.quad_quat_w = (float)(nw / n);
    });
    void ResetQuad() => Mutate((ref ReShadeControlService.XRControlBlock b) => b.quad_pos_z = float.NaN);

    void ToggleRepos() { if (_reposWin is { IsVisible: true }) _reposWin.Hide(); else { (_reposWin ??= BuildReposWin()).Show(); PlaceSidecar(_reposWin); } }
    void ToggleTrans() { if (_transWin is { IsVisible: true }) _transWin.Hide(); else { (_transWin ??= BuildTransWin()).Show(); PlaceSidecar(_transWin); } }
    void PlaceSidecar(Window w) { w.Left = Left + Width + 8; w.Top = Top; }

    Window Sidecar(string title, UIElement content)
    {
        var w = new Window { Title = title, Width = 232, SizeToContent = SizeToContent.Height, WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false, Owner = this, ResizeMode = ResizeMode.NoResize, FontFamily = new FontFamily("Segoe UI"), FontSize = 14 };
        var root = new Border { CornerRadius = new CornerRadius(10), Background = Shell, BorderBrush = Border_, BorderThickness = new Thickness(1) };
        var sp = new StackPanel { Margin = new Thickness(14, 10, 14, 14) };
        var bar = new Grid();
        bar.ColumnDefinitions.Add(new ColumnDefinition());
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var tt = new TextBlock { Text = title, Foreground = Head, FontSize = 10, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var cx = new TextBlock { Text = "X", Foreground = Muted, Cursor = Cursors.Hand, Padding = new Thickness(8, 2, 2, 2) };
        cx.MouseLeftButtonDown += (_, e) => { e.Handled = true; w.Hide(); }; Grid.SetColumn(cx, 1);
        bar.Children.Add(tt); bar.Children.Add(cx);
        bar.MouseLeftButtonDown += (_, e) => { if (!ReferenceEquals(e.OriginalSource, cx) && e.ButtonState == MouseButtonState.Pressed) { try { w.DragMove(); } catch { } } };
        sp.Children.Add(bar); sp.Children.Add(content);
        root.Child = sp; w.Content = root;
        return w;
    }

    static UIElement Grid3(params UIElement?[] cells)
    {
        var g = new Grid();
        for (int c = 0; c < 3; c++) g.ColumnDefinitions.Add(new ColumnDefinition());
        for (int i = 0; i < cells.Length; i++)
        {
            int r = i / 3;
            while (g.RowDefinitions.Count <= r) g.RowDefinitions.Add(new RowDefinition());
            if (cells[i] == null) continue;
            Grid.SetRow(cells[i], r); Grid.SetColumn(cells[i], i % 3); g.Children.Add(cells[i]);
        }
        return g;
    }

    Window BuildReposWin()
    {
        const float step = 0.03f;
        var pad = Grid3(
            null, PadButton("Up", () => NudgePos(0, step, 0)), null,
            PadButton("Left", () => NudgePos(-step, 0, 0)), PadButton("Down", () => NudgePos(0, -step, 0)), PadButton("Right", () => NudgePos(step, 0, 0)),
            PadButton("Closer", () => NudgePos(0, 0, -step)), null, PadButton("Farther", () => NudgePos(0, 0, step))
        );
        var sp = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(pad);
        sp.Children.Add(RedButton("Reset position", ResetQuad));
        return Sidecar("REPOSITION  (3 cm / press)", sp);
    }

    Window BuildTransWin()
    {
        var sp = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        sp.Children.Add(SubHead("SIZE"));
        sp.Children.Add(Row(PadButton("- smaller", () => ScaleQuad(0.92f)), PadButton("+ bigger", () => ScaleQuad(1.08f))));
        sp.Children.Add(SubHead("ROTATE  (5 deg / press)"));
        sp.Children.Add(Grid3(
            PadButton("Yaw -", () => RotateQuad('y', -5)), PadButton("Pitch -", () => RotateQuad('x', -5)), PadButton("Roll -", () => RotateQuad('z', -5)),
            PadButton("Yaw +", () => RotateQuad('y', 5)), PadButton("Pitch +", () => RotateQuad('x', 5)), PadButton("Roll +", () => RotateQuad('z', 5))
        ));
        sp.Children.Add(SubHead("OPACITY"));
        sp.Children.Add(Row(PadButton("-", () => AlphaQuad(-0.1f)), PadButton("+", () => AlphaQuad(0.1f))));
        sp.Children.Add(RedButton("Reset transform", ResetQuad));
        return Sidecar("TRANSFORM", sp);
    }

    UIElement SubHead(string s) => new TextBlock { Text = s, Foreground = Muted, FontSize = 9, FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 9, 0, 2) };

    static string? PayloadRoot()
    {
        try
        {
            // The app is published single-file with IncludeAllContentForSelfExtract, so
            // AppContext.BaseDirectory is a %TEMP% extraction folder — NOT the install
            // directory. Environment.ProcessPath is the real exe location; check it first.
            string? exeDir = Path.GetDirectoryName(Environment.ProcessPath);
            foreach (string? root in new[] { exeDir, AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                if (string.IsNullOrEmpty(root)) continue;
                string candidate = Path.Combine(root, "ReShadePayload");
                if (File.Exists(Path.Combine(candidate, "ReShade64.dll")) &&
                    File.Exists(Path.Combine(candidate, "ReShade64_XR.json")))
                {
                    return candidate;
                }
            }
        }
        catch
        {
        }
        return null;
    }

    static bool HasBundledPayload() => PayloadRoot() != null;

    static string PsQuote(string value) => "'" + value.Replace("'", "''") + "'";

    static bool IsPayloadInstalled() => File.Exists(PayloadDll) && File.Exists(PayloadManifest);

    static bool IsPayloadEnabled()
    {
        try
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using RegistryKey? key = baseKey.OpenSubKey(@"SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit", false);
            return key?.GetValue(PayloadManifest) is int value && value == 0;
        }
        catch { return false; }
    }

    static void SetButtonText(Border button, string text) => ((TextBlock)button.Child).Text = text;

    bool RunElevated(string command, string failed, string cancelled)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -EncodedCommand " +
                Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop';" + command)),
            UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden
        };
        try
        {
            using Process? process = Process.Start(psi);
            if (process == null) { _setup.Text = failed; return false; }
            process.WaitForExit();
            if (process.ExitCode == 0) return true;
            _setup.Text = failed;
        }
        catch { _setup.Text = cancelled; }
        return false;
    }

    void ToggleInstallPayload()
    {
        if (IsPayloadInstalled())
        {
            string remove =
                "Remove-Item -LiteralPath " + PsQuote(PayloadDll) + " -Force -ErrorAction SilentlyContinue;" +
                "Remove-Item -LiteralPath " + PsQuote(PayloadManifest) + " -Force -ErrorAction SilentlyContinue;";
            if (RunElevated(remove, "Uninstall failed. Close any game using the payload and try again.",
                "Uninstall cancelled. Windows permission is required to remove ViewLab's payload files."))
                _setup.Text = "Payload files removed. OpenXR layer registration was not changed.";
            OnTick(null, EventArgs.Empty);
            return;
        }

        string? payload = PayloadRoot();
        if (payload == null)
        {
            _setup.Text = "Unavailable: this ViewLab build does not include the ReShade Remote component.";
            return;
        }

        string command = "$src=" + PsQuote(payload) + ";" +
            "$dest='C:\\ProgramData\\ReShade';" +
            "New-Item -ItemType Directory -Force -Path $dest | Out-Null;" +
            "Copy-Item -LiteralPath (Join-Path $src 'ReShade64.dll') -Destination (Join-Path $dest 'ReShade64.dll') -Force;" +
            "Copy-Item -LiteralPath (Join-Path $src 'ReShade64_XR.json') -Destination (Join-Path $dest 'ReShade64_XR.json') -Force;";
        if (RunElevated(command, "Install failed. Close any game using the payload and try again.",
            "Install cancelled. Windows permission is required to copy ViewLab's payload files."))
            _setup.Text = "Payload files installed. Enable the ViewLab layer when required.";
        OnTick(null, EventArgs.Empty);
    }

    void TogglePayloadEnabled()
    {
        bool enabled = IsPayloadEnabled();
        string command = enabled
            ? "$key=" + PsQuote(LayerRegistryPath) + ";$json=" + PsQuote(PayloadManifest) + ";if(Test-Path $key){Remove-ItemProperty -Path $key -Name $json -ErrorAction SilentlyContinue};"
            : "$key=" + PsQuote(LayerRegistryPath) + ";$json=" + PsQuote(PayloadManifest) + ";if(-not(Test-Path $key)){New-Item -Path $key | Out-Null};New-ItemProperty -Path $key -Name $json -PropertyType DWord -Value 0 -Force | Out-Null;";
        string verb = enabled ? "Disable" : "Enable";
        if (RunElevated(command, verb + " failed. No other OpenXR registration was changed.",
            verb + " cancelled. Windows permission is required to change ViewLab's registration."))
            _setup.Text = enabled ? "ViewLab layer deregistered; payload files remain installed."
                                  : "ViewLab layer registered and enabled; payload files were not changed.";
        OnTick(null, EventArgs.Empty);
    }

    void UpdateSetupText(bool live)
    {
        bool hasPayload = HasBundledPayload();
        bool installed = IsPayloadInstalled();
        bool enabled = IsPayloadEnabled();

        // Controls are always usable once a payload is present. The remote should behave like a TV
        // remote: buttons can be pressed even if the game is not currently running. Settings apply
        // when the game starts.
        SetButtonText(_btnInstall, installed ? "Uninstall ReShade" : "Install ReShade");
        SetButtonText(_btnEnable, enabled ? "Disable ReShade" : "Enable ReShade");
        SetButtonEnabled(_btnInstall, (hasPayload || installed) && !live);
        SetButtonEnabled(_btnEnable, installed || enabled);
        SetButtonEnabled(_gameplay, hasPayload);
        SetButtonEnabled(_tuning, hasPayload);
        SetButtonEnabled(_btnReposition, hasPayload);
        SetButtonEnabled(_btnTransform, hasPayload);
        _menu.IsEnabled = hasPayload;
        _headless.IsEnabled = hasPayload;
        _onTop.IsEnabled = hasPayload;

        if (live)
        {
            _setup.Text = "Connected: ReShade is running in the game.";
        }
        else if (!hasPayload)
        {
            _setup.Text = "Unavailable: this ViewLab build does not include the ReShade Remote component.";
        }
        else if (!installed)
        {
            _setup.Text = "Not installed. Install copies only ViewLab's payload files.";
        }
        else if (!enabled)
            _setup.Text = "Installed but disabled. Enable registers only ViewLab's OpenXR layer.";
        else
        {
            _setup.Text = "Installed and enabled. Connected requires a live game-side handshake.";
        }
    }

    void OnTick(object? sender, EventArgs e)
    {
        if (!_svc.Connected) AttachControlChannel();
        if (!_svc.Connected)
        {
            _status.Text = "[Unavailable] Control channel failed";
            _status.Foreground = Muted;
            UpdateSetupText(live: false);
            return;
        }

        var b = _svc.ReadBlock();
        if (!_handshakeBaselineSet) { _lastHb = b.heartbeat; _handshakeBaselineSet = true; }
        else if (b.heartbeat != _lastHb) { _lastHb = b.heartbeat; _lastHbChange = DateTime.Now; }
        bool live = _lastHbChange != DateTime.MinValue && (DateTime.Now - _lastHbChange).TotalSeconds < 2.0;

        if (live)
        {
            _status.Text = "[Connected] ReShade running";
            _status.Foreground = Green;
        }
        else if (!IsPayloadInstalled())
        {
            _status.Text = "[Not installed]";
            _status.Foreground = Amber;
        }
        else if (IsPayloadEnabled())
        {
            _status.Text = "[Installed and enabled]";
            _status.Foreground = Amber;
        }
        else
        {
            _status.Text = "[Installed but disabled]";
            _status.Foreground = Amber;
        }

        UpdateSetupText(live);

        if (b.revision != _lastAppliedRevision)
        {
            _lastAppliedRevision = b.revision;
            _applying = true;
            SetChip(_gameplay, b.xr_mode == 0); SetChip(_tuning, b.xr_mode == 1);
            _menu.IsChecked = b.menu_visible != 0;
            _headless.IsChecked = b.win_headless != 0;
            _onTop.IsChecked = b.win_always_on_top != 0;
            _applying = false;
        }
    }
}
