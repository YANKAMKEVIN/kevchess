using System;

namespace KevChess.Core
{
    [Flags]
    public enum MoveFlags
    {
        None = 0,
        Capture = 1,
        DoublePush = 2,
        EnPassant = 4,
        Castle = 8,
    }

    [Flags]
    public enum CastlingRights
    {
        None = 0,
        WhiteKingSide = 1,
        WhiteQueenSide = 2,
        BlackKingSide = 4,
        BlackQueenSide = 8,
        All = 15,
    }

    /// <summary>
    /// Coup compact : bits 0-5 départ, 6-11 arrivée, 12-14 promotion, 15-18 drapeaux.
    /// default(Move) est le coup nul.
    /// </summary>
    public readonly struct Move : IEquatable<Move>
    {
        readonly int data;

        public Move(int from, int to, MoveFlags flags = MoveFlags.None, PieceType promotion = PieceType.None)
        {
            data = from | (to << 6) | ((int)promotion << 12) | ((int)flags << 15);
        }

        public static readonly Move Null = default;

        public int From => data & 63;
        public int To => (data >> 6) & 63;
        public PieceType Promotion => (PieceType)((data >> 12) & 7);
        public MoveFlags Flags => (MoveFlags)((data >> 15) & 15);
        public bool IsNull => data == 0;
        public bool IsCapture => (Flags & MoveFlags.Capture) != 0;
        public bool IsPromotion => Promotion != PieceType.None;
        public bool IsCastle => (Flags & MoveFlags.Castle) != 0;
        public bool IsEnPassant => (Flags & MoveFlags.EnPassant) != 0;
        public int Raw => data;

        /// <summary>Notation UCI : e2e4, e7e8q.</summary>
        public string ToUci()
        {
            if (IsNull) return "0000";
            string s = Square.Name(From) + Square.Name(To);
            if (IsPromotion) s += char.ToLowerInvariant(Piece.ToFenChar(Piece.Make(PieceColor.White, Promotion)));
            return s;
        }

        public bool SameAs(int from, int to, PieceType promotion) =>
            From == from && To == to && Promotion == promotion;

        public bool Equals(Move other) => data == other.data;
        public override bool Equals(object obj) => obj is Move m && m.data == data;
        public override int GetHashCode() => data;
        public static bool operator ==(Move a, Move b) => a.data == b.data;
        public static bool operator !=(Move a, Move b) => a.data != b.data;
        public override string ToString() => ToUci();
    }
}
