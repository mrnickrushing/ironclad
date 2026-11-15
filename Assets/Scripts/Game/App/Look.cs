using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ironclad.Game
{
    /// <summary>
    /// The picture: a filmic tone curve, a touch of bloom on glass and lights, a little more contrast and colour, a
    /// faint vignette, and shadows that reach across the whole fight. The day turns slowly over a run: morning, noon, a golden dusk, a blue night.
    /// </summary>
    public sealed class Look : MonoBehaviour
    {
        public static Look Instance { get; private set; }
        /// <summary>Lightning: set to 1 and it fades in a blink.</summary>
        public static float Flash;
        ColorAdjustments _grade;
        Bloom _bloom;
        Light _sun;
        float _gloom;

        public static Look Create(Camera cam, Light sun)
        {
            var go = new GameObject("Look");
            var look = go.AddComponent<Look>();
            Instance = look;
            look._sun = sun;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) { urp.shadowDistance = 420; urp.shadowCascadeCount = 2; }

            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.profile = p;
            var tone = p.Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var bloom = p.Add<Bloom>(); look._bloom = bloom; bloom.intensity.Override(0.18f); bloom.threshold.Override(1.5f); bloom.scatter.Override(0.6f);
            look._grade = p.Add<ColorAdjustments>();
            look._grade.postExposure.Override(0.1f); look._grade.contrast.Override(8f); look._grade.saturation.Override(22f);
            var vig = p.Add<Vignette>(); vig.intensity.Override(0.2f); vig.smoothness.Override(0.5f);
            return look;
        }

        /// <summary>The line's own haze colour by day (TrainView sets it with the line).</summary>
        public static Color FogBase = new Color(0.86f, 0.8f, 0.68f);
        /// <summary>Where the day stands, 0..1: morning, noon, dusk at about 0.55, night from 0.68 to 0.9, then dawn.</summary>
        public static float Day = 0.12f;
        /// <summary>How dark it is, 0 day .. 1 night (for the headlamp and anything else that lights up).</summary>
        public static float Night { get; private set; }
        float _shown = 0.12f;

        static readonly float[] Keys = { 0f, 0.42f, 0.56f, 0.68f, 0.9f, 1f };
        static readonly Color[] SunColour = { new Color(1f, 0.93f, 0.8f), new Color(1f, 0.95f, 0.86f), new Color(1f, 0.62f, 0.36f), new Color(0.55f, 0.66f, 1f), new Color(0.55f, 0.66f, 1f), new Color(1f, 0.93f, 0.8f) };
        static readonly float[] SunPower = { 1.4f, 1.45f, 1.15f, 0.8f, 0.8f, 1.4f };
        static readonly float[] SunHeight = { 38f, 58f, 16f, 50f, 50f, 38f };
        static readonly float[] Dark = { 0f, 0f, 0.35f, 1f, 1f, 0f };
        static readonly Color[] SkyAmb = { new Color(0.62f, 0.72f, 0.86f), new Color(0.64f, 0.74f, 0.88f), new Color(0.66f, 0.52f, 0.56f), new Color(0.3f, 0.37f, 0.6f), new Color(0.3f, 0.37f, 0.6f), new Color(0.62f, 0.72f, 0.86f) };
        static readonly Color[] FogTint = { Color.white, Color.white, new Color(1.05f, 0.72f, 0.6f), new Color(0.2f, 0.25f, 0.42f), new Color(0.2f, 0.25f, 0.42f), Color.white };

        void Update()
        {
            Flash = Mathf.MoveTowards(Flash, 0, Time.unscaledDeltaTime * 7);
            // The day moves slowly and never jumps (a new run starts in the morning: the light turns to it).
            float want = Mathf.Repeat(Day, 1f);
            float gap = Mathf.Repeat(want - _shown + 0.5f, 1f) - 0.5f;
            _shown = Mathf.Repeat(_shown + Mathf.Clamp(gap, -0.25f, 0.25f) * Mathf.Min(1, Time.unscaledDeltaTime * 1.5f), 1f);
            int k = 0; while (k < Keys.Length - 2 && _shown > Keys[k + 1]) k++;
            float t = Mathf.InverseLerp(Keys[k], Keys[k + 1], _shown); t = t * t * (3 - 2 * t);
            Night = Mathf.Lerp(Dark[k], Dark[k + 1], t);
            if (_sun != null)
            {
                _sun.color = Color.Lerp(SunColour[k], SunColour[k + 1], t);
                _sun.intensity = Mathf.Lerp(SunPower[k], SunPower[k + 1], t) + Flash * 2.5f;
                _sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(SunHeight[k], SunHeight[k + 1], t), -128 + _shown * 70, 0);
                _sun.shadowStrength = Mathf.Lerp(0.85f, 0.55f, Night);
            }
            var sky = Color.Lerp(SkyAmb[k], SkyAmb[k + 1], t);
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.55f, 0.58f, 0.56f), new Color(0.3f, 0.34f, 0.5f), Night);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.3f, 0.32f, 0.26f), new Color(0.12f, 0.14f, 0.22f), Night);
            var fog = FogBase * Color.Lerp(FogTint[k], FogTint[k + 1], t); fog.a = 1;
            RenderSettings.fogColor = fog;
            if (CameraRig.Instance != null) CameraRig.Instance.Cam.backgroundColor = fog;
            if (RenderSettings.skybox != null) RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(1.05f, 0.3f, Night));
            _bloom.intensity.Override(Mathf.Lerp(0.18f, 0.7f, Night));
            _bloom.threshold.Override(Mathf.Lerp(1.5f, 1.0f, Night));
            _grade.postExposure.Override(Mathf.Lerp(0.1f, 0.25f, Night));      // the night is blue, not black: everything stays readable
        }
    }
}
