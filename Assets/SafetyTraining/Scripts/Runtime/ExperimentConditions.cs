using System;

namespace SafetyTraining.Runtime
{
    public enum FeedbackMode
    {
        Immediate,
        Delayed
    }

    /// <summary>
    /// Session-level experimental condition flags. Feedback mode controls whether
    /// inspection verdicts and the running score are shown immediately (default)
    /// or withheld until the site debrief — the manipulation the stealth-assessment
    /// design needs to keep exploration from turning into score hunting.
    /// Assignment: environment variable SAFETY_FEEDBACK_MODE=delayed, or Override()
    /// at runtime. The active condition is logged to telemetry at session start.
    /// </summary>
    public static class ExperimentConditions
    {
        public static FeedbackMode Feedback { get; private set; } = Resolve();

        public static bool DelayedFeedback => Feedback == FeedbackMode.Delayed;

        public static void Override(FeedbackMode mode) => Feedback = mode;

        public static string Token => DelayedFeedback ? "delayed" : "immediate";

        static FeedbackMode Resolve()
        {
            return string.Equals(Environment.GetEnvironmentVariable("SAFETY_FEEDBACK_MODE"),
                "delayed", StringComparison.OrdinalIgnoreCase)
                ? FeedbackMode.Delayed
                : FeedbackMode.Immediate;
        }
    }
}
