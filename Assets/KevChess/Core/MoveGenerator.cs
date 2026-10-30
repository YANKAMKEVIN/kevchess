using System.Collections.Generic;

namespace KevChess.Core
{
    public static class MoveGenerator
    {
        static readonly PieceType[] Promotions = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

        /// <summary>Coups légaux (le roi du joueur au trait n'est jamais laissé en échec).</summary>
        public static List<Move> Legal(Position pos)
        {
            var moves = new List<Move>(64);
            GenerateLegal(pos, moves);
            return moves;
        }

        public static void GenerateLegal(Position pos, List<Move> moves, bool capturesOnly = false)
        {
            moves.Clear();
            GeneratePseudoLegal(pos, moves, capturesOnly);
            var us = pos.SideToMove;
            int write = 0;
            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                pos.MakeMove(m);
                bool legal = !pos.IsInCheck(us);
                pos.UnmakeMove();
                if (legal) moves[write++] = m;
            }
            moves.RemoveRange(write, moves.Count - write);
        }

        public static bool HasLegalMove(Position pos)
        {
            var moves = new List<Move>(64);
            GeneratePseudoLegal(pos, moves, false);
            var us = pos.SideToMove;
            foreach (var m in moves)
            {
                pos.MakeMove(m);
                bool legal = !pos.IsInCheck(us);
                pos.UnmakeMove();
                if (legal) return true;
            }
            return false;
        }

        /// <summary>
        /// Coups pseudo-légaux. Avec capturesOnly, seulement les prises et les promotions (pour la quiescence).
        /// Attention : ajoute à la liste sans la vider (contrairement à GenerateLegal).
        /// </summary>
        public static void GeneratePseudoLegal(Position pos, List<Move> moves, bool capturesOnly = false)
        {
            var us = pos.SideToMove;
            var them = Piece.Opposite(us);

            for (int from = 0; from < 64; from++)
            {
                int piece = pos[from];
                if (!Piece.IsColor(piece, us)) continue;

                switch (Piece.TypeOf(piece))
                {
                    case PieceType.Pawn:
                        GeneratePawn(pos, from, us, moves, capturesOnly);
                        break;
                    case PieceType.Knight:
                        GenerateSteps(pos, from, Tables.KnightTargets[from], them, moves, capturesOnly);
                        break;
                    case PieceType.King:
                        GenerateSteps(pos, from, Tables.KingTargets[from], them, moves, capturesOnly);
                        if (!capturesOnly) GenerateCastling(pos, from, us, moves);
                        break;
                    case PieceType.Bishop:
                        GenerateSliders(pos, from, 4, 8, them, moves, capturesOnly);
                        break;
                    case PieceType.Rook:
                        GenerateSliders(pos, from, 0, 4, them, moves, capturesOnly);
                        break;
                    case PieceType.Queen:
                        GenerateSliders(pos, from, 0, 8, them, moves, capturesOnly);
                        break;
                }
            }
        }

        static void GeneratePawn(Position pos, int from, PieceColor us, List<Move> moves, bool capturesOnly)
        {
            int dir = us == PieceColor.White ? 8 : -8;
            int startRank = us == PieceColor.White ? 1 : 6;
            int promoRank = us == PieceColor.White ? 7 : 0;
            int rank = Square.Rank(from);

            int one = from + dir;
            if (pos[one] == Piece.Empty)
            {
                if (Square.Rank(one) == promoRank)
                    AddPromotions(from, one, MoveFlags.None, moves);
                else if (!capturesOnly)
                {
                    moves.Add(new Move(from, one));
                    int two = one + dir;
                    if (rank == startRank && pos[two] == Piece.Empty)
                        moves.Add(new Move(from, two, MoveFlags.DoublePush));
                }
            }

            foreach (int to in Tables.PawnAttacks[(int)us][from])
            {
                int target = pos[to];
                if (target != Piece.Empty && Piece.ColorOf(target) != us)
                {
                    if (Square.Rank(to) == promoRank) AddPromotions(from, to, MoveFlags.Capture, moves);
                    else moves.Add(new Move(from, to, MoveFlags.Capture));
                }
                else if (to == pos.EnPassantSquare)
                {
                    moves.Add(new Move(from, to, MoveFlags.Capture | MoveFlags.EnPassant));
                }
            }
        }

        static void AddPromotions(int from, int to, MoveFlags flags, List<Move> moves)
        {
            foreach (var t in Promotions) moves.Add(new Move(from, to, flags, t));
        }

        static void GenerateSteps(Position pos, int from, int[] targets, PieceColor them, List<Move> moves, bool capturesOnly)
        {
            foreach (int to in targets)
            {
                int target = pos[to];
                if (target == Piece.Empty)
                {
                    if (!capturesOnly) moves.Add(new Move(from, to));
                }
                else if (Piece.ColorOf(target) == them)
                {
                    moves.Add(new Move(from, to, MoveFlags.Capture));
                }
            }
        }

        static void GenerateSliders(Position pos, int from, int dirStart, int dirEnd, PieceColor them, List<Move> moves, bool capturesOnly)
        {
            for (int d = dirStart; d < dirEnd; d++)
            {
                foreach (int to in Tables.Rays[d][from])
                {
                    int target = pos[to];
                    if (target == Piece.Empty)
                    {
                        if (!capturesOnly) moves.Add(new Move(from, to));
                        continue;
                    }
                    if (Piece.ColorOf(target) == them) moves.Add(new Move(from, to, MoveFlags.Capture));
                    break;
                }
            }
        }

        static void GenerateCastling(Position pos, int from, PieceColor us, List<Move> moves)
        {
            var them = Piece.Opposite(us);
            bool white = us == PieceColor.White;
            var kingSide = white ? CastlingRights.WhiteKingSide : CastlingRights.BlackKingSide;
            var queenSide = white ? CastlingRights.WhiteQueenSide : CastlingRights.BlackQueenSide;
            if ((pos.Castling & (kingSide | queenSide)) == 0) return;
            if (pos.IsSquareAttacked(from, them)) return;

            // from = e1/e8. La case d'arrivée est vérifiée par le filtre de légalité.
            if ((pos.Castling & kingSide) != 0
                && pos[from + 1] == Piece.Empty && pos[from + 2] == Piece.Empty
                && !pos.IsSquareAttacked(from + 1, them))
                moves.Add(new Move(from, from + 2, MoveFlags.Castle));

            if ((pos.Castling & queenSide) != 0
                && pos[from - 1] == Piece.Empty && pos[from - 2] == Piece.Empty && pos[from - 3] == Piece.Empty
                && !pos.IsSquareAttacked(from - 1, them))
                moves.Add(new Move(from, from - 2, MoveFlags.Castle));
        }

        /// <summary>Nombre de feuilles à la profondeur donnée (test de référence du générateur).</summary>
        public static long Perft(Position pos, int depth)
        {
            if (depth == 0) return 1;
            var moves = new List<Move>(64);
            GenerateLegal(pos, moves);
            if (depth == 1) return moves.Count;
            long nodes = 0;
            foreach (var m in moves)
            {
                pos.MakeMove(m);
                nodes += Perft(pos, depth - 1);
                pos.UnmakeMove();
            }
            return nodes;
        }
    }
}
