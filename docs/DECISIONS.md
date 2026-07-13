# Decision log

## D21 — Hardware telemetry is snapshot-only at the render boundary

All Windows, PDH, DXGI and future vendor sensor work belongs to a bounded telemetry provider worker.
OpenXR/D3D hooks may only attempt a non-blocking copy of its completed fixed snapshot. Catalogue live
settings use `XRViewLabTelemetryConfigV1`; the 208-byte v7 general overlay mapping is not enlarged.
Advanced vendor sensors remain absent until licensing and failure isolation are reviewed; missing
data is never represented as zero.

## D20 — Performance timings are named by observable hook boundaries

APP is wall time from `xrBeginFrame` return to `xrEndFrame` entry, not exact CPU execution. Wait and
submit graph channels are durations inside their respective runtime calls. Adapter 3D utilisation is
not GPU frame time, which remains deferred until ViewLab has a correctly scoped non-blocking D3D
timestamp-query pipeline. Cadence derives from the active `predictedDisplayPeriod`, never a list of
familiar millisecond values.

> Why things are the way they are. Append-only; add an entry when a decision becomes permanent.
> Format: decision, reasoning, alternatives rejected, consequences.

## D1 — Crop lives in `xrLocateViews` + recommended-size reduction (2026-07; updated 2026-07-11)
Real GPU savings require the game to render fewer pixels, so ViewLab crops the FOV tangents the
game sees AND reduces the recommended image size. Rejected: reporting full FOV while rendering
reduced size (the `lod_popin_fix` experiment) — projection/culling and submitted geometry
disagree, producing stretched output. Consequence: aggressive crops can cause LOD pop-in.
`lod_popin_fix` and `edge_smear_fix` were removed from UI and code in 4.1.122; they were never
productized and only produced noise. Both config keys are ignored.

## D2 — Technique C Direct is the product visor path (2026-07, v4.1.61+)
Draw the mask directly into the game's eye texture at `xrReleaseSwapchainImage` (last legal write
point while the app owns the image). Rejected as product paths: A (head-locked quad layer —
works, VDXR-compatible, but composites over everything incl. runtime UI) and B (swapchain
interception — fragile across formats/MSAA). A/B code stays in-tree, hidden, for reference.

## D3 — `mask_size` is the single source of truth for the visor opening (4.1.65)
Never derive the opening from legacy `mask_vertical`/`mask_horizontal` — their missing-key
fallback computes 1.0 = full opening = invisible mask (the "no mask for 3 days" bug, R4).
Missing-key fallback is 0.82 in BOTH UI and DLL.

## D4 — Visor enable is global-only (4.1.6x)
Per-app registry `mask_enabled` is deliberately ignored; stale per-app 0s silently overrode the
global toggle. Per-app SHAPE values are still honored.

## D5 — Geometry parity: `BeanMaskEditor.cs` is the spec (2026-07-10)
Preview and native visor must be formula-identical (same curve exponent, same apex math, same
bezier), with the UI-screen-y ↔ NDC-y flip in exactly one native helper (`ApexYFromConfigNdc`).
Two past regressions (apex inversion 4.1.66, divot-on-top R2) came from ad-hoc per-site flips.

## D6 — Curve slider maps geometrically: exp = 32·(2/32)^curve (4.1.67, re-adopted 2026-07-10)
Linear mapping wastes the slider's midrange (barely-changing flat shapes); 4.1.66's linear-to-7
cap lost the lemon extreme entirely. Geometric puts the old "looks maxed" point at 0.5 and keeps
exp=2 (full lemon) reachable at 1.0.

## D7 — FreeLibrary(d3dcompiler) only after ALL shader blob use (4.1.89 / 2026-07-10)
`D3DCompile` blobs point into memory that dies with the DLL; freeing before
`CreateVertexShader`/`CreatePixelShader`/`CreateInputLayout` crashes any game without
d3dcompiler_47 already resident (Pistol Whip). Free after the last blob use + on every error path.

