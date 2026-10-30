using System;
using KevChess.Core;

namespace KevChess.AI
{
    /// <summary>
    /// Évaluation statique en centipions, du point de vue du camp au trait (positif = bon pour lui).
    /// Matériel + tables pièce-case milieu/finale interpolées par la phase de jeu (tapered eval),
    /// structure de pions, paire de fous, mobilité, sécurité du roi, tours sur colonnes ouvertes,
    /// « mop-up » pour conclure les finales gagnées sans pions.
    /// Une instance contient des tampons de travail : elle n'est pas partagée entre threads.
    /// </summary>
    public sealed class Evaluator
    {
        // Index par PieceType (0 = vide, 1 = pion ... 6 = roi).
        public static readonly int[] MgValue = { 0, 100, 320, 330, 480, 950, 0 };
        public static readonly int[] EgValue = { 0, 120, 300, 320, 520, 960, 0 };

        /// <summary>Valeur simple utilisée pour le tri des coups et l'échange statique (SEE).</summary>
        public static readonly int[] SimpleValue = { 0, 100, 320, 330, 500, 950, 20000 };

        static readonly int[] PhaseWeight = { 0, 0, 1, 1, 2, 4, 0 };
        const int TotalPhase = 24;
        const int Tempo = 10;

        // ---------- Tables pièce-case ----------
        // Écrites « comme on regarde l'échiquier côté blancs » : première ligne = 8e rangée.
        // Pour une pièce blanche sur sq, l'index est sq ^ 56 ; pour une noire, sq (miroir vertical).

        static readonly int[] PawnMg =
        {
              0,   0,   0,   0,   0,   0,   0,   0,
             50,  50,  50,  55,  55,  50,  50,  50,
             12,  14,  22,  32,  32,  22,  14,  12,
              6,   8,  12,  26,  26,  12,   8,   6,
              0,   0,   6,  20,  20,   6,   0,   0,
              4,  -2,  -4,   6,   6,  -8,  -2,   4,
              4,   8,   8, -18, -18,   8,   8,   4,
              0,   0,   0,   0,   0,   0,   0,   0,
        };

        static readonly int[] PawnEg =
        {
              0,   0,   0,   0,   0,   0,   0,   0,
             85,  80,  75,  70,  70,  75,  80,  85,
             50,  48,  44,  40,  40,  44,  48,  50,
             28,  26,  22,  18,  18,  22,  26,  28,
             14,  12,  10,   8,   8,  10,  12,  14,
              4,   4,   2,   2,   2,   2,   4,   4,
              0,   0,   0,   0,   0,   0,   0,   0,
              0,   0,   0,   0,   0,   0,   0,   0,
        };

        static readonly int[] KnightMg =
        {
            -50, -38, -30, -28, -28, -30, -38, -50,
            -36, -18,   0,   4,   4,   0, -18, -36,
            -28,   4,  12,  18,  18,  12,   4, -28,
            -26,   6,  18,  24,  24,  18,   6, -26,
            -26,   2,  16,  22,  22,  16,   2, -26,
            -28,   4,  12,  14,  14,  12,   4, -28,
            -38, -18,   0,   4,   4,   0, -18, -38,
            -50, -30, -28, -26, -26, -28, -30, -50,
        };

        static readonly int[] KnightEg =
        {
            -40, -30, -22, -18, -18, -22, -30, -40,
            -30, -14,  -4,   0,   0,  -4, -14, -30,
            -22,  -4,   8,  12,  12,   8,  -4, -22,
            -18,   0,  12,  18,  18,  12,   0, -18,
            -18,   0,  12,  18,  18,  12,   0, -18,
            -22,  -4,   8,  12,  12,   8,  -4, -22,
            -30, -14,  -4,   0,   0,  -4, -14, -30,
            -40, -30, -22, -18, -18, -22, -30, -40,
        };

        static readonly int[] BishopMg =
        {
            -20, -10, -10, -10, -10, -10, -10, -20,
            -10,   0,   0,   0,   0,   0,   0, -10,
            -10,   2,   6,  10,  10,   6,   2, -10,
             -8,   6,   8,  12,  12,   8,   6,  -8,
             -8,   4,  12,  12,  12,  12,   4,  -8,
             -8,  10,  10,  10,  10,  10,  10,  -8,
             -8,  12,   4,   2,   2,   4,  12,  -8,
            -20, -10, -14, -10, -10, -14, -10, -20,
        };

