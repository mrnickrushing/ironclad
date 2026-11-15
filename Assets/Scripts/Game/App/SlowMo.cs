using UnityEngine;

namespace Ironclad.Game
{
    /// <summary>
    /// A moment of slow motion for the big beats (the Baron's carriage falling, the whistle): the whole fight, its
    /// rules and its pictures slow to <c>scale</c> and ease back over <c>seconds</c> of real time. Menus, the camera and
    /// the music run on unscaled time, so only the fight is slowed. GameRoot ticks it every frame.
    /// </summary>
    public static class SlowMo
    {
        static float _scale = 1, _left, _length;

        public static void Hit(float seconds, float scale)
        {
            if (seconds <= 0) return;
            // A stronger or longer moment wins over one already running.
            if (_left > 0 && scale >= _scale && seconds <= _left) return;
            _scale = Mathf.Clamp(scale, 0.05f, 1f); _left = _length = seconds;
        }

        /// <summary>Back to full speed at once (a card, the end of the run, the app going to the background).</summary>
        public static void Clear() { _left = 0; Time.timeScale = 1; }

        public static void Tick(float unscaledDt, bool allowed)
        {
            if (!allowed || _left <= 0) { if (Time.timeScale != 1) Time.timeScale = 1; _left = 0; return; }
            _left -= unscaledDt;
            // Held for the first half, then eased back to full speed.
            float k = Mathf.Clamp01(_left / Mathf.Max(0.01f, _length * 0.5f));
            Time.timeScale = Mathf.Lerp(1f, _scale, k * k * (3 - 2 * k));
        }
    }
}
