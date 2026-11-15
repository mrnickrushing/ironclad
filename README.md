# Ironclad: Rail Defence

An idle tower defence game for iPhone and iPad, played in landscape. The tower is an armoured train crossing the
frontier. Each wagon is a weapon, raiders ride in waves, and a run ends when the engine's hull is gone. Steel earned
on a run buys workshop upgrades that are kept, so each run gets further.

- **Made by:** Rushing Technologies.
- **Built with:** Unity 6000.0.85f1 and the Universal Render Pipeline (URP 17).
- **App Store:** "Ironclad: Rail Defence", bundle `com.rushingtech.ironclad`.
- **Who plays it:** mobile players who like idle and incremental games with short sessions. The game also earns while
  it is closed, and the player comes back to collect.

This is not a web app: there are no routes, no web server and no SQL database behind the game. The sections below
describe the game's equivalents (screens, the save file, the statistics worker). Full design: `docs/DESIGN.md`.
Notes for coding agents: `CLAUDE.md`.

## Roles

| Role | What it can do |
|---|---|
| **Player (guest)** | Plays without an account: runs, the workshop, the train, daily gift and tasks, awards, the shop, opt-in rewarded adverts and Game Center. Restores one-time purchases through their Apple account. Can delete their progress (Settings, Delete account). The save is kept on the device and in the player's own iCloud. |
| **Player signed in with Apple** | All of the above. Purchases are also tied to the Apple ID in RevenueCat, so they follow the player to a new phone. Delete account also signs them out. |
| **Operator (the studio)** | No in-game admin. Works through `Tools/`: App Store listing, purchases and Game Center (`Tools/appstore/*.py`, using an App Store Connect API key), the iOS export (`Tools/ios-export.sh`), and reading play statistics from the Cloudflare D1 database (`Tools/stats-worker`). |

## Screens

The interface is built from code at runtime (`Assets/Scripts/Game/UI`). There are no URLs. The run's phase picks one
of four **faces** (`Hud.cs`, `enum Face`); **screens** open over the depot (`Hud.Screens.cs`, `enum Page`).

| Screen | Where | Purpose |
|---|---|---|
| Loading | `UI/LoadingScreen.cs` | Key art and the Rushing Technologies logo while the world is built. |
| Depot (home) | `Face.Depot`, `Hud.Depot.cs` | Between runs. Pick the line, the speed, AUTO and Express (start part-way down the line), then set off. Shows the depot's steel earned while away (one free double a day, then an advert), the TODAY badge and the bottom bar: WORKSHOP, TRAIN, AWARDS, SHOP, MORE. |
| Run | `Face.Run`, `Hud.Run.cs` | The fight. Mile and hull bars; buy field levels with scrip under each car; the whistle; tap a raider to mark it; speed control. |
| Station | `Face.Station` | Every tenth mile after the boss: choose one of three cards (Common/Rare/Epic; one free new deal, then gems) and recouple the wagons. |
| Run over | `Face.Over` | Results: miles, raiders, steel, how close the run came and the next unlock. Revive once (an advert or gems) and double the steel (an advert). With AUTO on it collects after a countdown. |
| Workshop | `Page.Workshop` | Steel upgrades kept for good. Below them, the seven perks bought with the Baron's seals, then A NEW ENGINE (start again for charters). |
| Train | `Page.Train` | Build wagons from plans, level them, and choose their order (up to eight behind the engine). Liveries for the engine (gems). |
| Today | `Page.Today` | Seven-day gift (a missed day loses nothing) and three daily tasks. |
| Awards | `Page.Awards` | Achievements that pay gems, and Game Center. |
| Shop | `Page.Shop` | Gem packs and one-time purchases, steel bought with gems, restore purchases. |
| Settings ("MORE") | `Page.Settings` | Sound, music, battery saver, vibration, reminders, anonymous statistics, Sign in with Apple, the conductor's name, Rate, privacy policy, help and support, Credits, Delete account. |
| Cards | `Hud.Card(...)` | Welcome ("ALL ABOARD"), The Depot, Fireman's Pack offer, Conductor (the name), rate prompt (`Hud.Rate.cs`), Credits, delete confirmation. |
| Signs | `Hud.Signs.cs` | Words that rise from the fight, and toasts. |

