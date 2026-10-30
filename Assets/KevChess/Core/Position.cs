using System;
using System.Collections.Generic;
using System.Text;

namespace KevChess.Core
{
    /// <summary>
    /// Position d'échecs mutable (tableau 64 cases) avec jouer/déjouer et hachage Zobrist incrémental.
    /// </summary>
    public sealed class Position
    {
        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        readonly int[] board = new int[64];
        readonly int[] kingSquare = new int[2];
        readonly List<Undo> history = new List<Undo>(256);

        public PieceColor SideToMove { get; private set; }
        public CastlingRights Castling { get; private set; }
        /// <summary>Case de prise en passant, renseignée seulement si un pion adverse peut réellement la jouer.</summary>
        public int EnPassantSquare { get; private set; } = Square.None;
        public int HalfmoveClock { get; private set; }
        public int FullmoveNumber { get; private set; } = 1;
        public ulong Hash { get; private set; }
        public int Ply => history.Count;

        struct Undo
        {
            public Move Move;
            public int Captured;
            public CastlingRights Castling;
            public int EnPassant;
            public int Halfmove;
            public ulong Hash;
        }

        // Droits de roque conservés quand une pièce part de / arrive sur la case.
        static readonly CastlingRights[] CastleMask = BuildCastleMask();

        static CastlingRights[] BuildCastleMask()
        {
            var m = new CastlingRights[64];
            for (int i = 0; i < 64; i++) m[i] = CastlingRights.All;
            m[Square.Parse("a1")] &= ~CastlingRights.WhiteQueenSide;
            m[Square.Parse("h1")] &= ~CastlingRights.WhiteKingSide;
            m[Square.Parse("e1")] &= ~(CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide);
            m[Square.Parse("a8")] &= ~CastlingRights.BlackQueenSide;
            m[Square.Parse("h8")] &= ~CastlingRights.BlackKingSide;
            m[Square.Parse("e8")] &= ~(CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide);
            return m;
        }

        public int this[int square] => board[square];
        public int KingSquare(PieceColor color) => kingSquare[(int)color];

        public static Position Start() => FromFen(StartFen);

        public Position Clone()
        {
            var p = new Position();
            Array.Copy(board, p.board, 64);
            Array.Copy(kingSquare, p.kingSquare, 2);
            p.history.AddRange(history);
            p.SideToMove = SideToMove;
            p.Castling = Castling;
            p.EnPassantSquare = EnPassantSquare;
            p.HalfmoveClock = HalfmoveClock;
            p.FullmoveNumber = FullmoveNumber;
            p.Hash = Hash;
            return p;
        }

        // ---------- FEN ----------

        public static Position FromFen(string fen)
        {
            if (string.IsNullOrWhiteSpace(fen)) throw new FormatException("FEN vide");
            var parts = fen.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) throw new FormatException("FEN incomplet : " + fen);

            var p = new Position();
            p.kingSquare[0] = p.kingSquare[1] = Square.None;
            var rows = parts[0].Split('/');
            if (rows.Length != 8) throw new FormatException("FEN : 8 rangées attendues");
            for (int i = 0; i < 8; i++)
            {
                int rank = 7 - i, file = 0;
                foreach (char c in rows[i])
                {
                    if (char.IsDigit(c)) { file += c - '0'; continue; }
                    int piece = Piece.FromFenChar(c);
                    if (piece < 0 || file > 7) throw new FormatException("FEN : caractère invalide '" + c + "'");
                    p.Put(piece, Square.Make(file, rank));
                    file++;
                }
                if (file != 8) throw new FormatException("FEN : rangée de longueur invalide");
            }
            if (p.kingSquare[0] == Square.None || p.kingSquare[1] == Square.None)
                throw new FormatException("FEN : chaque camp doit avoir un roi");

            p.SideToMove = parts[1] == "b" ? PieceColor.Black : PieceColor.White;

            var cr = CastlingRights.None;
            if (parts[2] != "-")
                foreach (char c in parts[2])
                {
                    if (c == 'K') cr |= CastlingRights.WhiteKingSide;
                    else if (c == 'Q') cr |= CastlingRights.WhiteQueenSide;
                    else if (c == 'k') cr |= CastlingRights.BlackKingSide;
                    else if (c == 'q') cr |= CastlingRights.BlackQueenSide;
                }
            p.Castling = p.SanitizeCastling(cr);

