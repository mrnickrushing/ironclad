#if UNITY_5_3_OR_NEWER
using System.Linq;
using Ironclad.Core;
using NUnit.Framework;
using UnityEngine;

namespace Ironclad.Tests
{
    /// <summary>The save as the game writes it (JsonUtility): a run in progress comes back and plays on the same.</summary>
    public class SaveTests
    {
        [Test]
        public void ARunInProgressSurvivesTheSaveAndPlaysOnTheSame()
        {
            var a = TestGame.New();
            a.State.cars.Add(new Level { id = "cannon", level = 2 }); a.State.train.Add("cannon");
            a.StartRun();
            for (int i = 0; i < 300; i++) a.Tick(0.2);
            a.FieldUpgrade(1);
            var copy = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(a.State));
            var b = new Sim(copy); b.RealNow = a.RealNow;
            Assert.IsNotNull(b.Run);
            Assert.AreEqual(a.Run.mile, b.Run.mile);
            Assert.AreEqual(a.Run.enemies.Count, b.Run.enemies.Count);
            Assert.AreEqual(a.Run.cars.Select(c => c.type).ToArray(), b.Run.cars.Select(c => c.type).ToArray());
            for (int i = 0; i < 200; i++) { a.Tick(0.2); b.Tick(0.2); }
            Assert.AreEqual(a.Run.kills, b.Run.kills);
            Assert.AreEqual(a.Run.scrip, b.Run.scrip, 1e-6);
        }

        [Test]
        public void NoRunComesBackAsNoRun()
        {
            var a = TestGame.New();
            var copy = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(a.State));
            var b = new Sim(copy);
            Assert.IsNull(b.Run);
            Assert.IsTrue(b.StartRun());
        }
    }
}
#endif
