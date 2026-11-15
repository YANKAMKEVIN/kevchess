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
            // Une UCI (« e2e4 », « e7e8q ») ne peut pas être une SAN : chemin direct, sans calculer la SAN de chaque coup.
            if (IsUci(text.Trim())) return ParseUci(pos, text);
            string wanted = Normalize(text);
            List<Move> legal = MoveGenerator.Legal(pos);
            foreach (var m in legal)
                if (Normalize(ToSan(pos, m)) == wanted || m.ToUci() == text.Trim()) return m;
            return Move.Null;
        }

        /// <summary>
        /// Retrouve le coup légal correspondant à une UCI (« e2e4 », roque « e1g1 », promotion « e7e8q »).
        /// Ne génère que les coups légaux, sans calcul de SAN. Renvoie Move.Null si le texte est invalide ou le coup illégal.
        /// </summary>
        public static Move ParseUci(Position pos, string uci)
        {
            if (uci == null) return Move.Null;
            uci = uci.Trim();
            if (!IsUci(uci)) return Move.Null;
            int from = Square.Make(uci[0] - 'a', uci[1] - '1');
            int to = Square.Make(uci[2] - 'a', uci[3] - '1');
            PieceType promotion = uci.Length == 5 ? PromotionOf(uci[4]) : PieceType.None;
            foreach (var m in MoveGenerator.Legal(pos))
                if (m.SameAs(from, to, promotion)) return m;
            return Move.Null;
        }

        static bool IsUci(string s)
        {
            if (s.Length != 4 && s.Length != 5) return false;
            if (s[0] < 'a' || s[0] > 'h' || s[1] < '1' || s[1] > '8') return false;
            if (s[2] < 'a' || s[2] > 'h' || s[3] < '1' || s[3] > '8') return false;
            return s.Length == 4 || PromotionOf(s[4]) != PieceType.None;
        }

        static PieceType PromotionOf(char c)
        {
            switch (c)
            {
                case 'q': return PieceType.Queen;
                case 'r': return PieceType.Rook;
                case 'b': return PieceType.Bishop;
                case 'n': return PieceType.Knight;
                default: return PieceType.None;
            }
        }

        /// <summary>
        /// SAN affichable : en français, les lettres des pièces deviennent R D T F C (Roi, Dame, Tour, Fou, Cavalier),
        /// promotions comprises (e8=Q → e8=D). Les colonnes (minuscules) et le roque ne changent pas.
        /// </summary>
        public static string Localize(string san, bool french)
        {
            if (!french || string.IsNullOrEmpty(san)) return san;
            var chars = san.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                switch (chars[i])
                {
                    case 'K': chars[i] = 'R'; break;
                    case 'Q': chars[i] = 'D'; break;
                    case 'R': chars[i] = 'T'; break;
                    case 'B': chars[i] = 'F'; break;
                    case 'N': chars[i] = 'C'; break;
                }
            }
            return new string(chars);
        }

        static string Normalize(string san) =>
            san.Trim().Replace("0-0-0", "O-O-O").Replace("0-0", "O-O").TrimEnd('+', '#', '!', '?');
    }
}
