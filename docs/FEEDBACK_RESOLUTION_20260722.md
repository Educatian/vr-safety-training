# Student Pilot Feedback Resolution Record

- Date: 2026-07-22
- Feedback source: follow-up student pilot email and 3:29 gameplay video
- Branch: `codex/demo-pilot-validation`
- Unity: 6000.4.9f1
- Automated verification: **210/210 EditMode tests passed**

## Resolution matrix

| Reported issue | Update | Verification |
| --- | --- | --- |
| No convenient way to exit or minimize | Retained ESC Resume/Quit overlay and added a persistent right-side `MENU` tab. The canvas now also accepts XR tracked-device ray input. | Pause-menu composition and binding tests; GPU play capture shows the side tab. |
| Tutor slides while following | The safe procedural locomotion gait now runs during normal play even when the avatar has an Animator Controller but authored locomotion is disabled. Idle, two walking phases, and settled poses were captured separately. | Runtime gait regression test and GPU capture frames `04-construction-npc-rest/walking-a/walking-b`. |
| Tutor/player pass through walls and material props | Existing site boundary colliders remain solid. Runtime migration now adds inexpensive root box colliders to solid construction props such as barriers, cement/material stacks, crates, pallets, formwork, cabinets, gates, and panels. Tutor capsule movement is collision-limited, and off-screen formation recovery now refuses a destination whose route crosses a solid wall. Future imported props receive the same optimized collider treatment. | Scene contract checks boundary colliders, player collision, solid-prop coverage, and a dedicated tutor-through-wall route regression. |
| Tutor response appears as 1/14, 2/14, etc. | Removed timed multi-page speech playback. The bubble now shows one concise readable preview; the complete response is written once to the coach transcript. | Bubble-preview regression test. |
| Earlier chatbot responses cannot be reviewed | The 24-turn per-coach history remains persistent and the compact translucent coach panel now exposes a permanent scrollbar plus `MOUSE WHEEL OR XR DRAG TO REVIEW` instruction. | Chat history/scrollbar composition test and runtime screenshot. |
| Engineering decision objects do not respond to clicks | Desktop center-ray activation now handles engineering options, hypotheses, evidence, inquiry submission, and learning-board paging in addition to ordinary inspection targets. | Direct activation test submits the selected engineering decision. |

## Visual QA

GPU-rendered play captures are stored in `Captures/student-qa-2026-07-22/`. The capture tour generated 68 PNG frames plus a completion marker, including construction overview, real props, engineering stations, hands-on interaction, compact coach chat, and tutor motion phases.

The full campus/inquiry map was regenerated at `Captures/inquiry-editor-v1/00-full-training-campus-map.png`.

## Release artifacts

- Windows: `Builds/VR-Safety-Training-Windows-StudentQA-2026-07-22.zip` (77,867,713 bytes)
- Meta Quest: `Builds/MetaQuest/VR-Safety-Training-Quest-StudentQA-2026-07-22.apk` (102,092,577 bytes)
- Windows startup smoke test: stable for 12 seconds, no crash/reference error, zero startup rig or camera drift.

## Remaining physical-device gate

This workstation had no Android Debug Bridge command available, so controller reach, headset comfort, wall perception, and frame timing still require one short on-device check after sideloading the refreshed Quest APK. This is a device QA gate, not an unresolved code defect.
