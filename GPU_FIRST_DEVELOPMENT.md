# GPU-First VR Development Policy

This project prioritizes GPU-efficient rendering for every future environment pass.

## Starting budgets and validation

These are starting values, not claims of universal best practice. Each value must be validated on the target headset and Windows GPU build.

- Meta Quest target: hold 72 Hz in a representative five-minute site traversal; investigate any sustained frame time above 13.9 ms.
- Windows VR target: hold 90 Hz where the headset/runtime supports it; investigate sustained frame time above 11.1 ms.
- Repeated opaque environment materials: GPU instancing enabled.
- Imported real-environment anchors: one LOD group with distance culling; add authored LOD1/LOD2 only when profiler evidence justifies memory cost.
- Particles: maximum 80 per ambient system, collision disabled, automatic culling.
- HDR environment: one shared 1K CC0 pure-sky cubemap for all sites; avoid per-site HDR duplication.
- Lighting: baked/static-friendly geometry, limited real-time lights, shared reflection probes, and linear distance fog for horizon control.

## Test plan

1. Build and run the Windows player with the visual capture tour using the real GPU path.
2. Traverse every operational subzone and inspect sky, fog, LOD pop, particles, HMI readability, and NPC masking.
3. Profile one full module on Meta Quest. Record GPU frame time, CPU frame time, draw calls, batches, triangles, and memory.
4. If the 72 Hz target fails, reduce transparent particles and shadow distance first, then simplify distant geometry or HDR import size.
5. Keep interaction colliders and analytics volumes independent of visual LOD so learning evidence is never culled.