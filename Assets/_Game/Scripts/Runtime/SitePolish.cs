using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Runtime polish for greybox props the scene builders leave as bare primitives (design review 2026-09-29,
    // "fix every glitch"): the PPE table floated with no legs, the sign-in and inspection boards were blank white
    // slabs, the hard hat was a cube, and the trench box walls read as white cards. Runs once per scene; purely visual,
    // it never touches colliders the game logic uses.
    public sealed class SitePolish : MonoBehaviour
    {
        private static Font font;

        private void Start()
        {
            font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var all = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var r in all)
            {
                switch (r.name)
                {
                    case "PpeTable": TableLegs(r.transform); break;
                    case "SignInBoard": Sign(r.transform, "DAILY SIGN-IN", "Sign in, then take your PPE.\nHard hat · vest · glasses · gloves", new Color(0.1f, 0.35f, 0.15f)); break;
                    case "InspectionBoard": Sign(r.transform, "EXCAVATION\nDAILY INSPECTION", "Soil: Type C · Protective system: box\nInspected by competent person 6:40 AM", new Color(0.55f, 0.1f, 0.08f)); break;
                    case "HardHat": HardHat(r); break;
                    case "HiVisVest": VestStripes(r.transform); break;
                    case "TrenchBox_WallW":
                    case "TrenchBox_WallE": Aluminium(r); break;
                }
            }
        }

        // Four legs from the ground to the underside of the table top (so it stops floating).
        private static void TableLegs(Transform top)
        {
            var size = top.lossyScale;
            var height = top.position.y - size.y / 2f;
            if (height <= 0.05f) return;
            var mat = top.GetComponent<Renderer>().sharedMaterial;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leg.name = "PpeTable_Leg";
                    Destroy(leg.GetComponent<Collider>());
                    leg.transform.SetParent(top.parent, true);
                    leg.transform.position = top.position + top.right * sx * (size.x / 2f - 0.06f) + top.forward * sz * (size.z / 2f - 0.06f) + Vector3.down * (top.position.y - height / 2f);
                    leg.transform.rotation = top.rotation;
                    leg.transform.localScale = new Vector3(0.05f, height, 0.05f);
                    leg.GetComponent<Renderer>().sharedMaterial = mat;
                }
        }

        // Printed sign face on both sides of a board (world-space uGUI so the board's non-uniform scale can't stretch it).
        private static void Sign(Transform board, string title, string body, Color band)
        {
            var size = board.lossyScale;
            foreach (var side in new[] { 1f, -1f })
            {
                var go = new GameObject("SignFace", typeof(RectTransform), typeof(Canvas));
                go.transform.SetParent(board.parent, true);
                go.transform.position = board.position + board.forward * side * (size.z / 2f + 0.004f);
                go.transform.rotation = board.rotation * Quaternion.Euler(0, side > 0 ? 180f : 0f, 0);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(size.x * 1000f, size.y * 1000f);
                rt.localScale = Vector3.one * 0.001f;
                go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Panel(rt, new Color(0.96f, 0.95f, 0.9f), Vector2.zero, Vector2.one);
                Panel(rt, band, new Vector2(0, 0.74f), Vector2.one);
                Label(rt, title, 58, Color.white, new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.99f));
                Label(rt, body, 34, new Color(0.12f, 0.12f, 0.12f), new Vector2(0.07f, 0.05f), new Vector2(0.93f, 0.72f));
            }
        }

        private static void Panel(RectTransform parent, Color c, Vector2 min, Vector2 max)
        {
            var img = new GameObject("Panel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.rectTransform.anchorMin = min; img.rectTransform.anchorMax = max; img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
            img.color = c; img.raycastTarget = false;
        }

        private static void Label(RectTransform parent, string text, int size, Color c, Vector2 min, Vector2 max)
        {
            var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.rectTransform.anchorMin = min; t.rectTransform.anchorMax = max; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.font = font; t.fontSize = size; t.color = c; t.text = text; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; t.raycastTarget = false;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 12; t.resizeTextMaxSize = size;
        }

        // Dome + brim instead of a yellow cube (children of the rack item, so they hide with it when taken).
        private static void HardHat(MeshRenderer box)
        {
            var mf = box.GetComponent<MeshFilter>();
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mf.sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
            var brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            brim.name = "HardHat_Brim";
            Destroy(brim.GetComponent<Collider>());
            brim.transform.SetParent(box.transform, false);
            brim.transform.localPosition = new Vector3(0, -0.42f, 0.06f);
            brim.transform.localScale = new Vector3(1.25f, 0.04f, 1.3f);
            brim.GetComponent<Renderer>().sharedMaterial = box.sharedMaterial;
        }

        // Two silver reflective bands across the folded vest.
        private static void VestStripes(Transform vest)
        {
            var s = vest.lossyScale;
            var silver = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            silver.SetColor("_BaseColor", new Color(0.82f, 0.84f, 0.86f));
            silver.SetFloat("_Smoothness", 0.8f);
            foreach (var z in new[] { -0.09f, 0.09f })
            {
                var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
                band.name = "Vest_Reflective";
                Destroy(band.GetComponent<Collider>());
                band.transform.SetParent(vest, false);
                band.transform.localPosition = new Vector3(0, 0.55f, z / Mathf.Max(0.01f, s.z));
                band.transform.localScale = new Vector3(0.96f, 0.2f, 0.035f / Mathf.Max(0.01f, s.z));
                band.GetComponent<Renderer>().sharedMaterial = silver;
            }
        }

        // Brushed aluminium, not a white card.
        private static void Aluminium(MeshRenderer wall)
        {
            var m = wall.material;
            m.SetColor("_BaseColor", new Color(0.56f, 0.59f, 0.62f));
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.75f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.45f);
        }
    }
}
