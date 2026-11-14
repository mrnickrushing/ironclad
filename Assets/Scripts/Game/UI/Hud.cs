using System;
using System.Collections.Generic;
using Ironclad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironclad.Game
{
    /// <summary>
    /// The interface. It has four faces, picked by where the game stands: the depot (between runs), the run, a station,
    /// and the end of a run. Screens (workshop, train, awards, shop, settings) open over the depot.
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        public static Hud Instance { get; private set; }
        static Sim S { get { return GameRoot.Sim; } }

        RectTransform _root, _fxLayer, _face, _screenRoot, _modal;
        bool _dirty = true;
        enum Face { None, Depot, Run, Station, Over }
        Face _shown = Face.None;
        Sim _sim;

        public static Hud Create()
        {
            var go = new GameObject("Hud");
            var hud = go.AddComponent<Hud>();
            Instance = hud;
            var canvas = Ui.Canvas("Canvas", go.transform);
            hud.Build(canvas);
            return hud;
        }

        void Build(RectTransform canvas)
        {
            var pad = Ui.Img("Pad", canvas, new Color(0, 0, 0, 0)); pad.raycastTarget = true; pad.rectTransform.Fill();
            pad.gameObject.AddComponent<DragPad>();
            _root = Ui.Rect("Safe", canvas); _root.Fill(); _root.gameObject.AddComponent<SafeArea>();
            _fxLayer = Ui.Rect("Fx", _root); _fxLayer.Fill();
            _face = Ui.Rect("Face", _root); _face.Fill();
            _handLayer = Ui.Rect("Hand", _root); _handLayer.Fill();
            _screenRoot = Ui.Rect("Screen", _root); _screenRoot.Fill();
            _modal = Ui.Rect("Modal", _root); _modal.Fill();
            _sim = S;
            _sim.Changed += OnChanged; _sim.Plans += OnPlans; _sim.FirstStation += OnFirstStation;
            if (GameRoot.Instance.PendingCloud != null) OfferCloud();
        }

        void OnDestroy()
        {
            if (_sim != null) { _sim.Changed -= OnChanged; _sim.Plans -= OnPlans; _sim.FirstStation -= OnFirstStation; }
            if (Instance == this) Instance = null;
        }

        void OnChanged() { _dirty = true; }

        void OnFirstStation(int mile, int gems) { Toast("First time at the mile " + mile + " station: +" + gems + " gems"); Sfx.Play("claim", 0.7f); }

        void OnPlans(CarDef d) { Toast("Plans found: the " + d.Name + ". Build it in TRAIN after this run"); Sfx.Play("star", 0.8f); Haptics.Success(); }

        Face Wanted
        {
            get
            {
                var r = S.Run;
                if (r == null) return Face.Depot;
                return r.phase == (int)Phase.Station ? Face.Station : r.phase == (int)Phase.Over ? Face.Over : Face.Run;
            }
        }

        void Update()
        {
            if (S == null || S != _sim) return;
            var want = Wanted;
            if (want != _shown || _dirty)
            {
                bool changed = want != _shown;
                _shown = want; _dirty = false;
                if (changed && _screen != Page.None && want != Face.Depot) CloseScreen();
                if (changed && ModalOpen) CloseCard();      // a card belongs to the face it was opened over
                Ui.Clear(_face);
                _chips.Clear(); _departButton = null; _workshopButton = null; _whistle = null;
                switch (want)
                {
                    case Face.Depot: BuildDepot(); break;
                    case Face.Run: BuildRun(); break;
                    case Face.Station: BuildRun(); BuildStation(); break;
                    case Face.Over: BuildOver(); break;
                }
                if (_screen != Page.None) RefreshScreen();
            }
            if (_shown == Face.Run || _shown == Face.Station) UpdateRun();
            if (_shown == Face.Depot) UpdateDepot();
            UpdateHand();
            UpdatePops();
            UpdateToast();
            UpdateRate();
        }

        public bool ModalOpen { get { return _modal != null && _modal.childCount > 0; } }
        bool ScreenOpen { get { return _screen != Page.None; } }
        bool SheetOpen { get { return false; } }

        /// <summary>A tap on the open country: point out the raider under the finger.</summary>
        public void TapWorld(Vector2 screen)
        {
            if (!S.Running || TrainView.Instance == null) return;
            int id = TrainView.Instance.Pick(screen);
            if (id != 0 && S.Mark(id)) { Sfx.Play("click", 0.5f, 1.3f); Haptics.Light(); }
        }

        bool Project(Vector3 world, out Vector2 canvasPos)
        {
            canvasPos = Vector2.zero;
            var cam = CameraRig.Instance != null ? CameraRig.Instance.Cam : null;
            if (cam == null) return false;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_fxLayer, sp, null, out canvasPos);
        }

        // ---- the first steps: a welcome, then a hand that points at the next thing to press ----

        RectTransform _handLayer, _hand, _departButton, _workshopButton;

        void Welcome()
        {
            var card = Card(980, 600, "ALL ABOARD");
            Ui.Label("B", card, "The Baron's raiders hold every mile of this line.\n\nYour train fights by itself. Take it as far down the track as you can: every mile pays <b>steel</b>, and steel builds a stronger train for the next run.", 34, Ui.Ink, TextAnchor.UpperCenter, false)
                .rectTransform.Band(true, -100, 300, 60);
            var go = Ui.Button("Go", card, "LET'S ROLL", Ui.Green, () => { S.State.tutorialStep = 1; CloseCard(); GameRoot.Instance.Save(); }, 46);
            ((RectTransform)go.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 44), new Vector2(460, 124));
        }

        /// <summary>What the hand should point at right now (null: nothing).</summary>
        RectTransform HandTarget()
        {
            var s = S; var r = s.Run;
            if (ModalOpen || ScreenOpen || s.State.tutorialStep >= 2) return null;
            if (_shown == Face.Depot)
            {
                if (s.State.runs == 0) return _departButton;
                if (s.State.upgrades.Count == 0 && s.State.steel >= s.UpgradeCost("hull")) return _workshopButton;
                if (s.State.upgrades.Count > 0 && s.State.runs >= 2) { s.State.tutorialStep = 2; return null; }
                return s.State.runs <= 2 ? _departButton : null;
            }
            if (_shown == Face.Run && r != null && s.State.runs == 0)
            {
                bool bought = false; foreach (var c in r.cars) if (c.field > 0) bought = true;
                if (!bought)
                {
                    Chip best = null;
                    foreach (var c in _chips) if (c.Car < r.cars.Count && r.scrip >= s.FieldCost(c.Car) && (best == null || c.Car > best.Car)) best = c;
                    if (best != null) return (RectTransform)best.Buy.transform;
                }
                if (s.State.whistles == 0 && s.WhistleReady && r.enemies.Count >= 6 && _whistle != null) return (RectTransform)_whistle.transform;
            }
            return null;
        }

        void UpdateHand()
        {
            if (_shown == Face.Depot && S.State.tutorialStep == 0 && !ModalOpen && S.State.runs == 0) Welcome();
            var target = HandTarget();
            if (target == null) { if (_hand != null) _hand.gameObject.SetActive(false); return; }
            if (_hand == null)
            {
                var img = Ui.ArtImg("Hand", _handLayer, "hand", Color.white, 0);
                _hand = img.rectTransform; _hand.sizeDelta = new Vector2(120, 120); _hand.pivot = new Vector2(0.3f, 1f);
            }
            _hand.gameObject.SetActive(true);
            var corners = new Vector3[4]; target.GetWorldCorners(corners);
            var mid = (corners[0] + corners[2]) * 0.5f;
            _hand.position = mid + new Vector3(0, (-10 + Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5)) * -22) * _hand.lossyScale.y, 0);
        }

        // ---- shared pieces ----

        /// <summary>A pill with a picture and a number: steel, scrip, gems.</summary>
        Text Pill(Transform parent, Vector2 anchor, Vector2 pos, float width, Sprite icon, Color iconTint, Action tap = null)
        {
            var img = Ui.ArtImg("Pill", parent, "pill", Color.white, 62, 2f);
            img.rectTransform.Place(anchor, new Vector2(anchor.x, anchor.y), pos, new Vector2(width, 78));
            if (icon != null) { var ic = Ui.Img("I", img.transform, iconTint); ic.sprite = icon; ic.preserveAspect = true; ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 4), new Vector2(60, 60)); }
            var t = Ui.Label("T", img.transform, "", 38, Ui.Ink, TextAnchor.MiddleRight); t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(76, 10); t.rectTransform.offsetMax = new Vector2(-24, -2);
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 22; t.resizeTextMaxSize = 38;
            if (tap != null) { img.raycastTarget = true; img.gameObject.AddComponent<Button>().onClick.AddListener(() => tap()); }
            return t;
        }

        /// <summary>A bar: a dark well with a coloured fill. Returns the fill (set its anchorMax.x).</summary>
        RectTransform Bar(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color colour, out Image fill)
        {
            var well = Ui.Img("Bar", parent, new Color(0.18f, 0.1f, 0.07f, 0.92f), true); well.rectTransform.Place(anchor, pivot, pos, size);
            fill = Ui.Img("Fill", well.transform, colour, true);
            var f = fill.rectTransform; f.anchorMin = Vector2.zero; f.anchorMax = Vector2.one; f.offsetMin = new Vector2(4, 4); f.offsetMax = new Vector2(-4, -4);
            return f;
        }

        // ---- cards over everything ----

        /// <summary>A card in the middle of the screen over a dimmed game. Returns the card to fill.</summary>
        RectTransform Card(float width, float height, string title)
        {
            if (_toast != null) { if (_toastAge < 1.6f) _toastHeld = _toastText; Destroy(_toast.gameObject); _toast = null; }
            Ui.Clear(_modal);
            var dim = Ui.Img("Dim", _modal, new Color(0.14f, 0.07f, 0.04f, 0.62f)); dim.raycastTarget = true; dim.rectTransform.Fill();
            var card = Ui.ArtImg("Card", _modal, "panel", Color.white, 60);
            card.raycastTarget = true;
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            card.gameObject.AddComponent<PopIn>();
            var ribbon = Ui.Ribbon("Ribbon", card.transform, Ui.Coral);
            ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -8), new Vector2(Mathf.Min(width + 40, 700), 116));
            var t = Ui.Label("Title", ribbon.transform, title, 50, Color.white); t.rectTransform.Fill(); t.rectTransform.offsetMin = new Vector2(40, 24); t.rectTransform.offsetMax = new Vector2(-40, 0); t.Stroke(2.5f);
            Sfx.Play("open", 0.5f);
            return card.rectTransform;
        }

        void CloseCard() { Ui.Clear(_modal); }

        /// <summary>A card with two buttons asking for a yes or a no.</summary>
        void Confirm(string title, string body, string yes, Color yesColour, Action onYes, string no = "CANCEL")
        {
            var card = Card(880, 500, title);
            var b = Ui.Label("B", card, body, 32, Ui.Ink, TextAnchor.UpperCenter, false); b.rectTransform.Band(true, -96, 220, 50);
            var y = Ui.Button("Yes", card, yes, yesColour, () => { CloseCard(); onYes(); }, 40); ((RectTransform)y.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(205, 44), new Vector2(380, 110));
            var n = Ui.Button("No", card, no, Ui.Off, CloseCard, 40); ((RectTransform)n.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-205, 44), new Vector2(380, 110));
        }

        // ---- adverts: always the player's choice ----

        /// <summary>Whether this reward can be had for an advert right now (or at once, with the Conductor's Pass).</summary>
        bool AdReady(AdReward reward) { return S.AdOffered(reward) && (S.SkipsAdverts || Ads.Ready); }

        void Advert(AdReward reward, Action done)
        {
            if (S.SkipsAdverts) { if (S.GrantAd(reward)) { GameRoot.Instance.Save(); done(); } return; }
            Ads.Show(ok =>
            {
                if (!ok) { Toast("The advert wasn't finished, so nothing was given."); return; }
                if (S.GrantAd(reward)) { GameRoot.Instance.Save(); done(); }
            });
        }

        string AdLabel(string what) { return S.SkipsAdverts ? what : what + "\n<size=24>watch an advert</size>"; }

        // ---- the welcome back ----

        /// <summary>Back from the background: if the depot has steel waiting and no run is on, show it.</summary>
        public void OfferAway()
        {
            GameRoot.Instance.ClearAway();
            if (S.Run == null && S.DepotSteel >= 1 && S.DepotSeconds >= 300 && !ModalOpen) ShowDepotCard();
        }

        RectTransform _cloud;

        /// <summary>iCloud holds a newer save from another device: ask which one to keep (never replaced unasked).</summary>
        public void OfferCloud()
        {
            var cloud = GameRoot.Instance.PendingCloud; var meta = GameRoot.Instance.PendingMeta;
            if (cloud == null || _cloud != null) return;
            _cloud = Ui.Img("Cloud", _root, new Color(0.14f, 0.07f, 0.04f, 0.7f)).rectTransform;
            _cloud.Fill(); _cloud.GetComponent<Image>().raycastTarget = true;
            var box = Ui.ArtImg("Box", _cloud, "panel", Color.white, 60).rectTransform;
            box.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940, 640));
            Ui.Label("T", box, "CONTINUE FROM YOUR OTHER DEVICE?", 48, Ui.Ink).rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(860, 80));
            int theirs = meta != null ? meta.Stars : new Sim(cloud).Stars;
            Ui.Label("B", box, "iCloud has a newer save from another device: <b>" + theirs + " miles</b> across its lines, saved " + Format.Time(Math.Max(0, SaveSystem.Now - cloud.savedAt)) + " ago.\n\n"
                + "This device has <b>" + S.Stars + " miles</b>.\n\nThe one you don't pick is replaced.", 34, Ui.Ink, TextAnchor.UpperCenter, false)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(820, 330));
            var yes = Ui.Button("Yes", box, "USE ICLOUD", Ui.Blue, () => { Destroy(_cloud.gameObject); _cloud = null; GameRoot.Instance.ResolveCloud(true); }, 42);
            ((RectTransform)yes.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(215, 46), new Vector2(400, 116));
            var no = Ui.Button("No", box, "KEEP THIS ONE", Ui.Off, () => { Destroy(_cloud.gameObject); _cloud = null; GameRoot.Instance.ResolveCloud(false); }, 42);
            ((RectTransform)no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-215, 46), new Vector2(400, 116));
        }
    }
}
