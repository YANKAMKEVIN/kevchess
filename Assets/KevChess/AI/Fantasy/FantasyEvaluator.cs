using System;
using KevChess.Core;
using KevChess.Core.Fantasy;

namespace KevChess.AI.Fantasy
{
    /// <summary>
    /// Évaluation du mode Légendes en centipions, du point de vue du camp au trait.
    /// Matériel (dragon compris) et tables pièce-case calculées, structure de pions, abri du roi,
    /// puis les termes propres au mode : éclats, invocation à portée, Souffle prêt, pièces sur une fissure.
    /// </summary>
    public sealed class FantasyEvaluator
    {
        // Index par type : 0, pion, cavalier, fou, tour, dame, roi, dragon.
        public static readonly int[] MgValue = { 0, 100, 320, 330, 480, 950, 0, 820 };
        public static readonly int[] EgValue = { 0, 120, 300, 320, 520, 960, 0, 880 };
        /// <summary>Valeur simple pour le tri des coups.</summary>
        public static readonly int[] SimpleValue = { 0, 100, 320, 330, 500, 950, 20000, 850 };

        static readonly int[] PhaseWeight = { 0, 0, 1, 1, 2, 4, 0, 3 };
        const int TotalPhase = 24;
        const int Tempo = 10;

        public const int EnergyValue = 18;
        public const int SummonReadyBonus = 70;
        public const int BreathReadyBonus = 25;

        // Tables pièce-case [type][case vue des blancs], milieu et finale.
        static readonly int[][] PstMg = new int[8][];
        static readonly int[][] PstEg = new int[8][];

        static FantasyEvaluator()
        {
            for (int t = 0; t < 8; t++) { PstMg[t] = new int[64]; PstEg[t] = new int[64]; }
            for (int sq = 0; sq < 64; sq++)
            {
                int f = sq & 7, r = sq >> 3;
                double cf = 3.5 - Math.Abs(f - 3.5), cr = 3.5 - Math.Abs(r - 3.5); // 0..3 vers le centre
                int center = (int)(cf + cr); // 0..6

                // Pion : avancer, surtout au centre ; finale : avancer.
                PstMg[1][sq] = r == 0 || r == 7 ? 0 : (r - 1) * 4 + (f >= 2 && f <= 5 ? (r >= 3 ? 12 : 4) : 0) - (r == 1 && (f == 3 || f == 4) ? 10 : 0);
                PstEg[1][sq] = r == 0 || r == 7 ? 0 : (r - 1) * 12;
                PstMg[2][sq] = center * 9 - 30;
                PstEg[2][sq] = center * 7 - 22;
                PstMg[3][sq] = center * 5 - 12;
                PstEg[3][sq] = center * 4 - 12;
                PstMg[4][sq] = (r == 6 ? 18 : 0) + (f == 3 || f == 4 ? 6 : 0);
                PstEg[4][sq] = r == 6 ? 12 : 0;
                PstMg[5][sq] = center * 3 - 8;
                PstEg[5][sq] = center * 5 - 14;
                PstMg[6][sq] = r == 0 ? (f <= 2 || f >= 6 ? 20 : 0) : -12 * r;
                PstEg[6][sq] = center * 8 - 26;
                PstMg[7][sq] = center * 7 - 18;
                PstEg[7][sq] = center * 6 - 16;
            }
        }

        readonly int[][] pawnCount = { new int[8], new int[8] };

        public static int EvaluateOnce(FantasyPosition pos) => new FantasyEvaluator().Evaluate(pos);

