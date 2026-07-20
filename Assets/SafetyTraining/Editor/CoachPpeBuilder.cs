using SafetyTraining.Core;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SafetyTraining.Editor
{
    internal static class CoachPpeBuilder
    {
        const string MeshFolder = "Assets/SafetyTraining/Generated/EquipmentMeshes";
        const string MaterialFolder = "Assets/SafetyTraining/GeneratedMaterials/SafetyEquipment";
        static readonly Color ChemicalFrame = new(0.08f, 0.22f, 0.27f);
        static readonly Color RespiratorColor = new(0.17f, 0.2f, 0.22f);
        static readonly Color ElectricalShell = new(0.14f, 0.17f, 0.21f);
        static readonly Color GloveColor = new(0.84f, 0.34f, 0.06f);

        public static void Apply(TrainingSiteId siteId, GameObject coach, Animator animator)
        {
            if (siteId == TrainingSiteId.ChemicalProcessing)
                CreateChemicalPpe(coach.transform, HeadBone(coach.transform, animator));
            if (siteId == TrainingSiteId.ElectricalMaintenance)
                CreateElectricalPpe(coach.transform, animator, HeadBone(coach.transform, animator));
        }

        static void CreateChemicalPpe(Transform coach, Transform head)
        {
            var goggles = Anchor("Chemical Splash Goggles", head,
                head.position + coach.up * 0.035f + coach.forward * 0.105f, coach.rotation);
            AddPrimitive(goggles, PrimitiveType.Sphere, "Integrated Splash Lens", Vector3.zero,
                Vector3.zero, new Vector3(0.17f, 0.068f, 0.024f),
                TransparentMaterial("ChemicalGoggleLens", new Color(0.48f, 0.9f, 0.95f, 0.38f)));
            var respirator = Anchor("Half Mask Respirator", head,
                head.position - coach.up * 0.07f + coach.forward * 0.11f, coach.rotation);
            AddPrimitive(respirator, PrimitiveType.Sphere, "Integrated Respirator Shell", Vector3.zero,
                Vector3.zero, new Vector3(0.13f, 0.078f, 0.052f),
                OpaqueMaterial(RespiratorColor, 0.08f, 0.32f));
        }

        static void CreateElectricalPpe(Transform coach, Animator animator, Transform head)
        {
            var shield = Anchor("Arc Flash Face Shield", head,
                head.position - coach.up * 0.045f + coach.forward * 0.095f, coach.rotation);
            var panel = new GameObject("Amber Visor Panel");
            panel.transform.SetParent(shield, false);
            panel.AddComponent<MeshFilter>().sharedMesh = FaceShieldMesh();
            panel.AddComponent<MeshRenderer>().sharedMaterial = TransparentMaterial("ArcFlashAmber", new Color(1f, 0.5f, 0.08f, 0.42f));

            CreateGlove("Voltage Rated Gloves", HandBone(coach, animator, HumanBodyBones.LeftHand));
            CreateGlove("Voltage Rated Glove - Right", HandBone(coach, animator, HumanBodyBones.RightHand));
        }

        static void CreateGlove(string name, Transform hand)
        {
            var glove = Anchor(name, hand, hand.position, hand.rotation);
            var material = OpaqueMaterial(GloveColor, 0.03f, 0.28f);
            AddPrimitive(glove, PrimitiveType.Sphere, "Insulating Glove Palm", new Vector3(0f, 0.035f, 0.01f),
                Vector3.zero, new Vector3(0.105f, 0.14f, 0.08f), material);
            AddPrimitive(glove, PrimitiveType.Cylinder, "Insulating Glove Cuff", new Vector3(0f, -0.105f, -0.01f),
                Vector3.zero, new Vector3(0.09f, 0.095f, 0.09f), material);
        }

        static Transform HeadBone(Transform coach, Animator animator)
        {
            return HandBone(coach, animator, HumanBodyBones.Head);
        }

        static Transform HandBone(Transform coach, Animator animator, HumanBodyBones bone)
        {
            var resolved = animator != null && animator.isHuman ? animator.GetBoneTransform(bone) : null;
            return resolved != null ? resolved : coach;
        }

        static Transform Anchor(string name, Transform bone, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject(name).transform;
            root.SetPositionAndRotation(position, rotation);
            root.SetParent(bone, true);
            return root;
        }

        static void AddPrimitive(Transform parent, PrimitiveType type, string name, Vector3 position,
            Vector3 rotation, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.Euler(rotation);
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(item.GetComponent<Collider>());
        }

        static Material OpaqueMaterial(Color color, float metallic, float smoothness)
        {
            return SafetyEquipmentMeshFactory.Material(color, metallic, smoothness);
        }

        static Material TransparentMaterial(string name, Color color)
        {
            SafetyScenePrimitives.EnsureFolder(MaterialFolder);
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Mesh FaceShieldMesh()
        {
            SafetyScenePrimitives.EnsureFolder(MeshFolder);
            var path = $"{MeshFolder}/Arc Flash Face Shield Panel.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh();
                AssetDatabase.CreateAsset(mesh, path);
            }

            const int columns = 12;
            const int rows = 6;
            var vertices = new List<Vector3>((columns + 1) * (rows + 1));
            var uvs = new List<Vector2>((columns + 1) * (rows + 1));
            var triangles = new List<int>(columns * rows * 6);
            for (var y = 0; y <= rows; y++)
            for (var x = 0; x <= columns; x++)
            {
                var horizontal = x / (float)columns - 0.5f;
                var vertical = y / (float)rows - 0.5f;
                vertices.Add(new Vector3(horizontal * 0.36f, vertical * 0.42f,
                    -0.055f + horizontal * horizontal * 0.17f));
                uvs.Add(new Vector2(x / (float)columns, y / (float)rows));
            }
            for (var y = 0; y < rows; y++)
            for (var x = 0; x < columns; x++)
            {
                var lowerLeft = y * (columns + 1) + x;
                triangles.Add(lowerLeft);
                triangles.Add(lowerLeft + columns + 1);
                triangles.Add(lowerLeft + 1);
                triangles.Add(lowerLeft + 1);
                triangles.Add(lowerLeft + columns + 1);
                triangles.Add(lowerLeft + columns + 2);
            }
            mesh.Clear();
            mesh.name = "Arc Flash Face Shield Panel";
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
}
