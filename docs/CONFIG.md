# Config contract — every key, both sides

## Changing settings live (for AI assistants) — read this when the user asks you to change a setting

The user wants to say things like "enable the rear-closing glow, disable the HUD and clock, make the trace less
sensitive, add a sticky note" and watch it happen in the headset without restarting the sim. That works when:
the **ViewLab settings app is open** (it watches the files below and publishes changes live) and you edit files
directly — never through the GUI.

1. **Global settings:** `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini`, section `[Settings]`, `key=value`. Edit only the
   lines you need; keep everything else. Booleans are `1`/`0`.
2. **Per-app settings WIN over global.** Find the running game's key: the newest `app=` in
   `%LOCALAPPDATA%\XR ViewLab\Logs\ViewLab.log` (iRacing = `iRacingSim64DX11.exe`). Its profile is
   `HKCU\Software\cooooked\xr-viewlab\Apps\<exe>`. Overlay overrides there are `REG_SZ` values named
   `overlay_override_<feature>__<key>` (features: `clock`, `hud`, `trace`, `sticky`, `crosshair`, `notifications`).
   If the game has ANY `overlay_override_<feature>__*` value, change that feature THERE (same key, as a string), or
   delete all of that feature's override values to make it follow global. Colour/optical-centring per-app values are
   `REG_DWORD`s with the plain key name (`colour_grade_*`, `optical_centring`).
3. Save once; the app reloads within ~0.3 s and the game updates live. Changes to render crop / FOV / resolution
   (`top_render_height`, `split_mode`, `optical_centring`, …) only apply at the next game start — tell the user.
4. Afterwards, say in one line what you changed and where.

Common requests (overlay keys below are the global ini names; per-app use `overlay_override_<feature>__<key>`):

| Request | Key(s) |
|---|---|
| Performance HUD on/off | `hud_enabled` (feature `hud`) |
| Clock on/off | `clock_widget_enabled` (feature `clock`) |
| Performance Trace: off / always / alarm only | `hud_trace_visibility_mode` `0`/`1`/`2` (+ `hud_trace_enabled`) (feature `trace`) |
| Performance HUD: off / always / alarm only | `hud_visibility_mode` `0`/`1`/`2` (+ legacy `hud_enabled`) (feature `hud`) |
| Trace less/more sensitive (alarm only, frame cost) | `hud_trace_alarm_sensitivity` 0–1 (lower = less sensitive) |
| Trace fade in / out | `hud_trace_fade_in_ms`, `hud_trace_fade_out_ms` (0–1000) |
| Trace graph mode | `hud_graph_mode` (4 = frame cost) |
| Rear-closing glow / spotter / flags / race start / grip bar / shift light | `iracing_rear_closing`, `iracing_spotter_glow`, `iracing_flag_border`, `iracing_race_start`, `iracing_grip_bar`, `iracing_shift_light` (global only) |
| Low fuel warning | `iracing_fuel_warning` (global; the notification broker applies it) |
| Sticky note | `sticky_note_enabled=1`, `sticky_note_count=N`, then per note `i` from 0: `sticky_note_{i}_enabled`, `_text`, `_x`, `_y` (0–1, 0 = left/top), `_scale`, `_opacity`, `_theme` (0 Classic yellow, 1 Rose — use for pink/magenta, 2 Mint, 3 Sky, 4 Paper), `_style` (0 standard, 1 HD paper) |
| Colour | `colour_grade_*` (see Colour grade below); engine `colour_grade_lut` |

## Standalone DynLOD editor

`Tools/DynLOD/IniFile.cs` reads/writes only nine existing keys in the selected iRacing renderer INI.
Main is `[Graphics Options]`; Replay is `[Replay Graphics]`, verified in the local OpenXR renderer INI.
Following iRacing's own UI writes and current iRSidekick, World Main maps to `LODPctDynoMin`/`LODPctDynoMax`
and Cars Main maps to `LODPctMin`/`LODPctMax`; the corresponding mirror keys use the same family plus `Mirrors`.
World and Cars selectors reproduce the six iRacing preset value sets; direct numeric edits select Custom.
LOD values are whole numbers 25–500 with Min <= Max. iRacing also requires World Max >= Cars Min independently
for Main and Mirrors; startup expands World Max to Cars Min when violated, so validation and slider bounds prevent it.
Drag/track clicks snap within 4 points of multiples of 25; typed values
remain unsnapped. `LODMinFPSTarget` is the literal nonnegative integer text,
without a refresh-rate transform. Existing invalid values remain visible for correction; missing/duplicate keys
or sections fail explicitly. Neither loading nor Refresh changes the INI. Apply rereads the latest file and replaces
only the target numeric tokens, retaining comments, whitespace, encoding and newlines. Atomic file replacement
creates `<ini>.dynlod-<timestamp>-<id>.bak`. Only the selected path persists in `%LOCALAPPDATA%/DynLOD/selected-ini.txt`;
the edit buffer stays in memory after Apply, and Refresh/source selection replaces it with disk values.

## Factory baseline and per-app overlays

`config/factory-baseline-v4.1.255.json` is the machine-readable clean-install and missing-key baseline. Startup
applies only its listed keys once when `HKCU\Software\cooooked\xr-viewlab\FactoryBaselineAppliedVersion` is not
`4.1.255`. It never alters `Apps`, ReShade registration, payload files or handshake state.