## D8 — The stencil filter runs even when the visor is active (4.1.102 / 2026-07-10)
Old code skipped `xrGetVisibilityMaskKHR` filtering whenever the native visor was on ("avoid
double boundaries") — wrong: the GAME stencils whatever the mask returns, so Virtual Desktop's
FOV stencil blacked the inner/top/bottom edges with VD's shape regardless of the visor. Only the
legacy reshaper stays gated on visor-off. Also: the checkbox writes `stencil_outer_edges_only`;
the DLL must read that key (it read a different name for months — R3).

## D9 — Partner-eye boundary only in closed-bean mode (4.1.103)
`BuildProjectedPartnerVisorVerts` blacks the inner band the cropped partner eye can't render
(anti binocular-rivalry at aggressive horizontal crop). In open-inner ("stencil outer edges
only") mode that's exactly the inner-edge stenciling the user is turning OFF — so it draws only
when the checkbox is unchecked. Both visor geometry and this boundary key off the same flag.

## D10 — Visor edge AA and HD were removed (2026-07-11)
The previous feathered-edge anti-aliasing implementation broke the visor mask in practice and
the HD checkbox (2× tessellation) had no visible benefit. Both checkboxes and their code paths
were removed; `visor_hd` and `visor_antialiasing` config keys are ignored. The visor is drawn
with opaque geometry at the default tessellation level.

## D11 — Repository is the canonical memory (2026-07-10)
`agents.md` = operating manual, `STATE.md` = live state, `docs/` = deep knowledge, updated in
the same commit as the change. Tool-specific files (`CLAUDE.md`) are thin pointers, never
independent memory. No handoff/session/scratchpad documents. Root-level doc sprawl from
earlier eras was consolidated 2026-07-10 (27 root .md → 4 + docs/).

## Legacy (ReShade Remote subsystem, pre-2026-07)
Gameplay mode is launch-time config, not a live toggle; shared-memory `XRControlBlock` layout is
frozen (both sides must match); commands are edge-triggered (rising edge) to prevent repeat
execution; desktop and HMD render paths stay separated. Details: `ReShadePayload/Docs/`.

## D12 - MSI upgrades preserve user settings (revised 2026-07-11)
The packaged ini supplies safe defaults for a fresh install. Ordinary upgrades do not strip the
live `%LOCALAPPDATA%\XR ViewLab\xr-viewlab.ini` or per-app HKCU overrides. A changing MSI-version
reset marker was rejected because it made every update a factory reset, and running that cleanup
from the native OpenXR layer also put file/registry mutation inside a game's startup path.

## D13 — Stencil/crop outer edges stay on; foveated pose compensation is retired (revised 2026-07-13)
`stencil_outer_edges_only`/`outer_edge_visibility_mask_only` and `crop_outer_edges_only` remain
hardcoded on and their config keys are ignored. `foveated_center_compensation` is retired and fixed off:
its asymmetric-crop implementation rotated `XrView.pose.orientation`, tilting/folding the game world.
Overlay or crop code may read game poses but must never mutate them. Consequence: `crop_outer_edges_only`
always applies the horizontal crop from the outer edges, preserving the inner edge for the nose/bridge.
`stencil_outer_edges_only` keeps the open-inner (single-eye) shape in the visibility mask and preview.

## D14 — Treat prescriptive prompts as intent and constraints (2026-07-13)
Repository evidence and known-good behaviour outrank generated architectural prescriptions. When a prompt
dictates maths or structure, first verify that design against the current implementation and headset-proven
contracts. Preserve what works, infer the narrowest change from symptoms, and reject instructions that would
violate established invariants. Prompts provide intent and constraints; they are not unquestionable architecture.

## D15 — Windows notifications use a packaged medium-integrity broker (2026-07-13)

Keep the established MSI/OpenXR layout and grant only `ViewLab.NotificationBroker.exe` a signed
package-with-external-location identity plus `userNotificationListener` capability. The broker owns
global consent, collection, composition and the existing shared-memory card queue independently of
the settings window and game. The ordinary WPF settings process runs asInvoker; machine-wide layer
changes elevate through a narrow command only when registration changes. Rejected: an AUMID-only
shortcut (does not grant listener capability), permanent elevation, collection in the game, and a
full MSIX migration solely for this API.

## D16 — Topmost is automatic presentation policy, not an ordinary feature (2026-07-13)

Once every visor feature used the common scene renderer, the repaired composition-layer backend
became the default session policy. The first frame stays on direct rendering while Topmost arms;
direct drawing is then suppressed to prevent duplicates. Any failure permanently selects direct for
that session. Keep `overlay_force_direct` as an advanced profile/diagnostic escape, never as a normal
checkbox. The one-attempt/device-loss safety latch is a release invariant.
