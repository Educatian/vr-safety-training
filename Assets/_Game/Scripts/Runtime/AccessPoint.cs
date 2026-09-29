using UnityEngine;

namespace Jobsite.Runtime
{
    // Stair tower / ladder access between levels (E to climb). A short fade stands in for the climb.
    public sealed class AccessPoint : MonoBehaviour
    {
        [SerializeField] private string label = "Climb the stair tower";
        [SerializeField] private Vector3 exit;
        [SerializeField] private float exitYaw;

        public string Label => label;
        public void Configure(string text, Vector3 to, float yaw) { label = text; exit = to; exitYaw = yaw; }

        public void Use(SitePlayer player)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(exit, Quaternion.Euler(0, exitYaw, 0));
            if (cc != null) cc.enabled = true;
            AudioDirector.Play("step_0"); AudioDirector.Play("step_1", 0.7f);
        }
    }
}