        static readonly int[] BishopEg =
        {
            -16, -10,  -8,  -6,  -6,  -8, -10, -16,
            -10,  -2,   0,   2,   2,   0,  -2, -10,
             -8,   0,   6,   8,   8,   6,   0,  -8,
             -6,   2,   8,  12,  12,   8,   2,  -6,
             -6,   2,   8,  12,  12,   8,   2,  -6,
             -8,   0,   6,   8,   8,   6,   0,  -8,
            -10,  -2,   0,   2,   2,   0,  -2, -10,
            -16, -10,  -8,  -6,  -6,  -8, -10, -16,
        };

        static readonly int[] RookMg =
        {
              4,   6,   8,  10,  10,   8,   6,   4,
             18,  22,  22,  24,  24,  22,  22,  18,
             -4,   0,   2,   4,   4,   2,   0,  -4,
             -6,  -2,   0,   2,   2,   0,  -2,  -6,
             -8,  -4,  -2,   0,   0,  -2,  -4,  -8,
             -8,  -4,  -2,   0,   0,  -2,  -4,  -8,
            -10,  -6,  -2,   0,   0,  -2,  -6, -10,
             -4,  -2,   2,   8,   8,   4,  -2,  -4,
        };

        static readonly int[] RookEg =
        {
              8,   8,  10,  10,  10,  10,   8,   8,
             12,  14,  14,  14,  14,  14,  14,  12,
              4,   4,   4,   4,   4,   4,   4,   4,
              2,   2,   2,   2,   2,   2,   2,   2,
              0,   0,   0,   0,   0,   0,   0,   0,
             -2,  -2,  -2,  -2,  -2,  -2,  -2,  -2,
             -4,  -4,  -4,  -4,  -4,  -4,  -4,  -4,
             -6,  -4,  -2,   0,   0,  -2,  -4,  -6,
        };

        static readonly int[] QueenMg =
        {
            -20, -12, -10,  -6,  -6, -10, -12, -20,
            -12,  -6,   0,   0,   0,   0,  -6, -12,
            -10,   0,   4,   4,   4,   4,   0, -10,
             -6,   0,   4,   6,   6,   4,   0,  -6,
             -4,   0,   4,   6,   6,   4,   0,  -4,
            -10,   2,   4,   4,   4,   4,   2, -10,
            -12,   0,   2,   2,   2,   0,   0, -12,
            -20, -12, -10,  -4,  -6, -10, -12, -20,
        };

        static readonly int[] QueenEg =
        {
            -24, -14, -10,  -8,  -8, -10, -14, -24,
            -14,  -4,   2,   4,   4,   2,  -4, -14,
            -10,   2,  10,  12,  12,  10,   2, -10,
             -8,   4,  12,  18,  18,  12,   4,  -8,
             -8,   4,  12,  18,  18,  12,   4,  -8,
            -10,   2,  10,  12,  12,  10,   2, -10,
            -14,  -4,   2,   4,   4,   2,  -4, -14,
            -24, -14, -10,  -8,  -8, -10, -14, -24,
        };

        static readonly int[] KingMg =
        {
            -40, -46, -46, -54, -54, -46, -46, -40,
            -36, -42, -42, -50, -50, -42, -42, -36,
            -32, -38, -38, -46, -46, -38, -38, -32,
            -28, -34, -34, -42, -42, -34, -34, -28,
            -20, -26, -26, -32, -32, -26, -26, -20,
            -10, -16, -16, -20, -20, -16, -16, -10,
             14,  14,  -6, -10, -10,  -6,  14,  14,
             18,  28,  10, -10,   0,   6,  30,  18,
        };

        static readonly int[] KingEg =
        {
            -50, -36, -28, -22, -22, -28, -36, -50,
            -30, -14,  -4,   0,   0,  -4, -14, -30,
            -26,  -2,  14,  20,  20,  14,  -2, -26,
            -24,   0,  20,  28,  28,  20,   0, -24,
            -24,   0,  20,  28,  28,  20,   0, -24,
            -26,  -4,  12,  18,  18,  12,  -4, -26,
            -32, -16,  -6,   0,   0,  -6, -16, -32,
            -50, -36, -30, -26, -26, -30, -36, -50,
        };

        static readonly int[][] PstMg = { null, PawnMg, KnightMg, BishopMg, RookMg, QueenMg, KingMg };
        static readonly int[][] PstEg = { null, PawnEg, KnightEg, BishopEg, RookEg, QueenEg, KingEg };

