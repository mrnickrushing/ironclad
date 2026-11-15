using System;
using System.Collections.Generic;

namespace Ironclad.Core
{
    /// <summary>What is kept between runs: steel, the workshop's upgrades, the wagons built and the order they are coupled in.</summary>
    public sealed partial class Sim
    {
        public int UpgradeLevel(string id) { var u = State.upgrades.Find(x => x.id == id); return u == null ? 0 : u.level; }
        public int CarLevel(string id) { var c = State.cars.Find(x => x.id == id); return c == null ? 0 : c.level; }
        public bool Built(string id) { return CarLevel(id) > 0; }

        public double UpgradeCost(string id)
        {
            var d = Content.Upgrade(id);
            return Math.Floor(d.Cost * Math.Pow(d.Growth, UpgradeLevel(id)));
        }
        public bool UpgradeMaxed(string id) { var d = Content.Upgrade(id); return d.Max > 0 && UpgradeLevel(id) >= d.Max; }

        public bool BuyUpgrade(string id)
        {
            var d = Content.Upgrade(id);
            if (d == null || UpgradeMaxed(id)) return false;
            double cost = UpgradeCost(id);
            if (State.steel < cost) return false;
            State.steel -= cost;
            var u = State.upgrades.Find(x => x.id == id);
            if (u == null) State.upgrades.Add(u = new Level { id = id });
            u.level++;
            Touch();
            return true;
        }

        // ---- what the Baron's seals buy ----

        public int PerkLevel(string id) { var u = State.perks.Find(x => x.id == id); return u == null ? 0 : u.level; }
        public bool PerkMaxed(string id) { return PerkLevel(id) >= Content.Perk(id).Max; }
        public int PerkCost(string id) { return Content.Perk(id).Cost * (PerkLevel(id) + 1); }

        public bool BuyPerk(string id)
        {
            var d = Content.Perk(id);
            if (d == null || PerkMaxed(id)) return false;
            int cost = PerkCost(id);
            if (State.seals < cost) return false;
            State.seals -= cost;
            var u = State.perks.Find(x => x.id == id);
            if (u == null) State.perks.Add(u = new Level { id = id });
            u.level++;
            Touch();
            return true;
        }

        /// <summary>The best mile reached on any line.</summary>
        public int BestMile { get { int b = 0; foreach (int m in State.best) b = Math.Max(b, m); return b; } }

        /// <summary>Whether the plans for this wagon have turned up yet.</summary>
        public bool PlansFound(string id) { var d = Content.Car(id); return d != null && BestMile >= d.Mile; }

        /// <summary>The wagon whose plans turn up next (null: every plan is found).</summary>
        public CarDef NextPlans
        {
            get
            {
                CarDef next = null;
                foreach (var d in Content.Cars) if (d.Id != "engine" && !PlansFound(d.Id) && (next == null || d.Mile < next.Mile)) next = d;
                return next;
            }
        }

        /// <summary>Wagons whose plans are found but which haven't been built yet.</summary>
        public List<CarDef> Unbuilt { get { return Content.Cars.FindAll(d => d.Id != "engine" && PlansFound(d.Id) && !Built(d.Id)); } }

        /// <summary>Steel for the next workshop level of a wagon (or to build the first one).</summary>
        public double CarCost(string id)
        {
            var d = Content.Car(id);
            int lv = CarLevel(id);
            if (lv == 0) return d.Steel;
            return Math.Floor((Content.CarCost + d.Steel * 0.25) * Math.Pow(Content.CarGrowth, lv - 1));
        }

        /// <summary>Builds a wagon the first time, or raises its workshop level.</summary>
        public bool BuyCar(string id)
        {
            var d = Content.Car(id);
            if (d == null) return false;
            if (!Built(id) && !PlansFound(id)) return false;
            double cost = CarCost(id);
            if (State.steel < cost) return false;
            State.steel -= cost;
            var c = State.cars.Find(x => x.id == id);
            if (c == null) { State.cars.Add(new Level { id = id, level = 1 }); if (State.train.Count < Slots) State.train.Add(id); }
            else c.level++;
            Touch();
            return true;
        }

        /// <summary>How many wagons the engine can pull.</summary>
        public int Slots { get { return Math.Min(Content.MaxSlots, Content.StartSlots + UpgradeLevel("slots")); } }
        public int SpeedsOpen { get { return Math.Min(Content.Speeds.Length, 1 + UpgradeLevel("speed")); } }

        public bool SetSpeed(int index)
        {
            if (index < 0 || index >= SpeedsOpen) return false;
            State.speed = index; Touch();
            return true;
        }

        /// <summary>Whether the train can be recoupled now: between runs, or standing at a station.</summary>
        public bool CanCouple { get { return State.run == null || State.run.phase != (int)Phase.Rolling; } }

        /// <summary>Couples a built wagon on at the back. The same kind can ride more than once.</summary>
        public bool Couple(string id)
        {
            if (!CanCouple || id == "engine" || !Built(id) || State.train.Count >= Slots) return false;
            State.train.Add(id);
            SyncRunTrain();
            Touch();
            return true;
        }

        /// <summary>Takes the wagon in this place off (0 is the one behind the engine). One always stays.</summary>
        public bool Uncouple(int place)
        {
            if (!CanCouple || place < 0 || place >= State.train.Count || State.train.Count <= 1) return false;
            State.train.RemoveAt(place);
            if (State.run != null) State.run.cars.RemoveAt(place + 1);
            Touch();
            return true;
        }

