using UnityEngine;

namespace SafetyTraining.Runtime
{
    /// <summary>One snap point on the assembly jig, accepting one part at a time.</summary>
    public sealed class AssemblySocket : MonoBehaviour
    {
        [SerializeField] string socketId = "socket";
        [SerializeField] string acceptedCategory = "sling";
        [SerializeField, Min(0.1f)] float snapRadius = 0.55f;
        [SerializeField] string prerequisiteCategory = "";
        [SerializeField, Min(0)] int prerequisiteCount;

        public string SocketId => socketId;
        public string AcceptedCategory => acceptedCategory;
        public float SnapRadius => snapRadius;
        public string PrerequisiteCategory => prerequisiteCategory;
        public int PrerequisiteCount => prerequisiteCount;

        /// <summary>Gates this socket until N parts of another category are seated.</summary>
        public void RequirePrerequisite(string category, int count)
        {
            prerequisiteCategory = category;
            prerequisiteCount = Mathf.Max(0, count);
        }
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
