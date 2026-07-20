#include "pch.h"
#include "HardwareTelemetry.h"
#include "RenderPolicy.h"
#include "RacingCueGeometry.h"
#include "ProjectionTopology.h"
#include "ViewLabBridge/BridgeCore.h"
#include "ClockWidget.h"
#include "StickyNote.h"
#include <io.h>

namespace {
constexpr const char* LayerName = "XR_APILAYER_cooooked_xrviewlab";
constexpr const wchar_t* ConfigFileName = L"xr-viewlab.ini";
constexpr const wchar_t* AppRegistryRoot = L"Software\\cooooked\\xr-viewlab\\Apps";
constexpr double DefaultTotalTangent = 0.18;
constexpr double DefaultTopTangent = 0.09;
constexpr double DefaultBottomTangent = 0.09;
constexpr double DefaultHorizontalRenderWidth = 0.80;
constexpr double MinVerticalTangent = 0.0;
constexpr double MaxVerticalTangent = 1.0;
constexpr double MinRenderScale = 0.01;
constexpr double MinUevrRecommendedScale = 0.50;

std::filesystem::path layerDirectory;
std::ofstream logStream;
std::ofstream verboseLogStream;
std::mutex logMutex;
bool sessionLogged = false;

bool enabled = true;
bool cropOuterEdgesOnly = true;
// CROP (total_render_height / horizontal_render_width) controls how much the game
// actually RENDERS (FOV + recommended resolution) — keep generous to avoid LOD
// pop-in. MASK is a separate black visor border drawn via the OpenXR visibility
// mesh: it bounds the VISIBLE area (black beyond) without changing what is rendered,
// and costs no extra GPU (fewer pixels shaded). mask_* values are ABSOLUTE in the
// same units as CROP; the layer converts them to a fraction of the cropped view.
bool maskEnabled = false;
double maskVertical = 1.0;    // legacy absolute vtangent bound
double maskHorizontal = 1.0;  // legacy absolute hwidth bound
bool maskRounded = false;     // round the visible-area corners (pill/bean look)
double maskCorner = 1.0;      // corner radius as a fraction of the shorter half-extent
double maskTopBias = 0.0;     // -1..1, moves the top lip of the visor opening vertically
double maskBottomBias = 0.0;  // -1..1, moves the bottom lip of the visor opening vertically
double maskLeftBias = 0.0;    // -1..1, moves the left lip of the visor opening horizontally
double maskRightBias = 0.0;   // -1..1, moves the right lip of the visor opening horizontally
double maskTopCurve = 0.0;    // -1..1, bends the top lip for bridge/nose-like shapes
double maskBottomCurve = 0.0; // -1..1, bends the bottom lip
// Per-game render-resolution multiplier (supersampling). 1.0 = unchanged; e.g. 1.37 = 137%.
// Lets the user set quality per game instead of changing VD/VDXR resolution between games.
double renderScale = 1.0;
double visorSize = 1.0;
double visorWidth = 1.0;
double visorHeight = 1.0;
double visorCurve = 0.75;
double visorOffsetX = 0.0;
double visorOffsetY = 0.0;
// Visor shape controls. Formula-identical to the UI preview (BeanMaskEditor.cs) — any
// change here must be mirrored there. Values are authored in the UI's coordinate
// convention (y grows DOWN); the UI-to-NDC flip happens ONLY in ApexYFromConfigNdc.
float g_visorColor[3] = {0.f, 0.f, 0.f}; // item 21: visor fill colour (default black); live-state carries 0xRRGGBB
double visorOuterApexY = 0.0;          // mask_outer_apex_y: -0.5..0.5, outer-curve apex offset
double visorInnerLowerY = 0.0;         // mask_inner_lower_y: 0..0.666, nose-divot band height (0 = off)
double visorInnerBridgeWidth = 0.5;    // mask_inner_bridge_width: 0..1, bezier handle width
double visorInnerBridgeRise = 0.0;     // mask_inner_bridge_rise: -0.5..1, endpoint tangent rise
double visorInnerBridgePeakX = 0.5;    // mask_inner_bridge_peak_x: -1..2, handle horizontal shift
double visorInnerBridgeSteepness = 0.5;// mask_inner_bridge_steepness: -1..2, handle length scale
double visorNoseSpreadX = 0.0;         // mask_nose_spread_x: 0..0.5, mirrored outward from each eye centre
double liveVisorRevision = 0.0;
bool liveVisorUsesProfileOverride = false;
uint32_t profileOverlayOverrideMask = 0;
uint32_t profileStickyOverlayOverrideMask = 0;
bool profileObsFeatureOverride = false;
bool profileIRacingFeatureOverride = false;
constexpr bool visorHD = false;          // HD visor removed
constexpr bool visorAntialiasing = false; // anti-aliasing removed
// There is one product visor: a ViewLab-owned curved corner mask drawn into the
// game's D3D11 eye textures. The old alternate visor experiments were removed.
// Per-frame hook detail (xrLocateViews / xrEnumerateViewConfigurationViews /
// xrGetVisibilityMaskKHR) is gated behind this flag and, when on, routed to a
// SEPARATE ViewLab.verbose.log so the human-facing ViewLab.log (and the in-UI
// Log button) only ever shows meaningful init/config/error events. OFF by default.
bool verboseLogging = false;
bool splitMode = false;
constexpr bool foveatedCenterCompensation = false; // retired: rotating game eye poses tilted asymmetric crops
bool visualMaskOnly = false;
bool horizontalVisualMaskOnly = false;
void Log(const char* fmt, ...);
bool outerEdgeVisibilityMaskOnly = true;  // permanently enabled
constexpr bool lodPopInFix = false;        // LOD pop-in fix removed
// Calibration diagnostics are deliberately rendered into the submitted texture.  They are
// therefore a reference for the whole downstream runtime/encode/display path, rather than a
// head-locked UI overlay.
bool calibrationGrid = false;
bool calibrationRuler = false;
bool calibrationGratings = false;
bool calibrationBars = false;
bool calibrationBeacon = false;
bool calibrationEdgeProbes = false;
bool calibrationCheckerboards = false;
bool calibrationZonePlate = false;
bool calibrationClippingSteps = false;
bool calibrationMotionStrip = false;
void LogVerbose(const char* format, ...);
// Native performance HUD. It uses the same submitted-texture path as the visor, never an
// OpenXR quad layer. Values remain unavailable unless measurable or explicitly debug-supplied.
bool hudEnabled = false;
double hudAnchorX = 0.04, hudAnchorY = 0.05, hudScale = 1.0, hudSpacing = 0.018, hudOpacity = 0.70, hudTraceOpacity = 0.70, hudSafeMargin = 0.025;
bool hudClampToVisible = true;
double hudGreenThreshold = 75.0, hudRedThreshold = 90.0, hudTraceSensitivityMs = 2.0;
constexpr double kHudGpuWarningThreshold = 90.0, kHudGpuCriticalThreshold = 98.0;
double hudSysWarningThreshold=30.0,hudSysCriticalThreshold=10.0;
// Frame-pacing trace layout: X/Y anchor and width are fractions of the shared binocular
// overlap of the cropped views; scale multiplies the trace height; history is in samples.
double hudTraceX = 0.05, hudTraceY = 0.75, hudTraceScale = 1.0, hudTraceWidth = 0.42, hudTraceHistory = 120.0;
bool hudTraceEnabled = false;
uint32_t hudTraceVisibilityMode = 0; // 0 off, 1 always, 2 alarm only
float g_hudTraceVisibilityAlpha = 0.f;
viewlab::policy::TraceVisibilityState g_hudTraceVisibilityState{};
bool hudAlarmOnly = false;
double hudAlarmHoldMs = 1500.0;
double networkPingWarningMs=80.0,networkPingCriticalMs=150.0;
double networkLossWarning=2.0,networkLossCritical=5.0;
double networkJitterWarningMs=15.0,networkJitterCriticalMs=30.0;
uint32_t hudUpdateIntervalMs = 100;
bool hudDebugValues = false;
double hudDebugCpu = 52.0, hudDebugGpu = 98.0, hudDebugSystem = 44.0, hudDebugVr = 18.0;
enum class HudWidgetId : uint8_t {
    Cpu=0, Gpu=1, App=2, Vr=3, CpuPeak=4, CpuFrequency=5,
    Ram=6, Commit=7, Vram=8, Sys=9, Fps=10, FrameInterval=11,
    NetworkPing=12, NetworkLoss=13, NetworkJitter=14, NetworkStatus=15, Count=16
};
constexpr size_t kHudWidgetCount=static_cast<size_t>(HudWidgetId::Count);
std::array<double,kHudWidgetCount> hudWidgetWarning{{75,90,75,103,75,0,75,75,75,30,0,103,80,2,15,1}};
std::array<double,kHudWidgetCount> hudWidgetCritical{{90,98,90,108,90,0,90,90,90,10,0,108,150,5,30,2}};
constexpr uint64_t kHudLowerIsWorseMask=(1ull<<(size_t)HudWidgetId::CpuFrequency)|(1ull<<(size_t)HudWidgetId::Sys)|(1ull<<(size_t)HudWidgetId::Fps);
enum class HudMetricState : uint8_t { OnTarget=0, Warning=1, Critical=2, Reprojection=3, Unstable=4, Unavailable=5 };
enum class HudGraphMode : uint32_t { Deviation=0, Milliseconds=1, Fps=2, BudgetPercent=3 };
enum HudGraphChannel : uint32_t {
    GraphFrameInterval=1u<<0, GraphFps=1u<<1, GraphBudgetDeviation=1u<<2,
    GraphAppWork=1u<<3, GraphWaitDuration=1u<<4, GraphSubmitDuration=1u<<5,
    GraphDisplayPeriod=1u<<6
};
struct HudWidgetDescriptor {
    HudWidgetId id; const char* label; const char* unit; uint8_t decimals; bool cadenceRelative;
};
constexpr std::array<HudWidgetDescriptor,kHudWidgetCount> kHudWidgetRegistry{{
    {HudWidgetId::Cpu,"CPU","%",0,false}, {HudWidgetId::Gpu,"GPU","%",0,false},
    {HudWidgetId::App,"APP","%",0,false}, {HudWidgetId::Vr,"VR","ms",1,true},
    {HudWidgetId::CpuPeak,"PEAK","%",0,false}, {HudWidgetId::CpuFrequency,"CLK","MHz",0,false},
    {HudWidgetId::Ram,"RAM","%",0,false}, {HudWidgetId::Commit,"CMT","%",0,false},
    {HudWidgetId::Vram,"VRAM","%",0,false}, {HudWidgetId::Sys,"SYS","%",0,false},
    {HudWidgetId::Fps,"FPS","fps",0,true}, {HudWidgetId::FrameInterval,"FT","ms",1,true},
    {HudWidgetId::NetworkPing,"PING","ms",0,false}, {HudWidgetId::NetworkLoss,"LOSS","%",0,false},
    {HudWidgetId::NetworkJitter,"JIT","ms",0,false}, {HudWidgetId::NetworkStatus,"NET","state",0,false}
}};
uint64_t hudWidgetMask = (1ull<<0)|(1ull<<1)|(1ull<<3)|(1ull<<9);
uint32_t hudWidgetSymbolMask = 0;
uint32_t hudWidgetUnitHiddenMask = 0; // bit set = hide that metric's unit suffix (item 16)
uint32_t hudWidgetOrderPacked = 0x03020100u; // four widget IDs, low byte first
std::array<uint8_t,kHudWidgetCount> hudWidgetOrder{{0,1,9,3,2,4,5,6,7,8,10,11,12,13,14,15}};
uint32_t hudMaxPerRow=static_cast<uint32_t>(kHudWidgetCount); // legacy mapping field; renderer is deliberately one row
uint32_t hudGraphChannels = GraphBudgetDeviation;
HudGraphMode hudGraphMode = HudGraphMode::Deviation;
std::array<uint8_t,kHudWidgetCount> OrderedHudWidgets(uint32_t) { return hudWidgetOrder; }
uint32_t PackHudWidgetOrder(const std::array<int,4>& positions) {
    std::array<std::pair<int,uint8_t>,4> sorted{};
    for(uint8_t id=0;id<4;++id) sorted[id]={positions[id],id};
    std::stable_sort(sorted.begin(),sorted.end(),[](const auto&a,const auto&b){return a.first<b.first;});
    uint32_t packed=0; for(size_t slot=0;slot<4;++slot) packed|=(uint32_t)sorted[slot].second<<(slot*8);
    return packed;
}
struct HudMetric { double value = 0.0; bool available = false; };
struct HudFrameSample {
    double actualMs = 0.0, targetMs = 0.0, deviationMs = 0.0;
    double appWorkMs = 0.0, waitDurationMs = 0.0, submitDurationMs = 0.0, displayPeriodMs = 0.0;
    int64_t qpc = 0;
    uint32_t markerNumber = 0;
    double gpuPercent = -1.0;
    uint64_t warningMask = 0, criticalMask = 0, visibleAlarmMask = 0;
    bool overBudget = false;
};
HudMetric g_hudMetrics[kHudWidgetCount];
std::array<HudFrameSample, 600> g_hudFrameHistory{};
size_t g_hudFrameHistoryStart = 0, g_hudFrameHistoryCount = 0;
uint64_t g_hudNextUpdateTick = 0, g_hudLastIdle100ns = 0, g_hudLastKernel100ns = 0, g_hudLastUser100ns = 0;
PDH_HQUERY g_hudPdhQuery = nullptr;
PDH_HCOUNTER g_hudGpuCounter = nullptr;
LARGE_INTEGER g_hudQpcFrequency{};
double g_hudAppRawPercent = 0.0, g_hudAppSmoothedPercent = 0.0;
double g_hudLastAppWorkMs = 0.0, g_hudLastWaitDurationMs = 0.0, g_hudLastSubmitDurationMs = 0.0;
std::mutex g_hudTimingMutex;
// The post-session recording is the same QPC sample stream used by the visor trace, retained in a
// bounded ring for persistence at xrDestroySession. Markers are events in this stream, never log lines.
constexpr size_t kSessionTraceCapacity = 216000; // one hour at 60 samples/s; oldest samples roll off
std::vector<HudFrameSample> g_sessionTraceSamples;
size_t g_sessionTraceStart = 0;
struct PerformanceTraceMarker { uint32_t number = 0; int64_t qpc = 0; };
std::vector<PerformanceTraceMarker> g_sessionTraceMarkers;
int64_t g_sessionTraceStartQpc = 0;
int64_t g_sessionTraceStartUtcFileTime = 0;
bool performanceTraceRecording = true, g_traceMarkerKeyDown = false;
int performanceTraceMarkerKey = VK_F8;
uint32_t g_pendingTraceMarker = 0, g_nextTraceMarker = 1;
XrSession g_performanceTraceSession = XR_NULL_HANDLE;
uint64_t g_sessionTraceTotalSamples = 0, g_traceCheckpointSampleCursor = 0, g_traceCheckpointLastRebuild = 0;
size_t g_traceCheckpointMarkerCursor = 0;
bool g_traceCheckpointNeedsRebuild = true;
std::mutex g_traceFileMutex;
std::filesystem::path g_sessionTraceFilePath;
bool g_latestTraceIsHardLink=false;
std::atomic<uint32_t> g_traceMarkerConfirmation{0};
std::atomic<uint64_t> g_traceMarkerConfirmationUntil{0};
std::atomic<double> g_traceGpuPercent{-1.0};
std::atomic<uint64_t> g_traceWarningMask{0}, g_traceCriticalMask{0}, g_traceVisibleAlarmMask{0};
// Effective application cadence detection. The app's real frame interval is the QPC time
// between successive xrWaitFrame returns (the runtime throttles the app there, so under
// ASW/SSW/reprojection the interval settles at an integer multiple of the display period).
// A rolling median plus a consecutive-agreement counter means a single slow frame can
// never flip the detected cadence.
LARGE_INTEGER g_hudLastWaitStop{};
std::array<double, 90> g_hudIntervalRing{};
size_t g_hudIntervalCount = 0, g_hudIntervalNext = 0;
int g_hudCadenceMultiple = 1, g_hudCadenceCandidate = 1, g_hudCadenceStable = 0;
double g_hudDisplayPeriodMs = 0.0, g_hudFrameTimeMs = 0.0, g_hudEffectiveBudgetMs = 0.0;
HudMetricState g_hudVrState = HudMetricState::Unavailable;
int g_hudVrAmberSamples = 0, g_hudVrRedSamples = 0;
std::string g_hudVrColorReason = "initializing";
// Alarm-only mode: a metric's indicator is shown only while red. Entry uses the normal red
// threshold on the already-smoothed value; exit uses a lower threshold plus a hold time so
// indicators do not flicker at the boundary.
struct HudAlarmState {
    bool inAlarm = false;
    HudMetricState state = HudMetricState::Unavailable;
    viewlab::policy::SustainedAlarmState policy{};
};
HudAlarmState g_hudAlarm[kHudWidgetCount];

// ---- Render-boundary flash (feature 1) ----
// The UI raises a drag-active flag while the user drags any HUD/trace layout control. The layer
// then paints the exact cropped render boundary in both eyes and fades it out ~500 ms after the
// flag drops. The fade timer lives in native so it survives the UI closing mid-drag.
bool g_boundaryDragActive = false;
uint64_t g_boundaryReleaseTick = 0;      // tick when drag last went inactive; 0 = never held
constexpr uint64_t kBoundaryFadeMs = 500;

// ---- Counter-Strike-style static crosshair (feature 2) ----
// All values arrive already parsed/normalized from the UI (legacy cl_* commands and CS2 share
// codes are decoded there). Sizes are CS reference pixels scaled by chScale; the layer maps them
// into each eye's pixels at the calibrated stereo centre with zero angular disparity.
bool crosshairEnabled = false, crosshairDot = false, crosshairOutline = true, crosshairTStyle = false;
double crosshairSize = 5.0, crosshairGap = -2.0, crosshairThickness = 1.0, crosshairOutlineThickness = 1.0;
double crosshairAlpha = 1.0, crosshairScale = 1.0;
double crosshairOffsetX = 0.0, crosshairOffsetY = 0.0;
float crosshairR = 0.0f, crosshairG = 1.0f, crosshairB = 0.0f; // default CS green

// ---- Windows desktop notification mirror (feature 3) ----
// Native only reads pre-composited RGBA cards from a separate shared mapping; all collection,
// text layout, image decode, queueing, and animation happen in the UI process off the render
// thread. These are just the render-side settings.
bool notifyEnabled = false;
bool topmostVisorOverlays = false;
bool experimentalDrawInVoid = false; // Persisted future route request; deliberately has no rendering effect.
double notifyX = 0.98, notifyY = 0.98, notifyScale = 1.0, notifyOpacity = 1.0;

// ---- Dedicated clock and OpenXR-session timer widget ----
// The session clock starts at successful xrCreateSession and uses monotonic uptime; local time is
// read only for display. It is deliberately independent of notifications and performance alarms.
bool clockWidgetEnabled = false, clockSessionTimerEnabled = true, clock24Hour = true;
// clockWidgetTheme selects the visual DESIGN (0 Classic card, 1 Minimal, 2 Terminal,
// 3 Banner); clockWidgetPalette selects the colour set only (0 Graphite, 1 Paper, 2 OLED,
// 3 Amber, 4 Mint). Legacy configs stored the palette in clock_widget_theme; a missing
// clock_widget_palette key migrates that value so old selections keep their colours.
uint32_t clockWidgetTheme = 0;
uint32_t clockWidgetPalette = 0;
double clockWidgetX = 0.50, clockWidgetY = 0.10, clockWidgetScale = 1.0, clockWidgetOpacity = 0.82;
std::atomic<uint64_t> g_clockSessionStartTick{0};

// ---- Bounded sticky-note visor collection ----
bool stickyNoteEnabled=false;
struct StickyNoteConfig { bool enabled=true; double x=.78,y=.22,scale=1.0,opacity=.85; uint32_t theme=0; std::wstring text; };
constexpr size_t kStickyNoteMax=8;
std::array<StickyNoteConfig,kStickyNoteMax> stickyNotes{};
size_t stickyNoteCount=0;

enum class OverlayFeatureId : uint8_t { Hud=0, Trace=1, Clock=2, StickyNote=3, Crosshair=4, Notifications=5, Count=6 };
struct OverlayFeatureVisibility { int toggleKey=0; bool keyDown=false; std::atomic<bool> visible{true}; };
std::array<OverlayFeatureVisibility,(size_t)OverlayFeatureId::Count> g_overlayFeatureVisibility{};
bool OverlayFeatureVisible(OverlayFeatureId id) { return g_overlayFeatureVisibility[(size_t)id].visible.load(std::memory_order_acquire); }
void UpdateOverlayFeatureHotkeys() {
    for(auto& feature:g_overlayFeatureVisibility) {
        const bool down=feature.toggleKey>0&&(GetAsyncKeyState(feature.toggleKey)&0x8000)!=0;
        if(down&&!feature.keyDown)feature.visible.store(!feature.visible.load(std::memory_order_acquire),std::memory_order_release);
        feature.keyDown=down;
    }
}

bool obsIndicatorEnabled=false;
double obsIndicatorOpacity=.72,obsIndicatorThickness=.009;

enum MirrorFeature : uint32_t {
    MirrorVisor = 1u << 0,
    MirrorHud = 1u << 1,
    MirrorTrace = 1u << 2,
    MirrorClock = 1u << 3,
    MirrorNotifications = 1u << 4,
    MirrorStickyNotes = 1u << 5,
    MirrorCrosshair = 1u << 6,
    MirrorBoundaryFlash = 1u << 7,
    MirrorRecordingCue = 1u << 8,
    MirrorRacingCues = 1u << 9,
};
constexpr uint32_t kAllMirrorFeatures = (1u << 10) - 1u;
uint32_t obsMirrorVisibilityMask = kAllMirrorFeatures;
inline bool IncludesMirrorFeature(uint32_t mask, MirrorFeature feature) {
    return (mask & static_cast<uint32_t>(feature)) != 0;
}

// ---- Generic racing presentation (iRacing is one broker-side producer) ----
bool iracingEnabled = false, iracingLapPopup = false, iracingSpotterGlow = false, iracingFlagBorder = false;
// Race-start border light (item 5). Enable + visual params read at session start like the other iRacing keys.
bool iracingRaceStart = false;
double iracingRaceStartRedOpacity = 0.8, iracingRaceStartGreenOpacity = 0.8, iracingRaceStartGreenMs = 3000.0, iracingRaceStartThickness = 0.02;
// Rear-closing pressure cue (item 4): a top-centre glow driven by the packed reserved1 state.
bool iracingRearClosing = false;
double iracingRearClosingOpacity = 0.9;
// Grip-O-Bar (item 6): lower-left/right peripheral whole-car grip-loss bar from the packed gripState.
bool iracingGripBar = false;
double iracingGripBarOpacity = 0.9;
double iracingSpotterWidth = 0.12, iracingSpotterStrength = 1.0, iracingSpotterOpacity = 0.65, iracingSpotterFade = 1.8;
double iracingFlagWidth = 0.018, iracingFlagOpacity = 0.60;
uint32_t iracingSpotterColor = 0xFF4500;
struct HudTrackedFrame {
    uint64_t frameSerial = 0;
    XrTime displayTime = 0;
    XrDuration displayPeriod = 0;
    LARGE_INTEGER waitStart{}, waitStop{}, beginStart{}, beginStop{}, endStart{}, endStop{};
    bool canBegin = false, begun = false;
};
std::array<HudTrackedFrame, 3> g_hudTrackedFrames{};
size_t g_hudNextWaitFrame = 0;
std::atomic<uint64_t> g_calibrationFrameSerial{0};
inline bool AnyCalibrationPattern() {
    return calibrationGrid || calibrationRuler || calibrationGratings || calibrationBars ||
        calibrationBeacon || calibrationEdgeProbes || calibrationCheckerboards ||
        calibrationZonePlate || calibrationClippingSteps || calibrationMotionStrip;
}
// Notifications are "active" only while at least one card is live in the shared queue; that is
// evaluated per-frame in the draw path, so the coarse gate here simply includes notifyEnabled.
inline bool BoundaryFlashActive() { return g_boundaryDragActive || g_boundaryReleaseTick != 0; }
inline bool TraceMarkerConfirmationActive() { return g_traceMarkerConfirmationUntil.load(std::memory_order_acquire)>GetTickCount64(); }
inline bool AnyStickyNote(){for(size_t i=0;i<stickyNoteCount;++i)if(stickyNotes[i].enabled&&!stickyNotes[i].text.empty()&&stickyNotes[i].opacity>.001)return true;return false;}
inline bool AnyViewLabOverlay() { return (clockWidgetEnabled&&OverlayFeatureVisible(OverlayFeatureId::Clock)) || obsIndicatorEnabled || (stickyNoteEnabled&&OverlayFeatureVisible(OverlayFeatureId::StickyNote)&&AnyStickyNote()) || (crosshairEnabled&&OverlayFeatureVisible(OverlayFeatureId::Crosshair)) || (notifyEnabled&&OverlayFeatureVisible(OverlayFeatureId::Notifications)) || TraceMarkerConfirmationActive() || (iracingEnabled && (iracingLapPopup || iracingSpotterGlow || iracingFlagBorder || iracingRaceStart || iracingRearClosing || iracingGripBar)) || BoundaryFlashActive(); }
inline bool AnyDirectOverlay() { return maskEnabled || AnyCalibrationPattern() || (hudEnabled&&OverlayFeatureVisible(OverlayFeatureId::Hud)) || (hudTraceEnabled&&OverlayFeatureVisible(OverlayFeatureId::Trace)) || AnyViewLabOverlay(); }

uint64_t FileTimeToUint64(const FILETIME& time) {
    ULARGE_INTEGER value{}; value.LowPart = time.dwLowDateTime; value.HighPart = time.dwHighDateTime; return value.QuadPart;
}
double SmoothHudPercent(double previous, double raw) {
    const double clamped = std::clamp(raw, 0.0, 100.0);
    return previous > 0.0 ? previous * 0.75 + clamped * 0.25 : clamped;
}
bool GetHudRenderAdapterLuid(uint64_t& adapterLuid);
#if 0 // HardwareTelemetry.cpp owns PDH collection; never compile it into a render-boundary helper.
void InitHudCounters() {
    if (g_hudPdhQuery || PdhOpenQueryW(nullptr, 0, &g_hudPdhQuery) != ERROR_SUCCESS) return;
    PdhAddEnglishCounterW(g_hudPdhQuery, L"\\GPU Engine(*)\\Utilization Percentage", 0, &g_hudGpuCounter);
    PdhCollectQueryData(g_hudPdhQuery);
    QueryPerformanceFrequency(&g_hudQpcFrequency);
}
bool ParseGpuEngineInstance(const wchar_t* name, uint64_t& adapterLuid, uint32_t& physicalAdapter, uint32_t& engineId) {
    if (!name) return false;
    unsigned long pid = 0, luidHigh = 0, luidLow = 0, phys = 0, engine = 0;
    wchar_t engineType[64]{};
    if (swscanf_s(name, L"pid_%lu_luid_0x%lx_0x%lx_phys_%lu_eng_%lu_engtype_%63s",
        &pid, &luidHigh, &luidLow, &phys, &engine, engineType, static_cast<unsigned>(_countof(engineType))) != 6 ||
        _wcsicmp(engineType, L"3D") != 0) return false;
    adapterLuid = (static_cast<uint64_t>(luidHigh) << 32) | luidLow;
    physicalAdapter = phys;
    engineId = engine;
    return true;
}
double ReadGpuAdapterUtilization(uint64_t& selectedAdapterLuid) {
    selectedAdapterLuid = 0;
    if (!g_hudGpuCounter) return -1.0;
    DWORD count = 0, bufferSize = 0;
    if (PdhGetFormattedCounterArrayW(g_hudGpuCounter, PDH_FMT_DOUBLE, &bufferSize, &count, nullptr) != PDH_MORE_DATA || bufferSize == 0) return -1.0;
    std::vector<uint8_t> buffer(bufferSize);
    auto* values = reinterpret_cast<PDH_FMT_COUNTERVALUE_ITEM_W*>(buffer.data());
    if (PdhGetFormattedCounterArrayW(g_hudGpuCounter, PDH_FMT_DOUBLE, &bufferSize, &count, values) != ERROR_SUCCESS) return -1.0;
    std::map<std::pair<uint64_t, uint32_t>, double> engines;
    for (DWORD i = 0; i < count; ++i) {
        if (values[i].FmtValue.CStatus != ERROR_SUCCESS || !std::isfinite(values[i].FmtValue.doubleValue)) continue;
        uint64_t luid = 0; uint32_t phys = 0, engine = 0;
        if (!ParseGpuEngineInstance(values[i].szName, luid, phys, engine)) continue;
        engines[{luid, engine}] += (std::max)(0.0, values[i].FmtValue.doubleValue);
    }
    std::map<uint64_t, double> adapters;
    for (const auto& entry : engines) adapters[entry.first.first] = (std::max)(adapters[entry.first.first], std::clamp(entry.second, 0.0, 100.0));
    if (adapters.empty()) return -1.0;
    uint64_t renderLuid = 0;
    if (GetHudRenderAdapterLuid(renderLuid)) {
        const auto renderAdapter = adapters.find(renderLuid);
        if (renderAdapter != adapters.end()) { selectedAdapterLuid = renderLuid; return renderAdapter->second; }
    }
    const auto busiest = std::max_element(adapters.begin(), adapters.end(), [](const auto& a, const auto& b) { return a.second < b.second; });
    selectedAdapterLuid = busiest->first;
    return busiest->second;
}
#endif
// Frame-pacing trace samples: one per app frame interval, deviation measured against the
// effective (cadence-aware) frame budget so the baseline is correct under half-rate modes.
void RecordHudFrameSample(double actualMs, double targetMs, int64_t qpc) {
    if (!std::isfinite(actualMs) || !std::isfinite(targetMs) || actualMs < 0.0 || targetMs <= 0.0) return;
    HudFrameSample sample{};
    sample.actualMs=actualMs; sample.targetMs=targetMs; sample.deviationMs=actualMs-targetMs;
    sample.appWorkMs=g_hudLastAppWorkMs; sample.waitDurationMs=g_hudLastWaitDurationMs;
    sample.submitDurationMs=g_hudLastSubmitDurationMs; sample.displayPeriodMs=g_hudDisplayPeriodMs;
    sample.qpc=qpc; sample.markerNumber=g_pendingTraceMarker; g_pendingTraceMarker=0;
    sample.gpuPercent=g_traceGpuPercent.load(std::memory_order_acquire);
    sample.warningMask=g_traceWarningMask.load(std::memory_order_acquire);
    sample.criticalMask=g_traceCriticalMask.load(std::memory_order_acquire);
    sample.visibleAlarmMask=g_traceVisibleAlarmMask.load(std::memory_order_acquire);
    sample.overBudget=actualMs > targetMs * 1.03;
    const size_t index = (g_hudFrameHistoryStart + g_hudFrameHistoryCount) % g_hudFrameHistory.size();
    if (g_hudFrameHistoryCount == g_hudFrameHistory.size()) {
        g_hudFrameHistory[g_hudFrameHistoryStart] = sample;
        g_hudFrameHistoryStart = (g_hudFrameHistoryStart + 1) % g_hudFrameHistory.size();
    } else {
        g_hudFrameHistory[index] = sample;
        ++g_hudFrameHistoryCount;
    }
    if (performanceTraceRecording) {
        if (g_sessionTraceSamples.size() < kSessionTraceCapacity) g_sessionTraceSamples.push_back(sample);
        else { g_sessionTraceSamples[g_sessionTraceStart]=sample; g_sessionTraceStart=(g_sessionTraceStart+1)%kSessionTraceCapacity; }
        ++g_sessionTraceTotalSamples;
    }
}
// APP workload is the application-side wall-clock window after xrBeginFrame returns and before
// xrEndFrame is entered. It excludes runtime blocking inside both calls and is expressed against
// the cadence-aware budget. It is not presented as exact thread CPU execution time.
void RecordHudAppWorkSample(double appWorkMs, double budgetMs, double submitDurationMs) {
    if (!std::isfinite(appWorkMs) || !std::isfinite(budgetMs) || appWorkMs < 0.0 || budgetMs <= 0.0) return;
    g_hudLastAppWorkMs = appWorkMs;
    g_hudLastSubmitDurationMs = (std::max)(0.0, submitDurationMs);
    g_hudAppRawPercent = std::clamp(appWorkMs / budgetMs * 100.0, 0.0, 199.0);
    const double clampedForDisplay = std::clamp(g_hudAppRawPercent, 0.0, 100.0);
    g_hudAppSmoothedPercent = SmoothHudPercent(g_hudAppSmoothedPercent, clampedForDisplay);
}
// Cadence and pacing update, called under g_hudTimingMutex from xrWaitFrame.
void UpdateHudCadence(const LARGE_INTEGER& waitStop, XrDuration displayPeriod) {
    const double periodMs = displayPeriod > 0 ? static_cast<double>(displayPeriod) / 1000000.0 : 0.0;
    if (periodMs > 0.0) g_hudDisplayPeriodMs = periodMs;
    if (g_hudLastWaitStop.QuadPart > 0 && g_hudQpcFrequency.QuadPart > 0 && g_hudDisplayPeriodMs > 0.0) {
        const double intervalMs = 1000.0 * static_cast<double>(waitStop.QuadPart - g_hudLastWaitStop.QuadPart) / g_hudQpcFrequency.QuadPart;
        // Ignore hitches and pauses far outside plausible cadence so they cannot poison the median.
        if (intervalMs > g_hudDisplayPeriodMs * 0.3 && intervalMs < g_hudDisplayPeriodMs * 6.0) {
            g_hudIntervalRing[g_hudIntervalNext] = intervalMs;
            g_hudIntervalNext = (g_hudIntervalNext + 1) % g_hudIntervalRing.size();
            if (g_hudIntervalCount < g_hudIntervalRing.size()) ++g_hudIntervalCount;
            if (g_hudIntervalCount >= 12) {
                std::array<double, 90> sorted{};
                std::copy_n(g_hudIntervalRing.begin(), g_hudIntervalCount, sorted.begin());
                std::nth_element(sorted.begin(), sorted.begin() + g_hudIntervalCount / 2, sorted.begin() + g_hudIntervalCount);
                const double medianMs = sorted[g_hudIntervalCount / 2];
                const int rawMultiple = std::clamp(static_cast<int>(std::lround(medianMs / g_hudDisplayPeriodMs)), 1, 4);
                if (rawMultiple == g_hudCadenceCandidate) ++g_hudCadenceStable;
                else { g_hudCadenceCandidate = rawMultiple; g_hudCadenceStable = 1; }
                if (g_hudCadenceStable >= 20 && g_hudCadenceMultiple != g_hudCadenceCandidate) {
                    g_hudCadenceMultiple = g_hudCadenceCandidate;
                    // Samples recorded against the previous cadence budget are not comparable.
                    g_hudFrameHistoryStart = g_hudFrameHistoryCount = 0;
                    Log("HUD cadence: effective app cadence is 1/%d of display rate (period=%.2fms budget=%.2fms)\n",
                        g_hudCadenceMultiple, g_hudDisplayPeriodMs, g_hudDisplayPeriodMs * g_hudCadenceMultiple);
                }
            }
            g_hudEffectiveBudgetMs = g_hudDisplayPeriodMs * g_hudCadenceMultiple;
            g_hudFrameTimeMs = g_hudFrameTimeMs > 0.0 ? g_hudFrameTimeMs * 0.8 + intervalMs * 0.2 : intervalMs;
            RecordHudFrameSample(intervalMs, g_hudEffectiveBudgetMs, waitStop.QuadPart);
        }
    }
    g_hudLastWaitStop = waitStop;
}
// Shared time-based alarm policy for every widget. It is independent of draw/update frequency,
// and the recovery hold starts from the first non-critical input rather than the latched red state.
void UpdateHudState(int index, HudMetricState desired, uint64_t nowTick) {
    HudAlarmState& alarm = g_hudAlarm[index];
    auto severity=[](HudMetricState state) {
        if(state==HudMetricState::Warning)return 1;
        if(state==HudMetricState::Critical || state==HudMetricState::Unstable)return 2;
        if(state==HudMetricState::Unavailable)return -1;
        return 0;
    };
    viewlab::policy::UpdateSustainedAlarm(alarm.policy, static_cast<int>(desired), severity(desired),
        nowTick, 750, 750, static_cast<uint64_t>(std::clamp(hudAlarmHoldMs,0.0,10000.0)));
    alarm.state=static_cast<HudMetricState>(alarm.policy.stableState);
    alarm.inAlarm=alarm.policy.visible;
}
HudMetricState ClassifyHudMetric(size_t index, const HudMetric& metric) {
    if(!metric.available) return HudMetricState::Unavailable;
    double value=metric.value;
    if((index==(size_t)HudWidgetId::Vr||index==(size_t)HudWidgetId::FrameInterval) && g_hudEffectiveBudgetMs>0.0)
        value=100.0*metric.value/g_hudEffectiveBudgetMs;
    const double warning=hudWidgetWarning[index],critical=hudWidgetCritical[index];
    if(warning<=0.0&&critical<=0.0)return HudMetricState::OnTarget;
    const bool lower=(kHudLowerIsWorseMask&(1ull<<index))!=0;
    if(lower) {
        if(critical>0.0&&value<=critical)return HudMetricState::Critical;
        if(warning>0.0&&value<=warning)return HudMetricState::Warning;
    } else {
        if(critical>0.0&&value>=critical)return HudMetricState::Critical;
        if(warning>0.0&&value>=warning)return HudMetricState::Warning;
    }
    return HudMetricState::OnTarget;
}
void UpdateHudMetrics() {
    auto publishTraceState=[] {
        const HudMetric& gpu=g_hudMetrics[(size_t)HudWidgetId::Gpu];
        g_traceGpuPercent.store(gpu.available?gpu.value:-1.0,std::memory_order_release);
        uint64_t warning=0,critical=0,visible=0;
        for(size_t i=0;i<kHudWidgetCount&&i<64;++i) {
            const uint64_t bit=1ull<<i;
            if(g_hudAlarm[i].state==HudMetricState::Warning) warning|=bit;
            else if(g_hudAlarm[i].state==HudMetricState::Critical) critical|=bit;
            if(g_hudAlarm[i].inAlarm) visible|=bit;
        }
        g_traceWarningMask.store(warning,std::memory_order_release);
        g_traceCriticalMask.store(critical,std::memory_order_release);
        g_traceVisibleAlarmMask.store(visible,std::memory_order_release);
    };
    const uint64_t now = GetTickCount64();
    if (now < g_hudNextUpdateTick) return;
    g_hudNextUpdateTick = now + (std::max)(uint32_t{50}, hudUpdateIntervalMs);
    if (hudDebugValues) {
        g_hudMetrics[0] = { std::clamp(hudDebugCpu, 0.0, 100.0), true };
        g_hudMetrics[1] = { std::clamp(hudDebugGpu, 0.0, 100.0), true };
        g_hudMetrics[2] = { std::clamp(hudDebugSystem, 0.0, 100.0), true };
        // Debug VR value is interpreted as milliseconds of app frame time.
        g_hudMetrics[3] = { std::clamp(hudDebugVr, 0.0, 999.9), true };
        if (g_hudEffectiveBudgetMs <= 0.0) g_hudEffectiveBudgetMs = 11.1;
        for (int i = 0; i < 3; ++i) {
            UpdateHudState(i,ClassifyHudMetric((size_t)i,g_hudMetrics[i]),now);
        }
        UpdateHudState(3,ClassifyHudMetric((size_t)HudWidgetId::Vr,g_hudMetrics[3]),now);
        g_hudVrState=g_hudAlarm[3].state;
        LogVerbose("HUD telemetry: DEBUG raw CPU=%.2f%% GPU=%.2f%% APP=%.2f%% VRms=%.2f; displayed=%.2f%%/%.2f%%/%.2f%%/%.1fms\n",
            hudDebugCpu, hudDebugGpu, hudDebugSystem, hudDebugVr, g_hudMetrics[0].value, g_hudMetrics[1].value, g_hudMetrics[2].value, g_hudMetrics[3].value);
        publishTraceState(); return;
    }
    // Hardware collection is deliberately absent from this render-thread function. The worker
    // owns PDH/DXGI/system calls and publishes a completed snapshot; a contended snapshot is
    // simply skipped, leaving the previous display intact.
    viewlab::telemetry::Snapshot hardware{};
    if(viewlab::telemetry::TryGetSnapshot(hardware)) {
        auto copyHardware=[&](HudWidgetId widget,viewlab::telemetry::MetricId metric) {
            const auto& source=hardware.metrics[static_cast<size_t>(metric)];
            const size_t target=static_cast<size_t>(widget);
            g_hudMetrics[target]={source.value,source.availability==viewlab::telemetry::Availability::Available};
        };
        copyHardware(HudWidgetId::Cpu,viewlab::telemetry::MetricId::CpuUtilisation);
        copyHardware(HudWidgetId::Gpu,viewlab::telemetry::MetricId::GpuUtilisation);
        copyHardware(HudWidgetId::CpuPeak,viewlab::telemetry::MetricId::CpuPeakCore);
        copyHardware(HudWidgetId::CpuFrequency,viewlab::telemetry::MetricId::CpuFrequency);
        copyHardware(HudWidgetId::Ram,viewlab::telemetry::MetricId::RamPressure);
        copyHardware(HudWidgetId::Commit,viewlab::telemetry::MetricId::CommitPressure);
        copyHardware(HudWidgetId::Vram,viewlab::telemetry::MetricId::VramPressure);
        copyHardware(HudWidgetId::Sys,viewlab::telemetry::MetricId::SystemHeadroom);
        copyHardware(HudWidgetId::NetworkPing,viewlab::telemetry::MetricId::NetworkPing);
        copyHardware(HudWidgetId::NetworkLoss,viewlab::telemetry::MetricId::NetworkLoss);
        copyHardware(HudWidgetId::NetworkJitter,viewlab::telemetry::MetricId::NetworkJitter);
        copyHardware(HudWidgetId::NetworkStatus,viewlab::telemetry::MetricId::NetworkStatus);
    }
    {
        std::lock_guard<std::mutex> lock(g_hudTimingMutex);
        if(g_hudAppSmoothedPercent>0.0||g_hudAppRawPercent>0.0)g_hudMetrics[(size_t)HudWidgetId::App]={std::clamp(g_hudAppSmoothedPercent,0.0,100.0),true};
        const bool cadenceAvailable=g_hudIntervalCount>=2&&g_hudFrameTimeMs>0.0&&g_hudEffectiveBudgetMs>0.0;
        g_hudMetrics[(size_t)HudWidgetId::Vr]={std::clamp(g_hudFrameTimeMs,0.0,999.9),cadenceAvailable};
        g_hudMetrics[(size_t)HudWidgetId::FrameInterval]=g_hudMetrics[(size_t)HudWidgetId::Vr];
        g_hudMetrics[(size_t)HudWidgetId::Fps]={cadenceAvailable?1000.0/g_hudFrameTimeMs:0.0,cadenceAvailable};
    }
    for(size_t i=0;i<kHudWidgetCount;++i) {
        UpdateHudState((int)i,ClassifyHudMetric(i,g_hudMetrics[i]),now);
    }
    g_hudVrState=g_hudAlarm[(size_t)HudWidgetId::Vr].state;
    publishTraceState();
    return;
}

#if 0 // Superseded render-thread collectors retained only as historical recovery reference.
    InitHudCounters();
    FILETIME idle{}, kernel{}, user{};
    double cpuRaw = 0.0; uint64_t cpuIdleDelta = 0, cpuTotalDelta = 0; bool cpuAvailable = false;
    if (GetSystemTimes(&idle, &kernel, &user)) {
        const uint64_t idleNow = FileTimeToUint64(idle), kernelNow = FileTimeToUint64(kernel), userNow = FileTimeToUint64(user);
        if (g_hudLastKernel100ns && kernelNow >= g_hudLastKernel100ns && userNow >= g_hudLastUser100ns && idleNow >= g_hudLastIdle100ns) {
            cpuIdleDelta = idleNow - g_hudLastIdle100ns;
            cpuTotalDelta = kernelNow - g_hudLastKernel100ns + userNow - g_hudLastUser100ns;
            if (cpuTotalDelta > 0) {
                cpuRaw = std::clamp(100.0 * (1.0 - static_cast<double>(cpuIdleDelta) / cpuTotalDelta), 0.0, 100.0);
                g_hudMetrics[0] = { SmoothHudPercent(g_hudMetrics[0].available ? g_hudMetrics[0].value : 0.0, cpuRaw), true };
                cpuAvailable = true;
            }
        }
        g_hudLastIdle100ns = idleNow; g_hudLastKernel100ns = kernelNow; g_hudLastUser100ns = userNow;
    }
    double gpuRaw = 0.0; uint64_t gpuLuid = 0; bool gpuAvailable = false;
    if (g_hudPdhQuery && PdhCollectQueryData(g_hudPdhQuery) == ERROR_SUCCESS) {
        const double gpuSample = ReadGpuAdapterUtilization(gpuLuid);
        if (gpuSample >= 0.0) {
            gpuRaw = std::clamp(gpuSample, 0.0, 100.0);
            g_hudMetrics[1] = { SmoothHudPercent(g_hudMetrics[1].available ? g_hudMetrics[1].value : 0.0, gpuRaw), true };
            gpuAvailable = true;
        }
    }
    if (!cpuAvailable) g_hudMetrics[0].available = false;
    if (!gpuAvailable) g_hudMetrics[1].available = false;
    double appRaw = 0.0, frameMs = 0.0, budgetMs = 0.0, periodMs = 0.0;
    int cadence = 1;
    {
        std::lock_guard<std::mutex> lock(g_hudTimingMutex);
        appRaw = g_hudAppRawPercent;
        if (g_hudAppSmoothedPercent > 0.0 || g_hudAppRawPercent > 0.0)
            g_hudMetrics[2] = { std::clamp(g_hudAppSmoothedPercent, 0.0, 100.0), true };
        frameMs = g_hudFrameTimeMs; budgetMs = g_hudEffectiveBudgetMs;
        periodMs = g_hudDisplayPeriodMs; cadence = g_hudCadenceMultiple;
        if (g_hudIntervalCount >= 2 && frameMs > 0.0 && budgetMs > 0.0)
            g_hudMetrics[3] = { std::clamp(frameMs, 0.0, 999.9), true };
        else g_hudMetrics[3].available = false;
    }
    for (int i = 0; i < 3; ++i) {
        const HudMetric& metric=g_hudMetrics[i];
        const HudMetricState desired=!metric.available?HudMetricState::Unavailable:
            metric.value>=hudRedThreshold?HudMetricState::Critical:
            metric.value>=hudGreenThreshold?HudMetricState::Warning:HudMetricState::OnTarget;
        UpdateHudState(i,desired,now);
    }
    int missCount = 0, meaningfulMissCount = 0;
    double rollingRatio = 0.0;
    std::array<double,60> ratios{}; size_t ratioCount=0;
    {
        std::lock_guard<std::mutex> lock(g_hudTimingMutex);
        const size_t n=(std::min)(g_hudFrameHistoryCount,size_t{60});
        for(size_t i=g_hudFrameHistoryCount-n;i<g_hudFrameHistoryCount;++i){const auto&s=g_hudFrameHistory[(g_hudFrameHistoryStart+i)%g_hudFrameHistory.size()];if(s.actualMs>s.targetMs*1.05)++missCount;if(s.actualMs>s.targetMs*1.15)++meaningfulMissCount;if(s.targetMs>0.0)ratios[ratioCount++]=s.actualMs/s.targetMs;}
        if(ratioCount){std::sort(ratios.begin(),ratios.begin()+ratioCount);rollingRatio=ratios[ratioCount/2];}
    }
    double spread=0.0;
    if(ratioCount>=10) spread=ratios[(ratioCount*9)/10]-ratios[ratioCount/10];
    HudMetricState cadenceDesired=HudMetricState::Unavailable;
    if(g_hudMetrics[3].available && ratioCount>=12) {
        const bool transition=g_hudCadenceCandidate!=g_hudCadenceMultiple && g_hudCadenceStable>=4;
        if(transition || spread>0.18) cadenceDesired=HudMetricState::Unstable;
        else if(g_hudCadenceMultiple>1 && g_hudCadenceStable>=20 && rollingRatio>=0.94 && rollingRatio<=1.06) cadenceDesired=HudMetricState::Reprojection;
        else if(rollingRatio>1.08) cadenceDesired=HudMetricState::Critical;
        else if(rollingRatio>1.03) cadenceDesired=HudMetricState::Warning;
        else cadenceDesired=HudMetricState::OnTarget;
    }
    UpdateHudState(3,cadenceDesired,now);
    g_hudVrState=g_hudAlarm[3].state;
    g_hudVrColorReason = g_hudVrState==HudMetricState::Critical ? "sustained cadence above active budget" :
        g_hudVrState==HudMetricState::Warning ? "approaching active cadence budget" :
        g_hudVrState==HudMetricState::Reprojection ? "stable cadence division" :
        g_hudVrState==HudMetricState::Unstable ? "cadence multiple or rolling spread is unstable" : "on active runtime target";
    LogVerbose("HUD telemetry: CPU source=GetSystemTimes unit=100ns idle_delta=%llu total_delta=%llu conversion=100*(1-idle/total) raw=%.2f%% available=%d displayed=%.2f%%; GPU source=PDH_GPU_Engine unit=percent adapterLuid=0x%016llx conversion=max(group_by_luid_3D_engine) raw=%.2f%% available=%d displayed=%.2f%%; APP source=OpenXR_QPC unit=ms conversion=begin_return_to_end_entry/effective_budget*100 raw=%.2f%% available=%d displayed=%.2f%%; VR source=xrWaitFrame_QPC_interval unit=ms period=%.3f cadence_multiple=%d budget=%.3f conversion=EMA(interval) frame=%.3f available=%d displayed=%.1fms\n",
        static_cast<unsigned long long>(cpuIdleDelta), static_cast<unsigned long long>(cpuTotalDelta), cpuRaw, cpuAvailable ? 1 : 0, g_hudMetrics[0].value,
        static_cast<unsigned long long>(gpuLuid), gpuRaw, gpuAvailable ? 1 : 0, g_hudMetrics[1].value,
        appRaw, g_hudMetrics[2].available ? 1 : 0, g_hudMetrics[2].value,
        periodMs, cadence, budgetMs, frameMs, g_hudMetrics[3].available ? 1 : 0, g_hudMetrics[3].value);
    LogVerbose("HUD pacing decision: refresh=%.2fHz effective=%.2fHz target=%.4fms raw/latest=%.4fms smoothed=%.4fms rollingRatio=%.4f misses=%d meaningful=%d state=%s reason=%s\n",
        periodMs>0?1000.0/periodMs:0.0, budgetMs>0?1000.0/budgetMs:0.0,budgetMs,
        g_hudFrameHistoryCount?g_hudFrameHistory[(g_hudFrameHistoryStart+g_hudFrameHistoryCount-1)%g_hudFrameHistory.size()].actualMs:0.0,
        frameMs,rollingRatio,missCount,meaningfulMissCount,g_hudVrState==HudMetricState::Critical?"critical":g_hudVrState==HudMetricState::Warning?"warning":g_hudVrState==HudMetricState::Reprojection?"reprojection":g_hudVrState==HudMetricState::Unstable?"unstable":"target",g_hudVrColorReason.c_str());
}
#endif

#pragma pack(push, 4)
struct LiveStateBlock {
    uint32_t magic, version, size, generation, calibrationMask, flags;
    float visorSize, maskCorner, apexY, innerLow, bridgeWidth, bridgeRise, bridgePeakX, bridgeSteepness;
    float hudAnchorX, hudAnchorY, hudScale, hudSafeMargin;
    uint32_t hudFlags;
    float traceSensitivityMs, traceX, traceY, traceScale, traceWidth, traceHistory, alarmHoldMs;
    // v4 additions -------------------------------------------------------------------------------
    uint32_t interactFlags;   // bit0 = a HUD/trace layout control is being dragged (boundary flash)
    uint32_t crosshairFlags;  // bit0 enabled, bit1 dot, bit2 outline, bit3 t-style
    float chSize, chGap, chThickness, chOutlineThickness, chAlpha, chScale;
    uint32_t chColor;         // 0xAARRGGBB
    uint32_t notifyFlags;     // bit0 enabled, bit1 show icon, bit2 show image
    float notifyX, notifyY, notifyScale, notifyOpacity, notifyDurationMs;
    uint32_t notifyMaxVisible, notifyPrivacy; // privacy: 0 full, 1 title only, 2 app only
    uint32_t iracingFlags;    // bit0 enabled, bit1 lap popup, bit2 spotter glow, bit3 flag border; v12: bit4 race start, bit5 rear closing, bit6 Grip-O-Bar
    uint32_t traceFlags;      // v5: bit0 independent performance trace enabled
    float crosshairOffsetX, crosshairOffsetY; // normalized full-lens tangent coordinates
    uint32_t topmostFlags;    // v6: bit0 experimental topmost composition layer
    uint32_t hudWidgetMask, hudWidgetOrder, hudGraphChannels, hudGraphMode; // v7
    uint32_t clockFlags; float clockX,clockY,clockScale,clockOpacity; uint32_t clockTheme; float visorNoseSpreadX;
    uint32_t overlayToggleKeys[6]; // v8
    uint32_t obsMirrorVisibilityMask; // v9
    uint32_t clockPalette; // v10: clockTheme is now the layout design; palette is colours only
    uint32_t visorColor;   // v11: 0x00RRGGBB visor fill colour (0 = black, unchanged default)
    // v12: iRacing cue tuning is live (previously ini-only, applied on session restart). The
    // iracingFlags word gains bit4 race start, bit5 rear closing, bit6 Grip-O-Bar.
    float irSpotterWidth, irSpotterStrength, irSpotterOpacity, irSpotterFade;
    uint32_t irSpotterColor;  // 0x00RRGGBB
    float irFlagWidth, irFlagOpacity;
    float irRaceStartRedOpacity, irRaceStartGreenOpacity, irRaceStartGreenMs, irRaceStartThickness;
    float irRearClosingOpacity, irGripBarOpacity;
};
#pragma pack(pop)
static_assert(sizeof(LiveStateBlock)==324,"live state v12 contract size");
constexpr uint32_t kLiveStateMagic = 0x534C4C56; // VLLS
HANDLE g_liveStateMap = nullptr;
const LiveStateBlock* g_liveState = nullptr;
uint32_t g_liveStateGeneration = 0;
uint64_t g_liveStateNextConnectTick = 0;

void ConnectLiveState() {
    if (g_liveState) return;
    const uint64_t now = GetTickCount64();
    if (now < g_liveStateNextConnectTick) return;
    // The settings app is normally closed while a game runs. Avoid an OpenFileMapping
    // system call every submitted frame, but reconnect promptly when the app opens.
    g_liveStateNextConnectTick = now + 1000;
    g_liveStateMap = OpenFileMappingW(FILE_MAP_READ, FALSE, L"Local\\XRViewLabLiveState");
    if (!g_liveStateMap) return;
    g_liveState = static_cast<const LiveStateBlock*>(MapViewOfFile(g_liveStateMap, FILE_MAP_READ, 0, 0, sizeof(LiveStateBlock)));
    if (!g_liveState) { CloseHandle(g_liveStateMap); g_liveStateMap = nullptr; }
}
void DisconnectLiveState() { if (g_liveState) { UnmapViewOfFile(g_liveState); g_liveState = nullptr; } if (g_liveStateMap) { CloseHandle(g_liveStateMap); g_liveStateMap = nullptr; } g_liveStateGeneration = 0; g_liveStateNextConnectTick = 0; }

#pragma pack(push,4)
struct TelemetryConfigBlock { uint32_t magic,version,size,generation; uint64_t widgetMask; uint8_t order[16]; uint32_t maxPerRow; float sysWarning,sysCritical; uint32_t flags,reserved[2]; };
#pragma pack(pop)
HANDLE g_telemetryConfigMap=nullptr; const TelemetryConfigBlock* g_telemetryConfig=nullptr; uint32_t g_telemetryConfigGeneration=0;
void ConsumeTelemetryConfig() {
    if(!g_telemetryConfig){g_telemetryConfigMap=OpenFileMappingW(FILE_MAP_READ,FALSE,L"Local\\XRViewLabTelemetryConfigV1");if(g_telemetryConfigMap)g_telemetryConfig=(const TelemetryConfigBlock*)MapViewOfFile(g_telemetryConfigMap,FILE_MAP_READ,0,0,sizeof(TelemetryConfigBlock));}
    if(!g_telemetryConfig||g_telemetryConfig->magic!=0x31435456u||g_telemetryConfig->version!=1||g_telemetryConfig->size!=64||g_telemetryConfig->generation==g_telemetryConfigGeneration)return;
    const TelemetryConfigBlock stable=*g_telemetryConfig;if(stable.generation!=g_telemetryConfig->generation)return;
    if((profileOverlayOverrideMask&(1u<<(uint32_t)OverlayFeatureId::Hud))==0){hudWidgetMask=stable.widgetMask&((1ull<<kHudWidgetCount)-1);hudWidgetSymbolMask=stable.flags&0xFFFFu;hudWidgetUnitHiddenMask=stable.reserved[0];hudMaxPerRow=std::clamp(stable.maxPerRow,1u,16u);hudSysWarningThreshold=std::clamp((double)stable.sysWarning,10.0,60.0);hudSysCriticalThreshold=std::clamp((double)stable.sysCritical,0.0,hudSysWarningThreshold);
    viewlab::telemetry::SetNetworkProbeEnabled((hudWidgetMask & (0xFull<<12)) != 0);
    std::array<bool,kHudWidgetCount> seen{};size_t n=0;for(uint8_t id:stable.order)if(id<kHudWidgetCount&&!seen[id]){hudWidgetOrder[n++]=id;seen[id]=true;}for(uint8_t id=0;id<kHudWidgetCount;++id)if(!seen[id])hudWidgetOrder[n++]=id;}
    g_telemetryConfigGeneration=stable.generation;
}
void DisconnectTelemetryConfig(){if(g_telemetryConfig)UnmapViewOfFile(g_telemetryConfig);if(g_telemetryConfigMap)CloseHandle(g_telemetryConfigMap);g_telemetryConfig=nullptr;g_telemetryConfigMap=nullptr;g_telemetryConfigGeneration=0;}

// ---- Notification content bridge (feature 3, read-only for the layer) ----
// The UI process composites each notification card (app icon, image/avatar, title/sender, and a
// shortened body) into a fixed-size top-down RGBA bitmap and runs queue lifecycle off the render
// thread. The layer evaluates timestamped fade/slide at render cadence; it never collects, decodes,
// polls Windows, or rewrites pixels per frame.
constexpr uint32_t kNotifyMagic = 0x314E4C56; // "VLN1"
constexpr uint32_t kNotifyMaxCards = 6;
// Supersampled slot: logical footprint max (336x96) x supersample cap (3) = 1008x288. The UI
// composites each card at raster dimensions (physical scale x quality) into this slot; the layer
// samples only the used card.width x card.height sub-rectangle. Keep in sync with
// NotificationCardLayout (SlotW/SlotH) and the notify block version.
constexpr uint32_t kNotifyCardW = 1008;
constexpr uint32_t kNotifyCardH = 288;
#pragma pack(push, 4)
struct NotifyCardBlock {
    uint32_t active;        // 1 = slot carries a live card this generation
    uint32_t id;            // stable id so the layer re-uploads pixels only on real change
    uint32_t width, height; // used pixels within the fixed card texture
    uint32_t enterTick;     // low GetTickCount64 bits; native animates at render cadence
    uint32_t leaveTick;     // zero until expiry begins
    uint32_t stackIndex;    // 0 = bottom card; higher stacks upward
    uint32_t contentSerial; // bumps whenever the RGBA changes
    uint8_t rgba[kNotifyCardW * kNotifyCardH * 4]; // top-down BGRA-as-RGBA, straight alpha
};
struct NotifyBlock {
    uint32_t magic, version, count, generation;
    NotifyCardBlock cards[kNotifyMaxCards];
};
#pragma pack(pop)
HANDLE g_notifyMap = nullptr;
const NotifyBlock* g_notify = nullptr;
uint64_t g_notifyNextConnectTick = 0;
void ConnectNotify() {
    if (g_notify) return;
    const uint64_t now = GetTickCount64();
    if (now < g_notifyNextConnectTick) return;
    g_notifyNextConnectTick = now + 1000;
    g_notifyMap = OpenFileMappingW(FILE_MAP_READ, FALSE, L"Local\\XRViewLabNotifications");
    if (!g_notifyMap) return;
    g_notify = static_cast<const NotifyBlock*>(MapViewOfFile(g_notifyMap, FILE_MAP_READ, 0, 0, sizeof(NotifyBlock)));
    if (!g_notify) { CloseHandle(g_notifyMap); g_notifyMap = nullptr; }
}
void DisconnectNotify() { if (g_notify) { UnmapViewOfFile(g_notify); g_notify = nullptr; } if (g_notifyMap) { CloseHandle(g_notifyMap); g_notifyMap = nullptr; } g_notifyNextConnectTick = 0; }

#pragma pack(push,4)
struct ObsRecordingStateBlock{uint32_t magic,version,generation,state;};
#pragma pack(pop)
HANDLE g_obsStateMap=nullptr;const ObsRecordingStateBlock* g_obsState=nullptr;uint64_t g_obsNextConnectTick=0;
void ConnectObsState(){if(g_obsState)return;const uint64_t now=GetTickCount64();if(now<g_obsNextConnectTick)return;g_obsNextConnectTick=now+1000;g_obsStateMap=OpenFileMappingW(FILE_MAP_READ,FALSE,L"Local\\XRViewLabObsRecordingState");if(g_obsStateMap)g_obsState=(const ObsRecordingStateBlock*)MapViewOfFile(g_obsStateMap,FILE_MAP_READ,0,0,sizeof(ObsRecordingStateBlock));if(!g_obsState&&g_obsStateMap){CloseHandle(g_obsStateMap);g_obsStateMap=nullptr;}}
bool ObsRecordingActive(){if(!obsIndicatorEnabled)return false;ConnectObsState();if(!g_obsState||g_obsState->magic!=0x314F4C56u||g_obsState->version!=1)return false;const auto first=*g_obsState;MemoryBarrier();return first.generation==g_obsState->generation&&first.state==2;}
void DisconnectObsState(){if(g_obsState)UnmapViewOfFile(g_obsState);if(g_obsStateMap)CloseHandle(g_obsStateMap);g_obsState=nullptr;g_obsStateMap=nullptr;g_obsNextConnectTick=0;}

// Generic racing presentation bridge. The producer owns simulator-specific telemetry and writes
// generation last; native sees only stable spotter/flag/lap semantics.
constexpr uint32_t kRacingMagic = 0x31524C56; // VLR1
#pragma pack(push,4)
struct RacingStateBlock {
    uint32_t magic,version,size,generation;
    uint32_t spotterState,flagState,flagColor,lapFlags;
    int32_t lapNumber; float lapSeconds,lapDeltaSeconds; uint32_t reserved0;
    int64_t lapExpiresTick; uint32_t presentationFlags,reserved1;
    uint32_t gripState; // v2: packed Grip-O-Bar — bit0 active, bits1-2 dominance, bits3-4 direction, severity<<8
};
#pragma pack(pop)
static_assert(sizeof(RacingStateBlock)==68,"Racing state contract size");
HANDLE g_racingMap=nullptr; const RacingStateBlock* g_racing=nullptr; RacingStateBlock g_racingStable{}; uint32_t g_racingGeneration=0; uint64_t g_racingNextConnectTick=0;
void ConsumeRacingState(){
    if(!g_racing){const uint64_t now=GetTickCount64();if(now<g_racingNextConnectTick)return;g_racingNextConnectTick=now+1000;g_racingMap=OpenFileMappingW(FILE_MAP_READ,FALSE,L"Local\\XRViewLabRacingState");if(g_racingMap)g_racing=(const RacingStateBlock*)MapViewOfFile(g_racingMap,FILE_MAP_READ,0,0,sizeof(RacingStateBlock));}
    if(!g_racing||g_racing->magic!=kRacingMagic||g_racing->version!=2||g_racing->size!=sizeof(RacingStateBlock)||g_racing->generation==g_racingGeneration)return;
    const RacingStateBlock snapshot=*g_racing;MemoryBarrier();if(snapshot.generation!=g_racing->generation)return;g_racingStable=snapshot;g_racingGeneration=snapshot.generation;
}
void DisconnectRacingState(){if(g_racing)UnmapViewOfFile(g_racing);if(g_racingMap)CloseHandle(g_racingMap);g_racing=nullptr;g_racingMap=nullptr;g_racingStable={};g_racingGeneration=0;g_racingNextConnectTick=0;}

#pragma pack(push,4)
struct StickyNoteLiveRecord { uint32_t enabled; float x,y,scale,opacity; uint32_t theme; wchar_t text[120]; };
struct StickyNoteLiveBlock { uint32_t magic,version,size,generation,enabled; StickyNoteLiveRecord notes[kStickyNoteMax]; };
#pragma pack(pop)
static_assert(sizeof(StickyNoteLiveRecord)==264&&sizeof(StickyNoteLiveBlock)==2132,"sticky note live contract size");
HANDLE g_stickyNoteMap=nullptr;const StickyNoteLiveBlock* g_stickyNoteState=nullptr;uint32_t g_stickyNoteGeneration=0;uint64_t g_stickyNoteNextConnectTick=0;
void ConsumeStickyNoteState(){
    if(!g_stickyNoteState){const uint64_t now=GetTickCount64();if(now<g_stickyNoteNextConnectTick)return;g_stickyNoteNextConnectTick=now+1000;g_stickyNoteMap=OpenFileMappingW(FILE_MAP_READ,FALSE,L"Local\\XRViewLabStickyNotes");if(g_stickyNoteMap)g_stickyNoteState=(const StickyNoteLiveBlock*)MapViewOfFile(g_stickyNoteMap,FILE_MAP_READ,0,0,sizeof(StickyNoteLiveBlock));}
    if(!g_stickyNoteState||g_stickyNoteState->magic!=0x314E5356u||g_stickyNoteState->version!=1||g_stickyNoteState->size!=sizeof(StickyNoteLiveBlock)||g_stickyNoteState->generation==g_stickyNoteGeneration)return;
    const StickyNoteLiveBlock snapshot=*g_stickyNoteState;MemoryBarrier();if(snapshot.generation!=g_stickyNoteState->generation)return;
    if((profileOverlayOverrideMask&(1u<<(uint32_t)OverlayFeatureId::StickyNote))!=0){g_stickyNoteGeneration=snapshot.generation;return;}
    if((profileOverlayOverrideMask&(1u<<(uint32_t)OverlayFeatureId::StickyNote))==0)stickyNoteEnabled=snapshot.enabled!=0;stickyNoteCount=0;
    for(size_t i=0;i<kStickyNoteMax;++i){const auto&r=snapshot.notes[i];size_t length=0;while(length<std::size(r.text)&&r.text[length])++length;if(length==0&&!r.enabled)continue;auto&n=stickyNotes[stickyNoteCount++];n.enabled=r.enabled!=0;if((profileStickyOverlayOverrideMask&(1u<<i))==0){n.x=std::clamp((double)r.x,0.0,1.0);n.y=std::clamp((double)r.y,0.0,1.0);n.scale=std::clamp((double)r.scale,.5,2.5);}n.opacity=std::clamp((double)r.opacity,.1,1.0);n.theme=std::clamp(r.theme,0u,4u);n.text.assign(r.text,length);}
    g_stickyNoteGeneration=snapshot.generation;
}
void DisconnectStickyNoteState(){if(g_stickyNoteState)UnmapViewOfFile(g_stickyNoteState);if(g_stickyNoteMap)CloseHandle(g_stickyNoteMap);g_stickyNoteState=nullptr;g_stickyNoteMap=nullptr;g_stickyNoteGeneration=0;g_stickyNoteNextConnectTick=0;}

void ConsumeLiveState() {
    ConsumeTelemetryConfig();
    ConsumeStickyNoteState();
    if (!g_liveState) { ConnectLiveState(); if (!g_liveState) return; }
    const LiveStateBlock snapshot = *g_liveState;
    if (snapshot.magic != kLiveStateMagic || snapshot.version != 12 || snapshot.size != sizeof(LiveStateBlock) || snapshot.generation == g_liveStateGeneration) return;
    MemoryBarrier();
    const LiveStateBlock stable = *g_liveState;
    if (stable.generation != snapshot.generation) return;
    g_liveStateGeneration = stable.generation;
    calibrationGrid = (stable.calibrationMask & (1u << 0)) != 0; calibrationRuler = (stable.calibrationMask & (1u << 1)) != 0;
    calibrationGratings = (stable.calibrationMask & (1u << 2)) != 0; calibrationBars = (stable.calibrationMask & (1u << 3)) != 0;
    calibrationBeacon = (stable.calibrationMask & (1u << 4)) != 0; calibrationEdgeProbes = (stable.calibrationMask & (1u << 5)) != 0;
    calibrationCheckerboards = (stable.calibrationMask & (1u << 6)) != 0; calibrationZonePlate = (stable.calibrationMask & (1u << 7)) != 0;
    calibrationClippingSteps = (stable.calibrationMask & (1u << 8)) != 0; calibrationMotionStrip = (stable.calibrationMask & (1u << 9)) != 0;
    if((profileOverlayOverrideMask&(1u<<(uint32_t)OverlayFeatureId::Hud))==0){hudEnabled = (stable.hudFlags & 1u) != 0; hudClampToVisible = (stable.hudFlags & 2u) != 0; hudAlarmOnly = (stable.hudFlags & 4u) != 0;hudAnchorX = std::clamp((double)stable.hudAnchorX, 0.0, 1.0); hudAnchorY = std::clamp((double)stable.hudAnchorY, 0.0, 1.0); hudScale = std::clamp((double)stable.hudScale, 0.15, 3.0);hudSafeMargin = std::clamp((double)stable.hudSafeMargin, 0.0, 0.25);hudAlarmHoldMs = std::clamp((double)stable.alarmHoldMs, 0.0, 10000.0);}
    if((profileOverlayOverrideMask&(1u<<(uint32_t)OverlayFeatureId::Trace))==0){hudTraceSensitivityMs = std::clamp((double)stable.traceSensitivityMs, 0.25, 8.0);hudTraceX = std::clamp((double)stable.traceX, 0.0, 1.0); hudTraceY = std::clamp((double)stable.traceY, 0.0, 1.0); hudTraceScale = std::clamp((double)stable.traceScale, 0.25, 3.0);hudTraceWidth = std::clamp((double)stable.traceWidth, 0.10, 1.0);hudTraceHistory = std::clamp((double)stable.traceHistory, 10.0, 600.0);hudTraceEnabled = (stable.traceFlags & 1u) != 0;hudTraceVisibilityMode = !hudTraceEnabled ? 0u : (stable.traceFlags & 2u) != 0 ? 2u : 1u;}
    // Backend selection is session-owned and profile-aware. Live UI snapshots must not override a
    // per-game diagnostic force-direct policy halfway through a session.
    if((profileOverlayOverrideMask&(1u<<(uint32_t)OverlayFeatureId::Hud))==0){hudWidgetMask=(hudWidgetMask&~0x0Full)|(stable.hudWidgetMask&0x0Fu);hudWidgetOrderPacked=stable.hudWidgetOrder;}
    if((profileOverlayOverrideMask&(1u<<(uint32_t)OverlayFeatureId::Trace))==0){hudGraphChannels=stable.hudGraphChannels&0x7Fu;hudGraphMode=(HudGraphMode)std::clamp(stable.hudGraphMode,0u,3u);}
    // Feature 1: render-boundary flash drag state. A rising edge to inactive stamps the fade start.
    {
        const bool dragNow = (stable.interactFlags & 1u) != 0;
        if (g_boundaryDragActive && !dragNow) g_boundaryReleaseTick = GetTickCount64();
        else if (dragNow) g_boundaryReleaseTick = 0;
        g_boundaryDragActive = dragNow;
    }
    // Feature 2: crosshair.
    if((profileOverlayOverrideMask&(1u<<(uint32_t)OverlayFeatureId::Crosshair))==0){crosshairEnabled = (stable.crosshairFlags & 1u) != 0; crosshairDot = (stable.crosshairFlags & 2u) != 0;
    crosshairOutline = (stable.crosshairFlags & 4u) != 0; crosshairTStyle = (stable.crosshairFlags & 8u) != 0;
    crosshairSize = std::clamp((double)stable.chSize, 0.0, 1000.0); crosshairGap = std::clamp((double)stable.chGap, -50.0, 50.0);
    crosshairThickness = std::clamp((double)stable.chThickness, 0.1, 50.0); crosshairOutlineThickness = std::clamp((double)stable.chOutlineThickness, 0.0, 10.0);
    crosshairAlpha = std::clamp((double)stable.chAlpha, 0.0, 1.0);crosshairScale = std::clamp((double)stable.chScale, 0.1, 10.0);crosshairOffsetX = std::clamp((double)stable.crosshairOffsetX, -1.0, 1.0); crosshairOffsetY = std::clamp((double)stable.crosshairOffsetY, -1.0, 1.0);
    crosshairR = ((stable.chColor >> 16) & 0xFF) / 255.f; crosshairG = ((stable.chColor >> 8) & 0xFF) / 255.f; crosshairB = (stable.chColor & 0xFF) / 255.f;
    }
    Log("crosshair: live resolve generation=%u enabled=%d size=%.2f gap=%.2f thickness=%.2f outline=%d/%.2f dot=%d tstyle=%d rgba=(%.3f,%.3f,%.3f,%.3f) scale=%.2f\n",
        stable.generation,crosshairEnabled?1:0,crosshairSize,crosshairGap,crosshairThickness,crosshairOutline?1:0,crosshairOutlineThickness,crosshairDot?1:0,crosshairTStyle?1:0,crosshairR,crosshairG,crosshairB,crosshairAlpha,crosshairScale);
    // Feature 3: notification render settings (content arrives via the separate mapping).
    if((profileOverlayOverrideMask&(1u<<(uint32_t)OverlayFeatureId::Notifications))==0){notifyEnabled = (stable.notifyFlags & 1u) != 0;notifyX = std::clamp((double)stable.notifyX, 0.0, 1.0); notifyY = std::clamp((double)stable.notifyY, 0.0, 1.0);notifyScale = std::clamp((double)stable.notifyScale, 0.1, 3.0);notifyOpacity = std::clamp((double)stable.notifyOpacity, 0.1, 1.0);}
    // Generic racing presentation enables; event state arrives through its dedicated mapping.
    // v12: the cue tuning values also arrive live so slider drags apply without a session restart.
    if(!profileIRacingFeatureOverride){
        iracingEnabled = (stable.iracingFlags & 1u) != 0; iracingLapPopup = (stable.iracingFlags & 2u) != 0;iracingSpotterGlow = (stable.iracingFlags & 4u) != 0; iracingFlagBorder = (stable.iracingFlags & 8u) != 0;
        iracingRaceStart = (stable.iracingFlags & 16u) != 0; iracingRearClosing = (stable.iracingFlags & 32u) != 0; iracingGripBar = (stable.iracingFlags & 64u) != 0;
        iracingSpotterWidth = std::clamp((double)stable.irSpotterWidth, 0.03, 0.35); iracingSpotterStrength = std::clamp((double)stable.irSpotterStrength, 0.1, 2.0);
        iracingSpotterOpacity = std::clamp((double)stable.irSpotterOpacity, 0.05, 1.0); iracingSpotterFade = std::clamp((double)stable.irSpotterFade, 0.25, 4.0);
        iracingSpotterColor = stable.irSpotterColor & 0xFFFFFFu;
        iracingFlagWidth = std::clamp((double)stable.irFlagWidth, 0.003, 0.12); iracingFlagOpacity = std::clamp((double)stable.irFlagOpacity, 0.05, 1.0);
        iracingRaceStartRedOpacity = std::clamp((double)stable.irRaceStartRedOpacity, 0.05, 1.0); iracingRaceStartGreenOpacity = std::clamp((double)stable.irRaceStartGreenOpacity, 0.05, 1.0);
        iracingRaceStartGreenMs = std::clamp((double)stable.irRaceStartGreenMs, 250.0, 15000.0); iracingRaceStartThickness = std::clamp((double)stable.irRaceStartThickness, 0.005, 0.12);
        iracingRearClosingOpacity = std::clamp((double)stable.irRearClosingOpacity, 0.05, 1.0); iracingGripBarOpacity = std::clamp((double)stable.irGripBarOpacity, 0.05, 1.0);
    }
    if((profileOverlayOverrideMask&(1u<<(uint32_t)OverlayFeatureId::Clock))==0){clockWidgetEnabled=(stable.clockFlags&1u)!=0;clockSessionTimerEnabled=(stable.clockFlags&2u)!=0;clock24Hour=(stable.clockFlags&4u)!=0;clockWidgetX=std::clamp((double)stable.clockX,0.0,1.0);clockWidgetY=std::clamp((double)stable.clockY,0.0,1.0);clockWidgetScale=std::clamp((double)stable.clockScale,.1,2.0);clockWidgetOpacity=std::clamp((double)stable.clockOpacity,.1,1.0);clockWidgetTheme=std::clamp(stable.clockTheme,0u,3u);clockWidgetPalette=std::clamp(stable.clockPalette,0u,4u);}
    obsMirrorVisibilityMask = stable.obsMirrorVisibilityMask & kAllMirrorFeatures;
    for(size_t i=0;i<(size_t)OverlayFeatureId::Count;++i)if((profileOverlayOverrideMask&(1u<<(uint32_t)i))==0)g_overlayFeatureVisibility[i].toggleKey=(int)std::clamp(stable.overlayToggleKeys[i],0u,255u);
    if ((stable.flags & 1u) != 0 && !liveVisorUsesProfileOverride) {
        maskEnabled = (stable.flags & 4u) != 0;
        visorSize = std::clamp((double)stable.visorSize, 0.1, 1.0); visorCurve = std::clamp(1.0 - (double)stable.maskCorner, 0.0, 1.0);
        visorOuterApexY = std::clamp((double)stable.apexY, -0.5, 0.5); visorInnerLowerY = std::clamp((double)stable.innerLow, 0.0, 0.666);
        visorInnerBridgeWidth = std::clamp((double)stable.bridgeWidth, 0.0, 1.0); visorInnerBridgeRise = std::clamp((double)stable.bridgeRise, -0.5, 1.0);
        visorInnerBridgePeakX = std::clamp((double)stable.bridgePeakX, -1.0, 2.0); visorInnerBridgeSteepness = std::clamp((double)stable.bridgeSteepness, -1.0, 2.0);
        visorNoseSpreadX = std::clamp((double)stable.visorNoseSpreadX, 0.0, 0.5);
        // Item 21: visor fill colour, 0x00RRGGBB. Default 0 = black, so existing behaviour is unchanged.
        g_visorColor[0] = ((stable.visorColor >> 16) & 0xFF) / 255.f;
        g_visorColor[1] = ((stable.visorColor >> 8) & 0xFF) / 255.f;
        g_visorColor[2] = (stable.visorColor & 0xFF) / 255.f;
    }
}
bool uevrLikeProcess = false;
double totalTangent = DefaultTotalTangent;
double topTangent = DefaultTopTangent;
double bottomTangent = DefaultBottomTangent;
double horizontalRenderWidth = DefaultHorizontalRenderWidth;
std::wstring currentAppKey;

#pragma pack(push,4)
struct ActiveProfileBlock { uint32_t magic,version,generation,reserved; wchar_t appKey[128]; };
#pragma pack(pop)
HANDLE g_activeProfileMap=nullptr;ActiveProfileBlock* g_activeProfileBlock=nullptr;
void PublishActiveProfile(){
    if(!g_activeProfileMap)g_activeProfileMap=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,0,sizeof(ActiveProfileBlock),L"Local\\XRViewLabActiveProfileV1");
    if(g_activeProfileMap&&!g_activeProfileBlock)g_activeProfileBlock=(ActiveProfileBlock*)MapViewOfFile(g_activeProfileMap,FILE_MAP_ALL_ACCESS,0,0,sizeof(ActiveProfileBlock));
    if(!g_activeProfileBlock)return;ActiveProfileBlock next{};next.magic=0x31504156u;next.version=1;next.generation=g_activeProfileBlock->generation+1;wcsncpy_s(next.appKey,currentAppKey.c_str(),_TRUNCATE);*g_activeProfileBlock=next;MemoryBarrier();
}

PFN_xrGetInstanceProcAddr nextXrGetInstanceProcAddr = nullptr;
PFN_xrCreateSession nextXrCreateSession = nullptr;
PFN_xrDestroySession nextXrDestroySession = nullptr;
PFN_xrEndSession nextXrEndSession = nullptr;
PFN_xrLocateViews nextXrLocateViews = nullptr;
PFN_xrEnumerateViewConfigurationViews nextXrEnumerateViewConfigurationViews = nullptr;
PFN_xrGetVisibilityMaskKHR nextXrGetVisibilityMaskKHR = nullptr;
PFN_xrWaitFrame nextXrWaitFrame = nullptr;
PFN_xrBeginFrame nextXrBeginFrame = nullptr;
PFN_xrEndFrame nextXrEndFrame = nullptr;
PFN_xrEnumerateSwapchainFormats nextXrEnumerateSwapchainFormats = nullptr;
PFN_xrCreateSwapchain nextXrCreateSwapchain = nullptr;
PFN_xrDestroySwapchain nextXrDestroySwapchain = nullptr;
PFN_xrCreateReferenceSpace nextXrCreateReferenceSpace = nullptr;
PFN_xrDestroySpace nextXrDestroySpace = nullptr;
PFN_xrEnumerateSwapchainImages nextXrEnumerateSwapchainImages = nullptr;
PFN_xrAcquireSwapchainImage nextXrAcquireSwapchainImage = nullptr;
PFN_xrWaitSwapchainImage nextXrWaitSwapchainImage = nullptr;
PFN_xrReleaseSwapchainImage nextXrReleaseSwapchainImage = nullptr;

std::atomic<uint32_t> locateViewsLogCount{0};
std::atomic<uint32_t> enumerateViewsLogCount{0};
std::atomic<uint32_t> enumerateSkipLogCount{0};
std::atomic<uint32_t> g_runtimeRecommendedViewWidth{0};
std::atomic<uint32_t> g_runtimeRecommendedViewHeight{0};
std::atomic<uint32_t> visibilityMaskLogCount{0};
std::atomic<uint32_t> visorMaskNoHookLogCount{0};
std::atomic<uint32_t> visorRendererLogCount{0};
std::atomic<bool> g_visibilityMaskEverCalled{false};

// One-shot diagnostic flags for the visor renderer. Each stage logs the
// first time it is hit (success or failure) so a single in-headset run reveals the
// first failing stage without spamming the log.
std::atomic<bool> g_diagEndFrameCalled{false};
std::atomic<bool> g_diagEndFrameGated{false};
std::atomic<bool> g_diagProjFound{false};
std::atomic<bool> g_diagNoProjLayer{false};
std::atomic<bool> g_diagDrawScNotTracked{false};
std::atomic<bool> g_diagDrawBadIndex{false};
std::atomic<bool> g_diagDrawNullTex{false};
std::atomic<bool> g_diagDrawNoVerts{false};
std::atomic<bool> g_diagDrawMapFailed{false};
std::atomic<bool> g_diagDrawRtvFailed{false};
std::atomic<bool> g_diagDrawOk{false};

std::atomic<bool> g_diagLodFullFov{false};
std::atomic<bool> g_diagVisorSkipsVisibilityEdgeFilter{false};
std::atomic<bool> g_diagEndFrameLateDraw{false};

// Per-game swapchain tracking for the D3D11 visor renderer.
struct EyeView {
    XrRect2Di rect{};       // sub-image rect within the texture for this eye
    uint32_t arraySlice = 0; // texture-array slice for this eye
    uint32_t viewIndex = 0;  // projection view index: 0 = left, 1 = right for primary stereo
    XrPosef pose{};          // submitted projection pose; diagnostics expose assumptions about stereo alignment
    XrFovf fov{};
    XrFovf fullFov{};
};

struct ProjectionFrameContext {
    XrSession session = XR_NULL_HANDLE;
    XrTime displayTime = 0;
    XrSpace space = XR_NULL_HANDLE;
    uint64_t frameSerial = 0;
    viewlab::projection::FrameTopology<XrSwapchain, EyeView> topology;

    bool ValidFor(XrSession expectedSession) const {
        return session == expectedSession && !topology.views.empty();
    }
};

// Bounded verbose-only renderer evidence. A direct eye-texture overlay is rendered before
// xrEndFrame, so it necessarily consumes the previous submitted layout. Keep enough correlated
// state to prove whether that layout, the latest locate result, and the current submission are
// actually equivalent instead of assuming that every application drives OpenXR identically.
struct LocateViewsEvidence {
    uint64_t callSerial = 0;
    XrSession session = XR_NULL_HANDLE;
    XrTime displayTime = 0;
    XrSpace space = XR_NULL_HANDLE;
    XrViewStateFlags viewStateFlags = 0;
    uint32_t viewCount = 0;
    std::array<XrView, 2> original{};
    std::array<XrView, 2> cropped{};
};
std::mutex g_pipelineEvidenceMutex;
std::deque<LocateViewsEvidence> g_locateViewsEvidence;
std::unordered_map<XrSpace, XrReferenceSpaceType> g_referenceSpaceTypes;
std::atomic<uint64_t> g_pipelineFrameSerial{0};
std::atomic<uint64_t> g_pipelineLocateSerial{0};
std::atomic<uint64_t> g_pipelineSwapchainSerial{0};
constexpr size_t kLocateEvidenceCapacity = 64;
bool TracePipelineSerial(uint64_t serial) { return verboseLogging && (serial <= 900 || serial % 300 == 0); }
const char* ReferenceSpaceTypeName(XrReferenceSpaceType type) {
    switch (type) {
    case XR_REFERENCE_SPACE_TYPE_VIEW: return "VIEW";
    case XR_REFERENCE_SPACE_TYPE_LOCAL: return "LOCAL";
    case XR_REFERENCE_SPACE_TYPE_STAGE: return "STAGE";
#ifdef XR_REFERENCE_SPACE_TYPE_LOCAL_FLOOR_EXT
    case XR_REFERENCE_SPACE_TYPE_LOCAL_FLOOR_EXT: return "LOCAL_FLOOR_EXT";
#endif
    default: return "OTHER";
    }
}
XrReferenceSpaceType TrackedReferenceSpaceType(XrSpace space) {
    std::lock_guard<std::mutex> lock(g_pipelineEvidenceMutex);
    const auto found = g_referenceSpaceTypes.find(space);
    return found == g_referenceSpaceTypes.end() ? static_cast<XrReferenceSpaceType>(0) : found->second;
}
void LogPipelinePose(const char* stage, uint64_t serial, uint32_t viewIndex, const XrPosef& pose, const XrFovf& fov) {
    LogVerbose("PIPE %s serial=%llu view=%u pose_p=(%.6f,%.6f,%.6f) pose_q=(%.7f,%.7f,%.7f,%.7f) fov=(L %.7f R %.7f U %.7f D %.7f)\n",
        stage, static_cast<unsigned long long>(serial), viewIndex,
        pose.position.x, pose.position.y, pose.position.z,
        pose.orientation.x, pose.orientation.y, pose.orientation.z, pose.orientation.w,
        fov.angleLeft, fov.angleRight, fov.angleUp, fov.angleDown);
}

enum class OverlayPlacement { RenderArea, FullLens, LensPinned };
struct OverlayCoordinateResolver {
    float left=0, top=0, width=1, height=1;
    float selectedL=-1, selectedR=1, selectedU=1, selectedD=-1;
    float fullL=-1, fullR=1, fullU=1, fullD=-1;
    bool tangentValid=false;
    static bool Valid(const XrFovf& f) { return f.angleRight>f.angleLeft && f.angleUp>f.angleDown &&
        fabsf(f.angleLeft)<1.55f && fabsf(f.angleRight)<1.55f && fabsf(f.angleUp)<1.55f && fabsf(f.angleDown)<1.55f; }
    float sharedSelectedL=-1, sharedSelectedR=1, sharedSelectedU=1, sharedSelectedD=-1;
    float sharedFullL=-1, sharedFullR=1, sharedFullU=1, sharedFullD=-1;
    explicit OverlayCoordinateResolver(const EyeView& eye, const std::vector<EyeView>& allViews) {
        left=(float)eye.rect.offset.x; top=(float)eye.rect.offset.y;
        width=(float)eye.rect.extent.width; height=(float)eye.rect.extent.height;
        tangentValid=Valid(eye.fov);
        if (tangentValid) { selectedL=tanf(eye.fov.angleLeft); selectedR=tanf(eye.fov.angleRight);
            selectedU=tanf(eye.fov.angleUp); selectedD=tanf(eye.fov.angleDown); }
        if (Valid(eye.fullFov)) { fullL=tanf(eye.fullFov.angleLeft); fullR=tanf(eye.fullFov.angleRight);
            fullU=tanf(eye.fullFov.angleUp); fullD=tanf(eye.fullFov.angleDown); }
        else { fullL=selectedL; fullR=selectedR; fullU=selectedU; fullD=selectedD; }
        sharedSelectedL=selectedL; sharedSelectedR=selectedR; sharedSelectedU=selectedU; sharedSelectedD=selectedD;
        sharedFullL=fullL; sharedFullR=fullR; sharedFullU=fullU; sharedFullD=fullD;
        for (const EyeView& other:allViews) {
            if (other.viewIndex==eye.viewIndex) continue;
            if (Valid(other.fov)) { sharedSelectedL=(std::max)(sharedSelectedL,tanf(other.fov.angleLeft)); sharedSelectedR=(std::min)(sharedSelectedR,tanf(other.fov.angleRight)); sharedSelectedU=(std::min)(sharedSelectedU,tanf(other.fov.angleUp)); sharedSelectedD=(std::max)(sharedSelectedD,tanf(other.fov.angleDown)); }
            if (Valid(other.fullFov)) { sharedFullL=(std::max)(sharedFullL,tanf(other.fullFov.angleLeft)); sharedFullR=(std::min)(sharedFullR,tanf(other.fullFov.angleRight)); sharedFullU=(std::min)(sharedFullU,tanf(other.fullFov.angleUp)); sharedFullD=(std::max)(sharedFullD,tanf(other.fullFov.angleDown)); }
        }
        if (!(sharedSelectedR>sharedSelectedL && sharedSelectedU>sharedSelectedD)) { sharedSelectedL=selectedL; sharedSelectedR=selectedR; sharedSelectedU=selectedU; sharedSelectedD=selectedD; }
        if (!(sharedFullR>sharedFullL && sharedFullU>sharedFullD)) { sharedFullL=fullL; sharedFullR=fullR; sharedFullU=fullU; sharedFullD=fullD; }
    }
    float XFromTan(float x) const { return left+(x-selectedL)/(selectedR-selectedL)*width; }
    float YFromTan(float y) const { return top+(selectedU-y)/(selectedU-selectedD)*height; }
    std::pair<float,float> Resolve(OverlayPlacement mode, float nx, float ny, float offsetX=0, float offsetY=0) const {
        if (!tangentValid) return {left+std::clamp(nx,0.f,1.f)*width, top+std::clamp(ny,0.f,1.f)*height};
        const bool render=mode==OverlayPlacement::RenderArea;
        const float baseL=render?sharedSelectedL:sharedFullL, baseR=render?sharedSelectedR:sharedFullR;
        const float baseU=render?sharedSelectedU:sharedFullU, baseD=render?sharedSelectedD:sharedFullD;
        float tx=baseL+nx*(baseR-baseL)+offsetX*(sharedFullR-sharedFullL)*.5f;
        float ty=baseU-ny*(baseU-baseD)-offsetY*(sharedFullU-sharedFullD)*.5f;
        if (mode==OverlayPlacement::LensPinned) { tx=std::clamp(tx,sharedSelectedL,sharedSelectedR); ty=std::clamp(ty,sharedSelectedD,sharedSelectedU); }
        return {XFromTan(tx),YFromTan(ty)};
    }
    std::pair<float,float> ResolveSharedTangent(float tx,float ty,bool pin) const {
        if(!tangentValid) return {left+width*.5f,top+height*.5f};
        if(pin){tx=std::clamp(tx,sharedSelectedL,sharedSelectedR);ty=std::clamp(ty,sharedSelectedD,sharedSelectedU);}
        return {XFromTan(tx),YFromTan(ty)};
    }
};
struct TrackedSwapchain {
    XrSession session = XR_NULL_HANDLE;
    uint32_t width = 0;
    uint32_t height = 0;
    uint32_t arraySize = 1;
    uint32_t sampleCount = 1;
    int64_t format = 0;
    XrSwapchainUsageFlags usageFlags = 0;
    std::vector<ID3D11Texture2D*> textures;  // runtime textures; not AddRef'd
    std::vector<ID3D11RenderTargetView*> rtvs; // runtime texture RTVs, indexed image * arraySize + slice
    uint32_t lastAcquiredIndex = UINT32_MAX;
    uint64_t createSerial = 0;
    uint64_t acquireSerial = 0;
    uint64_t waitSerial = 0;
    uint64_t releaseSerial = 0;
};
struct TopmostLayerState {
    XrSession session = XR_NULL_HANDLE;
    XrSwapchain swapchain = XR_NULL_HANDLE;
    uint32_t width = 0, height = 0;
    int64_t format = DXGI_FORMAT_R8G8B8A8_UNORM;
    std::vector<XrSwapchainImageD3D11KHR> images;
    bool ready = false;
};
TopmostLayerState g_topmostLayer;
viewlab::bridge::OverlayBackend g_topmostBackend = viewlab::bridge::OverlayBackend::DirectEyeTexture;
viewlab::bridge::FeaturePresentationPlan g_featurePresentationPlan{true,true,viewlab::bridge::OverlayBackend::FeatureDisabled};
std::atomic<bool> g_topmostLayerBlocked{false};
bool g_topmostLayerAttempted = false;
bool g_topmostLayerDemanded = false;
std::atomic<bool> g_rendererDeviceLost{false};
std::atomic<bool> g_rendererDeviceLossLogged{false};
std::atomic<bool> g_topmostSubmissionLogged{false};

std::mutex g_swapchainMutex;
// Previous submitted primary projection. Release-time drawing happens before the
// current xrEndFrame, so it consumes this complete frame-level context. It is
// deliberately independent of g_swapchains: texture ownership never defines which
// other views participate in shared projection coordinates.
ProjectionFrameContext g_primaryProjectionContext;
// Serializes renderer lifetime and immediate-context use across OpenXR hooks. The D3D11
// immediate context and its state objects must not be released while another hook draws.
std::recursive_mutex g_rendererMutex;
std::unordered_map<XrSwapchain, TrackedSwapchain> g_swapchains;

// D3D11 shader-based mask renderer — draws the visor border directly into the
// game's own eye textures when the visibility-mask path is unavailable.
struct D3D11MaskState {
    XrSession session = XR_NULL_HANDLE;
    ID3D11Device* device = nullptr;
    ID3D11DeviceContext* context = nullptr;
    ID3D11VertexShader* vs = nullptr;
    ID3D11PixelShader* ps = nullptr;
    ID3D11PixelShader* calibrationPs = nullptr;
    ID3D11Buffer* calibrationColorCb = nullptr;
    ID3D11Buffer* visorColorCb = nullptr;
    ID3D11PixelShader* overlayPs = nullptr;
    // Textured path — used only for pre-composited notification cards. Samples a per-slot RGBA
    // texture and multiplies by a straight-alpha tint (for the animated fade).
    ID3D11PixelShader* texturedPs = nullptr;
    ID3D11Buffer* tintCb = nullptr;
    ID3D11SamplerState* linearSampler = nullptr;
    ID3D11Texture2D* notifyTex[kNotifyMaxCards] = {};
    ID3D11ShaderResourceView* notifySrv[kNotifyMaxCards] = {};
    uint32_t notifyTexSerial[kNotifyMaxCards] = {}; // last uploaded contentSerial per slot
    uint32_t notifyTexId[kNotifyMaxCards] = {};      // last card id occupying each slot
    ID3D11InputLayout* layout = nullptr;
    ID3D11Buffer* vb = nullptr;
    ID3D11RasterizerState* rs = nullptr;
    ID3D11RasterizerState* calibrationRs = nullptr;
    ID3D11BlendState* bs = nullptr;        // SRC_ALPHA blend — used only when AA is on
    ID3D11BlendState* bsOpaque = nullptr;  // blend disabled — the historically proven visor pipeline
    ID3D11DepthStencilState* dss = nullptr;
    bool initialized = false;
    bool failed = false;
    // Latest eye views from xrLocateViews (for future per-eye FOV use).
    XrSpace locateSpace = XR_NULL_HANDLE;
    XrTime locateTime = 0;
    std::array<XrView, 2> latestViews{};
    std::array<XrView, 2> latestOriginalViews{};
    uint32_t latestViewCount = 0;
};

D3D11MaskState g_d3d11Mask;

bool RendererDeviceHealthy(const char* stage) {
    if (g_rendererDeviceLost.load(std::memory_order_acquire)) return false;
    if (!g_d3d11Mask.device) return false;
    const HRESULT reason = g_d3d11Mask.device->GetDeviceRemovedReason();
    if (SUCCEEDED(reason)) return true;
    g_rendererDeviceLost.store(true, std::memory_order_release);
    g_topmostLayerBlocked.store(true, std::memory_order_release);
    if (!g_rendererDeviceLossLogged.exchange(true)) {
        Log("d3d11 safety: device removed stage=%s reason=0x%08X pid=%lu thread=%lu; "
            "all ViewLab rendering disabled for this session\n",
            stage ? stage : "unknown", static_cast<unsigned>(reason),
            static_cast<unsigned long>(GetCurrentProcessId()),
            static_cast<unsigned long>(GetCurrentThreadId()));
    }
    return false;
}

bool GetHudRenderAdapterLuid(uint64_t& adapterLuid) {
    adapterLuid = 0;
    if (!g_d3d11Mask.device) return false;
    IDXGIDevice* dxgiDevice = nullptr;
    IDXGIAdapter* adapter = nullptr;
    if (FAILED(g_d3d11Mask.device->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice))) || !dxgiDevice) return false;
    const HRESULT adapterResult = dxgiDevice->GetAdapter(&adapter);
    dxgiDevice->Release();
    if (FAILED(adapterResult) || !adapter) return false;
    DXGI_ADAPTER_DESC desc{};
    const HRESULT descResult = adapter->GetDesc(&desc);
    adapter->Release();
    if (FAILED(descResult)) return false;
    adapterLuid = (static_cast<uint64_t>(static_cast<uint32_t>(desc.AdapterLuid.HighPart)) << 32) | static_cast<uint32_t>(desc.AdapterLuid.LowPart);
    return true;
}

void ReleaseTrackedSwapchainResources(TrackedSwapchain& ts) {
    for (ID3D11RenderTargetView* rtv : ts.rtvs) {
        if (rtv) rtv->Release();
    }
    ts.rtvs.clear();
}

std::filesystem::path LocalAppDataPath() {
    wchar_t localAppData[MAX_PATH]{};
    const DWORD length = GetEnvironmentVariableW(L"LOCALAPPDATA", localAppData, static_cast<DWORD>(std::size(localAppData)));
    if (length == 0 || length >= std::size(localAppData)) {
        return {};
    }

    return std::filesystem::path(localAppData);
}

std::filesystem::path UserDataDirectory() {
    const std::filesystem::path localAppData = LocalAppDataPath();
    if (localAppData.empty()) {
        return layerDirectory;
    }

    return localAppData / L"XR ViewLab";
}

std::filesystem::path LatestPerformanceTracePath() {
    return UserDataDirectory() / L"PerformanceTraces" / L"latest.csv";
}

void BeginPerformanceTraceSession(int64_t startQpc) {
    std::lock_guard<std::mutex> fileLock(g_traceFileMutex);
    std::lock_guard<std::mutex> lock(g_hudTimingMutex);
    g_sessionTraceSamples.clear();
    if (g_sessionTraceSamples.capacity() < kSessionTraceCapacity) g_sessionTraceSamples.reserve(kSessionTraceCapacity);
    g_sessionTraceStart=0; g_sessionTraceMarkers.clear(); g_sessionTraceMarkers.reserve(256);
    FILETIME utc{}; GetSystemTimeAsFileTime(&utc); ULARGE_INTEGER utcValue{}; utcValue.LowPart=utc.dwLowDateTime; utcValue.HighPart=utc.dwHighDateTime;
    g_sessionTraceStartQpc=startQpc; g_sessionTraceStartUtcFileTime=static_cast<int64_t>(utcValue.QuadPart);
    SYSTEMTIME utcSystem{}; GetSystemTime(&utcSystem); wchar_t fileName[96]{};
    swprintf_s(fileName,L"session-%04u%02u%02u-%02u%02u%02u-%03u-%lu.csv",utcSystem.wYear,utcSystem.wMonth,utcSystem.wDay,utcSystem.wHour,utcSystem.wMinute,utcSystem.wSecond,utcSystem.wMilliseconds,static_cast<unsigned long>(GetCurrentProcessId()));
    g_sessionTraceFilePath=LatestPerformanceTracePath().parent_path()/fileName;
    g_latestTraceIsHardLink=false;
    g_nextTraceMarker=1; g_pendingTraceMarker=0; g_traceMarkerKeyDown=false;
    g_sessionTraceTotalSamples=0;g_traceCheckpointSampleCursor=0;g_traceCheckpointMarkerCursor=0;g_traceCheckpointLastRebuild=0;
    g_traceCheckpointNeedsRebuild=true;
    g_traceMarkerConfirmation.store(0,std::memory_order_release);
    g_traceMarkerConfirmationUntil.store(0,std::memory_order_release);
}

void CapturePerformanceTraceMarker(int64_t qpc) {
    if (!performanceTraceRecording || g_sessionTraceStartQpc<=0 || g_sessionTraceMarkers.size()>=999) return;
    const uint32_t number=g_nextTraceMarker++;
    g_sessionTraceMarkers.push_back({number,qpc});
    g_pendingTraceMarker=number;
    g_traceMarkerConfirmation.store(number,std::memory_order_release);
    g_traceMarkerConfirmationUntil.store(GetTickCount64()+1500,std::memory_order_release);
}

void SavePerformanceTraceSession() {
    struct SequencedSample { uint64_t sequence; HudFrameSample sample; };
    std::lock_guard<std::mutex> fileLock(g_traceFileMutex);
    const auto path=g_sessionTraceFilePath.empty()?LatestPerformanceTracePath():g_sessionTraceFilePath;
    std::error_code ec;
    const bool pathExists=std::filesystem::exists(path,ec);
    std::vector<SequencedSample> samples;
    std::vector<PerformanceTraceMarker> markers;
    uint64_t targetSampleCursor=0,oldestSequence=0;
    size_t targetMarkerCursor=0;
    int64_t startQpc=0,startUtcFileTime=0;
    bool rebuild=g_traceCheckpointNeedsRebuild||!pathExists;
    {
        std::lock_guard<std::mutex> lock(g_hudTimingMutex);
        if(!performanceTraceRecording||g_sessionTraceSamples.empty()||g_sessionTraceStartQpc<=0)return;
        const size_t stored=g_sessionTraceSamples.size();
        targetSampleCursor=g_sessionTraceTotalSamples;
        targetMarkerCursor=g_sessionTraceMarkers.size();
        oldestSequence=targetSampleCursor-stored;
        rebuild=rebuild||g_traceCheckpointSampleCursor<oldestSequence||
            (targetSampleCursor>=kSessionTraceCapacity&&targetSampleCursor-g_traceCheckpointLastRebuild>=3600);
        const uint64_t first=rebuild?oldestSequence:g_traceCheckpointSampleCursor;
        if(first==targetSampleCursor&&g_traceCheckpointMarkerCursor==targetMarkerCursor)return;
        samples.reserve(static_cast<size_t>(targetSampleCursor-first));
        for(uint64_t sequence=first;sequence<targetSampleCursor;++sequence) {
            const size_t offset=static_cast<size_t>(sequence-oldestSequence);
            samples.push_back({sequence+1,g_sessionTraceSamples[(g_sessionTraceStart+offset)%stored]});
        }
        const size_t markerFirst=rebuild?0:g_traceCheckpointMarkerCursor;
        const int64_t earliestQpc=samples.empty()?g_sessionTraceStartQpc:samples.front().sample.qpc;
        for(size_t i=markerFirst;i<targetMarkerCursor;++i)if(!rebuild||g_sessionTraceMarkers[i].qpc>=earliestQpc)markers.push_back(g_sessionTraceMarkers[i]);
        startQpc=g_sessionTraceStartQpc;
        startUtcFileTime=g_sessionTraceStartUtcFileTime;
    }
    std::filesystem::create_directories(path.parent_path(),ec);if(ec)return;
    const auto output=rebuild?path.wstring()+L".tmp":path.wstring();FILE* file=nullptr;
    if(_wfopen_s(&file,output.c_str(),rebuild?L"wb":L"ab")!=0||!file)return;
    LARGE_INTEGER frequency{};QueryPerformanceFrequency(&frequency);
    if(rebuild) {
        fprintf(file,"ViewLabPerformanceTrace,2\nfrequency,%lld\nstart_qpc,%lld\nstart_utc_filetime,%lld\n",
            (long long)frequency.QuadPart,(long long)startQpc,(long long)startUtcFileTime);
        fprintf(file,"type,sequence,qpc,elapsed_ms,actual_ms,target_ms,deviation_ms,app_ms,wait_ms,submit_ms,display_ms,marker,gpu_pct,warning_mask,critical_mask,visible_alarm_mask\n");
    }
    for(const auto&entry:samples){const auto&s=entry.sample;const double elapsed=frequency.QuadPart>0?1000.0*(double)(s.qpc-startQpc)/(double)frequency.QuadPart:0.0;
        fprintf(file,"S,%llu,%lld,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%.6f,%u,%.3f,%llu,%llu,%llu\n",(unsigned long long)entry.sequence,(long long)s.qpc,elapsed,s.actualMs,s.targetMs,s.deviationMs,s.appWorkMs,s.waitDurationMs,s.submitDurationMs,s.displayPeriodMs,s.markerNumber,s.gpuPercent,(unsigned long long)s.warningMask,(unsigned long long)s.criticalMask,(unsigned long long)s.visibleAlarmMask);}
    for(const auto&m:markers){const double elapsed=frequency.QuadPart>0?1000.0*(double)(m.qpc-startQpc)/(double)frequency.QuadPart:0.0;fprintf(file,"M,%u,%lld,%.6f,,,,,,,,%u\n",m.number,(long long)m.qpc,elapsed,m.number);}
    fflush(file);_commit(_fileno(file));fclose(file);
    if(rebuild&&!MoveFileExW(output.c_str(),path.c_str(),MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH)) {
        const DWORD error=GetLastError();std::filesystem::remove(output,ec);
        Log("Performance trace: checkpoint replace failed error=%lu path=%ls\n",static_cast<unsigned long>(error),path.c_str());return;
    }
    // latest.csv is a compatibility alias to the current archived session. Recreate the hard
    // link after an atomic rebuild because replacing the archive creates a new file identity.
    const auto latest=LatestPerformanceTracePath();
    if(path!=latest) {
        if(rebuild||!std::filesystem::exists(latest,ec)) {
            std::filesystem::remove(latest,ec);
            g_latestTraceIsHardLink=CreateHardLinkW(latest.c_str(),path.c_str(),nullptr)!=FALSE;
        }
        if(!g_latestTraceIsHardLink) CopyFileW(path.c_str(),latest.c_str(),FALSE);
    }
    g_traceCheckpointSampleCursor=targetSampleCursor;
    g_traceCheckpointMarkerCursor=targetMarkerCursor;
    if(rebuild){g_traceCheckpointLastRebuild=targetSampleCursor;g_traceCheckpointNeedsRebuild=false;}
    LogVerbose("Performance trace: checkpointed %zu samples and %zu markers to %ls\n",samples.size(),markers.size(),path.c_str());
}

std::filesystem::path LogPath() {
    return UserDataDirectory() / L"Logs" / L"ViewLab.log";
}

std::filesystem::path VerboseLogPath() {
    return UserDataDirectory() / L"Logs" / L"ViewLab.verbose.log";
}

void RotateLogIfNeeded(const std::filesystem::path& logPath) {
    std::error_code ec;
    if (!std::filesystem::exists(logPath, ec) || std::filesystem::file_size(logPath, ec) < 2 * 1024 * 1024) {
        return;
    }

    const std::filesystem::path oldPath = logPath.parent_path() / L"ViewLab.old.log";
    std::filesystem::remove(oldPath, ec);
    std::filesystem::rename(logPath, oldPath, ec);
}

void OpenLogIfNeeded() {
    if (logStream.is_open()) {
        return;
    }

    const std::filesystem::path logPath = LogPath();
    std::error_code ec;
    std::filesystem::create_directories(logPath.parent_path(), ec);
    RotateLogIfNeeded(logPath);
    logStream.open(logPath, std::ios_base::app);
}

void Log(const char* fmt, ...) {
    char buffer[1024]{};

    va_list args;
    va_start(args, fmt);
    _vsnprintf_s(buffer, sizeof(buffer), _TRUNCATE, fmt, args);
    va_end(args);

    SYSTEMTIME now{};
    GetLocalTime(&now);

    char line[1400]{};
    _snprintf_s(
        line,
        sizeof(line),
        _TRUNCATE,
        "%02u:%02u:%02u:%03u [%lu] | INFO  | %s",
        now.wHour,
        now.wMinute,
        now.wSecond,
        now.wMilliseconds,
        static_cast<unsigned long>(GetCurrentProcessId()),
        buffer);

    OutputDebugStringA(buffer);
    std::lock_guard<std::mutex> lock(logMutex);
    OpenLogIfNeeded();
    if (logStream.is_open()) {
        logStream << line;
        if (line[std::strlen(line) - 1] != '\n') {
            logStream << '\n';
        }
        logStream.flush();
    }
}

void OpenVerboseLogIfNeeded() {
    if (verboseLogStream.is_open()) {
        return;
    }

    const std::filesystem::path logPath = VerboseLogPath();
    std::error_code ec;
    std::filesystem::create_directories(logPath.parent_path(), ec);
    RotateLogIfNeeded(logPath);
    verboseLogStream.open(logPath, std::ios_base::app);
}

// Per-frame / high-frequency hook detail. No-op unless verbose_logging=1 in the
// INI. Writes to ViewLab.verbose.log only — never the human-facing ViewLab.log.
void LogVerbose(const char* fmt, ...) {
    if (!verboseLogging) {
        return;
    }

    char buffer[1024]{};

    va_list args;
    va_start(args, fmt);
    _vsnprintf_s(buffer, sizeof(buffer), _TRUNCATE, fmt, args);
    va_end(args);

    SYSTEMTIME now{};
    GetLocalTime(&now);

    char line[1400]{};
    _snprintf_s(
        line,
        sizeof(line),
        _TRUNCATE,
        "%02u:%02u:%02u:%03u [%lu] | VERB  | %s",
        now.wHour,
        now.wMinute,
        now.wSecond,
        now.wMilliseconds,
        static_cast<unsigned long>(GetCurrentProcessId()),
        buffer);

    std::lock_guard<std::mutex> lock(logMutex);
    OpenVerboseLogIfNeeded();
    if (verboseLogStream.is_open()) {
        verboseLogStream << line;
        if (line[std::strlen(line) - 1] != '\n') {
            verboseLogStream << '\n';
        }
        verboseLogStream.flush();
    }
}

std::filesystem::path GetLayerDirectory() {
    HMODULE module = nullptr;
    if (!GetModuleHandleExA(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCSTR>(&GetLayerDirectory),
            &module)) {
        return {};
    }

    wchar_t path[MAX_PATH]{};
    if (GetModuleFileNameW(module, path, static_cast<DWORD>(std::size(path))) == 0) {
        return {};
    }

    return std::filesystem::path(path).parent_path();
}

std::filesystem::path LegacyConfigPath() {
    if (layerDirectory.empty()) {
        const std::filesystem::path localAppData = LocalAppDataPath();
        if (!localAppData.empty()) {
            return localAppData / L"XR ViewLab" / ConfigFileName;
        }
        std::error_code ec;
        return std::filesystem::temp_directory_path(ec) / L"XR ViewLab" / ConfigFileName;
    }
    return layerDirectory / ConfigFileName;
}

std::filesystem::path UserConfigPath() {
    const std::filesystem::path userData = UserDataDirectory();
    if (userData.empty()) {
        return LegacyConfigPath();
    }

    return userData / ConfigFileName;
}

std::filesystem::path ConfigPath() {
    const std::filesystem::path userConfig = UserConfigPath();
    std::error_code ec;
    if (std::filesystem::exists(userConfig, ec)) {
        return userConfig;
    }

    return LegacyConfigPath();
}

bool ReadBoolSetting(const wchar_t* key, bool fallback) {
    wchar_t buffer[32]{};
    GetPrivateProfileStringW(L"Settings", key, fallback ? L"1" : L"0", buffer, static_cast<DWORD>(std::size(buffer)), ConfigPath().c_str());

    return _wcsicmp(buffer, L"1") == 0 ||
           _wcsicmp(buffer, L"true") == 0 ||
           _wcsicmp(buffer, L"yes") == 0 ||
           _wcsicmp(buffer, L"on") == 0;
}

double ReadDoubleSetting(const wchar_t* key, double fallback) {
    wchar_t buffer[64]{};
    GetPrivateProfileStringW(L"Settings", key, L"", buffer, static_cast<DWORD>(std::size(buffer)), ConfigPath().c_str());

    wchar_t* end = nullptr;
    const double value = std::wcstod(buffer, &end);
    if (end == buffer || !std::isfinite(value)) {
        return fallback;
    }

    return value;
}

std::wstring ReadStringSetting(const wchar_t* key, const wchar_t* fallback) {
    wchar_t buffer[256]{};
    GetPrivateProfileStringW(L"Settings",key,fallback,buffer,static_cast<DWORD>(std::size(buffer)),ConfigPath().c_str());
    return buffer;
}

std::wstring CurrentProcessPath() {
    wchar_t path[MAX_PATH]{};
    if (GetModuleFileNameW(nullptr, path, static_cast<DWORD>(std::size(path))) == 0) {
        return {};
    }

    return path;
}

std::wstring CurrentProcessFileName() {
    const std::filesystem::path path(CurrentProcessPath());
    const std::wstring name = path.filename().wstring();
    return name.empty() ? L"Unknown.exe" : name;
}

bool EndsWithInsensitive(const std::wstring& value, const wchar_t* suffix) {
    const size_t suffixLength = std::wcslen(suffix);
    if (value.size() < suffixLength) {
        return false;
    }

    return _wcsicmp(value.c_str() + value.size() - suffixLength, suffix) == 0;
}

bool ContainsInsensitive(const std::wstring& value, const wchar_t* pattern) {
    std::wstring loweredValue = value;
    std::wstring loweredPattern = pattern;
    std::transform(loweredValue.begin(), loweredValue.end(), loweredValue.begin(), [](wchar_t ch) { return static_cast<wchar_t>(std::towlower(ch)); });
    std::transform(loweredPattern.begin(), loweredPattern.end(), loweredPattern.begin(), [](wchar_t ch) { return static_cast<wchar_t>(std::towlower(ch)); });
    return loweredValue.find(loweredPattern) != std::wstring::npos;
}

bool DetectUevrLikeProcess(const std::wstring& processPath, const std::wstring& processName, const std::wstring& openXrAppName) {
    return EndsWithInsensitive(processName, L"-Win64-Shipping.exe") ||
           EndsWithInsensitive(processName, L"-WinGDK-Shipping.exe") ||
           EndsWithInsensitive(processName, L"-Windows-Shipping.exe") ||
           ContainsInsensitive(processPath, L"\\Engine\\Binaries\\Win64\\") ||
           ContainsInsensitive(openXrAppName, L"Unreal") ||
           ContainsInsensitive(openXrAppName, L"UEVR");
}

std::wstring SanitizeRegistryKey(std::wstring value) {
    if (value.empty()) {
        return L"Unknown.exe";
    }

    for (wchar_t& ch : value) {
        if (ch == L'\\' || ch == L'/' || ch == L':' || ch == L'*' || ch == L'?' || ch == L'"' || ch == L'<' || ch == L'>' || ch == L'|') {
            ch = L'_';
        }
    }

    return value;
}

std::wstring Utf8ToWide(const char* value) {
    if (value == nullptr || value[0] == '\0') {
        return {};
    }

    const int length = MultiByteToWideChar(CP_UTF8, 0, value, -1, nullptr, 0);
    if (length <= 1) {
        return {};
    }

    std::wstring result(static_cast<size_t>(length - 1), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, value, -1, result.data(), length);
    return result;
}

std::wstring AppRegistryPath(const std::wstring& appKey) {
    return std::wstring(AppRegistryRoot) + L"\\" + appKey;
}

std::wstring CurrentUtcTimestamp() {
    SYSTEMTIME st{};
    GetSystemTime(&st);
    wchar_t buffer[32]{};
    swprintf_s(buffer, L"%04u-%02u-%02uT%02u:%02u:%02uZ",
        st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond);
    return buffer;
}

void SetRegistryStringValue(HKEY key, const wchar_t* name, const std::wstring& value) {
    RegSetValueExW(key, name, 0, REG_SZ, reinterpret_cast<const BYTE*>(value.c_str()), static_cast<DWORD>((value.size() + 1) * sizeof(wchar_t)));
}

void SetRegistryDwordValue(HKEY key, const wchar_t* name, DWORD value) {
    RegSetValueExW(key, name, 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value));
}

void RememberApplication(const char* openXrAppName, const char* openXrEngineName, XrResult createResult, const char* runtimeName) {
    currentAppKey = SanitizeRegistryKey(CurrentProcessFileName());
    PublishActiveProfile();
    const std::wstring displayName = Utf8ToWide(openXrAppName);
    const std::wstring engineName = Utf8ToWide(openXrEngineName);
    const std::wstring runtime = Utf8ToWide(runtimeName);
    const std::wstring modulePath = CurrentProcessPath();
    uevrLikeProcess = DetectUevrLikeProcess(modulePath, CurrentProcessFileName(), displayName);

    HKEY key = nullptr;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, AppRegistryPath(currentAppKey).c_str(), 0, nullptr, REG_OPTION_NON_VOLATILE, KEY_SET_VALUE, nullptr, &key, nullptr) != ERROR_SUCCESS) {
        Log("app profile: failed to create registry key for %ls\n", currentAppKey.c_str());
        return;
    }

    if (!displayName.empty()) {
        SetRegistryStringValue(key, L"display_name", displayName);
        SetRegistryStringValue(key, L"last_openxr_app_name", displayName);
    }
    if (!engineName.empty()) {
        SetRegistryStringValue(key, L"last_openxr_engine_name", engineName);
    }
    if (!modulePath.empty()) {
        SetRegistryStringValue(key, L"module", modulePath);
    }
    if (!runtime.empty()) {
        SetRegistryStringValue(key, L"last_runtime", runtime);
    }
    const std::wstring now = CurrentUtcTimestamp();
    SetRegistryStringValue(key, L"last_seen", now);
    SetRegistryStringValue(key, L"last_seen_utc", now);
    SetRegistryDwordValue(key, L"last_pid", static_cast<DWORD>(GetCurrentProcessId()));
    SetRegistryDwordValue(key, L"last_result", static_cast<DWORD>(createResult));
#if defined(_WIN64)
    SetRegistryStringValue(key, L"last_bitness", L"64-bit");
#else
    SetRegistryStringValue(key, L"last_bitness", L"32-bit");
#endif
    RegCloseKey(key);

    Log("app profile: active app_key=%ls display=%ls module=%ls result=%d runtime=%ls\n",
        currentAppKey.c_str(),
        displayName.empty() ? L"<unknown>" : displayName.c_str(),
        modulePath.empty() ? L"<unknown>" : modulePath.c_str(),
        createResult,
        runtime.empty() ? L"<unknown>" : runtime.c_str());
    if (_wcsicmp(CurrentProcessFileName().c_str(), L"Forefront_Internal.exe") == 0 ||
        _wcsicmp(CurrentProcessFileName().c_str(), L"Forefront.exe") == 0) {
        Log("forefront diag: VIEWLAB_LOADED process=%ls app_key=%ls create_instance_result=%d runtime=%ls; "
            "this marker proves the implicit layer entered Forefront's OpenXR process\n",
            modulePath.empty() ? L"<unknown>" : modulePath.c_str(), currentAppKey.c_str(), createResult,
            runtime.empty() ? L"<unknown>" : runtime.c_str());
    }
    if (uevrLikeProcess) {
        Log("app profile: UEVR-like Unreal process detected, enabling conservative runtime guards\n");
    }
}



bool ReadProfileDword(const wchar_t* keyName, DWORD& value) {
    if (currentAppKey.empty()) {
        return false;
    }

    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, AppRegistryPath(currentAppKey).c_str(), 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) {
        return false;
    }

    DWORD type = 0;
    DWORD size = sizeof(value);
    const LONG result = RegQueryValueExW(key, keyName, nullptr, &type, reinterpret_cast<BYTE*>(&value), &size);
    RegCloseKey(key);
    return result == ERROR_SUCCESS && type == REG_DWORD;
}

bool ReadProfileDouble(const wchar_t* keyName, double& value) {
    if (currentAppKey.empty()) {
        return false;
    }

    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, AppRegistryPath(currentAppKey).c_str(), 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS) {
        return false;
    }

    wchar_t buffer[64]{};
    DWORD type = 0;
    DWORD size = sizeof(buffer);
    const LONG result = RegQueryValueExW(key, keyName, nullptr, &type, reinterpret_cast<BYTE*>(buffer), &size);
    RegCloseKey(key);
    if (result != ERROR_SUCCESS || (type != REG_SZ && type != REG_EXPAND_SZ)) {
        return false;
    }

    wchar_t* end = nullptr;
    const double parsed = std::wcstod(buffer, &end);
    if (end == buffer || !std::isfinite(parsed)) {
        return false;
    }
    value = parsed;
    return true;
}

bool ReadProfileString(const wchar_t* keyName, std::wstring& value) {
    if (currentAppKey.empty()) return false;
    HKEY key = nullptr;if(RegOpenKeyExW(HKEY_CURRENT_USER,AppRegistryPath(currentAppKey).c_str(),0,KEY_QUERY_VALUE,&key)!=ERROR_SUCCESS)return false;
    wchar_t buffer[512]{};DWORD type=0,size=sizeof(buffer);const LONG result=RegQueryValueExW(key,keyName,nullptr,&type,reinterpret_cast<BYTE*>(buffer),&size);RegCloseKey(key);
    if(result!=ERROR_SUCCESS||(type!=REG_SZ&&type!=REG_EXPAND_SZ))return false;buffer[std::size(buffer)-1]=0;value=buffer;return true;
}

double MillisToRenderHeight(DWORD value, double fallback) {
    return std::clamp(static_cast<double>(value) / 1000.0, MinVerticalTangent, MaxVerticalTangent);
}

double MillisToRenderScale(DWORD value, double fallback) {
    return std::clamp(static_cast<double>(value) / 1000.0, MinRenderScale, 1.0);
}

double MillisToMaskScale(DWORD value, double fallback) {
    return std::clamp(static_cast<double>(value) / 1000.0, 0.01, 1.0);
}

double DwordToRenderScale(DWORD value, double fallback) {
    if (value == 0u) {
        return fallback;
    }
    const double divisor = value > 3000u ? 1000000.0 : 1000.0;
    return std::clamp(static_cast<double>(value) / divisor, 0.1, 3.0);
}

double SignedMillisToUnit(DWORD value, double fallback) {
    if (value > 2000u) {
        return fallback;
    }
    return std::clamp((static_cast<double>(value) / 1000.0) - 1.0, -1.0, 1.0);
}

double OpeningFromMask(double vertical, double horizontal, double cropVertical, double cropHorizontal) {
    const double v = std::clamp(vertical / std::clamp(cropVertical, 0.01, 1.0), 0.05, 1.0);
    const double h = std::clamp(horizontal / std::clamp(cropHorizontal, 0.01, 1.0), 0.05, 1.0);
    return std::clamp((v + h) * 0.5, 0.05, 1.0);
}

XrQuaternionf NormalizeQuaternion(const XrQuaternionf& value) {
    const float length = std::sqrt(
        value.x * value.x +
        value.y * value.y +
        value.z * value.z +
        value.w * value.w);
    if (length <= 0.0f || !std::isfinite(length)) {
        return XrQuaternionf{0.0f, 0.0f, 0.0f, 1.0f};
    }

    return XrQuaternionf{
        value.x / length,
        value.y / length,
        value.z / length,
        value.w / length};
}

void ReleaseD3D11MaskRenderer() {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    const XrSession releasedSession = g_d3d11Mask.session;
    {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        for (auto& kv : g_swapchains) {
            ReleaseTrackedSwapchainResources(kv.second);
        }
        g_swapchains.clear();
        g_primaryProjectionContext = ProjectionFrameContext{};
    }
    if (g_d3d11Mask.vb)      { g_d3d11Mask.vb->Release();      g_d3d11Mask.vb = nullptr; }
    if (g_d3d11Mask.dss)     { g_d3d11Mask.dss->Release();     g_d3d11Mask.dss = nullptr; }
    if (g_d3d11Mask.bs)      { g_d3d11Mask.bs->Release();      g_d3d11Mask.bs = nullptr; }
    if (g_d3d11Mask.bsOpaque){ g_d3d11Mask.bsOpaque->Release(); g_d3d11Mask.bsOpaque = nullptr; }
    if (g_d3d11Mask.rs)      { g_d3d11Mask.rs->Release();      g_d3d11Mask.rs = nullptr; }
    if (g_d3d11Mask.calibrationRs) { g_d3d11Mask.calibrationRs->Release(); g_d3d11Mask.calibrationRs = nullptr; }
    if (g_d3d11Mask.layout)  { g_d3d11Mask.layout->Release();  g_d3d11Mask.layout = nullptr; }
    if (g_d3d11Mask.calibrationColorCb) { g_d3d11Mask.calibrationColorCb->Release(); g_d3d11Mask.calibrationColorCb = nullptr; }
    if (g_d3d11Mask.visorColorCb) { g_d3d11Mask.visorColorCb->Release(); g_d3d11Mask.visorColorCb = nullptr; }
    if (g_d3d11Mask.calibrationPs) { g_d3d11Mask.calibrationPs->Release(); g_d3d11Mask.calibrationPs = nullptr; }
    if (g_d3d11Mask.overlayPs) { g_d3d11Mask.overlayPs->Release(); g_d3d11Mask.overlayPs = nullptr; }
    for (uint32_t i = 0; i < kNotifyMaxCards; ++i) {
        if (g_d3d11Mask.notifySrv[i]) { g_d3d11Mask.notifySrv[i]->Release(); g_d3d11Mask.notifySrv[i] = nullptr; }
        if (g_d3d11Mask.notifyTex[i]) { g_d3d11Mask.notifyTex[i]->Release(); g_d3d11Mask.notifyTex[i] = nullptr; }
    }
    if (g_d3d11Mask.linearSampler) { g_d3d11Mask.linearSampler->Release(); g_d3d11Mask.linearSampler = nullptr; }
    if (g_d3d11Mask.tintCb)  { g_d3d11Mask.tintCb->Release();  g_d3d11Mask.tintCb = nullptr; }
    if (g_d3d11Mask.texturedPs) { g_d3d11Mask.texturedPs->Release(); g_d3d11Mask.texturedPs = nullptr; }
    if (g_d3d11Mask.ps)      { g_d3d11Mask.ps->Release();      g_d3d11Mask.ps = nullptr; }
    if (g_d3d11Mask.vs)      { g_d3d11Mask.vs->Release();      g_d3d11Mask.vs = nullptr; }
    if (g_d3d11Mask.context) { g_d3d11Mask.context->Release(); g_d3d11Mask.context = nullptr; }
    if (g_d3d11Mask.device)  { g_d3d11Mask.device->Release();  g_d3d11Mask.device = nullptr; }
    g_d3d11Mask = D3D11MaskState{};
    g_visibilityMaskEverCalled.store(false);
}

const XrBaseInStructure* FindStructInChain(const void* next, XrStructureType type) {
    const XrBaseInStructure* cursor = reinterpret_cast<const XrBaseInStructure*>(next);
    while (cursor != nullptr) {
        if (cursor->type == type) {
            return cursor;
        }
        cursor = cursor->next;
    }
    return nullptr;
}

DXGI_FORMAT GetNonSRGBFormat(DXGI_FORMAT fmt) {
    switch (fmt) {
        case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:  return DXGI_FORMAT_B8G8R8A8_UNORM;
        case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:  return DXGI_FORMAT_R8G8B8A8_UNORM;
        case DXGI_FORMAT_BC1_UNORM_SRGB:       return DXGI_FORMAT_BC1_UNORM;
        case DXGI_FORMAT_BC2_UNORM_SRGB:       return DXGI_FORMAT_BC2_UNORM;
        case DXGI_FORMAT_BC3_UNORM_SRGB:       return DXGI_FORMAT_BC3_UNORM;
        // TYPELESS swapchain allocations cannot back an RTV directly; resolve to a
        // concrete UNORM view. Black writes are identical regardless of SRGB encoding.
        case DXGI_FORMAT_R8G8B8A8_TYPELESS:    return DXGI_FORMAT_R8G8B8A8_UNORM;
        case DXGI_FORMAT_B8G8R8A8_TYPELESS:    return DXGI_FORMAT_B8G8R8A8_UNORM;
        case DXGI_FORMAT_R10G10B10A2_TYPELESS: return DXGI_FORMAT_R10G10B10A2_UNORM;
        case DXGI_FORMAT_R16G16B16A16_TYPELESS:return DXGI_FORMAT_R16G16B16A16_UNORM;
        default: return fmt;
    }
}

struct VisorVertex {
    float x;
    float y;
    float alpha;
    float r;
    float g;
    float b;
};

static const char kVisorVS[] =
    // The visor itself is black, but calibration patterns use this channel for exact colour.
    // TEXCOORD is the portable interpolant semantic; the old COLOR route produced black-only
    // diagnostics on the VDXR path despite correct vertex data.
    "struct VSIn { float2 pos : POSITION; float alpha : ALPHA; float3 color : TEXCOORD0; };"
    "struct VSOut { float4 pos : SV_POSITION; float alpha : ALPHA; float3 color : TEXCOORD0; };"
    "VSOut main(VSIn input) {"
    " VSOut output;"
    " output.pos = float4(input.pos.x, input.pos.y, 0.0f, 1.0f);"
    " output.alpha = input.alpha; output.color = input.color;"
    " return output; }";

static const char kVisorPS[] =
    // AA is intentionally disabled product-wide. Emit a guaranteed opaque pixel of the configured
    // visor colour (default black) via a constant buffer, instead of relying on an interpolated
    // alpha semantic on a transparent compositor-owned target. Item 21: the colour is user-tunable
    // so the mask can be matched to a streaming environment-blend key colour if desired.
    "cbuffer VisorColor : register(b0) { float4 visorColor; };"
    "float4 main(float alpha : ALPHA, float3 color : TEXCOORD0) : SV_TARGET { return float4(visorColor.rgb, 1.0f); }";

// Diagnostics deliberately do not rely on a vertex colour interpolant.  VDXR accepted the
// geometry but delivered that interpolant as black, so each opaque calibration colour is bound
// explicitly as a tiny constant buffer before its batch is drawn.
static const char kCalibrationPS[] =
    "cbuffer CalibrationColor : register(b0) { float3 color; float pad; };"
    "float4 main() : SV_TARGET { return float4(color, 1.0f); }";

// Flat overlays use an explicit constant colour. VDXR has previously delivered interpolated
// vertex colour semantics as black; using that path made a valid green crosshair disappear.
static const char kOverlayPS[] =
    "cbuffer OverlayColor : register(b0) { float4 color; };"
    "float4 main() : SV_TARGET { return color; }";

// Textured pixel shader for pre-composited notification cards. TEXCOORD0.xy carries UV (the third
// channel is unused here); the card RGBA is straight-alpha and the tint's alpha animates the fade.
static const char kTexturedPS[] =
    "Texture2D cardTex : register(t0); SamplerState cardSmp : register(s0);"
    "cbuffer Tint : register(b0) { float4 tint; };"
    "float4 main(float4 pos : SV_POSITION, float a : ALPHA, float3 uv : TEXCOORD0) : SV_TARGET {"
    " float4 c = cardTex.Sample(cardSmp, uv.xy);"
    " return float4(c.rgb, c.a * a * tint.a); }";

bool InitD3D11MaskRenderer() {
    if (g_d3d11Mask.initialized) return true;
    if (g_d3d11Mask.failed)      return false;
    if (!g_d3d11Mask.device || !g_d3d11Mask.context) return false;

    typedef HRESULT(WINAPI* PFN_D3DCompile)(
        LPCVOID, SIZE_T, LPCSTR, const D3D_SHADER_MACRO*, ID3DInclude*,
        LPCSTR, LPCSTR, UINT, UINT, ID3DBlob**, ID3DBlob**);

    HMODULE dxcLib = LoadLibraryA("d3dcompiler_47.dll");
    if (!dxcLib) dxcLib = LoadLibraryA("d3dcompiler_46.dll");
    if (!dxcLib) {
        Log("visor: D3D shader compiler not found; visor unavailable for this game\n");
        g_d3d11Mask.failed = true;
        return false;
    }
    auto D3DCompile = reinterpret_cast<PFN_D3DCompile>(GetProcAddress(dxcLib, "D3DCompile"));
    if (!D3DCompile) {
        Log("d3d11 mask: D3DCompile entry point missing\n");
        FreeLibrary(dxcLib);
        g_d3d11Mask.failed = true;
        return false;
    }

    ID3DBlob* vsBlob = nullptr;
    ID3DBlob* psBlob = nullptr;
    ID3DBlob* errBlob = nullptr;

    HRESULT hr = D3DCompile(kVisorVS, std::strlen(kVisorVS), "VS", nullptr, nullptr,
                            "main", "vs_4_0", 0, 0, &vsBlob, &errBlob);
    if (FAILED(hr)) {
        const char* msg = errBlob ? static_cast<const char*>(errBlob->GetBufferPointer()) : "?";
        Log("d3d11 mask: VS compile failed hr=0x%08X %s\n", static_cast<unsigned>(hr), msg);
        if (errBlob) errBlob->Release();
        FreeLibrary(dxcLib);
        g_d3d11Mask.failed = true;
        return false;
    }
    if (errBlob) { errBlob->Release(); errBlob = nullptr; }

    hr = D3DCompile(kVisorPS, std::strlen(kVisorPS), "PS", nullptr, nullptr,
                    "main", "ps_4_0", 0, 0, &psBlob, &errBlob);
    if (FAILED(hr)) {
        const char* msg = errBlob ? static_cast<const char*>(errBlob->GetBufferPointer()) : "?";
        Log("d3d11 mask: PS compile failed hr=0x%08X %s\n", static_cast<unsigned>(hr), msg);
        if (errBlob) errBlob->Release();
        vsBlob->Release();
        FreeLibrary(dxcLib);
        g_d3d11Mask.failed = true;
        return false;
    }
    if (errBlob) { errBlob->Release(); errBlob = nullptr; }

    hr = g_d3d11Mask.device->CreateVertexShader(
        vsBlob->GetBufferPointer(), vsBlob->GetBufferSize(), nullptr, &g_d3d11Mask.vs);
    if (FAILED(hr)) {
        Log("d3d11 mask: CreateVertexShader hr=0x%08X\n", static_cast<unsigned>(hr));
        vsBlob->Release(); psBlob->Release();
        FreeLibrary(dxcLib);
        g_d3d11Mask.failed = true;
        return false;
    }

    hr = g_d3d11Mask.device->CreatePixelShader(
        psBlob->GetBufferPointer(), psBlob->GetBufferSize(), nullptr, &g_d3d11Mask.ps);
    psBlob->Release();
    if (FAILED(hr)) {
        Log("d3d11 mask: CreatePixelShader hr=0x%08X\n", static_cast<unsigned>(hr));
        vsBlob->Release();
        FreeLibrary(dxcLib);
        g_d3d11Mask.failed = true;
        return false;
    }

    hr = D3DCompile(kCalibrationPS, std::strlen(kCalibrationPS), "CalibrationPS", nullptr, nullptr,
                    "main", "ps_4_0", 0, 0, &psBlob, &errBlob);
    if (FAILED(hr)) {
        Log("d3d11 mask: calibration PS compile failed hr=0x%08X\n", static_cast<unsigned>(hr));
        if (errBlob) errBlob->Release();
        vsBlob->Release(); FreeLibrary(dxcLib); g_d3d11Mask.failed = true;
        return false;
    }
    if (errBlob) { errBlob->Release(); errBlob = nullptr; }
    hr = g_d3d11Mask.device->CreatePixelShader(
        psBlob->GetBufferPointer(), psBlob->GetBufferSize(), nullptr, &g_d3d11Mask.calibrationPs);
    psBlob->Release();
    if (FAILED(hr) || !g_d3d11Mask.calibrationPs) {
        Log("d3d11 mask: calibration PS create failed hr=0x%08X\n", static_cast<unsigned>(hr));
        vsBlob->Release(); FreeLibrary(dxcLib); g_d3d11Mask.failed = true;
        return false;
    }

    hr = D3DCompile(kOverlayPS, std::strlen(kOverlayPS), "OverlayPS", nullptr, nullptr,
                    "main", "ps_4_0", 0, 0, &psBlob, &errBlob);
    if (FAILED(hr)) {
        Log("d3d11 mask: overlay PS compile failed hr=0x%08X\n", static_cast<unsigned>(hr));
        if (errBlob) errBlob->Release();
        vsBlob->Release(); FreeLibrary(dxcLib); g_d3d11Mask.failed = true;
        return false;
    }
    if (errBlob) { errBlob->Release(); errBlob = nullptr; }
    hr = g_d3d11Mask.device->CreatePixelShader(
        psBlob->GetBufferPointer(), psBlob->GetBufferSize(), nullptr, &g_d3d11Mask.overlayPs);
    psBlob->Release();
    if (FAILED(hr) || !g_d3d11Mask.overlayPs) {
        Log("d3d11 mask: overlay PS create failed hr=0x%08X\n", static_cast<unsigned>(hr));
        vsBlob->Release(); FreeLibrary(dxcLib); g_d3d11Mask.failed = true;
        return false;
    }

    // Textured PS for notification cards. Non-fatal: if it fails, notification image cards simply
    // do not draw; the visor, HUD, calibration, crosshair, and boundary flash are unaffected.
    hr = D3DCompile(kTexturedPS, std::strlen(kTexturedPS), "TexturedPS", nullptr, nullptr,
                    "main", "ps_4_0", 0, 0, &psBlob, &errBlob);
    if (SUCCEEDED(hr)) {
        g_d3d11Mask.device->CreatePixelShader(psBlob->GetBufferPointer(), psBlob->GetBufferSize(), nullptr, &g_d3d11Mask.texturedPs);
        psBlob->Release();
    } else {
        Log("d3d11 mask: textured PS compile failed hr=0x%08X (notification cards disabled)\n", static_cast<unsigned>(hr));
    }
    if (errBlob) { errBlob->Release(); errBlob = nullptr; }

    D3D11_INPUT_ELEMENT_DESC elems[] = {
        {"POSITION", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0},
        {"ALPHA", 0, DXGI_FORMAT_R32_FLOAT, 0, 8, D3D11_INPUT_PER_VERTEX_DATA, 0},
        {"TEXCOORD", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 12, D3D11_INPUT_PER_VERTEX_DATA, 0},
    };
    hr = g_d3d11Mask.device->CreateInputLayout(
        elems, 3, vsBlob->GetBufferPointer(), vsBlob->GetBufferSize(), &g_d3d11Mask.layout);
    vsBlob->Release();
    FreeLibrary(dxcLib);
    if (FAILED(hr)) {
        Log("d3d11 mask: CreateInputLayout hr=0x%08X\n", static_cast<unsigned>(hr));
        g_d3d11Mask.failed = true;
        return false;
    }

    // Enough for high-segment direct masks and the overlay texture.
    D3D11_BUFFER_DESC vbd{};
    vbd.ByteWidth      = 4096 * sizeof(VisorVertex);
    vbd.Usage          = D3D11_USAGE_DYNAMIC;
    vbd.BindFlags      = D3D11_BIND_VERTEX_BUFFER;
    vbd.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
    if (FAILED(g_d3d11Mask.device->CreateBuffer(&vbd, nullptr, &g_d3d11Mask.vb))) {
        Log("d3d11 mask: vertex buffer create failed\n");
        g_d3d11Mask.failed = true;
        return false;
    }

    D3D11_BUFFER_DESC cbd{};
    cbd.ByteWidth = 16;
    cbd.Usage = D3D11_USAGE_DYNAMIC;
    cbd.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
    cbd.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
    if (FAILED(g_d3d11Mask.device->CreateBuffer(&cbd, nullptr, &g_d3d11Mask.calibrationColorCb))) {
        Log("d3d11 mask: calibration colour buffer create failed\n");
        g_d3d11Mask.failed = true;
        return false;
    }
    if (FAILED(g_d3d11Mask.device->CreateBuffer(&cbd, nullptr, &g_d3d11Mask.visorColorCb))) {
        Log("d3d11 mask: visor colour buffer create failed\n");
        g_d3d11Mask.failed = true;
        return false;
    }

    // Tint constant buffer and sampler for the textured notification path. Non-fatal.
    if (g_d3d11Mask.texturedPs) {
        D3D11_BUFFER_DESC tcbd{}; tcbd.ByteWidth = 16; tcbd.Usage = D3D11_USAGE_DYNAMIC;
        tcbd.BindFlags = D3D11_BIND_CONSTANT_BUFFER; tcbd.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
        if (FAILED(g_d3d11Mask.device->CreateBuffer(&tcbd, nullptr, &g_d3d11Mask.tintCb))) g_d3d11Mask.tintCb = nullptr;
        D3D11_SAMPLER_DESC smpd{}; smpd.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
        smpd.AddressU = smpd.AddressV = smpd.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
        smpd.MaxLOD = D3D11_FLOAT32_MAX;
        if (FAILED(g_d3d11Mask.device->CreateSamplerState(&smpd, &g_d3d11Mask.linearSampler))) g_d3d11Mask.linearSampler = nullptr;
    }

    D3D11_RASTERIZER_DESC rsd{};
    rsd.FillMode        = D3D11_FILL_SOLID;
    rsd.CullMode        = D3D11_CULL_NONE;
    rsd.DepthClipEnable = TRUE;
    hr = g_d3d11Mask.device->CreateRasterizerState(&rsd, &g_d3d11Mask.rs);
    if (FAILED(hr) || !g_d3d11Mask.rs) {
        Log("d3d11 mask: rasterizer state create failed hr=0x%08X\n", static_cast<unsigned>(hr));
        ReleaseD3D11MaskRenderer();
        g_d3d11Mask.failed = true;
        return false;
    }
    D3D11_RASTERIZER_DESC calibrationRsd = rsd;
    calibrationRsd.ScissorEnable = TRUE;
    calibrationRsd.MultisampleEnable = FALSE;
    calibrationRsd.AntialiasedLineEnable = FALSE;
    hr = g_d3d11Mask.device->CreateRasterizerState(&calibrationRsd, &g_d3d11Mask.calibrationRs);
    if (FAILED(hr) || !g_d3d11Mask.calibrationRs) {
        Log("d3d11 mask: calibration rasterizer state create failed hr=0x%08X\n", static_cast<unsigned>(hr));
        ReleaseD3D11MaskRenderer();
        g_d3d11Mask.failed = true;
        return false;
    }

    D3D11_BLEND_DESC bld{};
    bld.RenderTarget[0].BlendEnable           = TRUE;
    bld.RenderTarget[0].SrcBlend              = D3D11_BLEND_SRC_ALPHA;
    bld.RenderTarget[0].DestBlend             = D3D11_BLEND_INV_SRC_ALPHA;
    bld.RenderTarget[0].BlendOp               = D3D11_BLEND_OP_ADD;
    bld.RenderTarget[0].SrcBlendAlpha         = D3D11_BLEND_ONE;
    bld.RenderTarget[0].DestBlendAlpha        = D3D11_BLEND_INV_SRC_ALPHA;
    bld.RenderTarget[0].BlendOpAlpha          = D3D11_BLEND_OP_ADD;
    bld.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
    hr = g_d3d11Mask.device->CreateBlendState(&bld, &g_d3d11Mask.bs);
    if (FAILED(hr) || !g_d3d11Mask.bs) {
        Log("d3d11 mask: blend state create failed hr=0x%08X\n", static_cast<unsigned>(hr));
        ReleaseD3D11MaskRenderer();
        g_d3d11Mask.failed = true;
        return false;
    }

    // Opaque pipeline for AA-off: blending disabled, exactly the draw path that was
    // last confirmed visible in-headset (4.1.103). The alpha-blended path above is
    // used ONLY when visor_antialiasing=1 draws feather strips.
    D3D11_BLEND_DESC bldOpaque{};
    bldOpaque.RenderTarget[0].BlendEnable           = FALSE;
    bldOpaque.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
    hr = g_d3d11Mask.device->CreateBlendState(&bldOpaque, &g_d3d11Mask.bsOpaque);
    if (FAILED(hr) || !g_d3d11Mask.bsOpaque) {
        Log("d3d11 mask: opaque blend state create failed hr=0x%08X\n", static_cast<unsigned>(hr));
        ReleaseD3D11MaskRenderer();
        g_d3d11Mask.failed = true;
        return false;
    }

    D3D11_DEPTH_STENCIL_DESC dsd{};
    dsd.DepthEnable   = FALSE;
    dsd.StencilEnable = FALSE;
    hr = g_d3d11Mask.device->CreateDepthStencilState(&dsd, &g_d3d11Mask.dss);
    if (FAILED(hr) || !g_d3d11Mask.dss) {
        Log("d3d11 mask: depth-stencil state create failed hr=0x%08X\n", static_cast<unsigned>(hr));
        ReleaseD3D11MaskRenderer();
        g_d3d11Mask.failed = true;
        return false;
    }

    g_d3d11Mask.initialized = true;
    Log("visor: D3D11 renderer initialized\n");
    return true;
}

// Curve slider -> superellipse exponent. A near-zero exponential shoulder makes the
// opening visually rectangular without a separate geometry mode. MUST stay identical
// to BeanMaskEditor.CurveExponent.
float VisorCurveExponent(double curve) {
    const float c = static_cast<float>(std::clamp(curve, 0.0, 1.0));
    return 32.0f * std::pow(2.0f / 32.0f, c) + 1024.0f * std::exp(-50.0f * c);
}

// The UI authors apex-y in screen coordinates (y grows DOWN); D3D NDC y grows UP.
// This is the ONLY place that flip happens — past inversion bugs came from flipping
// (or forgetting to flip) in individual geometry builders.
float ApexYFromConfigNdc(double configApexY) {
    return -static_cast<float>(std::clamp(configApexY, -0.5, 0.5));
}

// Fixed high-density tessellation. The retired HD setting previously left the base
// curve at 96 straight chords, which is visibly faceted on modern HMD eye textures.
// 512 segments keeps each chord below the visible stepping threshold while remaining
// within the existing 4096-vertex draw buffer.
uint32_t VisorCurveSegments(uint32_t requested = 512u) {
    return requested;
}

void PushVertex(VisorVertex* vertsOut, uint32_t& v, uint32_t vertCapacity, float x, float y, float alpha = 1.0f) {
    if (v >= vertCapacity) return;
    vertsOut[v++] = VisorVertex{x, y, alpha};
}

void PushTri(
    VisorVertex* vertsOut, uint32_t& v, uint32_t vertCapacity,
    float ax, float ay, float bx, float by, float cx, float cy,
    float aa = 1.0f, float ba = 1.0f, float ca = 1.0f) {
    if (v + 3 > vertCapacity) return;
    PushVertex(vertsOut, v, vertCapacity, ax, ay, aa);
    PushVertex(vertsOut, v, vertCapacity, bx, by, ba);
    PushVertex(vertsOut, v, vertCapacity, cx, cy, ca);
}

void PushQuad(
    VisorVertex* vertsOut, uint32_t& v, uint32_t vertCapacity,
    float x0, float y0, float x1, float y1, float alpha = 1.0f) {
    if (x1 <= x0 || y1 <= y0 || v + 6 > vertCapacity) return;
    PushTri(vertsOut, v, vertCapacity, x0, y0, x1, y0, x1, y1, alpha, alpha, alpha);
    PushTri(vertsOut, v, vertCapacity, x0, y0, x1, y1, x0, y1, alpha, alpha, alpha);
}

// Builds visor border as a flat triangle list (unindexed) in the given NDC bbox.
// Output: x/y/alpha vertices. Returns vertex count (= triangle count * 3).
// Shape math identical to BeanMaskEditor.BuildGeometry.
uint32_t BuildVisorBorderVerts(
    float bboxMinX, float bboxMaxX, float bboxMinY, float bboxMaxY,
    float featherX, float featherY,
    VisorVertex* vertsOut, uint32_t vertCapacity) {
    const float bboxW = bboxMaxX - bboxMinX;
    const float bboxH = bboxMaxY - bboxMinY;
    if (bboxW < 0.001f || bboxH < 0.001f || vertCapacity < 6) return 0;

    // Size uniformly shrinks the exposed aperture without affecting render crop/FOV.
    const float sw  = std::clamp(static_cast<float>(visorSize * visorWidth),  0.01f, 1.0f);
    const float sh  = std::clamp(static_cast<float>(visorSize * visorHeight), 0.01f, 1.0f);
    const float hw  = bboxW * 0.5f * sw;
    const float hh  = bboxH * 0.5f * sh;
    const float cx  = (bboxMinX + bboxMaxX) * 0.5f
                    + (bboxW * 0.5f - hw) * static_cast<float>(visorOffsetX);
    const float cy  = (bboxMinY + bboxMaxY) * 0.5f
                    + (bboxH * 0.5f - hh) * static_cast<float>(visorOffsetY);
    const float exp = VisorCurveExponent(visorCurve);
    // Apex offset, mirroring BeanMaskEditor.BuildGeometry (flip in ApexYFromConfigNdc).
    const float apexY = std::clamp(cy + ApexYFromConfigNdc(visorOuterApexY) * hh * 2.0f,
                                   cy - hh + 0.001f, cy + hh - 0.001f);

    const uint32_t segCount = (std::min)(VisorCurveSegments(), vertCapacity / (visorAntialiasing ? 12u : 6u));
    if (segCount < 4u) return 0;

    constexpr float kTwoPi = 6.28318530717958647692f;
    std::vector<std::pair<float, float>> inner(segCount);
    std::vector<std::pair<float, float>> feather(segCount);
    for (uint32_t i = 0; i < segCount; ++i) {
        const float angle = kTwoPi * static_cast<float>(i) / static_cast<float>(segCount);
        const float c  = std::cos(angle);
        const float s  = std::sin(angle);
        const float sc = (c < 0.0f ? -1.0f : 1.0f) * std::pow(std::abs(c), 2.0f / exp);
        const float syScale = s < 0.0f ? (apexY - (cy - hh)) : ((cy + hh) - apexY);
        const float ss = (s < 0.0f ? -1.0f : 1.0f) * std::pow(std::abs(s), 2.0f / exp) * syScale;
        const float x = std::clamp(cx + sc * hw, bboxMinX + 0.001f, bboxMaxX - 0.001f);
        const float y = std::clamp(apexY + ss, bboxMinY + 0.001f, bboxMaxY - 0.001f);
        inner[i] = {x, y};
        const float toCx = cx - x;
        const float toCy = apexY - y;
        const float len = std::sqrt(toCx * toCx + toCy * toCy);
        feather[i] = len > 0.00001f
            ? std::pair<float, float>{
                std::clamp(x + (toCx / len) * featherX, bboxMinX + 0.001f, bboxMaxX - 0.001f),
                std::clamp(y + (toCy / len) * featherY, bboxMinY + 0.001f, bboxMaxY - 0.001f)}
            : inner[i];
    }

    uint32_t v = 0;
    for (uint32_t i = 0; i < segCount; ++i) {
        const auto [px, py] = inner[i];
        const auto [qx, qy] = inner[(i + 1) % segCount];
        const float mid = kTwoPi * (static_cast<float>(i) + 0.5f) / static_cast<float>(segCount);
        if (std::abs(std::cos(mid)) > std::abs(std::sin(mid))) {
            const float ex = std::cos(mid) < 0.0f ? bboxMinX : bboxMaxX;
            PushTri(vertsOut, v, vertCapacity, ex,py, ex,qy, qx,qy);
            PushTri(vertsOut, v, vertCapacity, ex,py, qx,qy, px,py);
        } else {
            const float ey = std::sin(mid) < 0.0f ? bboxMinY : bboxMaxY;
            PushTri(vertsOut, v, vertCapacity, px,ey, qx,ey, qx,qy);
            PushTri(vertsOut, v, vertCapacity, px,ey, qx,qy, px,py);
        }
        if (visorAntialiasing && v + 6 <= vertCapacity) {
            const auto [fpX, fpY] = feather[i];
            const auto [fqX, fqY] = feather[(i + 1) % segCount];
            PushTri(vertsOut, v, vertCapacity, px, py, qx, qy, fqX, fqY, 1.0f, 1.0f, 0.0f);
            PushTri(vertsOut, v, vertCapacity, px, py, fqX, fqY, fpX, fpY, 1.0f, 0.0f, 0.0f);
        }
    }
    return v;
}

// Nose-divot fill: solid triangles between a cubic bezier and the opening's BOTTOM
// edge (NDC yBase), from the eye-rect centre to the inner edge. The bezier math is
// identical to BeanMaskEditor.AddNoseBridgeCurve (horizontal tangents at both ends;
// width/rise/peakX/steepness shape the handles), made sign-aware so the right eye
// mirrors correctly. Every emitted vertex is clamped into [yBase, yTop], so the divot
// is structurally bottom-only — it can never appear in the upper part of the lens.
void BuildNoseBridgeCurve(
    VisorVertex* vertsOut, uint32_t& v, uint32_t vertCapacity,
    float startX, float endX, float yBase, float yTop) {
    const float dx = endX - startX;
    if (std::abs(dx) < 0.0005f || yTop - yBase < 0.0005f) return;

    const float w  = static_cast<float>(std::clamp(visorInnerBridgeWidth, 0.0, 1.0));
    const float st = static_cast<float>(std::clamp(visorInnerBridgeSteepness, -1.0, 2.0));
    const float rs = static_cast<float>(std::clamp(visorInnerBridgeRise, -0.5, 1.0));
    const float px = static_cast<float>(std::clamp(visorInnerBridgePeakX, -1.0, 2.0));

    // Handle length proportional to the horizontal span, bridge width, and steepness
    // (same formula as the UI preview).
    const float handleLength = std::abs(dx) * (0.1f + w * 0.8f) * (0.5f + st * 0.5f);
    const float dir = dx > 0.0f ? 1.0f : -1.0f;
    const float dy  = yTop - yBase;

    const float p0x = startX,                                           p0y = yBase;
    const float p1x = startX + dir * handleLength * (0.5f + px * 0.5f), p1y = yBase + rs * dy;
    const float p2x = endX - dir * handleLength * (1.0f - px * 0.5f),   p2y = yTop - rs * dy;
    const float p3x = endX,                                             p3y = yTop;

    const float xLo = (std::min)(startX, endX);
    const float xHi = (std::max)(startX, endX);

    constexpr uint32_t segCount = 32;
    float prevX = p0x, prevY = p0y;
    for (uint32_t i = 1; i <= segCount; ++i) {
        const float t = static_cast<float>(i) / static_cast<float>(segCount);
        const float mt = 1.0f - t;
        const float mt2 = mt * mt, mt3 = mt2 * mt, t2 = t * t, t3 = t2 * t;
        float x = mt3 * p0x + 3.0f * mt2 * t * p1x + 3.0f * mt * t2 * p2x + t3 * p3x;
        float y = mt3 * p0y + 3.0f * mt2 * t * p1y + 3.0f * mt * t2 * p2y + t3 * p3y;
        x = std::clamp(x, xLo, xHi);
        y = std::clamp(y, yBase, yTop);
        // Trapezoid between this curve segment and the bottom edge.
        if (v + 6 <= vertCapacity) {
            PushTri(vertsOut, v, vertCapacity, prevX, yBase, prevX, prevY, x, y);
            PushTri(vertsOut, v, vertCapacity, prevX, yBase, x, y, x, yBase);
        }
        prevX = x; prevY = y;
    }
}

// Builds a per-eye visor mask that stays open toward the inner eye edge.
// Left eye: curved cap on the left/outer side, open toward the right/inner side.
// Right eye: mirrored. Top/bottom bands span the full eye rect so the corners are
// fully black instead of leaking rectangular game pixels around the curve.
uint32_t BuildOpenInnerEyeVisorVerts(
    float bboxMinX, float bboxMaxX, float bboxMinY, float bboxMaxY,
    bool outerLeft, float featherX, float featherY,
    VisorVertex* vertsOut, uint32_t vertCapacity) {
    const float bboxW = bboxMaxX - bboxMinX;
    const float bboxH = bboxMaxY - bboxMinY;
    if (bboxW < 0.001f || bboxH < 0.001f || vertCapacity < 18) return 0;

    // Size uniformly shrinks the exposed aperture without affecting render crop/FOV.
    const float sw  = std::clamp(static_cast<float>(visorSize * visorWidth),  0.01f, 1.0f);
    const float sh  = std::clamp(static_cast<float>(visorSize * visorHeight), 0.01f, 1.0f);
    const float hw  = bboxW * 0.5f * sw;
    const float hh  = bboxH * 0.5f * sh;
    const float cx  = (bboxMinX + bboxMaxX) * 0.5f;
    const float cy  = (bboxMinY + bboxMaxY) * 0.5f;
    const float exp = VisorCurveExponent(visorCurve);
    const float y0 = std::clamp(cy - hh, bboxMinY + 0.001f, bboxMaxY - 0.001f);
    const float y1 = std::clamp(cy + hh, bboxMinY + 0.001f, bboxMaxY - 0.001f);
    if (y1 <= y0) return 0;
    // Outer-curve apex, offset by the user's apex-y (flip handled in ApexYFromConfigNdc).
    // Mirrors BeanMaskEditor.AddOpenInnerHalf: the curve's vertical scale is split at
    // the apex so the shape stays inside [y0, y1] at any apex position.
    const float apexY = std::clamp(cy + ApexYFromConfigNdc(visorOuterApexY) * (y1 - y0), y0 + 0.001f, y1 - 0.001f);

    uint32_t v = 0;
    auto quad = [&](float x0, float yq0, float x1, float yq1) {
        if (x1 <= x0 || yq1 <= yq0 || v + 6 > vertCapacity) return;
        PushQuad(vertsOut, v, vertCapacity, x0, yq0, x1, yq1);
    };

    // Hard-mask top and bottom across the whole submitted eye rect; this fixes the
    // visible-corner leak from the closed oval path.
    quad(bboxMinX, bboxMinY, bboxMaxX, y0);
    quad(bboxMinX, y1, bboxMaxX, bboxMaxY);

    if (visorAntialiasing && featherY > 0.0f) {
        const float topFeatherY = std::clamp(y0 + featherY, y0, y1);
        const float bottomFeatherY = std::clamp(y1 - featherY, y0, y1);
        if (topFeatherY > y0 && v + 6 <= vertCapacity) {
            PushTri(vertsOut, v, vertCapacity, bboxMinX, y0, bboxMaxX, y0, bboxMaxX, topFeatherY, 1.0f, 1.0f, 0.0f);
            PushTri(vertsOut, v, vertCapacity, bboxMinX, y0, bboxMaxX, topFeatherY, bboxMinX, topFeatherY, 1.0f, 0.0f, 0.0f);
        }
        if (bottomFeatherY < y1 && v + 6 <= vertCapacity) {
            PushTri(vertsOut, v, vertCapacity, bboxMinX, bottomFeatherY, bboxMaxX, bottomFeatherY, bboxMaxX, y1, 0.0f, 0.0f, 1.0f);
            PushTri(vertsOut, v, vertCapacity, bboxMinX, bottomFeatherY, bboxMaxX, y1, bboxMinX, y1, 0.0f, 1.0f, 1.0f);
        }
    }

    const uint32_t segCount = (std::min)(VisorCurveSegments(), (vertCapacity > v ? (vertCapacity - v) / (visorAntialiasing ? 12u : 6u) : 0u));
    if (segCount < 4u) return v;

    constexpr float kPi = 3.14159265358979323846f;
    std::vector<std::pair<float, float>> curve(segCount + 1);
    std::vector<std::pair<float, float>> feather(segCount + 1);
    for (uint32_t i = 0; i <= segCount; ++i) {
        const float t = -0.5f * kPi + kPi * static_cast<float>(i) / static_cast<float>(segCount);
        const float c = (std::max)(0.0f, std::cos(t));
        const float s = std::sin(t);
        const float cxOffset = std::pow(c, 2.0f / exp) * hw;
        // Vertical scale split at the apex: below-apex points scale to (apexY - y0),
        // above-apex points to (y1 - apexY). Identical to the UI preview.
        const float syScale = s < 0.0f ? (apexY - y0) : (y1 - apexY);
        const float sy = (s < 0.0f ? -1.0f : 1.0f) * std::pow(std::abs(s), 2.0f / exp) * syScale;
        const float x = outerLeft ? (cx - cxOffset) : (cx + cxOffset);
        const float y = apexY + sy;
        const float clampedX = std::clamp(x, bboxMinX + 0.001f, bboxMaxX - 0.001f);
        const float clampedY = std::clamp(y, bboxMinY + 0.001f, bboxMaxY - 0.001f);
        curve[i] = {clampedX, clampedY};
        const float inwardX = outerLeft ? clampedX + featherX : clampedX - featherX;
        feather[i] = {
            std::clamp(inwardX, bboxMinX + 0.001f, bboxMaxX - 0.001f),
            std::clamp(clampedY, bboxMinY + 0.001f, bboxMaxY - 0.001f)};
    }

    const float outerX = outerLeft ? bboxMinX : bboxMaxX;
    for (uint32_t i = 0; i < segCount && v + 6 <= vertCapacity; ++i) {
        const auto [x0c, y0c] = curve[i];
        const auto [x1c, y1c] = curve[i + 1];
        if (outerLeft) {
            PushTri(vertsOut, v, vertCapacity, outerX, y0c, x1c, y1c, x0c, y0c);
            PushTri(vertsOut, v, vertCapacity, outerX, y0c, outerX, y1c, x1c, y1c);
        } else {
            PushTri(vertsOut, v, vertCapacity, x0c, y0c, x1c, y1c, outerX, y0c);
            PushTri(vertsOut, v, vertCapacity, outerX, y0c, x1c, y1c, outerX, y1c);
        }
        if (visorAntialiasing && v + 6 <= vertCapacity) {
            const auto [fx0, fy0] = feather[i];
            const auto [fx1, fy1] = feather[i + 1];
            PushTri(vertsOut, v, vertCapacity, x0c, y0c, x1c, y1c, fx1, fy1, 1.0f, 1.0f, 0.0f);
            PushTri(vertsOut, v, vertCapacity, x0c, y0c, fx1, fy1, fx0, fy0, 1.0f, 0.0f, 0.0f);
        }
    }

    // Nose divot: fill between the bezier and the opening's BOTTOM edge (NDC y0),
    // from the eye-rect centre to the inner edge. Note: the old implementation used
    // y1 here — NDC y-up vs UI y-down — which put the divot at the TOP of the lens.
    if (visorInnerLowerY > 0.0) {
        const float innerX = outerLeft ? bboxMaxX : bboxMinX;
        const float bandTopY = std::clamp(y0 + static_cast<float>(visorInnerLowerY) * (y1 - y0), y0, y1);
		const float spreadStartX = std::clamp(cx + (outerLeft ? -1.0f : 1.0f) *
			static_cast<float>(visorNoseSpreadX) * bboxW, bboxMinX, bboxMaxX);
        BuildNoseBridgeCurve(vertsOut, v, vertCapacity, spreadStartX, innerX, y0, bandTopY);
    }

    return v;
}

float FovLeftTan(const XrFovf& fov) { return static_cast<float>(std::tan(static_cast<double>(fov.angleLeft))); }
float FovRightTan(const XrFovf& fov) { return static_cast<float>(std::tan(static_cast<double>(fov.angleRight))); }
float FovDownTan(const XrFovf& fov) { return static_cast<float>(std::tan(static_cast<double>(fov.angleDown))); }
float FovUpTan(const XrFovf& fov) { return static_cast<float>(std::tan(static_cast<double>(fov.angleUp))); }

float TanToBboxX(float tx, const XrFovf& fov, float bboxMinX, float bboxMaxX) {
    const float l = FovLeftTan(fov);
    const float r = FovRightTan(fov);
    if (std::abs(r - l) < 0.00001f) return (bboxMinX + bboxMaxX) * 0.5f;
    const float ndc = ((tx - l) / (r - l)) * 2.0f - 1.0f;
    return bboxMinX + (ndc + 1.0f) * 0.5f * (bboxMaxX - bboxMinX);
}

float TanToBboxY(float ty, const XrFovf& fov, float bboxMinY, float bboxMaxY) {
    const float d = FovDownTan(fov);
    const float u = FovUpTan(fov);
    if (std::abs(u - d) < 0.00001f) return (bboxMinY + bboxMaxY) * 0.5f;
    const float ndc = ((ty - d) / (u - d)) * 2.0f - 1.0f;
    return bboxMinY + (ndc + 1.0f) * 0.5f * (bboxMaxY - bboxMinY);
}

float NdcToTanX(float x, const XrFovf& fov) {
    const float l = FovLeftTan(fov);
    const float r = FovRightTan(fov);
    return l + (x + 1.0f) * 0.5f * (r - l);
}

float NdcToTanY(float y, const XrFovf& fov) {
    const float d = FovDownTan(fov);
    const float u = FovUpTan(fov);
    return d + (y + 1.0f) * 0.5f * (u - d);
}

uint32_t BuildProjectedPartnerVisorVerts(
    float bboxMinX, float bboxMaxX, float bboxMinY, float bboxMaxY,
    bool drawLeftBoundary,
    const XrFovf& sourceFov, const XrFovf& targetFov,
    VisorVertex* vertsOut, uint32_t vertCapacity) {
    if (vertCapacity < 24) return 0;

    const float threshold = (bboxMaxX - bboxMinX) * 0.025f;
    const float sourceEdgeTan = drawLeftBoundary ? FovLeftTan(sourceFov) : FovRightTan(sourceFov);
    const float projectedEdgeX = TanToBboxX(sourceEdgeTan, targetFov, bboxMinX, bboxMaxX);
    if (drawLeftBoundary) {
        if (projectedEdgeX <= bboxMinX + threshold) return 0;
    } else {
        if (projectedEdgeX >= bboxMaxX - threshold) return 0;
    }

    // Hardcoded maximum corner coverage: opening fills the full bbox, mask extends to edges
    const float sw = std::clamp(static_cast<float>(visorWidth), 0.01f, 1.0f);
    const float sh = std::clamp(static_cast<float>(visorHeight), 0.01f, 1.0f);
    const float bboxW = bboxMaxX - bboxMinX;
    const float bboxH = bboxMaxY - bboxMinY;
    const float hw = bboxW * 0.5f * sw;
    const float hh = bboxH * 0.5f * sh;
    const float sourceCx = (bboxMinX + bboxMaxX) * 0.5f + (bboxW * 0.5f - hw) * static_cast<float>(visorOffsetX);
    const float sourceCy = (bboxMinY + bboxMaxY) * 0.5f + (bboxH * 0.5f - hh) * static_cast<float>(visorOffsetY);
    const float exp = VisorCurveExponent(visorCurve);
    const uint32_t segCount = VisorCurveSegments();
    constexpr float kPi = 3.14159265358979323846f;

    std::vector<std::pair<float, float>> curve(segCount + 1);
    for (uint32_t i = 0; i <= segCount; ++i) {
        const float t = -0.5f * kPi + kPi * static_cast<float>(i) / static_cast<float>(segCount);
        const float c = (std::max)(0.0f, std::cos(t));
        const float s = std::sin(t);
        const float cxOffset = std::pow(c, 2.0f / exp) * hw;
        const float sy = (s < 0.0f ? -1.0f : 1.0f) * std::pow(std::abs(s), 2.0f / exp) * hh;
        const float sourceX = std::clamp(sourceCx + (drawLeftBoundary ? -cxOffset : cxOffset), -1.0f, 1.0f);
        const float sourceY = std::clamp(sourceCy + sy, -1.0f, 1.0f);
        curve[i] = {
            std::clamp(TanToBboxX(NdcToTanX(sourceX, sourceFov), targetFov, bboxMinX, bboxMaxX), bboxMinX, bboxMaxX),
            std::clamp(TanToBboxY(NdcToTanY(sourceY, sourceFov), targetFov, bboxMinY, bboxMaxY), bboxMinY, bboxMaxY)};
    }

    uint32_t v = 0;
    for (uint32_t i = 0; i < segCount && v + 6 <= vertCapacity; ++i) {
        const auto [x0, y0] = curve[i];
        const auto [x1, y1] = curve[i + 1];
        if (std::abs(y1 - y0) < 0.00001f) continue;
        if (drawLeftBoundary) {
            if (x0 <= bboxMinX && x1 <= bboxMinX) continue;
            PushTri(vertsOut, v, vertCapacity, bboxMinX, y0, x1, y1, x0, y0);
            PushTri(vertsOut, v, vertCapacity, bboxMinX, y0, bboxMinX, y1, x1, y1);
        } else {
            if (x0 >= bboxMaxX && x1 >= bboxMaxX) continue;
            PushTri(vertsOut, v, vertCapacity, x0, y0, x1, y1, bboxMaxX, y0);
            PushTri(vertsOut, v, vertCapacity, bboxMaxX, y0, x1, y1, bboxMaxX, y1);
        }
    }
    return v;
}

// Draws the visor border directly into the given eye texture/slice/rect.
// Pure draw: no swapchain lookup, no locking — the caller resolves these and must
// call this on the render thread that owns the D3D11 immediate context. Called from
// XRViewLab_xrReleaseSwapchainImage, i.e. while the app still owns the image and
// immediately before the runtime takes it for composition (last legal write point).
void DrawVisorBorderToTexture(
    ID3D11Texture2D* tex, uint32_t arrSize, int64_t scFormat,
    const EyeView& eye, const std::vector<EyeView>& allViews,
    ID3D11RenderTargetView* cachedRtv = nullptr) {
    if (!g_d3d11Mask.initialized || !g_d3d11Mask.context || !tex) return;
    const XrRect2Di& rect = eye.rect;
    const uint32_t arrayIndex = eye.arraySlice;
    const uint32_t viewIndex = eye.viewIndex;

    D3D11_TEXTURE2D_DESC texDesc{};
    tex->GetDesc(&texDesc);
    if (texDesc.Width == 0 || texDesc.Height == 0 || rect.extent.width <= 0 || rect.extent.height <= 0) {
        return;
    }

    // Draw in full-texture NDC so we can cover the whole submitted eye rectangle,
    // including the corner areas that were outside the old rect-local oval draw.
    const float bboxMinX = std::clamp((2.0f * static_cast<float>(rect.offset.x) / static_cast<float>(texDesc.Width)) - 1.0f, -1.0f, 1.0f);
    const float bboxMaxX = std::clamp((2.0f * static_cast<float>(rect.offset.x + rect.extent.width) / static_cast<float>(texDesc.Width)) - 1.0f, -1.0f, 1.0f);
    const float bboxMaxY = std::clamp(1.0f - (2.0f * static_cast<float>(rect.offset.y) / static_cast<float>(texDesc.Height)), -1.0f, 1.0f);
    const float bboxMinY = std::clamp(1.0f - (2.0f * static_cast<float>(rect.offset.y + rect.extent.height) / static_cast<float>(texDesc.Height)), -1.0f, 1.0f);
    if (bboxMaxX <= bboxMinX || bboxMaxY <= bboxMinY) {
        return;
    }

    constexpr uint32_t kMaxVerts = 4096;
    std::vector<VisorVertex> verts(kMaxVerts);
    const bool outerLeft = viewIndex == 0;
    // The visor shape follows the same setting as the UI preview: "Stencil outer edges
    // only" ON = per-eye open-inner arch (inner/nose side stays visible), OFF = closed
    // bean that fully surrounds the opening (inner edge stenciled too).
    const bool openInnerShape = outerEdgeVisibilityMaskOnly;
    const float featherX = visorAntialiasing ? (3.0f / static_cast<float>(texDesc.Width)) : 0.0f;
    const float featherY = visorAntialiasing ? (3.0f / static_cast<float>(texDesc.Height)) : 0.0f;
    uint32_t vertCount = openInnerShape
        ? BuildOpenInnerEyeVisorVerts(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, outerLeft, featherX, featherY, verts.data(), kMaxVerts)
        : BuildVisorBorderVerts(bboxMinX, bboxMaxX, bboxMinY, bboxMaxY, featherX, featherY, verts.data(), kMaxVerts);
    // Projected partner-eye boundary: blacks out the inner-eye band the cropped partner
    // eye can no longer render. This is exactly the inner-edge stenciling the user turns
    // OFF with "Stencil outer edges only" — only draw it when that setting is unchecked.
    if (!openInnerShape && allViews.size() >= 2 && viewIndex < 2 && vertCount < kMaxVerts) {
        const EyeView& leftEye = allViews[0].viewIndex == 0 ? allViews[0] : allViews[1];
        const EyeView& rightEye = allViews[0].viewIndex == 1 ? allViews[0] : allViews[1];
        if (viewIndex == 0) {
            vertCount += BuildProjectedPartnerVisorVerts(
                bboxMinX, bboxMaxX, bboxMinY, bboxMaxY,
                false, rightEye.fov, eye.fov, verts.data() + vertCount, kMaxVerts - vertCount);
        } else {
            vertCount += BuildProjectedPartnerVisorVerts(
                bboxMinX, bboxMaxX, bboxMinY, bboxMaxY,
                true, leftEye.fov, eye.fov, verts.data() + vertCount, kMaxVerts - vertCount);
        }
    }
    if (vertCount == 0) {
        if (!g_diagDrawNoVerts.exchange(true))
            Log("d3d11 mask DIAG: FAIL border geometry produced 0 vertices\n");
        return;
    }

    D3D11_MAPPED_SUBRESOURCE mapped{};
    if (FAILED(g_d3d11Mask.context->Map(g_d3d11Mask.vb, 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped))) {
        if (!g_diagDrawMapFailed.exchange(true))
            Log("d3d11 mask DIAG: FAIL vertex buffer Map failed\n");
        return;
    }
    memcpy(mapped.pData, verts.data(), vertCount * sizeof(VisorVertex));
    g_d3d11Mask.context->Unmap(g_d3d11Mask.vb, 0);

    // Pick a concrete RTV format. Runtimes frequently allocate swapchain textures as
    // TYPELESS so they can be viewed as SRGB or UNORM; an RTV cannot use a TYPELESS
    // format. Prefer the app-requested swapchain format (mapped to non-SRGB) which is
    // always concrete and view-compatible with the underlying texture. Black writes are
    // identical under SRGB/UNORM so the non-SRGB view is safe.
    DXGI_FORMAT rtvFormat = GetNonSRGBFormat(static_cast<DXGI_FORMAT>(scFormat));
    if (rtvFormat == DXGI_FORMAT_UNKNOWN) {
        rtvFormat = GetNonSRGBFormat(texDesc.Format);
    }

    D3D11_RENDER_TARGET_VIEW_DESC rtvDesc{};
    rtvDesc.Format = rtvFormat;
    if (arrSize > 1) {
        rtvDesc.ViewDimension                  = D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
        rtvDesc.Texture2DArray.MipSlice        = 0;
        rtvDesc.Texture2DArray.FirstArraySlice = arrayIndex;
        rtvDesc.Texture2DArray.ArraySize       = 1;
    } else {
        rtvDesc.ViewDimension      = D3D11_RTV_DIMENSION_TEXTURE2D;
        rtvDesc.Texture2D.MipSlice = 0;
    }
    const bool ownsRtv = (cachedRtv == nullptr);
    ID3D11RenderTargetView* rtv = cachedRtv;
    if (!rtv) {
        const HRESULT rtvHr = g_d3d11Mask.device->CreateRenderTargetView(tex, &rtvDesc, &rtv);
        if (FAILED(rtvHr) || !rtv) {
            if (!g_diagDrawRtvFailed.exchange(true))
                Log("d3d11 mask DIAG: FAIL CreateRenderTargetView hr=0x%08X rtvFormat=%d texFormat=%d arrSize=%u slice=%u\n",
                    static_cast<unsigned>(rtvHr), static_cast<int>(rtvFormat),
                    static_cast<int>(texDesc.Format), arrSize, arrayIndex);
            return;
        }
    }

    D3D11_VIEWPORT vp{};
    vp.TopLeftX = 0.0f;
    vp.TopLeftY = 0.0f;
    vp.Width    = static_cast<float>(texDesc.Width);
    vp.Height   = static_cast<float>(texDesc.Height);
    vp.MaxDepth = 1.0f;

    // Save D3D11 state.
    ID3D11RenderTargetView* sRTVs[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT]{};
    ID3D11DepthStencilView* sDSV = nullptr;
    g_d3d11Mask.context->OMGetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, sRTVs, &sDSV);
    UINT sVPCount = D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE;
    D3D11_VIEWPORT sVPs[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{};
    g_d3d11Mask.context->RSGetViewports(&sVPCount, sVPs);
    ID3D11RasterizerState* sRS = nullptr;
    g_d3d11Mask.context->RSGetState(&sRS);
    ID3D11BlendState* sBS = nullptr; FLOAT sBF[4]{}; UINT sSM = 0;
    g_d3d11Mask.context->OMGetBlendState(&sBS, sBF, &sSM);
    ID3D11DepthStencilState* sDSS = nullptr; UINT sSRef = 0;
    g_d3d11Mask.context->OMGetDepthStencilState(&sDSS, &sSRef);
    ID3D11VertexShader* sVS = nullptr;
    g_d3d11Mask.context->VSGetShader(&sVS, nullptr, nullptr);
    ID3D11PixelShader* sPS = nullptr;
    g_d3d11Mask.context->PSGetShader(&sPS, nullptr, nullptr);
    ID3D11InputLayout* sLayout = nullptr;
    g_d3d11Mask.context->IAGetInputLayout(&sLayout);
    D3D11_PRIMITIVE_TOPOLOGY sTopo = D3D11_PRIMITIVE_TOPOLOGY_UNDEFINED;
    g_d3d11Mask.context->IAGetPrimitiveTopology(&sTopo);
    ID3D11Buffer* sVB = nullptr; UINT sStride = 0, sOff = 0;
    g_d3d11Mask.context->IAGetVertexBuffers(0, 1, &sVB, &sStride, &sOff);

    // Draw visor border.
    static const FLOAT kBF[] = {0.f, 0.f, 0.f, 0.f};
    UINT stride = sizeof(VisorVertex), offset = 0;
    g_d3d11Mask.context->OMSetRenderTargets(1, &rtv, nullptr);
    g_d3d11Mask.context->RSSetViewports(1, &vp);
    g_d3d11Mask.context->RSSetState(g_d3d11Mask.rs);
    g_d3d11Mask.context->OMSetBlendState(
        visorAntialiasing ? g_d3d11Mask.bs : g_d3d11Mask.bsOpaque, kBF, 0xFFFFFFFF);
    g_d3d11Mask.context->OMSetDepthStencilState(g_d3d11Mask.dss, 0);
    g_d3d11Mask.context->VSSetShader(g_d3d11Mask.vs, nullptr, 0);
    g_d3d11Mask.context->PSSetShader(g_d3d11Mask.ps, nullptr, 0);
    g_d3d11Mask.context->IASetInputLayout(g_d3d11Mask.layout);
    g_d3d11Mask.context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    g_d3d11Mask.context->IASetVertexBuffers(0, 1, &g_d3d11Mask.vb, &stride, &offset);
    // Item 21: bind the configured visor colour (default black) for kVisorPS at register b0.
    if (g_d3d11Mask.visorColorCb) {
        D3D11_MAPPED_SUBRESOURCE vcm{};
        if (SUCCEEDED(g_d3d11Mask.context->Map(g_d3d11Mask.visorColorCb, 0, D3D11_MAP_WRITE_DISCARD, 0, &vcm))) {
            const float vc[4] = { g_visorColor[0], g_visorColor[1], g_visorColor[2], 1.f };
            memcpy(vcm.pData, vc, sizeof(vc));
            g_d3d11Mask.context->Unmap(g_d3d11Mask.visorColorCb, 0);
        }
        g_d3d11Mask.context->PSSetConstantBuffers(0, 1, &g_d3d11Mask.visorColorCb);
    }
    g_d3d11Mask.context->Draw(vertCount, 0);

    if (!g_diagDrawOk.exchange(true))
        Log("d3d11 mask DIAG: OK draw executed verts=%u rtvFormat=%d texFormat=%d arrSize=%u slice=%u view=%u outer=%s "
            "rect=(%d,%d %dx%d) tex=%ux%u aa=%d hd=%d\n",
            vertCount, static_cast<int>(rtvFormat), static_cast<int>(texDesc.Format),
            arrSize, arrayIndex, viewIndex, outerLeft ? "left" : "right",
            rect.offset.x, rect.offset.y, rect.extent.width, rect.extent.height,
            texDesc.Width, texDesc.Height,
            visorAntialiasing ? 1 : 0, visorHD ? 1 : 0);

    // Restore D3D11 state.
    g_d3d11Mask.context->OMSetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, sRTVs, sDSV);
    if (sVPCount > 0) g_d3d11Mask.context->RSSetViewports(sVPCount, sVPs);
    g_d3d11Mask.context->RSSetState(sRS);
    g_d3d11Mask.context->OMSetBlendState(sBS, sBF, sSM);
    g_d3d11Mask.context->OMSetDepthStencilState(sDSS, sSRef);
    g_d3d11Mask.context->VSSetShader(sVS, nullptr, 0);
    g_d3d11Mask.context->PSSetShader(sPS, nullptr, 0);
    g_d3d11Mask.context->IASetInputLayout(sLayout);
    g_d3d11Mask.context->IASetPrimitiveTopology(sTopo);
    g_d3d11Mask.context->IASetVertexBuffers(0, 1, &sVB, &sStride, &sOff);

    if (ownsRtv) rtv->Release();
    for (ID3D11RenderTargetView* savedRtv : sRTVs) { if (savedRtv) savedRtv->Release(); }
    if (sDSV) sDSV->Release();
    if (sRS)  sRS->Release();  if (sBS)  sBS->Release();
    if (sDSS) sDSS->Release(); if (sVS)  sVS->Release();
    if (sPS)  sPS->Release();  if (sLayout) sLayout->Release();
    if (sVB)  sVB->Release();

    // Force submission so a separate-device runtime encoder (e.g. VDXR streaming)
    // sees the write before it consumes the released image.
    g_d3d11Mask.context->Flush();
}


ID3D11RenderTargetView* CachedRtvFor(const TrackedSwapchain& ts, uint32_t imageIndex, uint32_t arraySlice);

// Calibration grid (hidden ini key calibration_grid=1): vertical + horizontal lines every
// 64 texture px across the eye image, every 4th line thicker. Drawn opaque at release time,
// after the game has finished the frame, so the grid is exactly uniform in the submitted
// texture — a ground-truth ruler for judging downstream stream/encode quality.
void DrawCalibrationGridToTexture(
    ID3D11Texture2D* tex, uint32_t arrSize, int64_t scFormat,
    const EyeView& eye, const std::vector<EyeView>& allViews,
    ID3D11RenderTargetView* cachedRtv) {
    const XrRect2Di& rect = eye.rect;
    const uint32_t arrayIndex = eye.arraySlice;
    if (!g_d3d11Mask.initialized || !g_d3d11Mask.context || !tex ||
        rect.extent.width <= 0 || rect.extent.height <= 0) return;

    D3D11_TEXTURE2D_DESC texDesc{};
    tex->GetDesc(&texDesc);
    if (texDesc.Width == 0 || texDesc.Height == 0) return;

    constexpr int kSpacing = 64;
    constexpr uint32_t kMaxVerts = 4096;
    std::vector<VisorVertex> verts;
    verts.reserve(512);
    auto pushQuad = [&](float x0, float y0, float x1, float y1, float shade) {
        if (verts.size() + 6 > kMaxVerts) return;
        const float xs[6] = {x0, x1, x1, x0, x1, x0};
        const float ys[6] = {y0, y0, y1, y0, y1, y1};
        for (int i = 0; i < 6; ++i) {
            VisorVertex v{};
            v.x = xs[i]; v.y = ys[i];
            v.r = shade; v.g = shade; v.b = shade;
            v.alpha = 1.0f;
            verts.push_back(v);
        }
    };
    // Coordinates are integer submitted-image pixels, transformed through an imageRect-local
    // viewport.  This is D3D11's pixel-centre convention; deliberately no D3D9 half-pixel bias.
    auto ndcX = [&](int px) { return (2.0f * static_cast<float>(px - rect.offset.x) / static_cast<float>(rect.extent.width)) - 1.0f; };
    auto ndcY = [&](int py) { return 1.0f - (2.0f * static_cast<float>(py - rect.offset.y) / static_cast<float>(rect.extent.height)); };
    const float top = ndcY(rect.offset.y);
    const float bottom = ndcY(rect.offset.y + rect.extent.height);
    const float left = ndcX(rect.offset.x);
    const float right = ndcX(rect.offset.x + rect.extent.width);
    // Keep the ruler literal (one cell is always 64 submitted-texture pixels), but anchor its
    // origin at the same shared tangent-space centre as the fused crosshair. Crop/resolution
    // changes can reveal fewer cells; they must neither resize them nor move the origin to an
    // arbitrary imageRect corner.
    const OverlayCoordinateResolver coordinates(eye, allViews);
    const auto sharedCentre = coordinates.ResolveSharedTangent(0.f, 0.f, true);
    const int centreX = std::clamp(static_cast<int>(std::lround(sharedCentre.first)),
        rect.offset.x, rect.offset.x + rect.extent.width - 1);
    const int centreY = std::clamp(static_cast<int>(std::lround(sharedCentre.second)),
        rect.offset.y, rect.offset.y + rect.extent.height - 1);
    auto drawVertical = [&](int px, int step) {
        const bool major = (abs(step) % 4) == 0;
        const int width = major ? 4 : 2;
        pushQuad(ndcX(px), bottom, ndcX((std::min)(px + width, rect.offset.x + rect.extent.width)), top,
            major ? 1.0f : 0.55f);
    };
    auto drawHorizontal = [&](int py, int step) {
        const bool major = (abs(step) % 4) == 0;
        const int height = major ? 4 : 2;
        pushQuad(left, ndcY((std::min)(py + height, rect.offset.y + rect.extent.height)), right, ndcY(py),
            major ? 1.0f : 0.55f);
    };
    for (int step = 0, px = centreX; px < rect.offset.x + rect.extent.width; ++step, px += kSpacing)
        drawVertical(px, step);
    for (int step = -1, px = centreX - kSpacing; px >= rect.offset.x; --step, px -= kSpacing)
        drawVertical(px, step);
    for (int step = 0, py = centreY; py < rect.offset.y + rect.extent.height; ++step, py += kSpacing)
        drawHorizontal(py, step);
    for (int step = -1, py = centreY - kSpacing; py >= rect.offset.y; --step, py -= kSpacing)
        drawHorizontal(py, step);
    if (verts.empty()) return;

    D3D11_MAPPED_SUBRESOURCE mapped{};
    if (FAILED(g_d3d11Mask.context->Map(g_d3d11Mask.vb, 0, D3D11_MAP_WRITE_DISCARD, 0, &mapped))) return;
    memcpy(mapped.pData, verts.data(), verts.size() * sizeof(VisorVertex));
    g_d3d11Mask.context->Unmap(g_d3d11Mask.vb, 0);

    DXGI_FORMAT rtvFormat = GetNonSRGBFormat(static_cast<DXGI_FORMAT>(scFormat));
    if (rtvFormat == DXGI_FORMAT_UNKNOWN) rtvFormat = GetNonSRGBFormat(texDesc.Format);
    D3D11_RENDER_TARGET_VIEW_DESC rtvDesc{};
    rtvDesc.Format = rtvFormat;
    if (arrSize > 1) {
        rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
        rtvDesc.Texture2DArray.MipSlice = 0;
        rtvDesc.Texture2DArray.FirstArraySlice = arrayIndex;
        rtvDesc.Texture2DArray.ArraySize = 1;
    } else {
        rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2D;
        rtvDesc.Texture2D.MipSlice = 0;
    }
    const bool ownsRtv = (cachedRtv == nullptr);
    ID3D11RenderTargetView* rtv = cachedRtv;
    if (!rtv && (FAILED(g_d3d11Mask.device->CreateRenderTargetView(tex, &rtvDesc, &rtv)) || !rtv)) return;

    ID3D11RenderTargetView* sRTVs[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT]{};
    ID3D11DepthStencilView* sDSV = nullptr;
    g_d3d11Mask.context->OMGetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, sRTVs, &sDSV);
    UINT sVPCount = D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE;
    D3D11_VIEWPORT sVPs[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{};
    g_d3d11Mask.context->RSGetViewports(&sVPCount, sVPs);
    UINT sScissorCount = D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE;
    D3D11_RECT sScissors[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{};
    g_d3d11Mask.context->RSGetScissorRects(&sScissorCount, sScissors);
    ID3D11RasterizerState* sRS = nullptr; g_d3d11Mask.context->RSGetState(&sRS);
    ID3D11BlendState* sBS = nullptr; FLOAT sBF[4]{}; UINT sSM = 0; g_d3d11Mask.context->OMGetBlendState(&sBS, sBF, &sSM);
    ID3D11DepthStencilState* sDSS = nullptr; UINT sSRef = 0; g_d3d11Mask.context->OMGetDepthStencilState(&sDSS, &sSRef);
    ID3D11VertexShader* sVS = nullptr; g_d3d11Mask.context->VSGetShader(&sVS, nullptr, nullptr);
    ID3D11PixelShader* sPS = nullptr; g_d3d11Mask.context->PSGetShader(&sPS, nullptr, nullptr);
    ID3D11Buffer* sPSCB = nullptr; g_d3d11Mask.context->PSGetConstantBuffers(0,1,&sPSCB);
    ID3D11InputLayout* sLayout = nullptr; g_d3d11Mask.context->IAGetInputLayout(&sLayout);
    D3D11_PRIMITIVE_TOPOLOGY sTopo = D3D11_PRIMITIVE_TOPOLOGY_UNDEFINED; g_d3d11Mask.context->IAGetPrimitiveTopology(&sTopo);
    ID3D11Buffer* sVB = nullptr; UINT sStride = 0, sOff = 0; g_d3d11Mask.context->IAGetVertexBuffers(0, 1, &sVB, &sStride, &sOff);

    D3D11_VIEWPORT vp{}; vp.TopLeftX = static_cast<float>(rect.offset.x); vp.TopLeftY = static_cast<float>(rect.offset.y); vp.Width = static_cast<float>(rect.extent.width); vp.Height = static_cast<float>(rect.extent.height); vp.MaxDepth = 1.0f;
    D3D11_RECT scissor{rect.offset.x, rect.offset.y, rect.offset.x + rect.extent.width, rect.offset.y + rect.extent.height};
    static const FLOAT kBF[] = {0.f, 0.f, 0.f, 0.f};
    UINT stride = sizeof(VisorVertex), offset = 0;
    g_d3d11Mask.context->OMSetRenderTargets(1, &rtv, nullptr);
    g_d3d11Mask.context->RSSetViewports(1, &vp);
    g_d3d11Mask.context->RSSetState(g_d3d11Mask.calibrationRs);
    g_d3d11Mask.context->RSSetScissorRects(1, &scissor);
    g_d3d11Mask.context->OMSetBlendState(g_d3d11Mask.bsOpaque, kBF, 0xFFFFFFFF);
    g_d3d11Mask.context->OMSetDepthStencilState(g_d3d11Mask.dss, 0);
    g_d3d11Mask.context->VSSetShader(g_d3d11Mask.vs, nullptr, 0);
    // The grid previously remained on the interpolated visor-colour shader while every other
    // calibration pattern moved to the proven constant-colour shader. On VDXR that made valid
    // grid geometry disappear. Major/minor weight is carried by line thickness; colour is explicit.
    float gridColour[4]={1.f,1.f,1.f,0.f}; D3D11_MAPPED_SUBRESOURCE cm{};
    if(SUCCEEDED(g_d3d11Mask.context->Map(g_d3d11Mask.calibrationColorCb,0,D3D11_MAP_WRITE_DISCARD,0,&cm))) {
        memcpy(cm.pData,gridColour,sizeof(gridColour)); g_d3d11Mask.context->Unmap(g_d3d11Mask.calibrationColorCb,0);
    }
    g_d3d11Mask.context->PSSetShader(g_d3d11Mask.calibrationPs, nullptr, 0);
    g_d3d11Mask.context->PSSetConstantBuffers(0,1,&g_d3d11Mask.calibrationColorCb);
    g_d3d11Mask.context->IASetInputLayout(g_d3d11Mask.layout);
    g_d3d11Mask.context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    g_d3d11Mask.context->IASetVertexBuffers(0, 1, &g_d3d11Mask.vb, &stride, &offset);
    g_d3d11Mask.context->Draw(static_cast<UINT>(verts.size()), 0);

    g_d3d11Mask.context->OMSetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, sRTVs, sDSV);
    if (sVPCount > 0) g_d3d11Mask.context->RSSetViewports(sVPCount, sVPs);
    if (sScissorCount > 0) g_d3d11Mask.context->RSSetScissorRects(sScissorCount, sScissors);
    g_d3d11Mask.context->RSSetState(sRS);
    g_d3d11Mask.context->OMSetBlendState(sBS, sBF, sSM);
    g_d3d11Mask.context->OMSetDepthStencilState(sDSS, sSRef);
    g_d3d11Mask.context->VSSetShader(sVS, nullptr, 0);
    g_d3d11Mask.context->PSSetShader(sPS, nullptr, 0);
    g_d3d11Mask.context->PSSetConstantBuffers(0,1,&sPSCB);
    g_d3d11Mask.context->IASetInputLayout(sLayout);
    g_d3d11Mask.context->IASetPrimitiveTopology(sTopo);
    g_d3d11Mask.context->IASetVertexBuffers(0, 1, &sVB, &sStride, &sOff);

    if (ownsRtv) rtv->Release();
    for (ID3D11RenderTargetView* savedRtv : sRTVs) { if (savedRtv) savedRtv->Release(); }
    if (sDSV) sDSV->Release();
    if (sRS) sRS->Release(); if (sBS) sBS->Release();
    if (sDSS) sDSS->Release(); if (sVS) sVS->Release();
    if (sPS) sPS->Release(); if(sPSCB)sPSCB->Release(); if (sLayout) sLayout->Release();
    if (sVB) sVB->Release();
    g_d3d11Mask.context->Flush();
}

// Stereo-coherent HUD snapshot: metrics, alarm states, and trace samples are captured once
// when the left eye draws and reused verbatim for the right eye, so both eyes always show
// identical values and an identical trace (no retinal rivalry between the eyes). Guarded by
// g_rendererMutex, which every draw path already holds.
struct HudDrawSnapshot {
    HudMetric metrics[kHudWidgetCount]{};
    bool alarm[kHudWidgetCount]{};
    HudMetricState states[kHudWidgetCount]{};
    double budgetMs = 0.0;
    uint64_t widgetMask=0;
    uint32_t widgetOrder=0, graphChannels=0;
    HudGraphMode graphMode=HudGraphMode::Deviation;
    std::array<HudFrameSample, 600> samples{};
    size_t sampleCount = 0;
    bool valid = false;
};
HudDrawSnapshot g_hudDrawSnap;

// Draw the non-grid calibration plates in one bounded, pixel-space batch.  The grid retains its
// original renderer above so its investigation-proven output remains unchanged.
void DrawCalibrationOverlayToTexture(
    ID3D11Texture2D* tex, uint32_t arrSize, int64_t scFormat,
    const EyeView& eye, const std::vector<EyeView>& allViews, ID3D11RenderTargetView* cachedRtv,
    bool includeCalibration, bool includeHudTrace, uint32_t featureMask = kAllMirrorFeatures) {
    if (!g_d3d11Mask.initialized || !g_d3d11Mask.context || !tex ||
        eye.rect.extent.width <= 0 || eye.rect.extent.height <= 0) return;
    D3D11_TEXTURE2D_DESC texDesc{}; tex->GetDesc(&texDesc);
    if (texDesc.Width == 0 || texDesc.Height == 0) return;

    constexpr uint32_t kMaxVerts = 4096;
    std::vector<VisorVertex> verts; verts.reserve(kMaxVerts);
    auto ndcX = [&](float px) { return (2.f * (px - eye.rect.offset.x) / eye.rect.extent.width) - 1.f; };
    auto ndcY = [&](float py) { return 1.f - (2.f * (py - eye.rect.offset.y) / eye.rect.extent.height); };
    auto emit = [&](float x, float y, float r, float g, float b) {
        VisorVertex v{}; v.x = ndcX(x); v.y = ndcY(y); v.r = r; v.g = g; v.b = b; v.alpha = 1.f; verts.push_back(v);
    };
    auto quad = [&](float x0, float y0, float x1, float y1, float r, float g, float b) {
        if (verts.size() + 6 > kMaxVerts) return false;
        emit(x0,y0,r,g,b); emit(x1,y0,r,g,b); emit(x1,y1,r,g,b);
        emit(x0,y0,r,g,b); emit(x1,y1,r,g,b); emit(x0,y1,r,g,b); return true;
    };

    DXGI_FORMAT rtvFormat = GetNonSRGBFormat(static_cast<DXGI_FORMAT>(scFormat));
    if (rtvFormat == DXGI_FORMAT_UNKNOWN) rtvFormat = GetNonSRGBFormat(texDesc.Format);
    D3D11_RENDER_TARGET_VIEW_DESC rtvDesc{}; rtvDesc.Format = rtvFormat;
    if (arrSize > 1) { rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2DARRAY; rtvDesc.Texture2DArray.FirstArraySlice = eye.arraySlice; rtvDesc.Texture2DArray.ArraySize = 1; }
    else rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2D;
    const bool ownsRtv = cachedRtv == nullptr; ID3D11RenderTargetView* rtv = cachedRtv;
    if (!rtv && (FAILED(g_d3d11Mask.device->CreateRenderTargetView(tex, &rtvDesc, &rtv)) || !rtv)) return;

    ID3D11RenderTargetView* sRTVs[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT]{}; ID3D11DepthStencilView* sDSV = nullptr;
    g_d3d11Mask.context->OMGetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, sRTVs, &sDSV);
    UINT sVPCount = D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE; D3D11_VIEWPORT sVPs[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{}; g_d3d11Mask.context->RSGetViewports(&sVPCount, sVPs);
    UINT sScissorCount = D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE; D3D11_RECT sScissors[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{}; g_d3d11Mask.context->RSGetScissorRects(&sScissorCount, sScissors);
    ID3D11RasterizerState* sRS=nullptr; g_d3d11Mask.context->RSGetState(&sRS); ID3D11BlendState* sBS=nullptr; FLOAT sBF[4]{}; UINT sSM=0; g_d3d11Mask.context->OMGetBlendState(&sBS,sBF,&sSM);
    ID3D11DepthStencilState* sDSS=nullptr; UINT sSRef=0; g_d3d11Mask.context->OMGetDepthStencilState(&sDSS,&sSRef); ID3D11VertexShader* sVS=nullptr; g_d3d11Mask.context->VSGetShader(&sVS,nullptr,nullptr); ID3D11PixelShader* sPS=nullptr; g_d3d11Mask.context->PSGetShader(&sPS,nullptr,nullptr); ID3D11Buffer* sPSCB=nullptr; g_d3d11Mask.context->PSGetConstantBuffers(0,1,&sPSCB); ID3D11InputLayout* sLayout=nullptr; g_d3d11Mask.context->IAGetInputLayout(&sLayout); D3D11_PRIMITIVE_TOPOLOGY sTopo; g_d3d11Mask.context->IAGetPrimitiveTopology(&sTopo); ID3D11Buffer* sVB=nullptr; UINT sStride=0,sOff=0; g_d3d11Mask.context->IAGetVertexBuffers(0,1,&sVB,&sStride,&sOff);
    D3D11_VIEWPORT vp{}; vp.TopLeftX=(float)eye.rect.offset.x; vp.TopLeftY=(float)eye.rect.offset.y; vp.Width=(float)eye.rect.extent.width; vp.Height=(float)eye.rect.extent.height; vp.MaxDepth=1.f; D3D11_RECT scissor{eye.rect.offset.x,eye.rect.offset.y,eye.rect.offset.x+eye.rect.extent.width,eye.rect.offset.y+eye.rect.extent.height}; static const FLOAT kBF[] = {0,0,0,0}; UINT stride=sizeof(VisorVertex), offset=0;
    g_d3d11Mask.context->OMSetRenderTargets(1,&rtv,nullptr); g_d3d11Mask.context->RSSetViewports(1,&vp); g_d3d11Mask.context->RSSetScissorRects(1,&scissor); g_d3d11Mask.context->RSSetState(g_d3d11Mask.calibrationRs); g_d3d11Mask.context->OMSetBlendState(g_d3d11Mask.bsOpaque,kBF,0xFFFFFFFF); g_d3d11Mask.context->OMSetDepthStencilState(g_d3d11Mask.dss,0); g_d3d11Mask.context->VSSetShader(g_d3d11Mask.vs,nullptr,0); g_d3d11Mask.context->PSSetShader(g_d3d11Mask.calibrationPs,nullptr,0); g_d3d11Mask.context->IASetInputLayout(g_d3d11Mask.layout); g_d3d11Mask.context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST); g_d3d11Mask.context->IASetVertexBuffers(0,1,&g_d3d11Mask.vb,&stride,&offset);
    auto flush = [&] { if (verts.empty()) return; std::map<uint32_t, std::vector<VisorVertex>> colours; for(size_t i=0;i+2<verts.size();i+=3) { const VisorVertex& v=verts[i]; uint32_t key=(uint32_t)(v.r*255.f+.5f)<<16 | (uint32_t)(v.g*255.f+.5f)<<8 | (uint32_t)(v.b*255.f+.5f); auto& dst=colours[key]; dst.insert(dst.end(),verts.begin()+i,verts.begin()+i+3); } for(auto& pair:colours) { float c[4]={(float)((pair.first>>16)&255)/255.f,(float)((pair.first>>8)&255)/255.f,(float)(pair.first&255)/255.f,0}; D3D11_MAPPED_SUBRESOURCE cm{}; if(FAILED(g_d3d11Mask.context->Map(g_d3d11Mask.calibrationColorCb,0,D3D11_MAP_WRITE_DISCARD,0,&cm))) continue; memcpy(cm.pData,c,sizeof(c)); g_d3d11Mask.context->Unmap(g_d3d11Mask.calibrationColorCb,0); g_d3d11Mask.context->PSSetConstantBuffers(0,1,&g_d3d11Mask.calibrationColorCb); for(size_t off=0;off<pair.second.size();off+=kMaxVerts) { size_t n=(pair.second.size()-off)<kMaxVerts?(pair.second.size()-off):kMaxVerts; D3D11_MAPPED_SUBRESOURCE m{}; if(SUCCEEDED(g_d3d11Mask.context->Map(g_d3d11Mask.vb,0,D3D11_MAP_WRITE_DISCARD,0,&m))) { memcpy(m.pData,pair.second.data()+off,n*sizeof(VisorVertex)); g_d3d11Mask.context->Unmap(g_d3d11Mask.vb,0); g_d3d11Mask.context->Draw((UINT)n,0); } } } verts.clear(); };
    auto safeQuad = [&](float x0,float y0,float x1,float y1,float r,float g,float b) { if (!quad(x0,y0,x1,y1,r,g,b)) { flush(); quad(x0,y0,x1,y1,r,g,b); } };
    auto calibrationLine = [&](float x0,float y0,float x1,float y1,float thickness,float cr,float cg,float cb) {
        const float dx=x1-x0,dy=y1-y0,len=sqrtf(dx*dx+dy*dy); if(len<.001f)return;
        const float nx=-dy/len*thickness*.5f,ny=dx/len*thickness*.5f;
        if(verts.size()+6>kMaxVerts)flush(); emit(x0+nx,y0+ny,cr,cg,cb);emit(x1+nx,y1+ny,cr,cg,cb);emit(x1-nx,y1-ny,cr,cg,cb);emit(x0+nx,y0+ny,cr,cg,cb);emit(x1-nx,y1-ny,cr,cg,cb);emit(x0-nx,y0-ny,cr,cg,cb);
    };
    const OverlayCoordinateResolver calibrationCoordinates(eye,allViews);
    // Pixel diagnostics use the entire submitted eye rectangle. Cropping may remove available
    // pixels, but must not additionally shrink or inset rulers, probes, gratings, or ladders.
    const int l=eye.rect.offset.x, t=eye.rect.offset.y, w=eye.rect.extent.width, h=eye.rect.extent.height;
    const int r=l+w, b=t+h;
    auto stripeBand = [&](int y, int pitch) { for (int x=l; x<r; x+=pitch) { int x1 = x + pitch < r ? x + pitch : r; float v = ((x-l)/pitch)&1 ? 1.f : 0.f; safeQuad((float)x,(float)y,(float)x1,(float)(y+12),v,v,v); } };
    auto digit = [&](int x, int y, int scale, int value) {
        static const unsigned char segments[10] = {0x3f,0x06,0x5b,0x4f,0x66,0x6d,0x7d,0x07,0x7f,0x6f};
        if (value < 0 || value > 9) return;
        const unsigned char s = segments[value]; const int q = scale * 3;
        auto bar = [&](bool on, int x0, int y0, int x1, int y1) { if (on) safeQuad((float)(x+x0),(float)(y+y0),(float)(x+x1),(float)(y+y1),1,1,1); };
        bar(s&0x01, q,0,2*q,scale); bar(s&0x02, 2*q,scale,2*q+scale,q); bar(s&0x04, 2*q,q+scale,2*q+scale,2*q); bar(s&0x08, q,2*q,2*q,2*q+scale); bar(s&0x10, 0,q+scale,scale,2*q); bar(s&0x20, 0,scale,scale,q); bar(s&0x40, q,q,2*q,q+scale);
    };

    if(includeCalibration) {
    // Pixel ruler and bars live at the bottom; all values are texture-pixel coordinates.
    int bottomCursor=b;
    if (calibrationRuler) { bottomCursor-=14; safeQuad((float)l,(float)bottomCursor,(float)r,(float)b,0,0,0); for(int x=l;x<r;x+=8) { int tall=(x-l)%64==0?14:((x-l)%32==0?10:6); safeQuad((float)x,(float)(b-tall),(float)(x+1),(float)b,1,1,1); } }
    if (calibrationBars) { bottomCursor-=16; const float c[8][3]={{1,1,1},{1,1,0},{0,1,1},{0,1,0},{1,0,1},{1,0,0},{0,0,1},{0,0,0}}; for(int i=0;i<8;++i) safeQuad((float)(l+i*w/8),(float)bottomCursor,(float)(l+(i+1)*w/8),(float)(bottomCursor+8),c[i][0],c[i][1],c[i][2]); for(int i=0;i<16;++i){float v=i/15.f; safeQuad((float)(l+i*w/16),(float)(bottomCursor+8),(float)(l+(i+1)*w/16),(float)(bottomCursor+16),v,v,v);} }
    if (calibrationClippingSteps) { bottomCursor-=16; int black[]={0,2,4,6,8,12,16}, white[]={255,253,251,249,247,243,239}; for(int i=0;i<7;++i){float q=black[i]/255.f; safeQuad((float)(l+i*w/7),(float)bottomCursor,(float)(l+(i+1)*w/7),(float)(bottomCursor+8),q,q,q); q=white[i]/255.f; safeQuad((float)(l+i*w/7),(float)(bottomCursor+8),(float)(l+(i+1)*w/7),(float)(bottomCursor+16),q,q,q);} }
    if (calibrationGratings) for (int row=1;row<=3;++row) { int cy=t+h*row/4-18; stripeBand(cy,1); stripeBand(cy+12,2); stripeBand(cy+24,4); digit(l+4,cy+1,2,1); digit(l+4,cy+13,2,2); digit(l+4,cy+25,2,4); }
    if (calibrationBeacon) { float v=(g_calibrationFrameSerial.load()&1)?1.f:0.f; safeQuad((float)(l+8),(float)(t+8),(float)(l+32),(float)(t+32),v,v,v); }
    if (calibrationMotionStrip) { int y=t+40, travel=w-16 > 1 ? w-16 : 1, marker=l+(int)((g_calibrationFrameSerial.load()*8)%travel); safeQuad((float)l,(float)y,(float)r,(float)(y+8),0,0,0); safeQuad((float)marker,(float)y,(float)(marker+16),(float)(y+8),1,1,1); }
    if (calibrationEdgeProbes) { auto probe=[&](int x,int y,bool vertical){ for(int i=0;i<3;++i){int p=1<<i; for(int q=0;q<48;q+=p) {float v=((q/p)&1)?1.f:0.f; if(vertical) safeQuad((float)(x+q),(float)y,(float)(x+q+p),(float)(y+8),v,v,v); else safeQuad((float)x,(float)(y+q),(float)(x+8),(float)(y+q+p),v,v,v);}}}; probe(l+w/2-24,t,true); probe(l+w/2-24,b-8,true); probe(l,t+h/2-24,false); probe(r-8,t+h/2-24,false); digit(l+w/2-20,t+10,2,1); digit(l+w/2-8,t+10,2,2); digit(l+w/2+4,t+10,2,4); }
    if (calibrationCheckerboards) { int x=l+32,y=t+h/2-80; for(int p: {1,2,4,8}) { for(int yy=0;yy<48;yy+=p) for(int xx=0;xx<48;xx+=p){float v=(((xx/p)+(yy/p))&1)?1.f:0.f; safeQuad((float)(x+xx),(float)(y+yy),(float)(x+xx+p),(float)(y+yy+p),v,v,v);} digit(x+18,y+50,2,p); x+=56; } }
    if (calibrationZonePlate) {
        // A circle is defined in shared tangent space, then projected through each eye. Equal
        // texture-pixel radii are oval whenever horizontal/vertical pixels-per-tangent differ.
        const auto centre=calibrationCoordinates.ResolveSharedTangent(0.f,0.f,true);
        constexpr float pi=3.14159265f; const float radiusTan=(std::min)(calibrationCoordinates.sharedSelectedR-calibrationCoordinates.sharedSelectedL,calibrationCoordinates.sharedSelectedU-calibrationCoordinates.sharedSelectedD)*.16f;
        for(int i=0;i<32;++i){const float a=i*2*pi/32.f,a2=(i+1)*2*pi/32.f,v=(i&1)?1.f:0.f;const auto p1=calibrationCoordinates.ResolveSharedTangent(cosf(a)*radiusTan,sinf(a)*radiusTan,false);const auto p2=calibrationCoordinates.ResolveSharedTangent(cosf(a2)*radiusTan,sinf(a2)*radiusTan,false);if(verts.size()+3>kMaxVerts)flush();emit(centre.first,centre.second,v,v,v);emit(p1.first,p1.second,v,v,v);emit(p2.first,p2.second,v,v,v);}
        for(int q=1;q<=8;++q){const float qr=radiusTan*q/8.f;for(int i=0;i<64;++i){const float a=i*2*pi/64.f,a2=(i+1)*2*pi/64.f;const auto p1=calibrationCoordinates.ResolveSharedTangent(cosf(a)*qr,sinf(a)*qr,false);const auto p2=calibrationCoordinates.ResolveSharedTangent(cosf(a2)*qr,sinf(a2)*qr,false);calibrationLine(p1.first,p1.second,p2.first,p2.second,1.f,1,1,1);}}
    }
    }
    // The HUD draws in BOTH eyes. Every element is anchored at a shared tangent-space point
    // inside the binocular overlap of the cropped per-eye FOVs, mapped through this eye's own
    // tangent bounds into its sub-image pixels. Both eyes therefore see the overlay at the
    // same angular position (zero angular disparity — it fuses cleanly and never hugs the
    // opposite lens edge), and it stays positioned relative to the final cropped tangent
    // bounds at any eye resolution.
    if (includeHudTrace && (((hudEnabled&&OverlayFeatureVisible(OverlayFeatureId::Hud)) && IncludesMirrorFeature(featureMask, MirrorHud)) ||
        ((hudTraceEnabled&&OverlayFeatureVisible(OverlayFeatureId::Trace)) && IncludesMirrorFeature(featureMask, MirrorTrace))) && eye.viewIndex < 2) {
        if (eye.viewIndex == 0 || !g_hudDrawSnap.valid) {
            UpdateHudMetrics();
            for (size_t i = 0; i < kHudWidgetCount; ++i) { g_hudDrawSnap.metrics[i] = g_hudMetrics[i]; g_hudDrawSnap.alarm[i] = g_hudAlarm[i].inAlarm; g_hudDrawSnap.states[i]=g_hudAlarm[i].state; }
            std::lock_guard<std::mutex> lock(g_hudTimingMutex);
            g_hudDrawSnap.budgetMs = g_hudEffectiveBudgetMs;
            g_hudDrawSnap.widgetMask=hudWidgetMask; g_hudDrawSnap.widgetOrder=hudWidgetOrderPacked;
            g_hudDrawSnap.graphChannels=hudGraphChannels; g_hudDrawSnap.graphMode=hudGraphMode;
            g_hudDrawSnap.sampleCount = g_hudFrameHistoryCount;
            for (size_t i = 0; i < g_hudFrameHistoryCount; ++i)
                g_hudDrawSnap.samples[i] = g_hudFrameHistory[(g_hudFrameHistoryStart + i) % g_hudFrameHistory.size()];
            g_hudDrawSnap.valid = true;
        }
        const HudDrawSnapshot& snap = g_hudDrawSnap;
        if (eye.viewIndex == 0) {
            const uint32_t effectiveChannels=viewlab::policy::EffectiveGraphChannels(static_cast<uint32_t>(snap.graphMode),snap.graphChannels);
            const bool vrTrouble=snap.alarm[(size_t)HudWidgetId::Vr] &&
                (effectiveChannels&(GraphFrameInterval|GraphFps|GraphBudgetDeviation|GraphDisplayPeriod))!=0;
            const bool appTrouble=snap.alarm[(size_t)HudWidgetId::App] && (effectiveChannels&GraphAppWork)!=0;
            g_hudTraceVisibilityAlpha=viewlab::policy::UpdateTraceVisibility(g_hudTraceVisibilityState,
                hudTraceVisibilityMode,vrTrouble||appTrouble,GetTickCount64(),
                (uint64_t)std::clamp(hudAlarmHoldMs,0.0,10000.0));
        }
        XrFovf fovSelf = eye.fov, fovOther = eye.fov; bool haveOther = false;
        for (const EyeView& v : allViews) if (v.viewIndex != eye.viewIndex) { fovOther = v.fov; haveOther = true; break; }
        auto fovOk = [](const XrFovf& f) { return f.angleRight > f.angleLeft && f.angleUp > f.angleDown &&
            fabsf(f.angleLeft) < 1.55f && fabsf(f.angleRight) < 1.55f && fabsf(f.angleUp) < 1.55f && fabsf(f.angleDown) < 1.55f; };
        const bool stereoAnchor = fovOk(fovSelf) && (!haveOther || fovOk(fovOther));
        float selfL = 0.f, selfR = 1.f, selfU = 1.f, selfD = 0.f, ovLt = 0.f, ovRt = 1.f, ovUt = 1.f, ovDt = 0.f;
        if (stereoAnchor) {
            selfL = tanf(fovSelf.angleLeft); selfR = tanf(fovSelf.angleRight); selfU = tanf(fovSelf.angleUp); selfD = tanf(fovSelf.angleDown);
            const float oL = tanf(fovOther.angleLeft), oR = tanf(fovOther.angleRight), oU = tanf(fovOther.angleUp), oD = tanf(fovOther.angleDown);
            ovLt = (std::max)(selfL, oL); ovRt = (std::min)(selfR, oR); ovUt = (std::min)(selfU, oU); ovDt = (std::max)(selfD, oD);
            if (!(ovRt > ovLt && ovUt > ovDt)) { ovLt = selfL; ovRt = selfR; ovUt = selfU; ovDt = selfD; }
        }
        auto xFromTan = [&](float tx) { return l + (tx - selfL) / (selfR - selfL) * w; };
        auto yFromTan = [&](float ty) { return t + (selfU - ty) / (selfU - selfD) * h; };
        const OverlayCoordinateResolver coordinates(eye,allViews);
        auto anchorPxX = [&](double frac) { return coordinates.Resolve(OverlayPlacement::FullLens,(float)frac,.5f).first; };
        auto anchorPxY = [&](double frac) { return coordinates.Resolve(OverlayPlacement::FullLens,.5f,(float)frac).second; };
        const float obL=coordinates.tangentValid?coordinates.XFromTan(coordinates.sharedFullL):(float)l;
        const float obR=coordinates.tangentValid?coordinates.XFromTan(coordinates.sharedFullR):(float)r;
        const float obT=coordinates.tangentValid?coordinates.YFromTan(coordinates.sharedFullU):(float)t;
        const float obB=coordinates.tangentValid?coordinates.YFromTan(coordinates.sharedFullD):(float)b;
        // Angular-size contract: one reference pixel is a fixed tangent span (2/1080). Project it
        // independently into this eye. Crop/resolution changes therefore affect clipping and
        // available placement, not apparent HUD size.
        const float minDim = (float)(std::min)(w, h);
        const float fullMinDim=(std::max)(1.f,(std::min)(obR-obL,obB-obT));
        const float pxPerTanX = stereoAnchor ? w / (selfR-selfL) : w*.5f;
        const float pxPerTanY = stereoAnchor ? h / (selfU-selfD) : h*.5f;
        const float angularRefPx = (std::min)(pxPerTanX,pxPerTanY) * (2.f/1080.f);
        const float unit = std::clamp((float)(angularRefPx * 64.f * std::clamp(hudScale, 0.15, 3.0)), 4.f, fullMinDim * 0.45f);
        const float radius = unit * 0.48f;
        const float margin = std::clamp((float)hudSafeMargin, 0.f, .25f) * fullMinDim;
        const int pxs = (std::max)(1, (int)std::lround(unit * 0.045));
        const float numberHeight = (float)(pxs * 7), numberGap = unit * .08f;
        std::array<uint8_t,kHudWidgetCount> drawWidgets{}; size_t drawWidgetCount=0;
        for(uint8_t id:OrderedHudWidgets(snap.widgetOrder)) {
            if((snap.widgetMask&(1ull<<id))==0)continue;
            if(hudAlarmOnly && !snap.alarm[id])continue;
            drawWidgets[drawWidgetCount++]=id;
        }
        const float desiredX = anchorPxX(hudAnchorX) + radius;
        const float desiredY = anchorPxY(hudAnchorY) + radius;
        const auto hudLayout=viewlab::policy::SingleRowHudLayout(drawWidgetCount,radius,unit,(float)hudSpacing);
        const size_t widgetsPerRow=hudLayout.widgetsPerRow,rowCount=hudLayout.rowCount;
        const float gap=hudLayout.gap;
        const float rowStride=radius*2.f+numberGap+numberHeight+gap*.45f;
        const float hudWidth=hudLayout.width;
        const float maxX = obR - margin - (std::max)(0.f,hudWidth-radius);
        const float maxY = obB - margin - radius - numberGap - numberHeight - (rowCount?rowStride*(rowCount-1):0.f);
        const float startX = hudClampToVisible ? (std::clamp)(desiredX, obL + margin + radius, (std::max)(obL + margin + radius, maxX)) : desiredX;
        const float startY = hudClampToVisible ? (std::clamp)(desiredY, obT + margin + radius, (std::max)(obT + margin + radius, maxY)) : desiredY;
        const float intensity = std::clamp((float)hudOpacity, .10f, 1.f);
        // Item 3 (VR frame time) colours against the effective cadence budget; items 0-2
        // keep the percentage thresholds.
        auto hudColour = [&](int item, const HudMetric& m, float& rr, float& gg, float& bb) {
            const HudMetricState state=snap.states[item];
            if (!m.available || state==HudMetricState::Unavailable) { rr=.42f*intensity; gg=.50f*intensity; bb=.56f*intensity; }
            else if(state==HudMetricState::Critical) { rr=1.f*intensity; gg=.16f*intensity; bb=.12f*intensity; }
            else if(state==HudMetricState::Warning) { rr=1.f*intensity; gg=.62f*intensity; bb=.10f*intensity; }
            else if(state==HudMetricState::Reprojection) { rr=.20f*intensity; gg=.78f*intensity; bb=1.f*intensity; }
            else if(state==HudMetricState::Unstable) { rr=1.f*intensity; gg=.20f*intensity; bb=.78f*intensity; }
            else { rr=.18f*intensity; gg=1.f*intensity; bb=.48f*intensity; }
        };
        auto line = [&](float x0,float y0,float x1,float y1,float thickness,float rr,float gg,float bb) {
            const float dx=x1-x0, dy=y1-y0, len=sqrtf(dx*dx+dy*dy);
            if(len<.001f) return; const float nx=-dy/len*thickness*.5f, ny=dx/len*thickness*.5f;
            if(verts.size()+6>kMaxVerts) flush();
            emit(x0+nx,y0+ny,rr,gg,bb); emit(x1+nx,y1+ny,rr,gg,bb); emit(x1-nx,y1-ny,rr,gg,bb);
            emit(x0+nx,y0+ny,rr,gg,bb); emit(x1-nx,y1-ny,rr,gg,bb); emit(x0-nx,y0-ny,rr,gg,bb);
        };
        // Compact 5x7 pixel HUD font with a dedicated decimal-point glyph. Row bits are
        // rendered as merged horizontal runs at an integer pixel scale, so digits stay
        // crisp at small sizes and "13.3" can never collapse into "133".
        static const unsigned char kHudFont[39][7] = {
            {0x0E,0x11,0x13,0x15,0x19,0x11,0x0E}, // 0
            {0x04,0x0C,0x04,0x04,0x04,0x04,0x0E}, // 1
            {0x0E,0x11,0x01,0x02,0x04,0x08,0x1F}, // 2
            {0x1F,0x02,0x04,0x02,0x01,0x11,0x0E}, // 3
            {0x02,0x06,0x0A,0x12,0x1F,0x02,0x02}, // 4
            {0x1F,0x10,0x1E,0x01,0x01,0x11,0x0E}, // 5
            {0x06,0x08,0x10,0x1E,0x11,0x11,0x0E}, // 6
            {0x1F,0x01,0x02,0x04,0x04,0x04,0x04}, // 7
            {0x0E,0x11,0x11,0x0E,0x11,0x11,0x0E}, // 8
            {0x0E,0x11,0x11,0x0F,0x01,0x02,0x0C}, // 9
            {0x00,0x00,0x00,0x00,0x00,0x0C,0x0C}, // .
            {0x00,0x00,0x00,0x0E,0x00,0x00,0x00}, // -
            {0x0E,0x11,0x11,0x1F,0x11,0x11,0x11}, // A
            {0x1E,0x11,0x11,0x1E,0x11,0x11,0x1E}, // B
            {0x0E,0x11,0x10,0x10,0x10,0x11,0x0E}, // C
            {0x1E,0x11,0x11,0x11,0x11,0x11,0x1E}, // D
            {0x1F,0x10,0x10,0x1E,0x10,0x10,0x1F}, // E
            {0x1F,0x10,0x10,0x1E,0x10,0x10,0x10}, // F
            {0x0E,0x11,0x10,0x17,0x11,0x11,0x0F}, // G
            {0x11,0x11,0x11,0x1F,0x11,0x11,0x11}, // H
            {0x0E,0x04,0x04,0x04,0x04,0x04,0x0E}, // I
            {0x07,0x02,0x02,0x02,0x02,0x12,0x0C}, // J
            {0x11,0x12,0x14,0x18,0x14,0x12,0x11}, // K
            {0x10,0x10,0x10,0x10,0x10,0x10,0x1F}, // L
            {0x11,0x1B,0x15,0x15,0x11,0x11,0x11}, // M
            {0x11,0x19,0x15,0x13,0x11,0x11,0x11}, // N
            {0x0E,0x11,0x11,0x11,0x11,0x11,0x0E}, // O
            {0x1E,0x11,0x11,0x1E,0x10,0x10,0x10}, // P
            {0x0E,0x11,0x11,0x11,0x15,0x12,0x0D}, // Q
            {0x1E,0x11,0x11,0x1E,0x14,0x12,0x11}, // R
            {0x0F,0x10,0x10,0x0E,0x01,0x01,0x1E}, // S
            {0x1F,0x04,0x04,0x04,0x04,0x04,0x04}, // T
            {0x11,0x11,0x11,0x11,0x11,0x11,0x0E}, // U
            {0x11,0x11,0x11,0x11,0x11,0x0A,0x04}, // V
            {0x11,0x11,0x11,0x15,0x15,0x15,0x0A}, // W
            {0x11,0x11,0x0A,0x04,0x0A,0x11,0x11}, // X
            {0x11,0x11,0x0A,0x04,0x04,0x04,0x04}, // Y
            {0x1F,0x01,0x02,0x04,0x08,0x10,0x1F}, // Z
            {0x19,0x1A,0x04,0x08,0x16,0x06,0x00}, // %
        };
        auto rectLine=[&](float x0,float y0,float x1,float y1,float thickness,float rr,float gg,float bb){line(x0,y0,x1,y0,thickness,rr,gg,bb);line(x1,y0,x1,y1,thickness,rr,gg,bb);line(x1,y1,x0,y1,thickness,rr,gg,bb);line(x0,y1,x0,y0,thickness,rr,gg,bb);};
        auto circleLine=[&](float cx,float cy,float radius,float thickness,float rr,float gg,float bb){constexpr int segments=20;for(int i=0;i<segments;++i){const float a=6.2831853f*i/segments,b2=6.2831853f*(i+1)/segments;line(cx+cosf(a)*radius,cy+sinf(a)*radius,cx+cosf(b2)*radius,cy+sinf(b2)*radius,thickness,rr,gg,bb);}};
        auto glyphIdx = [](char c) -> int { if (c >= '0' && c <= '9') return c - '0'; if (c == '.') return 10; if (c == '-') return 11; if (c >= 'A' && c <= 'Z') return 12 + c - 'A'; if(c=='%')return 38; return -1; };
        auto glyphAdvance = [&](char c) { return c == '.' ? pxs * 4 : c == ' ' ? pxs * 3 : pxs * 6; };
        auto textWidth = [&](const char* s) { int tw = 0; for (const char* p = s; *p; ++p) tw += glyphAdvance(*p); return (float)(tw - pxs); };
        auto drawText = [&](float centreX, float topY, const char* s, float rr, float gg, float bb) {
            float x = centreX - textWidth(s) * 0.5f;
            for (const char* p = s; *p; ++p) {
                const int gi = glyphIdx(*p);
                if (gi >= 0) {
                    for (int row = 0; row < 7; ++row) {
                        const unsigned char bits = kHudFont[gi][row];
                        int col = 0;
                        while (col < 5) {
                            if (bits & (0x10 >> col)) {
                                const int runStart = col;
                                while (col < 5 && (bits & (0x10 >> col))) ++col;
                                safeQuad(x + runStart * pxs, topY + row * pxs, x + col * pxs, topY + (row + 1) * pxs, rr, gg, bb);
                            } else ++col;
                        }
                    }
                }
                x += glyphAdvance(*p);
            }
        };
        auto ring = [&](float cx,float cy,float fill,float rr,float gg,float bb) {
            constexpr int segments=32; constexpr float pi=3.14159265f;
            for(int s=0;s<segments;++s) { float a=-pi*.5f+s*2*pi/segments, b2=a+2*pi/segments*.78f;
                float fr=s<static_cast<int>(ceilf(fill*segments)) ? rr : .10f*intensity;
                float fg=s<static_cast<int>(ceilf(fill*segments)) ? gg : .12f*intensity;
                float fb=s<static_cast<int>(ceilf(fill*segments)) ? bb : .15f*intensity;
                float ri=radius*.80f; if(verts.size()+6>kMaxVerts) flush();
                emit(cx+cosf(a)*ri,cy+sinf(a)*ri,fr,fg,fb); emit(cx+cosf(a)*radius,cy+sinf(a)*radius,fr,fg,fb); emit(cx+cosf(b2)*radius,cy+sinf(b2)*radius,fr,fg,fb);
                emit(cx+cosf(a)*ri,cy+sinf(a)*ri,fr,fg,fb); emit(cx+cosf(b2)*radius,cy+sinf(b2)*radius,fr,fg,fb); emit(cx+cosf(b2)*ri,cy+sinf(b2)*ri,fr,fg,fb);
            }
        };
        if (hudEnabled&&OverlayFeatureVisible(OverlayFeatureId::Hud) && IncludesMirrorFeature(featureMask, MirrorHud)) for (size_t slot=0; slot<drawWidgetCount; ++slot) {
            const int item=drawWidgets[slot];
            const HudWidgetDescriptor& widget=kHudWidgetRegistry[item];
            const size_t row=slot/widgetsPerRow,col=slot%widgetsPerRow;
            float cx=startX+col*(radius*2+gap), cy=startY+row*rowStride, rr,gg,bb; const HudMetric& metric=snap.metrics[item]; hudColour(item,metric,rr,gg,bb);
            const float fillFrac = !metric.available ? 0.f
                : (item == (int)HudWidgetId::Vr || item == (int)HudWidgetId::FrameInterval) ? (snap.budgetMs > 0.0 ? std::clamp((float)(metric.value / snap.budgetMs), 0.f, 1.f) : 0.f)
                : item == (int)HudWidgetId::Sys ? std::clamp((float)(metric.value/100.0),0.f,1.f)
                : item == (int)HudWidgetId::NetworkPing ? std::clamp((float)(metric.value/networkPingCriticalMs),0.f,1.f)
                : item == (int)HudWidgetId::NetworkJitter ? std::clamp((float)(metric.value/networkJitterCriticalMs),0.f,1.f)
                : item == (int)HudWidgetId::NetworkStatus ? std::clamp((float)(metric.value/2.0),0.f,1.f)
                : std::clamp((float)(metric.value/100.0), 0.f, 1.f);
            ring(cx,cy,fillFrac,rr,gg,bb);
            if((hudWidgetSymbolMask&(1u<<item))==0) drawText(cx,cy-pxs*3.5f,widget.label,rr,gg,bb);
            else {
                const float q=unit*.095f,stroke=(std::max)(1.25f,unit*.022f);
                if(item==0){rectLine(cx-q*2,cy-q*2,cx+q*2,cy+q*2,stroke,rr,gg,bb);rectLine(cx-q*.8f,cy-q*.8f,cx+q*.8f,cy+q*.8f,stroke,rr,gg,bb);for(int k=-1;k<=1;++k){line(cx+q*k,cy-q*2.8f,cx+q*k,cy-q*2,stroke,rr,gg,bb);line(cx+q*k,cy+q*2,cx+q*k,cy+q*2.8f,stroke,rr,gg,bb);line(cx-q*2.8f,cy+q*k,cx-q*2,cy+q*k,stroke,rr,gg,bb);line(cx+q*2,cy+q*k,cx+q*2.8f,cy+q*k,stroke,rr,gg,bb);}}
                else if(item==1){rectLine(cx-q*2.8f,cy-q*1.8f,cx+q*2.4f,cy+q*1.8f,stroke,rr,gg,bb);circleLine(cx-q*.6f,cy,q*.85f,stroke,rr,gg,bb);line(cx+q*.6f,cy-q*.8f,cx+q*1.7f,cy-q*.8f,stroke,rr,gg,bb);line(cx+q*.6f,cy,cx+q*1.7f,cy,stroke,rr,gg,bb);}
                else if(item==2){rectLine(cx-q*1.8f,cy-q*2.7f,cx+q*1.8f,cy+q*2.7f,stroke,rr,gg,bb);line(cx-q*.9f,cy-q*1.5f,cx+q*.9f,cy-q*1.5f,stroke,rr,gg,bb);line(cx-q*.9f,cy-q*.7f,cx+q*.9f,cy-q*.7f,stroke,rr,gg,bb);circleLine(cx,cy+q*1.4f,q*.25f,stroke,rr,gg,bb);}
                else if(item==3){line(cx-q*2.8f,cy-q*.8f,cx-q*2.2f,cy-q*1.8f,stroke,rr,gg,bb);line(cx-q*2.2f,cy-q*1.8f,cx+q*2.2f,cy-q*1.8f,stroke,rr,gg,bb);line(cx+q*2.2f,cy-q*1.8f,cx+q*2.8f,cy-q*.8f,stroke,rr,gg,bb);line(cx+q*2.8f,cy-q*.8f,cx+q*2.1f,cy+q*1.8f,stroke,rr,gg,bb);line(cx+q*2.1f,cy+q*1.8f,cx+q*.7f,cy+q*1.8f,stroke,rr,gg,bb);line(cx+q*.7f,cy+q*1.8f,cx,cy+q*.8f,stroke,rr,gg,bb);line(cx,cy+q*.8f,cx-q*.7f,cy+q*1.8f,stroke,rr,gg,bb);line(cx-q*.7f,cy+q*1.8f,cx-q*2.1f,cy+q*1.8f,stroke,rr,gg,bb);line(cx-q*2.1f,cy+q*1.8f,cx-q*2.8f,cy-q*.8f,stroke,rr,gg,bb);}
                else if(item>=(int)HudWidgetId::NetworkPing){for(int k=0;k<3;++k){const float bh=q*(1.2f+k*.9f);rectLine(cx-q*2.4f+k*q*1.7f,cy+q*2.2f-bh,cx-q*1.5f+k*q*1.7f,cy+q*2.2f,stroke,rr,gg,bb);}}
                else {const int variant=item%4;if(variant==0){line(cx-q*2.2f,cy+q*1.7f,cx-q*.7f,cy-q*.4f,stroke,rr,gg,bb);line(cx-q*.7f,cy-q*.4f,cx+q*.4f,cy+q*.5f,stroke,rr,gg,bb);line(cx+q*.4f,cy+q*.5f,cx+q*2.2f,cy-q*1.7f,stroke,rr,gg,bb);}else if(variant==1){rectLine(cx-q*2.2f,cy-q*2.2f,cx+q*2.2f,cy+q*2.2f,stroke,rr,gg,bb);line(cx-q*1.4f,cy,cx+q*1.4f,cy,stroke,rr,gg,bb);}else if(variant==2){for(int k=-2;k<=2;++k)line(cx+q*k,cy+q*2,cx+q*k,cy-q*(.5f+abs(k)*.6f),stroke,rr,gg,bb);}else{circleLine(cx,cy,q*2.2f,stroke,rr,gg,bb);line(cx,cy,cx+q*1.5f,cy-q*1.2f,stroke,rr,gg,bb);}}
            }
            char text[24];
            if (!metric.available) { text[0]='-'; text[1]='-'; text[2]='\0'; }
            else if(item==(int)HudWidgetId::NetworkStatus) {const char* status=metric.value>=2?"OFF":metric.value>=1?"BAD":"OK";strcpy_s(text,status);}
            else { const bool showUnit=(hudWidgetUnitHiddenMask&(1u<<item))==0; const char* u=showUnit?widget.unit:"";
                if (widget.decimals == 1) snprintf(text, sizeof(text), showUnit?"%.1f %s":"%.1f%s", metric.value, u);
                else snprintf(text, sizeof(text), showUnit?"%d %s":"%d%s", std::clamp(static_cast<int>(std::round(metric.value)),0,9999), u); }
            for(char* p=text;*p;++p) if(*p>='a'&&*p<='z') *p=(char)(*p-'a'+'A');
            drawText(cx, cy + radius + numberGap, text, rr, gg, bb);
        }
        // Modular performance graph. Each mode admits only compatible units; selected channels
        // remain persisted when switching modes but incompatible lines are not drawn.
        if (hudTraceEnabled&&OverlayFeatureVisible(OverlayFeatureId::Trace) && IncludesMirrorFeature(featureMask, MirrorTrace) && g_hudTraceVisibilityAlpha > .001f) {
        const float traceIntensity=std::clamp((float)hudTraceOpacity,.10f,1.f)*g_hudTraceVisibilityAlpha;
        const size_t historyN = (size_t)std::clamp(hudTraceHistory, 10.0, (double)snap.samples.size());
		const float traceScale = std::clamp((float)hudTraceScale, 0.25f, 3.0f);
		const float traceAvailableW = (std::max)(32.f, obR - obL - 2.f * margin);
		const float traceW = (std::min)(traceAvailableW, std::clamp((float)hudTraceWidth, 0.10f, 1.0f) * traceAvailableW * traceScale);
		const float traceUnit = std::clamp(angularRefPx * 64.f, 4.f, fullMinDim * .45f);
		const float traceH = traceUnit * .55f * traceScale;
        float traceLeft = anchorPxX(hudTraceX);
        float traceTop = anchorPxY(hudTraceY);
        if (hudClampToVisible) {
            traceLeft = std::clamp(traceLeft, obL + margin, (std::max)(obL + margin, obR - margin - traceW));
            traceTop = std::clamp(traceTop, obT + margin, (std::max)(obT + margin, obB - margin - traceH));
        }
        const float traceRight = traceLeft + traceW, traceBottom=traceTop+traceH, traceCentre = traceTop + traceH * .5f;
        const bool deviationMode=snap.graphMode==HudGraphMode::Deviation;
        // Item 17: the dark base/centre reference line is removed in every mode (user-reported clutter).
        // The coloured data channels, budget-relative scaling and cyan event markers below are unaffected.
        (void)traceCentre;
        const size_t visible = (std::min)(snap.sampleCount, historyN);
        if (visible > 1) {
            const float sensitivity=(float)(std::max)(0.25,hudTraceSensitivityMs), dx=traceW/(float)(historyN-1);
            const float firstX=traceRight-dx*(visible-1);
            const size_t base = snap.sampleCount - visible;
            struct GraphStyle { uint32_t bit; float r,g,b; };
            constexpr GraphStyle styles[]={
                {GraphFrameInterval,.20f,.85f,1.f},{GraphFps,.32f,1.f,.46f},
                {GraphBudgetDeviation,1.f,.56f,.12f},{GraphAppWork,.78f,.38f,1.f},
                {GraphWaitDuration,.98f,.88f,.25f},{GraphSubmitDuration,1.f,.28f,.48f},
                {GraphDisplayPeriod,.72f,.76f,.82f}};
            const uint32_t effectiveChannels=viewlab::policy::EffectiveGraphChannels(static_cast<uint32_t>(snap.graphMode),snap.graphChannels);
            auto value=[&](const HudFrameSample&s,uint32_t bit){
                if(snap.graphMode==HudGraphMode::Fps)return s.actualMs>0.0?1000.0/s.actualMs:0.0;
                if(snap.graphMode==HudGraphMode::Deviation)return s.deviationMs;
                double raw=bit==GraphFrameInterval?s.actualMs:bit==GraphAppWork?s.appWorkMs:
                    bit==GraphWaitDuration?s.waitDurationMs:bit==GraphSubmitDuration?s.submitDurationMs:s.displayPeriodMs;
                if(snap.graphMode==HudGraphMode::BudgetPercent)return s.targetMs>0.0?raw/s.targetMs*100.0:0.0;
                return raw;
            };
            float observedMax=0.f;
            for(const GraphStyle& style:styles) if((effectiveChannels&style.bit)!=0) for(size_t i=0;i<visible;++i) {
                const double candidate=value(snap.samples[base+i],style.bit);
                if(std::isfinite(candidate)&&candidate>observedMax)observedMax=(float)candidate;
            }
            const float absoluteMax=snap.graphMode==HudGraphMode::Fps?(std::max)(60.f,observedMax*1.10f):
                snap.graphMode==HudGraphMode::BudgetPercent?(std::max)(120.f,observedMax*1.10f):
                (std::max)((float)(std::max)(sensitivity*2.0,snap.budgetMs*1.25),observedMax*1.10f);
            auto yFor=[&](double v){
                if(deviationMode)return traceCentre-std::clamp((float)(v/sensitivity),-1.f,1.f)*traceH*.5f;
                return traceBottom-std::clamp((float)(v/(std::max)(1.f,absoluteMax)),0.f,1.f)*traceH;
            };
            for(const GraphStyle& style:styles) {
                if((effectiveChannels&style.bit)==0)continue;
                const bool responsible=(snap.alarm[(size_t)HudWidgetId::Vr]&&(style.bit==GraphFrameInterval||style.bit==GraphFps||style.bit==GraphBudgetDeviation||style.bit==GraphDisplayPeriod))||
                    (snap.alarm[(size_t)HudWidgetId::App]&&style.bit==GraphAppWork);
                for(size_t i=1;i<visible;++i){
                    const HudFrameSample&s0=snap.samples[base+i-1]; const HudFrameSample&s1=snap.samples[base+i];
                    line(firstX+dx*(i-1),yFor(value(s0,style.bit)),firstX+dx*i,yFor(value(s1,style.bit)),
                        (std::max)(1.2f,unit*.018f)*(responsible?1.55f:1.f),style.r*traceIntensity,style.g*traceIntensity,style.b*traceIntensity);
                }
            }
            for(size_t i=0;i<visible;++i)if(snap.samples[base+i].markerNumber!=0){
                const float markerX=firstX+dx*i;
                line(markerX,traceTop,markerX,traceBottom,(std::max)(1.5f,unit*.022f),.15f*traceIntensity,1.f*traceIntensity,.92f*traceIntensity);
                char label[16]{};snprintf(label,sizeof(label),"M%u",snap.samples[base+i].markerNumber);
                drawText(markerX,traceTop-numberHeight-unit*.05f,label,.15f*traceIntensity,1.f*traceIntensity,.92f*traceIntensity);
            }
        }
        }
    }
    flush();
    g_d3d11Mask.context->OMSetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT,sRTVs,sDSV); if(sVPCount) g_d3d11Mask.context->RSSetViewports(sVPCount,sVPs); if(sScissorCount) g_d3d11Mask.context->RSSetScissorRects(sScissorCount,sScissors); g_d3d11Mask.context->RSSetState(sRS); g_d3d11Mask.context->OMSetBlendState(sBS,sBF,sSM); g_d3d11Mask.context->OMSetDepthStencilState(sDSS,sSRef); g_d3d11Mask.context->VSSetShader(sVS,nullptr,0); g_d3d11Mask.context->PSSetShader(sPS,nullptr,0); g_d3d11Mask.context->PSSetConstantBuffers(0,1,&sPSCB); g_d3d11Mask.context->IASetInputLayout(sLayout); g_d3d11Mask.context->IASetPrimitiveTopology(sTopo); g_d3d11Mask.context->IASetVertexBuffers(0,1,&sVB,&sStride,&sOff);
    if(ownsRtv) rtv->Release(); for(auto p:sRTVs) if(p) p->Release(); if(sDSV)sDSV->Release(); if(sRS)sRS->Release(); if(sBS)sBS->Release(); if(sDSS)sDSS->Release(); if(sVS)sVS->Release(); if(sPS)sPS->Release(); if(sPSCB)sPSCB->Release(); if(sLayout)sLayout->Release(); if(sVB)sVB->Release(); g_d3d11Mask.context->Flush();
}

// Ensure a per-slot dynamic RGBA texture exists and holds the current card pixels. Returns the
// SRV to sample, or nullptr if the card has no drawable image or creation failed. Upload happens
// only when the card's id or contentSerial changed, so steady-state frames do no texture work.
ID3D11ShaderResourceView* EnsureNotifyCardTexture(uint32_t slot, const NotifyCardBlock& card) {
    if (slot >= kNotifyMaxCards || !g_d3d11Mask.device || !g_d3d11Mask.context) return nullptr;
    if (card.width == 0 || card.height == 0 || card.width > kNotifyCardW || card.height > kNotifyCardH) return nullptr;
    if (!g_d3d11Mask.notifyTex[slot]) {
        D3D11_TEXTURE2D_DESC td{}; td.Width = kNotifyCardW; td.Height = kNotifyCardH; td.MipLevels = 1; td.ArraySize = 1;
        td.Format = DXGI_FORMAT_R8G8B8A8_UNORM; td.SampleDesc.Count = 1; td.Usage = D3D11_USAGE_DYNAMIC;
        td.BindFlags = D3D11_BIND_SHADER_RESOURCE; td.CPUAccessFlags = D3D11_CPU_ACCESS_WRITE;
        if (FAILED(g_d3d11Mask.device->CreateTexture2D(&td, nullptr, &g_d3d11Mask.notifyTex[slot]))) { g_d3d11Mask.notifyTex[slot] = nullptr; return nullptr; }
        if (FAILED(g_d3d11Mask.device->CreateShaderResourceView(g_d3d11Mask.notifyTex[slot], nullptr, &g_d3d11Mask.notifySrv[slot]))) {
            g_d3d11Mask.notifyTex[slot]->Release(); g_d3d11Mask.notifyTex[slot] = nullptr; return nullptr;
        }
        g_d3d11Mask.notifyTexId[slot] = 0xFFFFFFFFu; // force upload
    }
    if (g_d3d11Mask.notifyTexId[slot] != card.id || g_d3d11Mask.notifyTexSerial[slot] != card.contentSerial) {
        D3D11_MAPPED_SUBRESOURCE m{};
        if (SUCCEEDED(g_d3d11Mask.context->Map(g_d3d11Mask.notifyTex[slot], 0, D3D11_MAP_WRITE_DISCARD, 0, &m))) {
            const uint8_t* src = card.rgba; auto* dst = static_cast<uint8_t*>(m.pData);
            for (uint32_t y = 0; y < card.height; ++y)
                memcpy(dst + (size_t)y * m.RowPitch, src + (size_t)y * kNotifyCardW * 4, (size_t)card.width * 4);
            g_d3d11Mask.context->Unmap(g_d3d11Mask.notifyTex[slot], 0);
            g_d3d11Mask.notifyTexId[slot] = card.id; g_d3d11Mask.notifyTexSerial[slot] = card.contentSerial;
        }
    }
    return g_d3d11Mask.notifySrv[slot];
}

// Feature 1 (render-boundary flash), 2 (crosshair), and 3 (notification cards). These share one
// alpha-blended pass and are anchored, like the HUD, in the shared tangent-space binocular overlap
// so they fuse cleanly in both eyes. Uses the visor colour+alpha PS for flat geometry and the
// textured PS for pre-composited notification cards.
void DrawViewLabOverlaysToTexture(
    ID3D11Texture2D* tex, uint32_t arrSize, int64_t scFormat,
    const EyeView& eye, const std::vector<EyeView>& allViews, ID3D11RenderTargetView* cachedRtv,
    uint32_t featureMask = kAllMirrorFeatures) {
    if (!g_d3d11Mask.initialized || !g_d3d11Mask.context || !tex ||
        eye.rect.extent.width <= 0 || eye.rect.extent.height <= 0 || eye.viewIndex > 1) return;

    // Boundary fade envelope.
    float boundaryAlpha = 0.f;
    if (g_boundaryDragActive) boundaryAlpha = 1.f;
    else if (g_boundaryReleaseTick != 0) {
        const uint64_t elapsed = GetTickCount64() - g_boundaryReleaseTick;
        if (elapsed >= kBoundaryFadeMs) { g_boundaryReleaseTick = 0; boundaryAlpha = 0.f; }
        else boundaryAlpha = 1.f - (float)elapsed / (float)kBoundaryFadeMs;
    }
    const bool wantBoundary = IncludesMirrorFeature(featureMask, MirrorBoundaryFlash) && boundaryAlpha > 0.001f;
    const bool wantClock = IncludesMirrorFeature(featureMask, MirrorClock) && clockWidgetEnabled&&OverlayFeatureVisible(OverlayFeatureId::Clock) && clockWidgetOpacity > 0.001;
    const bool wantSticky=IncludesMirrorFeature(featureMask, MirrorStickyNotes) && stickyNoteEnabled&&OverlayFeatureVisible(OverlayFeatureId::StickyNote)&&AnyStickyNote();
    const bool wantObs=IncludesMirrorFeature(featureMask, MirrorRecordingCue) && ObsRecordingActive();
    const uint64_t markerUntil=g_traceMarkerConfirmationUntil.load(std::memory_order_acquire);
    const uint32_t markerNumber=g_traceMarkerConfirmation.load(std::memory_order_acquire);
    const bool wantTraceMarker=IncludesMirrorFeature(featureMask, MirrorTrace) && markerNumber!=0 && markerUntil>GetTickCount64();
    const bool wantCrosshair = IncludesMirrorFeature(featureMask, MirrorCrosshair) && crosshairEnabled&&OverlayFeatureVisible(OverlayFeatureId::Crosshair) && crosshairAlpha > 0.001f;
    ConsumeRacingState();
    const bool wantSpotter = IncludesMirrorFeature(featureMask, MirrorRacingCues) && ((iracingEnabled && iracingSpotterGlow) || (g_racingStable.presentationFlags & 1u)) && g_racingStable.spotterState != 0;
    const bool wantFlag = IncludesMirrorFeature(featureMask, MirrorRacingCues) && ((iracingEnabled && iracingFlagBorder) || (g_racingStable.presentationFlags & 2u)) && g_racingStable.flagState != 0 && g_racingStable.flagColor != 0;
    // Race-start border light: reserved0 carries the latched phase (1 waiting/red, 2 started/green).
    const bool wantRaceStart = IncludesMirrorFeature(featureMask, MirrorRacingCues) && ((iracingEnabled && iracingRaceStart) || (g_racingStable.presentationFlags & 8u)) && g_racingStable.reserved0 != 0;
    // Rear-closing pressure cue: packed state in reserved1 (bit0 active, opacity<<8, width<<16, intensity<<24).
    const bool wantRearClosing = IncludesMirrorFeature(featureMask, MirrorRacingCues) && ((iracingEnabled && iracingRearClosing) || (g_racingStable.presentationFlags & 16u)) && (g_racingStable.reserved1 & 1u) != 0;
    // Grip-O-Bar: packed gripState (bit0 active, dominance<<1, direction<<3 [1 left/2 right], severity<<8).
    const bool wantGripBar = IncludesMirrorFeature(featureMask, MirrorRacingCues) && ((iracingEnabled && iracingGripBar) || (g_racingStable.presentationFlags & 32u)) && (g_racingStable.gripState & 1u) != 0;
    bool wantNotify = false;
    const bool wantsNotificationCards = IncludesMirrorFeature(featureMask, MirrorNotifications) && notifyEnabled&&OverlayFeatureVisible(OverlayFeatureId::Notifications);
    const bool wantsRacingLap = IncludesMirrorFeature(featureMask, MirrorRacingCues) && ((iracingEnabled && iracingLapPopup) || (g_racingStable.presentationFlags & 4u));
    if ((wantsNotificationCards || wantsRacingLap) && g_d3d11Mask.texturedPs) { ConnectNotify(); if (g_notify && g_notify->magic == kNotifyMagic && g_notify->version == 3) wantNotify = true; }
    if (!wantBoundary && !wantClock && !wantSticky && !wantObs && !wantTraceMarker && !wantCrosshair && !wantNotify && !wantSpotter && !wantFlag) return;

    D3D11_TEXTURE2D_DESC texDesc{}; tex->GetDesc(&texDesc);
    if (texDesc.Width == 0 || texDesc.Height == 0) return;

    DXGI_FORMAT rtvFormat = GetNonSRGBFormat(static_cast<DXGI_FORMAT>(scFormat));
    if (rtvFormat == DXGI_FORMAT_UNKNOWN) rtvFormat = GetNonSRGBFormat(texDesc.Format);
    D3D11_RENDER_TARGET_VIEW_DESC rtvDesc{}; rtvDesc.Format = rtvFormat;
    if (arrSize > 1) { rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2DARRAY; rtvDesc.Texture2DArray.FirstArraySlice = eye.arraySlice; rtvDesc.Texture2DArray.ArraySize = 1; }
    else rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2D;
    const bool ownsRtv = cachedRtv == nullptr; ID3D11RenderTargetView* rtv = cachedRtv;
    if (!rtv && (FAILED(g_d3d11Mask.device->CreateRenderTargetView(tex, &rtvDesc, &rtv)) || !rtv)) return;

    // Full pipeline-state save.
    ID3D11RenderTargetView* sRTVs[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT]{}; ID3D11DepthStencilView* sDSV=nullptr;
    g_d3d11Mask.context->OMGetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT, sRTVs, &sDSV);
    UINT sVPCount=D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE; D3D11_VIEWPORT sVPs[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{}; g_d3d11Mask.context->RSGetViewports(&sVPCount, sVPs);
    UINT sScissorCount=D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE; D3D11_RECT sScissors[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{}; g_d3d11Mask.context->RSGetScissorRects(&sScissorCount, sScissors);
    ID3D11RasterizerState* sRS=nullptr; g_d3d11Mask.context->RSGetState(&sRS); ID3D11BlendState* sBS=nullptr; FLOAT sBF[4]{}; UINT sSM=0; g_d3d11Mask.context->OMGetBlendState(&sBS,sBF,&sSM);
    ID3D11DepthStencilState* sDSS=nullptr; UINT sSRef=0; g_d3d11Mask.context->OMGetDepthStencilState(&sDSS,&sSRef);
    ID3D11VertexShader* sVS=nullptr; g_d3d11Mask.context->VSGetShader(&sVS,nullptr,nullptr); ID3D11PixelShader* sPS=nullptr; g_d3d11Mask.context->PSGetShader(&sPS,nullptr,nullptr);
    ID3D11Buffer* sPSCB=nullptr; g_d3d11Mask.context->PSGetConstantBuffers(0,1,&sPSCB); ID3D11ShaderResourceView* sSRV=nullptr; g_d3d11Mask.context->PSGetShaderResources(0,1,&sSRV); ID3D11SamplerState* sSmp=nullptr; g_d3d11Mask.context->PSGetSamplers(0,1,&sSmp);
    ID3D11InputLayout* sLayout=nullptr; g_d3d11Mask.context->IAGetInputLayout(&sLayout); D3D11_PRIMITIVE_TOPOLOGY sTopo; g_d3d11Mask.context->IAGetPrimitiveTopology(&sTopo); ID3D11Buffer* sVB=nullptr; UINT sStride=0,sOff=0; g_d3d11Mask.context->IAGetVertexBuffers(0,1,&sVB,&sStride,&sOff);

    const int l=eye.rect.offset.x, t=eye.rect.offset.y, w=eye.rect.extent.width, h=eye.rect.extent.height, rr_=l+w, bb_=t+h;
    D3D11_VIEWPORT vp{}; vp.TopLeftX=(float)l; vp.TopLeftY=(float)t; vp.Width=(float)w; vp.Height=(float)h; vp.MaxDepth=1.f;
    D3D11_RECT scissor{l,t,rr_,bb_}; static const FLOAT kBF[]={0,0,0,0}; UINT stride=sizeof(VisorVertex), offset=0;
    g_d3d11Mask.context->OMSetRenderTargets(1,&rtv,nullptr); g_d3d11Mask.context->RSSetViewports(1,&vp); g_d3d11Mask.context->RSSetScissorRects(1,&scissor);
    g_d3d11Mask.context->RSSetState(g_d3d11Mask.calibrationRs); g_d3d11Mask.context->OMSetBlendState(g_d3d11Mask.bs,kBF,0xFFFFFFFF); g_d3d11Mask.context->OMSetDepthStencilState(g_d3d11Mask.dss,0);
    g_d3d11Mask.context->IASetInputLayout(g_d3d11Mask.layout); g_d3d11Mask.context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST); g_d3d11Mask.context->IASetVertexBuffers(0,1,&g_d3d11Mask.vb,&stride,&offset);

    // Tangent-space anchor frame for this eye (identical model to the HUD).
    XrFovf fSelf=eye.fov, fOther=eye.fov; bool haveOther=false;
    for (const EyeView& v : allViews) if (v.viewIndex != eye.viewIndex) { fOther=v.fov; haveOther=true; break; }
    auto fovOk=[](const XrFovf& f){ return f.angleRight>f.angleLeft && f.angleUp>f.angleDown && fabsf(f.angleLeft)<1.55f && fabsf(f.angleRight)<1.55f && fabsf(f.angleUp)<1.55f && fabsf(f.angleDown)<1.55f; };
    const bool stereo = fovOk(fSelf) && (!haveOther || fovOk(fOther));
    float sL=0,sR=1,sU=1,sD=0, oL=0,oR=1,oU=1,oD=0;
    if (stereo) {
        sL=tanf(fSelf.angleLeft); sR=tanf(fSelf.angleRight); sU=tanf(fSelf.angleUp); sD=tanf(fSelf.angleDown);
        const float xL=tanf(fOther.angleLeft), xR=tanf(fOther.angleRight), xU=tanf(fOther.angleUp), xD=tanf(fOther.angleDown);
        oL=(std::max)(sL,xL); oR=(std::min)(sR,xR); oU=(std::min)(sU,xU); oD=(std::max)(sD,xD);
        if (!(oR>oL && oU>oD)) { oL=sL; oR=sR; oU=sU; oD=sD; }
    }
    auto xFromTan=[&](float tx){ return l + (tx - sL)/(sR - sL) * w; };
    auto yFromTan=[&](float ty){ return t + (sU - ty)/(sU - sD) * h; };
    const OverlayCoordinateResolver coordinates(eye,allViews);
    auto anchorX=[&](double f){ return coordinates.Resolve(OverlayPlacement::FullLens,(float)f,.5f).first; };
    auto anchorY=[&](double f){ return coordinates.Resolve(OverlayPlacement::FullLens,.5f,(float)f).second; };
    const float fullLeft=coordinates.tangentValid?coordinates.XFromTan(coordinates.sharedFullL):(float)l;
    const float fullRight=coordinates.tangentValid?coordinates.XFromTan(coordinates.sharedFullR):(float)rr_;
    const float fullTop=coordinates.tangentValid?coordinates.YFromTan(coordinates.sharedFullU):(float)t;
    const float fullBottom=coordinates.tangentValid?coordinates.YFromTan(coordinates.sharedFullD):(float)bb_;
    // Fused-overlay contract: tangent (0,0) is the shared straight-ahead angular point. It must be
    // projected separately through each asymmetric eye FOV; equal normalized eye pixels create
    // unintended disparity and the two-crosshair failure.
    const float crosshairTanX=(float)crosshairOffsetX*(coordinates.sharedFullR-coordinates.sharedFullL)*.5f;
    const float crosshairTanY=-(float)crosshairOffsetY*(coordinates.sharedFullU-coordinates.sharedFullD)*.5f;
    const auto crosshairPosition = coordinates.ResolveSharedTangent(crosshairTanX,crosshairTanY,false);
    const float centreX = crosshairPosition.first;
    const float centreY = crosshairPosition.second;
    const float minDim = (float)(std::min)(w,h);

    // Flat colour+alpha geometry (boundary + crosshair). Snap to whole pixels for crisp edges.
    std::vector<VisorVertex> verts; verts.reserve(2048);
    auto ndcX=[&](float px){ return 2.f*(px-l)/w - 1.f; };
    auto ndcY=[&](float py){ return 1.f - 2.f*(py-t)/h; };
    auto emit=[&](float px,float py,float cr,float cg,float cb,float a){ VisorVertex v{}; v.x=ndcX(px); v.y=ndcY(py); v.r=cr; v.g=cg; v.b=cb; v.alpha=a; verts.push_back(v); };
    auto rectFill=[&](float x0,float y0,float x1,float y1,float cr,float cg,float cb,float a){
        x0=floorf(x0+.5f); y0=floorf(y0+.5f); x1=floorf(x1+.5f); y1=floorf(y1+.5f);
        if (x1<=x0||y1<=y0) return;
        emit(x0,y0,cr,cg,cb,a); emit(x1,y0,cr,cg,cb,a); emit(x1,y1,cr,cg,cb,a);
        emit(x0,y0,cr,cg,cb,a); emit(x1,y1,cr,cg,cb,a); emit(x0,y1,cr,cg,cb,a);
    };
    auto flushFlat=[&](float cr,float cg,float cb,float alpha){
        if (verts.empty()) return;
        float color[4]={cr,cg,cb,alpha}; D3D11_MAPPED_SUBRESOURCE cm{};
        if (FAILED(g_d3d11Mask.context->Map(g_d3d11Mask.calibrationColorCb,0,D3D11_MAP_WRITE_DISCARD,0,&cm))) { verts.clear(); return; }
        memcpy(cm.pData,color,sizeof(color)); g_d3d11Mask.context->Unmap(g_d3d11Mask.calibrationColorCb,0);
        g_d3d11Mask.context->VSSetShader(g_d3d11Mask.vs,nullptr,0); g_d3d11Mask.context->PSSetShader(g_d3d11Mask.overlayPs,nullptr,0);
        g_d3d11Mask.context->PSSetConstantBuffers(0,1,&g_d3d11Mask.calibrationColorCb);
        for (size_t off=0; off<verts.size(); off+=4096) {
            const size_t n=(std::min)(verts.size()-off, size_t{4096});
            D3D11_MAPPED_SUBRESOURCE m{};
            if (SUCCEEDED(g_d3d11Mask.context->Map(g_d3d11Mask.vb,0,D3D11_MAP_WRITE_DISCARD,0,&m))) {
                memcpy(m.pData, verts.data()+off, n*sizeof(VisorVertex)); g_d3d11Mask.context->Unmap(g_d3d11Mask.vb,0);
                g_d3d11Mask.context->Draw((UINT)n,0);
            }
        }
        verts.clear();
    };

    // Racing flag border is a restrained inner outline. It is static for an unchanged generic
    // flag state, so telemetry ticks cannot replay an animation or create an event storm.
    if (wantFlag) {
        const float cr=((g_racingStable.flagColor>>16)&255)/255.f,cg=((g_racingStable.flagColor>>8)&255)/255.f,cb=(g_racingStable.flagColor&255)/255.f;
        const float a=(float)iracingFlagOpacity,th=viewlab::racing::FlagBorderThickness(iracingFlagWidth,minDim),inset=2.f;
        rectFill(l+inset,t+inset,rr_-inset,t+inset+th,cr,cg,cb,a); rectFill(l+inset,bb_-inset-th,rr_-inset,bb_-inset,cr,cg,cb,a);
        rectFill(l+inset,t+inset,l+inset+th,bb_-inset,cr,cg,cb,a); rectFill(rr_-inset-th,t+inset,rr_-inset,bb_-inset,cr,cg,cb,a);
        flushFlat(cr,cg,cb,a);
    }

    // Race-start border light. The provider latches a phase into reserved0; native owns ONLY the
    // green hold+fade envelope so telemetry ticks cannot replay it. Phase 1 = waiting (red), phase 2 =
    // started (green, held then faded once). The green-seen tick is derived from the raw latched phase
    // so enabling/disabling mid-start does not restart the envelope arbitrarily.
    {
        static uint32_t s_prevRacePhase = 0; static uint64_t s_raceGreenTick = 0;
        const uint32_t rawPhase = g_racingStable.reserved0;
        if (rawPhase != 2) s_raceGreenTick = 0;
        else if (s_prevRacePhase != 2) s_raceGreenTick = GetTickCount64();
        s_prevRacePhase = rawPhase;
        if (wantRaceStart) {
            const uint32_t phase = rawPhase;
            float rsA = 0.f, rR = 0.f, rG = 0.f, rB = 0.f;
            if (phase == 1) { rsA = (float)std::clamp(iracingRaceStartRedOpacity, 0.05, 1.0); rR = 0.85f; rG = 0.05f; rB = 0.05f; }
            else if (phase == 2) {
                const double held = (double)(GetTickCount64() - s_raceGreenTick);
                const double holdMs = std::clamp(iracingRaceStartGreenMs, 250.0, 15000.0), fadeMs = 600.0;
                const float env = held <= holdMs ? 1.f : held >= holdMs + fadeMs ? 0.f : (float)(1.0 - (held - holdMs) / fadeMs);
                rsA = (float)std::clamp(iracingRaceStartGreenOpacity, 0.05, 1.0) * env; rR = 0.05f; rG = 0.85f; rB = 0.10f;
            }
            if (rsA > 0.001f) {
                const float th = std::clamp((float)iracingRaceStartThickness * minDim, 3.f, minDim * .12f), inset = 2.f;
                rectFill(l+inset, t+inset, rr_-inset, t+inset+th, rR, rG, rB, rsA); rectFill(l+inset, bb_-inset-th, rr_-inset, bb_-inset, rR, rG, rB, rsA);
                rectFill(l+inset, t+inset, l+inset+th, bb_-inset, rR, rG, rB, rsA); rectFill(rr_-inset-th, t+inset, rr_-inset, bb_-inset, rR, rG, rB, rsA);
                flushFlat(rR, rG, rB, rsA);
            }
        }
    }

    // Rear-closing pressure cue: a restrained top-centre glow. Width grows as the car behind gets
    // closer; brightness grows with closing speed. No left/right is inferred; the spotter takes over on
    // overlap (the provider stops publishing an active state once a side is reported).
    if (wantRearClosing) {
        const uint32_t rc = g_racingStable.reserved1;
        const float op = ((rc >> 8) & 255) / 255.f, wd = ((rc >> 16) & 255) / 255.f, inten = ((rc >> 24) & 255) / 255.f;
        const float alpha = op * (0.35f + 0.65f * inten) * (float)iracingRearClosingOpacity;
        const float halfW = viewlab::racing::RearGlowHalfWidth(wd, w);
        const float cx = (l + rr_) * 0.5f, band = std::clamp(minDim * 0.05f, 4.f, minDim * 0.14f);
        for (int i = 0; i < 4; ++i) {          // stacked bands fading downward from the top edge
            const float f = 1.f - i / 4.f, a = alpha * f, hw = halfW * (0.7f + 0.3f * f);
            rectFill(cx - hw, (float)t + i * band * 0.25f, cx + hw, (float)t + (i + 1) * band * 0.25f, 1.f, 0.35f, 0.15f, a);
            flushFlat(1.f, 0.35f, 0.15f, a);
        }
    }

    // Grip-O-Bar: a lower-left or lower-right peripheral bar for whole-car grip loss. Colour bands by
    // severity (yellow -> orange -> red); coverage and intensity grow with severity. Never a permanent
    // full-width bottom bar and never an individual-tyre claim — the provider reports whole-car direction.
    if (wantGripBar) {
        const uint32_t gp = g_racingStable.gripState;
        const int dir = (gp >> 3) & 3; const float sev = ((gp >> 8) & 255) / 255.f;
        float cr, cg, cb;
        switch (viewlab::racing::GripSeverityBand(sev)) {
            case 0: cr = 1.f; cg = 0.85f; cb = 0.10f; break;          // yellow
            case 1: cr = 1.f; cg = 0.50f; cb = 0.05f; break;          // orange
            default: cr = 1.f; cg = 0.15f; cb = 0.10f; break;         // red
        }
        const float alpha = (0.30f + 0.70f * sev) * (float)iracingGripBarOpacity;
        const float barW = viewlab::racing::GripBarWidth(sev, w), barH = std::clamp(minDim * 0.06f, 6.f, minDim * 0.16f);
        const float y0 = (float)bb_ - barH - 2.f;
        for (int i = 0; i < 4; ++i) {          // stacked bands fading toward the inside edge
            const float f = 1.f - i / 4.f, a = alpha * f;
            const float yy0 = y0 + barH * i / 4.f, yy1 = y0 + barH * (i + 1) / 4.f;
            if (dir == 1) { rectFill((float)l + 2.f, yy0, (float)l + 2.f + barW * f, yy1, cr, cg, cb, a); flushFlat(cr, cg, cb, a); }
            else if (dir == 2) { rectFill((float)rr_ - 2.f - barW * f, yy0, (float)rr_ - 2.f, yy1, cr, cg, cb, a); flushFlat(cr, cg, cb, a); }
        }
    }

    // Spotter is spatial rather than textual: maximum intensity at the relevant peripheral edge,
    // fading inward in eight bounded bands. Both-sides remains two simultaneous independent cues.
    if (wantSpotter) {
        const uint32_t state=g_racingStable.spotterState;
        const bool left=state==1||state==3||state==4,right=state==2||state==3||state==5;
        const float cr=((iracingSpotterColor>>16)&255)/255.f,cg=((iracingSpotterColor>>8)&255)/255.f,cb=(iracingSpotterColor&255)/255.f;
        const float width=viewlab::racing::SpotterWidthPx(iracingSpotterWidth,w),step=width/(float)viewlab::racing::kSpotterBands,base=viewlab::racing::SpotterBase(iracingSpotterOpacity,iracingSpotterStrength);
        for(int i=0;i<viewlab::racing::kSpotterBands;++i){
            const float inward=(i+.5f)/(float)viewlab::racing::kSpotterBands;
            if(left){const float a=viewlab::racing::SpotterBandAlpha(base,inward,iracingSpotterFade,true);rectFill(l+i*step,(float)t,l+(i+1)*step,(float)bb_,cr,cg,cb,a);flushFlat(cr,cg,cb,a);}
            if(right){const float a=viewlab::racing::SpotterBandAlpha(base,inward,iracingSpotterFade,false);rectFill(rr_-width+i*step,(float)t,rr_-width+(i+1)*step,(float)bb_,cr,cg,cb,a);flushFlat(cr,cg,cb,a);}
        }
    }

    // Feature 1: exact cropped render boundary in fixed cyan-white, constant screen thickness.
    if (wantBoundary) {
        const float boundaryPxPerTan=stereo?(std::min)(w/(sR-sL),h/(sU-sD)):minDim*.5f;
        const float th=(std::max)(2.f,boundaryPxPerTan*(2.f/1080.f)*4.5f);
        const float cr=0.55f, cg=1.f, cb=1.f, a=boundaryAlpha;
        const float inset=floorf(th*.5f)+2.f;
        const float il=l+inset,it=t+inset,ir=rr_-inset,ib=bb_-inset;
        rectFill(il,it,ir,it+th,cr,cg,cb,a);          // top, fully inside scissor
        rectFill(il,ib-th,ir,ib,cr,cg,cb,a);          // bottom
        rectFill(il,it,il+th,ib,cr,cg,cb,a);          // left
        rectFill(ir-th,it,ir,ib,cr,cg,cb,a);          // right
        flushFlat(cr,cg,cb,a);
    }

    // OBS state comes from an authenticated local obs-websocket query in the broker. Four short
    // corners are deliberately less intrusive than a full border. Capture exclusion is unclaimed:
    // this is drawn into the submitted eye texture and must be treated as capturable until proven otherwise.
    if(wantObs){const float th=std::clamp((float)obsIndicatorThickness*minDim,2.f,minDim*.04f),len=(std::max)(th*5.f,minDim*.055f),in=3.f,a=(float)obsIndicatorOpacity;
        rectFill(l+in,t+in,l+in+len,t+in+th,1.f,.04f,.04f,a);rectFill(l+in,t+in,l+in+th,t+in+len,1.f,.04f,.04f,a);rectFill(rr_-in-len,t+in,rr_-in,t+in+th,1.f,.04f,.04f,a);rectFill(rr_-in-th,t+in,rr_-in,t+in+len,1.f,.04f,.04f,a);
        rectFill(l+in,bb_-in-th,l+in+len,bb_-in,1.f,.04f,.04f,a);rectFill(l+in,bb_-in-len,l+in+th,bb_-in,1.f,.04f,.04f,a);rectFill(rr_-in-len,bb_-in-th,rr_-in,bb_-in,1.f,.04f,.04f,a);rectFill(rr_-in-th,bb_-in-len,rr_-in,bb_-in,1.f,.04f,.04f,a);flushFlat(1.f,.04f,.04f,a);}

    // A bind press earns an immediate numbered visor acknowledgement even when the live graph is
    // hidden. The same number and QPC timestamp are stored in the actual session trace.
    if(wantTraceMarker){
        const float scale=(std::max)(2.f,minDim/540.f),cx=anchorX(.5),top=anchorY(.06),cardW=42.f*scale,cardH=19.f*scale;
        rectFill(cx-cardW*.5f,top,cx+cardW*.5f,top+cardH,.025f,.065f,.075f,.88f);flushFlat(.025f,.065f,.075f,.88f);
        rectFill(cx-cardW*.5f,top,cx-cardW*.5f+2.f*scale,top+cardH,.15f,1.f,.92f,1.f);flushFlat(.15f,1.f,.92f,1.f);
        static const uint8_t digits[10][5]={{7,5,5,5,7},{2,6,2,2,7},{7,1,7,4,7},{7,1,7,1,7},{5,5,7,1,1},{7,4,7,1,7},{7,4,7,5,7},{7,1,1,1,1},{7,5,7,5,7},{7,5,7,1,7}};
        char value[8]{};snprintf(value,sizeof(value),"%u",markerNumber);const size_t n=strlen(value);float x=cx-(float)n*2.f*scale;
        for(const char*p=value;*p;++p){const int d=*p-'0';for(int row=0;row<5;++row)for(int col=0;col<3;++col)if(d>=0&&d<=9&&(digits[d][row]&(4>>col)))rectFill(x+col*scale,top+4.f*scale+row*scale,x+(col+1)*scale,top+4.f*scale+(row+1)*scale,.15f,1.f,.92f,1.f);x+=4.f*scale;}flushFlat(.15f,1.f,.92f,1.f);
    }

    if(wantSticky) for(size_t noteIndex=0;noteIndex<stickyNoteCount;++noteIndex){
        const auto& note=stickyNotes[noteIndex];if(!note.enabled||note.text.empty()||note.opacity<=.001)continue;
        const auto& stickyNoteText=note.text;const double stickyNoteX=note.x,stickyNoteY=note.y,stickyNoteScale=note.scale,stickyNoteOpacity=note.opacity;
        const auto wrapped=viewlab::sticky_note::Wrap(stickyNoteText);if(wrapped.count){
        const float pxPerTanX=stereo?w/(sR-sL):w*.5f,pxPerTanY=stereo?h/(sU-sD):h*.5f;
        const float ref=(float)stickyNoteScale*(2.f/1080.f),gx=(std::max)(1.f,floorf(ref*pxPerTanX*1.65f+.5f)),gy=(std::max)(1.f,floorf(ref*pxPerTanY*1.65f+.5f));
        size_t longest=1;for(size_t i=0;i<wrapped.count;++i)longest=(std::max)(longest,wrapped.lines[i].size());
        const float pad=3.f*gx,cardW=pad*2+(float)(longest*6-1)*gx,cardH=pad*2+(float)wrapped.count*8.f*gy-gy,cardSide=(std::max)(cardW,cardH);
        float cx=anchorX(stickyNoteX),cy=anchorY(stickyNoteY);const float sharedL=fullLeft,sharedR=fullRight,sharedT=fullTop,sharedB=fullBottom;
        cx=sharedR-sharedL>cardSide?std::clamp(cx,sharedL+cardSide*.5f,sharedR-cardSide*.5f):(sharedL+sharedR)*.5f;cy=sharedB-sharedT>cardSide?std::clamp(cy,sharedT+cardSide*.5f,sharedB-cardSide*.5f):(sharedT+sharedB)*.5f;
        const float x0=cx-cardW*.5f,y0=cy-cardH*.5f,a=(float)stickyNoteOpacity;
        struct PaperPalette{float r,g,b,tr,tg,tb,fr,fg,fb;};constexpr PaperPalette papers[]={{.96f,.82f,.38f,.58f,.42f,.10f,.10f,.075f,.035f},{.96f,.61f,.67f,.62f,.25f,.31f,.16f,.055f,.07f},{.60f,.88f,.69f,.25f,.52f,.35f,.05f,.12f,.075f},{.58f,.79f,.94f,.22f,.43f,.60f,.045f,.09f,.14f},{.92f,.89f,.78f,.56f,.50f,.38f,.12f,.10f,.07f}};const auto&p=papers[std::clamp(note.theme,0u,4u)];
        const float square=cardSide,sx0=cx-square*.5f,sy0=cy-square*.5f;
        rectFill(sx0,sy0,sx0+square,sy0+square,p.r,p.g,p.b,.96f*a);flushFlat(p.r,p.g,p.b,.96f*a);rectFill(sx0,sy0,sx0+square,sy0+gy,p.tr,p.tg,p.tb,.38f*a);flushFlat(p.tr,p.tg,p.tb,.38f*a);rectFill(sx0+square-6*gx,sy0,sx0+square,sy0+6*gy,1.f,1.f,1.f,.18f*a);flushFlat(1.f,1.f,1.f,.18f*a);
        static const uint8_t font[36][7]={
          {14,17,19,21,25,17,14},{4,12,4,4,4,4,14},{14,17,1,2,4,8,31},{30,1,1,14,1,1,30},{2,6,10,18,31,2,2},{31,16,30,1,1,17,14},{6,8,16,30,17,17,14},{31,1,2,4,8,8,8},{14,17,17,14,17,17,14},{14,17,17,15,1,2,12},
          {14,17,17,31,17,17,17},{30,17,17,30,17,17,30},{14,17,16,16,16,17,14},{30,17,17,17,17,17,30},{31,16,16,30,16,16,31},{31,16,16,30,16,16,16},{14,17,16,23,17,17,15},{17,17,17,31,17,17,17},{14,4,4,4,4,4,14},{7,2,2,2,2,18,12},{17,18,20,24,20,18,17},{16,16,16,16,16,16,31},{17,27,21,21,17,17,17},{17,25,21,19,17,17,17},{14,17,17,17,17,17,14},{30,17,17,30,16,16,16},{14,17,17,17,21,18,13},{30,17,17,30,20,18,17},{15,16,16,14,1,1,30},{31,4,4,4,4,4,4},{17,17,17,17,17,17,14},{17,17,17,17,17,10,4},{17,17,17,21,21,21,10},{17,17,10,4,10,17,17},{17,17,10,4,4,4,4},{31,1,2,4,8,16,31}};
        float ty=sy0+pad;for(size_t rowIndex=0;rowIndex<wrapped.count;++rowIndex){float tx=sx0+pad;for(char c:wrapped.lines[rowIndex]){int gi=c>='0'&&c<='9'?c-'0':c>='A'&&c<='Z'?10+c-'A':-1;if(gi>=0)for(int row=0;row<7;++row)for(int col=0;col<5;++col)if(font[gi][row]&(16>>col))rectFill(tx+col*gx,ty+row*gy,tx+(col+1)*gx,ty+(row+1)*gy,p.fr,p.fg,p.fb,.95f*a);else{}else if(c=='.')rectFill(tx+2*gx,ty+6*gy,tx+3*gx,ty+7*gy,p.fr,p.fg,p.fb,.95f*a);else if(c=='!'){rectFill(tx+2*gx,ty,tx+3*gx,ty+5*gy,p.fr,p.fg,p.fb,.95f*a);rectFill(tx+2*gx,ty+6*gy,tx+3*gx,ty+7*gy,p.fr,p.fg,p.fb,.95f*a);}tx+=6*gx;}ty+=8*gy;}flushFlat(p.fr,p.fg,p.fb,.95f*a);
        }}

    // Dedicated two-line clock card. The upper clock is local wall time; the lower hourglass is
    // monotonic time since this OpenXR session was created. It never enters the notification queue.
    if (wantClock) {
        SYSTEMTIME local{}; GetLocalTime(&local);
        const uint64_t nowTick = GetTickCount64();
        const uint64_t startTick = g_clockSessionStartTick.load(std::memory_order_acquire);
        const auto text = viewlab::clock_widget::Format(local.wHour, local.wMinute,
            startTick != 0 && nowTick >= startTick ? nowTick - startTick : 0,clock24Hour);
        const float pxPerTanX = stereo ? w/(sR-sL) : w*.5f;
        const float pxPerTanY = stereo ? h/(sU-sD) : h*.5f;
        const float referenceUnit = (float)clockWidgetScale * (2.f/1080.f);
        const float glyphX = (std::max)(1.f, floorf(referenceUnit*pxPerTanX*2.1f+.5f));
        const float glyphY = (std::max)(1.f, floorf(referenceUnit*pxPerTanY*2.1f+.5f));
        // Palette = colours only; theme = the actual visual design (layout, type scale,
        // padding, borders, icons, information arrangement). Classic (0) is byte-identical to
        // the historical card so migrated configs keep their exact appearance.
        struct ClockPalette{float br,bg,bb,ar,ag,ab,pr,pg,pb,sr,sg,sb;};constexpr ClockPalette palettes[]={{.055f,.058f,.064f,.72f,.73f,.76f,.96f,.96f,.95f,.68f,.69f,.72f},{.90f,.86f,.76f,.58f,.42f,.22f,.16f,.14f,.11f,.38f,.32f,.25f},{0,0,0,1,1,1,1,1,1,.68f,.68f,.68f},{.09f,.06f,.025f,.95f,.60f,.16f,1.f,.88f,.62f,.83f,.60f,.27f},{.025f,.075f,.06f,.24f,.82f,.60f,.90f,1.f,.95f,.48f,.78f,.67f}};const auto&theme=palettes[std::clamp(clockWidgetPalette,0u,4u)];
        const uint32_t clockLayout=std::clamp(clockWidgetTheme,0u,3u);
        const float opacity=(float)clockWidgetOpacity;

        static const unsigned char font[14][7]={
            {0x0E,0x11,0x13,0x15,0x19,0x11,0x0E},{0x04,0x0C,0x04,0x04,0x04,0x04,0x0E},
            {0x0E,0x11,0x01,0x02,0x04,0x08,0x1F},{0x1F,0x02,0x04,0x02,0x01,0x11,0x0E},
            {0x02,0x06,0x0A,0x12,0x1F,0x02,0x02},{0x1F,0x10,0x1E,0x01,0x01,0x11,0x0E},
            {0x06,0x08,0x10,0x1E,0x11,0x11,0x0E},{0x1F,0x01,0x02,0x04,0x04,0x04,0x04},
            {0x0E,0x11,0x11,0x0E,0x11,0x11,0x0E},{0x0E,0x11,0x11,0x0F,0x01,0x02,0x0C},
            {0x00,0x04,0x04,0x00,0x04,0x04,0x00},
            {0x0E,0x11,0x11,0x1F,0x11,0x11,0x11},{0x11,0x1B,0x15,0x15,0x11,0x11,0x11},{0x1E,0x11,0x11,0x1E,0x10,0x10,0x10}
        };
        auto drawClockText=[&](float tx,float ty,const char* value,float cr,float cg,float cb,float alpha,float textScale){
            for(const char* p=value;*p;++p){const int gi=*p==':'?10:*p=='A'?11:*p=='M'?12:*p=='P'?13:(*p>='0'&&*p<='9'?*p-'0':-1);if(gi>=0)for(int row=0;row<7;++row){const unsigned char bits=font[gi][row];for(int col=0;col<5;){if(bits&(0x10>>col)){const int first=col;while(col<5&&(bits&(0x10>>col)))++col;rectFill(tx+first*glyphX*textScale,ty+row*glyphY*textScale,tx+col*glyphX*textScale,ty+(row+1)*glyphY*textScale,cr,cg,cb,alpha);}else ++col;}}tx+=*p==' '?3.f*glyphX*textScale:6.f*glyphX*textScale;}flushFlat(cr,cg,cb,alpha);
        };
        auto clockTextWidth=[&](const char* value,float textScale){float total=0.f;for(const char* p=value;*p;++p)total+=*p==' '?3.f:6.f;return total*glyphX*textScale;};

        // Per-design metrics. Every design keeps the same anchor/clamp contract; only the
        // card geometry and content arrangement differ.
        float primaryScale,secondaryScale,padX,padY,iconW;
        switch(clockLayout){
        default: primaryScale=1.25f;secondaryScale=.90f;padX=4.f*glyphX;padY=2.5f*glyphY;iconW=8.f*glyphX;break; // Classic
        case 1u: primaryScale=1.70f;secondaryScale=1.00f;padX=1.5f*glyphX;padY=1.0f*glyphY;iconW=0.f;break;      // Minimal
        case 2u: primaryScale=1.00f;secondaryScale=.85f;padX=2.f*glyphX;padY=1.5f*glyphY;iconW=0.f;break;        // Terminal
        case 3u: primaryScale=1.15f;secondaryScale=.90f;padX=5.f*glyphX;padY=3.f*glyphY;iconW=0.f;break;         // Banner
        }
        const float primaryH=7.f*glyphY*primaryScale,secondaryH=7.f*glyphY*secondaryScale;
        const float timeW=clockTextWidth(text.local.data(),primaryScale);
        const float sessW=clockSessionTimerEnabled?clockTextWidth(text.session.data(),secondaryScale):0.f;
        const float terminalBorder=(std::max)(1.f,glyphX*.8f),terminalHeader=3.f*glyphY;
        const float bannerCap=1.5f*glyphX,bannerDivider=(std::max)(1.f,glyphX*.5f),bannerGap=3.f*glyphX;
        float cardW,cardH;
        switch(clockLayout){
        default:
            cardW=padX*2+iconW+2.f*glyphX+47.f*glyphX*primaryScale;
            cardH=padY*2+primaryH+(clockSessionTimerEnabled?secondaryH+3.f*glyphY:0.f);break;
        case 1u:
            cardW=padX*2+(std::max)(timeW,sessW);
            cardH=padY*2+primaryH+(clockSessionTimerEnabled?secondaryH+2.f*glyphY:0.f);break;
        case 2u:
            cardW=terminalBorder*2+padX*2+(std::max)(timeW,sessW);
            cardH=terminalBorder*2+terminalHeader+padY*2+primaryH+(clockSessionTimerEnabled?secondaryH+2.f*glyphY:0.f);break;
        case 3u:
            cardW=padX*2+bannerCap*2+timeW+(clockSessionTimerEnabled?bannerGap*2+bannerDivider+sessW:0.f);
            cardH=padY*2+primaryH;break;
        }
        const float sharedLeft=fullLeft,sharedRight=fullRight;
        const float sharedTop=fullTop,sharedBottom=fullBottom;
        float cx=anchorX(clockWidgetX),cy=anchorY(clockWidgetY);
        cx=sharedRight-sharedLeft>cardW?std::clamp(cx,sharedLeft+cardW*.5f,sharedRight-cardW*.5f):(sharedLeft+sharedRight)*.5f;
        cy=sharedBottom-sharedTop>cardH?std::clamp(cy,sharedTop+cardH*.5f,sharedBottom-cardH*.5f):(sharedTop+sharedBottom)*.5f;
        const float x0=cx-cardW*.5f,y0=cy-cardH*.5f,x1=cx+cardW*.5f,y1=cy+cardH*.5f;

        if(clockLayout==0u){
            // Classic: leading accent bar, clock-face and hourglass icons, stacked lanes.
            rectFill(x0,y0,x1,y1,theme.br,theme.bg,theme.bb,.92f*opacity); flushFlat(theme.br,theme.bg,theme.bb,.92f*opacity);
            rectFill(x0,y0,x0+glyphX,y1,theme.ar,theme.ag,theme.ab,.90f*opacity); flushFlat(theme.ar,theme.ag,theme.ab,.90f*opacity);
            const float dividerY=y0+padY+primaryH+1.5f*glyphY;
            if(clockSessionTimerEnabled){rectFill(x0+padX,dividerY,x1-padX,dividerY+(std::max)(1.f,glyphY*.35f),theme.ar,theme.ag,theme.ab,.35f*opacity); flushFlat(theme.ar,theme.ag,theme.ab,.35f*opacity);}
            const float iconX=x0+padX, textX=iconX+iconW+2.f*glyphX;
            const float firstY=y0+padY,secondY=dividerY+2.f*glyphY;
            // Square clock face with two hands.
            const float ith=(std::max)(1.f,glyphX*.7f),ic=iconX+3.f*glyphX,iy=firstY+3.5f*glyphY;
            rectFill(iconX,firstY,iconX+6*glyphX,firstY+ith,theme.ar,theme.ag,theme.ab,opacity);rectFill(iconX,firstY+7*glyphY-ith,iconX+6*glyphX,firstY+7*glyphY,theme.ar,theme.ag,theme.ab,opacity);
            rectFill(iconX,firstY,iconX+ith,firstY+7*glyphY,theme.ar,theme.ag,theme.ab,opacity);rectFill(iconX+6*glyphX-ith,firstY,iconX+6*glyphX,firstY+7*glyphY,theme.ar,theme.ag,theme.ab,opacity);
            rectFill(ic-ith*.5f,firstY+glyphY,ic+ith*.5f,iy+.5f*glyphY,theme.ar,theme.ag,theme.ab,opacity);rectFill(ic,iy-ith*.5f,iconX+5*glyphX,iy+ith*.5f,theme.ar,theme.ag,theme.ab,opacity);flushFlat(theme.ar,theme.ag,theme.ab,opacity);
            // Hourglass for elapsed session time.
            if(clockSessionTimerEnabled){rectFill(iconX,secondY,iconX+6*glyphX,secondY+ith,theme.sr,theme.sg,theme.sb,opacity);rectFill(iconX,secondY+7*glyphY-ith,iconX+6*glyphX,secondY+7*glyphY,theme.sr,theme.sg,theme.sb,opacity);
            for(int step=0;step<3;++step){rectFill(iconX+(step+1)*glyphX,secondY+(step+1)*glyphY,iconX+(step+2)*glyphX,secondY+(step+2)*glyphY,theme.sr,theme.sg,theme.sb,opacity);rectFill(iconX+(4-step)*glyphX,secondY+(step+1)*glyphY,iconX+(5-step)*glyphX,secondY+(step+2)*glyphY,theme.sr,theme.sg,theme.sb,opacity);rectFill(iconX+(step+1)*glyphX,secondY+(5-step)*glyphY,iconX+(step+2)*glyphX,secondY+(6-step)*glyphY,theme.sr,theme.sg,theme.sb,opacity);rectFill(iconX+(4-step)*glyphX,secondY+(5-step)*glyphY,iconX+(5-step)*glyphX,secondY+(6-step)*glyphY,theme.sr,theme.sg,theme.sb,opacity);}flushFlat(theme.sr,theme.sg,theme.sb,opacity);}
            drawClockText(textX,firstY,text.local.data(),theme.pr,theme.pg,theme.pb,opacity,primaryScale);
            if(clockSessionTimerEnabled)drawClockText(textX,secondY,text.session.data(),theme.sr,theme.sg,theme.sb,opacity,secondaryScale);
        } else if(clockLayout==1u){
            // Minimal: no card surface at all — large centred digits with a pixel drop shadow
            // for legibility, session lane centred underneath.
            const float shadow=(std::max)(1.f,glyphX*.4f);
            const float firstY=y0+padY,secondY=firstY+primaryH+2.f*glyphY;
            const float timeX=cx-timeW*.5f,sessX=cx-sessW*.5f;
            drawClockText(timeX+shadow,firstY+shadow,text.local.data(),0.f,0.f,0.f,.75f*opacity,primaryScale);
            drawClockText(timeX,firstY,text.local.data(),theme.pr,theme.pg,theme.pb,opacity,primaryScale);
            if(clockSessionTimerEnabled){
                drawClockText(sessX+shadow,secondY+shadow,text.session.data(),0.f,0.f,0.f,.75f*opacity,secondaryScale);
                drawClockText(sessX,secondY,text.session.data(),theme.sr,theme.sg,theme.sb,opacity,secondaryScale);
            }
        } else if(clockLayout==2u){
            // Terminal: thick square frame, solid accent header band, tight left-aligned
            // monospaced lanes beneath it.
            rectFill(x0,y0,x1,y1,theme.br,theme.bg,theme.bb,.94f*opacity);flushFlat(theme.br,theme.bg,theme.bb,.94f*opacity);
            rectFill(x0,y0,x1,y0+terminalBorder,theme.ar,theme.ag,theme.ab,opacity);
            rectFill(x0,y1-terminalBorder,x1,y1,theme.ar,theme.ag,theme.ab,opacity);
            rectFill(x0,y0,x0+terminalBorder,y1,theme.ar,theme.ag,theme.ab,opacity);
            rectFill(x1-terminalBorder,y0,x1,y1,theme.ar,theme.ag,theme.ab,opacity);
            rectFill(x0+terminalBorder,y0+terminalBorder,x1-terminalBorder,y0+terminalBorder+terminalHeader,theme.ar,theme.ag,theme.ab,.55f*opacity);
            flushFlat(theme.ar,theme.ag,theme.ab,opacity);
            const float textX=x0+terminalBorder+padX;
            const float firstY=y0+terminalBorder+terminalHeader+padY,secondY=firstY+primaryH+2.f*glyphY;
            drawClockText(textX,firstY,text.local.data(),theme.pr,theme.pg,theme.pb,opacity,primaryScale);
            if(clockSessionTimerEnabled)drawClockText(textX,secondY,text.session.data(),theme.sr,theme.sg,theme.sb,opacity,secondaryScale);
        } else {
            // Banner: one wide row — time and session side by side with a vertical divider,
            // accent end caps and thin horizontal rails instead of a boxed border.
            rectFill(x0,y0,x1,y1,theme.br,theme.bg,theme.bb,.92f*opacity);flushFlat(theme.br,theme.bg,theme.bb,.92f*opacity);
            const float rail=(std::max)(1.f,glyphY*.4f);
            rectFill(x0,y0,x1,y0+rail,theme.ar,theme.ag,theme.ab,.85f*opacity);
            rectFill(x0,y1-rail,x1,y1,theme.ar,theme.ag,theme.ab,.85f*opacity);
            rectFill(x0,y0,x0+bannerCap,y1,theme.ar,theme.ag,theme.ab,opacity);
            rectFill(x1-bannerCap,y0,x1,y1,theme.ar,theme.ag,theme.ab,opacity);
            flushFlat(theme.ar,theme.ag,theme.ab,opacity);
            const float timeX=x0+bannerCap+padX,timeY=y0+padY;
            drawClockText(timeX,timeY,text.local.data(),theme.pr,theme.pg,theme.pb,opacity,primaryScale);
            if(clockSessionTimerEnabled){
                const float divX=timeX+timeW+bannerGap;
                rectFill(divX,y0+padY,divX+bannerDivider,y1-padY,theme.sr,theme.sg,theme.sb,.55f*opacity);flushFlat(theme.sr,theme.sg,theme.sb,.55f*opacity);
                drawClockText(divX+bannerDivider+bannerGap,timeY+(primaryH-secondaryH),text.session.data(),theme.sr,theme.sg,theme.sb,opacity,secondaryScale);
            }
        }
    }

    // Feature 2: CS-style static crosshair at the calibrated stereo centre. CS reference pixels
    // are scaled to eye pixels by the VR scale and eye height, keeping angular size stable across
    // eye-buffer resolutions; every span is pixel-snapped for sharp edges.
    if (wantCrosshair) {
        const float pxPerTanX = stereo ? w/(sR-sL) : w*.5f;
        const float pxPerTanY = stereo ? h/(sU-sD) : h*.5f;
        const float unitX = (float)crosshairScale * pxPerTanX * (2.f/1080.f);
        const float unitY = (float)crosshairScale * pxPerTanY * (2.f/1080.f);
        const float a = (float)crosshairAlpha;
        const float armX=floorf((float)crosshairSize*unitX+.5f), armY=floorf((float)crosshairSize*unitY+.5f);
        const float thickX=(std::max)(1.f,floorf((float)crosshairThickness*unitX+.5f)), thickY=(std::max)(1.f,floorf((float)crosshairThickness*unitY+.5f));
        const float gapX=floorf((float)crosshairGap*unitX+.5f), gapY=floorf((float)crosshairGap*unitY+.5f);
        const float outlineX=crosshairOutline?(std::max)(1.f,floorf((float)crosshairOutlineThickness*unitX+.5f)):0.f;
        const float outlineY=crosshairOutline?(std::max)(1.f,floorf((float)crosshairOutlineThickness*unitY+.5f)):0.f;
        const float cx = floorf(centreX)+0.5f, cy = floorf(centreY)+0.5f; // centre on a pixel
        const float halfX=thickX*.5f,halfY=thickY*.5f,innerX=gapX+halfX,innerY=gapY+halfY;
        // Collect arm rectangles first so the black outline can be drawn behind all of them.
        struct R { float x0,y0,x1,y1; };
        std::vector<R> arms;
        arms.push_back({cx-innerX-armX,cy-halfY,cx-innerX,cy+halfY});
        arms.push_back({cx+innerX,cy-halfY,cx+innerX+armX,cy+halfY});
        if(!crosshairTStyle)arms.push_back({cx-halfX,cy-innerY-armY,cx+halfX,cy-innerY});
        arms.push_back({cx-halfX,cy+innerY,cx+halfX,cy+innerY+armY});
        R dot{}; const bool hasDot = crosshairDot;
        if(hasDot)dot={cx-halfX,cy-halfY,cx+halfX,cy+halfY};
        if(outlineX>0.f||outlineY>0.f){
            for(const R&r:arms)rectFill(r.x0-outlineX,r.y0-outlineY,r.x1+outlineX,r.y1+outlineY,0,0,0,a);
            if(hasDot)rectFill(dot.x0-outlineX,dot.y0-outlineY,dot.x1+outlineX,dot.y1+outlineY,0,0,0,a);
            flushFlat(0.f,0.f,0.f,a);
        }
        for (const R& r : arms) rectFill(r.x0, r.y0, r.x1, r.y1, crosshairR, crosshairG, crosshairB, a);
        if (hasDot) rectFill(dot.x0, dot.y0, dot.x1, dot.y1, crosshairR, crosshairG, crosshairB, a);
        flushFlat(crosshairR,crosshairG,crosshairB,a);
        static std::atomic<uint32_t> crosshairDrawLogs{0};
        if (crosshairDrawLogs.fetch_add(1) < 4)
            Log("crosshair: draw eye=%u sharedTan=(0,0) projectedPx=(%.1f,%.1f) unitPx=(%.3f,%.3f) armPx=(%.1f,%.1f) gapPx=(%.1f,%.1f) thickPx=(%.1f,%.1f) angularDisparity=0 fovTan=(%.4f,%.4f,%.4f,%.4f) rect=%d,%d %dx%d rgba=(%.3f,%.3f,%.3f,%.3f) executed=1\n",
                eye.viewIndex,cx,cy,unitX,unitY,armX,armY,gapX,gapY,thickX,thickY,sL,sR,sU,sD,l,t,w,h,crosshairR,crosshairG,crosshairB,a);
    }

    // Feature 3: notification cards (textured, pre-composited in the UI). Newest cards sit at the
    // bottom-right and stack upward; each carries its own animated alpha and horizontal slide.
    if (wantNotify && g_d3d11Mask.linearSampler && g_d3d11Mask.tintCb) {
        // Read cards in place; avoid copying the multi-hundred-KB pixel payload every frame. The
        // UI only rewrites a slot's pixels when its contentSerial changes, and EnsureNotifyCard
        // Texture re-uploads only on that change, so a rare torn read self-corrects next frame.
        const NotifyBlock* nb = g_notify;
        g_d3d11Mask.context->VSSetShader(g_d3d11Mask.vs,nullptr,0); g_d3d11Mask.context->PSSetShader(g_d3d11Mask.texturedPs,nullptr,0);
        g_d3d11Mask.context->PSSetSamplers(0,1,&g_d3d11Mask.linearSampler);
        const float margin = 0.02f*minDim;
        const float pxPerTanX=stereo?w/(sR-sL):w*.5f,pxPerTanY=stereo?h/(sU-sD):h*.5f;
        const float cardTan=(float)notifyScale*(2.f/1080.f)*460.f;
        const float cardW=std::clamp(cardTan*pxPerTanX,48.f,(fullRight-fullLeft)*.6f);
        const float gap=cardTan*pxPerTanY*.05f;
        const float baseRight = stereo ? std::clamp(anchorX(notifyX), fullLeft+margin+cardW, fullRight-margin) : fullRight-margin;
        const float baseBottom = stereo ? std::clamp(anchorY(notifyY), fullTop+margin, fullBottom-margin) : fullBottom-margin;
        for (uint32_t i=0;i<kNotifyMaxCards;++i) {
            const NotifyCardBlock& card = nb->cards[i];
            if (!card.active || card.width==0 || card.height==0) continue;
            ID3D11ShaderResourceView* srv = EnsureNotifyCardTexture(i, card);
            if (!srv) continue;
            const float aspect = (float)card.height/(float)card.width;
            const float cardH = cardTan*pxPerTanY*aspect;
            const auto animation = viewlab::policy::EvaluateNotificationAnimation(
                static_cast<uint32_t>(GetTickCount64()), card.enterTick, card.leaveTick);
            const float slidePx = animation.slide * (cardW + margin);
            const float x1 = baseRight + slidePx;                       // right edge (slides off-right)
            const float x0 = x1 - cardW;
            const float y1 = baseBottom - card.stackIndex*(cardH+gap);
            const float y0 = y1 - cardH;
            const float u1 = (float)card.width/(float)kNotifyCardW, v1=(float)card.height/(float)kNotifyCardH;
            const float a = animation.alpha * (float)notifyOpacity;
            if (a <= 0.003f) continue;
            // Tint alpha animates the fade; RGB unused by the textured PS.
            float tint[4]={1.f,1.f,1.f,a}; D3D11_MAPPED_SUBRESOURCE tm{};
            if (SUCCEEDED(g_d3d11Mask.context->Map(g_d3d11Mask.tintCb,0,D3D11_MAP_WRITE_DISCARD,0,&tm))) { memcpy(tm.pData,tint,sizeof(tint)); g_d3d11Mask.context->Unmap(g_d3d11Mask.tintCb,0); }
            g_d3d11Mask.context->PSSetConstantBuffers(0,1,&g_d3d11Mask.tintCb);
            g_d3d11Mask.context->PSSetShaderResources(0,1,&srv);
            // UV packed into TEXCOORD.xy (r,g); tint alpha carries the per-card fade once.
            VisorVertex q[6];
            auto V=[&](float px,float py,float u,float v){ VisorVertex vv{}; vv.x=ndcX(px); vv.y=ndcY(py); vv.r=u; vv.g=v; vv.b=0.f; vv.alpha=1.f; return vv; };
            q[0]=V(x0,y0,0,0); q[1]=V(x1,y0,u1,0); q[2]=V(x1,y1,u1,v1);
            q[3]=V(x0,y0,0,0); q[4]=V(x1,y1,u1,v1); q[5]=V(x0,y1,0,v1);
            D3D11_MAPPED_SUBRESOURCE m{};
            if (SUCCEEDED(g_d3d11Mask.context->Map(g_d3d11Mask.vb,0,D3D11_MAP_WRITE_DISCARD,0,&m))) { memcpy(m.pData,q,sizeof(q)); g_d3d11Mask.context->Unmap(g_d3d11Mask.vb,0); g_d3d11Mask.context->Draw(6,0); }
        }
        ID3D11ShaderResourceView* nullSrv=nullptr; g_d3d11Mask.context->PSSetShaderResources(0,1,&nullSrv);
    }

    // Restore.
    g_d3d11Mask.context->OMSetRenderTargets(D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT,sRTVs,sDSV); if(sVPCount) g_d3d11Mask.context->RSSetViewports(sVPCount,sVPs); if(sScissorCount) g_d3d11Mask.context->RSSetScissorRects(sScissorCount,sScissors);
    g_d3d11Mask.context->RSSetState(sRS); g_d3d11Mask.context->OMSetBlendState(sBS,sBF,sSM); g_d3d11Mask.context->OMSetDepthStencilState(sDSS,sSRef);
    g_d3d11Mask.context->VSSetShader(sVS,nullptr,0); g_d3d11Mask.context->PSSetShader(sPS,nullptr,0); g_d3d11Mask.context->PSSetConstantBuffers(0,1,&sPSCB); g_d3d11Mask.context->PSSetShaderResources(0,1,&sSRV); g_d3d11Mask.context->PSSetSamplers(0,1,&sSmp);
    g_d3d11Mask.context->IASetInputLayout(sLayout); g_d3d11Mask.context->IASetPrimitiveTopology(sTopo); g_d3d11Mask.context->IASetVertexBuffers(0,1,&sVB,&sStride,&sOff);
    if(ownsRtv) rtv->Release(); for(auto p:sRTVs) if(p) p->Release(); if(sDSV)sDSV->Release(); if(sRS)sRS->Release(); if(sBS)sBS->Release(); if(sDSS)sDSS->Release(); if(sVS)sVS->Release(); if(sPS)sPS->Release(); if(sPSCB)sPSCB->Release(); if(sSRV)sSRV->Release(); if(sSmp)sSmp->Release(); if(sLayout)sLayout->Release(); if(sVB)sVB->Release();
    g_d3d11Mask.context->Flush();
}

void DrawCalibrationPatternsToTexture(ID3D11Texture2D* tex, uint32_t arrSize, int64_t scFormat,
    const EyeView& eye, const std::vector<EyeView>& allViews, ID3D11RenderTargetView* cachedRtv,
    bool includeCalibration, bool includeHudAndOverlays, uint32_t featureMask = kAllMirrorFeatures) {
    if (includeCalibration && calibrationGrid) DrawCalibrationGridToTexture(tex, arrSize, scFormat, eye, allViews, cachedRtv);
    if ((includeCalibration && AnyCalibrationPattern()) || (includeHudAndOverlays && ((hudEnabled&&OverlayFeatureVisible(OverlayFeatureId::Hud)) || (hudTraceEnabled&&OverlayFeatureVisible(OverlayFeatureId::Trace)))))
        DrawCalibrationOverlayToTexture(tex, arrSize, scFormat, eye, allViews, cachedRtv,includeCalibration,includeHudAndOverlays,featureMask);
    if (includeHudAndOverlays && AnyViewLabOverlay())
        DrawViewLabOverlaysToTexture(tex, arrSize, scFormat, eye, allViews, cachedRtv,featureMask);
}

struct TopmostSubmission {
    XrCompositionLayerProjection layer{XR_TYPE_COMPOSITION_LAYER_PROJECTION};
    std::array<XrCompositionLayerProjectionView,2> views{};
};

void DestroyTopmostLayer() {
    if (g_topmostLayer.swapchain != XR_NULL_HANDLE && nextXrDestroySwapchain)
        nextXrDestroySwapchain(g_topmostLayer.swapchain);
    g_topmostLayer = TopmostLayerState{};
}

void BlockTopmostLayer(const char* stage, int64_t result) {
    const bool firstFailure = !g_topmostLayerBlocked.exchange(true, std::memory_order_acq_rel);
    // The ordered carrier is no longer eligible. Restore the single proven writable path for all
    // normal features together; no feature owns an independent fallback decision.
    g_featurePresentationPlan.drawDirectVisor = true;
    g_featurePresentationPlan.drawDirectCommonFeatures = true;
    const HRESULT deviceReason = g_d3d11Mask.device
        ? g_d3d11Mask.device->GetDeviceRemovedReason() : E_POINTER;
    if (FAILED(deviceReason) && g_d3d11Mask.device)
        RendererDeviceHealthy(stage);
    if (firstFailure) {
        Log("topmost safety: disabled for session stage=%s result=0x%llX deviceReason=0x%08X "
            "pid=%lu thread=%lu; no automatic retry\n",
            stage ? stage : "unknown", static_cast<unsigned long long>(result),
            static_cast<unsigned>(deviceReason),
            static_cast<unsigned long>(GetCurrentProcessId()),
            static_cast<unsigned long>(GetCurrentThreadId()));
    }
    // Do not destroy or recreate compositor resources from a failing render path. In particular,
    // a removed device is precisely the wrong moment to churn runtime-owned D3D resources. The
    // dormant swapchain is released once, during normal session teardown.
}

bool EnsureTopmostLayer(XrSession session, uint32_t width, uint32_t height, int64_t preferredFormat) {
    if (g_topmostLayerBlocked.load(std::memory_order_acquire)) return false;
    if (g_topmostLayer.ready) {
        if (g_topmostLayer.session==session && width<=g_topmostLayer.width &&
            height<=g_topmostLayer.height && g_topmostLayer.format==preferredFormat) return true;
        BlockTopmostLayer("projection capacity changed", XR_ERROR_LIMIT_REACHED);
        return false;
    }
    // A session gets exactly one allocation attempt. A failed runtime/driver allocation must never
    // become a per-frame retry loop, even if a future failure branch forgets to latch explicitly.
    if (g_topmostLayerAttempted) {
        BlockTopmostLayer("allocation already attempted", XR_ERROR_RUNTIME_FAILURE);
        return false;
    }
    g_topmostLayerAttempted = true;
    if (!nextXrCreateSwapchain || !nextXrEnumerateSwapchainImages || width==0 || height==0) {
        BlockTopmostLayer("invalid prerequisites", XR_ERROR_VALIDATION_FAILURE);
        return false;
    }
    XrSwapchainCreateInfo ci{XR_TYPE_SWAPCHAIN_CREATE_INFO};
    ci.usageFlags=XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT|XR_SWAPCHAIN_USAGE_SAMPLED_BIT;
    ci.format=preferredFormat; ci.sampleCount=1; ci.width=width; ci.height=height;
    ci.faceCount=1; ci.arraySize=2; ci.mipCount=1;
    XrSwapchain sc=XR_NULL_HANDLE;
    const XrResult createResult=nextXrCreateSwapchain(session,&ci,&sc);
    if (XR_FAILED(createResult)) {
        BlockTopmostLayer("xrCreateSwapchain", createResult);
        return false;
    }
    uint32_t count=0;
    XrResult enumerateResult=nextXrEnumerateSwapchainImages(sc,0,&count,nullptr);
    if (XR_FAILED(enumerateResult) || count==0) { nextXrDestroySwapchain(sc); BlockTopmostLayer("xrEnumerateSwapchainImages(count)", enumerateResult); return false; }
    std::vector<XrSwapchainImageD3D11KHR> images(count);
    for(auto& image:images) image.type=XR_TYPE_SWAPCHAIN_IMAGE_D3D11_KHR;
    enumerateResult=nextXrEnumerateSwapchainImages(sc,count,&count,reinterpret_cast<XrSwapchainImageBaseHeader*>(images.data()));
    if (XR_FAILED(enumerateResult)) { nextXrDestroySwapchain(sc); BlockTopmostLayer("xrEnumerateSwapchainImages(images)", enumerateResult); return false; }
    g_topmostLayer.session=session; g_topmostLayer.swapchain=sc; g_topmostLayer.width=width; g_topmostLayer.height=height;
    g_topmostLayer.format=ci.format; g_topmostLayer.images=std::move(images); g_topmostLayer.ready=true;
    D3D11_TEXTURE2D_DESC desc{};
    if(!g_topmostLayer.images.empty() && g_topmostLayer.images[0].texture)
        g_topmostLayer.images[0].texture->GetDesc(&desc);
    Log("topmost overlays: transparent projection swapchain ready %ux%u array=2 requestedFormat=%lld resourceFormat=%u "
        "bind=0x%X misc=0x%X images=%u pid=%lu thread=%lu\n",
        width,height,(long long)ci.format,(unsigned)desc.Format,desc.BindFlags,desc.MiscFlags,count,
        static_cast<unsigned long>(GetCurrentProcessId()),
        static_cast<unsigned long>(GetCurrentThreadId()));
    return true;
}

bool RenderTopmostLayer(XrSession session, const XrCompositionLayerProjection* source, TopmostSubmission& out) {
    if (!source || source->viewCount<1 || !source->views || !g_d3d11Mask.device || !g_d3d11Mask.context) {
        BlockTopmostLayer("render prerequisites", E_POINTER);
        return false;
    }
    if (!RendererDeviceHealthy("RenderTopmostLayer(begin)")) { BlockTopmostLayer("device removed before render", DXGI_ERROR_DEVICE_REMOVED); return false; }
    uint32_t width=0,height=0,viewCount=(std::min)(source->viewCount,2u);
    for(uint32_t i=0;i<viewCount;++i) {
        width=(std::max)(width,(uint32_t)source->views[i].subImage.imageRect.extent.width);
        height=(std::max)(height,(uint32_t)source->views[i].subImage.imageRect.extent.height);
    }
    int64_t preferredFormat=DXGI_FORMAT_R8G8B8A8_UNORM_SRGB;
    {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        auto tracked=g_swapchains.find(source->views[0].subImage.swapchain);
        if(tracked!=g_swapchains.end()) {
            const int64_t candidate=tracked->second.format;
            if(candidate==DXGI_FORMAT_R8G8B8A8_UNORM || candidate==DXGI_FORMAT_R8G8B8A8_UNORM_SRGB ||
               candidate==DXGI_FORMAT_B8G8R8A8_UNORM || candidate==DXGI_FORMAT_B8G8R8A8_UNORM_SRGB)
                preferredFormat=candidate;
            if(!tracked->second.textures.empty() && tracked->second.textures[0]) {
                D3D11_TEXTURE2D_DESC sourceDesc{};
                tracked->second.textures[0]->GetDesc(&sourceDesc);
                if(sourceDesc.ArraySize > 1) {
                    width=(std::max)(width,sourceDesc.Width);
                    height=(std::max)(height,sourceDesc.Height);
                }
            }
        }
    }
    if (!EnsureTopmostLayer(session,width,height,preferredFormat)) return false;
    uint32_t imageIndex=0; XrSwapchainImageAcquireInfo ai{XR_TYPE_SWAPCHAIN_IMAGE_ACQUIRE_INFO};
    const XrResult acquireResult=nextXrAcquireSwapchainImage(g_topmostLayer.swapchain,&ai,&imageIndex);
    if (XR_FAILED(acquireResult)) { BlockTopmostLayer("xrAcquireSwapchainImage", acquireResult); return false; }
    XrSwapchainImageWaitInfo wi{XR_TYPE_SWAPCHAIN_IMAGE_WAIT_INFO}; wi.timeout=100000000;
    const XrResult waitResult=nextXrWaitSwapchainImage(g_topmostLayer.swapchain,&wi);
    if (XR_FAILED(waitResult) || imageIndex>=g_topmostLayer.images.size()) { XrSwapchainImageReleaseInfo ri{XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO}; nextXrReleaseSwapchainImage(g_topmostLayer.swapchain,&ri); BlockTopmostLayer("xrWaitSwapchainImage", waitResult); return false; }
    ID3D11Texture2D* tex=g_topmostLayer.images[imageIndex].texture;
    if(!tex) { XrSwapchainImageReleaseInfo ri{XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO}; nextXrReleaseSwapchainImage(g_topmostLayer.swapchain,&ri); BlockTopmostLayer("null swapchain texture", E_POINTER); return false; }
    D3D11_TEXTURE2D_DESC topmostDesc{}; tex->GetDesc(&topmostDesc);
    std::vector<EyeView> eyes; eyes.reserve(viewCount);
    for(uint32_t i=0;i<viewCount;++i) {
        XrFovf full=source->views[i].fov;
        if(i<g_d3d11Mask.latestViewCount) full=g_d3d11Mask.latestOriginalViews[i].fov;
        eyes.push_back(EyeView{{{0,0},{source->views[i].subImage.imageRect.extent.width,
            source->views[i].subImage.imageRect.extent.height}},i,i,source->views[i].pose,source->views[i].fov,full});
    }
    bool rendered=true; HRESULT renderResult=S_OK;
    for(uint32_t i=0;i<viewCount;++i) {
        D3D11_RENDER_TARGET_VIEW_DESC rd{};
        rd.Format=topmostDesc.Format;
        if(rd.Format==DXGI_FORMAT_R8G8B8A8_TYPELESS || rd.Format==DXGI_FORMAT_B8G8R8A8_TYPELESS)
            rd.Format=(DXGI_FORMAT)g_topmostLayer.format;
        rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
        rd.Texture2DArray.FirstArraySlice=i; rd.Texture2DArray.ArraySize=1;
        ID3D11RenderTargetView* rtv=nullptr;
        renderResult=g_d3d11Mask.device->CreateRenderTargetView(tex,&rd,&rtv);
        if(FAILED(renderResult)||!rtv) { rendered=false; break; }
        const float clear[4]={0,0,0,0};
        g_d3d11Mask.context->ClearRenderTargetView(rtv,clear);
        if(maskEnabled) DrawVisorBorderToTexture(tex,2,g_topmostLayer.format,eyes[i],eyes,rtv);
        DrawCalibrationPatternsToTexture(tex,2,g_topmostLayer.format,eyes[i],eyes,rtv,false,true);
        rtv->Release();
    }
    if(rendered) g_d3d11Mask.context->Flush();
    XrSwapchainImageReleaseInfo ri{XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO}; const XrResult releaseResult=nextXrReleaseSwapchainImage(g_topmostLayer.swapchain,&ri);
    if(!rendered) { BlockTopmostLayer("CreateRenderTargetView", renderResult); return false; }
    if(XR_FAILED(releaseResult)) { BlockTopmostLayer("xrReleaseSwapchainImage", releaseResult); return false; }
    if (!RendererDeviceHealthy("RenderTopmostLayer(end)")) { BlockTopmostLayer("device removed after render", DXGI_ERROR_DEVICE_REMOVED); return false; }
    out.layer.layerFlags=XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT;
    out.layer.space=source->space; out.layer.viewCount=viewCount; out.layer.views=out.views.data();
    for(uint32_t i=0;i<viewCount;++i) {
        out.views[i]={XR_TYPE_COMPOSITION_LAYER_PROJECTION_VIEW};
        out.views[i].pose=source->views[i].pose; out.views[i].fov=source->views[i].fov;
        out.views[i].subImage.swapchain=g_topmostLayer.swapchain;
        out.views[i].subImage.imageRect=XrRect2Di{{0,0},{source->views[i].subImage.imageRect.extent.width,
            source->views[i].subImage.imageRect.extent.height}};
        out.views[i].subImage.imageArrayIndex=i;
    }
    return true;
}

#pragma pack(push, 4)
struct ObsMirrorSurfaceData {
    uint32_t lastProcessedIndex, frameNumber, eyeIndex;
    float overlap, blend, blendPos;
    uint64_t sharedHandle[3];
};
#pragma pack(pop)
static_assert(sizeof(ObsMirrorSurfaceData) == 48, "OpenXR OBS Mirror surface contract size");
HANDLE g_obsMirrorSurfaceMap = nullptr;
const ObsMirrorSurfaceData* g_obsMirrorSurface = nullptr;
ID3D11Texture2D* g_obsMirrorTexture = nullptr;
uint64_t g_obsMirrorTextureHandle = 0;
uint64_t g_obsMirrorNextConnectTick = 0;
std::atomic<bool> g_obsMirrorConnectedLogged{false};

void DisconnectObsMirrorSurface() {
    if (g_obsMirrorTexture) g_obsMirrorTexture->Release();
    if (g_obsMirrorSurface) UnmapViewOfFile(g_obsMirrorSurface);
    if (g_obsMirrorSurfaceMap) CloseHandle(g_obsMirrorSurfaceMap);
    g_obsMirrorTexture = nullptr; g_obsMirrorTextureHandle = 0;
    g_obsMirrorSurface = nullptr; g_obsMirrorSurfaceMap = nullptr;
    g_obsMirrorNextConnectTick = 0; g_obsMirrorConnectedLogged.store(false);
}

bool EnsureObsMirrorSurfaceTexture() {
    if (!g_obsMirrorSurface) {
        const uint64_t now = GetTickCount64();
        if (now < g_obsMirrorNextConnectTick) return false;
        g_obsMirrorNextConnectTick = now + 1000;
        g_obsMirrorSurfaceMap = OpenFileMappingW(FILE_MAP_READ, FALSE, L"OpenXROBSMirrorSurface");
        if (!g_obsMirrorSurfaceMap) return false;
        g_obsMirrorSurface = static_cast<const ObsMirrorSurfaceData*>(MapViewOfFile(
            g_obsMirrorSurfaceMap, FILE_MAP_READ, 0, 0, sizeof(ObsMirrorSurfaceData)));
        if (!g_obsMirrorSurface) {
            CloseHandle(g_obsMirrorSurfaceMap); g_obsMirrorSurfaceMap = nullptr; return false;
        }
    }
    const ObsMirrorSurfaceData snapshot = *g_obsMirrorSurface;
    MemoryBarrier();
    if (snapshot.eyeIndex > 2 || !std::isfinite(snapshot.overlap) || snapshot.overlap < 0.f ||
        snapshot.overlap > 100.f || snapshot.sharedHandle[0] == 0) return false;
    if (g_obsMirrorTexture && g_obsMirrorTextureHandle == snapshot.sharedHandle[0]) return true;
    if (g_obsMirrorTexture) { g_obsMirrorTexture->Release(); g_obsMirrorTexture = nullptr; }
    g_obsMirrorTextureHandle = 0;
    ID3D11Texture2D* texture = nullptr;
    const HRESULT opened = g_d3d11Mask.device->OpenSharedResource(
        reinterpret_cast<HANDLE>(static_cast<uintptr_t>(snapshot.sharedHandle[0])),
        __uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&texture));
    if (FAILED(opened) || !texture) return false;
    D3D11_TEXTURE2D_DESC desc{}; texture->GetDesc(&desc);
    if (desc.Width == 0 || desc.Height == 0 || desc.ArraySize != 1 ||
        (desc.BindFlags & D3D11_BIND_RENDER_TARGET) == 0) {
        // Truthful diagnostic: the game frame still reaches OBS (the mirror layer copies it), but
        // ViewLab cannot draw overlays onto a shared surface that is not render-targetable. This is
        // the usual reason "OpenXR Mirror Capture shows the game but no ViewLab overlays". Logged
        // once so the cause is visible rather than a silent no-op.
        static std::atomic<bool> loggedNonRt{false};
        if ((desc.BindFlags & D3D11_BIND_RENDER_TARGET) == 0 && !loggedNonRt.exchange(true))
            Log("OBS mirror route: shared surface %ux%u is not render-targetable (bindFlags=0x%X); "
                "ViewLab overlays cannot be composited into OpenXR Mirror Capture until the mirror "
                "surface exposes D3D11_BIND_RENDER_TARGET\n", desc.Width, desc.Height, desc.BindFlags);
        texture->Release(); return false;
    }
    g_obsMirrorTexture = texture; g_obsMirrorTextureHandle = snapshot.sharedHandle[0];
    if (!g_obsMirrorConnectedLogged.exchange(true))
        Log("OBS mirror route: connected to OpenXROBSMirrorSurface (%ux%u format=%d eyeMode=%u)\n",
            desc.Width, desc.Height, static_cast<int>(desc.Format), snapshot.eyeIndex);
    return true;
}

// Draws the checkbox-selected ViewLab features onto the OpenXR-OBSMirror shared display
// texture. Capture-point contract (from the MIT-licensed Jabbah/OpenXR-Layer-OBSMirror
// source): the mirror layer blends the frame into its private compositor at xrEndFrame,
// queues compositor->sharedHandle[0] in copyToMirror(), and only Flush()es that copy at the
// NEXT xrBeginFrame, which is also when lastProcessedIndex advances. Drawing after
// xrEndFrame therefore gets overwritten at the start of every frame and OBS almost never
// samples the overlaid texture. This must be called from xrBeginFrame AFTER the call has
// passed down the chain, so the mirror's copy is already submitted and ViewLab's drawing
// stays on the displayed texture for the whole frame in either implicit-layer order.
void DrawObsMirrorSurface() {
    if (obsMirrorVisibilityMask == 0 ||
        !g_d3d11Mask.device || !g_d3d11Mask.context || !EnsureObsMirrorSurfaceTexture()) return;
    const ObsMirrorSurfaceData surface = *g_obsMirrorSurface;
    D3D11_TEXTURE2D_DESC desc{}; g_obsMirrorTexture->GetDesc(&desc);
    std::vector<EyeView> sourceViews = g_primaryProjectionContext.topology.AllViews();
    if (sourceViews.empty()) return;
    std::sort(sourceViews.begin(), sourceViews.end(), [](const EyeView& a, const EyeView& b) {
        return a.viewIndex < b.viewIndex;
    });
    std::vector<EyeView> mirrorViews;
    if (surface.eyeIndex < 2) {
        auto selected = std::find_if(sourceViews.begin(), sourceViews.end(), [&](const EyeView& eye) {
            return eye.viewIndex == surface.eyeIndex;
        });
        EyeView eye = selected != sourceViews.end() ? *selected : sourceViews.front();
        eye.rect = {{0, 0}, {static_cast<int32_t>(desc.Width), static_cast<int32_t>(desc.Height)}};
        eye.arraySlice = 0; mirrorViews.push_back(eye);
    } else {
        const int32_t eyeWidth = (std::min)(sourceViews.front().rect.extent.width, static_cast<int32_t>(desc.Width));
        const int32_t offset = static_cast<int32_t>(std::lround(eyeWidth * surface.overlap / 100.f));
        for (size_t i = 0; i < sourceViews.size() && i < 2; ++i) {
            EyeView eye = sourceViews[i]; eye.arraySlice = 0;
            eye.rect = {{i == 0 ? 0 : offset, 0},
                {(std::min)(eyeWidth, static_cast<int32_t>(desc.Width) - (i == 0 ? 0 : offset)), static_cast<int32_t>(desc.Height)}};
            if (eye.rect.extent.width > 0) mirrorViews.push_back(eye);
        }
    }
    for (const EyeView& eye : mirrorViews) {
        if (maskEnabled && IncludesMirrorFeature(obsMirrorVisibilityMask, MirrorVisor))
            DrawVisorBorderToTexture(g_obsMirrorTexture, 1, desc.Format, eye, mirrorViews, nullptr);
        DrawCalibrationPatternsToTexture(g_obsMirrorTexture, 1, desc.Format, eye, mirrorViews, nullptr,
            false, true, obsMirrorVisibilityMask);
    }
    g_d3d11Mask.context->Flush();
}

// ---- Experimental calibration-suite left-eye capture -------------------------------------
// The settings app owns a small read/write control block ("XRViewLabCalibrationCapture").
// While a session is submitting frames the layer stamps a heartbeat tick; when the UI
// advances requestSerial the layer copies the final submitted left-eye sub-image (game
// pixels + crop + every direct-drawn ViewLab feature; ordered-carrier features are
// composited onto the copy) into a staging texture on the render thread, then encodes a
// real PNG plus a JSON metadata sidecar on a worker thread. No desktop capture, OBS,
// ReShade or UI automation is involved and no placeholder file is ever produced.
#include <wincodec.h>
#pragma comment(lib, "windowscodecs.lib")
#pragma comment(lib, "ole32.lib")

#pragma pack(push, 4)
struct CalibrationCaptureBlock {
    uint32_t version;         // 1
    uint32_t requestSerial;   // UI increments to request one capture of the current left eye
    uint32_t completedSerial; // layer sets equal to requestSerial when the request finishes
    int32_t  resultCode;      // 0 ok; see CalibrationCaptureResult codes below
    uint64_t heartbeatTick;   // GetTickCount64 stamped every xrEndFrame of the active session
    uint32_t cancelledSerial;
    uint32_t reserved;
    wchar_t  requestStem[96];
    wchar_t  resultPath[512];
};
#pragma pack(pop)
static_assert(sizeof(CalibrationCaptureBlock) == 16 + 8 + 8 + 96 * 2 + 512 * 2,
    "calibration capture control-block contract");

// Result codes shared with the UI backend: 0 ok, 1 no projection context, 2 unsupported
// texture format, 3 renderer unavailable, 4 encode/write failed, 5 no left-eye view,
// 6 GPU copy/map failed, 7 cancelled.
HANDLE g_calibCaptureMap = nullptr;
CalibrationCaptureBlock* g_calibCapture = nullptr;
uint64_t g_calibCaptureNextConnectTick = 0;
uint32_t g_calibCaptureProcessedSerial = 0;

void ConnectCalibrationCapture() {
    if (g_calibCapture) return;
    const uint64_t now = GetTickCount64();
    if (now < g_calibCaptureNextConnectTick) return;
    g_calibCaptureNextConnectTick = now + 1000;
    g_calibCaptureMap = OpenFileMappingW(FILE_MAP_ALL_ACCESS, FALSE, L"XRViewLabCalibrationCapture");
    if (!g_calibCaptureMap) return;
    g_calibCapture = static_cast<CalibrationCaptureBlock*>(MapViewOfFile(
        g_calibCaptureMap, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(CalibrationCaptureBlock)));
    if (!g_calibCapture) {
        CloseHandle(g_calibCaptureMap); g_calibCaptureMap = nullptr; return;
    }
    if (g_calibCapture->version != 1) {
        UnmapViewOfFile(g_calibCapture); CloseHandle(g_calibCaptureMap);
        g_calibCapture = nullptr; g_calibCaptureMap = nullptr; return;
    }
    // Requests raised before this session connected are stale; the UI gates on the heartbeat
    // before submitting real requests, so dropping them can never lose a live capture.
    g_calibCaptureProcessedSerial = g_calibCapture->requestSerial;
    Log("calibration capture: connected to UI control block\n");
}

struct CalibrationCapturePixels {
    uint32_t width = 0, height = 0, rowPitch = 0;
    DXGI_FORMAT format = DXGI_FORMAT_UNKNOWN;
    std::vector<uint8_t> data;
    std::wstring stem;
    XrFovf fov{};
    XrFovf fullFov{};
    bool orderedCarrierComposited = false;
    int64_t swapchainFormat = 0;
};

DXGI_FORMAT ResolveCaptureReadFormat(int64_t scFormat, DXGI_FORMAT descFormat) {
    DXGI_FORMAT format = static_cast<DXGI_FORMAT>(scFormat);
    switch (format) {
    case DXGI_FORMAT_R8G8B8A8_TYPELESS: return DXGI_FORMAT_R8G8B8A8_UNORM;
    case DXGI_FORMAT_B8G8R8A8_TYPELESS: return DXGI_FORMAT_B8G8R8A8_UNORM;
    case DXGI_FORMAT_R16G16B16A16_TYPELESS: return DXGI_FORMAT_R16G16B16A16_FLOAT;
    case DXGI_FORMAT_R10G10B10A2_TYPELESS: return DXGI_FORMAT_R10G10B10A2_UNORM;
    case DXGI_FORMAT_UNKNOWN: break;
    default: return format;
    }
    switch (descFormat) {
    case DXGI_FORMAT_R8G8B8A8_TYPELESS: return DXGI_FORMAT_R8G8B8A8_UNORM;
    case DXGI_FORMAT_B8G8R8A8_TYPELESS: return DXGI_FORMAT_B8G8R8A8_UNORM;
    case DXGI_FORMAT_R16G16B16A16_TYPELESS: return DXGI_FORMAT_R16G16B16A16_FLOAT;
    case DXGI_FORMAT_R10G10B10A2_TYPELESS: return DXGI_FORMAT_R10G10B10A2_UNORM;
    default: return descFormat;
    }
}

inline float CaptureHalfToFloat(uint16_t half) {
    const uint32_t sign = (half & 0x8000u) << 16;
    uint32_t exponent = (half >> 10) & 0x1Fu;
    uint32_t mantissa = half & 0x3FFu;
    uint32_t bits;
    if (exponent == 0) {
        if (mantissa == 0) { bits = sign; }
        else {
            exponent = 127 - 15 + 1;
            while ((mantissa & 0x400u) == 0) { mantissa <<= 1; --exponent; }
            mantissa &= 0x3FFu;
            bits = sign | (exponent << 23) | (mantissa << 13);
        }
    } else if (exponent == 0x1Fu) {
        bits = sign | 0x7F800000u | (mantissa << 13);
    } else {
        bits = sign | ((exponent - 15 + 127) << 23) | (mantissa << 13);
    }
    float value;
    std::memcpy(&value, &bits, sizeof(value));
    return value;
}

inline uint8_t CaptureLinearToSrgb8(float v) {
    v = (std::min)(1.0f, (std::max)(0.0f, v));
    const float srgb = v <= 0.0031308f ? v * 12.92f : 1.055f * std::pow(v, 1.0f / 2.4f) - 0.055f;
    return static_cast<uint8_t>(std::lround(srgb * 255.0f));
}

bool ConvertCapturePixelsToBgra(const CalibrationCapturePixels& in, std::vector<uint8_t>& bgra) {
    bgra.assign(static_cast<size_t>(in.width) * in.height * 4, 0);
    for (uint32_t y = 0; y < in.height; ++y) {
        const uint8_t* row = in.data.data() + static_cast<size_t>(y) * in.rowPitch;
        uint8_t* out = bgra.data() + static_cast<size_t>(y) * in.width * 4;
        switch (in.format) {
        case DXGI_FORMAT_R8G8B8A8_UNORM:
        case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB:
            for (uint32_t x = 0; x < in.width; ++x) {
                out[x * 4 + 0] = row[x * 4 + 2]; out[x * 4 + 1] = row[x * 4 + 1];
                out[x * 4 + 2] = row[x * 4 + 0]; out[x * 4 + 3] = 255;
            }
            break;
        case DXGI_FORMAT_B8G8R8A8_UNORM:
        case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB:
            for (uint32_t x = 0; x < in.width; ++x) {
                out[x * 4 + 0] = row[x * 4 + 0]; out[x * 4 + 1] = row[x * 4 + 1];
                out[x * 4 + 2] = row[x * 4 + 2]; out[x * 4 + 3] = 255;
            }
            break;
        case DXGI_FORMAT_R10G10B10A2_UNORM:
            for (uint32_t x = 0; x < in.width; ++x) {
                uint32_t v; std::memcpy(&v, row + x * 4, 4);
                out[x * 4 + 0] = static_cast<uint8_t>(((v >> 20) & 0x3FFu) >> 2);
                out[x * 4 + 1] = static_cast<uint8_t>(((v >> 10) & 0x3FFu) >> 2);
                out[x * 4 + 2] = static_cast<uint8_t>((v & 0x3FFu) >> 2);
                out[x * 4 + 3] = 255;
            }
            break;
        case DXGI_FORMAT_R16G16B16A16_FLOAT:
            for (uint32_t x = 0; x < in.width; ++x) {
                const uint16_t* px = reinterpret_cast<const uint16_t*>(row + x * 8);
                out[x * 4 + 0] = CaptureLinearToSrgb8(CaptureHalfToFloat(px[2]));
                out[x * 4 + 1] = CaptureLinearToSrgb8(CaptureHalfToFloat(px[1]));
                out[x * 4 + 2] = CaptureLinearToSrgb8(CaptureHalfToFloat(px[0]));
                out[x * 4 + 3] = 255;
            }
            break;
        default:
            return false;
        }
    }
    return true;
}

bool SaveCapturePngBgra(const std::filesystem::path& path, uint32_t width, uint32_t height,
    std::vector<uint8_t>& bgra) {
    IWICImagingFactory* factory = nullptr;
    IWICBitmapEncoder* encoder = nullptr;
    IWICBitmapFrameEncode* frame = nullptr;
    IWICStream* stream = nullptr;
    bool ok = false;
    HRESULT hr = CoCreateInstance(CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER,
        IID_PPV_ARGS(&factory));
    if (SUCCEEDED(hr)) hr = factory->CreateStream(&stream);
    if (SUCCEEDED(hr)) hr = stream->InitializeFromFilename(path.c_str(), GENERIC_WRITE);
    if (SUCCEEDED(hr)) hr = factory->CreateEncoder(GUID_ContainerFormatPng, nullptr, &encoder);
    if (SUCCEEDED(hr)) hr = encoder->Initialize(stream, WICBitmapEncoderNoCache);
    if (SUCCEEDED(hr)) hr = encoder->CreateNewFrame(&frame, nullptr);
    if (SUCCEEDED(hr)) hr = frame->Initialize(nullptr);
    if (SUCCEEDED(hr)) hr = frame->SetSize(width, height);
    WICPixelFormatGUID pixelFormat = GUID_WICPixelFormat32bppBGRA;
    if (SUCCEEDED(hr)) hr = frame->SetPixelFormat(&pixelFormat);
    if (SUCCEEDED(hr)) hr = frame->WritePixels(height, width * 4,
        static_cast<UINT>(bgra.size()), bgra.data());
    if (SUCCEEDED(hr)) hr = frame->Commit();
    if (SUCCEEDED(hr)) hr = encoder->Commit();
    ok = SUCCEEDED(hr);
    if (frame) frame->Release();
    if (encoder) encoder->Release();
    if (stream) stream->Release();
    if (factory) factory->Release();
    if (!ok) Log("calibration capture: PNG encode failed hr=0x%08X for %ls\n",
        static_cast<unsigned>(hr), path.c_str());
    return ok;
}

bool WriteCaptureMetadata(const std::filesystem::path& path, const CalibrationCapturePixels& p,
    const std::wstring& pngName) {
    wchar_t exePath[MAX_PATH]{};
    GetModuleFileNameW(nullptr, exePath, MAX_PATH);
    SYSTEMTIME st{}; GetSystemTime(&st);
    std::ofstream meta(path, std::ios::binary | std::ios::trunc);
    if (!meta) return false;
    auto narrow = [](const std::wstring& w) {
        std::string s; s.reserve(w.size());
        for (wchar_t c : w) { if (c == L'\\') { s += "\\\\"; } else if (c == L'"') { s += "\\\""; } else if (c < 128) { s += static_cast<char>(c); } else { s += '?'; } }
        return s;
    };
    char iso[40]{};
    std::snprintf(iso, sizeof(iso), "%04u-%02u-%02uT%02u:%02u:%02u.%03uZ",
        st.wYear, st.wMonth, st.wDay, st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    meta << "{\n"
        << "  \"pattern\": \"" << narrow(p.stem) << "\",\n"
        << "  \"image\": \"" << narrow(pngName) << "\",\n"
        << "  \"capturedUtc\": \"" << iso << "\",\n"
        << "  \"application\": \"" << narrow(exePath) << "\",\n"
        << "  \"eye\": \"left\",\n"
        << "  \"width\": " << p.width << ",\n"
        << "  \"height\": " << p.height << ",\n"
        << "  \"dxgiSwapchainFormat\": " << p.swapchainFormat << ",\n"
        << "  \"dxgiReadFormat\": " << static_cast<int>(p.format) << ",\n"
        << "  \"submittedFov\": { \"left\": " << p.fov.angleLeft << ", \"right\": " << p.fov.angleRight
        << ", \"up\": " << p.fov.angleUp << ", \"down\": " << p.fov.angleDown << " },\n"
        << "  \"fullFov\": { \"left\": " << p.fullFov.angleLeft << ", \"right\": " << p.fullFov.angleRight
        << ", \"up\": " << p.fullFov.angleUp << ", \"down\": " << p.fullFov.angleDown << " },\n"
        << "  \"orderedCarrierComposited\": " << (p.orderedCarrierComposited ? "true" : "false") << ",\n"
        << "  \"source\": \"ViewLab submitted left-eye texture at xrEndFrame\"\n"
        << "}\n";
    meta.flush();
    return meta.good();
}

int32_t CaptureLeftEyePixels(const std::wstring& stem, CalibrationCapturePixels& out) {
    if (!g_d3d11Mask.initialized || !g_d3d11Mask.device || !g_d3d11Mask.context ||
        g_rendererDeviceLost.load(std::memory_order_acquire)) return 3;
    ID3D11Texture2D* sourceTex = nullptr;
    EyeView leftEye{};
    std::vector<EyeView> projectionViews;
    int64_t scFormat = 0;
    {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        if (!g_primaryProjectionContext.ValidFor(g_d3d11Mask.session)) return 1;
        projectionViews = g_primaryProjectionContext.topology.AllViews();
        bool found = false;
        for (const auto& kv : g_swapchains) {
            const TrackedSwapchain& ts = kv.second;
            if (ts.session != g_d3d11Mask.session || ts.lastAcquiredIndex >= ts.textures.size()) continue;
            for (const EyeView& ev : g_primaryProjectionContext.topology.TargetsFor(kv.first)) {
                if (ev.viewIndex != 0) continue;
                sourceTex = ts.textures[ts.lastAcquiredIndex];
                if (sourceTex) sourceTex->AddRef();
                leftEye = ev;
                scFormat = ts.format;
                found = true;
                break;
            }
            if (found) break;
        }
        if (!found || !sourceTex) return 5;
    }
    const uint32_t width = static_cast<uint32_t>((std::max)(0, leftEye.rect.extent.width));
    const uint32_t height = static_cast<uint32_t>((std::max)(0, leftEye.rect.extent.height));
    if (width == 0 || height == 0) { sourceTex->Release(); return 5; }
    D3D11_TEXTURE2D_DESC srcDesc{}; sourceTex->GetDesc(&srcDesc);
    const DXGI_FORMAT readFormat = ResolveCaptureReadFormat(scFormat, srcDesc.Format);

    D3D11_TEXTURE2D_DESC copyDesc{};
    copyDesc.Width = width; copyDesc.Height = height;
    copyDesc.MipLevels = 1; copyDesc.ArraySize = 1;
    copyDesc.Format = srcDesc.Format;
    copyDesc.SampleDesc.Count = 1;
    copyDesc.Usage = D3D11_USAGE_DEFAULT;
    copyDesc.BindFlags = D3D11_BIND_RENDER_TARGET;
    ID3D11Texture2D* copyTex = nullptr;
    HRESULT hr = g_d3d11Mask.device->CreateTexture2D(&copyDesc, nullptr, &copyTex);
    if (FAILED(hr) || !copyTex) { sourceTex->Release(); return 6; }
    D3D11_BOX box{};
    box.left = static_cast<UINT>((std::max)(0, leftEye.rect.offset.x));
    box.top = static_cast<UINT>((std::max)(0, leftEye.rect.offset.y));
    box.right = box.left + width; box.bottom = box.top + height;
    box.front = 0; box.back = 1;
    const UINT sourceSubresource = D3D11CalcSubresource(0, leftEye.arraySlice, srcDesc.MipLevels);
    g_d3d11Mask.context->CopySubresourceRegion(copyTex, 0, 0, 0, 0, sourceTex, sourceSubresource, &box);
    sourceTex->Release();

    // Ordered-carrier frames keep the game texture free of common features; composite the
    // same content onto the copy so the capture matches what the headset compositor shows.
    EyeView copyEye = leftEye;
    copyEye.rect = { {0, 0}, {static_cast<int32_t>(width), static_cast<int32_t>(height)} };
    copyEye.arraySlice = 0;
    bool composited = false;
    if (maskEnabled && !g_featurePresentationPlan.drawDirectVisor) {
        DrawVisorBorderToTexture(copyTex, 1, scFormat, copyEye, projectionViews, nullptr);
        composited = true;
    }
    if (!g_featurePresentationPlan.drawDirectCommonFeatures) {
        DrawCalibrationPatternsToTexture(copyTex, 1, scFormat, copyEye, projectionViews, nullptr,
            false, true);
        composited = true;
    }

    D3D11_TEXTURE2D_DESC stagingDesc = copyDesc;
    stagingDesc.BindFlags = 0;
    stagingDesc.Usage = D3D11_USAGE_STAGING;
    stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ;
    ID3D11Texture2D* staging = nullptr;
    hr = g_d3d11Mask.device->CreateTexture2D(&stagingDesc, nullptr, &staging);
    if (FAILED(hr) || !staging) { copyTex->Release(); return 6; }
    g_d3d11Mask.context->CopyResource(staging, copyTex);
    copyTex->Release();
    D3D11_MAPPED_SUBRESOURCE mapped{};
    hr = g_d3d11Mask.context->Map(staging, 0, D3D11_MAP_READ, 0, &mapped);
    if (FAILED(hr)) { staging->Release(); return 6; }
    out.width = width; out.height = height;
    out.rowPitch = mapped.RowPitch;
    out.format = readFormat;
    out.swapchainFormat = scFormat;
    out.stem = stem;
    out.fov = leftEye.fov;
    out.fullFov = leftEye.fullFov;
    out.orderedCarrierComposited = composited;
    out.data.resize(static_cast<size_t>(mapped.RowPitch) * height);
    std::memcpy(out.data.data(), mapped.pData, out.data.size());
    g_d3d11Mask.context->Unmap(staging, 0);
    staging->Release();
    return 0;
}

void CompleteCalibrationCapture(uint32_t serial, int32_t code, const std::wstring& path) {
    if (!g_calibCapture) return;
    wmemset(g_calibCapture->resultPath, 0, std::size(g_calibCapture->resultPath));
    if (!path.empty()) {
        const size_t count = (std::min)(path.size(), std::size(g_calibCapture->resultPath) - 1);
        wmemcpy(g_calibCapture->resultPath, path.c_str(), count);
    }
    g_calibCapture->resultCode = code;
    MemoryBarrier();
    g_calibCapture->completedSerial = serial;
}

bool CalibrationCaptureCancelled(uint32_t serial) {
    MemoryBarrier();
    return g_calibCapture && g_calibCapture->cancelledSerial == serial;
}

void ProcessCalibrationCaptureRequest() {
    ConnectCalibrationCapture();
    if (!g_calibCapture) return;
    g_calibCapture->heartbeatTick = GetTickCount64();
    const uint32_t requested = g_calibCapture->requestSerial;
    if (requested == g_calibCaptureProcessedSerial) return;
    g_calibCaptureProcessedSerial = requested;
    wchar_t stemBuffer[96]{};
    wmemcpy(stemBuffer, g_calibCapture->requestStem, std::size(stemBuffer) - 1);
    std::wstring stem(stemBuffer);
    // Only a safe file-name stem is accepted; anything else falls back to a fixed name.
    for (wchar_t& c : stem) {
        if (c == L'\\' || c == L'/' || c == L':' || c == L'*' || c == L'?' || c == L'"' ||
            c == L'<' || c == L'>' || c == L'|') c = L'-';
    }
    if (stem.empty()) stem = L"capture";
    if (CalibrationCaptureCancelled(requested)) {
        CompleteCalibrationCapture(requested, 7, L"");
        return;
    }
    CalibrationCapturePixels pixels;
    const int32_t code = CaptureLeftEyePixels(stem, pixels);
    if (code != 0) {
        Log("calibration capture: request %u failed on render thread (code %d)\n", requested, code);
        CompleteCalibrationCapture(requested, code, L"");
        return;
    }
    std::thread([requested, pixels = std::move(pixels)]() mutable {
        const HRESULT coInit = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        int32_t workerCode = 0;
        std::wstring finalPath;
        std::filesystem::path pngPath, metadataPath;
        std::error_code ec;
        std::vector<uint8_t> bgra;
        if (CalibrationCaptureCancelled(requested)) {
            workerCode = 7;
        } else if (!ConvertCapturePixelsToBgra(pixels, bgra)) {
            Log("calibration capture: unsupported texture format %d\n", static_cast<int>(pixels.format));
            workerCode = 2;
        } else {
            const std::filesystem::path directory = UserDataDirectory() / L"CalibrationCaptures";
            std::filesystem::create_directories(directory, ec);
            SYSTEMTIME lt{}; GetLocalTime(&lt);
            wchar_t suffix[32]{};
            std::swprintf(suffix, std::size(suffix), L"-%04u%02u%02u-%02u%02u%02u",
                lt.wYear, lt.wMonth, lt.wDay, lt.wHour, lt.wMinute, lt.wSecond);
            const std::wstring baseName = pixels.stem + suffix;
            pngPath = directory / (baseName + L".png");
            metadataPath = directory / (baseName + L".json");
            if (!SaveCapturePngBgra(pngPath, pixels.width, pixels.height, bgra)) {
                workerCode = 4;
            } else if (!WriteCaptureMetadata(metadataPath, pixels, baseName + L".png")) {
                Log("calibration capture: metadata write failed for %ls\n", pngPath.c_str());
                std::filesystem::remove(pngPath, ec);
                workerCode = 4;
            } else {
                finalPath = pngPath.wstring();
            }
        }
        if (CalibrationCaptureCancelled(requested)) {
            if (!pngPath.empty()) std::filesystem::remove(pngPath, ec);
            if (!metadataPath.empty()) std::filesystem::remove(metadataPath, ec);
            finalPath.clear();
            workerCode = 7;
        } else if (workerCode == 0) {
            Log("calibration capture: saved %ux%u left-eye image to %ls\n",
                pixels.width, pixels.height, finalPath.c_str());
        }
        CompleteCalibrationCapture(requested, workerCode, finalPath);
        if (SUCCEEDED(coInit)) CoUninitialize();
    }).detach();
}

// ---- ViewLab Media Capture (VLMC) producer ------------------------------------------------
// Publishes the final submitted eye (game pixels PLUS every ViewLab feature) into a shared,
// triple-buffered ring of D3D11 textures plus a control block that the VLMC OBS source
// (ViewLabMirrorPlugin, id "viewlab_mirror_capture") opens read-only. Because ViewLab owns
// the producer, nothing overwrites its drawing — the defect that makes the third-party
// OpenXR-OBSMirror route drop ViewLab overlays cannot occur here. Contract mirrors
// ViewLabMirrorPlugin/viewlab_mirror_contract.h exactly (same name, magic, layout).
#pragma pack(push, 4)
struct ViewLabMirrorSurface {
    uint32_t magic;           // 'VLMS'
    uint32_t version;         // 1
    uint32_t frameNumber;     // incremented after each completed ring copy
    uint32_t displayIndex;    // 0..2: index of the last completed ring texture
    uint32_t width, height;   // dimensions of every ring texture
    uint32_t format;          // DXGI_FORMAT of the ring textures
    uint32_t eyeMode;         // mode actually published this frame (0 left, 1 right, 2 SbS)
    uint64_t heartbeatTick;   // GetTickCount64 stamped every produced frame
    uint64_t sharedHandle[3]; // legacy D3D11 shared handles of the ring textures
    uint32_t requestedEyeMode;     // v2: consumer writes the eye mode the user selected
    uint32_t consumerHeartbeatTick;// v2: low32 GetTickCount64 the OBS source stamps each render
};
#pragma pack(pop)
static_assert(sizeof(ViewLabMirrorSurface) == 72, "VLMC mirror surface contract size (v2)");

static const wchar_t* kViewLabMirrorSurfaceName = L"Local\\XRViewLabMirrorSurface";
static const uint32_t kViewLabMirrorMagic = 0x534D4C56u; // 'VLMS'
static const uint32_t kViewLabMirrorVersion = 2u;

HANDLE g_vlmcMap = nullptr;
ViewLabMirrorSurface* g_vlmcSurface = nullptr;
ID3D11Texture2D* g_vlmcRing[3] = {};
uint64_t g_vlmcRingHandle[3] = {};
uint32_t g_vlmcRingW = 0, g_vlmcRingH = 0;
DXGI_FORMAT g_vlmcRingFmt = DXGI_FORMAT_UNKNOWN;
uint32_t g_vlmcFrameNumber = 0;
std::atomic<bool> g_vlmcConnectedLogged{false};

void ReleaseViewLabMirrorRing() {
    for (int i = 0; i < 3; ++i) {
        if (g_vlmcRing[i]) g_vlmcRing[i]->Release();
        g_vlmcRing[i] = nullptr; g_vlmcRingHandle[i] = 0;
    }
    g_vlmcRingW = g_vlmcRingH = 0; g_vlmcRingFmt = DXGI_FORMAT_UNKNOWN;
}

void DisconnectViewLabMirror() {
    ReleaseViewLabMirrorRing();
    if (g_vlmcSurface) UnmapViewOfFile(g_vlmcSurface);
    if (g_vlmcMap) CloseHandle(g_vlmcMap);
    g_vlmcSurface = nullptr; g_vlmcMap = nullptr;
    g_vlmcFrameNumber = 0; g_vlmcConnectedLogged.store(false);
}

// Create (once) the shared control block. The producer owns it, so it is created read/write.
bool EnsureViewLabMirrorSurface() {
    if (g_vlmcSurface) return true;
    g_vlmcMap = CreateFileMappingW(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE, 0,
        sizeof(ViewLabMirrorSurface), kViewLabMirrorSurfaceName);
    if (!g_vlmcMap) return false;
    g_vlmcSurface = static_cast<ViewLabMirrorSurface*>(MapViewOfFile(
        g_vlmcMap, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(ViewLabMirrorSurface)));
    if (!g_vlmcSurface) { CloseHandle(g_vlmcMap); g_vlmcMap = nullptr; return false; }
    std::memset(g_vlmcSurface, 0, sizeof(ViewLabMirrorSurface));
    g_vlmcSurface->magic = kViewLabMirrorMagic;
    g_vlmcSurface->version = kViewLabMirrorVersion;
    return true;
}

// (Re)create the triple-buffered ring of shared textures when the output size/format changes,
// and publish the new dimensions/handles into the control block. Legacy MISC_SHARED handles so
// the OBS source can open them with gs_texture_open_shared on its own D3D11 device.
bool EnsureViewLabMirrorRing(uint32_t width, uint32_t height, DXGI_FORMAT fmt) {
    if (g_vlmcRing[0] && g_vlmcRingW == width && g_vlmcRingH == height && g_vlmcRingFmt == fmt)
        return true;
    ReleaseViewLabMirrorRing();
    for (int i = 0; i < 3; ++i) {
        D3D11_TEXTURE2D_DESC d{};
        d.Width = width; d.Height = height; d.MipLevels = 1; d.ArraySize = 1;
        d.Format = fmt; d.SampleDesc.Count = 1; d.Usage = D3D11_USAGE_DEFAULT;
        d.BindFlags = D3D11_BIND_RENDER_TARGET | D3D11_BIND_SHADER_RESOURCE;
        d.MiscFlags = D3D11_RESOURCE_MISC_SHARED;
        if (FAILED(g_d3d11Mask.device->CreateTexture2D(&d, nullptr, &g_vlmcRing[i])) || !g_vlmcRing[i]) {
            ReleaseViewLabMirrorRing(); return false;
        }
        IDXGIResource* dxgi = nullptr;
        HANDLE shared = nullptr;
        if (SUCCEEDED(g_vlmcRing[i]->QueryInterface(__uuidof(IDXGIResource), reinterpret_cast<void**>(&dxgi))) && dxgi) {
            dxgi->GetSharedHandle(&shared);
            dxgi->Release();
        }
        if (!shared) { ReleaseViewLabMirrorRing(); return false; }
        g_vlmcRingHandle[i] = static_cast<uint64_t>(reinterpret_cast<uintptr_t>(shared));
    }
    g_vlmcRingW = width; g_vlmcRingH = height; g_vlmcRingFmt = fmt;
    // Publish geometry + handles before any displayIndex references them.
    g_vlmcSurface->width = width; g_vlmcSurface->height = height;
    g_vlmcSurface->format = static_cast<uint32_t>(fmt);
    g_vlmcSurface->eyeMode = 0;
    for (int i = 0; i < 3; ++i) g_vlmcSurface->sharedHandle[i] = g_vlmcRingHandle[i];
    MemoryBarrier();
    return true;
}

// Finds the submitted texture for a projection view index (0 left, 1 right) and AddRefs it into
// *outTex. Returns false if that eye is not currently submitted (mono titles submit view 0 only).
// Caller holds no lock; this takes g_swapchainMutex.
static bool AcquireSubmittedEye(uint32_t viewIndex, ID3D11Texture2D** outTex, EyeView* outEye,
                                int64_t* outFmt, std::vector<EyeView>* outAllViews) {
    std::lock_guard<std::mutex> lk(g_swapchainMutex);
    if (!g_primaryProjectionContext.ValidFor(g_d3d11Mask.session)) return false;
    if (outAllViews) *outAllViews = g_primaryProjectionContext.topology.AllViews();
    for (const auto& kv : g_swapchains) {
        const TrackedSwapchain& ts = kv.second;
        if (ts.session != g_d3d11Mask.session || ts.lastAcquiredIndex >= ts.textures.size()) continue;
        for (const EyeView& ev : g_primaryProjectionContext.topology.TargetsFor(kv.first)) {
            if (ev.viewIndex != viewIndex) continue;
            ID3D11Texture2D* tex = ts.textures[ts.lastAcquiredIndex];
            if (!tex) return false;
            tex->AddRef();
            *outTex = tex; *outEye = ev; *outFmt = ts.format;
            return true;
        }
    }
    return false;
}

// Copies one submitted eye's sub-rect into the ring texture at horizontal offset dstX.
static void VlmcCopyEye(ID3D11Texture2D* dst, uint32_t dstX, ID3D11Texture2D* src,
                        const EyeView& eye, uint32_t mipLevels, uint32_t w, uint32_t h) {
    D3D11_BOX box{};
    box.left = static_cast<UINT>((std::max)(0, eye.rect.offset.x));
    box.top = static_cast<UINT>((std::max)(0, eye.rect.offset.y));
    box.right = box.left + w; box.bottom = box.top + h;
    box.front = 0; box.back = 1;
    const UINT sub = D3D11CalcSubresource(0, eye.arraySlice, mipLevels);
    g_d3d11Mask.context->CopySubresourceRegion(dst, 0, dstX, 0, 0, src, sub, &box);
}

// Composites the checkbox-selected ViewLab features onto one eye's region of the ring, honouring
// the SAME "Show in OBS mirror" mask as the OXRMC route (obsMirrorVisibilityMask). When the direct
// plan already drew a feature into the submitted eye, the copied pixels already carry it. Callers
// skip this entirely when the mask is 0, giving a pure game-frame mirror (OXRMC-equivalent) with
// no extra draw passes.
static void VlmcComposeEye(ID3D11Texture2D* dst, DXGI_FORMAT ringFormat, const EyeView& eye,
                           uint32_t dstX, uint32_t w, uint32_t h, const std::vector<EyeView>& allViews) {
    EyeView copyEye = eye;
    copyEye.rect = { {static_cast<int32_t>(dstX), 0}, {static_cast<int32_t>(w), static_cast<int32_t>(h)} };
    copyEye.arraySlice = 0;
    if (maskEnabled && !g_featurePresentationPlan.drawDirectVisor &&
        IncludesMirrorFeature(obsMirrorVisibilityMask, MirrorVisor))
        DrawVisorBorderToTexture(dst, 1, static_cast<int64_t>(ringFormat), copyEye, allViews, nullptr);
    if (!g_featurePresentationPlan.drawDirectCommonFeatures)
        DrawCalibrationPatternsToTexture(dst, 1, static_cast<int64_t>(ringFormat), copyEye, allViews,
            nullptr, false, true, obsMirrorVisibilityMask);
}

// Called at xrEndFrame after the submitted textures hold the frame's complete direct-drawn
// content (same capture point the calibration suite uses). Publishes the eye(s) the consumer
// requested (0 left, 1 right, 2 side-by-side) into the next ring slot, compositing overlays and
// falling back to left when the requested eye is unavailable.
void ProduceViewLabMirrorFrame() {
    if (!g_d3d11Mask.initialized || !g_d3d11Mask.device || !g_d3d11Mask.context ||
        g_rendererDeviceLost.load(std::memory_order_acquire)) return;
    if (!EnsureViewLabMirrorSurface()) return;

    uint32_t requested = g_vlmcSurface->requestedEyeMode;
    if (requested > 2) requested = 0;

    // Primary eye: right for mode 1, otherwise left.
    ID3D11Texture2D* tex0 = nullptr; EyeView eye0{}; int64_t fmt0 = 0;
    std::vector<EyeView> allViews;
    const uint32_t primaryView = (requested == 1) ? 1u : 0u;
    if (!AcquireSubmittedEye(primaryView, &tex0, &eye0, &fmt0, &allViews)) {
        if (primaryView == 1 && AcquireSubmittedEye(0, &tex0, &eye0, &fmt0, &allViews)) requested = 0;
        else return;
    }
    // Second eye only for side-by-side; degrade to single left if the right eye is absent.
    ID3D11Texture2D* tex1 = nullptr; EyeView eye1{}; int64_t fmt1 = 0;
    if (requested == 2 && !AcquireSubmittedEye(1, &tex1, &eye1, &fmt1, nullptr)) requested = 0;

    const uint32_t w0 = static_cast<uint32_t>((std::max)(0, eye0.rect.extent.width));
    const uint32_t h0 = static_cast<uint32_t>((std::max)(0, eye0.rect.extent.height));
    if (w0 == 0 || h0 == 0) { tex0->Release(); if (tex1) tex1->Release(); return; }
    D3D11_TEXTURE2D_DESC d0{}; tex0->GetDesc(&d0);
    const DXGI_FORMAT ringFormat = ResolveCaptureReadFormat(fmt0, d0.Format);

    uint32_t outW = w0, outH = h0, w1 = 0, h1 = 0;
    if (requested == 2 && tex1) {
        w1 = static_cast<uint32_t>((std::max)(0, eye1.rect.extent.width));
        h1 = static_cast<uint32_t>((std::max)(0, eye1.rect.extent.height));
        if (w1 == 0 || h1 == 0) { requested = 0; tex1->Release(); tex1 = nullptr; }
        else { outW = w0 + w1; outH = (std::max)(h0, h1); }
    }

    // EnsureViewLabMirrorRing publishes the geometry + shared handles so the OBS source can
    // connect even before it starts rendering. Only the expensive per-frame GPU work below is
    // gated on a live consumer: the source stamps consumerHeartbeatTick each render, so when no
    // source is capturing we skip the copy/composite/Flush entirely (no game-thread GPU cost).
    if (!EnsureViewLabMirrorRing(outW, outH, ringFormat)) { tex0->Release(); if (tex1) tex1->Release(); return; }
    const uint32_t nowTick = static_cast<uint32_t>(GetTickCount64());
    const uint32_t consumerTick = g_vlmcSurface->consumerHeartbeatTick;
    if (consumerTick == 0 || (nowTick - consumerTick) > 2000u) {
        tex0->Release(); if (tex1) tex1->Release();
        g_vlmcConnectedLogged.store(false);
        return;
    }
    const uint32_t nextIndex = (g_vlmcFrameNumber + 1) % 3;
    ID3D11Texture2D* dst = g_vlmcRing[nextIndex];

    // Overlay link off (no features selected) → pure game-frame mirror: copy only, no draw
    // passes. This is the OXRMC-equivalent fallback for maximum fidelity / minimum overhead.
    const bool composite = (obsMirrorVisibilityMask != 0);
    VlmcCopyEye(dst, 0, tex0, eye0, d0.MipLevels, w0, h0);
    if (composite) VlmcComposeEye(dst, ringFormat, eye0, 0, w0, h0, allViews);
    tex0->Release();
    if (requested == 2 && tex1) {
        D3D11_TEXTURE2D_DESC d1{}; tex1->GetDesc(&d1);
        VlmcCopyEye(dst, w0, tex1, eye1, d1.MipLevels, w1, h1);
        if (composite) VlmcComposeEye(dst, ringFormat, eye1, w0, w1, h1, allViews);
        tex1->Release();
    }
    g_d3d11Mask.context->Flush();

    // Publish the completed slot: eyeMode/displayIndex only after Flush so the consumer never
    // samples a torn frame, then advance frameNumber and stamp the heartbeat.
    g_vlmcSurface->eyeMode = requested;
    g_vlmcSurface->displayIndex = nextIndex;
    MemoryBarrier();
    g_vlmcFrameNumber = (g_vlmcFrameNumber + 1) & 0x7fffffffu;
    g_vlmcSurface->frameNumber = g_vlmcFrameNumber;
    g_vlmcSurface->heartbeatTick = GetTickCount64();
    if (!g_vlmcConnectedLogged.exchange(true))
        Log("VLMC producer: publishing %ux%u mode=%u format=%d to %ls\n",
            outW, outH, requested, static_cast<int>(ringFormat), kViewLabMirrorSurfaceName);
}

void DrawCapturedProjectionTextures(bool drawVisor, bool drawOverlays, const char* tag) {
    if ((!drawVisor && !drawOverlays) || !g_d3d11Mask.initialized || !g_d3d11Mask.context) return;

    struct PendingDraw {
        ID3D11Texture2D* tex = nullptr;
        uint32_t arrSize = 1;
        int64_t format = 0;
        std::vector<EyeView> targets;
        std::vector<EyeView> projectionViews;
        std::vector<ID3D11RenderTargetView*> rtvs;
    };

    std::vector<PendingDraw> pending;
    {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        if (!g_primaryProjectionContext.ValidFor(g_d3d11Mask.session)) return;
        const std::vector<EyeView> projectionViews = g_primaryProjectionContext.topology.AllViews();
        for (const auto& kv : g_swapchains) {
            const TrackedSwapchain& ts = kv.second;
            const std::vector<EyeView> targets = g_primaryProjectionContext.topology.TargetsFor(kv.first);
            if (ts.session != g_d3d11Mask.session ||
                ts.lastAcquiredIndex >= ts.textures.size() ||
                targets.empty()) {
                continue;
            }
            PendingDraw draw{
                ts.textures[ts.lastAcquiredIndex],
                ts.arraySize,
                ts.format,
                targets,
                projectionViews,
                {}};
            if (draw.tex) draw.tex->AddRef();
            draw.rtvs.reserve(draw.targets.size());
            for (const EyeView& ev : draw.targets) {
                draw.rtvs.push_back(CachedRtvFor(ts, ts.lastAcquiredIndex, ev.arraySlice));
            }
            pending.push_back(std::move(draw));
        }
    }

    size_t eyeCount = 0;
    for (const PendingDraw& p : pending) {
        if (p.tex == nullptr) continue;
        for (size_t i = 0; i < p.targets.size(); ++i) {
            const EyeView& ev = p.targets[i];
            ID3D11RenderTargetView* rtv = i < p.rtvs.size() ? p.rtvs[i] : nullptr;
            // Visor first, overlays second: overlays inside the visor mask stay visible.
            if (drawVisor) DrawVisorBorderToTexture(p.tex, p.arrSize, p.format, ev, p.projectionViews, rtv);
            if (drawOverlays) DrawCalibrationPatternsToTexture(p.tex, p.arrSize, p.format, ev, p.projectionViews, rtv, true, true);
            ++eyeCount;
        }
        for (ID3D11RenderTargetView* rtv : p.rtvs) {
            if (rtv) rtv->Release();
        }
        p.tex->Release();
    }

    if (eyeCount > 0 && !g_diagEndFrameLateDraw.exchange(true)) {
        Log("%s: late xrEndFrame draw executed (%zu eye view(s))\n",
            tag ? tag : "d3d11 mask", eyeCount);
    }
}

ID3D11RenderTargetView* CachedRtvFor(const TrackedSwapchain& ts, uint32_t imageIndex, uint32_t arraySlice) {
    if (ts.rtvs.empty() || ts.arraySize == 0) return nullptr;
    const size_t rtvIndex = static_cast<size_t>(imageIndex) * ts.arraySize + arraySlice;
    if (rtvIndex >= ts.rtvs.size() || ts.rtvs[rtvIndex] == nullptr) return nullptr;
    ts.rtvs[rtvIndex]->AddRef();
    return ts.rtvs[rtvIndex];
}

void RebuildCachedRtvs(TrackedSwapchain& ts) {
    for (ID3D11RenderTargetView* rtv : ts.rtvs) {
        if (rtv) rtv->Release();
    }
    ts.rtvs.clear();

    if (!g_d3d11Mask.device || ts.textures.empty() || ts.arraySize == 0) return;

    DXGI_FORMAT rtvFormat = GetNonSRGBFormat(static_cast<DXGI_FORMAT>(ts.format));
    if (rtvFormat == DXGI_FORMAT_UNKNOWN && ts.textures[0]) {
        D3D11_TEXTURE2D_DESC desc{};
        ts.textures[0]->GetDesc(&desc);
        rtvFormat = GetNonSRGBFormat(desc.Format);
    }
    if (rtvFormat == DXGI_FORMAT_UNKNOWN) return;

    ts.rtvs.resize(ts.textures.size() * ts.arraySize, nullptr);
    for (size_t imageIndex = 0; imageIndex < ts.textures.size(); ++imageIndex) {
        ID3D11Texture2D* tex = ts.textures[imageIndex];
        if (!tex) continue;
        for (uint32_t slice = 0; slice < ts.arraySize; ++slice) {
            D3D11_RENDER_TARGET_VIEW_DESC rtvDesc{};
            rtvDesc.Format = rtvFormat;
            if (ts.arraySize > 1) {
                rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
                rtvDesc.Texture2DArray.MipSlice = 0;
                rtvDesc.Texture2DArray.FirstArraySlice = slice;
                rtvDesc.Texture2DArray.ArraySize = 1;
            } else {
                rtvDesc.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2D;
                rtvDesc.Texture2D.MipSlice = 0;
            }
            ID3D11RenderTargetView* rtv = nullptr;
            if (SUCCEEDED(g_d3d11Mask.device->CreateRenderTargetView(tex, &rtvDesc, &rtv)) && rtv) {
                ts.rtvs[imageIndex * ts.arraySize + slice] = rtv;
            }
        }
    }
}

// ---- Swapchain tracking for the D3D11 visor ----

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrCreateReferenceSpace(
    XrSession session, const XrReferenceSpaceCreateInfo* info, XrSpace* space) {
    if (!nextXrCreateReferenceSpace) return XR_ERROR_FUNCTION_UNSUPPORTED;
    const XrResult result = nextXrCreateReferenceSpace(session, info, space);
    if (XR_SUCCEEDED(result) && info && space && *space != XR_NULL_HANDLE) {
        {
            std::lock_guard<std::mutex> lock(g_pipelineEvidenceMutex);
            g_referenceSpaceTypes[*space] = info->referenceSpaceType;
        }
        LogVerbose("PIPE space-create handle=%p type=%s(%d) pose_p=(%.6f,%.6f,%.6f) pose_q=(%.7f,%.7f,%.7f,%.7f) result=%d\n",
            reinterpret_cast<void*>(*space), ReferenceSpaceTypeName(info->referenceSpaceType),
            static_cast<int>(info->referenceSpaceType), info->poseInReferenceSpace.position.x,
            info->poseInReferenceSpace.position.y, info->poseInReferenceSpace.position.z,
            info->poseInReferenceSpace.orientation.x, info->poseInReferenceSpace.orientation.y,
            info->poseInReferenceSpace.orientation.z, info->poseInReferenceSpace.orientation.w, result);
    }
    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrDestroySpace(XrSpace space) {
    if (!nextXrDestroySpace) return XR_ERROR_FUNCTION_UNSUPPORTED;
    const XrReferenceSpaceType type = TrackedReferenceSpaceType(space);
    const XrResult result = nextXrDestroySpace(space);
    LogVerbose("PIPE space-destroy handle=%p type=%s(%d) result=%d\n",
        reinterpret_cast<void*>(space), ReferenceSpaceTypeName(type), static_cast<int>(type), result);
    if (XR_SUCCEEDED(result)) {
        std::lock_guard<std::mutex> lock(g_pipelineEvidenceMutex);
        g_referenceSpaceTypes.erase(space);
    }
    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrCreateSwapchain(
    XrSession session, const XrSwapchainCreateInfo* info, XrSwapchain* swapchain) {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    const XrResult r = nextXrCreateSwapchain(session, info, swapchain);
    if (r == XR_SUCCESS && swapchain && *swapchain != XR_NULL_HANDLE && info) {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        TrackedSwapchain& ts = g_swapchains[*swapchain];
        ts.session   = session;
        ts.width     = info->width;
        ts.height    = info->height;
        ts.arraySize = info->arraySize;
        ts.sampleCount = info->sampleCount;
        ts.format    = info->format;
        ts.usageFlags = info->usageFlags;
        ts.createSerial = g_pipelineSwapchainSerial.fetch_add(1) + 1;
        LogVerbose("PIPE swapchain-create serial=%llu handle=%p session=%p size=%ux%u array=%u samples=%u faces=%u mips=%u format=%lld usage=0x%llX createFlags=0x%llX result=%d\n",
            static_cast<unsigned long long>(ts.createSerial), reinterpret_cast<void*>(*swapchain),
            reinterpret_cast<void*>(session), info->width, info->height, info->arraySize,
            info->sampleCount, info->faceCount, info->mipCount, static_cast<long long>(info->format),
            static_cast<unsigned long long>(info->usageFlags),
            static_cast<unsigned long long>(info->createFlags), r);
    }
    return r;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrEnumerateSwapchainImages(
    XrSwapchain swapchain, uint32_t capacity, uint32_t* count, XrSwapchainImageBaseHeader* images) {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    const XrResult r = nextXrEnumerateSwapchainImages(swapchain, capacity, count, images);
    if (r == XR_SUCCESS && images && count && *count > 0 &&
        images->type == XR_TYPE_SWAPCHAIN_IMAGE_D3D11_KHR) {
        auto* d3d = reinterpret_cast<XrSwapchainImageD3D11KHR*>(images);
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        auto it = g_swapchains.find(swapchain);
        if (it != g_swapchains.end()) {
            it->second.textures.resize(*count, nullptr);
            for (uint32_t i = 0; i < *count; ++i) it->second.textures[i] = d3d[i].texture;
            RebuildCachedRtvs(it->second);
            LogVerbose("PIPE swapchain-images serial=%llu handle=%p count=%u type=%d\n",
                static_cast<unsigned long long>(it->second.createSerial),
                reinterpret_cast<void*>(swapchain), *count, static_cast<int>(images->type));
        }
    }
    return r;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrAcquireSwapchainImage(
    XrSwapchain swapchain, const XrSwapchainImageAcquireInfo* info, uint32_t* imageIndex) {
    const XrResult r = nextXrAcquireSwapchainImage(swapchain, info, imageIndex);
    if (r == XR_SUCCESS && imageIndex) {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        auto it = g_swapchains.find(swapchain);
        if (it != g_swapchains.end()) {
            it->second.lastAcquiredIndex = *imageIndex;
            const uint64_t serial = ++it->second.acquireSerial;
            if (TracePipelineSerial(serial))
                LogVerbose("PIPE swapchain-acquire scSerial=%llu acquire=%llu handle=%p image=%u frame=%llu result=%d\n",
                    static_cast<unsigned long long>(it->second.createSerial),
                    static_cast<unsigned long long>(serial), reinterpret_cast<void*>(swapchain), *imageIndex,
                    static_cast<unsigned long long>(g_pipelineFrameSerial.load()), r);
        }
    }
    return r;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrWaitSwapchainImage(
    XrSwapchain swapchain, const XrSwapchainImageWaitInfo* info) {
    if (!nextXrWaitSwapchainImage) return XR_ERROR_FUNCTION_UNSUPPORTED;
    LARGE_INTEGER start{}, stop{};
    QueryPerformanceCounter(&start);
    const XrResult result = nextXrWaitSwapchainImage(swapchain, info);
    QueryPerformanceCounter(&stop);
    std::lock_guard<std::mutex> lock(g_swapchainMutex);
    const auto found = g_swapchains.find(swapchain);
    if (found != g_swapchains.end()) {
        const uint64_t serial = ++found->second.waitSerial;
        if (TracePipelineSerial(serial)) {
            const double milliseconds = g_hudQpcFrequency.QuadPart > 0 && stop.QuadPart >= start.QuadPart
                ? 1000.0 * static_cast<double>(stop.QuadPart - start.QuadPart) / g_hudQpcFrequency.QuadPart : -1.0;
            LogVerbose("PIPE swapchain-wait scSerial=%llu wait=%llu handle=%p image=%u timeout=%lld durationMs=%.4f frame=%llu result=%d\n",
                static_cast<unsigned long long>(found->second.createSerial),
                static_cast<unsigned long long>(serial), reinterpret_cast<void*>(swapchain),
                found->second.lastAcquiredIndex, info ? static_cast<long long>(info->timeout) : -1LL,
                milliseconds, static_cast<unsigned long long>(g_pipelineFrameSerial.load()), result);
        }
    }
    return result;
}

std::atomic<bool> g_diagReleaseSeen{false};
std::atomic<bool> g_diagReleaseNoLayout{false};
std::atomic<uint32_t> g_releaseDrawLogCount{0};
std::atomic<bool> g_releaseDrewViewLabBatchThisFrame{false};

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrReleaseSwapchainImage(
    XrSwapchain swapchain, const XrSwapchainImageReleaseInfo* info) {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    // Draw the visor border NOW — the app has finished rendering this image (it is
    // releasing it) and the runtime has not yet consumed it. This is the last point
    // at which the app side legitimately owns the image, so the write cannot be
    // overwritten by the app and is guaranteed present when the runtime composites.
    // Independent of the visibility-mask path; that path must never suppress this.

    if (enabled && ((maskEnabled && g_featurePresentationPlan.drawDirectVisor) ||
        (AnyDirectOverlay() && g_featurePresentationPlan.drawDirectCommonFeatures) || AnyCalibrationPattern()) &&
        g_d3d11Mask.initialized && g_d3d11Mask.context && RendererDeviceHealthy("xrReleaseSwapchainImage")) {
        ID3D11Texture2D* tex = nullptr;
        uint32_t arrSize = 1;
        int64_t  scFormat = 0;
        std::vector<EyeView> targetViews;
        std::vector<EyeView> projectionViews;
        std::vector<ID3D11RenderTargetView*> rtvs;
        bool tracked = false, sessionOk = false;
        uint64_t swapchainCreateSerial = 0, releaseSerial = 0, layoutFrameSerial = 0;
        XrTime layoutDisplayTime = 0;
        XrSpace layoutSpace = XR_NULL_HANDLE;
        uint32_t releasedImageIndex = UINT32_MAX;
        {
            std::lock_guard<std::mutex> lk(g_swapchainMutex);
            auto it = g_swapchains.find(swapchain);
            if (it != g_swapchains.end()) {
                tracked = true;
                TrackedSwapchain& ts = it->second;
                swapchainCreateSerial = ts.createSerial;
                releaseSerial = ++ts.releaseSerial;
                layoutFrameSerial = g_primaryProjectionContext.frameSerial;
                layoutDisplayTime = g_primaryProjectionContext.displayTime;
                layoutSpace = g_primaryProjectionContext.space;
                releasedImageIndex = ts.lastAcquiredIndex;
                sessionOk = (ts.session == g_d3d11Mask.session);
                if (sessionOk && ts.lastAcquiredIndex < ts.textures.size()) {
                    tex      = ts.textures[ts.lastAcquiredIndex];
                    if (tex) tex->AddRef();
                    arrSize  = ts.arraySize;
                    scFormat = ts.format;
                    if (g_primaryProjectionContext.ValidFor(ts.session)) {
                        targetViews = g_primaryProjectionContext.topology.TargetsFor(swapchain);
                        projectionViews = g_primaryProjectionContext.topology.AllViews();
                    }
                    rtvs.reserve(targetViews.size());
                    for (const EyeView& ev : targetViews) {
                        rtvs.push_back(CachedRtvFor(ts, ts.lastAcquiredIndex, ev.arraySlice));
                    }
                }
            }
        }
        if (tracked && TracePipelineSerial(releaseSerial)) {
            LogVerbose("PIPE swapchain-release scSerial=%llu release=%llu handle=%p image=%u currentFrame=%llu layoutFrame=%llu layoutDisplayTime=%lld layoutSpace=%p layoutSpaceType=%s targetViews=%zu projectionViews=%zu sessionMatch=%d\n",
                static_cast<unsigned long long>(swapchainCreateSerial),
                static_cast<unsigned long long>(releaseSerial), reinterpret_cast<void*>(swapchain),
                releasedImageIndex, static_cast<unsigned long long>(g_pipelineFrameSerial.load()),
                static_cast<unsigned long long>(layoutFrameSerial), static_cast<long long>(layoutDisplayTime),
                reinterpret_cast<void*>(layoutSpace), ReferenceSpaceTypeName(TrackedReferenceSpaceType(layoutSpace)),
                targetViews.size(), projectionViews.size(), sessionOk ? 1 : 0);
        }
        if (!g_diagReleaseSeen.exchange(true))
            Log("d3d11 mask DIAG: xrReleaseSwapchainImage reached (tracked=%d sessionMatch=%d targetViews=%zu projectionViews=%zu)\n",
                tracked ? 1 : 0, sessionOk ? 1 : 0, targetViews.size(), projectionViews.size());

        if (tracked && sessionOk && tex) {
            if (targetViews.empty() || projectionViews.empty()) {
                if (!g_diagReleaseNoLayout.exchange(true))
                    Log("d3d11 mask DIAG: no eye layout yet for this swapchain (captured on first xrEndFrame; mask starts next frame)\n");
            } else {
                const bool directCommon=g_featurePresentationPlan.drawDirectCommonFeatures||
                    g_topmostLayerBlocked.load(std::memory_order_acquire);
                bool drewVisor=false, drewBatch=false;
                for (size_t i = 0; i < targetViews.size(); ++i) {
                    if (maskEnabled && g_featurePresentationPlan.drawDirectVisor) {
                        DrawVisorBorderToTexture(tex, arrSize, scFormat, targetViews[i], projectionViews,
                            i < rtvs.size() ? rtvs[i] : nullptr);
                        drewVisor=true; drewBatch=true;
                    }
                    // Literal-pixel calibration always measures the submitted game texture. It is
                    // never silently redefined as pixels of ViewLab's separate Topmost target.
                    if (AnyCalibrationPattern() || (directCommon && ((hudEnabled&&OverlayFeatureVisible(OverlayFeatureId::Hud)) || (hudTraceEnabled&&OverlayFeatureVisible(OverlayFeatureId::Trace)) || AnyViewLabOverlay()))) {
                        DrawCalibrationPatternsToTexture(tex, arrSize, scFormat, targetViews[i], projectionViews,
                            i < rtvs.size() ? rtvs[i] : nullptr,true,directCommon);
                        drewBatch=true;
                    }
                }
                for (ID3D11RenderTargetView* rtv : rtvs) {
                    if (rtv) rtv->Release();
                }
                const uint32_t n = drewVisor ? g_releaseDrawLogCount.fetch_add(1) : 1;
                if (drewVisor && n == 0)
                    Log("visor: draw executed (%zu target view(s), %zu projection view(s))\n",
                        targetViews.size(), projectionViews.size());
                if(drewBatch) g_releaseDrewViewLabBatchThisFrame.store(true);
            }
        } else {
            for (ID3D11RenderTargetView* rtv : rtvs) {
                if (rtv) rtv->Release();
            }
        }
        if (tex) tex->Release();
    }
    return nextXrReleaseSwapchainImage(swapchain, info);
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrDestroySwapchain(XrSwapchain swapchain) {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    {
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        auto it = g_swapchains.find(swapchain);
        if (it != g_swapchains.end()) {
            LogVerbose("PIPE swapchain-destroy serial=%llu handle=%p acquire=%llu wait=%llu release=%llu\n",
                static_cast<unsigned long long>(it->second.createSerial), reinterpret_cast<void*>(swapchain),
                static_cast<unsigned long long>(it->second.acquireSerial),
                static_cast<unsigned long long>(it->second.waitSerial),
                static_cast<unsigned long long>(it->second.releaseSerial));
            ReleaseTrackedSwapchainResources(it->second);
            g_swapchains.erase(it);
        }
        // A destroyed destination invalidates the previous frame as a complete
        // projection context. The next submitted projection repopulates it atomically.
        g_primaryProjectionContext = ProjectionFrameContext{};
    }
    return nextXrDestroySwapchain(swapchain);
}

// ---- Head-locked RGBA layer helper (shared by test quad and technique-A visor) ----

// Creates a head-locked VIEW reference space + an RGBA swapchain of the given size.
void LoadConfig() {
    enabled = ReadBoolSetting(L"enabled", true);
    verboseLogging = ReadBoolSetting(L"verbose_logging", false);
    splitMode = ReadBoolSetting(L"split_mode", false);
    // Foveated centre compensation is retired: it rotated the game's eye poses and made
    // asymmetric vertical crops appear as a tilted/folded world. Crop/stencil outer edges remain on.
    visualMaskOnly = ReadBoolSetting(L"visual_mask_only", false);
    horizontalVisualMaskOnly = ReadBoolSetting(L"horizontal_visual_mask_only", false);
    // There is exactly ONE visor renderer (D3D11 draw into the game's eye textures).
    // The old hidden-mesh reshaper is gone: visibility_mask_visor=1 is retired. Old
    // inis may still carry the key; it is read only to warn, never to reshape.
    bool visibilityMaskVisor = ReadBoolSetting(L"visibility_mask_visor", false);
    if (visibilityMaskVisor) {
        Log("config: visibility_mask_visor=1 is retired and ignored; the visor draws into eye textures only\n");
    }
    visibilityMaskVisor = false;
    // edge_smear_fix, edge_smear_pixels, lod_popin_fix, and crop_experiment_mode are removed;
    // the experimental crop-fix code paths are gone and these keys are ignored.
    calibrationGrid = ReadBoolSetting(L"calibration_grid", false);
    calibrationRuler = ReadBoolSetting(L"calibration_ruler", false);
    calibrationGratings = ReadBoolSetting(L"calibration_gratings", false);
    calibrationBars = ReadBoolSetting(L"calibration_bars", false);
    calibrationBeacon = ReadBoolSetting(L"calibration_beacon", false);
    calibrationEdgeProbes = ReadBoolSetting(L"calibration_edge_probes", false);
    calibrationCheckerboards = ReadBoolSetting(L"calibration_checkerboards", false);
    calibrationZonePlate = ReadBoolSetting(L"calibration_zone_plate", false);
    calibrationClippingSteps = ReadBoolSetting(L"calibration_clipping_steps", false);
    calibrationMotionStrip = ReadBoolSetting(L"calibration_motion_strip", false);
    hudEnabled = ReadBoolSetting(L"hud_enabled", false);
    hudAnchorX = std::clamp(ReadDoubleSetting(L"hud_anchor_x", 0.04), 0.0, 1.0);
    hudAnchorY = std::clamp(ReadDoubleSetting(L"hud_anchor_y", 0.05), 0.0, 1.0);
    hudScale = std::clamp(ReadDoubleSetting(L"hud_scale", 1.0), 0.15, 3.0);
    hudSpacing = std::clamp(ReadDoubleSetting(L"hud_spacing", 0.018), 0.0, 0.10);
	hudOpacity = std::clamp(ReadDoubleSetting(L"hud_opacity", 0.70), 0.10, 1.0);
	hudTraceOpacity = std::clamp(ReadDoubleSetting(L"hud_trace_opacity", hudOpacity), 0.10, 1.0);
    hudSafeMargin = std::clamp(ReadDoubleSetting(L"hud_safe_margin", 0.025), 0.0, 0.25);
    hudClampToVisible = ReadBoolSetting(L"hud_clamp_to_visible", true);
    hudUpdateIntervalMs = static_cast<uint32_t>(std::clamp(ReadDoubleSetting(L"hud_update_ms", 100.0), 50.0, 1000.0));
    hudGreenThreshold = std::clamp(ReadDoubleSetting(L"hud_green_threshold", 75.0), 1.0, 99.0);
    hudRedThreshold = std::clamp(ReadDoubleSetting(L"hud_red_threshold", 90.0), hudGreenThreshold + 1.0, 100.0);
    hudTraceSensitivityMs = std::clamp(ReadDoubleSetting(L"hud_trace_sensitivity_ms", 2.0), 0.25, 8.0);
    hudTraceX = std::clamp(ReadDoubleSetting(L"hud_trace_x", 0.05), 0.0, 1.0);
    hudTraceY = std::clamp(ReadDoubleSetting(L"hud_trace_y", 0.75), 0.0, 1.0);
    hudTraceScale = std::clamp(ReadDoubleSetting(L"hud_trace_scale", 1.0), 0.25, 3.0);
    hudTraceWidth = std::clamp(ReadDoubleSetting(L"hud_trace_width", 0.42), 0.10, 1.0);
    hudTraceHistory = std::clamp(ReadDoubleSetting(L"hud_trace_history", 120.0), 10.0, 600.0);
    hudTraceEnabled = ReadBoolSetting(L"hud_trace_enabled", false);
    hudTraceVisibilityMode = (uint32_t)std::clamp((int)ReadDoubleSetting(L"hud_trace_visibility_mode",hudTraceEnabled?1.0:0.0),0,2);
    hudTraceEnabled = hudTraceVisibilityMode != 0;
    performanceTraceRecording=ReadBoolSetting(L"performance_trace_recording",true);
    performanceTraceMarkerKey=(int)std::clamp(ReadDoubleSetting(L"performance_trace_marker_vk",VK_F8),1.0,255.0);
    constexpr const wchar_t* widgetKeys[kHudWidgetCount]={L"cpu",L"gpu",L"app",L"vr",L"cpu_peak",L"cpu_frequency",L"ram",L"commit",L"vram",L"sys",L"fps",L"frame_interval",L"network_ping",L"network_loss",L"network_jitter",L"network_status"};
    hudWidgetMask=0;
    for(size_t i=0;i<kHudWidgetCount;++i){wchar_t key[80]{};swprintf_s(key,L"hud_widget_%s_enabled",widgetKeys[i]);const bool defaultOn=i==0||i==1||i==3||i==9;if(ReadBoolSetting(key,defaultOn))hudWidgetMask|=1ull<<i;}
    hudWidgetSymbolMask=0;for(size_t i=0;i<kHudWidgetCount;++i){wchar_t key[80]{};swprintf_s(key,L"hud_widget_%s_symbol",widgetKeys[i]);if(ReadBoolSetting(key,false))hudWidgetSymbolMask|=1u<<i;}
    std::array<std::pair<int,uint8_t>,kHudWidgetCount> ordered{};
    for(size_t i=0;i<kHudWidgetCount;++i){wchar_t key[80]{};swprintf_s(key,L"hud_widget_%s_order",widgetKeys[i]);ordered[i]={(int)ReadDoubleSetting(key,(double)i),(uint8_t)i};}
    std::stable_sort(ordered.begin(),ordered.end(),[](const auto&a,const auto&b){return a.first<b.first;});
    for(size_t i=0;i<kHudWidgetCount;++i)hudWidgetOrder[i]=ordered[i].second;
    hudMaxPerRow=(uint32_t)std::clamp(ReadDoubleSetting(L"hud_max_per_row",static_cast<double>(kHudWidgetCount)),1.0,16.0);
    hudSysWarningThreshold=std::clamp(ReadDoubleSetting(L"hud_sys_warning",30.0),10.0,60.0);
    hudSysCriticalThreshold=std::clamp(ReadDoubleSetting(L"hud_sys_critical",10.0),0.0,hudSysWarningThreshold);
    networkPingWarningMs=std::clamp(ReadDoubleSetting(L"network_ping_warning_ms",80.0),1.0,1000.0);
    networkPingCriticalMs=std::clamp(ReadDoubleSetting(L"network_ping_critical_ms",150.0),networkPingWarningMs,5000.0);
    networkLossWarning=std::clamp(ReadDoubleSetting(L"network_loss_warning",2.0),0.1,100.0);
    networkLossCritical=std::clamp(ReadDoubleSetting(L"network_loss_critical",5.0),networkLossWarning,100.0);
    networkJitterWarningMs=std::clamp(ReadDoubleSetting(L"network_jitter_warning_ms",15.0),0.1,1000.0);
    networkJitterCriticalMs=std::clamp(ReadDoubleSetting(L"network_jitter_critical_ms",30.0),networkJitterWarningMs,5000.0);
    hudWidgetWarning={{hudGreenThreshold,kHudGpuWarningThreshold,hudGreenThreshold,103.0,hudGreenThreshold,0.0,hudGreenThreshold,hudGreenThreshold,hudGreenThreshold,hudSysWarningThreshold,0.0,103.0,networkPingWarningMs,networkLossWarning,networkJitterWarningMs,1.0}};
    hudWidgetCritical={{hudRedThreshold,kHudGpuCriticalThreshold,hudRedThreshold,108.0,hudRedThreshold,0.0,hudRedThreshold,hudRedThreshold,hudRedThreshold,hudSysCriticalThreshold,0.0,108.0,networkPingCriticalMs,networkLossCritical,networkJitterCriticalMs,2.0}};
    for(size_t i=0;i<kHudWidgetCount;++i){
        wchar_t warningKey[96]{},criticalKey[96]{};
        swprintf_s(warningKey,L"hud_widget_%s_warning",widgetKeys[i]);
        swprintf_s(criticalKey,L"hud_widget_%s_critical",widgetKeys[i]);
        double warning=std::clamp(ReadDoubleSetting(warningKey,hudWidgetWarning[i]),0.0,1000000.0);
        double critical=std::clamp(ReadDoubleSetting(criticalKey,hudWidgetCritical[i]),0.0,1000000.0);
        const bool lower=(kHudLowerIsWorseMask&(1ull<<i))!=0;
        if(lower&&critical>warning)std::swap(critical,warning);
        if(!lower&&critical<warning)std::swap(critical,warning);
        hudWidgetWarning[i]=warning;hudWidgetCritical[i]=critical;
    }
    IN_ADDR probeAddress{};const std::wstring probeTarget=ReadStringSetting(L"network_probe_target",L"1.1.1.1");
    const bool validProbeTarget=InetPtonW(AF_INET,probeTarget.c_str(),&probeAddress)==1;
    viewlab::telemetry::SetNetworkProbeTarget(validProbeTarget?probeAddress.S_un.S_addr:0);
    viewlab::telemetry::SetNetworkProbeEnabled(validProbeTarget && (hudWidgetMask & (0xFull<<12)) != 0);
    hudWidgetOrderPacked=PackHudWidgetOrder({
        (int)ReadDoubleSetting(L"hud_widget_cpu_order",0.0),
        (int)ReadDoubleSetting(L"hud_widget_gpu_order",1.0),
        (int)ReadDoubleSetting(L"hud_widget_app_order",2.0),
        (int)ReadDoubleSetting(L"hud_widget_vr_order",3.0)});
    hudGraphChannels=(ReadBoolSetting(L"hud_graph_frame_interval",false)?GraphFrameInterval:0u)|
        (ReadBoolSetting(L"hud_graph_fps",false)?GraphFps:0u)|
        (ReadBoolSetting(L"hud_graph_budget_deviation",true)?GraphBudgetDeviation:0u)|
        (ReadBoolSetting(L"hud_graph_app_work",false)?GraphAppWork:0u)|
        (ReadBoolSetting(L"hud_graph_wait_duration",false)?GraphWaitDuration:0u)|
        (ReadBoolSetting(L"hud_graph_submit_duration",false)?GraphSubmitDuration:0u)|
        (ReadBoolSetting(L"hud_graph_display_period",false)?GraphDisplayPeriod:0u);
    hudGraphMode=(HudGraphMode)std::clamp((int)ReadDoubleSetting(L"hud_graph_mode",0.0),0,3);
    topmostVisorOverlays = !ReadBoolSetting(L"overlay_force_direct", false);
    experimentalDrawInVoid = ReadBoolSetting(L"experimental_draw_in_void", false);
    hudAlarmOnly = ReadBoolSetting(L"hud_alarm_only", false);
    hudAlarmHoldMs = std::clamp(ReadDoubleSetting(L"hud_alarm_hold_ms", 1500.0), 0.0, 10000.0);
    hudDebugValues = ReadBoolSetting(L"hud_debug_values", false);
    hudDebugCpu = ReadDoubleSetting(L"hud_debug_cpu", 52.0); hudDebugGpu = ReadDoubleSetting(L"hud_debug_gpu", 98.0);
    hudDebugSystem = ReadDoubleSetting(L"hud_debug_app",ReadDoubleSetting(L"hud_debug_system",44.0)); hudDebugVr = ReadDoubleSetting(L"hud_debug_vr", 18.0);
    // Feature 2: crosshair defaults (the UI parses legacy cl_* configs / CS2 share codes into these).
    crosshairEnabled = ReadBoolSetting(L"crosshair_enabled", false);
    crosshairDot = ReadBoolSetting(L"crosshair_dot", false);
    crosshairOutline = ReadBoolSetting(L"crosshair_outline", true);
    crosshairTStyle = ReadBoolSetting(L"crosshair_tstyle", false);
    crosshairSize = std::clamp(ReadDoubleSetting(L"crosshair_size", 5.0), 0.0, 1000.0);
    crosshairGap = std::clamp(ReadDoubleSetting(L"crosshair_gap", -2.0), -50.0, 50.0);
    crosshairThickness = std::clamp(ReadDoubleSetting(L"crosshair_thickness", 1.0), 0.1, 50.0);
    crosshairOutlineThickness = std::clamp(ReadDoubleSetting(L"crosshair_outline_thickness", 1.0), 0.0, 10.0);
    crosshairAlpha = std::clamp(ReadDoubleSetting(L"crosshair_alpha", 1.0), 0.0, 1.0);
    crosshairScale = std::clamp(ReadDoubleSetting(L"crosshair_scale", 1.0), 0.1, 10.0);
    crosshairOffsetX = std::clamp(ReadDoubleSetting(L"crosshair_offset_x", 0.0), -1.0, 1.0);
    crosshairOffsetY = std::clamp(ReadDoubleSetting(L"crosshair_offset_y", 0.0), -1.0, 1.0);
    {
        const uint32_t chCol = static_cast<uint32_t>(ReadDoubleSetting(L"crosshair_color", (double)0x00FF00));
        crosshairR = ((chCol >> 16) & 0xFF) / 255.f; crosshairG = ((chCol >> 8) & 0xFF) / 255.f; crosshairB = (chCol & 0xFF) / 255.f;
    }
    // Feature 3: notification render settings (content bridge is separate).
    notifyEnabled = ReadBoolSetting(L"notify_enabled", false);
    notifyX = std::clamp(ReadDoubleSetting(L"notify_x", 0.98), 0.0, 1.0);
    notifyY = std::clamp(ReadDoubleSetting(L"notify_y", 0.98), 0.0, 1.0);
    notifyScale = std::clamp(ReadDoubleSetting(L"notify_scale", 1.0), 0.1, 3.0);
    notifyOpacity = std::clamp(ReadDoubleSetting(L"notify_opacity", 1.0), 0.1, 1.0);
    clockWidgetEnabled = ReadBoolSetting(L"clock_widget_enabled", false);
    clockSessionTimerEnabled=ReadBoolSetting(L"clock_session_timer_enabled",true);clock24Hour=ReadBoolSetting(L"clock_24_hour",true);
    {
        // Migration: legacy configs stored the recolour in clock_widget_theme. When the new
        // palette key is absent, that value becomes the palette and the design stays Classic.
        const double storedTheme=std::clamp(ReadDoubleSetting(L"clock_widget_theme",0),0.0,4.0);
        const double storedPalette=ReadDoubleSetting(L"clock_widget_palette",-1.0);
        if(storedPalette<0.0){clockWidgetPalette=(uint32_t)storedTheme;clockWidgetTheme=0;}
        else{clockWidgetPalette=(uint32_t)std::clamp(storedPalette,0.0,4.0);clockWidgetTheme=(uint32_t)std::clamp(storedTheme,0.0,3.0);}
    }
    clockWidgetX = std::clamp(ReadDoubleSetting(L"clock_widget_x", 0.50), 0.0, 1.0);
    clockWidgetY = std::clamp(ReadDoubleSetting(L"clock_widget_y", 0.10), 0.0, 1.0);
    clockWidgetScale = std::clamp(ReadDoubleSetting(L"clock_widget_scale", 1.0), 0.10, 2.0);
    clockWidgetOpacity = std::clamp(ReadDoubleSetting(L"clock_widget_opacity", 0.82), 0.10, 1.0);
    stickyNoteEnabled=ReadBoolSetting(L"sticky_note_enabled",false);stickyNoteCount=0;
    const std::wstring noteCountText=ReadStringSetting(L"sticky_note_count",L"");
    if(noteCountText.empty()){auto&n=stickyNotes[0];n.enabled=true;n.text=ReadStringSetting(L"sticky_note_text",L"");n.x=std::clamp(ReadDoubleSetting(L"sticky_note_x",.78),0.0,1.0);n.y=std::clamp(ReadDoubleSetting(L"sticky_note_y",.22),0.0,1.0);n.scale=std::clamp(ReadDoubleSetting(L"sticky_note_scale",1.0),.5,2.5);n.opacity=std::clamp(ReadDoubleSetting(L"sticky_note_opacity",.85),.1,1.0);n.theme=0;stickyNoteCount=1;}
    else {const int count=std::clamp(_wtoi(noteCountText.c_str()),0,(int)kStickyNoteMax);for(int i=0;i<count;++i){wchar_t key[80]{};auto&n=stickyNotes[stickyNoteCount++];swprintf_s(key,L"sticky_note_%d_enabled",i);n.enabled=ReadBoolSetting(key,true);swprintf_s(key,L"sticky_note_%d_text",i);n.text=ReadStringSetting(key,L"");swprintf_s(key,L"sticky_note_%d_x",i);n.x=std::clamp(ReadDoubleSetting(key,.78),0.0,1.0);swprintf_s(key,L"sticky_note_%d_y",i);n.y=std::clamp(ReadDoubleSetting(key,.22),0.0,1.0);swprintf_s(key,L"sticky_note_%d_scale",i);n.scale=std::clamp(ReadDoubleSetting(key,1),.5,2.5);swprintf_s(key,L"sticky_note_%d_opacity",i);n.opacity=std::clamp(ReadDoubleSetting(key,.85),.1,1.0);swprintf_s(key,L"sticky_note_%d_theme",i);n.theme=(uint32_t)std::clamp(ReadDoubleSetting(key,0),0.0,4.0);}}
    const int legacyStickyNoteToggleKey=(int)std::clamp(ReadDoubleSetting(L"sticky_note_toggle_vk",VK_F7),1.0,255.0);
    constexpr const wchar_t* overlayToggleKeys[] = {
        L"overlay_hud_toggle_vk",L"overlay_trace_toggle_vk",L"overlay_clock_toggle_vk",
        L"overlay_sticky_note_toggle_vk",L"overlay_crosshair_toggle_vk",L"overlay_notifications_toggle_vk"};
    const int overlayToggleDefaults[] = {0,0,0,legacyStickyNoteToggleKey,0,0};
    for(size_t i=0;i<(size_t)OverlayFeatureId::Count;++i){
        auto& feature=g_overlayFeatureVisibility[i];
        feature.toggleKey=(int)std::clamp(ReadDoubleSetting(overlayToggleKeys[i],overlayToggleDefaults[i]),0.0,255.0);
        feature.keyDown=false;feature.visible.store(true,std::memory_order_release);
    }
    obsIndicatorEnabled=ReadBoolSetting(L"obs_indicator_enabled",false);
    obsIndicatorOpacity=std::clamp(ReadDoubleSetting(L"obs_indicator_opacity",.72),.1,1.0);
    obsIndicatorThickness=std::clamp(ReadDoubleSetting(L"obs_indicator_thickness",.009),.002,.04);
    obsMirrorVisibilityMask = 0;
    if (ReadBoolSetting(L"obs_mirror_show_visor", true)) obsMirrorVisibilityMask |= MirrorVisor;
    if (ReadBoolSetting(L"obs_mirror_show_hud", true)) obsMirrorVisibilityMask |= MirrorHud;
    if (ReadBoolSetting(L"obs_mirror_show_trace", true)) obsMirrorVisibilityMask |= MirrorTrace;
    if (ReadBoolSetting(L"obs_mirror_show_clock", true)) obsMirrorVisibilityMask |= MirrorClock;
    if (ReadBoolSetting(L"obs_mirror_show_notifications", true)) obsMirrorVisibilityMask |= MirrorNotifications;
    if (ReadBoolSetting(L"obs_mirror_show_sticky_notes", true)) obsMirrorVisibilityMask |= MirrorStickyNotes;
    if (ReadBoolSetting(L"obs_mirror_show_crosshair", true)) obsMirrorVisibilityMask |= MirrorCrosshair;
    if (ReadBoolSetting(L"obs_mirror_show_boundary_flash", true)) obsMirrorVisibilityMask |= MirrorBoundaryFlash;
    if (ReadBoolSetting(L"obs_mirror_show_recording_cue", true)) obsMirrorVisibilityMask |= MirrorRecordingCue;
    if (ReadBoolSetting(L"obs_mirror_show_racing_cues", true)) obsMirrorVisibilityMask |= MirrorRacingCues;
    // Generic racing presentation settings.
    iracingEnabled = ReadBoolSetting(L"iracing_enabled", false);
    iracingLapPopup = ReadBoolSetting(L"iracing_lap_popup", false);
    iracingSpotterGlow = ReadBoolSetting(L"iracing_spotter_glow", false);
    iracingFlagBorder = ReadBoolSetting(L"iracing_flag_border", false);
    iracingRaceStart = ReadBoolSetting(L"iracing_race_start", false);
    iracingRaceStartRedOpacity = std::clamp(ReadDoubleSetting(L"iracing_race_start_red_opacity", 0.8), 0.05, 1.0);
    iracingRaceStartGreenOpacity = std::clamp(ReadDoubleSetting(L"iracing_race_start_green_opacity", 0.8), 0.05, 1.0);
    iracingRaceStartGreenMs = std::clamp(ReadDoubleSetting(L"iracing_race_start_green_ms", 3000.0), 250.0, 15000.0);
    iracingRaceStartThickness = std::clamp(ReadDoubleSetting(L"iracing_race_start_thickness", 0.02), 0.005, 0.12);
    iracingRearClosing = ReadBoolSetting(L"iracing_rear_closing", false);
    iracingRearClosingOpacity = std::clamp(ReadDoubleSetting(L"iracing_rear_closing_opacity", 0.9), 0.05, 1.0);
    iracingGripBar = ReadBoolSetting(L"iracing_grip_bar", false);
    iracingGripBarOpacity = std::clamp(ReadDoubleSetting(L"iracing_grip_bar_opacity", 0.9), 0.05, 1.0);
    iracingSpotterWidth = std::clamp(ReadDoubleSetting(L"iracing_spotter_width", 0.12), 0.03, 0.35);
    iracingSpotterStrength = std::clamp(ReadDoubleSetting(L"iracing_spotter_strength", 1.0), 0.1, 2.0);
    iracingSpotterOpacity = std::clamp(ReadDoubleSetting(L"iracing_spotter_opacity", 0.65), 0.05, 1.0);
    iracingSpotterFade = std::clamp(ReadDoubleSetting(L"iracing_spotter_fade", 1.8), 0.25, 4.0);
    iracingSpotterColor = static_cast<uint32_t>(ReadDoubleSetting(L"iracing_spotter_color", (double)0xFF4500)) & 0xFFFFFFu;
    iracingFlagWidth = std::clamp(ReadDoubleSetting(L"iracing_flag_width", 0.018), 0.003, 0.12);
    iracingFlagOpacity = std::clamp(ReadDoubleSetting(L"iracing_flag_opacity", 0.60), 0.05, 1.0);
    ConnectLiveState();
    if (AnyCalibrationPattern()) {
        Log("calibration: grid=%d ruler=%d gratings=%d bars=%d beacon=%d edge_probes=%d checkerboards=%d zone_plate=%d clipping_steps=%d motion_strip=%d\n",
            calibrationGrid, calibrationRuler, calibrationGratings, calibrationBars, calibrationBeacon,
            calibrationEdgeProbes, calibrationCheckerboards, calibrationZonePlate, calibrationClippingSteps, calibrationMotionStrip);
    }
    const double legacyTotal = ReadDoubleSetting(L"vertical_tangent", DefaultTotalTangent);
    totalTangent = std::clamp(
        ReadDoubleSetting(L"total_render_height", ReadDoubleSetting(L"total_share", legacyTotal)),
        MinVerticalTangent,
        MaxVerticalTangent);
    horizontalRenderWidth = std::clamp(
        ReadDoubleSetting(L"horizontal_render_width", DefaultHorizontalRenderWidth),
        MinRenderScale,
        1.0);

    maskEnabled = ReadBoolSetting(L"mask_enabled", false);
    maskVertical = std::clamp(ReadDoubleSetting(L"mask_vertical", 1.0), MinVerticalTangent, MaxVerticalTangent);
    maskHorizontal = std::clamp(ReadDoubleSetting(L"mask_horizontal", 1.0), 0.01, 1.0);
    maskRounded = ReadBoolSetting(L"mask_rounded", true);
    maskCorner = std::clamp(ReadDoubleSetting(L"mask_corner", 0.5), 0.0, 1.0);
    const double oldMaskOffsetY = std::clamp(ReadDoubleSetting(L"mask_offset_y", 0.0), -1.0, 1.0);
    maskTopBias = std::clamp(ReadDoubleSetting(L"mask_top_bias", oldMaskOffsetY), -1.0, 1.0);
    maskBottomBias = std::clamp(ReadDoubleSetting(L"mask_bottom_bias", oldMaskOffsetY), -1.0, 1.0);
    maskLeftBias = std::clamp(ReadDoubleSetting(L"mask_left_bias", 0.0), -1.0, 1.0);
    maskRightBias = std::clamp(ReadDoubleSetting(L"mask_right_bias", 0.0), -1.0, 1.0);
    maskTopCurve = std::clamp(ReadDoubleSetting(L"mask_top_curve", 0.0), -1.0, 1.0);
    maskBottomCurve = std::clamp(ReadDoubleSetting(L"mask_bottom_curve", 0.0), -1.0, 1.0);
    renderScale = std::clamp(ReadDoubleSetting(L"render_scale", 1.0), 0.1, 3.0);
    // HD visor and anti-aliasing are permanently disabled.

    // Visor shape controls (ranges identical to the UI sliders).
    visorOuterApexY = std::clamp(ReadDoubleSetting(L"mask_outer_apex_y", 0.0), -0.5, 0.5);
    visorInnerLowerY = std::clamp(ReadDoubleSetting(L"mask_inner_lower_y", 0.0), 0.0, 0.666);
    visorInnerBridgeWidth = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_width", 0.5), 0.0, 1.0);
    visorInnerBridgeRise = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_rise", 0.0), -0.5, 1.0);
    visorInnerBridgePeakX = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_peak_x", 0.5), -1.0, 2.0);
    visorInnerBridgeSteepness = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_steepness", 0.5), -1.0, 2.0);
    visorNoseSpreadX = std::clamp(ReadDoubleSetting(L"mask_nose_spread_x", 0.0), 0.0, 0.5);
    // Item 21: visor fill colour 0xRRGGBB (default 0 = black). Resolved through the same profile/global
    // INI as the other visor keys, so per-app custom visor profiles carry their own colour.
    {
        uint32_t mc = static_cast<uint32_t>(ReadDoubleSetting(L"mask_color", 0.0)) & 0xFFFFFFu;
        g_visorColor[0] = ((mc >> 16) & 0xFF) / 255.f;
        g_visorColor[1] = ((mc >> 8) & 0xFF) / 255.f;
        g_visorColor[2] = (mc & 0xFF) / 255.f;
    }

    if (splitMode) {
        topTangent = std::clamp(
            ReadDoubleSetting(L"top_tangent", totalTangent * 0.5),
            MinVerticalTangent,
            MaxVerticalTangent);
        bottomTangent = std::clamp(
            ReadDoubleSetting(L"bottom_tangent", totalTangent * 0.5),
            MinVerticalTangent,
            MaxVerticalTangent);
        totalTangent = std::clamp(topTangent + bottomTangent, MinVerticalTangent, MaxVerticalTangent);
    } else {
        topTangent = totalTangent * 0.5;
        bottomTangent = totalTangent * 0.5;
    }

    DWORD appEnabled = 1;
    if (ReadProfileDword(L"app_enabled", appEnabled) && appEnabled == 0) {
        enabled = false;
    }

    liveVisorUsesProfileOverride = false;
    profileOverlayOverrideMask = 0;
    profileStickyOverlayOverrideMask = 0;
    profileObsFeatureOverride = false;
    profileIRacingFeatureOverride = false;
    DWORD profileEnabled = 0;
    DWORD profileVisorSize = 0;
    DWORD profileVisorWidth = 0;
    DWORD profileVisorHeight = 0;
    if (ReadProfileDword(L"profile_enabled", profileEnabled) && profileEnabled != 0) {
        DWORD profileSplitMode = splitMode ? 1u : 0u;
        DWORD profileTotal = static_cast<DWORD>(std::lround(totalTangent * 1000.0));
        DWORD profileTop = static_cast<DWORD>(std::lround(topTangent * 1000.0));
        DWORD profileBottom = static_cast<DWORD>(std::lround(bottomTangent * 1000.0));
        DWORD profileHorizontal = static_cast<DWORD>(std::lround(horizontalRenderWidth * 1000.0));
        ReadProfileDword(L"split_mode", profileSplitMode);
        ReadProfileDword(L"total_render_height", profileTotal) || ReadProfileDword(L"total_share", profileTotal) || ReadProfileDword(L"vertical_tangent", profileTotal);
        ReadProfileDword(L"top_tangent", profileTop);
        ReadProfileDword(L"bottom_tangent", profileBottom);
        ReadProfileDword(L"horizontal_render_width", profileHorizontal);

        DWORD profileMaskVertical = static_cast<DWORD>(std::lround(maskVertical * 1000.0));
        DWORD profileMaskHorizontal = static_cast<DWORD>(std::lround(maskHorizontal * 1000.0));
        DWORD profileMaskRounded = maskRounded ? 1u : 0u;
        DWORD profileMaskEnabled = maskEnabled ? 1u : 0u;
        DWORD profileMaskCorner = static_cast<DWORD>(std::lround(maskCorner * 1000.0));
        DWORD profileMaskTopBias = static_cast<DWORD>(std::lround((std::clamp(maskTopBias, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskBottomBias = static_cast<DWORD>(std::lround((std::clamp(maskBottomBias, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskLeftBias = static_cast<DWORD>(std::lround((std::clamp(maskLeftBias, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskRightBias = static_cast<DWORD>(std::lround((std::clamp(maskRightBias, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskTopCurve = static_cast<DWORD>(std::lround((std::clamp(maskTopCurve, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskBottomCurve = static_cast<DWORD>(std::lround((std::clamp(maskBottomCurve, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileMaskOffsetY = static_cast<DWORD>(std::lround(((maskTopBias + maskBottomBias) * 0.5 + 1.0) * 1000.0));
        ReadProfileDword(L"mask_vertical", profileMaskVertical);
        ReadProfileDword(L"mask_horizontal", profileMaskHorizontal);
        DWORD profileRenderScale = static_cast<DWORD>(std::lround(renderScale * 1000000.0));
        ReadProfileDword(L"mask_rounded", profileMaskRounded);
        ReadProfileDword(L"mask_corner", profileMaskCorner);
        ReadProfileDword(L"mask_offset_y", profileMaskOffsetY);
        ReadProfileDword(L"mask_top_bias", profileMaskTopBias);
        ReadProfileDword(L"mask_bottom_bias", profileMaskBottomBias);
        ReadProfileDword(L"mask_left_bias", profileMaskLeftBias);
        ReadProfileDword(L"mask_right_bias", profileMaskRightBias);
        ReadProfileDword(L"mask_top_curve", profileMaskTopCurve);
        ReadProfileDword(L"mask_bottom_curve", profileMaskBottomCurve);
        ReadProfileDword(L"visor_size", profileVisorSize);
        ReadProfileDword(L"visor_width", profileVisorWidth);
        ReadProfileDword(L"visor_height", profileVisorHeight);
        if (profileVisorSize > 0) {
            ReadProfileDword(L"mask_enabled", profileMaskEnabled);
            maskEnabled = profileMaskEnabled != 0;
            liveVisorUsesProfileOverride = true;
        }

        // Per-app visor shape overrides (ProfileWindow stores apex-y as signed millis
        // (v+1)*1000, inner-low/bridge-width as plain millis v*1000). Initialized from
        // the global values so a missing registry key keeps the global setting.
        DWORD profileOuterApexY = static_cast<DWORD>(std::lround((std::clamp(visorOuterApexY, -1.0, 1.0) + 1.0) * 1000.0));
        DWORD profileInnerLowerY = static_cast<DWORD>(std::lround(visorInnerLowerY * 1000.0));
        DWORD profileBridgeWidth = static_cast<DWORD>(std::lround(visorInnerBridgeWidth * 1000.0));
        DWORD profileBridgeRise = (visorInnerBridgeRise < 0.0 || visorInnerBridgeRise > 0.5)
            ? static_cast<DWORD>(std::lround(20000.0 + (visorInnerBridgeRise + 0.5) * 1000.0))
            : static_cast<DWORD>(std::lround(visorInnerBridgeRise * 1000.0));
        DWORD profileBridgePeakX = (visorInnerBridgePeakX < 0.0 || visorInnerBridgePeakX > 1.0)
            ? static_cast<DWORD>(std::lround(10000.0 + (visorInnerBridgePeakX + 1.0) * 1000.0))
            : static_cast<DWORD>(std::lround(visorInnerBridgePeakX * 1000.0));
        DWORD profileBridgeSteepness = (visorInnerBridgeSteepness < 0.0 || visorInnerBridgeSteepness > 1.0)
            ? static_cast<DWORD>(std::lround(30000.0 + (visorInnerBridgeSteepness + 1.0) * 1000.0))
            : static_cast<DWORD>(std::lround(visorInnerBridgeSteepness * 1000.0));
        DWORD profileNoseSpreadX = static_cast<DWORD>(std::lround(visorNoseSpreadX * 1000.0));
        ReadProfileDword(L"mask_outer_apex_y", profileOuterApexY);
        ReadProfileDword(L"mask_inner_lower_y", profileInnerLowerY);
        ReadProfileDword(L"mask_inner_bridge_width", profileBridgeWidth);
        ReadProfileDword(L"mask_inner_bridge_rise", profileBridgeRise);
        ReadProfileDword(L"mask_inner_bridge_peak_x", profileBridgePeakX);
        ReadProfileDword(L"mask_inner_bridge_steepness", profileBridgeSteepness);
        ReadProfileDword(L"mask_nose_spread_x", profileNoseSpreadX);
        visorOuterApexY = std::clamp(SignedMillisToUnit(profileOuterApexY, visorOuterApexY), -0.5, 0.5);
        visorInnerLowerY = std::clamp(static_cast<double>(profileInnerLowerY) / 1000.0, 0.0, 0.666);
        visorInnerBridgeWidth = std::clamp(static_cast<double>(profileBridgeWidth) / 1000.0, 0.0, 1.0);
        visorInnerBridgeRise = profileBridgeRise >= 20000u
            ? std::clamp((static_cast<double>(profileBridgeRise) - 20000.0) / 1000.0 - 0.5, -0.5, 1.0)
            : std::clamp(static_cast<double>(profileBridgeRise) / 1000.0, 0.0, 0.5);
        // Values below 10000 are the legacy 0..1 encoding. The 10000 marker lets new
        // profiles represent the extended negative range without reinterpreting old tuning.
        visorInnerBridgePeakX = profileBridgePeakX >= 10000u
            ? std::clamp((static_cast<double>(profileBridgePeakX) - 10000.0) / 1000.0 - 1.0, -1.0, 2.0)
            : std::clamp(static_cast<double>(profileBridgePeakX) / 1000.0, 0.0, 1.0);
        visorInnerBridgeSteepness = profileBridgeSteepness >= 30000u
            ? std::clamp((static_cast<double>(profileBridgeSteepness) - 30000.0) / 1000.0 - 1.0, -1.0, 2.0)
            : std::clamp(static_cast<double>(profileBridgeSteepness) / 1000.0, 0.0, 1.0);
        visorNoseSpreadX = std::clamp(static_cast<double>(profileNoseSpreadX) / 1000.0, 0.0, 0.5);
        if (!ReadProfileDouble(L"render_scale", renderScale)) {
            ReadProfileDword(L"render_scale", profileRenderScale);
            renderScale = DwordToRenderScale(profileRenderScale, renderScale);
        }
        renderScale = std::clamp(renderScale, 0.1, 3.0);
        maskVertical = MillisToRenderHeight(profileMaskVertical, maskVertical);
        maskHorizontal = MillisToMaskScale(profileMaskHorizontal, maskHorizontal);
        maskRounded = profileMaskRounded != 0;
        maskCorner = std::clamp(static_cast<double>(profileMaskCorner) / 1000.0, 0.0, 1.0);
        const double oldProfileOffsetY = SignedMillisToUnit(profileMaskOffsetY, (maskTopBias + maskBottomBias) * 0.5);
        maskTopBias = SignedMillisToUnit(profileMaskTopBias, oldProfileOffsetY);
        maskBottomBias = SignedMillisToUnit(profileMaskBottomBias, oldProfileOffsetY);
        maskLeftBias = SignedMillisToUnit(profileMaskLeftBias, maskLeftBias);
        maskRightBias = SignedMillisToUnit(profileMaskRightBias, maskRightBias);
        maskTopCurve = SignedMillisToUnit(profileMaskTopCurve, maskTopCurve);
        maskBottomCurve = SignedMillisToUnit(profileMaskBottomCurve, maskBottomCurve);

        auto readOverlayPlacement = [&](const wchar_t* xKey,const wchar_t* yKey,const wchar_t* scaleKey,
            double& x,double& y,double& scale,double minX,double maxX,double minScale,double maxScale,OverlayFeatureId feature) {
            double px=x,py=y,ps=scale;
            const bool present=ReadProfileDouble(xKey,px)|ReadProfileDouble(yKey,py)|ReadProfileDouble(scaleKey,ps);
            if(!present)return;
            x=std::clamp(px,minX,maxX);y=std::clamp(py,minX,maxX);scale=std::clamp(ps,minScale,maxScale);
            profileOverlayOverrideMask|=1u<<(uint32_t)feature;
        };
        readOverlayPlacement(L"overlay_layout_hud_x",L"overlay_layout_hud_y",L"overlay_layout_hud_scale",hudAnchorX,hudAnchorY,hudScale,0,1,.15,3,OverlayFeatureId::Hud);
        readOverlayPlacement(L"overlay_layout_trace_x",L"overlay_layout_trace_y",L"overlay_layout_trace_scale",hudTraceX,hudTraceY,hudTraceScale,0,1,.25,3,OverlayFeatureId::Trace);
        readOverlayPlacement(L"overlay_layout_clock_x",L"overlay_layout_clock_y",L"overlay_layout_clock_scale",clockWidgetX,clockWidgetY,clockWidgetScale,0,1,.1,2,OverlayFeatureId::Clock);
        readOverlayPlacement(L"overlay_layout_crosshair_x",L"overlay_layout_crosshair_y",L"overlay_layout_crosshair_scale",crosshairOffsetX,crosshairOffsetY,crosshairScale,-1,1,.1,5,OverlayFeatureId::Crosshair);
        readOverlayPlacement(L"overlay_layout_notifications_x",L"overlay_layout_notifications_y",L"overlay_layout_notifications_scale",notifyX,notifyY,notifyScale,0,1,.1,3,OverlayFeatureId::Notifications);
        for(size_t i=0;i<stickyNoteCount&&i<kStickyNoteMax;++i){
            wchar_t xKey[64]{},yKey[64]{},scaleKey[64]{};swprintf_s(xKey,L"overlay_layout_sticky_%zu_x",i);swprintf_s(yKey,L"overlay_layout_sticky_%zu_y",i);swprintf_s(scaleKey,L"overlay_layout_sticky_%zu_scale",i);
            double x=stickyNotes[i].x,y=stickyNotes[i].y,scale=stickyNotes[i].scale;
            if(ReadProfileDouble(xKey,x)|ReadProfileDouble(yKey,y)|ReadProfileDouble(scaleKey,scale)){stickyNotes[i].x=std::clamp(x,0.0,1.0);stickyNotes[i].y=std::clamp(y,0.0,1.0);stickyNotes[i].scale=std::clamp(scale,.5,2.5);profileStickyOverlayOverrideMask|=1u<<i;profileOverlayOverrideMask|=1u<<(uint32_t)OverlayFeatureId::StickyNote;}
        }

        auto readOverlayBool=[&](const wchar_t* key,bool& value,OverlayFeatureId feature){double raw=value?1.0:0.0;if(!ReadProfileDouble(key,raw))return false;value=raw!=0.0;profileOverlayOverrideMask|=1u<<(uint32_t)feature;return true;};
        auto readOverlayDouble=[&](const wchar_t* key,double& value,double lo,double hi,OverlayFeatureId feature){double raw=value;if(!ReadProfileDouble(key,raw))return false;value=std::clamp(raw,lo,hi);profileOverlayOverrideMask|=1u<<(uint32_t)feature;return true;};
        auto readOverlayU32=[&](const wchar_t* key,uint32_t& value,uint32_t lo,uint32_t hi,OverlayFeatureId feature){double raw=value;if(!ReadProfileDouble(key,raw))return false;value=std::clamp((uint32_t)std::lround(raw),lo,hi);profileOverlayOverrideMask|=1u<<(uint32_t)feature;return true;};

        readOverlayBool(L"overlay_override_clock__clock_widget_enabled",clockWidgetEnabled,OverlayFeatureId::Clock);
        readOverlayBool(L"overlay_override_clock__clock_session_timer_enabled",clockSessionTimerEnabled,OverlayFeatureId::Clock);
        readOverlayBool(L"overlay_override_clock__clock_24_hour",clock24Hour,OverlayFeatureId::Clock);
        readOverlayDouble(L"overlay_override_clock__clock_widget_x",clockWidgetX,0,1,OverlayFeatureId::Clock);
        readOverlayDouble(L"overlay_override_clock__clock_widget_y",clockWidgetY,0,1,OverlayFeatureId::Clock);
        readOverlayDouble(L"overlay_override_clock__clock_widget_scale",clockWidgetScale,.1,2,OverlayFeatureId::Clock);
        readOverlayDouble(L"overlay_override_clock__clock_widget_opacity",clockWidgetOpacity,.1,1,OverlayFeatureId::Clock);
        readOverlayU32(L"overlay_override_clock__clock_widget_theme",clockWidgetTheme,0,3,OverlayFeatureId::Clock);
        readOverlayU32(L"overlay_override_clock__clock_widget_palette",clockWidgetPalette,0,4,OverlayFeatureId::Clock);

        readOverlayBool(L"overlay_override_hud__hud_enabled",hudEnabled,OverlayFeatureId::Hud);
        readOverlayBool(L"overlay_override_hud__hud_alarm_only",hudAlarmOnly,OverlayFeatureId::Hud);
        readOverlayBool(L"overlay_override_hud__hud_clamp_to_visible",hudClampToVisible,OverlayFeatureId::Hud);
        readOverlayDouble(L"overlay_override_hud__hud_anchor_x",hudAnchorX,0,1,OverlayFeatureId::Hud);
        readOverlayDouble(L"overlay_override_hud__hud_anchor_y",hudAnchorY,0,1,OverlayFeatureId::Hud);
        readOverlayDouble(L"overlay_override_hud__hud_scale",hudScale,.15,3,OverlayFeatureId::Hud);
        readOverlayDouble(L"overlay_override_hud__hud_opacity",hudOpacity,.1,1,OverlayFeatureId::Hud);
        readOverlayDouble(L"overlay_override_hud__hud_safe_margin",hudSafeMargin,0,.25,OverlayFeatureId::Hud);
        constexpr const wchar_t* profileWidgetKeys[kHudWidgetCount]={L"cpu",L"gpu",L"app",L"vr",L"cpu_peak",L"cpu_frequency",L"ram",L"commit",L"vram",L"sys",L"fps",L"frame_interval",L"network_ping",L"network_loss",L"network_jitter",L"network_status"};
        std::array<std::pair<int,uint8_t>,kHudWidgetCount> profileWidgetOrder{};
        for(size_t i=0;i<kHudWidgetCount;++i){
            wchar_t enabledKey[128]{},symbolKey[128]{},orderKey[128]{},warningKey[128]{},criticalKey[128]{};
            swprintf_s(enabledKey,L"overlay_override_hud__hud_widget_%s_enabled",profileWidgetKeys[i]);swprintf_s(symbolKey,L"overlay_override_hud__hud_widget_%s_symbol",profileWidgetKeys[i]);swprintf_s(orderKey,L"overlay_override_hud__hud_widget_%s_order",profileWidgetKeys[i]);swprintf_s(warningKey,L"overlay_override_hud__hud_widget_%s_warning",profileWidgetKeys[i]);swprintf_s(criticalKey,L"overlay_override_hud__hud_widget_%s_critical",profileWidgetKeys[i]);
            bool enabled=(hudWidgetMask&(1ull<<i))!=0;if(readOverlayBool(enabledKey,enabled,OverlayFeatureId::Hud)){if(enabled)hudWidgetMask|=1ull<<i;else hudWidgetMask&=~(1ull<<i);}
            bool symbol=(hudWidgetSymbolMask&(1u<<i))!=0;if(readOverlayBool(symbolKey,symbol,OverlayFeatureId::Hud)){if(symbol)hudWidgetSymbolMask|=1u<<i;else hudWidgetSymbolMask&=~(1u<<i);}
            double order=(double)i;ReadProfileDouble(orderKey,order);profileWidgetOrder[i]={(int)order,(uint8_t)i};
            readOverlayDouble(warningKey,hudWidgetWarning[i],0,1000000,OverlayFeatureId::Hud);readOverlayDouble(criticalKey,hudWidgetCritical[i],0,1000000,OverlayFeatureId::Hud);
        }
        if((profileOverlayOverrideMask&(1u<<(uint32_t)OverlayFeatureId::Hud))!=0){std::stable_sort(profileWidgetOrder.begin(),profileWidgetOrder.end(),[](const auto&a,const auto&b){return a.first<b.first;});for(size_t i=0;i<kHudWidgetCount;++i)hudWidgetOrder[i]=profileWidgetOrder[i].second;viewlab::telemetry::SetNetworkProbeEnabled((hudWidgetMask&(0xFull<<12))!=0);}

        readOverlayBool(L"overlay_override_trace__hud_trace_enabled",hudTraceEnabled,OverlayFeatureId::Trace);
        readOverlayU32(L"overlay_override_trace__hud_trace_visibility_mode",hudTraceVisibilityMode,0,2,OverlayFeatureId::Trace);
        readOverlayDouble(L"overlay_override_trace__hud_trace_x",hudTraceX,0,1,OverlayFeatureId::Trace);
        readOverlayDouble(L"overlay_override_trace__hud_trace_y",hudTraceY,0,1,OverlayFeatureId::Trace);
        readOverlayDouble(L"overlay_override_trace__hud_trace_scale",hudTraceScale,.25,3,OverlayFeatureId::Trace);
        readOverlayDouble(L"overlay_override_trace__hud_trace_width",hudTraceWidth,.1,1,OverlayFeatureId::Trace);
        readOverlayDouble(L"overlay_override_trace__hud_trace_opacity",hudTraceOpacity,.1,1,OverlayFeatureId::Trace);
        readOverlayDouble(L"overlay_override_trace__hud_trace_history",hudTraceHistory,10,600,OverlayFeatureId::Trace);
        readOverlayDouble(L"overlay_override_trace__hud_trace_sensitivity_ms",hudTraceSensitivityMs,.25,8,OverlayFeatureId::Trace);
        uint32_t graphMode=(uint32_t)hudGraphMode;if(readOverlayU32(L"overlay_override_trace__hud_graph_mode",graphMode,0,3,OverlayFeatureId::Trace))hudGraphMode=(HudGraphMode)graphMode;
        readOverlayBool(L"overlay_override_trace__performance_trace_recording",performanceTraceRecording,OverlayFeatureId::Trace);double markerKey=performanceTraceMarkerKey;if(ReadProfileDouble(L"overlay_override_trace__performance_trace_marker_vk",markerKey)){performanceTraceMarkerKey=(int)std::clamp(markerKey,1.0,255.0);profileOverlayOverrideMask|=1u<<(uint32_t)OverlayFeatureId::Trace;}
        struct ProfileGraphKey{const wchar_t* key;uint32_t flag;};const ProfileGraphKey graphKeys[]={{L"overlay_override_trace__hud_graph_frame_interval",GraphFrameInterval},{L"overlay_override_trace__hud_graph_fps",GraphFps},{L"overlay_override_trace__hud_graph_budget_deviation",GraphBudgetDeviation},{L"overlay_override_trace__hud_graph_app_work",GraphAppWork},{L"overlay_override_trace__hud_graph_wait_duration",GraphWaitDuration},{L"overlay_override_trace__hud_graph_submit_duration",GraphSubmitDuration},{L"overlay_override_trace__hud_graph_display_period",GraphDisplayPeriod}};
        for(const auto& graph:graphKeys){bool enabled=(hudGraphChannels&graph.flag)!=0;if(readOverlayBool(graph.key,enabled,OverlayFeatureId::Trace)){if(enabled)hudGraphChannels|=graph.flag;else hudGraphChannels&=~graph.flag;}}

        readOverlayBool(L"overlay_override_sticky__sticky_note_enabled",stickyNoteEnabled,OverlayFeatureId::StickyNote);
        double profileStickyCount=(double)stickyNoteCount;
        if(ReadProfileDouble(L"overlay_override_sticky__sticky_note_count",profileStickyCount)){
            profileOverlayOverrideMask|=1u<<(uint32_t)OverlayFeatureId::StickyNote;stickyNoteCount=(size_t)std::clamp(std::round(profileStickyCount),0.0,(double)kStickyNoteMax);
            for(size_t i=0;i<stickyNoteCount;++i){
                wchar_t enabledKey[128]{},textKey[128]{},xKey[128]{},yKey[128]{},scaleKey[128]{},opacityKey[128]{},themeKey[128]{};
                swprintf_s(enabledKey,L"overlay_override_sticky__sticky_note_%zu_enabled",i);swprintf_s(textKey,L"overlay_override_sticky__sticky_note_%zu_text",i);swprintf_s(xKey,L"overlay_override_sticky__sticky_note_%zu_x",i);swprintf_s(yKey,L"overlay_override_sticky__sticky_note_%zu_y",i);swprintf_s(scaleKey,L"overlay_override_sticky__sticky_note_%zu_scale",i);swprintf_s(opacityKey,L"overlay_override_sticky__sticky_note_%zu_opacity",i);swprintf_s(themeKey,L"overlay_override_sticky__sticky_note_%zu_theme",i);
                auto& note=stickyNotes[i];readOverlayBool(enabledKey,note.enabled,OverlayFeatureId::StickyNote);std::wstring noteText;if(ReadProfileString(textKey,noteText)){note.text.assign(noteText.begin(),noteText.end());profileOverlayOverrideMask|=1u<<(uint32_t)OverlayFeatureId::StickyNote;}
                readOverlayDouble(xKey,note.x,0,1,OverlayFeatureId::StickyNote);readOverlayDouble(yKey,note.y,0,1,OverlayFeatureId::StickyNote);readOverlayDouble(scaleKey,note.scale,.5,2.5,OverlayFeatureId::StickyNote);readOverlayDouble(opacityKey,note.opacity,.1,1,OverlayFeatureId::StickyNote);readOverlayU32(themeKey,note.theme,0,4,OverlayFeatureId::StickyNote);
            }
        }

        readOverlayBool(L"overlay_override_crosshair__crosshair_enabled",crosshairEnabled,OverlayFeatureId::Crosshair);
        readOverlayBool(L"overlay_override_crosshair__crosshair_dot",crosshairDot,OverlayFeatureId::Crosshair);
        readOverlayBool(L"overlay_override_crosshair__crosshair_outline",crosshairOutline,OverlayFeatureId::Crosshair);
        readOverlayBool(L"overlay_override_crosshair__crosshair_tstyle",crosshairTStyle,OverlayFeatureId::Crosshair);
        readOverlayDouble(L"overlay_override_crosshair__crosshair_offset_x",crosshairOffsetX,-1,1,OverlayFeatureId::Crosshair);
        readOverlayDouble(L"overlay_override_crosshair__crosshair_offset_y",crosshairOffsetY,-1,1,OverlayFeatureId::Crosshair);
        readOverlayDouble(L"overlay_override_crosshair__crosshair_size",crosshairSize,0,1000,OverlayFeatureId::Crosshair);
        readOverlayDouble(L"overlay_override_crosshair__crosshair_gap",crosshairGap,-50,50,OverlayFeatureId::Crosshair);
        readOverlayDouble(L"overlay_override_crosshair__crosshair_thickness",crosshairThickness,.1,50,OverlayFeatureId::Crosshair);
        readOverlayDouble(L"overlay_override_crosshair__crosshair_outline_thickness",crosshairOutlineThickness,0,10,OverlayFeatureId::Crosshair);
        readOverlayDouble(L"overlay_override_crosshair__crosshair_scale",crosshairScale,.1,10,OverlayFeatureId::Crosshair);
        double crosshairAlphaValue=crosshairAlpha;if(readOverlayDouble(L"overlay_override_crosshair__crosshair_alpha",crosshairAlphaValue,0,1,OverlayFeatureId::Crosshair))crosshairAlpha=(float)crosshairAlphaValue;
        double crosshairColorValue=((uint32_t)(crosshairR*255)<<16)|((uint32_t)(crosshairG*255)<<8)|(uint32_t)(crosshairB*255);if(readOverlayDouble(L"overlay_override_crosshair__crosshair_color",crosshairColorValue,0,0xFFFFFF,OverlayFeatureId::Crosshair)){uint32_t color=(uint32_t)std::lround(crosshairColorValue);crosshairR=((color>>16)&255)/255.f;crosshairG=((color>>8)&255)/255.f;crosshairB=(color&255)/255.f;}

        readOverlayBool(L"overlay_override_notifications__notify_enabled",notifyEnabled,OverlayFeatureId::Notifications);
        readOverlayDouble(L"overlay_override_notifications__notify_x",notifyX,0,1,OverlayFeatureId::Notifications);
        readOverlayDouble(L"overlay_override_notifications__notify_y",notifyY,0,1,OverlayFeatureId::Notifications);
        readOverlayDouble(L"overlay_override_notifications__notify_scale",notifyScale,.1,3,OverlayFeatureId::Notifications);
        readOverlayDouble(L"overlay_override_notifications__notify_opacity",notifyOpacity,.1,1,OverlayFeatureId::Notifications);

        double featureEnabled=obsIndicatorEnabled?1.0:0.0;if(ReadProfileDouble(L"overlay_override_obs__obs_indicator_enabled",featureEnabled)){obsIndicatorEnabled=featureEnabled!=0;profileObsFeatureOverride=true;}
        featureEnabled=iracingEnabled?1.0:0.0;if(ReadProfileDouble(L"overlay_override_iracing__iracing_enabled",featureEnabled)){iracingEnabled=featureEnabled!=0;profileIRacingFeatureOverride=true;}
        const struct{const wchar_t* key;OverlayFeatureId feature;} profileHotkeys[]={{L"overlay_override_hud__overlay_hud_toggle_vk",OverlayFeatureId::Hud},{L"overlay_override_trace__overlay_trace_toggle_vk",OverlayFeatureId::Trace},{L"overlay_override_clock__overlay_clock_toggle_vk",OverlayFeatureId::Clock},{L"overlay_override_sticky__overlay_sticky_note_toggle_vk",OverlayFeatureId::StickyNote},{L"overlay_override_crosshair__overlay_crosshair_toggle_vk",OverlayFeatureId::Crosshair},{L"overlay_override_notifications__overlay_notifications_toggle_vk",OverlayFeatureId::Notifications}};
        for(const auto& hotkey:profileHotkeys){double value=g_overlayFeatureVisibility[(size_t)hotkey.feature].toggleKey;if(ReadProfileDouble(hotkey.key,value)){g_overlayFeatureVisibility[(size_t)hotkey.feature].toggleKey=(int)std::clamp(value,0.0,255.0);profileOverlayOverrideMask|=1u<<(uint32_t)hotkey.feature;}}

        splitMode = profileSplitMode != 0;
        totalTangent = MillisToRenderHeight(profileTotal, totalTangent);
        horizontalRenderWidth = MillisToRenderScale(profileHorizontal, horizontalRenderWidth);
        if (splitMode) {
            topTangent = MillisToRenderHeight(profileTop, topTangent);
            bottomTangent = MillisToRenderHeight(profileBottom, bottomTangent);
            totalTangent = std::clamp(topTangent + bottomTangent, MinVerticalTangent, MaxVerticalTangent);
        } else {
            topTangent = totalTangent * 0.5;
            bottomTangent = totalTangent * 0.5;
        }
    }

    // Size controls only the exposed aperture (1.0 preserves the previous full opening).
    visorSize = std::clamp(ReadDoubleSetting(L"mask_size", 1.0), 0.05, 1.0);
    visorWidth = std::clamp(ReadDoubleSetting(L"mask_width_scale", 1.0), 0.25, 2.0);
    visorHeight = std::clamp(ReadDoubleSetting(L"mask_height_scale", 1.0), 0.25, 2.0);
    if (profileVisorSize > 0)
        visorSize = std::clamp(static_cast<double>(profileVisorSize) / 1000.0, 0.05, 1.0);
    if (profileVisorWidth > 0)
        visorWidth = std::clamp(static_cast<double>(profileVisorWidth) / 1000.0, 0.25, 2.0);
    if (profileVisorHeight > 0)
        visorHeight = std::clamp(static_cast<double>(profileVisorHeight) / 1000.0, 0.25, 2.0);
    visorCurve = std::clamp(1.0 - maskCorner, 0.0, 1.0);
    // X/Y visor positioning was removed from the product UI. Ignore stale global or
    // per-app bias values so old profiles cannot reintroduce binocular mismatch.
    visorOffsetX = 0.0;
    visorOffsetY = 0.0;
    liveVisorRevision = ReadDoubleSetting(L"visor_live_revision", 0.0);

    Log("config: enabled=%d app=%ls mode=%s total_render_height=%.3f top_render_height=%.3f bottom_render_height=%.3f horizontal_render_width=%.3f top_scale=%.3f bottom_scale=%.3f foveated_center_compensation=%d visual_mask_only=%d horizontal_visual_mask_only=%d outer_edge_visibility_mask_only=%d crop_outer_edges_only=%d visor_enabled=%d visor_size=%.3f visor_width=%.3f visor_height=%.3f visor_curve=%.3f visor_offset_x=%.3f visor_offset_y=%.3f visor_outer_apex_y=%.3f visor_inner_lower_y=%.3f visor_inner_bridge_w/r/px/s=%.3f/%.3f/%.3f/%.3f render_scale=%.6f uevr_like=%d verbose_logging=%d\n",
        enabled ? 1 : 0,
        currentAppKey.empty() ? L"<global>" : currentAppKey.c_str(),
        splitMode ? "split" : "total",
        totalTangent,
        topTangent,
        bottomTangent,
        horizontalRenderWidth,
        std::clamp(topTangent * 2.0, 0.0, 1.0),
        std::clamp(bottomTangent * 2.0, 0.0, 1.0),
        foveatedCenterCompensation ? 1 : 0,
        visualMaskOnly ? 1 : 0,
        horizontalVisualMaskOnly ? 1 : 0,
        outerEdgeVisibilityMaskOnly ? 1 : 0,
        cropOuterEdgesOnly ? 1 : 0,
        maskEnabled ? 1 : 0,
        visorSize,
        visorWidth,
        visorHeight,
        visorCurve,
        visorOffsetX,
        visorOffsetY,
        visorOuterApexY,
        visorInnerLowerY,
        visorInnerBridgeWidth,
        visorInnerBridgeRise,
        visorInnerBridgePeakX,
        visorInnerBridgeSteepness,
        renderScale,
        uevrLikeProcess ? 1 : 0,
        verboseLogging ? 1 : 0);
}

// Only visor values are refreshed, and only at the end-of-frame safe point while the
// renderer lock is held. Crop/FOV/resolution and per-app profiles stay startup snapshots.
void RefreshLiveVisorConfig() {
    if (liveVisorUsesProfileOverride) return;

    const double revision = ReadDoubleSetting(L"visor_live_revision", liveVisorRevision);
    if (revision <= liveVisorRevision) return;

    maskEnabled = ReadBoolSetting(L"mask_enabled", maskEnabled);
    maskCorner = std::clamp(ReadDoubleSetting(L"mask_corner", maskCorner), 0.0, 1.0);
    visorSize = std::clamp(ReadDoubleSetting(L"mask_size", visorSize), 0.1, 1.0);
    visorWidth = std::clamp(ReadDoubleSetting(L"mask_width_scale", visorWidth), 0.25, 2.0);
    visorHeight = std::clamp(ReadDoubleSetting(L"mask_height_scale", visorHeight), 0.25, 2.0);
    visorOuterApexY = std::clamp(ReadDoubleSetting(L"mask_outer_apex_y", visorOuterApexY), -0.5, 0.5);
    visorInnerLowerY = std::clamp(ReadDoubleSetting(L"mask_inner_lower_y", visorInnerLowerY), 0.0, 0.666);
    visorInnerBridgeWidth = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_width", visorInnerBridgeWidth), 0.0, 1.0);
    visorInnerBridgeRise = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_rise", visorInnerBridgeRise), -0.5, 1.0);
    visorInnerBridgePeakX = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_peak_x", visorInnerBridgePeakX), -1.0, 2.0);
    visorInnerBridgeSteepness = std::clamp(ReadDoubleSetting(L"mask_inner_bridge_steepness", visorInnerBridgeSteepness), -1.0, 2.0);
    visorNoseSpreadX = std::clamp(ReadDoubleSetting(L"mask_nose_spread_x", visorNoseSpreadX), 0.0, 0.5);
    visorCurve = std::clamp(1.0 - maskCorner, 0.0, 1.0);
    liveVisorRevision = revision;
    Log("visor: live settings refreshed revision=%.0f size=%.3f curve=%.3f apex=%.3f inner=%.3f bridge=%.3f/%.3f/%.3f/%.3f\n",
        revision, visorSize, visorCurve, visorOuterApexY, visorInnerLowerY,
        visorInnerBridgeWidth, visorInnerBridgeRise, visorInnerBridgePeakX, visorInnerBridgeSteepness);
}

void EnsureInitialized() {
    if (layerDirectory.empty()) {
        layerDirectory = GetLayerDirectory();
    }

    if (!sessionLogged) {
        sessionLogged = true;
        Log("------------------------------------------------------------\n");
        Log("XR ViewLab layer starting\n");
        Log("layer_dir=%ls\n", layerDirectory.empty() ? L"<unknown>" : layerDirectory.c_str());
        Log("config_path=%ls\n", ConfigPath().c_str());
        Log("log_path=%ls\n", LogPath().c_str());
        Log("process=%ls\n", CurrentProcessPath().c_str());
    }

    LoadConfig();
}

double EffectiveOuterEdgeHorizontalScale() {
    const double horizontalScale = std::clamp(horizontalRenderWidth, MinRenderScale, 1.0);
	// horizontal_render_width is retained per-eye tangent width. Outer-edge-only chooses
	// which boundary moves; it does not halve the requested reduction.
    return horizontalScale;
}

double ConservativeRecommendedScale(double scale) {
    if (!uevrLikeProcess) {
        return scale;
    }

    return std::clamp(scale, MinUevrRecommendedScale, 1.0);
}

double EffectiveHorizontalScaleForView(uint32_t viewIndex, double requestedWidthScale) {
    if (viewIndex >= g_d3d11Mask.latestViewCount) {
        return ConservativeRecommendedScale(requestedWidthScale);
    }

    const XrFovf& original = g_d3d11Mask.latestOriginalViews[viewIndex].fov;
    const XrFovf& cropped = g_d3d11Mask.latestViews[viewIndex].fov;
    const double originalWidth =
        std::tan(static_cast<double>(original.angleRight)) -
        std::tan(static_cast<double>(original.angleLeft));
    const double croppedWidth =
        std::tan(static_cast<double>(cropped.angleRight)) -
        std::tan(static_cast<double>(cropped.angleLeft));
    if (!std::isfinite(originalWidth) || std::abs(originalWidth) < 0.00001 ||
        !std::isfinite(croppedWidth)) {
        return ConservativeRecommendedScale(requestedWidthScale);
    }
    return ConservativeRecommendedScale(std::clamp(croppedWidth / originalWidth, MinRenderScale, 1.0));
}

double EffectiveVerticalScaleForView(uint32_t viewIndex, double requestedHeightScale) {
    if (viewIndex >= g_d3d11Mask.latestViewCount) {
        return ConservativeRecommendedScale(requestedHeightScale);
    }

    const XrFovf& original = g_d3d11Mask.latestOriginalViews[viewIndex].fov;
    const XrFovf& cropped = g_d3d11Mask.latestViews[viewIndex].fov;
    const double originalHeight =
        std::tan(static_cast<double>(original.angleUp)) -
        std::tan(static_cast<double>(original.angleDown));
    const double croppedHeight =
        std::tan(static_cast<double>(cropped.angleUp)) -
        std::tan(static_cast<double>(cropped.angleDown));
    if (!std::isfinite(originalHeight) || std::abs(originalHeight) < 0.00001 ||
        !std::isfinite(croppedHeight)) {
        return ConservativeRecommendedScale(requestedHeightScale);
    }
    return ConservativeRecommendedScale(std::clamp(croppedHeight / originalHeight, MinRenderScale, 1.0));
}


void ApplyXRViewLabFov(uint32_t viewIndex, XrView& view, bool& compensated, float& pitchOffset) {
    const double topScale = std::clamp(topTangent * 2.0, 0.0, 1.0);
    const double bottomScale = std::clamp(bottomTangent * 2.0, 0.0, 1.0);
    const double horizontalScale = std::clamp(horizontalRenderWidth, MinRenderScale, 1.0);
    const double originalLeftTan = std::tan(static_cast<double>(view.fov.angleLeft));
    const double originalRightTan = std::tan(static_cast<double>(view.fov.angleRight));
    double desiredLeftTan = originalLeftTan;
    double desiredRightTan = originalRightTan;
    if (cropOuterEdgesOnly && viewIndex == 0) {
		// Left eye: retain exactly horizontalScale of the full tangent width while the
		// inner/right boundary remains fixed.
        desiredLeftTan = originalRightTan - (originalRightTan - originalLeftTan) * horizontalScale;
    } else if (cropOuterEdgesOnly && viewIndex == 1) {
		// Right eye: mirrored; the inner/left boundary remains fixed.
        desiredRightTan = originalLeftTan + (originalRightTan - originalLeftTan) * horizontalScale;
    } else {
		// Legacy full crop scales both sides around tangent zero.
        desiredLeftTan = originalLeftTan * horizontalScale;
        desiredRightTan = originalRightTan * horizontalScale;
    }
    const double originalTopTan = (std::max)(0.0, std::tan(static_cast<double>(view.fov.angleUp)));
    const double originalBottomTan = (std::max)(0.0, -std::tan(static_cast<double>(view.fov.angleDown)));
    const double desiredTopTan = originalTopTan * topScale;
    const double desiredBottomTan = originalBottomTan * bottomScale;

    view.fov.angleLeft = static_cast<float>(std::atan(desiredLeftTan));
    view.fov.angleRight = static_cast<float>(std::atan(desiredRightTan));

    compensated = false;
    pitchOffset = 0.0f;
    view.fov.angleUp = static_cast<float>(std::atan(desiredTopTan));
    view.fov.angleDown = -static_cast<float>(std::atan(desiredBottomTan));
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrLocateViews(
    XrSession session,
    const XrViewLocateInfo* viewLocateInfo,
    XrViewState* viewState,
    uint32_t viewCapacityInput,
    uint32_t* viewCountOutput,
    XrView* views) {
    const XrResult result = nextXrLocateViews(session, viewLocateInfo, viewState, viewCapacityInput, viewCountOutput, views);

    if (!enabled ||
        result != XR_SUCCESS ||
        viewLocateInfo == nullptr ||
        viewCountOutput == nullptr ||
        views == nullptr ||
        viewLocateInfo->viewConfigurationType != XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO) {
        return result;
    }

    if (viewLocateInfo != nullptr &&
        *viewCountOutput >= 2 &&
        viewCapacityInput >= 2) {
        g_d3d11Mask.locateSpace = viewLocateInfo->space;
        g_d3d11Mask.locateTime = viewLocateInfo->displayTime;
        g_d3d11Mask.latestViewCount = (std::min)(*viewCountOutput, 2u);
    }

    LocateViewsEvidence evidence{};
    evidence.callSerial = g_pipelineLocateSerial.fetch_add(1) + 1;
    evidence.session = session;
    evidence.displayTime = viewLocateInfo->displayTime;
    evidence.space = viewLocateInfo->space;
    evidence.viewStateFlags = viewState ? viewState->viewStateFlags : 0;
    evidence.viewCount = (std::min)(*viewCountOutput, 2u);
    if (TracePipelineSerial(evidence.callSerial)) {
        LogVerbose("PIPE locate serial=%llu frame=%llu displayTime=%lld space=%p spaceType=%s flags=0x%llX count=%u capacity=%u result=%d\n",
            static_cast<unsigned long long>(evidence.callSerial),
            static_cast<unsigned long long>(g_pipelineFrameSerial.load()),
            static_cast<long long>(evidence.displayTime), reinterpret_cast<void*>(evidence.space),
            ReferenceSpaceTypeName(TrackedReferenceSpaceType(evidence.space)),
            static_cast<unsigned long long>(evidence.viewStateFlags), evidence.viewCount,
            viewCapacityInput, result);
    }

    const uint32_t logCount = locateViewsLogCount.fetch_add(1);
    if (maskEnabled && visibilityMaskLogCount.load() == 0 && logCount == 300) {
        const uint32_t warnCount = visorMaskNoHookLogCount.fetch_add(1);
        if (warnCount == 0) {
            Log("visibility mask: rounded visor enabled, but xrGetVisibilityMaskKHR has not been called after 300 locate frames; this runtime path may not consume OpenXR visibility masks, so no in-HMD rounded mask will be visible\n");
        }
    }
    for (uint32_t i = 0; i < *viewCountOutput; ++i) {
        const XrFovf before = views[i].fov;
        const XrView originalView = views[i];
        bool compensated = false;
        float pitchOffset = 0.0f;
        ApplyXRViewLabFov(i, views[i], compensated, pitchOffset);
        if (i < g_d3d11Mask.latestViewCount) {
            g_d3d11Mask.latestOriginalViews[i] = originalView;
            g_d3d11Mask.latestViews[i] = views[i];
        }
        if (i < evidence.viewCount) {
            evidence.original[i] = originalView;
            evidence.cropped[i] = views[i];
            if (TracePipelineSerial(evidence.callSerial)) {
                LogPipelinePose("locate-original", evidence.callSerial, i, originalView.pose, originalView.fov);
                LogPipelinePose("locate-cropped", evidence.callSerial, i, views[i].pose, views[i].fov);
            }
        }
        if (verboseLogging && (logCount < 20 || logCount % 300 == 0)) {
            LogVerbose("xrLocateViews[%u]: up %.5f -> %.5f down %.5f -> %.5f left %.5f -> %.5f right %.5f -> %.5f original_fov=(L %.5f R %.5f U %.5f D %.5f valid=%d) horizontal_render_width=%.3f crop_outer_edges_only=%d foveated_center=%d pitch_offset=%.5f\n",
                i,
                before.angleUp,
                views[i].fov.angleUp,
                before.angleDown,
                views[i].fov.angleDown,
                before.angleLeft,
                views[i].fov.angleLeft,
                before.angleRight,
                views[i].fov.angleRight,
                before.angleLeft,
                before.angleRight,
                before.angleUp,
                before.angleDown,
                i < g_d3d11Mask.latestViewCount ? 1 : 0,
                horizontalRenderWidth,
                cropOuterEdgesOnly ? 1 : 0,
                compensated ? 1 : 0,
                pitchOffset);
        }
    }

    {
        std::lock_guard<std::mutex> lock(g_pipelineEvidenceMutex);
        g_locateViewsEvidence.push_back(evidence);
        while (g_locateViewsEvidence.size() > kLocateEvidenceCapacity) g_locateViewsEvidence.pop_front();
    }

    if (logCount == 0) {
        Log("render: FOV adjustment active (per-frame detail in verbose log only)\n");
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrCreateSession(
    XrInstance instance,
    const XrSessionCreateInfo* createInfo,
    XrSession* session) {
    const XrResult result = nextXrCreateSession(instance, createInfo, session);
    if (result != XR_SUCCESS || createInfo == nullptr || session == nullptr || *session == XR_NULL_HANDLE) {
        return result;
    }

    // The ordered feature backend owns a VIEW reference space even when the application never
    // asks for one. Resolve these core entry points from the next layer explicitly; relying on the
    // application's xrGetInstanceProcAddr traffic made capability discovery application-shaped.
    PFN_xrVoidFunction createReferenceSpaceFn=nullptr;
    PFN_xrVoidFunction destroySpaceFn=nullptr;
    if (nextXrGetInstanceProcAddr) {
        if (XR_SUCCEEDED(nextXrGetInstanceProcAddr(instance,"xrCreateReferenceSpace",&createReferenceSpaceFn)))
            nextXrCreateReferenceSpace=reinterpret_cast<PFN_xrCreateReferenceSpace>(createReferenceSpaceFn);
        if (XR_SUCCEEDED(nextXrGetInstanceProcAddr(instance,"xrDestroySpace",&destroySpaceFn)))
            nextXrDestroySpace=reinterpret_cast<PFN_xrDestroySpace>(destroySpaceFn);
    }

    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);

    ReleaseD3D11MaskRenderer();
    DestroyTopmostLayer();
    g_rendererDeviceLost.store(false, std::memory_order_release);
    g_rendererDeviceLossLogged.store(false, std::memory_order_release);
    g_topmostLayerBlocked.store(false, std::memory_order_release);
    g_topmostLayerAttempted=false;
    g_topmostLayerDemanded=false;
    g_topmostBackend=viewlab::bridge::OverlayBackend::DirectEyeTexture;
    g_featurePresentationPlan={true,true,viewlab::bridge::OverlayBackend::FeatureDisabled};
    g_topmostSubmissionLogged.store(false,std::memory_order_release);
    g_pipelineFrameSerial.store(0);
    g_pipelineLocateSerial.store(0);
    g_pipelineSwapchainSerial.store(0);
    {
        std::lock_guard<std::mutex> evidenceLock(g_pipelineEvidenceMutex);
        g_locateViewsEvidence.clear();
        g_referenceSpaceTypes.clear();
    }
    g_d3d11Mask.session = *session;
    g_performanceTraceSession = *session;
    g_clockSessionStartTick.store(GetTickCount64(), std::memory_order_release);
    LARGE_INTEGER traceStart{};QueryPerformanceCounter(&traceStart);BeginPerformanceTraceSession(traceStart.QuadPart);
    const auto* d3d11Binding = reinterpret_cast<const XrGraphicsBindingD3D11KHR*>(
        FindStructInChain(createInfo->next, XR_TYPE_GRAPHICS_BINDING_D3D11_KHR));
    viewlab::telemetry::SetCheckpointCallback(SavePerformanceTraceSession);
    viewlab::telemetry::Start();
    if (d3d11Binding != nullptr && d3d11Binding->device != nullptr) {
        g_d3d11Mask.device = d3d11Binding->device;
        g_d3d11Mask.device->AddRef();
        g_d3d11Mask.device->GetImmediateContext(&g_d3d11Mask.context);
        uint64_t adapterLuid=0; GetHudRenderAdapterLuid(adapterLuid);
        viewlab::telemetry::SetPreferredAdapterLuid(adapterLuid);
        Log("visor: D3D11 session detected pid=%lu thread=%lu device=%p context=%p adapterLuid=%016llX "
            "featureLevel=0x%X; initializing renderer\n",
            static_cast<unsigned long>(GetCurrentProcessId()),
            static_cast<unsigned long>(GetCurrentThreadId()),
            g_d3d11Mask.device,g_d3d11Mask.context,
            static_cast<unsigned long long>(adapterLuid),
            static_cast<unsigned>(g_d3d11Mask.device->GetFeatureLevel()));
        InitD3D11MaskRenderer();
    } else {
        Log("visor: this game does not use D3D11; ViewLab's visor is unavailable\n");
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrDestroySession(XrSession session) {
    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);
    if (session == g_performanceTraceSession) viewlab::telemetry::RequestCheckpoint();
    if (session == g_topmostLayer.session) DestroyTopmostLayer();
    g_topmostLayerBlocked.store(false, std::memory_order_release);
    g_topmostLayerAttempted=false;
    g_topmostLayerDemanded=false;
    g_topmostBackend=viewlab::bridge::OverlayBackend::DirectEyeTexture;
    g_featurePresentationPlan={true,true,viewlab::bridge::OverlayBackend::FeatureDisabled};
    g_topmostSubmissionLogged.store(false,std::memory_order_release);
    if (session == g_d3d11Mask.session) {
        g_clockSessionStartTick.store(0, std::memory_order_release);
        viewlab::telemetry::Stop();
        viewlab::telemetry::SetPreferredAdapterLuid(0);
        ReleaseD3D11MaskRenderer();
        DisconnectLiveState();
        DisconnectObsMirrorSurface();
        DisconnectViewLabMirror();
        DisconnectTelemetryConfig();
        DisconnectStickyNoteState();
        DisconnectNotify();
        DisconnectObsState();
        DisconnectRacingState();
    }
    g_rendererDeviceLost.store(false, std::memory_order_release);
    g_rendererDeviceLossLogged.store(false, std::memory_order_release);
    const XrResult result=nextXrDestroySession(session);
    if (session == g_performanceTraceSession) g_performanceTraceSession=XR_NULL_HANDLE;
    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrEndSession(XrSession session) {
    if (!nextXrEndSession) return XR_ERROR_FUNCTION_UNSUPPORTED;
    const XrResult result=nextXrEndSession(session);
    if (XR_SUCCEEDED(result) && session == g_performanceTraceSession) viewlab::telemetry::RequestCheckpoint();
    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrWaitFrame(
    XrSession session,
    const XrFrameWaitInfo* frameWaitInfo,
    XrFrameState* frameState) {
    if (!nextXrWaitFrame) return XR_ERROR_FUNCTION_UNSUPPORTED;
    LARGE_INTEGER start{}, stop{};
    if (!g_hudQpcFrequency.QuadPart) QueryPerformanceFrequency(&g_hudQpcFrequency);
    QueryPerformanceCounter(&start);
    const XrResult result = nextXrWaitFrame(session, frameWaitInfo, frameState);
    QueryPerformanceCounter(&stop);
    if (XR_SUCCEEDED(result) && frameState) {
        std::lock_guard<std::mutex> lock(g_hudTimingMutex);
        if(g_hudQpcFrequency.QuadPart>0 && stop.QuadPart>=start.QuadPart)
            g_hudLastWaitDurationMs=1000.0*(double)(stop.QuadPart-start.QuadPart)/g_hudQpcFrequency.QuadPart;
        HudTrackedFrame& frame = g_hudTrackedFrames[g_hudNextWaitFrame++ % g_hudTrackedFrames.size()];
        frame = HudTrackedFrame{};
        frame.frameSerial = g_pipelineFrameSerial.fetch_add(1) + 1;
        frame.displayTime = frameState->predictedDisplayTime;
        frame.displayPeriod = frameState->predictedDisplayPeriod;
        frame.waitStart = start; frame.waitStop = stop; frame.canBegin = true;
        if (TracePipelineSerial(frame.frameSerial))
            LogVerbose("PIPE wait-frame frame=%llu displayTime=%lld period=%lld shouldRender=%d qpcStart=%lld qpcStop=%lld durationMs=%.4f result=%d\n",
                static_cast<unsigned long long>(frame.frameSerial), static_cast<long long>(frame.displayTime),
                static_cast<long long>(frame.displayPeriod), frameState->shouldRender ? 1 : 0,
                static_cast<long long>(start.QuadPart), static_cast<long long>(stop.QuadPart),
                g_hudLastWaitDurationMs, result);
        // The runtime throttles the app inside xrWaitFrame, so the wait-to-wait interval is
        // the app's true frame cadence — the input for reprojection-aware budget detection.
        UpdateHudCadence(stop, frameState->predictedDisplayPeriod);
        const bool markerDown=(GetAsyncKeyState(performanceTraceMarkerKey)&0x8000)!=0;
        if(markerDown&&!g_traceMarkerKeyDown)CapturePerformanceTraceMarker(stop.QuadPart);
        g_traceMarkerKeyDown=markerDown;
        UpdateOverlayFeatureHotkeys();
    }
    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrBeginFrame(
    XrSession session,
    const XrFrameBeginInfo* frameBeginInfo) {
    if (!nextXrBeginFrame) return XR_ERROR_FUNCTION_UNSUPPORTED;
    size_t frameIndex = g_hudTrackedFrames.size();
    LARGE_INTEGER start{}, stop{};
    QueryPerformanceCounter(&start);
    {
        std::lock_guard<std::mutex> lock(g_hudTimingMutex);
        for (size_t i = 0; i < g_hudTrackedFrames.size(); ++i) {
            if (g_hudTrackedFrames[i].canBegin) {
                frameIndex = i; g_hudTrackedFrames[i].canBegin = false; g_hudTrackedFrames[i].begun = true; break;
            }
        }
    }
    const XrResult result = nextXrBeginFrame(session, frameBeginInfo);
    // After the chain returns, the OBS mirror layer (wherever it sits in the implicit stack)
    // has flushed its compositor copy for the previous frame; drawing now keeps ViewLab's
    // selected features on the displayed shared texture for the entire frame window.
    if (XR_SUCCEEDED(result) && !g_rendererDeviceLost.load(std::memory_order_acquire) &&
        g_d3d11Mask.initialized && session == g_d3d11Mask.session)
        DrawObsMirrorSurface();
    QueryPerformanceCounter(&stop);
    if (frameIndex < g_hudTrackedFrames.size()) {
        std::lock_guard<std::mutex> lock(g_hudTimingMutex);
        HudTrackedFrame& frame = g_hudTrackedFrames[frameIndex];
        if (XR_SUCCEEDED(result)) {
            frame.beginStart = start; frame.beginStop = stop;
            if (TracePipelineSerial(frame.frameSerial))
                LogVerbose("PIPE begin-frame frame=%llu displayTime=%lld qpcStart=%lld qpcStop=%lld durationMs=%.4f result=%d\n",
                    static_cast<unsigned long long>(frame.frameSerial), static_cast<long long>(frame.displayTime),
                    static_cast<long long>(start.QuadPart), static_cast<long long>(stop.QuadPart),
                    g_hudQpcFrequency.QuadPart > 0 ? 1000.0 * static_cast<double>(stop.QuadPart - start.QuadPart) / g_hudQpcFrequency.QuadPart : -1.0,
                    result);
        }
        else frame = HudTrackedFrame{};
    }
    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrEndFrame(
    XrSession session,
    const XrFrameEndInfo* frameEndInfo) {
    if (nextXrEndFrame == nullptr || frameEndInfo == nullptr) {
        return nextXrEndFrame ? nextXrEndFrame(session, frameEndInfo) : XR_ERROR_RUNTIME_FAILURE;
    }

    size_t trackedFrameIndex = g_hudTrackedFrames.size();
    uint64_t pipelineFrameSerial = g_pipelineFrameSerial.load();
    LARGE_INTEGER endStart{};
    QueryPerformanceCounter(&endStart);
    {
        std::lock_guard<std::mutex> lock(g_hudTimingMutex);
        for (size_t i = 0; i < g_hudTrackedFrames.size(); ++i) {
            if (g_hudTrackedFrames[i].begun && g_hudTrackedFrames[i].displayTime == frameEndInfo->displayTime) {
                trackedFrameIndex = i; g_hudTrackedFrames[i].endStart = endStart;
                pipelineFrameSerial = g_hudTrackedFrames[i].frameSerial; break;
            }
        }
    }

    LocateViewsEvidence matchedLocate{};
    bool hasMatchedLocate = false;
    {
        std::lock_guard<std::mutex> lock(g_pipelineEvidenceMutex);
        for (auto it = g_locateViewsEvidence.rbegin(); it != g_locateViewsEvidence.rend(); ++it) {
            if (it->session == session && it->displayTime == frameEndInfo->displayTime) {
                matchedLocate = *it;
                hasMatchedLocate = true;
                break;
            }
        }
    }
    if (TracePipelineSerial(pipelineFrameSerial)) {
        LogVerbose("PIPE end-enter frame=%llu displayTime=%lld blendMode=%d layers=%u locateMatch=%d locateSerial=%llu locateSpace=%p locateSpaceType=%s qpc=%lld\n",
            static_cast<unsigned long long>(pipelineFrameSerial), static_cast<long long>(frameEndInfo->displayTime),
            static_cast<int>(frameEndInfo->environmentBlendMode), frameEndInfo->layerCount,
            hasMatchedLocate ? 1 : 0, static_cast<unsigned long long>(matchedLocate.callSerial),
            reinterpret_cast<void*>(matchedLocate.space), ReferenceSpaceTypeName(TrackedReferenceSpaceType(matchedLocate.space)),
            static_cast<long long>(endStart.QuadPart));
        for (uint32_t layerIndex = 0; layerIndex < frameEndInfo->layerCount; ++layerIndex) {
            const XrCompositionLayerBaseHeader* header = frameEndInfo->layers[layerIndex];
            if (!header) {
                LogVerbose("PIPE layer frame=%llu index=%u null=1\n",
                    static_cast<unsigned long long>(pipelineFrameSerial), layerIndex);
                continue;
            }
            LogVerbose("PIPE layer frame=%llu index=%u type=%d flags=0x%llX space=%p spaceType=%s locateSpaceEqual=%d\n",
                static_cast<unsigned long long>(pipelineFrameSerial), layerIndex, static_cast<int>(header->type),
                static_cast<unsigned long long>(header->layerFlags), reinterpret_cast<void*>(header->space),
                ReferenceSpaceTypeName(TrackedReferenceSpaceType(header->space)),
                hasMatchedLocate && header->space == matchedLocate.space ? 1 : 0);
            if (header->type != XR_TYPE_COMPOSITION_LAYER_PROJECTION) continue;
            const auto* projection = reinterpret_cast<const XrCompositionLayerProjection*>(header);
            LogVerbose("PIPE projection frame=%llu layer=%u views=%u next=%p\n",
                static_cast<unsigned long long>(pipelineFrameSerial), layerIndex, projection->viewCount,
                const_cast<void*>(projection->next));
            for (uint32_t viewIndex = 0; viewIndex < projection->viewCount; ++viewIndex) {
                const auto& view = projection->views[viewIndex];
                LogVerbose("PIPE projection-view frame=%llu layer=%u view=%u swapchain=%p rect=(%d,%d %dx%d) array=%u next=%p\n",
                    static_cast<unsigned long long>(pipelineFrameSerial), layerIndex, viewIndex,
                    reinterpret_cast<void*>(view.subImage.swapchain), view.subImage.imageRect.offset.x,
                    view.subImage.imageRect.offset.y, view.subImage.imageRect.extent.width,
                    view.subImage.imageRect.extent.height, view.subImage.imageArrayIndex,
                    const_cast<void*>(view.next));
                LogPipelinePose("projection", pipelineFrameSerial, viewIndex, view.pose, view.fov);
                if (hasMatchedLocate && viewIndex < matchedLocate.viewCount)
                    LogPipelinePose("matched-locate", pipelineFrameSerial, viewIndex,
                        matchedLocate.cropped[viewIndex].pose, matchedLocate.cropped[viewIndex].fov);
            }
        }
    }

    std::lock_guard<std::recursive_mutex> rendererLock(g_rendererMutex);

    // One generation check against the UI-published shared snapshot; no ini parsing here.
    ConsumeLiveState();
    if (g_d3d11Mask.initialized && !RendererDeviceHealthy("xrEndFrame") &&
        g_topmostLayer.swapchain != XR_NULL_HANDLE) {
        BlockTopmostLayer("device removed at xrEndFrame", DXGI_ERROR_DEVICE_REMOVED);
    }
    const XrCompositionLayerProjection* primaryProjection = nullptr;

    if (!g_diagEndFrameCalled.exchange(true)) {
        Log("d3d11 mask DIAG: xrEndFrame hook called (enabled=%d maskEnabled=%d visMaskCalled=%d "
            "sessionMatch=%d d3d11Init=%d layerCount=%u)\n",
            enabled ? 1 : 0, maskEnabled ? 1 : 0, g_visibilityMaskEverCalled.load() ? 1 : 0,
            (session == g_d3d11Mask.session) ? 1 : 0, g_d3d11Mask.initialized ? 1 : 0,
            frameEndInfo->layerCount);
    }

    // Capture one complete ordered projection context and bind each view to its
    // submitted swapchain sub-image. The actual draw happens in
    // xrReleaseSwapchainImage, which runs earlier in the frame loop, so the layout
    // captured here is consumed by the NEXT frame's release. Layout is stable frame to
    // frame. Not gated on the visibility-mask path — the D3D11 visor runs regardless.
    if (enabled && AnyDirectOverlay() &&
        g_d3d11Mask.initialized && session == g_d3d11Mask.session) {
        bool foundProj = false;
        std::lock_guard<std::mutex> lk(g_swapchainMutex);
        g_primaryProjectionContext = ProjectionFrameContext{};
        for (uint32_t l = 0; l < frameEndInfo->layerCount; ++l) {
            const XrCompositionLayerBaseHeader* hdr = frameEndInfo->layers[l];
            if (hdr == nullptr || hdr->type != XR_TYPE_COMPOSITION_LAYER_PROJECTION) continue;
            foundProj = true;
            const auto* proj = reinterpret_cast<const XrCompositionLayerProjection*>(hdr);
            primaryProjection = proj;
            g_primaryProjectionContext.session = session;
            g_primaryProjectionContext.displayTime = frameEndInfo->displayTime;
            g_primaryProjectionContext.space = proj->space;
            g_primaryProjectionContext.frameSerial = pipelineFrameSerial;
            g_primaryProjectionContext.topology.views.reserve(proj->viewCount);
            if (!g_diagProjFound.exchange(true)) {
                Log("d3d11 mask DIAG: projection layer found (layer=%u viewCount=%u)\n",
                    l, proj->viewCount);
            }
            for (uint32_t v = 0; v < proj->viewCount; ++v) {
                const XrCompositionLayerProjectionView& pv = proj->views[v];
                XrFovf fullFov = pv.fov;
                const auto sameFov = [](const XrFovf& a, const XrFovf& b) {
                    constexpr float tolerance = 0.00001f;
                    return fabsf(a.angleLeft - b.angleLeft) <= tolerance &&
                        fabsf(a.angleRight - b.angleRight) <= tolerance &&
                        fabsf(a.angleUp - b.angleUp) <= tolerance &&
                        fabsf(a.angleDown - b.angleDown) <= tolerance;
                };
                if (hasMatchedLocate && v < matchedLocate.viewCount &&
                    sameFov(pv.fov, matchedLocate.cropped[v].fov)) {
                    fullFov = matchedLocate.original[v].fov;
                }
                g_primaryProjectionContext.topology.views.push_back({
                    pv.subImage.swapchain,
                    EyeView{pv.subImage.imageRect,
                            static_cast<uint32_t>(pv.subImage.imageArrayIndex),
                            v,
                            pv.pose,
                            pv.fov,
                            fullFov}});
            }
            // The first projection layer is the application's primary stereo submission.
            // OpenComposite may repeat the same projection texture in additional layers; recording
            // those made release-time overlays draw twice into one image and produced HUD ghosts.
            break;
        }
        if (foundProj && AnyCalibrationPattern()) {
            // This serial represents submitted projection frames, never a release-hook count.
            // The following release calls for both eyes therefore draw the same temporal state.
            g_calibrationFrameSerial.fetch_add(1);
        }
        if (!foundProj && !g_diagNoProjLayer.exchange(true)) {
            Log("d3d11 mask DIAG: FAIL no projection layer in submitted frame (layerCount=%u)\n",
                frameEndInfo->layerCount);
        }
    }

    // Re-evaluate actual frame topology rather than assuming every scene contains a projection.
    // Projection-only frames retain direct rendering. Any distinct or composition-only topology
    // latches ordered presentation for the session so projection -> menu -> projection transitions
    // cannot silently drop ViewLab while resources churn underneath them.
    if (topmostVisorOverlays && frameEndInfo->layerCount>0) {
        bool distinctApplicationLayer=primaryProjection==nullptr;
        if(primaryProjection) {
            for(uint32_t l=0;l<frameEndInfo->layerCount&&!distinctApplicationLayer;++l) {
                const XrCompositionLayerBaseHeader* hdr=frameEndInfo->layers[l];
                if(!hdr||hdr==reinterpret_cast<const XrCompositionLayerBaseHeader*>(primaryProjection))continue;
                if(hdr->type!=XR_TYPE_COMPOSITION_LAYER_PROJECTION){distinctApplicationLayer=true;break;}
                const auto* other=reinterpret_cast<const XrCompositionLayerProjection*>(hdr);
                if(other->viewCount!=primaryProjection->viewCount||!other->views){distinctApplicationLayer=true;break;}
                for(uint32_t v=0;v<other->viewCount;++v) {
                    const auto& a=other->views[v].subImage; const auto& b=primaryProjection->views[v].subImage;
                    if(a.swapchain!=b.swapchain||a.imageArrayIndex!=b.imageArrayIndex||
                       a.imageRect.offset.x!=b.imageRect.offset.x||a.imageRect.offset.y!=b.imageRect.offset.y||
                       a.imageRect.extent.width!=b.imageRect.extent.width||a.imageRect.extent.height!=b.imageRect.extent.height) {
                        distinctApplicationLayer=true;break;
                    }
                }
            }
        }
        const bool wasDemanded=g_topmostLayerDemanded;
        g_topmostLayerDemanded=viewlab::policy::LatchTopmostDemand(g_topmostLayerDemanded,distinctApplicationLayer);
        if(g_topmostLayerDemanded) {
            const viewlab::bridge::RuntimeCapabilities capabilities{
                viewlab::bridge::GraphicsApi::D3D11,primaryProjection!=nullptr,
                frameEndInfo->layerCount>0,g_topmostLayerDemanded,primaryProjection!=nullptr,false,
                primaryProjection!=nullptr,g_topmostLayer.ready};
            const auto previousBackend=g_topmostBackend;
            g_featurePresentationPlan=viewlab::bridge::SelectFeaturePresentationPlan(capabilities);
            g_topmostBackend=g_featurePresentationPlan.orderedBackend;
            if(!wasDemanded || previousBackend!=g_topmostBackend) {
                Log("topmost overlays: topology=%s layers=%u backend=%s\n",
                    primaryProjection?"layered-projection":"composition-only",frameEndInfo->layerCount,
                    viewlab::bridge::OverlayBackendName(g_topmostBackend));
                for(uint32_t l=0;l<frameEndInfo->layerCount;++l)
                    Log("topmost overlays: source layer[%u] type=%d\n",l,
                        frameEndInfo->layers[l]?static_cast<int>(frameEndInfo->layers[l]->type):-1);
            }
        }
    }

    if (enabled && g_d3d11Mask.initialized && !g_rendererDeviceLost.load(std::memory_order_acquire) &&
        session == g_d3d11Mask.session) {
        const bool releaseDrewBatch = g_releaseDrewViewLabBatchThisFrame.exchange(false);
        if (maskEnabled && (!topmostVisorOverlays || !g_topmostLayer.ready || g_topmostLayerBlocked.load(std::memory_order_acquire)) && !releaseDrewBatch) {
            // Late direct fallback: draw the full ViewLab batch (visor then overlays) so
            // overlays positioned inside the visor mask remain visible even when the
            // ordered/topmost carrier is unavailable.
            DrawCapturedProjectionTextures(true, true, "direct-fallback");
        }
        // The submitted textures now contain the frame's complete direct-drawn content, so
        // this is the calibration suite's capture point for the final left-eye image.
        ProcessCalibrationCaptureRequest();
        // Same capture point feeds the VLMC OBS source: publish the composited eye into the
        // ViewLab-owned shared ring so "ViewLab Mirror Capture" shows game frame + overlays.
        ProduceViewLabMirrorFrame();
    }

    XrFrameEndInfo submittedInfo=*frameEndInfo;
    std::vector<const XrCompositionLayerBaseHeader*> submittedLayers;
    TopmostSubmission topmostSubmission;
    bool submittedTopmost=false;
    if (topmostVisorOverlays && g_topmostLayerDemanded && primaryProjection &&
        g_topmostBackend==viewlab::bridge::OverlayBackend::SeparateProjection &&
        !g_topmostLayerBlocked.load(std::memory_order_acquire) &&
        !g_rendererDeviceLost.load(std::memory_order_acquire)) {
        const bool wasReady=g_topmostLayer.ready;
        if (RenderTopmostLayer(session,primaryProjection,topmostSubmission)) {
            // The release-time draw for this frame has already happened. Arm exclusive ordered
            // presentation now so the next frame cannot produce the obsolete behind-menu copy.
            g_featurePresentationPlan.drawDirectVisor=false;
            g_featurePresentationPlan.drawDirectCommonFeatures=false;
            // The allocation frame retains direct presentation; the proven transparent stereo
            // projection is appended from the next frame without duplicating the transition frame.
            if (wasReady) {
                submittedLayers.assign(frameEndInfo->layers,frameEndInfo->layers+frameEndInfo->layerCount);
                submittedLayers.push_back(reinterpret_cast<const XrCompositionLayerBaseHeader*>(&topmostSubmission.layer));
                submittedInfo.layerCount=(uint32_t)submittedLayers.size(); submittedInfo.layers=submittedLayers.data(); submittedTopmost=true;
            }
        }
    }
    const XrResult result = nextXrEndFrame(session, submittedTopmost ? &submittedInfo : frameEndInfo);
    if (TracePipelineSerial(pipelineFrameSerial)) {
        LARGE_INTEGER submittedStop{};
        QueryPerformanceCounter(&submittedStop);
        LogVerbose("PIPE end-return frame=%llu submittedTopmost=%d sourceLayers=%u submittedLayers=%u qpcStart=%lld qpcStop=%lld durationMs=%.4f result=%d\n",
            static_cast<unsigned long long>(pipelineFrameSerial), submittedTopmost ? 1 : 0,
            frameEndInfo->layerCount, submittedTopmost ? submittedInfo.layerCount : frameEndInfo->layerCount,
            static_cast<long long>(endStart.QuadPart), static_cast<long long>(submittedStop.QuadPart),
            g_hudQpcFrequency.QuadPart > 0 ? 1000.0 * static_cast<double>(submittedStop.QuadPart - endStart.QuadPart) / g_hudQpcFrequency.QuadPart : -1.0,
            result);
    }
    if(submittedTopmost && !g_topmostSubmissionLogged.exchange(true))
        Log("topmost overlays: submitted backend=%s sourceLayers=%u submittedLayers=%u projection=%ux%u result=%d\n",
            viewlab::bridge::OverlayBackendName(g_topmostBackend),frameEndInfo->layerCount,
            submittedInfo.layerCount,g_topmostLayer.width,g_topmostLayer.height,result);
    if (submittedTopmost && XR_FAILED(result)) BlockTopmostLayer("xrEndFrame", result);
    LARGE_INTEGER endStop{};
    QueryPerformanceCounter(&endStop);
    if (trackedFrameIndex < g_hudTrackedFrames.size()) {
        std::lock_guard<std::mutex> lock(g_hudTimingMutex);
        HudTrackedFrame& frame = g_hudTrackedFrames[trackedFrameIndex];
        frame.endStop = endStop;
        if (XR_SUCCEEDED(result) && g_hudQpcFrequency.QuadPart > 0 && frame.beginStop.QuadPart > 0 &&
            frame.endStart.QuadPart >= frame.beginStop.QuadPart && frame.endStop.QuadPart >= frame.endStart.QuadPart) {
            const double appWorkMs=1000.0*(double)(frame.endStart.QuadPart-frame.beginStop.QuadPart)/g_hudQpcFrequency.QuadPart;
            const double submitMs=1000.0*(double)(frame.endStop.QuadPart-frame.endStart.QuadPart)/g_hudQpcFrequency.QuadPart;
            const double budgetMs=g_hudEffectiveBudgetMs>0.0?g_hudEffectiveBudgetMs:(double)frame.displayPeriod/1000000.0;
            RecordHudAppWorkSample(appWorkMs,budgetMs,submitMs);
        }
        frame = HudTrackedFrame{};
    }
    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrEnumerateViewConfigurationViews(
    XrInstance instance,
    XrSystemId systemId,
    XrViewConfigurationType viewConfigurationType,
    uint32_t viewCapacityInput,
    uint32_t* viewCountOutput,
    XrViewConfigurationView* views) {
    const XrResult result = nextXrEnumerateViewConfigurationViews(instance, systemId, viewConfigurationType, viewCapacityInput, viewCountOutput, views);

    if (!enabled ||
        result != XR_SUCCESS ||
        viewConfigurationType != XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO ||
        viewCountOutput == nullptr ||
        views == nullptr) {
        return result;
    }

    PFN_xrGetViewConfigurationProperties getProperties = nullptr;
    if (nextXrGetInstanceProcAddr(
            instance,
            "xrGetViewConfigurationProperties",
            reinterpret_cast<PFN_xrVoidFunction*>(&getProperties)) == XR_SUCCESS &&
        getProperties != nullptr) {
        XrViewConfigurationProperties properties{XR_TYPE_VIEW_CONFIGURATION_PROPERTIES};
        const XrResult propertiesResult = getProperties(instance, systemId, viewConfigurationType, &properties);
        if ((propertiesResult != XR_SUCCESS || properties.fovMutable != XR_TRUE) && verboseLogging) {
            const uint32_t skipCount = enumerateSkipLogCount.fetch_add(1);
            if (skipCount < 10 || skipCount % 100 == 0) {
                LogVerbose("xrEnumerateViewConfigurationViews: continuing recommended-size scale, properties_result=%d fovMutable=%d\n",
                    propertiesResult,
                    properties.fovMutable == XR_TRUE ? 1 : 0);
            }
        }
    }

    const uint32_t logCount = enumerateViewsLogCount.fetch_add(1);
    const double requestedHeightScale = std::clamp(topTangent + bottomTangent, 0.01, 1.0);
    const double requestedWidthScale = EffectiveOuterEdgeHorizontalScale();
    for (uint32_t i = 0; i < *viewCountOutput; ++i) {
        const double perViewHeightScale = visualMaskOnly ? 1.0 : EffectiveVerticalScaleForView(i, requestedHeightScale);
        const double perViewWidthScale = horizontalVisualMaskOnly ? 1.0 : EffectiveHorizontalScaleForView(i, requestedWidthScale);
        const double renderHeightScale = perViewHeightScale * renderScale;
        const double renderWidthScale = perViewWidthScale * renderScale;
        const uint32_t beforeWidth = views[i].recommendedImageRectWidth;
        const uint32_t beforeHeight = views[i].recommendedImageRectHeight;
        g_runtimeRecommendedViewWidth.store(
            (std::max)(g_runtimeRecommendedViewWidth.load(std::memory_order_relaxed),beforeWidth),
            std::memory_order_release);
        g_runtimeRecommendedViewHeight.store(
            (std::max)(g_runtimeRecommendedViewHeight.load(std::memory_order_relaxed),beforeHeight),
            std::memory_order_release);
        views[i].recommendedImageRectWidth = std::max<uint32_t>(
            1,
            static_cast<uint32_t>(std::lround(static_cast<double>(views[i].recommendedImageRectWidth) * renderWidthScale)));
        views[i].recommendedImageRectHeight = std::max<uint32_t>(
            1,
            static_cast<uint32_t>(std::lround(static_cast<double>(views[i].recommendedImageRectHeight) * renderHeightScale)));
        if (i == 0 && logCount == 0) {
            Log("render: resolution scaled width=%.3f height=%.3f (per-view detail in verbose log only)\n",
                renderWidthScale,
                renderHeightScale);
        }
        if (verboseLogging && (logCount < 10 || logCount % 100 == 0)) {
            LogVerbose("xrEnumerateViewConfigurationViews[%u]: recommended width %u -> %u horizontal_render_width=%.3f effective_horizontal_width=%.3f horizontal_visual_mask_only=%d height %u -> %u total_render_height=%.3f effective_vertical_height=%.3f visual_mask_only=%d uevr_like=%d\n",
                i,
                beforeWidth,
                views[i].recommendedImageRectWidth,
                horizontalRenderWidth,
                perViewWidthScale,
                horizontalVisualMaskOnly ? 1 : 0,
                beforeHeight,
                views[i].recommendedImageRectHeight,
                requestedHeightScale,
                perViewHeightScale,
                visualMaskOnly ? 1 : 0,
                uevrLikeProcess ? 1 : 0);
        }
    }

    return result;
}

// Replace the runtime's hidden mesh with a rectangular black "visor" border that
// bounds the visible area. mask_vertical/mask_horizontal are absolute (CROP units);
// here they become a fraction of the cropped view (1.0 = no mask on that axis).
// The frame is 8 vertices / 24 indices, well within the runtime's lens-mesh capacity,
// so no buffer growth is needed. Centered rect for now; per-eye/feathered "bean"
// shaping follows. Returns false if there is nothing to mask or no room.
// Replaces the runtime hidden-area mesh with a visor-shaped border.
// Shape math identical to BeanMaskEditor.BuildGeometry and BuildVisorBorderVerts.
XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrGetVisibilityMaskKHR(
    XrSession session,
    XrViewConfigurationType viewConfigurationType,
    uint32_t viewIndex,
    XrVisibilityMaskTypeKHR visibilityMaskType,
    XrVisibilityMaskKHR* visibilityMask) {
    // Informational only. The game calling xrGetVisibilityMaskKHR does NOT disable the
    // native D3D11 visor — that path runs independently at xrReleaseSwapchainImage.
    if (!g_visibilityMaskEverCalled.exchange(true)) {
        Log("visibility mask: game calls xrGetVisibilityMaskKHR (informational; native D3D11 visor unaffected, runs independently)\n");
    }

    const XrResult result = nextXrGetVisibilityMaskKHR(session, viewConfigurationType, viewIndex, visibilityMaskType, visibilityMask);

    if (!enabled ||
        result != XR_SUCCESS ||
        viewConfigurationType != XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO ||
        viewIndex > 1 ||
        visibilityMaskType != XR_VISIBILITY_MASK_TYPE_HIDDEN_TRIANGLE_MESH_KHR ||
        visibilityMask == nullptr ||
        visibilityMask->vertices == nullptr ||
        visibilityMask->indices == nullptr ||
        visibilityMask->vertexCountOutput == 0 ||
        visibilityMask->indexCountOutput < 3 ||
        visibilityMask->vertexCapacityInput < visibilityMask->vertexCountOutput ||
        visibilityMask->indexCapacityInput < visibilityMask->indexCountOutput) {
        return result;
    }

    // Outer-edge stencil filter (UI checkbox "Stencil outer edges only"): keep only the
    // outer-half hidden triangles so the runtime's FOV stencil (e.g. Virtual Desktop's)
    // never stencils the inner-eye/top/bottom regions. This MUST also run when the native
    // D3D11 visor is active: the visor only draws pixels, but the game stencils whatever
    // this mask returns — an unfiltered mask blacks out the inner edges regardless of the
    // visor shape, with the runtime's curve instead of the user's apex-y curve.
    if (uevrLikeProcess) {
        if (!g_diagVisorSkipsVisibilityEdgeFilter.exchange(true)) {
            Log("visibility mask: outer-edge filtering OFF for UEVR-like process; runtime mask passed through unmodified\n");
        }
        return result;
    }

    const uint32_t beforeIndexCount = visibilityMask->indexCountOutput;

    // A single triangle is already an indivisible runtime mask. Attempting to
    // classify it by centroid can discard the entire mask for one eye (VDXR
    // reports the same local-coordinate sign for both eyes), which violates
    // the non-empty result the application just queried and can crash clients
    // that immediately consume both meshes. Preserve such masks verbatim.
    if (beforeIndexCount == 3) {
        const uint32_t logCount = visibilityMaskLogCount.fetch_add(1);
        if (logCount == 0) {
            Log("visibility mask: runtime mask is indivisible; passing through unmodified\n");
        }
        LogVerbose("xrGetVisibilityMaskKHR: view=%u hidden indices %u -> %u indivisible_passthrough=1\n",
            viewIndex,
            beforeIndexCount,
            beforeIndexCount);
        return result;
    }

    uint32_t writeIndex = 0;
    for (uint32_t readIndex = 0; readIndex + 2 < beforeIndexCount; readIndex += 3) {
        const uint32_t i0 = visibilityMask->indices[readIndex];
        const uint32_t i1 = visibilityMask->indices[readIndex + 1];
        const uint32_t i2 = visibilityMask->indices[readIndex + 2];
        if (i0 >= visibilityMask->vertexCountOutput ||
            i1 >= visibilityMask->vertexCountOutput ||
            i2 >= visibilityMask->vertexCountOutput) {
            continue;
        }

        const float centerX =
            (visibilityMask->vertices[i0].x +
             visibilityMask->vertices[i1].x +
             visibilityMask->vertices[i2].x) / 3.0f;

        // Outer half only: left eye (view 0) keeps its left side, right eye (view 1)
        // keeps its right side. Inner-eye (nose-side) and centre triangles are dropped.
        const bool keepOuterEdge =
            (viewIndex == 0 && centerX <= 0.0f) ||
            (viewIndex == 1 && centerX >= 0.0f);
        if (!keepOuterEdge) {
            continue;
        }

        visibilityMask->indices[writeIndex++] = i0;
        visibilityMask->indices[writeIndex++] = i1;
        visibilityMask->indices[writeIndex++] = i2;
    }

    // Never turn a valid non-empty runtime mesh into an empty mesh. The
    // application's safety is more important than applying this optional
    // stencil optimization to an unfamiliar runtime topology.
    if (writeIndex == 0) {
        const uint32_t logCount = visibilityMaskLogCount.fetch_add(1);
        if (logCount == 0) {
            Log("visibility mask: filter rejected the complete runtime mask; passing through unmodified\n");
        }
        LogVerbose("xrGetVisibilityMaskKHR: view=%u hidden indices %u -> %u empty_filter_passthrough=1\n",
            viewIndex,
            beforeIndexCount,
            beforeIndexCount);
        return result;
    }

    visibilityMask->indexCountOutput = writeIndex;

    const uint32_t logCount = visibilityMaskLogCount.fetch_add(1);
    if (logCount == 0) {
        Log("visibility mask: outer-edge filtering active (per-call detail in verbose log only)\n");
    }
    if (verboseLogging && (logCount < 20 || logCount % 100 == 0)) {
        LogVerbose("xrGetVisibilityMaskKHR: view=%u hidden indices %u -> %u outer_edge_only=1\n",
            viewIndex,
            beforeIndexCount,
            visibilityMask->indexCountOutput);
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrGetInstanceProcAddr(
    XrInstance instance,
    const char* name,
    PFN_xrVoidFunction* function) {
    const XrResult result = nextXrGetInstanceProcAddr(instance, name, function);

    if (result != XR_SUCCESS || name == nullptr || function == nullptr || *function == nullptr) {
        return result;
    }

    if (std::strcmp(name, "xrLocateViews") == 0) {
        nextXrLocateViews = reinterpret_cast<PFN_xrLocateViews>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrLocateViews);
        Log("hook installed: xrLocateViews\n");
    } else if (std::strcmp(name, "xrCreateSession") == 0) {
        nextXrCreateSession = reinterpret_cast<PFN_xrCreateSession>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrCreateSession);
        Log("hook installed: xrCreateSession\n");
    } else if (std::strcmp(name, "xrDestroySession") == 0) {
        nextXrDestroySession = reinterpret_cast<PFN_xrDestroySession>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrDestroySession);
        Log("hook installed: xrDestroySession\n");
    } else if (std::strcmp(name, "xrEndSession") == 0) {
        nextXrEndSession = reinterpret_cast<PFN_xrEndSession>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrEndSession);
        Log("hook installed: xrEndSession\n");
    } else if (std::strcmp(name, "xrWaitFrame") == 0) {
        nextXrWaitFrame = reinterpret_cast<PFN_xrWaitFrame>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrWaitFrame);
        Log("hook installed: xrWaitFrame\n");
    } else if (std::strcmp(name, "xrBeginFrame") == 0) {
        nextXrBeginFrame = reinterpret_cast<PFN_xrBeginFrame>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrBeginFrame);
        Log("hook installed: xrBeginFrame\n");
    } else if (std::strcmp(name, "xrEndFrame") == 0) {
        nextXrEndFrame = reinterpret_cast<PFN_xrEndFrame>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrEndFrame);
        Log("hook installed: xrEndFrame\n");
    } else if (std::strcmp(name, "xrEnumerateSwapchainFormats") == 0) {
        nextXrEnumerateSwapchainFormats = reinterpret_cast<PFN_xrEnumerateSwapchainFormats>(*function);
        Log("hook captured: xrEnumerateSwapchainFormats\n");
    } else if (std::strcmp(name, "xrCreateSwapchain") == 0) {
        nextXrCreateSwapchain = reinterpret_cast<PFN_xrCreateSwapchain>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrCreateSwapchain);
        Log("hook installed: xrCreateSwapchain\n");
    } else if (std::strcmp(name, "xrDestroySwapchain") == 0) {
        nextXrDestroySwapchain = reinterpret_cast<PFN_xrDestroySwapchain>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrDestroySwapchain);
        Log("hook installed: xrDestroySwapchain\n");
    } else if (std::strcmp(name, "xrEnumerateSwapchainImages") == 0) {
        nextXrEnumerateSwapchainImages = reinterpret_cast<PFN_xrEnumerateSwapchainImages>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrEnumerateSwapchainImages);
        Log("hook installed: xrEnumerateSwapchainImages\n");
    } else if (std::strcmp(name, "xrAcquireSwapchainImage") == 0) {
        nextXrAcquireSwapchainImage = reinterpret_cast<PFN_xrAcquireSwapchainImage>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrAcquireSwapchainImage);
        Log("hook installed: xrAcquireSwapchainImage\n");
    } else if (std::strcmp(name, "xrWaitSwapchainImage") == 0) {
        nextXrWaitSwapchainImage = reinterpret_cast<PFN_xrWaitSwapchainImage>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrWaitSwapchainImage);
        Log("hook installed: xrWaitSwapchainImage\n");
    } else if (std::strcmp(name, "xrReleaseSwapchainImage") == 0) {
        nextXrReleaseSwapchainImage = reinterpret_cast<PFN_xrReleaseSwapchainImage>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrReleaseSwapchainImage);
        Log("hook installed: xrReleaseSwapchainImage\n");
    } else if (std::strcmp(name, "xrCreateReferenceSpace") == 0) {
        nextXrCreateReferenceSpace = reinterpret_cast<PFN_xrCreateReferenceSpace>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrCreateReferenceSpace);
        Log("hook installed: xrCreateReferenceSpace\n");
    } else if (std::strcmp(name, "xrDestroySpace") == 0) {
        nextXrDestroySpace = reinterpret_cast<PFN_xrDestroySpace>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrDestroySpace);
        Log("hook installed: xrDestroySpace\n");
    } else if (std::strcmp(name, "xrEnumerateViewConfigurationViews") == 0) {
        nextXrEnumerateViewConfigurationViews = reinterpret_cast<PFN_xrEnumerateViewConfigurationViews>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrEnumerateViewConfigurationViews);
        Log("hook installed: xrEnumerateViewConfigurationViews\n");
    } else if (std::strcmp(name, "xrGetVisibilityMaskKHR") == 0) {
        nextXrGetVisibilityMaskKHR = reinterpret_cast<PFN_xrGetVisibilityMaskKHR>(*function);
        *function = reinterpret_cast<PFN_xrVoidFunction>(XRViewLab_xrGetVisibilityMaskKHR);
        Log("hook installed: xrGetVisibilityMaskKHR\n");
    }

    return result;
}

XRAPI_ATTR XrResult XRAPI_CALL XRViewLab_xrCreateApiLayerInstance(
    const XrInstanceCreateInfo* instanceCreateInfo,
    const XrApiLayerCreateInfo* apiLayerInfo,
    XrInstance* instance) {
    if (!apiLayerInfo ||
        apiLayerInfo->structType != XR_LOADER_INTERFACE_STRUCT_API_LAYER_CREATE_INFO ||
        apiLayerInfo->structVersion != XR_API_LAYER_CREATE_INFO_STRUCT_VERSION ||
        apiLayerInfo->structSize != sizeof(XrApiLayerCreateInfo) ||
        !apiLayerInfo->nextInfo ||
        apiLayerInfo->nextInfo->structType != XR_LOADER_INTERFACE_STRUCT_API_LAYER_NEXT_INFO ||
        apiLayerInfo->nextInfo->structVersion != XR_API_LAYER_NEXT_INFO_STRUCT_VERSION ||
        apiLayerInfo->nextInfo->structSize != sizeof(XrApiLayerNextInfo) ||
        apiLayerInfo->nextInfo->layerName == nullptr ||
        std::strcmp(apiLayerInfo->nextInfo->layerName, LayerName) != 0 ||
        !apiLayerInfo->nextInfo->nextGetInstanceProcAddr ||
        !apiLayerInfo->nextInfo->nextCreateApiLayerInstance) {
        Log("xrCreateApiLayerInstance validation failed\n");
        return XR_ERROR_INITIALIZATION_FAILED;
    }

    nextXrGetInstanceProcAddr = apiLayerInfo->nextInfo->nextGetInstanceProcAddr;

    XrApiLayerCreateInfo chainedApiLayerInfo = *apiLayerInfo;
    chainedApiLayerInfo.nextInfo = apiLayerInfo->nextInfo->next;

    const XrResult result = apiLayerInfo->nextInfo->nextCreateApiLayerInstance(instanceCreateInfo, &chainedApiLayerInfo, instance);
    const char* appName = instanceCreateInfo ? instanceCreateInfo->applicationInfo.applicationName : "<unknown>";
    const char* engineName = instanceCreateInfo ? instanceCreateInfo->applicationInfo.engineName : "<unknown>";
    std::string runtimeName;
    if (result == XR_SUCCESS && instance != nullptr && *instance != XR_NULL_HANDLE) {
        PFN_xrGetInstanceProperties getInstanceProperties = nullptr;
        if (nextXrGetInstanceProcAddr(
                *instance,
                "xrGetInstanceProperties",
                reinterpret_cast<PFN_xrVoidFunction*>(&getInstanceProperties)) == XR_SUCCESS &&
            getInstanceProperties != nullptr) {
            XrInstanceProperties properties{XR_TYPE_INSTANCE_PROPERTIES};
            if (getInstanceProperties(*instance, &properties) == XR_SUCCESS) {
                runtimeName = properties.runtimeName;
                Log("runtime: name=%s version=%u.%u.%u\n",
                    properties.runtimeName,
                    XR_VERSION_MAJOR(properties.runtimeVersion),
                    XR_VERSION_MINOR(properties.runtimeVersion),
                    XR_VERSION_PATCH(properties.runtimeVersion));
            }
        }
    }
    RememberApplication(appName, engineName, result, runtimeName.c_str());
    LoadConfig();
    if (instanceCreateInfo != nullptr) {
        Log("application: name=%s engine=%s api=%u.%u.%u extensions=%u\n",
            instanceCreateInfo->applicationInfo.applicationName,
            instanceCreateInfo->applicationInfo.engineName,
            XR_VERSION_MAJOR(instanceCreateInfo->applicationInfo.apiVersion),
            XR_VERSION_MINOR(instanceCreateInfo->applicationInfo.apiVersion),
            XR_VERSION_PATCH(instanceCreateInfo->applicationInfo.apiVersion),
            instanceCreateInfo->enabledExtensionCount);
        for (uint32_t i = 0; i < instanceCreateInfo->enabledExtensionCount; ++i) {
            Log("application extension[%u]=%s\n", i, instanceCreateInfo->enabledExtensionNames[i]);
        }
    }
    Log("xrCreateApiLayerInstance result=%d state=%s app=%s\n", result, enabled ? "enabled" : "bypassed", appName);
    return result;
}
}

extern "C" {
XrResult __declspec(dllexport) XRAPI_CALL cooooked_xrviewlab_xrNegotiateLoaderApiLayerInterface(
    const XrNegotiateLoaderInfo* loaderInfo,
    const char* apiLayerName,
    XrNegotiateApiLayerRequest* apiLayerRequest) {
    EnsureInitialized();

    if (apiLayerName == nullptr || std::strcmp(apiLayerName, LayerName) != 0) {
        Log("invalid apiLayerName: %s\n", apiLayerName ? apiLayerName : "<null>");
        return XR_ERROR_INITIALIZATION_FAILED;
    }

    if (!loaderInfo ||
        !apiLayerRequest ||
        loaderInfo->structType != XR_LOADER_INTERFACE_STRUCT_LOADER_INFO ||
        loaderInfo->structVersion != XR_LOADER_INFO_STRUCT_VERSION ||
        loaderInfo->structSize != sizeof(XrNegotiateLoaderInfo) ||
        apiLayerRequest->structType != XR_LOADER_INTERFACE_STRUCT_API_LAYER_REQUEST ||
        apiLayerRequest->structVersion != XR_API_LAYER_INFO_STRUCT_VERSION ||
        apiLayerRequest->structSize != sizeof(XrNegotiateApiLayerRequest) ||
        loaderInfo->minInterfaceVersion > XR_CURRENT_LOADER_API_LAYER_VERSION ||
        loaderInfo->maxInterfaceVersion < XR_CURRENT_LOADER_API_LAYER_VERSION ||
        loaderInfo->maxApiVersion < XR_CURRENT_API_VERSION ||
        loaderInfo->minApiVersion > XR_CURRENT_API_VERSION) {
        Log("xrNegotiateLoaderApiLayerInterface validation failed\n");
        return XR_ERROR_INITIALIZATION_FAILED;
    }

    apiLayerRequest->layerInterfaceVersion = XR_CURRENT_LOADER_API_LAYER_VERSION;
    apiLayerRequest->layerApiVersion = XR_CURRENT_API_VERSION;
    apiLayerRequest->getInstanceProcAddr = reinterpret_cast<PFN_xrGetInstanceProcAddr>(XRViewLab_xrGetInstanceProcAddr);
    apiLayerRequest->createApiLayerInstance = reinterpret_cast<PFN_xrCreateApiLayerInstance>(XRViewLab_xrCreateApiLayerInstance);
    return XR_SUCCESS;
}
}
