# GDD — Civil Jobsite: Focus Four (vr-safety-training v2)

Revamp of the 2026-07 prototype, which had 5 shallow multi-industry sites, into **one realistic civil jobsite** built around OSHA's construction **Focus Four**: falls, caught-in/between, struck-by and electrocution.

- **Collaboration line:** [[David Awoyemi]] (IVR civil-engineering safety study, BORIS codebook, Hazard Safety Engineering Assessment) and NIOSH R21 RoofSafe-XR (recognition → control).
- **Platform:** OpenXR PC VR plus a desktop first-person build. Both use one scoring rule, and the desktop build is a full learning path, not a lesser version [Wu2020].
- **Evidence base:** `Desktop\_research\2026-09-28_simulation-based-construction-safety-training-design.md`. Bracketed keys cite that note.

---

## 1. Training needs analysis → design requirements

| # | Training need (evidence) | Design requirement |
|---|---|---|
| N1 | Workers recognize about 57% of Focus Four hazards, about 18% of other hazards, and under 10% of pressure and chemical hazards [Albert2020; Uddin2020]. At most 6.7% of method statements identify every hazard [Carter2006]. | Score recognition as a **Hazard Identification Index (HII)** = found ÷ present, not pass/fail. Include **non-Focus-Four hazards** (silica, noise, heat, pressure) so learners don't develop tunnel vision. |
| N2 | Misses come from selective attention, an unknown hazard set, and underrated risk [Jeelani2017a]. Workers weight severity over probability [Perlman2014]. | **Energy-source classification** (energy wheel) [Albert2014], **compliant look-alike pairs**, and a separate **probability × severity rating**. |
| N3 | Recognizing a hazard is not controlling it. The course assessment targets the **hierarchy of controls** and the misconception that "PPE is the first line of defense" (Hazard Safety Engineering Assessment). | Control is a **choice across the hierarchy**: elimination, engineering, administrative, PPE. The learner then performs the chosen control. Higher-level controls score more, and PPE-only is partial credit where engineering is feasible. |
| N4 | Production pressure suppresses speaking up [Han2014; Huang2025]. | **Schedule pressure is a scored mechanic.** A foreman NPC pushes to "just get it done," and the player has **stop-work authority**, scored on latency and assertiveness. |
| N5 | Struck-by vigilance decays over a session [Hussain2024]. Most struck-by deaths involve vehicles (CPWR 2021). | **Dynamic hazards appear late**: a dump truck backs up while the learner is mid-task, and a load swings. |
| N6 | In our IVR data, learners were text-driven (Instruction Read, 512 events) and **Display Distraction → Confusion** was a top-10 transition. Headsets raise load and can cut learning [Makransky2019a]. Presence correlated inversely with HII [Eiris2020b]. | **Voice first, icons first, minimal text.** In-world HUD capped at 12 words per panel. Realism serves authenticity; any detail near a teaching target must be functional, not decorative. |
| N7 | Pre-training helps only in VR (d = 0.81) [Meyer2019]. Segmenting with generative pauses restores learning [Parong2018]. | **Trailer pre-briefing** (desktop/2D): equipment names, the energy wheel, the hierarchy of controls. Each zone ends with a **30-second "explain it back"** pause. |
| N8 | Elaborated feedback (ES 0.49) beats right/wrong (0.05) [VanDerKleij2015]. Explanatory feedback beats corrective [Moreno2005]. The modal Verbal Collaborator cluster in our data **repeats errors and ignores feedback** (1.42×). | **Immediate, elaborated feedback on critical errors** (why + CFR + consequence). A **"why?" 3-option radial** after key decisions [Johnson2010]. Repeating the same error triggers a **worked-example replay** instead of more text. |
| N9 | Novices gain from examples; experts gain from reflection [Chernikova2020]. Experience shapes attention [Hasanzadeh2017]. | Choose the **learner profile** at the gate (years on site plus a 3-item pretest): *Guided* (worked example, signaling cues that fade [Wouters2013]) or *Field* (no cues, reflection prompts). |
| N10 | Debriefs add about 25% (d = 0.67) [Tannenbaum2013]. Showing missed hazards by energy type personalizes feedback [Jeelani2017b]. | **End-of-shift after-action review.** Replay the learner's path, list missed hazards grouped by energy source, and give the coach's reflection on an **expert's correct replay** [Moreno2005]. |
| N11 | Knowledge decays by 4 weeks [Stefan2024; Feng2024]. The real target is transfer to untrained hazards [Xiong2026; Sacks2013]. | **Hazard pools with randomized configurations** each run, **mastery gates** [Cook2013], a **novel-configuration transfer shift**, and a **4-week booster shift**. |
| N12 | Hispanic workers have higher fall-fatality odds (OR = 1.48) [Dong2009]. Training has a median of 1 hour with little written material [O'Connor2005]. Localization works best co-designed [Evia2011]. | **EN/ES from v2.0** (voice + UI). Zones are **8–12 minutes each**, so one zone fits toolbox-talk time. No gore; consequences are shown through near-miss replays. |

**Burden weighting** (BLS CFOI 2024: falls were 370 of 1,032 construction deaths; OSHA fall protection has been #1 in citations for 15 years; CPWR: construction has 85% of trench deaths and 49% of electrical deaths):
- Falls get about 35% of hazard instances, split into 4 sub-types.
- Struck-by, trench and electrical get about 20% each.
- Non-Focus-Four hazards get about 5%.

---

## 2. Competency model (evidence-centered design [Mislevy2003])

| Competency | Evidence (observable) | Indicator |
|---|---|---|
| C1 Recognize | Hazard inspected before timeout. Gaze or head dwell on the hazard region. | HII, time-to-detect, gaze-dwell ratio |
| C2 Classify | Correct energy source and Focus Four tag | Classification accuracy |
| C3 Assess risk | Probability and severity ratings vs. the expert key | Absolute deviation per axis |
| C4 Control | Hierarchy level chosen, then the control executed correctly (location, order, completeness) | Control level, execution success, attempts |
| C5 Escalate | Stop-work or speak-up when required, including under foreman pressure | Latency, stop-work used, assertiveness choice |
| C6 Transfer | Performance on the novel-configuration shift vs. the trained shifts | ΔHII, ΔC4 |

External validation: Hazard Safety Engineering Assessment (pre/post/4-week), plus a matched physical task in the lab [Makransky2019b].

---

## 3. Core loop

```
Gate trailer: pre-briefing (2D) → profile → PPE check (hands-on)
 → Zone shift (8–12 min, randomized hazard config)
    walk + work task (carry, measure, deliver)      ← hazards surface inside real work
    inspect → classify (energy wheel) → rate P×S
      → choose control level (hierarchy radial) → perform control (grab/place/tie-off/two-hand)
      → "why?" radial on key decisions
    dynamic event (truck backing / swing load / foreman pressure) → stop-work?
    zone end: 30 s explain-back
 → after-action review (replay + missed-by-energy + expert replay)
```

Scoring:
- Recognition: +100 per hazard, −25 for the first look-alike.
- Classification: +25.
- Risk rating: +0–25 by accuracy.
- Control: +40 for elimination or engineering, +25 administrative, +10 PPE-only (full credit when PPE is the correct control).
- Stop-work: +50, with a latency bonus.

The deterministic engine owns every number. The LLM coach only explains.

---

## 4. Zones (hazard pools; each run samples 3–4 real hazards + 2 look-alikes per zone)

| Zone | Hazard pool (control task) | Look-alike pool |
|---|---|---|
| **A. Falls: 2-storey frame + low-slope roof** | Unprotected leading edge over 6 ft (guardrail: 42″ ± 3″ top rail, midrail, toeboard). Uncovered opening or skylight (secured cover stenciled "HOLE"). Ladder short of the landing or not at 4:1 (reset to 3 ft extension). Scaffold missing a guardrail or planking, or with no base plate (complete it). Worker unclipped at the edge (tie off to an anchor; stop-work). | Guardrailed stair opening. Worker tied off to a rated anchor. Compliant scaffold with a green tag. |
| **B. Caught-in: trench** | 6 ft trench with no protective system (trench box). Spoil within 2 ft of the edge (relocate the spoil). No ladder within 25 ft (place a ladder). Water in the trench (pump out and re-inspect). | Sloped bench cut. Competent-person inspection tag present. |
| **C. Struck-by: equipment yard** | Unbarricaded excavator swing radius (barricade). Truck backing with a worker in the blind spot (stop-work, spotter). Suspended load with no tagline (tagline). Unsecured material at height (secure it). | Spotter with a radio. Secured material stack. |
| **D. Electrical: temp power + overhead line** | Dump bed or boom within 10 ft of the line (stop-work, then place proximity markers). Damaged cord (tag out and swap). Tool not on GFCI (move it to GFCI). | Cord on a ramp. Covered, labeled panel. |
| **Cross-cutting (non-Focus-Four)** | Dry-cutting concrete (silica: switch to wet cutting). Leaking hydraulic hose or compressed-air whip (pressure). Heat and no water station. | Ear protection correctly worn near the generator. |
| **Capstone: combined** | An excavator digging a trench under an overhead line, with foreman pressure. Tests C1–C5 together. | |

---

## 5. Realism ("현장감") targets, with guardrails

Realism must make the site credible without becoming the answer key or adding load (N6).

- **Density:** 150+ distinct props with wear, mud and rust. Weathering is applied equally to hazards and look-alikes (parity rule).
- **Life:** 8–12 Rocketbox workers running work loops, and equipment running idle cycles with backup alarms.
- **Sound:** layered ambience per zone. Sound also carries cues: the backup alarm is a real struck-by cue.
- **Light:** HDRI, soft sun, URP post-processing, decals.
- **Load guard:** no decorative motion or text within the learner's view cone of an active teaching target.

---

## 6. Telemetry (JSONL, PII-free) ↔ BORIS codes

| Event | BORIS proxy | Competency |
|---|---|---|
| `inspect`, `gaze_dwell` | Identify Task Spot | C1 |
| `classify`, `risk_rate` | Identify Task Hazard | C2, C3 |
| `control_choose`, `control_attempt`, `control_success` | Efficient Task Completion / Repeated Error | C4 |
| `why_choice` | General Feedback Verbalization | C4 |
| `coach_query` | Clarification Sought | — |
| `stop_work`, `foreman_comply` | Hazard Discussion / (compliance under pressure) | C5 |
| `feedback_shown` → same error again | Feedback Ignored | — |
| `revisit_zone` | Revisiting Areas | — |
| `idle_60s`, `panel_open_no_action` | Confusion / Display Distraction | — |

Profile, language, input mode (XR or desktop) and hazard configuration seed are logged with each session to support modality and transfer analyses.

---

## 7. Out of scope (v2)

- Multiplayer and crew play.
- Quest standalone.
- Custom face-rigged characters.
- The warehouse, fire and chemical sites (kept in git history).
- Eye tracking hardware: head gaze is the proxy, with an `IGazeSource` seam for later.
