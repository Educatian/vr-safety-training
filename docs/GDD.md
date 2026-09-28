# GDD — Civil Jobsite: Focus Four (vr-safety-training v2)

Revamp of the 2026-07 prototype (5 shallow multi-industry sites) into **one realistic civil/commercial jobsite** built around OSHA's construction **Focus Four**: falls, caught-in/between, struck-by, electrocution. Collaboration line: [[David Awoyemi]] (IVR civil-engineering safety study, BORIS codebook) and the NIOSH R21 RoofSafe-XR concept (recognition → control).

Platform: OpenXR PC VR (Quest Link / SteamVR) + desktop first-person fallback. One scoring rule for both modes.

## 1. Learning construct

Learners must move from **recognition** to **control**:

1. **Recognize.** Spot a hazard in a dense, live jobsite. Nothing is highlighted before inspection.
2. **Classify.** Tag the hazard with its Focus Four category.
3. **Control.** Choose a control from the hierarchy of controls and physically perform it: install, place, reposition, tie off, or tag out.
4. **Escalate.** Use stop-work authority when a hazard is beyond the learner's role, such as a power line near a boom or an unprotected trench with a worker inside.

The deterministic engine owns hazards, correctness, order and score. The LLM coach explains but never changes an outcome, same as v1.

## 2. Core loop (≈ 25–35 min session)

```
Site gate: toolbox talk + PPE check
  → walk the jobsite (4 zones, continuous, no portals)
    → inspect object → classify (Focus Four radial)
      → correct: control task unlocks → grab/place/tie-off → snap + feedback
      → safe look-alike: −25, coach explains why it is compliant
    → stop-work call where required
  → zone debrief (coach) → next zone
→ end-of-shift debrief: recognition vs. control profile
```

## 3. Zones and scenario content

Each zone has **3 real hazards + 2 compliant look-alikes**. OSHA 29 CFR 1926 values are authored as data so a subject-matter expert can revise them.

| Zone | Real hazards (control task) | Compliant look-alikes |
|---|---|---|
| **A. Falls: 2-storey frame + low-slope roof** | Unprotected leading edge at more than 6 ft (install guardrail: top rail 42″ ± 3″, midrail, toeboard). Uncovered floor opening or skylight (place a secured cover marked "HOLE"). Extension ladder short of the landing (reset it to extend 3 ft above the landing at a 4:1 angle). | Guardrailed stair opening. Worker tied off to a rated anchor with a full-body harness. |
| **B. Caught-in: trench / excavation** | 6 ft trench with no protective system (lower the trench box). Spoil pile within 2 ft of the edge (relocate the spoil line). No egress ladder within 25 ft (place a ladder). | Sloped bench cut. Competent-person inspection tag present. |
| **C. Struck-by: equipment yard** | Excavator swing radius not barricaded (set barricade and cones). Worker in the dump truck's blind spot while it backs up (call stop-work and route the worker out). Suspended load with no tagline (attach the tagline). | Spotter with a hi-vis radio. Secured material stack. |
| **D. Electrical: temp power + overhead line** | Boom or dump bed within 10 ft of an overhead line (stop-work, then place line-proximity markers). Damaged extension cord in use (tag out and swap). Tool not on a GFCI (plug it into the GFCI). | Cord on a cord ramp. Covered, labeled temporary panel. |

Gate zone: PPE donning (hard hat, glasses, vest, gloves, and a harness for zone A) using the v1 placement mechanic.

## 4. Realism targets ("현장감")

A player should believe it is a real jobsite before they read any text.

- **Density.** 150+ distinct props with wear, dirt, mud, rust and label decals. No untextured primitives in view.
- **Life.** Animated workers (Rocketbox) doing idle work loops: hammering, carrying, spotting. Heavy equipment runs idle loops: excavator arm cycles, dump truck backing with an alarm.
- **Sound.** Layered ambience: generator hum, backup alarm, nail gun, distant traffic, wind on the roof. Spatialized per zone.
- **Light.** HDRI sky, sun with soft shadows, URP post-processing (tonemapping, AO, bloom on work lights, subtle haze), and decals for tire tracks, puddles and oil stains.
- **Readability guard.** Realism never becomes a cue. A hazard and its look-alike share material quality, so the answer is never "the ugly one."

## 5. Feel targets (5-component filter)

- **Clarity:** only the current control task's prop and destination become active after classification.
- **Motivation:** zone progress ring plus a shift-report card.
- **Response:** props follow the hand continuously and snap in 0.28 s. Heavy items (trench box, barricade) use two-hand or crane-assisted placement.
- **Satisfaction:** snap sound, installed-state visual, coach nod or line, and score tick.
- **Fit:** every control is a real control performed at its real location.

## 6. Research instrumentation

JSONL events stay PII-free and are mapped to the IVR study's BORIS codes so v2 sessions can be compared with the N = 27 cohort:

| Event | BORIS code proxy |
|---|---|
| `inspect` (first gaze-dwell or select on target) | Identify Task Spot |
| `classify` (correct / incorrect category) | Identify Task Hazard |
| `control_attempt` / `control_success` | Efficient Task Completion / Repeated Error |
| `coach_query` | Clarification Sought |
| `stop_work` | Hazard Discussion (escalation) |
| `revisit_zone` | Revisiting Areas |
| `idle_confusion` (no progress for 60 s or more) | Confusion / Unresolved Problem |

## 7. Out of scope (v2)

- Multiplayer.
- Quest standalone build.
- Custom face-rigged characters. Rocketbox is kept; a `char-pipeline` pass comes later.
- Warehouse, fire and chemical sites. They are dropped; git history keeps them.
