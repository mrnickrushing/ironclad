using System.Collections.Generic;
using UnityEngine;

namespace Ironclad.Game
{
    /// <summary>
    /// Sound: interface clips from Resources/Audio (takes named name, name_2, name_3 are picked at random), the music,
    /// and a quiet bed of wind made in code.
    /// </summary>
    public static class Sfx
    {
        static readonly Dictionary<string, AudioClip[]> _clips = new Dictionary<string, AudioClip[]>();
        static AudioSource[] _pool;
        static AudioSource _bed;
        static int _next;
        static AudioSource _day, _night;
        const float MusicLevel = 0.32f;

        /// <summary>Music on or off (its own switch: plenty of people want the sounds without it).</summary>
        public static bool Music
        {
            get { return PlayerPrefs.GetInt("re_music", 1) == 1; }
            set { PlayerPrefs.SetInt("re_music", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// Keeps the music with the clock: the day loop while the sun is up, the quieter night one after dark, a slow
        /// cross-fade between them, and both down while the title screen or a card wants quiet (<paramref name="duck"/>).
        /// </summary>
        public static void TickMusic(float night, float dt, float duck = 1f)
        {
            if (_day == null) return;
            float on = Music ? MusicLevel * duck : 0;
            float wantDay = on * (1 - night), wantNight = on * night * 1.1f;
            _day.volume = Mathf.MoveTowards(_day.volume, wantDay, dt * 0.12f);
            _night.volume = Mathf.MoveTowards(_night.volume, wantNight, dt * 0.12f);
            // A silent track is stopped (nothing decoding for nothing) and starts again from the top when wanted.
            foreach (var src in new[] { _day, _night })
            {
                bool want = src == _day ? wantDay > 0 : wantNight > 0;
                if (src.clip == null) continue;
                if (want && !src.isPlaying) src.Play();
                else if (!want && src.volume <= 0.0001f && src.isPlaying) src.Stop();
            }
        }

        /// <summary>The harbour's bed of sound: wind and water, a little quieter at night.</summary>
        public static void TickAmbience(float night, float dt)
        {
            if (_bed == null) return;
            _bed.volume = Mathf.MoveTowards(_bed.volume, Mathf.Lerp(0.06f, 0.035f, night), dt * 0.06f);
        }

        static AudioSource MusicSource(Transform parent, string name)
        {
            var s = new GameObject("Music " + name).AddComponent<AudioSource>();
            s.transform.SetParent(parent, false);
            s.clip = Resources.Load<AudioClip>("Audio/Music/" + name);
            s.loop = true; s.volume = 0; s.spatialBlend = 0; s.playOnAwake = false; s.priority = 0;
            return s;
        }

        public static bool Muted
        {
            get { return PlayerPrefs.GetInt("re_muted", 0) == 1; }
            set { PlayerPrefs.SetInt("re_muted", value ? 1 : 0); PlayerPrefs.Save(); AudioListener.volume = value ? 0 : 1; }
        }

        public static void Init(Transform root)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(root, false);
            _pool = new AudioSource[10];
            for (int i = 0; i < _pool.Length; i++)
            {
                _pool[i] = new GameObject("Voice").AddComponent<AudioSource>();
                _pool[i].transform.SetParent(go.transform, false);
                _pool[i].playOnAwake = false; _pool[i].spatialBlend = 0;
            }
            _bed = new GameObject("Bed").AddComponent<AudioSource>();
            _bed.transform.SetParent(go.transform, false);
            _bed.clip = Roar("Wind", 6f, 260f, 0f, loop: true); _bed.loop = true; _bed.volume = 0.05f; _bed.spatialBlend = 0;
            _bed.Play();
            _day = MusicSource(go.transform, "day"); _night = MusicSource(go.transform, "night");
            AudioListener.volume = Muted ? 0 : 1;
        }

        /// <summary>Filtered noise swelling and fading (a pass-by), with an optional drone under it (propellers).</summary>
        static AudioClip Roar(string name, float seconds, float cutoff, float droneHz, bool loop = false)
        {
            const int rate = 22050;
            int n = (int)(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float lp = 0, a = Mathf.Clamp01(2 * Mathf.PI * cutoff / rate);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                lp += a * ((float)rng.NextDouble() * 2 - 1 - lp);
                float env = loop ? 0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 2) : Mathf.Pow(Mathf.Sin(t * Mathf.PI), 1.6f);
                float drone = droneHz > 0 ? Mathf.Sin(i * 2 * Mathf.PI * droneHz / rate) * 0.25f + Mathf.Sin(i * 2 * Mathf.PI * droneHz * 2.01f / rate) * 0.12f : 0;
                data[i] = (lp * 2.2f + drone) * env;
            }
            if (loop) for (int i = 0; i < 2000; i++) { float k = i / 2000f; data[i] = data[i] * k + data[n - 2000 + i] * (1 - k); }   // no click at the seam
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// The sounds of the fight, made here rather than recorded: bursts of filtered noise for shots and blasts, a
        /// ring for struck plate, a chord with breath in it for the whistle.
        /// </summary>
        static AudioClip Synth(string name)
        {
            const int rate = 22050;
            float seconds, cutoff, decay, tone = 0, toneLevel = 0, drop = 0;
            switch (name)
            {
                case "shot": seconds = 0.12f; cutoff = 5200; decay = 34; tone = 190; toneLevel = 0.35f; drop = 0.5f; break;
                case "rifle": seconds = 0.38f; cutoff = 3600; decay = 13; tone = 130; toneLevel = 0.5f; drop = 0.6f; break;
                case "cannon": seconds = 0.7f; cutoff = 1400; decay = 6.5f; tone = 72; toneLevel = 0.9f; drop = 0.55f; break;
                case "boom": seconds = 1.1f; cutoff = 900; decay = 4.2f; tone = 52; toneLevel = 1f; drop = 0.5f; break;
                case "fall": seconds = 0.2f; cutoff = 1500; decay = 18; tone = 150; toneLevel = 0.5f; drop = 0.7f; break;
                case "clang": seconds = 0.35f; cutoff = 6000; decay = 12; tone = 880; toneLevel = 0.6f; break;
                case "zap": seconds = 0.2f; cutoff = 8000; decay = 16; tone = 1400; toneLevel = 0.5f; drop = 0.8f; break;
                case "flame": seconds = 0.4f; cutoff = 1100; decay = 5; break;
                case "whistle": return WhistleClip();
                default: return null;
            }
            int n = (int)(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float lp = 0, a = Mathf.Clamp01(2 * Mathf.PI * cutoff / rate), phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate, k = i / (float)n;
                lp += a * ((float)rng.NextDouble() * 2 - 1 - lp);
                float env = Mathf.Exp(-decay * t) * Mathf.Min(1, i / 40f) * (1 - k * k * k);
                phase += 2 * Mathf.PI * tone * (1 - drop * k) / rate;
                float ring = name == "clang" ? Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 2.76f) * 0.3f + Mathf.Sin(phase * 5.4f) * 0.2f : Mathf.Sin(phase);
                data[i] = Mathf.Clamp((lp * 1.8f + ring * toneLevel) * env, -1, 1);
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip WhistleClip()
        {
            const int rate = 22050; const float seconds = 1.5f;
            int n = (int)(seconds * rate);
            var data = new float[n];
            var rng = new System.Random(7);
            float lp = 0;
            float[] notes = { 392f, 494f, 587f };      // a steam whistle's three bells
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                lp += 0.35f * ((float)rng.NextDouble() * 2 - 1 - lp);
                float env = Mathf.Min(1, t / 0.08f) * Mathf.Min(1, (seconds - t) / 0.35f);
                float bend = 1 - 0.03f * Mathf.Exp(-t * 9);
                float v = 0;
                foreach (float f in notes) v += Mathf.Sin(2 * Mathf.PI * f * bend * t) * 0.2f + Mathf.Sin(2 * Mathf.PI * f * 2 * bend * t) * 0.06f;
                data[i] = (v + lp * 0.35f) * env * 0.8f;
            }
            var clip = AudioClip.Create("whistle", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Clip(string name)
        {
            AudioClip[] takes;
            if (!_clips.TryGetValue(name, out takes))
            {
                var list = new List<AudioClip>();
                var a = Resources.Load<AudioClip>("Audio/" + name);
                if (a != null) list.Add(a);
                for (int i = 2; i <= 4; i++) { var b = Resources.Load<AudioClip>("Audio/" + name + "_" + i); if (b != null) list.Add(b); }
                if (list.Count == 0) { var made = Synth(name); if (made != null) list.Add(made); }
                _clips[name] = takes = list.ToArray();
            }
            return takes.Length == 0 ? null : takes[Random.Range(0, takes.Length)];
        }

        public static void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (_pool == null) return;
            var clip = Clip(name);
            if (clip != null) Voice(clip, volume, pitch);
        }

        static void Voice(AudioClip clip, float volume, float pitch)
        {
            var s = _pool[_next]; _next = (_next + 1) % _pool.Length;
            s.clip = clip; s.volume = volume; s.pitch = pitch * Random.Range(0.97f, 1.03f);
            s.Play();
        }
    }
}
