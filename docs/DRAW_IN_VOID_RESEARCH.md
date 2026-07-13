# Draw in the Void — research and diagnosis

**Status:** research only, 2026-07-12. No overlay-preservation implementation is authorised by
this document. A failed probe diagnoses that probe/configuration; it does not disprove the goal.

## Finding in one sentence

ViewLab presently crops the **game's requested projection FOV and recommended eye-texture
dimensions**, not a submitted projection `imageRect` and not the composed output. Under the
OpenXR contract, an independently submitted quad is a separate composition layer. However, VDXR
then PC-composes every submitted layer into **one encoded stream**; the reduced FOV constrains that
single stream. An overlay cannot remain visible in the unrendered field unless the final composed
layer is full-FOV (including black bars), or a streamer implements separate-layer transport.

## 1. Current crop path

| Stage | ViewLab behaviour | What it does **not** do |
|---|---|---|
| `XRViewLab_xrLocateViews` → `ApplyXRViewLabFov` | Narrows `XrView.fov` tangents before they return to the game. Vertical top/bottom and horizontal outer/full scaling live here. | It does not alter `XrCompositionLayerProjectionView` values at `xrEndFrame`. |
| `XRViewLab_xrEnumerateViewConfigurationViews` | Reduces `recommendedImageRectWidth`/`Height`; the game normally creates smaller projection swapchains and renders fewer pixels. | It does not alter a created swapchain's size, its viewport/scissor, or a submitted `subImage.imageRect`. |
| `XRViewLab_xrEndFrame` | Reads projection views to map game textures for the Direct-C visor/calibration path, then normally forwards the untouched `XrFrameEndInfo`. | It does not crop or mask the runtime's final composition. |
| `XRViewLab_xrReleaseSwapchainImage` | Draws the black visor/calibration pattern into a **game projection texture** before it is released. | It cannot black out an independent third-party composition layer. |

The former diagnostic `draw_in_void_test` cloned the pointer array, appended a ViewLab-owned
quad, and forwarded that amended `XrFrameEndInfo`. It neither rewrote the game projection nor
other layers. It was removed on 2026-07-12 after the VDXR developer verdict made it irrelevant to
the intended runtime path.

## 2. What the third-party layers are expected to be

