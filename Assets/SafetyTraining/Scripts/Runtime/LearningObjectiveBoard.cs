using SafetyTraining.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Runtime
{
    [RequireComponent(typeof(Collider), typeof(XRSimpleInteractable))]
    public sealed class LearningObjectiveBoard : MonoBehaviour
    {
        [SerializeField] TrainingSiteId siteId;
        [SerializeField] Text objectiveIdDisplay;
        [SerializeField] Text titleDisplay;
        [SerializeField] Text statementDisplay;
        [SerializeField] Text evidenceDisplay;
        [SerializeField] Text standardDisplay;
        [SerializeField] Text pageDisplay;
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
        public void Configure(TrainingSiteId site, Text objectiveId, Text title, Text statement,
            Text evidence, Text standard, Text pageLabel)
        {
            siteId = site;
            objectiveIdDisplay = objectiveId;
            titleDisplay = title;
            statementDisplay = statement;
            evidenceDisplay = evidence;
            standardDisplay = standard;
            pageDisplay = pageLabel;
            page = 0;
            Refresh();
        }
        public void NextPage()
        {
            var count = LearningObjectiveCatalog.ForSite(siteId).Count;
            page = (page + 1) % count;
            Refresh();
        }
        void Refresh()
        {
            var objective = LearningObjectiveCatalog.ObjectiveAt(siteId, page);
            if (objectiveIdDisplay != null) objectiveIdDisplay.text = objective.Id;
            if (titleDisplay != null) titleDisplay.text = objective.Title.ToUpperInvariant();
            if (statementDisplay != null) statementDisplay.text = objective.Statement;
            if (evidenceDisplay != null) evidenceDisplay.text = objective.AssessmentEvidence;
            if (standardDisplay != null) standardDisplay.text = objective.Standards;
            if (pageDisplay != null)
                pageDisplay.text = $"SELECT PANEL FOR NEXT OBJECTIVE   {page + 1}/{LearningObjectiveCatalog.ForSite(siteId).Count}";
        }
        void OnSelected(SelectEnterEventArgs _) => NextPage();
    }
}
