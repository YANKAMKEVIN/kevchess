using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using KevChess.Core;
using KevChess.Core.Fantasy;

namespace KevChess.AI.Fantasy
{
    public sealed class FantasySearchResult
    {
        public FantasyAction BestAction;
        public int Score;
        public int Depth;
        public long Nodes;
        public long ElapsedMs;
        public bool Aborted;
        /// <summary>Scores des actions racine (exacts seulement si demandé), du meilleur au pire.</summary>
        public KeyValuePair<FantasyAction, int>[] RootScores = Array.Empty<KeyValuePair<FantasyAction, int>>();

        public bool IsMate => Math.Abs(Score) > FantasySearcher.MateBound;

        public override string ToString() =>
            "best " + BestAction + " score " + Score + " depth " + Depth + " nodes " + Nodes + " time " + ElapsedMs + "ms";
    }

    /// <summary>
    /// Recherche negamax alpha-bêta (PVS) du mode Légendes : approfondissement itératif, quiescence (prises,
    /// Souffles, promotions), table de transposition, killers, historique, null move, LMR, extension d'échec.
    /// Les pouvoirs sont élagués par le générateur (GenMode.Search). Aucun nouvel événement n'est tiré dans
    /// l'arbre : l'IA ne connaît que les événements déjà annoncés.
    /// </summary>
    public sealed class FantasySearcher
    {
        public const int Infinity = 32000;
        public const int MateScore = 31000;
        public const int MateBound = MateScore - 512;
        public const int MaxPly = 96;

        static readonly int[,] LmrTable = BuildLmr();

        struct TtEntry
        {
            public ulong Key;
            public int Action;
            public short Score;
            public sbyte Depth;
            public byte Bound; // 1 haute, 2 basse, 3 exacte
        }

        readonly TtEntry[] tt;
        readonly int ttMask;
        readonly FantasyEvaluator evaluator = new FantasyEvaluator();
        readonly List<FantasyAction>[] lists = new List<FantasyAction>[MaxPly + 2];
        readonly int[][] scores = new int[MaxPly + 2][];
        readonly FantasyAction[][] killers = new FantasyAction[MaxPly + 2][];
        readonly int[] history = new int[2 * 64 * 64];
        readonly Stopwatch stopwatch = new Stopwatch();

        FantasyPosition pos;
        long nodes, timeLimitMs, nodeLimit;
        CancellationToken token;
        bool stopped;
        int completedDepth;

        public FantasySearcher(int hashMb = 16)
        {
            int entries = 1;
            long wanted = (long)hashMb * 1024 * 1024 / 24;
            while (entries * 2L <= wanted) entries *= 2;
            tt = new TtEntry[entries];
            ttMask = entries - 1;
            for (int i = 0; i < lists.Length; i++)
            {
                lists[i] = new List<FantasyAction>(256);
                scores[i] = new int[2048];
                killers[i] = new FantasyAction[2];
            }
        }

        public void Reset()
        {
            Array.Clear(tt, 0, tt.Length);
            Array.Clear(history, 0, history.Length);
        }

        static int[,] BuildLmr()
        {
            var t = new int[64, 64];
            for (int d = 1; d < 64; d++)
                for (int m = 1; m < 64; m++)
                    t[d, m] = (int)(0.5 + Math.Log(d) * Math.Log(m) / 2.2);
            return t;
        }

