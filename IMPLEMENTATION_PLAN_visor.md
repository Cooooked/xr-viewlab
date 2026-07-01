# Implementation Plan — Visor Mask Techniques A / B / C (for the next dev chat)

**Date:** 2026-07-01
**Read alongside:** `RESEARCH_visor_mask.md` (why), `PROJECT_STATUS.md` / `HANDOFF.md` (state).
**Goal:** implement all three masking techniques behind a config selector so each can be tested
individually in-headset, then keep whichever works best (expected winner: A).

## STATUS (2026-07-02, v4.1.54)

- Selector `visor_technique` supports `off`, `a`, `b`, and `c` in config and UI.
- Technique A is active again as an OpenXR composition-layer quad. It submits a BOTH-eye head-locked alpha
  quad for VDXR compatibility, but the texture now contains open-inner left/right visor artwork.
- Technique B is implemented experimentally for D3D11 colour non-MSAA swapchains only. Unsupported
  swapchains bypass interception to avoid the previous full-black behaviour.
- Technique C is implemented at `xrReleaseSwapchainImage` and also does a late `xrEndFrame` draw to catch
  OpenComposite/OpenVR timing paths such as DiRT.
- The UI exposes Size/Width/Height/Curve. X/Y sliders are removed, and stale X/Y registry/INI biases are
  ignored by the DLL.
- Vertical/horizontal crop remains active. `lod_popin_fix` is diagnostic-only and must not restore the
  unsafe full-FOV/stretch path.
- `edge_smear_fix` now draws black guard bands into projection textures. Do not restore the old
  submitted-`imageRect` inset approach.

## STATUS (2026-07-01 late, v4.1.47)

