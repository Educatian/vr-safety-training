using UnityEngine;

namespace SafetyTraining.Runtime
{
    /// <summary>One snap point on the assembly jig, accepting one part at a time.</summary>
    public sealed class AssemblySocket : MonoBehaviour
    {
        [SerializeField] string socketId = "socket";
        [SerializeField] string acceptedCategory = "sling";
        [SerializeField, Min(0.1f)] float snapRadius = 0.55f;

        public string SocketId => socketId;
        public string AcceptedCategory => acceptedCategory;
        public float SnapRadius => snapRadius;
        public AssemblyPart Occupant { get; private set; }
        public Vector3 AttachPosition => transform.position;
        public Quaternion AttachRotation => transform.rotation;

        public void Configure(string id, string category, float radius)
        {
            socketId = id;
            acceptedCategory = category;
            snapRadius = Mathf.Max(0.1f, radius);
        }

        public bool IsFree => Occupant == null;

        public void SetOccupant(AssemblyPart part) => Occupant = part;
    }
}
