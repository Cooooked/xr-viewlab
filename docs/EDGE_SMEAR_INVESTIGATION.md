# Edge-smear investigation (crop-boundary distortion) — CLOSED 2026-07-11

> **Status: root cause identified and confirmed by Virtual Desktop support staff.** See
> [FIXED_FOVEATION.md](FIXED_FOVEATION.md) for the plain-language summary and go-forward plan.
> This file is the full investigation record (formerly `BUGFIX.md`). Append corrections to §10
> (Journal) — do not rewrite history, strike it through and date the correction. Restructured
> 2026-07-11 from `Downloads\bugfix.txt` (chat transcript) plus findings not captured there.

---

## 1. Current problem statement (after all corrections)

ViewLab narrows the rendered FOV (tangent-space, per-edge) via its `xrLocateViews` hook and scales
the recommended swapchain size. The game renders a narrow forward rectangle; in the headset it
appears at its correct angular position surrounded by black. The crop works and saves GPU.

**The defect:** a **variable-width distorted/smeared band located INSIDE the valid rendered
rectangle, adjacent to its visible outer perimeter.** The black stays black — nothing bleeds
outward into it. Band severity/size changes when crop values change, but the relationship is
irregular and unmeasured. Left/right (outer) boundaries are worst; top/bottom also affected.

**Reported to occur regardless of game and regardless of runtime** (user statement 2026-07-11).
This eliminates all VD-only and game-specific mechanisms as *primary* cause.

Boundaries under investigation (only these are visible/inspectable):
- Left eye's outer-left boundary.
- Right eye's outer-right boundary.
- Top and bottom boundaries (both eyes, identical crop).
- The inner (nasal) edges extend into the binocular overlap, are not visible as a seam, and are
  **not** a diagnostic target.

---

## 2. Corrections timeline (how the symptom model evolved)

| # | Date | Correction | Consequence |
|---|---|---|---|
| 1 | 07-11 | Initial brief: "boundaries smear or repeat pixels", assumed possibly stretched to fill lenses | First analysis blamed VD clamp-filling the whole uncovered FOV |
| 2 | 07-11 | Image is a normal rectangle **already surrounded by black**; smear is local at its edges | Killed "fills entire uncovered FOV"; black is post-submission |
| 3 | 07-11 | Stereo model: one continuous perceived rectangle; only outer edges cropped; no visible inner seam; **binocular-rivalry claim withdrawn** (no evidence) | H≫V severity needs a pixel-level account |
| 4 | 07-11 | **Smear does NOT extend into the black. The band is INSIDE the valid rectangle** next to the perimeter. Also: logs verify submission *metadata*, not pixel *contents* — no source capture exists yet | Killed every "outward bleed / texel repeated into black" formulation; reframed to interior-resampling mechanisms |
| 5 | 07-11 | Happens **regardless of game or runtime** | Killed VDXR precompositor, VD encoder, Unity PP as primary; leaves runtime-universal mechanisms |

---

## 3. Verified facts (logs + local source)

Evidence classes: **[V]** verified (log/source), **[I]** inference from source, **[U]** user observation.

### 3.1 Session data (Pistol Whip, VDXR 1.0.10, Quest 3, 2026-07-11)

From `C:\Users\strif\AppData\Local\XR ViewLab\Logs\ViewLab.log` / `ViewLab.verbose.log`:

- **[V]** Config: `horizontal_render_width=0.23` (outer-edge-only), `total_render_height=0.202`,
  visor disabled (`maskEnabled=0`), `split_mode=0`, `pitch_offset=0.00000` (no pose modification).
- **[V]** Per-eye tangents (left eye; right eye mirrored):

  | Edge | Original tan | Cropped tan |
  |---|---|---|
  | outer (left) | 1.376 | 0.317 |
  | inner (right) | 0.839 | 0.839 (untouched) |
  | up | 0.966 | 0.195 |
  | down | 1.428 | 0.288 |

- **[V]** ~~Recommended size 3072×3264 → 2364×666~~ **CORRECTED 07-11 (late):** 2364×666 belongs
  to the 18:11 session (h=0.539 → factor 0.7695). The 18:58–19:11 probe sessions ran h=0.23 →
  factor 0.615 → recommendation **1889×659**, and the game created **1890×660** (Unity aligned up;
  recommendations are non-binding). ArraySize 2, samples 1, format 29 (`R8G8B8A8_UNORM_SRGB`).
- **[V]** Submitted `imageRect` = **full texture on all four sides** in every logged session
  (2364×666 at 18:11; 1890×660 at 18:58–19:11); in bounds.
- **[V]** Submitted FOV ≡ cropped locate FOV to 6 decimals, both eyes, display-time-correlated
  (`crop-contract submit … delta=(0,0,0,0) matches=(cropped 1)`).
- **[V]** Exactly one projection layer, two views (`layerCount=1`) — no extra quads/layers.
- **[V]** VDXR's hidden-area visibility mask is a **single triangle** (3 indices); ViewLab's filter
  left 3/0 per eye. Game-side stencil black is negligible; all visibility-mask experiments were
  operating on an almost-empty mesh and could never have changed anything.
