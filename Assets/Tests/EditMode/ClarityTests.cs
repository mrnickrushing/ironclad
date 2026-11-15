using System.Linq;
using Ironclad.Core;
using NUnit.Framework;

namespace Ironclad.Tests
{
    /// <summary>What the wagons give each other, buying many levels, formations, the breakdown of a lost run, purchase
    /// previews, the next goal and the UNLOCKED card.</summary>
    public class ClarityTests
    {
        static Sim WithWagons(params string[] ids)
        {
            var s = TestGame.New();
            foreach (var id in ids.Distinct()) if (!s.Built(id)) s.State.cars.Add(new Level { id = id, level = 1 });
            s.State.upgrades.Add(new Level { id = "slots", level = 6 });
            s.State.train.Clear(); s.State.train.AddRange(ids);
            return s;
        }

        [Test]
        public void ASupplyCarSpeedsUpTheGunsEitherSideOfIt()
        {
            var s = WithWagons("gatling", "supply", "cannon", "armour");
            var g = s.TrainGifts();
            Assert.AreEqual(0.25, g[0].Haste, 1e-9, "the Gatling Car in front of it");
            Assert.AreEqual(0.25, g[2].Haste, 1e-9, "the Cannon Car behind it");
            Assert.AreEqual(0, g[1].Haste, "a support wagon doesn't fire, so it gets no haste");
            Assert.AreEqual(0.33, g[2].Shield, 1e-9, "the Armoured Car shields the cannon next to it");
            Assert.IsFalse(g.Any(x => x.Wasted));

            var waste = WithWagons("supply", "armour", "gatling");
            Assert.IsTrue(waste.TrainGifts()[1].Wasted, "a supply car next to an armoured car wastes its gift on it");
        }

        [Test]
        public void APreviewSaysWhatAMoveWouldChange()
        {
            var s = WithWagons("supply", "gatling", "cannon");
            var change = s.ShuntPreview(0, 1);      // the supply car between the two guns
            Assert.IsTrue(change.Any(c => c.Type == "cannon" && c.Haste > 0.2), "the cannon would fire faster");
            Assert.IsFalse(change.Any(c => c.Type == "gatling"), "the gatling is next to it either way");
            StringAssert.Contains("Cannon Car fires 25% faster", Sim.Describe(change));
            s.Shunt(0, 1);
            Assert.AreEqual(0.25, s.TrainGifts()[2].Haste, 1e-9, "and the move does what the preview said");
            Assert.AreEqual("No change to what the wagons give each other", Sim.Describe(s.ShuntPreview(0, 0)));
        }

        [Test]
        public void TenOrMaxLevelsCostWhatTheyWouldOneAtATime()
        {
            var s = TestGame.New();
            s.State.steel = 1_000_000;
            int n; double ten = s.UpgradeCostN("power", 10, out n);
            Assert.AreEqual(10, n);
            double sum = 0; for (int i = 0; i < 10; i++) sum += System.Math.Floor(15 * System.Math.Pow(1.16, i));
            Assert.AreEqual(sum, ten);
            Assert.AreEqual(10, s.BuyUpgrades("power", 10));
            Assert.AreEqual(1_000_000 - sum, s.State.steel);

            s.State.steel = 500;
            double max = s.UpgradeCostN("hull", Sim.BuyMax, out n);
            Assert.LessOrEqual(max, 500); Assert.Greater(n, 1);
            Assert.AreEqual(n, s.BuyUpgrades("hull", Sim.BuyMax), "MAX buys as many as it said");
            Assert.Less(s.State.steel, s.UpgradeCost("hull"), "and there isn't enough for another");

            s.State.steel = 0;
            s.UpgradeCostN("plate", Sim.BuyMax, out n);
            Assert.AreEqual(1, n, "with no steel MAX still shows the price of one");
            int crits; s.State.steel = 1e30; s.UpgradeCostN("crit", Sim.BuyMax, out crits);
            Assert.AreEqual(40, crits, "never past an upgrade's last level");

            s.State.steel = 1e9;
            Assert.AreEqual(5, s.BuyCars("gatling", 5)); Assert.AreEqual(6, s.CarLevel("gatling"));
            Assert.AreEqual(0, s.BuyCars("dynamo", 5), "no plans, no wagon");
        }

        [Test]
        public void AFormationCouplesUpTheSameOrderAgain()
        {
            var s = WithWagons("gatling", "cannon", "supply");
            Assert.IsTrue(s.SaveFormation(0));
            s.Shunt(0, 2); s.Uncouple(0);
            Assert.AreNotEqual("gatling,cannon,supply", string.Join(",", s.State.train));
            Assert.IsTrue(s.UseFormation(0));
            Assert.AreEqual("gatling,cannon,supply", string.Join(",", s.State.train));
            Assert.IsFalse(s.UseFormation(1), "an empty slot does nothing");

            // At a station the wagons keep their own plating and levels.
            s.StartRun();
            s.State.run.cars[2].field = 4;
            s.State.run.phase = (int)Phase.Station;
            s.SaveFormation(1);
            s.State.formations[1].cars = new System.Collections.Generic.List<string> { "cannon", "gatling", "supply" };
            Assert.IsTrue(s.UseFormation(1));
            Assert.AreEqual("cannon", s.State.run.cars[1].type);
            Assert.AreEqual(4, s.State.run.cars[1].field, "the cannon brought its field levels with it");
            s.State.run.phase = (int)Phase.Rolling;
            Assert.IsFalse(s.UseFormation(0), "not while rolling");
        }

