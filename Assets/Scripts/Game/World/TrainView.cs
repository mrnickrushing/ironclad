using System.Collections.Generic;
using Ironclad.Core;
using UnityEngine;

namespace Ironclad.Game
{
    /// <summary>
    /// Draws the run: the train standing at the origin while the country scrolls past it, a weapon on every wagon that
    /// turns to its target, the raiders, and every shot, shell and burst the simulation reports.
    /// </summary>
    public sealed class TrainView : MonoBehaviour
    {
        public static TrainView Instance { get; private set; }
        /// <summary>The running speed, for things that animate with the fight.</summary>
        public static float Pace = 1;

        static Sim S { get { return GameRoot.Sim; } }

        sealed class Theme
        {
            public Color Ground, Patch, Bed, Fog;
            public string[] Near, Far;      // small things in the fight, big things beyond it
            public float Hue;               // turns the plants' colours
            public Color Rock = new Color(0.62f, 0.62f, 0.62f);      // the stones are pale: each line paints them its own colour
            public Color Plant = Color.white;                         // grass and bushes, warmed to sit in the line's own ground
            public string Cover; public int CoverCount;               // ground cover drawn instanced: thick grass on the green lines
        }

        static readonly Theme[] Themes =
        {
            new Theme { Ground = Palette.Hex(0x93A857), Patch = Palette.Hex(0xA7AE62), Bed = Palette.Hex(0x8C7B62), Fog = Palette.Hex(0xE3D6B4), Plant = new Color(1.35f, 1.0f, 0.5f), Cover = "grass", CoverCount = 1500, Near = new[] { "grass", "grass", "flowers", "rock_d", "bush" }, Far = new[] { "tree_oak", "tree_a", "rock_a", "bush", "windmill", "fence", "log_stack" } },
            new Theme { Ground = Palette.Hex(0xC9774A), Patch = Palette.Hex(0xB5633C), Bed = Palette.Hex(0x7E5A48), Fog = Palette.Hex(0xE8B98C), Rock = new Color(0.78f, 0.42f, 0.3f), Cover = "rock_b", CoverCount = 1100, Near = new[] { "rock_d", "rock_b", "cactus_b", "stump" }, Far = new[] { "cactus_a", "cactus_a", "rock_c", "rock_e", "rock_c", "cactus_b", "cliff", "cliff" } },
            new Theme { Ground = Palette.Hex(0xE9EEF2), Patch = Palette.Hex(0xD3DEE6), Bed = Palette.Hex(0x7A7F88), Fog = Palette.Hex(0xDCE6EE), Rock = new Color(0.8f, 0.86f, 0.95f), Cover = "rock_b", CoverCount = 700, Near = new[] { "rock_d", "stump", "rock_b" }, Far = new[] { "tree_pine", "tree_pine_b", "tree_pine", "rock_c", "tree_pine_b", "log_stack" } },
            new Theme { Ground = Palette.Hex(0xE6DCC6), Patch = Palette.Hex(0xD6C8AC), Bed = Palette.Hex(0x9A8C78), Fog = Palette.Hex(0xF0E8D8), Rock = new Color(0.86f, 0.8f, 0.68f), Cover = "rock_b", CoverCount = 600, Near = new[] { "rock_d", "rock_b" }, Far = new[] { "rock_c", "rock_a", "cactus_a", "stump" } },
            new Theme { Ground = Palette.Hex(0x6E5A5A), Patch = Palette.Hex(0x5C4A4C), Bed = Palette.Hex(0x4A4044), Fog = Palette.Hex(0x8A7478), Rock = new Color(0.36f, 0.3f, 0.33f), Cover = "rock_b", CoverCount = 1100, Near = new[] { "rock_d", "rock_b", "stump" }, Far = new[] { "rock_c", "rock_e", "rock_c", "rock_e", "rock_a", "cliff", "cliff" } },
        };

        const float Span = 170;         // the country is drawn this far either side of the engine
        const float Cruise = 13;        // metres a second at running speed 1

        Transform _country, _rails, _train, _foes, _fx, _station, _ground; float _groundAt, _cloudAt, _travel;
        readonly List<Transform> _props = new List<Transform>();
        readonly List<Transform> _railBits = new List<Transform>();
        float _railLen = 6, _railAt, _speed, _stationX = float.NaN;
        int _theme = -1;
        string _trainKey;
        Sim _sim;

        sealed class CarView
        {
            public Transform Root, Gun, Body; public Vector3 Muzzle; public float Flash, Hurt, GunYaw; public bool Out; public Renderer[] Rends; public float AimOffset; public Light Lamp;
        }
        readonly List<CarView> _cars = new List<CarView>();

        sealed class FoeView
        {
            public Transform Root, Bar, Fill, Body; public Vector3 At; public float Bob, Seen, Punch, Size = 1, Lunge; public double LastCooldown; public EnemyState E; public float Height; public Strider[] Striders;
        }
        readonly Dictionary<int, FoeView> _foeViews = new Dictionary<int, FoeView>();
        readonly List<int> _gone = new List<int>();

        public static TrainView Create(Transform parent)
        {
            var go = new GameObject("TrainView");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<TrainView>();
            Instance.Build();
            return Instance;
        }

        void Build()
        {
            _country = new GameObject("Country").transform; _country.SetParent(transform, false);
            _rails = new GameObject("Rails").transform; _rails.SetParent(transform, false);
            _train = new GameObject("Train").transform; _train.SetParent(transform, false);
            _foes = new GameObject("Raiders").transform; _foes.SetParent(transform, false);
            _fx = new GameObject("Fx").transform; _fx.SetParent(transform, false);
            Fx.Init(_fx);
            _sim = S;
            _sim.Fired += OnFired; _sim.Fell += OnFell; _sim.Burst += OnBurst; _sim.Struck += OnStruck; _sim.Hit += OnHit; _sim.RunOver += OnRunOver; _sim.MileDone += OnMile;
            BuildRails();
            Sync(true);
        }

        void OnDestroy()
        {
            if (_sim != null) { _sim.Fired -= OnFired; _sim.Fell -= OnFell; _sim.Burst -= OnBurst; _sim.Struck -= OnStruck; _sim.Hit -= OnHit; _sim.RunOver -= OnRunOver; _sim.MileDone -= OnMile; }
            if (Instance == this) Instance = null;
        }

        // ---- the country ----

