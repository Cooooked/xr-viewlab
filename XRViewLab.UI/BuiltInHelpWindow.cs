using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XRViewLab.UI;

internal sealed record HelpSection(string Heading, string Body);

internal static class BuiltInHelpWindow
{
    internal static readonly IReadOnlyList<HelpSection> ReShadeSections = new[]
    {
        new HelpSection("What it is", "Advanced ReShade Remote controls ViewLab's optional modified ReShade OpenXR payload. ViewLab's native crop, visor and ordinary overlays do not depend on it."),
        new HelpSection("How communication works", "The settings app and payload exchange a small control block through Local\\ReShadeXRControl shared memory. Connected appears only after ViewLab observes the game-side payload advance its heartbeat after this window attaches."),
        new HelpSection("States", "Not installed means the two payload files are absent. Installed but disabled means the files exist without ViewLab's manifest registration. Installed and enabled means the manifest is registered for OpenXR loading. Connected means a running payload has completed a live heartbeat handshake."),
        new HelpSection("Install and enable", "Install copies only ReShade64.dll and ReShade64_XR.json to C:\\ProgramData\\ReShade. Uninstall removes only those two paths. Enable registers only that manifest under the 64-bit OpenXR implicit-layer key. Disable removes only that registration and leaves all files in place."),
        new HelpSection("Controls", "Gameplay selects normal use; Tuning selects adjustment behaviour. Show desktop menu / overlay controls the desktop representation of the in-HMD menu. Borderless and Always on top affect that desktop window. Reposition and Transform adjust the in-headset menu quad. Values remain ready for the next connection."),
        new HelpSection("Safety", "Close games using the payload before install or uninstall. Uninstall and disable do not enumerate, reorder or remove unrelated OpenXR layers. If Connected does not appear, confirm the payload is installed, enabled, and the game is actually using OpenXR or OpenComposite.")
    };

    internal static readonly IReadOnlyList<HelpSection> DiagMonSections = new[]
    {
        new HelpSection("What DiagMon(ster) does", "DiagMon(ster) captures application-agnostic performance evidence into portable ViewLab session folders. It exposes collector health, preserves partial evidence and never silently deletes sessions."),
        new HelpSection("Starting a capture", "Use Standard for the safe low-rate default, Detailed for one-second telemetry plus loaded modules and graphics-API detection, or Trace for Detailed plus a time-bounded Windows Performance Recorder trace. Choose a target method, add a workload label and start the capture."),
        new HelpSection("Controls and collector states", "The capture panel selects mode, target and label. Stop finalises evidence; Open Current Session reviews it; Session Library manages history; VR Session Graph opens native OpenXR traces; Export creates a bounded package. Collector states are pending, running, complete, partial, missing or failed. One weak collector does not erase evidence from the others."),
        new HelpSection("Graphs", "Legends identify each series and unit, budget guides show active display cadence, and downsampling retains spikes. Use the mouse wheel to zoom, drag to pan, hover for exact time/value, and reset for the full session. Marker and alarm lines are recorded events."),
        new HelpSection("Interpreting results", "Compare like workloads with compatible hardware/runtime fingerprints. Average describes the centre; P95, P99 and maximum expose the slow tail. Estimated cadence misses are timing inferences, not proof of dropped presentation. Treat missing collectors and incomplete sessions as explicit limits."),
        new HelpSection("Exporting", "Stop and finalise first, then Export Analysis Package creates a ZIP with the manifest, metrics, summary and retained raw evidence. Read collector states and limitations alongside any conclusion.")
    };