- **OpenKneeboard:** its developer documentation is explicit: its only modification of game API
  calls is appending a `XrCompositionLayerQuad` in `xrEndFrame`; it also creates/locates spaces.
  It is consequently expected to be independently composited, commonly world-locked. This must
  be confirmed in the target game's log, not assumed for every OpenKneeboard version/configuration.
  Source: [OpenKneeboard third-party guidance](https://openkneeboard.com/faq/third-party-developers/).
- **RaceLab VR 3:** RaceLab installs `XR_APILAYER_app_racelab_Overlay64` on this machine and
  documents native OpenXR support. Its settings say world-locked positions are the default and
  that the fallback converts them to head-relative positions each frame. That is strong evidence
  for a separately submitted composition layer, but it does **not** establish whether its exact
  type is quad, cylinder, or a projection layer. Instrument it. Sources: [RaceLab VR 3](https://garage.racelab.app/news/2025/06/03/2025/racelab-vr-3-stable/), [world-lock setting](https://garage.racelab.app/docs/vr/settings/).
- **Older RaceLab docs are obsolete evidence:** a 2024 PDF says OpenXR was unsupported. Do not
  use it to infer behaviour of the installed VR 3 layer.

## 3. Specification and VDXR facts

The OpenXR specification requires every layer for a frame to be supplied to `xrEndFrame`, and
requires the runtime to compose them in submitted order (painter's algorithm). Projection and
quad layers are distinct core layer types; a quad is a spatial surface specified by a space,
pose, physical size, and swapchain sub-image. A later layer may overwrite earlier pixels.
Sources: [OpenXR compositing](https://registry.khronos.org/OpenXR/specs/1.0-khr/html/xrspec.html#rendering-compositing), [quad reference](https://registry.khronos.org/OpenXR/specs/1.1/man/html/XrCompositionLayerQuad.html).

Consequences:

1. The specification provides no rule that a quad be clipped to another layer's projection FOV.
2. It does not promise a background outside a projection layer, nor mandate every runtime's
   handling of uncovered display regions; that is a runtime result to test.
3. The specification promises submitted order, not an automatic depth sort between arbitrary
   layers.

VDXR publicly lists both projection and quad layers as supported, but also says it is not
formally conformant. Its open source path converts projection views to `ovrLayerEyeFov` and then
hands the layer list to proprietary Virtual Desktop code at `ovr_EndFrame`; current repository
evidence places VD's fixed-foveated video quality handling beyond that boundary. This establishes
that VDXR accepts these layer classes, not how VD transports them. Sources: [VDXR feature table](https://github-wiki-see.page/m/mbucchia/VirtualDesktop-OpenXR/wiki/Developers), [VDXR frame path](https://github.com/mbucchia/VirtualDesktop-OpenXR/blob/051774f3d89c131312963877cdf53584ddca98b2/virtualdesktop-openxr/frame.cpp), [existing local VDXR boundary audit](FIXED_FOVEATION.md).

### VDXR developer answer — decisive runtime behaviour (2026-07-12)

A VDXR developer has now confirmed the missing runtime fact in direct correspondence: **all
OpenXR layers are composited on the PC side**, because transporting separate layers would consume
more encoder and network bandwidth. With a FOV-tangent crop, VDXR therefore sends one layer with
the reduced FOV; all composed content, including third-party quads, is constrained to it.

They identified two architectural routes:

1. Transport the quad separately, consuming additional encoder/network bandwidth; they know of
   no streamer that currently does this.
2. Compose everything into a full-FOV output that includes the unrendered region as black bars.
   This is technically possible but deliberately spends encoder resolution/bandwidth on those
   bars, reducing the crispness benefit users get from the cropped stream.

This is runtime-specific behaviour, not an OpenXR-spec guarantee. It changes the previous
runtime hypothesis from unproven to confirmed: ViewLab cannot preserve existing overlays in the
black void **while retaining VDXR's reduced-FOV single-stream optimisation**.

### VDXR developer follow-up — ViewLab-controlled full-FOV fallback (2026-07-12)

The developer clarified that ViewLab could implement route 2 itself: report the narrowed FOV to
the game so it renders the small central projection, then create/submit a **full-FOV final
projection** to VDXR and place the third-party quad layers after it. This is not a simple
copy/scale-with-black-bars operation. It requires a **FOV-reprojection shader**: for each output
pixel in ViewLab's full-FOV target, map its ray through the app-submitted eye pose/FOV into the
cropped source projection, sample it only when that ray lies inside the source FOV, and clear the
remaining target pixels black. ViewLab then submits the target with the original full FOV.

Merely replacing a submitted projection view's FOV while leaving its smaller game swapchain
untouched would stretch the game image across the full field; it is not the required black-bar
composition. Nor is it sufficient to use a screen-space copy: asymmetric FOVs, split crop/pitch
compensation, and per-eye poses require tangent-space reprojection.

That design can preserve the game's GPU rendering reduction, but **not** the reduced stream's
pixel density or bandwidth use: VD must encode and transmit the full-FOV target, including black
regions. The result is therefore technically valid but counter to ViewLab's primary streaming
quality objective. It remains shelved, rather than impossible.

The developer also confirmed that Virtual Desktop's own performance overlay is rendered on the
headset, so it is not evidence of a PC-to-headset separately streamed OpenXR overlay path. Their
comparison to the Quad Views API layer is architectural only: it likewise combines smaller
game-rendered views into a single full-FOV submission.

Finally, the developer confirmed that ViewLab's current approach is semantically leveraging the
runtime's mutable-FOV behaviour: it changes the FOV returned by `xrLocateViews`, and VDXR honours
that. Current code does not use `fovMutable` as a gate—it queries the property only for a verbose
diagnostic and still changes FOV/recommended size when false—but the resulting crop is not
portable merely because the layer performs the mutation. The developer specifically expects it
not to work with Quest Link. `fovMutable` alone cannot implement the full-FOV fallback; the
required work is the reprojection target, shader, and `xrEndFrame` layer rewrite.

### Implementation gate for the shelved full-FOV mode

Do not start this as an incremental extension of the Direct-C visor. It is a separate
composition/reprojection subsystem and needs all of the following designed and tested together:

1. Preserve the original per-eye `XrView` pose/FOV before ViewLab returns the cropped values to
   the game, keyed to the same session/display time as the later submitted projection.
2. At game swapchain release—while the game texture is still safely available—reproject the
   app-submitted cropped image into ViewLab-owned full-size per-eye target(s), using the submitted
   projection pose/FOV and original display FOV. Do not write after release.
3. At `xrEndFrame`, replace only the relevant game projection layer/views with ViewLab-owned
   full-FOV views, preserving order and forwarding unrelated layers untouched. The layer stack
   must still allow lower API layers to append their overlays.
4. Carry projection `next` chains deliberately. In particular, depth submission must be either
   reprojected consistently or withheld only under an explicit, tested compatibility policy;
   VDXR ignores depth, but other runtimes may not.
5. Handle stereo/asymmetric FOV, array slices, MSAA/resolve, swapchain lifecycle, session loss,
   and timing without adding a GPU/CPU stall. Validate on VDXR first, then a non-VD runtime before
   calling it portable.

Estimated scope: high-risk, multi-pass renderer work—not a small diagnostic toggle. It should
remain shelved until the user deliberately prioritises overlay compatibility over encoded-stream
pixel density and accepts headset testing on each target runtime.

## 4. API-layer ordering: what is known and what is not

The loader constructs a chain. The upper layer receives an `xrEndFrame` call first and forwards
it to the next layer; a lower layer sees the amended call. Thus:

```text
game → upper API layer(s) → ViewLab → lower API layer(s) → VDXR
```

If ViewLab is **below** OpenKneeboard/RaceLab, it can log their appended layers. If it is
**above** them, it sees the game list only, but the lower overlay layer may append afterwards;
ViewLab's log cannot prove that overlay is absent from the runtime's final list. Calling
`nextXrEndFrame` is therefore necessary but insufficient to observe the whole chain from above.

The current x64 registry has manifests for OpenKneeboard, RaceLab, ReShade, XRFrameTools,
OpenXR Toolkit, OBSMirror, VD compatibility, and ViewLab. At inspection time only ViewLab's value
was `0` (loaded); OpenKneeboard and RaceLab were `1` (disabled). Therefore the present machine
cannot yet reveal their layer types or relative live order. Registry enumeration order is not a
safe ordering contract. Capture the actual chain using loader debug output and a lower-chain
`XrApiLayerNextInfo` log during a controlled run. The loader defines topmost as closest to the
application and requires layers to forward calls. Source: [OpenXR loader ordering](https://registry.khronos.org/OpenXR/specs/1.1/loader.html#overall-api-layer-ordering).

OpenKneeboard additionally warns that it is generally fine for it to be closer to the runtime
than a modifying layer; forcing a pose-manipulation layer between game and OpenKneeboard is a
design smell. Do not try to change installation order as a product mechanism.

## 5. Removed creepy-face probe: audit record

`EnsureVoidQuad` creates a D3D11 `R8G8B8A8_UNORM`, 256×256, one-sample, one-face, one-array,
one-mip colour-attachment swapchain; enumerates images; acquires/waits/updates/releases every
image once; creates a `VIEW` space; and appends a BOTH-eye 0.28 m square at `(0, +0.72, -1)`.
Its identity orientation and negative Z are correct for a forward-facing VIEW-space quad. The
sub-image rectangle is full 256×256 with array index zero, and the default flags make its opaque
pixels opaque. It is appended after all layers ViewLab can see.

Likely defects/limitations, in priority order:

1. **Not a controlled test.** There is only one elevated quad, no centred control, no crop
   matrix, no before-projection placement, and no frame/result observations. Its visibility says
   little about the requested question.
2. **Format is assumed, not negotiated.** `DXGI_FORMAT_R8G8B8A8_UNORM` is not selected from
   `xrEnumerateSwapchainFormats`; `nextXrEnumerateSwapchainFormats` is available but unused here.
   Creation failure is swallowed into `false` with no `XrResult` log.
3. **Failure reporting is almost absent.** Reference-space, swapchain, enumerate, acquire, wait,
   release, D3D `UpdateSubresource`, and amended `xrEndFrame` results are not logged. The final
   forward result is returned without logging.
4. **The texture is not refreshed each submitted frame.** Static content is valid only where the
   runtime allows it, but an overlay/API layer can require per-frame acquire/wait/release. The
   historic code's one-off upload is an avoidable variable.
5. **No explicit alpha experiment.** Opaque face data is suitable for existence testing, but it
   cannot separate alpha/blend/ordering behaviour. The no-alpha layer flags are correct for its
   opaque pixels, not a transparent-overlay test.
6. **No max-layer check.** `layerCount < 16` is an arbitrary conservative cap, not the runtime's
   `XrSystemGraphicsProperties::maxLayerCount`; the test can silently skip a valid frame.
7. **Lifetime is only partly robust.** Session destruction cleans its handles, but errors do not
   log which handle/result failed, and it has no deliberate session-loss/recreation test. Its
   D3D11 context is borrowed from the game binding and safe only while renderer/session locking
   remains correct.

The acquire/wait/release ordering, reference space, pose sign, physical size, image rect, and
append-after-visible-layers logic were plausible. It was never headset-validated, and has been
removed rather than expanded.

## 6. Ranked hypotheses for the reported clipping

1. **Confirmed on VDXR: PC-side final composition is encoded as one reduced-FOV stream.**
   Third-party layers can enter `xrEndFrame` independently but are flattened before streaming;
   they have nowhere to appear outside that stream's FOV.
2. **The overlay is not currently a native OpenXR layer in that scenario.** OpenKneeboard/RaceLab
   are disabled in the observed registry state, and legacy RaceLab may use OpenVR or a
   desktop-capture route. No independent layer exists even before VDXR composition.
3. **ViewLab is above the overlay layer.** The overlay is appended later, so ViewLab cannot see it
   in its pre-forward list. This affects diagnostics, not the confirmed final-stream constraint.
4. **The overlay is a projection layer or writes into the game texture.** It then inherits the
   cropped game FOV/resolution before composition as well.
5. **Probe defect/configuration.** Unsupported assumed format, lack of update, placement outside
   the useful viewing angle, layer limit, or an unlogged `xrEndFrame` error can all make the face
   disappear without explaining third-party layers.

## 7. Smallest useful instrumentation

Keep it debug-only and default-off; use a rate-limited structural signature so an unchanged frame
logs once, and log again only when count/type/order/critical fields change. Do not retain or
modify third-party layer memory.

- At `XRViewLab_xrEndFrame`, emit: session, display time, `layerCount`; for every layer: index,
  `type`, `layerFlags`, `space` handle; projection `viewCount`, each FOV and sub-image
  swapchain/imageRect/array index; quad pose, size, eye visibility, and sub-image fields.
- Include the downstream `xrEndFrame` result and a `viewlab-layer-signature` hash. Add a hard cap
  on lines and an explicit config/debug environment gate.
- In `XRViewLab_xrCreateApiLayerInstance`, log the lower `XrApiLayerNextInfo` names; combine it
  with `XR_LOADER_DEBUG=info` capture for the complete resolved order. It cannot report layers
  above ViewLab from the next-info chain alone.
- Upgrade only the test harness diagnostics: enumerate formats and select an advertised RGBA
  format; log every XR/D3D failure and `maxLayerCount`; acquire/wait/release once per frame;
  label each control quad distinctly. This is one isolated diagnostic patch, not an overlay
  rewriter.

## 8. Staged headset experiment plan

Record runtime name/version, enabled manifest values, loader order, ViewLab log, and a headset
result for every row. A blank result means “this row failed”, not “the concept failed”.

| Stage | Crop | Test layers / ordering | Purpose |
|---|---|---|---|
| A | Off | ViewLab centred control quad | Prove format, graphics binding, swapchain lifecycle, forward pose and visibility. |
| B | Off | Same quad above centre | Isolate spatial placement from crop. |
| C | Mild | Two labelled ViewLab quads: one inside the projection slice and one above | Confirm VDXR's PC-side reduced-stream constraint visually. |
| D | Extreme normal sim-racing | Same two quads | Reproduce the actual condition after controls establish a baseline. |
| E | D | Submit the diagnostic quad before, then after, the projection layer where the list is legal | Separate painter-order/occlusion from FOV clipping. Never reorder third-party layers. |
| F | D | Repeat with `VIEW`, then `LOCAL` reference space | Identify pose/space interpretation issues. |
| G | D | Enable OpenKneeboard; capture ViewLab/loader logs and its overlay inside/outside slice | Verify actual layer entries and ViewLab visibility. |
| H | D | Enable RaceLab VR 3; repeat G | Determine its actual layer type and visibility independently. |

For C–H, use a per-frame refreshed opaque control first. Repeat a transparent-alpha version only
after opaque controls work. Test one variable at a time; preserve the resulting logs verbatim.

## 9. Candidate implementations after evidence exists

| Approach | When justified | Risk / complexity |
|---|---|---|
| Leave third-party layers untouched | Using a runtime that independently presents them, or no FOV crop | Ideal; no implementation. On VDXR with reduced FOV, it cannot put them in the void. |
| Full-FOV composed output with black bars | Overlay visibility outweighs VD encoder-resolution efficiency | High; ViewLab would own a full-sized target, clear/copy the cropped game projection into it, submit it with original FOV, and pass later overlay layers after it. It can retain game GPU savings but sacrifices stream pixel density/bandwidth. This is a new product mode, not a small overlay fix. |
| Separate-layer streaming | VD/another streamer explicitly supports it | Not currently available according to the VDXR developer; outside ViewLab's control. |
| Fix ViewLab's own crop so it affects only game projection calls | Evidence shows ViewLab accidentally mutates a third-party call | Medium; useful for correctness, but cannot defeat VDXR's final single-stream FOV. |
| Submit a ViewLab-owned independent layer | Needed only for ViewLab content, not to rescue third-party overlays on VDXR | Medium; robust per-frame swapchain and ordering handling. |
| Transform/copy third-party layer structs in-flight | Only after type, order, and runtime behaviour prove a narrowly safe need | High; pointers remain owned by caller for the call, `next` chains/layer-specific extensions must survive, swapchains/spaces are not owned by ViewLab, and changing an upper layer cannot change layers appended below it. Copying a struct does not copy or make safe its swapchain. |
| Duplicate third-party content/swapchains | Only with explicit producer cooperation | Very high / generally unsuitable: ViewLab has neither producer rendering ownership nor a safe way to acquire another layer's swapchain. |
| Runtime/VD-specific workaround | Only after controls prove compositor clipping | High and fragile; requires VDXR/VD developer confirmation. |

## 10. Unknowns requiring headset evidence or a VDXR answer

- The final, runtime-visible list for OpenKneeboard and RaceLab in the target game.
- Whether each is above or below ViewLab, and whether ViewLab sees it before forwarding.
- Whether a future VDXR/Virtual Desktop release adds a separately transported overlay path or an
  opt-in full-FOV composition mode.
- RaceLab's exact layer type(s), flags, reference spaces, and whether it uses a different route in
  the affected configuration.

Until those are measured, no conclusion of impossibility—or success—is warranted. Small black
holes in an otherwise elaborate theory are, regrettably, still holes.
