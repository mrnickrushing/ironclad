using System.Collections.Generic;
using UnityEngine;

namespace Ironclad.Game
{
    /// <summary>Everything that flashes, smokes and flies: two particle systems, pooled tracer lines, shells and rings.</summary>
    public static class Fx
    {
        static Transform _root;
        static Material _add, _alpha, _ringMat, _plain;
        static ParticleSystem _smoke, _fire;
        static Texture2D _soft, _ring;

        sealed class Line { public LineRenderer L; public float Life, Max; public Color C; }
        static readonly List<Line> _lines = new List<Line>();
        sealed class Flying { public Transform T; public Vector3 A, B; public float Time, Max, Arc; }
        static readonly List<Flying> _shells = new List<Flying>();
        sealed class Ring { public Transform T; public MeshRenderer R; public float Life, Max, Size; public Color C; public MaterialPropertyBlock P; }
        static readonly List<Ring> _rings = new List<Ring>();
        static Transform _mark; static Mesh _quad;

        public static void Init(Transform root)
        {
            _root = root; _lines.Clear(); _shells.Clear(); _rings.Clear(); _mark = null;
            _soft = Disc(64, false); _ring = Disc(128, true);
            _add = new Material(Resources.Load<Shader>("Shaders/FxAdd")) { mainTexture = _soft };
            _alpha = new Material(Resources.Load<Shader>("Shaders/FxAlpha")) { mainTexture = _soft };
            _ringMat = new Material(Resources.Load<Shader>("Shaders/FxAdd")) { mainTexture = _ring };
            _plain = new Material(Resources.Load<Shader>("Shaders/FxAlpha")) { mainTexture = Texture2D.whiteTexture };
            _smoke = Particles("Smoke", _alpha, true);
            _fire = Particles("Fire", _add, false);
        }

