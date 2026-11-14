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

            // The line: its name on a ribbon, arrows to the others that are open, how far it has been crossed.
            var ribbon = Ui.Ribbon("Line", _face, Ui.Coral); ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(720, 110));
            var def = Content.Routes[s.State.route];
            var t = Ui.Label("T", ribbon.transform, def.Name.ToUpperInvariant(), 44, Color.white); t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(60, 24); t.rectTransform.offsetMax = new Vector2(-60, 0); t.Stroke(2.5f);
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 26; t.resizeTextMaxSize = 44;
            int best = s.State.best[s.State.route];
            string next = s.State.route + 1 < Content.Routes.Count && !s.RouteOpen(s.State.route + 1) ? "  ·  " + Content.Routes[s.State.route + 1].Name + " opens at mile " + Content.Routes[s.State.route + 1].Opens : "";
            // What to aim for: a wagon to build, else the next plans, else the next line.
            var canBuild = s.Unbuilt; var plans = s.NextPlans;
            string goal = canBuild.Count > 0 ? "  ·  <color=#B8FF9A>The " + canBuild[0].Name + " can be built: TRAIN</color>" : plans != null ? "  ·  " + plans.Name + " plans at mile " + plans.Mile : next;
            var sub = Ui.Label("S", _face, (best > 0 ? "Furthest: mile " + best : def.Blurb) + goal, 28, Color.white, TextAnchor.MiddleCenter, false); sub.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(1200, 40)); sub.Stroke(2f);
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

            // DEPART, and whether to set off again by itself.
            var go = Ui.Button("Depart", _face, "DEPART", Ui.Green, () => { if (S.StartRun()) { Sfx.Play("dispatch", 0.9f); Haptics.Medium(); GameRoot.Instance.Save(); } }, 64);
            ((RectTransform)go.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(520, 150));
            _departButton = (RectTransform)go.transform;
            var auto = Ui.Button("Auto", _face, s.State.auto ? "AUTO: ON" : "AUTO: OFF", s.State.auto ? Ui.Blue : Ui.Off, () => { S.State.auto = !S.State.auto; _dirty = true; Toast(S.State.auto ? "The train sets off again by itself after each run" : "The train waits for you after each run"); }, 26);
            ((RectTransform)auto.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(390, 164), new Vector2(210, 76));

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
            if (_awardDot != null) _awardDot.gameObject.SetActive(s.AchievementsReady > 0);
            if (_todayDot != null) _todayDot.gameObject.SetActive(s.TodayReady > 0);
            if (_trainDot != null) _trainDot.gameObject.SetActive(s.Unbuilt.Count > 0);
        }

        /// <summary>The patrols' steel: collect it, or twice over (free once a day, then for an advert).</summary>
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
            bool free = s.DoubleFree, ad = !free && AdReady(AdReward.DoubleDepot);
            var c = Ui.Button("Collect", card, "COLLECT", free || ad ? Ui.Off : Ui.Green, () => { S.CollectDepot(); done(); }, 40);
            ((RectTransform)c.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(free || ad ? -210 : 0, 44), new Vector2(390, 124));
            if (free)
            {
                var d = Ui.Button("Double", card, "DOUBLE IT\n<size=24>free today</size>", Ui.Green, () => { S.CollectDepot(true); done(); }, 38);
                ((RectTransform)d.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(210, 44), new Vector2(390, 124));
            }
            else if (ad)
            {
                var d = Ui.Button("Double", card, AdLabel("DOUBLE IT"), Ui.Gold, () => Advert(AdReward.DoubleDepot, done), 38);
                ((RectTransform)d.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(210, 44), new Vector2(390, 124));
            }
        }
    }
}
