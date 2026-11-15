using System;
using System.Collections.Generic;

namespace Ironclad.Core
{
    /// <summary>What a wagon gets from the support wagons either side of it.</summary>
    public struct Gift
    {
        public double Haste;        // fires this much faster (0.25: +25%)
        public double Shield;       // takes this much less (0.33: a third less)
        public bool Wasted;         // a support wagon's gift lands on another support wagon that can't use it
    }

    /// <summary>A wagon whose gifts change if the train is recoupled.</summary>
    public struct GiftChange { public string Type; public double Haste, Shield; }

    /// <summary>The end-of-run breakdown: who did the work, what went wrong first, and one thing to try.</summary>
    public sealed class Debrief
    {
        public sealed class Share { public string Type; public double Damage, Part; }
        public List<Share> Weapons = new List<Share>();     // each kind of wagon's share of the damage, biggest first
        public string FirstOut;                             // "the Cannon Car (3rd wagon) at mile 23", or null
        public string Danger;                               // the kind of raider that did the most harm, by name
        public double DangerPart;                           // its share of the harm
        public string TipId, Tip;
    }

    /// <summary>Something to aim at next and how far there is to go.</summary>
    public sealed class Goal
    {
        public string Name, Unit;   // "the Fitters' Car plans", "mile" / "steel"
        public double Have, Need;
        public string Where;        // "on any line", "TRAIN", ...
    }

    /// <summary>
    /// Making what is already there easier to see: what the wagons give each other and what a move would change, buying
    /// many levels at once, saved formations, the breakdown of a lost run, what a purchase would really give, the next goal
    /// and the card that celebrates reaching it.
    /// </summary>
    public sealed partial class Sim
    {
        // ---- what the neighbours give ----

        /// <summary>The gifts every place of a train order gets (place 0 is the wagon behind the engine). Field levels
        /// are the run's when there is one, else the Quartermaster's head start.</summary>
        public Gift[] NeighbourGifts(IList<string> order, IList<int> fields = null)
        {
            var g = new Gift[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                double haste = 0, shield = 1;
                for (int k = i - 1; k <= i + 1; k += 2)
                {
                    if (k < 0 || k >= order.Count) continue;
                    var d = Content.Car(order[k]);
                    if (d == null || (d.Haste <= 0 && d.Shield <= 0)) continue;
                    double mult = 1 + 0.04 * (CarLevel(order[k]) - 1) + 0.10 * (fields != null && k < fields.Count ? fields[k] : PerkLevel("quarter"));
                    haste += d.Haste * mult;
                    shield *= 1 - Math.Min(0.6, d.Shield * mult);
                    if (d.Haste > 0 && Content.Car(order[i]).Support) g[i].Wasted = true;
                }
                g[i].Haste = haste; g[i].Shield = 1 - shield;
                // Haste only matters to a wagon that fires.
                if (Content.Car(order[i]).Support) g[i].Haste = 0;
            }
            return g;
        }

        /// <summary>The gifts of the train as it is coupled now.</summary>
        public Gift[] TrainGifts()
        {
            var r = State.run;
            if (r != null)
            {
                var order = new List<string>(); var fields = new List<int>();
                for (int i = 1; i < r.cars.Count; i++) { order.Add(r.cars[i].type); fields.Add(r.cars[i].field); }
                return NeighbourGifts(order, fields);
            }
            return NeighbourGifts(State.train);
        }

        /// <summary>What moving the wagon at <paramref name="from"/> to <paramref name="to"/> (as <see cref="Shunt"/> does)
        /// would change: every wagon that would fire faster or slower, or take more or less.</summary>
        public List<GiftChange> ShuntPreview(int from, int to)
        {
            var list = new List<GiftChange>();
            int n = State.train.Count;
            if (from < 0 || to < 0 || from >= n || to >= n || from == to) return list;
            var order = new List<string>(State.train); var who = new List<int>();
            for (int i = 0; i < n; i++) who.Add(i);
            List<int> fields = null;
            if (State.run != null) { fields = new List<int>(); for (int i = 1; i < State.run.cars.Count; i++) fields.Add(State.run.cars[i].field); }
            var before = NeighbourGifts(order, fields);
            string id = order[from]; order.RemoveAt(from); order.Insert(to, id);
            int w = who[from]; who.RemoveAt(from); who.Insert(to, w);
            List<int> moved = null;
            if (fields != null) { moved = new List<int>(fields); int f = moved[from]; moved.RemoveAt(from); moved.Insert(to, f); }
            var after = NeighbourGifts(order, moved);
            for (int i = 0; i < n; i++)
            {
                var b = before[who[i]]; var a = after[i];
                double dh = a.Haste - b.Haste, ds = a.Shield - b.Shield;
                if (Math.Abs(dh) > 1e-6 || Math.Abs(ds) > 1e-6) list.Add(new GiftChange { Type = order[i], Haste = dh, Shield = ds });
            }
            return list;
        }

