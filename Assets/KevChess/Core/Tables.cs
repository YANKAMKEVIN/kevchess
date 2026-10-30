namespace KevChess.Core
{
    /// <summary>Tables précalculées : sauts du cavalier/roi, attaques de pion, rayons des pièces glissantes, Zobrist.</summary>
    public static class Tables
    {
        public static readonly int[][] KnightTargets = new int[64][];
        public static readonly int[][] KingTargets = new int[64][];

        /// <summary>PawnAttacks[couleur][case] = cases attaquées par un pion de cette couleur posé sur la case.</summary>
        public static readonly int[][][] PawnAttacks = { new int[64][], new int[64][] };

        /// <summary>Directions 0-3 : orthogonales (tour), 4-7 : diagonales (fou).</summary>
        public static readonly int[][][] Rays = new int[8][][];

        static readonly int[,] RayDeltas = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 }, { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };
        static readonly int[,] KnightDeltas = { { 1, 2 }, { 2, 1 }, { 2, -1 }, { 1, -2 }, { -1, -2 }, { -2, -1 }, { -2, 1 }, { -1, 2 } };

        // Zobrist : [pièce 0-15][case], trait, roque (16 combinaisons), colonne en passant.
        public static readonly ulong[,] ZobristPieces = new ulong[16, 64];
        public static readonly ulong ZobristBlackToMove;
        public static readonly ulong[] ZobristCastling = new ulong[16];
        public static readonly ulong[] ZobristEnPassant = new ulong[8];

        static Tables()
        {
            for (int sq = 0; sq < 64; sq++)
            {
                int f = Square.File(sq), r = Square.Rank(sq);
                KnightTargets[sq] = Collect(f, r, KnightDeltas);
                KingTargets[sq] = Collect(f, r, RayDeltas);
                PawnAttacks[0][sq] = Collect(f, r, new[,] { { -1, 1 }, { 1, 1 } });
                PawnAttacks[1][sq] = Collect(f, r, new[,] { { -1, -1 }, { 1, -1 } });
            }

            for (int d = 0; d < 8; d++)
            {
                Rays[d] = new int[64][];
                for (int sq = 0; sq < 64; sq++)
                {
                    var list = new System.Collections.Generic.List<int>(7);
                    int f = Square.File(sq) + RayDeltas[d, 0], r = Square.Rank(sq) + RayDeltas[d, 1];
                    while (Square.IsValid(f, r))
                    {
                        list.Add(Square.Make(f, r));
                        f += RayDeltas[d, 0];
                        r += RayDeltas[d, 1];
                    }
                    Rays[d][sq] = list.ToArray();
                }
            }

            // Graine fixe : les hachages sont stables d'une exécution à l'autre.
            ulong seed = 0x9E3779B97F4A7C15UL;
            for (int p = 0; p < 16; p++)
                for (int sq = 0; sq < 64; sq++)
                    ZobristPieces[p, sq] = Next(ref seed);
            ZobristBlackToMove = Next(ref seed);
            for (int i = 0; i < 16; i++) ZobristCastling[i] = Next(ref seed);
            for (int i = 0; i < 8; i++) ZobristEnPassant[i] = Next(ref seed);
        }

        static int[] Collect(int f, int r, int[,] deltas)
        {
            var list = new System.Collections.Generic.List<int>(8);
            for (int i = 0; i < deltas.GetLength(0); i++)
            {
                int nf = f + deltas[i, 0], nr = r + deltas[i, 1];
                if (Square.IsValid(nf, nr)) list.Add(Square.Make(nf, nr));
            }
            return list.ToArray();
        }

        // SplitMix64
        static ulong Next(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
