using System;
using System.Collections.Generic;

namespace Ironclad.Core
{
    /// <summary>What the next unlock is and how close it is, for the end of a run.</summary>
    public sealed class UnlockInfo
    {
        public string Name;     // "the Fitters' Car plans", "Badlands Cut"
        public int Mile;        // the mile that opens it
        public int Have;        // the best mile toward it so far
        public string Where;    // "on any line" or the line to reach it on
    }

    /// <summary>
    /// A new engine (the long game): once the steel this engine has earned is worth a charter, the workshop and the
    /// wagons start again from a Gatling Car, and every charter makes steel and damage a tenth stronger for good.
    /// Kept: gems, seals and their perks, the lines opened and plans found, awards, purchases and liveries.
    /// Also the engine's liveries, and the numbers the end-of-run card shows.
    /// </summary>
    public sealed partial class Sim
    {
        /// <summary>Steel earned since this engine was built.</summary>
        public double EngineSteel { get { return Math.Max(0, State.lifetime - State.lifetimeAtRenew); } }

        /// <summary>Charters a new engine would bring now.</summary>
        public int ChartersOnRenew { get { return (int)Math.Floor(Math.Sqrt(EngineSteel / Content.CharterSteel)); } }

        /// <summary>Steel this engine still needs for one more charter than <see cref="ChartersOnRenew"/>.</summary>
        public double SteelToNextCharter
        {
            get { int n = ChartersOnRenew + 1; return Math.Max(0, Content.CharterSteel * n * n - EngineSteel); }
        }

        /// <summary>The workshop shows a new engine once Badlands Cut is open (or after the first one).</summary>
        public bool RenewShown { get { return State.engines > 0 || RouteOpen(1); } }
        public bool CanRenew { get { return State.run == null && ChartersOnRenew >= 1; } }

        /// <summary>Steel and damage, for the charters held.</summary>
        public double CharterMult { get { return Trial ? 1 : 1 + Content.CharterBonus * State.charters; } }

        /// <summary>Builds a new engine. Returns the charters it brought (0: not now).</summary>
        public int Renew()
        {
            if (!CanRenew) return 0;
            int n = ChartersOnRenew;
            State.charters += n;
            State.engines++;
            State.lifetimeAtRenew = State.lifetime;
            State.steel = 0;
            State.upgrades.Clear();
            State.cars.Clear();
            State.cars.Add(new Level { id = "engine", level = 1 });
            State.cars.Add(new Level { id = "gatling", level = 1 });
            State.train.Clear(); State.train.Add("gatling");
            State.speed = 0; State.express = false; State.route = 0;
            foreach (var k in Content.Skins) if (k.Engines > 0 && k.Engines <= State.engines && !State.skins.Contains(k.Id)) State.skins.Add(k.Id);
            Touch();
            return n;
        }

        // ---- liveries ----

        public SkinDef Skin { get { return Content.Skin(State.skin) ?? Content.Skins[0]; } }
        public bool SkinOwned(string id) { return string.IsNullOrEmpty(id) || State.skins.Contains(id); }

        /// <summary>Buys a livery for gems and puts it on the engine.</summary>
        public bool BuySkin(string id)
        {
            var k = Content.Skin(id);
            if (k == null || SkinOwned(id) || k.Gems <= 0 || State.gems < k.Gems) return false;
            State.gems -= k.Gems;
            State.skins.Add(id);
            State.skin = id;
            Touch();
            return true;
        }

        public bool UseSkin(string id)
        {
            if (Content.Skin(id) == null || !SkinOwned(id) || State.skin == (id ?? "")) return false;
            State.skin = id ?? "";
            Touch();
            return true;
        }

        // ---- the end of a run ----

        /// <summary>The next thing a further run opens: a wagon's plans or a new line, whichever is nearer (null: all open).</summary>
        public UnlockInfo NextUnlock
        {
            get
            {
                UnlockInfo best = null;
                var plans = NextPlans;
                if (plans != null) best = new UnlockInfo { Name = "the " + plans.Name + " plans", Mile = plans.Mile, Have = BestMile, Where = "on any line" };
                for (int i = 1; i < Content.Routes.Count; i++)
                {
                    if (RouteOpen(i)) continue;
                    var rt = Content.Routes[i];
                    var line = new UnlockInfo { Name = rt.Name, Mile = rt.Opens, Have = State.best[i - 1], Where = "on " + Content.Routes[i - 1].Name };
                    if (best == null || line.Mile - line.Have < best.Mile - best.Have) best = line;
                    break;      // only the next line can open
                }
                return best;
            }
        }

        /// <summary>The run's near miss, for the last card: "3 raiders short of mile 40". Null when there is nothing to say.</summary>
        public string NearMiss
        {
            get
            {
                var r = State.run;
                if (r == null || r.phase != (int)Phase.Over || r.retired) return null;
                if (r.bossLeft >= 0)
                {
                    int pct = Math.Max(1, (int)Math.Ceiling(r.bossLeft * 100));
                    var boss = Content.Enemy(r.bossKind.Length > 0 ? r.bossKind : "boss");
                    return (pct <= 25 ? "So close: " : "") + boss.Name + " was down to " + pct + "%";
                }
                if (r.left <= 0) return null;
                string what = r.mile % Content.StationEvery == 0 ? "the station" : "mile " + (r.mile + 1);
                return (r.left <= 5 ? "So close: " : "") + r.left + (r.left == 1 ? " raider" : " raiders") + " short of " + what;
            }
        }

        /// <summary>Notes how close the run came, as the hull goes.</summary>
        void NoteNearMiss()
        {
            var r = State.run;
            r.left = r.queue.Count; r.bossLeft = -1;
            foreach (var e in r.enemies)
            {
                if (e.hp <= 0) continue;
                if (Content.Enemy(e.kind).Boss) r.bossLeft = e.hpMax > 0 ? e.hp / e.hpMax : 0;
                else r.left++;
            }
            if (BossMile && !r.bossOut) { r.bossLeft = 1; r.bossKind = BossKind(r.mile); }
        }

        // ---- free deals at a station ----

        public bool FreeReroll { get { return AtStation && !State.run.rerolled; } }
    }
}
