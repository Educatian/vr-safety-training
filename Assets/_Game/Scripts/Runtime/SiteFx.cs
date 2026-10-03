using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Immersion feedback for the moments that matter (2026-09-30): the camera viewfinder locks on when a condition is
    // photographable, a ground pulse marks the shot, dust kicks up when a control goes in (with a check mark rising),
    // a near miss flashes red and jolts the view, rewards toast at the top. All cheap (a few particles, one overlay
    // canvas, no post effects) and honest: effects follow real game events, never hint at hidden hazards.
    // Reduce motion (GameSettings) turns camera jolts off.
    public sealed class SiteFx : MonoBehaviour
    {
        static SiteFx instance;
        static SiteFx I => instance != null ? instance : instance = new GameObject("SiteFx").AddComponent<SiteFx>();

        Canvas canvas;
        Image flash;
        Text toast;
        RectTransform finder;
        readonly List<Image> brackets = new List<Image>();
        float flashUntil, flashDur, toastUntil, shakeUntil, shakeAmp, finderLock;
        Color flashColor;
        bool finderOn;
        Material particleMat;
        Transform shakeView; Vector3 shakeBase; bool shaking;

        public static int Bursts { get; private set; }
        public static string LastToast { get; private set; } = "";

        void Awake()
        {
            var go = new GameObject("SiteFxCanvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 300;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080) / GameSettings.TextScale; sc.matchWidthOrHeight = 1;
            flash = new GameObject("Flash", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            flash.transform.SetParent(go.transform, false); Stretch(flash.rectTransform); flash.raycastTarget = false; flash.color = Color.clear;
            toast = new GameObject("Toast", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            toast.transform.SetParent(go.transform, false);
            var tr = toast.rectTransform; tr.anchorMin = new Vector2(0.3f, 0.8f); tr.anchorMax = new Vector2(0.7f, 0.86f); tr.offsetMin = tr.offsetMax = Vector2.zero;
            toast.font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            toast.fontSize = 34; toast.alignment = TextAnchor.MiddleCenter; toast.color = new Color(1f, .85f, .3f, 0f); toast.raycastTarget = false;
            // Viewfinder: four corner brackets around the aim point.
            finder = new GameObject("Viewfinder", typeof(RectTransform)).GetComponent<RectTransform>();
            finder.SetParent(go.transform, false); finder.anchorMin = finder.anchorMax = new Vector2(0.5f, 0.5f); finder.sizeDelta = new Vector2(260, 190);
            foreach (var (x, y) in new[] { (-1, 1), (1, 1), (-1, -1), (1, -1) })
            {
                var h = Bar(finder, new Vector2(x * 0.5f, y * 0.5f), new Vector2(-x * 22, -y * 2), new Vector2(44, 4));
                var v = Bar(finder, new Vector2(x * 0.5f, y * 0.5f), new Vector2(-x * 2, -y * 22), new Vector2(4, 44));
                brackets.Add(h); brackets.Add(v);
            }
            finder.gameObject.SetActive(false);
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { name = "FxSoftDot" };
            for (var yy = 0; yy < 32; yy++) for (var xx = 0; xx < 32; xx++)
            {
                var d = Vector2.Distance(new Vector2(xx + .5f, yy + .5f), new Vector2(16, 16)) / 16f;
                tex.SetPixel(xx, yy, new Color(1, 1, 1, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
            tex.Apply();
            particleMat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, name = "FxParticles" };
        }

        static Image Bar(RectTransform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = new GameObject("Bracket", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            var r = img.rectTransform; r.anchorMin = r.anchorMax = anchor; r.sizeDelta = size; r.anchoredPosition = pos;
            img.color = new Color(1f, 1f, 1f, 0.85f); img.raycastTarget = false;
            return img;
        }

        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        // ---- API ----------------------------------------------------------------------------------------------------
        public static void Viewfinder(bool photographable) => I.finderOn = photographable;

        public static void Toast(string text) { var i = I; i.toast.text = text; i.toastUntil = Time.unscaledTime + 2.2f; LastToast = text; }

        public static void Flash(Color c, float seconds) { var i = I; i.flashColor = c; i.flashDur = seconds; i.flashUntil = Time.unscaledTime + seconds; }

        public static void Shake(Transform view, float amplitude, float seconds)
        {
            if (GameSettings.ReduceMotion || view == null) return;
            var i = I;
            if (!i.shaking) { i.shakeView = view; i.shakeBase = view.localPosition; }
            i.shaking = true; i.shakeAmp = amplitude; i.shakeUntil = Time.time + seconds;
        }

        // Dust / sparkle burst: a one-shot particle system that cleans itself up.
        public static void Burst(Vector3 at, Color color, int count = 18, float speed = 1.2f, float size = 0.22f)
        {
            var go = new GameObject("FxBurst");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.duration = 0.5f; main.loop = false; main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed); main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color; main.gravityModifier = 0.25f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = count;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.25f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = I.particleMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            Bursts++;
        }

        // Expanding ring on the ground at a spot (the photo "shutter" at the condition, a request marker).
        public static void Pulse(Vector3 at, Color color, float radius = 1.2f, float seconds = 0.7f) => I.StartCoroutine(I.PulseRoutine(at, color, radius, seconds));

        System.Collections.IEnumerator PulseRoutine(Vector3 at, Color color, float radius, float seconds)
        {
            var go = new GameObject("FxPulse");
            var lr = go.AddComponent<LineRenderer>();
            lr.loop = true; lr.positionCount = 40; lr.useWorldSpace = true; lr.sharedMaterial = particleMat; lr.widthMultiplier = 0.06f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (var t = 0f; t < seconds; t += Time.deltaTime)
            {
                var k = t / seconds; var rr = Mathf.Lerp(0.2f, radius, 1f - (1f - k) * (1f - k));
                for (var i = 0; i < 40; i++) { var a = i * Mathf.PI * 2 / 40; lr.SetPosition(i, at + new Vector3(Mathf.Cos(a) * rr, 0.04f, Mathf.Sin(a) * rr)); }
                var c = color; c.a *= 1f - k; lr.startColor = lr.endColor = c;
                yield return null;
            }
            Destroy(go);
        }

        // A check mark that rises and fades over a spot (control installed / request done).
        public static void Check(Vector3 at) => I.StartCoroutine(I.CheckRoutine(at));

        System.Collections.IEnumerator CheckRoutine(Vector3 at)
        {
            var go = new GameObject("FxCheck");
            var tm = go.AddComponent<TextMesh>();
            tm.text = "✓"; tm.fontSize = 96; tm.characterSize = 0.035f; tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(.45f, 1f, .5f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); tm.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            var cam = Camera.main;
            for (var t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                go.transform.position = at + Vector3.up * (0.4f + t * 0.5f);
                if (cam != null) go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
                var c = tm.color; c.a = Mathf.Clamp01(1.4f - t); tm.color = c;
                yield return null;
            }
            Destroy(go);
        }

        void LateUpdate()
        {
            // flash
            if (Time.unscaledTime < flashUntil) { var c = flashColor; c.a *= (flashUntil - Time.unscaledTime) / Mathf.Max(0.01f, flashDur); flash.color = c; }
            else if (flash.color.a > 0f) flash.color = Color.clear;
            // toast
            var ta = Mathf.Clamp01((toastUntil - Time.unscaledTime) / 0.4f);
            toast.color = new Color(1f, .85f, .3f, ta);
            // viewfinder: brackets close in when a photographable condition is centred
            finderLock = Mathf.MoveTowards(finderLock, finderOn ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            finder.gameObject.SetActive(finderLock > 0.01f);
            if (finderLock > 0.01f)
            {
                var s = Mathf.Lerp(1.35f, 1f, finderLock);
                finder.localScale = new Vector3(s, s, 1f);
                foreach (var b in brackets) { var c = b.color; c.a = 0.85f * finderLock; b.color = c; }
            }
            finderOn = false;   // must be re-asserted every frame by the HUD
            // camera jolt
            if (shaking && shakeView != null)
            {
                if (Time.time < shakeUntil && !GameSettings.ReduceMotion)
                {
                    var k = (shakeUntil - Time.time);
                    shakeView.localPosition = shakeBase + new Vector3(Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * 30f) - 0.5f, 0f) * shakeAmp * Mathf.Clamp01(k * 3f);
                }
                else { shakeView.localPosition = shakeBase; shaking = false; }
            }
        }
    }
}
