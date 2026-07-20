# Spatial analytics contract

The training player writes pseudonymous JSONL events to `Application.persistentDataPath/SafetyTrainingLogs`.

`spatial_sample` is emitted once per second while a learner is active in a site. It records both world coordinates and coordinates relative to that site's `SiteExperienceZone`. `site_enter`, `site_exit`, `zone_enter`, and `zone_exit` capture visit and sub-zone dwell duration. Inspection, hands-on placement, and coach-turn events share the same coordinate fields, so analysis can join learner movement to decisions and guidance.

Use `Safety Training/Analytics/Export Persistent Logs To CSV` to produce `spatial_samples.csv`, `inquiry_events.csv`, `zone_dwell.csv`, and `learner_route_summary.csv`. The spatial CSV preserves `zoneName` and `metricKind`, so downstream analysis can separate instant events, seconds of dwell, release distance, and sampled movement distance without re-reading raw JSONL. The route summary rolls each learner/session/site into visit seconds, zone dwell seconds, path distance, zones visited, evidence collected, placement attempts, placement successes, and coach turns.

The stream intentionally stores no name, account identifier, voice content, or headset biometrics. Collect informed consent before exporting logs, apply a retention limit, and aggregate or de-identify records before research reporting.

When a site is expanded, keep its `SiteExperienceZone` at a stable local origin. Add task landmarks at stable locations rather than moving existing hazards; this preserves comparability of heat maps, path length, dwell time, return visits, and decision-to-action sequences across releases.
