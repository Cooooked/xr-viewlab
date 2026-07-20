using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace XRViewLab.UI;

public class App : Application
{
	[DllImport("user32.dll")]
	private static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern int SetForegroundWindow(IntPtr hWnd);

	private const string MutexName = "XRViewLabSingleInstance";

	private static string ConfigDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XR ViewLab");

	public void InitializeComponent()
	{
		base.StartupUri = new Uri("MainWindow.xaml", UriKind.Relative);
	}

	[STAThread]
	public static void Main(string[] args)
	{
		if (args.Length >= 4 && args[0].Equals("--set-layer-enabled", StringComparison.OrdinalIgnoreCase))
		{
			bool enabled = args[1] == "1";
			SetLayerRegistration(RegistryView.Registry64, args[2], enabled);
			SetLayerRegistration(RegistryView.Registry32, args[3], enabled);
			return;
		}

			// Elevated OBS-plugin install: copy the bundled DLL into OBS's install obs-plugins/64bit
			// folder (the location OBS actually scans on Windows). Exit code 0 = success, 1 = failure.
			if (args.Length >= 3 && args[0].Equals("--install-obs-plugin", StringComparison.OrdinalIgnoreCase))
			{
				try
				{
					Directory.CreateDirectory(Path.GetDirectoryName(args[2])!);
					File.Copy(args[1], args[2], overwrite: true);
					Environment.Exit(0);
				}
				catch { Environment.Exit(1); }
			}

				// Elevated OBS-plugin uninstall: delete the ViewLab DLL (plus its bundled LICENSE/README
				// if present) from OBS's obs-plugins/64bit folder. Missing files count as success (already
				// gone). Exit code 0 = success, 1 = failure.
				if (args.Length >= 2 && args[0].Equals("--uninstall-obs-plugin", StringComparison.OrdinalIgnoreCase))
				{
					try
					{
						string dll = args[1];
						if (File.Exists(dll)) File.Delete(dll);
						string? dir = Path.GetDirectoryName(dll);
						if (dir != null)
						{
							foreach (string companion in new[] { "LICENSE.txt", "README.md" })
							{
								string p = Path.Combine(dir, companion);
								if (File.Exists(p)) File.Delete(p);
							}
						}
						Environment.Exit(0);
					}
					catch { Environment.Exit(1); }
				}

		using Mutex mutex = new Mutex(true, MutexName, out bool createdNew);
		if (!createdNew)
		{
			// Bring existing instance to foreground
			IntPtr hwnd = FindWindowByCaption(IntPtr.Zero, "xr-viewlab");
			if (hwnd != IntPtr.Zero)
			{
				ShowWindow(hwnd, 1);
				SetForegroundWindow(hwnd);
			}
			return;
		}

		// Record an unhandled exception before the process goes down, so the next launch can
		// explain what happened instead of the window just vanishing. Never swallows the crash —
		// only observes it on the way out.
		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
		{
			if (e.ExceptionObject is Exception ex) CrashMarker.Write(ConfigDirectory, ex);
		};

		App app = new App();
		app.InitializeComponent();
		app.DispatcherUnhandledException += (_, e) =>
		{
			CrashMarker.Write(ConfigDirectory, e.Exception);
			// Deliberately not marking e.Handled — swallowing a UI-thread exception would leave
			// the app running in an unknown, possibly corrupted state, which is worse than exiting.
		};
		app.Run();
	}

	private static void SetLayerRegistration(RegistryView view, string manifestPath, bool enabled)
	{
		const string keyPath = "Software\\Khronos\\OpenXR\\1\\ApiLayers\\Implicit";
		using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
		using RegistryKey key = baseKey.CreateSubKey(keyPath, writable: true) ?? throw new InvalidOperationException("Could not open the OpenXR layer registry key.");
		foreach (string valueName in key.GetValueNames())
		{
			if (valueName.Contains("XR_APILAYER_cooooked_xrviewlab", StringComparison.OrdinalIgnoreCase) &&
				!valueName.Equals(manifestPath, StringComparison.OrdinalIgnoreCase))
				key.DeleteValue(valueName, throwOnMissingValue: false);
		}
		key.SetValue(manifestPath, enabled ? 0 : 1, RegistryValueKind.DWord);
	}

	private static IntPtr FindWindowByCaption(IntPtr zeroOnly, string caption)
	{
		// Simple P/Invoke — finds first top-level window with matching title
		return FindWindowW(null, caption);
	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr FindWindowW(string? className, string? windowName);
}
