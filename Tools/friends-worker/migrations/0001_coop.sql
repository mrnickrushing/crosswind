-- The co-op week (POST /v1/coop/report): one row per player per week they reported, their passengers that week.
-- A player's rows older than two weeks go when they report again, and all of them go with their account.
-- The same table is in schema.sql for a fresh database; this file adds it to the one that already exists.
CREATE TABLE IF NOT EXISTS coop (
  player     INTEGER NOT NULL,          -- players.id
  week       INTEGER NOT NULL,          -- the game's week (Sim.Week: (days since 1970 + 3) / 7, rounded down)
  passengers INTEGER NOT NULL,          -- passengers handled that week, as the game counts them
  updated    INTEGER NOT NULL,          -- unix seconds
  PRIMARY KEY (player, week)
) WITHOUT ROWID;
