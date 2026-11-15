using System;
using System.Collections.Generic;

namespace Ironclad.Core
{
    [Serializable] public sealed class Level { public string id; public int level; }

    /// <summary>A car of the train on a run. Index 0 is the engine.</summary>
    [Serializable]
    public sealed class CarState
    {
        public string type;
        public double plating, platingMax;  // the engine has none: blows on it go to the hull
        public int field;                   // levels bought with scrip on this run
        public double cooldown;
        public double downFor;              // seconds it has been out (Spare Plating brings it back)
        public bool down;                   // plating gone: it stops firing and raiders pass it by until a quarter is back
        public bool Out { get { return down; } }
        public double dealt;                // damage this car has done on the run (the end-of-run breakdown)
        public int outs;                    // times it has been knocked out on the run
    }

    [Serializable]
    public sealed class EnemyState
    {
        public int id;
        public string kind;
        public double x, z, hp, hpMax, damage, cooldown, stun;
        public double facing;               // radians, for the picture
        public bool striking;               // close enough to hit the train: the guns deal with these first
        public bool escort;                 // rides with the Baron's convoy
        public double windup;               // the Long Gun is aiming (seconds left)
        public int aimCar;                  // at this car
        public double burn;                 // the Furnace Car set it alight (seconds left)
    }

    [Serializable]
    public sealed class Shell { public int car; public double x, z, t, damage, splash; public bool crit; }

    [Serializable]
    public sealed class Pick { public string card; public int rarity; }

    /// <summary>A count kept by name (damage by raider kind, kills by wagon).</summary>
    [Serializable]
    public sealed class Tally { public string id; public double v; }

    /// <summary>A train order the player saved, to couple up again in one tap.</summary>
    [Serializable]
    public sealed class Formation { public List<string> cars = new List<string>(); }

    public enum Phase { Rolling, Station, Over }

    /// <summary>A run in progress. It is part of the save, so closing the game mid-run loses nothing.</summary>
    [Serializable]
    public sealed class RunState
    {
        public int route;
        public int mile = 1;                // the mile being rolled
        public double mileTime;             // seconds into it
        public int phase;                   // Phase
        public double hull, hullMax;
        public double scrip, steel;         // steel earned so far (paid out when the run ends)
        public long kills;
        public List<CarState> cars = new List<CarState>();
        public List<EnemyState> enemies = new List<EnemyState>();
        public List<Shell> shells = new List<Shell>();
        public List<string> queue = new List<string>();     // this mile's raiders still to arrive
        public double spawnIn, spawnGap;
        public bool bossOut;                // this mile's boss has ridden in
        public int nextId = 1;
        public int mark;                    // the raider the player pointed out (0: none)
        public double whistleIn;
        public List<Pick> cards = new List<Pick>();         // taken
        public List<Pick> offer = new List<Pick>();         // on the table at a station
        public bool revived;
        public bool doubled;                // the run's steel was doubled after it ended
        public int seals;                   // seals won on this run (already in the save's count)
        public double time;                 // seconds rolled
        public int startBest;               // the best mile on this line when the run set off (the BEST post stands there)
        public bool rerolled;               // this station's free deal of other cards has been used
        public bool retired;                // the player ended it (no "so close" on the last card)
        public int left;                    // raiders still riding or still to come when the hull went
        public double bossLeft = -1;        // the Baron's carriage's share of health left then (-1: not on the field)
        // For the breakdown at the end: who hurt the train, and the first wagon knocked out.
        public List<Tally> hurtBy = new List<Tally>();
        public double hurtClose;            // of the damage taken, what came from raiders striking from close in
        public int firstOut = -1; public string firstOutType = ""; public int firstOutMile;
        public bool legOut;                 // a wagon was knocked out since the last station
        public string bossKind = "";        // the boss of the station leg being rolled
        // A choice the line offers between stations (see Content.Choices).
        public string choice = ""; public double choiceIn;
        public string boostKind = ""; public int boostMiles;
        public bool trial;                  // the week's trial: its own train, no steel, no workshop      // what a choice taken does to the next miles
        public double surge;                // seconds of Steam Recharge's faster fire left
    }