        void BuildCountry(int theme)
        {
            _theme = theme;
            for (int i = _country.childCount - 1; i >= 0; i--) Destroy(_country.GetChild(i).gameObject);
            if (_ground != null) Destroy(_ground.gameObject);
            _props.Clear();
            var t = Themes[theme];
            // The ground: a wide sheet whose colour wanders smoothly (a colour at every corner, no squares), the same
            // again every Span * 2 so it can roll past for ever. Along the line the grass gives way to a worn verge.
            float period = Span * 2;
            var verge = Color.Lerp(t.Bed, t.Ground, 0.5f);
            var deep = t.Ground * 0.84f; deep.a = 1;
            System.Func<float, float, Color> tint = (x, z) =>
            {
                float u = x / period * Mathf.PI * 2;
                float n = Mathf.Sin(u * 3 + z * 0.045f) * 0.5f + Mathf.Sin(u * 7 - z * 0.11f + 1.3f) * 0.3f + Mathf.Sin(u * 13 + z * 0.23f + 4f) * 0.2f;
                float big = Mathf.Sin(u * 2 - z * 0.021f + 2.1f) * 0.6f + Mathf.Sin(u * 5 + z * 0.037f + 0.4f) * 0.4f;
                var c = Color.Lerp(t.Ground, t.Patch, Mathf.Clamp01(0.5f + n * 0.75f));
                c = Color.Lerp(c, deep, Mathf.Clamp01(big * 0.9f) * 0.8f);
                float worn = Mathf.InverseLerp(6.5f, 3.4f, Mathf.Abs(z)) * (0.75f + 0.25f * Mathf.Sin(u * 23 + z * 0.9f));
                return Color.Lerp(c, verge, Mathf.Clamp01(worn));
            };
            _ground = Models.Built("Ground", transform, mb =>
            {
                mb.Matte();
                const float cell = 5f; int nx = Mathf.CeilToInt(period * 2 / cell), nz = 84;
                for (int ix = 0; ix < nx; ix++)
                    for (int iz = 0; iz < nz; iz++)
                    {
                        float x0 = -period + ix * cell, z0 = -160 + iz * cell, x1 = x0 + cell, z1 = z0 + cell;
                        mb.QuadSmooth(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0),
                            Vector3.up, Vector3.up, Vector3.up, Vector3.up, tint(x0, z0), tint(x0, z1), tint(x1, z1), tint(x1, z0));
                    }
            }).transform;
            _ground.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _groundAt = 0;
            // The ballast: a low bank of stone the sleepers sit on, sloping down to the verge.
            var bed = Models.Built("Bed", _country, mb =>
            {
                mb.Matte();
                var top = t.Bed; var side = t.Bed * 0.82f; side.a = 1;
                const float L = 700, hw = 2.4f, bw = 3.6f, h = 0.2f;
                mb.Quad(new Vector3(-L, h, -hw), new Vector3(-L, h, hw), new Vector3(L, h, hw), new Vector3(L, h, -hw), top);
                mb.Quad(new Vector3(-L, 0.01f, bw), new Vector3(L, 0.01f, bw), new Vector3(L, h, hw), new Vector3(-L, h, hw), side);
                mb.Quad(new Vector3(-L, h, -hw), new Vector3(L, h, -hw), new Vector3(L, 0.01f, -bw), new Vector3(-L, 0.01f, -bw), side);
            });
            bed.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Look.FogBase = t.Fog;

            var rnd = new System.Random(theme * 977 + 5);
            System.Func<float, float, float> R = (a, b) => a + (float)rnd.NextDouble() * (b - a);
            // Worn patches of ground, flat on the earth.
            for (int i = 0; i < 0; i++)
            {
                float w = R(8, 26), l = R(5, 16);
                var p = Models.Built("Patch", _country, mb => { mb.Matte(); mb.Cylinder(Vector3.zero, new Vector3(0, 0.03f, 0), 1, 1, 14, t.Patch); });
                p.transform.localScale = new Vector3(w, 1, l);
                p.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Place(p.transform, R(-Span, Span), R(6, 120) * (rnd.Next(2) == 0 ? -1 : 1), R(0, 360));
            }
            // Ground cover drawn instanced (one call per thousand), thickest near the line: grass tufts on the prairie, pebbles on the dry lines.
            _cover.Clear(); _coverMesh = null;
            if (!string.IsNullOrEmpty(t.Cover))
            {
                _coverMesh = Resources.Load<Mesh>("Models/" + t.Cover);
                if (_coverMesh != null)
                {
                    bool stones = t.Cover.StartsWith("rock");
                    _coverMat = Materials.KitTint(stones ? t.Rock : t.Plant);
                    var b = _coverMesh.bounds;
                    _coverPivot = Matrix4x4.Translate(new Vector3(-b.center.x, -b.min.y, -b.center.z));
                    float unit = 1f / Mathf.Max(0.0001f, b.size.y);
                    for (int i = 0; i < t.CoverCount; i++)
                    {
                        float z = (3.9f + 58f * Mathf.Pow(R(0, 1), 1.7f)) * (rnd.Next(2) == 0 ? -1 : 1);
                        _cover.Add(new Tuft { X = R(-Span, Span), Z = z, Turn = Quaternion.Euler(0, R(0, 360), 0), Size = (stones ? R(0.12f, 0.4f) : R(0.45f, 1.05f)) * unit });      // pebbles on the dry lines
                    }
                }
            }
            // The telegraph line beside the track: a pole every so often, two wires sagging between them.
            {
                const int poles = 14; float gap = Span * 2 / poles, h = 6.6f;
                var wire = new Color(0.16f, 0.14f, 0.13f);
                for (int i = 0; i < poles; i++)
                {
                    var p = Models.Tall("pole", _country, h, new Color(0.62f, 0.48f, 0.36f));
                    var w = Models.Built("Wires", p, mb =>
                    {
                        mb.Matte();
                        foreach (float side in new[] { -PoleArm, PoleArm })
                        {
                            const int seg = 8;
                            for (int k = 0; k < seg; k++)
                            {
                                float a = (float)k / seg, b = (float)(k + 1) / seg;
                                float ya = h * PoleTop - 0.55f * 4 * a * (1 - a), yb = h * PoleTop - 0.55f * 4 * b * (1 - b);
                                var pa = new Vector3(a * gap, ya, side); var pb = new Vector3(b * gap, yb, side);
                                mb.Quad(pa + Vector3.down * 0.03f, pa + Vector3.up * 0.03f, pb + Vector3.up * 0.03f, pb + Vector3.down * 0.03f, wire);
                                mb.Quad(pb + Vector3.down * 0.03f, pb + Vector3.up * 0.03f, pa + Vector3.up * 0.03f, pa + Vector3.down * 0.03f, wire);
                            }
                        }
                    });
                    w.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    Place(p, -Span + i * gap, 6.8f, 0);
                }
            }
            // Small things where the fight is, big things beyond it.
            for (int i = 0; i < 300; i++)
            {
                string id = t.Near[rnd.Next(t.Near.Length)];
                var p = id.StartsWith("rock") ? Models.Tall(id, _country, R(0.5f, 1.3f), t.Rock) : Plantish(id) ? Models.Tall(id, _country, R(0.5f, 1.3f), t.Plant) : Models.Tall(id, _country, R(0.5f, 1.3f));
                // Thicker by the line, thinning out into the open.
                float z = i % 3 == 0 ? R(4.2f, 12) : R(5, 46);
                Place(p, R(-Span, Span), z * (rnd.Next(2) == 0 ? -1 : 1), R(0, 360));
            }
            for (int i = 0; i < 70; i++)
            {
                string id = t.Far[rnd.Next(t.Far.Length)];
                float h = id.StartsWith("tree") ? R(7, 12) : id == "windmill" ? 13 : id == "cliff" ? R(9, 17) : id == "rock_c" || id == "rock_e" ? R(5, 13) : id.StartsWith("cactus") ? R(4, 7) : R(2.5f, 6);
                var p = id.StartsWith("rock") ? Models.Tall(id, _country, h, t.Rock) : Plantish(id) ? Models.Tall(id, _country, h, t.Plant) : Models.Tall(id, _country, h);
                float z = rnd.Next(5) == 0 ? -R(40, 56) : R(24, 150);      // few on the camera's side, where they would hide the fight
                Place(p, R(-Span, Span), z, R(0, 360));
            }
        }

