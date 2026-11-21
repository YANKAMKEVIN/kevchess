#if UNITY_ANDROID
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace KevChess.EditorTools
{
    /// <summary>
    /// Stockfish est livré comme « bibliothèque native » (Assets/Plugins/Android/libs/arm64-v8a/libstockfish.so) mais
    /// c'est un exécutable : l'app le lance depuis nativeLibraryDir (AIAdapter, AndroidUciTransport). Android n'y
    /// extrait les bibliothèques que si l'APK les garde compressées (« legacy packaging ») ; sinon elles restent dans
    /// l'APK et le dossier est vide. Après la génération du projet Gradle, on force donc
    /// packagingOptions.jniLibs.useLegacyPackaging = true dans le module launcher (équivalent de
    /// android:extractNativeLibs="true"). Sans effet si le réglage est déjà présent.
    /// </summary>
    public sealed class StockfishAndroidPackaging : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            var launcher = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(unityLibraryPath)) ?? "", "launcher");
            var groovy = Path.Combine(launcher, "build.gradle");
            var kotlin = Path.Combine(launcher, "build.gradle.kts");
            var file = File.Exists(groovy) ? groovy : File.Exists(kotlin) ? kotlin : null;
            if (file == null)
            {
                Debug.LogWarning("[KevChess] build.gradle du launcher introuvable : Stockfish ne sera peut-être pas extrait (" + launcher + ")");
                return;
            }
            var text = File.ReadAllText(file);
            var patched = EnsureLegacyJniPackaging(text, file.EndsWith(".kts"));
            if (patched != text)
            {
                File.WriteAllText(file, patched);
                Debug.Log("[KevChess] useLegacyPackaging activé pour extraire libstockfish.so (" + file + ")");
            }
        }

        /// <summary>Met useLegacyPackaging à true (remplace un false, ou ajoute un bloc android à la fin).</summary>
        public static string EnsureLegacyJniPackaging(string gradle, bool kotlinDsl)
        {
            var existing = new Regex(@"useLegacyPackaging\s*(=\s*)?(true|false)");
            var m = existing.Match(gradle);
            if (m.Success)
                return m.Groups[2].Value == "true" ? gradle : gradle.Substring(0, m.Groups[2].Index) + "true" + gradle.Substring(m.Groups[2].Index + 5);
            var block = kotlinDsl
                ? "\n// KevChess : Stockfish lancé depuis nativeLibraryDir (StockfishAndroidPackaging)\nandroid {\n    packaging {\n        jniLibs {\n            useLegacyPackaging = true\n        }\n    }\n}\n"
                : "\n// KevChess : Stockfish lancé depuis nativeLibraryDir (StockfishAndroidPackaging)\nandroid {\n    packagingOptions {\n        jniLibs {\n            useLegacyPackaging true\n        }\n    }\n}\n";
            return gradle.TrimEnd('\n') + "\n" + block;
        }
    }
}
#endif
