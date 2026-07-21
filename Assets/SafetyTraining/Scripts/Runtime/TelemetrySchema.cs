namespace SafetyTraining.Runtime
{
    /// <summary>
    /// Version stamp written into every locally persisted telemetry entry so downstream
    /// analysis can detect schema changes across builds. Bump when any logged field
    /// is added, removed, or changes meaning.
    /// </summary>
    public static class TelemetrySchema
    {
        public const int Version = 2;
    }
}
