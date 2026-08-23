# ViewLab Stabilizer — OBS Studio filter plugin

ViewLab Stabilizer is a focused port of the LiveVisionKit 1.2.2 video-stabilization filter.
It is separate from ViewLab Enhancer: this module performs motion stabilization only and
registers the OBS filter ID `viewlab_stabilizer`.

The port preserves LVK's OpenCV/OpenCL pipeline: sparse optical-flow tracking, dynamic
affine/homography motion estimation, future-frame Gaussian trajectory smoothing, buffered
stream delay, crop clamping and GPU warping through D3D11/OpenCL interop. Its smoothing radius
therefore adds deliberate video delay; OBS audio should be delayed to match when required.

## Provenance

- Upstream: LiveVisionKit 1.2.2, commit `faff156a2a8bcdb208be3b1ed33fb57cecac2e8b`.
- Upstream source is vendored under `upstream/LiveVisionKit` so releases do not depend on
  `reference/` or a network checkout.
- ViewLab modifications are limited to the focused module entry point, ViewLab naming/source
  ID and property labels, removal of unrelated LVK filters/sources, and an OBS release-ABI
  compatibility adjustment for ViewLab's SDR sRGB capture path.
- OpenCV 4.7 is bundled as `opencv_world470.dll`, matching LVK's stable Windows release.

LiveVisionKit and this derivative are GPL-3.0-or-later. See `LICENSE-LiveVisionKit.txt`.

## Build and install

Build `ViewLabStabilizerFilter.vcxproj` as x64 Release. Install the resulting
`viewlab-stabilizer.dll` and `opencv_world470.dll` into OBS's `obs-plugins/64bit` directory,
and install the three FSR effect files under
`data/obs-plugins/viewlab-stabilizer/effects`. `Install-ViewLabObsPlugins.ps1` performs the
complete split Enhancer/Stabilizer installation after OBS has closed.