Per-app true-overlay values are `overlay_override_<feature>__<canonical_ini_key>` registry strings. OBS Recording
Cue and iRacing Telemetry use this form only for enable state. Layout uses `overlay_layout_<id>_{x,y,scale}`.
The native layer publishes its current executable key through `Local\XRViewLabActiveProfileV1`; the independent
notification broker uses that key to resolve saved `overlay_override_notifications__*` values and to validate the
generation-stamped `Local\XRViewLabNotificationSettingsV1` live mapping. The mapping carries unsaved composition,
filter, media and queue settings only for the matching active executable; unscoped globals apply only when the
profile inherits notifications. The broker is resident from login, so since 4.1.295 it reads saved settings
settings on demand instead of once per second: a `FileSystemWatcher` on the INI (300 ms debounce) covers global keys,
a 2 s `OpenFileMappingW` probe of that mapping covers profile switches without throwing while no session exists, a
30 s timer is the fallback, and the settings app sends the existing `refresh` pipe command after a per-app profile
save because registry overrides are invisible to a file watcher.
`experimental_draw_in_void` defaults off and has no renderer effect. ReShade Remote preferences use
`reshade_remote_xr_mode`, `reshade_remote_menu_visible` (in-HMD only),
`reshade_remote_desktop_menu_visible`, `reshade_remote_win_headless` and
`reshade_remote_win_always_on_top`. The payload's configurable keys use
`reshade_remote_hotkey_{toggle_effects,next_preset,previous_preset,toggle_menu}` and
`reshade_remote_hotkey_menu_desktop`. `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini` is authoritative for all of
them; the old ProgramData `[Window]` values are compatibility-read-only when a preference is absent. Deployment
state is deliberately absent. Fresh installs start with the in-HMD menu hidden and desktop preview visible.

## OBS mirror-only visibility

`obs_mirror_show_visor`, `obs_mirror_show_hud`, `obs_mirror_show_trace`, `obs_mirror_show_clock`,
`obs_mirror_show_notifications`, `obs_mirror_show_sticky_notes`, `obs_mirror_show_crosshair`,
`obs_mirror_show_boundary_flash`, `obs_mirror_show_recording_cue`, and `obs_mirror_show_racing_cues` default to `1`.
They select features drawn into `OpenXROBSMirrorSurface`; headset visibility is unchanged.

> Canonical reference for the UI ↔ DLL configuration contract. Update in the same commit as any
> key change. A key that exists on only one side is a bug (see REGRESSIONS R3).

Live ini: `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini` (section `[Settings]`; UI writes via
`WritePrivateProfileString`, DLL reads in `LoadConfig`). Bundled defaults: repo `xr-viewlab.ini`.
Per-app registry: `HKCU\Software\cooooked\xr-viewlab\Apps\<exe>` — DWORD encodings:
**millis** = round(v·1000); **signed millis** = round((v+1)·1000); render_scale = v·1,000,000.

## DiagMon(ster) capture settings

DiagMon capture policy is deliberately separate from the native layer ini. It lives at
`%LOCALAPPDATA%\XR ViewLab\DiagMon\settings.json`; the implicit OpenXR layer never reads it.

| JSON property | default | Behaviour |
|---|---:|---|
| `standardSampleSeconds` | 2 | Target and Windows-counter cadence in Standard mode. |
| `detailedSampleSeconds` | 1 | Cadence in Detailed and Trace modes. |
| `traceMaximumMinutes` | 10 | Hard time cap for an explicitly requested WPR Trace capture. |
| `retentionDays` | 30 | Storage guidance shown in the cockpit; no automatic evidence deletion. |
| `retentionSessionCount` | 20 | Session-count guidance shown in the cockpit. |
| `retentionMaximumMb` | 250 | Total-session-size guidance shown in the cockpit. |
| `presentMonPath` | empty | Optional fallback executable path. The pinned `PresentMon-2.4.1-x64.exe` installed beside ViewLab takes precedence; absence of both is reported as a missing collector. |

Retention limits are warnings rather than silent deletion. The user deletes a session explicitly in
the Session Library, with confirmation; valid raw evidence is never removed by a background policy.

## Racing cues and Performance Trace additions (2026-09-26, source only)

| Key | Where | Default | Meaning |
|---|---|---|---|
| `iracing_flag_show_pit_limiter` | ini; broker `ReadFlagVisibilityMask` | `1` | Show the pit-limiter warning border (flag state `PitLimiter`, value 10) |
| `iracing_shift_light` | ini; live `iracingFlags` bit 128 | `0` | Rhythm shift light |
| `iracing_shift_light_opacity` | ini (session start) | `0.9` | Shift light opacity 0.05–1 |
| `iracing_shift_light_width` | ini; live state | `1.0` | Shift-light line width multiplier 0.25–3.0 |
| `iracing_shift_light_position` | ini; live state | `0.035` | Inward position from the outer eye edge, 0–0.35 of eye width |
| `iracing_spotter_theme` | ini + live state | `0` | `0` glow · `1` edge line. Switching in the settings UI applies live. |
| `iracing_spotter_line_width` | ini + live state v19 | `1.0` | Edge-line thickness multiplier 0.25–3.0; initial fallback is the shift-light width. |
| `iracing_spotter_line_inset` | ini + live state v19 | `0.18` | Inward position from the outer eye edge, 0–0.35 of eye width; initial fallback is the shift-light position. |
| `iracing_rear_closing_theme` | ini + live state | `0` | `0` glow · `1` mirror chevrons. Switching in the settings UI applies live. |
| `hud_trace_theme` | legacy (read only) | `0` | Retired: `1` is read as `hud_graph_mode=4`. The UI always writes `0`. |
| `hud_trace_cost_lines` | ini; per-app `overlay_override_trace__hud_trace_cost_lines`; live bits 8–11 of the live graph mode | `7` | Frame cost lines, overlaid (bitmask): `1` total · `2` CPU · `4` GPU · `8` wait; `0` reads as `7`. CPU = xrWaitFrame return → xrEndFrame entry. |
| `hud_trace_cost_labels` | ini; per-app `overlay_override_trace__hud_trace_cost_labels`; live bits 12–13 | `0` | `0` full (axis values, time span, legend, big readout) · `1` minimal+ (big readout only) · `2` minimal (lines + budget line) |
| `hud_trace_alarm_sensitivity` | ini; per-app `overlay_override_trace__hud_trace_alarm_sensitivity`; live traceFlags bits 2–7 (×63) | `0.8` | Frame-cost alarm trigger: 0 = never, 0.5 = the frame budget, 0.25 = serious overruns only, 0.75 = near-misses and overruns, 1 = always visible. One effective trigger line; no rolling frame-count rule. |
| `hud_trace_fade_in_ms` / `hud_trace_fade_out_ms` | ini; per-app `overlay_override_trace__hud_trace_fade_in_ms` / `_out_ms`; live in traceFlags bits 8–19 / 20–31 | `150` / `150` | Alarm-only fade in / fade out time (0 = instant). The short-lived `hud_trace_fade_ms` is read as the default for both. |
| `hud_trace_cost_view` | retired | — | Replaced by `hud_trace_cost_lines`; no longer read. |

