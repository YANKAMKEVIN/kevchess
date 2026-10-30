using System.Collections;
using System.Collections.Generic;
using KevChess.Core;
using KevChess.Game.Match;
using KevChess.Game.Visuals;
using UnityEngine;

namespace KevChess.Game.View
{
    /// <summary>
    /// Plateau 3D synchronisé avec la partie : pièces, animations de coups, surbrillances et sélection au rayon.
    /// </summary>
    public sealed class BoardView : MonoBehaviour, IBoardPresenter
    {
        const float AnimationTimeout = 4f;
        const float HintSeconds = 2.8f;

        readonly PieceView[] pieces = new PieceView[64];
        BoardHighlights highlights;
        Transform piecesRoot;
        Coroutine hintRoutine;
        int selectedSquare = Square.None;

        public void Build()
        {
            BoardBuilder.Build(transform);
            highlights = BoardHighlights.Create(transform);
            piecesRoot = new GameObject("Pieces").transform;
            piecesRoot.SetParent(transform, false);
        }

        // ---------------------------------------------------------------- IBoardPresenter

        public void ShowPosition(Position position, PieceColor viewSide)
        {
            StopHint();
            for (int sq = 0; sq < 64; sq++)
            {
                if (pieces[sq] != null) Destroy(pieces[sq].gameObject);
                pieces[sq] = null;
            }
            // Les pièces en cours de capture (hors tableau) se détruisent d'elles-mêmes.
            for (int sq = 0; sq < 64; sq++)
            {
                int p = position[sq];
                if (p != Piece.Empty) pieces[sq] = Spawn(p, sq);
            }
            highlights.ClearAll();
            selectedSquare = Square.None;
        }

