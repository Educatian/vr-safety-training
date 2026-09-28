# Competent Person — vr-safety-training v2 (Unity 6000.3.25f1, URP)

A serious game about construction safety, built as an OpenXR PC VR build plus a desktop build. The branch is `revamp/v2`. Specs live in `docs/`:
- `GDD.md` is the source of truth for design.
- `TechSpec.md` holds architecture and the asset routes.
- `Tasks.md` lists what may be built.
- `TestPlan.md` defines how each task is verified.

## Workflow
Follow the `engine-gamedev` skill: spec → one small edit → refresh → errors-only console → screenshot or play check → commit `[T#] ...`.
- Don't build anything that isn't in `docs/Tasks.md`.
- Never call two Unity editor tools in parallel. After 2 failures of the same call, stop and ask.
- Run `unity command eval` from Bash, not PowerShell.

## Design invariants (from the GDD; don't break them)
- The deterministic `Jobsite.Core` engine owns hazards, state, scoring and CP mastery. The LLM coach only speaks from authored facts and never changes state.
- Nothing is highlighted before inspection. Hazards and their look-alikes get equal weathering and art quality.
- Stop-work never lowers the safety or CP rating. Look-alike reports cost only time and precision evidence.
- The UI is diegetic (tablet or radio), with at most 12 words per card and voice first.
- Telemetry is PII-free JSONL, mapped to the BORIS codes in GDD §9.

## Allowed paths
- `Assets/_Game/**`
- `Assets/SafetyTraining/Scripts/**`: port from here only. The old code moves into `_Game` as it is rewritten.
- `docs/**`, `Tools/**`, `Packages/manifest.json`, `ProjectSettings/**` (URP/XR)

## Conventions
- Namespaces: `Jobsite.Core` (no UnityEngine), `Jobsite.Runtime`, `Jobsite.Editor`, `Jobsite.Tests`.
- Use `[SerializeField] private` and one MonoBehaviour per file.
- Input System + XR Interaction Toolkit. The UI is uGUI in world space.
- Asset prefixes: `SM_`, `M_`, `T_`, `PF_`, `SO_`, `SFX_`, `AMB_`, `VID_`.

## Assets
- 3D generation goes through **Tripo** only (`Tools/tripo/`). Never Hunyuan, and never Higgsfield 3D.
- Higgsfield handles images, layout boards, cinematics and audio.
- Compliance geometry (guardrails, ladders, trench box, scaffold) is scripted in Blender under `Tools/blender/`.
- Every generated mesh gets a Blender cleanup pass and is logged in `docs/AssetManifest.md` with its license.

## Never
- Hand-edit `.unity`, `.prefab` or `.asset` YAML when a tool exists.
- Delete `Library/` without asking.
- Commit secrets. LLM keys come from environment variables.
