# Ironclad: working notes for coding agents

An idle tower defence for iPhone and iPad (landscape), Unity **6000.0.85f1**, URP. Rushing Technologies. The tower is
an armoured train: it rolls across the frontier, each wagon is a weapon, raiders come in waves, and a run ends when the
engine's hull is gone. Steel earned on runs buys upgrades that are kept. Design: `docs/DESIGN.md`.
Original names and places only, nothing recreated from an existing game. Free models only (CC0 first; CC BY credited
in `CREDITS.md` and in the game). Ads are rewarded only, never shown unasked.

The project was started from Reel Empire (`~/reelempire`): the platform code (`Game/Platform`, `App/Store`,
`App/Telemetry`, `UI/Ui`, `UI/LoadingScreen`), the tools and the conventions are the same. The services are this game's own: App Store Connect app
"Ironclad: Rail Defence" (Apple ID 6834888824, bundle `com.rushingtech.ironclad`, team PH4AKDQ4Q7), RevenueCat project
`c5d6cc32`, AdMob app `~7307958921` with rewarded unit `/2358884575`, statistics worker `ironclad-stats`.

- `Assets/Scripts/Core` (no Unity dependency): `Content` (wagons, raiders, lines, workshop upgrades, cards, tuning),
  `GameState` (the save, including the run in progress), `Sim` (tick in fixed 0.05 s steps at the running speed),
  `Sim.Run` (miles, spawning, raiders striking, cars firing, shells, mending, stations and cards, whistle, mark,
  revive, closing a run), `Sim.Workshop` (steel, upgrades, building and coupling wagons, the multipliers a run is
  fought with, field levels), `Sim.Depot` (steel while away), `Sim.Daily` (gift, tasks), `Sim.Achievements`,
  `Sim.Ads` (rewarded only: revive, double the run, double the depot), `Sim.Store`, `Sim.Reminders`.
  Car 0 is the engine at x = 0; wagons trail at `Sim.CarX(i)`; z is across the track. Raiders strike the nearest car
  that isn't `Out`; a wagon is out from plating 0 until a quarter is mended.
- Tests (`Assets/Tests/EditMode`): `RunTests`, `GameCenterTests`, `PacingTests` (a greedy player's first hours: run
  `Tools/core-test.sh --logger "console;verbosity=detailed" --filter "Name~FirstHours"` after any tuning change).
  Build sessions with `TestGame.New()` (fixed clock).
- `Assets/Scripts/Game` (everything built from code at runtime; `GameRoot` boots in any scene): `World/TrainView`
  (the train stands at the origin and the country scrolls past: ground sheet, props and rails wrap every
  `Span * 2`; a station slides in when the run stops; `BuildCar` puts a weapon on each wagon, `BuildFoe` makes each
  raider; the simulation's events drive tracers, shells, bursts), `World/Fx` (two particle systems, pooled lines,
  shells, rings), `World/Models` (`Along`: a kit model laid along the track; `Tall`: stood at a height),
  `App/CameraRig` (`Punch`), `App/Sfx` (interface sounds are files; the fight's sounds are synthesised in `Synth`;
  `Tension` under the music at low hull), `App/SlowMo` (the whistle, the Baron's fall; GameRoot ticks it, unscaled),
  `App/Look` (`Danger`: the red vignette).
- Interface (`UI/Hud*.cs`): four faces picked by the run's phase (`Hud.Depot`, `Hud.Run` for the run, the station
  and the end), screens over the depot (`Hud.Screens`: workshop, train, today, awards, shop, settings), `Hud.Signs`
  (words rising from the fight, the toast). Parchment, oxblood and brass, painted by `Tools/art/ui_art.py`.
  Check new screens at 1800x830 (`Tools/unity-capture.sh`).
- Art: `python3 -I Tools/art/kenney.py <kits>` and `python3 -I Tools/art/props.py` fetch into `art-src/free`
  (ignored by git); `python3 -I Tools/art/models.py [id...]` converts to `Assets/Resources/Models/<id>.fbx`.
  Keep `CREDITS.md` and `Hud.Screens.CreditsText` in step with what ships.
- App Store: `docs/appstore/listing.json`, `gamecenter.json` (+ badges from `Tools/art/gc_badges.py`),
  `Tools/appstore/iap.py` (the same products as `Sim.Products`). Web pages are the `gh-pages` branch
  (https://mrnickrushing.github.io/ironclad/).

- Long-term layers: daily gift and three daily tasks from six (`Sim.Daily`), awards, gems for each station first
  reached on a line, and the Baron's seals (`Content.Perks`, `Sim.BuyPerk`: one seal a boss, seven lasting perks, the
  workshop's second section). A new engine (`Sim.Engine`: `Renew`, charters by `ChartersOnRenew`, `CharterMult`
  on damage, steel and the depot) and gem liveries (`Content.Skins`, `BuySkin`/`UseSkin`; paint only, never stats).
  The last card's `NearMiss` and `NextUnlock`; one free deal of cards per station (`FreeReroll`).