        public FantasySearchResult Search(FantasyPosition root, SearchLimits limits, bool exactRoot = false)
        {
            limits = limits ?? new SearchLimits();
            pos = root.Clone();
            pos.Report = null;
            nodes = 0;
            stopped = false;
            completedDepth = 0;
            timeLimitMs = Math.Max(0, limits.MaxTimeMs);
            nodeLimit = Math.Max(0, limits.MaxNodes);
            token = limits.CancellationToken;
            int maxDepth = Math.Max(1, Math.Min(64, limits.MaxDepth));
            stopwatch.Restart();
            for (int i = 0; i < killers.Length; i++) { killers[i][0] = FantasyAction.Null; killers[i][1] = FantasyAction.Null; }
            for (int i = 0; i < history.Length; i++) history[i] /= 8;

            var result = new FantasySearchResult();
            var rootActions = FantasyMoveGenerator.Legal(pos, GenMode.Search);
            if (rootActions.Count == 0)
            {
                // Le mode Search peut écarter des pouvoirs : on vérifie avec la génération complète.
                rootActions = FantasyMoveGenerator.Legal(pos, GenMode.All);
                if (rootActions.Count == 0)
                {
                    result.Score = pos.InCheck ? -MateScore : 0;
                    return result;
                }
            }

            var rootScores = new int[rootActions.Count];
            for (int i = 0; i < rootActions.Count; i++)
            {
                pos.MakeAction(rootActions[i]);
                rootScores[i] = -QSearch(-Infinity, Infinity, 1);
                pos.UnmakeAction();
            }
            Sort(rootActions, rootScores);
            result.BestAction = rootActions[0];
            result.Score = rootScores[0];

            var iterScores = new int[rootActions.Count];
            for (int depth = 1; depth <= maxDepth; depth++)
            {
                int alpha = -Infinity, beta = Infinity, best = -Infinity, bestIdx = 0;
                for (int i = 0; i < rootActions.Count; i++)
                {
                    pos.MakeAction(rootActions[i]);
                    nodes++;
                    int score;
                    if (exactRoot || i == 0) score = -Search(depth - 1, -beta, -alpha, 1, true);
                    else
                    {
                        score = -Search(depth - 1, -alpha - 1, -alpha, 1, true);
                        if (!stopped && score > alpha) score = -Search(depth - 1, -beta, -alpha, 1, true);
                    }
                    pos.UnmakeAction();
                    if (stopped) break;
                    iterScores[i] = score;
                    if (score > best) { best = score; bestIdx = i; }
                    if (!exactRoot && score > alpha) alpha = score;
                }
                if (stopped)
                {
                    // Itération incomplète : on garde un coup qui a battu le précédent après recherche complète.
                    if (!exactRoot && best > result.Score && bestIdx > 0 && completedDepth > 0)
                    {
                        result.BestAction = rootActions[bestIdx];
                        result.Score = best;
                    }
                    break;
                }

                completedDepth = depth;
                result.BestAction = rootActions[bestIdx];
                result.Score = best;
                result.Depth = depth;
                var copy = (int[])iterScores.Clone();
                if (exactRoot)
                {
                    Sort(rootActions, copy);
                    result.RootScores = new KeyValuePair<FantasyAction, int>[rootActions.Count];
                    for (int i = 0; i < rootActions.Count; i++)
                        result.RootScores[i] = new KeyValuePair<FantasyAction, int>(rootActions[i], copy[i]);
                }
                else
                {
                    var b = rootActions[bestIdx];
                    rootActions.RemoveAt(bestIdx);
                    rootActions.Insert(0, b);
                }

                if (timeLimitMs > 0 && stopwatch.ElapsedMilliseconds * 2 >= timeLimitMs) break;
                if (nodeLimit > 0 && nodes >= nodeLimit) break;
                if (Math.Abs(best) > MateBound && depth >= (MateScore - Math.Abs(best)) + 2) break;
                if (rootActions.Count == 1 && depth >= 3) break;
            }

            result.Nodes = nodes;
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
            result.Aborted = stopped;
            if (result.Depth == 0) result.Depth = completedDepth;
            return result;
        }

        static void Sort(List<FantasyAction> list, int[] s)
        {
            for (int i = 1; i < list.Count; i++)
            {
                var a = list[i];
                int v = s[i];
                int j = i - 1;
                while (j >= 0 && s[j] < v) { list[j + 1] = list[j]; s[j + 1] = s[j]; j--; }
                list[j + 1] = a;
                s[j + 1] = v;
            }
        }

        void CheckStop()
        {
            if (token.IsCancellationRequested) { stopped = true; return; }
            if (completedDepth < 1) return;
            if (timeLimitMs > 0 && stopwatch.ElapsedMilliseconds >= timeLimitMs) stopped = true;
            else if (nodeLimit > 0 && nodes >= nodeLimit) stopped = true;
        }