        /// <summary>A preview in words: "Cannon Car fires 25% faster, Gatling Car takes 33% more".</summary>
        public static string Describe(List<GiftChange> changes)
        {
            if (changes.Count == 0) return "No change to what the wagons give each other";
            var parts = new List<string>();
            foreach (var c in changes)
            {
                string name = Content.Car(c.Type).Name;
                if (Math.Abs(c.Haste) > 1e-6) parts.Add(name + (c.Haste > 0 ? " fires " : " fires ") + Pct(Math.Abs(c.Haste)) + (c.Haste > 0 ? " faster" : " slower"));
                if (Math.Abs(c.Shield) > 1e-6) parts.Add(name + " takes " + Pct(Math.Abs(c.Shield)) + (c.Shield > 0 ? " less" : " more"));
            }
            return string.Join(", ", parts);
        }

        static string Pct(double v) { return Math.Round(v * 100) + "%"; }

        // ---- many levels at once ----

        /// <summary>Levels at once: 1, 10, or as many as the steel buys (int.MaxValue).</summary>
        public const int BuyMax = int.MaxValue;

        /// <summary>What up to <paramref name="want"/> more levels of a workshop upgrade cost, and how many that is
        /// (MAX: as many as the steel in hand pays for, at least one to show a price).</summary>
        public double UpgradeCostN(string id, int want, out int count)
        {
            var d = Content.Upgrade(id); count = 0; double total = 0;
            int lv = UpgradeLevel(id);
            while (count < want && (d.Max <= 0 || lv + count < d.Max))
            {
                double c = Math.Floor(d.Cost * Math.Pow(d.Growth, lv + count));
                if (want == BuyMax && count > 0 && total + c > State.steel) break;
                total += c; count++;
                if (want == BuyMax && total > State.steel) break;
            }
            return total;
        }

        /// <summary>Buys up to <paramref name="want"/> levels of an upgrade, as many as the steel pays for. Returns how many.</summary>
        public int BuyUpgrades(string id, int want)
        {
            int n = 0;
            while (n < want && BuyUpgrade(id)) n++;
            return n;
        }

        /// <summary>The same for a built wagon's workshop levels.</summary>
        public double CarCostN(string id, int want, out int count)
        {
            count = 0; double total = 0;
            int lv = CarLevel(id);
            if (lv == 0) { count = 1; return CarCost(id); }
            var d = Content.Car(id);
            while (count < want)
            {
                double c = Math.Floor((Content.CarCost + d.Steel * 0.25) * Math.Pow(Content.CarGrowth, lv + count - 1));
                if (want == BuyMax && count > 0 && total + c > State.steel) break;
                total += c; count++;
                if (want == BuyMax && total > State.steel) break;
            }
            return total;
        }

        /// <summary>Raises a wagon up to <paramref name="want"/> levels (building it counts as the first). Returns how many.</summary>
        public int BuyCars(string id, int want)
        {
            int n = 0;
            bool building = !Built(id);
            while (n < want && BuyCar(id)) { n++; if (building) break; }
            return n;
        }

        // ---- formations ----

        public const int Formations = 3;

        /// <summary>Keeps the train's order in a slot.</summary>
        public bool SaveFormation(int slot)
        {
            if (slot < 0 || slot >= Formations) return false;
            State.formations[slot].cars = new List<string>(State.train);
            Touch();
            return true;
        }

        /// <summary>Couples up a saved order (what is built of it, as many as there are places). Only between runs or at a station.</summary>
        public bool UseFormation(int slot)
        {
            if (slot < 0 || slot >= Formations || !CanCouple) return false;
            var want = State.formations[slot].cars.FindAll(id => Built(id) && id != "engine");
            if (want.Count > Slots) want = want.GetRange(0, Slots);
            if (want.Count == 0) return false;
            var r = State.run;
            if (r != null)
            {
                // At a station each wagon keeps its own plating and field levels: the same kind takes the same car back.
                var pool = r.cars.GetRange(1, r.cars.Count - 1);
                var cars = new List<CarState> { r.cars[0] };
                foreach (var id in want)
                {
                    int k = pool.FindIndex(c => c.type == id);
                    if (k >= 0) { cars.Add(pool[k]); pool.RemoveAt(k); }
                    else cars.Add(NewCar(id));
                }
                r.cars = cars;
            }
            State.train = want;
            Touch();
            return true;
        }

        // ---- the breakdown of a lost run ----

        static string Ordinal(int n) { return n + (n % 100 >= 11 && n % 100 <= 13 ? "th" : n % 10 == 1 ? "st" : n % 10 == 2 ? "nd" : n % 10 == 3 ? "rd" : "th"); }