Racing state mapping `Local\\XRViewLabRacingState` is now **version 4, 76 bytes**: `shiftState` at offset 68 (bit0
active, bit1 in shift window, bit2 over-rev, bit3 within 50 RPM of the reported shift point, bits 8–15 progress); `spotterProximity` at offset 72 carries the independent 0–255 value for a car behind within two metres. `flagColor` bit 24 asks the native border to pulse.

## Colour grade (OpenXR Toolkit post-processing port)

Native-only; read once at session start by `LoadColourGradeConfig()` (called at the end of `LoadConfig()`).
Values use OpenXR Toolkit's own 0–1000 units and defaults so imported values match exactly.

| Key | Where | Default | Meaning |
|---|---|---|---|
| `colour_grade_mode` | ini `[Settings]`; per-app DWORD overrides | `1` | `0` off · `1` on (ViewLab values below). The removed follow-OpenXR-Toolkit value `2` reads as `1`; ViewLab never reads Toolkit settings. |
| `colour_grade_lut` | ini `[Settings]`; per-app DWORD override (absent = global); live (live state v16 `colourFlags` bit 0, the settings app publishes the running game's effective value) | `1` | Colour engine: `1` baked 33³ lookup table (constant per-pixel cost), `0` per-pixel maths. |
| `colour_grade_post_process` | ini; per-app DWORD overrides | `1` | Toolkit "post-processing" switch: `0` applies colour gains only |
| `optical_centring` | ini `[Settings]`; per-app DWORD override (absent = global) | `0` | `1` keeps the vertical crop band's height but centres it on straight ahead (tangent 0 = the Quest 3 lens optical centre) instead of trimming top/bottom in proportion. Band-anchored overlays follow. Next launch. |
| `colour_menu_scale` / `_x` / `_y` | per-app DWORD (written by the in-headset menu) | `100` / `1000` / `1000` | Colour menu size in percent (50–200) and offset in 1/1000 tangent units + 1000 (x right, y up). The menu is a fixed tangent size in every game, centred on the shared render band. |
| `colour_grade_contrast` / `_brightness` / `_exposure` / `_saturation` | ini; per-app DWORD overrides | `500` | Toolkit Contrast/Brightness/Exposure/Saturation (500 = neutral) |
| `colour_grade_gain_r` / `_gain_g` / `_gain_b` | ini; per-app DWORD overrides | `500` | Toolkit colour gains (500 = neutral) |
| `colour_grade_vibrance` / `_shadows` | ini; per-app DWORD overrides | `0` | Toolkit Vibrance / Shadows (0 = neutral) |
| `colour_grade_highlights` | ini; per-app DWORD overrides | `1000` | Toolkit Highlights (1000 = neutral) |
| `colour_grade_sunglasses` | ini; per-app DWORD overrides | `0` | Toolkit preset: 0 none, 1 light, 2 dark, 3 night |
| `colour_grade_levels_black` / `_white` / `_gamma` | ini (0–1 / 0–1 / 0.4–2.5); per-app DWORD ×1000 overrides | `0` / `1` / `1` | Levels from the in-headset calibration, applied after the Toolkit stage in display encoding: `black + (white − black) · value^gamma` |

The Settings app writes `colour_grade_mode` and the ten `colour_grade_*` values (Colour section) as the global
default. The in-headset colour menu (any Ctrl+W/A/S/D or Ctrl+arrow opens it, Ctrl+F2 toggles, 15 s idle closes; Ctrl+W/S or Ctrl+Up/Down select, Ctrl+A/D or Ctrl+Left/Right change)
writes every `colour_grade_*` value, including the levels and `colour_grade_mode=1`, as DWORDs to the running game's
profile key on close or after 1.5 s idle. The in-headset calibration (Ctrl+Alt+C; Left/Right adjust, Enter
next/confirm, Backspace back, Esc cancel) writes the three `colour_grade_levels_*` DWORDs to the same key. The
per-app profile window shows these values and can delete them ("Reset this game's colour to global"). The levels stage still runs when OpenXR Toolkit is
loaded; only the Toolkit-equivalent stage stands down.

Mode `2` reads `HKCU\SOFTWARE\OpenXR_Toolkit\<XrApplicationInfo::applicationName>` (`post_process`, `post_sunglasses`,
`post_contrast`, `post_brightness`, `post_exposure`, `post_saturation`, `post_vibrance`, `post_highlights`,
`post_shadows`, `post_gain_r/g/b`), falling back to `HKLM\SOFTWARE\OpenXR_Toolkit` exactly as the Toolkit does.
Toolkit `post_process=0` (Off) or `2` (CA correction) applies only colour gains, as the Toolkit's pass-through shader does;
`1` applies the full grade. CA correction itself is not ported. Missing values are Toolkit defaults (neutral).
When every resulting shader parameter is neutral the pass is skipped (zero GPU cost). When the OpenXR Toolkit layer DLL
is loaded in the same process ViewLab stands down so the image is never graded twice.

## Render crop (core perf feature)

| ini key | range/default | DLL global | per-app | Notes |
|---|---|---|---|---|
| `enabled` | 1 | `enabled` | `app_enabled`, `profile_enabled` | master switch |
| `total_render_height` | 0..1 / 0.18 | `totalTangent` | millis | legacy fallbacks `total_share`, `vertical_tangent` |
| `split_mode` + `top_tangent` / `bottom_tangent` | 0.09/0.09 | `topTangent`/`bottomTangent` | millis | Stored values are full-lens shares (`0..0.5` effective per side). UI split controls are half-relative `0..1`, converted ×0.5 on save and ×2 on load. |
| `horizontal_render_width` | 0..1 / 0.80 | `horizontalRenderWidth` | millis | Exact retained width. `0.8` keeps 80% of each eye's submitted horizontal span; with outer-edge-only policy the entire 20% is removed from the outer boundary while the inner boundary stays fixed. |
| `crop_outer_edges_only` | 1 | `cropOuterEdgesOnly` | — | **Permanently enabled** — config key ignored. Horizontal crop takes from outer edges only. |
| `foveated_center_compensation` | 0 | retired/false | dword | Retained only for compatibility; ignored and permanently off because pose compensation tilted asymmetric crops. |
| `visual_mask_only`, `horizontal_visual_mask_only` | 0 | same names | — | mask instead of crop (loses GPU savings); Edge Masks popup "Both" controls write these keys |
| `render_scale` | 0.1..3 / 1.0 | `renderScale` | ×1e6 dword | per-game supersampling |

## Visor mask

| ini key | range/default | DLL global | per-app | UI control |
|---|---|---|---|---|
| `mask_enabled` | 0 | `maskEnabled` | `mask_enabled` DWORD when `visor_size > 0`; absent when using globals | Visor mask checkbox |
| `mask_size` | 0.1..1 / **1.0** | `visorSize` | `visor_size` millis | Uniform visor-opening scale. `1.0` preserves the existing full opening; smaller values hide an outer band without changing crop/FOV/resolution. |
| `mask_corner` | 0..1 | `visorCurve = 1 − maskCorner` | `mask_corner` millis when custom | Curve slider (stored inverted). Near zero stays visually square through one continuous curve; Nose remains active. |
| `mask_outer_apex_y` | −0.5..0.5 / 0 | `visorOuterApexY` | signed millis | Outer Dip slider + red pin |
| `mask_inner_lower_y` | 0..0.666 / 0 | `visorInnerLowerY` | millis | Nose slider + orange pin |
| `mask_nose_spread_x` | 0..0.5 / 0 | `visorNoseSpreadX` | millis | Nose Spread X. Moves the left-eye nose boundary left and the right-eye boundary right by the same normalized amount. Zero preserves prior geometry. Published live and supported by per-app visor overrides. |
| `mask_inner_bridge_width` | 0..1 / 0.5 | `visorInnerBridgeWidth` | millis | Legacy compatibility key; the main editor fixes the supported curve at 0.5. |
| `mask_inner_bridge_rise` | −0.5..1 / 0 | `visorInnerBridgeRise` | legacy millis plus extended marker encoding for per-app profiles | Legacy compatibility key; the main editor fixes the supported curve at 0. |
| `mask_inner_bridge_peak_x` | −1..2 / 0.5 | `visorInnerBridgePeakX` | legacy millis 0..1000; extended marker encoding for per-app profiles | Legacy compatibility key; the main editor fixes the supported curve at 0.5. |
| `mask_inner_bridge_steepness` | −1..2 / 0.5 | `visorInnerBridgeSteepness` | legacy millis plus extended marker encoding for per-app profiles | Legacy compatibility key; the main editor fixes the supported curve at 0.5. |
| `visor_live_revision` | monotonic timestamp | `liveVisorRevision` | — | Internal commit marker. The UI writes it last after global visor controls, allowing a safe live visor-only refresh at `xrEndFrame`. |
| `mask_width_scale` / `mask_height_scale` | 0.25..2 / 1.0 | `visorWidth`/`visorHeight` | `visor_width`/`visor_height` millis when custom | Width and Height sliders; Size scales both uniformly. |
| `visor_technique` | `c` | `visorTechnique` | — | a/b hidden; DirectWrite is the product path |
| `visor_hd` | 0 | — | — | **Removed** — code disabled; key ignored. |
| `visor_antialiasing` | 0 | — | — | **Removed** — code disabled; key ignored. |
| `preview_circle_guides` | 1 | UI-only | — | Preview calibration preference. `1` shows two overlapping true circles; `0` shows one binocular oval. Both use the same 85% width / 90% height periphery boundary and do not alter runtime. |
| `preview_per_eye_frames` | 0 | UI-only | — | Independent frame-guide preference. `0` shows one combined binocular outer frame; `1` shows two overlapping per-eye rectangles at the actual `2064:2208` eye aspect. Guide-only; crop and runtime are unchanged. |
| `preview_optical_centre` | — | retired | — | The separate preview-only toggle was removed: the main and per-app previews now show the optical-centred layout exactly when `optical_centring` (global, or the app's override) is on. |
| `preview_ipd_mm` | 50.0..80.0 / 67.0 | UI-only | — | Calibration-helper IPD with 0.1 mm input steps. Changes only centre separation/overlap for the two-circle and two-per-eye-frame guides. It never changes crop, visor, overlays or native runtime output. |

Global visor controls are always published through the generation-stamped live-state mapping while
the UI is open. The retired `live_visor_tuning` switch is ignored/removed; per-app profile overrides
remain startup-owned and are never overwritten by the global live snapshot.

The profile window has two distinct global actions. `Use global visor settings` stores
`visor_size=0` while preserving the app's crop/resolution override. `Use Global Values` is the
explicit whole-profile reset and clears `profile_enabled`. Saving ordinary crop/resolution edits
always writes `profile_enabled=1`, reloads the registry-backed app list, and never treats the
visor-only checkbox as permission to discard the profile.

## Stencil / visibility mask

| ini key | default | DLL global | Notes |
|---|---|---|---|
| `stencil_outer_edges_only` | 1 | `outerEdgeVisibilityMaskOnly` | **Permanently enabled** — config keys ignored. Legacy fallback `outer_edge_visibility_mask_only` also ignored. Drives the 3-part stencil pipeline (ARCHITECTURE) |
| `visibility_mask_visor` | 0 | ignored/retired | retired legacy hidden-mesh reshaper; `1` is logged and ignored |

## Diagnostics / misc

| ini key | default | DLL global |
|---|---|---|
| `verbose_logging` | 0 | `verboseLogging` |
| `calibration_grid` | 0 | `calibrationGrid` | 64 px full-eye texture grid; every fourth line is thicker. |
| `calibration_ruler` | 0 | `calibrationRuler` | Bottom pixel ruler: 8 px ticks, 32 px medium ticks, 64 px major ticks. |
| `calibration_gratings` | 0 | `calibrationGratings` | Repeated 1/2/4 px black/white full-width bands at 25/50/75% height. |
| `calibration_bars` | 0 | `calibrationBars` | Eight colour bars plus a 16-step grey ramp. |
| `calibration_beacon` | 0 | `calibrationBeacon` | 24 px temporal beacon driven once per submitted projection frame. |
| `calibration_edge_probes` | 0 | `calibrationEdgeProbes` | 1/2/4 px probes at top, bottom, inner, and outer eye edges. |
| `calibration_checkerboards` | 0 | `calibrationCheckerboards` | 1/2/4/8 px checkerboard ladder. |
| `calibration_zone_plate` | 0 | `calibrationZonePlate` | Radial spoke and ring pattern for aliasing/falloff inspection. |
| `calibration_clipping_steps` | 0 | `calibrationClippingSteps` | Near-black and near-white clipping steps. |
| `calibration_motion_strip` | 0 | `calibrationMotionStrip` | Frame-serial-driven moving stripe marker for temporal artefacts. |
| `hud_enabled` | 0 | `hudEnabled` | Enables the modular performance-widget row as one stereo-coherent visor-space element. |
| `hud_trace_visibility_mode` | 0 | graph visibility | 0 off, 1 always visible, 2 alarm only. Alarm-only records while hidden, uses sustained widget alarm state, recovery hold and a 500 ms fade. |
| `hud_visibility_mode` | derived from `hud_enabled` | whole HUD visibility | 0 off, 1 always visible, 2 alarm only. Alarm mode uses enabled widget alarms and the trace hold/fade durations. This is the sole HUD alarm setting. The visible section-header enable checkbox mirrors off/on with this dropdown in the global and per-app editors. |
| `hud_trace_enabled` | 0 | migration only | Legacy boolean read only when `hud_trace_visibility_mode` is absent; saves mirror mode != off for older builds. |
| `overlay_force_direct` | 1 | backend diagnostics | Direct eye-texture rendering is the default even when this key is absent from an existing installed config. Set to 0 only to try the ordered Topmost carrier. Topmost gets one stable allocation attempt per session; any failure latches direct fallback. Restart the game after changing this setting. |
| `topmost_visor_overlays` | — | — | Legacy experimental switch; ignored. Backend choice is automatic. |
| `hud_anchor_x`, `hud_anchor_y` | 0.04, 0.05 | `hudAnchorX`, `hudAnchorY` | Full-lens normalized position in the shared binocular coordinate system; crop clips rather than redefining X/Y. Live HUD sliders retain the full 0–1 range. |
| `hud_scale`, `hud_spacing`, `hud_opacity` | 1.0, 0.018, 0.70 | shared overlay/HUD layout | Whole-widget scale (0.15–3.0), normalized gap, and opacity. Rings, literal labels, unit-bearing values, spacing, and padding scale together. |
| `hud_safe_margin`, `hud_clamp_to_visible` | 0.025, 1 | HUD layout | Normalized safe margin and complete-bounds clamp against the binocular overlap region. The HUD uses the smaller current eye-region dimension then applies `hud_scale`, so crop/resolution changes retain its proportion. |
| `hud_update_ms` | 100 | HUD telemetry | Bounded CPU/GPU/widget-state refresh period (50–1000 ms). Fixed-ring OpenXR timing samples feed APP, VR, and graph channels per frame. |
| `hud_green_threshold`, `hud_red_threshold` | 75, 90 | migration only | Legacy general percentage values used only as fallback defaults before per-widget keys exist. |
| `telemetry_settings_version` | 2 | telemetry migration | Version 2 adds 144 Hz cadence defaults. Untouched global VR/frame-interval 103/108 thresholds migrate once to 102/105; custom global and per-app values are preserved. Kept separate from the overlay live-state contract. |
| `hud_widget_{cpu,gpu,app,vr,cpu_peak,cpu_frequency,ram,commit,vram,sys,fps,frame_interval,network_ping,network_loss,network_jitter,network_status}_enabled` | CPU/GPU/SYS/VR on | widget registry | Independent widget enables. All network widgets default off. Disabled widgets are omitted; unavailable configured widgets remain dormant. |
| `hud_widget_{...}_order` | CPU,GPU,SYS,VR then catalogue | widget registry | Persisted order. Invalid/duplicate positions normalize to one occurrence of every widget. |
| `hud_widget_{...}_warning`, `hud_widget_{...}_critical` | widget-specific | widget registry | Independent warning and critical thresholds for every active widget. VR/frame interval default to 102/105 percent of `predictedDisplayPeriod × detected cadence multiple`; their rolling distribution can also report unstable cadence or stable reprojection. SYS is inverse remaining headroom; network widgets use their displayed units. Legacy SYS/network keys are read only as migration fallbacks. `0/0` disables an alarm where there is no honest universal default. |
| `hud_max_per_row` | 16 | migration only | Legacy field retained in the mapping/ini. Current HUD layout deliberately packs every enabled widget into one row; there is no row-limit control. |
| `hud_graph_mode` | 0 | `HudGraphMode` | 0 deviation ms, 1 absolute milliseconds, 2 FPS, 3 percentage of cadence-aware budget, 4 frame cost (ms axes, budget line, CPU/GPU/Wait/Total readout; series from `hud_trace_cost_view`). Live. Incompatible channels are not mixed. |
| `hud_graph_frame_interval`, `hud_graph_fps`, `hud_graph_budget_deviation`, `hud_graph_app_work`, `hud_graph_wait_duration`, `hud_graph_submit_duration`, `hud_graph_display_period` | 0,0,1,0,0,0,0 | graph channels | Independent bounded-history lines. Sources/units are defined in `PERFORMANCE_HUD_REDESIGN.md`; default remains one understandable deviation line. |
| `hud_trace_sensitivity_ms` | 2 | `hudTraceSensitivityMs` | Deviation mode vertical range (±ms); in absolute-ms mode it supplies a minimum scale before automatic budget scaling. |
| `hud_trace_x`, `hud_trace_y` | 0.05, 0.75 | graph layout | Live graph position within the shared binocular overlap. Legacy key names are retained for migration. |
| `hud_trace_scale`, `hud_trace_width`, `hud_trace_opacity` | 1.0, 0.42, 0.70 | shared overlay/graph layout | Live whole-graph scale (0.25–3), base width/shape fraction (0.1–1), and independent opacity. Width is bounded to the available render area after uniform scale. |
| `hud_trace_history` | 120 | graph history | Live sample count shown (30–600); storage is always a fixed 600-sample ring. |
| `performance_trace_recording` | 0 | native trace recorder | **Opt-in since 4.1.295.** While off the layer starts no hardware-telemetry collector thread, reserves no sample ring and writes no `session-*.csv`; the Session Graph and DiagMon simply have no new evidence. The collector also runs whenever `hud_enabled` or the Performance Trace overlay is on, because those overlays consume its samples. Recording resolves at session start, while HUD/trace enable arrives live and starts the collector mid-session. An upgrade clears a pre-4.1.295 stored `1` exactly once, tracked by the `DiagnosticsOptInApplied` marker under `HKCU\Software\cooooked\xr-viewlab`. Retains a bounded one-hour ring of the same QPC samples used by the visor graph. Each session checkpoints to a unique `%LOCALAPPDATA%\XR ViewLab\PerformanceTraces\session-*.csv`; `latest.csv` remains a hard-link/copy compatibility alias. The DiagMonster Session Graph browser opens, compares and explicitly deletes retained sessions. Abrupt exit does not require a shutdown callback. |
| `performance_trace_marker_vk` | 119 (F8) | native trace marker | Windows virtual-key code for a rising-edge marker bind (UI offers F6–F12). Each press receives an exact QPC timestamp, numbered visor confirmation and post-session graph marker. Read at session startup. |
| `crosshair_offset_x`, `crosshair_offset_y` | 0, 0 | `crosshairOffsetX`, `crosshairOffsetY` | User calibration in normalized full-lens tangent coordinates. Applied to the lens-centre target before Lens Pinned clamping. |
| `hud_alarm_only` | retired | ignored | Legacy alarm-only symbols filter; the single Visibility dropdown now controls the whole HUD. |
| `hud_alarm_hold_ms` | 1500 | `hudAlarmHoldMs` | How long a red indicator stays visible from the first non-critical input (0–10000 ms). The deadline is not refreshed by the latched red display state. Cadence metrics enter after 300 ms so high-refresh bursts remain visible; all other metrics retain 750 ms sustained entry and every metric retains 750 ms recovery. |
| `hud_debug_values` | 0 | HUD telemetry | Development values: `hud_debug_cpu`, `hud_debug_gpu`, `hud_debug_app` percentages and `hud_debug_vr` milliseconds. Old `hud_debug_system` is accepted as APP fallback. Normal APP is begin-return→end-entry wall time / cadence-aware budget; VR is wait-return cadence relative to `predictedDisplayPeriod × detected multiple` (1–4). |
| `network_probe_target` | 1.1.1.1 | network worker | Numeric IPv4 target for optional network HUD probes. It describes that path only; it is not automatically a game server. Global and active per-app changes apply live. |
| `clock_widget_enabled`, `clock_session_timer_enabled` | 0, 1 | clock card | Clock visibility and the independent elapsed-session lane. With the timer disabled the card collapses to one lane. Both apply live. |
| `clock_timer_mode` | 0 | lower clock lane | 0 existing session timer, 1 pausable session stopwatch, 2 countdown, 3 next local target time. The current local time remains on the upper lane. |
| `clock_countdown_minutes` | 15 | countdown | 1–180 minutes, starts on successful XR session creation. |
| `clock_target_hour`, `clock_target_minute` | 17, 46 | target clock | Local 24-hour time. A target at or before the current local second resolves to tomorrow; the remaining interval then advances monotonically, so wall-clock adjustments during the session do not jump the timer. |
| `clock_alarm_enabled` | 1 | zero indicator | At zero the lower lane turns red with a restrained 1 Hz visual pulse; no audio or haptics. Amber begins at 25% remaining, red at 10%. |
| `clock_24_hour`, `clock_widget_theme`, `clock_widget_palette` | 1, 0, 0 | clock presentation | Selects 24-hour versus 12-hour AM/PM text, card design (Classic/Minimal/Terminal/Banner) and colours (Graphite/Paper/OLED/Amber/Mint). Global and active per-app changes apply live. |
| `clock_widget_x`, `clock_widget_y` | 0.50, 0.10 | clock layout | Widget centre in the shared binocular render-area frame (0–1). Bounds are clamped to the current shared overlap. |
| `clock_widget_scale`, `clock_widget_opacity` | 1.0, 0.82 | clock layout | Whole-widget angular scale (0.1-2.0) and card/text opacity (0.1-1.0). The slider and preview resize pin share this scale range. Applies live. |
| `sticky_note_count`, `sticky_note_{0..7}_{enabled,text,x,y,scale,opacity,theme,style}` | count 0; scale 1.0; opacity 0.85 | sticky note collection | Up to eight independent square paper notes. Text is capped at 120 characters. Scale is 0.1-2.5 in the global editor, per-app editor, preview, live state and every native startup/profile path; opacity is 0.1-1.0. `theme` selects Classic yellow/Rose/Mint/Sky/Paper colour (0-4); `style` independently selects the original 8-bit geometry (0) or 1024-square HD Paper (1). Missing `style` remains 8-bit for migration safety; newly added notes default to HD Paper. HD Paper uses up to five wrapped lines. The unindexed legacy note migrates to note 0 and remains mirrored for downgrade safety. |
| `hud_widget_<id>_symbol` | 0 | HUD widget presentation | Per-widget compact pictogram selection. `0` retains the literal catalogue label; changes publish live through the telemetry catalogue. |
| `notify_theme` | 0 | notification composition | Graphite/Light/OLED/Amber/Mint pre-composited card theme (0-4). Newly composed cards use the selected theme. |
| `overlay_{hud,trace,clock,sticky_note,crosshair,notifications}_toggle_vk` | 0 except sticky note 118 (F7) | shared overlay visibility | Optional rising-edge show/hide bind. `0` means None; the UI offers None and F6-F12. Bind changes publish live. |
| `sticky_note_toggle_vk` | 118 (F7) | migration only | Legacy sticky-note bind read only when `overlay_sticky_note_toggle_vk` is absent; the shared settings path mirrors it while existing settings migrate. |
| `obs_indicator_enabled` | 0 | OBS provider/native cue | Enables authenticated local obs-websocket `GetRecordStatus` polling in the broker and subtle red native visor corners only while recording is active. OBS process presence is never treated as recording. |
| `obs_websocket_url`, `obs_websocket_password` | `ws://127.0.0.1:4455`, empty | OBS provider | OBS WebSocket v5 endpoint and authentication password. The UI edits the endpoint as Host/IP plus Port and recomposes this existing URL key. Use localhost/127.0.0.1 on the same PC or the OBS computer's local-network IP when remote. The password remains in the local user ini. |
| `obs_indicator_opacity`, `obs_indicator_thickness` | 0.72, 0.009 | native cue | Red-corner alpha and thickness fraction of the eye minimum dimension. Capture exclusion is unverified; because the cue is drawn into submitted eye textures it must be assumed capturable. |
| `crosshair_enabled` | 0 | `crosshairEnabled` | Static CS-style crosshair at the calibrated stereo centre (both eyes, zero disparity). |
| `crosshair_size`, `crosshair_gap`, `crosshair_thickness` | 5, -2, 1 | crosshair | CS reference-pixel size, gap (may be negative), and arm thickness. One CS pixel is a fixed tangent span (`2/1080`), projected with each eye's X/Y pixels-per-tangent density and pixel-snapped; crop changes cannot alter angular size or convergence. |
| `crosshair_dot`, `crosshair_outline`, `crosshair_outline_thickness`, `crosshair_tstyle` | 0, 1, 1, 0 | crosshair | Centre dot, black outline + its thickness, and T-style (top arm hidden). The outlined green built-in default is immediately visible when enabled; no import is required. |
| `crosshair_alpha`, `crosshair_scale` | 1.0, 1.0 | crosshair | Crosshair alpha (0–1) and overall ViewLab VR scale (0.1–10). |
| `crosshair_color` | 65280 | crosshair | Colour as a decimal `0xRRGGBB` integer (65280 = `00FF00`). The UI parses `cl_crosshair*` configs and CS2 `CSGO-` share codes into all of the above. |
| `notify_enabled` | 0 | `notifyEnabled` | Mirror supported Windows notifications into the visor (both eyes, bottom-right of the cropped region). |
| `notify_x`, `notify_y`, `notify_scale`, `notify_opacity` | 0.98, 0.98, 1.0, 1.0 | notify render | Card anchor within the binocular overlap, card scale (0.1-3.0), and opacity. The scale slider and preview resize pin share the same bounds. |
| `notify_duration_ms`, `notify_max_visible`, `notify_privacy` | 3000, 3, 0 | notification broker | Hold time, max concurrent cards, and privacy mode (0 full / 1 title-only / 2 app-only). Consumed by the independent medium-integrity broker, not the DLL/settings lifetime. |
| `notify_show_icon`, `notify_show_image` | 1, 1 | notification broker | Whether cards composite the source app icon and the image/thumbnail where Windows exposes one. |
| `notify_allowlist_mode`, `notify_app_filters` | 0, (empty) | notification broker | Comma-separated source-app filter; `allowlist_mode=1` shows only matches, otherwise it is a blocklist. Permission remains global. |
| `media_notify_enabled` | 0 | notification broker | Watches the OS-wide Now Playing (SMTC) session and mirrors track title/artist/artwork into the same card pipeline as desktop notifications when the track changes. No transport controls, no polling fallback. Independent of `notify_enabled`/Windows listener permission. |
| `iracing_enabled`, `iracing_lap_popup`, `iracing_spotter_glow`, `iracing_flag_border` | 0,0,0,0 | iRacing provider | Gates the optional background `IRSDKMemMapFileName` reader and generic lap/spotter/flag event consumers. Raw iRacing fields never enter the native renderer. UI simulations work without iRacing. |
| `iracing_spotter_width`, `iracing_spotter_strength`, `iracing_spotter_opacity`, `iracing_spotter_fade` | 0.12, 1.0, 0.65, 1.8 | racing renderer | Peripheral width (0.03–0.70), intensity multiplier (0.1–4.0), edge-alpha control (0.05–2.0) and inward falloff exponent. |
| `iracing_spotter_fade_in_ms`, `iracing_spotter_fade_out_ms` | 0, 0 | racing renderer | Optional activation/deactivation envelope durations in milliseconds; zero preserves immediate transitions. |
| `iracing_spotter_color` | 16729344 (`FF4500`) | racing renderer | Decimal `0xRRGGBB` colour for side glows. |
| `iracing_spotter_mode` | 0 | racing renderer | 0 Classic keeps the existing side cue and chosen colour. 1 Proximity colour uses its own 0–255 signal only when the nearest car behind is within two metres, drawing yellow through orange to red on both outer edges. `CarLeftRight` overlap replaces it with red `FF0000` on the confirmed side. Before overlap the data does not identify a side. The rear-closing cue has separate pressure detection, enable and theme. Published live in `iracingFlags` bit 8; spotter and rear themes use bits 9 and 10. |
| `iracing_flag_width`, `iracing_flag_opacity` | 0.018, 0.60 | racing renderer | Inner flag-border width as eye-min-dimension fraction and alpha. Flag colour comes from the generic prioritised state. |
| `iracing_lap_duration_ms` | 4500 | broker card queue | Lap result card lifetime, clamped to 1000–15000 ms. Independent of Windows-notification permission. |
| `iracing_fuel_warning`, `iracing_fuel_warning_threshold_pct` | 0, 10 | iRacing provider | Fires a transient card once per crossing below the threshold, from the SDK's optional `FuelLevelPct`. Absent on cars/tracks that don't report it — no warning fires in that case. Clears via hysteresis (1.5× the threshold) rather than the instant fuel ticks back above the raw cutoff. |
| `crop_experiment_mode` | — | — | **Removed 2026-07-11** (experimental crop-fix purge) — code and UI gone; key ignored. Root cause of the edge smear was VD fixed foveated encoding: `docs/FIXED_FOVEATION.md`. |
| `edge_smear_fix`, `edge_smear_pixels` | — | — | **Removed 2026-07-11** — edge-guard code deleted; keys ignored. |
| `lod_popin_fix` | 0 | — | **Removed** — code disabled; key ignored. |

Dead keys (removed 4.1.65, do not resurrect): `mask_vertical`/`mask_horizontal` as opening source,
`mask_rounded`, `mask_offset_y`, `mask_*_bias`, `mask_*_curve` as visor inputs, `reshade_hmd_menu`,
`reshade_desktop_duplicate`, `reshade_3d_menu`. Some still exist as read-fallbacks in the current
4.1.55-derived `LoadConfig`; treat them as legacy compat only.

UI-only keys: window size/column layout keys, `stencil_outer_edges_only` also gates preview mode +
inner-low slider enablement. ReShade Remote preferences live in this INI and are mirrored through the volatile
`Local\ReShadeXRControl` block. Only the ReShade quad transform remains in ProgramData
`openxr_quad_transform.ini`; its obsolete `[Window]` entries are migration fallback, not a second authority.

## 2026-07-10 implementation update

- MSI install/upgrade preserves the live `%LOCALAPPDATA%` configuration and per-app registry
  profiles. The packaged ini is a fresh-install template only; neither the installer nor the
  OpenXR layer resets user tuning during an ordinary upgrade.
- The bundled `xr-viewlab.ini` must include every product key with safe defaults, including
  `mask_enabled=0`, `mask_size=1.0`, `mask_corner=0.5`, `visor_hd=0`, and
  `visor_antialiasing=0`.
- `mask_size=1` is the compatibility default and preserves the previous full opening. Reducing it
  only masks more of the already-rendered image; it never changes crop tangents, submitted FOV,
  or recommended render resolution.
- `visor_hd` is now native: it doubles visor curve tessellation. `visor_antialiasing` is now
  native: it enables a per-vertex alpha feather strip on the visor aperture edge.
- Custom per-app visors carry `mask_enabled`, `visor_size`, `visor_width`, `visor_height`,
  `mask_corner`, `mask_outer_apex_y`, `mask_inner_lower_y`, `mask_nose_spread_x` and the retained
  compatibility bridge keys. `visor_size=0` means use the complete global visor configuration;
  saving that state deletes the custom-only visor keys rather than preserving a stale partial override.
- Missing `mask_size` falls back directly to `1.0` in both UI and DLL. Do not reintroduce
  legacy `mask_vertical`/`mask_horizontal` opening derivations as a fallback.
- `visibility_mask_visor` no longer changes runtime geometry. The Direct C visor is the product
  path; the old hidden-mesh reshaper cannot represent current shape/AA/HD behaviour.
