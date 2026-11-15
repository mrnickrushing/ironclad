using System;
using System.Collections.Generic;

namespace Ironclad.Core
{
    /// <summary>A long goal on the line: done once, it gives a title and sometimes a paint for the whole train.</summary>
    public sealed class MasteryDef
    {
        public string Id, Name, Blurb, Title, Skin;
        public double Goal;
        public Func<Sim, double> Have;
    }

    /// <summary>A train that works, with why: shown on the TRAIN screen and coupled up in one tap.</summary>
    public sealed class BuildDef
    {
        public string Name, Why;
        public string[] Cars;
    }

    /// <summary>The week's trial: the same train, levels and opening cards for everyone, nothing from the workshop.</summary>
    public sealed class TrialDef
    {
        public string Name, Blurb;
        public string[] Cars, Cards;
        public int Level;
    }

    /// <summary>A step on the expedition track: points to reach it and what it gives ("steel", "gems", "seal",
    /// "title:Name", "skin:id").</summary>
    public sealed class StepDef { public int Tier; public double Need; public string Gift; public int Amount; }

    /// <summary>How the depot has grown: tracks laid, old engines standing in the sidings, trophies, people at work.</summary>
    public struct DepotLook { public int Sidings, Parked, Trophies, Workers; }

    public sealed partial class Sim
    {
        // ---- mastery ----

        public static readonly List<MasteryDef> Masteries = new List<MasteryDef>
        {
            new MasteryDef { Id = "gatling", Name = "Gatling Master", Blurb = "Turn back 5,000 raiders with Gatling Cars", Goal = 5000, Have = s => s.CarKills("gatling"), Title = "Gatling Master" },
            new MasteryDef { Id = "cannon", Name = "Big Guns", Blurb = "Turn back 3,000 raiders with Cannon Cars", Goal = 3000, Have = s => s.CarKills("cannon"), Title = "Cannon Captain", Skin = "gunmetal" },
            new MasteryDef { Id = "clean", Name = "Iron Wall", Blurb = "Reach 25 stations without a wagon knocked out on the way", Goal = 25, Have = s => s.State.stationsClean, Title = "Iron Wall" },
            new MasteryDef { Id = "light", Name = "Travelling Light", Blurb = "Reach 15 stations with two wagons or fewer", Goal = 15, Have = s => s.State.stationsLight, Title = "Lightweight", Skin = "racer" },
            new MasteryDef { Id = "bosses", Name = "Baron Breaker", Blurb = "Bring down 25 bosses", Goal = 25, Have = s => s.State.bosses, Title = "Baron Breaker", Skin = "baron" },
            new MasteryDef { Id = "crits", Name = "Dead Eye", Blurb = "Land 2,000 crits", Goal = 2000, Have = s => s.State.crits, Title = "Dead Eye" },
            new MasteryDef { Id = "far", Name = "Far Rider", Blurb = "Reach mile 100 on any line", Goal = 100, Have = s => s.BestMile, Title = "Far Rider" },
            new MasteryDef { Id = "choices", Name = "Gulch Runner", Blurb = "Take 30 of the line's offers", Goal = 30, Have = s => s.State.choicesTaken, Title = "Gulch Runner" },
        };

        public static MasteryDef Mastery(string id) { return Masteries.Find(m => m.Id == id); }
        public bool MasteryDone(MasteryDef m) { return m.Have(this) >= m.Goal; }
        public bool MasteryClaimed(MasteryDef m) { return State.mastered.Contains(m.Id); }
        public int MasteriesReady { get { int n = 0; foreach (var m in Masteries) if (MasteryDone(m) && !MasteryClaimed(m)) n++; return n; } }

        /// <summary>Takes a finished mastery: its title, and its paint if it has one.</summary>
        public bool ClaimMastery(string id)
        {
            var m = Mastery(id);
            if (m == null || !MasteryDone(m) || MasteryClaimed(m)) return false;
            State.mastered.Add(id);
            GiveTitle(m.Title);
            if (m.Skin != null && !State.skins.Contains(m.Skin)) State.skins.Add(m.Skin);
            Touch();
            return true;
        }

        void GiveTitle(string t)
        {
            if (string.IsNullOrEmpty(t) || State.titles.Contains(t)) return;
            State.titles.Add(t);
            if (State.title.Length == 0) State.title = t;
        }

        /// <summary>Wears a title the player has earned ("" for none).</summary>
        public bool UseTitle(string t)
        {
            t = t ?? "";
            if (t.Length > 0 && !State.titles.Contains(t)) return false;
            State.title = t;
            Touch();
            return true;
        }

        // ---- builds that work ----

