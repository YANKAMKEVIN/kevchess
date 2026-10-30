using System.Collections.Generic;
using System.Text;

namespace KevChess.Core
{
    /// <summary>Notation algébrique standard (SAN) : Nf3, exd5, O-O, e8=Q+, Qxf7#.</summary>
    public static class Notation
    {
        /// <summary>SAN d'un coup légal dans la position (avant qu'il soit joué).</summary>
        public static string ToSan(Position pos, Move move)
        {
            var sb = new StringBuilder(8);
            int piece = pos[move.From];
            var type = Piece.TypeOf(piece);

            if (move.IsCastle)
            {
                sb.Append(Square.File(move.To) == 6 ? "O-O" : "O-O-O");
            }
            else
            {
                if (type == PieceType.Pawn)
                {
                    if (move.IsCapture) sb.Append((char)('a' + Square.File(move.From)));
                }
                else
                {
                    sb.Append(Piece.SanLetter(type));
                    AppendDisambiguation(pos, move, type, sb);
                }
                if (move.IsCapture) sb.Append('x');
                sb.Append(Square.Name(move.To));
                if (move.IsPromotion) sb.Append('=').Append(Piece.SanLetter(move.Promotion));
            }

            pos.MakeMove(move);
            if (pos.InCheck) sb.Append(MoveGenerator.HasLegalMove(pos) ? '+' : '#');
            pos.UnmakeMove();
            return sb.ToString();
        }

        static void AppendDisambiguation(Position pos, Move move, PieceType type, StringBuilder sb)
        {
            bool ambiguous = false, sameFile = false, sameRank = false;
            foreach (var other in MoveGenerator.Legal(pos))
            {
                if (other.To != move.To || other.From == move.From) continue;
                if (Piece.TypeOf(pos[other.From]) != type) continue;
                ambiguous = true;
                if (Square.File(other.From) == Square.File(move.From)) sameFile = true;
                if (Square.Rank(other.From) == Square.Rank(move.From)) sameRank = true;
            }
            if (!ambiguous) return;
            if (!sameFile) sb.Append((char)('a' + Square.File(move.From)));
            else if (!sameRank) sb.Append((char)('1' + Square.Rank(move.From)));
            else sb.Append(Square.Name(move.From));
        }

        /// <summary>Retrouve le coup légal correspondant à une SAN (ou UCI). Renvoie Move.Null si introuvable.</summary>
        public static Move Parse(Position pos, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Move.Null;
            string wanted = Normalize(text);
            List<Move> legal = MoveGenerator.Legal(pos);
            foreach (var m in legal)
                if (Normalize(ToSan(pos, m)) == wanted || m.ToUci() == text.Trim()) return m;
            return Move.Null;
        }

        static string Normalize(string san) =>
            san.Trim().Replace("0-0-0", "O-O-O").Replace("0-0", "O-O").TrimEnd('+', '#', '!', '?');
    }
}
