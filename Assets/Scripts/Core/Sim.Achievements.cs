using System;
using System.Collections.Generic;

namespace Ironclad.Core
{
    public sealed class AchievementDef
    {
        public string Id, Name, Blurb;
        public int Gems;
        public double Goal;
        public Func<Sim, double> Have;
    }

    /// <summary>Awards: each pays gems once, claimed from the AWARDS page, and is reported to Game Center.</summary>
    public sealed partial class Sim
    {
        public static readonly List<AchievementDef> Achievements = new List<AchievementDef>
        {
            new AchievementDef { Id = "mile10", Name = "First Station", Blurb = "Reach mile 10 on any line", Gems = 10, Goal = 10, Have = s => s.BestMile },
            new AchievementDef { Id = "mile25", Name = "Out of Sight of Town", Blurb = "Reach mile 25 on any line", Gems = 15, Goal = 25, Have = s => s.BestMile },
            new AchievementDef { Id = "mile50", Name = "Half a Hundred", Blurb = "Reach mile 50 on any line", Gems = 25, Goal = 50, Have = s => s.BestMile },
            new AchievementDef { Id = "mile100", Name = "The Century", Blurb = "Reach mile 100 on any line", Gems = 50, Goal = 100, Have = s => s.BestMile },
            new AchievementDef { Id = "badlands", Name = "Into the Badlands", Blurb = "Open Badlands Cut", Gems = 20, Goal = 1, Have = s => s.RouteOpen(1) ? 1 : 0 },
            new AchievementDef { Id = "pass", Name = "Over the Top", Blurb = "Open the High Pass", Gems = 30, Goal = 1, Have = s => s.RouteOpen(2) ? 1 : 0 },
            new AchievementDef { Id = "canyon", Name = "The Baron's Country", Blurb = "Open Black Canyon", Gems = 60, Goal = 1, Have = s => s.RouteOpen(4) ? 1 : 0 },
            new AchievementDef { Id = "kills1k", Name = "Line Holder", Blurb = "Turn back 1,000 raiders", Gems = 10, Goal = 1000, Have = s => s.State.kills },
            new AchievementDef { Id = "kills25k", Name = "Scourge of the Frontier", Blurb = "Turn back 25,000 raiders", Gems = 30, Goal = 25000, Have = s => s.State.kills },
            new AchievementDef { Id = "boss5", Name = "Carriage Breaker", Blurb = "Bring down 5 of the Baron's carriages", Gems = 15, Goal = 5, Have = s => s.State.bosses },
            new AchievementDef { Id = "boss50", Name = "The Baron's Bane", Blurb = "Bring down 50 of the Baron's carriages", Gems = 40, Goal = 50, Have = s => s.State.bosses },
            new AchievementDef { Id = "wagons4", Name = "Rolling Arsenal", Blurb = "Build 4 kinds of wagon", Gems = 15, Goal = 4, Have = s => s.State.cars.Count - 1 },
            new AchievementDef { Id = "wagons9", Name = "Every Plan in the Drawer", Blurb = "Build all 9 kinds of wagon", Gems = 50, Goal = 9, Have = s => s.State.cars.Count - 1 },
            new AchievementDef { Id = "long", Name = "The Long Train", Blurb = "Couple on 8 wagons", Gems = 40, Goal = 8, Have = s => s.State.train.Count },
            new AchievementDef { Id = "cards30", Name = "A Full Hand", Blurb = "Take 30 station cards", Gems = 15, Goal = 30, Have = s => s.State.cardsTaken },
            new AchievementDef { Id = "whistle50", Name = "Full Head of Steam", Blurb = "Sound the whistle 50 times", Gems = 10, Goal = 50, Have = s => s.State.whistles },
            new AchievementDef { Id = "runs100", Name = "Regular Service", Blurb = "Set off on 100 runs", Gems = 20, Goal = 100, Have = s => s.State.runs },
        };

        public bool AchievementDone(AchievementDef a) { return a.Have(this) >= a.Goal; }
        public bool AchievementClaimed(AchievementDef a) { return State.achieved.Contains(a.Id); }
        public int AchievementsReady { get { int n = 0; foreach (var a in Achievements) if (AchievementDone(a) && !AchievementClaimed(a)) n++; return n; } }

        public bool ClaimAchievement(string id)
        {
            var a = Achievements.Find(x => x.Id == id);
            if (a == null || !AchievementDone(a) || AchievementClaimed(a)) return false;
            State.achieved.Add(a.Id);
            State.gems += a.Gems;
            Touch();
            return true;
        }

        /// <summary>The miles reached on every line, added up: how far along a save is (shown when iCloud offers another device's).</summary>
        public int Stars { get { int n = 0; foreach (int m in State.best) n += m; return n; } }

        public void Rename(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > 16) name = name.Substring(0, 16);
            State.playerName = name;
            Touch();
        }
    }
}