        public static readonly List<BuildDef> Builds = new List<BuildDef>
        {
            new BuildDef { Name = "Bullet Hose", Cars = new[] { "gatling", "supply", "gatling", "armour" }, Why = "A Supply Car between two Gatlings speeds both; the Armoured Car at the back takes the hits." },
            new BuildDef { Name = "Artillery Train", Cars = new[] { "cannon", "supply", "cannon", "mortar", "armour" }, Why = "Shells burst among the crowd; the mortar reaches the far ones the cannons can't." },
            new BuildDef { Name = "Hold the Rear", Cars = new[] { "gatling", "cannon", "repair", "armour", "flame" }, Why = "The Furnace burns what reaches the back while the fitters keep it plated." },
            new BuildDef { Name = "Marksmen", Cars = new[] { "rifle", "supply", "rifle", "gatling", "armour" }, Why = "Sharpshooters pick off bosses and war wagons from far off; the Gatling cleans up." },
            new BuildDef { Name = "Storm Front", Cars = new[] { "dynamo", "supply", "dynamo", "flame", "armour" }, Why = "Arcs jump between packed raiders; the Furnace keeps them packed and burning." },
        };

        /// <summary>The wagons a build needs that haven't been built yet.</summary>
        public List<string> BuildMissing(BuildDef b)
        {
            var miss = new List<string>();
            foreach (var id in b.Cars) if (!Built(id) && !miss.Contains(id)) miss.Add(id);
            return miss;
        }

        /// <summary>Couples a build up in its order (as far as the places go).</summary>
        public bool UseBuild(int index)
        {
            if (index < 0 || index >= Builds.Count || BuildMissing(Builds[index]).Count > 0) return false;
            return UseOrder(new List<string>(Builds[index].Cars));
        }

        // ---- the weekly trial ----

        public static readonly List<TrialDef> Trials = new List<TrialDef>
        {
            new TrialDef { Name = "Gatling Gauntlet", Blurb = "Three Gatlings and a Supply Car. Nothing from the workshop.", Cars = new[] { "gatling", "supply", "gatling", "gatling" }, Level = 12, Cards = new[] { "rate" } },
            new TrialDef { Name = "Big Bore", Blurb = "Cannons only, and the shells split.", Cars = new[] { "cannon", "cannon", "supply", "cannon" }, Level = 12, Cards = new[] { "cluster" } },
            new TrialDef { Name = "Glass Express", Blurb = "Hits hard, breaks easily.", Cars = new[] { "gatling", "cannon", "rifle", "supply" }, Level = 14, Cards = new[] { "glass" } },
            new TrialDef { Name = "The Decoy", Blurb = "Everything comes for the armoured car at the back.", Cars = new[] { "gatling", "cannon", "repair", "armour" }, Level = 12, Cards = new[] { "decoy" } },
            new TrialDef { Name = "Steam and Thunder", Blurb = "The whistle reloads every gun.", Cars = new[] { "gatling", "mortar", "supply", "cannon" }, Level = 12, Cards = new[] { "recharge" } },
            new TrialDef { Name = "Lightning Line", Blurb = "Two Dynamos and a Furnace.", Cars = new[] { "dynamo", "flame", "dynamo", "supply" }, Level = 10, Cards = new[] { "arc" } },
        };

        /// <summary>Monday-to-Monday weeks (local time), counted from 1970.</summary>
        public long Week { get { return (Today + 3) / 7; } }
        public TrialDef TrialNow { get { return Trials[(int)(Week % Trials.Count)]; } }
        public const int TrialOpensAt = 20;
        public bool TrialOpen { get { return BestMile >= TrialOpensAt; } }
        public bool Trial { get { return State.run != null && State.run.trial; } }
        public int TrialBest { get { return State.trialWeek == Week ? State.trialBest : 0; } }
        public static readonly int[] TrialMarks = { 10, 25, 40 };
        public static readonly string[] TrialTitles = { "Trial Runner", "Proving Ground Regular", null };

        /// <summary>Sets off on this week's trial: its own train at its own level, the week's luck, no steel, no workshop.</summary>
        public bool StartTrial()
        {
            if (State.run != null || !TrialOpen) return false;
            var t = TrialNow;
            if (State.trialWeek != Week) { State.trialWeek = Week; State.trialBest = 0; }
            State.trialRuns++;
            var r = new RunState { route = 0, trial = true, startBest = State.trialBest };
            State.run = r;
            State.seed = (uint)(Week * 2654435761L % 4294967291L) | 1u;     // the same luck for everyone this week
            r.cars.Add(NewCar("engine"));
            foreach (var id in t.Cars) r.cars.Add(NewCar(id));
            foreach (var c in t.Cards) r.cards.Add(new Pick { card = c, rarity = 0 });
            RefreshPlating();
            r.hullMax = HullMax; r.hull = r.hullMax;
            r.scrip = StartScrip;
            FillQueue();
            Touch();
            return true;
        }

