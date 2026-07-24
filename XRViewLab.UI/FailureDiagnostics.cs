using System;
using System.Collections.Generic;
using System.Linq;

namespace XRViewLab.UI;

public enum FailureCertainty { Confirmed, Likely }

// A single explained failure. Evidence always quotes the exact source line (log text or crash
// marker) a Confirmed finding is based on — Likely findings say plainly what's missing rather
// than asserting a cause the evidence doesn't actually establish.
public sealed record FailureFinding(string Category, string Summary, string Evidence, FailureCertainty Certainty, string Recommendation);

// Turns ViewLab's existing signals — native log lines, a UI-side crash marker, and the
// notification broker's own status/iRacing status files — into a short list of plain-English
// explanations. Every category here maps to a real, already-recorded signal somewhere in ViewLab
// (dllmain.cpp's log, or the separate notification-broker process's status files); this class does
// not invent new failure detection, only interprets what's already written. Kept dependency-free
// (no file I/O) so the classification rules are fixture-testable; the caller reads the files and
// supplies their contents.
public static class FailureDiagnostics
{
	// An external crash/anti-cheat record gathered from the Windows Application event log by the caller
	// (kept out of this class so the rules stay dependency-free and fixture-testable).
	public sealed record SystemEvent(string Source, string Message, DateTime WhenLocal);

