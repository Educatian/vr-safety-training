using System;
using UnityEngine;

namespace Jobsite.Runtime
{
    [Flags]
    public enum WorkDay { None = 0, Mon = 1, Tue = 2, Wed = 4, Thu = 8, Fri = 16, All = 31 }

    // Marks which days of the project week this object exists on (SiteLayout §3 daily evolution).
    public sealed class PhaseMember : MonoBehaviour
    {
        [SerializeField] private WorkDay days = WorkDay.All;

        public WorkDay Days => days;
        public void Configure(WorkDay visibleOn) => days = visibleOn;
        public bool IsPresentOn(WorkDay day) => (days & day) != 0;
    }
}
