using System;
using System.Collections.Generic;

namespace Ironclad.Core
{
    /// <summary>
    /// The game's rules. A run is fought in small fixed steps (<see cref="Content.Step"/>) so it plays the same at any
    /// frame rate or running speed; what is kept between runs is in <c>Sim.Workshop</c>.
    /// </summary>
    public sealed partial class Sim
    {
        public readonly GameState State;
        public Sim(GameState state) { State = state; state.Repair(); if (state.run == null && !RouteOpen(state.route)) state.route = 0; }

        /// <summary>Something the interface shows has changed (levels, unlocks, owned things, the phase of the run).</summary>
        public event Action Changed;
        /// <summary>A car fired: car index, where the shot goes (x, z), and whether it was a crit.</summary>
        public event Action<int, double, double, bool> Fired;
        /// <summary>A raider was hurt: the raider, the damage, crit.</summary>
        public event Action<EnemyState, double, bool> Hit;
        /// <summary>A raider is down: the raider and the scrip it paid.</summary>
        public event Action<EnemyState, double> Fell;
        /// <summary>Something burst here (x, z) this wide: a shell landing or a powder monkey.</summary>
        public event Action<double, double, double> Burst;
        /// <summary>A car was struck: car index and damage.</summary>
        public event Action<int, double> Struck;
        /// <summary>A raider rode in.</summary>
        public event Action<EnemyState> Arrived;
        /// <summary>A mile was finished: the mile and the steel it paid.</summary>
        public event Action<int, double> MileDone;
        /// <summary>The plans for a wagon have turned up (a best mile was reached).</summary>
        public event Action<CarDef> Plans;
        /// <summary>A station on this line was reached for the first time: the mile and the gems it paid.</summary>
        public event Action<int, int> FirstStation;
        /// <summary>The run is over.</summary>
        public event Action RunOver;

        public Func<double> RealNow = () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
        /// <summary>Seconds to add to UTC for the player's clock (days turn at local midnight).</summary>
        public double UtcOffset;
        public long Today { get { return (long)Math.Floor((RealNow() + UtcOffset) / 86400); } }

        public RunState Run { get { return State.run; } }
        public bool Running { get { return State.run != null && State.run.phase == (int)Phase.Rolling; } }

        uint Next()
        {
            uint x = State.seed;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            State.seed = x == 0 ? 2463534242 : x;
            return State.seed;
        }
        /// <summary>0 &lt;= value &lt; 1.</summary>
        public double Roll() { return (Next() >> 8) / 16777216.0; }

        void Touch() { Changed?.Invoke(); }

        double _carry;

        /// <summary>Advances the game by real seconds. The run rolls at the chosen running speed.</summary>
        public void Tick(double dt)
        {
            if (dt <= 0) return;
            if (State.firstDay < 0) State.firstDay = Today;
            RollTasks();
            State.played += dt;
            if (!Running) { _carry = 0; return; }
            _carry += Math.Min(dt, 1) * Speed;
            while (_carry >= Content.Step && Running)
            {
                _carry -= Content.Step;
                StepRun(Content.Step);
            }
        }

        /// <summary>The running speed in use.</summary>
        public double Speed { get { return Content.Speeds[Math.Min(State.speed, SpeedsOpen - 1)]; } }
    }
}
