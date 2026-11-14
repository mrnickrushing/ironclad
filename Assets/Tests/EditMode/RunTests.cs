using System.Linq;
using Ironclad.Core;
using NUnit.Framework;

namespace Ironclad.Tests
{
    public class RunTests
    {
        static void Roll(Sim s, double seconds) { for (double t = 0; t < seconds && s.Running; t += 0.5) s.Tick(0.5); }

        [Test]
        public void ANewGameSetsOffWithTheEngineAndAGatlingCar()
        {
            var s = TestGame.New();
            Assert.IsTrue(s.StartRun());
            Assert.AreEqual(new[] { "engine", "gatling" }, s.Run.cars.Select(c => c.type).ToArray());
            Assert.AreEqual(s.HullMax, s.Run.hull);
            Assert.IsFalse(s.StartRun(), "one run at a time");
        }

        [Test]
        public void TheFirstMilesAreWonAndPaySteelWhenTheRunIsClosed()
        {
            var s = TestGame.New();
            s.StartRun();
            Roll(s, Content.MileSeconds * 3 + 2);
            Assert.GreaterOrEqual(s.Run.mile, 3);
            Assert.Greater(s.Run.kills, 5);
            Assert.Greater(s.Run.scrip, s.StartScrip);
            Assert.AreEqual(0, s.State.steel, "steel is banked when the run ends");
            s.Retire();
            double due = s.RunSteel;
            Assert.Greater(due, 0);
            Assert.IsFalse(s.CanRevive, "a run given up can't be revived");
            s.CloseRun();
            Assert.AreEqual(due, s.State.steel);
            Assert.IsNull(s.Run);
            Assert.AreEqual(1, s.State.runs);
        }

        [Test]
        public void AnUnimprovedTrainIsOverrunInTheEnd()
        {
            var s = TestGame.New();
            s.StartRun();
            for (int i = 0; i < 4000 && !s.RunEnded; i++) { s.Tick(0.5); if (s.AtStation) s.PassStation(); }
            Assert.IsTrue(s.RunEnded);
            Assert.AreEqual(0, s.Run.hull);
            Assert.Greater(s.State.best[0], 3);
        }

        [Test]
        public void ARunPlaysTheSameAtAnyFrameRate()
        {
            var a = TestGame.New(); var b = TestGame.New();
            a.StartRun(); b.StartRun();
            for (int i = 0; i < 100; i++) a.Tick(0.5);
            for (int i = 0; i < 5000; i++) b.Tick(0.01);
            Assert.AreEqual(a.Run.kills, b.Run.kills);
            Assert.AreEqual(a.Run.scrip, b.Run.scrip, 1e-6);
        }

        [Test]
        public void ScripBuysFieldLevelsThatLastTheRun()
        {
            var s = TestGame.New();
            s.StartRun();
            double before = s.ShotDamage(s.Run.cars[1]);
            Assert.IsTrue(s.FieldUpgrade(1));
            Assert.AreEqual(before * Content.FieldPower, s.ShotDamage(s.Run.cars[1]), 1e-9);
            Assert.AreEqual(s.StartScrip - Content.FieldCost, s.Run.scrip);
            s.Run.scrip = 0;
            Assert.IsFalse(s.FieldUpgrade(1));
        }

        static Sim AtFirstStation()
        {
            var s = TestGame.New();
            s.State.upgrades.Add(new Level { id = "power", level = 60 });
            s.State.upgrades.Add(new Level { id = "hull", level = 60 });
            s.StartRun();
            for (int i = 0; i < 2000 && !s.AtStation && !s.RunEnded; i++) s.Tick(0.5);
            Assert.IsTrue(s.AtStation, "a strong train reaches the first station: mile " + s.Run.mile + " t " + s.Run.mileTime + " ended " + s.RunEnded + " foes " + s.Run.enemies.Count + " queue " + s.Run.queue.Count + " boss " + s.Run.bossOut);
            return s;
        }

