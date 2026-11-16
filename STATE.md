# STATE — live project state

17 November 2026 validation follow-up: ownership/void bug fixed in ViewLab 4.1.479 + matched VLXR 0.5.206 (installed). User confirms TV in void and smooth. Second 180-second capture had 181 moving samples; game mean 206.71fps (2s minimum 206.0), headset panel mean 59.89fps (minimum 57.5), zero submit failures. Browser diagnostic subsequently measured the current video as 3840x2160, 25fps, zero browser drops over 30s; therefore a 60fps panel update counter must not be represented as 60 unique content frames for that video. Exact 1080p60 content validation awaits user selecting a known 60fps source. Keep goal active; do not claim a perfect FPS lock. Fixed root cause is Devin transcript node 3139 explicitly setting id=1,in_game=true and persisting it, compounded by old runtime labelling the ViewLab TV RaceLab. Creation-only marker separates owner namespaces with no additional per-frame GPU work. Source recovery archives preserve the full current source plus pre-existing dirty patches.

- **17 November 2026: native-panel void ownership repair. Live 1920x1080 panel labelled RaceLab matched ViewLab TV size (2.36m x 1.3275m). Clearing its saved in_game route restored void drawing, confirmed by user. Devin transcript node 3139 proves Devin explicitly sent id=1,in_game=true; node 3142 acknowledged this persisted workaround, then later mistook the same RaceLab-labelled TV for a missing overlay. Installed runtime binary contains no ViewLab owner recognition despite newer dirty source having it. Added creation-only, thread-local ViewLabNativePanelCreationActive export; VLXR uses that explicit signal instead of guessing ownership from forwarding/end-frame hooks. Real RaceLab/OKB attribution remains separate. No new per-frame GPU work; capture and Panel FPS graph unchanged. Built and installed ViewLab 4.1.479 and matched VLXR 0.5.206 MSI/runtime/APK. Contracts, creation-scope and nested-owner fixtures pass. New live session inventory owner=ViewLab,in_game=false; actual runtime path/hash verified; headset reports panel updates at 60.0fps and game submits about 414/2s with zero failures. User confirms TV in void and smooth. Driving endurance capture in progress.**

- **17 November — native panels: swapchain starvation fix, built 4.1.475 (install pending iRacing close):** live session showed host pipeline healthy (cap/pub/con ~40-55 fps, fail=0) but headset displayed only ~4 fps — the break was in the OpenXR quad swapchain path, not capture. Two defects fixed in `NativePanels.inl`: (1) `xrWaitSwapchainImage` timeout=0 polls left the image ACQUIRED across frames while retrying — a held acquired image can starve the runtime's compositor; now a timeout releases the image immediately and re-acquires fresh next frame. (2) Swapchain was created without `XR_SWAPCHAIN_USAGE_SAMPLED_BIT`, which quad-layer textures need for runtime sampling. Added `sc`/`swt`/`saf`/`si` counters to the 2-second stats line to verify copy vs timeout rates next session. Upstream note: desktop only produces ~40-55 changed fps for the panel window (browser/DWM-side, cap≈con so nothing dropped in ViewLab). Contracts pass.

- **17 November — native panels: copy-storm fix + monitor retarget, built/installed 4.1.473:** live test of whole-monitor sources exposed a GPU copy storm — Phase B drained up to 8 desktop frames per wake and copied/cropped EVERY one (~hundreds of full-screen copies/s on busy outputs), and `AcquireNextFrame`/`ReleaseFrame` were called per iteration which also released the "newest" surface before it was cropped. Fixed: drain keeps only the newest acquired surface, crops once, then a single `ReleaseFrame` for the batch. Monitor panel was also pointed at the wrong display (`\\.\DISPLAY2` = primary/game monitor); correct secondary target is `\\.\DISPLAY1`. `native-panels.json` was truncated to 0 bytes by an ENOSPC write while C: was 100% full (Temp held 11.4 GB); reconstructed from conversation history — panels/profiles/bindings exact, Sources catalog reduced to 3 known entries (self-heals as windows are re-bound). 4.1.473 installed elevated; Monitor panel left disabled pending TV re-validation, then re-enable on `\\.\DISPLAY1`. C: still needs real cleanup — it hit 0 bytes free once already.

- **16 November — native panels: DXGI duplication capture + whole-monitor sources, built 4.1.471/4.1.472:** WGC capture replaced with DXGI Desktop Duplication (one `IDXGIOutputDuplication` per monitor/output, per-panel window-rect crop, QPC 60 fps pacing, change-driven so static content publishes nothing). Fixes the silent WGC stall that froze panels for minutes (same API caused OpenKneeboard freezes). 4.1.471 verified live: ~20 min iRacing drive, stream 206-208 fps, panel ~50-60 fps real frames, fail=0. New feature in 4.1.472: panels can capture a WHOLE MONITOR — UI source combo gains "Whole monitor — \\.\DISPLAYn" entries; `Panel.window` top bit = monitor source carrying the HMONITOR (`MonitorSourceTag` in NativePanelState.h), native crops the full output rect, no occlusion checks. Contracts pass; 4.1.472 MSI built, install pending (needs iRacing closed).

