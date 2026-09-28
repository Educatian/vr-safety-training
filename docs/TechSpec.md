# Tech Spec — v2 revamp

## Engine and packages

- Unity **6000.0.75f1 → 6000.3.25f1** (6.3 LTS).
- Render pipeline: **Built-in → URP**. The URP package and asset pair has separate PC and XR renderer assets.
- Keep: XR Interaction Toolkit 3.x, OpenXR, Input System, Newtonsoft, Test Framework, TextMeshPro.
- Add: `com.unity.render-pipelines.universal` and `com.coplaydev.unity-mcp` (editor bridge).
- Don't add anything else without approval.

## Reuse vs. replace

| Keep (port) | Replace |
|---|---|
| `Core/TrainingSession` scoring rule. Extend it with classify + control steps. | The 5-site `TrainingSiteId` enum becomes `ZoneId { Gate, Falls, Trench, StrikeBy, Electrical }`. |
| `HandsOnPlacementInteractable` state machine (Locked → Ready → Held → Released). | All `Editor/*Factory`, `*Dresser`, and `*Builder` procedural scene generators. The v2 scene is authored, not regenerated. |
| `TrainingEventLogger`, which gets new event types. | `SitePortal` and `SiteIsolationController`. The site is continuous, with no portals. |
| NPC chat stack: `IConversationService`, OpenAI-compatible client, offline fallback, chat panel. | Generated materials and meshes under `Assets/SafetyTraining/Generated*`. |
| `DesktopExplorerController` and `StartupGroundingGuard`. | Kenney sci-fi UI. The HUD is restyled as a field-ops tablet. |

## Architecture

- **Data-driven scenarios.** A `ZoneDefinition` ScriptableObject holds a list of `HazardDefinition` entries: id, category, isHazard, CFR reference, control task id, explanation, and look-alike rationale. An SME edits `.asset` files, not code.
- **`Core` stays engine-free** (no `UnityEngine`) and is unit-tested. New classes: `FocusFourCategory`, `ControlTask`, and `ZoneSession`. `ZoneSession` does inspect → classify → control, handles stop-work, and scores.
- **Runtime:**
  - `HazardTarget` binds a scene object to a `HazardDefinition`.
  - `ControlTaskInteractable` generalizes the placement mechanic and adds a tie-off variant and a two-hand variant.
  - `ClassifyRadial` is world-space UI that works in XR and on desktop.
  - `StopWorkCall` is the escalation action.
  - `JobsiteAmbience` handles zone audio mixing.
  - `WorkerLoop` handles NPC idle work animation.
- **UI:** uGUI (world-space). It is already used, and XRI supports it.

## Asset pipeline (Blender MCP + Tripo 3D + Higgsfield 2D/video/audio)

Every prop gets exactly one source route, recorded in `docs/AssetManifest.md` with a license column.

| Route | Tool | Use for |
|---|---|---|
| **B-PROC** | Blender MCP `execute_blender_code` (bpy script, saved under `Tools/blender/`) | **Compliance-critical geometry**, where dimensions matter for the lesson: guardrail system (42″/21″/3.5″), hole cover, trench box, extension ladder, frame scaffold, cord ramp, barricade, line-proximity markers. Fully parametric and re-runnable. |
| **B-LIB** | Blender MCP Poly Haven / Sketchfab (CC0 / CC-BY) / Poly Pizza, followed by a cleanup, scale and decimate pass in Blender | Heavy equipment (excavator, dump truck, mobile crane, skid steer), generic site clutter (pallets, barrels, lumber, rebar, tools), HDRI and PBR ground/soil/gravel/concrete textures. |
| **TR-3D** | Image reference (Higgsfield `generate_image`, clean product shot on a neutral background) → **Tripo API** image-to-model (no rig; via a prop wrapper over `CyberPlay_Lab/tools/charpipe/tripo_char.py` with its credit ledger and cap) → Blender cleanup, retopo/decimate, UV check, real-world scale, FBX | Specific props with no good library match: harness and lanyard, SRL on an anchor, porta-john, jobsite generator, GFCI spider box, tool bags, material hoist bucket, site trailer, concrete saw, water/shade station. |
| **HF-LAYOUT** | Higgsfield `generate_image` | **Layout design** before greybox: a top-down site plan and one key-art/mood board per zone (camera height 1.7 m) showing sightlines, where each hazard hides inside the work, and look-alike placement. The boards are the blockout reference, not in-game art. |
| **HF-CINE** | Higgsfield `generate_video` (+ `generate_audio` for VO/score) | **Cinematics**, pre-rendered and played through Unity `VideoPlayer` on in-world screens or a fade-to-2D overlay: gate orientation (30–45 s), 4 zone-intro establishing shots (8–10 s), 4 near-miss consequence clips with no gore (6–10 s), and an after-action review outro. Clips carry no factual safety claims; the coach and data carry facts. |
| **HF-IMG** | Higgsfield `generate_image` | Decals (tire tracks, oil stains, mud splash), signage textures (generic OSHA-style: "DANGER – EXCAVATION", "HARD HAT AREA"; no real company logos), inspection tags, labels, and the field-tablet UI art. |
| **HF-AUD** | Higgsfield `generate_audio` | Ambience beds and one-shots: generator, backup alarm, nail gun, circular saw, excavator hydraulics, radio chatter, and the snap/confirm UI sounds. |

**Rules**

- No Hunyuan3D anywhere (standing preference).
- 3D generation is **Tripo only**. Higgsfield is used for images, layout boards, cinematics and audio, never `generate_3d`.
- Budget per hero prop: ≤ 40k tris.
- Budget per clutter prop: ≤ 8k tris.
- Textures: 2K hero and 1K clutter, packed as BaseMap + Normal + MaskMap for URP Lit.
- Scene target: ≤ 2.5M tris visible, 90 fps on RTX-class PCVR. Mark statics and use LOD Groups on equipment.
- Every generated asset goes through a Blender pass before Unity: apply scale, set the origin at its base, set real-world dimensions, name it `SM_<Name>` (static mesh prefix), and export FBX to `Assets/_Game/Art/Models/<Route>/`.

## Allowed paths (this revamp)

- `Assets/_Game/**` (new home for v2 code, art, scenes, and data)
- `Assets/SafetyTraining/Scripts/**`: port only. Files move into `_Game` as they are rewritten.
- `docs/**`, `Tools/blender/**`, `Packages/manifest.json`, `ProjectSettings/**` (URP and XR only)
- Deleted at the end of M1: `Assets/SafetyTraining/Editor/*Factory*`, `*Dresser*`, `Generated*`, `Scenes/SafetyTrainingExplorer.unity`, `ThirdParty/Kenney`

## Naming

- Namespaces: `Jobsite.Core`, `Jobsite.Runtime`, `Jobsite.Editor`, `Jobsite.Tests`.
- Asset prefixes: `SM_` mesh, `M_` material, `T_` texture, `PF_` prefab, `SO_` ScriptableObject, `SFX_` / `AMB_` audio.
