using System.Linq;
using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    internal sealed class PracticalProgressRegistry
    {
        ConstructionHandsOnController construction;
        SitePracticalController[] sitePracticals;

        public PracticalProgressRegistry()
        {
            RefreshControllers();
        }

        public int CompletedSteps
        {
            get
            {
                EnsureLiveControllers();
                return (construction?.CompletedSteps ?? 0) +
                       sitePracticals.Sum(item => item.CompletedSteps);
            }
        }

        public int TotalSteps
        {
            get
            {
                EnsureLiveControllers();
                return (construction?.TotalSteps ?? 0) +
                       sitePracticals.Sum(item => item.TotalSteps);
            }
        }

        public bool IsComplete
        {
            get
            {
                EnsureLiveControllers();
                return construction != null && construction.IsComplete &&
                       sitePracticals.Length == 4 && sitePracticals.All(item => item.IsComplete);
            }
        }

        public float Progress01 => (float)CompletedSteps / Mathf.Max(1, TotalSteps);

        public void Begin(TrainingSiteId siteId)
        {
            EnsureLiveControllers();
            if (siteId == TrainingSiteId.Construction)
            {
                construction?.Begin();
                return;
            }

            sitePracticals.FirstOrDefault(item => item.SiteId == siteId)?.Begin();
        }

        void EnsureLiveControllers()
        {
            if (construction == null || sitePracticals == null || sitePracticals.Length != 4 ||
                sitePracticals.Any(item => item == null))
                RefreshControllers();
        }

        void RefreshControllers()
        {
            construction = Object.FindFirstObjectByType<ConstructionHandsOnController>();
            sitePracticals = Object.FindObjectsByType<SitePracticalController>(FindObjectsSortMode.None);
        }
    }
}