        struct Tuft { public float X, Z, Size; public Quaternion Turn; }
        readonly List<Tuft> _cover = new List<Tuft>();
        Mesh _coverMesh; Material _coverMat; Matrix4x4 _coverPivot;
        readonly Matrix4x4[] _coverBatch = new Matrix4x4[1023];

        /// <summary>Draws the ground cover where the country has rolled to.</summary>
        void DrawCover()
        {
            if (_coverMesh == null || _cover.Count == 0) return;
            var world = transform.localToWorldMatrix;
            bool station = _station != null && !float.IsNaN(_stationX);
            int n = 0;
            for (int i = 0; i < _cover.Count; i++)
            {
                var c = _cover[i];
                float x = Mathf.Repeat(c.X - _travel + Span, Span * 2) - Span;
                if (x < -80 || x > 70) continue;      // only what the camera can see (it looks a little behind the engine)
                if (station && c.Z > 3.5f && c.Z < 22 && Mathf.Abs(x - _stationX) < 26) continue;      // not through the platform or the saloon
                _coverBatch[n++] = world * Matrix4x4.TRS(new Vector3(x, 0, c.Z), c.Turn, Vector3.one * c.Size) * _coverPivot;
                if (n == _coverBatch.Length)
                {
                    Graphics.DrawMeshInstanced(_coverMesh, 0, _coverMat, _coverBatch, n, null, UnityEngine.Rendering.ShadowCastingMode.Off, true, gameObject.layer);
                    n = 0;
                }
            }
            if (n > 0) Graphics.DrawMeshInstanced(_coverMesh, 0, _coverMat, _coverBatch, n, null, UnityEngine.Rendering.ShadowCastingMode.Off, true, gameObject.layer);
        }

        static bool Plantish(string id) { return id == "grass" || id == "bush" || id.StartsWith("tree"); }

        public static float PoleTop = 0.93f, PoleArm = 0.7f;      // where the wires meet the pole (share of its height; metres either side)

        void Place(Transform p, float x, float z, float yaw)
        {
            p.localPosition = new Vector3(x, p.localPosition.y, z);
            p.localRotation = Quaternion.Euler(0, yaw, 0);
            _props.Add(p);
        }

        void BuildRails()
        {
            for (float x = -Span; x <= Span + _railLen; x += _railLen)
            {
                var r = Models.Along("rail", _rails, _railLen);
                r.localPosition = new Vector3(x, 0.2f, 0);
                _railBits.Add(r);
            }
        }

        void Scroll(float metres)
        {
            if (metres == 0) return;
            foreach (var p in _props)
            {
                var v = p.localPosition; v.x -= metres;
                if (v.x < -Span) v.x += Span * 2; else if (v.x > Span) v.x -= Span * 2;
                p.localPosition = v;
            }
            _groundAt = Mathf.Repeat(_groundAt + metres, Span * 2);
            _travel = Mathf.Repeat(_travel + metres, Span * 2);
            _cloudAt = Mathf.Repeat(_cloudAt + metres, 100000f); Shader.SetGlobalFloat("_CloudScroll", _cloudAt);
            if (_ground != null) _ground.localPosition = new Vector3(-_groundAt + Span, 0, 0);
            _railAt = Mathf.Repeat(_railAt + metres, _railLen);
            for (int i = 0; i < _railBits.Count; i++) _railBits[i].localPosition = new Vector3(-Span + i * _railLen - _railAt, 0.2f, 0);
            if (_station != null)
            {
                _stationX -= metres;
                _station.localPosition = new Vector3(_stationX, 0, 0);
                if (_stationX < -Span - 40) { Destroy(_station.gameObject); _station = null; _stationX = float.NaN; }
            }
        }

        // ---- the station ----

        void BuildStation(float x)
        {
            if (_station != null) Destroy(_station.gameObject);
            _station = new GameObject("Station").transform; _station.SetParent(transform, false);
            _stationX = x; _station.localPosition = new Vector3(x, 0, 0);
            var wood = Palette.Hex(0xA9825A);
            var plat = Models.Built("Platform", _station, mb => { mb.Matte(); mb.Box(new Vector3(0, 0.45f, 8.2f), new Vector3(46, 0.9f, 7), wood, Palette.Hex(0xC7A277)); });
            var saloon = Models.Tall("saloon", _station, 11); saloon.localPosition = new Vector3(4, 0.9f, 15.5f); saloon.localRotation = Quaternion.Euler(0, 180, 0);
            var tower = Models.Tall("water_tower_c", _station, 12); tower.localPosition = new Vector3(-17, 0, 14);
            var tent = Models.Tall("tent", _station, 3.2f); tent.localPosition = new Vector3(19, 0, 16); tent.localRotation = Quaternion.Euler(0, 200, 0);
            for (int i = 0; i < 5; i++) { var b = Models.Tall(i % 2 == 0 ? "barrel" : "crate", _station, 1.1f); b.localPosition = new Vector3(-9 + i * 1.5f, 0.9f, 6.2f + (i % 3) * 0.5f); }
            var flag = Models.Tall("flag", _station, 6); flag.localPosition = new Vector3(21, 0.9f, 6.5f);
        }

