# Config contract — every key, both sides

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

## Render crop (core perf feature)

| ini key | range/default | DLL global | per-app | Notes |
|---|---|---|---|---|
| `enabled` | 1 | `enabled` | `app_enabled`, `profile_enabled` | master switch |
| `total_render_height` | 0..1 / 0.18 | `totalTangent` | millis | legacy fallbacks `total_share`, `vertical_tangent` |
| `split_mode` + `top_tangent` / `bottom_tangent` | 0.09/0.09 | `topTangent`/`bottomTangent` | millis | split top/bottom crop |
| `horizontal_render_width` | 0..1 / 0.80 | `horizontalRenderWidth` | millis | Exact retained width. `0.8` keeps 80% of each eye's submitted horizontal span; with outer-edge-only policy the entire 20% is removed from the outer boundary while the inner boundary stays fixed. |
| `crop_outer_edges_only` | 1 | `cropOuterEdgesOnly` | — | **Permanently enabled** — config key ignored. Horizontal crop takes from outer edges only. |
| `foveated_center_compensation` | 0 | retired/false | dword | Retained only for compatibility; ignored and permanently off because pose compensation tilted asymmetric crops. |
| `visual_mask_only`, `horizontal_visual_mask_only` | 0 | same names | — | mask instead of crop (loses GPU savings); Edge Masks popup "Both" controls write these keys |
| `render_scale` | 0.1..3 / 1.0 | `renderScale` | ×1e6 dword | per-game supersampling |

## Visor mask

