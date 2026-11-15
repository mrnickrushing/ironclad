using System.Collections.Generic;
using Ironclad.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironclad.Game
{
    /// <summary>Words that rise out of the fight (crits, scrip, steel) and the line of news at the top of the screen.</summary>
    public sealed partial class Hud
    {
        sealed class Popped { public RectTransform Rt; public Text T; public Vector3 World; public float Age, Life; }
        readonly List<Popped> _pops = new List<Popped>();

        /// <summary>A word rising from a place in the world.</summary>
        public void Pop(Vector3 world, string text, Color color, int size = 34, float life = 1.1f)
        {
            if (_pops.Count >= 36) return;
            var l = Ui.Label("Pop", _fxLayer, text, size, color); l.Stroke(2f);
            l.rectTransform.sizeDelta = new Vector2(360, 60);
            _pops.Add(new Popped { Rt = l.rectTransform, T = l, World = world, Life = life });
        }

        void UpdatePops()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _pops.Count - 1; i >= 0; i--)
            {
                var p = _pops[i];
                p.Age += dt;
                if (p.Age >= p.Life || p.Rt == null) { if (p.Rt != null) Destroy(p.Rt.gameObject); _pops.RemoveAt(i); continue; }
                Vector2 at;
                bool on = Project(p.World + Vector3.up * p.Age * 3.5f, out at);
                p.Rt.gameObject.SetActive(on);
                p.Rt.anchoredPosition = at;
                float k = p.Age / p.Life;
                p.Rt.localScale = Vector3.one * (k < 0.15f ? Mathf.Lerp(0.5f, 1.15f, k / 0.15f) : 1);
                var c = p.T.color; c.a = k > 0.6f ? 1 - (k - 0.6f) / 0.4f : 1; p.T.color = c;
            }
        }

        Text _marker;

        /// <summary>A sign held over a place in the world (the BEST post), or none (null).</summary>
        public void Marker(Vector3? world, string text)
        {
            if (world == null || text == null) { if (_marker != null && _marker.gameObject.activeSelf) _marker.gameObject.SetActive(false); return; }
            if (_marker == null)
            {
                _marker = Ui.Label("Marker", _fxLayer, "", 34, new Color(1f, 0.86f, 0.4f), TextAnchor.LowerCenter, false); _marker.Stroke(2.5f);
                _marker.rectTransform.sizeDelta = new Vector2(320, 100); _marker.rectTransform.pivot = new Vector2(0.5f, 0);
            }
            Vector2 at;
            bool on = Project(world.Value, out at);
            if (_marker.gameObject.activeSelf != on) _marker.gameObject.SetActive(on);
            _marker.rectTransform.anchoredPosition = at;
            if (_marker.text != text) _marker.text = text;
        }

        RectTransform _toast; float _toastAge;
        string _toastHeld, _toastText;

        public void Toast(string text)
        {
            if (text == null) return;
            if (ModalOpen) { _toastHeld = text; return; }      // a card is up: say it when the card has gone
            if (_toast != null) Destroy(_toast.gameObject);
            var img = Ui.ArtImg("Toast", _root, "plate", Ui.Ink, 36);
            img.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(Mathf.Min(1200, 130 + text.Length * 19), 76));
            var l = Ui.Label("T", img.transform, text, 32, Color.white, TextAnchor.MiddleCenter, false); l.rectTransform.Fill(8); l.rectTransform.offsetMin = new Vector2(8, 12);
            img.gameObject.AddComponent<CanvasGroup>().alpha = 0;
            _toast = img.rectTransform; _toastAge = 0; _toastText = text;
        }

        void UpdateToast()
        {
            if (_toast == null) { if (_toastHeld != null && !ModalOpen) { var t = _toastHeld; _toastHeld = null; Toast(t); } return; }
            _toastAge += Time.unscaledDeltaTime;
            var g = _toast.GetComponent<CanvasGroup>();
            g.alpha = _toastAge < 0.2f ? _toastAge / 0.2f : _toastAge > 2.6f ? 1 - (_toastAge - 2.6f) / 0.4f : 1;
            if (_toastAge > 3f) { Destroy(_toast.gameObject); _toast = null; }
        }

        static readonly Color[] RarityColors = { new Color(0.55f, 0.6f, 0.66f), new Color(0.2f, 0.56f, 0.9f), new Color(0.72f, 0.36f, 0.95f) };
        static readonly string[] RarityNames = { "COMMON", "RARE", "EPIC" };
    }
}
