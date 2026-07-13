# ViewLab Vision Alignment Audit

**Original audit baseline:** branch `codex/topmost-incident-safety-20260713`, commit `ce7ef4d`,
repository state inspected 2026-07-13. Correction: the former `STATE.md` field naming 4.1.103 as
“last confirmed-good” was an incomplete documentation record, not evidence that later builds had no
headset testing. Recent Pistol Whip and DiRT Rally 2 testing is reconstructed as far as evidence
allows in `VIEWLAB_VALIDATION_HISTORY.md`. This remains a source audit, not a claim that every
feature in one recent build passed a complete headset matrix.

## Executive finding

ViewLab has moved beyond a crop utility. It already contains the foundations of a per-game VR
experience layer: projection shaping, D3D11 presentation, profile persistence, calibration,
performance classification, desktop-card rendering, generic event types, telemetry workers,
ReShade control, and deliberate rendering containment.

The strongest parts are quiet by nature: crop, scale, masks, crosshair, and fail-closed rendering.
The weakest parts are where a control implies an outcome that the deployed product cannot yet
deliver. Windows notification collection is blocked by the application identity/deployment model.
iRacing reads real shared memory, but its events are neither fully correct nor presented through
the promised spatial racing cues. The settings application also gives mature features,
implementation-level experiments, diagnostics, and unfinished integrations too similar a visual
status.

The product is therefore **capable but unevenly truthful**. Its next useful work is not a broad
renderer redesign. It is to finish the two flagship information paths, introduce one attention
arbiter, expand per-game policy beyond crop geometry, and separate ordinary outcomes from advanced
mechanisms.

## Method and evidence

The audit followed `repo-index.json` into the owning symbols rather than treating labels as proof.
It inspected:

- WPF settings, pop-ups, profile editor, persistence, application discovery, and update UI;
- OpenXR hooks, projection/resolution reporting, D3D11 direct and compositor-layer rendering;
- visor, edge-mask, calibration, crosshair, HUD, trace, notification, and telemetry contracts;
- ReShade payload installation/control and its recorded limitations;
- installer manifests, application manifest, deployment layout, defaults, tests, logs, and safety
  documentation;
- disabled, retired, experimental, hidden, and debug-only paths.

Classifications below distinguish **source present** from **deployed**, **validated**, and
**user-complete**. A subsystem can be functionally plausible and still be unvalidated.

## Verified feature inventory

