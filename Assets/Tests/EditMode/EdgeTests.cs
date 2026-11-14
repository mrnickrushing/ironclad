using System;
using System.Linq;
using Ironclad.Core;
using NUnit.Framework;

namespace Ironclad.Tests
{
    /// <summary>The corners: long trains, late lines, big numbers, limits by the day, and saves that come back odd.</summary>
    public class EdgeTests
    {
        static Sim Strong(int power = 80)
        {
            var s = TestGame.New();
            foreach (var id in new[] { "power", "hull", "plate" }) s.State.upgrades.Add(new Level { id = id, level = power });
            return s;
        }

        static void Play(Sim s, double seconds)
        {
            for (double t = 0; t < seconds && !s.RunEnded; t += 0.5) { s.Tick(0.5); if (s.AtStation) s.PassStation(); }
        }

        [Test]
        public void AFullTrainOfEveryWagonFightsWithoutTrouble()
        {
            var s = Strong(120);
            s.State.upgrades.Add(new Level { id = "slots", level = 6 });
            s.State.train.Clear();
            foreach (var d in Content.Cars.Where(c => c.Id != "engine").Take(8)) { s.State.cars.RemoveAll(c => c.id == d.Id); s.State.cars.Add(new Level { id = d.Id, level = 3 }); s.State.train.Add(d.Id); }
            Assert.AreEqual(8, s.Slots);
            s.StartRun();
            Assert.AreEqual(9, s.Run.cars.Count);
            Play(s, 900);
            Assert.GreaterOrEqual(s.Run.mile, 25);
            Assert.IsFalse(double.IsNaN(s.Run.hull) || double.IsNaN(s.Run.scrip) || double.IsNaN(s.Run.steel));
            foreach (var c in s.Run.cars) Assert.IsFalse(double.IsNaN(c.plating));
            foreach (var e in s.Run.enemies) Assert.IsFalse(double.IsNaN(e.x) || double.IsNaN(e.z) || double.IsNaN(e.hp));
        }

        [Test]
        public void EveryLineCanBeRun()
        {
            for (int route = 0; route < Content.Routes.Count; route++)
            {
                var s = Strong(60 + (int)(Content.Routes[route].Hp * 3));      // about as strong as a train that has earned the line
                for (int i = 0; i < route; i++) s.State.best[i] = 200;
                Assert.IsTrue(s.ChooseRoute(route), Content.Routes[route].Id);
                s.StartRun();
                Play(s, 120);
                Assert.Greater(s.State.kills, 0, Content.Routes[route].Id);
                if (!s.RunEnded) s.Retire();
                Assert.IsTrue(s.CloseRun());
                Assert.IsFalse(double.IsNaN(s.State.steel) || double.IsInfinity(s.State.steel));
            }
        }

        [Test]
        public void AVeryLongRunKeepsItsNumbersSane()
        {
            var s = Strong(400);
            s.StartRun();
            for (int i = 0; i < 30000 && !s.RunEnded && s.Run.mile < 150; i++) { s.Tick(0.5); if (s.AtStation) s.PassStation(); for (int c = 0; c < s.Run.cars.Count; c++) s.FieldUpgrade(c); }
            Assert.GreaterOrEqual(s.Run.mile, 50);
            Assert.IsFalse(double.IsInfinity(s.Run.scrip) || double.IsNaN(s.Run.scrip));
            Assert.IsFalse(double.IsInfinity(s.RunSteel) || double.IsNaN(s.RunSteel));
            Assert.LessOrEqual(s.Run.enemies.Count, 400, "raiders don't pile up without end");
            StringAssert.DoesNotContain("NaN", Format.Money(s.RunSteel));
        }

        [Test]
        public void UncouplingAndCouplingAtAStationKeepsTheRunInStep()
        {
            var s = Strong(100);
            s.State.cars.Add(new Level { id = "cannon", level = 1 });
            s.State.train.Add("cannon");
            s.StartRun();
            for (int i = 0; i < 2000 && !s.AtStation; i++) s.Tick(0.5);
            Assert.IsTrue(s.AtStation);
            s.TakeCard(0);
            Assert.IsTrue(s.Uncouple(0));
            Assert.AreEqual(new[] { "engine", "cannon" }, s.Run.cars.Select(c => c.type).ToArray());
            Assert.IsFalse(s.Uncouple(0), "one wagon always stays");
            Assert.IsTrue(s.Couple("gatling"));
            Assert.AreEqual(new[] { "engine", "cannon", "gatling" }, s.Run.cars.Select(c => c.type).ToArray());
            Assert.AreEqual(s.Run.cars[2].platingMax, s.Run.cars[2].plating);
            Assert.IsFalse(s.Couple("gatling"), "no room: two places");
            Assert.IsTrue(s.Depart());
            Play(s, 60);
            Assert.AreEqual(s.State.train.Count + 1, s.Run.cars.Count);
        }

        [Test]
        public void AGunWagonStandingOffIsShotBeforeARiderStillComing()
        {
            var s = TestGame.New();
            s.StartRun();
            s.Run.queue.Clear();
            var far = new EnemyState { id = 900, kind = "gunwagon", x = -6, z = 8.5, hp = 400, hpMax = 400, damage = 1, cooldown = 9 };
            var near = new EnemyState { id = 901, kind = "sledge", x = -6, z = 6, hp = 400, hpMax = 400, damage = 1 };
            s.Run.enemies.Add(far); s.Run.enemies.Add(near);
            s.Tick(1);
            Assert.IsTrue(far.striking);
            Assert.Less(far.hp, 400, "the one already shooting at the train is fired on");
            Assert.AreEqual(400, near.hp, "before the one still walking in");
        }

