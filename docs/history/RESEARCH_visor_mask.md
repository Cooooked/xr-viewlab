# Research Notes — VR Visor Mask Rendering (why it isn't showing, and the correct approaches)

**Date:** 2026-07-01
**Author:** Claude (research + diagnosis for ViewLab)
**Scope:** Why the kidney-shaped visor mask never appears in-headset (Pistol Whip native OpenXR;
DiRT Rally 2 via OpenVR→OpenComposite→VDXR), and what the proven techniques actually are.

---

## 2026-07-02 Addendum - 4.1.54 Repair Pass

Sources checked:

- OpenXR specification: <https://registry.khronos.org/OpenXR/specs/1.0-khr/html/xrspec.html>
- `xrEnumerateSwapchainImages`: <https://registry.khronos.org/OpenXR/specs/1.0/man/html/xrEnumerateSwapchainImages.html>
- `XrSwapchainImageD3D11KHR`: <https://registry.khronos.org/OpenXR/specs/1.0/man/html/XrSwapchainImageD3D11KHR.html>
- Archived mbucchia NIS scaler API-layer precedent: <https://github.com/mbucchia/_ARCHIVE_XR_APILAYER_NOVENDOR_nis_scaler>
- OpenComposite/OpenVR-to-OpenXR context: <https://github.com/QuestCraftPlusPlus/OpenComposite>

Conclusions applied in 4.1.54:

- The v/h tangent crop must stay in `xrLocateViews` and recommended-size reduction. Disabling horizontal
  crop to hide a double edge defeats ViewLab's performance purpose. The LOD experiment that reported full
  FOV while keeping reduced render size caused stretch because projection/culling and submitted image
  geometry no longer agreed. It is disabled for safety in 4.1.54.
- Edge smear should not be handled by insetting submitted `imageRect` metadata. That can create a second
  straight boundary and alter runtime sampling. 4.1.54 draws small black guard bands into the submitted
  projection texture instead.
- Direct-write Technique C is vulnerable to later OpenXR/API-layer post-processing order. If ReShade or
  another layer runs after ViewLab, it can process the black visor. Technique A is the better escape hatch
  because it submits a separate composition layer after the app projection layer.
- Technique B must stay narrow. OpenXR swapchain images returned to the app must be stable for the
  swapchain lifetime, and D3D11 image replacement must match the app's expected colour/non-MSAA usage. The
  4.1.54 implementation bypasses non-colour and MSAA swapchains instead of intercepting them.
- OpenComposite/OpenVR paths can shift timing enough that release-time C may miss a frame/layout. 4.1.54
  keeps the release-time draw and adds a late `xrEndFrame` draw before forwarding the frame to the next
  layer/runtime.

## 1. Problem statement

Goal: draw solid black outside a kidney/superellipse opening on the finished VR image, purely visual,
no change to projection/pose/crop/stereo. Across many builds (through 4.1.42) nothing has appeared in
the HMD for either game.

## 2. Evidence gathered on-machine (not speculation)

From `%LOCALAPPDATA%\XR ViewLab\Logs\ViewLab.log` and the registry, 2026-07-01:

- Hooks install correctly; D3D11 session detected; renderer initialises. Example (Pistol Whip):
  `d3d11 mask DIAG: xrEndFrame hook called (enabled=1 maskEnabled=0 visMaskCalled=1 sessionMatch=1 d3d11Init=1 layerCount=1)`
- **`maskEnabled=0`** in the live global ini (`mask_enabled=0`) AND in every per-app registry profile
  (`HKCU\Software\cooooked\xr-viewlab\Apps\*` all `mask_enabled=0`).
- The D3D11 draw is (correctly) gated on `maskEnabled`, so it **never ran**.

> **Critical conclusion:** every "no mask" report so far was with the mask *disabled*. The release-time
> D3D11 draw path has therefore **never actually been exercised** — it is unproven, not disproven. Two
> independent bugs exist: (a) the enable toggle is not persisting into the config the DLL reads, and
> (b) it is unknown whether the chosen draw method works at all. Fix (a) and/or use a path that doesn't
> depend on it (the `test_quad` probe) before drawing more conclusions.

## 3. The three real architectures for touching the final VR image

Research into shipping OpenXR API layers shows three distinct, proven patterns. ViewLab has only ever
tried a fragile variant of #3.

