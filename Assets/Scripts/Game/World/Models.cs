using System.Collections.Generic;
using UnityEngine;

namespace Ironclad.Game
{
    public static class Models
    {
        /// <summary>A free kit model (Resources/Models/{id}: one unit on its longest side, or one unit tall), placed, turned
        /// and sized in the parent's space. Null when it isn't there.</summary>
        public static GameObject Kit(string id, Transform parent, Vector3 at, float yaw, float size)
        {
            var mesh = Resources.Load<Mesh>("Models/" + id);
            if (mesh == null) return null;
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Materials.Kit;
            return go;
        }

        /// <summary>A kit model with its colours multiplied and its hues turned.</summary>
        public static GameObject Kit(string id, Transform parent, Vector3 at, float yaw, float size, Color tint, float hue = 0)
        {
            var go = Kit(id, parent, at, yaw, size);
            if (go != null) go.GetComponent<MeshRenderer>().sharedMaterial = Materials.KitTint(tint, hue);
            return go;
        }

        /// <summary>
        /// A kit model laid along the track: turned so its long side runs along x, sized to <paramref name="length"/>,
        /// centred, and stood on y = 0 of the holder it returns (move and turn the holder). <paramref name="flip"/>
        /// turns it end for end when the model was drawn facing the other way.
        /// </summary>
        public static Transform Along(string id, Transform parent, float length, bool flip = false, Color? tint = null, float hue = 0)
        {
            var holder = new GameObject(id).transform;
            holder.SetParent(parent, false);
            var mesh = Resources.Load<Mesh>("Models/" + id);
            if (mesh == null) return holder;
            var b = mesh.bounds;
            bool alongZ = b.size.z > b.size.x;
            float longSide = Mathf.Max(b.size.x, b.size.z);
            float scale = length / Mathf.Max(0.0001f, longSide);
            float yaw = (alongZ ? 90 : 0) + (flip ? 180 : 0);
            var go = new GameObject("Mesh");
            go.transform.SetParent(holder, false);
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = -(go.transform.localRotation * b.center) * scale + new Vector3(0, b.extents.y * scale, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = tint.HasValue || hue != 0 ? Materials.KitTint(tint ?? Color.white, hue) : Materials.Kit;
            return holder;
        }

        /// <summary>A kit model stood on the ground at this height, centred on its holder.</summary>
        public static Transform Tall(string id, Transform parent, float height, Color? tint = null, float hue = 0)
        {
            var holder = new GameObject(id).transform;
            holder.SetParent(parent, false);
            var mesh = Resources.Load<Mesh>("Models/" + id);
            if (mesh == null) return holder;
            var b = mesh.bounds;
            float scale = height / Mathf.Max(0.0001f, b.size.y);
            var go = new GameObject("Mesh");
            go.transform.SetParent(holder, false);
            go.transform.localScale = Vector3.one * scale;
            go.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z) * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = tint.HasValue || hue != 0 ? Materials.KitTint(tint ?? Color.white, hue) : Materials.Kit;
            return holder;
        }

        /// <summary>An object drawing what <paramref name="build"/> puts into a mesh builder (the game's own shapes).</summary>
        public static GameObject Built(string name, Transform parent, System.Action<MeshBuilder> build)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mb = new MeshBuilder();
            build(mb);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = Materials.World;
            return go;
        }
    }

    /// <summary>Swaps a figure's mesh through its frozen strides while it moves.</summary>
    public sealed class Strider : MonoBehaviour
    {
        public Mesh[] Strides; public Mesh Still; public float Rate = 6; public bool Moving = true;
        MeshFilter _mf; float _t;
        void Awake() { _mf = GetComponentInChildren<MeshFilter>(); }
        void Update()
        {
            if (_mf == null || Strides == null || Strides.Length == 0) return;
            if (!Moving) { if (Still != null) _mf.sharedMesh = Still; return; }
            _t += Time.deltaTime * Rate * TrainView.Pace;
            _mf.sharedMesh = Strides[(int)_t % Strides.Length];
        }
    }
}
