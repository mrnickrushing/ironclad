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
        enum Page { None, Workshop, Train, Awards, Shop, Settings }
        Page _screen = Page.None;
        RectTransform _list; float _listY;

        void Open(Page s)
        {
            _screen = s; _listY = 0; _list = null; _shuntFrom = -1;
            Sfx.Play("open", 0.5f);
            RefreshScreen();
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
            }
        }

        static readonly Dictionary<string, string> About = new Dictionary<string, string>
        {
            { "WORKSHOP", "Steel buys these for good: every run after starts stronger." },
            { "THE TRAIN", "Build wagons, raise their levels, and choose the order they are coupled in." },
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
                ab.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -listTop + 8), new Vector2(1380, 40));
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
                case "mend": return "Now " + (u.Step * lv * 100).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "% a second";
                default: return "Now +" + Math.Round(u.Step * lv * 100) + "%";
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
                    u.Name + "  <size=24><color=#7A5A40>level " + s.UpgradeLevel(id) + "</color></size>", u.Blurb + ".  " + UpgradeNow(s, u),
                    max ? null : Format.Money(cost) + " steel", Ui.Green, max || !can ? null : (Action)(() => { if (S.BuyUpgrade(id)) { Sfx.Play("build", 0.6f); Haptics.Light(); } }), max ? "MAX" : null);
            }
            EndList(-y + 30);
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
                else y = Entry(y, Ui.Ic(d.Support ? "wrench" : "target"), Ui.Ink, d.Name + (built ? "  <size=24><color=#7A5A40>level " + lv + "</color></size>" : ""), stats,
                    (built ? "" : "BUILD  ") + Format.Money(cost), built ? Ui.Green : Ui.Gold, can ? (Action)(() => { if (S.BuyCar(id)) { Sfx.Play("build", 0.7f); Haptics.Light(); } }) : null, null, !built);
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
            y = Heading(y, "FOR GOOD: bought once, kept for ever");
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
            y = Entry(y, Ui.Ic("bolt"), Ui.Ink, "Vibration", Haptics.On ? "On: a tap you can feel" : "Off", Haptics.On ? "TURN OFF" : "TURN ON", Ui.Blue, () => { Haptics.On = !Haptics.On; again(); });
            y = Entry(y, Ui.Ic("bell"), Ui.Ink, "Reminders", Reminders.On ? "On: a notification when the depot is full" : "Off", Reminders.On ? "TURN OFF" : "TURN ON", Ui.Blue, () => { Reminders.On = !Reminders.On; again(); });
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
            y = Entry(y, Ui.Ic("medal"), Ui.Ink, "Game Center achievements", "Every award you earn here is kept on your Game Center profile.", "OPEN", Ui.Blue, GameCenter.Blocked == null ? (Action)GameCenter.ShowAchievements : () => Toast(GameCenter.Blocked));

            y = Heading(y, "MORE");
            y = Entry(y, Ui.Art("star"), Color.white, "Enjoying Ironclad?", "Rate it on the App Store.", "RATE", Ui.Gold, OpenStoreReview);
            y = Entry(y, Ui.Ic("lock"), Ui.Ink, "Privacy policy", "What the game keeps and what it never does.", "READ", Ui.Blue, () => Application.OpenURL(PrivacyUrl));
            y = Entry(y, Ui.Ic("bell"), Ui.Ink, "Help and support", "Questions, problems, a purchase that went wrong.", "OPEN", Ui.Blue, () => Application.OpenURL(SupportUrl));
            y = Entry(y, Ui.Ic("medal"), Ui.Ink, "Credits", "The free models, fonts and sounds this game is built with.", "SHOW", Ui.Blue, ShowCredits);
            y = Entry(y, Ui.Ic("trash"), Ui.Ink, "Delete account and start over", "Erases your progress on this device and in your iCloud, and signs you out. Purchases made once can be restored.", "DELETE", Ui.Coral, AskDelete);
            var v = Ui.Label("V", _list, "Ironclad  " + Application.version + "  ·  Rushing Technologies", 24, Ui.Dim, TextAnchor.MiddleCenter, false); v.rectTransform.Band(true, y, 50);
            EndList(-y + 60);
        }

        void AskDelete()
        {
            Confirm("DELETE EVERYTHING?", "Your steel, wagons, workshop and gems are erased from this device and from your iCloud, and you are signed out. This can't be undone.\n\nThings bought once (not gems) can be restored from the shop.",
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
            "<b>Models</b>\nThe locomotive, wagons, rails, turrets, trees, rocks, props and people: Kenney (kenney.nl), CC0.\n" +
            "Cannon, cart and a horse: Quaternius, CC0. Cowboy: mastjie, CC0. Water tower: Kay Lousberg, CC0.\n" +
            "Galloping horse: jeremy, CC BY 3.0. Gatling gun: Zsky, CC BY 3.0. Carriage: Jie gu, CC BY 3.0. Dynamite: Neko, CC BY 3.0.\n" +
            "Minecart: Hunter Paramore, CC BY 3.0. Wild West Cart: Anonymous, CC BY 3.0. Saloon, wagon and windmill: Poly by Google, CC BY 3.0. (poly.pizza)\n\n" +
            "<b>Fonts</b>\nLilita One by Juan Montoreano and Fredoka by Milena Brandão and Hafontia, SIL Open Font License.\n\n" +
            "<b>Sound</b>\nInterface sounds: Kenney, CC0.\n\n<b>Sky</b>\nPoly Haven, CC0.\n\nThank you for playing.";

        void ShowCredits()
        {
            var card = Card(1200, 800, "CREDITS");
            var b = Ui.Label("B", card, CreditsText, 26, Ui.Ink, TextAnchor.UpperLeft, false); b.rectTransform.Fill(); b.rectTransform.offsetMin = new Vector2(50, 150); b.rectTransform.offsetMax = new Vector2(-50, -90);
            var ok = Ui.Button("Ok", card, "CLOSE", Ui.Blue, CloseCard, 40); ((RectTransform)ok.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(360, 104));
        }
    }

    /// <summary>Turns slowly (the rays behind a prize).</summary>
    public sealed class Spinner : MonoBehaviour
    {
        void Update() { transform.Rotate(0, 0, 22 * Time.unscaledDeltaTime); }
    }
}