        /// <summary>Who did the work on the run that just ended, what went first, the worst of the raiders, and one
        /// suggestion for the next run. Null when there is no ended run.</summary>
        public Debrief Debrief
        {
            get
            {
                var r = State.run;
                if (r == null || r.phase != (int)Phase.Over) return null;
                var d = new Debrief();
                double total = 0;
                var by = new Dictionary<string, double>();
                foreach (var c in r.cars) { double v; by.TryGetValue(c.type, out v); by[c.type] = v + c.dealt; total += c.dealt; }
                foreach (var kv in by) d.Weapons.Add(new Debrief.Share { Type = kv.Key, Damage = kv.Value, Part = total > 0 ? kv.Value / total : 0 });
                d.Weapons.Sort((a, b) => b.Damage.CompareTo(a.Damage));
                if (r.firstOut > 0) d.FirstOut = "the " + Content.Car(r.firstOutType).Name + " (" + Ordinal(r.firstOut) + " wagon) at mile " + r.firstOutMile;
                double hurt = 0; Tally worst = null;
                foreach (var t in r.hurtBy) { hurt += t.v; if (worst == null || t.v > worst.v) worst = t; }
                if (worst != null) { d.Danger = Plural(worst.id); d.DangerPart = hurt > 0 ? worst.v / hurt : 0; }
                PickTip(d, r, hurt, worst);
                return d;
            }
        }

        static string Plural(string kind)
        {
            switch (kind)
            {
                case "outrider": return "Outriders"; case "sledge": return "Sledgemen"; case "gunwagon": return "Gun Wagons";
                case "powder": return "Powder Monkeys"; case "handcar": return "Handcar Gangs"; case "warwagon": return "War Wagons";
            }
            var e = Content.Enemy(kind);
            return e != null ? e.Name : kind;
        }

        void PickTip(Debrief d, RunState r, double hurt, Tally worst)
        {
            bool Has(string t) { return r.cars.Exists(c => c.type == t); }
            double cheapest = double.MaxValue;
            for (int i = 0; i < r.cars.Count; i++) cheapest = Math.Min(cheapest, FieldCost(i));
            string w = worst != null ? worst.id : "";
            double part = d.DangerPart;
            var gifts = TrainGifts();
            bool wasted = false; foreach (var g in gifts) if (g.Wasted) wasted = true;
            int last = r.cars.Count - 1;

            if (r.scrip >= cheapest * 3 && r.mile >= 3)
                Tip(d, "scrip", "You finished with " + Format.Money(r.scrip) + " scrip unspent. Spend it on the green buttons during a run: every field level makes a wagon stronger at once.");
            else if (r.bossLeft >= 0 && !Has("rifle") && Built("rifle"))
                Tip(d, "boss_rifle", "The boss held you up. The Sharpshooter Car picks out the toughest raider on the field: couple it on for the boss miles.");
            else if (r.bossLeft >= 0)
                Tip(d, "boss_mark", "The boss held you up. Tap it to point every gun at it, and save the whistle for its escort.");
            else if (wasted)
                Tip(d, "wasted", "A Supply Car is next to another support wagon, which can't use the faster fire. Put guns either side of it.");
            else if (Has("mortar") && hurt > 0 && r.hurtClose / hurt > 0.5)
                Tip(d, "mortar_close", "Your mortar struggled against raiders close to the train: it can't fire inside 8 m. Couple a Gatling or Furnace Car beside it.");
            else if (r.firstOut == last && last > 1 && !Has("armour"))
                Tip(d, "rear", "The last wagon went first: raiders from behind reach it before anything else. Put your toughest wagon at the back" + (Built("armour") ? ", or the Armoured Car." : "."));
            else if (w == "powder" && part > 0.3)
                Tip(d, "powder", "Powder Monkeys did the most harm: they blow up on the first car they reach. The Gatling Car shoots the closest first, so keep one near the middle.");
            else if (w == "handcar" && part > 0.3)
                Tip(d, "handcar", "Handcar Gangs did the most harm: they come along the rails from ahead and behind. Strong guns at both ends of the train stop them.");
            else if ((w == "gunwagon" || w == "warwagon") && part > 0.3)
                Tip(d, "ranged", (w == "gunwagon" ? "Gun Wagons" : "War Wagons") + " did the most harm by standing off and shooting. " + (Built("mortar") ? "The Mortar Car's long reach is made for them." : Built("rifle") ? "The Sharpshooter Car reaches them from far off." : "Long Sights cards and the Cannon Car reach them."));
            else if (UpgradeLevel("hull") < UpgradeLevel("power") / 2)
                Tip(d, "hull", "Your guns are well ahead of your hull. A few levels of Boiler Plate in the workshop keep the engine going for longer.");
            else
                Tip(d, "workshop", "Steel from this run buys upgrades that are kept for good. The cheapest ones are often the best value: try the WORKSHOP.");
        }