- Selector `visor_technique` (off/a/c): DONE (config + UI radio).
- Technique A (quad overlay): DONE, reworked to a single BOTH-eye quad (per-eye didn't render on VDXR);
  awaits in-headset confirmation. Layer submission itself is CONFIRMED working (blue quad drew).
- Technique C (direct write): DONE; draws but doesn't appear on VDXR (streaming). Kept for native runtimes.
- Technique B (swapchain interception): NOT started (only pursue if A is insufficient and C stays broken).
- Root cause of prior "no mask" (size=1.0) FIXED (fallback 0.82).
- PENDING (user-requested, deferred until A confirmed): experimental edge-smear toggle (imageRect inset)
  and experimental LOD-pop-in toggle (full FOV for culling + reduced resolution). Gate off by default.

---

## 0. Preconditions already done in this chat (baseline for the new chat)

- **Enable is GLOBAL-only.** `dllmain.cpp` no longer lets a per-app profile override `mask_enabled`
  (removed the `maskEnabled = profileMaskEnabled != 0` line). Per-app profiles still tune shape.
- ProfileWindow's per-app "Enable visor mask" checkbox is **disabled + relabelled** (global-only).
- Global toggle is the main-window `MaskEnabledCheck` → writes `mask_enabled` to `xr-viewlab.ini`
  `[Settings]` → DLL reads it. Verified persist path.
- Technique C (direct write at `xrReleaseSwapchainImage`) is implemented but unproven in-HMD.
- Debug `test_quad` (head-locked blue `XrCompositionLayerQuad`) is implemented — reuse its plumbing for A.

## 1. Config selector (add first)

Add a global INI key **`visor_technique`** in `[Settings]`, read in `LoadConfig`:
- `off` / `0` — no visor (crop only).
- `a` / `1` — quad overlay layer (RECOMMENDED default once working).
- `b` / `2` — swapchain interception + composite.
- `c` / `3` — direct write into runtime swapchain at release (current).

Represent as an enum `VisorTechnique { Off, QuadOverlay, Interception, DirectWrite }`. Since the existing
config helpers are `ReadBoolSetting`/`ReadDoubleSetting`, either add a tiny `ReadStringSetting` or read an
int (`1/2/3`) via a DWORD/double cast. Keep `mask_enabled` as the master on/off; `visor_technique` picks
the mechanism. `test_quad` stays independent (pure probe).

Gate each path:
- C: only run the release-hook draw when `mask_enabled && visor_technique==DirectWrite`.
- A: only append the visor quad in `xrEndFrame` when `mask_enabled && visor_technique==QuadOverlay`.
- B: only install the interception swapchain substitution when `mask_enabled && visor_technique==Interception`.

## 2. Shared pieces (already present — reuse, don't duplicate)

- Superellipse math: `BuildVisorBorderVerts` (NDC triangles for C), `ApplyVisorMask` (mesh), and the
  UI `BeanMaskEditor.BuildGeometry` — all must stay identical: `exp = 32 - clamp(curve,0,1)*30`,
  `halfW = bboxW*0.5*clamp(size*width,0.01,1)`, offset `(bboxW*0.5-halfW)*offsetX`, etc.
- Per-eye FOV/pose is already captured each frame in `g_d3d11Mask.latestViews` (from `xrLocateViews`).
- `GetNonSRGBFormat` handles SRGB/typeless → concrete UNORM for RTVs.
- D3D11 device/context in `g_d3d11Mask`. Save/restore all pipeline state around any draw.

---

## 3. Technique A — Quad overlay (lowest risk; do first)

**Idea:** an alpha-cutout visor rendered into our own swapchain and submitted as an extra composition
layer in `xrEndFrame`, after the app's projection layer. Proven pattern (OpenKneeboard).

**Build on the existing `test_quad` code** (`InitTestQuad` / `RenderTestQuadLayer`) — it already creates a
VIEW reference space + swapchain and appends a quad. Changes:
1. Texture content: instead of solid blue clear, render the visor — clear to transparent, then draw the
   black region OUTSIDE the superellipse with alpha=1 (RGB=0). Reuse `BuildVisorBorderVerts` (it already
   emits the outside-border triangles in NDC over the full [-1,1] quad). Pixel shader outputs (0,0,0,1);
   the transparent interior stays alpha=0 from the clear.
2. Swapchain format: RGBA8 UNORM(_SRGB). Alpha channel required.
3. Layer flags: `XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT`. RGB is 0 everywhere, so
   premultiplied vs unpremultiplied is identical (black); default (unpremultiplied) is fine.
4. Placement: head-locked VIEW space. Size the quad to cover the FOV at distance d:
   `size.x = 2*d*tan(hFov/2)`, `size.y = 2*d*tan(vFov/2)` using the captured per-eye FOV; d ≈ 0.5–1.0 m.
   Pose position (0,0,−d), identity orientation.
5. Stereo nose: submit TWO quads, `eyeVisibility = LEFT` and `RIGHT`, each rendered with that eye's
   opening (apply `visorOffsetX` sign per eye / use that eye's FOV). Two swapchains or one array/two rects.
   Start monoscopic (BOTH) to validate, then split for the nose.
6. Layer order: append AFTER the app layers so the visor is on top.
7. Per-frame acquire/wait/release of our swapchain (already done for test_quad) — satisfies the
   "update every frame" runtime quirk.

**Pros:** runtime-agnostic, no game-texture writes, works on VDXR/OpenComposite. **Cons:** flat billboard
(fine for a visor); need per-eye quads for a correct nose gap.

**Test:** `mask_enabled=1`, `visor_technique=a`. Expect a black surround with a kidney hole locked to the
view. Log: `visor A: submitting quad layer(s)`.

---

## 4. Technique C — Direct write at release (already implemented; verify)

**Status:** code complete in `XRViewLab_xrReleaseSwapchainImage` → `DrawVisorBorderToTexture`, using the
per-eye layout captured in `xrEndFrame`. Just needs `mask_enabled=1` (now global) and
`visor_technique=c`.

**Verify path with logs:** `xrReleaseSwapchainImage reached (tracked=1 sessionMatch=1 eyeViews=N)` →
`direct-write at release active` → `DIAG: OK draw executed ... rect=(...) slice=...`. If OK but nothing
visible, the app is not rendering into the runtime swapchain we see (interposed pipeline) → that is the
signal to prefer A or B.

**Known risk:** for DiRT (OpenComposite), the OpenXR swapchain we see is OpenComposite's; the draw only
lands if OpenComposite copies the game frame into it before release (before our hook forwards release).

---

## 5. Technique B — Swapchain interception (highest risk; do last)

**Idea (from mbucchia nis_scaler):** hand the app OUR textures; composite into the runtime's real
swapchain, drawing the mask during the composite.

Steps in `dllmain.cpp`:
1. `XRViewLab_xrCreateSwapchain`: call `nextXrCreateSwapchain` to make the runtime swapchain (store its
   textures as `runtimeTextures`). Also create a parallel set of app-facing `ID3D11Texture2D` (same
   width/height/format/arraySize, usage RTV+SRV) that WE own (`appTextures`). Keep both in the tracked map.
2. `XRViewLab_xrEnumerateSwapchainImages`: return OUR `appTextures` to the app (not the runtime's). The
   app renders into ours.
3. `xrAcquire/Wait/ReleaseSwapchainImage`: forward to the runtime; track the acquired index.
4. At `xrReleaseSwapchainImage` (or `xrEndFrame`): for the released image, copy/draw `appTextures[i]` →
   `runtimeTextures[i]` (full-screen blit via SRV→RTV), then draw the visor border on top of the runtime
   texture, then forward release / submit. Per array slice.
5. Formats: match exactly; use `GetNonSRGBFormat` for the RTV, an SRV for the app texture; handle
   `arraySize>1` (texture array slices) and `sampleCount>1` (resolve if MSAA — most eye swapchains are 1).

**Pros:** pixel-exact on the real eye image, no extra layer. **Cons:** most code, must own every
create/enumerate/acquire/release path, extra full-screen copy per eye per frame, easy to break other
layers. Only pursue if A is insufficient and C is proven unreliable.

**Test:** `mask_enabled=1`, `visor_technique=b`. Log: `visor B: composited app->runtime + mask`.

---

## 6. Files to touch

- `dllmain.cpp`: config selector + read; gate C; implement A (extend test-quad code into a visor quad);
  implement B (swapchain substitution). Keep all three behind the selector.
- `pch.h`: already has D3D11 + OpenXR. B may need `<d3d11.h>` SRV types (already included).
- UI: none required for testing (drive via `visor_technique` in the ini). Optional later: a technique
  dropdown in Render Options.
- Docs: update `dllmain_features.md` (add `visor_technique`), `CHANGELOG.md`, `PROJECT_STATUS.md`.

## 7. Test matrix (per technique, both games)

| technique | config | in-HMD expectation | key log line |
|-----------|--------|--------------------|--------------|
| probe | `test_quad=1` | blue square 1 m front, head-locked | `test quad: submitting ...` |
| A | `mask_enabled=1; visor_technique=a` | black surround + kidney hole, head-locked | `visor A: submitting quad layer(s)` |
| C | `mask_enabled=1; visor_technique=c` | black border baked into the eye image | `DIAG: OK draw executed` |
| B | `mask_enabled=1; visor_technique=b` | black border, pixel-exact | `visor B: composited ...` |

Test order: probe → A → C → B. Do Pistol Whip (native) and DiRT (OpenComposite/VDXR) for each.

## 8. Acceptance / rules

- Confirm each technique **in-headset**, not from logs. Logs only prove a path executed.
- No stereo distortion, no crop/projection change, correct kidney nose area, runtime shape == UI shape.
- Keep every technique toggleable and failing back to plain crop.
- Do not reintroduce: per-app enable override; the old projection-orb renderer; visibility-mask as primary.
