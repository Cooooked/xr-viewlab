# Topmost root cause and correction

## Faulty path

`XRViewLab_xrEndFrame` called `RenderTopmostLayer`, which called `EnsureTopmostLayer` using the
current projection `imageRect` dimensions. Any size change destroyed the existing Topmost swapchain
and created another. A one-pixel 1763/1764 height oscillation therefore caused continuous large
allocation churn. When RTV/swapchain creation failed, the old implementation returned to the next
frame without a persistent failure latch, producing 193 more allocation attempts.

The RTV path also requested a non-sRGB view of the Topmost resource. That reinterpretation is only
legal for a typeless allocation; a runtime may provide a strongly typed sRGB resource. The alpha
target was produced by a source-alpha blend (premultiplied RGB in a transparent target) but marked
as unpremultiplied for compositor submission, causing a second alpha multiplication and washed-out
overlay colours.

## Why the transition exposed it

DiRT Rally changes submitted projection rectangles between menus and the car. ViewLab incorrectly
treated those rectangles as allocation dimensions rather than subregions of a stable game texture.
The transition therefore exercised repeated destruction/allocation on the game's D3D11 device at
the exact point where the renderer and runtime were changing state.

## Correction

- A Topmost allocation is attempted at most once per OpenXR session.
- Capacity is derived from the underlying tracked game texture where available, not frame-to-frame
  `imageRect` jitter. Smaller rectangles reuse the allocation.
- A larger/incompatible transition, any OpenXR failure, or any RTV failure permanently blocks
  Topmost for that session and restores the ordinary direct renderer on the next frame.
- Checkbox cycling cannot re-arm a failed session.
- Failure does not destroy/recreate compositor resources on the render path. Dormant resources are
  released once during normal session teardown.
- `GetDeviceRemovedReason()` gates both render paths. Once failed, ViewLab performs no further D3D
  rendering during that session.
- Topmost creates RTVs with the runtime resource's typed format and submits the blended target as
  premultiplied alpha.

These rules bound failure to one attempt and prefer a missing overlay over graphics-stack churn.