- **25 October — REVERTED: desktop-window-mirror correction attempt failed; do not resurrect this approach.** An opt-in `desktop_mirror_fix` was tried for the Assetto Corsa desktop-mirror stretch (CSP `VR_MIRRORING` `FILL_VIEW` stretches the cropped eye texture to the window aspect). Implementation: process-wide `IDXGISwapChain::Present`/`Present1` class-vtable patches (learned from dummy swapchains), per-present `EnumWindows` game-window detection, and a per-frame `xrEndFrame` staging copy of the submitted eye. **Result: mirror unchanged AND it broke the headset stream.** Patching `vt[22]` on a shared class vtable can stomp an unrelated entry point on swapchain classes whose vtable is shorter, `EnumWindows` ran on the present thread, and the per-frame copy+`Flush` added render-thread work. Installed DLLs were restored by forced MSI repair of 4.1.453 (verified original sizes/timestamps); all source, config keys, registry DWORDs and docs were reverted. Any future fix must NOT hook Present process-wide and must not add work to the game's present or submit path — investigate a separate presentation path (e.g. own swapchain/window or compositor-side correction) instead.

- **17 October — LFE minimum activation, installed4.1.451:** live iRacing threshold set to-30dBFS while sensitivity remains12. The control is relabelled Minimum activation. Source decouples gating from intensity so quiet audio is hidden but signals above the threshold retain their full sensitivity/palette intensity; fixture covers this. Peripheral fixtures, contracts and full WPF/x64/Win32/MSI payload validation pass. Elevated MSI exit0; installed executable and uninstall record verify4.1.451, live INI unchanged, iRacing absent.

- **17 October — live LFE tuning:** iRacing profile narrowed to10–125Hz after capture showed live content through ~122Hz (peak ~48Hz), sensitivity12, brightness2.25 and opacity.9. Moved mirror X from.108 to.16 and height from.133 to.085 at user request. Warm colours stay unchanged; user uses the existing maximum-Hz cutoff to remove unwanted bands. iRacing remains running.

- **17 October — LFE frequency-band smoothing, built4.1.450:** at the live narrow10–80Hz range, a4096-sample48kHz FFT supplies only about six true source bins. The old per-pixel max aggregation turned them into visible colour bands. Narrow spans now keep linear interpolation; wide spans still max aggregate compressed complete bins. Peripheral fixtures, contracts and full WPF/x64/Win32/MSI payload validation pass.4.1.450 is not installed because iRacing is running;4.1.449 carries an abandoned palette edit and must not be installed.

- **17 October — LFE reflection correction, installed 4.1.448:** mirrored peripheral mode puts spectrum coordinate 0 at both outer lens edges, with the existing live Minimum frequency slider selecting its actual Hz. The right copy flips; the left does not. The full horizontal FOV-union placement remains, so each haze reaches the actual outer lens boundary. Fixtures cover coordinate direction as well as asymmetric outer-edge placement; contracts and full WPF/x64/Win32/MSI payload validation passed. Elevated MSI install exit0; installed app and uninstall record both verify4.1.448, live INI hash unchanged, iRacing absent. Earlier4.1.447 incorrectly put coordinate0 at inner edges and is retained only as an invalid package. Headset validation remains pending.

- **17 October — first LFE headset feedback correction:** user reports4.1.442 too visible/orange at entry and little extra response during aggressive driving; also requires actual horizontal reflection on the left peripheral copy and no numeric Hz labels. Root cause: normalised logarithmic amplitude range compressed changes; UVs were identical and labels explicitly drawn. Changed to gated linear amplitude with existing sensitivity/noise-floor controls, reflected left-copy UVs, removed labels. Same capture/analysis pipeline and texture, no audio routing changes. Quiet/strong/gated transfer fixtures added. Built4.1.443: full WPF/x64/Win32/MSI build and payload validation passed; response fixtures and contracts passed. User subsequently reported simulator closed;4.1.443 installed successfully (MSI exit0), installed UI version verified4.1.443.0, live INI hash unchanged, settings UI reopened. No corrected-headset response claim yet. Preserved4.1.442 MSI and targeted source/settings recovery in backups/lfe-feedback-before-443.

- **17 October — assistance and LFE implementation:** user approved design 2 then removed TC. Exactly three independent symbols: real binary SDK ABS (same flag MAIRA uses), MAIRA v9 wheelspin/lock estimates. Live global/per-app controls, ordinary preview/position/scale/opacity/hotkeys, scoped temporary editing and three timed tests. Assistance MSI4.1.440 passed full build/payload validation. LFE uses actual selectable PCM capture (NAudio.Wasapi2.2.1), reused FFT/haze buffers and one256x64 texture shared by single/mirrored draws. CABLE-B Output48kHz capture opened and packets arrived; sampled interval was silent (alpha0), not an on-track energy validation. Fixtures cover ABI, independent sources, stale/invalid data, fades, silence, full-range bin aggregation and decay; WPF external-editor fixtures pass. Combined4.1.442 full build and MSI payload validation passed. User reported iRacing closed and explicitly requested installation: elevated MSI exit0, installed UI4.1.442.0, live INI hash preserved, installed UI reopened. No game launch/restart or routing changes. Headset visuals and VR performance pending. Recovery: backups/overlays-before-install-442; prior4.1.437 MSI retained.



