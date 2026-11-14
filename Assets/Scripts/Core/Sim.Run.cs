using System;
using System.Collections.Generic;

namespace Ironclad.Core
{
    /// <summary>
    /// A run: the train rolls a mile at a time while raiders ride in from both sides and along the track. Wagons fire by
    /// themselves; raiders strike the nearest car that still has plating, and the engine's hull when they reach it. Every
    /// tenth mile a boss rides, and when it is down the train stands at a station: a card for the rest of the run, the
    /// plating made good, and the wagons can be recoupled. The run ends when the hull is gone.
    /// </summary>
    public sealed partial class Sim
    {
        public RouteDef Route { get { return Content.Routes[State.run != null ? State.run.route : State.route]; } }

        /// <summary>Where a car's middle is along the track. The engine is at 0, wagons trail behind it.</summary>
        public static double CarX(int index) { return -index * Content.CarLength; }

        CarState NewCar(string type)
        {
            var c = new CarState { type = type };
            if (type != "engine") { c.platingMax = PlateOf(c); c.plating = c.platingMax; }
            return c;
        }

        /// <summary>Sets off on the chosen line. False if a run is already under way.</summary>
        public bool StartRun()
        {
            if (State.run != null) return false;
            if (!RouteOpen(State.route)) State.route = 0;
            var r = new RunState { route = State.route };
            State.run = r;
            r.cars.Add(NewCar("engine"));
            foreach (var id in State.train) if (r.cars.Count - 1 < Slots) r.cars.Add(NewCar(id));
            r.hullMax = HullMax; r.hull = r.hullMax;
            r.scrip = StartScrip;
            r.whistleIn = 0;
            FillQueue();
            Touch();
            return true;
        }

        double MileHp(int mile) { return Math.Pow(Content.HpPerMile, mile - 1) * Route.Hp; }
        double MileDamage(int mile) { return Math.Pow(Content.DamagePerMile, mile - 1) * Route.Damage; }
        /// <summary>Steel for finishing this mile on this line, before the player's own multipliers.</summary>
        public static double MileSteel(RouteDef route, int mile) { return Content.SteelPerMileBase * Math.Pow(Content.SteelPerMile, mile - 1) * route.Steel; }
        public bool BossMile { get { return State.run != null && State.run.mile % Content.StationEvery == 0; } }

        void FillQueue()
        {
            var r = State.run;
            r.queue.Clear();
            double budget = 5 + 1.5 * r.mile;
            if (r.mile % Content.StationEvery == 0) budget *= 0.5;      // the Baron's carriage rides with a smaller escort
            var pool = Content.Enemies.FindAll(e => !e.Boss && e.Mile <= r.mile);
            while (budget >= 1 && r.queue.Count < 60)
            {
                double total = 0;
                foreach (var e in pool) if (e.Threat <= budget) total += Weight(e);
                if (total <= 0) break;
                double pick = Roll() * total;
                foreach (var e in pool)
                {
                    if (e.Threat > budget) continue;
                    pick -= Weight(e);
                    if (pick > 0) continue;
                    r.queue.Add(e.Id); budget -= e.Threat;
                    break;
                }
            }
            r.spawnGap = Content.SpawnSeconds / Math.Max(1, r.queue.Count);
            r.spawnIn = 1.5;
            r.bossOut = false;
        }

        static double Weight(EnemyDef e) { return e.Threat == 1 ? 3 : e.Threat >= 10 ? 0.5 : 1; }

        EnemyState Spawn(string kind)
        {
            var r = State.run;
            var d = Content.Enemy(kind);
            double back = CarX(r.cars.Count - 1);
            var e = new EnemyState { id = r.nextId++, kind = kind };
            e.hpMax = e.hp = d.Hp * MileHp(r.mile);
            e.damage = d.Damage * MileDamage(r.mile);
            e.cooldown = d.Interval * 0.5;
            if (d.Rail)
            {
                bool ahead = Roll() < 0.5;
                e.x = ahead ? Content.SpawnRing : back - Content.SpawnRing;
                e.z = (Roll() - 0.5) * 1.2;
            }
            else
            {
                double side = Roll() < 0.5 ? -1 : 1;
                e.x = back / 2 + (Roll() - 0.5) * (-back + 34);
                e.z = side * Content.SpawnRing * (0.75 + 0.25 * Roll());
            }
            r.enemies.Add(e);
            Arrived?.Invoke(e);
            return e;
        }

