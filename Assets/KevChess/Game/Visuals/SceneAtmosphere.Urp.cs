using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KevChess.Game.Visuals
{
    // Partie dépendante d'URP (Volume, overrides de post-traitement, données additionnelles).
    // Isolée pour pouvoir vérifier le reste du code sans le package URP.
    public sealed partial class SceneAtmosphere
    {
        Volume _volume;

        /// <summary>Volume global de post-traitement créé au runtime.</summary>
        public Volume PostProcessVolume => _volume;

        partial void SetupUrpPipeline()
        {
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            if (asset == null)
            {
                Debug.LogWarning("[KevChess] URP n'est pas le pipeline actif : l'ambiance sera dégradée.");
                return;
            }
            if (!asset.supportsHDR)
            {
                if (forcePipelineHdr)
                    asset.supportsHDR = true;
                else
                    Debug.LogWarning("[KevChess] HDR désactivé sur l'asset URP : le bloom néon et le tonemapping ACES seront ternes. Cochez « HDR » dans l'asset URP.");
            }
        }

        partial void SetupUrpCamera(Camera cam)
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data == null) return;
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = postProcessing;
            data.renderShadows = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing; // peu coûteux sur mobile
            data.dithering = true; // évite le banding dans les dégradés sombres du ciel
            data.volumeLayerMask = data.volumeLayerMask | (1 << gameObject.layer);
        }

        partial void SetupUrpLight(Light light)
        {
            UniversalAdditionalLightData data = light.GetUniversalAdditionalLightData();
            if (data != null)
                data.softShadowQuality = SoftShadowQuality.Medium;
        }

        partial void SetupPostProcessing()
        {
            var go = new GameObject("Neon Post Volume");
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer; // inclus dans le volumeLayerMask de la caméra (voir SetupUrpCamera)
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _volume.weight = 1f;
            _volume.sharedProfile = NeonPostProcessing.CreateRuntimeProfile();
        }
    }

    /// <summary>Réglages de post-traitement de la direction artistique néon.</summary>
    public static class NeonPostProcessing
    {
        /// <summary>
        /// Profil à utiliser au runtime : copie de celui de la <see cref="NeonShaderLibrary"/> s'il existe
        /// (ce qui garantit aussi que les variantes de post-traitement ne sont pas strippées), sinon créé à la volée.
        /// </summary>
        public static VolumeProfile CreateRuntimeProfile()
        {
            NeonShaderLibrary lib = NeonShaderLibrary.Load();
            if (lib != null && lib.postProcessProfile is VolumeProfile template)
                return Object.Instantiate(template);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "KevChess Neon Profile (runtime)";
            Configure(profile);
            return profile;
        }

        /// <summary>Ajoute / règle Bloom, Tonemapping ACES, Vignette et Color Adjustments.</summary>
        public static void Configure(VolumeProfile profile)
        {
            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.85f);
            bloom.scatter.Override(0.72f);
            bloom.clamp.Override(40f);
            bloom.tint.Override(new Color(0.92f, 0.95f, 1f));
            bloom.highQualityFiltering.Override(false); // coût mobile
            bloom.downscale.Override(BloomDownscaleMode.Half);
            bloom.maxIterations.Override(6);

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.color.Override(new Color(0.02f, 0.0f, 0.05f));
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);
            vignette.rounded.Override(false);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(0.35f); // compense l'assombrissement d'ACES
            color.contrast.Override(14f);
            color.saturation.Override(10f);
            color.colorFilter.Override(new Color(0.97f, 0.97f, 1f));
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
                return component;
            return profile.Add<T>(false);
        }
    }
}