        // Bonus du pion passé selon le rang relatif (0 = 1re rangée du camp).
        static readonly int[] PassedMg = { 0, 4, 8, 14, 24, 40, 60, 0 };
        static readonly int[] PassedEg = { 0, 10, 18, 32, 56, 92, 140, 0 };

        const int DoubledMg = 10, DoubledEg = 18;
        const int IsolatedMg = 10, IsolatedEg = 14;
        const int BishopPairMg = 28, BishopPairEg = 48;
        const int RookOpenMg = 24, RookOpenEg = 10;
        const int RookSemiOpenMg = 11, RookSemiOpenEg = 5;

        // Mobilité : (cases atteintes - moyenne) * poids.
        static readonly int[] MobilityMg = { 0, 0, 4, 4, 2, 1, 0 };
        static readonly int[] MobilityEg = { 0, 0, 4, 5, 4, 2, 0 };
        static readonly int[] MobilityAvg = { 0, 0, 4, 6, 6, 11, 0 };

        // Sécurité du roi : poids des attaquants de la zone du roi, puis pénalité indexée par la somme.
        static readonly int[] KingAttackWeight = { 0, 0, 2, 2, 3, 5, 0 };
        static readonly int[] KingDangerTable = BuildKingDanger();

        static readonly byte[,] Distance = BuildDistance();
        static readonly int[] CenterDistance = BuildCenterDistance();

        // ---------- Tampons de travail ----------
        readonly int[][] pawnCount = { new int[8], new int[8] };
        readonly int[][] pawnMinRank = { new int[8], new int[8] };
        readonly int[][] pawnMaxRank = { new int[8], new int[8] };
        readonly int[] pawnSquares = new int[16];
        readonly int[] pieceSquares = new int[32];

        static int[] BuildKingDanger()
        {
            var t = new int[64];
            for (int i = 0; i < 64; i++) t[i] = Math.Min(400, i * i * 3 / 2);
            return t;
        }

        static byte[,] BuildDistance()
        {
            var d = new byte[64, 64];
            for (int a = 0; a < 64; a++)
                for (int b = 0; b < 64; b++)
                    d[a, b] = (byte)Math.Max(Math.Abs(Square.File(a) - Square.File(b)), Math.Abs(Square.Rank(a) - Square.Rank(b)));
            return d;
        }

        static int[] BuildCenterDistance()
        {
            var t = new int[64];
            for (int sq = 0; sq < 64; sq++)
            {
                int f = Square.File(sq), r = Square.Rank(sq);
                int df = Math.Max(3 - f, f - 4), dr = Math.Max(3 - r, r - 4);
                t[sq] = df + dr; // 0 au centre, 6 dans un coin
            }
            return t;
        }

        /// <summary>Évaluation ponctuelle sans instance (alloue des tampons : à éviter dans une boucle).</summary>
        public static int EvaluateOnce(Position pos) => new Evaluator().Evaluate(pos);

        /// <summary>Score en centipions du point de vue du camp au trait.</summary>
        public int Evaluate(Position pos)
        {
            int white = Score(pos, out int whiteNpm, out int blackNpm, out int whitePawns, out int blackPawns);
            int score = white;

            // Finales sans pion du côté fort avec un avantage trop faible pour mater : nulles probables.
            if (score > 0 && whitePawns == 0 && whiteNpm - blackNpm < 400) score /= 8;
            else if (score < 0 && blackPawns == 0 && blackNpm - whiteNpm < 400) score /= 8;

            if (pos.SideToMove == PieceColor.Black) score = -score;
            return score + Tempo;
        }