        [Test]
        public void AFieldLevelPutsAKnockedOutWagonBackInTheFight()
        {
            var s = TestGame.New();
            s.StartRun();
            var g = s.Run.cars[1];
            g.plating = 0; g.down = true;
            Assert.IsTrue(s.FieldUpgrade(1));
            Assert.IsFalse(g.Out);
            Assert.AreEqual(g.platingMax, g.plating);
        }

        [Test]
        public void PlansAreAnnouncedWhenTheirMileIsFirstReached()
        {
            var s = Strong(60);
            var found = new System.Collections.Generic.List<string>();
            s.Plans += d => found.Add(d.Id);
            Assert.AreEqual("cannon", s.NextPlans.Id);
            s.StartRun();
            Play(s, 22 * 13);
            CollectionAssert.Contains(found, "cannon");
            Assert.AreEqual(1, found.Count(f => f == "cannon"));
            CollectionAssert.Contains(s.Unbuilt.Select(d => d.Id).ToList(), "cannon");
        }

        [Test]
        public void TheFirstTimeAtAStationPaysGemsOnce()
        {
            var s = Strong(120);
            s.StartRun();
            Play(s, 22 * 11);
            Assert.AreEqual(Sim.StationGems, s.State.gems);
            s.Retire(); s.CloseRun();
            s.StartRun();
            Play(s, 22 * 11);
            Assert.AreEqual(Sim.StationGems, s.State.gems, "not again for the same station");
        }

        [Test]
        public void AdvertsStopAtTheDaysLimitAndComeBackTheNextDay()
        {
            double now = 1_700_000_000;
            var s = new Sim(GameState.New()); s.RealNow = () => now;
            s.State.best[0] = 30; s.State.depotAt = now - 3600; s.State.doubleDay = s.Today;
            int given = 0;
            for (int i = 0; i < 20; i++) { s.State.depotAt = now - 3600; if (s.GrantAd(AdReward.DoubleDepot)) given++; }
            Assert.AreEqual(Sim.AdsPerDay, given);
            Assert.AreEqual(0, s.AdsLeft);
            now += 86400; s.State.doubleDay = s.Today; s.State.depotAt = now - 3600;
            Assert.AreEqual(Sim.AdsPerDay, s.AdsLeft);
            Assert.IsTrue(s.GrantAd(AdReward.DoubleDepot));
        }

        [Test]
        public void TheConductorsPassIsKnownToTheRules()
        {
            var s = TestGame.New();
            Assert.IsFalse(s.SkipsAdverts);
            s.Grant("ironclad.pass", "t");
            Assert.IsTrue(s.SkipsAdverts);
            s.Grant("ironclad.depot", "t2");
            Assert.AreEqual((Content.DepotHours + 4) * 3600, s.DepotCap);
        }

        [Test]
        public void ADamagedSaveIsMadePlayable()
        {
            var st = new GameState { train = null, cars = null, upgrades = null, best = null, owned = null, route = 99, speed = 99, seed = 0 };
            st.Repair();
            var s = new Sim(st);
            Assert.AreEqual(new[] { "gatling" }, st.train.ToArray());
            Assert.AreEqual(0, st.route, "a line that isn't open is never left chosen");
            Assert.IsTrue(s.StartRun());

            // A run whose train no longer matches what is built (a wagon kind taken out of the game).
            var st2 = GameState.New();
            st2.run = new RunState(); st2.run.cars.Add(new CarState { type = "engine" }); st2.run.cars.Add(new CarState { type = "nosuchwagon" });
            st2.train.Add("nosuchwagon");
            st2.Repair();
            Assert.IsFalse(st2.train.Contains("nosuchwagon"));
            Assert.IsTrue(st2.run == null || st2.run.cars.All(c => Content.Car(c.type) != null));
        }

        [Test]
        public void SpeedOnlyChangesHowFastTheSameRunPlays()
        {
            var a = TestGame.New(); var b = TestGame.New();
            b.State.upgrades.Add(new Level { id = "speed", level = 3 });
            Assert.IsTrue(b.SetSpeed(3));
            Assert.IsFalse(a.SetSpeed(1), "not built yet");
            a.StartRun(); b.StartRun();
            for (int i = 0; i < 300; i++) a.Tick(0.2);
            for (int i = 0; i < 100; i++) b.Tick(0.2);
            Assert.AreEqual(a.Run.kills, b.Run.kills);
            Assert.AreEqual(a.Run.mile, b.Run.mile);
        }

        [Test]
        public void RemindersAreFewAndNeverAtNight()
        {
            double now = 1_700_000_000;
            var s = new Sim(GameState.New()); s.RealNow = () => now;
            s.State.best[0] = 20; s.State.depotAt = now;
            var plan = s.ReminderPlan();
            Assert.LessOrEqual(plan.Count, 2);
            foreach (var r in plan)
            {
                double local = ((now + s.UtcOffset + r.In) % 86400 + 86400) % 86400;
                Assert.IsTrue(local >= 8 * 3600 - 1 && local < 22 * 3600, r.Title + " at " + local / 3600);
            }
        }

        [Test]
        public void MoneyReadsWellAtEverySize()
        {
            Assert.AreEqual("999", Format.Money(999.9));
            Assert.AreEqual("1K", Format.Money(1000));
            Assert.AreEqual("15.6M", Format.Money(15_600_000));
            Assert.AreEqual("0", Format.Money(double.NaN));
            StringAssert.IsMatch("^[0-9.]+[a-z]{2}$", Format.Money(1e30));
        }
    }
}