        /// <summary>A trial run ends: its best this week, and what each mark gives (once ever).</summary>
        void CloseTrial(RunState r)
        {
            int reached = r.mile - 1;
            if (State.trialWeek != Week) { State.trialWeek = Week; State.trialBest = 0; }
            State.trialBest = Math.Max(State.trialBest, reached);
            State.trialBestEver = Math.Max(State.trialBestEver, reached);
            for (int i = 0; i < TrialMarks.Length; i++)
                if (reached >= TrialMarks[i])
                {
                    if (TrialTitles[i] != null) GiveTitle(TrialTitles[i]);
                    else if (!State.skins.Contains("trial")) State.skins.Add("trial");
                }
        }

        // ---- the depot grows ----

        public DepotLook Depot
        {
            get
            {
                return new DepotLook
                {
                    Sidings = Math.Min(3, 1 + (State.engines > 0 ? 1 : 0) + (BestMile >= 100 ? 1 : 0)),
                    Parked = Math.Min(4, State.engines),
                    Trophies = (int)Math.Min(6, State.bosses / 5),
                    Workers = (int)Math.Min(6, 1 + State.runs / 15),
                };
            }
        }

        // ---- the expedition track ----

        public const int Tiers = 30, SeasonDays = 28;
        public const double PerTier = 30;
        public long Season { get { return (Today + 3) / SeasonDays; } }
        public int SeasonDaysLeft { get { return (int)(SeasonDays - (Today + 3) % SeasonDays); } }

        /// <summary>The free track: steel every step, gems every third, a seal every fifth, a title halfway, a paint at the end.</summary>
        public static readonly List<StepDef> Steps = MakeSteps();
        static List<StepDef> MakeSteps()
        {
            var list = new List<StepDef>();
            for (int t = 1; t <= Tiers; t++)
            {
                var st = new StepDef { Tier = t, Need = t * PerTier, Gift = "steel", Amount = 2 };
                if (t % 3 == 0) { st.Gift = "gems"; st.Amount = 10 + t / 3 * 2; }
                if (t % 5 == 0) { st.Gift = "seal"; st.Amount = 1; }
                if (t == 15) { st.Gift = "title:Expedition Hand"; st.Amount = 0; }
                if (t == Tiers) { st.Gift = "skin:pathfinder"; st.Amount = 0; }
                list.Add(st);
            }
            return list;
        }

        void SeasonTurn()
        {
            if (State.expSeason == Season) return;
            State.expSeason = Season; State.expPoints = 0; State.expClaimed.Clear();
        }

        public double ExpPoints { get { return State.expSeason == Season ? State.expPoints : 0; } }
        public int ExpTier { get { return (int)Math.Min(Tiers, Math.Floor(ExpPoints / PerTier)); } }
        public bool StepClaimed(int tier) { return State.expSeason == Season && State.expClaimed.Contains(tier); }
        public int StepsReady { get { int n = 0; for (int t = 1; t <= ExpTier; t++) if (!StepClaimed(t)) n++; return n; } }

        void AddExp(double points) { SeasonTurn(); State.expPoints += points; }

        /// <summary>Steel a step gives: a couple of hours of the depot's patrols, never less than a run's worth early on.</summary>
        public double StepSteel(StepDef st) { return Math.Floor(Math.Max(200, DepotRate * st.Amount)); }

        public bool ClaimStep(int tier)
        {
            SeasonTurn();
            if (tier < 1 || tier > ExpTier || StepClaimed(tier)) return false;
            var st = Steps[tier - 1];
            State.expClaimed.Add(tier);
            if (st.Gift == "steel") { double v = StepSteel(st); State.steel += v; State.lifetime += v; }
            else if (st.Gift == "gems") State.gems += st.Amount;
            else if (st.Gift == "seal") State.seals += st.Amount;
            else if (st.Gift.StartsWith("title:")) GiveTitle(st.Gift.Substring(6));
            else if (st.Gift.StartsWith("skin:") && !State.skins.Contains(st.Gift.Substring(5))) State.skins.Add(st.Gift.Substring(5));
            Touch();
            return true;
        }

        public static string StepName(StepDef st)
        {
            if (st.Gift == "gems") return st.Amount + " gems";
            if (st.Gift == "seal") return st.Amount + " seal";
            if (st.Gift.StartsWith("title:")) return "the title \"" + st.Gift.Substring(6) + "\"";
            if (st.Gift.StartsWith("skin:")) { var k = Content.Skin(st.Gift.Substring(5)); return (k != null ? k.Name : "a paint") + " for the whole train"; }
            return "steel";
        }
    }
}
