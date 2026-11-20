using System.Collections.Generic;
using KevChess.Core;
using KevChess.Core.Ux;
using KevChess.Game.Audio;
using KevChess.Game.UI;
using KevChess.Game.View;
using UnityEngine;

namespace KevChess.Game.Match
{
    /// <summary>
    /// Sélection au toucher : toucher une pièce, puis une case en surbrillance.
    /// Toucher une autre pièce de son camp change la sélection. On peut aussi glisser-déposer la pièce.
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
        int dragFrom = Square.None;
        bool dragStarted;

        public BoardInteraction(MatchController match, BoardView board, Hud hud, SoundBank sfx, Camera camera)
        {
            this.match = match;
            this.board = board;
            this.hud = hud;
            this.sfx = sfx;
            this.camera = camera;
            match.StateChanged += () => { if (!match.CanHumanAct) ClearSelection(); };
        }

        // ---------------------------------------------------------------- Glisser-déposer

        /// <summary>Un appui commence-t-il sur une pièce du camp au trait (glisser possible) ?</summary>
        public bool CanStartDrag(Vector2 screenPos)
        {
            dragFrom = Square.None;
            dragStarted = false;
            if (awaitingPromotion || hud.IsBlockingOverlayOpen || !match.CanHumanAct) return false;
            if (!board.TryPick(camera.ScreenPointToRay(screenPos), out int pieceSquare, out int boardSquare)) return false;
            int square = pieceSquare != Square.None ? pieceSquare : boardSquare;
            if (square == Square.None || !Piece.IsColor(match.Game.Position[square], match.Game.SideToMove)) return false;
            dragFrom = square;
            return true;
        }

        public void OnDrag(Vector2 screenPos)
        {
            if (dragFrom == Square.None || !match.CanHumanAct) return;
            if (!dragStarted)
            {
                dragStarted = true;
                if (selected != dragFrom) Select(dragFrom);
            }
            board.DragPiece(dragFrom, camera.ScreenPointToRay(screenPos));
        }

        public void OnDrop(Vector2 screenPos)
        {
            int from = dragFrom;
            dragFrom = Square.None;
            if (from == Square.None || !dragStarted) return;
            dragStarted = false;

            int to = BoardView.SquareUnder(camera.ScreenPointToRay(screenPos));
            if (match.CanHumanAct && TryFindTarget(to, out var move))
            {
                Play(move);
                return;
            }
            // Lâcher hors cible : retour sur la case d'origine, la sélection reste (on peut encore toucher une case).
            board.ReturnPiece(from);
            if (to != from && to != Square.None)
            {
                sfx.Play(Sfx.Illegal, 0.6f);
                Haptics.Illegal();
                ExplainRefusal(from, to);
            }
        }

        public void OnDragCancelled()
        {
            if (dragFrom != Square.None && dragStarted) board.ReturnPiece(dragFrom);
            dragFrom = Square.None;
            dragStarted = false;
        }

        // ---------------------------------------------------------------- Toucher

        /// <summary>Traite un toucher ; renvoie vrai s'il a visé le plateau ou une pièce.</summary>
        public bool OnTap(Vector2 screenPos)
        {
            if (awaitingPromotion || hud.IsBlockingOverlayOpen) return true;
            if (!board.TryPick(camera.ScreenPointToRay(screenPos), out int pieceSquare, out int boardSquare))
            {
                ClearSelection();
                return false;
            }
            if (!match.CanHumanAct) return true;

            // Pièce touchée prenable : on la prend. Sinon une pièce haute peut masquer la case visée
            // derrière elle : la case sous le rayon, si elle est une cible légale, l'emporte.
            if (selected != Square.None)
            {
                if (TryFindTarget(pieceSquare, out var m1)) { Play(m1); return true; }
                if (TryFindTarget(boardSquare, out var m2)) { Play(m2); return true; }
            }

            var game = match.Game;
            int square = pieceSquare != Square.None ? pieceSquare : boardSquare;
            if (Piece.IsColor(game.Position[square], game.SideToMove))
            {
                if (square == selected) ClearSelection();
                else Select(square);
                return true;
            }

            if (selected != Square.None)
            {
                sfx.Play(Sfx.Illegal, 0.6f);
                Haptics.Illegal();
                ExplainRefusal(selected, square);
            }
            ClearSelection();
            return true;
        }

        /// <summary>Coup refusé : dit pourquoi (échec, pièce clouée, case attaquée) et fait clignoter le roi et ses agresseurs.</summary>
        void ExplainRefusal(int from, int to)
        {
            var refusal = MoveRefusal.Explain(match.Game.Position, from, to);
            if (refusal.Reason == RefusalReason.None) return;
            var squares = new List<int>(refusal.Attackers) { refusal.KingSquare };
            board.FlashSquares(squares);
            hud.Toast(refusal.Message, UIKit.Danger);
        }

        void Play(Move move)
        {
            int from = selected;
            ClearSelection();
            if (move.IsPromotion) AskPromotion(from, move.To);
            else match.TryHumanMove(from, move.To);
        }

        bool TryFindTarget(int square, out Move move)
        {
            move = Move.Null;
            if (square == Square.None) return false;
            foreach (var m in targets)
                if (m.To == square) { move = m; return true; }
            return false;
        }

        void Select(int square)
        {
            selected = square;
            targets.Clear();
            targets.AddRange(match.Game.LegalMovesFrom(square));
            board.ShowSelection(square, targets);
            sfx.Play(Sfx.Select, 0.7f);
            Haptics.Select();
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
                if (type == PieceType.None || !match.TryHumanMove(from, to, type)) board.ReturnPiece(from);
            }, to);
        }
    }
}
