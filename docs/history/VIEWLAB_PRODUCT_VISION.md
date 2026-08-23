# ViewLab Product Vision

## 1. Product definition

ViewLab is a per-application VR experience layer for Windows and OpenXR. It began by letting a
user crop more of the projection than Virtual Desktop's fixed controls allowed. Its useful scope
is now broader but remains disciplined: improve the image and rendering budget, present selected
information at the moment it matters, verify that the result is trustworthy, and otherwise leave
the user alone.

ViewLab is becoming one quiet system with six responsibilities:

1. **Shape the render workload.** Crop unwanted projection area and exchange the saved work for
   frame-time headroom, supersampling, or better use of a constrained video stream.
2. **Shape the visible experience.** Turn exposed crop boundaries into intentional edge masks or a
   visor-shaped aperture without changing the underlying projection contract.
3. **Remember the game.** Load the appropriate crop, visual treatment, compatibility choices,
   image processing, information policy, and telemetry provider when an application starts.
4. **Prove the path.** Supply deliberate calibration tools for scale, geometry, colour, clipping,
   timing, stereo centre, and lens-edge behaviour.
5. **Answer live questions.** Classify performance trouble, show its recent history, import only
   useful desktop information, and convert game telemetry into spatial cues.
6. **Contain failure.** If an optional ViewLab subsystem fails, remove that subsystem, record an
   exact diagnosis, and preserve the game, runtime, and graphics stack wherever the platform
   permits.

The measure of the product is not how much it can draw. It is how much effort, uncertainty, and
distraction it removes for the person wearing the headset.

> **One glance, one answer, back to the experience.**

## 2. End-to-end user story

Before a race, Sam opens ViewLab and selects iRacing from the applications it has discovered. A
simple profile summary says that this game uses a conservative horizontal crop, a slightly larger
top crop, 115% render scale, a shaped visor, the racing information policy, and a known-compatible
overlay path. Sam does not reconstruct those decisions each evening. Launching iRacing makes that
profile active; games without a specific choice inherit the global baseline.

The OpenXR layer reports the reduced projection and recommended render size early enough for the
game to allocate the intended eye textures. Edge treatment makes the retained region look clean,
and the visor gives the crop a physical visual explanation. The selected ReShade preset sharpens
the retained image. These systems have no reason to announce themselves during normal use. Their
success is the absence of needless pixels, rough boundaries, and repeated setup.

After changing headset resolution, Sam deliberately enters Calibration. Normal overlays are
suppressed and a guided sequence shows a pixel grid, centre reference, circles, edge probes, colour
steps, and temporal markers. Each pattern states what to inspect and what a failure means. The
patterns use the submitted eye-image geometry rather than shrinking with the crop, so a pixel
measurement remains a pixel measurement and a centred pattern remains centred in the view. Sam
finishes the sequence; Calibration leaves no pattern behind.

On the grid, ViewLab is quiet. The crosshair is enabled as a faint personal preference, but the
HUD and trace are in alarm-only mode. Performance samples and trace history continue in the
background. There is no permanent instrumentation merely to demonstrate that instrumentation
exists.

Halfway through the race, frame delivery becomes uneven. The sustained condition crosses the
configured alarm threshold, so a compact HUD appears. Its four status symbols say, in one visual
sentence, that CPU, GPU, and application work are healthy while VR cadence is unstable. The trace
appears beside it with the cadence channel emphasised and enough preceding history to show when the
change began. Sam glances on the straight and stops wondering whether the whole computer is
overloaded. When delivery recovers, the immediate warning clears first; the trace remains briefly
for context, then both fade.

A car reaches Sam's left rear quarter. A restrained amber glow occupies the left periphery. It is
spatial information delivered spatially: there is no label to read and no need to look away from
the road. When overlap clears, the glow clears promptly. A local yellow then changes the visor
border to a controlled yellow caution state. Because the flag outranks performance and lap
information, it owns the relevant border until the flag state changes.