        /// <summary>How far a point is from a car's body (a 5 m stretch of the track).</summary>
        static double Reach(double x, double z, int car)
        {
            double cx = CarX(car);
            double dx = Math.Max(0, Math.Abs(x - cx) - 2.5);
            return Math.Sqrt(dx * dx + z * z);
        }

        const double Body = 1.2;        // a car's half width: melee reach is measured from its side

        void StepRun(double dt)
        {
            var r = State.run;
            r.time += dt; r.mileTime += dt;
            if (r.whistleIn > 0) r.whistleIn -= dt;

            // Raiders ride in.
            if (r.queue.Count > 0)
            {
                r.spawnIn -= dt;
                if (r.spawnIn <= 0) { Spawn(r.queue[0]); r.queue.RemoveAt(0); r.spawnIn += r.spawnGap; }
            }
            if (BossMile && !r.bossOut && r.mileTime >= 2)
            {
                r.bossOut = true;
                var b = Spawn("boss");
                double grow = 1 + 0.6 * (r.mile / Content.StationEvery - 1);
                b.hpMax = b.hp = b.hp * grow;
            }

            // Raiders close in and strike.
            double slow = 1 - Math.Min(0.6, CardSum("slow"));
            for (int i = 0; i < r.enemies.Count; i++)
            {
                var e = r.enemies[i];
                if (e.hp <= 0) continue;
                if (e.stun > 0) { e.stun -= dt; continue; }
                var d = Content.Enemy(e.kind);
                int target = 0; double best = double.MaxValue;
                for (int c = 0; c < r.cars.Count; c++)
                {
                    if (r.cars[c].Out) continue;
                    double dist = Reach(e.x, e.z, c);
                    if (dist < best) { best = dist; target = c; }
                }
                e.striking = best <= d.Range + Body;
                if (!e.striking)
                {
                    double tx = Math.Max(CarX(target) - 2.5, Math.Min(CarX(target) + 2.5, e.x));
                    double dx = tx - e.x, dz = -e.z, len = Math.Sqrt(dx * dx + dz * dz);
                    if (len > 1e-6)
                    {
                        double move = Math.Min(d.Speed * slow * dt, len);
                        e.x += dx / len * move; e.z += dz / len * move;
                        e.facing = Math.Atan2(dx, dz);
                    }
                    continue;
                }
                e.cooldown -= dt;
                if (e.cooldown > 0) continue;
                e.cooldown = d.Interval;
                Strike(target, e.damage, e);
                if (d.Blast) { Burst?.Invoke(e.x, e.z, 4); e.hp = 0; }
                if (r.phase != (int)Phase.Rolling) return;
            }

            // The train fires.
            for (int c = 0; c < r.cars.Count; c++)
            {
                var car = r.cars[c];
                var def = Content.Car(car.type);
                if (car.Out || def.Support) continue;
                if (car.cooldown > 0) { car.cooldown -= dt; if (car.cooldown > 0) continue; }
                if (Fire(c, car, def)) car.cooldown += def.Interval / (RateMult * Haste(c));
                else car.cooldown = 0;
            }

            // Shells land.
            for (int i = r.shells.Count - 1; i >= 0; i--)
            {
                var s = r.shells[i];
                s.t -= dt;
                if (s.t > 0) continue;
                r.shells.RemoveAt(i);
                double wide = s.splash * (1 + CardSum("splash"));
                Burst?.Invoke(s.x, s.z, wide);
                foreach (var e in r.enemies)
                    if (e.hp > 0 && Sq(e.x - s.x, e.z - s.z) <= wide * wide) Hurt(e, s.damage, s.crit);
            }

            // Mending.
            double fitters = CardSum("mend");
            for (int c = 1; c < r.cars.Count; c++)
            {
                var car = r.cars[c];
                if (fitters > 0) Mend(car, car.platingMax * fitters * dt);
                var def = Content.Car(car.type);
                if (def.Mend <= 0 || car.Out) continue;
                CarState worst = null;
                for (int k = 1; k < r.cars.Count; k++)
                {
                    var o = r.cars[k];
                    if (o.plating < o.platingMax && (worst == null || o.plating / o.platingMax < worst.plating / worst.platingMax)) worst = o;
                }
                if (worst != null) Mend(worst, worst.platingMax * def.Mend * SupportMult(car) * dt);
                else r.hull = Math.Min(r.hullMax, r.hull + r.hullMax * def.Mend * 0.2 * SupportMult(car) * dt);
            }
            if (HullMend > 0) r.hull = Math.Min(r.hullMax, r.hull + r.hullMax * HullMend * dt);

            // The fallen are cleared away.
            r.enemies.RemoveAll(e => e.hp <= 0);

            // The mile is done once its time is up, its raiders have all ridden in and, on a boss's mile, the boss is down.
            if (r.mileTime >= Content.MileSeconds && r.queue.Count == 0 && (!BossMile || r.bossOut && !r.enemies.Exists(e => Content.Enemy(e.kind).Boss)))
                FinishMile();
        }

