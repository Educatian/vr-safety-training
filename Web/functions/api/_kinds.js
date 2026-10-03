// Event-schema v1: the only event kinds the game emits (kept in sync with Runtime/ShiftDirector.cs Log("...") calls by
// the EditMode test TelemetrySchemaTests). Rows with any other kind are rejected.
export const SCHEMA = "cp-events-v1";
export const KINDS = new Set([
  "access", "arcade_result", "carry_forward", "hands_on", "inspect_close", "chat_reply", "checkin", "coach_query", "confirm_compliant", "control_choose", "crew_request", "crew_request_done", "crew_request_missed", "crew_self_report", "episode_complete",
  "hierarchy_order", "hint", "incident_review", "install_attempt", "install_success", "instrument_match", "kit_choose",
  "kit_collect", "ksa", "ksa_profile", "mastery", "measure", "measure_none", "measured_first", "mission_complete",
  "mission_step", "perf", "photo", "placement_attempt", "quiz", "radio_query_open", "report", "session_start", "shift_begin",
  "shift_end", "speakup_choice", "stop_work", "streak", "talk_top_risk", "toolbox_talk", "vehicle_enter", "weather",
  "weather_decision", "why_choice",
  // DayEventKind (hazard state changes)
  "Lapsed", "NearMiss", "Recordable", "StopLifted",
]);
// Pseudonymous roster codes only: letters, digits, dash, underscore. A value with spaces (a typed real name) is refused.
export const CODE = /^[A-Za-z0-9_-]{0,24}$/;
// Days a row is kept; older rows are purged opportunistically on ingest (Pages Functions have no cron).
export const RETENTION_DAYS_DEFAULT = 400;
