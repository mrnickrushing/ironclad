using System.Linq;
using Ironclad.Core;
using NUnit.Framework;

namespace Ironclad.Tests
{
    /// <summary>The four bosses, the line's choices between stations, cards that change how a wagon fights and the
    /// wagons' tiers.</summary>
    public class DepthTests
    {
        static Sim Quiet(params string[] wagons)
        {
            var s = TestGame.New();
            s.State.upgrades.Add(new Level { id = "slots", level = 6 });
            foreach (var id in wagons.Distinct()) if (!s.Built(id)) s.State.cars.Add(new Level { id = id, level = 1 });
            s.State.train.Clear(); s.State.train.AddRange(wagons);
            s.StartRun();
            s.Run.queue.Clear(); s.Run.spawnGap = 1e9;
            return s;
        }

        static EnemyState Put(Sim s, string kind, double x, double z, double hp = 1000)
        {
            var e = new EnemyState { id = 800 + s.Run.enemies.Count, kind = kind, x = x, z = z, hp = hp, hpMax = hp, damage = 10, cooldown = 99 };
            s.Run.enemies.Add(e);
            return e;
        }

        [Test]
        public void EachStationHasItsOwnBoss()
        {
            Assert.AreEqual("boss", Sim.BossKind(10));
            Assert.AreEqual("convoy", Sim.BossKind(20));
            Assert.AreEqual("longgun", Sim.BossKind(30));
            Assert.AreEqual("mender", Sim.BossKind(40));
            Assert.AreEqual("boss", Sim.BossKind(50), "and round again");
            foreach (var id in Content.Bosses)
            {
                var d = Content.Enemy(id);
                Assert.IsTrue(d.Boss, id);
                Assert.IsFalse(string.IsNullOrEmpty(d.Hint), id + " says how to beat it");
                Assert.LessOrEqual(d.Range, 13, id + " comes within reach of the deck gun");
            }
        }

        [Test]
        public void TheConvoyRidesWithItsEscortAndIsShieldedByIt()
        {
            var s = Quiet("gatling");
            s.Run.mile = 20; s.Run.mileTime = 2.5;
            s.Tick(0.1);
            Assert.AreEqual("convoy", s.Run.bossKind);
            Assert.AreEqual(3, s.Run.enemies.Count(e => e.escort && e.kind == "warwagon"));

            double Lost(bool escort)
            {
                var t = Quiet("gatling");
                var c = Put(t, "convoy", 2, 6);
                if (escort) { var g = Put(t, "warwagon", 2, 80, 1e9); g.escort = true; }
                for (int i = 0; i < 20; i++) t.Tick(0.25);
                return c.hpMax - c.hp;
            }
            double alone = Lost(false), guarded = Lost(true);
            Assert.Greater(alone, 0);
            Assert.AreEqual(alone * 0.25, guarded, alone * 0.05, "three quarters of the damage is turned while the escort lives");
        }

        [Test]
        public void TheLongGunWarnsBeforeItFiresAndTheWhistleSpoilsIt()
        {
            var s = Quiet("gatling", "cannon");
            int warned = 0, spoiled = 0;
            s.Warning += (x, z, w, t) => warned++;
            s.Spoiled += e => spoiled++;
            var g = Put(s, "longgun", 3, 10);
            g.cooldown = 0; g.damage = 30;
            s.Tick(0.1);
            Assert.AreEqual(1, warned, "it marks a wagon first");
            Assert.Greater(g.windup, 0);
            double hull = s.Run.hull;
            s.Run.whistleIn = 0;
            Assert.IsTrue(s.Whistle());
            Assert.AreEqual(1, spoiled);
            Assert.AreEqual(0, g.windup);
            for (int i = 0; i < 10; i++) s.Tick(0.25);
            Assert.AreEqual(hull, s.Run.hull, 1e-9, "the spoiled shot never lands");

            // Left alone, the shot lands on the marked wagon and the ones either side.
            var u = Quiet("gatling", "cannon");
            var h = Put(u, "longgun", 3, 10); h.cooldown = 0; h.damage = 30;
            u.Tick(0.1);
            int aim = h.aimCar;
            double before = u.Run.cars.Sum(c => c.plating) + u.Run.hull;
            for (int i = 0; i < 12; i++) u.Tick(0.25);
            Assert.Less(u.Run.cars.Sum(c => c.plating) + u.Run.hull, before - 30, "more than one car took the shot (aimed at " + aim + ")");
        }

        [Test]
        public void TheMendingWagonHealsTheRaidersNearIt()
        {
            var s = Quiet("gatling");
            Put(s, "mender", 0, 60);
            var near = Put(s, "warwagon", 4, 60); near.hp = 500;
            var far = Put(s, "warwagon", 40, 60); far.hp = 500;
            for (int i = 0; i < 8; i++) s.Tick(0.25);
            Assert.Greater(near.hp, 500);
            Assert.AreEqual(500, far.hp, 1e-9, "only within 12 m");
            Assert.LessOrEqual(near.hp, near.hpMax);
        }

