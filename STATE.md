# STATE — live project state

> Single source of truth for "where are we". Keep this file SHORT (target < 150 lines): current version,
> unbuilt work, active queue, open issues. Update it in the same commit as any behaviour change. Completed
> history goes to `CHANGELOG.md`; the full pre-2026-09-26 journal is `docs/history/STATE-archive-2026-09-26.md`.

**Updated:** 2026-09-26

- **Built for headset test:** 4.1.404 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.404.msi`. Proximity colour now has an independent 0–255 signal only inside two metres; the three-panel button row is fixed to 3×2. Racing cue validation awaits user confirmation.

- **Pending headset test:** native rendering now defaults to direct eye-texture presentation even when an
  existing installed config lacks `overlay_force_direct`. iRacing presentation tests now run while telemetry is
  connected and override the corresponding live cue until cleared. Offline iRacing fixtures pass, including
  live test/clear/restore; headset visibility still needs the user's test after the next MSI.

- **Recent fixes:** proximity colour now uses the same peripheral spotter geometry, independent
  of rear-closing presentation; both theme selectors publish live. Timed race-start, proximity, rear-closing
  and shift-light tests exercise full cue progressions; racing cards omit the generic app caption. Headset
  validation remains necessary after the next MSI.

## Version

- **Last built:** 4.1.404 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.404.msi` (pending visual validation).
- **Last confirmed in-headset by the user:** 4.1.351 (2026-09-26: "it works"). Nothing after it is confirmed.
- **Nothing is committed.** All work since `ca33c96` is uncommitted on `dev` (never `git restore`/`stash` it).

## Built after 4.1.368, awaiting headset test (2026-09-26)

User requests after testing 4.1.367/368, all built into the next MSI (contracts + `Verify-PerformanceHud.ps1` pass):
1. **Colour menu EXIT MENU** — last entry (`CmExit`); Ctrl+D on it closes; Ctrl+W from the top wraps to it.
2. **Calibration on Ctrl+W/A/S/D** — Ctrl+A/D adjust, Ctrl+S next/confirm, Ctrl+W back (cancel on step 1); the
   Ctrl+Alt set still works. Started from the menu, it returns to the menu (`ColourCalibrationState::fromMenu`).
3. **Menu = OpenXR Toolkit's size, movable** — taken from the Toolkit's source: it sizes its menu in eye-image
   pixels (font 44 pt x 0.75 = 33 px, entry every 49.5 px), centred in the eye image. Ours: 4.5 image px per font
   px (row = 11 font px), never scaled to the render band. MENU SIZE (50–200%), MENU X, MENU Y entries, saved per
   app (`colour_menu_*`). Calibration panel keeps a fixed angular size (0.30 tangent tall).
4. **Panel centred on the render band** — vertical anchor = centre of the shared selected band
   (`(sharedSelectedU + sharedSelectedD)/2`), not the lens centre (the panel top reached the distorted band edge).
5. **Frame cost CPU fixed** — `HudFrameSample::cpuFrameMs` = xrWaitFrame return → xrEndFrame entry. APP work
   (begin→end) missed iRacing's simulation, so Total = max(CPU, GPU) always equalled GPU.
6. **Frame cost lines and labels** — overlaid lines picked by tick boxes (`hud_trace_cost_lines` bitmask, default
   Total+CPU+GPU) and labels Full / Minimal+ / Minimal (`hud_trace_cost_labels`); global and per-app; live in bits
   8–13 of the live graph mode. `hud_trace_cost_view` retired. (UI wiring partly done by a GPT session; reviewed.)

7. **Colour menu = OpenXR Toolkit layout** (from its menu.cpp): tab row (COLOUR / GAINS / LEVELS / MENU; A/D switches
   tab when the tab row is selected), title | value column, choices shown side by side with the current one boxed,
   EXIT MENU last on every tab. Fixed 142 x 262 font px at 3 image px per font px.
8. **Frame cost text scaled to the graph** (small 0.75x HUD glyph, big 2x small; was 1x / 3x and collided); y-scale
   1.2x budget (was 1.5x); the legend moves up a row only if it would still hit the big readout. Alarm-only in
   Frame cost mode (4.1.376) triggers on the newest Total > budget − (sensitivity − 0.5 ms); it used the HUD's
   cadence alarms, which fire on any dip below the refresh rate, so the graph never hid at 144 Hz.
9. **Optical centring** (`optical_centring`, global + per-app "Use global / On / Off") — `ApplyXRViewLabFov` keeps
   the band height and centres it on tangent 0. Next launch.

