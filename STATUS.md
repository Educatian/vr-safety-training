# Status: Competent Person v2 (revamp/v2)

Updated 2026-09-29.

## Playable
- Five episodes (EP1-EP5). Each has a cold open, gate check-in, hierarchy ranking, toolbox quiz, a 10-minute shift, a closing quiz and a toolbox talk.
- Five episodes (Mon-Fri) run on one evolving site, with weather calls and heat strain.

## Serious-game loop (design review 2026-09-29)
- **Answer leaks closed:**
  - Neutral pre-report names.
  - Zone-level mission cues (cued finds count half).
  - The same photo prompt on every surface.
  - Instrument readings as raw values with a deliberate measure action.
- **Exploits closed:**
  - Repeat stops earn nothing.
  - Ending early forfeits Zero Recordables.
  - A chosen fix counts only once it is installed.
  - False alarms cost time.
  - A "checked · compliant" action gives positive discrimination evidence.
- **New mechanics:**
  - Toolbox-talk writer.
  - Ray speak-up exchange.
  - Near-miss stop-down card.
  - Kit choice for engineered controls.
  - Elimination (remove from service).
  - Schedule slip and the On Schedule commendation.
  - Crew self-report at high trust.
  - Streaks earn hint tokens.
  - Good talks bank a token.
- **Assessment:**
  - Per-area CP mastery saved across episodes.
  - EP5 mastery gate (facilitator override in settings).
  - Branching capstone epilogue.
  - Debrief grouped by outcome, with chosen vs. best control.
- **Research:** play events leave the device only after research opt-in. Completion codes always work.

## Verification
- All five assemblies compile against the Unity 6000.3.25f1 references.
- EditMode suite: 54 tests passing.
- PlayMode suite: must be run in the Unity editor (muted). It saves per-episode screenshots to `Captures/t_full` and `Captures/t_show`.

## Open (needs Unity, assets or an SME)
- Rebuild WebGL and redeploy.
- Add hazards to EP3 (density).
- EN/ES localization.
- Week 2 transfer project and 4-week booster.
- OpenXR VR build of v2.
- SME review of quiz items and the kit distractors.
- Research-grade pre/post items (HSEA-aligned).
