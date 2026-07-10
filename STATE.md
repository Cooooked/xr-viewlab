# STATE — live project state

> Single source of truth for "where are we". Update this file in the same commit as any
> behavior change. Do not create handoff/status/session documents — this is the only one.

**Updated:** 2026-07-10
**Current version:** 4.1.105 — `F:\AI-Projects\ViewLab\dist\ViewLab-4.1.105.msi`
**Last confirmed-good in headset:** 4.1.103 (stencil inner-eye fix confirmed by user)
**Publish state:** local commits ahead of origin/master; DO NOT push until user confirms current work in-headset.

## Context: the 2026-07-10 recovery

A 12-hour spiral of blind edits (versions 4.1.88–4.1.101, unreleased) broke and then lost the
visor shape features. The native layer was reset to the clean v4.1.55 baseline and the features
are being **rewritten** (not restored) in passes. Full post-mortem: `docs/REGRESSIONS.md`.
Three fixes from the recovery are load-bearing and must survive all future work:
FreeLibrary-after-blob-use, stencil key wiring + filter-runs-with-visor-active, partner-eye
boundary gated to closed-bean mode (details in `docs/DECISIONS.md` D7–D9).

## Active milestone: visor feature rewrite (4 passes)

- **Pass 1 — DONE, built as 4.1.105, AWAITING HEADSET CONFIRMATION.**
  Apex Y / Inner low / Bridge / Rise / Peak X / Steepness rewritten natively, formula-identical
  to the preview; nose divot anchored to NDC bottom (old top-of-lens bug is structurally
  impossible now); visor shape follows the stencil checkbox (ON = open-inner arch, OFF = closed
  bean); geometric curve exponent everywhere; per-app registry reads for apex/inner-low/bridge-width.
  Headset checklist: divot bottom-inner only at 0.8 crop; nothing at top; apex-y bends outer
  curve; bridge sliders reshape divot; stencil toggle bean/arch after game restart.
- **Pass 2 — pending:** pin click-drag root fix in `BeanMaskEditor.cs` (`CaptureMouse()` on
  mouse-down + release on up/lost-capture; five threshold tweaks already failed — it's the input
  pipeline, not thresholds). Desktop-testable, no headset needed.
- **Pass 3 — pending:** edge quality ("8-bit look"). AA checkbox (`visor_antialiasing`) =
  single-pass feathered edge strip (vertex format gains a coverage float; boundary strip ~1–1.5
  texels, inner alpha 1 → outer alpha 0; SRC_ALPHA blend). HD checkbox (`visor_hd`) = double
  curve tessellation (96→192 segments). Supersampling was considered and rejected
  (`docs/DECISIONS.md` D10). Historical alternative: 4.1.68 used 4-pass JitterCB
  (`docs/history/dllmain_features_4.1.68.md`).
- **Pass 4 — pending, lowest priority:** re-add perf/robustness items lost with the 4.1.55
  reset: per-image/slice RTV caching; late `xrEndFrame` fallback draw (OpenComposite titles,
  e.g. DiRT); `mask_size` missing-key fallback 0.82 + dead legacy-key removal; verbose-log split
  (`ViewLab.verbose.log`); edge-smear FOV diagnostics (contract asserts parked in the test file).

## What works (as of 4.1.105, pending headset confirm where noted)

- Render crop (vertical total/split + horizontal, outer-edges-only option) — core feature, stable.
- Per-app enable + custom profiles (registry).
- Visor mask Technique C Direct (D3D11 draw into eye textures at `xrReleaseSwapchainImage`).
- "Stencil outer edges only": filters the runtime's FOV-stencil mesh to outer halves AND
  switches visor geometry bean/arch (confirmed in-headset at 4.1.103 for the filter part).
- Visor shape controls Size/Curve/Apex Y (+ Inner low/Bridge×4 pending headset confirm).
- ReShade Remote popout + bundled payload install.
- Update check, app list, responsive UI layouts.

## Known issues / not working

- **Pin click-drag in the preview canvas** — broken (Pass 2).
- **Visor edges look "8-bit"/blocky in-headset** — no AA in current native code (Pass 3).
- `visor_hd` / `visor_antialiasing` checkboxes exist in UI + ini but native does nothing with
  them yet (Pass 3 wires them).
- Edge smear at aggressive crop — unresolved long-standing investigation; diagnostics were lost
  with the 4.1.55 reset (Pass 4).
- Inner-low sliders are not disabled in closed-bean mode even though the divot only renders in
  open-inner mode (cosmetic UI gap).
- MSI install can overwrite/drop live ini keys (`docs/REGRESSIONS.md` R6) — re-check settings
  after install.

## Environment facts

- Test game: Pistol Whip, `D:\VR Games\Pistol Whip-working\Pistol Whip.exe`, via Virtual
  Desktop / VDXR, Quest 3. ReShade in that folder: Home = menu, PrintScreen = screenshot
  (screenshots land in `%USERPROFILE%\Documents\ReShade\Screenshots\`) — see `docs/VERIFICATION.md`.
- Games only query the visibility mask at session start — **restart the game** after toggling
  stencil settings.
- Layer registration: HKLM `SOFTWARE\Khronos\OpenXR\1\ApiLayers\Implicit` (x64) and
  `WOW6432Node` (Win32). A stray 32-bit entry in the 64-bit hive was removed 2026-07-10.
