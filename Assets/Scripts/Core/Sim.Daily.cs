using System;
using System.Collections.Generic;

namespace Ironclad.Core
{
    public sealed class TaskDef
    {
        public string Id, Text;         // {0} in the text is the goal
        public double Goal; public int Gems;
        public Func<Sim, double> Stat;
        public string Car;              // only once this wagon is built
        public bool Strategy;           // asks for a way of playing, not just more of it
    }

    /// <summary>
    /// Something for every day: a gift (a week of them, the seventh the best; a missed day loses nothing, the week
    /// just waits) and three tasks counted from the day's start.
    /// </summary>
    public sealed partial class Sim
    {
        public sealed class GiftDef { public int Gems; public double Hours; }
        /// <summary>The week of gifts: gems, or hours of the depot's steel.</summary>
        public static readonly GiftDef[] Gifts =
        {
            new GiftDef { Gems = 5 }, new GiftDef { Hours = 1 }, new GiftDef { Gems = 10 }, new GiftDef { Hours = 2 },
            new GiftDef { Gems = 15 }, new GiftDef { Hours = 4 }, new GiftDef { Gems = 30 },
        };

        public bool GiftReady { get { return State.giftDay != Today; } }
        /// <summary>Which of the week's gifts is next (0..6).</summary>
        public int GiftIndex { get { return State.giftStreak % Gifts.Length; } }
        public double GiftSteel(GiftDef g) { return g.Hours <= 0 ? 0 : Math.Max(60 * g.Hours, Math.Floor(DepotRate * g.Hours)); }

        public bool ClaimGift()
        {
            if (!GiftReady) return false;
            var g = Gifts[GiftIndex];
            double steel = GiftSteel(g);
            State.gems += g.Gems; State.steel += steel; State.lifetime += steel;
            State.giftDay = Today; State.giftStreak++;
            Touch();
            return true;
        }

        /// <summary>Every task there is. Three are drawn each day from those the train can do (a wagon's own task once it
        /// is built), shuffled by the day, so the days don't repeat in a pattern.</summary>
        public static readonly List<TaskDef> Tasks = new List<TaskDef>
        {
            new TaskDef { Id = "kills", Text = "Turn back {0} raiders", Goal = 250, Gems = 5, Stat = s => s.State.kills },
            new TaskDef { Id = "miles", Text = "Roll {0} miles", Goal = 30, Gems = 5, Stat = s => s.State.miles },
            new TaskDef { Id = "cards", Text = "Take {0} station cards", Goal = 2, Gems = 5, Stat = s => s.State.cardsTaken },
            new TaskDef { Id = "whistle", Text = "Sound the whistle {0} times", Goal = 5, Gems = 5, Stat = s => s.State.whistles },
            new TaskDef { Id = "boss", Text = "Bring down {0} of the Baron's bosses", Goal = 2, Gems = 5, Stat = s => s.State.bosses },
            new TaskDef { Id = "runs", Text = "Finish {0} runs", Goal = 3, Gems = 5, Stat = s => s.State.runs },
            new TaskDef { Id = "crits", Text = "Land {0} crits", Goal = 120, Gems = 5, Stat = s => s.State.crits },
            new TaskDef { Id = "field", Text = "Buy {0} field levels with scrip", Goal = 25, Gems = 5, Stat = s => s.State.fieldBuys },
            new TaskDef { Id = "clean", Text = "Reach {0} stations without a wagon knocked out on the way", Goal = 2, Gems = 8, Stat = s => s.State.stationsClean, Strategy = true },
            new TaskDef { Id = "light", Text = "Reach a station pulling two wagons or fewer", Goal = 1, Gems = 8, Stat = s => s.State.stationsLight, Strategy = true },
            new TaskDef { Id = "k_gatling", Text = "Turn back {0} raiders with the Gatling Car", Goal = 150, Gems = 5, Car = "gatling", Stat = s => s.CarKills("gatling") },
            new TaskDef { Id = "k_cannon", Text = "Turn back {0} raiders with the Cannon Car", Goal = 80, Gems = 5, Car = "cannon", Stat = s => s.CarKills("cannon") },
            new TaskDef { Id = "k_rifle", Text = "Turn back {0} raiders with the Sharpshooter Car", Goal = 40, Gems = 5, Car = "rifle", Stat = s => s.CarKills("rifle") },
            new TaskDef { Id = "k_flame", Text = "Turn back {0} raiders with the Furnace Car", Goal = 100, Gems = 6, Car = "flame", Stat = s => s.CarKills("flame") },
            new TaskDef { Id = "k_mortar", Text = "Turn back {0} raiders with the Mortar Car", Goal = 60, Gems = 6, Car = "mortar", Stat = s => s.CarKills("mortar") },
            new TaskDef { Id = "k_dynamo", Text = "Turn back {0} raiders with the Dynamo Car", Goal = 120, Gems = 6, Car = "dynamo", Stat = s => s.CarKills("dynamo") },
        };
        public const int TasksADay = 3;

