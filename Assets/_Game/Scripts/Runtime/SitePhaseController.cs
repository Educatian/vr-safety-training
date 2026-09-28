using UnityEngine;

namespace Jobsite.Runtime
{
    // Switches the one site to a given day's construction phase. Works in edit mode (captures) and play.
    public sealed class SitePhaseController : MonoBehaviour
    {
        [SerializeField] private WorkDay day = WorkDay.Mon;

        public WorkDay Day => day;

        private void Awake() => Apply(day);

        public void SetDay(WorkDay value)
        {
            day = value;
            Apply(value);
        }

        public static void Apply(WorkDay day)
        {
            foreach (var member in Object.FindObjectsByType<PhaseMember>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                member.gameObject.SetActive(member.IsPresentOn(day));
        }
    }
}