- **Two overlay proposals — feasibility/design approval, 17 October 2026 (no implementation):** user requests one movable four-indicator driving-assistance group and a real-PCM LFE haze display with optional synchronized peripheral copies. Reuse OverlaySettingsCatalog, preview/coordinate resolver, global/per-app persistence and live state, broker presentation tests, native batched geometry/textured quads. Existing Performance HUD uses thin segmented rings, compact kHudFont glyphs, amber/red warnings and time-based fades. ABS `BrakeABSactive` is binary intervention; `BrakeABSCutPct` was documented as disk-only, not a live intensity source (official notes: https://support.iracing.com/support/solutions/articles/31000161130-2021-season-3-release-notes-2021-06-08-02- and https://support.iracing.com/support/solutions/articles/31000170128-2023-season-3-release-notes-2023-06-05-02-). No verified live TC-intervention source found; adjustment/toggle values must not masquerade as intervention. Installed/running MAIRA 2.1.7776 matches inspected source tag; read-only Local\\MAIRARefactoredTelemetry header reports v9. Its sound wheelspin/lock activity and volume fields are available, derived from learned per-gear longitudinal-speed/RPM ratios, not direct tyre measurements or audio analysis. Detection depends on MAIRA sound feature enablement/calibration; cannot establish front-wheel-only lock. Prefer direct version/capacity/tick-validated reading over duplicating estimates; suppress stale/unknown data. Source: https://github.com/mherbold/MarvinsAIRARefactored/tree/2.1.7776/MarvinsAIRARefactored/Components (Telemetry.cs, Sounds.cs, Simulator.cs). VoiceMeeter render and capture endpoints are installed; exact isolated tactile route remains unverified. WASAPI shared-mode render loopback or selected VoiceMeeter capture bus can supply actual PCM; exclusive/ASIO routes may require a dedicated exposed tap (https://learn.microsoft.com/en-us/windows/win32/coreaudio/loopback-recording). Proposed LFE path: worker capture/FFT/smoothing and reused small heatmap buffers, one bounded snapshot/texture shared by both draws, no render-thread FFT/audio waits or added GPU flush. Low-frequency resolution trades against analysis delay; performance is unmeasured. User requires symbol approval before graphics implementation. Spectrogram reference image is not attached. No simulator launch, settings change, build or install performed.



- **iRacing launcher mouse race (16 October, built4.1.438):** approved launch script submits pointer movement plus button down/up as one SendInput batch; user mouse movement previously slipped between separate calls and missed navigation/Launch. Existing runtime/headset, focus and layout guards remain. Native layout/coordinate/refusal tests and contracts pass. Build/payload validation passed with zero warnings/errors; MSI4.1.438 not installed. Live approved launch, advancing replay, valid144Hz capture and complete pinned-setting restoration verified; next launch/capture also succeeded. Evidence: TestResults/iracing-launcher-input-20261016.json. Physical simultaneous mouse movement was not instrumented, so no broader focus-race guarantee is claimed. Preserve installed4.1.437 and unrelated dirty changes.



- **Render % cache fix (15 October, built4.1.437):** external registry scale updates now hydrate the app row and open profile editor; dirty local Render % edits survive. Regression fixture covers115% adoption and unsaved120% preservation. Contracts and real WPF fixtures pass; MSI payload validated. Installed4.1.437 verified: MSI exit0, installed/published UI hashes match, live ini unchanged, app scale1.15 retained (TestResults/ss-profile-install-20261015.json). Existing IRacingTelemetryProvider nullable warning remains outside this fix. Performance testing stays paused.



> Single source of truth for "where are we". Keep this file SHORT (target < 150 lines): current version,

> unbuilt work, active queue, open issues. Update it in the same commit as any behaviour change. Completed

> history goes to `CHANGELOG.md`; the full pre-2026-09-26 journal is `docs/history/STATE-archive-2026-09-26.md`.



**Updated:** 2026-10-17



- **FIXED 4.1.436 — sticky note editor/headset position mismatch (2026-10-14):** root cause was precedence, not the crop: ProfileWindow.ApplyOverlayPreviewState let overlay_layout_sticky_N win for sticky previews while native (and every other feature preview) lets canonical sticky_note_N_x/y/scale win. A stale layout value (0.771/0.466) was shown in the editor while the headset drew canonical 0.84/0.54 (restored by the 4.1.424 overlay-sync repair), outside the crop band. Preview now resolves sticky notes like native. User intent (editor position) applied: sticky_note_0_x=0.771, y=0.466; verified visible by left-eye capture after relaunch on 4.1.436 (installed, hash match, contracts pass). Original record: with iRacing's vertical crop (total_render_height 160) the rendered band spans only about y 0.36–0.50 in sticky coordinates; the note at x=0.84 y=0.54 was drawn (agent stickyVisible=true, HD texture cached) but entirely outside the submitted image, so the user never saw it. Verified with left-eye calibration captures (classic style invisible at the same spot; centred HD note visible but clipped). Settings fix applied for iRacing: sticky_note_0_x=0.72, sticky_note_0_y=0.43 (fully visible). Code follow-up: clamp sticky notes to the visible crop like hud_clamp_to_visible. Not a performance cost (A/B 6.50 vs 6.55 ms).



- **Overlay flush batching (2026-10-14, built 4.1.435):** stack sampling on 4.1.434 showed ViewLab's remaining render-thread cost was mostly ID3D11DeviceContext::Flush (vtable 0x378) called at the end of DrawVisorBorderToTexture, DrawCalibrationOverlayToTexture and DrawViewLabOverlaysToTexture, per eye (up to 6 kernel submissions/frame, ~2.7-4.6% of iRacing's render thread). Those three now call OverlayFlush(); inside the swapchain-release eye loop (thread_local g_deferOverlayFlush) it defers to ONE Flush per released image, merged with the existing grade-only flush. All other callers (OBS mirror, capture copies, topmost layer) still flush directly. Hotkey polling measured negligible (GameWindowFocused ~0.16%), left unchanged. Contracts pass.