### A. Overlay via your own composition layer — OpenKneeboard model (RECOMMENDED to try first)
- The layer creates its OWN reference space(s) and its OWN swapchain, renders into that swapchain, and
  **appends an extra `XrCompositionLayerQuad` to `XrFrameEndInfo` in `xrEndFrame`**. It does **not**
  modify the game's swapchains at all.
- OpenKneeboard (widely used across SteamVR, VDXR, WMR, OpenComposite) does exactly this: *"does not
  modify the game's API calls, except for appending a quad layer in xrEndFrame()"* and *"calls
  xrCreateReferenceSpace() independently of the game."*
- This is the most compatible and lowest-risk way to put pixels in front of the user. It is precisely
  what ViewLab 4.1.42's `test_quad` implements as a probe.
- For a **visor**: use a head-locked quad (VIEW space) with an **alpha-cutout texture** — opaque black
  frame, transparent kidney hole. Because a quad is a flat billboard rendered on top, "in front of the
  face" is exactly the desired visor behaviour.
- Stereo nose gap: a quad is monoscopic. To get a per-eye opening, submit **two** quads with
  `eyeVisibility = XR_EYE_VISIBILITY_LEFT` / `RIGHT`, each with a horizontally-offset hole.

### B. Swapchain interception + post-process — OpenXR-Toolkit model
- The layer intercepts `xrCreateSwapchain`/`xrEnumerateSwapchainImages` and gives the **app its own
  intermediate textures**. The app renders into those. At submit, the layer processes (upscale,
  post-fx, or here: draw the mask) from the app texture into the **runtime's real swapchain**, then
  submits the runtime swapchain.
- Description of OXRTK: *"receives the rendered frames back from the app, modifies the images … then
  passes them onto the OpenXR runtime."*
- Pixel-exact, no extra layer geometry, but the heaviest and most failure-prone (must own every format,
  array slice, sample count, usage flag, and an extra copy/draw pass).

### C. Direct write into the runtime's swapchain image (ViewLab's current 4.1.41+ approach)
- Assumes the app renders **directly** into the runtime's swapchain image (true for a normal native app
  with no other interposing layer). The layer draws into that same texture at the last app-owned moment.
- ViewLab draws in the **`xrReleaseSwapchainImage` hook, before forwarding the release** — which is
  spec-legal because from the runtime's perspective the image is still acquired until we forward. (Drawing
  in `xrEndFrame`, as pre-4.1.41 did, is **after** release → the app no longer owns the image → wrong.)
- Simplest, but: unproven here; and it silently breaks if anything else intercepts the swapchains
  (another layer, or OpenComposite/OXRTK giving the app non-runtime textures).

## 4. Key OpenXR spec facts that constrain all of this

- **Layers are drawn in submission order**; the 0th layer is drawn first, later layers on top. So an
  overlay quad must be appended **after** the app's projection layer to sit on top.
- Per frame, each swapchain implicitly uses **one image: the one from the last `xrReleaseSwapchainImage`**.
  So `lastAcquiredIndex` tracking is correct.
- After `xrReleaseSwapchainImage` the **app must not write** to the image. ViewLab's release hook draws
  *before* calling the real release, so it stays inside the legal window. Ordering on the shared D3D11
  immediate context guarantees the mask draw executes before the runtime composites.
- **Alpha blending** for a layer requires `XR_COMPOSITION_LAYER_BLEND_TEXTURE_SOURCE_ALPHA_BIT` in
  `layerFlags`; premultiplied vs straight alpha matters for edge quality (black is safe either way).
- **Swapchain-update-every-frame quirk:** some runtimes (older SteamVR on Index/Pico) *incorrectly*
  require any swapchain referenced in a frame to be acquired/waited/released **that frame**, or they
  stutter / drop the layer. OpenKneeboard ships an "always update swapchains" quirk for this. ViewLab's
  `test_quad` already acquires/waits/releases every frame, so it is safe; a *static* overlay swapchain
  would need this quirk. VDXR (Meta/Oculus-derived path) is generally spec-compliant here.

## 5. Runtime-specific notes

- **Pistol Whip (Unity, native OpenXR):** Unity calls `xrGetVisibilityMaskKHR` for its occlusion mesh.
  Pre-4.1.41 ViewLab used that call to *disable* the D3D11 path — the root cause of "nothing on Pistol
  Whip." That gate is removed in 4.1.41. Unity renders directly into the runtime swapchain, so approach
  C *can* work; approaches A/B also work.