| ini key | range/default | DLL global | per-app | UI control |
|---|---|---|---|---|
| `mask_enabled` | 0 | `maskEnabled` | global-only (per-app enable deliberately ignored — DECISIONS D6) | Visor mask checkbox |
| `mask_size` | 0.1..1 / **1.0** | `visorSize` | `visor_size` millis | Uniform visor-opening scale. `1.0` preserves the existing full opening; smaller values hide an outer band without changing crop/FOV/resolution. |
| `mask_corner` | 0..1 | `visorCurve = 1 − maskCorner` | `mask_corner` millis | Curve slider (stored inverted). Near zero stays visually square through one continuous curve; Inner low remains active. |
| `mask_outer_apex_y` | −0.5..0.5 / 0 | `visorOuterApexY` | signed millis | Apex Y slider + red pin |
| `mask_inner_lower_y` | 0..0.666 / 0 | `visorInnerLowerY` | millis | Inner low slider + orange pin |
| `mask_nose_spread_x` | 0..0.5 / 0 | `visorNoseSpreadX` | millis | Nose Spread X. Moves the left-eye nose boundary left and the right-eye boundary right by the same normalized amount. Zero preserves prior geometry. Published live and supported by per-app visor overrides. |
| `mask_inner_bridge_width` | 0..1 / 0.5 | `visorInnerBridgeWidth` | millis | Legacy compatibility key; the main editor fixes the supported curve at 0.5. |
| `mask_inner_bridge_rise` | −0.5..1 / 0 | `visorInnerBridgeRise` | legacy millis plus extended marker encoding for per-app profiles | Legacy compatibility key; the main editor fixes the supported curve at 0. |
| `mask_inner_bridge_peak_x` | −1..2 / 0.5 | `visorInnerBridgePeakX` | legacy millis 0..1000; extended marker encoding for per-app profiles | Legacy compatibility key; the main editor fixes the supported curve at 0.5. |
| `mask_inner_bridge_steepness` | −1..2 / 0.5 | `visorInnerBridgeSteepness` | legacy millis plus extended marker encoding for per-app profiles | Legacy compatibility key; the main editor fixes the supported curve at 0.5. |
| `visor_live_revision` | monotonic timestamp | `liveVisorRevision` | — | Internal commit marker. The UI writes it last after global visor controls, allowing a safe live visor-only refresh at `xrEndFrame`. |
| `mask_width_scale` / `mask_height_scale` | 1.0 | `visorWidth`/`visorHeight` | `visor_width`/`visor_height` | Fixed at 1.0; the visor mask always fills the crop opening and only affects corners. |
| `visor_technique` | `c` | `visorTechnique` | — | a/b hidden; DirectWrite is the product path |
| `visor_hd` | 0 | — | — | **Removed** — code disabled; key ignored. |
| `visor_antialiasing` | 0 | — | — | **Removed** — code disabled; key ignored. |
| `preview_circle_guides` | 1 | UI-only | — | Preview calibration preference. `1` shows two overlapping true circles; `0` shows one binocular oval. Both use the same 85% width / 90% height periphery boundary and do not alter runtime. |
| `preview_per_eye_frames` | 0 | UI-only | — | Independent frame-guide preference. `0` shows one combined binocular outer frame; `1` shows two overlapping per-eye rectangles at the actual `2064:2208` eye aspect. Guide-only; crop and runtime are unchanged. |
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
| `hud_trace_enabled` | 0 | migration only | Legacy boolean read only when `hud_trace_visibility_mode` is absent; saves mirror mode != off for older builds. |
| `overlay_force_direct` | 0 | backend diagnostics | Automatic selection keeps projection-only applications on direct eye-texture rendering and demands Topmost only after a distinct application compositor layer appears. Set to 1 only for diagnostics. Topmost gets one stable allocation attempt per session; any failure latches direct fallback. |
| `topmost_visor_overlays` | — | — | Legacy experimental switch; ignored. Backend choice is automatic. |
| `hud_anchor_x`, `hud_anchor_y` | 0.04, 0.05 | `hudAnchorX`, `hudAnchorY` | Normalized position within the shared binocular overlap of the cropped views; live HUD X/Y sliders (full 0–1 range). |
| `hud_scale`, `hud_spacing`, `hud_opacity` | 1.0, 0.018, 0.70 | shared overlay/HUD layout | Whole-widget scale (0.15–3.0), normalized gap, and opacity. Rings, literal labels, unit-bearing values, spacing, and padding scale together. |
| `hud_safe_margin`, `hud_clamp_to_visible` | 0.025, 1 | HUD layout | Normalized safe margin and complete-bounds clamp against the binocular overlap region. The HUD uses the smaller current eye-region dimension then applies `hud_scale`, so crop/resolution changes retain its proportion. |
| `hud_update_ms` | 100 | HUD telemetry | Bounded CPU/GPU/widget-state refresh period (50–1000 ms). Fixed-ring OpenXR timing samples feed APP, VR, and graph channels per frame. |
| `hud_green_threshold`, `hud_red_threshold` | 75, 90 | migration only | Legacy general percentage values used only as fallback defaults before per-widget keys exist. |
| `telemetry_settings_version` | 1 | telemetry migration | Explicit catalogue/settings schema. Kept separate from the 208-byte v7 overlay live-state contract. |
| `hud_widget_{cpu,gpu,app,vr,cpu_peak,cpu_frequency,ram,commit,vram,sys,fps,frame_interval,network_ping,network_loss,network_jitter,network_status}_enabled` | CPU/GPU/SYS/VR on | widget registry | Independent widget enables. All network widgets default off. Disabled widgets are omitted; unavailable configured widgets remain dormant. |
| `hud_widget_{...}_order` | CPU,GPU,SYS,VR then catalogue | widget registry | Persisted order. Invalid/duplicate positions normalize to one occurrence of every widget. |
| `hud_widget_{...}_warning`, `hud_widget_{...}_critical` | widget-specific | widget registry | Independent warning and critical thresholds for every active widget. VR/frame interval use percent of the current frame budget; SYS is inverse remaining headroom; network widgets use their displayed units. Legacy SYS/network keys are read only as migration fallbacks. `0/0` disables an alarm where there is no honest universal default. |
| `hud_max_per_row` | 16 | migration only | Legacy field retained in the mapping/ini. Current HUD layout deliberately packs every enabled widget into one row; there is no row-limit control. |
| `hud_graph_mode` | 0 | `HudGraphMode` | 0 deviation ms, 1 absolute milliseconds, 2 FPS, 3 percentage of cadence-aware budget. Incompatible channels are not mixed. |
| `hud_graph_frame_interval`, `hud_graph_fps`, `hud_graph_budget_deviation`, `hud_graph_app_work`, `hud_graph_wait_duration`, `hud_graph_submit_duration`, `hud_graph_display_period` | 0,0,1,0,0,0,0 | graph channels | Independent bounded-history lines. Sources/units are defined in `PERFORMANCE_HUD_REDESIGN.md`; default remains one understandable deviation line. |
| `hud_trace_sensitivity_ms` | 2 | `hudTraceSensitivityMs` | Deviation mode vertical range (±ms); in absolute-ms mode it supplies a minimum scale before automatic budget scaling. |
| `hud_trace_x`, `hud_trace_y` | 0.05, 0.75 | graph layout | Live graph position within the shared binocular overlap. Legacy key names are retained for migration. |
| `hud_trace_scale`, `hud_trace_width`, `hud_trace_opacity` | 1.0, 0.42, 0.70 | shared overlay/graph layout | Live whole-graph scale (0.25–3), base width/shape fraction (0.1–1), and independent opacity. Width is bounded to the available render area after uniform scale. |
| `hud_trace_history` | 120 | graph history | Live sample count shown (30–600); storage is always a fixed 600-sample ring. |
| `performance_trace_recording` | 1 | native trace recorder | Retains a bounded one-hour ring of the same QPC samples used by the visor graph. Each session checkpoints to a unique `%LOCALAPPDATA%\XR ViewLab\PerformanceTraces\session-*.csv`; `latest.csv` remains a hard-link/copy compatibility alias. The DiagMonster Session Graph browser opens, compares and explicitly deletes retained sessions. Abrupt exit does not require a shutdown callback. |
| `performance_trace_marker_vk` | 119 (F8) | native trace marker | Windows virtual-key code for a rising-edge marker bind (UI offers F6–F12). Each press receives an exact QPC timestamp, numbered visor confirmation and post-session graph marker. Read at session startup. |
| `crosshair_offset_x`, `crosshair_offset_y` | 0, 0 | `crosshairOffsetX`, `crosshairOffsetY` | User calibration in normalized full-lens tangent coordinates. Applied to the lens-centre target before Lens Pinned clamping. |
| `hud_alarm_only` | 0 | `hudAlarmOnly` | Hides widgets unless their sustained state is critical/unstable or within the post-recovery hold. Enabled alarming widgets pack together; the graph remains independent. |
| `hud_alarm_hold_ms` | 1500 | `hudAlarmHoldMs` | How long a red indicator stays visible from the first non-critical input (0–10000 ms). The deadline is not refreshed by the latched red display state. |
| `hud_debug_values` | 0 | HUD telemetry | Development values: `hud_debug_cpu`, `hud_debug_gpu`, `hud_debug_app` percentages and `hud_debug_vr` milliseconds. Old `hud_debug_system` is accepted as APP fallback. Normal APP is begin-return→end-entry wall time / cadence-aware budget; VR is wait-return cadence relative to `predictedDisplayPeriod × detected multiple` (1–4). |
| `network_probe_target` | 1.1.1.1 | network worker | Numeric IPv4 target for optional network HUD probes. It describes that path only; it is not automatically a game server. Changes apply next OpenXR session. |
| `clock_widget_enabled`, `clock_session_timer_enabled` | 0, 1 | clock card | Clock visibility and the independent elapsed-session lane. With the timer disabled the card collapses to one lane. Both apply live. |
| `clock_24_hour`, `clock_widget_theme` | 1, 0 | clock presentation | Selects 24-hour versus 12-hour AM/PM text and Graphite/Paper/OLED/Amber/Mint theme (0-4). Applies live. |
| `clock_widget_x`, `clock_widget_y` | 0.50, 0.10 | clock layout | Widget centre in the shared binocular render-area frame (0–1). Bounds are clamped to the current shared overlap. |
| `clock_widget_scale`, `clock_widget_opacity` | 1.0, 0.82 | clock layout | Whole-widget angular scale (0.1-2.0) and card/text opacity (0.1-1.0). The slider and preview resize pin share this scale range. Applies live. |
| `sticky_note_count`, `sticky_note_{0..7}_{enabled,text,x,y,scale,opacity,theme}` | 1; note 0 defaults | sticky note collection | Up to eight independent square paper notes. Text is capped at 120 characters and four native lines; theme is Classic/Rose/Mint/Sky/Paper (0-4). The unindexed legacy note migrates to note 0 and remains mirrored for downgrade safety. |
| `hud_widget_<id>_symbol` | 0 | HUD widget presentation | Per-widget compact pictogram selection. `0` retains the literal catalogue label; changes publish live through the telemetry catalogue. |
| `notify_theme` | 0 | notification composition | Graphite/Light/OLED/Amber/Mint pre-composited card theme (0-4). Newly composed cards use the selected theme. |
| `overlay_{hud,trace,clock,sticky_note,crosshair,notifications}_toggle_vk` | 0 except sticky note 118 (F7) | shared overlay visibility | Optional rising-edge show/hide bind. `0` means None; the UI offers None and F6-F12. Bind changes publish live. |
| `sticky_note_toggle_vk` | 118 (F7) | migration only | Legacy sticky-note bind read only when `overlay_sticky_note_toggle_vk` is absent; the shared settings path mirrors it while existing settings migrate. |
| `obs_indicator_enabled` | 0 | OBS provider/native cue | Enables authenticated local obs-websocket `GetRecordStatus` polling in the broker and subtle red native visor corners only while recording is active. OBS process presence is never treated as recording. |
| `obs_websocket_url`, `obs_websocket_password` | `ws://127.0.0.1:4455`, empty | OBS provider | OBS WebSocket v5 endpoint and optional authentication password. The password is stored in the local user ini; restrict the endpoint to localhost. |
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
| `iracing_spotter_width`, `iracing_spotter_strength`, `iracing_spotter_opacity`, `iracing_spotter_fade` | 0.12, 1.0, 0.65, 1.8 | racing renderer | Peripheral width, intensity multiplier, maximum edge alpha and inward falloff exponent. |
| `iracing_spotter_color` | 16729344 (`FF4500`) | racing renderer | Decimal `0xRRGGBB` colour for side glows. |
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
inner-low slider enablement. ReShade Remote state lives in ProgramData (shared-memory block +
`openxr_quad_transform.ini`), not in this ini.

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
- Per-app profiles now carry all six shape keys: `mask_outer_apex_y`, `mask_inner_lower_y`,
  `mask_inner_bridge_width`, `mask_inner_bridge_rise`, `mask_inner_bridge_peak_x`, and
  `mask_inner_bridge_steepness`. Visor enable remains global-only and the UI deletes any stale
  per-app `mask_enabled` override when saving a custom profile.
- Missing `mask_size` falls back directly to `1.0` in both UI and DLL. Do not reintroduce
  legacy `mask_vertical`/`mask_horizontal` opening derivations as a fallback.
- `visibility_mask_visor` no longer changes runtime geometry. The Direct C visor is the product
  path; the old hidden-mesh reshaper cannot represent current shape/AA/HD behaviour.