        int Score(Position pos, out int whiteNpm, out int blackNpm, out int whitePawns, out int blackPawns)
        {
            int mgW = 0, egW = 0, mgB = 0, egB = 0;
            int phase = 0;
            int pawnN = 0, pieceN = 0;
            int bishopsW = 0, bishopsB = 0;
            int npmW = 0, npmB = 0;

            for (int c = 0; c < 2; c++)
                for (int f = 0; f < 8; f++)
                {
                    pawnCount[c][f] = 0;
                    pawnMinRank[c][f] = 8;
                    pawnMaxRank[c][f] = -1;
                }

            // 1) Matériel, tables pièce-case, recensement.
            for (int sq = 0; sq < 64; sq++)
            {
                int p = pos[sq];
                if (p == Piece.Empty) continue;
                int t = p & 7;
                bool isWhite = (p & 8) == 0;
                int idx = isWhite ? sq ^ 56 : sq;
                phase += PhaseWeight[t];
                if (isWhite)
                {
                    mgW += MgValue[t] + PstMg[t][idx];
                    egW += EgValue[t] + PstEg[t][idx];
                }
                else
                {
                    mgB += MgValue[t] + PstMg[t][idx];
                    egB += EgValue[t] + PstEg[t][idx];
                }

                if (t == (int)PieceType.Pawn)
                {
                    int c = isWhite ? 0 : 1, f = sq & 7, r = sq >> 3;
                    pawnCount[c][f]++;
                    if (r < pawnMinRank[c][f]) pawnMinRank[c][f] = r;
                    if (r > pawnMaxRank[c][f]) pawnMaxRank[c][f] = r;
                    if (pawnN < 16) pawnSquares[pawnN++] = sq;
                }
                else if (t != (int)PieceType.King)
                {
                    if (pieceN < 32) pieceSquares[pieceN++] = sq;
                    if (isWhite) npmW += MgValue[t]; else npmB += MgValue[t];
                    if (t == (int)PieceType.Bishop) { if (isWhite) bishopsW++; else bishopsB++; }
                }
            }
            if (phase > TotalPhase) phase = TotalPhase;

            // 2) Structure de pions.
            for (int i = 0; i < pawnN; i++)
            {
                int sq = pawnSquares[i];
                bool isWhite = (pos[sq] & 8) == 0;
                int c = isWhite ? 0 : 1, o = 1 - c;
                int f = sq & 7, r = sq >> 3;
                int mg = 0, eg = 0;

                bool isolated = (f == 0 || pawnCount[c][f - 1] == 0) && (f == 7 || pawnCount[c][f + 1] == 0);
                if (isolated) { mg -= IsolatedMg; eg -= IsolatedEg; }

                // Doublé : pénalité pour chaque pion qui a un pion ami devant lui sur la colonne.
                bool doubled = isWhite ? pawnMaxRank[c][f] > r : pawnMinRank[c][f] < r;
                if (doubled) { mg -= DoubledMg; eg -= DoubledEg; }

                // Passé : aucun pion adverse devant lui sur sa colonne ni sur les colonnes voisines,
                // et pas de pion ami devant lui (le plus avancé seulement).
                bool passed = !doubled;
                for (int df = -1; df <= 1 && passed; df++)
                {
                    int ff = f + df;
                    if (ff < 0 || ff > 7) continue;
                    if (isWhite) { if (pawnMaxRank[o][ff] > r) passed = false; }
                    else { if (pawnMinRank[o][ff] < r) passed = false; }
                }
                if (passed)
                {
                    int rel = isWhite ? r : 7 - r;
                    mg += PassedMg[rel];
                    eg += PassedEg[rel];
                    // Case devant bloquée : bonus réduit.
                    int front = isWhite ? sq + 8 : sq - 8;
                    if (front >= 0 && front < 64 && pos[front] != Piece.Empty) eg -= PassedEg[rel] / 3;
                }

                if (isWhite) { mgW += mg; egW += eg; } else { mgB += mg; egB += eg; }
            }

            // 3) Paire de fous.
            if (bishopsW >= 2) { mgW += BishopPairMg; egW += BishopPairEg; }
            if (bishopsB >= 2) { mgB += BishopPairMg; egB += BishopPairEg; }

            // 4) Mobilité, tours sur colonnes ouvertes, attaques sur la zone du roi.
            int wKing = pos.KingSquare(PieceColor.White), bKing = pos.KingSquare(PieceColor.Black);
            int attackUnitsOnBlack = 0, attackersOnBlack = 0, attackUnitsOnWhite = 0, attackersOnWhite = 0;

            for (int i = 0; i < pieceN; i++)
            {
                int sq = pieceSquares[i];
                int p = pos[sq];
                int t = p & 7;
                bool isWhite = (p & 8) == 0;
                int enemyKing = isWhite ? bKing : wKing;
                int mob = 0, zoneHits = 0;

                if (t == (int)PieceType.Knight)
                {
                    var targets = Tables.KnightTargets[sq];
                    for (int k = 0; k < targets.Length; k++)
                    {
                        int to = targets[k];
                        int q = pos[to];
                        if (q == Piece.Empty || ((q & 8) == 0) != isWhite) mob++;
                        if (Distance[to, enemyKing] <= 1) zoneHits++;
                    }
                }
                else
                {
                    int dStart = t == (int)PieceType.Bishop ? 4 : 0;
                    int dEnd = t == (int)PieceType.Rook ? 4 : 8;
                    for (int d = dStart; d < dEnd; d++)
                    {
                        var ray = Tables.Rays[d][sq];
                        for (int k = 0; k < ray.Length; k++)
                        {
                            int to = ray[k];
                            int q = pos[to];
                            if (Distance[to, enemyKing] <= 1) zoneHits++;
                            if (q == Piece.Empty) { mob++; continue; }
                            if (((q & 8) == 0) != isWhite) mob++;
                            break;
                        }
                    }
                }

                int mobMg = (mob - MobilityAvg[t]) * MobilityMg[t];
                int mobEg = (mob - MobilityAvg[t]) * MobilityEg[t];
                int extraMg = 0, extraEg = 0;

                if (t == (int)PieceType.Rook)
                {
                    int f = sq & 7, c = isWhite ? 0 : 1;
                    if (pawnCount[c][f] == 0)
                    {
                        if (pawnCount[1 - c][f] == 0) { extraMg += RookOpenMg; extraEg += RookOpenEg; }
                        else { extraMg += RookSemiOpenMg; extraEg += RookSemiOpenEg; }
                    }
                }

                if (isWhite)
                {
                    mgW += mobMg + extraMg; egW += mobEg + extraEg;
                    if (zoneHits > 0) { attackersOnBlack++; attackUnitsOnBlack += KingAttackWeight[t] * Math.Min(zoneHits, 3); }
                }
                else
                {
                    mgB += mobMg + extraMg; egB += mobEg + extraEg;
                    if (zoneHits > 0) { attackersOnWhite++; attackUnitsOnWhite += KingAttackWeight[t] * Math.Min(zoneHits, 3); }
                }
            }

            // 5) Sécurité du roi (milieu de partie) : bouclier de pions + attaquants de la zone.
            mgW -= KingShieldPenalty(pos, PieceColor.White, wKing);
            mgB -= KingShieldPenalty(pos, PieceColor.Black, bKing);
            if (attackersOnWhite >= 2 && HeavyMaterial(npmB)) mgW -= KingDangerTable[Math.Min(63, attackUnitsOnWhite)];
            else if (attackersOnWhite >= 2) mgW -= KingDangerTable[Math.Min(63, attackUnitsOnWhite)] / 2;
            if (attackersOnBlack >= 2 && HeavyMaterial(npmW)) mgB -= KingDangerTable[Math.Min(63, attackUnitsOnBlack)];
            else if (attackersOnBlack >= 2) mgB -= KingDangerTable[Math.Min(63, attackUnitsOnBlack)] / 2;

            int mgScore = mgW - mgB, egScore = egW - egB;

            // 6) Mop-up : sans pion chez le perdant et gros avantage, pousser son roi au bord.
            int pawnsW = 0, pawnsB = 0;
            for (int f = 0; f < 8; f++) { pawnsW += pawnCount[0][f]; pawnsB += pawnCount[1][f]; }
            if (pawnsB == 0 && npmW - npmB >= 400)
                egScore += CenterDistance[bKing] * 12 + (14 - 2 * Distance[wKing, bKing]) * 4;
            else if (pawnsW == 0 && npmB - npmW >= 400)
                egScore -= CenterDistance[wKing] * 12 + (14 - 2 * Distance[wKing, bKing]) * 4;

            whiteNpm = npmW; blackNpm = npmB; whitePawns = pawnsW; blackPawns = pawnsB;
            return (mgScore * phase + egScore * (TotalPhase - phase)) / TotalPhase;
        }

        // Assez de matériel lourd pour qu'une attaque sur le roi soit dangereuse.
        static bool HeavyMaterial(int npm) => npm >= MgValue[(int)PieceType.Queen];

        int KingShieldPenalty(Position pos, PieceColor color, int kingSq)
        {
            int c = (int)color;
            int kf = kingSq & 7, kr = kingSq >> 3;
            int relRank = color == PieceColor.White ? kr : 7 - kr;
            if (relRank > 1) return 20; // roi sorti de sa base en milieu de partie
            int penalty = 0;
            int pawn = Piece.Make(color, PieceType.Pawn);
            int dir = color == PieceColor.White ? 8 : -8;
            for (int f = Math.Max(0, kf - 1); f <= Math.Min(7, kf + 1); f++)
            {
                int one = kingSq + dir + (f - kf);
                int two = one + dir;
                if (one >= 0 && one < 64 && pos[one] == pawn) { }
                else if (two >= 0 && two < 64 && pos[two] == pawn) penalty += 8;
                else penalty += 18;
                if (pawnCount[c][f] == 0) penalty += pawnCount[1 - c][f] == 0 ? 14 : 7;
            }
            return penalty;
        }
    }
}