    internal static readonly IReadOnlyList<HelpSection> WhatHappenedSections = new[]
    {
        new HelpSection("What this window does", "It answers \"why didn't ViewLab work just then?\" without you reading a log file. ViewLab already records what happens across several places — the OpenXR layer's log, the background helper's status, Windows' own crash and anti-cheat records — and this window reads all of them, matches them against known failure patterns, and explains each one in plain language with the exact evidence it used."),
        new HelpSection("Confirmed vs Likely", "CONFIRMED means a specific line of recorded evidence proves it, and that line is quoted underneath. LIKELY means the evidence is consistent with the explanation but does not prove it — it is offered as a hypothesis to check, never as a fact. Nothing here is invented: if there is no evidence, no finding is shown."),
        new HelpSection("What it can detect", "The settings window crashing; your game crashing; anti-cheat (EasyAntiCheat, BattlEye) blocking or complaining about ViewLab's layer; the graphics device being lost mid-session; the visor renderer failing to start; Topmost overlay mode falling back; the headset or OpenXR runtime being unavailable; the layer failing to attach to a game; the background helper being stopped or stale (which silently disables iRacing cues, notifications and the OBS cue); notification permission and listener problems; and iRacing telemetry being unreadable."),
        new HelpSection("What it cannot detect", "Anything that leaves no trace. If a game never launched, or a crash was so severe Windows recorded nothing, there is nothing to find. \"No issues detected\" means no known pattern matched — it is not a guarantee that nothing went wrong."),
        new HelpSection("Using it", "Check it right after something goes wrong, while the evidence is recent — most checks only look at the last few hours. Refresh re-reads everything. When asking for help, quote the CONFIRMED lines: they name the exact failure.")
    };

    internal static readonly IReadOnlyList<HelpSection> ObsRecordingSections = new[]
    {
        new HelpSection("Enable OBS WebSocket", "In OBS, open Tools > WebSocket Server Settings, then enable the WebSocket server."),
        new HelpSection("OBS on this PC", "Use localhost for Host / IP. Use the same port shown in OBS, normally 4455, and enter the OBS WebSocket password in ViewLab."),
        new HelpSection("OBS on another PC", "Enter that computer's local network IP address and the WebSocket port shown by OBS. Both computers must be reachable on the same local network."),
        new HelpSection("What ViewLab reads", "ViewLab only reads whether OBS is currently recording. A failed connection or authentication never activates the recording cue.")
    };

    internal static readonly IReadOnlyList<HelpSection> EdgeMaskSections = new[]
    {
        new HelpSection("What this does", "Paints the outer edges of your view black. It hides the borders left behind when ViewLab crops the picture, so you see a clean edge instead of a hard line or flicker."),
        new HelpSection("Does it cost performance?", "No. It only covers pixels that are already being drawn. It does not change how much the game renders, so it neither speeds anything up nor slows it down. Use the Vertical and Horizontal sliders on the main window if you want an actual performance saving."),
        new HelpSection("Left/right and top/bottom", "Tick whichever edges look untidy in the headset. Most people either need both or neither. Masking only one side, or only one eye, is not offered because it does not display reliably."),
        new HelpSection("Recenter foveated rendering", "Only matters if you use Split top and bottom with different values, so your view sits off-centre. Some headsets render the middle of the picture sharper than the edges. This re-aims each eye so that sharp area follows where you are actually looking. Takes effect the next time you start a game. If the world looks slightly tilted afterwards, turn it back off.")
    };

    internal static readonly IReadOnlyList<HelpSection> CalibrationSections = new[]
    {
        new HelpSection("What this is for", "Test patterns drawn straight into the picture your headset receives. They are measuring tools, not features to leave switched on. Use them to check that ViewLab's crop and mask line up with what you actually see, then switch them off."),
        new HelpSection("How to use it", "Tick a pattern, put the headset on, and look at where the pattern sits relative to the edges of your view. If something is off, adjust the crop or mask sliders and look again."),
        new HelpSection("Capturing images", "The capture button saves what the left eye is really being sent while a game is running, so you can compare it side by side on the desktop. Images and their details are saved under your local ViewLab folder in CalibrationCaptures."),
        new HelpSection("Remember to turn them off", "Patterns stay visible in game while ticked. They are also visible in a recording unless you turn them off first.")
    };

    internal static readonly IReadOnlyList<HelpSection> PreviewGuidesSections = new[]
    {
        new HelpSection("What this does", "Adds guide lines to the picture on your monitor that shows the shape of your mask. It is a drawing aid only."),
        new HelpSection("Nothing reaches the headset", "These guides never appear in the headset and never appear in a recording. They exist purely to help you line things up while you are setting the shape."),
        new HelpSection("The options", "Circle guides show the round area a lens covers. Per-eye frames outline each eye separately. Optical centre marks the middle of each lens. IPD adjusts the spacing between the eyes in the preview to match your own, so the preview matches what you will see.")
    };