        static double Sq(double x, double z) { return x * x + z * z; }

        void Mend(CarState car, double amount)
        {
            car.plating = Math.Min(car.platingMax, car.plating + amount);
            if (car.down && car.plating >= car.platingMax * 0.25) { car.down = false; Touch(); }
        }

        /// <summary>How much faster this car fires for the supply wagons coupled next to it.</summary>
        double Haste(int car)
        {
            double h = 1;
            var r = State.run;
            for (int k = car - 1; k <= car + 1; k += 2)
            {
                if (k < 1 || k >= r.cars.Count || r.cars[k].Out) continue;
                h += Content.Car(r.cars[k].type).Haste * SupportMult(r.cars[k]);
            }
            return h;
        }

        /// <summary>The share of a blow this car takes, for the armoured wagons coupled next to it.</summary>
        double Shielded(int car)
        {
            double s = 1;
            var r = State.run;
            for (int k = car - 1; k <= car + 1; k += 2)
            {
                if (k < 1 || k >= r.cars.Count || r.cars[k].Out) continue;
                s *= 1 - Math.Min(0.6, Content.Car(r.cars[k].type).Shield * SupportMult(r.cars[k]));
            }
            return s;
        }

        void Strike(int index, double damage, EnemyState by)
        {
            var r = State.run;
            var car = r.cars[index];
            damage *= Shielded(index);
            if (index == 0) r.hull -= damage;
            else
            {
                car.plating -= damage;
                if (car.plating <= 0) { car.plating = 0; car.down = true; Touch(); }
            }
            Struck?.Invoke(index, damage);
            double vents = CardSum("vents");
            if (vents > 0 && by != null && Content.Enemy(by.kind).Range < 3) Hurt(by, damage * vents * MileHp(r.mile) / Math.Max(1e-9, MileDamage(r.mile)), false);
            if (r.hull <= 0) { r.hull = 0; EndRun(); }
        }

        bool Fire(int index, CarState car, CarDef def)
        {
            var r = State.run;
            double cx = CarX(index), range = def.Range * RangeMult;
            bool crit = Roll() < CritChance + def.Crit;
            double damage = ShotDamage(car) * (crit ? CritDamage : 1);

            if (def.Aim == Aim.Around)
            {
                bool any = false;
                foreach (var e in r.enemies)
                    if (e.hp > 0 && Reach(e.x, e.z, index) <= range) { Hurt(e, damage, crit); any = true; }
                if (any) Fired?.Invoke(index, cx, 0, crit);
                return any;
            }

            EnemyState target = null; double score = 0;
            foreach (var e in r.enemies)
            {
                if (e.hp <= 0) continue;
                double dist = Math.Sqrt(Sq(e.x - cx, e.z));
                if (dist > range || dist < def.MinRange) continue;
                if (e.id == r.mark) { target = e; break; }
                // Guns that fire at the nearest deal first with whoever is already hitting the train, weakest first (one
                // fewer striking soonest): a gun wagon standing off and shooting is not left alone for a closer rider.
                double s = def.Aim == Aim.Strongest ? e.hp : def.Aim == Aim.Farthest ? dist : e.striking ? 1e6 - e.hp : -dist;
                if (target == null || s > score) { target = e; score = s; }
            }
            if (target == null) return false;
            Fired?.Invoke(index, target.x, target.z, crit);

            if (def.Flight > 0)
            {
                r.shells.Add(new Shell { car = index, x = target.x, z = target.z, t = def.Flight, damage = damage, splash = def.Splash, crit = crit });
                return true;
            }
            Hurt(target, damage, crit);
            var from = target;
            _chained.Clear(); _chained.Add(target.id);
            for (int j = 0; j < def.Chain; j++)
            {
                EnemyState next = null; double near = 49;
                foreach (var e in r.enemies)
                {
                    if (e.hp <= 0 || _chained.Contains(e.id)) continue;
                    double dd = Sq(e.x - from.x, e.z - from.z);
                    if (dd < near) { near = dd; next = e; }
                }
                if (next == null) break;
                damage *= 0.7;
                Hurt(next, damage, crit);
                Fired?.Invoke(index, next.x, next.z, crit);
                _chained.Add(next.id); from = next;
            }
            return true;
        }
        readonly List<int> _chained = new List<int>();