10. **Colour menu drawing** (4.1.374) — 4.1.373's HD texture menu was REMOVED (re-rendered on the render thread per
    change = frame stalls; minified = blurry). The menu draws like the HUD: block-font quads, whole pixels per font
    pixel, pixel-aligned origin. MENU SIZE default 75, key `colour_menu_size`. MENU DISTANCE = OpenXR Toolkit's
    menu_distance (cm, default 100, 10 cm steps, key `colour_menu_distance`; each eye converges (IPD/2)/D). Calibration: the game now follows the
    slider live on all three steps (steps 1–2 used to change only the checkerboard).

11. **Frame-time audit (4.1.379–380)** — colour-panel text draws one quad per lit run (was per cell); the stray
    per-live-change "crosshair: live resolve" main-log line (flushed to disk on the render thread, ~280/session)
    was deleted; the user's `verbose_logging` was switched off (it wrote ~15 lines/s during play; it is an
    AI/debug aid, keep it off for normal use). Checked fine: GPU timer never stalls, HUD hardware reads are on a
    worker thread (render thread copies a snapshot ≤ every 50 ms), config is only read at instance creation.

12. **Colour pipeline overhaul (4.1.381)** — shaders live in `Shaders\ColourGrade.hlsl`, compiled by
    `Shaders\Build-Shaders.ps1` (run by build.ps1) into committed `Shaders\ColourGrade_*.h`; no in-game D3DCompile
    for colour (was a 20–100 ms first-use hitch; the main overlay renderer still compiles once at session start).
    Engine toggle `colour_grade_lut` (default on; Render → Colour → "Fast colour (lookup table)"; live via live
    state v16 `colourFlags`): a 33³ R16F LUT in display encoding, re-baked on the GPU (33 slice draws) only when
    the settings hash changes; per pixel = one trilinear lookup. `GradeEyes` grades all eyes in one pass with
    state saved/restored once and no per-eye flush (one flush per image only when nothing else flushes).
    `PrewarmColourGrade` creates pipeline/LUT/scratch once per session while colour is On (even neutral).
    Colour section moved from Overlays to the Render menu. NOT yet compared in-headset: LUT vs maths look.

13. **Alarm sensitivity 0–1 + external edits live (4.1.385)** — frame cost alarm-only uses `hud_trace_alarm_sensitivity`
    (see CONFIG). The settings app watches xr-viewlab.ini (FileSystemWatcher) and the Apps registry subtree
    (RegNotifyChangeKeyValue); an outside change (not within 2 s of input in the window, no dialog open) reloads
    and publishes live — for the running game its per-app overrides are published as authoritative. This is how
    an assistant changes settings live: edit the ini / per-app DWORDs and the running sim picks them up.

User preferences learnt this session: in-headset UI must copy OpenXR Toolkit's behaviour (Ctrl+W/A/S/D opens and
drives menus; exit entry at the bottom), ViewLab theme = near-black + red #C90012 (no grey/white panels); the mouse
wheel must never change a setting in the app; wants terse progress updates and the MSI path after every build.

## Built, awaiting headset confirmation (2026-09-26)

Items 1–11 were first built as 4.1.363 (the Windows SDK is now installed on `D:\Windows Kits\10`; `build.ps1` finds
it via the `KitsRoot10` registry value). User feedback on 4.1.363 produced items 12–16, built in the next MSI.
Contracts and `Verify-PerformanceHud.ps1` pass.

1. **HUD editor labels readable** — `ProfileWindow.xaml` `ProfileHudWidgetList` Foreground `#E8E8E8`.
2. **Colour grade (OpenXR Toolkit port)** — `GradeEyeTexture` etc. in `dllmain.cpp`; stands down if the
   Toolkit DLL is loaded. See `docs/CONFIG.md`. Check log: `colour grade: source=… active=…`. (Follow mode removed: item 12.)
3. **Pit limiter warning** — new `RacingFlagState.PitLimiter` (appended, value 10): moving (>1 m/s) on pit road with
   EngineWarnings bit 0x10 clear. Colour `0x01FF5A00`; bit 24 of `flagColor` = generic native pulse (2 Hz, ≥35%).
   Visibility checkbox `iracing_flag_show_pit_limiter` (default on). Needs the flag-state border enabled.
4. **Performance Trace "Frame cost"** — now graph mode 4 (item 14); `hud_trace_cost_view`
   (0 total, 1 CPU, 2 GPU, 3 wait, 4 all), per-app `overlay_override_trace__…` overrides.
   GPU ms = D3D11 timestamp span xrBeginFrame→xrEndFrame (`GpuFrameTimer*`), only while the theme is visible.
   Total = max(CPU APP work, GPU). Axes, dashed budget line, one-decimal readout with budget colouring.