- **HUD/trace render-thread cost fix (2026-10-14, built + installed 4.1.434):** WPR stack sampling of iRacing's render thread (iRacing Performance Lab `reports/etw/attribute_stacks.py`) attributed ~9% of it to ViewLab; live agent A/B/A showed alarm-only HUD+trace cost ~0.45 ms/frame (app p50 6.50 → 6.05 → 6.52 ms, misses 23 → 1 → 6). Root cause: `DrawCalibrationOverlayToTexture` saved/restored full D3D state, allocated a 4096-vertex vector and called Flush() every frame per eye even at zero alpha, and drew one CB update + VB discard + Draw per distinct colour. Fix: `UpdateHudTraceVisibility()` runs first and the function returns before any D3D work when calibration, HUD and trace are all invisible; HUD/trace batches use new `kVertexColourPS` (SV_POSITION-first signature like kTexturedPS) for one upload + one draw. Calibration diagnostics keep the exact per-colour CB path. Contracts pass; MSI exit 0; installed DLL hash = build. Pending: in-headset visual check of HUD/trace colours on VDXR (old note says vertex colour came through black; suspected PS signature mismatch). Sticky note measured as not a cost (6.50 vs 6.55 ms). Also: the 4.1.431–433 "installed" Application-log events were build payload-validation extractions, not installs; 4.1.424 remained installed until 4.1.434. Not committed (tree holds other agents' uncommitted work).



- **Launcher ownership guard built (2026-10-14, 4.1.433):** script Close accepts optional ExpectedProcessId/ExpectedStartUtc and verifies them against the actual process immediately before graceful close. Lab rechecks ownership after load-ready waits and passes both identifiers, preventing a concurrent replacement session being closed. Contracts and full WPF/x64/Win32/MSI build plus payload validation passed. Package `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.433.msi`; no new package installed. Build completed before further benchmark measurements.



- **Concurrent desktop replay launching (2026-10-14, user-authorised):** launcher now forcibly reacquires iRacing UI focus immediately before each navigation click and search entry, with eight bounded immediate attempts. It does not idle-wait for desktop activity or blindly repeat a launch; an appearing simulator stops further input. User explicitly requested this behaviour while using the PC. Initial replay launch verified with SDK event 88580087, Spa Grand Prix, Ferrari 296 GT3 #12 cockpit, fresh tracked-head and submitted/sent frames with zero submission failures. Lab owner PID/start-time guards and settled-load wait protect subsequent reloads. Current lab requires one replay mirror, stable native 120 Hz first, then 144/180, with grouped profile screening before fine tuning. VDXR A/B later. `F:/AI-Projects/iRacing Performance Lab` owns bench evidence and settings recovery; existing dirty ViewLab source preserved.



- **Replay lab launcher (2026-10-14, validation in progress):** approved `Tools/DynLOD/Test-IracingNormalization.ps1 -Mode Launch -ReplayName GT3RaceSpa` now uses user-supplied Replays/search/Launch coordinates (157,918), (411,381), (1628,470). Replay source existence/hash, existing-session rejection, layout/focus guards and passive VLXR app/listener readiness precede launch. Replay requests reject Cycle/Close. Parser and no-click DryRun passed at 1920x1078. No live replay launch verified yet. User omitted recentering. Preserve all pre-existing dirty work; launcher backup `backups/replay-launch-before-20261014-065634`.



- **Shared external/profile overlay synchronisation fix (2026-10-12, built 4.1.424):** proven failure: registry/API reported enabled P10 text while opening the cached iRacing profile republished disabled Porsche text into the live sticky block. External reload now refreshes cached overlays and reconciles open editors; before publish/save, changed persisted keys win while unrelated unsaved edits survive. Cancel restores current saved values, not the opening snapshot. Effective native sticky acknowledgement now includes text/placement/enabled/visibility; `-Wait` rejects missing fields, mismatches and hidden notes. Real WPF modal editor and acknowledgement regression fixtures pass; full contracts, WPF/x64/Win32 build and MSI payload validation pass. Package: `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.424.msi`. User-authorized installation succeeded (MSI exit 0, 17:26 Brisbane). Installed app/uninstall record 4.1.424; app and x64/Win32 DLL hashes match the validated build. Reopened installed UI PID 26744 responds to agent status/get; live INI hash unchanged, registry differences only discovery last_pid/last_seen/last_seen_utc. Simulator was absent initially and subsequently appeared separately (PID 22176); loaded native status confirms matching enabled P10 text/position and open sticky visibility gate. This is effective-state verification, not headset pixels. No game launch/stop/restart or runtime/headset operation performed. Dirty source preserved. Installation recovery: `backups\overlay-sync-before-install-20261012-172619`. Readiness claim correction: status/get proved connectivity, not live control; never claim agent-ready from connectivity alone. Pre-change recovery: `backups\overlay-sync-before-20261012-171219`.



