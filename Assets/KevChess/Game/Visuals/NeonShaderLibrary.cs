using UnityEngine;

namespace KevChess.Game.Visuals
{
    /// <summary>
    /// Références vers des matériaux « gabarits » (et le profil de post-traitement) placés dans
    /// un dossier Resources. Leur seule présence dans le build garantit que les shaders URP et
    /// les variantes de mots-clés utilisés par code (_EMISSION, transparence, etc.) ne sont pas
    /// strippés. À l'exécution, <see cref="NeonMaterials"/> clone ces gabarits.
    /// L'asset est généré automatiquement dans l'éditeur (menu KevChess > Visuals).
    /// </summary>
    public sealed class NeonShaderLibrary : ScriptableObject
    {
        /// <summary>Nom de l'asset dans Resources (sans extension).</summary>
        public const string ResourceName = "KevChessNeonShaderLibrary";

        [Tooltip("URP/Lit opaque avec _EMISSION.")]
        public Material litOpaqueEmissive;

        [Tooltip("URP/Lit transparent (alpha, spéculaire préservé) avec _EMISSION.")]
        public Material litTransparentEmissive;

        [Tooltip("URP/Particles/Unlit transparent additif.")]
        public Material particleAdditive;

        [Tooltip("Skybox/Cubemap.")]
        public Material skyboxCubemap;

        [Tooltip("VolumeProfile URP (Bloom, Tonemapping, Vignette, Color Adjustments).")]
        public ScriptableObject postProcessProfile;

        static NeonShaderLibrary _cached;
        static bool _searched;

        /// <summary>Charge la bibliothèque depuis Resources (null si absente).</summary>
        public static NeonShaderLibrary Load()
        {
            if (_cached == null && !_searched)
            {
                _cached = Resources.Load<NeonShaderLibrary>(ResourceName);
                _searched = true;
            }
            return _cached;
        }

        /// <summary>Force un nouveau chargement (après régénération des assets dans l'éditeur).</summary>
        public static void ResetCache()
        {
            _cached = null;
            _searched = false;
        }
    }
}
