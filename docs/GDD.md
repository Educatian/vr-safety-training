# GDD — *Competent Person* (vr-safety-training v2)

**A serious game about construction safety.** One civil project, one work week, one crew. You are the site's new safety lead and are working toward your **OSHA "competent person"** designation. That is the real role that inspects trenches, scaffolds and fall protection, and it has authority to take prompt corrective action. The game teaches **recognition → risk → control → escalation** by making those four the core game verbs.

- **Collaboration line:** [[David Awoyemi]] (IVR civil-engineering safety study, BORIS codebook, Hazard Safety Engineering Assessment) and NIOSH R21 RoofSafe-XR.
- **Platform:** OpenXR PC VR plus desktop first-person. One rule set; desktop is a full learning path, not a lesser version [Wu2020].
- **Evidence base:** `Desktop\_research\2026-09-28_simulation-based-construction-safety-training-design.md`. Bracketed keys cite it. Serious-game design keys are in §13.

---

## 1. Why a serious game, and what kind

- Digital games beat non-game instruction, most with **enhanced scaffolding** and **multiple sessions** [Clark2016]. Serious games improve learning and retention, and work best with **multiple sessions** and when **supplemented with instruction or debrief** [Wouters2013b].
- Learning is greatest when the content is delivered **through the core mechanic** (intrinsic integration) rather than as a quiz between game moments [Habgood2011].
- Therefore every learning mechanic below maps 1:1 to a game mechanic [Arnab2015] (§4). There is no separate quiz layer.

**Design pillars**
1. **You're the one who notices.** Nothing is highlighted. The game is about seeing.
2. **Controls have consequences over time.** Weak controls (PPE-only, a verbal warning) decay during the shift; engineered controls stay. The hierarchy of controls is learned by *living with* its consequences, not memorizing it.
3. **Real work, real pressure.** Hazards live inside production. Schedule pressure is a system, and so is the right to stop work.
4. **The crew are people.** Named workers with habits. Safety is something you do *with* them.

---

## 2. Training needs → serious-game answer

