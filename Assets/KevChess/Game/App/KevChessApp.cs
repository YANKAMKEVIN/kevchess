using KevChess.Core;
using KevChess.Game.Audio;
using KevChess.Game.CameraRig;
using KevChess.Game.Input;
using KevChess.Game.Legends;
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
        AmbientMusic music;
        PointerInput pointer;
        BoardInteraction interaction;
        LegendsMode legends;
        bool lastTapHitBoard;

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

            DisableForeignSceneObjects();
            BuildCamera();
            BuildWorld();

            sfx = new GameObject("Sound").AddComponent<SoundBank>();
            sfx.transform.SetParent(transform, false);
            music = AmbientMusic.Create(transform);

            // Réglages enregistrés : thème, lisibilité, vitesse des animations.
            GamePrefs.ApplyAll();

            board.TraysOnXSides = () => orbit.TraysOnXSides;

            match = gameObject.AddComponent<MatchController>();
            match.Init(board);

            BuildEventSystem();
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.transform.SetParent(transform, false);
            hud.Build(match, orbit, sfx, board, ShowHint);
            hud.SetMusic(music);

            interaction = new BoardInteraction(match, board, hud, sfx, mainCamera);

            // Mode Légendes : construit à côté, il reçoit les touchers quand il est actif.
            var legendsGo = new GameObject("Legends");
            legendsGo.transform.SetParent(transform, false);
            legends = legendsGo.AddComponent<LegendsMode>();
            legends.Init(mainCamera, orbit, sfx, ExitLegends);
            hud.LegendsRequested += EnterLegends;

            pointer = gameObject.AddComponent<PointerInput>();
            pointer.Tapped += p =>
            {
                if (legends.Active) legends.OnTap(p);
                else lastTapHitBoard = interaction.OnTap(p);
            };
            pointer.PieceGestureFilter = p => legends.Active ? legends.CanStartDrag(p) : interaction.CanStartDrag(p);
            pointer.PieceDragged += p => { if (legends.Active) legends.OnDrag(p); else interaction.OnDrag(p); };
            pointer.PieceDropped += p => { if (legends.Active) legends.OnDrop(p); else interaction.OnDrop(p); };
            pointer.PieceDragCancelled += () => { if (legends.Active) legends.OnDragCancelled(); else interaction.OnDragCancelled(); };
            pointer.Dragged += d =>
            {
                if (CameraBlocked) return;
                orbit.Orbit(d);
                if (!legends.Active) hud.NotifyCameraOrbited(d);
            };
            pointer.Zoomed += f => { if (!CameraBlocked) orbit.Zoom(f); };
            // Double-tap hors plateau : recentrer (sur le plateau, les touchers servent à jouer).
            pointer.DoubleTapped += _ =>
            {
                if (legends.Active) legends.OnDoubleTap();
                else if (!hud.IsBlockingOverlayOpen && !lastTapHitBoard) hud.ResetCamera();
            };

            match.MoveStarted += OnMoveStarted;
            match.MoveLanded += OnMoveLanded;
        }

        void Start()
        {
            // Reprend la partie interrompue si elle existe, sinon plateau prêt pour une partie à deux.
            if (!match.ResumeSaved()) match.StartNew(new MatchSettings { Mode = MatchMode.LocalTwoPlayers });
            orbit.SnapToView(match.Settings.Mode == MatchMode.LocalTwoPlayers ? match.Game.SideToMove : match.ViewSide);
            hud.ShowMenu(true);

            if (AIBench.Requested()) AIBench.Run();
            if (AutoTest.Requested(out var folder)) gameObject.AddComponent<AutoTest>().Run(match, hud, folder);
            if (LegendsAutoTest.Requested(out var legendsFolder)) gameObject.AddComponent<LegendsAutoTest>().Run(legends, EnterLegends, legendsFolder);
        }

        bool CameraBlocked => legends.Active ? legends.BlocksCamera : hud.IsBlockingOverlayOpen;

        void EnterLegends()
        {
            match.Paused = true;
            interaction.ClearSelection();
            board.gameObject.SetActive(false);
            hud.gameObject.SetActive(false);
            legends.Enter();
        }

        void ExitLegends()
        {
            board.gameObject.SetActive(true);
            hud.gameObject.SetActive(true);
            orbit.SnapToView(match.Settings.Mode == MatchMode.LocalTwoPlayers ? match.Game.SideToMove : match.ViewSide);
            hud.ShowMenu(true);
        }

        /// <summary>Lancé depuis une autre scène (Main Camera, Directional Light…) : on coupe ce qui doublonnerait.</summary>
        void DisableForeignSceneObjects()
        {
            foreach (var cam in FindObjectsByType<Camera>())
                if (!cam.transform.IsChildOf(transform)) cam.gameObject.SetActive(false);
            foreach (var light in FindObjectsByType<Light>())
                if (!light.transform.IsChildOf(transform)) light.enabled = false;
            foreach (var listener in FindObjectsByType<AudioListener>())
                if (!listener.transform.IsChildOf(transform)) listener.enabled = false;
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
                if (legends.Active) legends.HandleBack();
                else hud.HandleBack();
            }
        }

        void ShowHint(Move move)
        {
            interaction.ClearSelection();
            board.ShowHint(move);
        }

        void OnMoveStarted(MoveVisual v)
        {
            interaction.ClearSelection();
        }

        void OnMoveLanded(MoveVisual v)
        {
            bool mate = match.Game.EndReason == GameEndReason.Checkmate;
            sfx.PlayForMove(v.IsCapture, v.IsCastle, v.IsPromotion, match.Game.InCheck, mate);

            if (match.Game.InCheck && !match.Game.IsOver) Haptics.Check();
            else if (v.IsCapture) Haptics.Capture();
            else Haptics.Land();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && match != null && match.Game != null && !match.Game.IsOver)
                SavedMatch.Store(match.Settings, match.Game, match.Clock);
        }
    }
}
