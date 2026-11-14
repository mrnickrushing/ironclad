using System;

namespace Ironclad.Core
{
    /// <summary>What a watched advert can pay for. Always the player's choice, never shown unasked.</summary>
    public enum AdReward { Revive, DoubleRun, DoubleDepot }

    /// <summary>
    /// Rewarded adverts: a free way to three things gems or patience buy (back on the rails once a run, the ended run's
    /// steel twice over, the depot's steel twice over after the day's free double), a few times a day. The rules are
    /// here; showing the advert is <c>Game/Platform/Ads</c>. Owners of the Conductor's Pass get the reward without one.
    /// </summary>
    public sealed partial class Sim
    {
        public const int AdsPerDay = 8;

        public int AdsLeft { get { return State.adDay == Today ? Math.Max(0, AdsPerDay - State.adCount) : AdsPerDay; } }
        public bool SkipsAdverts { get { return Owns("ironclad.pass"); } }

        /// <summary>Whether this reward can be offered right now (something to gain, and adverts left today).</summary>
        public bool AdOffered(AdReward reward)
        {
            if (AdsLeft <= 0) return false;
            switch (reward)
            {
                case AdReward.Revive: return CanRevive;
                case AdReward.DoubleRun: return RunEnded && !State.run.doubled && State.run.steel >= 1;
                case AdReward.DoubleDepot: return !DoubleFree && DepotSteel >= 1;
            }
            return false;
        }

        void CountAd()
        {
            if (State.adDay != Today) { State.adDay = Today; State.adCount = 0; }
            State.adCount++;
            Touch();
        }

        /// <summary>Pays out a reward after its advert was watched. False if it wasn't on offer.</summary>
        public bool GrantAd(AdReward reward)
        {
            if (!AdOffered(reward)) return false;
            switch (reward)
            {
                case AdReward.Revive: Revive(false); break;
                case AdReward.DoubleRun: State.run.doubled = true; break;
                case AdReward.DoubleDepot: PayDepot(2, false); break;
            }
            CountAd();
            return true;
        }
    }
}