Web pages (gh-pages branch, https://mrnickrushing.github.io/ironclad/): home, support and privacy policy.

## Data model

### The save (`Assets/Scripts/Core/GameState.cs`)

The save is written as JSON by `JsonUtility` to `ironclad_save.json` in `Application.persistentDataPath`. It is copied
to the player's iCloud key-value store under `ironclad.save` and `ironclad.meta` (`Platform/CloudSave.cs`; the rules
for conflicts are in `Core/CloudRules.cs`).

| Record | Main fields |
|---|---|
| `GameState` | `version`, `seed`, `savedAt`, `played`, `playerName`, `gems`, `steel`, `lifetime`, `route`, `speed`, `auto`, `express`, `train` (wagon ids, front to back), `cars` (built wagons and their levels), `upgrades`, `seals`, `perks`, `best` (best mile per line), `charters`/`engines`/`lifetimeAtRenew` (new engines), `skin`/`skins` (liveries), `run`, the totals (`runs`, `kills`, `miles`, `whistles`, `cardsTaken`, `bosses`), `depotAt`, `doubleDay`, `adDay`/`adCount`, `tutorialStep`, `goalIndex`, `achieved`, `seenFeatures`, `firstDay`, `giftDay`/`giftStreak`, `starterOffered`, `taskDay`/`taskClaimed`/`taskBase`, `owned`, `transactions`, `pending` |
| `RunState` (the run in progress, saved too) | `route`, `mile`, `mileTime`, `phase` (Rolling/Station/Over), `hull`/`hullMax`, `scrip`, `steel`, `kills`, `cars`, `enemies`, `shells`, `queue`, `spawnIn`/`spawnGap`, `bossOut`, `mark`, `whistleIn`, `cards`, `offer`, `revived`, `doubled`, `seals`, `time`, `startBest`, `rerolled`, `retired`, `left`/`bossLeft` (the near miss) |
| `CarState` | `type`, `plating`/`platingMax`, `field`, `cooldown`, `down`, `downFor` |
| `EnemyState` | `id`, `kind`, `x`, `z`, `hp`/`hpMax`, `damage`, `cooldown`, `stun`, `facing`, `striking` |
| `Shell`, `Pick`, `Level` | a shell in flight; a card and its rarity; an id with a level |
| `PendingPurchase` (`Sim.Store.cs`) | a paid product waiting to be granted |

