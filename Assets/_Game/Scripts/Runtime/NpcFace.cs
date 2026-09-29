using UnityEngine;

namespace Jobsite.Runtime
{
    // Face layer for the Tripo NPCs (shape keys from Tools/blender/npc_face_rig.py): auto-blink, situational
    // expressions and viseme cycling while the NPC is speaking. Body gestures stay in CrewGestures, which drives this.
    public sealed class NpcFace : MonoBehaviour
    {
        public enum Mood { Neutral, Smile, Frown, Surprise, Angry, BrowUp }

        private SkinnedMeshRenderer face;
        private int blinkL = -1, blinkR = -1, jaw = -1;
        private int[] moods;          // index per Mood (Neutral unused)
        private int[] visemes;
        private float nextBlink, blinkT = -1, moodUntil, speakUntil, nextViseme;
        private Mood mood; private int viseme = -1;
        private readonly float[] moodW = new float[6];
        public Mood Current => Time.time < moodUntil ? mood : Mood.Neutral;
        public bool HasFace => face != null;

        private void Awake()
        {
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("blink_L") >= 0) { face = r; break; }
            if (face == null) { enabled = false; return; }
            var m = face.sharedMesh;
            int I(string n) => m.GetBlendShapeIndex(n);
            blinkL = I("blink_L"); blinkR = I("blink_R"); jaw = I("jaw_open");
            moods = new[] { -1, I("smile"), I("frown"), I("surprise"), I("angry"), I("brow_up") };
            visemes = new[] { I("AA"), I("EE"), I("OO"), I("MBP"), I("FV") };
            nextBlink = Time.time + Random.Range(1f, 4f);
        }

        public void Express(Mood m, float seconds = 2.5f) { mood = m; moodUntil = Time.time + seconds; }
        public void Speak(float seconds) => speakUntil = Time.time + seconds;

        private void LateUpdate()
        {
            // Blink: ~0.14 s close-open every 2.5-6 s (starting values; real adults blink ~15-20/min).
            if (blinkT < 0 && Time.time >= nextBlink) blinkT = 0;
            var blink = 0f;
            if (blinkT >= 0)
            {
                blinkT += Time.deltaTime;
                blink = Mathf.Clamp01(1f - Mathf.Abs(blinkT / 0.07f - 1f));
                if (blinkT > 0.14f) { blinkT = -1; nextBlink = Time.time + Random.Range(2.5f, 6f); }
            }
            Set(blinkL, blink * 100f); Set(blinkR, blink * 100f);

            // Moods ease in and out.
            for (var i = 1; i < moods.Length; i++)
            {
                var target = Current == (Mood)i ? (i == (int)Mood.BrowUp ? 60f : 70f) : 0f;
                moodW[i] = Mathf.MoveTowards(moodW[i], target, Time.deltaTime * 300f);
                Set(moods[i], moodW[i]);
            }

            // Speech: a new viseme every ~90 ms with the jaw following.
            var speaking = Time.time < speakUntil;
            if (speaking && Time.time >= nextViseme) { viseme = Random.Range(0, visemes.Length); nextViseme = Time.time + Random.Range(0.07f, 0.12f); }
            for (var i = 0; i < visemes.Length; i++) Set(visemes[i], speaking && i == viseme ? 80f : 0f);
            Set(jaw, speaking ? (viseme == 3 ? 0f : 35f + Mathf.Sin(Time.time * 20f) * 15f) : 0f);
        }

        private void Set(int index, float w) { if (index >= 0) face.SetBlendShapeWeight(index, w); }
    }
}
