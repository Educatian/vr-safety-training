# Civil Engineering Learning and Assessment Design

This implementation turns the Construction site from a hazard-recognition scene into an evidence-based civil and construction engineering investigation. Learners gather field observations, compare them with task data, calculate compliance or capacity, select a control, implement controls, and submit a defensible report. Every scored action is exported with an objective ID and criterion ID for serious-game analytics.

## Constructive alignment

| Module | Explicit learning objective | Required assessment evidence | Runtime evidence ID |
|---|---|---|---|
| Construction | CON-01 Diagnose site risk | Three relevant field observations; distractors remain visible as comparison choices | `field:<evidenceId>` |
| Construction | CON-02 Make engineering decisions | Correct formwork, crane, and trench decisions supported by the displayed calculation | `decision:<decisionId>` |
| Construction | CON-03 Implement controls | Successful sequenced hands-on placements | `practical:<stepIndex>` |
| Construction | CON-04 Defend the safety plan | Evidence-gated hypothesis plus final report | `hypothesis`, `final_report` |
| Warehouse | WAR-01 Diagnose material-flow risk | Relevant storage, loading, pedestrian, and vehicle observations | `field:<evidenceId>` |
| Warehouse | WAR-02 Restore safe flow | Correct hands-on workplace reconfiguration | `practical:<stepIndex>` |
| Warehouse | WAR-03 Justify the control | Evidence-gated hypothesis plus final report | `hypothesis`, `final_report` |
| Fire Response | FIR-01 Assess response conditions | Relevant alarm, egress, fire, and extinguisher observations | `field:<evidenceId>` |
| Fire Response | FIR-02 Execute response sequence | Correct response-equipment placement sequence | `practical:<stepIndex>` |
| Fire Response | FIR-03 Defend response choice | Evidence-gated hypothesis plus final report | `hypothesis`, `final_report` |
| Chemical Processing | CHE-01 Characterize chemical risk | Label, SDS, compatibility, exposure-path, and containment evidence | `field:<evidenceId>` |
| Chemical Processing | CHE-02 Control the release | Correct compatible spill-control placements | `practical:<stepIndex>` |
| Chemical Processing | CHE-03 Communicate the response | Evidence-gated hypothesis plus final report | `hypothesis`, `final_report` |
| Electrical Maintenance | ELE-01 Identify electrical energy risk | Source, conductor, wet-interface, and lockout evidence | `field:<evidenceId>` |
| Electrical Maintenance | ELE-02 Establish safe work condition | Correct isolation and access-control sequence | `practical:<stepIndex>` |
| Electrical Maintenance | ELE-03 Defend the isolation plan | Evidence-gated hypothesis plus final report | `hypothesis`, `final_report` |

## Construction engineering decision tasks

### Formwork and shoring capacity

The station provides a 14 ft by 10 ft by 8 in slab, 150 pcf fresh-concrete unit weight, 3,500 lb crew/equipment load, 1,500 lb form self-weight, and four 4,000 lb shore frames. Learners verify 19,000 lb demand against 16,000 lb capacity (D/C = 1.19) and must hold the pour pending a revised, verified shoring plan.

### Crane lift-plan radius

The station combines a 6,200 lb load and 800 lb rigging. The learner compares 7,000 lb gross load with chart capacities of 6,500 lb at 60 ft and 8,200 lb at 50 ft. The supported decision is to stop and reconfigure to a verified 50 ft radius with lift-zone control.

### Trench protective system

The station presents a 6.5 ft excavation in Type C soil, spoil one foot from the edge, a ladder 34 ft away, and no protective system. A complete response requires a verified protective system, spoil setback of at least two feet, and egress within 25 ft before entry.

## Learning materials and feedback

Each work zone has a selectable in-world objective board. It cycles through the objective, assessment evidence, and governing standard without opening an occluding full-screen panel. Construction engineering consoles show field data, the worked calculation structure, decision options, and diagnostic feedback. Incorrect options explain the missing engineering constraint; correct options record evidence, add score, and trigger coach encouragement.

## Analytics schema

`inquiry_events.csv` now includes `objectiveId`, `criterionId`, `outcome`, `earnedPoints`, and `possiblePoints`. `learner_route_summary.csv` includes assessment attempts, evidence points earned, and objectives touched. These fields can be joined to site-local XYZ coordinates, analytics zones, dwell time, route distance, evidence collection, practical attempts, and NPC turns by `sessionId`.

## Standards basis

- OSHA 29 CFR 1926.703: formwork must support reasonably anticipated vertical and lateral loads, and drawings or revisions must be available onsite.
- OSHA 29 CFR 1926.1417: crane operation must remain within rated capacity and use verified load and configuration information.
- OSHA 29 CFR 1926.651 and 1926.652: excavation egress, spoil setback, competent-person inspection, and protective-system requirements.
- ABET 2026-2027 engineering student outcomes: problem solving, engineering design, communication, ethical responsibility, and experimentation/evidence interpretation.

This is instructional simulation content, not a substitute for a site-specific engineered design, competent-person determination, lift plan, or employer safety program.
