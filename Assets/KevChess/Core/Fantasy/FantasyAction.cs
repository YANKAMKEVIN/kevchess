using System;

namespace KevChess.Core.Fantasy
{
    public enum ActionKind { Normal = 0, Teleport = 1, Breath = 2 }

    public enum PowerKind { None = 0, Shield = 1, Freeze = 2, Summon = 3 }

    /// <summary>Le dragon occupe la valeur de type 7, libre dans le codage des pièces sur 3 bits.</summary>
    public static class FantasyPiece
    {
        public const PieceType Dragon = (PieceType)7;

        const string FenChars = " pnbrqkd";

        public static char ToFenChar(int piece)
        {
            if (piece == Piece.Empty) return '.';
            char c = FenChars[(int)Piece.TypeOf(piece)];
            return Piece.ColorOf(piece) == PieceColor.White ? char.ToUpperInvariant(c) : c;
        }

        public static int FromFenChar(char c)
        {
            int idx = FenChars.IndexOf(char.ToLowerInvariant(c));
            if (idx <= 0) return -1;
            return Piece.Make(char.IsUpper(c) ? PieceColor.White : PieceColor.Black, (PieceType)idx);
        }

        /// <summary>Lettre SAN (anglaise, D pour le dragon, vide pour le pion).</summary>
        public static string SanLetter(PieceType type) => type == Dragon ? "D" : Piece.SanLetter(type);

        public static bool IsDragon(int piece) => piece != Piece.Empty && Piece.TypeOf(piece) == Dragon;
    }

    /// <summary>
    /// Action d'un tour : pouvoir éventuel puis coup. Compacte sur un int :
    /// bits 0-5 départ, 6-11 arrivée, 12-14 promotion, 15-18 drapeaux, 19-20 nature du coup,
    /// 21-22 pouvoir, 23-28 case du pouvoir. default = action nulle.
    /// </summary>
    public readonly struct FantasyAction : IEquatable<FantasyAction>
    {
        readonly int data;

        FantasyAction(int raw) { data = raw; }

        public FantasyAction(int from, int to, MoveFlags flags = MoveFlags.None, PieceType promotion = PieceType.None,
            ActionKind kind = ActionKind.Normal, PowerKind power = PowerKind.None, int powerSquare = 0)
        {
            data = from | (to << 6) | ((int)promotion << 12) | ((int)flags << 15) | ((int)kind << 19)
                   | ((int)power << 21) | (powerSquare << 23);
        }

        public static readonly FantasyAction Null = default;
        public static FantasyAction FromRaw(int raw) => new FantasyAction(raw);

        public int From => data & 63;
        public int To => (data >> 6) & 63;
        public PieceType Promotion => (PieceType)((data >> 12) & 7);
        public MoveFlags Flags => (MoveFlags)((data >> 15) & 15);
        public ActionKind Kind => (ActionKind)((data >> 19) & 3);
        public PowerKind Power => (PowerKind)((data >> 21) & 3);
        public int PowerSquare => (data >> 23) & 63;
        public int Raw => data;

        public bool IsNull => data == 0;
        public bool IsCapture => (Flags & MoveFlags.Capture) != 0 || Kind == ActionKind.Breath;
        public bool IsPromotion => Promotion != PieceType.None;
        public bool IsCastle => (Flags & MoveFlags.Castle) != 0;
        public bool IsEnPassant => (Flags & MoveFlags.EnPassant) != 0;
        public bool HasPower => Power != PowerKind.None;

        /// <summary>Même coup avec un pouvoir ajouté avant.</summary>
        public FantasyAction WithPower(PowerKind power, int square) =>
            new FantasyAction((data & ~(0xFF << 21)) | ((int)power << 21) | (square << 23));

        /// <summary>Le coup seul, sans le pouvoir.</summary>
        public FantasyAction WithoutPower => new FantasyAction(data & ~(0xFF << 21));

        /// <summary>Le coup classique correspondant (sans sens pour une Téléportation ou un Souffle).</summary>
        public Move ToMove() => new Move(From, To, Flags, Promotion);

        public static FantasyAction FromMove(Move m) => new FantasyAction(m.From, m.To, m.Flags, m.Promotion);

        public override string ToString()
        {
            if (IsNull) return "0000";
            string p = Power == PowerKind.None ? "" : "[" + "-SFD"[(int)Power] + " " + Square.Name(PowerSquare) + "] ";
            string sep = Kind == ActionKind.Teleport ? "~" : Kind == ActionKind.Breath ? "^" : "";
            string promo = IsPromotion ? FantasyPiece.ToFenChar(Piece.Make(PieceColor.Black, Promotion)).ToString() : "";
            return p + Square.Name(From) + sep + Square.Name(To) + promo;
        }

        public bool Equals(FantasyAction other) => data == other.data;
        public override bool Equals(object obj) => obj is FantasyAction a && a.data == data;
        public override int GetHashCode() => data;
        public static bool operator ==(FantasyAction a, FantasyAction b) => a.data == b.data;
        public static bool operator !=(FantasyAction a, FantasyAction b) => a.data != b.data;
    }
}