5. **Rhythm shift light** — racing state **v3 (72 bytes)** adds `shiftState` at offset 68 (bit0 active, bit1 at shift,
   bit2 over-rev, progress<<8). Provider uses `RPM`, `PlayerCarSLFirstRPM/ShiftRPM/BlinkRPM`, `Gear`. Native draws
   monocular edge tracks with closing bars and a centre pill (green flash at shift, red strobe past blink RPM).
   `iracing_shift_light`, `iracing_shift_light_opacity`; live bit 128 in `iracingFlags`. Per-car custom RPM
   (SoundShift-style) is NOT built yet.
6. **Cue themes** — `iracing_spotter_theme` (0 glow, 1 edge line), `iracing_rear_closing_theme` (0 glow, 1 mirror
   chevrons); UI combos (next launch).
7. **Fix: racing cues drawn alone** — the overlay early-return omitted race start, rear-closing and Grip-O-Bar, so
   they only appeared when another overlay was active. Now included (R62).
8. **Fix: stale broker status** — `NotificationBrokerClient` reports "not running" when the broker mutex is absent.
9. **Fix: Verify-Quest3 false failure** — post-save reload assertion made structural.
10. **Silent next-launch labels** — tooltips on the render sliders/Split checkbox and the new next-launch settings.
11. **Colour grade UI + in-headset calibration** — Settings → Colour: mode (Off / On) and the ten Toolkit-scale
    sliders. Levels stage (black, white, gamma in display encoding). Ctrl+Alt+C runs MHW's three steps and saves
    `colour_grade_levels_*` to the game's profile. Keys: Ctrl+Alt+Left/Right, Enter, Backspace, Esc.
12. **OpenXR Toolkit follow mode removed** (user: ViewLab replaces the Toolkit). Default `colour_grade_mode` 1; legacy
    2 reads as 1; no Toolkit registry reads remain (contract). User's iRacing Toolkit values were all neutral, so the
    grade never ran before calibration (log `active=0`).
13. **In-headset colour menu** — any Ctrl+W/A/S/D (or arrow) opens it like the Toolkit; Ctrl+F2 toggles; 15 s idle closes; Ctrl+W/S (Up/Down) select, Ctrl+A/D (Left/Right) change, hold to
    repeat. Items: post-processing, sunglasses, the 10 Toolkit values (shown value/10 like the Toolkit), black/white/
    gamma levels, run calibration, reset all. Saves per game (`SaveColourMenu`) on close / 1.5 s idle.
14. **Stereo panel fix** — calibration panel and menu were centred per eye rectangle (two unfused panels). Now anchored
    at the shared straight-ahead tangent via `OverlayCoordinateResolver::ResolveSharedTangent(0,0)`, sized in tangent
    units, scissored to the eye rect. Calibration step 3 dim target lowered from 0.06 to 4/255: the user matched
    "barely visible" at gamma 1.52 (0.06^1.52 ≈ 3.6/255), which darkened the whole image. Their iRacing levels were
    deleted from the registry (back to neutral) on 2026-09-26.
15. **Frame cost is graph mode 4** (was a separate next-launch "Theme" combo that did not apply live, so switching
    it globally mid-session did nothing). Live: the UI packs `hud_trace_cost_view` into bits 8–11 of the live graph
    mode. Theme combo removed; the cost-view picker shows only in that mode. Per-app window gained the mode + picker.
