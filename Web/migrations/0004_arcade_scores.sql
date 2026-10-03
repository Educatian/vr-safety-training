-- Hazard Hunt daily leaderboard (public, opt-in). A self-chosen display handle only: never the roster code, never a
-- name field, and nothing here joins to the research tables. Rows older than 60 days are purged on post.
CREATE TABLE IF NOT EXISTS arcade_scores (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  received TEXT NOT NULL DEFAULT (datetime('now')),
  day INTEGER NOT NULL, handle TEXT NOT NULL, score INTEGER NOT NULL,
  found INTEGER, total INTEGER, false_alarms INTEGER, incidents INTEGER, grade TEXT, seconds INTEGER,
  session TEXT NOT NULL, build TEXT
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_arcade_day_session ON arcade_scores(day, session);
CREATE INDEX IF NOT EXISTS idx_arcade_day_score ON arcade_scores(day, score DESC);
