using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals.Editor
{
    // Partie dépendante d'URP : création du VolumeProfile asset (sous-assets inclus).
    // Embarqué via la NeonShaderLibrary, il évite aussi que « Strip Unused Post Processing Variants »
    // ne retire les variantes Bloom / Tonemapping utilisées seulement par le profil runtime.
    public static partial class NeonAssetGenerator
    {
        static partial void CreatePostProcessProfile(string folder, ref ScriptableObject profile)
        {
            string path = folder + "/KevChessNeonVolumeProfile.asset";
            var asset = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(asset, path);
            }

            NeonPostProcessing.Configure(asset);

            // Les VolumeComponent doivent être des sous-assets du profil pour être sérialisés.
            foreach (VolumeComponent component in asset.components)
            {
                if (component == null) continue;
                if (!AssetDatabase.Contains(component))
                {
                    component.name = component.GetType().Name;
                    component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(component, asset);
                }
                EditorUtility.SetDirty(component);
            }
            EditorUtility.SetDirty(asset);
            profile = asset;
        }
    }
}
