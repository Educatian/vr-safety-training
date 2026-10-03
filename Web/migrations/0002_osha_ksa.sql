-- Every event carries its OSHA standard (29 CFR 1926.x) and, for scored actions, a KSA code + 0..1 performance score.
ALTER TABLE events ADD COLUMN cfr TEXT;
ALTER TABLE events ADD COLUMN ksa TEXT;
ALTER TABLE events ADD COLUMN score REAL;
CREATE INDEX IF NOT EXISTS idx_events_cfr ON events(class_code, cfr);