        int Search(int depth, int alpha, int beta, int ply, bool allowNull)
        {
            if ((++nodes & 511) == 0) CheckStop();
            if (stopped) return 0;
            if (ply >= MaxPly - 2) return evaluator.Evaluate(pos);

            bool inCheck = pos.InCheck;
            if (inCheck && ply < MaxPly / 2) depth++;
            if (depth <= 0) return QSearch(alpha, beta, ply);
            bool pvNode = beta - alpha > 1;

            if (pos.HalfmoveClock >= 100 || (pos.HalfmoveClock >= 4 && pos.RepetitionCount() >= 2) || pos.IsInsufficientMaterial())
                return 0;

            alpha = Math.Max(alpha, -MateScore + ply);
            beta = Math.Min(beta, MateScore - ply - 1);
            if (alpha >= beta) return alpha;

            ulong key = pos.Hash;
            ref var e = ref tt[(int)key & ttMask];
            var ttAction = FantasyAction.Null;
            if (e.Key == key)
            {
                ttAction = FantasyAction.FromRaw(e.Action);
                if (!pvNode && e.Depth >= depth)
                {
                    int s = FromTt(e.Score, ply);
                    if (e.Bound == 3 || (e.Bound == 2 && s >= beta) || (e.Bound == 1 && s <= alpha)) return s;
                }
            }

            int staticEval = inCheck ? -Infinity : evaluator.Evaluate(pos);
            var us = pos.SideToMove;

            if (!pvNode && !inCheck)
            {
                if (depth <= 5 && Math.Abs(beta) < MateBound && staticEval - 100 * depth >= beta)
                    return staticEval - 100 * depth;
                if (allowNull && depth >= 3 && staticEval >= beta && HasNonPawnMaterial(us))
                {
                    int r = depth >= 7 ? 3 : 2;
                    pos.MakeNullMove();
                    int score = -Search(depth - 1 - r, -beta, -beta + 1, ply + 1, false);
                    pos.UnmakeNullMove();
                    if (stopped) return 0;
                    if (score >= beta) return score >= MateBound ? beta : score;
                }
            }

            var list = lists[ply];
            FantasyMoveGenerator.GenerateLegal(pos, list, GenMode.Search);
            if (list.Count == 0)
            {
                if (!inCheck) return 0;
                // Le mode Search peut écarter une parade (Invocation) : vérification complète avant de déclarer mat.
                FantasyMoveGenerator.GenerateLegal(pos, list, GenMode.All);
                if (list.Count == 0) return -MateScore + ply;
            }
            var sc = Score(list, ttAction, ply);

            int best = -Infinity, originalAlpha = alpha, n = 0;
            var bestAction = FantasyAction.Null;
            var killer = killers[ply];
            for (int i = 0; i < list.Count; i++)
            {
                PickNext(list, sc, i);
                var a = list[i];
                bool quiet = !a.IsCapture && !a.IsPromotion;
                pos.MakeAction(a);
                n++;
                int newDepth = depth - 1, score;
                if (n == 1) score = -Search(newDepth, -beta, -alpha, ply + 1, true);
                else
                {
                    int r = 0;
                    if (depth >= 3 && n > (pvNode ? 4 : 3) && quiet && !inCheck && a != killer[0] && a != killer[1])
                    {
                        r = LmrTable[Math.Min(depth, 63), Math.Min(n, 63)];
                        if (a.HasPower) r++;
                        if (pvNode) r--;
                        if (pos.InCheck) r = 0;
                        if (r > newDepth - 1) r = newDepth - 1;
                        if (r < 0) r = 0;
                    }
                    score = -Search(newDepth - r, -alpha - 1, -alpha, ply + 1, true);
                    if (score > alpha && r > 0 && !stopped) score = -Search(newDepth, -alpha - 1, -alpha, ply + 1, true);
                    if (score > alpha && score < beta && !stopped) score = -Search(newDepth, -beta, -alpha, ply + 1, true);
                }
                pos.UnmakeAction();
                if (stopped) return 0;

                if (score > best)
                {
                    best = score;
                    bestAction = a;
                    if (score > alpha)
                    {
                        alpha = score;
                        if (score >= beta)
                        {
                            if (quiet)
                            {
                                if (killer[0] != a) { killer[1] = killer[0]; killer[0] = a; }
                                int idx = HistoryIndex(us, a);
                                history[idx] += depth * depth;
                                if (history[idx] > 1000000) for (int k = 0; k < history.Length; k++) history[k] /= 2;
                            }
                            break;
                        }
                    }
                }
            }

            byte bound = best >= beta ? (byte)2 : best > originalAlpha ? (byte)3 : (byte)1;
            if (e.Key != key || depth >= e.Depth || bound == 3)
            {
                int action = bound == 1 ? (e.Key == key ? e.Action : 0) : bestAction.Raw;
                e.Key = key;
                e.Action = action;
                e.Score = (short)ToTt(best, ply);
                e.Depth = (sbyte)Math.Min(depth, 127);
                e.Bound = bound;
            }
            return best;
        }

