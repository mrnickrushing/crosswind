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
