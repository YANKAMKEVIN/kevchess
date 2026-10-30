using System.IO;
using UnityEditor;
using UnityEngine;

namespace KevChess.Game.Visuals.Editor
{
    /// <summary>
    /// Génère (une seule fois, automatiquement) la <see cref="NeonShaderLibrary"/> et ses matériaux
    /// gabarits dans Assets/KevChess/Game/Visuals/Resources. Ces assets embarquent dans le build
    /// les shaders URP et les variantes de mots-clés que le code utilise, pour éviter le stripping.
    /// </summary>
    [InitializeOnLoad]
    public static partial class NeonAssetGenerator
    {
        const string Folder = "Assets/KevChess/Game/Visuals/Resources";
        const string TemplateFolder = Folder + "/NeonTemplates";
        const string LibraryPath = Folder + "/" + NeonShaderLibrary.ResourceName + ".asset";

        static NeonAssetGenerator()
        {
            EditorApplication.delayCall += EnsureAssets;
        }

        static void EnsureAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var lib = AssetDatabase.LoadAssetAtPath<NeonShaderLibrary>(LibraryPath);
            if (lib == null || lib.litOpaqueEmissive == null || lib.litTransparentEmissive == null ||
                lib.particleAdditive == null || lib.skyboxCubemap == null || lib.postProcessProfile == null)
                Generate(false);
        }

        [MenuItem("KevChess/Visuals/Régénérer les gabarits de matériaux (build)")]
        static void RegenerateMenu()
        {
            Generate(true);
        }

        /// <summary>Crée ou met à jour la bibliothèque et ses gabarits.</summary>
        public static void Generate(bool log)
        {
            Shader lit = Shader.Find(NeonMaterials.LitShaderName);
            Shader particles = Shader.Find(NeonMaterials.ParticleUnlitShaderName);
            Shader sky = Shader.Find(NeonMaterials.SkyboxShaderName);
            if (lit == null || particles == null || sky == null)
            {
                Debug.LogWarning("[KevChess] Shaders URP introuvables : la NeonShaderLibrary sera générée quand URP sera installé.");
                return;
            }

            Directory.CreateDirectory(TemplateFolder);
            AssetDatabase.Refresh();

            var lib = AssetDatabase.LoadAssetAtPath<NeonShaderLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<NeonShaderLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            lib.litOpaqueEmissive = Template("LitOpaqueEmissive", lit, NeonMaterials.ConfigureLitOpaqueEmissive);
            lib.litTransparentEmissive = Template("LitTransparentEmissive", lit, NeonMaterials.ConfigureLitTransparentEmissive);
            lib.particleAdditive = Template("ParticleAdditive", particles, NeonMaterials.ConfigureParticleAdditive);
            lib.skyboxCubemap = Template("SkyboxCubemap", sky, m => { });

            ScriptableObject profile = null;
            CreatePostProcessProfile(Folder, ref profile);
            if (profile != null) lib.postProcessProfile = profile;

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            NeonShaderLibrary.ResetCache();
            if (log) Debug.Log("[KevChess] NeonShaderLibrary générée : " + LibraryPath);
        }

        static Material Template(string name, Shader shader, System.Action<Material> configure)
        {
            string path = TemplateFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                configure(mat);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
                configure(mat);
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        /// <summary>Implémenté dans NeonAssetGenerator.Urp.cs (crée le VolumeProfile asset).</summary>
        static partial void CreatePostProcessProfile(string folder, ref ScriptableObject profile);

        // ------------------------------------------------------------------ brouillard

        [MenuItem("KevChess/Visuals/Conserver les variantes de brouillard (Graphics Settings)")]
        public static void KeepFogVariants()
        {
            // Le brouillard est activé par code : sans cela, ses variantes peuvent être strippées
            // si aucune scène du build n'a le brouillard coché dans Lighting Settings.
            Object gs = UnityEngine.Rendering.GraphicsSettings.GetGraphicsSettings();
            var so = new SerializedObject(gs);
            SerializedProperty stripping = so.FindProperty("m_FogStripping");
            SerializedProperty exp2 = so.FindProperty("m_FogKeepExp2");
            if (stripping == null || exp2 == null)
            {
                Debug.LogWarning("[KevChess] Propriétés de brouillard introuvables : réglez à la main Project Settings > Graphics > Shader Stripping > Fog Modes = Custom + Exponential Squared.");
                return;
            }
            stripping.intValue = 1; // Custom
            exp2.boolValue = true;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[KevChess] Fog Modes = Custom, Exponential Squared conservé.");
        }
    }
}
