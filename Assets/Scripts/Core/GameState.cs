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
    }

    [Serializable]
    public sealed class EnemyState
    {
        public int id;
        public string kind;
        public double x, z, hp, hpMax, damage, cooldown, stun;
        public double facing;               // radians, for the picture
        public bool striking;               // close enough to hit the train: the guns deal with these first
    }

    [Serializable]
    public sealed class Shell { public int car; public double x, z, t, damage, splash; public bool crit; }

    [Serializable]
    public sealed class Pick { public string card; public int rarity; }

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
        public long taskDay = -1; public int taskClaimed; public List<double> taskBase = new List<double>();

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
                run.cars.RemoveAll(c => c == null || Content.Car(c.type) == null);
                if (run.cars.Count == 0 || run.cars[0].type != "engine") run = null;
            }
        }
    }
}
