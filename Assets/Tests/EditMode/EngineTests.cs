using System.Linq;
using Ironclad.Core;
using NUnit.Framework;

namespace Ironclad.Tests
{
    /// <summary>A new engine, liveries, the free deal at a station and what the end-of-run card says.</summary>
    public class EngineTests
    {
        static void Roll(Sim s, double seconds) { for (double t = 0; t < seconds && s.Running; t += 0.5) s.Tick(0.5); }

        /// <summary>A train strong enough to reach the first station quickly.</summary>
        static Sim Strong()
        {
            var s = TestGame.New();
            s.State.upgrades.Add(new Level { id = "power", level = 200 });
            s.State.upgrades.Add(new Level { id = "hull", level = 200 });
            return s;
        }

        static void ToStation(Sim s)
        {
            s.StartRun();
            for (int i = 0; i < 4000 && !s.AtStation && !s.RunEnded; i++) s.Tick(0.5);
            Assert.IsTrue(s.AtStation, "the strong train reaches the first station");
        }

        [Test]
        public void ANewEngineNeedsACharterAndPaysOneForEveryStepOfSteel()
        {
            var s = TestGame.New();
            Assert.AreEqual(0, s.ChartersOnRenew);
            Assert.IsFalse(s.CanRenew);
            Assert.AreEqual(0, s.Renew());
            s.State.lifetime = Content.CharterSteel - 1;
            Assert.AreEqual(0, s.ChartersOnRenew);
            s.State.lifetime = Content.CharterSteel;
            Assert.AreEqual(1, s.ChartersOnRenew);
            s.State.lifetime = Content.CharterSteel * 9;
            Assert.AreEqual(3, s.ChartersOnRenew, "by the square root: nine times the steel, three charters");
            Assert.AreEqual(Content.CharterSteel * 16 - s.EngineSteel, s.SteelToNextCharter);
        }

        [Test]
        public void ANewEngineStartsTheWorkshopAgainAndKeepsTheRest()
        {
            var s = TestGame.New();
            s.State.lifetime = Content.CharterSteel * 4; s.State.steel = 12345;
            s.State.gems = 77; s.State.seals = 3; s.State.perks.Add(new Level { id = "quarter", level = 2 });
            s.State.best[0] = 95; s.State.best[1] = 20;
            s.State.upgrades.Add(new Level { id = "power", level = 30 }); s.State.upgrades.Add(new Level { id = "speed", level = 2 });
            s.State.cars.Add(new Level { id = "cannon", level = 12 }); s.State.train.Add("cannon");
            s.State.speed = 2; s.State.express = true; s.State.route = 1;
            s.State.owned.Add("ironclad.pass");
            double damageBefore = s.DamageMult;

            Assert.AreEqual(2, s.Renew());
            Assert.AreEqual(2, s.State.charters); Assert.AreEqual(1, s.State.engines);
            Assert.AreEqual(0, s.State.steel);
            Assert.AreEqual(0, s.UpgradeLevel("power")); Assert.AreEqual(1, s.SpeedsOpen); Assert.AreEqual(0, s.State.speed);
            Assert.AreEqual(new[] { "engine", "gatling" }, s.State.cars.Select(c => c.id).ToArray());
            Assert.AreEqual(1, s.CarLevel("gatling"));
            Assert.AreEqual(new[] { "gatling" }, s.State.train.ToArray());
            Assert.IsFalse(s.State.express); Assert.AreEqual(0, s.State.route);
            // Kept.
            Assert.AreEqual(77, s.State.gems); Assert.AreEqual(3, s.State.seals); Assert.AreEqual(2, s.PerkLevel("quarter"));
            Assert.AreEqual(95, s.State.best[0]); Assert.IsTrue(s.RouteOpen(1)); Assert.IsTrue(s.PlansFound("cannon"));
            Assert.IsTrue(s.Owns("ironclad.pass"));
            // Stronger for good.
            Assert.AreEqual(1.2, s.CharterMult, 1e-9);
            Assert.AreEqual(1.2, s.SteelMult, 1e-9, "no upgrades left: only the charters");
            Assert.Less(s.DamageMult, damageBefore, "thirty levels of powder are worth more than two charters");
            Assert.IsTrue(s.SkinOwned("charter"), "the first new engine comes with its livery");
            // This engine starts counting from nothing.
            Assert.AreEqual(0, s.EngineSteel); Assert.AreEqual(0, s.ChartersOnRenew); Assert.IsFalse(s.CanRenew);
        }

        [Test]
        public void NoNewEngineInTheMiddleOfARun()
        {
            var s = TestGame.New();
            s.State.lifetime = Content.CharterSteel;
            s.StartRun();
            Assert.IsFalse(s.CanRenew);
            Assert.AreEqual(0, s.Renew());
        }

        [Test]
        public void TheWorkshopShowsANewEngineOnceTheSecondLineIsOpen()
        {
            var s = TestGame.New();
            Assert.IsFalse(s.RenewShown);
            s.State.best[0] = Content.Routes[1].Opens;
            Assert.IsTrue(s.RenewShown);
        }

        [Test]
        public void ChartersMakeTheDepotPayMore()
        {
            var s = TestGame.New();
            s.State.best[0] = 40;
            double before = s.DepotRate;
            s.State.charters = 5;
            Assert.AreEqual(before * 1.5, s.DepotRate, before * 1e-9);
        }

