# Competent Person: serious-game design review (revamp/v2, 2026-09-29)

Scope: GDD v2 against the current runtime (`Assets/_Game/Scripts`), after the fixes that followed `GapAudit_2026-09-28.md` (commit 92ba2f2 and later). I read the GDD, Core and Runtime scripts, the slice builders and tests, and the EP2/EP3 capture sheets. I did not play a build.

**Verdict.** The GDD is a strong serious-game design: learning mechanics map 1:1 to game mechanics, the evidence base is explicit, and "weak controls decay over the shift" is a genuinely good way to teach the hierarchy. The build does not yet deliver the core learning loop the GDD promises, and before this pass several paths gave the answer away, so HII and KSA scores from a pilot would have been inflated.

## Fixed since the 09-28 audit (verified in code)
A1/A2 missed-hazard labels and epilogue gating, A3 GFCI 15/20 A, A4 option shuffle, A6 text wrap, C1 tag and P x S feedback, B1 server telemetry (Pages Functions + D1), B2 completion code, B3 AI-chat consent, B5 pause/settings, B7 audio, C3 partial (one hazard becomes its compliant twin per run, triggers jitter +/-45 s), A5 partial (stops lift after 120 s).

## 1. Answer leaks (assessment validity) — FIXED in this pass
| # | Leak | Fix |
|---|---|---|
| L1 | Mission cue (on by default at levels 1-2) put a diamond, ring and "CHECK: ..." label on the hazard itself; those finds got full recognition credit. | Cue marks a 6 m zone centred on all of the step's candidates (hazard and look-alike), jittered up to 3 m. Finds while a cue covers the condition are flagged `Cued` (0.5 mastery and SRecognize, XP unchanged). The EP1 tutorial beacon find is flagged the same way. |
| L2 | The tablet title is the diagnosis ("Guardrail without midrail" / "Marked hole cover") and was shown before the call. Compliant twins kept the hazard title. | `ConditionNames`: a shared neutral name per hazard/look-alike pair until the learner reports. Compliant twins get a compliant title. Hint tier 3 uses the neutral name. |
| L3 | "E Photograph" appeared only on conditions, which acted as a highlight. | The same prompt on any surface in reach. Empty and badly framed shots get the same answer. Every photo costs 5 s of shift time. |
| L4 | Instrument readings showed before the call, stated the verdict ("needs 3 ft", "Type C", "above the PEL"), and were logged as SInspect 1.0 on every photo. | Measuring is a tablet action (10 s). `InstrumentTable` holds raw values only, with compliant values for controlled conditions and twins. SInspect is scored only when the measurement comes before the call. |
| L5 | Opening a chat scored SCommunicate and ACare at 1.0. | The chat opening is logged but not scored. |

## 2. Scoring exploits — FIXED in this pass
- **Stop-work farming.** A stop lifted after 120 s could be re-stopped for another +30 XP and +1 trust. A repeat stop now still holds the crew but returns `Repeated` (no XP or trust). Crew idle time from stops (`StoppedSeconds`) is shown on the tablet and in the debrief.
- **Finish at minute one.** This earned Zero Recordables (+100 SP) and a completion code. The badge now needs the full shift, and "Finish shift" early asks for confirmation. `shift_end` logs `early=`.
- **Choosing an engineered fix counted as the fix.** `AppliedControl` is now set on a successful install. The kit can still be installed if the crew was stopped mid-install.
- **Report-everything dominance.** A false alarm costs 15 s. A new "Checked · compliant" action gives positive discrimination evidence (+15 XP, paid at shift end). Right and wrong confirmations look identical until the debrief, so the action cannot be used to probe. The debrief lists every look-alike and the learner's call on it.

