using System;
using System.Collections.Generic;
using Ironclad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironclad.Game
{
    /// <summary>The screens that open over the depot: workshop, train, awards, shop, settings.</summary>
    public sealed partial class Hud
    {
        enum Page { None, Workshop, Train, Awards, Shop, Settings, Today }
        Page _screen = Page.None;
        RectTransform _list; float _listY;

        void Open(Page s)
        {
            _screen = s; _listY = 0; _list = null; _shuntFrom = -1;
            Sfx.Play("open", 0.5f);
            RefreshScreen();
            if (_screenRoot.childCount > 1) _screenRoot.GetChild(_screenRoot.childCount - 1).gameObject.AddComponent<PopIn>();
        }

        void CloseScreen() { _screen = Page.None; _list = null; Ui.Clear(_screenRoot); Sfx.Play("close", 0.4f); }

        void RefreshScreen()
        {
            switch (_screen)
            {
                case Page.Workshop: ShowWorkshop(); break;
                case Page.Train: ShowTrain(); break;
                case Page.Awards: ShowAwards(); break;
                case Page.Shop: ShowShop(); break;
                case Page.Settings: ShowSettings(); break;
                case Page.Today: ShowToday(); break;
            }
        }

        static readonly Dictionary<string, string> About = new Dictionary<string, string>
        {
            { "WORKSHOP", "Steel buys these for good. Seals, further down, buy rarer things." },
            { "THE TRAIN", "Build wagons, raise their levels, and choose the order they are coupled in." },
            { "TODAY", "A gift every day and three tasks. A missed day loses nothing." },
            { "AWARDS", "Gems for what you've done on the line. Tap a green one to take it." },
            { "SHOP", "Gems speed things up. Everything here is optional: the whole game can be played free." },
            { "SETTINGS", "Sound, reminders, your account and help." },
        };

        /// <summary>The frame every screen shares: a dimmed game, a paper panel, the title on a ribbon, a close button and a
        /// scrolling list. Returns the panel; <see cref="_list"/> is where rows go.</summary>
        RectTransform Frame(string title, float listTop = 70)
        {
            if (_list != null) _listY = _list.anchoredPosition.y;
            Ui.Clear(_screenRoot);
            var dim = Ui.Img("Dim", _screenRoot, new Color(0.14f, 0.07f, 0.04f, 0.62f)); dim.raycastTarget = true; dim.rectTransform.Fill();
            dim.gameObject.AddComponent<Button>().onClick.AddListener(CloseScreen);
            var panel = Ui.ArtImg("Panel", _screenRoot, "panel", Color.white, 60); panel.raycastTarget = true;
            var p = panel.rectTransform; p.anchorMin = new Vector2(0.5f, 0); p.anchorMax = new Vector2(0.5f, 1); p.pivot = new Vector2(0.5f, 0.5f); p.offsetMin = new Vector2(-760, 24); p.offsetMax = new Vector2(760, -70);
            var ribbon = Ui.Ribbon("Ribbon", p, Ui.Coral); ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(620, 116));
            var t = Ui.Label("Title", ribbon.transform, title, 52, Color.white); t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(40, 24); t.rectTransform.offsetMax = new Vector2(-40, 0); t.Stroke(2.5f);
            var x = Ui.Button("X", p, "✕", Ui.Coral, CloseScreen, 34); ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-26, -30), new Vector2(84, 84));
            var steel = Pill(p, new Vector2(0, 1), new Vector2(26, -14), 280, Ui.Art("steel"), Color.white); steel.text = Format.Money(S.State.steel);

            string about; string key = title.StartsWith("SHOP") ? "SHOP" : title;
            if (About.TryGetValue(key, out about))
            {
                var ab = Ui.Label("About", p, about, 28, Ui.Dim, TextAnchor.MiddleCenter, false);
                ab.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(120, -listTop + 8), new Vector2(1100, 40));
                listTop += 44;
            }
            var view = Ui.Rect("View", p); view.Fill(); view.offsetMin = new Vector2(34, 44); view.offsetMax = new Vector2(-34, -listTop);
            view.gameObject.AddComponent<RectMask2D>();
            var catcher = view.gameObject.AddComponent<Image>(); catcher.color = new Color(0, 0, 0, 0); catcher.raycastTarget = true;
            _list = Ui.Rect("List", view); _list.anchorMin = new Vector2(0, 1); _list.anchorMax = new Vector2(1, 1); _list.pivot = new Vector2(0.5f, 1); _list.offsetMin = _list.offsetMax = Vector2.zero;
            var sr = view.gameObject.AddComponent<ScrollRect>(); sr.content = _list; sr.horizontal = false; sr.viewport = view; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 40;
            return p;
        }

        void EndList(float height) { _list.sizeDelta = new Vector2(0, height); _list.anchoredPosition = new Vector2(0, Mathf.Max(0, _listY)); }

        /// <summary>A row: a picture, a name and what it is, and a button (or a word where the button would be).</summary>
        float Entry(float y, Sprite icon, Color iconTint, string name, string blurb, string button, Color colour, Action tap, string status = null, bool lit = false)
        {
            const float H = 122;
            var row = Ui.ArtImg("Row", _list, "card", lit ? new Color(0.76f, 0.93f, 0.72f) : Ui.Card, 26); row.rectTransform.Band(true, y, H - 10);
            if (icon != null) { var ic = Ui.Img("I", row.transform, iconTint); ic.sprite = icon; ic.preserveAspect = true; ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(84, 84)); }
            var n = Ui.Label("N", row.transform, name, 34, Ui.Ink, TextAnchor.UpperLeft); n.rectTransform.Fill(); n.rectTransform.offsetMin = new Vector2(124, 0); float right = button != null ? -330 : status != null ? -200 : -24; n.rectTransform.offsetMax = new Vector2(right, -12);
            var b = Ui.Label("B", row.transform, blurb, 25, Ui.Dim, TextAnchor.LowerLeft, false); b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(124, 12); b.rectTransform.offsetMax = new Vector2(right, -50);
            b.resizeTextForBestFit = true; b.resizeTextMinSize = 17; b.resizeTextMaxSize = 25; b.verticalOverflow = VerticalWrapMode.Truncate;
            if (button != null)
            {
                var btn = Ui.Button("B", row.transform, button, tap == null ? Ui.Off : colour, tap, 32);
                ((RectTransform)btn.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 2), new Vector2(300, 88));
                if (tap != null) btn.gameObject.AddComponent<HoldRepeat>().Tap = tap;
            }
            else if (status != null) { var st = Ui.Label("S", row.transform, status, 30, new Color(0.12f, 0.5f, 0.2f), TextAnchor.MiddleRight); st.rectTransform.Fill(); st.rectTransform.offsetMax = new Vector2(-30, 0); }
            return y - H;
        }

        float Heading(float y, string text)
        {
            var l = Ui.Label("H", _list, text, 30, Ui.Dim, TextAnchor.LowerLeft); l.rectTransform.Band(true, y, 50, 8);
            return y - 58;
        }

        // ---- the workshop ----

        static string UpgradeNow(Sim s, UpgradeDef u)
        {
            int lv = s.UpgradeLevel(u.Id);
            switch (u.Id)
            {
                case "slots": return "Now " + s.Slots + " wagons";
                case "speed": return "Now up to x" + Content.Speeds[s.SpeedsOpen - 1].ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
                case "start": return "Now " + Format.Money(s.StartScrip) + " scrip";
                case "whistle": return "Now every " + Math.Round(s.WhistleEvery) + " s";
                case "mend": return lv == 0 ? "" : "Now " + (u.Step * lv * 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "% a second";
                default: return lv == 0 ? "" : "Now +" + Math.Round(u.Step * lv * 100) + "%";
            }
        }

        void ShowWorkshop()
        {
            var s = S;
            Frame("WORKSHOP");
            float y = 0;
            foreach (var u in Content.Upgrades)
            {
                var id = u.Id; bool max = s.UpgradeMaxed(id); double cost = s.UpgradeCost(id); bool can = s.State.steel >= cost;
                y = Entry(y, Ui.Ic(id == "slots" ? "box" : id == "speed" ? "bolt" : id == "whistle" ? "bell" : id == "scrip" || id == "start" ? "coins" : id == "steel" ? "sack" : id == "hull" || id == "plate" || id == "mend" ? "wrench" : "target"), Ui.Ink,
                    u.Name + "  <size=24><color=#7A5A40>level " + s.UpgradeLevel(id) + "</color></size>", (u.Blurb + ".  " + UpgradeNow(s, u)).TrimEnd(),
                    max ? null : Format.Money(cost) + " steel", Ui.Green, max || !can ? null : (Action)(() => { if (S.BuyUpgrade(id)) { Sfx.Play("build", 0.6f); Haptics.Light(); } }), max ? "MAX" : null);
            }
            y = Heading(y, "THE BARON'S SEALS: you have " + s.State.seals + ". One from every carriage you bring down.");
            foreach (var d in Content.Perks)
            {
                var id = d.Id; bool max = s.PerkMaxed(id); int cost = s.PerkCost(id); bool can = s.State.seals >= cost;
                y = Entry(y, Ui.Ic("medal"), Ui.Ink, d.Name + "  <size=24><color=#7A5A40>level " + s.PerkLevel(id) + " of " + d.Max + "</color></size>", d.Blurb + ".",
                    max ? null : cost + (cost == 1 ? " seal" : " seals"), Ui.Grape, max || !can ? null : (Action)(() => { if (S.BuyPerk(id)) { Sfx.Play("star", 0.7f); Haptics.Success(); GameRoot.Instance.Save(); } }), max ? "MAX" : null);
            }
            if (s.RenewShown) y = NewEngineRows(y);
            EndList(-y + 30);
        }

        // ---- a new engine ----

        float NewEngineRows(float y)
        {
            var s = S;
            int n = s.ChartersOnRenew, have = s.State.charters;
            y = Heading(y, "A NEW ENGINE: charters held " + have + (have > 0 ? " (+" + Mathf.RoundToInt(have * (float)Content.CharterBonus * 100) + "% steel and damage)" : ""));
            string body = n > 0
                ? "Start the workshop and the wagons again for " + n + (n == 1 ? " charter" : " charters") + ": +" + Mathf.RoundToInt(n * (float)Content.CharterBonus * 100) + "% more steel and damage, for good. " + Format.Money(s.SteelToNextCharter) + " more steel on this engine for " + (n + 1) + "."
                : "Every charter makes steel and damage " + Mathf.RoundToInt((float)Content.CharterBonus * 100) + "% stronger, for good. This engine has earned " + Format.Money(s.EngineSteel) + " steel: " + Format.Money(s.SteelToNextCharter) + " more for the first charter.";
            string why = s.State.run != null ? "FINISH THE RUN" : null;
            y = Entry(y, TrainView.Instance != null ? TrainView.Instance.Icon("engine@charter") : Ui.Art("star"), Color.white, "Build a new engine" + (s.State.engines > 0 ? "  <size=24><color=#7A5A40>engine no. " + (s.State.engines + 1) + "</color></size>" : ""), body,
                n > 0 && why == null ? "NEW ENGINE" : null, Ui.Gold, n > 0 && s.CanRenew ? (Action)AskRenew : null, n > 0 ? why : "NOT YET", n > 0 && s.CanRenew);
            return y;
        }

        void AskRenew()
        {
            var s = S; int n = s.ChartersOnRenew;
            Confirm("A NEW ENGINE?", "You get " + n + (n == 1 ? " charter" : " charters") + ": +" + Mathf.RoundToInt(n * (float)Content.CharterBonus * 100) + "% steel and damage for good.\n\nYour steel, workshop and wagons start again from a Gatling Car. You keep gems, seals and perks, the lines and plans you have found, awards, liveries and purchases.",
                "BUILD IT", Ui.Gold, () =>
                {
                    int got = S.Renew();
                    if (got <= 0) return;
                    Sfx.Play("star", 0.9f); Haptics.Success(); GameRoot.Instance.Save(true);
                    Toast("Engine no. " + (S.State.engines + 1) + " is on the rails: +" + got + (got == 1 ? " charter" : " charters"));
                    if (_screen == Page.Workshop) ShowWorkshop();
                }, "NOT YET");
        }

        // ---- liveries ----

        float LiveryRows(float y)
        {
            var s = S;
            y = Heading(y, "LIVERIES: the engine's paint. Looks only, nothing else changes.");
            foreach (var k in Content.Skins)
            {
                var id = k.Id; bool own = s.SkinOwned(id), on = s.State.skin == id;
                string button = on ? null : own ? "USE" : k.Gems > 0 ? k.Gems + " GEMS" : null;
                Action tap = null;
                if (!on && own) tap = () => { if (S.UseSkin(id)) { Sfx.Play("build", 0.6f); GameRoot.Instance.Save(); } };
                else if (!own && k.Gems > 0 && s.State.gems >= k.Gems) tap = () => { if (S.BuySkin(id)) { Sfx.Play("star", 0.8f); Haptics.Success(); GameRoot.Instance.Save(true); Toast(k.Name + " is on the engine"); } };
                y = Entry(y, TrainView.Instance != null ? TrainView.Instance.Icon("engine@" + id) : null, Color.white, k.Name, k.Blurb, button, own ? Ui.Blue : Ui.Grape, tap,
                    on ? "IN USE" : !own && k.Gems <= 0 ? (k.Engines > 0 ? "ENGINE NO. " + (k.Engines + 1) : null) : null, on);
            }
            return y;
        }

        // ---- the train ----

        void ShowTrain()
        {
            var s = S;
            var p = Frame("THE TRAIN");
            float y = 0;
            var head = Ui.Rect("Order", _list); head.Band(true, y, 250);
            var help = Ui.Label("H", head, "Coupled now (the engine leads, on the right). Tap a wagon, then another, to swap them; tap ✕ to take one off.", 26, Ui.Dim, TextAnchor.MiddleCenter, false);
            help.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(1400, 40));
            TrainRow(head, new Vector2(0, -46), false);
            SpareRow(head, new Vector2(0, -196));
            y -= 270;
            y = Heading(y, "WAGONS");
            foreach (var d in Content.Cars)
            {
                var id = d.Id; int lv = s.CarLevel(id); bool built = lv > 0; bool plans = built || s.PlansFound(id);
                double cost = s.CarCost(id); bool can = s.State.steel >= cost;
                string stats = d.Support ? d.Blurb : d.Blurb + "  " + Format.Money(d.Damage * Math.Pow(Content.CarPower, Math.Max(0, lv - 1)) * s.DamageMult) + " damage every " + d.Interval.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " s";
                if (!plans) y = Entry(y, Ui.Ic("lock"), Ui.Dim, d.Name, "The plans turn up at mile " + d.Mile + " on any line.", null, Ui.Off, null, "MILE " + d.Mile);
                else y = Entry(y, TrainView.Instance != null ? TrainView.Instance.Icon(id) : null, Color.white, d.Name + (built ? "  <size=24><color=#7A5A40>level " + lv + "</color></size>" : ""), stats,
                    (built ? "" : "BUILD  ") + Format.Money(cost) + " steel", built ? Ui.Green : Ui.Gold, can ? (Action)(() => { if (S.BuyCar(id)) { Sfx.Play("build", 0.7f); Haptics.Light(); } }) : null, null, !built);
            }
            y = LiveryRows(y);
            EndList(-y + 30);
        }

        // ---- today ----

        void ShowToday()
        {
            var s = S;
            Frame("TODAY");
            float y = 0;
            y = Heading(y, "THE WEEK'S GIFTS");
            var row = Ui.Rect("Gifts", _list); row.Band(true, y, 170);
            for (int i = 0; i < Sim.Gifts.Length; i++)
            {
                var g = Sim.Gifts[i]; bool taken = i < s.GiftIndex, next = i == s.GiftIndex;
                var tile = Ui.ArtImg("G", row, "card", next && s.GiftReady ? new Color(0.76f, 0.93f, 0.72f) : taken ? new Color(0.84f, 0.8f, 0.72f) : Ui.Card, 26);
                tile.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - 3) * 196, 0), new Vector2(186, 160));
                Ui.Label("D", tile.transform, "DAY " + (i + 1), 24, Ui.Dim).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(170, 30));
                var ic = Ui.Img("I", tile.transform, taken ? new Color(1, 1, 1, 0.4f) : Color.white); ic.sprite = Ui.Art(g.Gems > 0 ? "gem" : "steel"); ic.preserveAspect = true;
                ic.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(64, 64));
                Ui.Label("V", tile.transform, taken ? "TAKEN" : g.Gems > 0 ? g.Gems + " gems" : Format.Money(s.GiftSteel(g)) + " steel", 24, Ui.Ink).rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(176, 30));
            }
            y -= 180;
            var take = Ui.Button("Take", _list, s.GiftReady ? "TAKE TODAY'S GIFT" : "COME BACK TOMORROW", s.GiftReady ? Ui.Green : Ui.Off, s.GiftReady ? (Action)(() => { if (S.ClaimGift()) { Sfx.Play("claim", 0.9f); Haptics.Success(); GameRoot.Instance.Save(); } }) : null, 36);
            var tr = (RectTransform)take.transform; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1); tr.pivot = new Vector2(0.5f, 1); tr.anchoredPosition = new Vector2(0, y); tr.sizeDelta = new Vector2(560, 96);
            y -= 116;
            y = Heading(y, "TODAY'S TASKS");
            for (int i = 0; i < Sim.TasksADay; i++)
            {
                var t = s.TaskAt(i); int k = i; bool done = s.TaskDone(i), claimed = s.TaskClaimed(i);
                y = Entry(y, Ui.Ic("tasks"), Ui.Ink, string.Format(t.Text, Format.Money(t.Goal)), Format.Money(s.TaskProgress(i)) + " of " + Format.Money(t.Goal) + " today",
                    claimed ? null : "+" + t.Gems + " gems", Ui.Green, done && !claimed ? (Action)(() => { if (S.ClaimTask(k)) { Sfx.Play("claim", 0.8f); Haptics.Success(); GameRoot.Instance.Save(); } }) : null, claimed ? "DONE" : null, done && !claimed);
            }
            EndList(-y + 30);
        }

        // ---- awards ----

        static int Rank(Sim s, AchievementDef a) { return s.AchievementClaimed(a) ? 2 : s.AchievementDone(a) ? 0 : 1; }

        void ShowAwards()
        {
            var s = S;
            Frame("AWARDS");
            float y = 0;
            y = Entry(y, Ui.Ic("trophy"), Ui.Ink, "This week's furthest run", "Every run counts toward this week's board on Game Center. It starts again every Monday.", "SEE", Ui.Blue,
                GameCenter.Blocked == null ? (Action)GameCenter.ShowWeekly : () => Toast(GameCenter.Blocked));
            var list = new List<AchievementDef>(Sim.Achievements);
            list.Sort((a, b) => Rank(s, a).CompareTo(Rank(s, b)));
            foreach (var a in list)
            {
                var id = a.Id; bool done = s.AchievementDone(a), claimed = s.AchievementClaimed(a);
                double have = Math.Min(a.Goal, a.Have(s));
                y = Entry(y, Ui.Art("star"), claimed ? new Color(1, 1, 1, 0.45f) : Color.white, a.Name, a.Blurb + (done || a.Goal <= 1 ? "" : "  (" + Format.Money(have) + " of " + Format.Money(a.Goal) + ")"),
                    claimed ? null : "+" + a.Gems + " gems", Ui.Green, done && !claimed ? (Action)(() => { if (S.ClaimAchievement(id)) { Sfx.Play("claim", 0.8f); Haptics.Success(); GameRoot.Instance.Save(); } }) : null, claimed ? "TAKEN" : null, done && !claimed);
            }
            EndList(-y + 30);
        }

        // ---- the shop ----

        void ShowShop()
        {
            var s = S;
            Frame("SHOP" + (Store.Testing ? " (TEST)" : ""));
            float y = 0;
            y = Heading(y, "TIME WARPS: the depot's steel, right now");
            for (int i = 0; i < Sim.Warps.Length; i++)
            {
                var w = Sim.Warps[i]; int k = i; double steel = s.WarpSteel(w);
                y = Entry(y, Ui.Ic("clock"), Ui.Ink, w.Name, steel > 0 ? "+" + Format.Money(steel) + " steel: " + w.Hours + " hours of patrols at once" : "Finish a run first: the patrols work the lines you have crossed",
                    w.Gems + " GEMS", Ui.Grape, steel > 0 && s.State.gems >= w.Gems ? (Action)(() => { if (S.BuyWarp(k)) { Sfx.Play("coin", 0.9f); Toast("+" + Format.Money(steel) + " steel"); GameRoot.Instance.Save(); } }) : null);
            }
            y = Heading(y, "BOUGHT ONCE");
            foreach (var prod in Sim.Products)
            {
                if (!prod.Once) continue;
                var pr = prod; bool owned = s.Owns(prod.Id);
                y = Entry(y, Ui.Art("star"), Color.white, prod.Name, prod.Blurb, owned ? null : Store.Price(prod), Ui.Green,
                    owned ? null : (Action)(() => Store.Buy(pr, (ok, msg) => { Toast(msg); if (ok) Sfx.Play("confirm"); if (_screen == Page.Shop) ShowShop(); })), owned ? "OWNED" : null, owned);
            }
            y = Heading(y, "GEMS");
            foreach (var prod in Sim.Products)
            {
                if (prod.Once) continue;
                var pr = prod;
                y = Entry(y, Ui.Art("gem"), Color.white, prod.Name, prod.Blurb, Store.Price(prod), Ui.Green,
                    () => Store.Buy(pr, (ok, msg) => { Toast(msg); if (ok) Sfx.Play("confirm"); if (_screen == Page.Shop) ShowShop(); }));
            }
            y = Entry(y, Ui.Ic("cart"), Ui.Ink, "Restore purchases", "Bring back what this Apple Account has bought once.", "RESTORE", Ui.Blue, () => Store.Restore(msg => { Toast(msg); if (_screen == Page.Shop) ShowShop(); }));
            var note = Ui.Label("Note", _list, "Adverts are only ever shown when you ask for one. Everything in the game can be earned by playing.", 26, Ui.Dim, TextAnchor.MiddleCenter, false); note.rectTransform.Band(true, y, 60);
            EndList(-y + 70);
        }

        // ---- settings ----

        const string PrivacyUrl = "https://mrnickrushing.github.io/ironclad/privacy.html", SupportUrl = "https://mrnickrushing.github.io/ironclad/support.html";

        void ShowSettings()
        {
            var s = S;
            Frame("SETTINGS");
            Action again = () => { if (_screen == Page.Settings) ShowSettings(); };
            float y = 0;
            y = Heading(y, "THE GAME");
            y = Entry(y, Ui.Ic("sound"), Ui.Ink, "Sound", Sfx.Muted ? "Off" : "On", Sfx.Muted ? "TURN ON" : "TURN OFF", Ui.Blue, () => { Sfx.Muted = !Sfx.Muted; again(); });
            y = Entry(y, Ui.Ic("sound"), Ui.Ink, "Music", Sfx.Music ? "On" : "Off", Sfx.Music ? "TURN OFF" : "TURN ON", Ui.Blue, () => { Sfx.Music = !Sfx.Music; again(); });
            y = Entry(y, Ui.Ic("bolt"), Ui.Ink, "Battery saver", FrameBudget.BatterySaver ? "On: 30 frames a second, a cooler phone" : "Off: 60 frames a second", FrameBudget.BatterySaver ? "TURN OFF" : "TURN ON", Ui.Blue, () => { FrameBudget.BatterySaver = !FrameBudget.BatterySaver; again(); });
            y = Entry(y, Ui.Ic("vibrate"), Ui.Ink, "Vibration", Haptics.On ? "On: a tap you can feel" : "Off", Haptics.On ? "TURN OFF" : "TURN ON", Ui.Blue, () => { Haptics.On = !Haptics.On; again(); });
            y = Entry(y, Ui.Ic("bell"), Ui.Ink, "Reminders", Reminders.On ? "On: when the depot is full, the train is waiting, or today's gift is ready" : "Off", Reminders.On ? "TURN OFF" : "TURN ON", Ui.Blue, () => { Reminders.On = !Reminders.On; again(); });
            y = Entry(y, Ui.Ic("chart"), Ui.Ink, "Anonymous play statistics", (Telemetry.Sharing ? "On" : "Off") + ": where players get stuck. Nothing about you, no tracking.", Telemetry.Sharing ? "TURN OFF" : "TURN ON", Ui.Blue, () => { Telemetry.Sharing = !Telemetry.Sharing; again(); });

            y = Heading(y, "YOUR ACCOUNT");
            if (!AppleAccount.SignedIn)
                y = Entry(y, Ui.Ic("user"), Ui.Ink, "Sign in with Apple", "Ties your purchases to your account so they follow you to a new phone.", AppleAccount.Supported ? "SIGN IN" : "ON IPHONE", Ui.Ink,
                    AppleAccount.Supported ? (Action)(() => AppleAccount.SignIn((ok, msg) => { Toast(ok ? "Signed in" : msg ?? "Not signed in"); if (ok) GameRoot.Instance.Save(); again(); })) : null);
            else
                y = Entry(y, Ui.Ic("user"), Ui.Ink, "Signed in with Apple", "Your purchases are tied to your account.", "SIGN OUT", Ui.Off, () => { AppleAccount.SignOut(); Toast("Signed out"); again(); });
            y = Entry(y, Ui.Ic("user"), Ui.Ink, "Conductor: " + (string.IsNullOrEmpty(s.State.playerName) ? "(no name yet)" : s.State.playerName), "The name on your train.", "RENAME", Ui.Blue, AskName);
            y = Entry(y, Ui.Ic("globe"), Ui.Ink, "iCloud save", CloudSave.Available ? "On: your progress is kept in your own iCloud and follows you to a new phone." : "Off: sign in to iCloud in the Settings app to keep your progress safe.", null, Ui.Off, null, CloudSave.Available ? "ON" : "OFF");
            y = Entry(y, Ui.Ic("trophy"), Ui.Ink, "Leaderboards", "Furthest mile, miles rolled and raiders turned back, under your Game Center name.", "OPEN", Ui.Blue, GameCenter.Blocked == null ? (Action)GameCenter.ShowBoards : () => Toast(GameCenter.Blocked));
            y = Entry(y, Ui.Ic("clock"), Ui.Ink, "This week's furthest run", "The best single run anyone has made since Monday. The board starts again every week.", "OPEN", Ui.Blue, GameCenter.Blocked == null ? (Action)GameCenter.ShowWeekly : () => Toast(GameCenter.Blocked));
            y = Entry(y, Ui.Ic("medal"), Ui.Ink, "Game Center achievements", "Every award you earn here is kept on your Game Center profile.", "OPEN", Ui.Blue, GameCenter.Blocked == null ? (Action)GameCenter.ShowAchievements : () => Toast(GameCenter.Blocked));

            y = Heading(y, "MORE");
            y = Entry(y, Ui.Art("star"), Color.white, "Enjoying Ironclad?", "Rate it on the App Store.", "RATE", Ui.Gold, OpenStoreReview);
            y = Entry(y, Ui.Ic("lock"), Ui.Ink, "Privacy policy", "What the game keeps and what it never does.", "READ", Ui.Blue, () => Application.OpenURL(PrivacyUrl));
            y = Entry(y, Ui.Ic("bell"), Ui.Ink, "Help and support", "Questions, problems, a purchase that went wrong.", "OPEN", Ui.Blue, () => Application.OpenURL(SupportUrl));
            y = Entry(y, Ui.Ic("medal"), Ui.Ink, "Credits", "The free models, fonts, icons and sounds this game is built with.", "SHOW", Ui.Blue, ShowCredits);
            y = Entry(y, Ui.Ic("trash"), Ui.Ink, "Delete account and start over", "Erases your progress on this device and in your iCloud, and signs you out. Purchases made once can be restored.", "DELETE", Ui.Coral, AskDelete);
            var v = Ui.Label("V", _list, "Ironclad  " + Application.version + "  ·  Rushing Technologies", 24, Ui.Dim, TextAnchor.MiddleCenter, false); v.rectTransform.Band(true, y, 50);
            EndList(-y + 60);
        }

        void AskDelete()
        {
            Confirm("DELETE EVERYTHING?", "Your steel, wagons, workshop, seals and gems are erased from this device and from your iCloud, and you are signed out. This can't be undone.\n\nThings bought once (not gems) can be restored from the shop.",
                "DELETE", Ui.Coral, () =>
                {
                    AppleAccount.SignOut(); Telemetry.Forget(); GameCenter.Forget();
                    _screen = Page.None;
                    GameRoot.Instance.Restart();
                });
        }

        void AskName()
        {
            var card = Card(880, 440, "CONDUCTOR");
            var back = Ui.ArtImg("Field", card, "card", Color.white, 26); back.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(700, 100));
            var text = Ui.Label("T", back.transform, "", 44, Ui.Ink); text.rectTransform.Fill(12); text.supportRichText = false;
            var input = back.gameObject.AddComponent<InputField>(); back.raycastTarget = true;
            input.textComponent = text; input.characterLimit = 16; input.text = S.State.playerName ?? "";
            var ok = Ui.Button("Ok", card, "SAVE", Ui.Green, () => { S.Rename(input.text); GameRoot.Instance.Save(); CloseCard(); if (_screen == Page.Settings) ShowSettings(); }, 40);
            ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(205, 44), new Vector2(380, 110));
            var no = Ui.Button("No", card, "CANCEL", Ui.Off, CloseCard, 40); ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-205, 44), new Vector2(380, 110));
            input.Select(); input.ActivateInputField();
        }

        const string CreditsText =
            "Ironclad is made by Rushing Technologies.\n\n" +
            "<b>Models</b>\nThe locomotive, wagons, rails, turrets, poles, trees, rocks and props: Kenney (kenney.nl), CC0.\n" +
            "Cannon, cart and a horse: Quaternius, CC0. The raiders and riflemen: Cowboy by mastjie, CC0. Water tower: Kay Lousberg, CC0.\n" +
            "Galloping horse: jeremy, CC BY 3.0. Gatling gun: Zsky, CC BY 3.0. Carriage: Jie gu, CC BY 3.0. Dynamite: Neko, CC BY 3.0.\n" +
            "Minecart: Hunter Paramore, CC BY 3.0. Wild West Cart: Anonymous, CC BY 3.0. Saloon, wagon and windmill: Poly by Google, CC BY 3.0. (poly.pizza)\n\n" +
            "<b>Fonts</b>\nLilita One by Juan Montoreano and Fredoka by Milena Brandão and Hafontia, SIL Open Font License.\n\n" +
            "<b>Sound</b>\nInterface sounds: Kenney, CC0. The music and the sounds of the fight are made for the game.\n\n<b>Icons</b>\nFont Awesome Free by Fonticons, Inc. (fontawesome.com), CC BY 4.0.\n\n<b>Sky</b>\nPoly Haven, CC0.\n\nThank you for playing.";

        void ShowCredits()
        {
            var card = Card(1500, 880, "CREDITS");
            var b = Ui.Label("B", card, CreditsText, 26, Ui.Ink, TextAnchor.UpperLeft, false); b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(56, 150); b.rectTransform.offsetMax = new Vector2(-56, -90);
            b.resizeTextForBestFit = true; b.resizeTextMinSize = 16; b.resizeTextMaxSize = 26;      // it all fits above CLOSE, whatever the screen
            var ok = Ui.Button("Ok", card, "CLOSE", Ui.Blue, CloseCard, 40); ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(360, 104));
        }
    }

    /// <summary>Turns slowly (the rays behind a prize).</summary>
    public sealed class Spinner : MonoBehaviour
    {
        void Update() { transform.Rotate(0, 0, 22 * Time.unscaledDeltaTime); }
    }
}