        [Test]
        public void LiveriesAreBoughtWithGemsAndOnlyChangeThePaint()
        {
            var s = TestGame.New();
            Assert.IsTrue(s.SkinOwned("")); Assert.AreEqual("loco_b", s.Skin.Model);
            Assert.IsFalse(s.BuySkin("belle"), "no gems");
            Assert.IsFalse(s.BuySkin("charter"), "given, not sold");
            Assert.IsFalse(s.BuySkin("nonsense"));
            s.State.gems = 500;
            double damage = s.DamageMult, steel = s.SteelMult;
            Assert.IsTrue(s.BuySkin("belle"));
            Assert.AreEqual(500 - Content.Skin("belle").Gems, s.State.gems);
            Assert.AreEqual("belle", s.State.skin); Assert.AreEqual("loco", s.Skin.Model);
            Assert.IsFalse(s.BuySkin("belle"), "bought once");
            Assert.AreEqual(damage, s.DamageMult); Assert.AreEqual(steel, s.SteelMult);
            Assert.IsTrue(s.UseSkin(""));
            Assert.AreEqual("", s.State.skin);
            Assert.IsFalse(s.UseSkin("black"), "not owned");
        }

        [Test]
        public void AnUnknownLiveryInASaveFallsBackToTheFirst()
        {
            var st = GameState.New(); st.skin = "gone"; st.skins = null; st.Repair();
            Assert.AreEqual("", st.skin); Assert.IsNotNull(st.skins);
        }

        [Test]
        public void TheCardOddsAddUpAndTheLuckyTableTiltsThem()
        {
            var s = TestGame.New();
            var o = s.CardOdds;
            Assert.AreEqual(1.0, o[0] + o[1] + o[2], 1e-9);
            Assert.AreEqual(Content.RarityOdds[2], o[2], 1e-9);
            s.State.perks.Add(new Level { id = "lucky", level = 5 });
            var l = s.CardOdds;
            Assert.AreEqual(1.0, l[0] + l[1] + l[2], 1e-9);
            Assert.Greater(l[2], o[2]); Assert.Greater(l[1], o[1]); Assert.Less(l[0], o[0]);
        }

        [Test]
        public void EachStationDealsOtherCardsOnceForNothing()
        {
            var s = Strong();
            ToStation(s);
            Assert.IsTrue(s.FreeReroll);
            int gems = s.State.gems;
            Assert.IsTrue(s.RerollCards());
            Assert.AreEqual(gems, s.State.gems, "the first deal is free");
            Assert.IsFalse(s.FreeReroll);
            s.State.gems = 0;      // the station's first-visit gems would pay for one
            Assert.IsFalse(s.RerollCards(), "then it costs gems");
            s.State.gems = Sim.RerollGems;
            Assert.IsTrue(s.RerollCards());
            Assert.AreEqual(0, s.State.gems);
            s.TakeCard(0); s.Depart();
            for (int i = 0; i < 4000 && !s.AtStation && !s.RunEnded; i++) s.Tick(0.5);
            Assert.IsTrue(s.AtStation);
            Assert.IsTrue(s.FreeReroll, "a new station, a new free deal");
        }

        [Test]
        public void TheEndOfARunSaysHowCloseItCame()
        {
            var s = TestGame.New();
            s.StartRun();
            Roll(s, 30);
            s.Run.hull = 0.001;
            for (int i = 0; i < 2000 && !s.RunEnded; i++) s.Tick(0.5);
            Assert.IsTrue(s.RunEnded);
            var miss = s.NearMiss;
            Assert.IsNotNull(miss);
            StringAssert.Contains("short of", miss);
            Assert.Greater(s.Run.left, 0);

            var t = TestGame.New();
            t.StartRun(); Roll(t, 10); t.Retire();
            Assert.IsNull(t.NearMiss, "nothing to say about a run given up");
        }

        [Test]
        public void TheBaronsCarriageLeftIsTheNearMissOnHisMile()
        {
            var s = TestGame.New();
            s.StartRun();
            s.Run.mile = 10; s.Run.mileTime = 0;
            Roll(s, 6);
            Assert.IsTrue(s.Run.bossOut);
            s.Run.hull = 0.001;
            for (int i = 0; i < 2000 && !s.RunEnded; i++) s.Tick(0.5);
            Assert.IsTrue(s.RunEnded);
            Assert.GreaterOrEqual(s.Run.bossLeft, 0);
            StringAssert.Contains("Baron's Carriage", s.NearMiss);
        }

        [Test]
        public void TheNextUnlockIsWhicheverIsNearer()
        {
            var s = TestGame.New();
            var u = s.NextUnlock;
            StringAssert.Contains("Cannon Car", u.Name); Assert.AreEqual(6, u.Mile); Assert.AreEqual(0, u.Have);
            s.State.best[0] = 85;
            u = s.NextUnlock;
            Assert.AreEqual(Content.Routes[1].Name, u.Name, "every plan is found by mile 85: the second line is next");
            Assert.AreEqual(Content.Routes[1].Opens, u.Mile); Assert.AreEqual(85, u.Have);
        }

        [Test]
        public void TheRunRemembersTheBestItSetOffToBeat()
        {
            var s = TestGame.New();
            s.State.best[0] = 33;
            s.StartRun();
            Assert.AreEqual(33, s.Run.startBest);
        }
    }
}
