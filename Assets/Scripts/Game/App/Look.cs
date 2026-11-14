using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ironclad.Game
{
    /// <summary>
    /// The picture: a filmic tone curve, a touch of bloom on glass and lights, a little more contrast and colour, a
    /// faint vignette, and shadows that reach across the whole harbour. The weather tints and dims it.
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

        void Update()
        {
            Flash = Mathf.MoveTowards(Flash, 0, Time.unscaledDeltaTime * 7);
            if (_sun != null) _sun.intensity = 1.4f + Flash * 2.5f;
        }
    }
}
