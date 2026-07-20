# Evidence-Grounded Hands-On Placement Tasks

## Learning construct

The placement tasks teach procedural hazard control: learners must select the correct control, physically move it, and release it at the operationally correct location in the required sequence. Completion therefore demonstrates a spatial control decision and a procedural action, not recognition by clicking.

## Construct → evidence → mechanic → signal

| Construct | Evidence | Implemented mechanic | Telemetry signal |
|---|---|---|---|
| Construction hazard mitigation through active practice | Abotaleb et al. developed an interactive Unity VR model based on experiential learning, with programmed responses to trainee actions and improved hazard-identification and mitigation outcomes. [DOI](https://doi.org/10.1002/cae.22585), [OpenAlex W4309858751](https://openalex.org/W4309858751) | Every practical prop must be grabbed or dragged and released inside a highlighted control zone. A click cannot complete it. | `placement_attempt`: site, step, action, release distance, success, input mode |
| Personalized performance feedback | Wolf et al. evaluated active virtual construction-safety environments with personalized feedback and automatically collected performance data. [DOI](https://doi.org/10.1016/j.aei.2021.101469), [OpenAlex W3216772515](https://openalex.org/W3216772515) | Incorrect placement returns the prop and states the expected control location; success snaps the prop into its installed state and triggers coach encouragement. | attempt count, failed distance, successful distance, sequence state |
| Hazard-management performance in realistic contexts | Hasanzadeh et al. combined realistic environments, performance assessment, and feedback in a construction-safety protocol that improved hazard recognition and management. [DOI](https://doi.org/10.1108/ECAM-07-2019-0391), [OpenAlex W3018389716](https://openalex.org/W3018389716) | Destination zones are embedded in the worksite beside the hazard/control context, not presented as a detached quiz. | site and action identity paired with placement result |
| Skills and behavior, not presence alone | A 2024 meta-analysis reported positive effects of construction-safety VR across behavior, skills, and experience, with effects moderated by training context and work experience. [DOI](https://doi.org/10.1016/j.jsr.2023.11.011), [OpenAlex W4389262930](https://openalex.org/W4389262930) | The same scored action works with desktop dragging and VR controller grabbing, allowing modality comparisons without changing the learning rule. | `inputMode = DesktopDrag | XRGrab` |

## Module task map

| Module | Placement sequence |
|---|---|
| Construction | Stage PPE → place exclusion barricade → install guardrail kit → return material cart → deliver final walkdown clipboard |
| Warehouse | Deploy spill kit → separate vehicle route with barrier → relocate aisle load to staging |
| Fire response | Clear extinguisher access → stage response kit → deliver egress checklist |
| Chemical processing | Place containment kit → deliver GHS record → stage eyewash test kit |
| Electrical maintenance | Place lockout/tagout kit → deliver zero-energy record → protect cable crossing |

## Interaction state machine

1. `Locked`: non-current task has no active grab/click collider or destination marker.
2. `Ready`: current prop and destination marker are highlighted.
3. `Held`: desktop left-drag or XR select moves the prop; clicking without movement does not resolve the task.
4. `ReleasedIncorrect`: release outside tolerance logs failure, publishes corrective feedback, and returns the prop to its start pose.
5. `ReleasedCorrect`: release inside tolerance logs success, snaps to the installed pose, awards score, advances the sequence, and triggers NPC encouragement.
6. `Complete`: installed prop is locked and cannot be scored twice.

Interruption behavior: leaving a site, losing focus, or disabling the component returns an unfinished prop to its start pose. Later steps cannot be manipulated until the current placement succeeds.

## Five-component evaluation

- Clarity: only the current prop and its labeled destination are active.
- Motivation: every successful placement advances certified practical progress and score.
- Response: the prop follows the learner continuously and evaluates the actual release position.
- Satisfaction: successful snap, HUD confirmation, score change, and NPC encouragement provide multiple feedback channels.
- Fit: controls are real worksite props placed in operationally meaningful locations.

## Starting values and test plan

- Starting value: placement radius `0.85 m` for portable controls and `1.10 m` for large barriers/carts.
- Micro-test: complete all 17 placements once on desktop and once with XR input. Pass when intended releases succeed at least 9/10 times without accepting visibly wrong zones.
- If valid drops fail, increase radius by `0.10 m`; if wrong placements pass, decrease by `0.10 m` and re-test.
- Starting value: return/snap duration `0.28 s`. Pass when the state change reads clearly without delaying the next action; adjust in `0.05 s` steps.

## Assumptions

- ASSUMPTION: a planar desktop drag is an acceptable mouse analogue for controller grabbing. IMPACT: both modalities assess release location using one rule. IF WRONG: desktop learners may need depth control. VALIDATE: observe whether the five site tasks can be completed without camera repositioning during the drag.
- ASSUMPTION: the authored target locations represent the intended safety controls for this prototype. IMPACT: success means spatially correct mitigation. IF WRONG: a subject-matter expert must revise the destination coordinates. VALIDATE: construction-safety expert walkthrough before summative deployment.

## Abuse and readability checks

- A press-and-release at the start pose must not advance progress.
- Dropping a later-step object must not advance progress.
- Repeated release outside the zone must not award score.
- A completed object cannot be moved or scored again.
- An observer must see the active prop, destination marker, failed return, and successful installed state.


# Construction Golden Module — Evidence-Centered 20+ Minute Mission

## Mission goal

The learner acts as a field engineer investigating a US construction site before a crane lift and second-floor concrete operation. The learner must establish entry readiness, gather independent field evidence, make three civil-engineering decisions, install physical controls, defend the plan to the NPC coach, and submit an evidence-gated report.

The target duration of **22 minutes** is a starting value, not a validated constant. The pilot acceptance band is a novice median of 20–30 minutes, with no required stage completed in under one minute and no stage producing more than 15% abandonment.

## Ordered mission state machine

| Stage | Unlock condition | Learner action | Success evidence |
|---|---|---|---|
| 1. PPE entry | Enter Construction | Drag PPE kit to entry checkpoint | ppe_verified |
| 2. Field inquiry | PPE verified | Explore distinct paths and collect 4 relevant observations; distractors remain available | four unique golden_evidence_recorded signals |
| 3. Engineering decisions | 4 relevant observations | Solve formwork capacity, crane radius, and trench protective-system cases | three unique golden_engineering_verified signals |
| 4. Control installation | all calculations verified | Move material cart, install guardrail, place exclusion barricade, deliver walkdown | four unique control_installed signals |
| 5. Coach debrief | controls verified | Type a substantive risk-to-control explanation to the Construction NPC | coach_explanation_accepted |
| 6. Final report | debrief accepted | Submit the field report | golden_module_completed |

Out-of-order attempts generate golden_attempt_blocked; duplicate evidence, repeated correct decisions, and repeated control steps cannot advance progress. The final report is not available merely because three objects were clicked.

## Construct → evidence → mechanic → signal

| Construct | Observable evidence | Mechanic | Analytics signal |
|---|---|---|---|
| Inquiry-cycle competence | Learner distinguishes four relevant observations from comparison samples before concluding | free site exploration, evidence objects, distractor objects, evidence threshold | object ID, hazard type, distractor status, site-local/world XYZ |
| Civil-engineering judgment | Learner selects the defensible answer for three calculation/geometry cases | formwork, crane-radius, and trench HMI stations with diagnostic revision feedback | decision ID, accepted/revision, objective CON-02 |
| Procedural control implementation | Learner releases four real props in operationally correct locations and sequence | desktop drag / XR grab, placement tolerance, snap, coach gesture | action ID, release distance, input mode, success |
| Evidence-based communication | Learner links a construction risk to a specific control in at least eight words | typed NPC debrief, substantive-response validator, speech-bubble response | accepted/revise, explanation criterion, coach turn |
| Integrated safety-plan defense | Learner completes the entire chain before certification | report lock governed by the mission state machine | completion, stage, objective/criterion, timestamp, XYZ |

## Research grounding verified in OpenAlex

- Pedaste et al. organize inquiry-based learning around orientation, conceptualization, investigation, conclusion, and discussion. The mission implements these as entry briefing, field evidence, control hypothesis/calculation, installation, and coach/report reflection. [DOI](https://doi.org/10.1016/j.edurev.2015.02.003), [OpenAlex W2091552421](https://openalex.org/W2091552421).
- Mislevy, Steinberg, and Almond connect assessment claims to observable evidence and task features. The objective → criterion → interaction → telemetry links above follow this evidence-centered design logic. [DOI](https://doi.org/10.1207/S15366359MEA0101_02), [OpenAlex W2039681496](https://openalex.org/W2039681496).
- Chi et al. found that prompted self-explanation can support deeper understanding. The NPC debrief therefore requires an explicit risk-to-control explanation rather than a completion click. [DOI](https://doi.org/10.1207/s15516709cog1803_3), [OpenAlex W4243420394](https://openalex.org/W4243420394).
- Sacks, Perlman, and Barak tested immersive construction-safety training for identifying and assessing construction risks. The mission retains realistic site inspection and risk assessment before intervention. [DOI](https://doi.org/10.1080/01446193.2013.828844), [OpenAlex W1976408896](https://openalex.org/W1976408896).
- Yu, Wang, and Wu compared novice and experienced learners across 17 immersive construction hazard scenarios, supporting explicit pilot stratification by prior experience. [DOI](https://doi.org/10.1061/%28ASCE%29CO.1943-7862.0002337), [OpenAlex W4283163326](https://openalex.org/W4283163326).

## Five-component review

- Clarity: a compact in-world mission board shows the current stage and 4/3/4 progress counts; only the currently legal practical prop is enabled.
- Motivation: every stage changes the site state and opens a materially different activity—exploration, calculation, manipulation, conversation, then submission.
- Response: actions produce spatial snap/return, HUD coaching, NPC speech/gesture, and progress-board updates.
- Satisfaction: the final state requires accumulated evidence and produces a complete analytics trail rather than a single quiz score.
- Fit: all required actions express construction field-engineering practice and map to CON-01 through CON-04.

## Required playtests

- New player: a learner with no briefing must identify the current goal and first action within 30 seconds.
- Stress: three incorrect drops and two wrong engineering answers must preserve progress and provide a recoverable next step.
- Skilled player: an experienced learner can navigate directly but cannot skip PPE, evidence, calculations, debrief, or report.
- Abuse: duplicate evidence, repeat station selection, click-only placement, and premature report submission never award extra progress.
- Readability: an observer can read the stage change from the board/HUD and identify the active prop without verbal help.

## Pilot measures

- Completion time and stage dwell time by novice/experienced group.
- Path length, revisit count, and dwell heatmaps using site-local XYZ.
- Evidence precision: relevant observations / all observations.
- Engineering first-attempt accuracy and revision count by case.
- Placement error distance and retry count by input mode.
- Debrief acceptance/revision and report completion.
- Simulator comfort, perceived workload, confidence, and one-week hazard-recognition transfer check.

The 4-evidence, 3-decision, 4-control thresholds and 22-minute target are starting values. Revise only after pilot evidence: lower a threshold only if content validity remains intact; change route or cueing before reducing required reasoning.