        public double CarKills(string type) { var t = State.carKills.Find(x => x.id == type); return t == null ? 0 : t.v; }

        public static TaskDef Task(string id) { return Tasks.Find(t => t.Id == id); }

        /// <summary>The task in this place today (0..2).</summary>
        public TaskDef TaskAt(int place) { RollTasks(); return Task(State.taskIds[place]); }

        /// <summary>The day's three: a shuffle of the tasks this train can do, seeded by the day (never two the same,
        /// at most one of a wagon's own tasks, and a strategy task most days once the first station has been reached).</summary>
        public List<string> TasksFor(long day)
        {
            var can = Tasks.FindAll(t => t.Car == null || Built(t.Car));
            if (BestMile < Content.StationEvery) can.RemoveAll(t => t.Strategy || t.Id == "boss" || t.Id == "cards");
            uint h = (uint)(day * 2654435761L) ^ 0x9E3779B9u;
            Func<int, int> next = n => { h ^= h << 13; h ^= h >> 17; h ^= h << 5; return (int)(h % (uint)n); };
            for (int i = can.Count - 1; i > 0; i--) { int j = next(i + 1); var t = can[i]; can[i] = can[j]; can[j] = t; }
            var pick = new List<string>(); bool car = false;
            foreach (var t in can)
            {
                if (pick.Count == TasksADay) break;
                if (t.Car != null) { if (car) continue; car = true; }
                pick.Add(t.Id);
            }
            return pick;
        }

        /// <summary>The day's counts start when the day does.</summary>
        void RollTasks()
        {
            if (State.taskDay == Today && State.taskBase.Count == TasksADay && State.taskIds.Count == TasksADay && State.taskIds.TrueForAll(id => Task(id) != null)) return;
            State.taskDay = Today; State.taskClaimed = 0;
            State.taskIds = TasksFor(Today);
            State.taskBase.Clear();
            for (int i = 0; i < TasksADay; i++) State.taskBase.Add(Task(State.taskIds[i]).Stat(this));
        }

        public double TaskProgress(int place) { RollTasks(); return Math.Max(0, Math.Min(TaskAt(place).Goal, TaskAt(place).Stat(this) - State.taskBase[place])); }
        public bool TaskDone(int place) { return TaskProgress(place) >= TaskAt(place).Goal; }
        public bool TaskClaimed(int place) { RollTasks(); return (State.taskClaimed & (1 << place)) != 0; }

        public bool ClaimTask(int place)
        {
            if (place < 0 || place >= TasksADay || !TaskDone(place) || TaskClaimed(place)) return false;
            State.taskClaimed |= 1 << place;
            State.gems += TaskAt(place).Gems;
            Touch();
            return true;
        }

        /// <summary>How many things are waiting on the TODAY page.</summary>
        public int TodayReady
        {
            get
            {
                int n = GiftReady ? 1 : 0;
                for (int i = 0; i < TasksADay; i++) if (TaskDone(i) && !TaskClaimed(i)) n++;
                return n;
            }
        }
    }
}
