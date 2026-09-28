# Guardrail recipe, v2 URP candidate

Run from the project root using an installed Blender 4.2+ executable:

```powershell
blender --background --factory-startup --python Tools/blender/guardrail.py -- --render
node Tools/blender/validate_manifest.cjs docs/GuardrailSpecs.json
node Tools/blender/test_manifest.cjs
```

Replace the blender command with the installed executable path if it is not on PATH. These are reproduction commands, not a record of successful execution.

- No overwrite by default. Use --overwrite only after reviewing existing output files.
- Optional --output-root selects a separate project-shaped candidate directory.
- The recipe defaults to this project root based on its Tools/blender location.
- Four FBX candidates, a new source .blend, geometry report, and optional 12 service-worn review frames are planned outputs.
- Component edge/midpoint and absent-midrail checks are authored in Python but have NOT run in Blender.
- A new Blender review scene is created. Existing Unity scenes and runtime code are not edited.
- Use factory startup to avoid carrying unrelated open Blender data into the saved candidate .blend.
- No rating, certification, verified load capacity or real-world fastening design is implied.

See docs/PropBible.md and docs/AssetManifest.md before use.