At the timing line, a small card reports the completed lap, its validity, and delta. It is useful
but not urgent, so it waits if the flag cue needs the same visual region. It fades after the result
has been readable once. A Discord notification arrives at the same moment. ViewLab holds it rather
than stacking another card over the race. On the following straight it shows the source, sender,
and a privacy-filtered preview, then disappears before the next braking zone.

During the session the experimental topmost compositor backend fails to submit. Its retry budget
is bounded and the backend latches off for the session. If the established direct path remains
safe, ViewLab uses it; otherwise the affected overlays disappear. A rate-limited log records the
exact OpenXR result, backend, and transition. The race continues. No optional visual is worth a
submission storm or a damaged graphics session.

After exiting, ViewLab offers a compact session report in the desktop application: when the
performance alarm began, which channel led it, whether telemetry disconnected, which desktop
notifications were deferred or suppressed, and which subsystem fell back. Detailed graphs and
logs belong here, where attention is cheap. Sam can diagnose the session without asking the next
race to carry a desktop dashboard inside the headset.

## 3. Feature-question map

Priority runs from **P0** (protect the underlying experience) to **P7** (deliberate setup and
diagnostics). It describes information precedence, not engineering importance.

| Feature | User question | Smallest useful answer | Normal visibility | Activation and dismissal | Priority | Failure mode |
|---|---|---|---|---|---:|---|
| Layer enable/bypass | "Should ViewLab affect this application?" | Enabled, bypassed, or unavailable with a reason | Settings only | Profile/application launch; remains until changed | P0 | Bypass cleanly and record why |
| Safe rendering/fallback | "Can I trust ViewLab not to destabilise the session?" | Usually no message; exact failure after the session | Hidden unless action is required | Automatic fault detection; latched recovery per session | P0 | Disable the failing feature, bound retries, preserve game/runtime |
| Crop and recommended resolution | "Why render peripheral image I do not use?" | A retained view with a smaller workload | Effect always present; controls in settings | Loaded with profile; changed deliberately | P7 | Use valid unclipped projection values or bypass crop |
| Per-app render scale | "Where should I spend the saved GPU budget?" | The selected supersampling level | Settings only | Loaded with profile | P7 | Fall back to runtime recommendation/global value |
| Per-application profiles | "Why rebuild settings for every game?" | The correct configuration loads automatically | Brief desktop confirmation at most | Application identification; clears on process/session end | P7 | Fall back explicitly to global, never merge stale state |
| Edge masks | "How do I hide the raw cropped boundary?" | A clean, intentional edge | Normally passive | Profile enables it; disappears when disabled | P7 | Remove mask without changing crop/projection |
| Visor mask and editor | "How can the retained rectangle feel physically coherent?" | A stable visor-shaped aperture and faithful preview | Passive in VR; editor only during setup | Profile enables it; editor closes on completion | P7 | Remove visor or use proven direct path; never alter projection |
| Topmost overlay backend | "Can the visor remain above game-owned compositor layers?" | Correct occlusion with the same visual result | Backend choice should normally be automatic | Compatibility policy selects it; latch off after unsafe failure | P0/P7 | Bounded fail-closed fallback to established renderer |
| ReShade payload/remote | "How can I tune retained-image quality from VR?" | Active preset and a reachable, responsive tuning surface | Effect passive; controls only on request | Per-game preset/profile; tuning opened explicitly | P7 | Disable payload for that game and state exact incompatibility |
| Calibration workflow | "Can I trust scale, colour, geometry, timing, and submission?" | One named test and an interpretable expected result | Off in ordinary use | Explicit diagnostic session; exit clears every pattern | P7 | State unavailable test; never infer a pass |
| Crosshair | "Where is the shared visual centre?" | One fused, unobtrusive reference | Optional persistent aid | Profile/user enables; immediate disable | P6 | Hide it if stereo placement cannot be preserved |
| Performance telemetry collection | "Is there enough evidence to classify a problem?" | Truthful available/unavailable channels | Hidden collection | Session start; stops cleanly at session end | P4 | Mark unavailable, never substitute or fabricate values |
| Performance HUD | "What is unhealthy right now?" | Four compact states: CPU, GPU, APP, VR | Hidden when healthy in alarm mode | Sustained threshold; clears after stable recovery hold | P4 | Hide unavailable metrics and retain honest diagnosis |
| Performance trace | "What changed, and what led it?" | Short history with responsible channels emphasised | Hidden outside alarm by default | Records while hidden; alarm plus post-recovery hold | P4 | Keep collection independent; omit unsupported channels |
| Desktop notifications | "That sound occurred outside VR—do I care?" | Source, sender, safe preview, and age | Hidden until an eligible notification | Filtered arrival; timeout, dismissal, or priority delay | P6 | Explain permission/deployment limits; no stale/fabricated cards |
| Generic event bus | "Can information sources remain independent of presentation?" | A typed, timestamped event with source and lifecycle | Never directly visible | Provider publishes; consumer acknowledges/clears | P1–P6 | Drop malformed/stale events and isolate provider failure |
| Peripheral spotter cue | "Which side is occupied now?" | Left/right/both peripheral amber cue | Hidden with no overlap | Telemetry state transition; clears immediately with state | P2 | Clear stale cue on disconnect or unavailable data |
| Flag-state border | "Has race state changed urgently?" | One controlled border colour/state | Hidden for routine state | Flag transition; clears/replaces on authoritative transition | P1 | Clear on stale/disconnect; do not guess flag meaning |
| Lap result card | "What was the result of that lap?" | Time, validity, delta, and PB/session-best markers when known | Hidden between completed laps | Valid lap completion; brief readable timeout | P5 | Omit unavailable fields; reject duplicates/session resets |
| Boundary flash | "Where will this HUD/trace appear?" | A temporary placement boundary | Settings/drag operation only | User begins positioning; fades after release | P7 | Disappear; never persist into play |
| Application discovery/list | "Which games does ViewLab know, and what will each use?" | Searchable app state and profile summary | Desktop settings only | Runtime discovery/user management | P7 | Preserve unknown apps and global fallback |
| Update checking | "Is a newer trustworthy build available?" | Current/update state and deliberate action | Desktop settings only | Manual or restrained startup check | P7 | Non-blocking offline state; never affect VR startup |
| Diagnostics/logging | "What failed, when, and under which configuration?" | Rate-limited event with actionable context | Logs/post-session, not in-headset | State change/fault; retained by bounded policy | P0 | Logging failure must not enter render path or retry loop |
| Installation/registration | "Will the layer and optional integrations start reliably?" | Installed/registered status with repair action | Installer/settings only | Install, upgrade, repair, uninstall | P0/P7 | Leave runtime bypassable; preserve live user configuration |

