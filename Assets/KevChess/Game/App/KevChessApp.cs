using KevChess.Core;
using KevChess.Game.Audio;
using KevChess.Game.CameraRig;
using KevChess.Game.Input;
using KevChess.Game.Match;
using KevChess.Game.UI;
using KevChess.Game.View;
using KevChess.Game.Visuals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace KevChess.Game.App
{
    /// <summary>
    /// Point d'entrée : construit caméra, ambiance, plateau, interface et branche les événements.
    /// La scène peut être vide : l'app se crée toute seule au chargement.
    /// </summary>
    public sealed class KevChessApp : MonoBehaviour
    {
        Camera mainCamera;
        OrbitCamera orbit;
        BoardView board;
        MatchController match;
        Hud hud;
        SoundBank sfx;
        PointerInput pointer;
        BoardInteraction interaction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<KevChessApp>() == null) new GameObject("KevChess").AddComponent<KevChessApp>();
        }

        void Awake()
        {
            if (Application.isMobilePlatform)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
            }
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            BuildCamera();
            BuildWorld();

            sfx = new GameObject("Sound").AddComponent<SoundBank>();
            sfx.transform.SetParent(transform, false);

            match = gameObject.AddComponent<MatchController>();
            match.Init(board);

            BuildEventSystem();
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Build(match, orbit, sfx, board.ShowHint);

            interaction = new BoardInteraction(match, board, hud, sfx, mainCamera);

            pointer = gameObject.AddComponent<PointerInput>();
            pointer.Tapped += interaction.OnTap;
            pointer.Dragged += d => { if (!hud.IsBlockingOverlayOpen) orbit.Orbit(d); };
            pointer.Zoomed += f => { if (!hud.IsBlockingOverlayOpen) orbit.Zoom(f); };
            pointer.DoubleTapped += _ => { if (!hud.IsBlockingOverlayOpen) hud.ResetCamera(); };

            match.MoveStarted += OnMoveStarted;
            match.MoveLanded += OnMoveLanded;
        }

        void Start()
        {
            // Reprend la partie interrompue si elle existe, sinon plateau prêt pour une partie à deux.
            if (!match.ResumeSaved()) match.StartNew(new MatchSettings { Mode = MatchMode.LocalTwoPlayers });
            orbit.SnapToView(match.ViewSide);
            hud.ShowMenu(true);
        }

        void BuildCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.SetParent(transform, false);
            mainCamera = go.AddComponent<Camera>();
            mainCamera.clearFlags = CameraClearFlags.Skybox;
            mainCamera.backgroundColor = new Color(0.01f, 0.01f, 0.03f);
            mainCamera.allowHDR = true;
            mainCamera.allowMSAA = true;
            go.AddComponent<AudioListener>();
            orbit = go.AddComponent<OrbitCamera>();
        }

        void BuildWorld()
        {
            var boardGo = new GameObject("Board");
            boardGo.transform.SetParent(transform, false);
            board = boardGo.AddComponent<BoardView>();
            board.Build();

            // Ambiance néon : lumières, ciel, brouillard, reflets, post-traitement.
            // L'orbite gère la caméra : on coupe le cadrage automatique avant Awake.
            var atmoGo = new GameObject("Atmosphere");
            atmoGo.SetActive(false);
            atmoGo.transform.SetParent(transform, false);
            var atmosphere = atmoGo.AddComponent<SceneAtmosphere>();
            atmosphere.ManageCameraFraming = false;
            atmosphere.SetTargetCamera(mainCamera);
            atmoGo.SetActive(true);
        }

        static void BuildEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        void Update()
        {
            // Bouton retour Android / Échap : ouvre ou ferme le menu.
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                hud.CancelPromotion();
                hud.ShowMenu(!hud.IsBlockingOverlayOpen);
            }
        }

        void OnMoveStarted(MoveVisual v)
        {
            interaction.ClearSelection();
        }

        void OnMoveLanded(MoveVisual v)
        {
            if (v.IsPromotion) sfx.Play(Sfx.Promote);
            else if (v.IsCastle) sfx.Play(Sfx.Castle);
            else if (v.IsCapture) sfx.Play(Sfx.Capture);
            else sfx.Play(Sfx.Move);

            if (match.Game.InCheck && !match.Game.IsOver) sfx.Play(Sfx.Check, 0.8f);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && match != null && match.Game != null && !match.Game.IsOver)
                SavedMatch.Store(match.Settings, match.Game);
        }
    }
}
