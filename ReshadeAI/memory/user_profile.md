# User Profile

## Who They Are
- VR sim racer (iRacing is primary use case)
- Experienced with ReShade, OpenXR, VR hardware (Quest 3 + Virtual Desktop / VDXR)
- Building "ViewLab" — a companion app to control this ReShade overlay system
- Uses Pistol Whip for fast iteration testing (no launcher, instant startup)

## Hardware
- Quest 3 headset
- Virtual Desktop (VDXR) — OpenXR runtime, no SteamVR
- AMD Radeon RX 7800 XT
- Quest supports 72, 80, 90, 120Hz — nothing should be hardcoded to 90Hz

## Communication Style
- Terse. Short answers, no padding.
- Action-oriented. Don't explain what you're about to do — just do it.
- Don't ask permission on standard tasks (build, read files, check logs).
- Don't re-read files or logs without then acting on them.
- Direct corrections are fine — "that's wrong" means fix it immediately.
- No trailing summaries. User can see the diff.

## What Annoys Them
- Over-explaining or narrating decisions
- Reading logs and not doing anything
- Leaving the game running between deploys
- Hardcoding values that should be dynamic (like framerate)
- Making breaking changes without a clear reason
- Asking clarifying questions on things that have obvious answers

## Workflow Preferences
- **Always kill game before deploying DLL.** DLL is locked while game runs. Build → kill → deploy → test.
- Check logs after every deploy, not before.
- When there's a bug, fix it, build, deploy — don't just describe the fix.
- Reference codebase for texture/format questions: `C:\Users\strif\Desktop\ReShadeFix`

## Project Goals (user's words)
- "Performance mode" = HMD-only, minimal overhead, done tuning
- "Tuning mode" = desktop + HMD, Effect Runtime Sync, tweaking presets
- Spends 95% of time in performance mode once presets are set
- Wants the option for both modes
- Does NOT want dxgi.dll in the game folder (VR-only setup)
