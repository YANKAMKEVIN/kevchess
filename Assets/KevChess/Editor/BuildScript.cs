using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace KevChess.EditorTools
{
    /// <summary>
    /// Configuration du projet et builds en ligne de commande :
    ///   Unity -batchmode -quit -projectPath . -executeMethod KevChess.EditorTools.BuildScript.BuildAndroid
    ///   Unity -batchmode -quit -projectPath . -executeMethod KevChess.EditorTools.BuildScript.BuildMac
    /// </summary>
    public static class BuildScript
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string BundleId = "com.kevinyankam.kevchess";

        [MenuItem("KevChess/Configurer le projet")]
        public static void Setup()
        {
            EnsureScene();
            ConfigurePlayer();
            ConfigureRendering();
            AssetDatabase.SaveAssets();
            Debug.Log("[KevChess] Projet configuré");
        }

        [MenuItem("KevChess/Build Android (APK)")]
        public static void BuildAndroid()
        {
            Setup();
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Build(BuildTarget.Android, "Builds/Android/KevChess.apk");
        }

        [MenuItem("KevChess/Build macOS")]
        public static void BuildMac()
        {
            Setup();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
            Build(BuildTarget.StandaloneOSX, "Builds/macOS/KevChess.app");
        }

        static void Build(BuildTarget target, string output)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = target,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[KevChess] Build {target} : {s.result}, {s.totalErrors} erreur(s), {s.totalSize / (1024 * 1024)} Mo -> {output}");
            if (Application.isBatchMode && s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }

        /// <summary>La scène est vide : KevChessApp construit tout au lancement.</summary>
        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var app = new GameObject("KevChess");
                app.AddComponent<KevChess.Game.App.KevChessApp>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Kevin Yankam";
            PlayerSettings.productName = "KevChess";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleId);
            PlayerSettings.Android.bundleVersionCode = Math.Max(1, PlayerSettings.Android.bundleVersionCode);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 1000;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            // Écran de démarrage Unity : obligatoire en licence Personal, on garde le logo seul.
            PlayerSettings.SplashScreen.showUnityLogo = true;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.02f, 0.02f, 0.05f);
        }

        /// <summary>Ajuste les assets URP existants (projet créé depuis le modèle URP) pour la DA néon.</summary>
        static void ConfigureRendering()
        {
            var assets = new System.Collections.Generic.List<UniversalRenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset main) assets.Add(main);
            for (int i = 0; i < QualitySettings.names.Length; i++)
                if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset q && !assets.Contains(q)) assets.Add(q);

            if (assets.Count == 0)
            {
                Debug.LogError("[KevChess] Aucun asset URP : créer le projet depuis le modèle URP.");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }

            foreach (var a in assets)
            {
                a.supportsHDR = true;           // indispensable au bloom néon
                a.msaaSampleCount = 4;
                a.shadowDistance = 30f;
                a.supportsSoftShadows = true;
                a.renderScale = 1f;
                EditorUtility.SetDirty(a);
            }
            VisualsBuildSupport.Apply();
        }
    }
}
