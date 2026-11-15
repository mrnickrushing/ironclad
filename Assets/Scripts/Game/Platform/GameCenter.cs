using System.Collections.Generic;
using Ironclad.Core;
using UnityEngine;
using UnityEngine.SocialPlatforms;
#if UNITY_IOS
using UnityEngine.SocialPlatforms.GameCenter;
#endif

namespace Ironclad.Game
{
    /// <summary>
    /// Apple's Game Center: three leaderboards, a weekly one, and the game's achievements, mirrored. The player signs in with their
    /// own Game Center account (iOS asks once); nothing goes through our servers. Ids must match the ones created in
    /// App Store Connect by Tools/appstore/gamecenter.py: leaderboards <c>ironclad.board.*</c>, achievements
    /// <c>ironclad.ach.&lt;achievement id&gt;</c>. Does nothing in the Editor.
    /// </summary>
    public static class GameCenter
    {
        public const string Miles = "ironclad.board.miles", Distance = "ironclad.board.distance", Raiders = "ironclad.board.raiders";
        /// <summary>A recurring board that starts again every Monday: the furthest mile of one run that week.</summary>
        public const string Weekly = "ironclad.board.week";
        public const string AchievementPrefix = "ironclad.ach.";
        const string SentKey = "gc.sent";

        static bool Device { get { return Application.platform == RuntimePlatform.IPhonePlayer; } }
        public static bool SignedIn { get { return Device && Social.localUser.authenticated; } }
        /// <summary>Why the boards can't be opened, or null.</summary>
        public static string Blocked { get { return !Device ? "On iPhone and iPad" : SignedIn ? null : "Sign in to Game Center in Settings"; } }

        static readonly Dictionary<string, long> _scores = new Dictionary<string, long>();
        static HashSet<string> _sent;
        static float _next;

        /// <summary>Call once at launch: iOS signs the player in quietly, or shows its own sheet the first time.</summary>
        public static void Start()
        {
            if (!Device) return;
#if UNITY_IOS
            GameCenterPlatform.ShowDefaultAchievementCompletionBanner(true);
#endif
            Social.localUser.Authenticate(ok => { _next = 0; });
        }

        /// <summary>Sends what changed since last time (call often; it looks at most every 20 seconds).</summary>
        public static void Sync(Sim s)
        {
            if (s == null || !SignedIn || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 20;
            Score(Miles, s.BestMile);
            Score(Distance, s.State.miles);
            Score(Raiders, s.State.kills);
            if (_sent == null) _sent = new HashSet<string>(PlayerPrefs.GetString(SentKey, "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
            foreach (var a in Sim.Achievements)
            {
                if (_sent.Contains(a.Id) || !s.AchievementDone(a)) continue;
                var id = a.Id;
                _sent.Add(id);          // not asked again this session; kept for good once Game Center has it
                Social.ReportProgress(AchievementPrefix + id, 100.0, ok =>
                {
                    if (!ok) { _sent.Remove(id); return; }
                    PlayerPrefs.SetString(SentKey, string.Join(",", _sent));
                });
            }
        }

        static void Score(string board, long value)
        {
            long had;
            if (value <= 0 || (_scores.TryGetValue(board, out had) && had >= value)) return;
            _scores[board] = value;
            Social.ReportScore(value, board, ok => { if (!ok) _scores.Remove(board); });
        }

        /// <summary>A run has ended at this mile: the week's board keeps the best one (Game Center starts it again each week).</summary>
        public static void ReportRun(int mile)
        {
            if (!SignedIn || mile <= 1) return;
            Social.ReportScore(mile, Weekly, ok => { });
        }

        public static void ShowBoards() { if (SignedIn) Social.ShowLeaderboardUI(); }

        /// <summary>Opens this week's board by itself.</summary>
        public static void ShowWeekly()
        {
            if (!SignedIn) return;
#if UNITY_IOS
            GameCenterPlatform.ShowLeaderboardUI(Weekly, TimeScope.AllTime);
#else
            Social.ShowLeaderboardUI();
#endif
        }
        public static void ShowAchievements() { if (SignedIn) Social.ShowAchievementsUI(); }

        /// <summary>Erase everything: this device forgets what it sent (Game Center keeps the player's own record).</summary>
        public static void Forget() { PlayerPrefs.DeleteKey(SentKey); _sent = null; _scores.Clear(); }
    }
}