        // ---- the train ----

        static readonly Color Iron = new Color(0.7f, 0.72f, 0.78f), Dark = new Color(0.5f, 0.5f, 0.56f);

        void BuildTrain(List<string> types)
        {
            for (int i = _train.childCount - 1; i >= 0; i--) Destroy(_train.GetChild(i).gameObject);
            _cars.Clear();
            for (int i = 0; i < types.Count; i++)
            {
                var v = new CarView();
                v.Root = new GameObject(types[i]).transform; v.Root.SetParent(_train, false);
                v.Root.localPosition = new Vector3((float)Sim.CarX(i), 0.35f, 0);
                BuildCar(v, types[i]);
                v.Rends = v.Root.GetComponentsInChildren<Renderer>();
                _cars.Add(v);
            }
            if (CameraRig.Instance != null) CameraRig.Instance.Centre((float)Sim.CarX(types.Count - 1) * 0.5f, -(float)Sim.CarX(types.Count - 1) + 6);
        }

        Transform Gun(CarView v, string model, float length, float deck, float aimOffset, Color? tint = null, float hue = 0)
        {
            v.Gun = new GameObject("Gun").transform; v.Gun.SetParent(v.Root, false); v.Gun.localPosition = new Vector3(0, deck, 0);
            var g = Models.Along(model, v.Gun, length, false, tint, hue);
            v.AimOffset = aimOffset;
            v.Muzzle = new Vector3(0, deck + 0.9f, 0);
            return g;
        }

        void BuildCar(CarView v, string type)
        {
            const float L = 5.5f;
            switch (type)
            {
                case "engine":
                    v.Body = Models.Along("loco_b", v.Root, L + 0.3f, true, new Color(0.92f, 0.86f, 0.86f));
                    Gun(v, "ship_cannon", 1.9f, 2.15f, 0); v.Gun.localPosition = new Vector3(1.4f, 2.15f, 0); v.Muzzle = new Vector3(1.4f, 2.7f, 0);
                    {
                        // The headlamp: lit at night, throwing a pool of warm light ahead and around the engine.
                        var lampGo = new GameObject("Headlamp"); lampGo.transform.SetParent(v.Root, false); lampGo.transform.localPosition = new Vector3(4.5f, 3.2f, 0);
                        var lamp = v.Lamp = lampGo.AddComponent<Light>(); lamp.type = LightType.Point; lamp.color = new Color(1f, 0.82f, 0.5f); lamp.range = 32; lamp.intensity = 0; lamp.shadows = LightShadows.None; lamp.enabled = false;      // its own: the icon renders build engines too
                    }
                    break;
                case "gatling": v.Body = Models.Along("flatbed_wood", v.Root, L); Gun(v, "gatling", 2.6f, 1.05f, 0); break;
                case "cannon": v.Body = Models.Along("flatbed_wood", v.Root, L); Gun(v, "big_cannon", 3.3f, 1.05f, 0); break;
                case "mortar": v.Body = Models.Along("flatbed", v.Root, L); Gun(v, "cannon", 2.6f, 1.05f, 0, Dark); v.Gun.localRotation = Quaternion.Euler(0, 0, 0); break;
                case "dynamo": v.Body = Models.Along("flatbed", v.Root, L); Gun(v, "turret", 2.4f, 1.05f, 0, new Color(0.75f, 0.9f, 1.15f), 0.55f); break;
                case "rifle":
                    v.Body = Models.Along("boxcar", v.Root, L, false, new Color(1.05f, 0.92f, 0.8f));
                    v.Gun = new GameObject("Gun").transform; v.Gun.SetParent(v.Root, false); v.Gun.localPosition = new Vector3(0, 2.3f, 0);
                    for (int k = -1; k <= 1; k += 2) { var m = Models.Tall("cowboy", v.Gun, 1.7f); m.localPosition = new Vector3(0, 0, k * 0.55f); m.localRotation = Quaternion.Euler(0, 90, 0); }
                    v.Muzzle = new Vector3(0, 3.6f, 0);
                    break;
                case "supply": v.Body = Models.Along("woodcar", v.Root, L); for (int k = -1; k <= 1; k++) { var c = Models.Tall(k == 0 ? "chest" : "crate", v.Root, 0.9f); c.localPosition = new Vector3(k * 1.3f, 2.0f, 0); } break;
                case "armour": v.Body = Models.Along("coalcar", v.Root, L, false, new Color(0.62f, 0.64f, 0.72f)); break;
                case "flame": v.Body = Models.Along("tankcar", v.Root, L, false, new Color(1.1f, 0.85f, 0.8f)); v.Muzzle = new Vector3(0, 1.6f, 0); break;
                case "repair": v.Body = Models.Along("lumbercar", v.Root, L); { var c = Models.Tall("box", v.Root, 0.9f); c.localPosition = new Vector3(1.2f, 2.0f, 0); } break;
                default: v.Body = Models.Along("boxcar", v.Root, L); break;
            }
        }

        // ---- raiders ----

        static readonly string[] Men = { "man_0", "man_1", "man_2", "man_3", "man_4", "man_5" };
        static Mesh[] _strides(string man) { var m = new Mesh[4]; for (int i = 0; i < 4; i++) m[i] = Resources.Load<Mesh>("Models/" + man + "_w" + i); return m; }

        Transform Man(Transform parent, int pick, float height, bool walks, List<Strider> striders, Color? tint = null)
        {
            string man = Men[Mathf.Abs(pick) % Men.Length];
            var t = Models.Tall(man + "_idle", parent, height, tint);
            if (walks) { var s = t.gameObject.AddComponent<Strider>(); s.Strides = _strides(man); s.Still = Resources.Load<Mesh>("Models/" + man + "_idle"); striders.Add(s); }
            return t;
        }

