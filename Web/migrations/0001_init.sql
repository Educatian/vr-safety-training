-- Play events (PII-free: pseudonymous class/learner codes; chat text is never stored).
CREATE TABLE IF NOT EXISTS events (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  received TEXT NOT NULL DEFAULT (datetime('now')),
  session TEXT NOT NULL, class_code TEXT, learner TEXT, episode INTEGER, seed INTEGER,
  t TEXT, kind TEXT, condition TEXT, detail TEXT, clock REAL
);
CREATE INDEX IF NOT EXISTS idx_events_class ON events(class_code, learner);
CREATE INDEX IF NOT EXISTS idx_events_session ON events(session);

-- Server-signed completion records; the code is what a student hands in.
CREATE TABLE IF NOT EXISTS completions (
  code TEXT PRIMARY KEY,
  received TEXT NOT NULL DEFAULT (datetime('now')),
  session TEXT NOT NULL, class_code TEXT, learner TEXT, episode INTEGER,
  xp INTEGER, hii REAL, precision REAL, incidents INTEGER, quiz_correct INTEGER, quiz_total INTEGER
);
CREATE INDEX IF NOT EXISTS idx_completions_class ON completions(class_code, learner);

-- Fixed-window rate limit counters for the AI chat proxy.
CREATE TABLE IF NOT EXISTS ratelimit (k TEXT NOT NULL, w INTEGER NOT NULL, n INTEGER NOT NULL DEFAULT 0, PRIMARY KEY (k, w));