        [Test]
        public void ALostRunExplainsItself()
        {
            var s = WithWagons("gatling", "cannon");
            s.StartRun();
            for (int i = 0; i < 20000 && !s.RunEnded; i++) s.Tick(0.5);
            Assert.IsTrue(s.RunEnded);
            var d = s.Debrief;
            Assert.IsNotNull(d);
            Assert.AreEqual(1, d.Weapons.Sum(w => w.Part), 1e-6, "every gun's share adds up");
            Assert.IsTrue(d.Weapons.SequenceEqual(d.Weapons.OrderByDescending(w => w.Damage)), "biggest first");
            Assert.IsNotNull(d.Danger, "something hurt the train");
            Assert.IsFalse(string.IsNullOrEmpty(d.Tip));
            Assert.Greater(s.State.carKills.Sum(t => t.v), 0, "kills are counted by wagon");
            Assert.LessOrEqual(s.State.carKills.Sum(t => t.v), s.State.kills, "steam vents kill without a wagon");
            s.CloseRun();
            Assert.IsNull(s.Debrief, "nothing to explain between runs");
            Assert.Greater(s.State.lastRunSteel, 0);
        }

        [Test]
        public void UnspentScripIsTheFirstThingToFix()
        {
            var s = TestGame.New();
            s.StartRun();
            s.State.run.mile = 5; s.State.run.scrip = 100000;
            s.State.run.phase = (int)Phase.Over;
            Assert.AreEqual("scrip", s.Debrief.TipId);
        }

        [Test]
        public void PurchasePreviewsUseThePlayersOwnNumbers()
        {
            var s = TestGame.New();
            s.State.lastRunSteel = 1234; s.State.best[0] = 30;
            var mill = s.PurchasePreview("ironclad.doubler");
            StringAssert.Contains(Format.Money(1234), mill[0]);
            StringAssert.Contains(Format.Money(2468), mill[1]);
            var night = s.PurchasePreview("ironclad.depot");
            StringAssert.Contains("12 hours", night[1]);
            StringAssert.Contains(Format.Money(System.Math.Floor(s.DepotRate * 4)), night[2]);
            Assert.IsNull(s.PurchasePreview("ironclad.gems.small"));
        }

        [Test]
        public void TheNextGoalIsAlwaysSomething()
        {
            var s = TestGame.New();
            var g = s.NextGoal;
            Assert.AreEqual("mile", g.Unit); Assert.AreEqual(6, g.Need, "the Cannon Car's plans at mile 6");
            s.State.best[0] = 6;
            Assert.AreEqual("Build the Cannon Car", s.NextGoal.Name);
            for (int i = 0; i < s.State.best.Count; i++) s.State.best[i] = 999;
            foreach (var c in Content.Cars) if (!s.Built(c.Id)) s.State.cars.Add(new Level { id = c.Id, level = 1 });
            Assert.AreEqual("Your first charter", s.NextGoal.Name);
        }

        [Test]
        public void TheUnlockedCardShowsOnlyWhatIsNew()
        {
            var s = TestGame.New();
            s.State.best[0] = 7;
            Assert.AreEqual(0, s.NewlyUnlocked.Count, "an old save starts with what it already has");
            s.State.best[0] = 13;
            CollectionAssert.AreEqual(new[] { "The Supply Car plans" }, s.NewlyUnlocked);
            s.Celebrated();
            Assert.AreEqual(0, s.NewlyUnlocked.Count);
            s.State.best[0] = 95;
            Assert.IsTrue(s.NewlyUnlocked.Contains("Badlands Cut"));
        }

        [Test]
        public void HalfThePlayersSeeTheStarterPackAfterTheirFirstBoss()
        {
            var a = TestGame.New(); a.State.cohort = 10; a.Tick(0.1);
            var b = TestGame.New(); b.State.cohort = 90; b.Tick(0.1);
            foreach (var s in new[] { a, b }) { s.State.runs = 3; s.State.bosses = 1; }
            Assert.IsFalse(a.StarterOfferDue, "the first group waits for another day");
            Assert.IsTrue(b.StarterOfferDue, "the second group sees it after a boss");
            b.StarterOffered();
            Assert.IsFalse(b.StarterOfferDue, "never twice");
            Assert.AreNotEqual(a.StarterVariant, b.StarterVariant);
        }
    }
}