        int QSearch(int alpha, int beta, int ply)
        {
            if ((++nodes & 511) == 0) CheckStop();
            if (stopped) return 0;
            if (ply >= MaxPly - 2) return evaluator.Evaluate(pos);

            bool inCheck = pos.InCheck;
            int best, standPat = 0;
            if (!inCheck)
            {
                standPat = evaluator.Evaluate(pos);
                if (standPat >= beta) return standPat;
                if (standPat + 1150 < alpha) return standPat;
                if (standPat > alpha) alpha = standPat;
                best = standPat;
            }
            else best = -Infinity;

            var list = lists[ply];
            FantasyMoveGenerator.GenerateLegal(pos, list, inCheck ? GenMode.Search : GenMode.Tactical);
            if (inCheck && list.Count == 0)
            {
                FantasyMoveGenerator.GenerateLegal(pos, list, GenMode.All);
                if (list.Count == 0) return -MateScore + ply;
            }
            var sc = Score(list, FantasyAction.Null, ply);

            for (int i = 0; i < list.Count; i++)
            {
                PickNext(list, sc, i);
                var a = list[i];
                if (!inCheck)
                {
                    if (a.IsPromotion && a.Promotion != PieceType.Queen) continue;
                    if (!a.IsPromotion)
                    {
                        int victim = a.IsEnPassant ? 1 : pos[a.To] & 7;
                        if (standPat + FantasyEvaluator.SimpleValue[victim] + 200 <= alpha) continue;
                    }
                }
                pos.MakeAction(a);
                int score = -QSearch(-beta, -alpha, ply + 1);
                pos.UnmakeAction();
                if (stopped) return 0;
                if (score > best)
                {
                    best = score;
                    if (score > alpha) { alpha = score; if (score >= beta) break; }
                }
            }
            return best;
        }

        const int TtScore = 30000000, CaptureScore = 20000000, Killer1 = 19000000, Killer2 = 18000000;

        int[] Score(List<FantasyAction> list, FantasyAction ttAction, int ply)
        {
            var s = scores[ply];
            if (s.Length < list.Count) { s = new int[list.Count * 2]; scores[ply] = s; }
            var killer = killers[ply];
            var us = pos.SideToMove;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                int v;
                if (a == ttAction && !ttAction.IsNull) v = TtScore;
                else if (a.IsCapture || a.IsPromotion)
                {
                    int victim = a.IsEnPassant ? 1 : pos[a.To] & 7;
                    int attacker = a.Kind == ActionKind.Breath ? 0 : pos[a.From] & 7;
                    v = CaptureScore + FantasyEvaluator.SimpleValue[victim] * 10 - attacker;
                    if (a.IsPromotion) v += a.Promotion == PieceType.Queen ? 9000 : -30000000;
                    if (a.HasPower) v -= 500;
                }
                else if (a == killer[0]) v = Killer1;
                else if (a == killer[1]) v = Killer2;
                else
                {
                    v = history[HistoryIndex(us, a)];
                    if (a.Power == PowerKind.Summon) v += 15000000;
                    else if (a.HasPower || a.Kind == ActionKind.Teleport) v -= 100000;
                }
                s[i] = v;
            }
            return s;
        }

        static void PickNext(List<FantasyAction> list, int[] s, int start)
        {
            int bi = start, bs = s[start];
            for (int j = start + 1; j < list.Count; j++) if (s[j] > bs) { bs = s[j]; bi = j; }
            if (bi != start)
            {
                var t = list[start]; list[start] = list[bi]; list[bi] = t;
                s[bi] = s[start]; s[start] = bs;
            }
        }

        static int HistoryIndex(PieceColor side, FantasyAction a) => ((int)side << 12) | (a.From << 6) | a.To;

        bool HasNonPawnMaterial(PieceColor side)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                int p = pos[sq];
                if (p == Piece.Empty || Piece.ColorOf(p) != side) continue;
                var t = Piece.TypeOf(p);
                if (t != PieceType.Pawn && t != PieceType.King) return true;
            }
            return false;
        }

        static int ToTt(int score, int ply) => score > MateBound ? score + ply : score < -MateBound ? score - ply : score;
        static int FromTt(int score, int ply) => score > MateBound ? score - ply : score < -MateBound ? score + ply : score;
    }
}
