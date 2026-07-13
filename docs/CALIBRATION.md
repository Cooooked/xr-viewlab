# ViewLab calibration patterns

All patterns are default-off and draw into the submitted eye image. Pixel-measurement tools use
literal texture pixels and the full submitted eye rectangle; perceptual shapes use the shared
straight-ahead tangent centre and project separately through each eye so they remain binocularly
coherent. Inspect pixel tools in a lossless capture at 100% zoom; inspect perceptual shapes fused
in-headset with one eye closed in turn.

## 64 px texture grid

- **Purpose:** expose resampling, non-uniform scaling, geometric distortion, and texture-edge damage.
- **Coordinates:** intersections originate at the fused crosshair centre; spacing is always 64 submitted-texture pixels.
- **Expected:** straight parallel lines, square 64 x 64 texture cells, and a major line through the common centre.
- **A failure suggests:** rectangular captured cells mean downstream scaling; curved or uneven cells mean warp or distortion.
- **Verify:** capture at native size, count exactly 64 pixels between lines, then check the centre intersection in-headset.

## Pixel ruler

- **Purpose:** provide an exact horizontal scale for measuring crop, blur radius, and captured feature size.
- **Coordinates:** fixed 1/8/32/64-pixel ticks along the bottom edge of the submitted eye rectangle.
- **Expected:** uniform tick spacing with crisp one-pixel minor marks and no crop-percentage rescaling.
- **A failure suggests:** missing edge ticks indicate clipping; changed spacing indicates a resize before submission or capture.
- **Verify:** inspect at 100% capture zoom and compare the 64-pixel ticks with the texture grid.

## Resolution gratings

- **Purpose:** show where one-, two-, and four-pixel alternating detail stops resolving cleanly.
- **Coordinates:** literal submitted-texture pixels, repeated in horizontal bands across the available eye image.
- **Expected:** stable alternating bands; one-pixel detail may soften downstream but two/four-pixel bands should remain distinct.
- **A failure suggests:** premature grey blending indicates filtering, encoding loss, foveation, or an unintended resize.
- **Verify:** compare centre and edge regions in a 100% capture and note the finest pitch that remains separable.

## Colour bars and grey ramp

- **Purpose:** reveal hue shifts, chroma loss, banding, incorrect transfer functions, and alpha-composition errors.
- **Coordinates:** full-width proportional bars; colour values themselves are exact shader output values.
- **Expected:** saturated primaries/secondaries and a monotonic neutral ramp. Calibration remains on the submitted game texture regardless of common-scene backend choice.
- **A failure suggests:** pale colours indicate blend/alpha mishandling; tinted greys indicate colour-space conversion damage.
- **Verify:** compare captures before and after backend demand; the calibration pixels must remain identical.

## Frame beacon

- **Purpose:** identify submitted-frame cadence, frame reuse, interpolation, and synthetic-frame behaviour.
- **Coordinates:** fixed pixel-size square at the submitted eye rectangle's upper-left edge.
- **Expected:** alternates black/white once for each captured projection frame and remains pinned to the edge.
- **A failure suggests:** repeated states indicate frame reuse; intermediate states or trails suggest synthesis or temporal blending.
- **Verify:** record high-frame-rate video and compare beacon transitions with ViewLab frame-timing telemetry.

## Edge probes

- **Purpose:** measure blur, fixed foveation, clipping, and compression immediately at each usable texture edge.
- **Coordinates:** literal 1/2/4-pixel probes touch all four bounds of the submitted eye rectangle.
- **Expected:** probes sit at the intended edges, even when partly outside the headset's visible lens region.
- **A failure suggests:** symmetric inset means the tool used overlap/safe bounds; asymmetric loss can identify edge processing.
- **Verify:** use a 100% capture for exact edge contact, then compare visibility in-headset without repositioning them.

## Checkerboard ladder

- **Purpose:** expose scaling blur, moire, chroma contamination, and resolution loss by spatial frequency.
- **Coordinates:** literal 1/2/4/8-pixel square texels; crop changes only how much of the ladder is available.
- **Expected:** geometrically square checks in the submitted texture, with progressively easier-to-resolve larger checks.
- **A failure suggests:** rectangular captured checks mean non-uniform scaling; grey or coloured checks indicate filtering/encoding.
- **Verify:** inspect at 100% capture zoom and compare each labelled pitch at the centre and toward the edges.

## Radial spokes / zone plate

- **Purpose:** reveal angular anisotropy, uneven sharpness, aliasing, and direction-dependent reconstruction.
- **Coordinates:** centred at shared tangent `(0,0)` and constructed as circles/rays in tangent space per eye.
- **Expected:** one fused circle centred with the crosshair, circular rather than oval, with evenly spaced radial spokes.
- **A failure suggests:** an egg shape indicates pixel-space aspect error; double centres indicate broken binocular projection.
- **Verify:** close each eye in turn to confirm a common perceived centre, then judge circularity with both eyes fused.

## Black / white clipping steps

- **Purpose:** expose crushed shadow detail, clipped highlights, limited-range conversion, and contrast expansion.
- **Coordinates:** proportional full-width patches carrying fixed near-black and near-white linear values.
- **Expected:** adjacent steps remain ordered; the closest extremes may be subtle but must not reverse or acquire colour.
- **A failure suggests:** merged black/white patches indicate clipping; coloured steps indicate transfer or chroma contamination.
- **Verify:** disable dynamic display enhancements where possible and compare direct/topmost captures under identical settings.

## Motion strip

- **Purpose:** reveal temporal smearing, encoder trails, frame interpolation, and scan-direction artefacts.
- **Coordinates:** fixed-pixel marker moves through a full-width edge-anchored track at a deterministic frame rate.
- **Expected:** one crisp moving marker with predictable travel and no persistent copies behind or ahead of it.
- **A failure suggests:** trails indicate temporal filtering; duplicated markers indicate frame synthesis or repeated composition.
- **Verify:** record at high frame rate, step frame-by-frame, and correlate marker displacement with the frame beacon.