        void Hurt(EnemyState e, double damage, bool crit)
        {
            if (e.hp <= 0) return;
            var r = State.run;
            e.hp -= damage;
            Hit?.Invoke(e, damage, crit);
            if (e.hp > 0) return;
            e.hp = 0;
            var d = Content.Enemy(e.kind);
            double scrip = d.Bounty * Math.Pow(Content.BountyPerMile, r.mile - 1) * ScripMult;
            r.scrip += scrip; r.kills++; State.kills++;
            if (d.Boss) { State.bosses++; r.steel += Content.BossSteel * MileSteel(Route, r.mile) / Content.SteelPerMileBase * SteelMult; }
            if (r.mark == e.id) r.mark = 0;
            Fell?.Invoke(e, scrip);
        }

        void FinishMile()
        {
            var r = State.run;
            double steel = MileSteel(Route, r.mile) * SteelMult;
            r.steel += steel;
            State.miles++;
            int before = BestMile;
            if (r.mile > State.best[r.route]) State.best[r.route] = r.mile;
            if (BestMile > before) foreach (var d in Content.Cars) if (d.Mile > before && d.Mile <= BestMile && d.Id != "engine" && !Built(d.Id)) Plans?.Invoke(d);
            MileDone?.Invoke(r.mile, steel);
            if (BossMile) { EnterStation(); return; }
            r.mile++; r.mileTime = 0;
            FillQueue();
        }

        // ---- stations ----

        void EnterStation()
        {
            var r = State.run;
            r.phase = (int)Phase.Station;
            r.enemies.Clear(); r.shells.Clear(); r.queue.Clear(); r.mark = 0;
            r.hull = Math.Min(r.hullMax, r.hull + r.hullMax * Content.StationHull);
            foreach (var c in r.cars) { c.plating = c.platingMax; c.down = false; c.cooldown = 0; }
            DealCards();
            Touch();
        }

        void DealCards()
        {
            var r = State.run;
            r.offer.Clear();
            var left = new List<CardDef>(Content.Cards);
            for (int i = 0; i < 3 && left.Count > 0; i++)
            {
                int k = (int)(Roll() * left.Count);
                double roll = Roll();
                int rarity = roll < Content.RarityOdds[2] ? 2 : roll < Content.RarityOdds[2] + Content.RarityOdds[1] ? 1 : 0;
                r.offer.Add(new Pick { card = left[k].Id, rarity = rarity });
                left.RemoveAt(k);
            }
        }

        public bool AtStation { get { return State.run != null && State.run.phase == (int)Phase.Station; } }

        /// <summary>Takes one of the cards on the table. The others go back in the box.</summary>
        public bool TakeCard(int index)
        {
            var r = State.run;
            if (!AtStation || index < 0 || index >= r.offer.Count) return false;
            var p = r.offer[index];
            r.offer.Clear();
            r.cards.Add(p);
            State.cardsTaken++;
            if (p.card == "hull") { r.hullMax = HullMax; r.hull = r.hullMax; }
            if (p.card == "plate") foreach (var c in r.cars) if (c.platingMax > 0) { c.platingMax = PlateOf(c); c.plating = c.platingMax; }
            Touch();
            return true;
        }

        public const int RerollGems = 5;

        /// <summary>Three other cards, for gems.</summary>
        public bool RerollCards()
        {
            var r = State.run;
            if (!AtStation || r.offer.Count == 0 || State.gems < RerollGems) return false;
            State.gems -= RerollGems;
            DealCards();
            Touch();
            return true;
        }

