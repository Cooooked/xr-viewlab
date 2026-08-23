# ViewLab attention policy

ViewLab uses a few fixed visual channels rather than a general scheduler.

| Channel | Content | Rule |
|---|---|---|
| Spatial edge | Spotter left/right/both | Immediate; never delayed by cards |
| Visor border | Safety-relevant flags | Immediate; may coexist with spotter |
| Compact performance | HUD/trace alarms | Independent; restrained size and existing hold/fade |
| Transient cards | Lap results and desktop notifications | Lap results remain prompt; desktop cards may wait |

While a spotter cue or blue/yellow/debris/red/black/disqualification flag is active, newly arriving
desktop cards stay off-screen. If the safety state clears within five seconds, the card begins its
normal arrival/hold/leave animation then. At five seconds it is stale and discarded. Existing and
queued card counts remain bounded. Lap cards are racing information and do not wait behind their own
safety channel, but must use the established bottom-right card area rather than the peripheral edge.

Desktop notification permission remains global. Racing integrations never enable it. Message bodies
remain governed by the selected privacy mode and are not written to ordinary logs/history. Spatial
cues and flag borders carry no desktop text. Repeated trouble extends the existing state; unchanged
telemetry does not replay animations.
