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

// ---- Expanded detection (What happened? made actually useful) --------------------------------
// The background helper being stopped is the failure that hid R51 for a full day: every iRacing cue,
// notification card and the OBS cue silently do nothing and NOTHING reports an error anywhere.
var brokerStopped = FailureDiagnostics.Analyze("", true, true,
	brokerProcessRunning: false, anyBrokerFeatureEnabled: true, brokerStatusAge: TimeSpan.FromHours(5));
Require(brokerStopped.Any(f => f.Category.Contains("background helper") && f.Certainty == FailureCertainty.Confirmed),
	"a stopped background helper with dependent features enabled is Confirmed");

// ...but it must NOT nag when the user has none of those features switched on.
var brokerStoppedUnused = FailureDiagnostics.Analyze("", true, true,
	brokerProcessRunning: false, anyBrokerFeatureEnabled: false);
Require(!brokerStoppedUnused.Any(f => f.Category.Contains("background helper")),
	"a stopped helper is not reported when nothing depends on it");

// Running but silent for hours: stated as Likely, since the process being alive is not proof of fault.
var brokerStale = FailureDiagnostics.Analyze("", true, true,
	brokerProcessRunning: true, anyBrokerFeatureEnabled: true, brokerStatusAge: TimeSpan.FromHours(3));
Require(brokerStale.Any(f => f.Category.Contains("stopped reporting") && f.Certainty == FailureCertainty.Likely),
	"a live but stale helper is reported as Likely, not Confirmed");
var brokerFresh = FailureDiagnostics.Analyze("", true, true,
	brokerProcessRunning: true, anyBrokerFeatureEnabled: true, brokerStatusAge: TimeSpan.FromSeconds(30));
Require(!brokerFresh.Any(f => f.Category.Contains("stopped reporting")), "a freshly reporting helper is not flagged");

var now = new DateTime(2026, 7, 25, 12, 0, 0);
var antiCheat = FailureDiagnostics.Analyze("", true, true, systemEvents: new[] {
	new FailureDiagnostics.SystemEvent("EasyAntiCheat", "Untrusted system file XR_APILAYER_cooooked_xrviewlab.dll", now) });
Require(antiCheat.Any(f => f.Category.Contains("Anti-cheat") && f.Certainty == FailureCertainty.Confirmed),
	"an anti-cheat event log record is reported as Confirmed");

var gameCrash = FailureDiagnostics.Analyze("", true, true, systemEvents: new[] {
	new FailureDiagnostics.SystemEvent("Application Error", "Faulting application name: iRacingSim64DX11.exe", now) });
Require(gameCrash.Any(f => f.Category.Contains("program crashed") && f.Certainty == FailureCertainty.Confirmed),
	"a game crash recorded by Windows is reported as Confirmed");

// A ViewLab crash must be attributed to ViewLab, not reported as "your game crashed".
var ownCrash = FailureDiagnostics.Analyze("", true, true, systemEvents: new[] {
	new FailureDiagnostics.SystemEvent("Application Error", "Faulting application name: ViewLab.NotificationBroker.exe", now) });
Require(ownCrash.Any(f => f.Category.Contains("ViewLab process crashed")),
	"a crash in ViewLab's own process is attributed to ViewLab");
Require(!ownCrash.Any(f => f.Category.Contains("program crashed")),
	"a ViewLab crash is not also reported as a generic third-party program crash");

var noEvents = FailureDiagnostics.Analyze("", true, true, systemEvents: Array.Empty<FailureDiagnostics.SystemEvent>());
Require(!noEvents.Any(f => f.Category.Contains("Anti-cheat") || f.Category.Contains("crashed")),
	"no event log records produces no crash or anti-cheat findings");

var headsetMissing = FailureDiagnostics.Analyze("INFO | xrGetSystem failed XR_ERROR_FORM_FACTOR_UNAVAILABLE\n", true, true);
Require(headsetMissing.Any(f => f.Category.Contains("headset") && f.Certainty == FailureCertainty.Confirmed),
	"an unavailable headset/runtime is reported as Confirmed");

var attachFailed = FailureDiagnostics.Analyze("INFO | xrCreateApiLayerInstance result=-1 state=enabled\n", true, true);
Require(attachFailed.Any(f => f.Category.Contains("attach")), "a rejected layer instance is reported");
var attachOk = FailureDiagnostics.Analyze("INFO | xrCreateApiLayerInstance result=0 state=enabled\n", true, true);
Require(!attachOk.Any(f => f.Category.Contains("attach")), "a successful layer instance is never reported as a failure");

Console.WriteLine("Failure diagnostics classification fixtures passed.");
