using System;

namespace Ironclad.Core
{
    /// <summary>
    /// The depot: while the game is closed, patrol trains work the lines already crossed and bring steel home. It pays a
    /// share of what playing that line would, for a limited time, so coming back is worth it and staying away isn't punished.
    /// </summary>
    public sealed partial class Sim
    {
        /// <summary>Steel an hour from the patrols: the best any open line gives.</summary>
        public double DepotRate
        {
            get
            {
                double best = 0;
                for (int i = 0; i < Content.Routes.Count; i++)
                {
                    int miles = State.best[i];
                    if (miles <= 0) continue;
                    double total = 0;
                    for (int m = 1; m <= miles; m++) total += MileSteel(Content.Routes[i], m);
                    best = Math.Max(best, total / (miles * Content.MileSeconds) * 3600);
                }
                double mult = (1 + Content.Upgrade("steel").Step * UpgradeLevel("steel")) * (Owns("ironclad.doubler") ? 2 : 1);
                return best * Content.DepotShare * mult * (1 + 0.1 * PerkLevel("patrol"));
            }
        }

        public double DepotCap { get { return (Content.DepotHours + (Owns("ironclad.depot") ? 4 : 0)) * 3600; } }

        /// <summary>Seconds of patrols waiting to be collected.</summary>
        public double DepotSeconds { get { return State.depotAt <= 0 ? 0 : Math.Max(0, Math.Min(DepotCap, RealNow() - State.depotAt)); } }
        public double DepotSteel { get { return Math.Floor(DepotRate * DepotSeconds / 3600); } }
        public bool DoubleFree { get { return State.doubleDay != Today; } }

        /// <summary>Banks what the patrols brought. <paramref name="doubled"/> uses the day's free double.</summary>
        public double CollectDepot(bool doubled = false)
        {
            if (doubled && !DoubleFree) doubled = false;
            return PayDepot(doubled ? 2 : 1, doubled);
        }

        double PayDepot(double mult, bool usesFree)
        {
            double steel = DepotSteel * mult;
            if (steel <= 0) return 0;
            if (usesFree) State.doubleDay = Today;
            State.steel += steel; State.lifetime += steel;
            State.depotAt = RealNow();
            Touch();
            return steel;
        }
    }
}
