using UnityEngine;

namespace Ironclad.Game
{
    /// <summary>The view over the train: from one side and above, wide enough to see raiders ride in on both flanks.
    /// Dragged to pan along the track and pinched (or scrolled) to zoom.</summary>
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }
        public Camera Cam { get; private set; }

        public const float Pitch = 47f, Yaw = 9f;
        public Vector3 Focus = new Vector3(-6, 0, 0);
        public float Distance = 150f;
        public const float MinDistance = 50f, MaxDistance = 190f;
        float _shown, _shake; Vector3 _wantFocus; bool _held;

        public static CameraRig Create()
        {
            var go = new GameObject("Camera");
            var rig = go.AddComponent<CameraRig>();
            rig.Cam = go.AddComponent<Camera>();
            rig.Cam.fieldOfView = 24;
            rig.Cam.nearClipPlane = 1f; rig.Cam.farClipPlane = 1400f;
            rig.Cam.clearFlags = CameraClearFlags.Skybox;
            rig.Cam.backgroundColor = new Color(0.86f, 0.8f, 0.68f);
            go.tag = "MainCamera";
            go.AddComponent<AudioListener>();
            rig.Distance = 86f;
            rig._shown = rig.Distance; rig._wantFocus = rig.Focus;
            rig.Place();
            Instance = rig;
            return rig;
        }

        /// <summary>Where the view rests: the middle of the train.</summary>
        public void Centre(float x, float length = 0) { _wantFocus = new Vector3(x, 0, -5); _length = length; _zoomed = false; }      // a little toward the viewer: the wagons' buttons cover the bottom of the screen
        float _length; bool _zoomed;

        /// <summary>How far back to stand for this screen: the fight's depth on a wide phone, further on a narrower
        /// screen, and always far enough that the whole train is in view with room at both ends. Worked out from the
        /// camera's own aspect every frame: the screen's size can be wrong for the first frames after launch.</summary>
        float Fit()
        {
            float aspect = Cam.aspect > 0.1f ? Cam.aspect : 1.78f;
            float need = (_length + 26) / (2 * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * aspect);
            float rest = Mathf.Clamp(86f * Mathf.Max(1f, 1.75f / aspect), 86f, 130f);
            return Mathf.Clamp(Mathf.Max(rest, need), MinDistance, MaxDistance);
        }

        /// <summary>Drag by screen pixels: the ground moves with the finger.</summary>
        public void Pan(Vector2 pixels)
        {
            float perPixel = 2f * Distance * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Screen.height;
            var right = Quaternion.Euler(0, Yaw, 0) * Vector3.right;
            var fwd = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
            Focus -= right * pixels.x * perPixel + fwd * pixels.y * perPixel / Mathf.Sin(Pitch * Mathf.Deg2Rad);
            Focus.x = Mathf.Clamp(Focus.x, -70, 30);
            Focus.z = Mathf.Clamp(Focus.z, -30, 30);
            _held = true; _heldFor = 4;
        }
        float _heldFor;

        /// <summary>Zoom by a factor (over 1 = closer).</summary>
        public void Zoom(float factor) { _zoomed = true; _zoomFor = 8; Distance = Mathf.Clamp(Distance / Mathf.Max(0.01f, factor), MinDistance, MaxDistance); }
        float _zoomFor;

        public void Shake(float amount) { _shake = Mathf.Max(_shake, amount); }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            // A pinch holds for a while, then the view settles back to fit the train.
            if (_zoomed && (_zoomFor -= dt) <= 0) _zoomed = false;
            if (!_zoomed) Distance = Mathf.Lerp(Distance, Fit(), 1 - Mathf.Exp(-4 * dt));
            _shown = Mathf.Lerp(_shown, Distance, 1 - Mathf.Exp(-12 * dt));
            // A few seconds after the last drag the view drifts back to the train.
            if (_held && (_heldFor -= dt) <= 0) _held = false;
            if (!_held) Focus = Vector3.Lerp(Focus, _wantFocus, 1 - Mathf.Exp(-2.5f * dt));
            _shake = Mathf.MoveTowards(_shake, 0, dt * 2.5f);
            Place();
        }

        void Place()
        {
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            transform.rotation = rot;
            var jolt = _shake > 0 ? new Vector3(Mathf.PerlinNoise(Time.unscaledTime * 31, 0.5f) - 0.5f, Mathf.PerlinNoise(0.5f, Time.unscaledTime * 29) - 0.5f, 0) * _shake * 2.4f : Vector3.zero;
            transform.position = Focus - rot * Vector3.forward * _shown + rot * jolt;
        }
    }
}
