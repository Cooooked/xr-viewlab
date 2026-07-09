using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

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
	public static void Main()
	{
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

	private static IntPtr FindWindowByCaption(IntPtr zeroOnly, string caption)
	{
		// Simple P/Invoke — finds first top-level window with matching title
		return FindWindowW(null, caption);
	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr FindWindowW(string? className, string? windowName);
}
