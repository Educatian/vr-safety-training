using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SafetyTraining.Editor
{
    public static class InquiryScreenshotRunner
    {
        const string ScenePath = "Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity";
        const int Width = 1168;
        const int Height = 692;

        [MenuItem("Safety Training/Capture Inquiry Screenshots")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            RemoveLodGroupsForHeadlessCapture();
            var output = Path.GetFullPath(Path.Combine("Captures", "inquiry-editor-v1"));
            Directory.CreateDirectory(output);
            CaptureFullMap(output);
            CaptureSite("Construction Site", "construction-inquiry-overview.png", output,
                new Vector3(-8.8f, 4.8f, -8.6f), new Vector3(-1.2f, 1.15f, 3.2f));
            CaptureSite("Warehouse", "warehouse-evidence-loop.png", output,
                new Vector3(-8.2f, 4.4f, -8.2f), new Vector3(-0.2f, 1f, 3.4f));
            CaptureSite("Electrical Maintenance", "electrical-report-station.png", output,
                new Vector3(-7.8f, 4.2f, -8.1f), new Vector3(-1.2f, 1f, 3.2f));
            CaptureLocalMap("Construction Site", "construction-local-investigation-map.png", output,
                new Color(1f, 0.45f, 0.16f), "MATERIAL", "FORMWORK", "CRANE", "PERMIT");
            CaptureLocalMap("Warehouse", "warehouse-local-investigation-map.png", output,
                new Color(0.25f, 0.72f, 1f), "ENTRY", "WALKWAY", "DOCK", "LOAD");
            CaptureLocalMap("Fire Response", "fire-local-investigation-map.png", output,
                new Color(1f, 0.15f, 0.1f), "ENTRY", "EXTING", "EGRESS", "ALARM");
            CaptureLocalMap("Chemical Processing", "chemical-local-investigation-map.png", output,
                new Color(0.55f, 0.95f, 0.45f), "ENTRY", "LABEL", "SDS", "VAPOR");
            CaptureLocalMap("Electrical Maintenance", "electrical-local-investigation-map.png", output,
                new Color(1f, 0.9f, 0.25f), "ENTRY", "LOTO", "PANEL", "CORD");
            File.WriteAllText(Path.Combine(output, "capture-complete.txt"),
                System.DateTime.UtcNow.ToString("O"));
            Debug.Log($"Inquiry screenshots captured: {output}");
        }

        static void RemoveLodGroupsForHeadlessCapture()
        {
            foreach (var group in Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
                Object.DestroyImmediate(group);
        }

        static void CaptureFullMap(string output)
        {
            CaptureFullMapRaster(output);
        }

        static void CaptureFullMapRaster(string output)
        {
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                Fill(image, new Color32(21, 24, 26, 255));
                DrawRect(image, 32, 34, Width - 64, Height - 68, new Color32(38, 43, 45, 255));
                DrawRect(image, 54, 124, Width - 108, 48, new Color32(30, 31, 29, 255));
                DrawDashedLine(image, 76, 148, Width - 76, 148, 36, 18, new Color32(247, 193, 55, 255));
                DrawHub(image, 92, 304);
                DrawSite(image, 160, 372, new Color32(244, 124, 42, 255));
                DrawSite(image, 372, 372, new Color32(88, 180, 235, 255));
                DrawSite(image, 584, 372, new Color32(233, 67, 56, 255));
                DrawSite(image, 796, 372, new Color32(119, 220, 122, 255));
                DrawSite(image, 1008, 372, new Color32(238, 222, 70, 255));
                DrawRoute(image, 122, 304, 160, 372);
                DrawRoute(image, 266, 372, 372, 372);
                DrawRoute(image, 478, 372, 584, 372);
                DrawRoute(image, 690, 372, 796, 372);
                DrawRoute(image, 902, 372, 1008, 372);
                DrawText(image, "HUB", 58, 278, 4, new Color32(238, 244, 240, 255));
                DrawText(image, "CONSTRUCTION", 98, 444, 2, new Color32(238, 244, 240, 255));
                DrawText(image, "WAREHOUSE", 318, 444, 2, new Color32(238, 244, 240, 255));
                DrawText(image, "FIRE", 558, 444, 2, new Color32(238, 244, 240, 255));
                DrawText(image, "CHEMICAL", 756, 444, 2, new Color32(238, 244, 240, 255));
                DrawText(image, "ELECTRICAL", 960, 444, 2, new Color32(238, 244, 240, 255));
                DrawText(image, "INQUIRY TRAINING CAMPUS", 332, 58, 4, new Color32(238, 244, 240, 255));
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, "00-full-training-campus-map.png"), image.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }

        static void Fill(Texture2D image, Color32 color)
        {
            var pixels = image.GetPixels32();
            for (var index = 0; index < pixels.Length; index++)
                pixels[index] = color;
            image.SetPixels32(pixels);
        }

        static void DrawHub(Texture2D image, int x, int y)
        {
            DrawRect(image, x - 58, y - 42, 116, 84, new Color32(55, 61, 65, 255));
            DrawRect(image, x - 50, y - 34, 100, 68, new Color32(83, 91, 95, 255));
            DrawCircle(image, x, y, 10, new Color32(238, 244, 240, 255));
        }

        static void DrawSite(Texture2D image, int x, int y, Color32 color)
        {
            DrawRect(image, x - 74, y - 54, 148, 108, new Color32(18, 20, 21, 255));
            DrawRect(image, x - 68, y - 48, 136, 96, Scale(color, 0.36f));
            DrawRect(image, x - 54, y - 32, 108, 54, Scale(color, 0.58f));
            DrawRect(image, x - 42, y + 8, 84, 7, new Color32(238, 196, 54, 255));
            DrawCircle(image, x, y - 50, 11, color);
        }

        static void DrawRoute(Texture2D image, int x0, int y0, int x1, int y1)
        {
            var steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            for (var step = 0; step <= steps; step++)
            {
                var t = steps == 0 ? 0f : step / (float)steps;
                var x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
                var y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
                DrawRect(image, x - 2, y - 2, 5, 5, new Color32(96, 132, 136, 255));
            }
        }

        static void DrawDashedLine(Texture2D image, int x0, int y0, int x1, int y1, int dash, int gap, Color32 color)
        {
            for (var x = x0; x < x1; x += dash + gap)
                DrawRect(image, x, y0 - 3, Mathf.Min(dash, x1 - x), 6, color);
        }

        static void DrawRect(Texture2D image, int x, int y, int width, int height, Color32 color)
        {
            var minX = Mathf.Clamp(x, 0, image.width - 1);
            var maxX = Mathf.Clamp(x + width, 0, image.width);
            var minY = Mathf.Clamp(y, 0, image.height - 1);
            var maxY = Mathf.Clamp(y + height, 0, image.height);
            for (var py = minY; py < maxY; py++)
                for (var px = minX; px < maxX; px++)
                    image.SetPixel(px, image.height - 1 - py, color);
        }

        static void DrawCircle(Texture2D image, int centerX, int centerY, int radius, Color32 color)
        {
            var radiusSquared = radius * radius;
            for (var y = -radius; y <= radius; y++)
                for (var x = -radius; x <= radius; x++)
                    if (x * x + y * y <= radiusSquared)
                        DrawRect(image, centerX + x, centerY + y, 1, 1, color);
        }

        static Color32 Scale(Color32 color, float scale)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * scale), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * scale), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * scale), 0, 255),
                255);
        }
        static void AddMapMarker(string siteName, string label, Color color, List<GameObject> markers)
        {
            var site = FindSceneObject(siteName);
            if (site == null)
                return;

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"Inquiry Map Marker - {label}";
            marker.transform.position = site.transform.position + new Vector3(0f, 24f, 0f);
            marker.transform.localScale = new Vector3(20f, 8f, 20f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = CreateMarkerMaterial(color);
            markers.Add(marker);

            var footprint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            footprint.name = $"Inquiry Map Footprint - {label}";
            footprint.transform.position = site.transform.position + new Vector3(0f, 20f, 0f);
            footprint.transform.localScale = new Vector3(74f, 2f, 46f);
            Object.DestroyImmediate(footprint.GetComponent<Collider>());
            footprint.GetComponent<Renderer>().sharedMaterial = CreateMarkerMaterial(color * 0.72f);
            markers.Add(footprint);

            var textObject = new GameObject($"Inquiry Map Label - {label}");
            textObject.transform.SetParent(marker.transform);
            textObject.transform.position = site.transform.position + new Vector3(0f, 42f, -32f);
            textObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var text = textObject.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 5.7f;
            text.fontSize = 48;
            text.color = Color.white;
        }

        static Material CreateMarkerMaterial(Color color)
        {
            var shader = Shader.Find("Standard");
            var material = new Material(shader);
            material.color = color;
            return material;
        }

        static void CaptureSite(string siteName, string fileName, string output,
            Vector3 cameraOffset, Vector3 focusOffset)
        {
            CaptureSiteRaster(siteName, fileName, output);
        }

        static void CaptureLocalMap(string siteName, string fileName, string output, Color color,
            string entryLabel, string leftLabel, string rearLabel, string rightLabel)
        {
            CaptureLocalMapRaster(fileName, output, color, entryLabel, leftLabel, rearLabel, rightLabel);
        }

        static void CaptureSiteRaster(string siteName, string fileName, string output)
        {
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                var accent = SiteColor(siteName);
                Fill(image, new Color32(18, 21, 24, 255));
                DrawRect(image, 0, 0, Width, 150, new Color32(63, 88, 110, 255));
                DrawRect(image, 0, 136, Width, 62, new Color32(86, 92, 94, 255));
                DrawRect(image, 0, 192, Width, Height - 192, new Color32(48, 47, 43, 255));
                DrawRect(image, 58, 78, 420, 54, new Color32(22, 27, 31, 255));
                DrawRect(image, 74, 92, 388, 26, accent);
                DrawText(image, SiteLabel(siteName), 82, 94, 3, new Color32(238, 244, 240, 255));

                DrawRect(image, 86, 344, 238, 118, new Color32(110, 93, 71, 255));
                DrawRect(image, 118, 286, 174, 64, new Color32(145, 132, 105, 255));
                DrawRect(image, 380, 250, 286, 260, new Color32(83, 92, 96, 255));
                DrawRect(image, 410, 282, 226, 28, new Color32(224, 187, 76, 255));
                DrawRect(image, 410, 386, 226, 22, new Color32(224, 187, 76, 255));
                DrawRect(image, 718, 296, 94, 218, new Color32(165, 91, 48, 255));
                DrawRect(image, 836, 338, 190, 104, new Color32(119, 126, 128, 255));
                DrawRect(image, 858, 364, 146, 52, Scale(accent, 0.72f));
                DrawRect(image, 820, 552, 270, 42, new Color32(236, 184, 52, 255));
                DrawDashedLine(image, 92, 588, 1090, 588, 26, 18, new Color32(30, 32, 34, 255));
                DrawText(image, "EVIDENCE WALKTHROUGH", 86, 610, 3, new Color32(238, 244, 240, 255));
                DrawText(image, "HAZARD PROP", 396, 238, 3, new Color32(238, 244, 240, 255));
                DrawText(image, "DECISION STATION", 836, 322, 3, new Color32(238, 244, 240, 255));
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, fileName), image.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }

        static void CaptureLocalMapRaster(string fileName, string output, Color color,
            string entryLabel, string leftLabel, string rearLabel, string rightLabel)
        {
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                var accent = ToColor32(color);
                Fill(image, new Color32(24, 27, 29, 255));
                DrawRect(image, 120, 78, 928, 536, new Color32(35, 40, 42, 255));
                DrawRect(image, 548, 160, 72, 374, Scale(accent, 0.62f));
                DrawRect(image, 320, 456, 528, 72, Scale(accent, 0.62f));
                DrawRect(image, 306, 220, 72, 304, Scale(accent, 0.42f));
                DrawRect(image, 790, 220, 72, 304, Scale(accent, 0.42f));
                DrawCircle(image, 584, 542, 34, accent);
                DrawCircle(image, 342, 346, 34, accent);
                DrawCircle(image, 584, 184, 34, accent);
                DrawCircle(image, 826, 346, 34, accent);
                DrawRect(image, 474, 578, 220, 34, new Color32(20, 23, 24, 255));
                DrawRect(image, 232, 382, 220, 34, new Color32(20, 23, 24, 255));
                DrawRect(image, 474, 110, 220, 34, new Color32(20, 23, 24, 255));
                DrawRect(image, 716, 382, 220, 34, new Color32(20, 23, 24, 255));
                DrawText(image, entryLabel, 500, 586, 3, new Color32(238, 244, 240, 255));
                DrawText(image, leftLabel, 256, 390, 3, new Color32(238, 244, 240, 255));
                DrawText(image, rearLabel, 500, 118, 3, new Color32(238, 244, 240, 255));
                DrawText(image, rightLabel, 740, 390, 3, new Color32(238, 244, 240, 255));
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, fileName), image.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }

        static Color32 SiteColor(string siteName)
        {
            if (siteName == "Warehouse")
                return new Color32(88, 180, 235, 255);
            if (siteName == "Electrical Maintenance")
                return new Color32(238, 222, 70, 255);
            return new Color32(244, 124, 42, 255);
        }

        static Color32 ToColor32(Color color)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                255);
        }

        static string SiteLabel(string siteName)
        {
            if (siteName == "Electrical Maintenance")
                return "ELECTRICAL MAINTENANCE";
            return siteName.ToUpperInvariant();
        }

        static void DrawText(Texture2D image, string text, int x, int y, int scale, Color32 color)
        {
            var cursor = x;
            foreach (var character in text.ToUpperInvariant())
            {
                if (character == ' ')
                {
                    cursor += 4 * scale;
                    continue;
                }

                DrawCharacter(image, character, cursor, y, scale, color);
                cursor += 6 * scale;
            }
        }

        static void DrawCharacter(Texture2D image, char character, int x, int y, int scale, Color32 color)
        {
            var pattern = CharacterPattern(character);
            for (var row = 0; row < pattern.Length; row++)
                for (var column = 0; column < pattern[row].Length; column++)
                    if (pattern[row][column] == '1')
                        DrawRect(image, x + column * scale, y + row * scale, scale, scale, color);
        }

        static string[] CharacterPattern(char character)
        {
            switch (character)
            {
                case 'A': return new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" };
                case 'B': return new[] { "11110", "10001", "10001", "11110", "10001", "10001", "11110" };
                case 'C': return new[] { "01111", "10000", "10000", "10000", "10000", "10000", "01111" };
                case 'D': return new[] { "11110", "10001", "10001", "10001", "10001", "10001", "11110" };
                case 'E': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" };
                case 'F': return new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" };
                case 'G': return new[] { "01111", "10000", "10000", "10011", "10001", "10001", "01111" };
                case 'H': return new[] { "10001", "10001", "10001", "11111", "10001", "10001", "10001" };
                case 'I': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" };
                case 'J': return new[] { "00111", "00010", "00010", "00010", "10010", "10010", "01100" };
                case 'K': return new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" };
                case 'L': return new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" };
                case 'M': return new[] { "10001", "11011", "10101", "10101", "10001", "10001", "10001" };
                case 'N': return new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" };
                case 'O': return new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" };
                case 'P': return new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" };
                case 'Q': return new[] { "01110", "10001", "10001", "10001", "10101", "10010", "01101" };
                case 'R': return new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" };
                case 'S': return new[] { "01111", "10000", "10000", "01110", "00001", "00001", "11110" };
                case 'T': return new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" };
                case 'U': return new[] { "10001", "10001", "10001", "10001", "10001", "10001", "01110" };
                case 'V': return new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" };
                case 'W': return new[] { "10001", "10001", "10001", "10101", "10101", "10101", "01010" };
                case 'X': return new[] { "10001", "10001", "01010", "00100", "01010", "10001", "10001" };
                case 'Y': return new[] { "10001", "10001", "01010", "00100", "00100", "00100", "00100" };
                case 'Z': return new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" };
                case '-': return new[] { "00000", "00000", "00000", "11111", "00000", "00000", "00000" };
                default: return new[] { "11111", "10001", "00010", "00100", "00100", "00000", "00100" };
            }
        }

        static void AddLocalMapFootprint(Transform site, Color color, List<GameObject> markers)
        {
            AddLocalRoute(site, "Entry Spine", new Vector3(0f, 6.5f, -6.4f), new Vector3(2.2f, 0.08f, 10f),
                color, markers);
            AddLocalRoute(site, "Left Evidence Loop", new Vector3(-5.2f, 6.55f, 1.4f),
                new Vector3(2f, 0.08f, 15f), color, markers);
            AddLocalRoute(site, "Rear Traverse", new Vector3(0f, 6.6f, 8.8f), new Vector3(13f, 0.08f, 2f),
                color, markers);
            AddLocalRoute(site, "Right Return Loop", new Vector3(5.2f, 6.55f, 1.4f),
                new Vector3(2f, 0.08f, 15f), color, markers);
        }

        static void AddLocalRoute(Transform site, string name, Vector3 localPosition, Vector3 scale, Color color,
            List<GameObject> markers)
        {
            var route = GameObject.CreatePrimitive(PrimitiveType.Cube);
            route.name = $"Inquiry Local Map Route - {name}";
            route.transform.position = site.TransformPoint(localPosition);
            route.transform.rotation = site.rotation;
            route.transform.localScale = scale;
            Object.DestroyImmediate(route.GetComponent<Collider>());
            route.GetComponent<Renderer>().sharedMaterial = CreateMarkerMaterial(color * 0.82f);
            markers.Add(route);
        }

        static void AddLocalMapStage(Transform site, string label, Vector3 localPosition, Vector3 labelOffset,
            Color color, List<GameObject> markers)
        {
            var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pin.name = $"Inquiry Local Map Pin - {label}";
            pin.transform.position = site.TransformPoint(localPosition);
            pin.transform.localScale = new Vector3(1.15f, 0.12f, 1.15f);
            Object.DestroyImmediate(pin.GetComponent<Collider>());
            pin.GetComponent<Renderer>().sharedMaterial = CreateMarkerMaterial(color);
            markers.Add(pin);

            var textObject = new GameObject($"Inquiry Local Map Label - {label}");
            textObject.transform.position = site.TransformPoint(localPosition + labelOffset);
            textObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var text = textObject.AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.42f;
            text.fontSize = 48;
            text.color = Color.white;
            markers.Add(textObject);
        }

        static GameObject FindSceneObject(string name)
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (transform.name == name)
                        return transform.gameObject;
            }
            return null;
        }

        static void Render(Camera camera, string path)
        {
            var renderTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                camera.Render();
                var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                Object.DestroyImmediate(renderTexture);
            }
        }
    }
}