- **[V]** The coloured-edge probe (mode=4) **executed** in three sessions (19:03:49, 19:05:30,
  19:10:38 — "edge-source probe executing at xrReleaseSwapchainImage eye texture" + "visor: draw
  executed (2 eye view(s))"). A full mode sweep 0→1→2→3→4 was run 19:03–19:11.
- **[V]** The currently installed layer DLL (`C:\Program Files\xr-viewlab\XR_APILAYER_cooooked_xrviewlab.dll`,
  timestamped 19:17 07-11) **postdates every logged game session**. Re-verify hash per observation.
- **[V]** Pistol Whip requested `XR_KHR_composition_layer_depth` (extension list in log).
- ~~**[I]** …Pistol Whip almost certainly submits depth…~~ **REFUTED 07-11 (late):** Unity
  `Player.log` states **"Depth Submission Mode: None"** — no depth is submitted; the extension
  request comes from the MetaXRFeature blanket extension string. Additionally VDXR ignores
  submitted depth unless `quirk_use_depth` is set (session.cpp:526; registry key absent here).
  Depth-based positional reprojection is **ruled out** for these sessions.

### 3.2 Key structural facts

- **[V]** ViewLab never modifies the `xrEndFrame` submission (hook is diagnostics + visor/probe
  draw only). Locate-FOV crop + recommended-size scale is the whole intervention.
- **[V]** `imageRect` touches the physical texture boundary on all four sides ⇒ **no guard pixels
  exist anywhere in the pipeline**; any downstream clamped tap reproduces game texels.
- **[V]** Width sizing uses the `(1+h)/2` estimate (0.615 at h=0.23) vs tangent-exact 0.522 —
  swapchain ~18% wider than exact. Density drift only; benign; pixels become anisotropic
  (~1.48:1 H:V density), which screen-space game effects would see as elliptical kernels.
- **NOT verified:** the submitted **pixel contents**. Logs prove metadata only. No pre-submission
  texture capture has ever been taken. (Correction 4 evidentiary point.)

### 3.3 Stereo geometry (corrected model, §2 #3)

- Per-eye texture coords: full slice per eye; nothing binocular at this layer.
- Angular coverage (tan, signed): left eye [−0.317, +0.839], right eye [−0.839, +0.317].
- Binocular overlap: tan ∈ [−0.317, +0.317] at h=0.23.
- Geometric note (no perceptual claim): when `h·tan(outer) < tan(inner)` (threshold h≈0.61 on
  Quest 3), the eyes' coverages diverge at the horizontal flanks. Perceptual relevance unknown.
- Visible outer perimeter = left eye outer-left, right eye outer-right, shared top/bottom.

---

## 4. External reference findings

### 4.1 OpenXR specification ([rendering.adoc](https://github.com/KhronosGroup/OpenXR-Docs/blob/main/specification/sources/chapters/rendering.adoc))

- Apps **may** submit a projection view "which has a different view or FOV than that from
  xrLocateViews. In this case, the runtime will map the view and FOV to the system display
  appropriately." → cropped-FOV submission is legal; pixels must land at correct angles.
- The spec says **nothing** about display regions a layer doesn't cover, and nothing about
  boundary treatment during reprojection. Both are runtime-defined.
- "the compositor **may bleed in pixels from outside the bounds** in some cases" (imageRect note).
- `fovMutable`: one-sentence capability flag; no error semantics.
- Asymmetric per-eye frusta are the normal case; no pose compensation is required for them.

### 4.2 Comparison implementations

| Project | Approach | Relevance |
|---|---|---|
| [mledour/OpenXR-Layer-crop-fov](https://github.com/mledour/OpenXR-Layer-crop-fov) (MIT) | Identical architecture: tangent narrowing in xrLocateViews, size = average of edge factors, endFrame untouched (`computeCroppedImageRect` exists but unused) | Same design reportedly yields pixel-exact black bars for its users — the architecture itself is viable on at least some stacks; its tangent→rect math is reusable (MIT) |
| [fommil/openxr-widescreen](https://github.com/fommil/openxr-widescreen) (Apache-2.0) | Symmetric vertical trim, gated on fovMutable | Same family, simpler |
| [mbucchia fov_modifier](https://github.com/mbucchia/_ARCHIVE_XR_APILAYER_NOVENDOR_fov_modifier) (archived → OpenXR Toolkit) | Multiplies raw angles in xrLocateViews only | Cruder than ViewLab |
| [VDXR](https://github.com/mbucchia/VirtualDesktop-OpenXR) (MIT, open source) | frame.cpp converts submitted fov verbatim to `ovrLayerEyeFov` tangents; depth chain → `ovrLayerType_EyeFovDepth` when `m_shouldUseDepth` (frame.cpp ~698–770); optional precompositor FSR-EASU/CAS pass with **linear clamp sampler** when VD sharpening/upscaling on (precompositor.cpp:61-250) | VD-path specifics — now secondary given cross-runtime reports |
| Oculus Debug Tool "FOV-Tangent Multiplier" | Same feature class on native LibOVR: users report black borders when reducing FOV ([Meta forum](https://communityforums.atmeta.com/t5/Tool-Feedback/Debug-Tool-What-is-the-new-FOV-Tangent-Multiplier/td-p/561715)) | Consumer precedent that reduced-FOV layers are a supported pattern on the layer contract VD emulates |

VDXR issue tracker: no hits for fov/edge/smear (search 2026-07-11; issues may be restricted).

### 4.3 Dead ends checked

- Pistol Whip folder screenshots (2026-06-25) are ReShade ASCII-shader captures — unusable.
- Pistol Whip stack: Unity IL2CPP, `UnityOpenXR` pre-init, LIV bridge present. PP stack unknown.
- Khronos registry blocks direct fetches; spec sourced from OpenXR-Docs repo.

---

## 5. Ruled out / withdrawn (do not re-run, do not re-assert)

**Ruled out by experiment (post-stale-DLL):**
- Full-size-swapchain submission (no change).
- Visibility-mask passthrough / empty / crop-aware transform (no change — and see §3.1: the mask
  is one triangle, so these tests had almost nothing to act on).
- Visor involvement (smear present with visor off).
- Visor tessellation.

**Ruled out by audit:**
- Crop tangent math, recommended-size rounding, imageRect/array-slice/off-by-one errors
  (all exact in logs). Sizing estimate error = density drift only.

**Withdrawn claims (with date):**
- 07-11: "VD clamp-fills the entire uncovered FOV" (correction 2: black is local and stays black).
- 07-11: "Binocular rivalry explains H≫V" (correction 3: no supporting evidence; geometry-only
  inference over-weighted).
- 07-11: Every "outward bleed" formulation — texel repetition into black, game→black encoder
  ringing as primary, stretched coloured border (correction 4: band is INSIDE the rectangle).
- 07-11: "Submitted contents are verifiably clean" → corrected to "submission metadata verified;
  pixel contents unverified pending capture."
- 07-11: VDXR precompositor / VD encoder / Unity PP as *primary* (correction 5: cross-game,
  cross-runtime). They remain possible *contributors* on specific stacks.

**Deployment history:** an earlier MSI updated the app but left a stale native DLL installed,
invalidating early comparisons. Every future headset observation requires a packaged-vs-installed
SHA256 match (extend `Tests/Verify-ViewLabContracts.ps1`).

---

## 6. Current hypothesis ranking (post-correction 5)

Cap 70% until an experiment directly observes the stage that creates the band.

1. **Universal compositor boundary resampling at an interior layer edge — ~50%.**
   Every runtime reprojects/timewarps the projection layer per display frame and has *some*
   boundary treatment where source data ends (clamp / stretch / feather over interior pixels).
   Normally that band sits at the lens periphery, invisible. ViewLab's crop relocates the layer
   boundary into central vision, exposing it. Runtime-independent, game-independent,
   crop-dependent, plausibly motion-dependent ("irregular" width). Depth submission (§3.1) adds
   positional-reprojection stretch on stacks that use it.
   *Test:* interior calibration ruler (§7), static vs motion, on two runtimes.
2. **ViewLab-transform ↔ render mismatch visible near edges — ~15%.**
   The only other runtime-independent stage. Submitted numbers are exact, but rendered pixel
   contents are uncaptured; a render-vs-submit frustum discrepancy shows as displacement growing
   linearly toward an edge. *Test:* ruler line spacing (uniform proportional displacement ⇒ this).
3. **Game/engine post-processing under cropped asymmetric projection — ~10%** (as contributor;
   cross-game universality argues against primary). Mip-chain clamp (bloom), TAA history clamp,
   anisotropic pixel density. *Test:* pre-submission capture; ruler stays straight while game
   content bands.
4. **Per-stack amplifiers — ~15% combined:** VDXR precompositor clamp kernels (VD sharpening),
   encoder mosquito noise confined inside the image, SSW. Real but stack-specific.
5. **Coordinate/rounding — ~2%.** Audit exhausted.
6. Residual for unenumerated mechanisms.

**H≫V severity, current best account:** head motion in these titles is yaw-dominant; horizontal
sampling excursions and horizontal motion vectors hit the vertical (left/right) boundaries
hardest. (Replaces the withdrawn rivalry claim.)

---

## 7. Planned diagnostics (designed, NOT yet implemented)

### 7.1 Interior calibration ruler (supersedes single-border probe)

Extend the mode-4 draw at `xrReleaseSwapchainImage` (after game+PP, before runtime):

- Per visible outer edge: 1–2 px lines at **1 / 8 / 16 / 32 / 64 px** inward — white, red, green,
  blue, yellow — with perpendicular tick marks every 128 px and a corner orientation block.
  Distinct alpha markers per line (probe shader already supports this) for programmatic ID.
- **Why it decides:** the ruler is painted after all game rendering. Ruler lines warped in-headset
  ⇒ post-submission resampling. Ruler straight while surrounding game content bands ⇒ the band is
  already in the submitted pixels.

### 7.2 Pre-submission texture capture (closes the §3.2 evidence gap)

Same hook: staged copy → `SaveDDSTextureToFile`/PNG every N seconds to a scratch folder.
First-ever direct observation of submitted pixel contents. Three-way compare per run:
**capture ↔ VD PC mirror ↔ headset** — first appearance of the band localises the stage.

### 7.3 Run matrix (static scene; head rested for static cells)

| Run | H crop | V crop |
|---|---|---|
| 1 | ~1.0 | ~1.0 |
| 2 | 0.6 | 0.6 |
| 3 | 0.23 | 0.202 |
| 4 (H-only) | 0.23 | ~1.0 |
| 5 (V-only) | ~1.0 | 0.202 |

Per run, per edge, record: band width in px (against the known ruler distances); which lines
affected; deformation type (stretched / duplicated / blurred / compressed / displaced); repeat
under slow yaw and slow pitch. Derive the scaling law: fixed px (kernel) / ∝ texture size (PP) /
∝ crop (transform mismatch) / ≈ fixed angular (reprojection) / motion-dependent (reprojection,
TAA, SSW).

### 7.4 Interpretation table

| Observation | Localisation |
|---|---|
| Band present in capture file | Source-side (game/engine) — ruler will be clean in it |
| Capture clean; band in PC mirror; ruler warped | PC-side composite (VD compositor or VDXR precompositor; split by toggling VD sharpening/SGSR) |
| Capture + mirror clean; band in headset only | Encode/decode or headset-side reprojection; static-vs-motion splits those |
| Band breathes with motion; gone when truly static (SSW off) | Reprojection confirmed (H1) |
| All ruler lines displaced by linearly growing offset | Transform mismatch (H2); measured scale identifies the disagreeing tangent |
| Identical band character on two different runtimes | Universal boundary treatment (H1); per-runtime tuning cannot fix it |

Deployment self-verifies (ruler visible & correctly spaced, or DLL is stale) + SHA256 per run.

### 7.5 Cheap zero-code side tests

- VD settings sweep, one at a time: sharpening→0, SGSR/upscaling off, SSW off.
- Same crop on a second runtime (SteamVR) with the same ruler build — the cross-runtime cell.

---

## 8. Candidate fixes — HELD IN RESERVE (conditional; none selected)

| If experiments show | Then |
|---|---|
| Post-submission, PC-side, band = boundary clamp of edge texels | Black guard band inside the rect (2/8/32 px configurable; periphery is product-intentionally black). Escalate to full-FOV composite if band unbounded under motion |
| Post-submission, universal across runtimes (H1 confirmed) | Architectural: stop putting a layer boundary in central vision — runtime-facing full-FOV composite swapchain (design retained below), or guard pixels + inset imageRect |
| Post-submission, absent from PC mirror | Encoder domain: feathered dark border (contrast reduction), bitrate/codec guidance. Composite swapchain will NOT help |
| Source-side (in capture) | Engine PP under cropped projection; mitigation: render full rect, submit **inset imageRect + tangent-matched inset FOV** so real rendered guard pixels exist outside the claimed rect (requires xrEndFrame view cloning) |
| VD sharpening implicated | Per-profile guidance + guard band |

**Retained design — runtime-facing composite swapchain** (from 07-11 report; unproven, do not
build yet): game keeps cropped locate-FOV + small swapchain (savings preserved); layer owns a
shadow swapchain at original recommended size, cleared black; per frame copy the app sub-image
into the tangent-derived rect `x0=W(T_L−t_L)/(T_L+T_R)` … (mledour MIT math); submit cloned views
with ORIGINAL fov+pose, full-texture rect; strip depth chain; disable pitch compensation in this
mode. Risks: format/sRGB match, MSAA resolve, array slices, sync, SSW loss, installer gate.

---

## 9. Standing rules

1. Verify installed-DLL SHA256 against the build before interpreting any headset observation.
2. One variable per test run. Static scene for static cells.
3. Log lines are proof of metadata, not pixels.
4. Preserve crop, per-app profiles, visor, ReShade Remote in any change.
5. No MSI until an experiment branch is chosen.
6. Append everything new to §10.

---

## 9b. Evidence-repair pass (2026-07-11, late) — verified environment & retractions

New **[V]** facts from `C:\ProgramData\Virtual Desktop\OpenXR.log` (VDXR's own log),
`StreamerSettings.json`, Unity `Player.log`, registry, and VD screenshot captures:

| Item | Status |
|---|---|
| VDXR runtime | v1.0.10 (051774f3), Streamer 1.34.18, OVR 1.64.0, D3D11 on RX 7800 XT |
| VDXR recommended res | 3072×3264, "1.000 supersampling, 1.000 upscaling" → **EASU upscaling INACTIVE** |
| VDXR CAS sharpening | **INACTIVE** — `m_sharpenFactor = getSetting("sharpen").value_or(0)`; `HKLM\SOFTWARE\Virtual Desktop, Inc.\OpenXR` key does not exist ⇒ 0 ⇒ the entire VDXR precompositor was inactive; VDXR path = slice resolve/commit + direct `ovrLayerEyeFov` submission ("Creating a swapchain with texture array") |
| Depth | Unity "Depth Submission Mode: None" + VDXR default ignores depth ⇒ **no depth reprojection** |
| VD streaming settings (PC-side JSON) | `Sharpening: 1.0` (VD's own video sharpening, applied in the proprietary path — location PC-vs-headset unverified), `UseFovStencil: false`, codec H.264+, AutoAdjustBitrate false. **SSW state unknown** (stored headset-side) |
| Game engine | Unity **2021.3.7f1**, OpenXR plugin 1.4.2 (also OculusXR + PxrPlatform subsystems present; UnityOpenXR active), IL2CPP, LIV bridge |
| Runtimes ever logged | **All 384 ViewLab sessions on record are VirtualDesktopXR 1.0.10.** The "happens regardless of runtime" report has **no local log corroboration** — no non-VD session exists in any available log |
| "≈3844 px width" | Appears in **no local log**. Closest real figure: VD screenshots are 3840×2160. Treat 3844 as unverified/external |
| Direct pixel evidence | **First found:** VD headset-side captures in `Pictures\VR Screenshots` (07-11 15:13–15:16, 17:47). The 17:47 capture = session 17:46:41 (h=0.539, v=0.204, **visor ON**, curve 1.0). Shows: black is clean, boundary sharp, **no outward bleed** (consistent with corrected model); interior vertical strip patterns near the left boundary are visible but not separable from game wall texture + H.264/JPEG chroma banding at this quality. Because the visor was on, the visible boundary in these captures is the in-texture visor edge, **not** the bare crop edge — they do not isolate the defect |
| Eye-texture captures | **None exist anywhere searched** (repo, dist, Downloads, Documents, scratch, ProgramData): no DDS/RDC/PIX/dumps of a submitted eye image. ViewLab has no readback path. The app-texture → VDXR boundary remains directly unobserved |

**Retractions (supersede earlier sections where they conflict):**
1. "Submitted texture contains no black / game fills the whole texture / source verifiably clean"
   → RETRACTED. Full `imageRect` declares validity; it does not prove pixel contents. Strongest
   defensible: *submission metadata is exact and self-consistent; pixel contents unobserved.*
2. Probe execution log ⇒ probe pixels present/visible → DOWNGRADED: commands were issued and a
   one-shot log fired; no GPU-completion proof, no readback, no reliable visual reading exists.
3. "Black is definitively generated by VD after submission" → DOWNGRADED to leading inference:
   source-internal black (small viewport/clear inside a fully-declared texture) is not excluded
   without pixel capture. VD-background remains most probable origin (single projection layer,
   `UseFovStencil=false`, and the 17:47 capture shows clean black beyond a sharp edge).
4. Depth-based reprojection on VD → RULED OUT (see table).
5. VDXR precompositor as a candidate → RULED OUT for these sessions (settings-verified inactive).
6. "PC mirror" experiments → WITHDRAWN (user: no usable PC mirror exists). Replacement late-stage
   capture channel: **VD in-headset screenshots** (post-decode, land in `Pictures\VR Screenshots`).

**Hypothesis ranking after this pass (≤60% cap; §6 superseded):**
1. **VD proprietary compositor/streamer boundary handling** (rotational reprojection ± SSW ±
   stream-side sharpening at 1.0 interacting with the layer edge) — **~40%**. All PC-side stages
   before it are now verified-inactive or verified-clean-metadata; VD receives the cropped-FOV
   layer directly. Unknown: closed pipeline; SSW state; where Sharpening=1.0 applies.
2. **Game-side rendering/PP under cropped projection** — **~25%** (raised: source pixels remain
   unobserved; Unity 2021.3 built-in pipeline; bloom/temporal effects unidentified from logs).
3. **Encoder/chroma (H.264+) confined inside the image** — ~10%.
4. **Quest-side decode/compositor second reprojection** — ~10%.
5. Coordinate/slice error — ~3% (audit exhausted at metadata level).
6. ViewLab draw path corrupting edges — ~3% (visor path writes only intended geometry per source;
   defect reported with visor off).
7. Residual unenumerated — ~9%.
   *Not included pending verification: any non-VD-runtime mechanism — no non-VD session is on
   record; if the user confirms a specific non-VD runtime run showing the identical band, ranking
   changes materially (H1's VD-specific branches lose weight to a universal-compositor account).*

**Single remaining decisive unknown:** the actual pixel values of the outer 1–64 texels
(per visible edge) of one released eye-swapchain image immediately after
`xrReleaseSwapchainImage` and before `xrEndFrame` — i.e., one readback of the submitted image.
That one fact splits source-side (band already present) from post-submission (band absent) for
the leading hypotheses. No existing artifact on this machine contains it; every capture found is
post-decode. Obtaining it requires either a ViewLab readback (new code) or a RenderDoc attach —
both out of scope for this research-only pass.

## 10. Journal

**2026-07-11 (evening)** — Mode sweep 0–4 run on Pistol Whip/VDXR (19:03–19:11); probe executed
and log-verified; observation not yet recorded in the §7.3/7.4 format. Installed DLL rebuilt at
19:17, after all sessions. Corrections 4 and 5 received (interior band; cross-game/cross-runtime).
Hypotheses re-ranked (§6). Ruler + capture diagnostics designed (§7), not implemented.

**Next action:** implement §7.1 ruler + §7.2 capture as a new `crop_experiment_mode`, run the
§7.3 matrix, fill in §7.4. Open question for the user: which non-VD runtime(s) and games showed
the band, and was its character (width/look) the same there?

**2026-07-11 (late) — evidence-repair pass.** Audited and retracted unsupported pixel claims
(§9b). Resolved the resolution chain (0.7695↔2364 at h=0.539 vs 0.615↔1889/1890 at h=0.23; no
inconsistency — different sessions). Verified from VDXR's own log + registry + Unity Player.log:
VDXR precompositor inactive, depth submission None, VD Sharpening=1.0, UseFovStencil=false,
H.264+, SSW unknown (headset-side). Found first direct pixel evidence (VD in-headset screenshots,
post-decode, visor-on sessions — consistent with corrected visual model but do not isolate the
defect). Confirmed no eye-texture capture exists anywhere; named the single decisive unknown:
one readback of a released eye image's outer 64 texels. All 384 logged sessions are VDXR —
"regardless of runtime" currently uncorroborated; awaiting user detail on which non-VD runtime
was tested.

**2026-07-11 (night) — mechanism confirmed from direct pixel evidence; guard-band fix deployed.**
User launched Pistol Whip at h=0.4, v=0.2 (visor ON), 21:34 session, swapchain 2150×654, and
took Quest-controller screenshots (`Pictures\VR Screenshots\...213541.jpg`, post-decode, 3840×2160).
Scanline measurements on that capture, same frame/codec/motion:
- Visor in-texture black boundary (arc, y=1300): black→content in **~12 px** — sharp.
- Top boundary (x=2000): **~4 px** — sharp.
- Left tip, where content touches the physical texture edge (y=1060): **~63 px** graduated smear
  ramp, horizontally streaked.
A 5× transition-width difference between boundary types within one frame rules out codec/SSW as
the discriminator: **the smear occurs exactly where valid content touches the swapchain's
physical edge, and is absent wherever in-texture black sits at the boundary.** The visor's own
black regions were unintentionally demonstrating the fix. Localisation: post-submission boundary
sampling in the VD path (clamp at texture edge), horizontal-dominant consistent with yaw motion.

**Fix implemented (dllmain.cpp):** revived `edge_smear_fix` as a real config-driven black edge
guard — opaque black frame (`edge_smear_pixels`, default 8, clamp 1–64) drawn around each eye's
imageRect at `xrReleaseSwapchainImage` (after visor, so the outermost ring is always black),
with the existing late-endFrame fallback; layout-capture and release gates extended. Colours in
`DrawEdgeGuardToTexture` now black for guard, palette only for the probe (mode 4).
Built x64 Release, installed to `C:\Program Files\xr-viewlab`, SHA256 match verified
(2B5073D3…83EF). Live ini: `edge_smear_fix=1` (user-set), pixels default 8.

**Pending headset acceptance (user):** relaunch Pistol Whip, same crop values, and check:
(1) left/right tips of the opening — the ~60px haze should be gone (edge goes sharp to black);
(2) visor OFF run — all four boundaries should now look like the visor-arc quality;
(3) cost check: the visible image shrinks by 8 px per edge (~0.4% width) — should be imperceptible.
If haze persists identically → the guard did not change the edge content → revisit (would imply
source-side band, contradicting today's measurement). If haze shrinks but a thin band remains →
raise `edge_smear_pixels` to 16.

**2026-07-11 (later night) — guard verified in-headset capture; progressive-degradation model tested.**
Session 21:49:42 ran the new DLL (log: "edge guard: enabled, width=8 px"); screenshot 21:50:14
(ReShade ASCII shader active — acts as an in-texture calibration grid). Measurements:
- Left boundary transition: **~3 px** (was ~63 px at 21:35, pre-guard). Terminal smear eliminated
  at the measured boundary. Guard confirmed effective; kept (config-gated via `edge_smear_fix`).
- Position-dependent-resampling test: the guard's black step edge measured at four screen
  positions (left edge y=500/y=900, top edge x=1400/x=1900) is uniformly **2–4 px sharp**.
  A downstream warp/blur strong enough to stretch interior pixels progressively would have to
  widen these steps with eccentricity; it does not. **The "broad progressive nonlinear
  resampling" model is contradicted for static frames** at these eccentricities.
- The apparent progressive glyph stretching near the boundary in the ASCII capture is confounded:
  dim scene regions map to thin dash-like glyphs; glyph-grid periodicity measurement was
  inconclusive (JPEG + scene variation). Not accepted as evidence of geometric warp.
Remaining open component: any **motion-dependent** degradation (reprojection/SSW during yaw)
cannot appear in a single static screenshot. Next evidence: short VD in-headset **video
recording** (Videos folder) during slow yaw with ASCII off, guard on — frame stepping will show
whether boundaries/interior breathe under motion.

**2026-07-11 (23:xx) — ruler verdict; cleanup.** Ruler+dump run (mode 5) delivered the decisive
pixel pair: submitted image on disk = uniform 64px grid, thin crisp lines to the edge (ViewLab
output proven clean). Headset capture of the same grid = spacing still uniform (~95px, constant
scale — **no geometric warp anywhere**), but line CONTRAST collapses progressively toward the
boundary (deep dark lines center → ~20% strength wide smudges at the edge). Conclusion:
**eccentricity-dependent quality/detail loss in the VD encode path** (bit-allocation falloff —
kills low-amplitude fine detail, preserves strong edges; codec- and bitrate-independent).
No user-accessible disable exists in VD. User declined vendor outreach and the pre-emphasis
(edge-weighted pre-sharpen) countermeasure. Per user instruction, all ruler/dump diagnostic code
removed (mode 5 deleted, clamp back to 0–4, Captures folder deleted), live ini restored
(mode 0, visor back on). **Edge guard kept** (edge_smear_fix=1, 8px — the verified fix for the
terminal boundary smear). Clean build installed, SHA256 match.
Open, if ever resumed: pre-emphasis pass remains the only ViewLab-side countermeasure candidate
for the encoder falloff; density-matching the swapchain to stream resolution (exact tangent
ratio instead of (1+h)/2) is a smaller secondary win.

**2026-07-11 (close-out) — root cause confirmed by Virtual Desktop support; investigation closed.**
User posted the evidence in the VD Discord. VD support staff ("w") confirmed the diagnosis
verbatim: *"the center has higher bitrate than the edges"*, *"the more you reduce your tangents
the more noticeable fixed foveation will be"*, *"the tangent changes the stream on the device"*,
and *"FFR has never been modifiable"* — a feature request to expose it was called a support
nightmare, unlikely to ever happen. Final conclusions:
1. **Root cause: Virtual Desktop's fixed foveated encoding** — center-weighted bitrate/quality
   allocation baked into the stream, anchored to the display, not modifiable, codec- and
   bitrate-independent (it is a ratio). ViewLab's crop parks its boundaries deep in the
   low-quality zone and frames them, making a normally invisible falloff conspicuous.
2. **Secondary defect (separate, FIXED):** terminal boundary smear from compositor clamp
   sampling at the physical texture edge — eliminated by the 8px black edge guard
   (`edge_smear_fix=1`), verified by before/after headset captures (63px ramp → 3px).
3. **ViewLab is exonerated end-to-end:** submitted pixels proven clean (ruler grid dump),
   submission metadata exact, VDXR passthrough verified.
Go-forward: mask-inset workaround + optional feature request. Details and plain-language summary
moved to [FIXED_FOVEATION.md](FIXED_FOVEATION.md). This document renamed from BUGFIX.md and closed.

**2026-07-12 — VDXR ownership/source audit (research-only; no product change).** Question:
does VD fixed-foveated quality falloff live in open-source VDXR 1.0.10, or after it in proprietary
Virtual Desktop? **Verdict: proprietary VD after VDXR ("Streamer" in product ownership terms),
92% confidence.**

Proven from the exact VDXR 1.0.10 source revision `051774f3`
(`version.info`: `major=1`, `minor=0`, `patch=10`):

1. `OpenXrRuntime::handleProjectionLayer` in `virtualdesktop-openxr/frame.cpp` converts the app's
   `XrCompositionLayerProjection` into `ovrLayerEyeFov`. It assigns the released colour texture,
   viewport, pose, and FOV tangent values; see source lines 665-702.
2. `OpenXrRuntime::xrEndFrame` builds the `ovrLayerHeader*` list and calls
   `ovr_EndFrame(m_ovrSession, ...)` at `frame.cpp:543`. This LibOVR/OVR C API call is the exact
   open-source/closed-source handoff. `ovr_CommitTextureSwapChain` makes a texture available to
   that interface; `ovr_EndFrame` is the actual frame/layer submission.
3. There is no video-encoding code, encoder SDK/API call, or foveated quality-control code in the
   VDXR source tree: no NVENC, NVENCODE, AMD AMF, Intel encoder, ROI, QP, quantisation,
   bitrate-allocation, or centre-weighted map. `virtualdesktop-openxr.vcxproj` links normal
   graphics libraries and uses LibOVR headers/shim, not an encoder SDK.
4. VDXR's optional primary-projection transforms are only FSR EASU upscaling and CAS sharpening:
   `OpenXrRuntime::upscaler` in `precompositor.cpp:61-244`. The audited session's VDXR log
   recorded 1.000 upscaling and sharpening off, so neither ran.
5. With those transforms disabled, VDXR passes the submitted image content onward. It directly
   uses a LibOVR swapchain when possible; `OpenXrRuntime::resolveSwapchainImage` in
   `d3d11_native.cpp:515-595` may perform a D3D11 copy or MSAA resolve solely to satisfy
   array-slice/MSAA compatibility. This is not encoding or quality allocation.

Inference bounded by those facts: VDXR cannot disable the confirmed fixed foveation. Changing it
could alter the submitted pixels or its FSR/CAS settings, but there is no VDXR foveation switch to
remove. A disable/control would require a change in proprietary Virtual Desktop after
`ovr_EndFrame`. Public source cannot establish whether that closed quality decision is made on
the PC Streamer encoder or another later VD/device component; vendor support's centre-bitrate
statement attributes the feature to VD, while the source audit independently rules VDXR out.

Source links: [VDXR frame handoff](https://github.com/mbucchia/VirtualDesktop-OpenXR/blob/051774f3d89c131312963877cdf53584ddca98b2/virtualdesktop-openxr/frame.cpp#L518-L544),
[projection conversion](https://github.com/mbucchia/VirtualDesktop-OpenXR/blob/051774f3d89c131312963877cdf53584ddca98b2/virtualdesktop-openxr/frame.cpp#L665-L702),
[optional precompositor](https://github.com/mbucchia/VirtualDesktop-OpenXR/blob/051774f3d89c131312963877cdf53584ddca98b2/virtualdesktop-openxr/precompositor.cpp#L61-L244),
and [copy/resolve path](https://github.com/mbucchia/VirtualDesktop-OpenXR/blob/051774f3d89c131312963877cdf53584ddca98b2/virtualdesktop-openxr/d3d11_native.cpp#L515-L595).

**2026-07-12 — second independent VDXR audit (Claude); concurs, adds the physical boundary.**
Ran against the local clone `C:\Users\strif\VirtualDesktop-OpenXR` at commit `051774f` — verified
to be the exact installed build (VDXR's own `OpenXR.log` reports "1.0.10 (051774f3)"). Verdict
unchanged: **fixed foveation lives in proprietary VD, not VDXR; combined confidence ~95%.**
New facts beyond the entry above:

1. **Physical closed-source boundary identified.** `ovr_EndFrame` is not Meta's LibOVR: VDXR's
   `OVR_CAPIShim.c` builds the library name `%lsLibOVRRT%hs_%d.dll` from a prefix that
   `system.cpp:308-330` sets to `<Streamer path>\VirtualDesktop.` (read from
   `HKLM\SOFTWARE\Virtual Desktop, Inc.\Virtual Desktop Streamer\Path`). It resolves to
   **`C:\Program Files\Virtual Desktop Streamer\VirtualDesktop.LibOVRRT64_1.dll`** (320 KB,
   closed source, verified on disk). Every stage after `frame.cpp:543` (sync) / `frame.cpp:1150`
   (async-submission thread) executes inside that DLL and the Streamer process.
2. **Encoder components located on disk, all Streamer-side:** `libVirtualDesktopAMF.dll`
   (AMD encoding — the relevant path for this machine's RX 7800 XT) plus the ffmpeg family
   (`avcodec-61.dll` etc.) sit in the Streamer install folder. Nothing comparable exists in VDXR.
3. **The pixels live in VD-owned memory before EndFrame.** Swapchain textures are created inside
   the proprietary DLL via `ovr_CreateTextureSwapChainDX` (`frame.cpp:145`, `swapchain.cpp`);
   the zero-copy commit path (`d3d11_native.cpp:543-566`) means the game renders directly into
   VD-owned surfaces. `ovr_EndFrame` is the submission signal, not the pixel transfer.
4. **Complete cross-boundary tuning inventory:** the only settings VDXR ever sends into the
   proprietary side are `ovr_SetBool("IsVDXR")`, `ovr_SetBool("IsOpenComposite")`
   (`system.cpp:432-433`) and `ovr_SetFloat("AppGpuTime")` (`frame.cpp:515`). No foveation, ROI,
   QP, or quality-map structure crosses the boundary in either direction.
5. **`smear.txt` (the Discord support transcript) exists locally** at
   `C:\Users\strif\Documents\smear.txt`. Additional support quotes relevant to ownership:
   *"your mask isnt changing the actual stream though, that's all handled on streamer/hmd side"*
   and *"vdxr is indeed open source / vd is not / … VD controls the stream"*.

Residual uncertainty (the missing ~5%) is only about **which closed stage** implements the
falloff (PC-side Streamer encode vs. something applied device-side — support's *"the tangent
changes the stream on the device"* leaves that ambiguous). That VDXR does not implement it is
proven by exhaustive source absence plus the clean ruler dump.

**2026-07-12 — VDXR boundary: PE exports and official release notes (Claude).**

Same clone and verdict as the 2026-07-12 audits above. This pass adds the exact PE-boundary proof and
official Virtual Desktop product documentation.

1. **PE export-table confirmation.** `dumpbin /exports` on `C:\Program Files\Virtual Desktop Streamer\VirtualDesktop.LibOVRRT64_1.dll` shows the OVR interface entries that VDXR calls:

   ```
   ovr_BeginFrame
   ovr_CommitTextureSwapChain
   ovr_CreateTextureSwapChainDX
   ovr_CreateTextureSwapChainGL
   ovr_CreateTextureSwapChainVk
   ovr_EndFrame
   ovr_GetTextureSwapChainBufferDX
   ovr_GetTextureSwapChainBufferGL
   ovr_GetTextureSwapChainBufferVk
   ovr_GetTextureSwapChainCurrentIndex
   ovr_GetTextureSwapChainDesc
   ovr_GetTextureSwapChainLength
   ovr_SubmitFrame
   ovr_SubmitFrame2
   ovr_WaitToBeginFrame
   ```

   `ovr_EndFrame` is the call made from `frame.cpp:543` (sync) and `frame.cpp:1150` (async); the export table proves the last open-source instruction is resolved into the proprietary `VirtualDesktop.LibOVRRT64_1.dll`.

2. **No encoder imports in the boundary DLL.** `dumpbin /imports` on `VirtualDesktop.LibOVRRT64_1.dll` lists only `dxgi.dll` (`CreateDXGIFactory1`) and `d3d11.dll` (`D3D11On12CreateDevice`). No `avcodec`, `libVirtualDesktopAMF`, `nvenc`, `mf`/`MediaFoundation`, `DXVA`, or `Intel` encoder imports. The actual encoder libraries are separate Streamer-side files (`libVirtualDesktopAMF.dll`, `avcodec-61.dll`, `avformat-61.dll`, `avutil-59.dll`, `swscale-8.dll`, `swresample-5.dll`) located in the same `C:\Program Files\Virtual Desktop Streamer` folder.

3. **Official Virtual Desktop release notes independently attribute fixed foveation to the product streamer.**
   - `guygodin/VirtualDesktop` v1.34.2 release notes (2022-09-20): “Added **bitrate fixed foveation** on 10-bit codecs with Nvidia GPUs (previously was only with H.264/H.264+)” — https://github.com/guygodin/VirtualDesktop/releases/tag/v1.34.2
   - `guygodin/VirtualDesktop` v1.34.16 release notes (2025-04-01): “Added **Foveated streaming** with eye tracked headsets (Quest Pro, Galaxy XR, ...). This uses eye tracking to improve the quality of the image where you are looking” and “Added **adaptive quantization** support with AMD GPUs using H.264/H.264+” — https://github.com/guygodin/VirtualDesktop/releases/tag/v1.34.16

   These are official Virtual Desktop release notes, independent of the Discord support transcript in `smear.txt`, and they explicitly list bitrate/foveation/adaptive quantization as product features of the closed-source Streamer/encoder. They concur with the support statements: *“the center has higher bitrate than the edges”* and *“FFR has never been modifiable”* (`smear.txt` lines 27-30, 69-70).

4. **Runtime process ownership.** The active components at the time of the session were `VirtualDesktop.Service` and `VirtualDesktop.Streamer.exe` (path `C:\Program Files\Virtual Desktop Streamer\VirtualDesktop.Streamer.exe`). VDXR (`XR_APILAYER_cooooked_xrviewlab.dll`) is an OpenXR API layer that runs inside the game process and does not host these encoder libraries.

Confidence remains **~95%** for the ownership verdict (fixed foveation is in proprietary VD after the `ovr_EndFrame` handoff; not in VDXR). The residual ~5% is only the exact closed sub-stage (PC-side Streamer encoder vs. a later device-side application), which the open-source VDXR tree and PE data cannot resolve.

<!-- Append new journal entries above this line’s section end; newest at bottom. -->
