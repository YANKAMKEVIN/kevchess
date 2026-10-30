namespace KevChess.Core
{
    public enum PieceColor { White = 0, Black = 1 }

    public enum PieceType { None = 0, Pawn = 1, Knight = 2, Bishop = 3, Rook = 4, Queen = 5, King = 6 }

    /// <summary>
    /// Une pièce est codée sur un int : bits 0-2 = type, bit 3 = couleur. 0 = case vide.
    /// </summary>
    public static class Piece
    {
        public const int Empty = 0;

        public static int Make(PieceColor color, PieceType type) => (int)type | ((int)color << 3);
        public static PieceType TypeOf(int piece) => (PieceType)(piece & 7);
        public static PieceColor ColorOf(int piece) => (PieceColor)((piece >> 3) & 1);
        public static bool Is(int piece, PieceColor color, PieceType type) => piece == Make(color, type);
        public static bool IsColor(int piece, PieceColor color) => piece != Empty && ColorOf(piece) == color;
        public static PieceColor Opposite(PieceColor color) => color == PieceColor.White ? PieceColor.Black : PieceColor.White;

        const string FenChars = " pnbrqk";

        public static char ToFenChar(int piece)
        {
            if (piece == Empty) return '.';
            char c = FenChars[(int)TypeOf(piece)];
            return ColorOf(piece) == PieceColor.White ? char.ToUpperInvariant(c) : c;
        }

        public static int FromFenChar(char c)
        {
            int idx = FenChars.IndexOf(char.ToLowerInvariant(c));
            if (idx <= 0) return -1;
            var color = char.IsUpper(c) ? PieceColor.White : PieceColor.Black;
            return Make(color, (PieceType)idx);
        }

        /// <summary>Lettre SAN anglaise (vide pour le pion).</summary>
        public static string SanLetter(PieceType type)
        {
            switch (type)
            {
                case PieceType.Knight: return "N";
                case PieceType.Bishop: return "B";
                case PieceType.Rook: return "R";
                case PieceType.Queen: return "Q";
                case PieceType.King: return "K";
                default: return "";
            }
        }
    }

    /// <summary>Cases indexées de 0 (a1) à 63 (h8) : index = rang * 8 + colonne.</summary>
    public static class Square
    {
        public const int None = -1;

        public static int File(int sq) => sq & 7;
        public static int Rank(int sq) => sq >> 3;
        public static int Make(int file, int rank) => rank * 8 + file;
        public static bool IsValid(int file, int rank) => file >= 0 && file < 8 && rank >= 0 && rank < 8;
        public static bool IsLight(int sq) => ((File(sq) + Rank(sq)) & 1) == 1;

        public static string Name(int sq)
        {
            if (sq < 0 || sq > 63) return "-";
            return new string(new[] { (char)('a' + File(sq)), (char)('1' + Rank(sq)) });
        }

        public static int Parse(string name)
        {
            if (name == null || name.Length != 2) return None;
            int f = name[0] - 'a', r = name[1] - '1';
            return IsValid(f, r) ? Make(f, r) : None;
        }
    }
}