- **ViewLab agent connectivity recovered (2026-10-12):** installed 4.1.420 settings assembly lacks AgentControlService; its responsive UI therefore exposes no XRViewLabAgentControl pipe. Gracefully closed only that UI and started the existing agent-capable `bin\Release\net8.0-windows10.0.19041.0\xr-viewlab.exe` hidden (PID 24132). Read-only global status/get and current-profile get succeed; live ini SHA-256 unchanged; iRacing PID 22588 left running. Installed MSI/layer remain 4.1.420. Native agent acknowledgement is unavailable in the running layer; do not claim verified live application. This is a temporary local UI recovery, not an installed matched release; relaunching the installed shortcut loses agent connectivity. No source implementation, build, install, runtime registration or game changes.



- **Mandatory iRacing launch path (2026-10-12):** use `Tools\DynLOD\Test-IracingNormalization.ps1 -Mode Launch` through the iRacing UI Test Drive flow. Never launch the simulator executable directly or fall back to it. Read the script; check headset streaming and runtime readiness before launch through CLI, logs or other non-UI diagnostics; stop and report the specific blocker if readiness cannot be established. Never use computer use for iRacing launches. No visual confirmation or manual window/layout verification is required; let the approved script perform its own checks. Stop on missing prerequisites. `Cycle` is not a launch-only command. Rule is in repo and global Codex AGENTS.md and `docs/VERIFICATION.md`. The assistant bypassed the script after installing 4.1.420; user rejected that launch. 4.1.420 installed successfully; live ini hash unchanged; headset profile visuals remain unconfirmed.



- **Hotkeys gated to the game window (2026-10-12, not headset-tested):** `GameWindowFocused()` (foreground window owned by this process) gates `PollColourMenuKeys`, `PollColourCalibrationKeys`, `UpdateOverlayFeatureHotkeys` and the trace marker; opening the colour menu resets `selected` to the tab row. Cause: 2026-10-12 01:06 a desktop Ctrl shortcut opened the menu and saved iRacing `colour_grade_post_process=0` (ViewLab.log `colour menu: saved post=0`). Built 4.1.419 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.419.msi` (4.1.418 tree + this fix); contracts pass; not committed (tree still holds the uncommitted 4.1.418 work).



- **Colour profiles corrected (2026-10-12, D34):** slots 1–3 always available; 2/3 default neutral. PROFILES only on COLOUR; applies/stores only that tab’s controls, preserving gains, calibration levels and menu settings. Optional slots 4–6 retained. Built and installed 4.1.420 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.420.msi`; contracts and MSI payload validation pass. Recovery check confirms installed app + Windows uninstall record 4.1.420 and installer success (status 0, 03:14 Brisbane). Prior installation check recorded live ini hash unchanged. Headset visuals unconfirmed; recovery did not launch/restart a game or change live settings. Uncommitted alongside the existing work; no implementation correction remained.