        static Texture2D Disc(int n, bool ring)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - n / 2f) * (x + 0.5f - n / 2f) + (y + 0.5f - n / 2f) * (y + 0.5f - n / 2f)) / (n / 2f);
                    float a = ring ? Mathf.Clamp01(1 - Mathf.Abs(d - 0.86f) / 0.12f) + Mathf.Clamp01(1 - d) * 0.18f : Mathf.Pow(Mathf.Clamp01(1 - d), 1.6f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            tex.SetPixels32(px); tex.Apply();
            return tex;
        }

        static ParticleSystem Particles(string name, Material mat, bool grows)
        {
            var go = new GameObject(name); go.transform.SetParent(_root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 900; main.startSpeed = 0; main.gravityModifier = 0;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(grows ? 0.9f : 1, 0), new GradientAlphaKey(grows ? 0.6f : 0.8f, 0.4f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var size = ps.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, grows ? new AnimationCurve(new Keyframe(0, 0.35f), new Keyframe(1, 1.5f)) : new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.25f)));
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Billboard;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 at, Vector3 velocity, Color c, float size, float life)
        {
            if (ps == null) return;
            var p = new ParticleSystem.EmitParams { position = at, velocity = velocity, startColor = c, startSize = size, startLifetime = life, rotation = Random.value * 360 };
            ps.Emit(p, 1);
        }

        public static void Smoke(Vector3 at, Color c, float size, Vector3 velocity) { Emit(_smoke, at, velocity, c, size * 2.2f, Random.Range(0.9f, 1.6f)); }
        public static void Flash(Vector3 at, float size) { Emit(_fire, at, Vector3.zero, new Color(1f, 0.85f, 0.45f, 1f), size * 2.4f, 0.07f); }
        public static void Flame(Vector3 at, Vector3 velocity) { Emit(_fire, at, velocity, new Color(1f, Random.Range(0.4f, 0.75f), 0.15f, 0.9f), Random.Range(1.6f, 2.8f), 0.42f); }
        public static void Spark(Vector3 at)
        {
            for (int i = 0; i < 3; i++) Emit(_fire, at, Random.onUnitSphere * Random.Range(3f, 8f) + Vector3.up * 3, new Color(1f, 0.85f, 0.5f, 1f), 0.5f, Random.Range(0.12f, 0.25f));
        }

        /// <summary>A flash, sparks thrown out, and a ring spreading over the ground.</summary>
        public static void Burst(Vector3 at, float wide, Color c)
        {
            Emit(_fire, at + Vector3.up * 0.6f, Vector3.zero, new Color(1f, 0.9f, 0.6f, 1f), wide * 2.2f, 0.12f);
            int n = Mathf.Clamp((int)(wide * 3), 4, 24);
            for (int i = 0; i < n; i++) Emit(_fire, at + Vector3.up * 0.5f, new Vector3(Random.Range(-1f, 1f), Random.Range(0.4f, 1.4f), Random.Range(-1f, 1f)) * wide * 2.4f, c, Random.Range(0.6f, 1.3f) * Mathf.Sqrt(wide), Random.Range(0.25f, 0.5f));
            RingAt(new Vector3(at.x, 0.12f, at.z), wide * 2.2f, c, 0.4f);
        }

        static Mesh QuadMesh
        {
            get
            {
                if (_quad != null) return _quad;
                _quad = new Mesh { name = "FxQuad" };
                _quad.SetVertices(new List<Vector3> { new Vector3(-0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f), new Vector3(0.5f, -0.5f) });
                _quad.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
                _quad.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
                _quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                return _quad;
            }
        }

        /// <summary>A flat coloured quad (health bars).</summary>
        public static Transform Quad(Transform parent, Color c, bool additive)
        {
            var go = new GameObject("Quad"); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = _plain; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = new MaterialPropertyBlock(); p.SetColor("_Color", c); r.SetPropertyBlock(p);
            return go.transform;
        }

        static void RingAt(Vector3 at, float size, Color c, float life)
        {
            Ring ring = null;
            foreach (var r in _rings) if (r.Life <= 0) { ring = r; break; }
            if (ring == null)
            {
                if (_rings.Count >= 24) return;
                var go = new GameObject("Ring"); go.transform.SetParent(_root, false);
                go.transform.rotation = Quaternion.Euler(90, 0, 0);
                go.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = _ringMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _rings.Add(ring = new Ring { T = go.transform, R = mr, P = new MaterialPropertyBlock() });
            }
            ring.T.gameObject.SetActive(true); ring.T.position = at; ring.Life = ring.Max = life; ring.Size = size; ring.C = c;
        }

        static Line NewLine(int points)
        {
            foreach (var l in _lines) if (l.Life <= 0 && l.L.positionCount == points) return l;
            if (_lines.Count >= 60) return null;
            var go = new GameObject("Line"); go.transform.SetParent(_root, false);
            var lr = go.AddComponent<LineRenderer>(); lr.sharedMaterial = _add; lr.positionCount = points; lr.useWorldSpace = true; lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.textureMode = LineTextureMode.Stretch;
            var line = new Line { L = lr }; _lines.Add(line);
            return line;
        }

        public static void Tracer(Vector3 from, Vector3 to, float width, Color c)
        {
            var l = NewLine(2); if (l == null) return;
            l.L.gameObject.SetActive(true); l.L.SetPosition(0, from); l.L.SetPosition(1, to); l.L.widthMultiplier = width * 2.2f; l.Life = l.Max = 0.08f; l.C = c;
        }

        /// <summary>A jagged arc between two points.</summary>
        public static void Bolt(Vector3 from, Vector3 to)
        {
            var l = NewLine(7); if (l == null) return;
            l.L.gameObject.SetActive(true);
            for (int i = 0; i < 7; i++)
            {
                var p = Vector3.Lerp(from, to, i / 6f);
                if (i > 0 && i < 6) p += Random.insideUnitSphere * 0.9f;
                l.L.SetPosition(i, p);
            }
            l.L.widthMultiplier = 0.45f; l.Life = l.Max = 0.14f; l.C = new Color(0.55f, 0.85f, 1f);
        }

        /// <summary>A shell lobbed from one point to another over this long.</summary>
        public static void Shell(Vector3 from, Vector3 to, float time, float arc)
        {
            Flying f = null;
            foreach (var s in _shells) if (s.Time >= s.Max) { f = s; break; }
            if (f == null)
            {
                if (_shells.Count >= 30) return;
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(_root, false); go.transform.localScale = Vector3.one * 0.55f;
                var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = Materials.World;
                _shells.Add(f = new Flying { T = go.transform });
            }
            f.T.gameObject.SetActive(true); f.A = from; f.B = to; f.Time = 0; f.Max = Mathf.Max(0.05f, time); f.Arc = arc;
        }

        /// <summary>The ring under the raider the player pointed out (null: none).</summary>
        public static void Mark(Vector3? at)
        {
            if (_mark == null)
            {
                var go = new GameObject("Mark"); go.transform.SetParent(_root, false); go.transform.rotation = Quaternion.Euler(90, 0, 0);
                go.AddComponent<MeshFilter>().sharedMesh = QuadMesh;
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = _ringMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var p = new MaterialPropertyBlock(); p.SetColor("_Color", new Color(1f, 0.25f, 0.2f, 1f)); mr.SetPropertyBlock(p);
                _mark = go.transform;
            }
            _mark.gameObject.SetActive(at.HasValue);
            if (at.HasValue) { _mark.position = new Vector3(at.Value.x, 0.15f, at.Value.z); _mark.localScale = Vector3.one * (5f + Mathf.Sin(Time.time * 7) * 0.5f); }
        }

        public static void Tick(float dt)
        {
            foreach (var l in _lines)
            {
                if (l.Life <= 0) continue;
                l.Life -= dt;
                if (l.Life <= 0) { l.L.gameObject.SetActive(false); continue; }
                var c = l.C; c.a = l.Life / l.Max; l.L.startColor = c; l.L.endColor = c;
            }
            foreach (var s in _shells)
            {
                if (s.Time >= s.Max) continue;
                s.Time += dt;
                if (s.Time >= s.Max) { s.T.gameObject.SetActive(false); continue; }
                float k = s.Time / s.Max;
                s.T.position = Vector3.Lerp(s.A, s.B, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * s.Arc;
            }
            foreach (var r in _rings)
            {
                if (r.Life <= 0) continue;
                r.Life -= dt;
                if (r.Life <= 0) { r.T.gameObject.SetActive(false); continue; }
                float k = 1 - r.Life / r.Max;
                r.T.localScale = Vector3.one * r.Size * (0.3f + 0.7f * k);
                var c = r.C; c.a = 1 - k; r.P.SetColor("_Color", c); r.R.SetPropertyBlock(r.P);
            }
        }
    }
}