Developer-only telemetry injection, verbose logging, legacy renderer implementations, retired crop
experiments, and deterministic HUD values are not product features. They may remain available to a
diagnostic workflow, but they should not compete with ordinary controls or imply support.

## 4. Attention model

### Visibility classes

**Immediate:** only information whose delay can harm the session or invalidate the user's next
action. This includes a ViewLab failure requiring intervention, an urgent flag state, and live car
overlap. Immediate does not mean large; spatial cues should use the smallest unmistakable signal.

**Interruptible:** performance alarms, lap results, and eligible desktop notifications. These can
wait for a higher-priority cue, share no more than one card region, and expire if their useful
moment has passed.

**Settings only:** crop, render scale, mask geometry, backend compatibility, ReShade installation,
profile editing, permissions, and debug verbosity. They shape the experience but are not live
experience content.

**Post-session:** complete traces, fault detail, provider connection history, deferred/suppressed
events, update information, and comparative tuning evidence. Diagnose after the event whenever
the user does not need the answer during the corner.

### Priority and arbitration

The default hierarchy is:

1. **P0 — containment:** a failure that needs the user's action; silent safe fallback otherwise.
2. **P1 — race state:** urgent flags and safety state.
3. **P2 — spatial overlap:** live left/right/both occupancy.
4. **P4 — performance:** a sustained actionable anomaly, not a single noisy sample.
5. **P5 — result:** completed lap information.
6. **P6 — external information:** filtered desktop notifications and routine messages.
7. **P7 — setup:** calibration and placement, entered deliberately and mutually exclusive with
   normal attention arbitration.