## 3. Still open: core loop the GDD promises but the build lacks
- **Reflect.** The toolbox talk is still two buttons with the same right answer in every episode. Implement: pick 3 of today's findings, order them by severity, then pick the "why". Score against the key. (GDD N7, §4)
- **Mastery gate.** `DaySession.Mastery()` has no callers, all 5 episodes are open, and "That's a competent person" plays regardless of performance. Show per-area mastery, gate EP5, and branch the capstone epilogue. (§5.2)
- **Production pressure (pillar 3, N4).** There is no Schedule system, Ray never pushes back, and there are no speak-up choices. Crew trust has no downstream effect (no self-reporting). (§5.2, §14)
- **Consequence and review (N10).** An incident is one radio line. There is no near-miss card, no path replay, no chosen-vs-best control comparison, and no Dolores walk.
- **C4 execution.** There is no Elimination option, and "engineered" means dropping a generic kit within 1.5 m. The learner never chooses which control (cover vs. rail).
- **Support and transfer.** There is no Guided/Field profile, pretest, Week 2 transfer, 4-week booster or EN/ES. `HintBank.Earn` and the streak bonus are unwired.
- **Density.** EP3 has 3 real hazards, and replay sampling leaves 2 per 10-minute shift.
- **Platform.** v2 runtime has no XR code; the GDD platform line (OpenXR PC VR) is not delivered.

## 4. Research and docs
- Telemetry sends class and learner codes with no research-consent gate (only AI-chat consent). This is needed before IRB use.
- Quiz items are 2-3 recall questions per quiz and are weakly aligned to the HSEA construct. The correct option is still often the longest.
- README, STATUS and GAME_CONCEPT still describe v1. The GDD cast (Marisol/Tyler/Earl) differs from the build (Marcus/Luis/Tasha/Kiara/Dale).
- Tasks.md checkboxes are stale. Weather, heat, vehicles and missions were built outside Tasks while T1.8-T1.10 (the learning loop) stay open.
- The "12 words per card" invariant is far exceeded on the tablet.

## Recommended next order
1. Toolbox-talk writer (M).
2. Debrief: chosen-vs-best control per hazard, plus an incident card (M).
3. Mastery bars, EP5 gate and a branching capstone epilogue (S-M).
4. Schedule meter and a Ray speak-up exchange with 3 assertiveness choices (M).
5. Research-consent gate for telemetry; rewrite README/STATUS for v2 (S).
6. Elimination option and a control choice per hazard (S-M).

## Pass 2 (same day): section 3 implemented
| Gap | Now |
|---|---|
| Reflect | **Toolbox-talk writer**: pick up to 3 of today's findings, order them, and answer a topic "why" item (5 authored items, shuffled). It is scored on selection, order vs. the P x S key, and the why. Leading with the top-risk finding earns XP, and a good talk banks a hint token for the next shift. |
| Mastery gate | Per-area mastery is shown in the debrief and saved (best across episodes). EP5 stays locked until Electrical, Excavation, Fall protection and Struck-by are all competent (0.7). A facilitator override is in settings. The capstone epilogue branches clean vs. rough (`ShiftVerdict`). |
| Pressure (N4) | After a justified stop, Ray pushes back with three answers. Passive restarts the crew and forfeits escalation evidence. Aggressive holds the stop but costs trust. Assertive holds it and builds trust. There is a schedule-slip meter and an On Schedule commendation (lateness never touches the rating). At crew trust ≥ 3, a worker radios in one unfound hazard, flagged as cued. |
| Consequence (N10) | A near miss opens a stop-down card ("what almost happened", no gore, citation), and the clock waits. The debrief groups hazards by outcome (controlled at best / below best / found but not controlled / missed) with chosen vs. best control, and lists every near miss. |
| C4 execution | Elimination (remove from service) for the damaged cord and the frayed sling. Engineered fixes require choosing the right kit from three. A wrong kit fails at the hazard as an install attempt. Installs snap in (0.28 s). |
| Economy | Streaks pay +50 XP and a hint token. `HintBank.Earn` is wired. |
| Research | Play events are sent only after research opt-in (title-screen toggle, consent version logged). |
| Playtest ops | `docs/PlaytestGuide.md`. Esc has "Reset progress (new participant)". PlayMode tests run muted. |

Verification: all five assemblies (Core, Runtime, Editor, EditMode tests, PlayMode tests) compile against the Unity 6000.3.25f1 reference assemblies. The EditMode suite passes (54 tests). The PlayMode suite and a WebGL rebuild still have to be run in the Unity editor.

Still open (needs Unity, assets or an SME): more hazards in EP3, EN/ES, Week 2 transfer, the OpenXR v2 build, and SME review of quiz items and kit distractors.
