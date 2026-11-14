# Ironclad: working notes for coding agents

An idle tower defence for iPhone and iPad (landscape), Unity **6000.0.85f1**, URP. Rushing Technologies. The tower is
an armoured train: it rolls across the frontier, each wagon is a weapon, raiders come in waves, and a run ends when the
engine's hull is gone. Steel earned on runs buys upgrades that are kept. Design: `docs/DESIGN.md`.
Original names and places only, nothing recreated from an existing game. Free models only (CC0 first; CC BY credited
in `CREDITS.md` and in the game). Adverts are rewarded only, never shown unasked.

The project was started from Reel Empire (`~/reelempire`): the platform code (`Game/Platform`, `App/Store`,
`App/Telemetry`, `UI/Ui`, `UI/LoadingScreen`), the tools and the conventions are the same. Service ids that belong to
this game don't exist yet and are marked `REPLACE` (RevenueCat key, AdMob app and rewarded unit, App Store id).

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
