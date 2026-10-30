using System.Collections.Generic;
using KevChess.Core;
using KevChess.Game.Audio;
using KevChess.Game.UI;
using KevChess.Game.View;
using UnityEngine;

namespace KevChess.Game.Match
{
    /// <summary>
    /// Sélection au toucher : toucher une pièce, puis une case en surbrillance.
    /// Toucher une autre pièce de son camp change la sélection.
    /// </summary>
    public sealed class BoardInteraction
    {
        readonly MatchController match;
        readonly BoardView board;
        readonly Hud hud;
        readonly SoundBank sfx;
        readonly Camera camera;

        int selected = Square.None;
        readonly List<Move> targets = new List<Move>();
        bool awaitingPromotion;

        public BoardInteraction(MatchController match, BoardView board, Hud hud, SoundBank sfx, Camera camera)
        {
            this.match = match;
            this.board = board;
            this.hud = hud;
            this.sfx = sfx;
            this.camera = camera;
            match.StateChanged += () => { if (!match.CanHumanAct) ClearSelection(); };
        }

        public void OnTap(Vector2 screenPos)
        {
            if (awaitingPromotion || hud.IsBlockingOverlayOpen) return;
            if (!match.CanHumanAct) return;

            if (!board.TryPickSquare(camera.ScreenPointToRay(screenPos), out int square))
            {
                ClearSelection();
                return;
            }

            var game = match.Game;
            int piece = game.Position[square];

            if (selected != Square.None && TryFindTarget(square, out var move))
            {
                int from = selected;
                ClearSelection();
                if (move.IsPromotion) AskPromotion(from, square);
                else match.TryHumanMove(from, square);
                return;
            }

            if (Piece.IsColor(piece, game.SideToMove))
            {
                if (square == selected) { ClearSelection(); return; }
                Select(square);
                return;
            }

            if (selected != Square.None) sfx.Play(Sfx.Illegal, 0.6f);
            ClearSelection();
        }

        bool TryFindTarget(int square, out Move move)
        {
            foreach (var m in targets)
                if (m.To == square) { move = m; return true; }
            move = Move.Null;
            return false;
        }

        void Select(int square)
        {
            selected = square;
            targets.Clear();
            targets.AddRange(match.Game.LegalMovesFrom(square));
            board.ShowSelection(square, targets);
            sfx.Play(Sfx.Select, 0.7f);
        }

        public void ClearSelection()
        {
            if (selected == Square.None && targets.Count == 0) return;
            selected = Square.None;
            targets.Clear();
            board.ClearSelection();
        }

        void AskPromotion(int from, int to)
        {
            awaitingPromotion = true;
            board.ShowPendingMove(from, to);
            hud.AskPromotion(type =>
            {
                awaitingPromotion = false;
                board.ClearSelection();
                if (type != PieceType.None) match.TryHumanMove(from, to, type);
            });
        }
    }
}
