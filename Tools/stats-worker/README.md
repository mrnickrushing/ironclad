# Ironclad play statistics

The game (`Assets/Scripts/Game/App/Telemetry.cs`) POSTs batches of anonymous events to this Worker, which checks them
and stores them in the D1 database `ironclad-stats`. Nothing can be read over HTTP; query with wrangler.

The table is shared in shape with Last Tide's, so the columns read: `step` = the line being run, `step_index` = the mile
of the run in progress, `level` = the best mile on any line, `location` = the line, `day` = lines open, `minutes` =
minutes played.

```bash
cd Tools/stats-worker
npx wrangler deploy
npx wrangler d1 execute ironclad-stats --remote --command "SELECT level / 10 * 10 AS best_mile, COUNT(DISTINCT user) FROM events WHERE test = 0 AND type = 'Run.ended' GROUP BY 1 ORDER BY 1"
```