    internal static readonly IReadOnlyList<HelpSection> OverlaysSections = new[]
    {
        new HelpSection("What overlays are", "Small pieces of information ViewLab draws inside your headset on top of the game: a clock, performance figures, desktop notifications, notes, a crosshair and racing cues. Each one can be turned on or off on its own."),
        new HelpSection("Moving and sizing them", "Every overlay has Position, Scale and Opacity. You can also drag them directly on the mask picture. Changes apply straight away in the headset, so you can leave a game running while you position things."),
        new HelpSection("Clock", "A small clock card. Turning on the session timer adds a second line underneath showing how long you have been in VR."),
        new HelpSection("Performance HUD and trace", "The HUD shows live figures such as frame rate and GPU load. The trace draws those figures as a moving graph. Both can be set to show all the time, or only when performance actually goes wrong, which keeps your view clear until something needs attention."),
        new HelpSection("Crosshair", "A fixed aiming dot at the centre of your view, in the style of a Counter-Strike crosshair. It does not move or spread when you shoot. You can paste in a CS2 share code to copy a crosshair you already like."),
        new HelpSection("Notifications", "Shows Windows notifications inside the headset so you do not have to take it off. Display duration sets how long each one stays. The app filter lets you limit it to only the apps you care about, and the privacy setting can hide message contents so only the app name shows."),
        new HelpSection("Sticky notes", "Short pieces of text you pin inside your view, useful for reminders such as settings to try or a checklist."),
        new HelpSection("iRacing cues", "Edge-of-vision signals driven by live iRacing telemetry: a glow on the side a car is alongside you, coloured flags, a race-start light and a rear-pressure cue. The test buttons let you check placement without being in a session."),
        new HelpSection("Show or hide with a key", "Each overlay can be given a keyboard shortcut so you can hide and show it mid-game without opening ViewLab.")
    };

    internal static readonly IReadOnlyList<HelpSection> ObsCaptureSections = new[]
    {
        new HelpSection("What this is for", "Getting what you see in the headset into OBS, so you can stream or record it."),
        new HelpSection("Show in OBS Mirror", "Chooses which ViewLab overlays appear in the recording. This is separate from what you see in the headset, so you can keep your HUD in VR but leave it out of the video, or the other way round."),
        new HelpSection("ViewLab Media Capture", "ViewLab's own OBS source. Add it in OBS through Sources, then the plus button. Unlike a plain mirror it captures ViewLab's overlays, ReShade, and other headset overlays such as RaceLab and OpenKneeboard. In its properties you can pick which eye to capture, crop the edges, and press Reinitialize if the picture ever stops updating."),
        new HelpSection("ViewLab Enhancer", "An OBS filter that adjusts how the picture looks: sharpness, colour, contrast and brightness. Add it to a source through that source's Filters menu. It changes the recording only, never the headset."),
        new HelpSection("ViewLab Stabilizer", "A separate OBS filter that smooths out head movement, so a recording is less shaky and easier to watch. Also added through a source's Filters menu, and also affects the recording only."),
        new HelpSection("Installing them", "The install buttons copy the plugins into OBS for you. Close OBS first, install, then start OBS again. Reinstall after updating ViewLab, because the plugins and ViewLab have to be the same version to talk to each other.")
    };

    internal static readonly IReadOnlyList<HelpSection> VrQuadSections = new[]
    {
        new HelpSection("What this is", "Controls for the floating panel ViewLab can show inside your headset, so you can read or adjust things without removing it."),
        new HelpSection("Position and size", "Move the panel around you and set how large and how far away it sits. Put it somewhere you can glance at without it covering anything you need during play."),
        new HelpSection("If you cannot find it", "Reset the position to bring it back in front of you. That is the quickest fix if you have moved it out of view.")
    };

    internal static void Show(Window owner, string title, IReadOnlyList<HelpSection> sections)
    {
        var panel = new StackPanel { Margin = new Thickness(18) };
        foreach (HelpSection section in sections)
        {
            panel.Children.Add(new TextBlock { Text = section.Heading, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 5) });
            panel.Children.Add(new TextBlock { Text = section.Body, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 204)), Margin = new Thickness(0, 0, 0, 16) });
        }
        var window = new Window { Title = title, Owner = owner, Width = 620, Height = 620, MinWidth = 440, MinHeight = 360,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(16, 17, 18)), Foreground = Brushes.White,
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
        window.Show();
    }
}
