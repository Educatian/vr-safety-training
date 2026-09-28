using UnityEngine;

namespace Jobsite.Runtime
{
    // Gate check-in (GDD §3 Mon): sign the visitor/crew sheet and take each PPE item from the rack.
    // Taking an item hides it from the rack so the learner sees the action register.
    public sealed class CheckInStation : MonoBehaviour
    {
        public enum Kind { SignIn, HardHat, Vest, Glasses, Gloves }

        [SerializeField] private Kind kind;
        [SerializeField] private GameObject hideWhenTaken;

        public Kind Station => kind;

        public void Configure(Kind k, GameObject visual) { kind = k; hideWhenTaken = visual; }

        public void Use(ShiftDirector director)
        {
            director.CheckIn(kind);
            if (hideWhenTaken != null && kind != Kind.SignIn) hideWhenTaken.SetActive(false);
        }
    }
}
