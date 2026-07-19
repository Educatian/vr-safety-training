using SafetyTraining.Core;

namespace SafetyTraining.Runtime
{
    public sealed class InquiryTelemetryEvent
    {
        public TrainingSiteId SiteId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public string HazardType { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string Hypothesis { get; set; } = string.Empty;
        public int EvidenceCount { get; set; }
        public int CollectedItemCount { get; set; }
        public int DistractorCount { get; set; }
        public bool IsDistractor { get; set; }
    }
}
