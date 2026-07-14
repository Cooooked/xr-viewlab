using System;
using System.Collections.Generic;
using System.Linq;

namespace XRViewLab.UI;

public enum FailureCertainty { Confirmed, Likely }

// A single explained failure. Evidence always quotes the exact source line (log text or crash
// marker) a Confirmed finding is based on — Likely findings say plainly what's missing rather
// than asserting a cause the evidence doesn't actually establish.
public sealed record FailureFinding(string Category, string Summary, string Evidence, FailureCertainty Certainty, string Recommendation);

// Turns ViewLab's existing native log lines (and a UI-side crash marker, if any) into a short list
// of plain-English explanations. Every category here maps to a real, already-logged signal in
// dllmain.cpp — this class does not invent new failure detection, only interprets what's already
// written. Kept dependency-free (no file I/O) so the classification rules are fixture-testable;
// the caller supplies the log text and a couple of already-known facts (layer registration state,
// whether any log line exists for "today").
public static class FailureDiagnostics
{
	public static IReadOnlyList<FailureFinding> Analyze(string logText, bool layerRegisteredInRegistry, bool anyLogLineToday,
		CrashMarker.Record? uiCrash = null)
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
