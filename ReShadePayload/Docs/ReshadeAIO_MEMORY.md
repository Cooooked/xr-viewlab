# ReshadeAIO — Memory Index

Read this first, then the file you need. Sibling project `F:\AI-Projects\ReshadeAI` holds the
patched ReShade **source**; this project (`ReshadeAIO`) is the **payload + installer + handoff**.

- [opencomposite-interface-fix.md](opencomposite-interface-fix.md) — THE fix: why VR games abort with
  ReShade under OpenComposite and the two source patches that resolve it. Most important file.
- [status-and-next.md](status-and-next.md) — what works now, what's next.
- [user-profile.md](user-profile.md) — who the user is and how they want to work.

## One-line status (2026-06-30)

DiRT Rally 2.0 **launches with ReShade in-HMD** via the OpenXR layer under OpenComposite (quad menu +
desktop mirror + 7770×803 VR runtime). Patched ReShade deployed to `C:\ProgramData\ReShade\ReShade64.dll`.
**Open VR-menu bugs (DiRT):** tabs unclickable, VR runtime compiles all ~473 effects (not synced to
desktop Gameplay/Tuning gating), menu laggy — see [status-and-next.md](status-and-next.md).
**Assetto Corsa won't launch** (pre-existing, deprioritized). Pinball FX2 32-bit TDR still open.