        public int Evaluate(FantasyPosition pos)
        {
            int mgW = 0, egW = 0, mgB = 0, egB = 0, phase = 0;
            int npmW = 0, npmB = 0, bishopsW = 0, bishopsB = 0;
            int rooksW = 0, rooksB = 0, dragonW = -1, dragonB = -1;
            Array.Clear(pawnCount[0], 0, 8);
            Array.Clear(pawnCount[1], 0, 8);
            var cfg = pos.Config;

            for (int sq = 0; sq < 64; sq++)
            {
                int p = pos[sq];
                if (p == Piece.Empty) continue;
                int t = p & 7;
                bool white = (p & 8) == 0;
                int idx = white ? sq : sq ^ 56;
                phase += PhaseWeight[t];
                int mg = MgValue[t] + PstMg[t][idx], eg = EgValue[t] + PstEg[t][idx];

                // Pièce sur une fissure qui va céder : elle risque de tomber.
                if (t != (int)PieceType.King)
                {
                    var hole = pos.HoleAt(sq);
                    if (hole == HoleState.Fissure)
                    {
                        int timer = HoleTimerAt(pos, sq);
                        int danger = timer <= 1 ? 2 : timer <= 2 ? 1 : 0;
                        if (danger > 0) { mg -= MgValue[t] * danger / 4; eg -= EgValue[t] * danger / 4; }
                    }
                }

                if (white) { mgW += mg; egW += eg; } else { mgB += mg; egB += eg; }

                switch ((PieceType)t)
                {
                    case PieceType.Pawn: pawnCount[white ? 0 : 1][sq & 7]++; break;
                    case PieceType.King: break;
                    default:
                        if (white) npmW += MgValue[t]; else npmB += MgValue[t];
                        if (t == (int)PieceType.Bishop) { if (white) bishopsW++; else bishopsB++; }
                        if (t == (int)PieceType.Rook) { if (white) rooksW++; else rooksB++; }
                        if (t == (int)FantasyPiece.Dragon) { if (white) dragonW = sq; else dragonB = sq; }
                        break;
                }
            }
            if (phase > TotalPhase) phase = TotalPhase;

            // Structure de pions : doublés et isolés.
            for (int c = 0; c < 2; c++)
            {
                int pen = 0;
                for (int f = 0; f < 8; f++)
                {
                    int n = pawnCount[c][f];
                    if (n == 0) continue;
                    if (n > 1) pen += 12 * (n - 1);
                    bool isolated = (f == 0 || pawnCount[c][f - 1] == 0) && (f == 7 || pawnCount[c][f + 1] == 0);
                    if (isolated) pen += 10 * n;
                }
                if (c == 0) { mgW -= pen; egW -= pen; } else { mgB -= pen; egB -= pen; }
            }

            if (bishopsW >= 2) { mgW += 30; egW += 45; }
            if (bishopsB >= 2) { mgB += 30; egB += 45; }

            // Abri du roi en milieu de partie.
            mgW -= KingShield(pos, PieceColor.White);
            mgB -= KingShield(pos, PieceColor.Black);

            int score = ((mgW - mgB) * phase + (egW - egB) * (TotalPhase - phase)) / TotalPhase;

            // Termes du mode : éclats, invocation, Souffle.
            score += Fantasy(pos, PieceColor.White, rooksW, dragonW, cfg) - Fantasy(pos, PieceColor.Black, rooksB, dragonB, cfg);

            if (pos.SideToMove == PieceColor.Black) score = -score;
            return score + Tempo;
        }

        static int Fantasy(FantasyPosition pos, PieceColor c, int rooks, int dragonSq, FantasyConfig cfg)
        {
            int e = pos.Energy(c);
            int s = e * EnergyValue;
            if (!pos.HasSummoned(c) && rooks > 0)
            {
                // L'invocation vaut d'autant plus qu'elle est proche.
                if (e >= cfg.SummonCost) s += SummonReadyBonus;
                else if (e >= cfg.SummonCost - 2) s += SummonReadyBonus / 3;
            }
            if (dragonSq >= 0 && pos.BreathCooldown(c) == 0) s += BreathReadyBonus;
            return s;
        }

        static int HoleTimerAt(FantasyPosition pos, int sq)
        {
            for (int i = 0; i < FantasyPosition.HoleSlotCount; i++)
                if (pos.HoleSquare(i) == sq) return pos.HoleTimer(i);
            return 99;
        }

        static int KingShield(FantasyPosition pos, PieceColor color)
        {
            int k = pos.KingSquare(color);
            int kf = k & 7, kr = k >> 3;
            int rel = color == PieceColor.White ? kr : 7 - kr;
            if (rel > 1) return 20;
            int pawn = Piece.Make(color, PieceType.Pawn);
            int dir = color == PieceColor.White ? 8 : -8;
            int pen = 0;
            for (int f = Math.Max(0, kf - 1); f <= Math.Min(7, kf + 1); f++)
            {
                int one = k + dir + (f - kf), two = one + dir;
                if (one >= 0 && one < 64 && pos[one] == pawn) continue;
                pen += two >= 0 && two < 64 && pos[two] == pawn ? 8 : 18;
            }
            return pen;
        }
    }
}
