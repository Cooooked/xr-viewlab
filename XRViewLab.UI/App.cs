using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace XRViewLab.UI;

public class App : Application
{
	[DllImport("user32.dll")]
	private static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	private static extern int SetForegroundWindow(IntPtr hWnd);

	private const string MutexName = "XRViewLabSingleInstance";

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

		App app = new App();
		app.InitializeComponent();
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
