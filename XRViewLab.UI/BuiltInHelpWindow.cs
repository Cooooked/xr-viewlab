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