	public static IReadOnlyList<FailureFinding> Analyze(string logText, bool layerRegisteredInRegistry, bool anyLogLineToday,
		CrashMarker.Record? uiCrash = null,
		string? notificationBrokerState = null, string? notificationBrokerDetail = null,
		string? iRacingDiagnostics = null,
		bool? brokerProcessRunning = null,
		bool anyBrokerFeatureEnabled = false,
		TimeSpan? brokerStatusAge = null,
		IReadOnlyList<SystemEvent>? systemEvents = null)
	{
		var findings = new List<FailureFinding>();
		string[] lines = string.IsNullOrEmpty(logText) ? Array.Empty<string>() : logText.Split('\n');

		if (uiCrash != null)
		{
			findings.Add(new FailureFinding(
				"ViewLab settings window crashed",
				$"The settings window closed unexpectedly with an unhandled {uiCrash.ExceptionType}.",
				$"{uiCrash.ExceptionType}: {uiCrash.Message}",
				FailureCertainty.Confirmed,
				"Reopen ViewLab. If it crashes again the same way, note the exception type/message above when asking for help."));
		}

		// D3D11 device loss disables all ViewLab rendering for the session — dllmain.cpp's own
		// safety net (RendererDeviceHealthy), already logged with the exact removal reason.
		string? deviceRemoved = lines.LastOrDefault(l => l.Contains("d3d11 safety: device removed", StringComparison.Ordinal));
		if (deviceRemoved != null)
		{
			findings.Add(new FailureFinding(
				"D3D11 graphics device was removed",
				"The game's D3D11 device was lost mid-session (driver reset, crash, or GPU issue). ViewLab disabled its own rendering for the rest of that session rather than risk further instability.",
				deviceRemoved.Trim(),
				FailureCertainty.Confirmed,
				"This is usually a driver or game-side GPU issue, not a ViewLab bug. Check Windows Event Viewer for a matching display driver or application-crash entry around the same time."));
		}

		// Visor renderer (shader/pipeline) initialization failures. All share the "d3d11 mask: ...
		// failed" prefix already used throughout dllmain.cpp's InitD3D11MaskRenderer.
		string? rendererInitFailed = lines.LastOrDefault(l => l.Contains("d3d11 mask:", StringComparison.Ordinal) && l.Contains("failed", StringComparison.Ordinal));
		if (rendererInitFailed != null)
		{
			findings.Add(new FailureFinding(
				"Visor renderer failed to initialize",
				"A step in setting up ViewLab's D3D11 visor/HUD renderer failed, so the overlay could not be drawn this session.",
				rendererInitFailed.Trim(),
				FailureCertainty.Confirmed,
				"Often caused by an outdated GPU driver or a game using an unusual D3D11 feature level. Update your graphics driver and try again."));
		}

		// Topmost overlay failures are already contained (fails closed to the direct backend) —
		// still worth surfacing so the user knows why an overlay mode silently reverted.
		string? topmostFailed = lines.LastOrDefault(l => l.Contains("topmost safety: disabled for session", StringComparison.Ordinal));
		if (topmostFailed != null)
		{
			findings.Add(new FailureFinding(
				"Topmost overlay mode was disabled for the session",
				"ViewLab's automatic Topmost compositor layer failed to initialize or render, and safely fell back to its direct rendering path for the rest of the session. The game itself was not affected.",
				topmostFailed.Trim(),
				FailureCertainty.Confirmed,
				"No action needed — this is the intended fail-safe behavior. If it happens every session with this game, the direct path is likely the better fit for it anyway."));
		}

		// iRacing shared-memory layout rejection (EnsureLayout's own validation messages).
		string? iracingLayout = lines.LastOrDefault(l => l.Contains("Invalid SDK", StringComparison.Ordinal) || l.Contains("Required SDK variable", StringComparison.Ordinal));
		if (iracingLayout != null)
		{
			findings.Add(new FailureFinding(
				"iRacing telemetry could not be read",
				"The iRacing SDK's shared-memory layout did not match what ViewLab expects, so telemetry (spotter, flags, laps) was unavailable this session.",
				iracingLayout.Trim(),
				FailureCertainty.Confirmed,
				"Usually means iRacing wasn't running or its telemetry wasn't active yet when ViewLab checked. Make sure you're in a live session, not just the sim launcher."));
		}

		// Notification broker states (from its own status file — this runs in a separate process
		// from the OpenXR layer, so these never appear in ViewLab.log).
		(string Category, string Summary, string Recommendation)? brokerFinding = notificationBrokerState switch
		{
			"PermissionNotGranted" => (
				"Windows didn't grant notification-mirroring permission",
				"ViewLab's notification broker needs Windows' notification-listener permission to mirror desktop notifications into the visor, and it hasn't been granted.",
				"Open ViewLab's notification settings and click \"Request access\", then approve the Windows permission prompt."),
			"UnsupportedDeployment" => (
				"Notification mirroring isn't supported on this install",
				"The notification broker could not register the package identity it needs for Windows to allow notification mirroring.",
				"Repair or reinstall ViewLab; this typically means a required signing certificate or package file is missing."),
			"ListenerInitializationFailure" => (
				"Notification listener failed to start",
				"ViewLab's notification broker could not start Windows' notification listener.",
				"Restart the notification broker from ViewLab's settings. If it keeps failing, check Windows notification settings for ViewLab."),
			"InternalRendererFailure" => (
				"Notification card renderer failed",
				"The notification broker could not create its shared-memory bridge to the visor, so notification cards cannot be shown this session.",
				"Restart ViewLab. If this persists, another process may be holding the same shared-memory name."),
			"BrokerError" => (
				"Notification broker's command channel failed",
				"The notification broker lost its internal command channel and could not process requests from the settings window.",
				"Restart the notification broker from ViewLab's settings."),
			_ => null
		};
		if (brokerFinding is { } bf)
		{
			findings.Add(new FailureFinding(bf.Category, bf.Summary, notificationBrokerDetail ?? notificationBrokerState ?? "", FailureCertainty.Confirmed, bf.Recommendation));
		}

		// The iRacing SDK layout rejection is actually thrown and recorded inside the notification
		// broker process (IRacingTelemetryProvider), not dllmain.cpp — the logText-based check above
		// is a defensive fallback in case that ever changes, but real data arrives here instead.
		bool iracingLayoutFromDiagnostics = iRacingDiagnostics != null &&
			(iRacingDiagnostics.Contains("Invalid SDK", StringComparison.Ordinal) || iRacingDiagnostics.Contains("Required SDK variable", StringComparison.Ordinal));
		if (iracingLayoutFromDiagnostics && iracingLayout == null)
		{
			findings.Add(new FailureFinding(
				"iRacing telemetry could not be read",
				"The iRacing SDK's shared-memory layout did not match what ViewLab expects, so telemetry (spotter, flags, laps) was unavailable this session.",
				iRacingDiagnostics!.Trim(),
				FailureCertainty.Confirmed,
				"Usually means iRacing wasn't running or its telemetry wasn't active yet when ViewLab checked. Make sure you're in a live session, not just the sim launcher."));
		}

		// The background helper is a SEPARATE process that owns every iRacing cue, the notification
		// cards and the OBS recording cue. If it is not running, all of those silently do nothing and
		// nothing anywhere reports an error — the status file simply stops being updated, which is
		// indistinguishable from "idle". This is exactly how R51 hid for a full day, so it is detected
		// explicitly rather than inferred.
		if (brokerProcessRunning == false && anyBrokerFeatureEnabled)
		{
			findings.Add(new FailureFinding(
				"The background helper isn't running",
				"ViewLab's background helper is stopped, and you have features switched on that depend on it — iRacing cues (spotter, flags, race start, lap and fuel), notification cards and the OBS recording cue. All of them do nothing while it is stopped, without reporting an error anywhere.",
				"ViewLab.NotificationBroker.exe is not present in the running process list." +
					(brokerStatusAge is { } age ? $" Its status was last updated {DescribeAge(age)}." : ""),
				FailureCertainty.Confirmed,
				"Reopen ViewLab, which starts it automatically, or sign out and back in to Windows. If this happened straight after updating ViewLab, update to 4.1.298 or newer — older installers stopped the helper and never restarted it."));
		}
		// Running, but its published state has gone stale: the process is alive yet no longer reporting,
		// so a status line elsewhere in the UI may be showing hours-old information as if it were current.
		else if (brokerProcessRunning == true && anyBrokerFeatureEnabled && brokerStatusAge is { } staleAge && staleAge > TimeSpan.FromMinutes(10))
		{
			findings.Add(new FailureFinding(
				"The background helper has stopped reporting",
				"ViewLab's background helper is running but has not updated its status for a long time. Anything it feeds — iRacing cues, notification cards, the OBS recording cue — may have stopped working, and status text elsewhere in ViewLab may be showing old information as though it were current.",
				$"Last status update was {DescribeAge(staleAge)}.",
				FailureCertainty.Likely,
				"Restart ViewLab. If it goes stale again soon after, note what you were doing at the time."));
		}

		// Anti-cheat. ViewLab's layer DLL is not code-signed, so EasyAntiCheat/BattlEye can warn about
		// it or refuse to let it hook — a known open issue. These records live in Windows' Application
		// event log, never in ViewLab's own log, so ViewLab could not previously explain them at all.
		SystemEvent? antiCheat = systemEvents?.LastOrDefault(e =>
			Mentions(e.Source, "EasyAntiCheat") || Mentions(e.Source, "BattlEye") ||
			Mentions(e.Message, "EasyAntiCheat") || Mentions(e.Message, "BattlEye") ||
			Mentions(e.Message, "Untrusted system file"));
		if (antiCheat != null)
		{
			findings.Add(new FailureFinding(
				"Anti-cheat reported a problem",
				"An anti-cheat system recorded an error around the time you were playing. ViewLab's OpenXR layer is not code-signed yet, so some anti-cheat systems warn about it (\"untrusted system file\") or block it from attaching — which can stop ViewLab's overlays appearing in that game, or show a popup at launch.",
				$"{antiCheat.WhenLocal:yyyy-MM-dd HH:mm} {antiCheat.Source}: {Truncate(antiCheat.Message, 300)}",
				FailureCertainty.Confirmed,
				"This is a known limitation and not something you can fix from ViewLab's settings — the layer needs a code-signing certificate. If a game refuses to start with ViewLab enabled, turn the layer off for that game."));
		}

		// Game crashes. Windows records these as Application Error / .NET Runtime entries naming the
		// faulting executable; ViewLab's own log usually just stops mid-session with no explanation.
		SystemEvent? appCrash = systemEvents?.LastOrDefault(e =>
			(Mentions(e.Source, "Application Error") || Mentions(e.Source, "Application Hang") ||
			 Mentions(e.Source, "Windows Error Reporting") || Mentions(e.Source, ".NET Runtime")) &&
			!IsViewLabProcess(e.Message));
		if (appCrash != null)
		{
			findings.Add(new FailureFinding(
				"A program crashed recently",
				"Windows recorded an application crash or hang around the time you were playing. If this is your game, ViewLab's overlays would have disappeared with it — that is the game ending, not ViewLab switching off.",
				$"{appCrash.WhenLocal:yyyy-MM-dd HH:mm} {appCrash.Source}: {Truncate(appCrash.Message, 300)}",
				FailureCertainty.Confirmed,
				"If the named program is your game, this is a game or driver crash rather than a ViewLab fault. To check whether ViewLab was involved, launch it once with the layer disabled and see if the crash still happens."));
		}

		// ViewLab's own processes crashing. Separated from the generic case so the wording is honest
		// about it being ViewLab's fault rather than the game's.
		SystemEvent? ownCrash = systemEvents?.LastOrDefault(e =>
			(Mentions(e.Source, "Application Error") || Mentions(e.Source, ".NET Runtime")) &&
			IsViewLabProcess(e.Message));
		if (ownCrash != null)
		{
			findings.Add(new FailureFinding(
				"A ViewLab process crashed",
				"Windows recorded a crash in one of ViewLab's own programs. If it was the background helper, every feature it owns (iRacing cues, notification cards, the OBS recording cue) stopped at that moment.",
				$"{ownCrash.WhenLocal:yyyy-MM-dd HH:mm} {ownCrash.Source}: {Truncate(ownCrash.Message, 300)}",
				FailureCertainty.Confirmed,
				"Reopen ViewLab. This is a ViewLab bug worth reporting — quote the line above, and say which game you were in."));
		}

		// The headset/runtime was not available when a game asked for it. dllmain.cpp surfaces the
		// runtime's own error code, so this is quoted rather than guessed.
		string? formFactor = lines.LastOrDefault(l => l.Contains("XR_ERROR_FORM_FACTOR_UNAVAILABLE", StringComparison.Ordinal));
		if (formFactor != null)
		{
			findings.Add(new FailureFinding(
				"The headset wasn't available",
				"A game asked the OpenXR runtime for your headset and it wasn't there — usually the headset was off, asleep, or not yet streaming when the game started.",
				formFactor.Trim(),
				FailureCertainty.Confirmed,
				"Get the headset connected and streaming first, then start the game. Nothing in ViewLab needs changing."));
		}

		// The layer loaded but the runtime rejected its instance creation, so ViewLab was absent for
		// that whole session even though the log shows it starting up.
		string? instanceFailed = lines.LastOrDefault(l =>
			l.Contains("xrCreateApiLayerInstance", StringComparison.Ordinal) &&
			l.Contains("result=", StringComparison.Ordinal) &&
			!l.Contains("result=0", StringComparison.Ordinal));
		if (instanceFailed != null)
		{
			findings.Add(new FailureFinding(
				"ViewLab couldn't attach to the game",
				"ViewLab's layer was loaded by the game but the OpenXR runtime refused to let it start, so none of ViewLab's features were active for that session.",
				instanceFailed.Trim(),
				FailureCertainty.Confirmed,
				"Check that your OpenXR runtime and headset software are up to date. If it only happens with one game, that game may be rejecting extra OpenXR layers."));
		}

		// Likely (not confirmed): the layer is registered in the registry, but nothing in the log
		// suggests it actually loaded into a game today. This is a plausible explanation for "I
		// enabled ViewLab but nothing happened in the headset" — stated as a hypothesis, not a fact,
		// since an idle log could also just mean no game has been launched yet today.
		if (layerRegisteredInRegistry && !anyLogLineToday)
		{
			findings.Add(new FailureFinding(
				"The OpenXR layer may not be loading into your game",
				"ViewLab's OpenXR layer is registered in Windows, but there's no log activity from today. This could mean the layer hasn't loaded into any game yet today, or that it failed to load silently.",
				"No ViewLab.log entries found for today's date.",
				FailureCertainty.Likely,
				"Launch your VR game via the same runtime you normally use, then check View Log again. If it still shows nothing after actually playing, the layer likely isn't being loaded by that runtime."));
		}

		return findings;
	}

	// Any ViewLab-owned executable (settings app, broker, bundled fixtures) is attributed to ViewLab
	// rather than reported as "a program crashed", which would otherwise read as "your game crashed".
	private static bool IsViewLabProcess(string? message) =>
		Mentions(message, "xr-viewlab") || Mentions(message, "ViewLab.");

	private static bool Mentions(string? haystack, string needle) =>
		haystack != null && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

	private static string Truncate(string value, int max)
	{
		string flat = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
		while (flat.Contains("  ", StringComparison.Ordinal)) flat = flat.Replace("  ", " ", StringComparison.Ordinal);
		return flat.Length <= max ? flat : flat[..max] + "…";
	}

	private static string DescribeAge(TimeSpan age) =>
		age < TimeSpan.FromMinutes(2) ? "just now"
		: age < TimeSpan.FromHours(1) ? $"{(int)age.TotalMinutes} minutes ago"
		: age < TimeSpan.FromDays(1) ? $"{(int)age.TotalHours} hours ago"
		: $"{(int)age.TotalDays} days ago";
}