- **SHADOW DEPTH colour control (2026-10-10, built 4.1.417 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.417.msi`, not headset-tested):** new `colour_grade_shadow_depth` (0–1000,

  1000 = vanilla), menu COLOUR tab after SHADOWS (menu now 153 font px tall for 11 entries) + WPF slider. Shader

  `AdjustShadowDepth` in `Grade()`, amount via `Levels.w` (hashed with levels → LUT re-bake, zero extra per-frame cost

  with the LUT engine). Needs in-headset tuning of the strength curve (max = half the shadow stops, 3-stop fade, 6-stop cap).



- **207 Hz buffering test, 2026-10-03 16:07:** one-frame buffering lowered latency ~1.4 ms but increased stale events (58 -> 88 over about 60 s). Rejected; restored two-frame buffering on the preserved October 1 runtime. No implementation changes. Encoder/delivery timing remains unresolved.



- **VLXR live recovery, 2026-10-03 16:02:** preserved October 1 PC runtime + `alvr.client.dev` is running Eleven at 207 Hz from `VirtualDesktop207Hz\backups-runtime\recovery-207-20261003-155958`. User reports it working. 60 s: headset 206-208 FPS, 58 stale events; game interval p99 5.02 ms; backend latency average 30.43 ms. VDXR remains the system default. Details/evidence in `VirtualDesktop207Hz\HANDOFF-CURRENT.md`. Older 0.4.6 success claims below are superseded by the regression and recovery.



- **VLXR 0.4.6 built for 144 Hz visual-quality validation (2026-10-02):** 0.4.4 PC remains installed, the latest Quest client is installed, VLXR is active, and Streamer is running. AV1 now has an explicit full-range BT.709 SDR contract; Quest no longer compresses decoded RGB into 16–235 or forces a second REC.709 mapping; retained frames keep their pose timestamp. Performance/settings now use a native Quest compositor quad outside ViewLab's crop, with false spacewarp/foveation rows removed and no hidden-panel border leakage. Direct USB sustained 144/144, and isolated H.264 at 2048x1536 per eye sustained 144/144 without freezing. Latest packages: `F:\AI-Projects\ViewLab\VirtualDesktop207Hz\dist\VLXR-0.4.6.msi` and `F:\AI-Projects\ViewLab\VirtualDesktop207Hz\dist\VLXR-0.4.6-headset.apk`.



- **Installed build checked 2026-09-29:** Windows uninstall record and installed

  `C:\Program Files\xr-viewlab\xr-viewlab.exe` both report 4.1.409. Live ini enables the DWM

  widget and its symbol (warning 25%, critical 40%). For the Eleven 60 Hz incident, the DWM

  reading at the time is not yet known.



- **DWM HUD widget (built 4.1.409, 2026-09-28):** new Performance HUD widget `DWM` (id 16, monitor pictogram) = dwm.exe's summed

  graphics/compute share of the render GPU, from the existing GPU PDH pass (`MetricId::DwmGpu`, `FindSessionDwmPid`). Warn 25 /

  crit 40 %. Found in Eleven at 144 Hz: DWM ~75 % (4K 144 Hz desktop + Twitch + mirrored cross-GPU monitor) vs ~21 % after

  fixing; game itself ~18 %. `TelemetryConfigBlock` v2 (576 B, 16 slots) → **v3 (720 B, 32 slots)**; UI and layer ship together.

- **Built for headset test:** 4.1.410 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.410.msi`. Proximity-colour spotter

  (D31): car 14 m → 5 m behind phases both glows yellow → deep orange (continuous); under 5 m only the

  `CarLeftRight` side shows, red, that side only. Fixes both glows lighting for a car on one side. 14/5 m thresholds

  need on-track confirmation.

- **Built for headset test:** 4.1.408 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.408.msi`. New rhythm shift light

  theme **Shift Hero** (Guitar Hero): `iracing_shift_light_theme` 0 lines / 1 Shift Hero (2 = legacy, read as bottom),

  live via `iracingFlags` bits 11–12. 4.1.408 (user request): one fused highway only, placed via

  `iracing_shift_light_placement` 0 bottom / 1 top / 2 left / 3 right (live bits 13–14); no per-eye edge pair. `ShiftHeroScorer` (IRacingCues.cs) judges full-throttle upshifts in the provider;

  racing state **v5 (96 bytes)** carries score/judge/combo/stats/judge tick (producer + native must ship together).

  Native draw is a lane-frame of flat quads (~20–35 small draws per eye at peak). Fixtures + contracts pass; headset visibility, text size

  and draw cost at 144 Hz still need the user's test. Test button plays a nine-note demo run.

- **4.1.406:** Help panels now describe the current clock, iRacing cue and test behaviour. Proximity colour remains independent inside two metres; the three-panel button row is fixed to 3×2.



- **Pending headset test:** native rendering now defaults to direct eye-texture presentation even when an

  existing installed config lacks `overlay_force_direct`. iRacing presentation tests now run while telemetry is

  connected and override the corresponding live cue until cleared. Offline iRacing fixtures pass, including

  live test/clear/restore; headset visibility still needs the user's test after the next MSI.



- **Recent fixes:** proximity colour now uses the same peripheral spotter geometry, independent

  of rear-closing presentation; both theme selectors publish live. Timed race-start, proximity, rear-closing

  and shift-light tests exercise full cue progressions; racing cards omit the generic app caption. Headset

  validation remains necessary after the next MSI.



## Version



- **Installed:** 4.1.443 (MSI exit0, installed UI version verified; live INI preserved). Corrected LFE headset response/performance remain pending.

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



- `Tests/Verify-PerformanceHud.ps1` line 58 still pins live mapping `_view.Write(4, 16u)`; `LiveStateService` is v19, so the

  script stops there (pre-existing, found 2026-09-28; `Verify-ViewLabContracts.ps1` passes). Update the pin in its own change.



- **EAC "Untrusted system file"** for the unsigned layer DLL. User has permanently declined code signing.

- **Late direct-fallback draw** (`xrEndFrame`, "direct-fallback") writes projection images after the app released

  them, before the runtime receives xrEndFrame. Outside the OpenXR ownership rules but no observed failure; left in

  place deliberately because removing it could drop the visor/overlays where the release-path draw does not run.

- **Contract suite CRLF bug (pre-existing):** the xrLocateViews hook assertion (~line 1172 of

  `Verify-ViewLabContracts.ps1`) contains literal CR/LF characters inside its regex, so it fails on a CRLF checkout

  (`core.autocrlf=true`) regardless of code. Everything else passes (verified 2026-09-28 with that pattern normalised).

- **R50** is cited by contracts (`Verify-ViewLabContracts.ps1`) but has no entry in `docs/REGRESSIONS.md`.

- **DiagMon** `Process` handle leak and `StartTime`-in-comparator exception storm (opt-in capture path only).

- Six windows have no help affordance (ProfileWindow, PerformanceTrace*, DiagMon*): needs written help content.

- Split-crop value semantics changed in 4.1.253 (½-lens scaling); old configs may need ×2 (no migration).

- AMD stereo-submission research (`Tools/StereoProbe`, `ViewLabBridge/Stereo*`) is parked by the user.



- **2026-10-03 overlay/capture:** compact lower PC overlay accepted by user; opaque left-eye capture verified. Checkpoint VirtualDesktop207Hz/known-good/overlay-compact-20261003. Next settings menu/Streamer ownership/presets/crop sizing. Full-resolution bitmap work remains on render thread; move to existing worker path before further ports to avoid stalls.



- **2026-10-03 latest:** black stats/diagnostics and quality/sharpening dropdown text built into VLXR0.5.5; portable Streamer live. Pacing p99 restored in controlled backend. Eleven launched with Godlike dimensions: verified stream3072x3200 at207Hz target, actual~103FPS and encode9-12ms. Not stable at Godlike207. Prior2048 checkpoint preserved. MSI VirtualDesktop207Hz/dist/VLXR-0.5.5.msi + unchanged matched October1 headset APK.



- **2026-10-03 VLXR0.5.7:** corrected graphics preset wiring. Runtime from isolated6954c6c reads render_quality independently of encoded size (3072x3216 Godlike fullFOV; ViewLab crops afterward), sharing portable profile with Streamer. System registered dist/VLXR-0.5.7-runtime; current game intentionally not restarted. Black dropdown templates user confirmed. Contracts pass; MSI/APK built; new-runtime headset validation pending. Automatic stream sizing for all presets still pending.



- **2026-10-12 ~04:20 registration regression:** VLXR 0.5.56 deployment helper recreated HKLM OpenXR/1 with New-Item -Force, deleting its 64-bit ApiLayers branch. Current simulator had no fresh ViewLab log; app/DLL remained installed at 4.1.420. Repaired via installed ViewLab --set-layer-enabled helper (exit 0); x64 manifest DWORD 0 verified. Installer helper changed to create the key only if absent. User requested simulator close; approved script closed it gracefully; left closed. Other erased x64 layer registrations have not been reconstructed without evidence of their prior state. Foveation remains enabled; headset visuals unconfirmed.



- **Agent-control delivery 4.1.422 (2026-10-12):** packaged existing local agent settings service and native acknowledgement after discovering installed 4.1.420 lacked the service. Contracts pass; corrected a malformed multiline hook-check regex without changing its intended assertion. Full build and MSI payload validation pass. Package: F:\AI-Projects\ViewLab\dist\ViewLab-4.1.422.msi. Installed successfully (elevated MSI exit 0, 05:53 Brisbane). Installed app version 4.1.422, PID 22048 from Program Files; status -Target global and settings get both return ok=true. Live ini SHA-256 unchanged. Simulator closed by user; no live in-game adjustment tested. Previous installation/config/profile recovery: backups\agent-control-before-install-20261012-055215. Existing dirty source retained; no game launched.



## Practice incident flash — 2026-10-12

SDK PlayerIncidents report byte 2 plus penalty byte 0x0200 and a rising PlayerCarMyIncidentCount produces a single 650 ms amber border. A missing counter uses a report edge; initial/reconnect samples establish a baseline. Ongoing report 3, zero-x and other incidents do not trigger. Safety flags and pit-limiter warning take precedence over practice feedback. Replay/missing driving evidence disables it. Uses existing border and provider worker, no added render work. Independent visibility key iracing_flag_show_off_track_award (default on) requires existing master iRacing/border enable. Source: https://github.com/kutu/pyirsdk/blob/master/irsdk.py (IncidentFlags), https://irsdk-node.bengsfort.dev/Common-Patterns/. Deterministic fixtures are not a captured live 1x or headset validation.



## Location coaching milestone — 2026-10-12

PracticeCues.cs validates bounded version-1 JSON (exact SDK track/layout/car strings, provenance, at most 512 sections; positions LapDistPct [0,1), forward end within quarter lap, lead metres 0–2000 OR seconds 0–20, duration 0.25–30 seconds). Practice pack editor/import under iRacing persists managed practice-pack.json. No advice/coordinates shipped. iracing_coaching defaults off; iracing_practice_file points to managed JSON. Separate iracing_coaching_fade_in_ms (0–2000, default 150) and iracing_coaching_fade_out_ms (0–3000, default 150) sliders use existing overlay interaction/reset style; broker polls settings, provider publishes fixed 304-byte Local\XRViewLabPracticeCue v1, native smooth envelope uses uptime independently of duration/lead. Once per passage; wrap rearms, jumps/tow/replay/pits/stale/mismatch suppress. Fresh heartbeat required (750 ms); no renderer imports/track search. Pixel sticky renderer reused with one bounded additional card; existing sticky settings untouched. Fixtures and compile are development evidence, not headset validation. Documentation will be expanded with reference/name extensions in following isolated milestones.



## Exact section names and native fade — 2026-10-12

Independent iracing_section_names toggle (default off) composes imported section.Name with optional coaching in the compact sticky cue, using the same approach/end/duration/passages. Practice JSON now also requires positive numeric TrackId and CarId; matching requires both IDs AND all exact strings. Empty TrackConfigName is allowed and must match exactly (observed track 249 has empty SDK configuration string). No verified Nordschleife section-position map is available: unsupported/missing maps are explicit in provider diagnostics; no coordinates or coaching invented. Native practice wrapping is cached on text change instead of allocated per frame. PracticeCueEnvelope.h fixture covers fade start/midpoints/end and stale heartbeat. Repeated identical note after a fade restarts its independent envelope correctly.



## Garage 61 reference gear — 2026-10-12

iracing_reference_gear (off), iracing_reference_gear_lead_m (0–500 m, default 100), iracing_reference_file (binding JSON) activate independent upcoming reference gears in the compact sticky card. Actual Garage 61 CSV headers Speed/LapDistPct/Gear/Brake/Throttle/PositionType parsed invariantly. Binding requires version 1, verified positive TrackId/CarId, exact SDK track/layout/car strings, source provenance, explicit SpeedUnit (m/s, km/h, mph), sibling CSV filename and SHA256. Full forward on-track coverage >=100 samples, first <=.002, last >=.998, maximum gap .002, valid pedals/gears/speed, bounded 50 MB/250000 samples. UI imports JSON+CSV into LocalAppData/XR ViewLab/References/<hash>; no API required. Missing/mismatch/session identity change blocks guidance, never fallback. Reference gear is floor sample at upcoming distance, never claimed optimal. Worker binary-search/interpolation, renderer gets compact text only. config/practice-reference contains user-selected Toni Mustang/Nordschleife 6:23.831 CSV copied from supplied Documents file (original preserved) and explicit verified binding, available to import; no live setting changed. SI units cross-check: 23030 samples at 60 Hz = 383.833 s vs supplied 383.831 lap; integrated speed ~20683.94 m. This is empirical unit verification, not metadata inside CSV. Official Garage61 OpenAPI https://garage61.net/api/openapi/v1.json confirms PositionType 3=on track but does not declare speed units. Exact-match/wrong-track/wrong-layout/wrong-car/unverified/import/interpolation fixtures use real supplied data. Blank SDK layout parser fixed to preserve empty value; driver identity reads only the DriverInfo list entry (not radio CarIdx). Native headset behavior unvalidated.



## Reference speed card — 2026-10-12

iracing_reference_speed defaults off. Independent fixed-map speed sticky card: CURRENT/REF km/h at same LapDistPct, MATCH green +/-3 km/h; FAST amber >3 to <10, red >=10; SLOW blue below -3. Relative reference comparison, not safe-speed/optimal-speed advice. Own horizontal/vertical position iracing_reference_speed_x/y [0,1] defaults .78/.62, scale .1–2.5 default .8; saved/live via broker. Background binary lookup/interpolation and capped 150 ms publication; fixed 304-byte Local/XRViewLabReferenceSpeed same contract as practice, heartbeat 750 ms. Native fixed text storage/wrapping performs no per-frame allocation or CSV IO. Exact binding plus position guard hides speed/gear on mismatch, pits/replay, stale session time, jump/tow, missing track length and first baseline; no other reference fallback. PositionType!=3 is retained as ineligible, zero gear during shifts suppresses gear hints. CSV pedal epsilon 1e-6 accepts float roundoff only, clamps to [0,1]. Toni binding+CSV included in MSI; import selects reference.json in installed app directory and copies immutable hash-bound pair to managed user References folder. Independent toggles remain off; no live settings or installed binaries changed. Supplied target tested, neutral and pit sample semantics covered.



- **17 October — LFE outer-lens positioning correction:** live scoped snapshot confirms X=.108, width=.22 reaches native correctly. Last recorded Quest left FOV -54/+40 and right -40/+54: shared INTERSECTION clamps the old left footprint to -40..-25.169deg (centre -33.205), excluding14deg of outer lens. Use horizontal UNION: footprint -54..-37.624deg (centre -47.032), symmetric on right. Mirrored copy draws only in its corresponding eye to prevent nasal-edge duplicates; single overlay retains shared angular placement with natural clipping. No new settings or render resources. Added exact asymmetric-FOV/edge/symmetry/eye-ownership fixtures.4.1.443 package retained; targeted recovery backups/lfe-position-before-444. 4.1.444 native compile failed because the namespaced helper was included inside the anonymous namespace; moved include to global scope. Native edge/asymmetry/eye-ownership fixtures and contracts pass. Replacement4.1.445 build pending; actual headset verdict pending.

## 20 October 2026 — colour profile tab memory

Fixed the headset COLOUR profile selector losing edits on switching. Before selection, the outgoing tab is saved; delayed/close saves also persist its ten COLOUR controls under the current game's indexed colour_preset_<slot>_* registry values. Per-game saved values override global INI templates at load and live template publication. Existing current colours without a selected slot belong to tab 1. Gains, calibration and menu placement remain shared and are not stored in the slots. The executable production-function fixture Tests/Verify-ColourProfileMemory.ps1 checks rapid switching, all three tabs, restart, template refresh, migration and calibration isolation. Live headset validation remains pending; the running simulator is left intact.

20 October 2026: colour profile memory fix built as 4.1.452. Production-function fixture and repository contracts passed; full WPF/x64/Win32 build and MSI payload hash validation passed. Package F:\AI-Projects\ViewLab\dist\ViewLab-4.1.452.msi. Installation started only after the simulator had exited independently. User's pre-switch colours recovered from 01:00:39 log: contrast635, saturation645, brightness/exposure500, sunglasses0; full record in work/colour-before-profile-switch-20261020.json (evidence only, not restored). Existing settings backed up under backups/colour-profile-memory-before-4.1.452. Source remains alongside pre-existing dirty work; do not commit unrelated changes.
Installation complete: MSI exit 0; installed app version4.1.452 and x64/Win32 DLL hashes verified. Live INI unchanged. No game launched. Headset validation pending next session.
20 October 01:10 Brisbane: user requested recovered pre-switch colours restored to profile 3. Saved all ten COLOUR fields to slot3 global template and iRacing per-slot override plus active game colours, selected slot3; readback verified contrast635/saturation645, others from recovered log. Gains/calibration unchanged. Simulator absent; next launch loads restored colours. Pre-write INI/registry preserved in work/colour-before-profile3-restore.*.
