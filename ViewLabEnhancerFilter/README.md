# ViewLab Enhancer — OBS Studio filter plugin

ViewLab Enhancer is the lightweight image-adjustment filter. It applies Sharpness, Saturation,
Vibrance, Contrast, Brightness and Gamma in one GPU pass. Neutral values are a true passthrough.

Stabilization is intentionally absent. Motion tracking, crop and frame delay belong to the separate
**ViewLab Stabilizer** plugin, which ports LiveVisionKit's buffered OpenCV implementation.

The stable OBS source ID remains `viewlab_enhancer`, so existing scenes and enhancement settings
continue to load after replacing the former combined plugin. The binary is `viewlab-enhancer.dll`.

Build with `ViewLabEnhancerFilter.vcxproj` for x64 Release. The module resolves libobs functions at
runtime from OBS and has no external dependencies.

Licence: GPL-2.0-or-later; see `LICENSE`.
