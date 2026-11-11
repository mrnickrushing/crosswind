-- Crosswind play statistics: one row per event. No IP addresses, no names: `user` is a hash made on the device.
CREATE TABLE IF NOT EXISTS events (
  id        INTEGER PRIMARY KEY AUTOINCREMENT,
  received  TEXT NOT NULL,              -- server time, ISO 8601 UTC
  sent      TEXT,                       -- device time when the event was made
  user      TEXT NOT NULL,              -- SHA-256 of a random id made on the device
  session   TEXT,
  type      TEXT NOT NULL,              -- e.g. Guide.stepReached
  test      INTEGER NOT NULL DEFAULT 0, -- TestFlight / debug builds
  value     REAL,
  step      TEXT, step_index INTEGER, level INTEGER, location TEXT, day INTEGER, minutes INTEGER,
  version   TEXT, platform TEXT, model TEXT,
  payload   TEXT                        -- every key the game sent, as JSON
);
CREATE INDEX IF NOT EXISTS events_type ON events(type, received);
CREATE INDEX IF NOT EXISTS events_user ON events(user, received);
CREATE INDEX IF NOT EXISTS events_step ON events(step_index);
