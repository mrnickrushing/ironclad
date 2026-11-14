using System;
using System.Linq;
using Ironclad.Core;
using NUnit.Framework;

namespace Ironclad.Tests
{
    /// <summary>A sensible, greedy player: prints how far each run gets and what the workshop holds, hour by hour.</summary>
    public class PacingTests
    {
        static void Spend(Sim s)
        {
            for (int guard = 0; guard < 5000; guard++)
            {
                string best = null; double cost = double.MaxValue; bool car = false;
                foreach (var u in Content.Upgrades)
                {
                    if (s.UpgradeMaxed(u.Id) || u.Id == "whistle") continue;
                    double c = s.UpgradeCost(u.Id);
                    if (c < cost) { cost = c; best = u.Id; car = false; }
                }
                foreach (var d in Content.Cars)
                {
                    if (!s.Built(d.Id) && !s.PlansFound(d.Id)) continue;
                    if (s.Built(d.Id) && d.Id != "engine" && !s.State.train.Contains(d.Id)) continue;
                    double c = s.CarCost(d.Id);
                    if (c < cost) { cost = c; best = d.Id; car = true; }
                }
                if (best == null || cost > s.State.steel) return;
                if (car) s.BuyCar(best); else s.BuyUpgrade(best);
                // Fill empty places with the newest wagons.
                foreach (var d in Content.Cars) if (d.Id != "engine" && s.Built(d.Id) && !s.State.train.Contains(d.Id)) s.Couple(d.Id);
            }
        }

        static void Field(Sim s)
        {
            for (int guard = 0; guard < 50; guard++)
            {
                int pick = -1; double cost = double.MaxValue;
                for (int i = 0; i < s.Run.cars.Count; i++) if (s.FieldCost(i) < cost) { cost = s.FieldCost(i); pick = i; }
                if (pick < 0 || !s.FieldUpgrade(pick)) return;
            }
        }

        [Test]
        public void FirstHours()
        {
            var s = TestGame.New();
            double clock = 0; int run = 0; double next = 0;
            while (clock < 6 * 3600 && run < 400)
            {
                Spend(s);
                s.SetSpeed(s.SpeedsOpen - 1);
                s.StartRun(); run++;
                double started = clock;
                for (int i = 0; i < 200000 && !s.RunEnded; i++)
                {
                    s.Tick(0.5); clock += 0.5 / s.Speed;
                    if (s.AtStation) s.PassStation();
                    if (i % 4 == 0) Field(s);
                }
                int mile = s.Run.mile; double steel = s.RunSteel; int route = s.Run.route;
                s.CloseRun();
                for (int r = Content.Routes.Count - 1; r > 0; r--) if (s.RouteOpen(r) && s.State.best[r - 1] >= Content.Routes[r].Opens + 15) { s.ChooseRoute(r); break; }
                if (clock >= next || run <= 6)
                {
                    TestContext.Out.WriteLine("{0,5:0.00} h  run {1,3}  {2,-9} mile {3,3}  {4,5:0.0} min x{9}  +{5,-8} steel {6,-8} power {7,2} train [{8}]",
                        clock / 3600, run, Content.Routes[route].Id, mile, (clock - started) / 60, Format.Money(steel), Format.Money(s.State.steel),
                        s.UpgradeLevel("power"), string.Join(" ", s.State.train.Select(id => id + s.CarLevel(id))), s.Speed);
                    if (run > 6) next = clock + 900;
                }
            }
            Assert.Greater(s.State.best[0], 20, "an hour or two in, the first line is well under way");
        }
    }
}
