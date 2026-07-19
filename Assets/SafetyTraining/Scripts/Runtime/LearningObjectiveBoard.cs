using SafetyTraining.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Runtime
{
    [RequireComponent(typeof(Collider), typeof(XRSimpleInteractable))]
    public sealed class LearningObjectiveBoard : MonoBehaviour
    {
        [SerializeField] TrainingSiteId siteId;
        [SerializeField] TextMesh body;
        int page;
        XRSimpleInteractable interactable;

        public TrainingSiteId SiteId => siteId;

        void Awake() => interactable = GetComponent<XRSimpleInteractable>();
        void OnEnable()
        {
            if (interactable == null) interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnSelected);
        }
        void OnDisable()
        {
            if (interactable != null) interactable.selectEntered.RemoveListener(OnSelected);
        }
        void OnMouseDown()
        {
            if (DesktopPointerInputGate.CanUseWorldPointer) NextPage();
        }
        public void Configure(TrainingSiteId site, TextMesh display)
        {
            siteId = site; body = display; page = 0; Refresh();
        }
        public void NextPage()
        {
            var count = LearningObjectiveCatalog.ForSite(siteId).Count;
            page = (page + 1) % count;
            Refresh();
        }
        void Refresh()
        {
            if (body == null) return;
            var objective = LearningObjectiveCatalog.ObjectiveAt(siteId, page);
            body.text = Wrap($"{objective.Id}  {objective.Title.ToUpperInvariant()}\n\nOBJECTIVE\n{objective.Statement}\n\nASSESSMENT EVIDENCE\n{objective.AssessmentEvidence}\n\nSTANDARD\n{objective.Standards}\n\nSELECT BOARD FOR NEXT OBJECTIVE", 58);
        }
        static string Wrap(string value, int width)
        {
            var output = new System.Text.StringBuilder();
            foreach (var paragraph in value.Split('\n'))
            {
                var length = 0;
                foreach (var word in paragraph.Split(' '))
                {
                    if (length > 0 && length + word.Length + 1 > width)
                    {
                        output.Append('\n'); length = 0;
                    }
                    if (length > 0) { output.Append(' '); length++; }
                    output.Append(word); length += word.Length;
                }
                output.Append('\n');
            }
            return output.ToString().TrimEnd();
        }
        void OnSelected(SelectEnterEventArgs _) => NextPage();
    }
}
