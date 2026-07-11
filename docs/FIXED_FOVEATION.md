# Fixed Foveation — what the "edge smear" actually is

> Plain-language summary of the 2026-07-11 investigation. Full evidence trail:
> [EDGE_SMEAR_INVESTIGATION.md](EDGE_SMEAR_INVESTIGATION.md).

## The one-paragraph version

Virtual Desktop compresses the video stream with **fixed foveated encoding**: the center of the
view gets high quality, and quality falls off toward the edges. This is baked into VD's streamer,
runs on every codec, cannot be turned off, and is unaffected by bitrate (it is a *ratio*, not a
budget — 500 Mbps just buys a more pristine center). Normal games never expose it because the
low-quality zone sits in peripheral vision. ViewLab's crop puts the image's outer boundaries deep
into that zone and draws a frame around them, so the user looks directly at the degradation.
That is the "edge smear." It was never ViewLab, never the game, never the codec, never the Quest.

## How we know (evidence, all reproducible)

1. **Ruler test.** ViewLab drew a grid of thin lines exactly 64 px apart into the eye texture and
   simultaneously saved that exact submitted frame to disk. Disk file: perfectly uniform, crisp to
   the edge. Headset capture of the same frame: line **spacing still perfectly uniform** (so no
   geometric warp anywhere in the chain), but line **contrast collapses toward the boundary** —
   full strength at center, ~20%-strength smudges near the edge. Detail is destroyed in place,
   not moved.
2. **Codec/bitrate immunity.** Identical on H.264+ and AV1; identical at 500 Mbps; SSW off.
3. **Everything upstream exonerated.** Submission metadata exact (FOV, imageRect, formats);
   submitted pixels proven clean by the dump; VDXR verified as straight passthrough (its own log:
   upscaling 1.000, sharpening off).
4. **Vendor confirmation.** VD support staff confirmed in the official Discord:
   "the center has higher bitrate than the edges", "the more you reduce your tangents the more
   noticeable fixed foveation will be", "FFR has never been modifiable."

## VDXR boundary audit (2026-07-12)