- Clarity (`Core/Sim.Clarity.cs`): `NeighbourGifts`/`TrainGifts` (what supply and armoured wagons give each place; the
  tags under the wagons) and `ShuntPreview` (what a move would change, shown under every place while a wagon is picked);
  ×1/×10/MAX (`UpgradeCostN`, `BuyUpgrades`, `CarCostN`, `BuyCars`); three saved formations (`SaveFormation`,
  `UseFormation`, also at a station); `Debrief` (each gun's share, first wagon out, the worst raiders, one tip; the run
  tracks `dealt`, `hurtBy`, `firstOut`); `PurchasePreview` (the shop's before/after card); `NextGoal` on the depot and
  the UNLOCKED card (`NewlyUnlocked`, `Celebrated`); the Fireman's Pack timing test (`StarterVariant` by `cohort`).
  Daily tasks are drawn from a bigger pool by a per-day shuffle (`TasksFor`, kept in `taskIds`). With AUTO on, `Hud.UpdateOver` collects a finished run after a countdown.
- Depth: four bosses take turns by station (`Sim.BossKind`, `Content.Bosses`): the Baron's Carriage; the Convoy (three
  `escort` war wagons, takes 75% less while they live); the Long Gun (marks a wagon, `Warning` event and a red filling
  ring, fires on it and its neighbours after `Windup`; the whistle spoils it, `Spoiled`); the Mending Wagon (heals
  raiders within 12 m). Each has a `Hint` shown while it rides. Halfway through each leg the line offers a choice
  (`Content.Choices`, `Sim.Choose`, declined when its 14 s run out): fitter, freight (more raiders, more steel), gulch
  (double pay, harder hits); `boostKind`/`boostMiles`. Tactic cards (`CardDef.Behaviour`, once a run, `Needs` a wagon,
  gentler rarity scale `TacticMult`): Split Shells, Arc Through Smoke, Steam Recharge, Iron Decoy (`RefreshPlating`),
  Ricochet, Glass Cannon. Cards show a glyph (`CardDef.Icon`, or painted art at `Resources/UI/Cards/<id>.png` if added),
  a rarity glow and the wagons they help (`Hud.CardFace`). Wagons look heavier at levels 10/25/50 (`CarTier`,
  `TrainView.Heavier`); guns kick, mortars dip, supply cars pulse, fitters spark; wagons scorch as plating runs down and
  say FIXED when patched; raiders stand on soft shadows with rings when striking; landmarks pass at miles 3 and 7 of
  each leg (`TrainView.BuildLandmark`, kept within 15 m of the line or the camera can't see them).
- Meta (`Core/Sim.Meta.cs`): themes paint the whole train (`SkinDef` engine tint, wagon tint `WR/WG/WB`, smoke
  `SR/SG/SB`, pennant and depot bunting `FR/FG/FB`; `From` names where an unsold one is earned); SEE IT tries one on at
  the depot (`TrainView.PreviewSkin`, `Hud.BuildPreviewBar`). Mastery (`Sim.Masteries`, `ClaimMastery`) gives titles
  (`State.titles`, worn one under the depot's sub line, `UseTitle`) and three themes. Builds that work (`Sim.Builds`,
  `UseBuild`, couple a tested order in one tap). The weekly trial (`Sim.Trials`, `Week`, opens at mile 20,
  `StartTrial`): a fixed train at `TrialDef.Level`, the same seed for everyone that week, no workshop, steel, revive,
  bests or plans; marks at miles 10/25/40 give two titles and the Proving Ground theme. The depot grows (`Sim.Depot`:
  sidings, parked old engines, trophy flags, workers). The expedition (`Sim.Steps`, 30 free steps a 28-day season,
  `AddExp` one a mile and five a boss; steel, gems, seals, a title and the Pathfinder theme) is on TODAY. Telemetry:
  Theme.previewed/bought, Mastery.claimed, Title.worn, Build.used, Trial.started/finished, Expedition.claimed.
- `Game/Debug/PlayBot.cs` (editor only) plays through the real HUD methods: `PlayBot.Begin(10)`, `PlayBot.Report()`.
- `Tools/ios-export.sh` switches the Editor to the iOS platform and recompiles before building, and refuses an export
  without the AdMob id or the entitlements. Music is `Tools/art/music.py` ("day" while rolling, "night" at rest).

## Verify every change

```bash
Tools/core-test.sh                           # Core tests without Unity
unity pipeline install --project-path .     # once per clone (local dev tool, never commit it)
unity open . --until-ready
Tools/unity-recompile.sh
Tools/unity-capture.sh out.png 8
```

Keep `com.unity.pipeline` out of the committed `Packages/manifest.json`; don't commit `Packages/packages-lock.json`.
Only one Unity Editor open at a time on this machine.
