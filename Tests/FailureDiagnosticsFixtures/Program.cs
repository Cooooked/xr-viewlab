using System;
using System.Linq;
using XRViewLab.UI;

static void Require(bool condition, string message)
{
	if (!condition) throw new InvalidOperationException("FAILED: " + message);
	Console.WriteLine("PASS: " + message);
}

// No log activity, layer not registered, no crash: nothing to report.
var none = FailureDiagnostics.Analyze("", layerRegisteredInRegistry: false, anyLogLineToday: false);
Require(none.Count == 0, "no signals produce no findings, not a fabricated one");

// Confirmed: D3D11 device removed.
string deviceRemovedLog = "some earlier line\nd3d11 safety: device removed stage=xrReleaseSwapchainImage reason=0x887A0006 pid=1234 thread=5678; all ViewLab rendering disabled for this session\n";
var deviceRemoved = FailureDiagnostics.Analyze(deviceRemovedLog, layerRegisteredInRegistry: true, anyLogLineToday: true);
Require(deviceRemoved.Any(f => f.Category.Contains("D3D11") && f.Certainty == FailureCertainty.Confirmed),
	"D3D11 device removal is reported as Confirmed");
Require(deviceRemoved.Single(f => f.Category.Contains("D3D11")).Evidence.Contains("0x887A0006"),
	"D3D11 finding quotes the exact HRESULT from the log, not a summary");

// Confirmed: visor renderer init failure.
var rendererFail = FailureDiagnostics.Analyze("d3d11 mask: VS compile failed hr=0x80070057 msg\n", true, true);
Require(rendererFail.Any(f => f.Category.Contains("Visor renderer") && f.Certainty == FailureCertainty.Confirmed),
	"visor renderer init failure is reported as Confirmed");

// Confirmed: Topmost fail-closed (framed as expected safety behavior, not an alarming crash).
var topmost = FailureDiagnostics.Analyze("topmost safety: disabled for session stage=xrCreateSwapchain result=0x1 pid=1 thread=1; no automatic retry\n", true, true);
Require(topmost.Any(f => f.Category.Contains("Topmost") && f.Recommendation.Contains("No action needed")),
	"Topmost fail-closed is explained as intended behavior, not framed as alarming");

// Confirmed: iRacing SDK layout rejection.
var iracing = FailureDiagnostics.Analyze("Required SDK variable 'CarLeftRight' is missing or invalid.\n", true, true);
Require(iracing.Any(f => f.Category.Contains("iRacing") && f.Certainty == FailureCertainty.Confirmed),
	"iRacing SDK layout rejection is reported as Confirmed");

// Likely (not Confirmed): layer registered but nothing logged today.
var likely = FailureDiagnostics.Analyze("", layerRegisteredInRegistry: true, anyLogLineToday: false);
Require(likely.Count == 1 && likely[0].Certainty == FailureCertainty.Likely,
	"layer-registered-but-silent-today is reported as Likely, not Confirmed");
Require(likely[0].Evidence.Contains("No ViewLab.log entries"), "Likely finding states plainly what evidence is missing");

// The "likely" hypothesis must not appear when the layer isn't registered — that's a different,
// more certain problem (not covered here) and conflating them would misdirect the user.
var notRegistered = FailureDiagnostics.Analyze("", layerRegisteredInRegistry: false, anyLogLineToday: false);
Require(!notRegistered.Any(f => f.Category.Contains("may not be loading")),
	"the layer-may-not-be-loading hypothesis requires the layer to actually be registered first");

// Confirmed: UI crash marker.
var crash = FailureDiagnostics.Analyze("", false, false,
	new CrashMarker.Record("NullReferenceException", "Object reference not set to an instance of an object.", "at Foo()", DateTimeOffset.UtcNow));
Require(crash.Any(f => f.Category.Contains("crashed") && f.Certainty == FailureCertainty.Confirmed),
	"a recorded UI crash is reported as Confirmed with the real exception type");
Require(crash.Single(f => f.Category.Contains("crashed")).Evidence.Contains("NullReferenceException"),
	"crash finding quotes the actual exception type, not a generic message");

// Notification broker failures arrive via its own status file, never via ViewLab.log.
var brokerPermission = FailureDiagnostics.Analyze("", true, true, notificationBrokerState: "PermissionNotGranted",
	notificationBrokerDetail: "UserNotificationListenerAccessStatus=Denied.");
Require(brokerPermission.Any(f => f.Category.Contains("permission") && f.Certainty == FailureCertainty.Confirmed),
	"notification broker permission-not-granted is reported as Confirmed");
Require(brokerPermission.Single(f => f.Category.Contains("permission")).Evidence.Contains("Denied"),
	"broker permission finding quotes the actual access status, not a summary");

var brokerRenderer = FailureDiagnostics.Analyze("", true, true, notificationBrokerState: "InternalRendererFailure",
	notificationBrokerDetail: "IOException: sharing violation");
Require(brokerRenderer.Any(f => f.Category.Contains("renderer") && f.Certainty == FailureCertainty.Confirmed),
	"notification broker internal renderer failure is reported as Confirmed");

var brokerNone = FailureDiagnostics.Analyze("", true, true, notificationBrokerState: null);
Require(!brokerNone.Any(f => f.Category.Contains("Notification") || f.Category.Contains("notification-mirroring") || f.Category.Contains("permission")),
	"a healthy or absent broker status file produces no broker finding");

// The real iRacing SDK layout rejection arrives via the broker's iracing-status.json diagnostics
// text, not ViewLab.log (IRacingTelemetryProvider throws in-process, not in the native layer).
var iracingFromDiagnostics = FailureDiagnostics.Analyze("", true, true,
	iRacingDiagnostics: "SDK read failed: Required SDK variable 'CarLeftRight' is missing or invalid.");
Require(iracingFromDiagnostics.Any(f => f.Category.Contains("iRacing") && f.Certainty == FailureCertainty.Confirmed),
	"real iRacing layout rejection (from broker diagnostics) is reported as Confirmed");

// A normal "Disconnected" idle state (iRacing simply isn't running) must never be flagged.
var iracingIdle = FailureDiagnostics.Analyze("", true, true, iRacingDiagnostics: "Waiting for the iRacing SDK mapping.");
Require(!iracingIdle.Any(f => f.Category.Contains("iRacing")), "normal iRacing idle/disconnected state is not treated as a failure");

// Multiple simultaneous signals all surface — findings are additive, not first-match-wins.
var combined = FailureDiagnostics.Analyze(deviceRemovedLog + "d3d11 mask: PS compile failed hr=0x1\n", true, true);
Require(combined.Count(f => f.Certainty == FailureCertainty.Confirmed) >= 2,
	"multiple distinct confirmed failures in one log all surface, not just the first match");

Console.WriteLine("Failure diagnostics classification fixtures passed.");
