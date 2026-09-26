# ViewLab feature requests

This file records the feature requests discussed with the user on 2026-09-26. It captures the desired outcomes, not a design or implementation plan. The next AI should inspect the current code and repository guidance, decide how best to deliver the requests, and ask only when a product decision cannot reasonably be inferred.

## iRacing presentation tests

- Remove the generic presentation-test section at the bottom of the iRacing settings that contains Left, Right, Both, Clear, Lap, Yellow, and Blue.
- Put a relevant test control with each iRacing telemetry feature, so the user can test its visual presentation without joining a live session.
- Add tests for the applicable features discussed: lap pop-up, peripheral spotter, flag border, race-start light, rear-closing pressure cue, rhythm shift light, low-fuel warning card, and Grip-O-Bar if that feature remains available.
- A test should show the actual on-screen presentation of that feature. It should not silently turn the production feature on or change its saved settings.
- For cues that remain displayed, the user expects to press the test button again to dismiss the test. For transient cards, a single press can show the card for an appropriate duration. The controls should make their behavior apparent.

## Performance HUD

- Give Performance HUD a visibility choice equivalent to the visibility choice already available for Performance Trace, including an alarm-only mode.
- Keep any existing “alarm-only symbols” option distinct if it controls which symbols are shown rather than whether the HUD itself is visible.
- Make the behavior available in the relevant global and per-app settings.

## Clock modes and race timer

- Make the clock more like a general clock app, with useful modes such as current time, stopwatch, countdown timer, and alarm or target-time alarm.
- Let the user configure a timer before entering a game—for example, a 15-minute countdown or an alarm for a particular time—and see it with the clock when that app’s clock overlay is enabled.
- Provide the expected timer controls and clear feedback as a countdown approaches its end. The user suggested a color that moves toward red and a countdown shown beneath the clock.

## Spotter proximity indication

- Improve the left/right proximity indication so it can communicate a nearby car’s closeness or urgency, with a softer near/amber indication and a stronger close/red indication where the available data supports it.
- Preserve useful left/right information and avoid presenting invented precision if the telemetry does not provide reliable proximity data.