        static void Tip(Debrief d, string id, string text) { d.TipId = id; d.Tip = text; }

        // ---- what a purchase would give this player ----

        /// <summary>A before-and-after for a thing sold once, worked out for this save. Null for packs of gems.</summary>
        public string[] PurchasePreview(string product)
        {
            switch (product)
            {
                case "ironclad.doubler":
                    {
                        double last = State.lastRunSteel; double rate = DepotRate;
                        return new[]
                        {
                            last > 0 ? "Your last run: " + Format.Money(last) + " steel" : "Every mile and boss: the steel it pays",
                            last > 0 ? "With the Rolling Mill: " + Format.Money(last * 2) + " steel" : "With the Rolling Mill: twice that",
                            rate > 0 ? "The depot: " + Format.Money(rate) + " an hour, with it " + Format.Money(rate * 2) : "The depot's patrols pay twice as much too",
                        };
                    }
                case "ironclad.pass":
                    return new[]
                    {
                        "Back on the rails once a run, the run's steel doubled, the depot doubled: all without an ad",
                        "Up to " + AdsPerDay + " a day, as now, for good",
                        State.adsWatched > 0 ? "You've watched " + State.adsWatched + (State.adsWatched == 1 ? " ad" : " ads") + " for rewards so far" : "Nothing else changes: you never have to watch ads to play",
                    };
                case "ironclad.depot":
                    {
                        double rate = DepotRate, extra = Math.Floor(rate * 4);
                        return new[]
                        {
                            "Now: the patrols work " + Content.DepotHours + " hours while you're away",
                            "With Night Shift: " + (Content.DepotHours + 4) + " hours",
                            rate > 0 ? "At your rate that's up to +" + Format.Money(extra) + " steel each night you're away" : "Finish a run first: the patrols work the lines you have crossed",
                        };
                    }
                case "ironclad.starter":
                    return new[] { "250 gems", Format.Money(Math.Max(Product(product).Steel, Math.Floor(DepotRate * 4))) + " steel", "One time only" };
            }
            return null;
        }

        // ---- the next goal ----

        /// <summary>The nearest thing to aim at: a wagon whose plans are found, the next plans or line, else the first
        /// (or next) charter.</summary>
        public Goal NextGoal
        {
            get
            {
                var unbuilt = Unbuilt;
                if (unbuilt.Count > 0)
                {
                    var c = unbuilt[0];
                    return new Goal { Name = "Build the " + c.Name, Unit = "steel", Have = Math.Min(State.steel, c.Steel), Need = c.Steel, Where = "TRAIN" };
                }
                var u = NextUnlock;
                if (u != null) return new Goal { Name = u.Name, Unit = "mile", Have = u.Have, Need = u.Mile, Where = u.Where };
                int n = ChartersOnRenew + 1;
                return new Goal { Name = n == 1 ? "Your first charter" : "Charter no. " + (State.charters + n), Unit = "steel", Have = EngineSteel, Need = Content.CharterSteel * n * n, Where = "WORKSHOP" };
            }
        }

        /// <summary>Everything that has opened up so far: wagons' plans and lines.</summary>
        List<string> Unlocked()
        {
            var list = new List<string>();
            foreach (var d in Content.Cars) if (d.Id != "engine" && d.Mile > 0 && PlansFound(d.Id)) list.Add("plans:" + d.Id);
            for (int i = 1; i < Content.Routes.Count; i++) if (RouteOpen(i)) list.Add("line:" + i);
            return list;
        }

        /// <summary>What has opened since the UNLOCKED card was last shown, by name (empty: nothing new).</summary>
        public List<string> NewlyUnlocked
        {
            get
            {
                if (!State.celebrateInit) { State.celebrated = Unlocked(); State.celebrateInit = true; }
                var names = new List<string>();
                foreach (var k in Unlocked())
                {
                    if (State.celebrated.Contains(k)) continue;
                    if (k.StartsWith("plans:")) names.Add("The " + Content.Car(k.Substring(6)).Name + " plans");
                    else names.Add(Content.Routes[int.Parse(k.Substring(5))].Name);
                }
                return names;
            }
        }

        public void Celebrated() { State.celebrated = Unlocked(); State.celebrateInit = true; Touch(); }

        // ---- a test of when to show the Fireman's Pack ----

        /// <summary>Half the players (by the save's cohort) are offered the Fireman's Pack after their first boss instead of
        /// on a later day: the statistics compare the two.</summary>
        public string StarterVariant { get { return State.cohort < 50 ? "later_day" : "first_boss"; } }
    }
}
