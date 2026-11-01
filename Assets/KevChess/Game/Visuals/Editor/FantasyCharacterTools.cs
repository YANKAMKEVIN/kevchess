using System.IO;
using System.Reflection;
using KevChess.Game.Visuals.Fantasy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KevChess.Game.Visuals.Editor
{
    /// <summary>
    /// Outils éditeur des personnages du mode Légendes :
    /// - menu « Démo des personnages » : nouvelle scène avec <see cref="FantasyDemo"/>, puis Play ;
    /// - capture en PNG (menu ou ligne de commande) pour juger la lisibilité sans ouvrir l'éditeur :
    ///   Unity -batchmode -quit -projectPath . -buildTarget OSXUniversal
    ///         -executeMethod KevChess.Game.Visuals.Editor.FantasyCharacterTools.CaptureBatch
    ///   Images écrites dans Logs/fantasy-*.png.
    /// </summary>
    public static class FantasyCharacterTools
    {
        static readonly CharacterKind[] Lineup =
        {
            CharacterKind.Sentinel, CharacterKind.Knight, CharacterKind.Mage, CharacterKind.Golem,
            CharacterKind.Queen, CharacterKind.King, CharacterKind.Dragon,
        };

        [MenuItem("KevChess/Visuals/Démo des personnages (Légendes)")]
        static void OpenDemo()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            new GameObject("Fantasy Demo").AddComponent<FantasyDemo>();
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("KevChess/Visuals/Capturer les personnages (Légendes)")]
        static void CaptureMenu()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Capture();
        }

        /// <summary>Point d'entrée -executeMethod.</summary>
        public static void CaptureBatch()
        {
            Capture();
        }

        static void Capture()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            Directory.CreateDirectory(dir);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera cam = camGo.AddComponent<Camera>();
            var atmo = new GameObject("Atmosphere").AddComponent<SceneAtmosphere>();
            atmo.SetTargetCamera(cam);
            // Hors Play, Awake n'est pas appelé : on déclenche la mise en place à la main.
            typeof(SceneAtmosphere).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(atmo, null);
            BoardBuilder.Build();

            // Blancs en rangée 2 (z = -2,5), noirs en rangée 6 (z = 1,5) : chaque camp peut être
            // photographié de face sans que l'autre ne masque la vue.
            var white = new FantasyPieceView[7];
            var black = new FantasyPieceView[7];
            for (int i = 0; i < 7; i++)
            {
                white[i] = FantasyPieceView.Create(Lineup[i], true, i, 1, null);
                black[i] = FantasyPieceView.Create(Lineup[i], false, i, 5, null);
            }
            var log = new System.Text.StringBuilder("[KevChess] Personnages : ");
            foreach (CharacterKind k in Lineup)
                log.Append(k).Append(' ').Append(FantasyPieceFactory.TriangleCount(k)).Append(" tri / ")
                    .Append(white[(int)k].Rig.Renderers.Count).Append(" rend. ; ");
            Debug.Log(log.ToString());

            PoseAll(white, black, null, 0f);
            atmo.RefreshReflections();

            // 1. Vue de jeu en portrait (téléphone). Un premier rendu « à blanc » initialise le pipeline.
            cam.aspect = 1080f / 1920f;
            atmo.FrameBoard();
            Render(cam, 270, 480, null);
            Render(cam, 1080, 1920, Path.Combine(dir, "fantasy-board.png"));

            // 2. Noirs de face (3/4 plongeant).
            cam.fieldOfView = 58f;
            Place(cam, new Vector3(-0.5f, 1.9f, -1.4f), new Vector3(-0.5f, 0.45f, 1.5f));
            Render(cam, 1800, 1000, Path.Combine(dir, "fantasy-black-front.png"));

            // 3. Blancs de face.
            Place(cam, new Vector3(-0.5f, 1.9f, 0.4f), new Vector3(-0.5f, 0.45f, -2.5f));
            Render(cam, 1800, 1000, Path.Combine(dir, "fantasy-white-front.png"));

            // 4. Vue de dessus (blancs en bas, noirs en haut).
            cam.fieldOfView = 50f;
            Place(cam, new Vector3(-0.5f, 6.2f, -0.5f), new Vector3(-0.5f, 0f, -0.5f), Vector3.forward);
            Render(cam, 1600, 1300, Path.Combine(dir, "fantasy-top.png"));

            // 5. Profils : les blancs se tournent vers +X, caméra basse côté blancs.
            for (int i = 0; i < 7; i++)
            {
                white[i].FaceTowards(white[i].transform.position + Vector3.right, true);
                white[i].PreviewPose(2.1f + i * 0.4f);
            }
            cam.fieldOfView = 40f;
            Place(cam, new Vector3(-0.5f, 1.1f, -7.6f), new Vector3(-0.5f, 0.55f, -2.5f));
            Render(cam, 1800, 800, Path.Combine(dir, "fantasy-profile.png"));

            // 6. Poses de frappe (blancs à l'armé, noirs à l'impact) et statuts.
            white[5].SetShielded(true);
            black[4].SetFrozen(true);
            for (int i = 0; i < 7; i++)
            {
                StrikeKind s = CharacterKinds.StrikeOf(Lineup[i]);
                float impact = CharacterRig.ImpactTime(s);
                white[i].FaceDefault(true);
                white[i].PreviewPose(1.3f, s, impact * 0.75f);
                black[i].PreviewPose(1.3f, s, impact + 0.02f);
            }
            cam.fieldOfView = 58f;
            Place(cam, new Vector3(-0.5f, 1.9f, -1.4f), new Vector3(-0.5f, 0.45f, 1.5f));
            Render(cam, 1800, 1000, Path.Combine(dir, "fantasy-black-strikes.png"));
            Place(cam, new Vector3(-0.5f, 1.9f, 0.4f), new Vector3(-0.5f, 0.45f, -2.5f));
            Render(cam, 1800, 1000, Path.Combine(dir, "fantasy-white-strikes.png"));

            Debug.Log("[KevChess] Captures des personnages écrites dans " + dir);
        }

        static void PoseAll(FantasyPieceView[] white, FantasyPieceView[] black, StrikeKind? strike, float time)
        {
            for (int i = 0; i < 7; i++)
            {
                white[i].PreviewPose(1.3f + time + i * 0.37f, strike, 0f);
                black[i].PreviewPose(0.4f + time + i * 0.53f, strike, 0f);
            }
        }

        static void Place(Camera cam, Vector3 position, Vector3 lookAt, Vector3? up = null)
        {
            cam.transform.position = position;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - position, up ?? Vector3.up);
        }

        static void Render(Camera cam, int width, int height, string path)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            cam.aspect = width / (float)height;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            if (path != null) File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
