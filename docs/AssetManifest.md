# Asset manifest: first v2 B-PROC candidate

Status: source preparation only. No generated FBX, Blender render, Unity import, prefab, collider, or scene placement exists from this increment.

Baseline inspected: revamp/v2, a32eaed9e80aba7f9b44f850f9a6dd35c644252b, Unity 6000.3.25f1, URP 17.3.0.

| Candidate FBX filename | Route | Condition | Source | Verification |
|---|---|---|---|---|
| SM_Guardrail_clean.fbx | B-PROC | Intact, clean | Tools/blender/guardrail.py | Not generated |
| SM_Guardrail_service_worn.fbx | B-PROC | Intact, cosmetic wear | Same | Not generated |
| SM_Guardrail_missing_midrail.fbx | B-PROC | Missing midrail, clean | Same | Not generated |
| SM_Guardrail_missing_midrail_worn.fbx | B-PROC | Missing midrail, cosmetic wear | Same | Not generated |

Destination: Assets/_Game/Art/Models/B-PROC/. No external mesh or texture is incorporated by this script. Project distribution license is not established here. The cosmetic wear axis is independent of intact/defective geometry; do not associate material cleanliness with the expert answer.

Geometry targets and bounded official evidence: docs/GuardrailSpecs.json and docs/PropBible.md. Normative dimensions are distinguished from authored width, tube, plate, clamp, and bolt dimensions. Strength, anchorage, connection engineering, and complete compliance are unverified.

## Integration gate

1. Run Blender with factory startup and no overwrite flag. Review actual component measurements and triangle counts in Captures/Props/guardrail/geometry-report.json. The render switch currently captures 12 fixed views of service_worn only; additionally inspect the other three variants before acceptance.
2. Inspect silhouette, rail joints, scuffs, board brackets, normals, scale, and fastening plausibility. Geometry-based scuffs and simple materials are prototypes, not finished scanned PBR assets. No source truth or strength is established by automated bounds tests.
3. In Unity 6000.3.25f1, verify FBX conversion produces X=2.0, Y=1.0668, Z=0.18 m with the root at the walking plane. Create/review URP/Lit materials; do not assume Blender node graphs transfer. Check metallic/smoothness and occlusion channel assignment against the actual URP shader.
4. Author collider and socket proxies separately from the visual LOD. Do not change runtime scoring, stable identifiers, timers, telemetry volumes, NPC routes, or interactions to fit a visual asset.
5. Preserve the current legacy cap-name dependency until the explicit T1.7 migration. Do not replace whole project folders with the older Built-in overlay.
6. Run current EditMode/PlayMode and desktop/PCVR regression tests, then scene-level review captures and performance measurements. Do not mark T2.1 complete or commit this increment until the required validation gates pass.

## Executed checks

50/50 metadata tests passed using the saved CJS source in the Aside JavaScript runtime. Result: Captures/Validation/guardrail-metadata-tests.json. These are not Unity NUnit tests, Blender geometry tests, or rendering/performance measurements. Existing project logs report 108 EditMode tests passing at the prior run; no new Unity test run was performed.

## 2026-09-28 build result (Blender 5.2.1, headless)

The 4 FBX files were generated in `Assets/_Game/Art/Models/B-PROC/`.

Measured on every variant:
- overall size 2.0 × 0.18 × 1.0668 m
- top edge 42 in
- midrail at 21 in where present
- toeboard top 3.5 in with a 5 mm gap
- 1,016–1,440 triangles

The look was revised for authenticity:
- rails are safety-yellow powder-coated 1-1/2 in pipe (1.900 in OD)
- posts are 1.5 in square tube
- base plates, clamps and bolts are galvanized
- the toeboard is a 2×4 set on edge
- the worn variants add paint chips and a mud line, applied equally to hazard and compliant variants

The clamp overshoot above 42 in was caught by the recipe's bounds check and fixed. The Unity import check is pending (next scene pass).
- 2026-09-28 Poly Haven CC0 textures (2K jpg: diff/nor_gl/rough): brown_mud_dry, muddy_tracks, gravel_road, excavated_soil_wall, brown_mud_02 — https://polyhaven.com/license
- 2026-09-28 Poly Haven CC0: HDRI kloofendal_48d_partly_cloudy_puresky (2k, sky only — replaces construction_yard HDRI whose non-US apartment backdrop broke authenticity); textures withered_grass, stony_dirt_path
- 2026-09-28 Poly Haven CC0 (Alabama context): textures red_dirt_mud_01, red_laterite_soil_stones, sparse_grass; models pine_tree_01, shrub_01, fire_hydrant (B-LIB, 1k fbx)
- 2026-09-28 Higgsfield (gpt_image_2_5) albedo textures ×8 → seamless + luminance normals via Tools/hf/make_pbr.py: TrailerSiding, DumpsterSteel, ToiletHDPE, Galvanized, Plywood, Windscreen, ConcreteSlab, MetalDeck (Assets/_Game/Art/Textures/HF/)
- 2026-09-28 UI: Higgsfield tablet frame + energy/control icon sheets (sliced to Assets/_Game/Resources/UI); font Barlow Condensed SemiBold (SIL OFL 1.1, Google Fonts)