            int ep = parts[3] == "-" ? Square.None : Square.Parse(parts[3]);
            p.EnPassantSquare = ep != Square.None && p.CanCaptureEnPassant(ep, p.SideToMove) ? ep : Square.None;

            p.HalfmoveClock = parts.Length > 4 && int.TryParse(parts[4], out var hm) ? hm : 0;
            p.FullmoveNumber = parts.Length > 5 && int.TryParse(parts[5], out var fm) ? Math.Max(1, fm) : 1;
            p.Hash = p.ComputeHash();
            return p;
        }

        public string ToFen()
        {
            var sb = new StringBuilder(90);
            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    int piece = board[Square.Make(file, rank)];
                    if (piece == Piece.Empty) { empty++; continue; }
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    sb.Append(Piece.ToFenChar(piece));
                }
                if (empty > 0) sb.Append(empty);
                if (rank > 0) sb.Append('/');
            }
            sb.Append(SideToMove == PieceColor.White ? " w " : " b ");
            if (Castling == CastlingRights.None) sb.Append('-');
            else
            {
                if ((Castling & CastlingRights.WhiteKingSide) != 0) sb.Append('K');
                if ((Castling & CastlingRights.WhiteQueenSide) != 0) sb.Append('Q');
                if ((Castling & CastlingRights.BlackKingSide) != 0) sb.Append('k');
                if ((Castling & CastlingRights.BlackQueenSide) != 0) sb.Append('q');
            }
            sb.Append(' ').Append(Square.Name(EnPassantSquare));
            sb.Append(' ').Append(HalfmoveClock).Append(' ').Append(FullmoveNumber);
            return sb.ToString();
        }

        // Retire les droits de roque incohérents (roi ou tour absents de leur case d'origine).
        CastlingRights SanitizeCastling(CastlingRights cr)
        {
            int wk = Piece.Make(PieceColor.White, PieceType.King), wr = Piece.Make(PieceColor.White, PieceType.Rook);
            int bk = Piece.Make(PieceColor.Black, PieceType.King), br = Piece.Make(PieceColor.Black, PieceType.Rook);
            if (board[4] != wk) cr &= ~(CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide);
            if (board[7] != wr) cr &= ~CastlingRights.WhiteKingSide;
            if (board[0] != wr) cr &= ~CastlingRights.WhiteQueenSide;
            if (board[60] != bk) cr &= ~(CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide);
            if (board[63] != br) cr &= ~CastlingRights.BlackKingSide;
            if (board[56] != br) cr &= ~CastlingRights.BlackQueenSide;
            return cr;
        }

        bool CanCaptureEnPassant(int epSquare, PieceColor capturer)
        {
            // Un pion du camp « capturer » attaque epSquare s'il est sur une case attaquée
            // depuis epSquare par un pion de la couleur opposée.
            int pawn = Piece.Make(capturer, PieceType.Pawn);
            foreach (int s in Tables.PawnAttacks[(int)Piece.Opposite(capturer)][epSquare])
                if (board[s] == pawn) return true;
            return false;
        }

        void Put(int piece, int sq)
        {
            board[sq] = piece;
            if (Piece.TypeOf(piece) == PieceType.King) kingSquare[(int)Piece.ColorOf(piece)] = sq;
        }

        public ulong ComputeHash()
        {
            ulong h = 0;
            for (int sq = 0; sq < 64; sq++)
                if (board[sq] != Piece.Empty) h ^= Tables.ZobristPieces[board[sq], sq];
            if (SideToMove == PieceColor.Black) h ^= Tables.ZobristBlackToMove;
            h ^= Tables.ZobristCastling[(int)Castling];
            if (EnPassantSquare != Square.None) h ^= Tables.ZobristEnPassant[Square.File(EnPassantSquare)];
            return h;
        }

        // ---------- Jouer / déjouer ----------

        /// <summary>Joue un coup supposé pseudo-légal (généré par MoveGenerator). Ne vérifie pas l'échec.</summary>
        public void MakeMove(Move m)
        {
            int from = m.From, to = m.To;
            int piece = board[from];
            var us = SideToMove;
            var them = Piece.Opposite(us);
            int captured = board[to];

            history.Add(new Undo
            {
                Move = m,
                Captured = captured,
                Castling = Castling,
                EnPassant = EnPassantSquare,
                Halfmove = HalfmoveClock,
                Hash = Hash,
            });

            ulong h = Hash;
            h ^= Tables.ZobristCastling[(int)Castling];
            if (EnPassantSquare != Square.None) h ^= Tables.ZobristEnPassant[Square.File(EnPassantSquare)];

            if (m.IsEnPassant)
            {
                int capSq = us == PieceColor.White ? to - 8 : to + 8;
                captured = board[capSq];
                board[capSq] = Piece.Empty;
                h ^= Tables.ZobristPieces[captured, capSq];
                var u = history[history.Count - 1];
                u.Captured = captured;
                history[history.Count - 1] = u;
            }
            else if (captured != Piece.Empty)
            {
                h ^= Tables.ZobristPieces[captured, to];
            }

            board[from] = Piece.Empty;
            h ^= Tables.ZobristPieces[piece, from];
            int placed = m.IsPromotion ? Piece.Make(us, m.Promotion) : piece;
            board[to] = placed;
            h ^= Tables.ZobristPieces[placed, to];
            if (Piece.TypeOf(piece) == PieceType.King) kingSquare[(int)us] = to;

            if (m.IsCastle)
            {
                int rookFrom, rookTo;
                if (Square.File(to) == 6) { rookFrom = to + 1; rookTo = to - 1; }
                else { rookFrom = to - 2; rookTo = to + 1; }
                int rook = board[rookFrom];
                board[rookFrom] = Piece.Empty;
                board[rookTo] = rook;
                h ^= Tables.ZobristPieces[rook, rookFrom] ^ Tables.ZobristPieces[rook, rookTo];
            }

            Castling &= CastleMask[from] & CastleMask[to];
            h ^= Tables.ZobristCastling[(int)Castling];

            EnPassantSquare = Square.None;
            if ((m.Flags & MoveFlags.DoublePush) != 0)
            {
                int ep = (from + to) / 2;
                if (CanCaptureEnPassant(ep, them))
                {
                    EnPassantSquare = ep;
                    h ^= Tables.ZobristEnPassant[Square.File(ep)];
                }
            }

            if (Piece.TypeOf(piece) == PieceType.Pawn || captured != Piece.Empty) HalfmoveClock = 0;
            else HalfmoveClock++;
            if (us == PieceColor.Black) FullmoveNumber++;

            SideToMove = them;
            h ^= Tables.ZobristBlackToMove;
            Hash = h;
        }

        public void UnmakeMove()
        {
            if (history.Count == 0) throw new InvalidOperationException("Aucun coup à annuler");
            var u = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);

            var m = u.Move;
            SideToMove = Piece.Opposite(SideToMove);
            var us = SideToMove;
            if (us == PieceColor.Black) FullmoveNumber--;

            int moved = board[m.To];
            int original = m.IsPromotion ? Piece.Make(us, PieceType.Pawn) : moved;
            board[m.From] = original;
            if (Piece.TypeOf(original) == PieceType.King) kingSquare[(int)us] = m.From;

            if (m.IsEnPassant)
            {
                board[m.To] = Piece.Empty;
                board[us == PieceColor.White ? m.To - 8 : m.To + 8] = u.Captured;
            }
            else
            {
                board[m.To] = u.Captured;
            }

            if (m.IsCastle)
            {
                int rookFrom, rookTo;
                if (Square.File(m.To) == 6) { rookFrom = m.To + 1; rookTo = m.To - 1; }
                else { rookFrom = m.To - 2; rookTo = m.To + 1; }
                board[rookFrom] = board[rookTo];
                board[rookTo] = Piece.Empty;
            }

            Castling = u.Castling;
            EnPassantSquare = u.EnPassant;
            HalfmoveClock = u.Halfmove;
            Hash = u.Hash;
        }

        /// <summary>Coup « nul » (passer son tour) pour la recherche de l'IA. Interdit en échec.</summary>
        public void MakeNullMove()
        {
            history.Add(new Undo { Move = Move.Null, Castling = Castling, EnPassant = EnPassantSquare, Halfmove = HalfmoveClock, Hash = Hash });
            ulong h = Hash;
            if (EnPassantSquare != Square.None) h ^= Tables.ZobristEnPassant[Square.File(EnPassantSquare)];
            EnPassantSquare = Square.None;
            HalfmoveClock++;
            SideToMove = Piece.Opposite(SideToMove);
            Hash = h ^ Tables.ZobristBlackToMove;
        }

        public void UnmakeNullMove()
        {
            var u = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            SideToMove = Piece.Opposite(SideToMove);
            EnPassantSquare = u.EnPassant;
            HalfmoveClock = u.Halfmove;
            Hash = u.Hash;
        }

        public Move LastMove => history.Count == 0 ? Move.Null : history[history.Count - 1].Move;

        /// <summary>Pièce capturée par le dernier coup (Piece.Empty sinon).</summary>
        public int LastCaptured => history.Count == 0 ? Piece.Empty : history[history.Count - 1].Captured;

        // ---------- Attaques ----------

        public bool IsSquareAttacked(int sq, PieceColor by)
        {
            // Pion de « by » qui attaque sq : il se trouve sur une case attaquée depuis sq par un pion adverse.
            int pawn = Piece.Make(by, PieceType.Pawn);
            foreach (int s in Tables.PawnAttacks[(int)Piece.Opposite(by)][sq])
                if (board[s] == pawn) return true;

            int knight = Piece.Make(by, PieceType.Knight);
            foreach (int s in Tables.KnightTargets[sq])
                if (board[s] == knight) return true;

            int king = Piece.Make(by, PieceType.King);
            foreach (int s in Tables.KingTargets[sq])
                if (board[s] == king) return true;

            int queen = Piece.Make(by, PieceType.Queen);
            int rook = Piece.Make(by, PieceType.Rook);
            int bishop = Piece.Make(by, PieceType.Bishop);
            for (int d = 0; d < 8; d++)
            {
                int slider = d < 4 ? rook : bishop;
                foreach (int s in Tables.Rays[d][sq])
                {
                    int p = board[s];
                    if (p == Piece.Empty) continue;
                    if (p == slider || p == queen) return true;
                    break;
                }
            }
            return false;
        }

        public bool IsInCheck(PieceColor color) => IsSquareAttacked(kingSquare[(int)color], Piece.Opposite(color));
        public bool InCheck => IsInCheck(SideToMove);

        /// <summary>Nombre d'occurrences de la position courante (elle comprise) depuis le dernier coup irréversible.</summary>
        public int RepetitionCount()
        {
            int count = 1;
            int limit = Math.Min(HalfmoveClock, history.Count);
            for (int i = 2; i <= limit; i += 2)
                if (history[history.Count - i].Hash == Hash) count++;
            return count;
        }

        /// <summary>Matériel insuffisant pour mater (position morte) : K-K, K+mineure-K, fous tous de même couleur.</summary>
        public bool IsInsufficientMaterial()
        {
            int minors = 0, knights = 0;
            int bishopColors = 0; // bit 0 : fou sur case sombre, bit 1 : fou sur case claire
            for (int sq = 0; sq < 64; sq++)
            {
                int p = board[sq];
                if (p == Piece.Empty) continue;
                switch (Piece.TypeOf(p))
                {
                    case PieceType.Pawn:
                    case PieceType.Rook:
                    case PieceType.Queen:
                        return false;
                    case PieceType.Knight:
                        minors++; knights++;
                        break;
                    case PieceType.Bishop:
                        minors++;
                        bishopColors |= Square.IsLight(sq) ? 2 : 1;
                        break;
                }
            }
            if (minors <= 1) return true;
            return knights == 0 && bishopColors != 3;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int rank = 7; rank >= 0; rank--)
            {
                for (int file = 0; file < 8; file++) sb.Append(Piece.ToFenChar(board[Square.Make(file, rank)]));
                sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
