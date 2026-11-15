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

## Questions the 1.0.1 events answer

Every event carries `Player.cohort` (0-99, fixed per save), `Player.starter` (which way the Fireman's Pack test goes:
`later_day` or `first_boss`), `Player.pass` and `Player.adsWatched`.

```bash
Q() { npx wrangler d1 execute ironclad-stats --remote --command "$1"; }

# The Fireman's Pack test: offers shown and bought, by variant.
Q "SELECT json_extract(payload,'$.\"Player.starter\"') AS variant,
          COUNT(DISTINCT CASE WHEN type='Offer.viewed' AND json_extract(payload,'$.\"Offer.product\"')='ironclad.starter' THEN user END) AS shown,
          COUNT(DISTINCT CASE WHEN type='Purchase.completed' AND json_extract(payload,'$.\"Purchase.product\"')='ironclad.starter' THEN user END) AS bought
   FROM events WHERE test = 0 GROUP BY 1"

# Conductor's Pass economics: ads watched a day by players without it, against how many buy it.
Q "SELECT date(received) AS day, COUNT(*) AS ads, COUNT(DISTINCT user) AS watchers FROM events
   WHERE test = 0 AND type = 'Ad.watched' GROUP BY 1 ORDER BY 1 DESC LIMIT 30"
Q "SELECT COUNT(DISTINCT user) FROM events WHERE test = 0 AND type = 'Purchase.completed'
   AND json_extract(payload,'$.\"Purchase.product\"') = 'ironclad.pass'"
# Pass price x buyers against (ads a day x ad revenue per ad x days played) for the same players before they bought:
# keep the $4.99 price and the benefit unless the ads given up are worth clearly more than what the pass brings.

# Which cards are taken (and at what rarity) - build variety.
Q "SELECT json_extract(payload,'$.\"Card.id\"') AS card, COUNT(*) FROM events WHERE test = 0 AND type = 'Card.taken' GROUP BY 1 ORDER BY 2 DESC"

# Shop offers seen against bought, by product.
Q "SELECT json_extract(payload,'$.\"Offer.product\"') AS product, COUNT(*) FROM events WHERE test = 0 AND type = 'Offer.viewed' GROUP BY 1"

# The tutorial: how many reach each step.
Q "SELECT value AS step, COUNT(DISTINCT user) FROM events WHERE test = 0 AND type = 'Tutorial.step' GROUP BY 1 ORDER BY 1"

# Return rates: players seen again 1 and 7 days after their first day.
Q "WITH f AS (SELECT user, MIN(date(received)) AS d0 FROM events WHERE test = 0 GROUP BY user)
   SELECT COUNT(*) AS players,
          SUM(EXISTS(SELECT 1 FROM events e WHERE e.user = f.user AND date(e.received) = date(f.d0, '+1 day'))) AS day1,
          SUM(EXISTS(SELECT 1 FROM events e WHERE e.user = f.user AND date(e.received) = date(f.d0, '+7 day'))) AS day7
   FROM f"
```

Other events: `Formation.used`, `Debrief.opened` (with the tip shown), `Ad.offered` / `Ad.unfinished` /
`Ad.skippedWithPass`.
