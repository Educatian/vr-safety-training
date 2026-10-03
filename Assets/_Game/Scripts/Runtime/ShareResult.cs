using System.Runtime.InteropServices;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Hands the result card to the system share sheet (phones) or the clipboard (desktop). Web build: Plugins/ShareResult.jslib.
    public static class ShareResult
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int CP_Share(string text);
#endif
        public static string LastShared { get; private set; }

        // 1 = share sheet opened, 2 = copied, 0 = not available (the text stays on screen to copy by hand).
        public static int Share(string text)
        {
            LastShared = text;
#if UNITY_WEBGL && !UNITY_EDITOR
            return CP_Share(text);
#else
            GUIUtility.systemCopyBuffer = text;
            return 2;
#endif
        }
    }
}