    /// <summary>The save: plain fields and lists so JsonUtility can write it. Every class it holds is [Serializable].</summary>
    [Serializable]
    public sealed class GameState
    {
        public int version = 1;
        public uint seed = 2463534242;
        public double savedAt;              // real time of the last save, seconds
        public double played;               // seconds in the game
        public string playerName = "";
        public int gems;
        public double steel, lifetime;      // in hand, and ever earned
        public int route;                   // the line chosen for the next run
        public int speed;                   // index into Content.Speeds
        public bool auto;                   // set off again by itself when a run ends
        public bool express;                // set off from part-way down the line (Sim.ExpressMile)
        public List<string> train = new List<string>();      // the wagons coupled on, front to back (the engine isn't listed)
        public List<Level> cars = new List<Level>();         // wagons built, and their workshop levels
        public List<Level> upgrades = new List<Level>();
        public int seals;                    // from the Baron's carriages
        public List<Level> perks = new List<Level>();        // what seals have bought
        public List<int> best = new List<int>();             // best mile on each line
        public RunState run;                // null between runs
        public long runs, kills, miles, whistles, cardsTaken, bosses;
        public double depotAt;              // real time the depot was last emptied
        public long doubleDay = -1;
        public long adDay = -1; public int adCount;
        public int tutorialStep;
        public int goalIndex;
        public List<string> achieved = new List<string>();
        public List<string> seenFeatures = new List<string>();
        public long firstDay = -1;
        public long giftDay = -1; public int giftStreak;
        public bool starterOffered;         // the Fireman's Pack has been shown once
        // A new engine: the workshop starts again and every charter earned makes steel and damage stronger for good.
        public int charters, engines;
        public double lifetimeAtRenew;      // lifetime steel when this engine was built
        // Liveries for the engine (paint and model only).
        public string skin = "";
        public List<string> skins = new List<string>();
        public long taskDay = -1; public int taskClaimed; public List<double> taskBase = new List<double>();
        public List<string> taskIds = new List<string>();     // the day's three tasks
        // Counts some of the day's tasks are made of.
        public long crits, fieldBuys, stationsClean, stationsLight, choicesTaken;
        public List<Tally> carKills = new List<Tally>();       // raiders turned back by each kind of wagon
        public int cohort = -1;             // 0..99, fixed per save: which way a test of the shop's timing goes
        public double lastRunSteel;         // what the last run paid (for the Rolling Mill's preview)
        public long adsWatched;
        public List<Formation> formations = new List<Formation>();
        public List<string> celebrated = new List<string>();   // unlocks already shown on the UNLOCKED card
        public bool celebrateInit;
        // Titles and long goals, the weekly trial, the expedition track.
        public List<string> titles = new List<string>(); public string title = "";
        public List<string> mastered = new List<string>();
        public long trialWeek = -1; public int trialBest, trialBestEver, trialRuns;
        public long expSeason = -1; public double expPoints; public List<int> expClaimed = new List<int>();

        // The shop.
        public List<string> owned = new List<string>();
        public List<string> transactions = new List<string>();
        public List<PendingPurchase> pending = new List<PendingPurchase>();

        public static GameState New()
        {
            var s = new GameState();
            s.Repair();
            return s;
        }

        /// <summary>Makes a new, an older or a damaged save playable.</summary>
        public void Repair()
        {
            if (train == null) train = new List<string>();
            if (cars == null) cars = new List<Level>();
            if (upgrades == null) upgrades = new List<Level>();
            if (perks == null) perks = new List<Level>();
            perks.RemoveAll(u => u == null || Content.Perk(u.id) == null);
            if (best == null) best = new List<int>();
            if (owned == null) owned = new List<string>();
            if (transactions == null) transactions = new List<string>();
            if (pending == null) pending = new List<PendingPurchase>();
            if (achieved == null) achieved = new List<string>();
            if (seenFeatures == null) seenFeatures = new List<string>();
            if (taskBase == null) taskBase = new List<double>();
            if (skins == null) skins = new List<string>();
            if (taskIds == null) taskIds = new List<string>();
            if (carKills == null) carKills = new List<Tally>();
            if (formations == null) formations = new List<Formation>();
            while (formations.Count < 3) formations.Add(new Formation());
            foreach (var f in formations) if (f.cars == null) f.cars = new List<string>();
            if (celebrated == null) celebrated = new List<string>();
            if (titles == null) titles = new List<string>();
            if (title == null || (title.Length > 0 && !titles.Contains(title))) title = "";
            if (mastered == null) mastered = new List<string>();
            if (expClaimed == null) expClaimed = new List<int>();
            if (cohort < 0 || cohort > 99) cohort = (int)((uint)Guid.NewGuid().GetHashCode() % 100);
            if (skin == null || (skin.Length > 0 && Content.Skin(skin) == null)) skin = "";
            if (lifetimeAtRenew > lifetime) lifetimeAtRenew = lifetime;
            if (playerName == null) playerName = "";
            if (seed == 0) seed = 2463534242;
            cars.RemoveAll(c => c == null || Content.Car(c.id) == null);
            upgrades.RemoveAll(u => u == null || Content.Upgrade(u.id) == null);
            if (!cars.Exists(c => c.id == "engine")) cars.Add(new Level { id = "engine", level = 1 });
            if (!cars.Exists(c => c.id == "gatling")) cars.Add(new Level { id = "gatling", level = 1 });
            foreach (var c in cars) c.level = Math.Max(1, c.level);
            train.RemoveAll(id => id == "engine" || !cars.Exists(c => c.id == id));
            if (train.Count == 0) train.Add("gatling");
            while (best.Count < Content.Routes.Count) best.Add(0);
            route = Math.Max(0, Math.Min(route, Content.Routes.Count - 1));
            speed = Math.Max(0, Math.Min(speed, Content.Speeds.Length - 1));
            // JsonUtility writes a null run as an empty one: a run with no engine was never started.
            if (run != null && (run.cars == null || run.cars.Count == 0)) run = null;
            if (run != null)
            {
                if (run.enemies == null) run.enemies = new List<EnemyState>();
                if (run.shells == null) run.shells = new List<Shell>();
                if (run.queue == null) run.queue = new List<string>();
                if (run.cards == null) run.cards = new List<Pick>();
                if (run.offer == null) run.offer = new List<Pick>();
                if (run.hurtBy == null) run.hurtBy = new List<Tally>();
                if (run.firstOutType == null) run.firstOutType = "";
                if (run.bossKind == null) run.bossKind = "";
                if (run.choice == null) run.choice = "";
                if (run.boostKind == null) run.boostKind = "";
                run.cars.RemoveAll(c => c == null || Content.Car(c.type) == null);
                if (run.cars.Count == 0 || run.cars[0].type != "engine") run = null;
            }
        }
    }
}
