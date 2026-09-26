#define TRACE
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using irSidekick;
using irSidekickProfiles.Properties;

namespace irSidekickProfiles;

public class App : Application
{
	private bool EarlyTerminate;

	private Mutex AppMutex = new Mutex(initiallyOwned: false, "Global\\" + Settings.Default.GUID + "-Program");

	private bool _contentLoaded;

	protected override void OnStartup(StartupEventArgs e)
	{
		iRacing.Validate();
		TraceLog.appStartup();
		appHistory.appStartup();
		base.DispatcherUnhandledException += DispatcherExceptionHandler;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
		AppDomain.CurrentDomain.UnhandledException += ExceptionHandler;
		if (CommandActioned(e) || SingleAppInstance.isAlreadyRunning())
		{
			EarlyTerminate = true;
			Environment.Exit(0);
		}
		else
		{
			base.OnStartup(e);
		}
	}

	private void ExceptionHandler(object sender, UnhandledExceptionEventArgs args)
	{
		Exception obj = (Exception)args.ExceptionObject;
		TraceLog.Exception(obj);
		MessageBox.Show(obj.Message, "Sorry, unexpected error");
	}

	private void DispatcherExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		TraceLog.Exception(e.Exception, "DispatcherExceptionHandler");
		e.Handled = true;
	}

	private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
	{
		TraceLog.Exception(e.Exception, "OnUnobservedTaskException");
		e.SetObserved();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (!EarlyTerminate)
		{
			appHistory.appExit();
			TraceLog.appExit();
		}
	}

	private bool CommandActioned(StartupEventArgs e)
	{
		if (e.Args.Length != 2)
		{
			return false;
		}
		bool flag = false;
		if (e.Args[0].ToLower() == "apply")
		{
			CommandApply(e.Args[1]);
			flag = true;
		}
		if (flag)
		{
			TraceLog.appExit();
		}
		return flag;
	}

	private bool CommandApply(string name)
	{
		string text = Ini.ReadKey("MemberID", "UserInfo", iniSidekick: true);
		if (!Client.OkeyDokey(text) || !GoodToGo(text))
		{
			TraceLog.Warn("Command 'apply' is only available when licensed");
			return false;
		}
		if (iRacing.IsRunning())
		{
			TraceLog.Warn("Command 'apply' failed because iRacing is running");
			return false;
		}
		appProfileList appProfileList2 = new appProfileList();
		appProfileList2.Discover();
		appProfileList2.Load();
		appProfile appProfile2 = appProfileList2.FindByName(name);
		if (appProfile2 == null)
		{
			TraceLog.Warn("Command 'apply' failed because profile '" + name + "' not found");
			return false;
		}
		if (appProfile2.IsBackup || appProfile2.IsMonitor)
		{
			TraceLog.Warn("Command 'apply' failed because profile '" + name + "' cannot be applied to iRacing");
			return false;
		}
		if (appProfile2.IsIRacingVR)
		{
			appProfile monitor = appProfileList2.Monitor;
			string value = monitor.irApp.tagGetValue("SplitsDeltas", "comparisonLapFileName");
			monitor.irApp.CloneFrom(appProfile2.irApp);
			monitor.irApp.tagSetValue("SplitsDeltas", "comparisonLapFileName", value);
			monitor.IsModified = true;
			monitor.Save();
			appProfile2.CarCameras.Apply();
			return true;
		}
		appProfile appProfile3;
		try
		{
			switch (appProfile2.DisplayMode)
			{
			case iniDisplayMode.Monitor:
				appProfile3 = appProfileList2.Monitor;
				break;
			case iniDisplayMode.Oculus:
				appProfile3 = appProfileList2.Oculus;
				break;
			case iniDisplayMode.OpenVR:
				appProfile3 = appProfileList2.OpenVR;
				break;
			case iniDisplayMode.OpenXR:
				appProfile3 = appProfileList2.OpenXR;
				break;
			default:
				TraceLog.Warn("Command 'apply' profile '" + name + "' failed because unable to determine the target monitor mode");
				return false;
			}
		}
		catch
		{
			TraceLog.Warn("Command 'apply' profile '" + name + "' failed because unable to find the target profile");
			return false;
		}
		Ini.WriteKey(appProfile3.DisplayName, appProfile2.DisplayName, "Origin");
		appProfile3.CloneFrom(appProfile2);
		if (appProfile3.IsIRacingVR)
		{
			appProfile monitor2 = appProfileList2.Monitor;
			monitor2.irApp.CloneFrom(appProfile2.irApp);
			monitor2.Save();
			appProfile2.CarCameras.Apply();
		}
		appProfile3.Save();
		TraceLog.Info("Command 'apply' profile '" + name + "' succeeded");
		return true;
	}

	private bool GoodToGo(string id)
	{
		string productOptions = Ini.GetProductOptions();
		OptionsContainer optionsContainer = new OptionsContainer();
		try
		{
			if (id.IsNullOrEmpty() || productOptions.IsNullOrEmpty())
			{
				return false;
			}
			optionsContainer.FromString(Letter.Open(productOptions));
			if (!optionsContainer.Number.Contains(id))
			{
				optionsContainer = new OptionsContainer();
			}
			return optionsContainer.Option[2];
		}
		catch
		{
			return false;
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			base.StartupUri = new Uri("winMain.xaml", UriKind.Relative);
			Uri resourceLocator = new Uri("/irSidekickProfiles;component/app.xaml", UriKind.Relative);
			Application.LoadComponent(this, resourceLocator);
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public static void Main()
	{
		App app = new App();
		app.InitializeComponent();
		app.Run();
	}
}
