# Hardware telemetry validation

## Automated before delivery

- Run `Tests\Verify-ViewLabContracts.ps1` and `Tests\Verify-PerformanceHud.ps1`.
- Build Release WPF, x64 layer, Win32 layer and MSI with `build.ps1`.
- Confirm the render-side update calls only `TryGetSnapshot`; PDH/DXGI/system sampling remains in
  `HardwareTelemetry.cpp`.
- Confirm the v7 208-byte live mapping and new versioned telemetry mapping both match native layout.

## Manual provider matrix

1. Native-only Windows system: verify CPU, PEAK, CLK, RAM, CMT and SYS become available.
2. AMD, NVIDIA and Intel GPU systems: verify GPU and VRAM select the game's adapter LUID.
3. Multi-GPU: run on each adapter and compare Task Manager GPU-engine identity and DXGI budget.
4. No usable GPU counter/DXGI 1.4: GPU/VRAM show unavailable, never 0%; CPU widgets continue.
5. Suspend a provider or induce stale data: stale widgets become unavailable while visor/crop and
   frame submission continue.
6. Close the game repeatedly: worker shutdown must be prompt with no lingering process, driver reset
   or repeated unexpected-shutdown event.

## Widget and layout matrix

- Toggle each of the twelve widgets alone and in mixed orders; disabled items leave no gaps.
- Restore defaults and verify CPU/GPU/SYS/VR in that order; APP remains optional.
- Enable all widgets at minimum scale and test maximum-per-row values two through eight.
- Verify SYS warning/critical use low-headroom semantics and sustained entry/recovery.
- Verify unavailable configured widgets remain selected after restart and hardware changes.
- Verify both eyes show the same value and state for every widget.
- Verify existing graph modes reject incompatible units; frame interval, FPS and application timing
  remain in their established unit-safe modes. Hardware history channels are a documented follow-up.

## VR regression sequence

With headset streaming, test Pistol Whip via VDXR, then Dirt Rally 2 via OpenComposite. Confirm crop,
visor, calibration, crosshair, notifications and standard/Topmost paths are unchanged. Exercise
session creation/destruction at least ten times. Do not automate these launches: if the runtime
reports `XR_ERROR_FORM_FACTOR_UNAVAILABLE`, stop because the headset is not streaming.

Measure worker overhead from the snapshot diagnostic (`workerCpuMs / elapsed wall time`) during a
five-minute session and compare frame-time traces with HUD off/on. Check for render-thread stalls in
a profiler: no PDH, DXGI or memory API should appear beneath an OpenXR/D3D draw hook.

Baseline smoke result on the development PC (2026-07-13): 20 samples over 5 seconds, six valid SYS
inputs, 15.625 ms worker CPU time, or 0.3125% of one logical CPU over wall time. This is a native-only
collector measurement, not a substitute for the five-minute in-headset frame-time comparison.
