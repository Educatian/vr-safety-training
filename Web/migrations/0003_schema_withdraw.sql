-- Event schema v1 (quality review 2026-09-30): every row records the event-schema, build, consent and ECD versions it
-- was produced under, so analyses can filter by version and a changed scoring model is never mixed silently.
ALTER TABLE events ADD COLUMN schema TEXT;
ALTER TABLE events ADD COLUMN build TEXT;
ALTER TABLE events ADD COLUMN consent TEXT;
ALTER TABLE events ADD COLUMN ecd TEXT;
CREATE INDEX IF NOT EXISTS idx_events_received ON events(received);
-- Withdrawal audit: what was deleted and when (no identifiers beyond the session/class codes already pseudonymous).
CREATE TABLE IF NOT EXISTS withdrawals (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  received TEXT NOT NULL DEFAULT (datetime('now')),
  scope TEXT NOT NULL, class_code TEXT, sessions INTEGER, events_deleted INTEGER, completions_deleted INTEGER
);