        /// <summary>Leaves the station for the next mile. A card must have been taken.</summary>
        public bool Depart()
        {
            var r = State.run;
            if (!AtStation || r.offer.Count > 0) return false;
            r.phase = (int)Phase.Rolling;
            r.mile++; r.mileTime = 0;
            FillQueue();
            Touch();
            return true;
        }

        /// <summary>For a player who isn't looking: takes the rarest card on the table and rolls on.</summary>
        public void PassStation()
        {
            var r = State.run;
            if (!AtStation) return;
            if (r.offer.Count > 0)
            {
                int best = 0;
                for (int i = 1; i < r.offer.Count; i++) if (r.offer[i].rarity > r.offer[best].rarity) best = i;
                TakeCard(best);
            }
            Depart();
        }

        // ---- what the player does in the fight ----

        /// <summary>Points a raider out: every gun that can reach it fires at it first.</summary>
        public bool Mark(int enemy)
        {
            var r = State.run;
            if (r == null || !r.enemies.Exists(e => e.id == enemy && e.hp > 0)) return false;
            r.mark = r.mark == enemy ? 0 : enemy;
            return true;
        }

        public bool WhistleReady { get { return Running && State.run.whistleIn <= 0; } }

        /// <summary>The whistle: a blast of steam that throws back and stuns every raider near the train.</summary>
        public bool Whistle()
        {
            if (!WhistleReady) return false;
            var r = State.run;
            foreach (var e in r.enemies)
            {
                if (e.hp <= 0) continue;
                int near = 0; double best = double.MaxValue;
                for (int c = 0; c < r.cars.Count; c++) { double d = Reach(e.x, e.z, c); if (d < best) { best = d; near = c; } }
                if (best > Content.WhistleRange) continue;
                bool boss = Content.Enemy(e.kind).Boss;
                e.stun = Content.WhistleStun * (boss ? 0.5 : 1);
                if (boss) continue;
                double dx = e.x - CarX(near), dz = e.z, len = Math.Sqrt(dx * dx + dz * dz);
                if (len < 1e-6) { dx = 0; dz = 1; len = 1; }
                e.x += dx / len * Content.WhistlePush; e.z += dz / len * Content.WhistlePush;
            }
            r.whistleIn = WhistleEvery;
            State.whistles++;
            Touch();
            return true;
        }

        // ---- the end of a run ----

        void EndRun()
        {
            var r = State.run;
            r.phase = (int)Phase.Over;
            r.shells.Clear();
            RunOver?.Invoke();
            Touch();
        }

        public bool RunEnded { get { return State.run != null && State.run.phase == (int)Phase.Over; } }
        public const int ReviveGems = 30;
        public bool CanRevive { get { return RunEnded && !State.run.revived; } }

        /// <summary>Once a run: back on the rails with half the hull, the plating made good and the raiders driven off.</summary>
        public bool Revive(bool forGems)
        {
            if (!CanRevive) return false;
            if (forGems) { if (State.gems < ReviveGems) return false; State.gems -= ReviveGems; }
            var r = State.run;
            r.revived = true;
            r.phase = (int)Phase.Rolling;
            r.hull = r.hullMax * Content.ReviveHull;
            foreach (var c in r.cars) { c.plating = c.platingMax; c.down = false; }
            r.enemies.Clear(); r.mark = 0;
            r.mileTime = 0;
            FillQueue();
            Touch();
            return true;
        }

        /// <summary>The steel the ended run pays when it is closed.</summary>
        public double RunSteel { get { var r = State.run; return r == null ? 0 : Math.Floor(r.steel * (r.doubled ? 2 : 1)); } }

        /// <summary>Gives up a run that is still rolling: it ends where it stands.</summary>
        public bool Retire()
        {
            if (State.run == null || RunEnded) return false;
            State.run.revived = true;       // a run given up can't be revived
            EndRun();
            return true;
        }

        /// <summary>Banks the run's steel and clears it away. Sets off again at once if the player asked for that.</summary>
        public bool CloseRun()
        {
            if (!RunEnded) return false;
            double steel = RunSteel;
            State.steel += steel; State.lifetime += steel;
            State.runs++;
            State.run = null;
            if (State.depotAt <= 0) State.depotAt = RealNow();
            if (State.auto) StartRun();
            Touch();
            return true;
        }
    }
}
