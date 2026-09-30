using UnityEngine;
using UnityEngine.Rendering;

namespace Jobsite.Runtime
{
    // People below grade (trench crew) stand in the walls' shadow with no bounce light: without light probes their faces
    // rendered almost black (quality review 2026-09-30, P0, t_npc/2_Marcus). Until the scene is baked with probe groups,
    // each below-grade character gets a custom probe: the scene's ambient plus the sky/bounce light a trench actually
    // receives from above and from the sunlit spoil side. No real-time light is added.
    public static class TrenchLighting
    {
        public const float BelowGrade = -0.6f;

        public static int Apply()
        {
            var n = 0;
            var probe = RenderSettings.ambientProbe;
            var sky = RenderSettings.ambientSkyColor.maxColorComponent > 0.01f ? RenderSettings.ambientSkyColor : new Color(0.55f, 0.62f, 0.72f);
            probe.AddDirectionalLight(Vector3.up, sky, 0.9f);                                   // open sky above the trench
            probe.AddDirectionalLight(new Vector3(0.3f, 0.4f, 0.85f).normalized, new Color(0.78f, 0.6f, 0.45f), 0.45f);   // bounce off the spoil / far wall
            var probes = new[] { probe };
            var block = new MaterialPropertyBlock();
            foreach (var r in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.bounds.center.y > BelowGrade + 0.9f) continue;                            // centre of a standing body in a >= 1.5 m trench
                r.lightProbeUsage = LightProbeUsage.CustomProvided;
                r.GetPropertyBlock(block);
                block.CopySHCoefficientArraysFrom(probes);
                r.SetPropertyBlock(block);
                n++;
            }
            return n;
        }
    }
}