Priority is mediated by visual channel. A left peripheral spotter glow may coexist with a compact
performance HUD because they do not demand the same gaze or surface. A flag border and a
notification card should not multiply into unrelated animations. When cues contend for one
channel, the lower priority is queued if it will remain useful, summarised if several similar items
arrive, or discarded with a post-session record if it has expired.

### Alarm-only behaviour

Alarm-only systems collect and retain a bounded history while invisible. Activation requires a
meaningful, preferably sustained condition. Appearance begins with the classification, then offers
supporting history. Recovery removes urgency immediately but preserves context for a short,
configurable hold. Repeated threshold crossings extend the current incident rather than replaying
animations. A channel without reliable data cannot trigger an alarm.

### Motion and fades

Animation establishes where information came from and whether it is arriving or resolving; it is
not decoration. Peripheral cues should fade in quickly enough for safety and fade out promptly
when state clears. Cards should enter from a consistent safe region, remain stationary while being
read, and leave before the next likely action point. Reduced-motion presentation must retain the
same information through opacity/state changes. No continuous pulsing unless it communicates a
continuing urgent state.

### Clutter budget

Normal gameplay targets zero demanded glances. At most one gaze-dependent card should be active by
default. Persistent aids such as a crosshair are user-chosen and visually quiet. Calibration owns
the display only while explicitly active. Debug values and implementation states belong on the
desktop. The product does not recreate Windows inside the headset; it imports a selected answer.

## 5. Product principles

1. **One glance, one answer.** A live element should resolve one current uncertainty without
   interpretation across several panels.
2. **Every feature earns its time on screen.** Capability is not a reason for visibility.
3. **Quiet is a successful state.** Healthy systems and routine values remain hidden by default.
4. **Simple by default, advanced by intent.** Common outcomes use human terms; projection,
   compositor, and debug controls remain discoverable in advanced or compatibility contexts.
5. **Profiles absorb per-game complexity.** A user should not become the integration layer each
   time a different executable starts.
6. **Diagnose after the event when possible.** Live attention is reserved for an answer that can
   change the user's next action.
7. **Peripheral information uses peripheral presentation.** Spatial facts preserve direction and
   do not become labels at the centre of gaze.
8. **Spatial meaning survives every backend.** Stereo fusion, centre, eye selection, scale, and
   placement are renderer contracts, not feature-specific approximations.
9. **Unavailable data is explicit.** It is never fabricated, inferred silently, or replaced with
   a nearby metric bearing a more convenient name.
10. **Failure removes ViewLab functionality, not the underlying experience.** Retries are bounded,
    optional backends fail closed, and rendering work never waits on telemetry or desktop APIs.
11. **Calibration is evidence.** Patterns measure submitted geometry and pixels; crop or UI scale
    must not invalidate what they claim to measure.
12. **Compatibility decisions are explainable.** Automatic choices expose their reason and allow a
    deliberate override, without making backend vocabulary the normal user journey.
13. **State transitions are complete.** Disabling a mode, changing profiles, reconnecting a data
    source, or ending a session clears stale state immediately.
14. **Privacy precedes convenience.** Desktop text is opt-in, filterable, briefly retained, and
    never logged in full by default.
15. **Truth outranks polish.** A precise unavailable state is better than a decorative checkbox or
    a convincing but unvalidated cue.

