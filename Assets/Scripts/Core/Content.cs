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
        // The bosses' own ways of fighting.
        public int Escorts;             // war wagons that ride in with it
        public double Shield;           // share of every blow it shrugs off while an escort still rides
        public double Windup;           // seconds it aims (a red mark on the train) before a shot that hits the car and its neighbours
        public double Heal;             // share of their health it gives back each second to raiders near it
        public string Hint;             // what to do about it, under the mile
    }

    /// <summary>A choice the line offers between stations: take it or roll on (rolling on is always safe).</summary>
    public sealed class ChoiceDef
    {
        public string Id, Title, Body, Take, Leave;
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

    /// <summary>Something the Baron's seals buy for good. Seals come from his carriages: one for each brought down.</summary>
    public sealed class PerkDef
    {
        public string Id, Name, Blurb;
        public int Cost;                // seals for the first level; each next level costs that much more
        public int Max;
    }

    public enum Rarity { Common, Rare, Epic }

    /// <summary>A livery for the engine: its model and paint. Nothing else changes.</summary>
    public sealed class SkinDef
    {
        public string Id, Name, Blurb, Model;
        public double R = 1, G = 1, B = 1;      // the tint over the model's own colours
        public int Gems;                        // 0: not sold (the first one, or a gift)
        public int Engines;                     // given with this many new engines (0: never)
        public double WR = 1, WG = 1, WB = 1;   // the whole train wears it: the wagons' tint
        public double SR = 0.93, SG = 0.93, SB = 0.93;     // the smoke
        public double FR = 1.3, FG = 0.45, FB = 0.35;      // the pennant on the engine and the depot's bunting
        public string From;                     // how one not sold is earned (shown in the list)
    }

    /// <summary>A card offered at a station: it lasts for the run.</summary>
    public sealed class CardDef
    {
        public string Id, Name, Blurb;  // {0} in the blurb is the amount, as a percentage
        public double Amount;           // at Common; Rare is x1.75, Epic x3
        public bool Behaviour;          // changes how the train fights (with a cost), not just a number; once a run
        public string Needs;            // only offered when this wagon is coupled on
        public string Icon;             // its picture on the card
    }

    public static partial class Content
    {
        // ---- tuning ----
        public const double Step = 0.05;            // seconds of the fight per step
        public const double CarLength = 6;          // metres between one car's middle and the next
        public const double MileSeconds = 22;       // a mile takes this long to roll
        public const double SpawnSeconds = 15;      // the mile's raiders arrive over this long
        public const int StationEvery = 10;         // miles between stations (a boss rides on each of those miles)
        public const double HullBase = 150, PlateBase = 60;
        public const double HpPerMile = 1.09, DamagePerMile = 1.05, BountyPerMile = 1.035, SteelPerMile = 1.055;
        public const double SteelPerMileBase = 13, BossSteel = 6;
        public const double FieldCost = 10, FieldGrowth = 1.36, FieldPower = 1.22;
        public const double CarCost = 20, CarGrowth = 1.17, CarPower = 1.10;
        public const double StartScrip = 20;
        public const double CritBase = 0.05, CritDamageBase = 2;
        public const double WhistleCooldown = 60, WhistleRange = 13, WhistleStun = 3, WhistlePush = 7;
        public const int StartSlots = 2, MaxSlots = 8;
        public const int EarlyRiders = 8;          // extra threat on mile 1, one less each mile after (gone by mile 8)
        public const int Pack = 3;                  // outriders ride in together, up to this many
        public const int MaxRaiders = 120;         // on the field at once: more wait their turn (and the mile waits for them)
        public const double SpawnRing = 30;         // how far out raiders appear
        public const double ReviveHull = 0.5;
        public const double StationHull = 0.3;      // share of the hull a station puts back
        public const double DepotHours = 8;         // the depot's patrols earn for this long unattended
        public const double DepotShare = 0.35;      // of the steel per hour a played run on that line would bring
        public static readonly double[] RarityMult = { 1, 1.75, 3 };
        public static readonly double[] TacticMult = { 1, 1.3, 1.6 };     // a card that changes how the train fights grows less with rarity
        public static double Mult(CardDef c, int rarity) { return (c.Behaviour ? TacticMult : RarityMult)[rarity]; }
        public static readonly double[] RarityOdds = { 0.72, 0.23, 0.05 };
        public static readonly double[] Speeds = { 1, 1.5, 2, 3 };
        // A new engine: charters for the steel this engine has earned (the first at CharterSteel, then by its square root),
        // each worth CharterBonus more steel and damage, for good.
        public const double CharterSteel = 100000, CharterBonus = 0.10;

        public static readonly List<CarDef> Cars = new List<CarDef>
        {
            new CarDef { Id = "engine", Name = "Deck Gun", Blurb = "The engine's own gun. Never leaves the front.", Damage = 6, Interval = 0.8, Range = 13 },
            new CarDef { Id = "gatling", Name = "Gatling Car", Blurb = "A storm of small rounds: first at whoever is hitting the train, then the closest.", Damage = 4, Interval = 0.2, Range = 12 },
            new CarDef { Id = "cannon", Name = "Cannon Car", Blurb = "A slow shell that bursts among them.", Damage = 28, Interval = 2.2, Range = 17, Splash = 3.5, Flight = 0.5, Steel = 60, Mile = 6 },
            new CarDef { Id = "supply", Name = "Supply Car", Blurb = "The wagons either side of it fire a quarter faster.", Aim = Aim.None, Haste = 0.25, Steel = 250, Mile = 12 },
            new CarDef { Id = "rifle", Name = "Sharpshooter Car", Blurb = "Picks out the toughest raider from far off. Crits often.", Aim = Aim.Strongest, Damage = 48, Interval = 2.4, Range = 24, Crit = 0.2, Steel = 900, Mile = 18 },
            new CarDef { Id = "armour", Name = "Armoured Car", Blurb = "Three times the plating, and the wagons either side take a third less.", Aim = Aim.None, Plating = 3, Shield = 0.33, Steel = 3000, Mile = 25 },
            new CarDef { Id = "flame", Name = "Furnace Car", Blurb = "Burns everything that gets close.", Aim = Aim.Around, Damage = 5, Interval = 0.25, Range = 7, Steel = 9000, Mile = 32 },
            new CarDef { Id = "mortar", Name = "Mortar Car", Blurb = "Lobs a heavy shell at the far ones. Can't hit close in.", Aim = Aim.Farthest, Damage = 70, Interval = 4, Range = 28, MinRange = 8, Splash = 5.5, Flight = 1.2, Steel = 30000, Mile = 40 },
            new CarDef { Id = "repair", Name = "Fitters' Car", Blurb = "Fitters put plating back on the worst-hit wagon as you roll.", Aim = Aim.None, Mend = 0.03, Steel = 90000, Mile = 50 },
            new CarDef { Id = "dynamo", Name = "Dynamo Car", Blurb = "An arc that jumps from raider to raider.", Damage = 16, Interval = 1, Range = 13, Chain = 3, Steel = 300000, Mile = 60 },
        };

        public static readonly List<EnemyDef> Enemies = new List<EnemyDef>
        {
            new EnemyDef { Id = "outrider", Name = "Outrider", Hp = 14, Speed = 6, Damage = 4, Interval = 1, Bounty = 1, Threat = 1, Mile = 1 },
            new EnemyDef { Id = "sledge", Name = "Sledgeman", Hp = 60, Speed = 2.6, Damage = 14, Interval = 1.6, Bounty = 3, Threat = 3, Mile = 4 },
            new EnemyDef { Id = "gunwagon", Name = "Gun Wagon", Hp = 32, Speed = 3.5, Damage = 5, Interval = 1.5, Range = 8, Bounty = 3, Threat = 3, Mile = 7 },
            new EnemyDef { Id = "powder", Name = "Powder Monkey", Hp = 18, Speed = 7.5, Damage = 40, Interval = 1, Bounty = 2, Threat = 2, Mile = 12, Blast = true },
            new EnemyDef { Id = "handcar", Name = "Handcar Gang", Hp = 45, Speed = 9, Damage = 8, Interval = 0.9, Bounty = 4, Threat = 3, Mile = 16, Rail = true },
            new EnemyDef { Id = "warwagon", Name = "War Wagon", Hp = 320, Speed = 2, Damage = 22, Interval = 2, Range = 8, Bounty = 16, Threat = 12, Mile = 22 },
            new EnemyDef { Id = "boss", Name = "The Baron's Carriage", Hp = 230, Speed = 1.6, Damage = 20, Interval = 2, Range = 7, Bounty = 50, Threat = 0, Mile = 10, Boss = true, Hint = "tap it to point every gun at it" },
            new EnemyDef { Id = "convoy", Name = "The Baron's Convoy", Hp = 200, Speed = 1.5, Damage = 18, Interval = 2, Range = 7, Bounty = 55, Threat = 0, Mile = 20, Boss = true, Escorts = 3, Shield = 0.75, Hint = "its escort shields it: bring the war wagons down first" },
            new EnemyDef { Id = "longgun", Name = "The Long Gun", Hp = 190, Speed = 1.4, Damage = 30, Interval = 6.5, Range = 11, Bounty = 55, Threat = 0, Mile = 30, Boss = true, Windup = 2.4, Hint = "sound the WHISTLE when it marks a wagon to spoil the shot" },
            new EnemyDef { Id = "mender", Name = "The Mending Wagon", Hp = 210, Speed = 1.5, Damage = 10, Interval = 2, Range = 9, Bounty = 55, Threat = 0, Mile = 40, Boss = true, Heal = 0.05, Hint = "it mends the raiders round it: bring it down first" },
        };

        /// <summary>The bosses in the order the stations bring them, over and over (the first station is always the carriage).</summary>
        public static readonly string[] Bosses = { "boss", "convoy", "longgun", "mender" };

        public static readonly List<ChoiceDef> Choices = new List<ChoiceDef>
        {
            new ChoiceDef { Id = "fitter", Title = "A STRANDED FITTER", Body = "A fitter waves from a broken handcar. Stopping costs a few seconds out here; she'd work on two of your guns.", Take = "PICK HER UP", Leave = "ROLL ON" },
            new ChoiceDef { Id = "freight", Title = "FREIGHT AT THE SIDING", Body = "Freight cars stuck at a siding, raiders closing in. Defend them: half as many raiders again, and +60% steel this mile and the next.", Take = "DEFEND IT", Leave = "ROLL ON" },
            new ChoiceDef { Id = "gulch", Title = "THE GULCH ROUTE", Body = "A short cut through a gulch: double steel and scrip for two miles, but the raiders there hit 30% harder.", Take = "TAKE THE GULCH", Leave = "STAY ON THE LINE" },
        };
        public const double ChoiceSeconds = 14;     // left alone, the train rolls on

        /// <summary>Workshop levels at which a wagon looks heavier: plating, then brass, then the works.</summary>
        public static readonly int[] TierLevels = { 10, 25, 50 };

        public static readonly List<RouteDef> Routes = new List<RouteDef>
        {
            new RouteDef { Id = "prairie", Name = "The Prairie Line", Blurb = "Open grass and a long way between towns.", Hp = 1, Damage = 1, Steel = 1 },
            new RouteDef { Id = "badlands", Name = "Badlands Cut", Blurb = "Red rock, dry gulches, and more of them.", Hp = 12, Damage = 5, Steel = 6, Opens = 90 },
            new RouteDef { Id = "pass", Name = "The High Pass", Blurb = "Snow on the sleepers and raiders in the pines.", Hp = 160, Damage = 28, Steel = 40, Opens = 120 },
            new RouteDef { Id = "salt", Name = "Salt Flats Run", Blurb = "Nowhere to hide, for anybody.", Hp = 2400, Damage = 170, Steel = 280, Opens = 150 },
            new RouteDef { Id = "canyon", Name = "Black Canyon", Blurb = "The Baron's own country.", Hp = 40000, Damage = 1100, Steel = 2000, Opens = 180 },
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
            new UpgradeDef { Id = "speed", Name = "Regulator", Blurb = "A faster running speed to choose", Cost = 60, Growth = 25, Step = 1, Max = 3 },
        };

        public static readonly List<PerkDef> Perks = new List<PerkDef>
        {
            new PerkDef { Id = "quarter", Name = "Quartermaster", Blurb = "Every car starts a run one field level higher", Cost = 3, Max = 5 },
            new PerkDef { Id = "spare", Name = "Spare Plating", Blurb = "A knocked-out wagon comes back by itself, sooner each level", Cost = 4, Max = 3 },
            new PerkDef { Id = "hand", Name = "An Extra Hand", Blurb = "One more card to choose from at every station", Cost = 10, Max = 2 },
            new PerkDef { Id = "lucky", Name = "A Lucky Table", Blurb = "Rare and Epic cards turn up more often", Cost = 5, Max = 5 },
            new PerkDef { Id = "flags", Name = "Signal Flags", Blurb = "The whistle stuns for a second longer", Cost = 3, Max = 3 },
            new PerkDef { Id = "wind", Name = "Second Wind", Blurb = "Back on the rails with a tenth more hull", Cost = 4, Max = 4 },
            new PerkDef { Id = "patrol", Name = "More Patrols", Blurb = "The depot brings home a tenth more steel", Cost = 2, Max = 10 },
        };
        public const double SpareSeconds = 26, SpareLess = 6;      // a knocked-out wagon's wait, and what each level of Spare Plating takes off

        public static PerkDef Perk(string id) { return Perks.Find(p => p.Id == id); }

        public static readonly List<SkinDef> Skins = new List<SkinDef>
        {
            new SkinDef { Id = "", Name = "Iron Horse", Blurb = "The engine the line gave you: plain iron and a lot of rivets.", Model = "loco_b", R = 0.92, G = 0.86, B = 0.86 },
            new SkinDef { Id = "charter", Name = "Charter Green", Blurb = "Painted for the line's new charter. Given with your first new engine.", Model = "loco_b", R = 0.62, G = 1.05, B = 0.66, Engines = 1, WR = 0.85, WG = 1.05, WB = 0.85, FR = 0.4, FG = 1.2, FB = 0.5 },
            new SkinDef { Id = "belle", Name = "Prairie Belle", Blurb = "An older engine with a tall stack, kept bright by her crew, and wagons to match.", Model = "loco", R = 1, G = 1, B = 1, Gems = 120, WR = 1.08, WG = 1.0, WB = 0.88, FR = 1.3, FG = 0.95, FB = 0.4 },
            new SkinDef { Id = "brass", Name = "Brass Baron", Blurb = "Polished brass from the buffers to the bell, on every wagon. Taken from the Baron himself.", Model = "loco_b", R = 1.3, G = 1.02, B = 0.5, Gems = 200, WR = 1.2, WG = 1.02, WB = 0.7, FR = 1.4, FG = 1.1, FB = 0.4 },
            new SkinDef { Id = "black", Name = "Black Diamond", Blurb = "A heavy coal hauler, black as the night line; the wagons in soot.", Model = "loco_c", R = 0.55, G = 0.55, B = 0.62, Gems = 300, WR = 0.62, WG = 0.62, WB = 0.7, SR = 0.42, SG = 0.42, SB = 0.45, FR = 0.3, FG = 0.3, FB = 0.35 },
            new SkinDef { Id = "frontier", Name = "Frontier Red", Blurb = "Barn red from end to end, with cream trim.", Model = "loco_b", R = 1.25, G = 0.55, B = 0.45, Gems = 250, WR = 1.2, WG = 0.72, WB = 0.62, FR = 1.4, FG = 1.3, FB = 1.1 },
            new SkinDef { Id = "midnight", Name = "Midnight Express", Blurb = "Deep blue and silver, with pale blue steam.", Model = "loco_c", R = 0.5, G = 0.62, B = 1.15, Gems = 400, WR = 0.72, WG = 0.82, WB = 1.15, SR = 0.78, SG = 0.86, SB = 1.0, FR = 0.6, FG = 0.8, FB = 1.4 },
            new SkinDef { Id = "gunmetal", Name = "Gunmetal", Blurb = "Blued steel, for a gunner who has fired a great many shells.", Model = "loco_c", R = 0.7, G = 0.75, B = 0.85, WR = 0.75, WG = 0.8, WB = 0.9, FR = 0.9, FG = 0.9, FB = 1.0, From = "MASTERY" },
            new SkinDef { Id = "racer", Name = "Yellow Racer", Blurb = "Light, bright and quick-looking: for those who travel light.", Model = "loco", R = 1.35, G = 1.15, B = 0.4, WR = 1.25, WG = 1.12, WB = 0.6, FR = 1.4, FG = 0.5, FB = 0.3, From = "MASTERY" },
            new SkinDef { Id = "baron", Name = "Baron's Bane", Blurb = "Black and gold, painted with the Baron's own colours turned against him.", Model = "loco_b", R = 0.5, G = 0.45, B = 0.45, WR = 0.6, WG = 0.55, WB = 0.5, SR = 0.55, SG = 0.5, SB = 0.5, FR = 1.4, FG = 1.1, FB = 0.3, From = "MASTERY" },
            new SkinDef { Id = "trial", Name = "Proving Ground", Blurb = "Test-track white and red. Mile 40 in a weekly trial.", Model = "loco_b", R = 1.3, G = 1.25, B = 1.2, WR = 1.25, WG = 1.2, WB = 1.15, FR = 1.4, FG = 0.35, FB = 0.3, From = "WEEKLY TRIAL" },
            new SkinDef { Id = "pathfinder", Name = "Pathfinder", Blurb = "Expedition green and copper. The last step of an expedition.", Model = "loco", R = 0.55, G = 0.9, B = 0.75, WR = 0.75, WG = 1.0, WB = 0.85, FR = 1.3, FG = 0.75, FB = 0.4, From = "EXPEDITION" },
        };
        public static SkinDef Skin(string id) { return Skins.Find(k => k.Id == (id ?? "")); }

        public static readonly List<CardDef> Cards = new List<CardDef>
        {
            new CardDef { Id = "damage", Name = "Hot Barrels", Blurb = "+{0}% damage", Amount = 0.20, Icon = "hammer" },
            new CardDef { Id = "rate", Name = "Oiled Actions", Blurb = "+{0}% rate of fire", Amount = 0.12, Icon = "gear" },
            new CardDef { Id = "plate", Name = "Riveted Plate", Blurb = "+{0}% plating, and it is made good now", Amount = 0.25, Icon = "box" },
            new CardDef { Id = "hull", Name = "Boiler Patch", Blurb = "+{0}% hull, and it is made good now", Amount = 0.20, Icon = "lock" },
            new CardDef { Id = "scrip", Name = "Wanted Posters", Blurb = "+{0}% scrip", Amount = 0.30, Icon = "coins" },
            new CardDef { Id = "steel", Name = "Salvage Rights", Blurb = "+{0}% steel from this run", Amount = 0.12, Icon = "sack" },
            new CardDef { Id = "range", Name = "Long Sights", Blurb = "+{0}% range", Amount = 0.12, Icon = "compass" },
            new CardDef { Id = "crit", Name = "Lucky Streak", Blurb = "+{0}% chance of a crit", Amount = 0.06, Icon = "star" },
            new CardDef { Id = "splash", Name = "Grapeshot", Blurb = "Shells burst {0}% wider", Amount = 0.25, Icon = "target" },
            new CardDef { Id = "vents", Name = "Steam Vents", Blurb = "Raiders who strike the train up close are scalded for {0}% of the blow", Amount = 0.60, Icon = "sound" },
            new CardDef { Id = "mend", Name = "Field Fitters", Blurb = "Plating mends {0}% a second", Amount = 0.01, Icon = "wrench" },
            new CardDef { Id = "slow", Name = "Sand on the Rails", Blurb = "Raiders come on {0}% slower", Amount = 0.10, Icon = "clock" },
            // Cards that change how the train fights, each with a price.
            new CardDef { Id = "cluster", Name = "Split Shells", Blurb = "Shells split into three small blasts, each {0}% of the shell. The shell hits 40% less.", Amount = 0.35, Behaviour = true, Needs = "splash", Icon = "gift" },
            new CardDef { Id = "arc", Name = "Arc Through Smoke", Blurb = "Arcs jump {0}% further and lose nothing through burning raiders. First strike 20% weaker.", Amount = 0.6, Behaviour = true, Needs = "dynamo", Icon = "bolt" },
            new CardDef { Id = "recharge", Name = "Steam Recharge", Blurb = "The whistle reloads every gun and they fire {0}% faster for 5 s. Whistle ready 15 s later.", Amount = 0.5, Behaviour = true, Icon = "bell" },
            new CardDef { Id = "decoy", Name = "Iron Decoy", Blurb = "Raiders go for the last wagon: {0}% more plating, but it fires 40% slower.", Amount = 1.0, Behaviour = true, Icon = "user" },
            new CardDef { Id = "ricochet", Name = "Ricochet", Blurb = "A Gatling kill carries on into the next raider for {0}% of the shot. Gatling 15% weaker.", Amount = 0.5, Behaviour = true, Needs = "gatling", Icon = "medal" },
            new CardDef { Id = "glass", Name = "Glass Cannon", Blurb = "+{0}% damage from every gun, but a quarter less hull.", Amount = 0.4, Behaviour = true, Icon = "crown" },
        };

        public static CarDef Car(string id) { return Cars.Find(c => c.Id == id); }
        public static EnemyDef Enemy(string id) { return Enemies.Find(e => e.Id == id); }
        public static UpgradeDef Upgrade(string id) { return Upgrades.Find(u => u.Id == id); }
        public static CardDef Card(string id) { return Cards.Find(c => c.Id == id); }
        public static ChoiceDef Choice(string id) { return Choices.Find(c => c.Id == id); }
        public static string Blurb(CardDef c, Rarity r)
        {
            double v = c.Amount * Mult(c, (int)r) * 100;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, c.Blurb, Math.Round(v, v < 10 ? 1 : 0));
        }
    }
}
