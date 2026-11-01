using System;
using KevChess.Core.Fantasy;
using KevChess.Game.Audio;
using KevChess.Game.CameraRig;
using KevChess.Game.Match;
using UnityEngine;

namespace KevChess.Game.Legends
{
    /// <summary>
    /// Le mode Légendes complet (plateau, partie, interface, interaction), construit à la demande à côté
    /// du mode classique. KevChessApp lui passe les touchers tant qu'il est actif.
    /// </summary>
    public sealed class LegendsMode : MonoBehaviour
    {
        Camera mainCamera;
        OrbitCamera orbit;
        SoundBank sfx;
        Action onExit;

        LegendsBoardView board;
        LegendsController match;
        LegendsHud hud;
        LegendsInteraction interaction;
        bool lastTapHitBoard;

        public bool Active { get; private set; }
        public LegendsController Controller => match;
        public LegendsHud Hud => hud;

        public void Init(Camera camera, OrbitCamera orbitCamera, SoundBank sounds, Action exitToClassic)
        {
            mainCamera = camera;
            orbit = orbitCamera;
            sfx = sounds;
            onExit = exitToClassic;

            var boardGo = new GameObject("LegendsBoard");
            boardGo.transform.SetParent(transform, false);
            board = boardGo.AddComponent<LegendsBoardView>();
            board.Build(mainCamera, orbit);

            match = gameObject.AddComponent<LegendsController>();
            match.Init(board);

            var hudGo = new GameObject("LegendsHud");
            hudGo.transform.SetParent(transform, false);
            hud = hudGo.AddComponent<LegendsHud>();
            interaction = new LegendsInteraction(match, board, sfx, mainCamera, () => hud.IsBlockingOverlayOpen,
                (from, to, options) => hud.AskPromotion(from, to, options));
            hud.Build(match, interaction, orbit, sfx, Exit, board.ShowHint);

            match.ActionStarted += _ => interaction.ClearSelection();
            match.ActionLanded += OnLanded;
            gameObject.SetActive(false);
        }

        /// <summary>Entre dans le mode : reprend la partie sauvegardée s'il y en a une, sinon montre l'écran de réglages.</summary>
        public void Enter()
        {
            gameObject.SetActive(true);
            Active = true;
            if (match.Game == null && !match.ResumeSaved())
                match.StartNew(new MatchSettings { Mode = MatchMode.VersusAI });
            orbit.SnapToView(match.Settings.Mode == MatchMode.LocalTwoPlayers ? match.Game.SideToMove : match.ViewSide);
            hud.ShowSetup(true);
            hud.Refresh();
        }

        void Exit()
        {
            match.Suspend();
            interaction.Reset();
            Active = false;
            gameObject.SetActive(false);
            Time.timeScale = 1f;
            onExit?.Invoke();
        }

        // ---------------------------------------------------------------- touchers relayés par l'app

        public void OnTap(Vector2 p)
        {
            if (board.IsPlayingDuel) { board.SkipDuel(); return; }
            lastTapHitBoard = interaction.OnTap(p);
        }

        public bool CanStartDrag(Vector2 p) => interaction.CanStartDrag(p);
        public void OnDrag(Vector2 p) => interaction.OnDrag(p);
        public void OnDrop(Vector2 p) => interaction.OnDrop(p);
        public void OnDragCancelled() => interaction.OnDragCancelled();
        public bool BlocksCamera => hud.IsBlockingOverlayOpen;
        public void OnDoubleTap() { if (!hud.IsBlockingOverlayOpen && !lastTapHitBoard) hud.ResetCamera(); }
        public void HandleBack() => hud.HandleBack();

        void OnLanded(ActionVisual v)
        {
            if (v.IsPromotion) sfx.Play(Sfx.Promote);
            else if (v.Action.Power == PowerKind.Summon) sfx.Play(Sfx.Promote, 1f);
            else if (v.IsCastle) sfx.Play(Sfx.Castle);
            else if (v.IsCapture) sfx.Play(Sfx.Capture);
            else sfx.Play(Sfx.Move);

            if (match.Game.InCheck && !match.Game.IsOver)
            {
                sfx.Play(Sfx.Check, 0.8f);
                Haptics.Check();
            }
            else if (v.IsCapture || v.FallenSquares.Length > 0) Haptics.Capture();
            else Haptics.Land();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && Active && match.Game != null && !match.Game.IsOver) match.StoreNow();
        }
    }
}