        FoeView BuildFoe(EnemyState e)
        {
            var v = new FoeView { E = e, At = new Vector3((float)e.x, 0, (float)e.z) };
            v.Root = new GameObject(e.kind).transform; v.Root.SetParent(_foes, false); v.Root.localPosition = v.At;
            var striders = new List<Strider>();
            var body = new GameObject("Body").transform; body.SetParent(v.Root, false);
            switch (e.kind)
            {
                case "outrider":
                    Models.Along("horse_f", body, 2.7f, false, HorseTint(e.id)).localRotation = Quaternion.Euler(0, -90, 0);
                    Man(body, e.id, 1.5f, false, striders).localPosition = new Vector3(0, 1.25f, -0.1f);
                    v.Height = 3.2f; v.Bob = 1;
                    break;
                case "sledge": Man(body, e.id, 2.9f, true, striders, new Color(0.8f, 0.7f, 0.7f)); v.Height = 3.4f; break;
                case "powder":
                    Man(body, e.id, 1.5f, true, striders, new Color(1.2f, 0.8f, 0.7f));
                    Models.Tall("dynamite", body, 0.9f).localPosition = new Vector3(0, 1.0f, -0.35f);
                    v.Height = 2.2f;
                    break;
                case "gunwagon":
                    Models.Along("west_cart", body, 3.4f).localRotation = Quaternion.Euler(0, -90, 0);
                    Models.Along("horse_f", body, 2.6f, false, HorseTint(e.id)).localPosition = new Vector3(0, 0, 2.9f); body.GetChild(1).localRotation = Quaternion.Euler(0, -90, 0);
                    Man(body, e.id, 1.5f, false, striders).localPosition = new Vector3(0, 1.0f, 0);
                    v.Height = 3.2f; v.Bob = 0.4f;
                    break;
                case "handcar":
                    Models.Along("minecart", body, 2.6f, false, new Color(1.5f, 1.4f, 1.3f)).localRotation = Quaternion.Euler(0, -90, 0);
                    Man(body, e.id, 1.5f, false, striders).localPosition = new Vector3(0, 1.0f, 0.3f);
                    Man(body, e.id + 1, 1.5f, false, striders).localPosition = new Vector3(0, 1.0f, -0.4f);
                    v.Height = 3f;
                    break;
                case "warwagon":
                    Models.Along("wagon_c", body, 4.6f, false, new Color(0.62f, 0.55f, 0.55f)).localRotation = Quaternion.Euler(0, -90, 0);
                    Models.Along("horse_f", body, 2.8f, false, new Color(0.55f, 0.5f, 0.5f)).localPosition = new Vector3(0, 0, 3.8f); body.GetChild(1).localRotation = Quaternion.Euler(0, -90, 0);
                    v.Height = 4.2f; v.Bob = 0.3f;
                    break;
                default:    // the Baron's carriage
                    Models.Along("carriage", body, 8f, false, new Color(0.75f, 0.5f, 0.55f)).localRotation = Quaternion.Euler(0, -90, 0);
                    Models.Tall("flag", body, 5f, new Color(1.3f, 0.4f, 0.4f)).localPosition = new Vector3(0, 3.6f, -1.5f);
                    v.Height = 7.5f; v.Bob = 0.2f;
                    break;
            }
            // Each line's raiders wear its colours (the kit figures are tinted, darker and harder the further out).
            int line = S.Run != null ? S.Run.route : 0;
            if (line > 0)
            {
                var wear = LineWear[Mathf.Min(line, LineWear.Length - 1)];
                foreach (var mr in body.GetComponentsInChildren<MeshRenderer>()) { var m = mr.sharedMaterial; var baseTint = m.HasProperty("_Tint") ? m.GetColor("_Tint") : Color.white; mr.sharedMaterial = Materials.KitTint(baseTint * wear, m.HasProperty("_HueShift") ? m.GetFloat("_HueShift") : 0); }
            }
            bool big = e.kind == "boss" || e.kind == "warwagon";
            body.localScale = Vector3.one * (big ? 1.15f : 1.5f); v.Height *= big ? 1.15f : 1.5f; v.Body = body; v.Size = big ? 1.15f : 1.5f;
            v.Striders = striders.ToArray();
            // The health bar: only drawn once the raider has been hurt.
            v.Bar = Fx.Quad(v.Root, new Color(0.12f, 0.07f, 0.05f, 0.85f), false); v.Bar.localScale = new Vector3(Content.Enemy(e.kind).Boss ? 5f : 1.9f, 0.3f, 1);
            v.Fill = Fx.Quad(v.Bar, new Color(0.95f, 0.25f, 0.2f, 1f), false); v.Fill.localPosition = new Vector3(0, 0, -0.01f);
            v.Bar.gameObject.SetActive(false);
            return v;
        }

        static readonly Color[] LineWear = { Color.white, new Color(1.1f, 0.85f, 0.7f), new Color(0.8f, 0.9f, 1.15f), new Color(1.15f, 1.1f, 0.95f), new Color(0.7f, 0.6f, 0.7f) };

        static Color HorseTint(int id) { float k = 0.75f + (id * 37 % 50) / 100f; return new Color(k, k * 0.95f, k * 0.9f); }

        // ---- every frame ----

        void Sync(bool first = false)
        {
            var s = S; var r = s.Run;
            int theme = Mathf.Clamp(r != null ? r.route : s.State.route, 0, Themes.Length - 1);
            if (theme != _theme) BuildCountry(theme);

            var types = new List<string> { "engine" };
            if (r != null) { types.Clear(); foreach (var c in r.cars) types.Add(c.type); }
            else types.AddRange(s.State.train.GetRange(0, Mathf.Min(s.State.train.Count, s.Slots)));
            string key = string.Join(",", types);
            if (key != _trainKey) { _trainKey = key; BuildTrain(types); }

            bool stopped = r == null || r.phase != (int)Phase.Rolling;
            float centre = (float)Sim.CarX(types.Count - 1) * 0.5f;
            if (stopped && r != null && r.phase == (int)Phase.Over) { }      // a beaten train stands where it fell: no station
            else if (stopped && _station == null) BuildStation(first ? centre : centre + 95);
        }

        void LateUpdate() { DrawCover(); }

