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
            // Tous les enfants, y compris une victime retirée du tableau dont l'animation a été interrompue.
            for (int i = piecesRoot.childCount - 1; i >= 0; i--)
            {
                var child = piecesRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            System.Array.Clear(pieces, 0, 64);
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
                FitCollider(mover, ToKind(Piece.TypeOf(v.PromotedPiece)));
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

        /// <summary>
        /// Cases visées par le rayon : celle de la pièce touchée (pièces hautes vues de biais)
        /// et celle du plateau sous le rayon. L'une ou l'autre peut valoir Square.None.
        /// </summary>
        public bool TryPick(Ray ray, out int pieceSquare, out int boardSquare)
        {
            pieceSquare = boardSquare = Square.None;
            int count = Physics.RaycastNonAlloc(ray, rayHits, 100f);
            float bestPiece = float.MaxValue, bestBoard = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var h = rayHits[i];
                var view = h.collider.GetComponentInParent<PieceView>();
                if (view != null)
                {
                    if (h.distance < bestPiece && BoardBuilder.WorldToSquare(view.transform.position, out int f, out int r))
                    {
                        bestPiece = h.distance;
                        pieceSquare = Square.Make(f, r);
                    }
                }
                else if (h.collider.transform.IsChildOf(transform) && h.distance < bestBoard
                         && BoardBuilder.WorldToSquare(h.point, out int bf, out int br))
                {
                    bestBoard = h.distance;
                    boardSquare = Square.Make(bf, br);
                }
            }
            return pieceSquare != Square.None || boardSquare != Square.None;
        }

        readonly RaycastHit[] rayHits = new RaycastHit[16];

        // ---------------------------------------------------------------- Outils

        PieceView Spawn(int piece, int square)
        {
            var view = PieceView.Create(ToKind(Piece.TypeOf(piece)), Piece.ColorOf(piece) == PieceColor.White,
                Square.File(square), Square.Rank(square), piecesRoot);
            EnsureCollider(view);
            return view;
        }

        static void EnsureCollider(PieceView view) => FitCollider(view, view.Kind);

        static void FitCollider(PieceView view, PieceKind kind)
        {
            if (!view.TryGetComponent<CapsuleCollider>(out var c)) c = view.gameObject.AddComponent<CapsuleCollider>();
            float h = PieceMeshFactory.Height(kind);
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
