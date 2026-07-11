# Decision log

> Why things are the way they are. Append-only; add an entry when a decision becomes permanent.
> Format: decision, reasoning, alternatives rejected, consequences.

## D1 — Crop lives in `xrLocateViews` + recommended-size reduction (2026-07)
Real GPU savings require the game to render fewer pixels, so ViewLab crops the FOV tangents the
game sees AND reduces the recommended image size. Rejected: reporting full FOV while rendering
reduced size (the `lod_popin_fix` experiment) — projection/culling and submitted geometry
disagree, producing stretched output. Consequence: aggressive crops can cause LOD pop-in;
`lod_popin_fix` stays diagnostic-only.

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

## D10 — Edge AA = feathered edge strips, not supersampling (2026-07-10, Pass 3 design)
The mask interior is flat black; only the boundary aliases. Chosen: per-vertex coverage strip
along the boundary (~1–1.5 texels, alpha 1→0), single pass, continuous gradient, ~zero cost.
Rejected: 2× offscreen supersample (extra RT + composite for the same 4 samples/px) and the
historical 4-pass JitterCB approach (4.1.68 — worked, but 4× fill for 4 quantized coverage
levels; see `docs/history/dllmain_features_4.1.68.md`). `visor_hd` becomes double tessellation
(curvature facets), not resolution.

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