16. **Per-app window** — Colour section (shows the game's headset-saved colour, "Reset this game's colour to global");
    the visor preview only zooms after it is clicked, so the wheel scrolls the window (`WheelZoomRequiresClick`).
    App-wide (`App.RegisterWheelPassThrough`): a closed ComboBox never takes the wheel, and a ListBox with nothing to
    scroll passes it on, so the wheel always scrolls the page.

## Active queue (agreed 2026-09-26, in order)

1. **iRacing feature-local presentation tests (built 4.1.389; awaiting headset test).** Remove the generic
   `Presentation tests (no live connection required)` strip (`Left`, `Right`, `Both`, `Clear`, `Lap`, `Yellow`,
   `Blue`) from `MainWindow.xaml`. Put a test button beside/under the controls for each testable iRacing feature:
   lap popup/card, peripheral spotter (Left/Right/Both states), flag border, race-start light, rear-closing
   pressure cue, rhythm shift light, low-fuel warning card, and Grip-O-Bar if it remains an available feature.
   Test buttons must publish through the existing generic provider/event → `RacingStateService`/notification
   broker → native renderer path; they must not depend on a live iRacing connection and must never persistently
   enable production feature settings. Use an explicit test marker/temporary state distinct from telemetry.
   Persistent visual cues (spotter, flag border, rear-closing cue, shift light, Grip-O-Bar) use a toggle button:
   first click starts the synthetic cue, second click for that feature clears it. Lap/fuel cards are one-shot
   transient cards. Race-start test presents the red waiting state first and can be clicked again to clear or
   advance to the green phase; document exact button behaviour in UI tooltips. Clear synthetic state safely on
   provider/broker shutdown and prevent test state from overwriting real telemetry after a real provider update.
   Avoid per-frame work or sleeps on the render thread. Cover test-event semantics and persistence isolation with
   deterministic fixtures/contracts.

2. **Performance HUD visibility mode (built 4.1.389; awaiting headset test).** Add the same explicit
   visibility dropdown used by Performance Trace to the Performance HUD: Off / Always visible / Alarm only.
   The old widget alarm-only checkbox is retired; the dropdown alone controls whole-HUD visibility. Reuse the existing trace visibility state,
   hold/fade behaviour, and live per-app profile resolution where practical. Ensure Off disables rendering and
   avoids unnecessary sampling if no other consumer needs the samples. Update both global and per-app editors,
   live-state contract, config docs, tests, and migration defaults without silently changing existing HUD users.

3. **Clock modes and per-app countdown (built 4.1.389; awaiting headset test).** Evolve the clock into a
   small clock-toolkit experience with explicit Current time, Session stopwatch, Countdown timer, and scheduled
   target-time/alarm modes. Keep existing current-time/session-timer behavior as the migration-safe default.
   The user’s concrete workflow: configure a 15-minute timer (or a target such as 17:46) before loading a game;
   enable the clock for that app; then see a countdown line under the clock in-headset. Add start, pause/resume,
   reset, and alarm enable controls with clear state. Persist the timer configuration per app and let it run from
   a monotonic clock while the relevant XR session is active; do not decrement once per rendered frame. A target
   wall-clock alarm must define local-time semantics and handle date rollover explicitly. Colour should progress
   from normal to amber to red as remaining time falls, with thresholds documented and configurable only if the
   UI stays simple. At zero, provide a restrained visual alert; audio/haptics require existing supported paths,
   otherwise do not invent a render-thread or runtime dependency. Implement through the existing fused clock
   overlay geometry and live-state mechanism with no per-frame allocations or disk writes. Add deterministic
   tests for countdown lifecycle, pause/resume, zero, target-time rollover, and profile persistence.

4. **Spotter proximity/radar strength research and mode (built 4.1.392; awaiting headset test).**
   User wants left/right spotter cues to communicate proximity/intensity with a softer amber/orange near zone
   and a stronger red close/overlap zone, similar to a flat racing radar. First establish which iRacing SDK
   telemetry is actually available and valid for side, distance, overlap, and approach speed; inspect official
   SDK variable documentation and the existing `IRacingTelemetryProvider` parser/provider. Do not infer distance
   from left/right-only spotter state or fabricate precision. If supported data exists, define a small generic
   proximity/intensity field in the provider event and renderer contract, with conservative neutral/amber/red
   mapping and hysteresis to prevent flicker. Preserve side correctness and current test overrides. If no reliable
   data exists, report the limitation and propose the closest honest visual behavior. Any external research must
   use authoritative/official sources and record source links in the implementation notes/docs.

5. Headset-check everything already built (colour in a second game; menu and calibration fuse in both eyes).
6. Shift light per-car custom RPM (SoundShift-style).
7. Installer size: UI and broker are separate self-contained single-file publishes (two .NET runtimes). The broker
   also ships inside the signed notification-identity package, so sharing a runtime means reworking that package
   and the WiX file list — needs a real Windows build to validate.
8. Cue themes are next-launch only; make them live if wanted (live-state contract).
9. Per-app iRacing production settings do not exist (iRacing cues are global only); feature-local synthetic tests
   are separate and must not add production per-app iRacing controls.

## Open issues

- **EAC "Untrusted system file"** for the unsigned layer DLL. User has permanently declined code signing.
- **Late direct-fallback draw** (`xrEndFrame`, "direct-fallback") writes projection images after the app released
  them, before the runtime receives xrEndFrame. Outside the OpenXR ownership rules but no observed failure; left in
  place deliberately because removing it could drop the visor/overlays where the release-path draw does not run.
- **R50** is cited by contracts (`Verify-ViewLabContracts.ps1`) but has no entry in `docs/REGRESSIONS.md`.
- **DiagMon** `Process` handle leak and `StartTime`-in-comparator exception storm (opt-in capture path only).
- Six windows have no help affordance (ProfileWindow, PerformanceTrace*, DiagMon*): needs written help content.
- Split-crop value semantics changed in 4.1.253 (½-lens scaling); old configs may need ×2 (no migration).
- AMD stereo-submission research (`Tools/StereoProbe`, `ViewLabBridge/Stereo*`) is parked by the user.