**Verdict: the fixed-foveated quality allocation is in proprietary Virtual Desktop after VDXR,
not in the open-source VDXR runtime (~95% confidence, two independent audits concurring).**
This is an ownership conclusion, not a claim that the closed source identifies the precise
PC-versus-headset implementation stage. Audited against the local clone
`C:\Users\strif\VirtualDesktop-OpenXR` at commit `051774f` — the exact installed build
(VDXR's own log: "1.0.10 (051774f3)").

VDXR 1.0.10 (`051774f3`, `version.info` = 1.0.10) turns each submitted OpenXR projection view
into an `ovrLayerEyeFov`: texture/slice, viewport, pose, and FOV tangents
([`frame.cpp`, `OpenXrRuntime::handleProjectionLayer`, lines 665-702](https://github.com/mbucchia/VirtualDesktop-OpenXR/blob/051774f3d89c131312963877cdf53584ddca98b2/virtualdesktop-openxr/frame.cpp#L665-L702)).
It then calls `ovr_EndFrame` from `OpenXrRuntime::xrEndFrame`
([`frame.cpp`, lines 518-544](https://github.com/mbucchia/VirtualDesktop-OpenXR/blob/051774f3d89c131312963877cdf53584ddca98b2/virtualdesktop-openxr/frame.cpp#L518-L544)).
That LibOVR/OVR C API call is the exact open-source-to-proprietary boundary: VDXR source ends;
Virtual Desktop's compositor/streamer implementation begins.

- The complete VDXR 1.0.10 source tree has no NVENC, AMD AMF, Intel encoder, ROI, QP,
  quantisation, bitrate-allocation, or foveation-map code or dependencies. VDXR does **not**
  submit frames to an encoder API.
- Its only optional primary-projection image transforms are FSR EASU upscaling and CAS sharpening
  ([`precompositor.cpp`, `OpenXrRuntime::upscaler`](https://github.com/mbucchia/VirtualDesktop-OpenXR/blob/051774f3d89c131312963877cdf53584ddca98b2/virtualdesktop-openxr/precompositor.cpp#L61-L244)).
  The investigated session had 1.000 upscaling and sharpening off, so that path was inactive.
- With those transforms disabled, VDXR passes the released projection texture onward unchanged in
  image content. It uses the LibOVR swapchain directly where possible; it performs only a D3D11
  copy/resolve when required for array-slice or MSAA compatibility
  ([`d3d11_native.cpp`, `OpenXrRuntime::resolveSwapchainImage`](https://github.com/mbucchia/VirtualDesktop-OpenXR/blob/051774f3d89c131312963877cdf53584ddca98b2/virtualdesktop-openxr/d3d11_native.cpp#L515-L595)).
- **The physical boundary:** `ovr_EndFrame` does not call Meta's LibOVR. VDXR's `OVR_CAPIShim.c`
  loads the DLL named by prefix `<Streamer path>\VirtualDesktop.` (registry-resolved in
  `system.cpp:308-330`) → **`C:\Program Files\Virtual Desktop Streamer\VirtualDesktop.LibOVRRT64_1.dll`**
  (closed source, verified on disk). The encoder components live next to it, all Streamer-side:
  `libVirtualDesktopAMF.dll` (AMD encoding) and the ffmpeg DLLs. The swapchain textures are
  themselves created inside that DLL (`ovr_CreateTextureSwapChainDX`), so the game renders
  directly into VD-owned surfaces; `ovr_EndFrame` is the submission signal, not a pixel copy.
- **Nothing quality-shaped crosses the boundary:** the only settings VDXR sends into proprietary
  code are `IsVDXR`, `IsOpenComposite`, and `AppGpuTime`. No foveation, ROI, QP, or quality map
  exists anywhere in the VDXR tree to disable.

Therefore editing VDXR could alter submitted pixels or disable VDXR's own FSR/CAS, but **cannot
turn fixed foveation off**. That needs a change or control in proprietary Virtual Desktop after
the `ovr_EndFrame` handoff. The source audit and the support attribution are independent evidence:
the former rules VDXR out; the latter attributes centre-weighted bitrate to VD. The Discord
support transcript (`smear.txt`) is not in the repository but exists locally at
`C:\Users\strif\Documents\smear.txt`; further ownership quotes from it: *"your mask isnt changing
the actual stream though, that's all handled on streamer/hmd side"* and *"vdxr is indeed open
source / vd is not / … VD controls the stream"*. Remaining uncertainty is only about which closed
stage (PC Streamer encode vs. device-side) implements the falloff — support's *"the tangent
changes the stream on the device"* leaves that ambiguous.

### VDXR boundary: PE exports and official release notes (2026-07-12)

The physical-boundary conclusion above is further locked down by the actual PE export table and
official Virtual Desktop release notes:

- **Export-table confirmation.** `dumpbin /exports` on `C:\Program Files\Virtual Desktop Streamer\VirtualDesktop.LibOVRRT64_1.dll` lists the exact OVR interface entries VDXR calls, including `ovr_BeginFrame`, `ovr_CommitTextureSwapChain`, `ovr_CreateTextureSwapChainDX`, `ovr_EndFrame`, `ovr_GetTextureSwapChainBufferDX`, `ovr_GetTextureSwapChainCurrentIndex`, `ovr_GetTextureSwapChainLength`, `ovr_WaitToBeginFrame`, and legacy `ovr_SubmitFrame`/`ovr_SubmitFrame2`. This confirms `ovr_EndFrame` (the last VDXR instruction) is resolved into the proprietary DLL.
- **No encoder imports in the boundary DLL.** `dumpbin /imports` on `VirtualDesktop.LibOVRRT64_1.dll` shows only `dxgi.dll` and `d3d11.dll`. It does not import `avcodec`, `libVirtualDesktopAMF`, NVENC, or Media Foundation. The encoder components (`libVirtualDesktopAMF.dll`, `avcodec-61.dll`, etc.) are separate Streamer-side files.
- **Official Virtual Desktop release notes explicitly describe bitrate fixed foveation as a product feature.** `guygodin/VirtualDesktop` v1.34.2 release notes state: “Added **bitrate fixed foveation** on 10-bit codecs with Nvidia GPUs (previously was only with H.264/H.264+)” (https://github.com/guygodin/VirtualDesktop/releases/tag/v1.34.2). `guygodin/VirtualDesktop` v1.34.16 release notes state: “Added **Foveated streaming** with eye tracked headsets (Quest Pro, Galaxy XR, ...). This uses eye tracking to improve the quality of the image where you are looking” and “Added **adaptive quantization** support with AMD GPUs using H.264/H.264+” (https://github.com/guygodin/VirtualDesktop/releases/tag/v1.34.16). These are published by Virtual Desktop, are independent of the Discord support transcript, and place the quality-allocation feature in the proprietary streamer/encoder.
- **Running process ownership.** At runtime the relevant components are `VirtualDesktop.Service` and `VirtualDesktop.Streamer.exe` (in `C:\Program Files\Virtual Desktop Streamer`). VDXR's layer DLL does not host or link the encoder.

## What is characteristic about it

- Strong, high-contrast edges (like the visor's black border) **survive** the compression.
- Low-amplitude fine detail (thin lines, text, distant texture) **dies**, progressively with
  distance from center.
- Fine detail loss reads as horizontal "stretching"/"smearing" of letters and texture.
- Deeper crop = boundaries at higher eccentricity = worse smear at the boundary.

## Separate bug we found and fixed along the way

The **terminal smear** — a wet-paint streak right at the boundary — was a different mechanism:
the compositor samples past the physical texture edge and clamps, streaking whatever pixels sit
on the outermost row/column. An 8 px black edge-guard frame sharpened the boundary in screenshot
scanline measurements (~63 px → ~3 px at the measured line), **but the user reports no noticeable
in-headset difference** — the dominant visible degradation was always the foveation falloff, not
the terminal clamp. **The guard was removed 2026-07-11 (late)** along with all other experimental
crop-fix code, at the user's request to return to a clean base. The mask-inset plan below covers the same boundary: with the visor
opening pulled inside the crop edge, the visor's own black occupies the outermost texels and
provides the identical protection.

## Go-forward plan

1. **Mask inset (the practical fix, available now).** Black mask edges survive encoding cleanly,
   so pull the visor opening inward to cover the worst ~50 px of the degraded band. Existing
   config knobs: `mask_horizontal` / `mask_vertical` (fractions of the cropped view). GPU savings
   are unchanged — the game renders the same reduced image; the ugliest strip is simply hidden
   behind black. The visible boundary becomes a crisp visor edge.
2. **Feature request (on record, low expectations).** Ask VD for any control over the fixed
   foveation curve, attaching the ruler evidence pair. Support staff have already said it is
   unlikely to happen.
3. **Not pursuing:** ALVR fork, custom streamer, encoder rewrite, pre-emphasis sharpening pass.

## Practical limits to remember

- No ViewLab code change can restore detail the encoder discards; the fix budget is
  masking + boundary placement.
- The falloff is anchored to the display center. Shallower crops keep boundaries in the
  high-quality zone; deeper crops pay more smear. This is now a documented product constraint,
  not an open bug.
