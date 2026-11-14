#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Ironclad.Core;
using UnityEngine;

namespace Ironclad.Game
{
    /// <summary>
    /// Editor only: plays the game through the real interface code, fast, and keeps a log. Start it with
    /// <c>Ironclad.Game.PlayBot.Begin(8)</c> in Play mode; read <c>PlayBot.Report()</c>. It spends like a sensible
    /// player, opens every screen, takes cards, recouples, revives, doubles, claims gifts and awards.
    /// </summary>
    public sealed class PlayBot : MonoBehaviour
    {
        public static PlayBot Instance;
        public static readonly List<string> Log = new List<string>();
        public static int Runs, Errors;
        float _next; int _tick; System.Random _rnd = new System.Random(11);

        public static void Begin(float speed)
        {
            if (Instance == null) Instance = new GameObject("PlayBot").AddComponent<PlayBot>();
            GameRoot.Speed = speed; Runs = 0; Errors = 0; Log.Clear();
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
        }

        public static void End() { GameRoot.Speed = 1; if (Instance != null) Destroy(Instance.gameObject); Instance = null; Application.logMessageReceived -= OnLog; }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            Errors++;
            if (Log.Count < 400) Log.Add("ERROR " + msg + " | " + (stack ?? "").Split('\n')[0]);
        }

        public static string Report(int last = 30)
        {
            var s = GameRoot.Sim;
            string head = "runs " + Runs + " errors " + Errors + " best " + string.Join("/", s.State.best) + " steel " + Format.Money(s.State.steel) + " gems " + s.State.gems
                + " train " + string.Join(",", s.State.train) + " speed x" + s.Speed + " played " + Format.Time(s.State.played);
            int from = Math.Max(0, Log.Count - last);
            return head + "\n" + string.Join("\n", Log.GetRange(from, Log.Count - from));
        }

        void Update()
        {
            if (Hud.Instance == null || GameRoot.Sim == null) return;
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.12f; _tick++;
            try { Hud.Instance.BotTick(this, _tick, _rnd); }
            catch (Exception e) { Errors++; if (Log.Count < 400) Log.Add("BOT EXCEPTION " + e.Message + " | " + (e.StackTrace ?? "").Split('\n')[0]); }
        }
    }

    public sealed partial class Hud
    {
        static readonly Page[] BotPages = { Page.Workshop, Page.Train, Page.Today, Page.Awards, Page.Shop, Page.Settings };

        /// <summary>One step of the play-tester, using the same methods the buttons call.</summary>
        public void BotTick(PlayBot bot, int tick, System.Random rnd)
        {
            var s = S; var r = s.Run;
            if (_shown == Face.Depot)
            {
                if (ModalOpen) { if (s.State.tutorialStep == 0) s.State.tutorialStep = 1; CloseCard(); return; }
                if (ScreenOpen)
                {
                    // Use the screen that is open, then close it.
                    if (_screen == Page.Today) { if (s.GiftReady) s.ClaimGift(); for (int i = 0; i < Sim.Tasks.Count; i++) s.ClaimTask(i); }
                    if (_screen == Page.Awards) foreach (var a in Sim.Achievements) s.ClaimAchievement(a.Id);
                    if (_screen == Page.Workshop || _screen == Page.Train) BotSpend(s);
                    if (_screen == Page.Train && s.State.train.Count > 1 && rnd.Next(3) == 0) s.Shunt(rnd.Next(s.State.train.Count), rnd.Next(s.State.train.Count));
                    CloseScreen();
                    return;
                }
                if (s.DepotSteel >= 1 && s.DepotSeconds >= 300) { ShowDepotCard(); s.CollectDepot(s.DoubleFree); return; }
                if (tick % 2 == 0 && rnd.Next(2) == 0) { Open(BotPages[rnd.Next(BotPages.Length)]); return; }
                BotSpend(s);
                for (int i = Content.Routes.Count - 1; i > 0; i--) if (s.RouteOpen(i) && s.State.best[i - 1] >= Content.Routes[i].Opens + 10) { s.ChooseRoute(i); break; }
                s.SetSpeed(s.SpeedsOpen - 1);
                s.StartRun();
                return;
            }
            if (_shown == Face.Run && r != null)
            {
                for (int guard = 0; guard < 20; guard++)
                {
                    int pick = -1; double cost = double.MaxValue;
                    for (int i = 0; i < r.cars.Count; i++) if (s.FieldCost(i) < cost) { cost = s.FieldCost(i); pick = i; }
                    if (pick < 0 || !s.FieldUpgrade(pick)) break;
                }
                if (s.WhistleReady && r.enemies.Count >= 8) SoundWhistle();
                if (r.enemies.Count > 0 && rnd.Next(12) == 0) s.Mark(r.enemies[rnd.Next(r.enemies.Count)].id);
                if (rnd.Next(40) == 0) CycleSpeed();
                return;
            }
            if (_shown == Face.Station && r != null)
            {
                if (r.offer.Count > 0) { if (s.State.gems >= 60 && rnd.Next(4) == 0) s.RerollCards(); int best = 0; for (int i = 1; i < r.offer.Count; i++) if (r.offer[i].rarity > r.offer[best].rarity) best = i; s.TakeCard(best); return; }
                if (s.State.train.Count > 1 && rnd.Next(2) == 0) s.Shunt(rnd.Next(s.State.train.Count), rnd.Next(s.State.train.Count));
                s.Depart();
                return;
            }
            if (_shown == Face.Over && r != null)
            {
                if (AdReady(AdReward.Revive) && rnd.Next(2) == 0) { Advert(AdReward.Revive, () => { }); return; }
                if (AdReady(AdReward.DoubleRun)) { Advert(AdReward.DoubleRun, () => { }); return; }
                PlayBot.Runs++;
                if (PlayBot.Log.Count < 400) PlayBot.Log.Add("run " + PlayBot.Runs + " " + s.Route.Id + " mile " + r.mile + " kills " + r.kills + " steel " + Format.Money(s.RunSteel) + " cars " + r.cars.Count + " at " + Format.Time(s.State.played));
                s.CloseRun();
            }
        }

        static void BotSpend(Sim s)
        {
            for (int guard = 0; guard < 400; guard++)
            {
                string best = null; double cost = double.MaxValue; bool car = false;
                foreach (var u in Content.Upgrades) { if (s.UpgradeMaxed(u.Id)) continue; double c = s.UpgradeCost(u.Id); if (c < cost) { cost = c; best = u.Id; car = false; } }
                foreach (var d in Content.Cars)
                {
                    if (!s.Built(d.Id) && !s.PlansFound(d.Id)) continue;
                    if (s.Built(d.Id) && d.Id != "engine" && !s.State.train.Contains(d.Id)) continue;
                    double c = s.CarCost(d.Id); if (c < cost) { cost = c; best = d.Id; car = true; }
                }
                if (best == null || cost > s.State.steel) break;
                if (car) s.BuyCar(best); else s.BuyUpgrade(best);
            }
            foreach (var d in Content.Cars) if (d.Id != "engine" && s.Built(d.Id) && !s.State.train.Contains(d.Id)) s.Couple(d.Id);
        }
    }
}
#endif
