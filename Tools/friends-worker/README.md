# Crosswind friends

Deployed at **https://crosswind-friends.mrnickrushing.workers.dev** (D1 database `crosswind-friends`,
id `f2d6519e-4653-4ad3-9278-fadb9171d55d`).

The game (`Assets/Scripts/Game/Platform/FriendsClient.cs`, rules in `Assets/Scripts/Core/Friends.cs`) signs a player in
with the opaque account id from Sign in with Apple, gets a 6-character friend code, adds friends by code and lists
their airports. No chat and no free text except the display name.

What is stored (`schema.sql`): per player a SHA-256 of the account id, a SHA-256 of the bearer token, the friend code,
display name, map id, stars, flights handled, created and last-seen times; per friendship two rows (one each way);
per player and week of the co-op goal, the passengers they handled (the last three weeks at most).
No IPs, emails or raw account ids are stored or logged. The per-IP rate limit (register: 10 per 10 minutes, friends/add:
30 per 10 minutes, coop/report: 30 per 10 minutes) lives in the isolate's memory, keyed by a salted hash that dies with the isolate, so it is a brake
on bursts rather than an exact count.

## Endpoints

JSON in, JSON out. Errors are `{"ok":false,"error":"..."}` with a 4xx status; the `error` text is shown to the player.
`Authorization: Bearer <token>` on everything except register.

| | body | answer |
|---|---|---|
| `POST /v1/register` | `{account, name, map, stars, flights}` | `{code, token}`; an account that exists keeps its code and gets a new token (the old one stops working) |
| `POST /v1/profile` | `{name, map, stars, flights}` | `{ok}`; also sets last seen |
| `POST /v1/friends/add` | `{code}` | `{ok, friend:{code,name,map,stars,flights,lastSeen}}`, or "No airport has that code" (404), "That's your own code", "Already friends", "Friend list is full" (50), "Their friend list is full" (409) |
| `POST /v1/friends/remove` | `{code}` | `{ok}` (also when they weren't friends) |
| `GET /v1/friends` | | `{friends:[{code,name,map,stars,flights,lastSeen}]}`, most stars first; also sets last seen |
| `POST /v1/coop/report` | `{week, passengers}` | `{ok, week, mine, total, members, tiers:[4 numbers]}`: stores this week's count (never lowered within a week) and answers with the total over the caller and their mutual friends for that week; `members` is the caller plus the friends with passengers that week (at most 51), `tiers` the goal for that many (as `Sim.CoopTargets`). 422 "Check the date on your device" when `week` is not a whole number within one of the server's |
| `POST /v1/delete` | | `{ok}`; removes the player, their co-op counts and their friendships in both directions |
| `GET /v1/health` | | `ok` |

Other answers: 401 "Sign in again" (no token, or one that was replaced or deleted), 422 "Bad account" (8-256
characters), 429 "Too many tries, wait a few minutes", 400 "Bad JSON", 413 over 4 KB.

Names keep letters, digits, spaces and `'-._` (20 characters, default "Airport Manager"); `FriendRules.CleanName` does
the same on the device, so change both together. Codes use `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` (no 0/O/1/I). Stars are
clamped to 0-99, flights and passengers to 0-2,000,000,000, `lastSeen` is unix seconds. The co-op week is the game's
`Sim.Week`, `floor((floor(unix / 86400) + 3) / 7)` (Mondays, UTC); the tiers are `[2500, 10000, 30000, 80000] x
(5 + 4 x (members - 1)) / 5` in both `worker.js` and `Core/Sim.Coop.cs`, so change both together.

## Test

`node --test` (Node 22.5 or newer, nothing to install) runs `test/*.test.mjs` against an in-memory SQLite database
made from `schema.sql` and the migrations; it never touches Cloudflare.

## Deploy and query

```bash
cd Tools/friends-worker
npx wrangler d1 execute crosswind-friends --remote --file schema.sql   # once, or after adding a table (IF NOT EXISTS)
npx wrangler d1 execute crosswind-friends --remote --file migrations/0001_coop.sql   # the co-op table, on the existing database
npx wrangler deploy
npx wrangler d1 execute crosswind-friends --remote --command "SELECT COUNT(*) AS players, (SELECT COUNT(*) FROM friends) / 2 AS friendships FROM players"
```

Try it:

```bash
U=https://crosswind-friends.mrnickrushing.workers.dev
curl -s -X POST $U/v1/register -d '{"account":"test-throwaway-1234","name":"Test","map":"meadow","stars":1,"flights":0}'
curl -s $U/v1/friends -H "Authorization: Bearer <token>"
curl -s -X POST $U/v1/coop/report -H "Authorization: Bearer <token>" -d "{\"week\":$(( ($(date +%s) / 86400 + 3) / 7 )),\"passengers\":1200}"
curl -s -X POST $U/v1/delete -H "Authorization: Bearer <token>"     # always delete test accounts afterwards
```
