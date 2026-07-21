using System;
using System.Collections.Generic;

namespace SafetyTraining.Core
{
    public sealed class RetrievalItemSpec
    {
        public RetrievalItemSpec(TrainingSiteId siteId, string targetId, bool isHazard)
        {
            if (string.IsNullOrWhiteSpace(targetId))
                throw new ArgumentException("A retrieval item needs a stable identifier.", nameof(targetId));
            SiteId = siteId;
            TargetId = targetId;
            IsHazard = isHazard;
        }

        public TrainingSiteId SiteId { get; }
        public string TargetId { get; }
        public bool IsHazard { get; }
    }

    public sealed class RetrievalJudgment
    {
        public RetrievalJudgment(bool correct, bool firstAttempt, int remainingItems)
        {
            Correct = correct;
            FirstAttempt = firstAttempt;
            RemainingItems = remainingItems;
        }

        public bool Correct { get; }
        public bool FirstAttempt { get; }
        public int RemainingItems { get; }
    }

    /// <summary>
    /// Post-completion recertification round: conditions the learner previously
    /// misjudged (false-positived look-alikes) are re-presented, mixed with real
    /// hazards from the same site, for explicit hazard/controlled reclassification.
    /// Score-neutral by design; every judgment is telemetry.
    /// </summary>
    public sealed class RetrievalRoundPlan
    {
        readonly Dictionary<string, RetrievalItemSpec> items = new(StringComparer.Ordinal);
        readonly HashSet<string> resolvedItems = new(StringComparer.Ordinal);
        readonly HashSet<string> attemptedItems = new(StringComparer.Ordinal);

        public RetrievalRoundPlan(IEnumerable<RetrievalItemSpec> itemSpecs)
        {
            foreach (var item in itemSpecs)
            {
                if (!items.ContainsKey(item.TargetId))
                    items.Add(item.TargetId, item);
            }
        }

        public int ItemCount => items.Count;
        public int ResolvedCount => resolvedItems.Count;
        public bool IsComplete => items.Count > 0 && resolvedItems.Count == items.Count;
        public IEnumerable<RetrievalItemSpec> Items => items.Values;

        public bool IsResolved(string targetId) => resolvedItems.Contains(targetId);

        /// <summary>
        /// Records a hazard/controlled judgment. An item is resolved by its first
        /// correct judgment; wrong judgments leave it open for retry.
        /// </summary>
        public RetrievalJudgment Judge(string targetId, bool judgedHazard)
        {
            if (!items.TryGetValue(targetId, out var item) || resolvedItems.Contains(targetId))
                return null;
            var firstAttempt = attemptedItems.Add(targetId);
            var correct = item.IsHazard == judgedHazard;
            if (correct)
                resolvedItems.Add(targetId);
            return new RetrievalJudgment(correct, firstAttempt, items.Count - resolvedItems.Count);
        }
    }
}
