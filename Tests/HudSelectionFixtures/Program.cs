using System.Collections;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        // Construct the real profile editor with disposable in-memory settings. No MainWindow,
        // registry, production shared mappings, game or visible window is needed.
        var assembly = Assembly.Load("xr-viewlab");
        var profileType = assembly.GetType("XRViewLab.UI.ProfileWindow", true)!;
        var widgetType = assembly.GetType("XRViewLab.UI.HudWidgetOption", true)!;
        string[] ids = { "cpu", "gpu", "app", "vr", "cpu_peak", "cpu_frequency", "ram", "commit", "vram", "sys", "fps", "frame_interval", "network_ping", "network_loss", "network_jitter", "network_status" };
        var widgets = Array.CreateInstance(widgetType, ids.Length);
        for (int i = 0; i < ids.Length; i++)
        {
            var widget = Activator.CreateInstance(widgetType)!;
            foreach (string name in new[] { "Id", "Label", "Provider", "Unit", "ToolTip" })
                widgetType.GetProperty(name)!.SetValue(widget, ids[i]);
            widgetType.GetProperty("MetricId")!.SetValue(widget, i);
            widgets.SetValue(widget, i);
        }
        var ctor = profileType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
        object? MakeArgument(ParameterInfo p)
        {
            var t = p.ParameterType;
            if (p.Name == "globalHudWidgets") return widgets;
            if (t == typeof(string)) return "HUD fixture";
            if (t == typeof(double)) return 1.0;
            if (t == typeof(bool)) return false;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)) return Array.CreateInstance(t.GetGenericArguments()[0], 0);
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)) return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(t.GetGenericArguments()));
            return Activator.CreateInstance(t);
        }
        var window = (Window)ctor.Invoke(ctor.GetParameters().Select(MakeArgument).ToArray());
        var models = (IEnumerable)profileType.GetField("_profileHudWidgets", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        var handler = (RoutedEventHandler)profileType.GetMethod("ProfileHudWidget_Changed", BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate(typeof(RoutedEventHandler), window);
        Dictionary<string, string>? published = null;
        profileType.GetField("OverlayLiveChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, (Action<Dictionary<string, string>, uint>)((values, _) => published = new(values)));
        int failures = 0, checks = 0;
        foreach (object widget in models)
        {
            string id = (string)widgetType.GetProperty("Id")!.GetValue(widget)!;
            foreach (var (property, suffix) in new[] { ("Enabled", "enabled"), ("UseSymbol", "symbol") })
            {
                var box = new CheckBox { DataContext = widget };
                box.SetBinding(ToggleButton.IsCheckedProperty, new Binding(property) { Mode = BindingMode.TwoWay });
                box.Checked += handler;
                box.Unchecked += handler;
                foreach (bool target in new[] { false, true, false })
                {
                    if (box.IsChecked == target) continue;
                    box.SetCurrentValue(ToggleButton.IsCheckedProperty, (bool?)target);
                    string key = $"hud:hud_widget_{id}_{suffix}";
                    string expected = target ? "1" : "0";
                    var overrides = profileType.GetProperty("OverlayOverrides")!.GetValue(window)!;
                    var saved = (string?)overrides.GetType().GetMethod("Get")!.Invoke(overrides, new object[] { "hud", $"hud_widget_{id}_{suffix}" });
                    bool ok = saved == expected && published?.GetValueOrDefault(key) == expected;
                    checks++;
                    if (!ok) { failures++; Console.WriteLine($"FAIL {id}.{property}={target}: saved={saved}, live={published?.GetValueOrDefault(key)}"); }
                }
            }
        }
        window.Close();
        Console.WriteLine($"HUD selection: {checks - failures}/{checks} checks passed.");
        return failures == 0 ? 0 : 1;
    }
}
