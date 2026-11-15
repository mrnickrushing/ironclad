using System;
using Ironclad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironclad.Game
{
    /// <summary>Between runs: the line to cross, DEPART, what the depot's patrols brought, and the way into every screen.</summary>
    public sealed partial class Hud
    {
        Text _homeSteel, _homeGems, _depotText; RectTransform _depotChip; RectTransform _awardDot, _todayDot, _trainDot;

        void BuildDepot()
        {
            var s = S;
            _homeSteel = Pill(_face, new Vector2(0, 1), new Vector2(24, -20), 320, Ui.Art("steel"), Color.white, () => Open(Page.Workshop));
            _homeGems = Pill(_face, new Vector2(1, 1), new Vector2(-24, -20), 240, Ui.Art("gem"), Color.white, () => Open(Page.Shop));
            if (TrainView.PreviewSkin != null) { BuildPreviewBar(); UpdateDepot(); return; }

            // The line: its name on a ribbon, arrows to the others that are open, how far it has been crossed.
            var ribbon = Ui.Ribbon("Line", _face, Ui.Coral); ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(720, 110));
            var def = Content.Routes[s.State.route];
            var t = Ui.Label("T", ribbon.transform, def.Name.ToUpperInvariant(), 44, Color.white); t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(60, 24); t.rectTransform.offsetMax = new Vector2(-60, 0); t.Stroke(2.5f);
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 26; t.resizeTextMaxSize = 44;
            int best = s.State.best[s.State.route];
            string next = s.State.route + 1 < Content.Routes.Count && !s.RouteOpen(s.State.route + 1) ? "  ·  " + Content.Routes[s.State.route + 1].Name + " opens at mile " + Content.Routes[s.State.route + 1].Opens : "";
            // What to aim for: a wagon to build, else the next plans, else the next line.
            string worn = s.State.title.Length > 0 ? "  ·  <color=#FFD27A>" + s.State.title + "</color>" : "";
            var sub = Ui.Label("S", _face, (best > 0 ? "Furthest: mile " + best + (s.State.runs > 0 ? "" : next) : def.Blurb) + worn, 28, Color.white, TextAnchor.MiddleCenter, false); sub.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(1200, 40)); sub.Stroke(2f);
            // The next goal, and how far there is to go.
            var g = s.NextGoal;
            if (g != null && s.State.runs > 0)
            {
                var gl = Ui.Label("G", _face, "NEXT GOAL: " + g.Name + (g.Unit == "mile" ? " at mile " + g.Need + " " + g.Where : "  (" + g.Where + ")"), 26, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleCenter, false);
                gl.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(900, 36)); gl.resizeTextForBestFit = true; gl.resizeTextMinSize = 18; gl.resizeTextMaxSize = 26; gl.Stroke(2f);
                Image fill; var bar = Bar(_face, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -198), new Vector2(560, 36), Ui.Gold, out fill);
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp((float)(g.Have / Math.Max(1, g.Need)), 0.03f, 1), 1);
                var bt = Ui.Label("T", bar.parent, g.Unit == "mile" ? "mile " + g.Have + " of " + g.Need : Format.Money(g.Have) + " of " + Format.Money(g.Need) + " steel", 24, Color.white); bt.rectTransform.Fill(); bt.Stroke(2f);
            }
            if (s.State.route > 0)
            {
                var l = Ui.Button("Prev", _face, "◀", Ui.Blue, () => { S.ChooseRoute(S.State.route - 1); }, 36); ((RectTransform)l.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-420, -18), new Vector2(86, 86));
            }
            if (s.RouteOpen(s.State.route + 1))
            {
                var rr = Ui.Button("Next", _face, "▶", Ui.Blue, () => { S.ChooseRoute(S.State.route + 1); }, 36); ((RectTransform)rr.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(420, -18), new Vector2(86, 86));
            }

            // Today's gift and tasks.
            var today = Ui.Button("Today", _face, null, Ui.Gold, () => Open(Page.Today), 30); var td = (RectTransform)today.transform;
            td.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -110), new Vector2(240, 84));
            var tic = Ui.Img("I", td, Color.white); tic.sprite = Ui.Ic("gift"); tic.preserveAspect = true; tic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 6), new Vector2(48, 48));
            var tl = Ui.Label("L", td, "TODAY", 30, Color.white); tl.rectTransform.Fill(); tl.rectTransform.offsetMin = new Vector2(66, 14); tl.rectTransform.offsetMax = new Vector2(-10, 0); tl.Stroke(2f);
            var tdot = Ui.Img("Dot", td, Ui.Coral); tdot.sprite = Ui.Circle; _todayDot = tdot.rectTransform; _todayDot.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-8, -6), new Vector2(34, 34));

            // What the patrols brought.
            var chip = Ui.Button("Depot", _face, "", Ui.Gold, ShowDepotCard, 28); _depotChip = (RectTransform)chip.transform;
            _depotChip.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -110), new Vector2(420, 84));
            _depotText = chip.GetComponentInChildren<Text>();

            // This week's trial, once there is something to measure it against.
            if (s.TrialOpen)
            {
                var tb = Ui.Button("Trial", _face, null, Ui.Grape, ShowTrialCard, 26); var trt = (RectTransform)tb.transform;
                trt.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 40), new Vector2(330, 96));
                var ti = Ui.Img("I", trt, Color.white); ti.sprite = Ui.Ic("calendar"); ti.preserveAspect = true; ti.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(18, 4), new Vector2(50, 50));
                var tlab = Ui.Label("L", trt, "WEEKLY TRIAL\n<size=20>" + (s.TrialBest > 0 ? "best: mile " + s.TrialBest : s.TrialNow.Name) + "</size>", 26, Color.white); tlab.rectTransform.Fill(); tlab.rectTransform.offsetMin = new Vector2(74, 10); tlab.rectTransform.offsetMax = new Vector2(-10, -4); tlab.Stroke(2f); tlab.lineSpacing = 0.85f;
            }

            // DEPART, and whether to set off again by itself.
            var go = Ui.Button("Depart", _face, "DEPART", Ui.Green, () => { if (S.StartRun()) { Sfx.Play("dispatch", 0.9f); Haptics.Medium(); GameRoot.Instance.Save(); } }, 64);
            ((RectTransform)go.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(520, 150));
            _departButton = (RectTransform)go.transform;
            var auto = Ui.Button("Auto", _face, s.State.auto ? "AUTO: ON" : "AUTO: OFF", s.State.auto ? Ui.Blue : Ui.Off, () => { S.State.auto = !S.State.auto; _dirty = true; Toast(S.State.auto ? "The train sets off again by itself after each run" : "The train waits for you after each run"); }, 26);
            ((RectTransform)auto.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(390, 164), new Vector2(210, 76));

            // Far enough down this line, a run can set off from part-way along it.
            if (s.ExpressMile > 1)
            {
                var ex = Ui.Button("Express", _face, s.State.express ? "FROM MILE " + s.ExpressMile : "FROM MILE 1", s.State.express ? Ui.Blue : Ui.Off, () =>
                {
                    S.State.express = !S.State.express; _dirty = true;
                    Toast(S.State.express ? "Express: set off at mile " + S.ExpressMile + " with scrip and a card from each station passed" : "Set off from the first mile");
                }, 26);
                ((RectTransform)ex.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-400, 164), new Vector2(230, 76));
            }

            // The screens.
            string[] names = { "WORKSHOP", "TRAIN", "AWARDS", "SHOP", "MORE" };
            Page[] pages = { Page.Workshop, Page.Train, Page.Awards, Page.Shop, Page.Settings };
            string[] icons = { "hammer", "box", "trophy", "shop", "gear" };
            Color[] tints = { Ui.Coral, Ui.Blue, Ui.Gold, Ui.Green, Ui.Grape };
            for (int i = 0; i < names.Length; i++)
            {
                var pg = pages[i];
                var b = Ui.Button(names[i], _face, null, tints[i], () => Open(pg), 30);
                var rt = (RectTransform)b.transform; rt.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((i - 2) * 300, 16), new Vector2(284, 112));
                if (pg == Page.Workshop) _workshopButton = rt;
                var ic = Ui.Img("I", rt, Color.white); ic.sprite = Ui.Ic(icons[i]); ic.preserveAspect = true; ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 6), new Vector2(56, 56));
                var l = Ui.Label("L", rt, names[i], 30, Color.white); l.rectTransform.Fill(); l.rectTransform.offsetMin = new Vector2(80, 14); l.rectTransform.offsetMax = new Vector2(-12, 0); l.Stroke(2f);
                if (pg == Page.Train)
                {
                    var tdt = Ui.Img("Dot", rt, Ui.Coral); tdt.sprite = Ui.Circle; _trainDot = tdt.rectTransform; _trainDot.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -8), new Vector2(34, 34));
                }
                if (pg == Page.Awards)
                {
                    var dot = Ui.Img("Dot", rt, Ui.Coral); dot.sprite = Ui.Circle; _awardDot = dot.rectTransform; _awardDot.Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -8), new Vector2(34, 34));
                }
            }
            if (s.State.runs == 0)
            {
                var hint = Ui.ArtImg("Hint", _face, "plate", Ui.Ink, 36); hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 320), new Vector2(1000, 70));
                var ht = Ui.Label("T", hint.transform, "Hold the line: get the train as far down the track as you can", 30, Color.white, TextAnchor.MiddleCenter, false); ht.rectTransform.Fill(8); ht.rectTransform.offsetMin = new Vector2(8, 12);
            }
            else if (s.State.runs <= 3 && s.State.upgrades.Count == 0)
            {
                var hint = Ui.ArtImg("Hint", _face, "plate", Ui.Ink, 36); hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 320), new Vector2(1000, 70));
                var ht = Ui.Label("T", hint.transform, "Spend your steel in the WORKSHOP: what it buys is kept for good", 30, Color.white, TextAnchor.MiddleCenter, false); ht.rectTransform.Fill(8); ht.rectTransform.offsetMin = new Vector2(8, 12);
            }
            UpdateDepot();
        }

        void UpdateDepot()
        {
            var s = S;
            if (_homeSteel == null) return;
            _homeSteel.text = Format.Money(s.State.steel);
            _homeGems.text = s.State.gems.ToString();
            double waiting = s.DepotSteel;
            bool show = waiting >= 1 && s.DepotSeconds >= 300;      // not worth a button until the patrols have been out a while
            if (_depotChip.gameObject.activeSelf != show) _depotChip.gameObject.SetActive(show);
            if (show) _depotText.text = "DEPOT  +" + Format.Money(waiting) + (s.DepotSeconds >= s.DepotCap ? "  (full)" : "");
            if (_awardDot != null) _awardDot.gameObject.SetActive(s.AchievementsReady > 0 || s.MasteriesReady > 0);
            if (_todayDot != null) _todayDot.gameObject.SetActive(s.TodayReady > 0 || s.StepsReady > 0);
            if (!ModalOpen && !ScreenOpen && s.Run == null && s.State.tutorialStep >= 1 && s.NewlyUnlocked.Count > 0) ShowUnlocked();
            if (s.StarterOfferDue && !ModalOpen && !ScreenOpen && Store.Available) OfferStarter();
            if (_trainDot != null) _trainDot.gameObject.SetActive(s.Unbuilt.Count > 0);
        }

        /// <summary>The Fireman's Pack, offered once (see <see cref="Sim.StarterOfferDue"/>): what it gives, its price, and
        /// a plain way to say no.</summary>
        void OfferStarter()
        {
            S.StarterOffered(); GameRoot.Instance.Save();
            Telemetry.Log("Offer.viewed", ("Offer.product", "ironclad.starter"), ("Offer.from", S.StarterVariant));
            var prod = Sim.Product("ironclad.starter");
            var card = Card(900, 560, "FIREMAN'S PACK");
            var gem = Ui.ArtImg("Gem", card, "gem", Color.white, 0); gem.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-150, -100), new Vector2(120, 120));
            var steel = Ui.ArtImg("Steel", card, "steel", Color.white, 0); steel.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(150, -100), new Vector2(120, 120));
            Ui.Label("B", card, "<b>250 gems</b> and <b>" + Format.Money(Math.Max(prod.Steel, Math.Floor(S.DepotRate * 4))) + " steel</b> to get the workshop going.\nOne time only. Everything in the game can still be earned by playing.", 30, Ui.Ink, TextAnchor.UpperCenter, false)
                .rectTransform.Band(true, -240, 150, 60);
            var buy = Ui.Button("Buy", card, Store.Price(prod), Ui.Green, () => Store.Buy(prod, (ok, msg) => { Toast(msg); if (ok) { Sfx.Play("confirm"); CloseCard(); _dirty = true; } }), 42);
            ((RectTransform)buy.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(205, 44), new Vector2(380, 116));
            var no = Ui.Button("No", card, "NOT NOW", Ui.Off, CloseCard, 38);
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-205, 44), new Vector2(380, 116));
        }

        /// <summary>Trying a theme on: its name, take it (or use it, if owned) or go back.</summary>
        void BuildPreviewBar()
        {
            var s = S; var k = Content.Skin(TrainView.PreviewSkin);
            if (k == null) { TrainView.PreviewSkin = null; _dirty = true; return; }
            var bar = Ui.ArtImg("Preview", _face, "panel", Color.white, 40); bar.raycastTarget = true;
            bar.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(1180, 230));
            var b = bar.rectTransform;
            Ui.Label("T", b, "TRYING ON: " + k.Name.ToUpperInvariant(), 40, Ui.Ink).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -18), new Vector2(1100, 54));
            var bl = Ui.Label("B", b, k.Blurb, 26, Ui.Dim, TextAnchor.MiddleCenter, false); bl.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(1100, 40)); bl.resizeTextForBestFit = true; bl.resizeTextMinSize = 18; bl.resizeTextMaxSize = 26;
            bool own = s.SkinOwned(k.Id);
            Action back = () => { TrainView.PreviewSkin = null; Sfx.Play("close", 0.5f); _dirty = true; };
            string label = own ? "USE IT" : k.Gems > 0 ? k.Gems + " GEMS" : k.Engines > 0 ? "ENGINE NO. " + (k.Engines + 1) : (k.From ?? "NOT FOR SALE");
            Action take = own ? (Action)(() => { S.UseSkin(k.Id); TrainView.PreviewSkin = null; Sfx.Play("build", 0.6f); GameRoot.Instance.Save(); _dirty = true; })
                : k.Gems > 0 && s.State.gems >= k.Gems ? (Action)(() => BuyTheme(k)) : null;
            var tk = Ui.Button("Take", b, label, own ? Ui.Blue : Ui.Grape, take, 34); ((RectTransform)tk.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(210, 22), new Vector2(380, 100));
            var bk = Ui.Button("Back", b, "BACK", Ui.Off, back, 34); ((RectTransform)bk.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-210, 22), new Vector2(380, 100));
        }

        /// <summary>This week's trial: the train, the rules, the best so far and what the marks give.</summary>
        void ShowTrialCard()
        {
            var s = S; var t = s.TrialNow;
            var card = Card(1160, 780, "WEEKLY TRIAL");
            Ui.Label("N", card, t.Name, 46, Ui.Ink).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(1000, 60));
            Ui.Label("B", card, t.Blurb, 28, Ui.Dim, TextAnchor.MiddleCenter, false).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -156), new Vector2(1000, 40));
            // The train, engine on the right, every wagon at level t.Level, and the opening card.
            int n = t.Cars.Length + 1; float w = 150;
            for (int i = 0; i < n; i++)
            {
                string type = i == n - 1 ? "engine" : t.Cars[n - 2 - i];
                var pic = CarPicture(card, type); pic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - (n - 1) * 0.5f) * w, -206), new Vector2(130, 130));
            }
            var c = Content.Card(t.Cards[0]);
            string rules = "Every wagon at level " + t.Level + ", starting with " + c.Name + ". The same train and the same luck for everyone this week. Nothing from your workshop, and no steel: just how far you get.";
            var rl = Ui.Label("R", card, rules, 26, Ui.Ink, TextAnchor.MiddleCenter, false); rl.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -350), new Vector2(1040, 80)); rl.resizeTextForBestFit = true; rl.resizeTextMinSize = 18; rl.resizeTextMaxSize = 26;
            string marks = "Mile 10: the title Trial Runner  ·  Mile 25: Proving Ground Regular  ·  Mile 40: the Proving Ground theme";
            var ml = Ui.Label("M", card, marks, 24, new Color(0.45f, 0.3f, 0.65f), TextAnchor.MiddleCenter, false); ml.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -436), new Vector2(1040, 60)); ml.resizeTextForBestFit = true; ml.resizeTextMinSize = 16; ml.resizeTextMaxSize = 24;
            int days = (int)(7 - (s.Today + 3) % 7);
            Ui.Label("W", card, (s.TrialBest > 0 ? "Your best this week: mile " + s.TrialBest : "No run yet this week") + "  ·  a new trial in " + days + (days == 1 ? " day" : " days"), 28, Ui.Dim, TextAnchor.MiddleCenter, false)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -510), new Vector2(1040, 44));
            var go = Ui.Button("Go", card, "START TRIAL", Ui.Grape, () =>
            {
                if (!S.StartTrial()) return;
                CloseCard(); Sfx.Play("dispatch", 0.9f); Haptics.Medium();
                Telemetry.Log("Trial.started", ("Trial.name", S.TrialNow.Name), ("Trial.week", S.Week.ToString()));
                GameRoot.Instance.Save();
            }, 40);
            ((RectTransform)go.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(210, 40), new Vector2(380, 110));
            var no = Ui.Button("No", card, "NOT NOW", Ui.Off, CloseCard, 38); ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-210, 40), new Vector2(380, 110));
        }

        /// <summary>A celebration of what has just opened up, and the next thing to aim for.</summary>
        void ShowUnlocked()
        {
            var got = S.NewlyUnlocked;
            S.Celebrated(); GameRoot.Instance.Save();
            var card = Card(980, 640, "UNLOCKED!");
            var rays = Ui.ArtImg("Rays", card, "star", new Color(1f, 0.85f, 0.4f, 0.5f), 0); rays.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(200, 200)); rays.gameObject.AddComponent<Spinner>();
            Ui.Label("B", card, string.Join("\n", got), 40, Ui.Ink, TextAnchor.MiddleCenter, false).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -320), new Vector2(880, 120));
            var g = S.NextGoal;
            if (g != null) Ui.Label("N", card, "Next: " + g.Name + (g.Unit == "mile" ? " at mile " + g.Need : ""), 30, Ui.Dim, TextAnchor.MiddleCenter, false).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -410), new Vector2(880, 44));
            Sfx.Play("star", 0.9f); Haptics.Success();
            var ok = Ui.Button("Ok", card, "ON WE GO", Ui.Green, CloseCard, 42); ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(420, 116));
        }

        /// <summary>The patrols' steel: collect it, or twice over for an ad.</summary>
        void ShowDepotCard()
        {
            var s = S;
            double steel = s.DepotSteel;
            if (steel < 1) return;
            var card = Card(900, 560, "THE DEPOT");
            Ui.Label("B", card, "While you were away your patrols worked the line for " + Format.Time(s.DepotSeconds) + " and brought home", 32, Ui.Dim, TextAnchor.UpperCenter, false).rectTransform.Band(true, -96, 90, 60);
            Ui.Label("S", card, "+" + Format.Money(steel) + " steel", 76, Ui.Ink).rectTransform.Band(true, -190, 100, 40);
            Ui.Label("R", card, Format.Money(s.DepotRate) + " an hour, for up to " + Math.Round(s.DepotCap / 3600) + " hours", 26, Ui.Dim, TextAnchor.UpperCenter, false).rectTransform.Band(true, -296, 40, 40);
            Action done = () => { CloseCard(); Sfx.Play("coin", 0.9f); Haptics.Success(); GameRoot.Instance.Save(); _dirty = true; };
            bool ad = AdReady(AdReward.DoubleDepot);
            var c = Ui.Button("Collect", card, "COLLECT", ad ? Ui.Off : Ui.Green, () => { S.CollectDepot(); done(); }, 40);
            ((RectTransform)c.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(ad ? -210 : 0, 44), new Vector2(390, 124));
            if (ad)
            {
                var d = Ui.Button("Double", card, AdLabel("DOUBLE IT"), Ui.Gold, () => WatchAd(AdReward.DoubleDepot, done), 38);
                ((RectTransform)d.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(210, 44), new Vector2(390, 124));
            }
        }
    }
}
