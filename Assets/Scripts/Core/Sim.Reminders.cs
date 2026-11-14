using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironclad.Core
{
    /// <summary>A local notification to schedule: seconds from now, and what it says.</summary>
    public sealed class Reminder { public double In; public string Title, Body; }

    public sealed partial class Sim
    {
        /// <summary>
        /// What is worth a notification once the app is closed: only things that have happened on the line (never an
        /// offer). At most two, none at night.
        /// </summary>
        public List<Reminder> ReminderPlan()
        {
            var all = new List<Reminder>();
            if (DepotRate > 0 && State.depotAt > 0)
                all.Add(new Reminder { In = Math.Max(600, DepotCap - DepotSeconds), Title = "The depot is full", Body = "Your patrols have brought home all the steel the yard can hold. Come and collect." });
            if (State.run != null && !RunEnded)
                all.Add(new Reminder { In = 4 * 3600, Title = "The train is waiting", Body = "Mile " + State.run.mile + " on " + Route.Name + ": steam is up and the crews are at their guns." });
            all.Add(new Reminder { In = 24 * 3600, Title = "The line needs holding", Body = "Today's gift and a free double are waiting at the depot." });

            // Two at most, never two within ten minutes, and none in the night: one due between 22:00 and 08:00 waits
            // for the morning.
            var plan = new List<Reminder>();
            foreach (var r in all.OrderBy(r => r.In))
            {
                double local = ((RealNow() + UtcOffset + r.In) % 86400 + 86400) % 86400;
                if (local >= 22 * 3600) r.In += 86400 - local + 8 * 3600;
                else if (local < 8 * 3600) r.In += 8 * 3600 - local;
            }
            foreach (var r in all.OrderBy(r => r.In))
            {
                if (plan.Count >= 2) break;
                if (plan.Count > 0 && r.In - plan[plan.Count - 1].In < 600) continue;
                plan.Add(r);
            }
            return plan;
        }
    }
}