        void Update()
        {
            var s = S; if (s == null) return;
            if (s != _sim) return;
            Sync();
            var r = s.Run;
            float dt = Time.deltaTime;
            Pace = s.Running ? (float)s.Speed * GameRoot.Speed : 1;
            bool rolling = s.Running;
            bool over = r != null && r.phase == (int)Phase.Over;

            // The country: at speed while rolling; into a station it runs the last metres and stops.
            float centre = _cars.Count > 0 ? (float)Sim.CarX(_cars.Count - 1) * 0.5f : 0;
            float want = rolling ? Cruise * Pace : 0;
            if (!rolling && !over && _station != null && _stationX > centre + 0.05f) want = Mathf.Clamp((_stationX - centre) * 1.6f, 1.5f, Cruise * 1.5f);
            _speed = Mathf.MoveTowards(_speed, want, dt * (rolling ? 9 : 26));
            float move = _speed * dt;
            if (!rolling && !over && _station != null && _stationX - move < centre) { move = Mathf.Max(0, _stationX - centre); _speed = 0; }
            Scroll(move);

            // The day: forty miles from one morning to the next, so every fourth station is reached at night. Standing
            // at the depot it is always morning.
            Look.Day = r != null ? Mathf.Repeat(0.12f + (r.mile - 1 + Mathf.Clamp01((float)(r.mileTime / Content.MileSeconds))) / 40f, 1f) : 0.12f;
            var headlamp = _cars.Count > 0 ? _cars[0].Lamp : null;
            if (headlamp != null) { headlamp.intensity = Look.Night * 18f; headlamp.enabled = Look.Night > 0.05f; }

            // The train.
            float rock = Mathf.Clamp01(_speed / Cruise);
            for (int i = 0; i < _cars.Count; i++)
            {
                var v = _cars[i];
                var car = r != null && i < r.cars.Count ? r.cars[i] : null;
                v.Root.localPosition = new Vector3((float)Sim.CarX(i), 0.35f + Mathf.Sin(Time.time * 9 + i * 1.7f) * 0.025f * rock, 0);
                v.Root.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 6 + i * 2.3f) * 0.6f * rock, 0, 0);
                bool isOut = car != null && car.Out;
                if (isOut != v.Out) { v.Out = isOut; foreach (var rd in v.Rends) rd.enabled = true; if (isOut) Fx.Burst(v.Root.position + Vector3.up * 1.5f, 3.5f, new Color(1f, 0.6f, 0.2f)); }
                if (v.Gun != null)
                {
                    float tilt = isOut ? 24 : 0;
                    v.Gun.localRotation = Quaternion.Slerp(v.Gun.localRotation, Quaternion.Euler(tilt, v.GunYaw + v.AimOffset, 0), 1 - Mathf.Exp(-18 * dt));
                }
                if (!isOut && car != null && car.platingMax > 0 && car.plating < car.platingMax * 0.35 && Random.value < dt * 4) Fx.Smoke(v.Root.position + new Vector3(Random.Range(-1f, 1f), 1.6f, 0), new Color(0.3f, 0.28f, 0.28f, 0.5f), 1f, new Vector3(-_speed * 0.5f, 3, 0));      // nearly gone: it smokes
                if (isOut && Random.value < dt * 9) Fx.Smoke(v.Root.position + new Vector3(Random.Range(-1.5f, 1.5f), 1.8f, 0), new Color(0.12f, 0.1f, 0.1f, 0.75f), 1.6f, new Vector3(-_speed * 0.5f, 4, 0));
                v.Flash = Mathf.MoveTowards(v.Flash, 0, dt * 9);
                if (v.Gun != null) v.Gun.localScale = Vector3.one * (1 + 0.14f * v.Flash);      // the gun jumps as it fires
                v.Hurt = Mathf.MoveTowards(v.Hurt, 0, dt * 5);
                if (v.Hurt > 0) v.Root.localPosition += new Vector3(0, 0, Mathf.Sin(Time.time * 70) * 0.07f * v.Hurt);
            }
            // Smoke from the stack, streaming back with the speed.
            if (_cars.Count > 0 && Random.value < dt * (rolling ? 55 : 6))
                Fx.Smoke(_cars[0].Root.position + new Vector3(1.9f, 3.9f, 0), new Color(0.93f, 0.93f, 0.93f, 0.5f), Random.Range(0.9f, 1.5f), new Vector3(-_speed * Random.Range(0.7f, 0.9f), Random.Range(3.5f, 6f), Random.Range(-0.6f, 0.6f)));

            // Raiders.
            _gone.Clear();
            foreach (var kv in _foeViews) kv.Value.Seen = 0;
            if (r != null)
                foreach (var e in r.enemies)
                {
                    if (e.hp <= 0) continue;
                    FoeView v;
                    if (!_foeViews.TryGetValue(e.id, out v)) _foeViews[e.id] = v = BuildFoe(e);
                    v.E = e; v.Seen = 1;
                    var to = new Vector3((float)e.x, 0, (float)e.z);
                    bool moving = (to - v.At).sqrMagnitude > 0.0004f;
                    v.At = Vector3.Lerp(v.At, to, 1 - Mathf.Exp(-14 * dt * Mathf.Max(1, Pace)));
                    float bob = moving ? Mathf.Abs(Mathf.Sin(Time.time * 11 * Mathf.Sqrt(Pace) + e.id)) * 0.32f * v.Bob : 0;
                    var toTrain = new Vector3(0, 0, -Mathf.Sign(v.At.z == 0 ? 1 : v.At.z));
                    v.Root.localPosition = v.At + new Vector3(0, bob, 0) + toTrain * Mathf.Sin(v.Lunge * Mathf.PI) * 0.9f;
                    float yaw = (float)e.facing * Mathf.Rad2Deg + (e.stun > 0 ? Mathf.Sin(Time.time * 40) * 14 : 0);
                    v.Root.localRotation = Quaternion.Slerp(v.Root.localRotation, Quaternion.Euler(moving ? -bob * 14 : 0, yaw, 0), 1 - Mathf.Exp(-10 * dt));
                    // A strike shows: its cooldown has just been wound back up. Shooters fire a red tracer at the car they
                    // are aiming for; the rest lunge at it.
                    if (e.striking && e.cooldown > v.LastCooldown + 0.05 && rolling)
                    {
                        var def = Content.Enemy(e.kind);
                        float back = _cars.Count > 0 ? (float)Sim.CarX(_cars.Count - 1) - 2.5f : 0;
                        var aim = new Vector3(Mathf.Clamp(v.At.x, back, 2.5f), 1.3f, 0);
                        if (def.Range > 3)
                        {
                            var muzzle = v.At + Vector3.up * (v.Height * 0.55f);
                            Fx.Tracer(muzzle, aim, def.Boss ? 0.34f : 0.24f, new Color(1f, 0.3f, 0.2f), 0.22f);      // slower to fade than the train's own: it is the one to notice
                            Fx.Spark(aim);
                            Fx.Flash(muzzle, def.Boss ? 1.6f : 0.9f);
                            if (Random.value < 0.5f) Sfx.Play("rifle", 0.16f, Random.Range(0.7f, 0.85f));
                        }
                        else v.Lunge = 1;
                    }
                    v.LastCooldown = e.cooldown;
                    v.Lunge = Mathf.MoveTowards(v.Lunge, 0, dt * 6);
                    foreach (var st in v.Striders) st.Moving = moving && e.stun <= 0;
                    v.Punch = Mathf.MoveTowards(v.Punch, 0, dt * 8);
                    if (v.Body != null) v.Body.localScale = new Vector3(1 + 0.16f * v.Punch, 1 - 0.12f * v.Punch, 1 + 0.16f * v.Punch) * v.Size;      // a hit squashes it for a moment
                    bool hurt = e.hp < e.hpMax;
                    if (v.Bar.gameObject.activeSelf != hurt) v.Bar.gameObject.SetActive(hurt);
                    if (hurt)
                    {
                        v.Bar.position = v.Root.position + Vector3.up * (v.Height + 0.5f);
                        v.Bar.rotation = CameraRig.Instance.transform.rotation;
                        float f = Mathf.Clamp01((float)(e.hp / e.hpMax));
                        v.Fill.localScale = new Vector3(f * 0.94f, 0.66f, 1); v.Fill.localPosition = new Vector3(-(1 - f) * 0.47f, 0, -0.01f);
                    }
                    if (rolling && moving && Random.value < dt * 5 * v.Bob) Fx.Smoke(v.At + new Vector3(0, 0.2f, 0), new Color(0.75f, 0.66f, 0.5f, 0.45f), 0.9f, new Vector3(-_speed * 0.3f, 1.2f, 0));
                }
            foreach (var kv in _foeViews) if (kv.Value.Seen == 0) _gone.Add(kv.Key);
            foreach (int id in _gone) { Destroy(_foeViews[id].Root.gameObject); _foeViews.Remove(id); }
            // Raiders are carried back with the ground when the train rolls: what the simulation calls standing still
            // is keeping pace, so nothing more to do here.

            Fx.Mark(r != null && r.mark != 0 && _foeViews.ContainsKey(r.mark) ? (Vector3?)_foeViews[r.mark].At : null);
            Fx.Tick(dt);
        }

        // ---- what the simulation reports ----

        void OnFired(int car, double x, double z, bool crit)
        {
            if (car < 0 || car >= _cars.Count) return;
            var v = _cars[car]; var r = S.Run;
            var def = Content.Car(r.cars[car].type);
            var from = v.Root.position + v.Muzzle;
            var to = new Vector3((float)x, 1.1f, (float)z);
            v.Flash = 1;
            v.GunYaw = Mathf.Atan2(to.x - v.Root.position.x, to.z) * Mathf.Rad2Deg - 90;
            if (def.Aim == Aim.Around)
            {
                for (int i = 0; i < 6; i++) { float a = Random.value * Mathf.PI * 2; Fx.Flame(v.Root.position + new Vector3(Mathf.Cos(a) * 2, 1.2f, Mathf.Sin(a) * 1.4f), new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a)) * (float)def.Range * 1.6f); }
                if (Random.value < 0.25f) Sfx.Play("flame", 0.18f, Random.Range(0.9f, 1.1f));
                return;
            }
            if (def.Flight > 0)
            {
                Fx.Shell(from, to, (float)def.Flight / Mathf.Max(0.2f, Pace), def.Id == "mortar" ? 14 : 4);
                Fx.Flash(from, 1.6f); Fx.Smoke(from, new Color(0.9f, 0.9f, 0.9f, 0.6f), 1.4f, Vector3.up * 2);
                Sfx.Play("cannon", 0.5f, def.Id == "mortar" ? 0.7f : Random.Range(0.92f, 1.08f));
                if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.12f);
                return;
            }
            if (def.Chain > 0) { Fx.Bolt(_lastBolt.HasValue && Time.time - _boltAt < 0.02f ? _lastBolt.Value : from, to); _lastBolt = to; _boltAt = Time.time; Sfx.Play("zap", 0.25f, Random.Range(0.9f, 1.2f)); return; }
            bool heavy = def.Id == "rifle" || def.Id == "engine";
            Fx.Tracer(from, to, heavy ? 0.16f : 0.09f, crit ? new Color(1f, 0.55f, 0.2f) : new Color(1f, 0.92f, 0.55f));
            Fx.Flash(from, heavy ? 1.1f : 0.7f);
            if (heavy || Random.value < 0.34f) Sfx.Play(heavy ? "rifle" : "shot", heavy ? 0.4f : 0.2f, Random.Range(0.9f, 1.12f));
        }
        Vector3? _lastBolt; float _boltAt;

        void OnHit(EnemyState e, double damage, bool crit)
        {
            FoeView v;
            if (!_foeViews.TryGetValue(e.id, out v)) return;
            Fx.Spark(v.At + Vector3.up * (v.Height * 0.5f));
            v.Punch = 1;
            if (crit && Hud.Instance != null) Hud.Instance.Pop(v.At + Vector3.up * v.Height, Format.Money(damage) + "!", new Color(1f, 0.78f, 0.25f), 34);
        }

        void OnFell(EnemyState e, double scrip)
        {
            FoeView v;
            var at = _foeViews.TryGetValue(e.id, out v) ? v.At : new Vector3((float)e.x, 0, (float)e.z);
            bool boss = Content.Enemy(e.kind).Boss;
            Fx.Burst(at + Vector3.up, boss ? 9 : 2.2f, boss ? new Color(1f, 0.6f, 0.2f) : new Color(0.9f, 0.8f, 0.6f));
            for (int i = 0; i < (boss ? 16 : 3); i++) Fx.Smoke(at + Random.insideUnitSphere * (boss ? 3 : 0.8f) + Vector3.up, new Color(0.7f, 0.62f, 0.5f, 0.6f), boss ? 3 : 1.3f, new Vector3(Random.Range(-2f, 2f), 3, Random.Range(-2f, 2f)));
            if (Hud.Instance != null && (boss || e.id % 3 == 0)) Hud.Instance.Pop(at + Vector3.up * 2.5f, "+" + Format.Money(System.Math.Max(1, scrip)), new Color(0.55f, 0.95f, 0.5f), boss ? 46 : 28);
            if (boss) { Sfx.Play("boom", 0.9f, 0.7f); Haptics.Success(); if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.8f); }
            else if (Random.value < 0.4f) Sfx.Play("fall", 0.22f, Random.Range(0.85f, 1.2f));
        }

        void OnBurst(double x, double z, double wide)
        {
            var at = new Vector3((float)x, 0.3f, (float)z);
            Fx.Burst(at, (float)wide, new Color(1f, 0.62f, 0.22f));
            for (int i = 0; i < 5; i++) Fx.Smoke(at + Random.insideUnitSphere * (float)wide * 0.4f + Vector3.up * 0.6f, new Color(0.55f, 0.48f, 0.4f, 0.65f), (float)wide * 0.55f, new Vector3(Random.Range(-2f, 2f), 4, Random.Range(-2f, 2f)));
            Sfx.Play("boom", 0.45f, Random.Range(0.9f, 1.15f));
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(0.25f);
        }

        void OnStruck(int car, double damage)
        {
            if (car < 0 || car >= _cars.Count) return;
            _cars[car].Hurt = 1;
            Fx.Spark(_cars[car].Root.position + new Vector3(Random.Range(-2f, 2f), 1.4f, Random.Range(-1f, 1f)));
            if (Random.value < 0.3f) Sfx.Play("clang", 0.3f, Random.Range(0.85f, 1.15f));
            if (car == 0) { Look.Flash = Mathf.Max(Look.Flash, 0.15f); if (Hud.Instance != null) Hud.Instance.HullHit(); }
        }

        void OnRunOver()
        {
            if (_cars.Count == 0) return;
            var at = _cars[0].Root.position + Vector3.up * 2;
            Fx.Burst(at, 10, new Color(1f, 0.55f, 0.2f));
            for (int i = 0; i < 20; i++) Fx.Smoke(at + Random.insideUnitSphere * 3, new Color(0.15f, 0.13f, 0.13f, 0.8f), 3.5f, new Vector3(Random.Range(-3f, 3f), 5, Random.Range(-3f, 3f)));
            Sfx.Play("boom", 1f, 0.6f); Haptics.Medium();
            if (CameraRig.Instance != null) CameraRig.Instance.Shake(1f);
        }

        void OnMile(int mile, double steel)
        {
            if (Hud.Instance != null && _cars.Count > 0) Hud.Instance.Pop(_cars[0].Root.position + Vector3.up * 6, "+" + Format.Money(steel) + " steel", new Color(0.8f, 0.9f, 1f), 36);
        }

        /// <summary>The whistle: steam bursting from under every car and a ring rolling out over the ground.</summary>
        public void WhistleFx()
        {
            for (int i = 0; i < _cars.Count; i++)
            {
                var at = _cars[i].Root.position;
                Fx.Burst(at + Vector3.up * 0.5f, (float)Content.WhistleRange * 0.9f, new Color(0.95f, 0.97f, 1f));
                for (int k = 0; k < 10; k++)
                {
                    float a = Random.value * Mathf.PI * 2;
                    Fx.Smoke(at + new Vector3(Mathf.Cos(a) * 2.5f, 0.8f, Mathf.Sin(a) * 1.5f), new Color(1f, 1f, 1f, 0.75f), 2.2f, new Vector3(Mathf.Cos(a), 0.25f, Mathf.Sin(a)) * 9f);
                }
            }
            if (_cars.Count > 0) Fx.Smoke(_cars[0].Root.position + new Vector3(0.6f, 4.2f, 0), new Color(1f, 1f, 1f, 0.9f), 3f, Vector3.up * 9);
        }

        /// <summary>The raider nearest a point on the screen (within a thumb's width), or 0.</summary>
        public int Pick(Vector2 screen)
        {
            var cam = CameraRig.Instance.Cam; int best = 0; float near = Screen.height * 0.09f;
            foreach (var kv in _foeViews)
            {
                var p = cam.WorldToScreenPoint(kv.Value.At + Vector3.up * kv.Value.Height * 0.5f);
                if (p.z <= 0) continue;
                float d = Vector2.Distance(screen, p);
                if (d < near) { near = d; best = kv.Key; }
            }
            return best;
        }

        // ---- pictures of the cars, for the interface ----

        readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();

        /// <summary>A picture of a car with its weapon, rendered once from the real model.</summary>
        public Sprite Icon(string type)
        {
            Sprite sp;
            if (_icons.TryGetValue(type, out sp)) return sp;
            var stage = new GameObject("IconStage").transform;
            stage.position = new Vector3(0, -900, 0);
            var v = new CarView { Root = new GameObject(type).transform };
            v.Root.SetParent(stage, false);
            BuildCar(v, type);
            if (v.Gun != null) v.Gun.localRotation = Quaternion.Euler(0, v.AimOffset, 0);
            var b = new Bounds(stage.position, Vector3.zero); bool any = false;
            foreach (var r in stage.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            var camGo = new GameObject("IconCam"); camGo.transform.SetParent(stage, false);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = Mathf.Max(b.size.x * 0.56f, b.size.y * 0.75f);
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 80; cam.enabled = false;
            var rot = Quaternion.Euler(22, 28, 0);
            cam.transform.rotation = rot; cam.transform.position = b.center - rot * Vector3.forward * 30;
            var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            data.renderPostProcessing = false; data.renderShadows = false;
            const int N = 256;
            var rt = RenderTexture.GetTemporary(N, N, 24, RenderTextureFormat.ARGB32);
            bool fog = RenderSettings.fog; RenderSettings.fog = false;
            cam.targetTexture = rt; cam.Render();
            RenderSettings.fog = fog;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, N, N), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
            DestroyImmediate(stage.gameObject);
            sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100);
            return _icons[type] = sp;
        }

        /// <summary>Where a car is, for the interface (signs over wagons).</summary>
        public Vector3 CarPosition(int index) { return index >= 0 && index < _cars.Count ? _cars[index].Root.position : Vector3.zero; }
    }
}