        [Test]
        public void TheTenthMileHasABossAndThenAStation()
        {
            var s = AtFirstStation();
            Assert.AreEqual(10, s.Run.mile);
            Assert.AreEqual(1, s.State.bosses);
            Assert.AreEqual(3, s.Run.offer.Count);
            Assert.AreEqual(3, s.Run.offer.Select(o => o.card).Distinct().Count());
            Assert.AreEqual(0, s.Run.enemies.Count);
            Assert.IsFalse(s.Depart(), "a card first");
            Assert.IsTrue(s.TakeCard(0));
            Assert.IsTrue(s.Depart());
            Assert.AreEqual(11, s.Run.mile);
            Assert.IsTrue(s.Running);
        }

        [Test]
        public void WagonsAreRecoupledAtAStationButNotOnTheMove()
        {
            var s = TestGame.New();
            s.State.cars.Add(new Level { id = "cannon", level = 1 });
            s.State.train.Add("cannon");
            s.State.upgrades.Add(new Level { id = "power", level = 60 });
            s.StartRun();
            Assert.IsFalse(s.Shunt(0, 1), "not while rolling");
            for (int i = 0; i < 2000 && !s.AtStation; i++) s.Tick(0.5);
            s.Run.cars[1].field = 3;
            Assert.IsTrue(s.Shunt(0, 1));
            Assert.AreEqual(new[] { "cannon", "gatling" }, s.State.train.ToArray());
            Assert.AreEqual(new[] { "engine", "cannon", "gatling" }, s.Run.cars.Select(c => c.type).ToArray());
            Assert.AreEqual(3, s.Run.cars[2].field, "a wagon keeps its field levels when it is moved");
        }

        [Test]
        public void AWagonWithNoPlatingStopsFiringAndTheFittersBringItBack()
        {
            var s = TestGame.New();
            s.State.cars.Add(new Level { id = "repair", level = 1 });
            s.State.train.Add("repair");
            s.StartRun();
            var g = s.Run.cars[1];
            g.plating = 0; g.down = true;
            s.Tick(1);
            Assert.IsTrue(g.Out);
            for (int i = 0; i < 40 && g.Out; i++) s.Tick(0.5);
            Assert.IsFalse(g.Out, "back in the fight at a quarter plating");
        }

        [Test]
        public void TheWhistleThrowsRaidersBackAndThenNeedsToBuildSteam()
        {
            var s = TestGame.New();
            s.StartRun();
            for (int i = 0; i < 400 && !s.Run.enemies.Any(e => System.Math.Abs(e.z) < 10); i++) s.Tick(0.1);
            var e0 = s.Run.enemies.First(e => System.Math.Abs(e.z) < 10);
            double z = System.Math.Abs(e0.z);
            Assert.IsTrue(s.Whistle());
            Assert.Greater(System.Math.Abs(e0.z), z);
            Assert.Greater(e0.stun, 0);
            Assert.IsFalse(s.Whistle());
            Assert.AreEqual(1, s.State.whistles);
        }

        [Test]
        public void ARunCanBeRevivedOnce()
        {
            var s = TestGame.New();
            s.StartRun();
            for (int i = 0; i < 4000 && !s.RunEnded; i++) { s.Tick(0.5); if (s.AtStation) s.PassStation(); }
            Assert.IsTrue(s.CanRevive);
            Assert.IsFalse(s.Revive(true), "no gems");
            s.State.gems = 30;
            Assert.IsTrue(s.Revive(true));
            Assert.AreEqual(0, s.State.gems);
            Assert.IsTrue(s.Running);
            Assert.AreEqual(s.Run.hullMax * Content.ReviveHull, s.Run.hull);
            for (int i = 0; i < 4000 && !s.RunEnded; i++) { s.Tick(0.5); if (s.AtStation) s.PassStation(); }
            Assert.IsFalse(s.CanRevive);
        }

        [Test]
        public void SteelBuysUpgradesAndWagonsOnceTheirPlansAreFound()
        {
            var s = TestGame.New();
            s.State.steel = 1000;
            Assert.IsTrue(s.BuyUpgrade("power"));
            Assert.AreEqual(1000 - 15, s.State.steel);
            Assert.AreEqual(1.06, s.DamageMult, 1e-9);
            Assert.IsFalse(s.BuyCar("cannon"), "no plans yet");
            s.State.best[0] = 6;
            Assert.IsTrue(s.BuyCar("cannon"));
            Assert.AreEqual(new[] { "gatling", "cannon" }, s.State.train.ToArray(), "a new wagon is coupled on if there is room");
            Assert.IsTrue(s.BuyCar("cannon"));
            Assert.AreEqual(2, s.CarLevel("cannon"));
        }

