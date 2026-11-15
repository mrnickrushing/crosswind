# Crosswind play statistics

The game (`Assets/Scripts/Game/App/Telemetry.cs`) POSTs batches of anonymous events to this Worker, which checks them
and stores them in the D1 database `crosswind-stats`. Nothing can be read over HTTP; query with wrangler.

The table is shared in shape with Last Tide's, so the columns read: `step` = the guide goal id, `step_index` = its
number, `level` = stars, `location` = the airport (map id), `day` = airports sold so far, `minutes` = minutes played.

```bash
cd Tools/stats-worker
npx wrangler deploy
npx wrangler d1 execute crosswind-stats --remote --command "SELECT step_index, step, COUNT(DISTINCT user) FROM events WHERE test = 0 AND type = 'Goal.reached' GROUP BY step_index ORDER BY step_index"
```

Retention and the newer events (2026-11-15):

```bash
# Day-1 and day-7 return: players whose first sitting was N days ago, and how many came back on day 1 and day 7.
npx wrangler d1 execute crosswind-stats --remote --command "WITH first AS (SELECT user, MIN(date(received)) d0 FROM events WHERE test = 0 GROUP BY user), seen AS (SELECT DISTINCT user, date(received) d FROM events WHERE test = 0 AND type = 'Session.started') SELECT f.d0, COUNT(*) players, SUM(EXISTS(SELECT 1 FROM seen s WHERE s.user = f.user AND s.d = date(f.d0, '+1 day'))) d1, SUM(EXISTS(SELECT 1 FROM seen s WHERE s.user = f.user AND s.d = date(f.d0, '+7 day'))) d7 FROM first f GROUP BY f.d0 ORDER BY f.d0"
# Where the tutorial loses people.
npx wrangler d1 execute crosswind-stats --remote --command "SELECT json_extract(payload, '$.\"Tutorial.step\"') step, COUNT(DISTINCT user) FROM events WHERE test = 0 AND type IN ('Tutorial.step', 'Tutorial.finished') GROUP BY step ORDER BY CAST(step AS INT)"
# How many players find each feature.
npx wrangler d1 execute crosswind-stats --remote --command "SELECT json_extract(payload, '$.\"Feature.name\"') f, COUNT(DISTINCT user) FROM events WHERE test = 0 AND type = 'Feature.used' GROUP BY f ORDER BY 2 DESC"
```

Event types: `Session.started|paused`, `Tutorial.step|finished`, `Goal.reached`, `Stars.gained`, `Automation.first|hired`,
`Contract.finished|dropped`, `Day.closed`, `Layout.edits` (counts per sitting), `Feature.used` (once per sitting each),
`Offer.viewed`, `Ad.completed|unfinished`, `Airport.sold`, `Performance.sample`. Purchases are never sent.
