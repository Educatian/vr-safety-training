# Current build status

Updated: 2026-07-21

- Unity 6.0 LTS project scaffold: complete
- OpenXR + XR Interaction Toolkit package manifest: complete
- Microsoft Rocketbox NPC assets: 3 characters imported
- Training sites: Construction, Warehouse, Fire Response, Chemical Processing, Electrical Maintenance
- Hidden-answer inspection model: 2 hazards + 2 safe distractors per site
- Natural site props: platforms, guardrails, access obstructions, spill, pallets, extinguishers, egress routes, chemical drums/eyewash, electrical panels, lockout tags, cable ramps
- Deterministic scoring: +100 hazard, -25 first false positive, repeated selections 0
- Completion and debrief: per-site progress, total score, all-sites completion message
- Exploration: five XR/mouse-selectable site portals, continuous campus ground, bright route lanes, and desktop movement
- Startup safety: XR rig spawn height and runtime grounding guard prevent initial drop
- HUD polish: compact field-ops header, score readout, cyan accent, and wrapped guidance panel
- Construction practical: five ordered XR-grab actions (PPE, barricade, guardrail, material cart, final walkdown) with sequence gating, visual completion state, HUD coaching, and +20 per-step bonus
- Construction modeling pass: authored multi-part scaffold, PPE case, barricade, guardrail kit, material cart, and clipboard assemblies replace the single-block placeholders
- External asset pass: Poly Haven CC0 hand truck, ladder, cement bag, drill, and industrial barrel downloaded into `Assets/ThirdParty/PolyHaven/` and wired into the construction scene
- NPC chat: click a Rocketbox coach to open a live input panel; typed questions route to the grounded LLM service with deterministic offline fallback
- NPC idle behavior: timed weight shift, field-pointing/explanation gestures on humanoid rigs, generic-rig body sway, and conversation-aware head/talk motion
- Environment lighting: procedural sky, warm soft-shadow sun, site spotlights, tri-light ambient color, distance fog, and five baked reflection probes
- NPC dialogue: four-step progress-aware coaching/debrief prompt cycle per site
- Privacy-conscious event log: JSONL inspection outcomes only; no learner identity or conversation text
- Core scoring validation: passed after the inspection-model update
- LLM NPC conversation: OpenAI-compatible client + 8-turn rolling context
- Offline fallback: implemented
- Local model check: `hermes3:8b` returned a grounded construction-safety answer
- Runtime/editor source revalidation: passed with 0 warnings and 0 errors
- Unity license and scene generation: complete
- Generated scene: `Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity`
- Inquiry expansion: 31 evidence objects, 20 inspection targets, hypothesis/final-explanation flow, and site-specific evidence collection across five modules
- Spatial analytics: 31 authored zones, 1 Hz XYZ tracking, zone dwell, route distance, retries, coach turns, hypotheses, and final-report export to JSONL/CSV
- OSHA scenario catalog: authority-aware learner actions and federal construction references for every inspection target
- OpenXR Standalone Project Validation: 0 outstanding issues
- Unity EditMode tests: 124/124 passed
- Desktop/IVR mode: lobby toggle, Windows OpenXR/Meta Quest Link activation, no-runtime Desktop fallback, and forced IVR on Quest Android
- Meta Quest validation: Android OpenXR 0 outstanding issues; ARM64, IL2CPP, API 29+, Meta Quest Support, Touch profiles, Vulkan/OpenGLES3
- Meta Quest APK: succeeded, `Builds/MetaQuest/VR-Safety-Training-Quest.apk` (93.6 MB)
- Current visual tour: `Captures/pilot-quest-toggle` (38 frames; hub plus five expanded sites, real props, and NPC idle/walk/dialogue states)
- Pilot walkthrough: `docs/media/VR-Safety-Pilot-Demo.mp4` (43.6 seconds, compressed H.264)
- Automated 20-minute pilot: 1,200 simulated seconds; all five sites; 31 zones; 20 inspections; 31 evidence interactions; 25 hands-on successes; analytics map generated
- Desktop startup stability: passed with zero XR-root/camera positional or angular drift in the observation window
- VR performance default: Standalone quality reduced from Ultra to High; build output 161.8 MB
- Windows standalone build: succeeded, `Builds/Windows/VR-Safety-Training.exe`

## Prototype status

Feature-complete for a supervised engineering demo. Before an unsupervised learner study: run physical-headset performance/comfort testing, conduct at least five formative human pilot sessions, review accessibility, and obtain a host-site/qualified-safety-professional content review. See `docs/PILOT_VALIDATION_REPORT.md`.

## 2026-07-21 update (audit-driven overhaul)

- Sixth training site: Tower Crane apartment build (OSHA 1926 Subpart CC), with an
  11-segment animated tower crane (deterministic slewing jib and traveling trolley),
  five-story apartment frame, and three hazard / three look-alike inspection items.
- Item bank expanded to 3+3 per site: 36 inspection items across six sites.
- Every site gained a rear annex yard (walkable expansion, two extra analytics
  zones per site; 49 zones total).
- Learner-selected hypothesis plates (one evidence-consistent + two plausible foils
  per site) now gate report submission; first-attempt markers are logged.
- Mastery-based certification (seat-time requirement removed); score-neutral
  recertification retrieval round after certification.
- Delayed-feedback experimental condition (SAFETY_FEEDBACK_MODE=delayed) with
  telemetry logging of the assigned condition.
- Engineering stations: first-attempt scoring differentiation, consequence-preview
  visuals on unsafe choices, and no-worked-solution transfer variants for the three
  construction calculations.
- Telemetry hardening: schemaVersion=2, inquiry sequence numbers, resilient CSV
  export incl. scoring_events.csv, assessment events joined to the cloud collector
  (kind=200, verified end-to-end against the live worker).
- Instructor dashboard (Tools/dashboard) with BKT mastery overlay; in-world
  session debrief board in the lobby; HUD section toggle buttons.
- Unity EditMode tests: 191/191 passed.
