using System.Collections.Generic;

namespace KevChess.Core.Ux
{
    public enum RefusalReason
    {
        /// <summary>Rien à expliquer (coup impossible pour la pièce, ou coup légal).</summary>
        None,
        /// <summary>Le roi est en échec et le coup ne le pare pas.</summary>
        InCheck,
        /// <summary>La pièce est clouée : la bouger découvrirait le roi.</summary>
        Pinned,
        /// <summary>Le roi irait sur une case attaquée.</summary>
        KingAttacked,
    }

    /// <summary>Raison d'un coup refusé, et cases à faire clignoter (roi menacé, pièces qui l'attaquent).</summary>
    public sealed class MoveRefusal
    {
        public RefusalReason Reason { get; private set; }

        /// <summary>Case du roi menacé (Square.None sans raison).</summary>
        public int KingSquare { get; private set; } = Square.None;

        /// <summary>Pièces adverses en cause : celles qui donnent échec, qui clouent, ou qui tiennent la case visée.</summary>
        public IReadOnlyList<int> Attackers => attackers;

        readonly List<int> attackers = new List<int>();

        /// <summary>Message court pour le joueur (null sans raison).</summary>
        public string Message
        {
            get
            {
                switch (Reason)
                {
                    case RefusalReason.InCheck: return "Votre roi est en échec : parez d'abord l'échec";
                    case RefusalReason.Pinned: return "Cette pièce est clouée : elle protège votre roi";
                    case RefusalReason.KingAttacked: return "Votre roi serait en échec sur cette case";
                    default: return null;
                }
            }
        }

        /// <summary>Explique pourquoi la pièce en <paramref name="from"/> ne peut pas aller en <paramref name="to"/>.</summary>
        public static MoveRefusal Explain(Position position, int from, int to)
        {
            var r = new MoveRefusal();
            if (position == null || from == Square.None || to == Square.None || from == to) return r;
            var side = position.SideToMove;
            if (!Piece.IsColor(position[from], side)) return r;

            Move pseudo = Move.Null;
            var moves = new List<Move>();
            MoveGenerator.GeneratePseudoLegal(position, moves);
            foreach (var m in moves)
                if (m.From == from && m.To == to) { pseudo = m; break; }

            if (pseudo.IsNull)
            {
                // Coup impossible pour cette pièce : seul l'échec mérite d'être rappelé.
                if (position.InCheck) r.SetCheck(position, side);
                return r;
            }

            var after = position.Clone();
            after.MakeMove(pseudo);
            int king = after.KingSquare(side);
            if (!after.IsSquareAttacked(king, Piece.Opposite(side))) return r; // coup légal : rien à dire

            if (position.InCheck)
            {
                r.SetCheck(position, side);
                return r;
            }
            bool kingMove = Piece.TypeOf(position[from]) == PieceType.King;
            r.Reason = kingMove ? RefusalReason.KingAttacked : RefusalReason.Pinned;
            r.KingSquare = kingMove ? to : position.KingSquare(side);
            r.attackers.AddRange(AttackersOf(after, king, Piece.Opposite(side)));
            return r;
        }

        void SetCheck(Position position, PieceColor side)
        {
            Reason = RefusalReason.InCheck;
            KingSquare = position.KingSquare(side);
            attackers.Clear();
            attackers.AddRange(AttackersOf(position, KingSquare, Piece.Opposite(side)));
        }

        /// <summary>Cases des pièces de <paramref name="by"/> qui attaquent <paramref name="square"/>.</summary>
        public static List<int> AttackersOf(Position pos, int square, PieceColor by)
        {
            var list = new List<int>();
            int pawn = Piece.Make(by, PieceType.Pawn);
            foreach (int s in Tables.PawnAttacks[(int)Piece.Opposite(by)][square])
                if (pos[s] == pawn) list.Add(s);
            int knight = Piece.Make(by, PieceType.Knight);
            foreach (int s in Tables.KnightTargets[square])
                if (pos[s] == knight) list.Add(s);
            int king = Piece.Make(by, PieceType.King);
            foreach (int s in Tables.KingTargets[square])
                if (pos[s] == king) list.Add(s);
            int queen = Piece.Make(by, PieceType.Queen);
            int rook = Piece.Make(by, PieceType.Rook);
            int bishop = Piece.Make(by, PieceType.Bishop);
            for (int d = 0; d < 8; d++)
            {
                int slider = d < 4 ? rook : bishop;
                foreach (int s in Tables.Rays[d][square])
                {
                    int p = pos[s];
                    if (p == Piece.Empty) continue;
                    if (p == slider || p == queen) list.Add(s);
                    break;
                }
            }
            return list;
        }
    }
}
