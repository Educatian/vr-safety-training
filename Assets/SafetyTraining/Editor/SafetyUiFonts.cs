using UnityEditor;
using UnityEngine;

namespace SafetyTraining.Editor
{
    /// <summary>
    /// Single resolution point for UI body text fonts. Prefers the bundled Inter
    /// family (SIL OFL), falling back to Liberation Sans and finally the engine
    /// default so scene builds never break on a missing asset.
    /// </summary>
    static class SafetyUiFonts
    {
        const string InterRegularPath = "Assets/SafetyTraining/Fonts/Inter-Regular.ttf";
        const string InterSemiBoldPath = "Assets/SafetyTraining/Fonts/Inter-SemiBold.ttf";
        const string LiberationPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";

        public static Font Body =>
            AssetDatabase.LoadAssetAtPath<Font>(InterRegularPath)
            ?? AssetDatabase.LoadAssetAtPath<Font>(LiberationPath)
            ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Font BodyStrong =>
            AssetDatabase.LoadAssetAtPath<Font>(InterSemiBoldPath)
            ?? Body;
    }
}
