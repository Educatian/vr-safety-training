using System.Linq;
using Jobsite.Runtime;
using UnityEditor;
using UnityEngine;

namespace Jobsite.Editor
{
    // Builds a drivable vehicle from a Tools/blender/rig_vehicle.py rig FBX (GDD §17).
    // Rig model axes: +X forward, +Y driver side, up. Unity WheelColliders drive along +Z, so the model
    // sits under a child rotated -90 deg about Y, and each visual wheel is re-parented under a pivot
    // aligned with the vehicle root so WheelCollider.GetWorldPose() maps 1:1 onto it.
    public static class VehicleSetup
    {
        const string RigDir = "Assets/_Game/Art/Models/TR-3D/Rigs/";

        public static GameObject Build(Transform parent, string rigName, Vector3 position, float yaw, float massKg)
        {
            var path = RigDir + rigName + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return null;
            if (!importer.bakeAxisConversion) { importer.bakeAxisConversion = true; importer.SaveAndReimport(); }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            var root = new GameObject(rigName.Replace("_Rig", "") + "_Vehicle");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            model.transform.localPosition = Vector3.zero;
            TripoImport.Apply(model, rigName);
            // Keep the importer root conversion; only add the -90 deg yaw (model +X forward -> root +Z).
            model.transform.localRotation = Quaternion.Euler(0, -90, 0) * prefab.transform.localRotation;

            Transform Find(string n) => model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith(n));

            var body = Find("Body");
            var bodyBounds = body.GetComponent<Renderer>().bounds;
            var col = new GameObject("BodyCollider");
            col.transform.SetParent(root.transform, false);
            col.transform.position = bodyBounds.center + Vector3.up * 0.15f;
            col.AddComponent<BoxCollider>().size = root.transform.InverseTransformVector(bodyBounds.size) - new Vector3(0, 0.3f, 0);
            col.GetComponent<BoxCollider>().size = Abs(col.GetComponent<BoxCollider>().size);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = massKg;
            rb.centerOfMass = new Vector3(0, 0.6f, 0);
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            WheelCollider MakeWheel(string n, out Transform visualPivot)
            {
                var mesh = Find(n);
                var r = mesh.GetComponent<Renderer>().bounds;
                var go = new GameObject(n + "_Collider");
                go.transform.SetParent(root.transform, false);
                go.transform.position = r.center;
                var wc = go.AddComponent<WheelCollider>();
                wc.radius = r.extents.y;
                wc.suspensionDistance = 0.18f;
                wc.mass = 40f;
                var pivot = new GameObject(n + "_Pivot").transform;
                pivot.SetParent(root.transform, false);
                pivot.SetPositionAndRotation(r.center, root.transform.rotation);
                mesh.SetParent(pivot, true);
                visualPivot = pivot;
                return wc;
            }

            // Any number of axles: WheelF* steer, WheelR* drive (pickup 2+2, tandem dump truck 2+4).
            var wheelNames = model.GetComponentsInChildren<Transform>(true).Select(x => x.name)
                .Where(n => n.StartsWith("Wheel") && !n.Contains("_")).Distinct().OrderBy(n => n).ToList();
            var steer = new System.Collections.Generic.List<WheelCollider>();
            var drive = new System.Collections.Generic.List<WheelCollider>();
            var visuals = new System.Collections.Generic.List<Transform>();
            foreach (var n in wheelNames.Where(n => n.StartsWith("WheelF"))) { steer.Add(MakeWheel(n, out var v)); visuals.Add(v); }
            foreach (var n in wheelNames.Where(n => n.StartsWith("WheelR"))) { drive.Add(MakeWheel(n, out var v)); visuals.Add(v); }

            // Door interact volume at the handle so the E ray finds the vehicle.
            var door = Find("DriverDoor");
            var doorBounds = door.GetComponent<Renderer>().bounds;
            var handle = new GameObject("DoorInteract");
            handle.transform.SetParent(door, true);
            handle.transform.position = doorBounds.center;
            var hc = handle.AddComponent<BoxCollider>();
            hc.isTrigger = true;
            hc.size = Abs(handle.transform.InverseTransformVector(doorBounds.size + new Vector3(0.2f, 0.2f, 0.2f)));

            var vc = root.AddComponent<VehicleController>();
            vc.Configure(door, Find("SeatEye"), Find("ExitPoint"), steer.ToArray(), drive.ToArray(), visuals.ToArray());
            return root;
        }

        // Tracked excavator: no wheels; House/Boom/Stick/Bucket chain driven by ExcavatorRig (NPC dig cycle).
        public static GameObject BuildExcavator(Transform parent, string rigName, Vector3 position, float yaw)
        {
            var path = RigDir + rigName + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return null;
            if (!importer.bakeAxisConversion) { importer.bakeAxisConversion = true; importer.SaveAndReimport(); }
            var root = new GameObject("Excavator20t_NPC");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
            TripoImport.Apply(model, rigName);
            Transform Find(string n) => model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n);
            var bounds = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var col = new GameObject("Collider"); col.transform.SetParent(root.transform, false);
            col.transform.position = bounds.center;
            col.AddComponent<BoxCollider>().size = Abs(root.transform.InverseTransformVector(bounds.size));
            var rig = root.AddComponent<ExcavatorRig>();
            rig.Configure(Find("House"), Find("Boom"), Find("Stick"), Find("Bucket"));
            return root;
        }

        // Rigged rough-terrain crane (Thu pick). Boom tip = far end of the last telescopic section.
        public static GameObject BuildCrane(Transform parent, string rigName, Vector3 position, float yaw)
        {
            var path = RigDir + rigName + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return null;
            if (!importer.bakeAxisConversion) { importer.bakeAxisConversion = true; importer.SaveAndReimport(); }
            var root = new GameObject("RtCrane_NPC");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
            TripoImport.Apply(model, rigName);
            Transform Find(string n) => model.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == n);
            var tele = Find("Tele1");
            var teleBounds = tele.GetComponent<Renderer>().bounds;
            var tip = new GameObject("BoomTip").transform;
            tip.SetParent(tele, false);
            // Model forward (+X) is the boom direction in the stowed pose; tip sits at the section's far end.
            tip.position = new Vector3(teleBounds.max.x, teleBounds.center.y, teleBounds.center.z);
            var hook = Find("HookBlock");
            hook.SetParent(root.transform, true);
            var line = hook.gameObject.AddComponent<LineRenderer>();
            line.widthMultiplier = 0.03f;
            line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.15f, 0.15f, 0.15f) };
            var rig = root.AddComponent<CraneRig>();
            rig.Configure(new Transform[0], Find("House"), Find("Boom"), new[] { tele }, tip, hook, line);
            var bounds = model.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var col = new GameObject("CarrierCollider"); col.transform.SetParent(root.transform, false);
            col.transform.position = new Vector3(bounds.center.x, bounds.min.y + 1.2f, bounds.center.z);
            col.AddComponent<BoxCollider>().size = new Vector3(bounds.size.x * 0.8f, 2.4f, bounds.size.z * 0.8f);
            return root;
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
