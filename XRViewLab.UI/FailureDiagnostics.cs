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
	public static IReadOnlyList<FailureFinding> Analyze(string logText, bool layerRegisteredInRegistry, bool anyLogLineToday,
		CrashMarker.Record? uiCrash = null,
		string? notificationBrokerState = null, string? notificationBrokerDetail = null,
		string? iRacingDiagnostics = null)
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
}