| Subsystem / feature | Current source reality | Configuration and dependencies | Limitations and failure behaviour | Vision alignment |
|---|---|---|---|---|
| OpenXR implicit layer | x64 and Win32 layer manifests and hooks; application-level enable/bypass | MSI registration, OpenXR loader, supported runtime | Loader/runtime absence bypasses the feature; native faults remain high consequence | **Strongly aligned**, safety-critical |
| Global crop | Top/bottom and horizontal projection reduction plus recommended image-rect reduction | Main WPF controls, INI, OpenXR view/configuration hooks | Runtime/game projection behaviour still requires per-game validation | **Strongly aligned** |
| Split top/bottom crop | Independent retained vertical regions with completed stale-state regression contracts | WPF toggle/sliders, INI, native config snapshot | Recent regressions demonstrate coupling risk; source contracts now protect mode clearing | **Aligned but historically fragile** |
| Per-app crop profile | Executable registry profile overrides global crop and render scale | App list/profile window, HKCU app keys | Coverage is much narrower than the product's per-game promise | **Partially aligned; architecturally limiting** |
| Render scale/supersampling | Adjusts reported recommended resolution independently of crop | Global/per-app settings, runtime allocation | Outcome depends on game respecting recommendation | **Strongly aligned** |
| Application discovery and management | Tracks executables, summaries, enable/bypass, hide/unhide, reset | HKCU application records, WPF list | Does not yet present a complete “what will this game use?” policy summary | **Functionally correct, under-presented** |
| Edge masks | Visual-only vertical/horizontal boundary treatment; unsupported one-sided controls disabled | WPF, INI, native D3D11 path | Current product offers only supported combined edges, correctly avoiding false precision | **Strongly aligned** |
| Visor mask/editor | Shaped Direct-C aperture with WPF preview parity and outer-edge stencil filtering | Global controls; limited per-app visor geometry support | Large configuration surface; preview/native parity is a maintenance contract | **Strongly aligned, advanced UI is dense** |
| Topmost Visor Overlays | Experimental transparent OpenXR quad/composition backend with per-session disable latch and direct fallback | Explicit checkbox, runtime compositor and swapchain support | Duplicates some render/submission logic; runtime-dependent; not broadly headset-validated | **Safety-aligned, technically fragile, implementation detail exposed** |
| ReShade Remote | Installs a global OpenXR payload, controls shared memory, presets/modes, position/transform UI | ProgramData payload, registration, ReShade runtime, elevated install | Recorded issues include empty default preset, incomplete in-HMD technique selection, expensive broad effect compilation, and headset validation gaps | **Incomplete and inconsistent across games** |
| Calibration suite | Ten independently selectable patterns in native overlay renderer | Calibration popup and INI; deliberate opt-in | Current build remains headset-unvalidated; individual purpose is not presented as a guided workflow | **Aligned capability, poorly organised diagnostic experience** |
| Crosshair | Fused stereo reference with style/import/offset controls | Overlay popup, INI; OpenXR view/projection data | Optional persistent aid can become clutter only by user choice | **Strongly aligned** |
| Boundary flash | Temporary HUD/trace placement guide while dragging | WPF drag interactions and notification bridge | Setup aid only; correctly transient | **Strongly aligned** |
| Hardware telemetry worker | Isolated worker gathers supported CPU/GPU/RAM metrics into atomics | Native layer, PDH/DXGI/vendor availability | Runs in the game process; unsupported vendor metrics report unavailable; future broker would improve isolation | **Truthful and safety-conscious, structurally constrained** |
| Performance HUD | Twelve widgets and compact four-symbol state summary; alarm-only behaviour exists | Calibration/overlay settings, INI, shared metrics | Terminology and grouping mix live performance with diagnostics; headset validation lags source | **Strong concept, partially validated/presented** |
| Performance Trace | Seven frame-timing series with recent history and stereo rendering | Same overlay renderer and metrics | No alarm-only mode; if enabled it remains visible; hardware graph channels are not implemented | **Functionally useful but overly visible** |
| Windows notification renderer | Shared-memory card queue, animation, icon upload, stereo native drawing, synthetic test | WPF collector + native consumer; filters/privacy/duration/position controls | Synthetic cards work independently of Windows listener; real collection is blocked as detailed below | **Rendering structurally present; end-to-end feature blocked** |
| Windows notification collection | Background `UserNotificationListener` request/poll with explicit diagnostic states | `Windows.UI.Notifications.Management`, user consent, package identity/capability | Current traditional MSI/elevated unpackaged process lacks the required package capability; real notifications are not reliable | **Blocked by deployment/API model** |
| Generic ViewLab events | `IViewLabEventProvider` and typed `ViewLabEvent` seam | WPF process; provider-to-consumer event | No central priority, lifecycle, expiry, or presentation router; current consumer turns all events into cards | **Correct direction, incomplete architecture** |
| iRacing shared-memory provider | Real worker opens official SDK mapping and reads selected variable headers/values | Optional setting; running iRacing SDK memory map | Structurally present but unvalidated; correctness and shutdown defects detailed below | **Structurally present but unvalidated** |
| Spotter presentation | Test buttons and telemetry events exist | iRacing provider, notification renderer | No peripheral glow renderer; left/right enum handling is wrong for two-car states; events become text cards | **Broken user behaviour / placeholder presentation** |
| Flag presentation | Reads session flag bits and emits a generic event | iRacing provider, notification renderer | No visor-border flag state, priority, flag semantic mapping, or stale-state clearing | **Placeholder only** |
| Lap result presentation | Emits a card when `LapCompleted` advances, using last-lap time | iRacing provider, notification renderer | No validity, delta, PB/session best, robust session reset, or duplicate proof; unvalidated live | **Partially working, incomplete and unvalidated** |
| Notification/iRacing diagnostics | Status text distinguishes listener access/deployment/internal renderer and telemetry connection | WPF status labels/logging | Important limitations are visible only after opening relevant UI; labels still call working source a scaffold | **Truthful internals, inconsistent terminology** |
| Settings layouts | Responsive compact/medium/large WPF layouts with pop-up groups | MainWindow size/state | Calibration group also owns HUD/trace; Overlays mixes mature crosshair, blocked notifications, and iRacing test controls | **Functionally correct but poorly organised** |
| Defaults | Visor, calibration, crosshair, HUD, notifications, and iRacing default off | Bundled INI and persistence | Quiet defaults are good; first-run guidance is limited | **Strongly aligned** |
| Error/log diagnostics | Local log, verbose option, state-specific UI messages, bounded native logging | `%LOCALAPPDATA%`, INI, desktop status | No compact post-session incident summary; users must inspect transient status/log text | **Partially aligned** |
| Update check | Desktop release/update status and action | Network/GitHub release availability | Must remain non-blocking and does not currently affect render safety | **Aligned desktop-only utility** |
| Installer/config migration | MSI installs UI/layers/payload, registers architectures, preserves/migrates known config | WiX, admin rights, registry, ProgramData/LocalAppData | Main UI requests administrator rights for ordinary use; this conflicts with notification collection and least privilege | **Functional but deployment architecture needs separation** |
| Hidden debug controls | Deterministic HUD values, verbose logging, legacy accepted keys | INI/manual diagnostics | Useful for development; not a normal product surface and some bundled comments are stale | **Appropriate only as diagnostics** |
| Legacy/retired render experiments | Technique A/B and retired visibility/crop paths remain hidden or ignored for reference | Source only or ignored keys | Duplicate historical code increases cognitive cost but is not advertised | **Neutral if unreachable; maintenance burden** |
| `ViewLabVR` DXGI proxy tree | Separate native proxy/effect implementation exists in repository | Separate project/source tree | Not indexed as an active subsystem and not part of the current MSI/build user path found in this audit | **Unclear/stranded; must not be advertised without a decision** |

