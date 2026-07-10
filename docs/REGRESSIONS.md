# Regression memory

> Institutional scar tissue. Read before touching the areas named here. Append an entry whenever
> a significant regression occurs: what / why / how detected / fix / how to never repeat it.

## R1 — FreeLibrary-before-blob-use crash (4.1.56–4.1.88 era; fixed 4.1.89, re-fixed 4.1.100)
**What:** Pistol Whip (and any game without d3dcompiler_47 resident) crashed 0xC0000005 at launch.
**Why:** `InitD3D11MaskRenderer` called `FreeLibrary(dxcLib)` before the compiled shader blobs
were consumed by `CreateVertexShader`/`CreatePixelShader`/`CreateInputLayout`.
**Detected:** game crash immediately at startup, only with the layer enabled.
**Never again:** free after last blob use + all error paths (D7). The contract test does not pin
this — verify manually when touching `InitD3D11MaskRenderer`.

## R2 — Nose divot rendered on TOP of the lens (4.1.63–4.1.101; fixed by rewrite 4.1.105)
**What:** inner-low "divot" appeared at the top of both lenses, wrong curve, barely visible.
**Why:** native code anchored the bezier to `y1` copying the UI's screen-coords formula — but
NDC y is up, so `y1` is the TOP. Plus the old `BuildNoseBridgeCurve` emitted bare points into a
TRIANGLELIST (garbage geometry).
**Never again:** divot anchors to NDC `y0`, every vertex band-clamped (structurally bottom-only);
the y-flip lives only in `ApexYFromConfigNdc` (D5). Contract test pins both.

## R3 — "Stencil outer edges only" wired to nothing (long-standing; fixed 4.1.102)
**What:** checkbox had zero effect; inner eye edges stayed stenciled.
**Why (two bugs):** UI wrote `stencil_outer_edges_only`, DLL read `outer_edge_visibility_mask_only`
— a silent key mismatch; AND the filter was skipped whenever the visor was active, so VD's FOV
stencil passed through unfiltered and the game stenciled all edges itself (D8).
**Never again:** contract test asserts the DLL reads the UI's key and that UI/DLL constants match.
When adding any key: grep BOTH sides before shipping.

## R4 — mask_size fallback 1.0 = invisible mask ("no mask for 3 days", 4.1.46 & again 4.1.65)
**What:** visor silently disappeared; drawing pipeline was fine.
**Why:** missing `mask_size` (wiped from ini) fell back to a formula that clamps to 1.0 = full
opening = zero-width border. Regressed twice via legacy `mask_vertical/horizontal` derivation.
**Never again:** fallback is 0.82 in UI and DLL; opening never derived from legacy keys (D3).
Contract tests pin the UI's direct `0.82` fallback so installer-stripped configs cannot recreate
the invisible visor on the next save.

## R5 — Untested "current settings as defaults" broke launch (4.1.88; reverted 4.1.89)
**What:** hardcoding the user's live settings as app defaults shipped `mask_size=1` (R4 again)
and wrong crop values; Pistol Whip broke immediately; the build was pushed to GitHub untested.
**Never again:** never publish untested builds (agents.md rule 2). Defaults changes are
high-risk — they interact with the MSI ini overwrite (R6).

## R6 — MSI install wipes/drops live ini keys (standing hazard)
**What:** installing an MSI overwrote `%LOCALAPPDATA%`-adjacent config; keys not present in the
bundled default ini (e.g. `mask_enabled`, `mask_size`, `mask_corner`) were dropped, silently
changing behavior after "just an update".
**Never again:** keep the bundled `xr-viewlab.ini` complete (every product key present with sane
default), include it in the MSI/package output, and make the reset policy explicit. Current
policy is backup-then-reset visor settings, not preserve-on-upgrade. Contract tests assert the
default key set, Product.wxs packaging, and the installer reset list.

## R7 — The 2026-07-10 spiral: 14 blind builds, features lost (4.1.88→4.1.101)
**What:** 12+ hours of guess-edit-build cycles on the inner-low/stencil symptoms; five failed
"fixes" for pin dragging alone; multiple full-file reverts (4.1.88 → 4.1.89 → 4.1.55) that threw
away working features (apex-y, inner-low, bridge, jitter AA, 4.1.64/65 perf work) while the
actual bugs (R2, R3) sat unread in the code.
**Why:** patching symptoms without reading the failing pipeline end-to-end; trusting stale
assumptions over source; version churn instead of root-cause isolation; changelog entries
claiming "tests pass" when the contract test was failing.
**How it ended:** reading `xrGetVisibilityMaskKHR`, the geometry builders, and the UI key
constants once, carefully — both root causes were visible in the code the whole time.
**Never again:** prove root cause before patching (agents.md rule 6); never claim test results
without running the test; when two consecutive fixes fail, stop editing and read the whole
subsystem; reverts are for known-good states, not exploration.

## R8 — Pin click-drag: five threshold tweaks, zero fixes (open, Pass 2)
**What:** preview pins can't be dragged; repeated "fixes" adjusted hit thresholds/Focusable.
**Why (diagnosed, unverified):** `OnMouseLeftButtonDown` never calls `CaptureMouse()`, so the
drag dies as soon as the cursor leaves the small canvas; thresholds were never the problem.
**Lesson:** input bugs are pipeline bugs (capture, routing, hit-testing) before they are
geometry bugs.

**2026-07-10 fix:** `BeanMaskEditor` now captures mouse only after a pin hit, releases on
mouse-up, and clears drag state on lost capture. Contract test pins the input pipeline.

**2026-07-10 correction:** capture alone was not enough. `ShapeChanged` saved the old slider
values, and `SaveGlobalSettings()` immediately rebuilt the editor from those stale sliders,
snapping the drag back. The host windows now sync editor values back to sliders before saving,
and the editor uses preview mouse events plus full-rectangle hit testing.

**2026-07-10 correction 2:** the apex pin drag inverse also used `pins.y0` instead of the
pin band centre. That made the centred pin map to `0.5` and clamped negative drags away. The
drag code now uses the same centre-origin formula as `PinPositions`; contract tests pin it.

## R9 - Intentional visor reset must not become silent key loss (2026-07-10)
**What:** MSI install/upgrade resets visor settings to safe defaults by design, but this can look
like the old accidental dropped-key bug if docs/tests are stale.
**Why:** `Installer\PreserveConfig.vbs` strips visor keys after backing up the live ini. That is
the chosen policy for now; crop/render settings are preserved.
**Never again:** bundled `xr-viewlab.ini` carries every product key with safe defaults, installer
reset strips the complete current visor key set, and the contract test pins both lists.