Fixed content (`Core/Content.cs`) has no table either: 10 cars (engine, Gatling, Cannon, Supply, Sharpshooter, Armoured,
Furnace, Mortar, Fitters', Dynamo), 7 raiders including the Baron's Carriage boss, 5 lines (Prairie, Badlands, High
Pass, Salt Flats, Black Canyon), 13 workshop upgrades, 7 perks, and the cards.

The device also keeps small preferences in `PlayerPrefs`: sound, music, battery saver, the rate prompt, the telemetry
id and opt-out, the Apple user id, and the cloud sync markers.

### Play statistics (`Tools/stats-worker`, Cloudflare D1 database `ironclad-stats`)

One table, `events` (`schema.sql`): `id`, `received`, `sent`, `user` (SHA-256 of a random id made on the device),
`session`, `type`, `test`, `value`, `step`, `step_index`, `level`, `location`, `day`, `minutes`, `version`, `platform`,
`model`, `payload` (JSON). It has no IP addresses and no names, and rows older than a year are deleted daily.

Event types sent: `Session.started`, `Session.paused`, `Run.ended`, `Progress.station`, `Line.opened`, `Wagon.built`,
`Performance.sample`. Telemetry is off in the Editor and when the player turns it off in Settings.

## Third-party services

| Purpose | Service | Where |
|---|---|---|
| Sign-in | Sign in with Apple (`com.lupidan.apple-signin-unity`) | `Game/Platform/AppleAccount.cs` |
| Save sync | iCloud key-value store (native plugin, no server) | `Game/Platform/CloudSave.cs`, `Plugins/iOS/IroncladCloud.mm` |
| Payments | App Store in-app purchases through RevenueCat (`com.revenuecat.purchases-unity`) | `Game/App/Store.cs`, products in `Core/Sim.Store.cs`, `Tools/appstore/iap.py` |
| Adverts | Google AdMob, rewarded only and non-personalised (`com.google.ads.mobile`) | `Game/Platform/Ads.cs`, `Assets/GoogleMobileAds/Resources` |
| Leaderboards and achievements | Apple Game Center (4 leaderboards, one weekly; 17 achievements) | `Game/Platform/GameCenter.cs`, `docs/appstore/gamecenter.json` |
| Reminders | Local notifications (`com.unity.mobile.notifications`) | `Game/Platform/Reminders.cs` |
| Statistics | Cloudflare Worker + D1 | `Game/App/Telemetry.cs`, `Tools/stats-worker` |
| Builds | Codemagic (signing and TestFlight upload), GitHub release `ios-latest` | `Tools/ios-export.sh` |
| Email | none | |
| AI | none in the game. Art and music are made offline by the scripts in `Tools/art` from free CC0/CC BY models (see `CREDITS.md`). | |

Purchases: `ironclad.gems.small|medium|large|huge`, `ironclad.starter` (Fireman's Pack), `ironclad.doubler` (Rolling
Mill), `ironclad.pass` (Conductor's Pass), `ironclad.depot` (Night Shift).

## Design system

The look is parchment, oxblood and brass: painted panels and ribbons from `Tools/art/ui_art.py`
(`Assets/Resources/UI`), and the widgets in `Game/UI/Ui.cs`.

- **Fonts:** Lilita One (`Ui.Bold`: titles, buttons, numbers) and Fredoka SemiBold (`Ui.Body`: body text), in
  `Assets/Resources/Fonts`. Both use the SIL Open Font License.
- **Palette (`Ui.cs`):**

  | Name | Colour | Used for |
  |---|---|---|
  | Ink | `#2E1B12` | text |
  | Paper / Panel | `#F6E7C6` | panels |
  | Card | `#F0D9AD` | cards |
  | Gold | `#F2AD29` | rewards, Awards |
  | Green | `#5CAD47` | go and buy, Shop |
  | Blue | `#3385A8` | Train, toggles |
  | Coral / Red | `#CC4233` | ribbons, close, Workshop, danger |
  | Grape | `#8C5CB8` | More / Settings |
  | Off | `#B2A899` | disabled |
  | Dim | Ink at 78% | secondary text |

  Scrims are `#240A0A` at 62%.
- **Canvas:** reference size 1920 x 940 with `Expand` matching (`Ui.Canvas`).
  - Screens wider than 16:9 (notched iPhones) draw everything about 15% larger, so the smallest labels clear 11 points.
  - 16:9 screens and iPads are limited by width.
  - `SafeArea` keeps the interface out of the notch.
- **Widths:**
  - There is no sidebar.
  - Over-depot screens are a centred panel 1520 units wide, running from 24 units above the bottom to 70 units below
    the top.
  - Cards are 880-980 wide (Credits is 1500 x 880).
  - The depot's bottom bar has five 284 x 112 buttons, 300 apart.
- **Breakpoints:** aspect ratio rather than width.
  - Wider than 16:9 (phones).
  - 16:9.
  - 4:3 (iPad).
  - Check new screens at 1800x830 and 1440x1080 (`Tools/unity-capture.sh`).
- **3D view:**
  - Renders at most about 2000 px across and drops to 0.6 render scale while frames run long (`App/FrameBudget.cs`).
  - Battery saver runs at 30 fps.

## Layout

```
Assets/Scripts/Core       rules without Unity: Content, GameState, Sim.* (run, workshop, depot, daily, ads, store)
Assets/Scripts/Game       App/ (GameRoot, camera, save, sound, store, telemetry), Platform/, UI/, World/, Debug/PlayBot
Assets/Tests/EditMode     Unity tests (RunTests, GameCenterTests, PacingTests, EngineTests)
Assets/Resources          models, UI art, fonts, audio, shaders, materials
Assets/Plugins/iOS        native iCloud, environment and haptics plugins
Assets/Editor             import rules, iOS capabilities, build steps
Packages, ProjectSettings Unity project
Tools/                    test and build scripts, CoreTests, appstore/, art/, blender/, stats-worker/
docs/                     DESIGN.md, appstore/ (listing, Game Center, screenshots)
```

## Running it

```bash
Tools/core-test.sh            # rules tests without Unity (.NET 8 SDK)
unity open . --until-ready    # or open the folder in Unity Hub with 6000.0.85f1
Tools/unity-recompile.sh
Tools/unity-test.sh
```

Environment variable names used by the tools are listed in `.env.example`; the game itself reads none.
