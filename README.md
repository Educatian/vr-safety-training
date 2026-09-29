# Competent Person

A serious game about construction safety. Over one week on a municipal lift-station job in Alabama, you are the new **OSHA competent person**. Each day you walk a changing site, find hazards that nobody has highlighted, and rate the risk. Then you pick and install controls, stop work when you have to, hold the line when the foreman pushes back, and brief the crew for tomorrow.

Built in Unity 6000.3.25f1 (URP). It is played in a web browser (WebGL) or as a desktop build. The design source of truth is `docs/GDD.md`.

## What the learner does (and what is scored)
| Verb | In game | Evidence |
|---|---|---|
| Recognize | Photograph any surface. The tablet shows only a neutral name until you report, so nothing gives the answer away. | HII, time to detect. Cued or hinted finds count half. |
| Classify, assess | Energy-wheel tag and a probability x severity card | Tag accuracy and deviation from the expert key, with immediate feedback |
| Discriminate | Report it, or log it "checked · compliant" (feedback on compliant calls comes in the debrief) | Precision, correct compliant confirmations |
| Inspect | Measure with a laser, GFCI tester, penetrometer or dust monitor (costs shift time). Readings are raw values, never a verdict. | Measured before the call |
| Control | Eliminate, engineer (pick the right kit, carry it, install it), assign, or PPE. Weak controls lapse later in the shift. | Chosen vs. best feasible control, lapses, install attempts |
| Escalate | Radio stop-work. Ray pushes back and you answer passively, assertively or aggressively. | Stop held or not, speak-up style, schedule slip |
| Reflect | Tomorrow's toolbox talk: pick three findings, order them, and answer the why | Selection and order vs. the risk key, why item |

Around the core loop: near-miss stop-down cards ("what almost happened", no gore), a debrief grouped by outcome with the OSHA citation, per-area CP mastery, and a Friday capstone that unlocks only at competent mastery. The capstone ending branches on how the shift actually went.

## Run it
- **Web:** https://competent-person.pages.dev/. The build and deploy steps are in `docs/Deploy.md`.
- **Editor:** open the project in Unity 6000.3.25f1 and play `Assets/_Game/Scenes/Jobsite.unity`.
- **Controls:** WASD to move, drag the mouse to look, E to photograph or interact, Tab for the tablet (F or its corner button for full view), M for the map, Esc for settings. On a phone or tablet, use the touch controls.

## Playtests
See `docs/PlaytestGuide.md` for the session script, the facilitator settings (research opt-in, unlock all episodes, visual cues) and what to observe.

## Tests
- EditMode (`Assets/_Game/Tests/EditMode`) covers the deterministic engine: hazard state machine, scoring, exploits, toolbox talk, speak-up, mastery gate and story verdict.
- PlayMode (`Assets/_Game/Tests/PlayMode`) plays every episode end to end and saves screenshots under `Captures/`. PlayMode tests always run muted.

## Design and evidence
- `docs/GDD.md`: design, evidence base, and the learning-mechanic ↔ game-mechanic map.
- `docs/DesignReview_2026-09-29.md`: the serious-game review and what has been fixed since.
- `docs/GapAudit_2026-09-28.md`: the earlier shippability audit.

Training record only. This game does not issue an OSHA 10/30 card or a competent-person designation.

Microsoft Rocketbox avatars are MIT licensed. Cite Gonzalez-Franco et al. (2020).