| # | Training need (evidence) | Serious-game mechanic that addresses it |
|---|---|---|
| N1 | Workers recognize about 57% of Focus Four hazards and about 18% of others [Albert2020; Uddin2020]. | **Site Walk with the tablet camera.** Photographing a condition *is* reporting it. The day's **Hazard Identification Index (HII)** appears on the daily report. Non-Focus-Four hazards (silica, pressure, heat) are in the pool. |
| N2 | Misses come from selective attention and underrated risk [Jeelani2017a]. Severity is overweighted vs. probability [Perlman2014]. | Each photo gets an **energy-source tag** [Albert2014] and a **risk-matrix card** (probability × severity). This is the real job-hazard-analysis form, used in the fiction. **Look-alike pairs** make "report everything" a losing strategy for precision (§6). |
| N3 | The course assessment targets the hierarchy of controls and the "PPE first" misconception. | **Fix / Assign / Stop.** Controls come from a limited daily **resource board** (crew hours, materials). **PPE-only and admin-only controls can lapse later in the shift** and trigger a near-miss (§5.3). Engineered controls persist. |
| N4 | Production pressure suppresses speaking up [Han2014; Huang2025]. | The **Schedule system** and a foreman NPC, Ray, who pushes. Stop-work pauses production but **never lowers the safety rating**. Speak-up dialogue has assertiveness choices. |
| N5 | Struck-by vigilance decays over time [Hussain2024]. | **Live site events** on a day timeline: deliveries, a backing dump truck, crane picks, a worker walking into a swing radius. They arrive late in the shift, while the player is busy. |
| N6 | Our IVR data showed text-driven learners and **Display Distraction → Confusion**. Headsets raise load [Makransky2019a]. Presence is inversely related to HII [Eiris2020b]. | **Diegetic, voice-first UI.** Everything is on the tablet or spoken over the radio, capped at 12 words per card. There is no decorative motion near an active hazard. |
| N7 | Pre-training helps in VR [Meyer2019]. Generative pauses help [Parong2018]. | A **Monday site orientation in the trailer** (2D): gear, the energy wheel, the hierarchy. At the end of each day **you write and deliver tomorrow's toolbox talk** by picking and ordering today's findings. This is the generative explain-back, inside the fiction. |
| N8 | Elaborated feedback beats right/wrong [VanDerKleij2015; Moreno2005]. The modal learner cluster in our data repeats errors and ignores feedback. | **Radio call from the mentor** (Dolores, veteran safety manager) right after a critical error: what, why, the CFR reference, and the consequence. On a **repeated error** she stops you and **walks the fix with you** (worked-example ghost replay). |
| N9 | Novices benefit from examples; experts from reflection [Chernikova2020]. | **Shadow day vs. solo.** Guided profile: Monday is spent shadowing Dolores, who models a site walk (worked example), and her cues fade by Wednesday [Wouters2013]. Field profile: solo from Monday, with reflection prompts. |
| N10 | Debriefs add about 25% [Tannenbaum2013]. Personalized missed-hazard feedback helps [Jeelani2017b]. | **End-of-day incident review**: path replay, missed hazards by energy source, "what almost happened" near-miss clips, and Dolores's expert walk of the same day. There is also an **instructor debrief export** for classroom use [Wouters2013b]. |
| N11 | Knowledge decays by 4 weeks [Stefan2024]. Transfer to untrained hazards is the real target [Xiong2026]. | **The site changes daily as construction progresses**, so every day is a new configuration. Seeded hazard pools vary per run. **Week 2: a different project** is both the transfer test and the 4-week booster. |
| N12 | Hispanic workers are over-represented in falls [Dong2009]. Training is short and text-poor [O'Connor2005]. Co-design works [Evia2011]. | **EN/ES** from v2.0. Crew lead Marisol works in Spanish. **Each day is a 10-minute shift**, so one day fits a toolbox-talk slot. Consequences are shown as near-misses, never gore. |

---

## 3. Structure: one project week

The project is a small municipal job: a stormwater line plus a 2-storey pump-station building. The site **physically evolves** each day, and each day foregrounds one Focus Four area while earlier areas stay live.

| Day | Construction phase | Foreground | Carry-over and live events |
|---|---|---|---|
| **Mon** | Mobilization | Orientation (2D) → PPE check → guided site walk | Temp power set-up; first delivery truck |
| **Tue** | Excavation and utility trench | **Caught-in**: trench box, spoil, egress, water, daily competent-person inspection | Excavator swing; spoil truck backing |
| **Wed** | Framing, deck and scaffold | **Falls**: leading edges, openings, ladders, scaffold | Trench still open; cords across the deck |
| **Thu** | Roof and crane pick | **Falls + struck-by**: roof edge, skylight, suspended load, tagline, swing radius | Scaffold modified overnight; heat |
| **Fri** | Concrete pour near the overhead line | **Electrical + capstone**: boom or pump truck within 10 ft of the line, GFCI, damaged cord. Foreman pressure peaks. | Everything above can resurface |
| **Week 2** | Different project (roof re-cover on a warehouse) | Transfer test / 4-week booster | Novel layouts only |

A day can be played on its own (toolbox mode) or the week can be played in sequence (course mode).

---

## 4. Learning mechanics ↔ game mechanics (LM-GM map [Arnab2015])

| Competency (ECD [Mislevy2003]) | Learning mechanic | Game mechanic | Evidence captured |
|---|---|---|---|
| C1 Recognize | Observation, discovery | **Site walk + tablet photo**. Nothing is highlighted; the photo frame must contain the hazard. | HII, time-to-detect, head-gaze dwell |
| C2 Classify | Categorize | **Energy-wheel tag** on the photo | Tag accuracy |
| C3 Assess risk | Judge, compare | **Risk-matrix card** (P × S) | Deviation from the expert key per axis |
| C4 Control | Apply, plan, act | **Fix / Assign / Stop** with the resource board, then a hands-on install (guardrail, cover, trench box, tie-off, barricade, GFCI) | Hierarchy level, execution, attempts, lapse events |
| C5 Escalate | Communicate, persist | **Radio stop-work + speak-up dialogue** with Ray or crew members | Latency, assertiveness choice, comply-vs-stop under pressure |
| Reflect | Self-explanation, generative | **Write tomorrow's toolbox talk**, plus a "why?" 3-option on key calls [Johnson2010] | Talk content vs. key findings |
| C6 Transfer | Apply in a novel context | **Week 2 new project** | ΔHII, ΔC4 vs. week 1 |

---

## 5. Systems

### 5.1 Day loop (≈ 10 min, starting value)
```
Morning radio brief (Ray: today's work; Dolores: tip in Guided)
 → Site walk (free roam; production runs on the day clock)
    photo → tag → risk card → Fix / Assign / Stop
    live events fire on the timeline
    lapses fire for weak controls
 → Whistle: end of shift
 → Daily report (Safety record · Schedule · Crew trust · CP progress)
 → Incident review (replay, missed-by-energy, near-miss clips, Dolores's walk)
 → Write tomorrow's toolbox talk (pick 3 findings, order them, pick the "why")
```
*Starting value:* 10 min per day. *Test:* the median first-time player finishes a day in 8–12 min with no idle stretch over 60 s. *If too long,* trim hazard count by 1. *If players are idle,* add a live event.

### 5.2 Outcome meters (in-fiction; the deterministic engine owns the numbers)
- **Safety record:** near-misses, first-aid cases, recordables. A recordable happens only if a *high-severity* hazard is left uncontrolled past its seeded trigger time. It is shown as a no-gore near-miss clip plus a stop-down.
- **Schedule:** project percent vs. plan. Fixes cost crew hours. Stop-work pauses the affected crew. **Safety is never traded directly for points.** Being late costs only the "on schedule" commendation, never the CP rating.
- **Crew trust:** rises with justified stops, respectful speak-up choices and engineered fixes. It falls with repeated false alarms on the same look-alike and with silent Assigns that lapse. Higher trust means workers self-report hazards on later days (a positive feedback loop).
- **CP progress (the stealth assessment):** a per-area mastery bar (Excavation, Fall protection, Scaffold, Rigging and struck-by, Electrical) driven by C1–C5 evidence. The **mastery gate** [Cook2013] is that Friday unlocks only when each prior area is at "competent" or better. Otherwise that day is replayed with a new seed.

### 5.3 Hazard state machine
| State | Entry | Exit |
|---|---|---|
| **Latent** | Spawned by the day seed | Photographed → *Reported*; trigger time reached → *Incident* |
| **Reported** | Valid photo, tagged and rated | Fix/Assign/Stop chosen → *Controlled(level)* |
| **Controlled: Engineered/Eliminated** | Hands-on install succeeds | Terminal for the day |
| **Controlled: Admin/PPE** | Assign or PPE chosen where an engineered control was feasible | Lapse time (seeded, later in the day) → *Lapsed*; if PPE was the correct control, terminal |
| **Lapsed** | A weak control decays | Re-reported → *Reported*; trigger → *Incident* |
| **Stopped** | Stop-work called | Control installed → *Controlled*; stop lifted without a fix → *Latent* (flagged) |
| **Incident** | Trigger reached while Latent or Lapsed | Near-miss or recordable clip → review queue |

Look-alikes have only *Latent → Reported(false)*. Their cost is time and tablet-precision evidence, **never** a safety penalty. Reporting stays cheap.

**Edge cases:**
- Photo framing is a cone test from the tablet camera: the hazard bounds must be fully in frame, the longer side must span at least 30% of the view (*starting value*; changed from 15% of the area, which ladders and poles could never meet), and nothing may occlude them.
- Two hazards in one photo produce two reports.
- Leaving the zone mid-install returns the prop to its start pose.
- Lapse and trigger timers pause while the player is in the review or tablet menus.

### 5.4 Crew and NPCs
- **Dolores** is the mentor (radio and in-person). She delivers feedback and worked examples, backed by the grounded LLM with an offline fallback.
- **Ray** is the foreman. He brings pressure and pushes back on stops.
- **Marisol** is the concrete/utility lead and speaks Spanish.
- **Tyler** is a new hire who never speaks up (a model for N4).
- **Earl** is a veteran who cuts corners.
- There are 6–8 background workers.
- The LLM speaks *as* the character but can read only authored facts. It never changes state.

---

## 6. Balance and abuse checks
| Exploit | Countermeasure |
|---|---|
| Photograph everything | Any surface can be photographed and the crosshair prompt is the same everywhere, so the camera never singles out conditions. Each photo costs 5 s of shift time, a measurement 10 s, and a false alarm 15 s (the crew explains why it is compliant). Precision is part of CP evidence. Crew trust dips only on *repeated* false reports of the same object. "Checked · compliant" gives positive discrimination evidence, with feedback held back until the debrief so it can't be used to probe. |
| Stop-work spam | A stop must name a reported hazard. Unjustified stops cost schedule only, never the CP rating (safety culture: stopping is never wrong to try). A repeat stop on the same hazard still holds the crew but earns no XP or trust, and crew idle time is shown as the schedule cost. |
| End the shift early | "Zero Recordables" needs the full shift, and ending early asks for confirmation. Unfound hazards count as missed. |
| Read the answer off the UI | Before a report, the tablet shows a neutral name shared by the hazard and its look-alike. Mission cues mark a jittered zone, never the condition, and finds made under a cue (or the tutorial beacon) are flagged `cued` (0.5 mastery). Instrument readings are raw values, never the verdict. |
| PPE on everything | Lapses fire later in the day. CP C4 evidence weights the hierarchy level. |
| Wait for incidents to reveal hazards | Incidents are recorded and lower CP C1. The review shows what was missed, not an answer key for the same seed, because replays re-roll the seed. |
| Memorize the layout | The seeded pools plus the daily site evolution plus Week 2 mean no fixed answer key. |

---

## 7. 5-component evaluation
- **Clarity:** the tablet shows the day's work and the crew locations, never hazard markers. Photo validity gets instant shutter and ping feedback. Lapses announce themselves over the radio ("Earl's off his lanyard").
- **Motivation:** named crew with persistent trust, the week-long project arc, and a CP certificate at the end.
- **Response:** the photo is instant, installs snap in 0.28 s (v1 value), and radio stop-work is one button.
- **Satisfaction:** installs have snap + sound + worker reaction + a trust tick. The daily report is a satisfying "shift closed" moment.
- **Fit:** every verb is a real competent-person task: inspect, document, rate, correct, stop, brief.

---

## 8. Realism ("현장감") targets and guardrails
- **Density:** 150+ distinct props with wear, mud and rust. **Weathering parity** between hazards and look-alikes.
- **Life:** the crew performs real work loops, and equipment runs cycles with backup alarms. Audio doubles as cueing (alarm = struck-by risk).
- **Light and look:** HDRI, soft sun, URP post-processing, decals. The site evolves daily.
- **Cinematics (Higgsfield):** Monday opening, day-intro establishing shots, near-miss "what almost happened" clips (no gore) and the week-end outro.
- **Load guard:** nothing decorative moves or labels itself inside the view cone of an active hazard.

---

## 9. Telemetry (JSONL, PII-free) ↔ BORIS codes
| Event | BORIS proxy | Competency |
|---|---|---|
| `photo`, `gaze_dwell` | Identify Task Spot | C1 |
| `tag`, `risk_rate` | Identify Task Hazard | C2, C3 |
| `control_choose`, `install_attempt`, `install_success`, `lapse` | Efficient Task Completion / Repeated Error | C4 |
| `why_choice`, `toolbox_talk` | General Feedback Verbalization | Reflect |
| `radio_query` | Clarification Sought | — |
| `stop_work`, `speakup_choice`, `foreman_comply` | Hazard Discussion | C5 |
| `feedback_shown` → same error | Feedback Ignored | — |
| `revisit_area` | Revisiting Areas | — |
| `idle_60s`, `tablet_open_no_action` | Confusion / Display Distraction | — |

Each session also logs profile, language, input mode, day and seed. The instructor export is CSV plus a per-learner CP summary.

External validation: the Hazard Safety Engineering Assessment (pre, post and 4 weeks) plus a matched physical task [Makransky2019b].

---

## 10. Playtest scenarios
1. **New player:** can they infer that "photo = report" with no text, from Monday's shadow walk alone?
2. **Stress:** spam photos and stops; leave mid-install; open the tablet during a live event.
3. **Skill:** does an experienced player's CP rise faster and with fewer lapses?
4. **Abuse:** the §6 table.
5. **Readability:** can an observer say why a near-miss happened from the replay alone?

---

## 11. Out of scope (v2)
- Multiplayer and crew play.
- Quest standalone.
- Custom face rigs.
- The old warehouse, fire and chemical sites.
- Eye-tracking hardware (head-gaze proxy, `IGazeSource` seam).

## 12. Assumptions
- ASSUMPTION: lapses of weak controls on a timer are a fair abstraction of real PPE and admin-control failure. IMPACT: this carries the hierarchy lesson. IF WRONG: learners read it as arbitrary. VALIDATE: SME walkthrough, plus a post-play item: "why did the harness fix fail?"
- ASSUMPTION: a 10-minute day is enough for 4–5 hazards plus live events. VALIDATE: §5.1 test.

## 13. Serious-game design sources (verified via Crossref)
- Arnab, S., Lim, T., Carvalho, M. B., Bellotti, F., et al. (2015). Mapping learning and game mechanics for serious games analysis. *BJET, 46*(2), 391–411. https://doi.org/10.1111/bjet.12113
- Clark, D. B., Tanner-Smith, E. E., & Killingsworth, S. S. (2016). Digital games, design, and learning: A systematic review and meta-analysis. *Review of Educational Research, 86*(1), 79–122. https://doi.org/10.3102/0034654315582065
- Connolly, T. M., Boyle, E. A., MacArthur, E., Hainey, T., & Boyle, J. M. (2012). A systematic literature review of empirical evidence on computer games and serious games. *Computers & Education, 59*(2), 661–686. https://doi.org/10.1016/j.compedu.2012.03.004
- Habgood, M. P. J., & Ainsworth, S. E. (2011). Motivating children to learn effectively: Exploring the value of intrinsic integration in educational games. *Journal of the Learning Sciences, 20*(2), 169–206. https://doi.org/10.1080/10508406.2010.508029
- Wouters, P., van Nimwegen, C., van Oostendorp, H., & van der Spek, E. D. (2013). A meta-analysis of the cognitive and motivational effects of serious games. *Journal of Educational Psychology, 105*(2), 249–265. https://doi.org/10.1037/a0031311


---

## 14. Progression, NPCs and bonus hints (game mechanics)

**Levels.** XP comes from evidence, never from time spent.

| Level | Unlocks | Scaffolding (fades with level; N9) |
|---|---|---|
| 1 Trainee | Mon–Tue, pickup truck | Dolores shadows; free hints; energy wheel suggests 3 candidates |
| 2 Site Walker | Wed (deck/scaffold), dump-truck ride-along | Free hints end; hint tokens start |
| 3 Safety Tech | Thu (roof + crane pick), crane signal-person role | Risk card no longer shows the expert range |
| 4 Competent Person (per area) | Fri capstone, Week 2 transfer, equipment-yard practice | No cues; reflection prompts only |

**XP sources.** All are deterministic and come from `DaySession` evidence:
- hazard found (+HII)
- correct energy tag
- risk rating within ±1
- highest feasible control, with a bonus for elimination or engineering
- justified stop-work
- a toolbox talk that orders the day's highest-severity finding first

**Streak bonus.** Three correct reports in a row with no look-alike errors.

Stop-work never costs XP (invariant).

**Badges:**
- Focus Four area badges: Falls, Caught-in, Struck-by, Electrical
- "Stopped the Line"
- "Zero Recordables Week"
- "Hierarchy Hawk": 5 engineered controls in one day
- "Clean Air": silica controls

**Bonus hints (hint economy)**
- **Earning tokens:** 1 token per streak and 1 per good explain-back. The Guided profile starts with 3.
- **Spending tokens:** hints are tiered, and each tier costs 1 token:
  1. Zone nudge ("Check the north deck edge.")
  2. Energy nudge ("Think gravity.")
  3. Dolores walks over and points.
- **Effect on scoring:** a hinted find still counts toward HII, but its C1 evidence is flagged `hinted`. It earns half XP, and mastery counts it at 0.5. This fades support without punishing help-seeking.

**NPCs**
- **Crew:** Dolores (mentor), Ray (foreman), Marisol (ES), Tyler (new hire), Earl (veteran), plus 6–8 background workers.
- **Behaviour state machine:** every NPC is in one of these states:
  - Working: a task loop at a work area.
  - Walking: on the walkway, or on the haul road when careless.
  - Reacting: turns to the player on stop-work and stops the task.
  - Lapsing: removes PPE or unclips when a weak control lapses; the change is visible.
  - SpeakingUp: at high crew trust, self-reports a hazard over the radio.
- **Operators:** the excavator, crane and trucks are NPC-driven along the day timeline. The player gives signals as the crane signal person, or orders a stop.

## 15. OSHA-standard feedback (information delivery)

Every piece of feedback is **anchored to the governing standard**. The deterministic engine owns the content. The LLM coach may paraphrase, but it must quote the citation card verbatim.

**Feedback card.** At most 12 words, plus a citation chip, for example: `Unprotected edge over 6 ft — 29 CFR 1926.501(b)(1)`.

Tapping the chip opens the **Standard panel**, which shows:
1. The requirement in plain language.
2. The numeric threshold(s).
3. What compliant looks like, using a photo of the compliant look-alike from the same site.
4. The hierarchy level chosen versus the best feasible level.

**Timing.**
- Critical errors get the card immediately.
- The full Standard panel appears in the end-of-day review. Missed hazards there are grouped by energy source and by CFR subpart.

**Source of truth.** The fields `HazardDefinition.cfr`, `.requirementPlain`, `.threshold` and `.compliantExample`. Every citation comes from `docs/PropBible.md` or the eCFR and carries a verification status. Unverified items show "SME check" in dev builds and are blocked from study builds.

## 16. Topic coverage beyond the Focus Four (OSHA 10-Hour Construction scope)

| Topic (29 CFR 1926) | In-game situation | Day |
|---|---|---|
| Silica / sand and concrete dust (1926.1153, Table 1) | Dry cutting pavers or concrete; dry sweeping; the haul road raising dust with no water truck running | Mon–Fri |
| Electrical ground fault / leakage (1926.404(b)(1) GFCI or assured grounding; 1926.416 damaged equipment) | Tool on a non-GFCI outlet; a nicked cord lying in a puddle; a spider box missing its cover; a tripped GFCI "reset" by bypassing it | Mon, Wed, Fri |
| Overhead power lines (1926.1408/1410; 1926.416) | Pump-truck boom or dump bed near the line | Fri |
| PPE (Subpart E: 1926.95/100/102) | Missing hard hat or glasses; wrong glove for the task | All |
| Noise (1926.52/101) | Cut-off saw used without hearing protection | Mon, Wed |
| Heat illness (General Duty Clause; OSHA heat guidance) | Empty water station; no shade; a new worker who is not acclimatized | Mon, Thu |
| Hazard communication (1926.59 → 1910.1200) | Unlabelled fuel or solvent container; SDS binder missing | Mon, Tue |
| Fire protection (1926.150/151) | Blocked extinguisher; fuel stored near the generator exhaust; missing inspection tag | Mon, Thu |
| Materials handling and storage (1926.250) | Unstable stack; material stored within 6 ft of the roof edge | Wed, Thu |
| Hand and power tools (1926.300–307) | Guard removed from a saw; air hose with no whip check | Mon, Wed |
| Motor vehicles and mechanized equipment (1926.600–602) | No backup alarm; seat belt unused; equipment left unattended with the bucket raised | Tue, Fri |
| Cranes and rigging (Subpart CC; 1926.1419 signals) | Load over workers; no tagline; outrigger not on cribbing; no signal person | Thu |
| Scaffolds (Subpart L), ladders and stairways (Subpart X) | See the §7 fall contexts | Wed–Thu |
| Excavations (Subpart P) | Trench box, spoil, egress, water, competent-person inspection | Tue |
| Concrete and masonry (1926.701(b) impalement) | Unprotected rebar dowels | Fri |
| Confined spaces (Subpart AA) | Pump-station **wet well** entry with no permit, no atmosphere test and no attendant | Thu–Fri |

The section numbers above are a design map only. Each one must be verified in PropBible and HazardDefinition before it ships (§15 rule).

## 17. Vehicles and rigged equipment

**Rideable vehicles:** crew pickup, dump truck, water truck, flatbed.
1. Walk up to the vehicle; a door-handle prompt appears.
2. Press E to open the hinged door (0.6 s animation).
3. The view moves to the seated cab camera, the door closes and the engine starts.
4. Drive with WheelColliders at site speed. The haul road limit is 8 mph, and speeding is logged.
5. Press E again to park, open the door and exit.

**Pre-use walk-around** (a mechanic under 1926.601/602). The player checks tires, lights, backup alarm, seat belt and fluid leaks. Driving without the walk-around is logged. A defect is planted in the walk-around to be found, for example a dead backup alarm.

**Rigged crane** (rough-terrain crane and boom truck). The part hierarchy is:
- `Carrier`
  - `Outrigger×4`: extend and jack down onto cribbing.
  - `Slew`: Y rotation.
    - `Boom`: luff angle.
      - `Tele1..n`: extend.
      - `HoistLine` → `HookBlock` → `Load`: the load swings as a pendulum.

The player drives the crane from the operator seat only in equipment-yard practice (Level 4). Otherwise an NPC operator runs it, and the player acts as the **signal person**, using a hand-signal radial based on the 1926.1419 chart: hoist, lower, swing, stop.

**Other rigged machines:**
- Excavator: slew, boom, stick, bucket, tracks.
- Backhoe: loader arm, backhoe arm, stabilizers.
- Telehandler: boom, forks.
- Scissor and boom lifts: platform lift.
- Roller: drum rotation and vibration.

**Build route.**
1. Generate the mesh in Tripo.
2. Run `Tools/blender/rig_vehicle.py`. It splits the mesh into parts using authored cut volumes (one JSON file per model), sets pivots on the hinge and axle axes, names the hierarchy, and exports an FBX.
3. Add the Unity `VehicleController` or `CraneRig` component.

## 18. Episodes and cinematic narrative

The week is split into topic-scoped episodes. Each can be run on its own, so an instructor can assign one OSHA-10 topic. Played in order they form one arc: a new competent person, Dolores the mentor and Ray the foreman under schedule pressure, all heading toward Friday's pour.

| EP | Title | Day | Topic | Status |
|---|---|---|---|---|
| 1 | First Light | Mon | Site entry, PPE, temporary power (1926.95, 1926.404(b)(1), 1926.416(e)(1)) | playable |
| 2 | The Cut | Tue | Trenching, silica, swing radius (1926.651/652, 1926.1153) | playable |
| 3 | The Edge | Wed | Guardrails, holes, ladders (1926.501/502, 1926.1053) | playable |
| 4 | The Pick | Thu | Crane, rigging, roof (1926.1402/1419/1424, 1926.501(b)(10)) | in production |
| 5 | Under the Line | Fri | Capstone: power lines + pump truck (1926.1408, 1926.416) | in production |

Flow: episode select (orbit over the site) → title card (Higgsfield key art, `Resources/Episodes/EP*.jpg`) → narrated in-engine flythrough (letterboxed, speaker plates, skippable) → gate check-in → topic toolbox quiz → shift → topic closing quiz → epilogue radio lines that hook the next episode → best XP stored per episode.

Data lives in `Scripts/Core/Episodes.cs`, and the runtime is `EpisodeDirector`. The intros are in-engine rather than generated video: a Higgsfield clip costs 35 credits and would not match the playable site.

## 19. Career: points, rewards, hints, gear

- **Safety Points (SP)** = episode XP + 100 per badge (Stopped the Line, Zero Recordables, Hierarchy Hawk). Lifetime XP sets level/rank: Trainee → Crew Lead → Site Lead → Competent Person.
- **Gear locker** (menu tab): real CP instruments bought with SP, some gated by level. They add **information, not protection**, and the learner still decides:

| Gear | SP | Lv | Effect |
|---|---|---|---|
| CP logbook | 100 | 1 | +1 hint token per shift |
| Laser distance meter | 150 | 1 | photo range 5 → 8 m; measured heights, gaps, depths |
| Receptacle / GFCI tester | 150 | 1 | GFCI-protected or not on temporary outlets |
| Pocket penetrometer | 250 | 2 | soil strength reading → Type A/B/C |
| Real-time dust monitor | 300 | 2 | respirable-dust reading at silica tasks |

- Readings are authored per condition (`Editor/InstrumentReadings.cs`). Look-alikes read "OK", so an instrument confirms a judgement rather than replacing it.
- **Hints** come from Dolores over the radio, one tier per token: area → energy / Focus Four → the condition. A hinted find earns half XP (existing rule). The base is 1 token per shift, and the logbook adds 1.
- Costs and levels are starting values. Target: one clean episode buys about one item. Tune after playtests.
- Career persists in PlayerPrefs (IndexedDB on WebGL) through `CareerStore`.
