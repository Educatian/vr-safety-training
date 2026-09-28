# Tasks — v2 revamp

Each task ends green: compile, errors-only console, test or screenshot, then commit `[T#] ...`.

## M0 — Foundation
- [ ] T0.1 Upgrade the project to Unity 6000.3.25f1. Resolve API updater changes and get the existing EditMode tests green.
- [ ] T0.2 Add URP with PC and XR renderer assets. Convert materials (Rocketbox and Poly Haven) and fix pink shaders.
- [ ] T0.3 Install the MCP for Unity bridge (stdio 6400) and verify `unity status` returns ready.
- [ ] T0.4 Add a project `CLAUDE.md` from the template and commit.

## M1 — Core + greybox
- [ ] T1.1 `Jobsite.Core`: add `EnergySource`, `FocusFourCategory`, `ControlLevel` (hierarchy of controls), `HazardDefinition` (POCO) and `ZoneSession` (inspect → classify → P×S rating → control level → control execution → stop-work), plus HII and the GDD §3 scoring. EditMode tests cover the scoring table, order gating, repeat-safety, look-alike penalties, PPE-only partial credit and stop-work latency.
- [ ] T1.2 Add the `ZoneDefinition` and `HazardDefinition` ScriptableObjects with **hazard pools and a seeded sampler** (3–4 real + 2 look-alikes per run). Author the GDD §4 pools, including cross-cutting and capstone, with the expert key for P×S ratings and the correct control level.
- [ ] T1.3a HF-LAYOUT: top-down site plan and 5 zone mood boards (gate + A–D), with sightlines, hazard hiding spots and look-alike placement. User signs off before greybox.
- [ ] T1.3 Build a greybox jobsite scene (`Assets/_Game/Scenes/Jobsite.unity`): terrain and grade, 2-storey frame with roof, trench cut, equipment yard, temp power, and an overhead line. It must be walkable with XR and desktop.
- [ ] T1.4 Port the placement mechanic to `ControlTaskInteractable`, then add the tie-off and two-hand variants.
- [ ] T1.5 Radials: energy-wheel classify, P×S rating, hierarchy-of-controls choice, and a "why?" 3-option. Add `StopWorkCall` and the field-tablet HUD, capped at 12 words per panel.
- [ ] T1.8 Gate-trailer pre-briefing (2D slides/video) and learner profile (years on site + 3-item pretest) → Guided or Field mode. In Guided mode, signaling cues fade over the session.
- [ ] T1.9 Foreman pressure NPC and schedule clock; dynamic late-session events (truck backing, swing load).
- [ ] T1.6 Extend the logger with the GDD §6 events, BORIS proxy codes and session meta (profile, language, input mode, config seed). Head-gaze dwell goes through `IGazeSource`.
- [ ] T1.7 Delete the v1 generators, generated assets and the old scene. Tests stay green.

## M2 — Asset wave 1: falls + trench (compliance geometry)
- [ ] T2.1 B-PROC `Tools/blender/guardrail.py`: parametric guardrail with a 42″ top rail, 21″ midrail and 3.5″ toeboard, plus posts and clamps.
- [ ] T2.2 B-PROC: hole cover (plywood with a "HOLE" stencil and cleats), extension ladder (rungs, rails, feet, 3 ft extension), and frame scaffold (base plates, mudsills, planks, guardrails).
- [ ] T2.3 B-PROC: aluminium trench box with spreaders, a trench-cut ground mesh with a spoil pile, and a sloped bench.
- [ ] T2.3b Tripo prop wrapper `Tools/tripo/tripo_prop.py`: image→model with no rig. Reuses the charpipe key, ledger and budget cap, and has a dry-run plan mode.
- [ ] T2.4 TR-3D: harness + lanyard, roof anchor + SRL, and a tool bag.
- [ ] T2.5 B-LIB: lumber stacks, sheathing, nail guns, and a roof membrane texture. Poly Haven soil, gravel and concrete PBR.
- [ ] T2.6 HF-IMG: signage and decal set 1 (excavation danger, hard hat area, competent-person tag, "HOLE" stencil, mud and tire decals).

## M3 — Asset wave 2: struck-by + electrical + life
- [ ] T3.1 B-LIB: excavator, dump truck, mobile crane and skid steer. Clean up in Blender, add LODs, and separate the pivots (boom, bucket, bed) for animation.
- [ ] T3.2 B-PROC: barricade and cone set, cord ramp, line-proximity marker flags, and a trench pump with discharge hose.
- [ ] T3.2b Cross-cutting props: concrete saw (dry vs. wet kit), leaking hydraulic hose, air-hose whip check, and a water/shade station (B-LIB or TR-3D).
- [ ] T3.3 TR-3D: generator, GFCI spider box, temp panel, porta-john and site trailer.
- [ ] T3.4 HF-AUD: ambience beds for 4 zones plus about 15 one-shots, mixed with `JobsiteAmbience`.
- [ ] T3.5 Workers: Rocketbox in `WorkerLoop` idle work animations (8–12 NPCs), and equipment idle animation.
- [ ] T3.6 HF-CINE: gate orientation, 4 zone intros, 4 near-miss consequence clips (no gore) and the AAR outro, wired through `VideoPlayer` (in-world TV or 2D overlay). EN/ES VO through `generate_audio`.

## M4 — Look + feel
- [ ] T4.1 Lighting: HDRI, sun, work lights, reflection probes, and a URP post-process volume.
- [ ] T4.2 Decal pass and dirt/wear material variants. Run the look-alike parity check (GDD §4 readability guard).
- [ ] T4.3 Coach: grounded fact sets per `HazardDefinition`. Immediate elaborated feedback (why + CFR + consequence); a repeated error triggers a worked-example replay; explain-back pause at each zone end; after-action review (path replay, missed hazards by energy, expert replay).
- [ ] T4.5 EN/ES localization (UI + coach voice). Session modes: single zone (toolbox talk), full shift, transfer shift (novel configurations), and 4-week booster.
- [ ] T4.4 Performance pass: LODs, static batching, occlusion. Target 90 fps.

## M5 — QA + ship
- [ ] T5.1 Gauntlet critic loop, 3 rounds max, until no high-severity defects remain.
- [ ] T5.2 PlayMode smoke test: complete every zone in desktop mode through scripted input.
- [ ] T5.3 Windows build, README and docs images refreshed, then PR `revamp/v2 → main`.
