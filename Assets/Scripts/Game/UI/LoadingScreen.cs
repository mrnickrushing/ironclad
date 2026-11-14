using UnityEngine;
using UnityEngine.UI;

namespace Ironclad.Game
{
    /// <summary>
    /// What covers the launch while the harbour is built behind it: the game's key picture, its name, a progress bar
    /// with an aircraft riding it, a tip, and the Rushing Technologies logo (every game's loading screen carries it).
    /// GameRoot moves <see cref="Progress"/> as it builds; the screen fades once it is told it's done and has been
    /// up long enough to be read.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }
        public float Progress;
        public bool Done;

        static readonly string[] Tips =
        {
            "Raiders from behind reach the last wagon first. Put something tough there.",
            "A Supply Car makes the wagons either side of it fire faster.",
            "Scrip only lasts the run. Spend it.",
            "The whistle throws back everything near the train.",
            "Tap a raider to point every gun at it.",
            "Every tenth mile there is a station, and a card to keep for the run.",
            "The depot's patrols bring steel home while you're away.",
        };

        CanvasGroup _group;
        RectTransform _fill, _plane, _rays;
        Text _percent;
        float _shown, _age, _fade = 1;

        public static LoadingScreen Show()
        {
            var go = new GameObject("Loading");
            var ls = go.AddComponent<LoadingScreen>();
            Instance = ls;
            ls.Build();
            return ls;
        }

        void Build()
        {
            var root = Ui.Canvas("Loading canvas", transform);
            root.GetComponent<Canvas>().sortingOrder = 100;
            _group = root.gameObject.AddComponent<CanvasGroup>();
            // A frontier sunset: burnt orange shading to dark earth at the bottom, with soft rays behind the name.
            var back = Ui.Img("Back", root, new Color(0.78f, 0.42f, 0.22f));
            back.rectTransform.Fill(); back.raycastTarget = true;
            var hero = Resources.Load<Sprite>("UI/Loading/hero");
            if (hero != null)
            {
                var art = Ui.Img("Hero", root, Color.white); art.sprite = hero;
                art.rectTransform.Fill();
                var fit = art.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; fit.aspectRatio = hero.rect.width / hero.rect.height;
            }
            else
                for (int i = 0; i < 8; i++)
                {
                    var band = Ui.Img("Deep", root, new Color(0.3f, 0.1f, 0.06f, 0.16f)).rectTransform;
                    band.Band(false, 0, 620 * (8 - i) / 8f);
                }

            _rays = Ui.ArtImg("Rays", root, "rays", new Color(1f, 1f, 1f, hero != null ? 0f : 0.22f), 0).rectTransform;
            _rays.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(1500, 1500));
            var title = Ui.Label("Title", root, "IRONCLAD", 180, Color.white);
            title.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1400, 210));
            title.Stroke(7f);
            var ribbon = Ui.Ribbon("Ribbon", root, Ui.Coral);
            ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -360), new Vector2(520, 118));
            var sub = Ui.Label("Sub", ribbon.transform, "RAIL DEFENCE", 54, Color.white);
            sub.rectTransform.Fill(); sub.rectTransform.offsetMin = new Vector2(0, 24); sub.Stroke(2.5f);

            // The bar: a paper track, a green fill, a coin riding its leading edge.
            var bar = Ui.ArtImg("Bar", root, "pill", Color.white, 62, 2f).rectTransform;
            bar.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 330), new Vector2(880, 78));
            var track = Ui.Rect("Track", bar); track.Fill(); track.offsetMin = new Vector2(16, 20); track.offsetMax = new Vector2(-16, -14);
            _fill = Ui.Img("Fill", track, Ui.Green, true).rectTransform;
            _fill.anchorMin = Vector2.zero; _fill.anchorMax = new Vector2(0.02f, 1); _fill.offsetMin = _fill.offsetMax = Vector2.zero;
            var planeImg = Ui.Img("Coin", track, Color.white); planeImg.sprite = Ui.Art("coin"); planeImg.preserveAspect = true;
            _plane = planeImg.rectTransform;
            _plane.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(96, 96));
            _percent = Ui.Label("Percent", root, "", 42, Color.white);
            _percent.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 420), new Vector2(800, 54));
            _percent.Stroke(2.5f);

            var tipPlate = Ui.ArtImg("TipPlate", root, "plate", Ui.Ink, 36);
            tipPlate.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 232), new Vector2(1180, 76));
            var tip = Ui.Label("Tip", tipPlate.transform, Tips[Random.Range(0, Tips.Length)], 32, Color.white, TextAnchor.MiddleCenter, false);
            tip.rectTransform.Fill(8); tip.rectTransform.offsetMin = new Vector2(8, 12);

            // The studio's mark on its own dark plate (it is drawn for a dark ground).
            var plate = Ui.Img("Studio", root, new Color(0.05f, 0.09f, 0.17f, 1f), true).rectTransform;
            plate.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(300, 176));
            var logo = Resources.Load<Sprite>("UI/Brand/rushingtech");
            if (logo != null)
            {
                var li = Ui.Img("Logo", plate, Color.white); li.sprite = logo; li.preserveAspect = true;
                li.rectTransform.Fill(8);
            }
            else Ui.Label("Name", plate, "RUSHING\nTECHNOLOGIES", 34, Color.white).rectTransform.Fill();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _age += dt;
            // The bar never jumps and never sits still: it eases toward the real progress, creeping a little meanwhile.
            float goal = Done ? 1 : Mathf.Min(0.94f, Mathf.Max(Progress, _shown + dt * 0.03f));
            _shown = Mathf.MoveTowards(_shown, goal, dt * (Done ? 1.6f : 0.7f));
            _fill.anchorMax = new Vector2(Mathf.Max(0.02f, _shown), 1);
            _plane.anchorMin = _plane.anchorMax = new Vector2(Mathf.Max(0.02f, _shown), 0.5f);
            _plane.anchoredPosition = new Vector2(0, Mathf.Sin(_age * 6) * 2);
            _percent.text = _shown >= 0.999f ? "ALL ABOARD!" : "Raising steam  " + Mathf.RoundToInt(_shown * 100) + "%";
            _rays.localRotation = Quaternion.Euler(0, 0, _age * 10);
            if (Done && _shown >= 0.999f && _age > 2.2f)
            {
                _fade -= dt * 2.2f;
                _group.alpha = Mathf.Clamp01(_fade);
                if (_fade <= 0) { if (Instance == this) Instance = null; Destroy(gameObject); }
            }
        }
    }
}
