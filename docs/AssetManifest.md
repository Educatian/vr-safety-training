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
