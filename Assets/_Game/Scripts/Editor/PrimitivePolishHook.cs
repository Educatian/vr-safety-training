using UnityEditor.Callbacks;
using UnityEngine.SceneManagement;

namespace Jobsite.Editor
{
    // Runs when a scene is processed for the player build or entering play mode, before static batching.
    public static class PrimitivePolishHook
    {
        [PostProcessScene(10)]
        public static void OnPostprocessScene()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++) Jobsite.Runtime.PrimitivePolish.Apply(SceneManager.GetSceneAt(i));
        }
    }
}
