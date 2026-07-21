using System.Collections;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// Authored physical-consequence preview for an engineering decision station.
    /// Hidden by default; shown briefly when the learner commits an unsafe choice,
    /// so the failure is experienced at world scale instead of as text alone.
    /// </summary>
    public sealed class ConsequenceVisual : MonoBehaviour
    {
        [SerializeField] string decisionId = string.Empty;
        [SerializeField, Min(1f)] float displaySeconds = 6f;

        Coroutine hideRoutine;

        public string DecisionId => decisionId;

        public void Configure(string id, float seconds = 6f)
        {
            decisionId = id;
            displaySeconds = Mathf.Max(1f, seconds);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (hideRoutine != null)
                StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        IEnumerator HideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(displaySeconds);
            hideRoutine = null;
            gameObject.SetActive(false);
        }

        public static ConsequenceVisual FindFor(string id)
        {
            foreach (var visual in Object.FindObjectsByType<ConsequenceVisual>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (visual.DecisionId == id)
                    return visual;
            }
            return null;
        }
    }
}
