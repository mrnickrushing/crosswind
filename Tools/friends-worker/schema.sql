-- Crosswind friends: one row per signed-in player and one row per direction of a friendship. No IP addresses, no
-- emails, no raw account ids: `account` and `token` are SHA-256 hashes. The only free text is the display name.
CREATE TABLE IF NOT EXISTS players (
  id        INTEGER PRIMARY KEY AUTOINCREMENT,
  account   TEXT NOT NULL UNIQUE,       -- SHA-256 of the opaque account id the game sent
  token     TEXT NOT NULL UNIQUE,       -- SHA-256 of the bearer token (rotated at every register)
  code      TEXT NOT NULL UNIQUE,       -- 6-character friend code
  name      TEXT NOT NULL,              -- display name, cleaned by the worker
  map       TEXT NOT NULL DEFAULT '',   -- the airport (map id)
  stars     INTEGER NOT NULL DEFAULT 0,
  flights   INTEGER NOT NULL DEFAULT 0, -- flights handled
  created   INTEGER NOT NULL,           -- unix seconds
  last_seen INTEGER NOT NULL            -- unix seconds
);

CREATE TABLE IF NOT EXISTS friends (
  player  INTEGER NOT NULL,             -- players.id
  friend  INTEGER NOT NULL,             -- players.id; a friendship is two rows, one each way
  added   INTEGER NOT NULL,             -- unix seconds
  PRIMARY KEY (player, friend)
) WITHOUT ROWID;
CREATE INDEX IF NOT EXISTS friends_friend ON friends(friend);