        public IEnumerator AnimateMove(MoveVisual v)
        {
            StopHint();
            ClearSelection();
            highlights.ClearCheck();

            var move = v.Move;
            var mover = pieces[move.From];
            PieceView victim = v.IsCapture ? pieces[v.CaptureSquare] : null;
            pieces[move.From] = null;
            if (v.IsCapture) pieces[v.CaptureSquare] = null;

            if (mover == null)
            {
                // Vue désynchronisée : on repart de l'état exact plutôt que d'animer faux.
                Debug.LogWarning("BoardView : pièce absente en " + Square.Name(move.From));
                yield break;
            }
            pieces[move.To] = mover;

            highlights.ShowLastMove(Square.File(move.From), Square.Rank(move.From), Square.File(move.To), Square.Rank(move.To));

            bool landed = false;
            mover.MoveTo(Square.File(move.To), Square.Rank(move.To), () =>
            {
                landed = true;
                if (victim != null) victim.Capture();
            });

            PieceView rook = null;
            if (v.IsCastle)
            {
                rook = pieces[v.RookFrom];
                pieces[v.RookFrom] = null;
                pieces[v.RookTo] = rook;
                yield return new WaitForSeconds(0.12f);
                if (rook != null) rook.MoveTo(Square.File(v.RookTo), Square.Rank(v.RookTo));
            }

            float t = 0f;
            while ((!landed || mover.IsAnimating || (rook != null && rook.IsAnimating)) && t < AnimationTimeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (!landed && victim != null) victim.Capture();

            if (v.IsPromotion)
            {
                bool done = false;
                mover.Promote(ToKind(Piece.TypeOf(v.PromotedPiece)), () => done = true);
                t = 0f;
                while (!done && t < AnimationTimeout) { t += Time.deltaTime; yield return null; }
            }

            // Filet de sécurité : position finale exacte.
            if (mover != null) mover.SnapTo(Square.File(move.To), Square.Rank(move.To));
            if (rook != null) rook.SnapTo(Square.File(v.RookTo), Square.Rank(v.RookTo));
        }

        public void ShowState(ChessGame game)
        {
            var last = game.LastMove;
            if (last.IsNull) highlights.ClearLastMove();
            else highlights.ShowLastMove(Square.File(last.From), Square.Rank(last.From), Square.File(last.To), Square.Rank(last.To));

            if (game.InCheck)
            {
                int k = game.Position.KingSquare(game.SideToMove);
                highlights.ShowCheck(Square.File(k), Square.Rank(k));
            }
            else highlights.ClearCheck();
        }

        // ---------------------------------------------------------------- Sélection

        public void ShowSelection(int square, IReadOnlyList<Move> targets)
        {
            StopHint();
            ClearSelection();
            selectedSquare = square;
            if (pieces[square] != null) pieces[square].SetSelected(true);
            highlights.ShowSelection(Square.File(square), Square.Rank(square));

            var quiet = new List<Vector2Int>();
            var captures = new List<Vector2Int>();
            var seen = new HashSet<int>();
            foreach (var m in targets)
            {
                if (!seen.Add(m.To)) continue; // promotions : 4 coups vers la même case
                var cell = new Vector2Int(Square.File(m.To), Square.Rank(m.To));
                if (m.IsCapture) captures.Add(cell);
                else quiet.Add(cell);
            }
            highlights.ShowLegalMoves(quiet, captures);
        }

        /// <summary>Promotion en attente : garde la pièce et la case d'arrivée marquées.</summary>
        public void ShowPendingMove(int from, int to)
        {
            highlights.ShowSelection(Square.File(from), Square.Rank(from));
            highlights.ShowLegalMoves(new[] { new Vector2Int(Square.File(to), Square.Rank(to)) }, new Vector2Int[0]);
        }

        public void ClearSelection()
        {
            if (selectedSquare != Square.None && pieces[selectedSquare] != null) pieces[selectedSquare].SetSelected(false);
            selectedSquare = Square.None;
            highlights.ClearSelection();
            highlights.ClearLegalMoves();
        }

        public void ShowHint(Move move)
        {
            StopHint();
            hintRoutine = StartCoroutine(HintRoutine(move));
        }

        IEnumerator HintRoutine(Move move)
        {
            ClearSelection();
            highlights.ShowSelection(Square.File(move.From), Square.Rank(move.From));
            var cell = new[] { new Vector2Int(Square.File(move.To), Square.Rank(move.To)) };
            if (move.IsCapture) highlights.ShowLegalMoves(new Vector2Int[0], cell);
            else highlights.ShowLegalMoves(cell, new Vector2Int[0]);
            if (pieces[move.From] != null) pieces[move.From].SetSelected(true);
            yield return new WaitForSeconds(HintSeconds);
            if (pieces[move.From] != null) pieces[move.From].SetSelected(false);
            highlights.ClearSelection();
            highlights.ClearLegalMoves();
            hintRoutine = null;
        }

        void StopHint()
        {
            if (hintRoutine == null) return;
            StopCoroutine(hintRoutine);
            hintRoutine = null;
            for (int sq = 0; sq < 64; sq++)
                if (pieces[sq] != null && pieces[sq].IsSelected) pieces[sq].SetSelected(false);
            highlights.ClearSelection();
            highlights.ClearLegalMoves();
        }

        // ---------------------------------------------------------------- Picking

        /// <summary>Case visée : d'abord les pièces (hautes, vues de biais), sinon le plateau.</summary>
        public bool TryPickSquare(Ray ray, out int square)
        {
            square = Square.None;
            var hits = Physics.RaycastAll(ray, 100f);
            float best = float.MaxValue;
            Vector3 point = Vector3.zero;
            bool found = false;
            foreach (var h in hits)
            {
                if (h.distance >= best) continue;
                bool isPiece = h.collider.GetComponentInParent<PieceView>() != null;
                bool isBoard = h.collider.transform == transform || h.collider.transform.IsChildOf(transform);
                if (!isPiece && !isBoard) continue;
                best = h.distance;
                point = isPiece ? h.collider.GetComponentInParent<PieceView>().transform.position : h.point;
                found = true;
            }
            if (!found) return false;
            if (!BoardBuilder.WorldToSquare(point, out int f, out int r)) return false;
            square = Square.Make(f, r);
            return true;
        }

        // ---------------------------------------------------------------- Outils

        PieceView Spawn(int piece, int square)
        {
            var view = PieceView.Create(ToKind(Piece.TypeOf(piece)), Piece.ColorOf(piece) == PieceColor.White,
                Square.File(square), Square.Rank(square), piecesRoot);
            EnsureCollider(view);
            return view;
        }

        static void EnsureCollider(PieceView view)
        {
            if (view.GetComponent<Collider>() != null) return;
            var c = view.gameObject.AddComponent<CapsuleCollider>();
            float h = PieceMeshFactory.Height(view.Kind);
            c.height = h;
            c.radius = 0.34f;
            c.center = new Vector3(0f, h * 0.5f, 0f);
        }

        public static PieceKind ToKind(PieceType type)
        {
            switch (type)
            {
                case PieceType.Pawn: return PieceKind.Pawn;
                case PieceType.Knight: return PieceKind.Knight;
                case PieceType.Bishop: return PieceKind.Bishop;
                case PieceType.Rook: return PieceKind.Rook;
                case PieceType.Queen: return PieceKind.Queen;
                default: return PieceKind.King;
            }
        }
    }
}
