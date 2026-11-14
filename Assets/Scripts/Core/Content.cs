using System;
using System.Collections.Generic;

namespace Ironclad.Core
{
    public enum Aim { Nearest, Strongest, Farthest, Around, None }

    /// <summary>A kind of wagon (and the engine itself, id "engine"). Numbers are for workshop level 1.</summary>
    public sealed class CarDef
    {
        public string Id, Name, Blurb;
        public Aim Aim = Aim.Nearest;
        public double Damage, Interval, Range, MinRange;
        public double Splash;           // shells hurt everything this close to where they land
        public double Flight;           // seconds a shell is in the air (0: the shot lands at once)
        public int Chain;               // how many more raiders the shot jumps to
        public double Crit;             // added to the train's crit chance
        public double Plating = 1;      // times the train's plating
        public double Haste;            // neighbours fire this much faster (0.25 = +25%)
        public double Shield;           // neighbours take this much less damage (0.3 = -30%)
        public double Mend;             // share of the weakest wagon's plating put back each second
        public double Steel;            // what the workshop asks to build the first one
        public int Mile;                // the best mile, on any line, at which the plans turn up
        public bool Support { get { return Aim == Aim.None; } }
    }

    public sealed class EnemyDef
    {
        public string Id, Name;
        public double Hp, Speed, Damage, Interval, Range = 1.6;
        public double Bounty;           // scrip
        public int Threat;              // what it costs out of a mile's threat
        public int Mile;                // first mile it rides on
        public bool Rail;               // comes along the track, from ahead or behind
        public bool Blast;              // spends itself on the first hit
        public bool Boss;
    }

    /// <summary>A line to cross. Each is harder than the last and pays more steel.</summary>
    public sealed class RouteDef
    {
        public string Id, Name, Blurb;
        public double Hp, Damage, Steel;
        public int Opens;               // the mile to reach on the line before
    }

    /// <summary>Something the workshop improves for good.</summary>
    public sealed class UpgradeDef
    {
        public string Id, Name, Blurb;
        public double Cost, Growth;     // steel for level 1, and how each next one multiplies
        public double Step;             // what a level adds
        public int Max;                 // 0: no limit
    }

    public enum Rarity { Common, Rare, Epic }

    /// <summary>A card offered at a station: it lasts for the run.</summary>
    public sealed class CardDef
    {
        public string Id, Name, Blurb;  // {0} in the blurb is the amount, as a percentage
        public double Amount;           // at Common; Rare is x1.75, Epic x3
    }

    public static class Content
    {
        // ---- tuning ----
        public const double Step = 0.05;            // seconds of the fight per step
        public const double CarLength = 6;          // metres between one car's middle and the next
        public const double MileSeconds = 22;       // a mile takes this long to roll
        public const double SpawnSeconds = 15;      // the mile's raiders arrive over this long
        public const int StationEvery = 10;         // miles between stations (a boss rides on each of those miles)
        public const double HullBase = 150, PlateBase = 60;
        public const double HpPerMile = 1.085, DamagePerMile = 1.05, BountyPerMile = 1.035, SteelPerMile = 1.055;
        public const double SteelPerMileBase = 8, BossSteel = 6;
        public const double FieldCost = 10, FieldGrowth = 1.36, FieldPower = 1.22;
        public const double CarCost = 20, CarGrowth = 1.17, CarPower = 1.10;
        public const double StartScrip = 20;
        public const double CritBase = 0.05, CritDamageBase = 2;
        public const double WhistleCooldown = 60, WhistleRange = 16, WhistleStun = 3, WhistlePush = 7;
        public const int StartSlots = 2, MaxSlots = 8;
        public const double SpawnRing = 44;         // how far out raiders appear
        public const double ReviveHull = 0.5;
        public const double StationHull = 0.3;      // share of the hull a station puts back
        public const double DepotHours = 8;         // the depot's patrols earn for this long unattended
        public const double DepotShare = 0.35;      // of the steel per hour a played run on that line would bring
        public static readonly double[] RarityMult = { 1, 1.75, 3 };
        public static readonly double[] RarityOdds = { 0.72, 0.23, 0.05 };
        public static readonly double[] Speeds = { 1, 1.5, 2, 3 };

