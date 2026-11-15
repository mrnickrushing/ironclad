using System.Linq;
using Ironclad.Core;
using NUnit.Framework;

namespace Ironclad.Tests
{
    /// <summary>Mastery titles and paints, builds that work, the weekly trial, the depot growing and the expedition track.</summary>
    public class MetaTests
    {
        [Test]
        public void AMasteryGivesItsTitleAndPaintOnce()
        {
            var s = TestGame.New();
            var m = Sim.Mastery("bosses");
            Assert.IsFalse(s.ClaimMastery("bosses"), "not done yet");
            s.State.bosses = 25;
            Assert.AreEqual(1, s.MasteriesReady);
            Assert.IsTrue(s.ClaimMastery("bosses"));
            Assert.Contains("Baron Breaker", s.State.titles);
            Assert.AreEqual("Baron Breaker", s.State.title, "the first title is worn at once");
            Assert.IsTrue(s.SkinOwned("baron"), "and the paint is the player's");
            Assert.IsFalse(s.ClaimMastery("bosses"), "once");
            Assert.IsFalse(s.UseTitle("Nobody Special"), "only titles earned");
            Assert.IsTrue(s.UseTitle("")); Assert.AreEqual("", s.State.title);
            foreach (var x in Sim.Masteries) Assert.IsTrue(x.Skin == null || Content.Skin(x.Skin) != null, x.Id);
        }

        [Test]
        public void ABuildCouplesUpOnlyWhenEveryWagonIsBuilt()
        {
            var s = TestGame.New();
            s.State.upgrades.Add(new Level { id = "slots", level = 6 });
            Assert.IsFalse(s.UseBuild(0), "no Supply or Armoured Car yet");
            CollectionAssert.AreEquivalent(new[] { "supply", "armour" }, s.BuildMissing(Sim.Builds[0]));
            foreach (var id in new[] { "supply", "armour" }) s.State.cars.Add(new Level { id = id, level = 1 });
            Assert.IsTrue(s.UseBuild(0));
            Assert.AreEqual("gatling,supply,gatling,armour", string.Join(",", s.State.train));
            foreach (var b in Sim.Builds) foreach (var c in b.Cars) Assert.IsNotNull(Content.Car(c), b.Name + ": " + c);
        }

        [Test]
        public void TheTrialIsTheSameForEveryoneAndPaysNoSteel()
        {
            var s = TestGame.New();
            Assert.IsFalse(s.StartTrial(), "opens at mile 20");
            s.State.best[0] = 25;
            foreach (var u in new[] { "power", "hull", "rate" }) s.State.upgrades.Add(new Level { id = u, level = 60 });
            double strong = s.DamageMult;
            Assert.IsTrue(s.StartTrial());
            Assert.IsTrue(s.Trial);
            var t = s.TrialNow;
            CollectionAssert.AreEqual(new[] { "engine" }.Concat(t.Cars).ToArray(), s.Run.cars.Select(c => c.type).ToArray());
            Assert.Less(s.DamageMult, strong, "the workshop stays at home");
            Assert.AreEqual(t.Level, s.CarLevel(t.Cars[0]));
            Assert.IsFalse(s.Couple("gatling"), "the trial's own train");
            Assert.IsTrue(s.Run.cards.Exists(p => p.card == t.Cards[0]), "with the week's card");
            uint seed = s.State.seed;

            double steel = s.State.steel; int best = s.State.best[0]; long runs = s.State.runs;
            for (int i = 0; i < 40000 && !s.RunEnded; i++) { s.Tick(0.5); if (s.AtStation) { s.TakeCard(0); s.Depart(); } }
            Assert.IsTrue(s.RunEnded);
            Assert.IsFalse(s.CanRevive, "no second go");
            int reached = s.Run.mile - 1;
            Assert.IsTrue(s.CloseRun());
            Assert.AreEqual(steel, s.State.steel, "no steel from a trial");
            Assert.AreEqual(best, s.State.best[0], "nor a best on the line");
            Assert.AreEqual(runs, s.State.runs);
            Assert.AreEqual(reached, s.TrialBest);
            if (reached >= 10) Assert.Contains("Trial Runner", s.State.titles);

            // Another player this week sets off with the same luck.
            var o = TestGame.New(); o.State.best[0] = 30; o.State.seed = 12345;
            o.StartTrial();
            Assert.AreEqual(seed, o.State.seed);
        }

        [Test]
        public void TheDepotGrowsWithThePlayer()
        {
            var s = TestGame.New();
            var a = s.Depot;
            Assert.AreEqual(1, a.Sidings); Assert.AreEqual(0, a.Parked); Assert.AreEqual(0, a.Trophies); Assert.AreEqual(1, a.Workers);
            s.State.engines = 2; s.State.bosses = 17; s.State.runs = 45; s.State.best[0] = 120;
            var b = s.Depot;
            Assert.AreEqual(3, b.Sidings); Assert.AreEqual(2, b.Parked); Assert.AreEqual(3, b.Trophies); Assert.AreEqual(4, b.Workers);
        }

        [Test]
        public void TheExpeditionTrackFillsWithMilesAndStartsAgainEachSeason()
        {
            var s = TestGame.New();
            Assert.AreEqual(Sim.Tiers, Sim.Steps.Count);
            Assert.AreEqual("skin:pathfinder", Sim.Steps[Sim.Tiers - 1].Gift);
            Assert.IsNotNull(Content.Skin("pathfinder"));
            s.StartRun();
            for (int i = 0; i < 4000 && !s.RunEnded && s.Run.mile < 12; i++) { s.Tick(0.5); if (s.AtStation) { s.TakeCard(0); s.Depart(); } }
            Assert.Greater(s.ExpPoints, 0, "every mile counts");

            s.State.expPoints = Sim.PerTier * 15 + 1;
            Assert.AreEqual(15, s.ExpTier);
            Assert.AreEqual(15, s.StepsReady);
            int gems = s.State.gems;
            Assert.IsTrue(s.ClaimStep(3));
            Assert.AreEqual(gems + Sim.Steps[2].Amount, s.State.gems);
            Assert.IsFalse(s.ClaimStep(3), "once");
            Assert.IsFalse(s.ClaimStep(16), "not reached");
            Assert.IsTrue(s.ClaimStep(15)); Assert.Contains("Expedition Hand", s.State.titles);
            double steel = s.State.steel; Assert.IsTrue(s.ClaimStep(1)); Assert.Greater(s.State.steel, steel);

            // A new season: the points and the steps start again, the title stays.
            var now = s.RealNow(); s.RealNow = () => now + 86400 * Sim.SeasonDays;
            Assert.AreEqual(0, s.ExpPoints);
            Assert.IsFalse(s.StepClaimed(3));
            Assert.Contains("Expedition Hand", s.State.titles);
        }

        [Test]
        public void EveryThemePaintsTheWholeTrain()
        {
            var sold = Content.Skins.Where(k => k.Gems > 0).ToList();
            Assert.GreaterOrEqual(sold.Count, 5, "two new ones in the shop");
            foreach (var k in Content.Skins)
                Assert.IsTrue(k.Gems > 0 || k.Engines > 0 || k.From != null || k.Id == "", k.Id + " can be had somehow");
        }
    }
}
