using UnityEngine;

namespace Jobsite.Runtime
{
    // Build-time reference to extra crew bodies so the runtime can vary the background crew (CrewVariety).
    // Resources/CrewBodies.asset is (re)created by the web build (Editor/WebBuild.EnsureCrewBodies).
    public sealed class CrewBodies : ScriptableObject
    {
        public GameObject female;
        public const string ResourcePath = "CrewBodies";
        public const string FemalePath = "Assets/ThirdParty/MicrosoftRocketbox/Construction_Female_01/Export/Construction_Female_01.fbx";

        public static GameObject Female()
        {
            var b = Resources.Load<CrewBodies>(ResourcePath);
            if (b != null && b.female != null) return b.female;
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FemalePath);
#else
            return null;
#endif
        }
    }
}