        [Test]
        public void TheLineOffersAChoiceHalfwayToEachStation()
        {
            var s = Quiet("gatling");
            Assert.IsNull(s.Choice);
            s.Run.mile = 4; s.Run.mileTime = 1e6;
            s.Tick(0.1);
            Assert.AreEqual(5, s.Run.mile);
            Assert.IsNotNull(s.Choice, "an offer at mile 5");
            for (int i = 0; i < 70 && s.Choice != null; i++) s.Tick(0.25);
            Assert.IsNull(s.Choice, "it doesn't wait for ever");
            Assert.AreEqual("", s.Run.boostKind, "and letting it go by costs nothing");
            Assert.AreEqual(0, s.State.choicesTaken);
        }

        [Test]
        public void TheGulchPaysDoubleAndHitsHarder()
        {
            var s = Quiet("gatling");
            s.Run.choice = "gulch"; s.Run.choiceIn = 10;
            var e = Put(s, "rider", 5, 30);
            double dmg = e.damage;
            Assert.IsTrue(s.Choose(true));
            Assert.AreEqual(dmg * 1.3, e.damage, 1e-9);
            Assert.AreEqual(1, s.State.choicesTaken);
            double steel = s.Run.steel;
            s.Run.enemies.Clear(); s.Run.mileTime = 1e6; s.Tick(0.1);
            double gained = s.Run.steel - steel;
            Assert.AreEqual(Sim.MileSteel(s.Route, s.Run.mile - 1) * s.SteelMult * 2, gained, 1e-6, "double steel for the mile");
            Assert.IsFalse(s.Choose(true), "nothing on offer now");

            var f = Quiet("gatling", "cannon");
            f.Run.choice = "fitter";
            f.Choose(true);
            Assert.AreEqual(2, f.Run.cars.Sum(c => c.field), "the fitter works on two guns");
        }

        [Test]
        public void AWagonsOwnCardOnlyComesWithTheWagon()
        {
            var s = Quiet("gatling");
            for (int k = 0; k < 40; k++)
            {
                s.Run.phase = (int)Phase.Station;
                typeof(Sim).GetMethod("DealCards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(s, null);
                foreach (var p in s.Run.offer)
                {
                    var c = Content.Card(p.card);
                    Assert.IsFalse(c.Needs == "dynamo" || c.Needs == "splash", c.Id + " needs a wagon this train hasn't got");
                }
            }
            Assert.IsTrue(Content.Cards.Any(c => c.Behaviour && c.Needs == "gatling"), "the gatling has one of its own");
        }

        [Test]
        public void GlassCannonTradesHullForPower()
        {
            var s = Quiet("gatling");
            double hull = s.HullMax, dmg = s.DamageMult;
            s.Run.cards.Add(new Pick { card = "glass", rarity = 0 });
            Assert.AreEqual(hull * 0.75, s.HullMax, 1e-6);
            Assert.Greater(s.DamageMult, dmg * 1.3);
        }

        [Test]
        public void SteamRechargeResetsTheGunsWhenTheWhistleBlows()
        {
            var s = Quiet("gatling");
            double every = s.WhistleEvery, rate = s.RateMult;
            s.Run.cards.Add(new Pick { card = "recharge", rarity = 0 });
            Assert.AreEqual(every + 15, s.WhistleEvery, 1e-9, "the whistle takes longer to come back");
            foreach (var c in s.Run.cars) c.cooldown = 5;
            s.Run.whistleIn = 0;
            Assert.IsTrue(s.Whistle());
            Assert.IsTrue(s.Run.cars.All(c => c.cooldown == 0), "every gun is ready at once");
            Assert.Greater(s.RateMult, rate, "and fires faster for a while");
        }

        [Test]
        public void IronDecoyThickensTheLastWagon()
        {
            var s = Quiet("gatling", "cannon");
            var last = s.Run.cars[s.Run.cars.Count - 1];
            double plate = last.platingMax, front = s.Run.cars[1].platingMax;
            s.Run.phase = (int)Phase.Station; s.Run.offer.Clear();
            s.Run.offer.Add(new Pick { card = "decoy", rarity = 0 });
            Assert.IsTrue(s.TakeCard(0));
            Assert.AreEqual(plate * 2, last.platingMax, 1e-6);
            Assert.AreEqual(front, s.Run.cars[1].platingMax, 1e-9, "only the last one");
        }

        [Test]
        public void WagonsLookHeavierAsTheyLevel()
        {
            var s = TestGame.New();
            Assert.AreEqual(0, s.CarTier("gatling"));
            s.State.cars.First(c => c.id == "gatling").level = 25;
            Assert.AreEqual(2, s.CarTier("gatling"));
            s.State.cars.First(c => c.id == "gatling").level = 60;
            Assert.AreEqual(3, s.CarTier("gatling"));
        }
    }
}