        /// <summary>Moves a wagon to another place in the train.</summary>
        public bool Shunt(int from, int to)
        {
            if (!CanCouple || from < 0 || to < 0 || from >= State.train.Count || to >= State.train.Count || from == to) return false;
            string id = State.train[from];
            State.train.RemoveAt(from); State.train.Insert(to, id);
            if (State.run != null)
            {
                var c = State.run.cars[from + 1];
                State.run.cars.RemoveAt(from + 1); State.run.cars.Insert(to + 1, c);
            }
            Touch();
            return true;
        }

        /// <summary>At a station a newly coupled wagon joins the run with full plating.</summary>
        void SyncRunTrain()
        {
            var r = State.run;
            if (r == null) return;
            while (r.cars.Count - 1 < State.train.Count) r.cars.Add(NewCar(State.train[r.cars.Count - 1]));
        }

        // ---- the lines ----

        public bool RouteOpen(int index)
        {
            if (index <= 0) return index == 0;
            return index < Content.Routes.Count && State.best[index - 1] >= Content.Routes[index].Opens;
        }

        public bool ChooseRoute(int index)
        {
            if (State.run != null || !RouteOpen(index)) return false;
            State.route = index; Touch();
            return true;
        }

        // ---- the numbers a run is fought with ----

        double CardSum(string id)
        {
            double v = 0;
            if (State.run == null) return 0;
            var d = Content.Card(id);
            foreach (var p in State.run.cards) if (p.card == id) v += d.Amount * Content.RarityMult[p.rarity];
            return v;
        }

        public double DamageMult { get { return (1 + Content.Upgrade("power").Step * UpgradeLevel("power")) * (1 + CardSum("damage")) * CharterMult; } }
        public double RateMult { get { return (1 + Content.Upgrade("rate").Step * UpgradeLevel("rate")) * (1 + CardSum("rate")); } }
        public double RangeMult { get { return 1 + CardSum("range"); } }
        public double CritChance { get { return Content.CritBase + Content.Upgrade("crit").Step * UpgradeLevel("crit") + CardSum("crit"); } }
        public double CritDamage { get { return Content.CritDamageBase + Content.Upgrade("critdmg").Step * UpgradeLevel("critdmg"); } }
        public double ScripMult { get { return (1 + Content.Upgrade("scrip").Step * UpgradeLevel("scrip")) * (1 + CardSum("scrip")); } }
        public double SteelMult { get { return (1 + Content.Upgrade("steel").Step * UpgradeLevel("steel")) * (1 + CardSum("steel")) * (Owns("ironclad.doubler") ? 2 : 1) * CharterMult; } }
        public double HullMax { get { return Content.HullBase * (1 + Content.Upgrade("hull").Step * UpgradeLevel("hull")) * (1 + CardSum("hull")); } }
        public double PlateMax(string type) { return Content.PlateBase * Content.Car(type).Plating * (1 + Content.Upgrade("plate").Step * UpgradeLevel("plate")) * (1 + CardSum("plate")); }
        public double HullMend { get { return Content.Upgrade("mend").Step * UpgradeLevel("mend"); } }
        public double WhistleEvery { get { return Content.WhistleCooldown - Content.Upgrade("whistle").Step * UpgradeLevel("whistle"); } }
        public double StartScrip { get { return Content.StartScrip + Content.Upgrade("start").Step * UpgradeLevel("start"); } }

        /// <summary>What one shot from this car does before crits, with its workshop and field levels.</summary>
        public double ShotDamage(CarState c)
        {
            return Content.Car(c.type).Damage * Math.Pow(Content.CarPower, CarLevel(c.type) - 1) * Math.Pow(Content.FieldPower, c.field) * DamageMult;
        }

        /// <summary>Scrip for the next field level of the car in this place of the run's train (0 is the engine).</summary>
        public double FieldCost(int car)
        {
            var r = State.run;
            if (r == null || car < 0 || car >= r.cars.Count) return 0;
            return Math.Floor(Content.FieldCost * Math.Pow(Content.FieldGrowth, r.cars[car].field));
        }

        /// <summary>Spends scrip on a car for the rest of this run: more damage from a gun, more of what a support wagon gives, and its plating made good.</summary>
        public bool FieldUpgrade(int car)
        {
            var r = State.run;
            if (r == null || r.phase == (int)Phase.Over || car < 0 || car >= r.cars.Count) return false;
            double cost = FieldCost(car);
            if (r.scrip < cost) return false;
            r.scrip -= cost;
            var c = r.cars[car];
            c.field++; State.fieldBuys++;
            if (c.platingMax > 0) { c.platingMax = PlateOf(c); c.plating = c.platingMax; c.down = false; }      // and it is back in the fight
            Touch();
            return true;
        }

        /// <summary>A wagon's plating grows a little with each field level too.</summary>
        double PlateOf(CarState c) { return PlateMax(c.type) * (1 + 0.10 * c.field); }

        /// <summary>How much stronger a support wagon's gift is for its levels.</summary>
        double SupportMult(CarState c) { return 1 + 0.04 * (CarLevel(c.type) - 1) + 0.10 * c.field; }
    }
}
