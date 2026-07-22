using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    public sealed class SiteIsolationController : MonoBehaviour
    {
        [SerializeField] Transform[] hubRoots;
        [SerializeField] SiteExperienceZone[] sites;

        public static SiteIsolationController Instance { get; private set; }
        public int SiteCount => sites?.Length ?? 0;
        public int HubRootCount => hubRoots?.Length ?? 0;
        public bool HubVisible
        {
            get
            {
                if (hubRoots == null || hubRoots.Length == 0)
                    return false;
                foreach (var root in hubRoots)
                    if (root == null || !root.gameObject.activeSelf)
                        return false;
                return true;
            }
        }

        public int VisibleSiteCount
        {
            get
            {
                var count = 0;
                if (sites == null)
                    return count;
                foreach (var site in sites)
                    if (site != null && site.gameObject.activeSelf)
                        count++;
                return count;
            }
        }

        void Awake()
        {
            Instance = this;
            EnsureSolidPropColliders();
        }

        public int EnsureSolidPropColliders()
        {
            if (sites == null)
                return 0;
            var added = 0;
            var solidKeywords = new[]
            {
                "barrier", "cement", "crate", "pallet", "toolbox", "cart",
                "generator", "drum", "utility box", "ladder", "formwork",
                "material", "chainlink", "perimeter", "gate", "door",
                "cabinet", "hand truck", "delivery stack", "panel row",
                "brick", "rebar", "beam", "column", "pipe", "shelf",
                "rack", "stack", "box", "barricade", "hoarding",
                "compressor", "forklift", "scaffold", "shoring"
            };
            foreach (var site in sites)
            {
                if (site == null)
                    continue;
                foreach (var candidate in site.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.GetComponent<BoxCollider>() != null ||
                        candidate.GetComponent<Terrain>() != null ||
                        candidate.GetComponent<Canvas>() != null ||
                        candidate.GetComponent<TextMesh>() != null ||
                        HasSolidColliderAncestor(candidate))
                        continue;
                    var lowerName = candidate.name.ToLowerInvariant();
                    var shouldBlock = false;
                    foreach (var keyword in solidKeywords)
                    {
                        if (!lowerName.Contains(keyword))
                            continue;
                        shouldBlock = true;
                        break;
                    }
                    if (!shouldBlock)
                        continue;
                    if (!HasVisibleRenderable(candidate))
                        continue;

                    var renderers = candidate.GetComponentsInChildren<Renderer>(true);
                    Bounds? worldBounds = null;
                    foreach (var renderer in renderers)
                    {
                        if (!renderer.enabled || renderer.GetComponent<TextMesh>() != null)
                            continue;
                        worldBounds = worldBounds.HasValue
                            ? Encapsulate(worldBounds.Value, renderer.bounds)
                            : renderer.bounds;
                    }
                    if (!worldBounds.HasValue)
                        continue;
                    var bounds = worldBounds.Value;
                    var localBounds = new Bounds(candidate.InverseTransformPoint(bounds.center), Vector3.zero);
                    foreach (var x in new[] { bounds.min.x, bounds.max.x })
                    foreach (var y in new[] { bounds.min.y, bounds.max.y })
                    foreach (var z in new[] { bounds.min.z, bounds.max.z })
                        localBounds.Encapsulate(candidate.InverseTransformPoint(new Vector3(x, y, z)));
                    var collider = candidate.gameObject.AddComponent<BoxCollider>();
                    collider.center = localBounds.center;
                    collider.size = localBounds.size + Vector3.one * 0.04f;
                    collider.isTrigger = false;
                    added++;
                }
            }
            return added;
        }

        static bool HasSolidColliderAncestor(Transform candidate)
        {
            var parent = candidate.parent;
            while (parent != null)
            {
                var collider = parent.GetComponent<BoxCollider>();
                if (collider != null && !collider.isTrigger)
                    return true;
                parent = parent.parent;
            }
            return false;
        }

        static bool HasVisibleRenderable(Transform candidate)
        {
            foreach (var renderer in candidate.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || renderer.GetComponent<TextMesh>() != null)
                    continue;
                if (renderer.bounds.size.x <= 0.05f &&
                    renderer.bounds.size.y <= 0.05f &&
                    renderer.bounds.size.z <= 0.05f)
                    continue;
                return true;
            }
            return false;
        }

        static Bounds Encapsulate(Bounds value, Bounds addition)
        {
            value.Encapsulate(addition);
            return value;
        }

        void Start()
        {
            ShowHub();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Configure(Transform[] trainingHubRoots, SiteExperienceZone[] trainingSites)
        {
            hubRoots = trainingHubRoots;
            sites = trainingSites;
        }

        public void ShowHub()
        {
            SetHubActive(true);
            SetOnlySiteActive(null);
            Debug.Log("Site isolation state: training hub only.");
        }

        public void ShowSite(TrainingSiteId siteId)
        {
            SetHubActive(false);
            SiteExperienceZone selected = null;
            if (sites != null)
            {
                foreach (var site in sites)
                    if (site != null && site.SiteId == siteId)
                        selected = site;
            }
            SetOnlySiteActive(selected);
            Debug.Log($"Site isolation state: {siteId} only.");
        }

        public bool IsSiteVisible(TrainingSiteId siteId)
        {
            if (sites == null)
                return false;
            foreach (var site in sites)
                if (site != null && site.SiteId == siteId)
                    return site.gameObject.activeSelf;
            return false;
        }

        void SetHubActive(bool active)
        {
            if (hubRoots == null)
                return;
            foreach (var root in hubRoots)
                if (root != null)
                    root.gameObject.SetActive(active);
        }

        void SetOnlySiteActive(SiteExperienceZone selected)
        {
            if (sites == null)
                return;
            foreach (var site in sites)
                if (site != null)
                    site.gameObject.SetActive(site == selected);
        }
    }
}