## Flagship audit: Windows desktop notifications

### Precise classification

- **Card rendering and synthetic test:** structurally present; synthetic path is working in source,
  with headset behaviour still requiring validation.
- **Real Windows notification collection in the shipped deployment:** **blocked by deployment/API
  limitations**.
- **End-to-end advertised feature:** **not working reliably** and must not be represented by an
  ordinary enabled checkbox without the exact unavailable state.

### Root cause

`NotificationService` correctly calls `UserNotificationListener.Current`, requests access, polls
eligible notifications off the render thread, filters content, and writes a bounded shared-memory
card queue. It also catches the characteristic unpackaged/class-registration failures instead of
allowing them to affect native rendering.

The executable, however, is installed by a traditional per-machine MSI and its application
manifest contains no package identity or restricted capability. It is also configured to run as
administrator. `UserNotificationListener` is a consented API whose supported package manifest
contract requires the restricted `uap3:userNotificationListener` capability. A Start-menu shortcut
or AppUserModelID alone is not that capability. This is an architectural deployment mismatch, not
an exception-message problem.

Microsoft's current documentation supports these conclusions:

- [`uap3:userNotificationListener` package capability](https://learn.microsoft.com/en-us/uwp/schemas/appxpackage/uapmanifestschema/element-uap3-capability-manual)
- [Windows app capability declarations](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations)
- [Packaging with external location / identity options](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/packaging/)
- [Desktop toast AppUserModelID registration](https://learn.microsoft.com/en-us/windows/win32/shell/enable-desktop-toast-with-appusermodelid)

The final link concerns a desktop application's own outgoing toasts; it does not replace the
identity and capability needed to read notification history.

### Supported paths

**Recommended low-risk path:** retain the MSI and existing native layer layout, but add a packaged,
medium-integrity notification broker with identity and `userNotificationListener` capability. The
broker owns consent and collection and writes the existing bounded shared-memory contract. The WPF
settings app controls and diagnoses it. This isolates the sensitive Windows API from the game and
from the currently elevated settings process.

**Broader alternative:** give the WPF application package identity through a signed sparse package
with external location, remove the always-elevated execution model, and move privileged
registration/repair work into an on-demand helper. This produces a cleaner long-term desktop
architecture but changes more installer and update assumptions.

**Largest packaging alternative:** migrate the desktop application to a full MSIX model while
retaining the OpenXR layer's required registration mechanics. This is defensible but too broad to
choose merely to fix one API.

Whichever path is approved must test first-run consent, denied/revoked access, package repair,
upgrade/uninstall, lock-screen/privacy filtering, notification removal, duplicate suppression,
collector restart, and loss of the native consumer. Windows only exposes notifications eligible
for notification history and allowed by user/system policy; the UI must name that limitation.

## Flagship audit: iRacing telemetry

### Precise classification

- **Official shared-memory connection and variable lookup:** **structurally present but
  unvalidated** against a live iRacing session in this build.
- **Generic event seam:** **partially working**, but lacks lifecycle, expiry, and arbitration.
- **Left/right overlap behaviour:** **broken** for some official states and presented as a
  placeholder card rather than a peripheral cue.
- **Flag behaviour:** **placeholder only**; no flag-border renderer or semantic priority exists.
- **Lap result:** **partially working but incomplete and unvalidated**.
- **Connection/stale/disconnection user events and clean shutdown:** **incomplete**.

### What is real

`IRacingTelemetryProvider` runs outside the UI and native render loops, opens
`Local\\IRSDKMemMapFileName`, parses the SDK header/variable table, and polls `CarLeftRight`,
`LapCompleted`, `LapLastLapTime`, and `SessionFlags`. It publishes decoupled `ViewLabEvent` values.
Changed-value gates prevent ordinary repeated samples from becoming a notification storm.

### Correctness gaps found

1. Spotter values 4, 5, and 6 are all normalised to `Both`. The established iRacing SDK enum
   distinguishes “car left/right”, “two cars left”, and “two cars right”; 5 and 6 therefore lose
   direction.
2. Every event is currently consumed by enabling desktop notifications and enqueueing a generic
   card. This silently changes/persists another feature and does not implement peripheral overlap
   or a flag border.
3. Disconnect does not publish authoritative clear/stale events or reset all previous sampled
   state. A stale cue could survive loss of telemetry, and reconnect can suppress the first state.
4. The worker's cancellation join is shorter than its longest reconnect sleep. The provider can
   clear its thread reference before that worker exits, permitting a second worker after a quick
   stop/start.
5. Required variable presence and finite values are not treated as an explicit availability
   contract. Session resets and lap-counter changes are not robustly modelled.
6. Lap output lacks validity, delta, PB, session-best state, and an authoritative session identity.
7. There is no measured live latency, replay fixture, reconnect test, or headset validation record.

### Smallest defensible completion path after approval

1. Vendor the exact public SDK enum/header definitions or a documented fixture into tests, then
   parse through a bounded snapshot reader that validates version, buffer/tick, offsets, types,
   lengths, and finite values.
2. Add session lifecycle and monotonic event identity: connected, disconnected, stale, clear,
   overlap state, flag state, and completed-lap result. Reset state on every session transition.
3. Make cancellation interruptible and prove one worker at a time. Keep telemetry entirely outside
   native rendering.
4. Route events through a generic priority/expiry arbiter. Implement left/right peripheral cues,
   flag-border ownership, and lap cards as consumers of generic event semantics—not iRacing memory.
5. Add recorded-memory fixtures for all overlap enum states, duplicate ticks, reconnect, session
   reset, invalid values, flag transitions, and completed laps.
6. Validate latency and clear behaviour in a live session before calling the feature working.

## Largest alignment strengths

1. **Quiet defaults.** Most in-headset features are off until explicitly useful.
2. **Projection and presentation are meaningfully separated.** Masks generally do not pretend to
   be crop controls, and established stereo/crop contracts are documented and tested.
3. **Performance HUD intent is unusually good.** It classifies a problem instead of merely listing
   counters, and unavailable hardware data is not fabricated.
4. **Safety work is becoming a product property.** Render locks, device-loss gates, state
   restoration, rate limiting, and a fail-closed topmost path directly support trust.
5. **Generic telemetry direction is correct.** The event provider seam can support simulations
   beyond iRacing if presentation is kept independent.
6. **Calibration is deliberate and off by default.** The product recognises that diagnostics are a
   workflow rather than normal scenery.
7. **Existing profile fallback is proven and useful.** The product already has the correct place to
   absorb per-game differences, even though its coverage must expand.

## Largest alignment gaps

1. **Controls over-promise deployment reality.** Notifications and iRacing look more available than
   they are; “test” success does not prove their real source path.
2. **There is no attention arbiter.** Racing events, performance overlays, and notifications have
   no shared priority, visual-channel ownership, expiry, or deferral policy.
3. **Per-app profiles are geometry profiles, not experience profiles.** They do not yet own the
   promised HUD, trace, notification, telemetry, ReShade, calibration, or compatibility policy.
4. **The trace violates the quiet model.** It can record and render, but cannot remain hidden until
   an actionable alarm.
5. **Settings organisation follows implementation history.** HUD/trace live with calibration;
   experimental backend selection is prominent; unfinished integrations and test controls sit
   beside ordinary features.
6. **No post-session answer exists.** Logs and live trace data exist, but the user receives no
   bounded incident history or suppression/fallback summary.
7. **Deployment privilege is too broad.** Running the ordinary settings UI as administrator makes
   a sensitive desktop-information feature harder and expands the consequence of UI faults.
8. **Validation recording debt is substantial.** Recent source received repeated headset testing,
   but observations were not consistently tied to build, runtime, feature and result. Some newest
   safety and hardware-telemetry behaviour still needs targeted validation.
9. **Documentation state is inconsistent.** Some UI/config comments still call iRacing a future
   scaffold with no telemetry even though a real provider now exists; this obscures honest status.

## Quick improvements (after direction is approved)

These are comparatively local and should be sequenced around, not ahead of, flagship completion:

1. Replace decorative enablement with exact states: Available, Permission required, Deployment
   unsupported, Provider disconnected, Stale, and Test renderer only.
2. Stop iRacing from silently enabling/persisting desktop notifications. Keep simulations clearly
   marked as presentation tests.
3. Add alarm-only trace visibility using existing collection/history and HUD alarm state; retain a
   short recovery hold.
4. Move calibration into a named diagnostic workflow and group HUD/trace under Performance.
5. Put Topmost backend and deterministic/debug values under Advanced Compatibility/Diagnostics,
   with automatic selection as a later decision.
6. Correct stale labels and bundled INI comments so source, UI, tests, and documentation agree.
7. Add concise per-app summaries of inherited versus overridden state before expanding profile
   coverage.
8. Make unsupported/unknown metric and provider states visually distinct from healthy/zero.

## Architectural improvements (after direction is approved)

1. **Notification identity boundary:** packaged medium-integrity broker or sparse-identity desktop
   architecture; keep collection and consent out of the game process.
2. **Generic event arbiter:** typed priority, timestamp, expiry, replacement key, visual channel,
   privacy class, acknowledgement, and suppression reason.
3. **Racing presentation consumers:** peripheral overlap, flag-border state, and lap cards consuming
   generic events with authoritative clear semantics.
4. **Experience profiles:** versioned per-app inheritance for visual, performance, information,
   telemetry, ReShade, and compatibility policies, while preserving existing registry values.
5. **Post-session incident record:** bounded metadata and performance summaries without desktop
   notification bodies by default.
6. **Privilege separation:** ordinary WPF UI at medium integrity; elevation only for install/repair
   operations that require it.
7. **Renderer policy:** one overlay scene contract and automatic compatible backend selection, with
   backend-specific submission isolated and observable.
8. **Replayable verification:** deterministic notification/event fixtures and recorded iRacing SDK
   memory snapshots feeding the same consumers used live.

## Proposed priority order

1. Decide notification deployment/privilege direction and event-priority policy.
2. Make notification availability truthful, then implement and validate the supported collector.
3. Correct and harden the iRacing provider with fixtures and clean lifecycle semantics.
4. Implement the generic arbiter and the three racing presentations.
5. Add alarm-only trace and bounded post-session diagnostics.
6. Reorganise settings and expand per-app experience policy incrementally.
7. Automate renderer selection only after current direct/topmost headset compatibility is recorded.

## Product-direction questions requiring an answer

1. Should ViewLab retain the MSI and add a small packaged notification broker (lowest disruption),
   or is removing always-on elevation and giving the WPF app sparse package identity an acceptable
   broader installer change?
2. Should racing safety cues always outrank desktop notifications with no user override, while
   users may reorder only the lower-priority performance, lap, and desktop categories?
3. Should the Performance Trace default to alarm-only—recording continuously, appearing on a
   sustained HUD alarm, and remaining briefly after recovery—with an explicit “always visible”
   advanced option?
4. May an experience profile automatically enable its telemetry provider and information policy,
   or must first use of notifications/iRacing always require a separate consent step even when a
   downloaded/built-in game profile recommends them?
5. Should Calibration become a guided, mutually exclusive desktop workflow with one active pattern
   and expected-result text, while retaining an Advanced free-selection mode for developers?
6. Should Topmost Visor Overlays become an automatic per-game compatibility decision after it is
   validated, leaving the current checkbox only under Advanced as a diagnostic override?
7. For post-session diagnostics, should ViewLab retain only bounded technical metadata by default,
   or also retain notification source/sender (never message bodies) and telemetry incident history?

No optimisation or behavioural change should begin until these decisions are answered and the
direction is explicitly approved.

