using System;
using System.Collections.Generic;
using Ironclad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironclad.Game
{
    /// <summary>The run: hull, mile, scrip, a chip for every car along the bottom, the whistle; the station; the end.</summary>
    public sealed partial class Hud
    {
        sealed class Chip { public int Car; public Image Back, Plate; public Text Level, Cost; public Button Buy; public Image BuyImg; }
        readonly List<Chip> _chips = new List<Chip>();
        Image _hullFill, _mileFill, _whistleFill; Text _hullText, _mileText, _scripText, _steelText, _gemText, _speedText, _hintText, _lineText;
        RectTransform _hullBar, _hint; Button _whistle;
        float _hullFlash;

        public void HullHit()
        {
            _hullFlash = 1;
            if (Time.unscaledTime - _lastHullBuzz > 0.6f) { _lastHullBuzz = Time.unscaledTime; Haptics.Light(); }      // felt, but not a constant buzz
        }
        float _lastHullBuzz;

        /// <summary>From the second run on the depot has steel to bring home while the player is away: the moment to
        /// ask, once, whether the game may say so with a notification.</summary>
        void AfterRun() { if (S.State.runs >= 2) Reminders.AskOnce(); }

        const float AutoCollectAfter = 12;
        Text _collectLabel; float _overAuto;

        /// <summary>With AUTO on, a finished run collects itself after a few seconds and the train sets off again.</summary>
        void UpdateOver()
        {
            var s = S;
            if (!s.State.auto || !s.RunEnded || ModalOpen) return;
            var p = UnityEngine.InputSystem.Pointer.current;
            if (p != null && p.press.isPressed) _overAuto = AutoCollectAfter;      // someone is here: let them choose
            _overAuto -= Time.unscaledDeltaTime;
            if (_collectLabel != null) _collectLabel.text = "COLLECT  <size=28>" + Mathf.CeilToInt(Mathf.Max(0, _overAuto)) + "</size>";
            if (_overAuto <= 0 && s.CloseRun()) { Sfx.Play("claim", 0.6f); GameRoot.Instance.Save(true); AfterRun(); }
        }

        static string Short(string type)
        {
            switch (type)
            {
                case "engine": return "ENGINE"; case "gatling": return "GATLING"; case "cannon": return "CANNON"; case "supply": return "SUPPLY"; case "rifle": return "SHARPS";
                case "armour": return "ARMOUR"; case "flame": return "FURNACE"; case "mortar": return "MORTAR"; case "repair": return "FITTERS"; default: return "DYNAMO";
            }
        }

        /// <summary>The picture of a car, from the model itself.</summary>
        static Image CarPicture(Transform parent, string type)
        {
            var img = Ui.Img("Pic", parent, Color.white);
            if (TrainView.Instance != null) img.sprite = TrainView.Instance.Icon(type);
            img.preserveAspect = true;
            return img;
        }

        void BuildRun()
        {
            var r = S.Run;
            // Hull, top left.
            _hullBar = Bar(_face, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -22), new Vector2(520, 64), Ui.Green, out _hullFill);
            _hullText = Ui.Label("T", _hullBar.parent, "", 32, Color.white); _hullText.rectTransform.Fill(); _hullText.Stroke(2f);
            var hl = Ui.Label("L", _face, "HULL", 26, Color.white, TextAnchor.MiddleLeft); hl.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -86), new Vector2(200, 34)); hl.Stroke(2f);

            // The mile, top centre.
            var ribbon = Ui.Ribbon("Mile", _face, Ui.Coral); ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(460, 104));
            _mileText = Ui.Label("T", ribbon.transform, "", 50, Color.white); _mileText.rectTransform.Fill(); _mileText.rectTransform.offsetMin = new Vector2(40, 22); _mileText.rectTransform.offsetMax = new Vector2(-40, 0); _mileText.Stroke(2.5f);
            Bar(_face, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(380, 26), Ui.Gold, out _mileFill);
            _lineText = Ui.Label("Line", _face, "", 26, Color.white); _lineText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(700, 34)); _lineText.Stroke(2f);

            // Scrip, steel and gems, top right.
            _scripText = Pill(_face, new Vector2(1, 1), new Vector2(-24, -20), 300, Ui.Art("coin"), Color.white);
            _steelText = Pill(_face, new Vector2(1, 1), new Vector2(-340, -20), 280, Ui.Art("steel"), Color.white);
            _gemText = Pill(_face, new Vector2(1, 1), new Vector2(-24, -108), 220, Ui.Art("gem"), Color.white);

            // Running speed and giving up, under them.
            var sp = Ui.Button("Speed", _face, "", Ui.Blue, CycleSpeed, 34); ((RectTransform)sp.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-260, -106), new Vector2(150, 82));
            _speedText = sp.GetComponentInChildren<Text>();
            var quit = Ui.Button("Quit", _face, "II", Ui.Off, AskRetire, 34); ((RectTransform)quit.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-424, -106), new Vector2(96, 82));

            // A chip for every car, in the order they stand on screen: the engine on the right.
            int n = r.cars.Count; float w = Mathf.Min(196, 1700f / n), x0 = -(n - 1) * w / 2;
            for (int i = 0; i < n; i++)
            {
                int car = i; var c = r.cars[i];
                var chip = new Chip { Car = i };
                chip.Back = Ui.ArtImg("Chip", _face, "card", Color.white, 26);
                chip.Back.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(x0 + (n - 1 - i) * w - 110, 14), new Vector2(w - 8, 176));
                var pic = CarPicture(chip.Back.transform, c.type); pic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 58), new Vector2(Mathf.Min(w - 24, 124), Mathf.Min(w - 24, 124)));
                var name = Ui.Label("N", chip.Back.transform, Short(c.type), 22, Ui.Ink); name.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -50), new Vector2(w * 0.62f, 26)); name.alignment = TextAnchor.MiddleLeft;
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 14; name.resizeTextMaxSize = 22;
                chip.Level = Ui.Label("L", chip.Back.transform, "", 22, Ui.Dim, TextAnchor.MiddleRight); chip.Level.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -50), new Vector2(w * 0.32f, 26));
                Image plate; Bar(chip.Back.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(w - 30, 14), Ui.Blue, out plate); chip.Plate = plate;
                if (i == 0) plate.transform.parent.gameObject.SetActive(false);
                chip.Buy = Ui.Button("Buy", chip.Back.transform, "", Ui.Green, () => BuyField(car), 28);
                ((RectTransform)chip.Buy.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(w - 24, 64));
                chip.Cost = chip.Buy.GetComponentInChildren<Text>(); chip.BuyImg = chip.Buy.GetComponent<Image>();
                {
                    // The price is in scrip: its coin sits at the left of the button.
                    var coin = Ui.Img("Coin", chip.Buy.transform, Color.white); coin.sprite = Ui.Art("coin"); coin.preserveAspect = true; coin.raycastTarget = false;
                    coin.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(36, 36));
                    chip.Cost.rectTransform.offsetMin = new Vector2(chip.Cost.rectTransform.offsetMin.x + 34, chip.Cost.rectTransform.offsetMin.y);
                    chip.Cost.resizeTextForBestFit = true; chip.Cost.resizeTextMinSize = 16; chip.Cost.resizeTextMaxSize = 28;
                }
                chip.Buy.gameObject.AddComponent<HoldRepeat>().Tap = () => BuyField(car);
                _chips.Add(chip);
            }

            // The whistle, bottom right.
            _whistle = Ui.Button("Whistle", _face, "WHISTLE", Ui.Gold, SoundWhistle, 30);
            ((RectTransform)_whistle.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-22, 20), new Vector2(210, 156));
            _whistleFill = Ui.Img("Cool", _whistle.transform, new Color(0.1f, 0.06f, 0.04f, 0.6f), true); _whistleFill.rectTransform.Fill(6);
            _whistleFill.type = Image.Type.Filled; _whistleFill.fillMethod = Image.FillMethod.Vertical; _whistleFill.sprite = Ui.Round;

            // One line of help while the game is new.
            var hint = Ui.ArtImg("Hint", _face, "plate", Ui.Ink, 36); _hint = hint.rectTransform;
            _hint.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-110, 262), new Vector2(980, 66));
            _hintText = Ui.Label("T", _hint, "", 30, Color.white, TextAnchor.MiddleCenter, false); _hintText.rectTransform.Fill(8); _hintText.rectTransform.offsetMin = new Vector2(8, 12);
            UpdateRun();
        }

        void BuyField(int car)
        {
            if (S.FieldUpgrade(car)) { Sfx.Play("build", 0.5f, 1 + Mathf.Min(0.6f, S.Run.cars[car].field * 0.03f)); Haptics.Light(); }
            else Sfx.Play("error", 0.3f);
        }

        void SoundWhistle()
        {
            if (S.Whistle()) { Sfx.Play("whistle", 0.9f); Haptics.Medium(); if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.4f); if (TrainView.Instance != null) TrainView.Instance.WhistleFx(); }
            else Sfx.Play("error", 0.3f);
        }

        void CycleSpeed()
        {
            if (S.SpeedsOpen <= 1) { Toast("Build the Regulator in the workshop to run faster"); return; }
            S.SetSpeed((S.State.speed + 1) % S.SpeedsOpen);
        }

        void AskRetire()
        {
            Confirm("END THE RUN?", "The train stops here. You keep the steel earned so far: " + Format.Money(S.RunSteel) + ".", "END RUN", Ui.Coral, () => { S.Retire(); GameRoot.Instance.Save(); }, "KEEP GOING");
        }

        string Hint()
        {
            var s = S; var r = s.Run;
            if (r == null || r.phase == (int)Phase.Station) return null;
            if (s.State.runs >= 3) { if (s.State.runs < 12) for (int i = 1; i < r.cars.Count; i++) if (r.cars[i].Out) return r.scrip >= s.FieldCost(i) ? "A wagon is out of action: tap FIX to put its plating back" : "A wagon with no plating stops firing until it is fixed or you reach a station"; return null; }
            bool canBuy = false; for (int i = 0; i < r.cars.Count; i++) if (r.scrip >= s.FieldCost(i)) canBuy = true;
            for (int i = 1; i < r.cars.Count; i++) if (r.cars[i].Out) return r.scrip >= s.FieldCost(i) ? "A wagon is out of action: tap FIX to put its plating back" : "A wagon with no plating stops firing until it is fixed or you reach a station";
            if (canBuy) return "You have scrip: tap a green button below to make that wagon stronger";
            if (s.State.whistles == 0 && s.WhistleReady && r.enemies.Count >= 6) return "Too many? Sound the WHISTLE to throw them back";
            if (s.State.runs == 0 && r.mile <= 2) return "Your guns fire by themselves. Raiders pay scrip when they fall";
            if (s.State.runs == 0 && r.mark == 0 && r.enemies.Count >= 4 && r.mile >= 4) return "Tap a raider to point every gun at it";
            return null;
        }

        void UpdateRun()
        {
            var s = S; var r = s.Run;
            if (r == null || _hullFill == null) return;
            float hull = Mathf.Clamp01((float)(r.hull / r.hullMax));
            _hullFill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, hull), 1);
            _hullFlash = Mathf.MoveTowards(_hullFlash, 0, Time.unscaledDeltaTime * 3);
            _hullFill.color = Color.Lerp(hull > 0.5f ? Ui.Green : hull > 0.25f ? Ui.Gold : Ui.Coral, Color.white, _hullFlash);
            _hullText.text = Format.Money(Math.Ceiling(r.hull)) + " / " + Format.Money(r.hullMax);
            _mileText.text = "MILE " + r.mile;
            // On the Baron's mile the bar under the ribbon is his carriage's health; otherwise how far through the mile.
            EnemyState boss = null; foreach (var e in r.enemies) if (e.hp > 0 && Content.Enemy(e.kind).Boss) { boss = e; break; }
            float bar = boss != null ? (float)(boss.hp / boss.hpMax) : (float)(r.mileTime / Content.MileSeconds);
            _mileFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp(bar, 0.02f, 1), 1);
            _mileFill.color = boss != null ? Ui.Coral : Ui.Gold;
            bool rollingFace = _shown == Face.Run;
            if (_lineText.gameObject.activeSelf != rollingFace) { _lineText.gameObject.SetActive(rollingFace); _mileFill.transform.parent.gameObject.SetActive(rollingFace); }
            _lineText.text = s.Route.Name + (s.BossMile ? "  ·  <color=#FFB347>THE BARON'S CARRIAGE</color>" : "  ·  station at mile " + ((r.mile - 1) / Content.StationEvery + 1) * Content.StationEvery);
            _scripText.text = Format.Money(r.scrip);
            _steelText.text = Format.Money(r.steel);
            _gemText.text = s.State.gems.ToString();
            _speedText.text = "x" + s.Speed.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
            foreach (var c in _chips)
            {
                if (c.Car >= r.cars.Count) continue;
                var car = r.cars[c.Car];
                double cost = s.FieldCost(c.Car); bool can = r.scrip >= cost;
                c.Cost.text = (car.Out ? "FIX " : "") + Format.Money(cost);
                c.BuyImg.color = can ? (car.Out ? Ui.Gold : Ui.Green) : Ui.Off;
                c.Level.text = car.Out ? "<color=#C8321E>OUT</color>" : "Lv " + (car.field + 1);
                if (car.platingMax > 0) { float p = Mathf.Clamp01((float)(car.plating / car.platingMax)); c.Plate.rectTransform.anchorMax = new Vector2(Mathf.Max(0.03f, p), 1); c.Plate.color = car.Out ? Ui.Coral : Ui.Blue; }
                c.Back.color = car.Out ? new Color(1f, 0.7f, 0.66f) : Color.white;
            }
            float cool = s.WhistleEvery > 0 ? Mathf.Clamp01((float)(r.whistleIn / s.WhistleEvery)) : 0;
            _whistleFill.fillAmount = cool;
            string hint = _shown == Face.Run ? Hint() : null;
            if (_hint.gameObject.activeSelf != (hint != null)) _hint.gameObject.SetActive(hint != null);
            if (hint != null) _hintText.text = hint;
            // Left alone at a station with AUTO on, the train takes the rarest card and rolls on.
            if (_shown == Face.Station && s.State.auto) { _stationIdle += Time.unscaledDeltaTime; if (_stationIdle > 12) { _stationIdle = 0; s.PassStation(); } }
        }

        // ---- the station ----

        int _shuntFrom = -1, _stationShownFor = -1; float _stationIdle; double _overShownFor = -1;

        void BuildStation()
        {
            var s = S; var r = s.Run;
            var panel = Ui.ArtImg("Station", _face, "panel", Color.white, 60); panel.raycastTarget = true;
            panel.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -196), new Vector2(1500, 540));
            if (_stationShownFor != r.mile * 2 + (r.offer.Count > 0 ? 1 : 0)) { _stationShownFor = r.mile * 2 + (r.offer.Count > 0 ? 1 : 0); panel.gameObject.AddComponent<PopIn>(); }
            var p = panel.rectTransform;
            var ribbon = Ui.Ribbon("R", p, Ui.Gold); ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(760, 110));
            var t = Ui.Label("T", ribbon.transform, "STATION  ·  MILE " + r.mile, 46, Color.white); t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(40, 24); t.rectTransform.offsetMax = new Vector2(-40, 0); t.Stroke(2.5f);

            if (r.offer.Count > 0)
            {
                var sub = Ui.Label("S", p, "Plating made good, " + Mathf.RoundToInt((float)Content.StationHull * 100) + "% of the hull back. Take one card: it lasts the rest of this run.", 30, Ui.Dim, TextAnchor.MiddleCenter, false); sub.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(1300, 40));
                for (int i = 0; i < r.offer.Count; i++)
                {
                    int k = i; var pick = r.offer[i]; var def = Content.Card(pick.card);
                    var card = Ui.ArtImg("Card", p, "card", Color.white, 26); card.raycastTarget = true;
                    int nc = r.offer.Count; float cw = nc <= 3 ? 410 : nc == 4 ? 340 : 276, gap = cw + 22;
                    card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - (nc - 1) * 0.5f) * gap, -36), new Vector2(cw, 370));
                    var band = Ui.Img("Band", card.transform, RarityColors[pick.rarity], true); band.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(cw - 40, 50));
                    Ui.Label("R", band.transform, RarityNames[pick.rarity], 28, Color.white).rectTransform.Fill();
                    var nm = Ui.Label("N", card.transform, def.Name, 40, Ui.Ink); nm.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(cw - 30, 70));
                    nm.resizeTextForBestFit = true; nm.resizeTextMinSize = 24; nm.resizeTextMaxSize = 40;
                    var bl = Ui.Label("B", card.transform, Content.Blurb(def, (Rarity)pick.rarity), 30, Ui.Dim, TextAnchor.MiddleCenter, false); bl.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(cw - 44, 120)); bl.resizeTextForBestFit = true; bl.resizeTextMinSize = 20; bl.resizeTextMaxSize = 30;
                    var take = Ui.Button("Take", card.transform, "TAKE", Ui.Green, () => { if (S.TakeCard(k)) { Sfx.Play("claim", 0.8f); Haptics.Success(); } }, 38);
                    ((RectTransform)take.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(cw - 80, 84));
                }
                // The first deal of other cards at each station is free; after that it costs gems.
                bool free = s.FreeReroll, canRoll = free || s.State.gems >= Sim.RerollGems;
                var roll = Ui.Button("Roll", p, s.State.run.offer.Count + " OTHER CARDS  ·  " + (free ? "FREE" : Sim.RerollGems + " gems"), free ? Ui.Green : canRoll ? Ui.Grape : Ui.Off,
                    canRoll ? (Action)(() => { if (S.RerollCards()) { Sfx.Play("open", 0.7f); _stationShownFor = -1; } }) : null, 26);
                ((RectTransform)roll.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -20), new Vector2(330, 62));
                _stationIdle = 0;
                return;
            }

            // The card is taken: recouple the wagons if wanted, then roll on.
            var help = Ui.Label("S", p, "Tap a wagon, then another, to swap their places. Raiders from behind reach the last wagon first.", 28, Ui.Dim, TextAnchor.MiddleCenter, false);
            help.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -66), new Vector2(1400, 40));
            TrainRow(p, new Vector2(0, -150), true);
            SpareRow(p, new Vector2(0, -300));
            var go = Ui.Button("Go", p, "DEPART", Ui.Green, () => { if (S.Depart()) { Sfx.Play("dispatch", 0.8f); Haptics.Medium(); } }, 48);
            ((RectTransform)go.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(460, 110));
        }

        /// <summary>The wagons as they are coupled, engine on the right: tap two to swap; ✕ uncouples.</summary>
        void TrainRow(RectTransform p, Vector2 at, bool compact)
        {
            var s = S; int n = s.State.train.Count; float w = Mathf.Min(170, 1250f / (n + 1));
            float x0 = -(n) * w / 2;
            var eng = Ui.ArtImg("Engine", p, "card", new Color(0.86f, 0.8f, 0.7f), 26); eng.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x0 + n * w, at.y), new Vector2(w - 8, 110));
            var el = Ui.Label("N", eng.transform, "ENGINE", 20, Ui.Ink, TextAnchor.LowerCenter); el.rectTransform.Fill(6); el.rectTransform.offsetMin = new Vector2(6, 14);
            CarPicture(eng.transform, "engine").rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 14), new Vector2(92, 92));
            for (int i = 0; i < n; i++)
            {
                int place = i; bool picked = _shuntFrom == i;
                var b = Ui.Button("W", p, Short(s.State.train[i]), picked ? Ui.Gold : Ui.Blue, () =>
                {
                    if (_shuntFrom < 0) _shuntFrom = place;
                    else { if (_shuntFrom != place && S.Shunt(_shuntFrom, place)) Sfx.Play("build", 0.5f); _shuntFrom = -1; }
                    _dirty = true;
                }, 24);
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x0 + (n - 1 - i) * w, at.y), new Vector2(w - 8, 110));
                var bl = b.GetComponentInChildren<Text>(); bl.alignment = TextAnchor.LowerCenter; bl.fontSize = 20;
                var bp = CarPicture(b.transform, s.State.train[i]); bp.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 14), new Vector2(92, 92));
                if (n > 1 && picked)
                {
                    var x = Ui.Button("X", b.transform, "✕", Ui.Coral, () => { _shuntFrom = -1; if (S.Uncouple(place)) Sfx.Play("close", 0.5f); _dirty = true; }, 24);
                    ((RectTransform)x.transform).Place(new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-8, -6), new Vector2(54, 54));
                }
            }
        }

        /// <summary>Wagons built that could be coupled on (the same kind may ride twice).</summary>
        void SpareRow(RectTransform p, Vector2 at)
        {
            var s = S;
            bool room = s.State.train.Count < s.Slots;
            var lab = Ui.Label("L", p, room ? "Room for " + (s.Slots - s.State.train.Count) + " more: couple on" : "The train is full (" + s.Slots + " wagons). Longer Train in the workshop adds a place.", 26, Ui.Dim, TextAnchor.MiddleCenter, false);
            lab.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, at.y + 34), new Vector2(1300, 34));
            if (!room) return;
            var built = s.State.cars.FindAll(c => c.id != "engine");
            float w = Mathf.Min(170, 1300f / built.Count), x0 = -(built.Count - 1) * w / 2;
            for (int i = 0; i < built.Count; i++)
            {
                string id = built[i].id;
                var b = Ui.Button("S", p, "+ " + Short(id), Ui.Green, () => { if (S.Couple(id)) Sfx.Play("build", 0.5f); _dirty = true; }, 22);
                ((RectTransform)b.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x0 + i * w, at.y - 6), new Vector2(w - 8, 76));
            }
        }

        // ---- the end of the run ----

        void BuildOver()
        {
            var s = S; var r = s.Run;
            var dim = Ui.Img("Dim", _face, new Color(0.14f, 0.07f, 0.04f, 0.5f)); dim.raycastTarget = true; dim.rectTransform.Fill();
            var panel = Ui.ArtImg("Over", _face, "panel", Color.white, 60); panel.raycastTarget = true;
            var p = panel.rectTransform; p.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1040, 820));
            if (_overShownFor != r.time) { _overShownFor = r.time; panel.gameObject.AddComponent<PopIn>(); GameCenter.ReportRun(r.mile); }
            var ribbon = Ui.Ribbon("R", p, Ui.Coral); ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(700, 116));
            var t = Ui.Label("T", ribbon.transform, "END OF THE LINE", 50, Color.white); t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(40, 24); t.rectTransform.offsetMax = new Vector2(-40, 0); t.Stroke(2.5f);
            bool best = r.mile - 1 >= s.State.best[r.route] && r.mile > 1;
            Ui.Label("M", p, "MILE " + r.mile, 88, Ui.Ink).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -74), new Vector2(900, 100));
            Ui.Label("L", p, s.Route.Name + (best ? "  ·  <color=#C8501E>your furthest yet</color>" : "  ·  best " + s.State.best[r.route]), 30, Ui.Dim, TextAnchor.MiddleCenter, false).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -172), new Vector2(900, 38));
            // How close it came: the thing that makes the next run feel winnable.
            var miss = s.NearMiss;
            if (miss != null) { var ml = Ui.Label("Miss", p, miss, 34, Ui.Coral); ml.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -212), new Vector2(940, 44)); }
            Ui.Label("K", p, Format.Money(r.kills) + " raiders turned back  ·  " + r.cards.Count + (r.cards.Count == 1 ? " card" : " cards") + (r.seals > 0 ? "  ·  <color=#7A3FB0>+" + r.seals + (r.seals == 1 ? " seal" : " seals") + "</color>" : ""), 26, Ui.Dim, TextAnchor.MiddleCenter, false).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, miss != null ? -254 : -216), new Vector2(900, 36));
            var steel = Ui.ArtImg("Steel", p, "well", Color.white, 30); steel.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -296), new Vector2(620, 104));
            Ui.Label("S", steel.transform, "+" + Format.Money(s.RunSteel) + " steel" + (r.doubled ? "  (doubled)" : ""), 52, Ui.Ink).rectTransform.Fill(8);
            // What the next run can open, and how far there is to go.
            var unbuilt = s.Unbuilt; var next = s.NextUnlock;
            if (unbuilt.Count > 0) Ui.Label("N", p, "<color=#2E7D32>Unlocked: the " + unbuilt[0].Name + ". Build it in TRAIN</color>", 28, Ui.Dim, TextAnchor.MiddleCenter, false).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -414), new Vector2(940, 40));
            else if (next != null)
            {
                Ui.Label("N", p, "Next unlock at mile " + next.Mile + " " + next.Where + ": " + next.Name, 28, Ui.Dim, TextAnchor.MiddleCenter, false).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -410), new Vector2(940, 38));
                Image fill; var bar = Bar(p, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -458), new Vector2(600, 42), Ui.Gold, out fill);
                fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp((float)next.Have / Mathf.Max(1, next.Mile), 0.03f, 1), 1);
                var bt = Ui.Label("T", bar.parent, "mile " + next.Have + " of " + next.Mile, 30, Color.white); bt.rectTransform.Fill(); bt.Stroke(2f);
            }

            float y = 190;
            if (s.CanRevive)
            {
                bool ad = AdReady(AdReward.Revive), gems = s.State.gems >= Sim.ReviveGems;
                if (ad)
                {
                    var b = Ui.Button("Revive", p, AdLabel("BACK ON THE RAILS"), Ui.Blue, () => WatchAd(AdReward.Revive, () => { Sfx.Play("star", 0.8f); Haptics.Success(); }), 32);
                    ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-250, y), new Vector2(470, 120));
                }
                var g = Ui.Button("ReviveGems", p, "BACK ON THE RAILS\n<size=24>" + Sim.ReviveGems + " gems</size>", gems ? Ui.Grape : Ui.Off, gems ? (Action)(() => { if (S.Revive(true)) { Sfx.Play("star", 0.8f); GameRoot.Instance.Save(); } }) : null, 32);
                ((RectTransform)g.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(ad ? 250 : 0, y), new Vector2(470, 120));
            }
            if (AdReady(AdReward.DoubleRun))
            {
                var d = Ui.Button("Double", p, AdLabel("DOUBLE THE STEEL"), Ui.Gold, () => WatchAd(AdReward.DoubleRun, () => { Sfx.Play("coin", 0.9f); Haptics.Success(); }), 32);
                ((RectTransform)d.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-250, 44), new Vector2(470, 120));
            }
            var c = Ui.Button("Collect", p, "COLLECT", Ui.Green, () =>
            {
                int mile = S.Run.mile;
                if (S.CloseRun()) { Sfx.Play("claim", 0.9f); Haptics.Success(); GameRoot.Instance.Save(true); if (mile >= 20) RateSoon(); AfterRun(); }
            }, 44);
            ((RectTransform)c.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(AdReady(AdReward.DoubleRun) ? 250 : 0, 44), new Vector2(470, 120));
            _collectLabel = c.GetComponentInChildren<Text>(); _overAuto = AutoCollectAfter;
        }
    }

    /// <summary>A button that keeps buying while it is held down.</summary>
    public sealed class HoldRepeat : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public Action Tap;
        bool _down; float _next;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { _down = true; _next = Time.unscaledTime + 0.4f; }
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { _down = false; }
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) { _down = false; }
        void Update() { if (_down && Time.unscaledTime >= _next) { _next = Time.unscaledTime + 0.09f; if (Tap != null) Tap(); } }
    }
}