        public static readonly List<CarDef> Cars = new List<CarDef>
        {
            new CarDef { Id = "engine", Name = "Deck Gun", Blurb = "The engine's own gun. Never leaves the front.", Damage = 6, Interval = 0.8, Range = 16 },
            new CarDef { Id = "gatling", Name = "Gatling Car", Blurb = "A storm of small rounds at whoever is closest.", Damage = 4, Interval = 0.2, Range = 15 },
            new CarDef { Id = "cannon", Name = "Cannon Car", Blurb = "A slow shell that bursts among them.", Damage = 28, Interval = 2.2, Range = 22, Splash = 3.5, Flight = 0.5, Steel = 60, Mile = 6 },
            new CarDef { Id = "supply", Name = "Supply Car", Blurb = "The wagons either side of it fire a quarter faster.", Aim = Aim.None, Haste = 0.25, Steel = 250, Mile = 12 },
            new CarDef { Id = "rifle", Name = "Sharpshooter Car", Blurb = "Picks out the toughest raider from far off. Crits often.", Aim = Aim.Strongest, Damage = 48, Interval = 2.4, Range = 30, Crit = 0.2, Steel = 900, Mile = 18 },
            new CarDef { Id = "armour", Name = "Armoured Car", Blurb = "Three times the plating, and the wagons either side take a third less.", Aim = Aim.None, Plating = 3, Shield = 0.33, Steel = 3000, Mile = 25 },
            new CarDef { Id = "flame", Name = "Furnace Car", Blurb = "Burns everything that gets close.", Aim = Aim.Around, Damage = 5, Interval = 0.25, Range = 8, Steel = 9000, Mile = 32 },
            new CarDef { Id = "mortar", Name = "Mortar Car", Blurb = "Lobs a heavy shell at the far ones. Can't hit close in.", Aim = Aim.Farthest, Damage = 70, Interval = 4, Range = 36, MinRange = 10, Splash = 5.5, Flight = 1.2, Steel = 30000, Mile = 40 },
            new CarDef { Id = "repair", Name = "Fitters' Car", Blurb = "Fitters put plating back on the worst-hit wagon as you roll.", Aim = Aim.None, Mend = 0.03, Steel = 90000, Mile = 50 },
            new CarDef { Id = "dynamo", Name = "Dynamo Car", Blurb = "An arc that jumps from raider to raider.", Damage = 16, Interval = 1, Range = 16, Chain = 3, Steel = 300000, Mile = 60 },
        };

        public static readonly List<EnemyDef> Enemies = new List<EnemyDef>
        {
            new EnemyDef { Id = "outrider", Name = "Outrider", Hp = 14, Speed = 6, Damage = 4, Interval = 1, Bounty = 1, Threat = 1, Mile = 1 },
            new EnemyDef { Id = "sledge", Name = "Sledgeman", Hp = 60, Speed = 2.6, Damage = 14, Interval = 1.6, Bounty = 3, Threat = 3, Mile = 4 },
            new EnemyDef { Id = "gunwagon", Name = "Gun Wagon", Hp = 32, Speed = 3.5, Damage = 5, Interval = 1.5, Range = 13, Bounty = 3, Threat = 3, Mile = 7 },
            new EnemyDef { Id = "powder", Name = "Powder Monkey", Hp = 18, Speed = 7.5, Damage = 40, Interval = 1, Bounty = 2, Threat = 2, Mile = 12, Blast = true },
            new EnemyDef { Id = "handcar", Name = "Handcar Gang", Hp = 45, Speed = 9, Damage = 8, Interval = 0.9, Bounty = 4, Threat = 3, Mile = 16, Rail = true },
            new EnemyDef { Id = "warwagon", Name = "War Wagon", Hp = 320, Speed = 2, Damage = 22, Interval = 2, Range = 11, Bounty = 16, Threat = 12, Mile = 22 },
            new EnemyDef { Id = "boss", Name = "The Baron's Carriage", Hp = 420, Speed = 1.6, Damage = 30, Interval = 2, Range = 9, Bounty = 50, Threat = 0, Mile = 10, Boss = true },
        };

        public static readonly List<RouteDef> Routes = new List<RouteDef>
        {
            new RouteDef { Id = "prairie", Name = "The Prairie Line", Blurb = "Open grass and a long way between towns.", Hp = 1, Damage = 1, Steel = 1 },
            new RouteDef { Id = "badlands", Name = "Badlands Cut", Blurb = "Red rock, dry gulches, and more of them.", Hp = 12, Damage = 5, Steel = 6, Opens = 60 },
            new RouteDef { Id = "pass", Name = "The High Pass", Blurb = "Snow on the sleepers and raiders in the pines.", Hp = 160, Damage = 28, Steel = 40, Opens = 70 },
            new RouteDef { Id = "salt", Name = "Salt Flats Run", Blurb = "Nowhere to hide, for anybody.", Hp = 2400, Damage = 170, Steel = 280, Opens = 80 },
            new RouteDef { Id = "canyon", Name = "Black Canyon", Blurb = "The Baron's own country.", Hp = 40000, Damage = 1100, Steel = 2000, Opens = 90 },
        };