        [Test]
        public void TheNextLineOpensAtItsMile()
        {
            var s = TestGame.New();
            Assert.IsFalse(s.ChooseRoute(1));
            s.State.best[0] = Content.Routes[1].Opens;
            Assert.IsTrue(s.ChooseRoute(1));
            s.StartRun();
            Assert.AreEqual("badlands", s.Route.Id);
        }

        [Test]
        public void TheDepotPaysForTimeAwayUpToItsCap()
        {
            double now = 1_700_000_000;
            var s = new Sim(GameState.New());
            s.RealNow = () => now;
            Assert.AreEqual(0, s.DepotSteel);
            s.State.best[0] = 20; s.State.depotAt = now;
            now += 3600;
            double hour = s.DepotSteel;
            Assert.Greater(hour, 0);
            now += 100 * 3600;
            Assert.AreEqual(System.Math.Floor(s.DepotRate * 8), s.DepotSteel);
            double got = s.CollectDepot(true);
            Assert.AreEqual(System.Math.Floor(s.DepotRate * 8) * 2, got);
            Assert.IsFalse(s.DoubleFree);
            Assert.AreEqual(0, s.DepotSteel);
        }

        [Test]
        public void AdvertsPayTheirRewardOnceAndAreCountedByTheDay()
        {
            var s = TestGame.New();
            Assert.IsFalse(s.GrantAd(AdReward.Revive));
            s.StartRun();
            for (int i = 0; i < 4000 && !s.RunEnded; i++) { s.Tick(0.5); if (s.AtStation) s.PassStation(); }
            double steel = s.RunSteel;
            Assert.IsTrue(s.GrantAd(AdReward.DoubleRun));
            Assert.AreEqual(steel * 2, s.RunSteel, 1);
            Assert.IsFalse(s.GrantAd(AdReward.DoubleRun));
            Assert.IsTrue(s.GrantAd(AdReward.Revive));
            Assert.AreEqual(Sim.AdsPerDay - 2, s.AdsLeft);
        }

        [Test]
        public void ThePurchasesGrantOncePerTransaction()
        {
            var s = TestGame.New();
            Assert.IsTrue(s.Grant("ironclad.starter", "t1"));
            Assert.AreEqual(250, s.State.gems);
            Assert.AreEqual(1500, s.State.steel);
            Assert.IsFalse(s.Grant("ironclad.starter", "t1"));
            Assert.IsTrue(s.Grant("ironclad.doubler", "t2"));
            Assert.AreEqual(2, s.SteelMult, 1e-9);
        }

        [Test]
        public void TheDailyGiftAndTasksPayOnceADay()
        {
            double now = 1_700_000_000;
            var s = new Sim(GameState.New());
            s.RealNow = () => now;
            Assert.IsTrue(s.ClaimGift());
            Assert.AreEqual(5, s.State.gems);
            Assert.IsFalse(s.ClaimGift());
            now += 3 * 86400;       // days missed lose nothing: the week just waits
            Assert.AreEqual(1, s.GiftIndex);
            Assert.IsTrue(s.ClaimGift());
            Assert.Greater(s.State.steel, 0);

            Assert.IsFalse(s.ClaimTask(0));
            s.Tick(0.1);
            s.State.kills += 250;
            Assert.IsTrue(s.ClaimTask(0));
            Assert.IsFalse(s.ClaimTask(0));
            Assert.AreEqual(10, s.State.gems);
            now += 86400;
            Assert.AreEqual(0, s.TaskProgress(0), "a new day starts the count again");
        }

        [Test]
        public void TheSaveSurvivesBeingWrittenWithAnEmptyRun()
        {
            var st = GameState.New();
            st.run = new RunState();      // what JsonUtility makes of "no run"
            st.Repair();
            Assert.IsNull(st.run);
        }
    }
}
