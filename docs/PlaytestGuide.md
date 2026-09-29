# Playtest guide: Competent Person (v2)

Purpose: run a moderated usability and learning playtest with target learners (construction management students or new site staff), not proxy users (GDD §10). One session takes about 45 minutes.

## Before the session (facilitator)
1. **Build.** Play in the browser at https://competent-person.pages.dev/, or run a local build (`docs/Deploy.md`). Use a laptop with a mouse. Chromebooks work but are slower.
2. **Codes.** Choose a class code for the study (for example `PT-2026-10`) and a participant ID per person (`P01`, `P02`, and so on). Never use names.
3. **Consent.** Read the study consent script. If the participant agrees, tap **RESEARCH DATA: OFF · tap to opt in** on the title screen. It changes to SHARING. Without opt-in, no play events leave the device, although the completion code still works.
4. **Settings** (Esc on the site, or before starting):
   - *Visual cues*: **Auto** for first-timers. Use **Off** for a transfer or skill session.
   - *Facilitator: all episodes*: **Unlocked** only if the protocol needs EP5 without the mastery gate.
   - *AI crew chat*: follow your IRB (built-in answers are fine).
   - *Volume*: set it to what the room needs. Automated tests always run muted.
5. **Reset between participants.** Esc → "Reset progress (new participant)" (tap twice), or use a fresh browser profile. Career, mastery and banked hints are stored per browser.

## Session script (about 45 min)
| Min | Step | Facilitator notes |
|---|---|---|
| 0-3 | Consent, codes, research opt-in | Say: "We're testing the game, not you." |
| 3-18 | **EP1 First Light**, think-aloud | Don't coach. Note where they hesitate. |
| 18-33 | **EP2 The Cut** or **EP3 The Edge** | Watch the stop-work, Ray pushback and kit choice moments. |
| 33-38 | Toolbox talk and debrief | Ask: "Why did you put that one first?" |
| 38-45 | Post-play interview and survey | See the questions below. |

## What to observe (tick when seen)
- [ ] Infers "photo = report" without help (GDD §10 scenario 1).
- [ ] Measures before reporting at least once.
- [ ] Uses "Checked · compliant" on a look-alike.
- [ ] Holds the stop when Ray pushes back (assertive or aggressive), and says why.
- [ ] Picks the correct kit on the first try. If not, can they say why the wrong kit failed?
- [ ] Reads the near-miss card and connects it to what they missed.
- [ ] Orders the toolbox talk by risk rather than by the order they found things.
- [ ] Any confusion longer than 60 s (note where).
- [ ] Any text they couldn't read or didn't understand (note which card).

## Post-play questions
1. What does a competent person do that a regular worker doesn't?
2. Why did the PPE-only fix lapse? (GDD §12 assumption check.)
3. When Ray pushed back, what made you choose your answer?
4. Was anything unfair or confusing about how you were scored?
5. SUS (10 items) and, if your protocol uses it, the HSEA pre/post items.

## Data you get
- **Completion code** per episode (instructor page → verify, or class report / CSV).
- **Event log** (only if research was opted in). It includes photos, measures, reports, confirmations, controls, kit choices, stop-work, speak-up style, toolbox-talk order and score, cued/hinted flags, per-area mastery, schedule slip and incidents. Every scored action carries a KSA and an OSHA citation.

## Known limits to tell participants
- Desktop or browser only (no VR build of v2 yet). English only.
- Some quiz items and kit distractors are pending SME review.