        public static readonly List<UpgradeDef> Upgrades = new List<UpgradeDef>
        {
            new UpgradeDef { Id = "power", Name = "Better Powder", Blurb = "+6% damage from every gun", Cost = 15, Growth = 1.16, Step = 0.06 },
            new UpgradeDef { Id = "hull", Name = "Boiler Plate", Blurb = "+10% engine hull", Cost = 12, Growth = 1.16, Step = 0.10 },
            new UpgradeDef { Id = "plate", Name = "Wagon Plating", Blurb = "+10% plating on every wagon", Cost = 12, Growth = 1.16, Step = 0.10 },
            new UpgradeDef { Id = "rate", Name = "Drilled Crews", Blurb = "+2% rate of fire", Cost = 40, Growth = 1.22, Step = 0.02, Max = 50 },
            new UpgradeDef { Id = "crit", Name = "Marksmanship", Blurb = "+1% chance of a crit", Cost = 50, Growth = 1.24, Step = 0.01, Max = 40 },
            new UpgradeDef { Id = "critdmg", Name = "Heavy Loads", Blurb = "+10% crit damage", Cost = 60, Growth = 1.2, Step = 0.10 },
            new UpgradeDef { Id = "scrip", Name = "Bounty Office", Blurb = "+5% scrip from every raider", Cost = 25, Growth = 1.18, Step = 0.05 },
            new UpgradeDef { Id = "steel", Name = "Salvage Gang", Blurb = "+4% steel from every mile", Cost = 30, Growth = 1.19, Step = 0.04 },
            new UpgradeDef { Id = "start", Name = "Paymaster", Blurb = "+15 scrip at the start of a run", Cost = 20, Growth = 1.2, Step = 15 },
            new UpgradeDef { Id = "mend", Name = "Running Repairs", Blurb = "The hull mends 0.05% a second more", Cost = 80, Growth = 1.25, Step = 0.0005, Max = 40 },
            new UpgradeDef { Id = "whistle", Name = "Bigger Whistle", Blurb = "The whistle is ready 2 seconds sooner", Cost = 100, Growth = 1.3, Step = 2, Max = 15 },
            new UpgradeDef { Id = "slots", Name = "Longer Train", Blurb = "Couple on one more wagon", Cost = 150, Growth = 9, Step = 1, Max = MaxSlots - StartSlots },
            new UpgradeDef { Id = "speed", Name = "Regulator", Blurb = "A faster running speed to choose", Cost = 100, Growth = 30, Step = 1, Max = 3 },
        };

        public static readonly List<CardDef> Cards = new List<CardDef>
        {
            new CardDef { Id = "damage", Name = "Hot Barrels", Blurb = "+{0}% damage", Amount = 0.20 },
            new CardDef { Id = "rate", Name = "Oiled Actions", Blurb = "+{0}% rate of fire", Amount = 0.12 },
            new CardDef { Id = "plate", Name = "Riveted Plate", Blurb = "+{0}% plating, and it is made good now", Amount = 0.25 },
            new CardDef { Id = "hull", Name = "Boiler Patch", Blurb = "+{0}% hull, and it is made good now", Amount = 0.20 },
            new CardDef { Id = "scrip", Name = "Wanted Posters", Blurb = "+{0}% scrip", Amount = 0.30 },
            new CardDef { Id = "steel", Name = "Salvage Rights", Blurb = "+{0}% steel from this run", Amount = 0.12 },
            new CardDef { Id = "range", Name = "Long Sights", Blurb = "+{0}% range", Amount = 0.12 },
            new CardDef { Id = "crit", Name = "Lucky Streak", Blurb = "+{0}% chance of a crit", Amount = 0.06 },
            new CardDef { Id = "splash", Name = "Grapeshot", Blurb = "Shells burst {0}% wider", Amount = 0.25 },
            new CardDef { Id = "vents", Name = "Steam Vents", Blurb = "Raiders who strike the train are scalded for {0}% of the blow", Amount = 0.60 },
            new CardDef { Id = "mend", Name = "Field Fitters", Blurb = "Plating mends {0}% a second", Amount = 0.01 },
            new CardDef { Id = "slow", Name = "Sand on the Rails", Blurb = "Raiders come on {0}% slower", Amount = 0.10 },
        };

        public static CarDef Car(string id) { return Cars.Find(c => c.Id == id); }
        public static EnemyDef Enemy(string id) { return Enemies.Find(e => e.Id == id); }
        public static UpgradeDef Upgrade(string id) { return Upgrades.Find(u => u.Id == id); }
        public static CardDef Card(string id) { return Cards.Find(c => c.Id == id); }
        public static string Blurb(CardDef c, Rarity r)
        {
            double v = c.Amount * RarityMult[(int)r] * 100;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, c.Blurb, Math.Round(v, v < 10 ? 1 : 0));
        }
    }
}