- **DiRT Rally 2 (OpenVR → OpenComposite → VDXR):** the game submits OpenVR textures; OpenComposite
  copies them into OpenXR swapchains and submits to VDXR. ViewLab sees OpenComposite's OpenXR swapchains.
  Approach C then depends on OpenComposite copying into the swapchain image *before* releasing it (likely
  true) — but this is exactly the kind of interposed pipeline where C is fragile. Approach A (own quad
  layer) is far more robust here because it is independent of how the game's frame reaches the swapchain.
- **VDXR (Virtual Desktop):** implements OpenXR over Virtual Desktop's Oculus-runtime emulation; supports
  projection and quad layers. Community reports confirm overlay layers (OpenKneeboard, OXRTK menu) work
  on VDXR, which is strong evidence approach A is viable on the user's exact setup.

## 6. Why ViewLab kept failing (summary of causes, in order encountered)

1. Old projection-layer "orb" renderer: wrong per-eye geometry → disconnected orbs / stereo mismatch. Removed.
2. Visibility-mask gate: any game calling `xrGetVisibilityMaskKHR` disabled the D3D11 path (killed Pistol
   Whip). Removed in 4.1.41.
3. Draw happened in `xrEndFrame` (after release) — too late. Moved to `xrReleaseSwapchainImage` in 4.1.41.
4. **The mask was never enabled** (`mask_enabled=0` everywhere) — so even the corrected path never ran.
   This is the current blocker and is a UI/config-persistence bug, independent of rendering.

## 7. Recommendation / plan

1. **Prove layer submission works** with `test_quad=1` (4.1.42). If the blue head-locked quad appears,
   approach A is validated on this hardware and the whole class of "can we draw in VR" doubt is resolved.
   Check the log for `test quad: submitting head-locked blue quad layer`.
2. **Pivot the visor to approach A (quad overlay)** if the box shows: a head-locked, alpha-cutout visor
   quad (per-eye via `eyeVisibility` for the nose). This is the OpenKneeboard-proven, runtime-agnostic
   path and avoids all swapchain-write fragility — especially important for the DiRT/OpenComposite chain.
3. **Independently fix `mask_enabled` persistence** (UI toggle → global ini `mask_enabled` and/or per-app
   registry `mask_enabled` → DLL read). Without this the mask can never turn on regardless of technique.
4. Keep approach C as a fallback only for native runtimes, and only after A is confirmed.
5. Do not claim success from logs; confirm each step in-headset.

## 8. Open questions to resolve with the next in-headset test

- Does the `test_quad` blue box appear in Pistol Whip? In DiRT? (Isolates layer-submission viability.)
- Does the log show `test quad DIAG: initialized` and `...submitting...`? (Isolates our side vs runtime.)
- When `mask_enabled` is genuinely 1 (fix persistence first), does approach C draw anything? Watch for
  `d3d11 mask: direct-write at release active` and `DIAG: OK draw executed`.

## 9. Sources

- OpenKneeboard — Third-Party Developers: https://openkneeboard.com/faq/third-party-developers/
- OpenKneeboard — Injectables/Internals: https://openkneeboard.com/internals/injectables/
- mbucchia — VirtualDesktop-OpenXR (VDXR): https://github.com/mbucchia/VirtualDesktop-OpenXR
- mbucchia — OpenXR-Toolkit: https://github.com/mbucchia/OpenXR-Toolkit and https://deepwiki.com/mbucchia/OpenXR-Toolkit
- mbucchia — OpenXR API Layer Template: https://github.com/mbucchia/OpenXR-Layer-Template
- Khronos — XrCompositionLayerQuad man page: https://registry.khronos.org/OpenXR/specs/1.0/man/html/XrCompositionLayerQuad.html
- Khronos forum — How to use XR_TYPE_COMPOSITION_LAYER_QUAD: https://community.khronos.org/t/how-to-use-xr-type-composition-layer-quad/110621
- Khronos — xrEndFrame man page: https://registry.khronos.org/OpenXR/specs/1.0/man/html/xrEndFrame.html
- Microsoft — OpenXR best practices: https://learn.microsoft.com/en-us/windows/mixed-reality/develop/native/openxr-best-practices
- LunarG — Overlays in OpenXR (PDF): https://www.lunarg.com/wp-content/uploads/2020/05/Overlays-in-OpenXR.pdf
- SteamVR discussion — composition layer swapchains required every frame (runtime quirk): https://steamcommunity.com/app/250820/discussions/0/3843305519472175943/
