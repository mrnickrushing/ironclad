using System;
using System.Collections.Generic;

namespace Ironclad.Core
{
    public sealed class TaskDef
    {
        public string Id, Text;         // {0} in the text is the goal
        public double Goal; public int Gems;
        public Func<Sim, double> Stat;
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

        public static readonly List<TaskDef> Tasks = new List<TaskDef>
        {
            new TaskDef { Id = "kills", Text = "Turn back {0} raiders", Goal = 250, Gems = 5, Stat = s => s.State.kills },
            new TaskDef { Id = "miles", Text = "Roll {0} miles", Goal = 30, Gems = 5, Stat = s => s.State.miles },
            new TaskDef { Id = "cards", Text = "Take {0} station cards", Goal = 2, Gems = 5, Stat = s => s.State.cardsTaken },
        };

        /// <summary>The day's counts start when the day does.</summary>
        void RollTasks()
        {
            if (State.taskDay == Today && State.taskBase.Count == Tasks.Count) return;
            State.taskDay = Today; State.taskClaimed = 0;
            State.taskBase.Clear();
            foreach (var t in Tasks) State.taskBase.Add(t.Stat(this));
        }

        public double TaskProgress(int index) { RollTasks(); return Math.Min(Tasks[index].Goal, Tasks[index].Stat(this) - State.taskBase[index]); }
        public bool TaskDone(int index) { return TaskProgress(index) >= Tasks[index].Goal; }
        public bool TaskClaimed(int index) { RollTasks(); return (State.taskClaimed & (1 << index)) != 0; }

        public bool ClaimTask(int index)
        {
            if (index < 0 || index >= Tasks.Count || !TaskDone(index) || TaskClaimed(index)) return false;
            State.taskClaimed |= 1 << index;
            State.gems += Tasks[index].Gems;
            Touch();
            return true;
        }

        /// <summary>How many things are waiting on the TODAY page.</summary>
        public int TodayReady
        {
            get
            {
                int n = GiftReady ? 1 : 0;
                for (int i = 0; i < Tasks.Count; i++) if (TaskDone(i) && !TaskClaimed(i)) n++;
                return n;
            }
        }
    }
}
