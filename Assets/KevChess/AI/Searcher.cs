using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using KevChess.Core;

namespace KevChess.AI
{
    /// <summary>Limites d'une recherche. Toute limite à 0 est ignorée (sauf MaxDepth, borné à 1..100).</summary>
    public sealed class SearchLimits
    {
        public int MaxDepth = 64;
        public int MaxTimeMs;
        public long MaxNodes;
        public CancellationToken CancellationToken;

        /// <summary>
        /// Calcule un score exact pour chaque coup racine (fenêtre pleine), utile pour choisir parmi
        /// plusieurs bons coups (niveaux faibles). Plus lent : réservé aux faibles profondeurs.
        /// </summary>
        public bool ExactRootScores;

        public static SearchLimits Depth(int depth) => new SearchLimits { MaxDepth = depth };
        public static SearchLimits Time(int ms, int maxDepth = 64) => new SearchLimits { MaxTimeMs = ms, MaxDepth = maxDepth };
    }

    public struct RootMoveScore
    {
        public Move Move;
        public int Score;
        public RootMoveScore(Move move, int score) { Move = move; Score = score; }
        public override string ToString() => Move + " " + Score;
    }

    public sealed class SearchResult
    {
        public Move BestMove;
        /// <summary>Score en centipions du point de vue du camp au trait à la racine.</summary>
        public int Score;
        /// <summary>Dernière profondeur entièrement terminée (0 si aucune).</summary>
        public int Depth;
        public long Nodes;
        public long ElapsedMs;
        public bool Aborted;
        public Move[] PrincipalVariation = Array.Empty<Move>();
        /// <summary>Scores des coups racine (exacts seulement si SearchLimits.ExactRootScores), triés du meilleur au pire.</summary>
        public RootMoveScore[] RootScores = Array.Empty<RootMoveScore>();

        public bool IsMate => Math.Abs(Score) > Searcher.MateBound;

        /// <summary>Mat en N coups : positif si le camp au trait mate, négatif s'il est maté, 0 sinon.</summary>
        public int MateInMoves
        {
            get
            {
                if (Score > Searcher.MateBound) return (Searcher.MateScore - Score + 1) / 2;
                if (Score < -Searcher.MateBound) return -(Searcher.MateScore + Score) / 2;
                return 0;
            }
        }

        public long NodesPerSecond => ElapsedMs > 0 ? Nodes * 1000 / ElapsedMs : Nodes * 1000;

        public override string ToString() =>
            "best " + BestMove + " score " + Score + " depth " + Depth + " nodes " + Nodes + " time " + ElapsedMs + "ms nps " + NodesPerSecond;
    }

    /// <summary>
    /// Recherche negamax alpha-bêta (PVS) avec approfondissement itératif, quiescence, table de transposition,
    /// tri des coups (TT, MVV-LVA/SEE, killers, historique), null move, LMR, extension d'échec, futilité.
    /// Travaille toujours sur une copie de la position fournie. Une instance = une recherche à la fois.
    /// </summary>
    public sealed class Searcher
    {
        public const int Infinity = 32000;
        public const int MateScore = 31000;
        public const int MateBound = MateScore - 512;
        public const int MaxPly = 128;

        static readonly int[] FutilityMargin = { 0, 130, 260, 400 };
        static readonly int[,] LmrTable = BuildLmr();

        readonly TranspositionTable tt;
        readonly Evaluator evaluator = new Evaluator();
        readonly List<Move>[] moveLists = new List<Move>[MaxPly + 2];
        readonly int[][] moveScores = new int[MaxPly + 2][];
        readonly Move[][] killers = new Move[MaxPly + 2][];
        readonly int[] history = new int[2 * 64 * 64];
        readonly Move[][] pvTable = new Move[MaxPly + 2][];
        readonly int[] pvLength = new int[MaxPly + 2];
        readonly int[] seeGain = new int[40];
        readonly Stopwatch stopwatch = new Stopwatch();

        Position pos;
        long nodes;
        long timeLimitMs;
        long nodeLimit;
        CancellationToken token;
        volatile bool stopRequested;
        bool stopped;
        int completedDepth;

        // État racine
        List<Move> rootMoves;
        int[] rootIterScores;
        bool exactRoot;
        Move partialBest;
        int partialScore;
        bool partialValid;

        /// <summary>Appelé à la fin de chaque itération terminée (depuis le thread de recherche).</summary>
        public Action<SearchResult> OnIteration;

        public Searcher(int hashMb = 16)
        {
            tt = new TranspositionTable(hashMb);
            for (int i = 0; i < moveLists.Length; i++)
            {
                moveLists[i] = new List<Move>(128);
                moveScores[i] = new int[256];
                killers[i] = new Move[2];
                pvTable[i] = new Move[MaxPly + 2];
            }
        }

        public TranspositionTable Table => tt;
        public Evaluator Evaluator => evaluator;

        /// <summary>Demande l'arrêt (thread-safe). La recherche rend son meilleur coup connu.</summary>
        public void Stop() => stopRequested = true;

        /// <summary>Oublie tout ce qui a été appris (nouvelle partie).</summary>
        public void Reset()
        {
            tt.Clear();
            Array.Clear(history, 0, history.Length);
        }

        static int[,] BuildLmr()
        {
            var t = new int[64, 64];
            for (int d = 1; d < 64; d++)
                for (int m = 1; m < 64; m++)
                    t[d, m] = (int)(0.4 + Math.Log(d) * Math.Log(m) / 2.4);
            return t;
        }

        // =====================================================================
        // Racine
        // =====================================================================

        public SearchResult Search(Position root, SearchLimits limits)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            limits = limits ?? new SearchLimits();

            pos = root.Clone();
            nodes = 0;
            stopped = false;
            stopRequested = false;
            completedDepth = 0;
            timeLimitMs = Math.Max(0, limits.MaxTimeMs);
            nodeLimit = Math.Max(0, limits.MaxNodes);
            token = limits.CancellationToken;
            exactRoot = limits.ExactRootScores;
            int maxDepth = Math.Max(1, Math.Min(100, limits.MaxDepth));
            stopwatch.Restart();

            tt.NewSearch();
            for (int i = 0; i < killers.Length; i++) { killers[i][0] = Move.Null; killers[i][1] = Move.Null; }
            for (int i = 0; i < history.Length; i++) history[i] /= 8;

            var result = new SearchResult();
            rootMoves = MoveGenerator.Legal(pos);
            if (rootMoves.Count == 0)
            {
                result.BestMove = Move.Null;
                result.Score = pos.InCheck ? -MateScore : 0;
                result.ElapsedMs = stopwatch.ElapsedMilliseconds;
                return result;
            }

            // Tri initial : coup TT puis résultat d'une quiescence sur chaque coup.
            rootIterScores = new int[rootMoves.Count];
            tt.Probe(pos.Hash, out Move ttMove, out _, out _, out _);
            var initial = new int[rootMoves.Count];
            for (int i = 0; i < rootMoves.Count; i++)
            {
                pos.MakeMove(rootMoves[i]);
                initial[i] = -QSearch(-Infinity, Infinity, 1);
                pos.UnmakeMove();
                if (rootMoves[i] == ttMove) initial[i] += 100000;
            }
            SortByScores(rootMoves, initial);

            result.BestMove = rootMoves[0];
            result.Score = initial[0] >= 100000 ? initial[0] - 100000 : initial[0];
            result.RootScores = Snapshot(rootMoves, initial, false);

            for (int depth = 1; depth <= maxDepth; depth++)
            {
                int score;
                if (exactRoot || depth < 5 || Math.Abs(result.Score) > MateBound)
                {
                    score = SearchRoot(depth, -Infinity, Infinity);
                }
                else
                {
                    int window = 30;
                    int alpha = Math.Max(-Infinity, result.Score - window);
                    int beta = Math.Min(Infinity, result.Score + window);
                    while (true)
                    {
                        score = SearchRoot(depth, alpha, beta);
                        if (stopped) break;
                        if (score <= alpha) { alpha = Math.Max(-Infinity, alpha - window * 3); window *= 3; }
                        else if (score >= beta) { beta = Math.Min(Infinity, beta + window * 3); window *= 3; }
                        else break;
                        if (window > 1000) { alpha = -Infinity; beta = Infinity; }
                    }
                }

                if (stopped)
                {
                    // Itération incomplète : on garde un coup qui a battu le précédent après recherche complète.
                    if (!exactRoot && partialValid && partialBest != result.BestMove)
                    {
                        result.BestMove = partialBest;
                        result.Score = partialScore;
                        result.PrincipalVariation = new[] { partialBest };
                    }
                    break;
                }

                completedDepth = depth;
                int bestIndex = 0;
                for (int i = 1; i < rootMoves.Count; i++)
                    if (rootIterScores[i] > rootIterScores[bestIndex]) bestIndex = i;
                result.BestMove = rootMoves[bestIndex];
                result.Score = rootIterScores[bestIndex];
                result.Depth = depth;
                result.PrincipalVariation = ExtractPv();
                if (result.PrincipalVariation.Length == 0 || result.PrincipalVariation[0] != result.BestMove)
                    result.PrincipalVariation = new[] { result.BestMove };
                result.RootScores = Snapshot(rootMoves, rootIterScores, true);

                // Réordonne : exact = tri complet, sinon meilleur coup en tête.
                if (exactRoot) SortByScores(rootMoves, (int[])rootIterScores.Clone());
                else
                {
                    var best = rootMoves[bestIndex];
                    rootMoves.RemoveAt(bestIndex);
                    rootMoves.Insert(0, best);
                }

                result.Nodes = nodes;
                result.ElapsedMs = stopwatch.ElapsedMilliseconds;
                OnIteration?.Invoke(result);

                if (rootMoves.Count == 1 && timeLimitMs > 0 && depth >= 4) break;
                if (timeLimitMs > 0 && stopwatch.ElapsedMilliseconds * 2 >= timeLimitMs) break;
                if (nodeLimit > 0 && nodes >= nodeLimit) break;
                if (Math.Abs(result.Score) > MateBound && depth >= (MateScore - Math.Abs(result.Score)) + 2) break;
            }

            result.Nodes = nodes;
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
            result.Aborted = stopped;
            if (result.Depth == 0) result.Depth = completedDepth;
            return result;
        }

        int SearchRoot(int depth, int alpha, int beta)
        {
            int best = -Infinity;
            int alphaStart = alpha;
            partialValid = false;
            partialBest = Move.Null;
            pvLength[0] = 0;

            for (int i = 0; i < rootMoves.Count; i++)
            {
                var m = rootMoves[i];
                pos.MakeMove(m);
                nodes++;
                int score;
                if (exactRoot)
                {
                    score = -Search(depth - 1, -Infinity, Infinity, 1, true);
                }
                else if (i == 0)
                {
                    score = -Search(depth - 1, -beta, -alpha, 1, true);
                }
                else
                {
                    score = -Search(depth - 1, -alpha - 1, -alpha, 1, true);
                    if (!stopped && score > alpha && score < beta)
                        score = -Search(depth - 1, -beta, -alpha, 1, true);
                }
                pos.UnmakeMove();
                if (stopped) break;

                rootIterScores[i] = score;
                if (score > best)
                {
                    best = score;
                    if (score > alpha || exactRoot)
                    {
                        UpdatePv(0, m);
                        if (score > alphaStart) { partialBest = m; partialScore = score; partialValid = true; }
                    }
                    if (!exactRoot && score > alpha)
                    {
                        alpha = score;
                        if (score >= beta)
                        {
                            for (int j = i + 1; j < rootMoves.Count; j++) rootIterScores[j] = -Infinity;
                            break;
                        }
                    }
                }
            }
            return best;
        }

        static void SortByScores(List<Move> moves, int[] scores)
        {
            // Tri par insertion stable (peu d'éléments).
            for (int i = 1; i < moves.Count; i++)
            {
                var m = moves[i];
                int s = scores[i];
                int j = i - 1;
                while (j >= 0 && scores[j] < s)
                {
                    moves[j + 1] = moves[j];
                    scores[j + 1] = scores[j];
                    j--;
                }
                moves[j + 1] = m;
                scores[j + 1] = s;
            }
        }

        static RootMoveScore[] Snapshot(List<Move> moves, int[] scores, bool sort)
        {
            var arr = new RootMoveScore[moves.Count];
            for (int i = 0; i < arr.Length; i++)
            {
                int s = scores[i];
                if (s >= 100000) s -= 100000;
                arr[i] = new RootMoveScore(moves[i], s);
            }
            if (sort) Array.Sort(arr, (a, b) => b.Score.CompareTo(a.Score));
            return arr;
        }

        Move[] ExtractPv()
        {
            int n = pvLength[0];
            if (n <= 0) return Array.Empty<Move>();
            var pv = new Move[n];
            Array.Copy(pvTable[0], pv, n);
            return pv;
        }

        void UpdatePv(int ply, Move m)
        {
            var row = pvTable[ply];
            row[ply] = m;
            int childLen = pvLength[ply + 1];
            if (childLen < ply + 1) childLen = ply + 1;
            var child = pvTable[ply + 1];
            for (int i = ply + 1; i < childLen; i++) row[i] = child[i];
            pvLength[ply] = childLen;
        }

        void CheckStop()
        {
            if (stopRequested || token.IsCancellationRequested) { stopped = true; return; }
            if (completedDepth < 1) return; // la profondeur 1 est toujours terminée (sauf annulation)
            if (timeLimitMs > 0 && stopwatch.ElapsedMilliseconds >= timeLimitMs) stopped = true;
            else if (nodeLimit > 0 && nodes >= nodeLimit) stopped = true;
        }

        // =====================================================================
        // Alpha-bêta
        // =====================================================================

        int Search(int depth, int alpha, int beta, int ply, bool allowNull)
        {
            pvLength[ply] = ply;
            if ((++nodes & 1023) == 0) CheckStop();
            if (stopped) return 0;
            if (ply >= MaxPly - 2) return evaluator.Evaluate(pos);

            bool inCheck = pos.InCheck;
            if (inCheck && ply < MaxPly / 2) depth++; // extension d'échec
            if (depth <= 0) return QSearch(alpha, beta, ply);

            bool pvNode = beta - alpha > 1;

            // Nulles : 50 coups, répétition (dans l'arbre ou avec l'historique de la partie), matériel insuffisant.
            if (pos.HalfmoveClock >= 100 || (pos.HalfmoveClock >= 4 && pos.RepetitionCount() >= 2) || pos.IsInsufficientMaterial())
                return 0;

            // Élagage par distance de mat.
            alpha = Math.Max(alpha, -MateScore + ply);
            beta = Math.Min(beta, MateScore - ply - 1);
            if (alpha >= beta) return alpha;

            ulong key = pos.Hash;
            Move ttMove = Move.Null;
            if (tt.Probe(key, out ttMove, out int ttScore, out int ttDepth, out Bound ttBound))
            {
                if (!pvNode && ttDepth >= depth)
                {
                    int s = FromTt(ttScore, ply);
                    if (ttBound == Bound.Exact || (ttBound == Bound.Lower && s >= beta) || (ttBound == Bound.Upper && s <= alpha))
                        return s;
                }
            }

            int staticEval = inCheck ? -Infinity : evaluator.Evaluate(pos);
            var us = pos.SideToMove;

            if (!pvNode && !inCheck)
            {
                // Futilité inverse (static null move) à faible profondeur.
                if (depth <= 6 && Math.Abs(beta) < MateBound && staticEval - 90 * depth >= beta)
                    return staticEval - 90 * depth;

                // Null move : pas en échec, pas en finale de pions seuls.
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

            var moves = moveLists[ply];
            var scores = moveScores[ply];
            moves.Clear(); // GeneratePseudoLegal ajoute sans vider
            MoveGenerator.GeneratePseudoLegal(pos, moves, false);
            ScoreMoves(moves, scores, ttMove, ply, false);

            bool canFutility = !pvNode && !inCheck && depth <= 3 && Math.Abs(alpha) < MateBound
                               && staticEval + FutilityMargin[depth] <= alpha;

            int legal = 0;
            int best = -Infinity;
            Move bestMove = Move.Null;
            int originalAlpha = alpha;
            var killer = killers[ply];

            for (int i = 0; i < moves.Count; i++)
            {
                PickNext(moves, scores, i);
                var m = moves[i];
                bool quiet = !m.IsCapture && !m.IsPromotion;

                pos.MakeMove(m);
                if (pos.IsInCheck(us)) { pos.UnmakeMove(); continue; }
                legal++;

                bool givesCheck = false;
                if (quiet && legal > 1 && (canFutility || depth >= 3)) givesCheck = pos.InCheck;

                if (canFutility && quiet && legal > 1 && !givesCheck)
                {
                    pos.UnmakeMove();
                    continue;
                }

                int newDepth = depth - 1;
                int score;
                if (legal == 1)
                {
                    score = -Search(newDepth, -beta, -alpha, ply + 1, true);
                }
                else
                {
                    int r = 0;
                    if (depth >= 3 && legal > (pvNode ? 4 : 3) && quiet && !inCheck && !givesCheck
                        && m != killer[0] && m != killer[1])
                    {
                        r = LmrTable[Math.Min(depth, 63), Math.Min(legal, 63)];
                        if (pvNode) r--;
                        if (history[HistoryIndex(us, m)] > 20000) r--;
                        if (r > newDepth - 1) r = newDepth - 1;
                        if (r < 0) r = 0;
                    }
                    score = -Search(newDepth - r, -alpha - 1, -alpha, ply + 1, true);
                    if (score > alpha && r > 0 && !stopped)
                        score = -Search(newDepth, -alpha - 1, -alpha, ply + 1, true);
                    if (score > alpha && score < beta && !stopped)
                        score = -Search(newDepth, -beta, -alpha, ply + 1, true);
                }
                pos.UnmakeMove();
                if (stopped) return 0;

                if (score > best)
                {
                    best = score;
                    bestMove = m;
                    if (score > alpha)
                    {
                        alpha = score;
                        UpdatePv(ply, m);
                        if (score >= beta)
                        {
                            if (quiet)
                            {
                                if (killer[0] != m) { killer[1] = killer[0]; killer[0] = m; }
                                AddHistory(us, m, depth * depth);
                            }
                            break;
                        }
                    }
                }
            }

            if (legal == 0) return inCheck ? -MateScore + ply : 0;

            Bound bound = best >= beta ? Bound.Lower : best > originalAlpha ? Bound.Exact : Bound.Upper;
            tt.Store(key, bound == Bound.Upper ? Move.Null : bestMove, ToTt(best, ply), depth, bound);
            return best;
        }

        // =====================================================================
        // Quiescence : prises et promotions (évasions complètes si en échec)
        // =====================================================================

        int QSearch(int alpha, int beta, int ply)
        {
            pvLength[ply] = ply;
            if ((++nodes & 1023) == 0) CheckStop();
            if (stopped) return 0;
            if (ply >= MaxPly - 2) return evaluator.Evaluate(pos);

            bool inCheck = pos.InCheck;
            int best, standPat = 0;
            if (!inCheck)
            {
                standPat = evaluator.Evaluate(pos);
                if (standPat >= beta) return standPat;
                // Delta pruning global : même une dame gagnée ne suffirait pas.
                if (standPat + 1150 < alpha) return standPat;
                if (standPat > alpha) alpha = standPat;
                best = standPat;
            }
            else best = -Infinity;

            var moves = moveLists[ply];
            var scores = moveScores[ply];
            moves.Clear(); // GeneratePseudoLegal ajoute sans vider
            MoveGenerator.GeneratePseudoLegal(pos, moves, !inCheck);
            ScoreMoves(moves, scores, Move.Null, ply, true);
            var us = pos.SideToMove;
            int legal = 0;

            for (int i = 0; i < moves.Count; i++)
            {
                PickNext(moves, scores, i);
                var m = moves[i];

                if (!inCheck)
                {
                    if (m.IsPromotion && m.Promotion != PieceType.Queen) continue;
                    if (!m.IsPromotion)
                    {
                        int victim = m.IsEnPassant ? (int)PieceType.Pawn : pos[m.To] & 7;
                        // Delta pruning par coup.
                        if (standPat + Evaluator.SimpleValue[victim] + 200 <= alpha) continue;
                        // Prises perdantes (SEE négatif).
                        int attacker = pos[m.From] & 7;
                        if (Evaluator.SimpleValue[attacker] > Evaluator.SimpleValue[victim] && See(m) < 0) continue;
                    }
                }

                pos.MakeMove(m);
                if (pos.IsInCheck(us)) { pos.UnmakeMove(); continue; }
                legal++;
                int score = -QSearch(-beta, -alpha, ply + 1);
                pos.UnmakeMove();
                if (stopped) return 0;

                if (score > best)
                {
                    best = score;
                    if (score > alpha)
                    {
                        alpha = score;
                        if (score >= beta) break;
                    }
                }
            }

            if (inCheck && legal == 0) return -MateScore + ply;
            return best;
        }

        // =====================================================================
        // Tri des coups
        // =====================================================================

        const int TtMoveScore = 30000000;
        const int GoodCaptureScore = 20000000;
        const int Killer1Score = 19000000;
        const int Killer2Score = 18000000;
        const int BadCaptureScore = -1000000;
        const int HistoryMax = 1000000;

        void ScoreMoves(List<Move> moves, int[] scores, Move ttMove, int ply, bool qsearch)
        {
            var killer = killers[ply];
            var us = pos.SideToMove;
            for (int i = 0; i < moves.Count; i++)
            {
                var m = moves[i];
                int s;
                if (m == ttMove && !ttMove.IsNull) s = TtMoveScore;
                else if (m.IsCapture || m.IsPromotion)
                {
                    int attacker = pos[m.From] & 7;
                    int victim = m.IsEnPassant ? (int)PieceType.Pawn : pos[m.To] & 7;
                    s = Evaluator.SimpleValue[victim] * 10 - attacker;
                    if (m.IsPromotion)
                    {
                        if (m.Promotion == PieceType.Queen) s += GoodCaptureScore + 9000;
                        else s = BadCaptureScore - 100000 + (int)m.Promotion;
                    }
                    else if (!qsearch && Evaluator.SimpleValue[attacker] > Evaluator.SimpleValue[victim] && See(m) < 0)
                        s += BadCaptureScore;
                    else s += GoodCaptureScore;
                }
                else if (m == killer[0]) s = Killer1Score;
                else if (m == killer[1]) s = Killer2Score;
                else s = history[HistoryIndex(us, m)];
                scores[i] = s;
            }
        }

        static void PickNext(List<Move> moves, int[] scores, int start)
        {
            int bestIdx = start, bestScore = scores[start];
            for (int j = start + 1; j < moves.Count; j++)
                if (scores[j] > bestScore) { bestScore = scores[j]; bestIdx = j; }
            if (bestIdx != start)
            {
                var tm = moves[start]; moves[start] = moves[bestIdx]; moves[bestIdx] = tm;
                scores[bestIdx] = scores[start]; scores[start] = bestScore;
            }
        }

        static int HistoryIndex(PieceColor side, Move m) => ((int)side << 12) | (m.From << 6) | m.To;

        void AddHistory(PieceColor side, Move m, int bonus)
        {
            int idx = HistoryIndex(side, m);
            history[idx] += bonus;
            if (history[idx] > HistoryMax)
                for (int i = 0; i < history.Length; i++) history[i] /= 2;
        }

        // =====================================================================
        // Échange statique (SEE)
        // =====================================================================

        /// <summary>Gain matériel attendu de la prise m après la séquence d'échanges sur la case d'arrivée.</summary>
        public int See(Move m)
        {
            int from = m.From, to = m.To;
            int victim = m.IsEnPassant ? (int)PieceType.Pawn : pos[to] & 7;
            ulong removed = 1UL << from;
            if (m.IsEnPassant) removed |= 1UL << (pos.SideToMove == PieceColor.White ? to - 8 : to + 8);

            var gain = seeGain;
            int d = 0;
            gain[0] = Evaluator.SimpleValue[victim];
            int attackerValue = Evaluator.SimpleValue[m.IsPromotion ? (int)m.Promotion : pos[from] & 7];
            int side = 1 - ((pos[from] >> 3) & 1);

            while (d < gain.Length - 2)
            {
                d++;
                gain[d] = attackerValue - gain[d - 1];
                if (Math.Max(-gain[d - 1], gain[d]) < 0) break;
                int sq = LeastValuableAttacker(to, side, removed, out int type);
                if (sq < 0) break;
                attackerValue = Evaluator.SimpleValue[type];
                removed |= 1UL << sq;
                side ^= 1;
            }
            while (--d > 0) gain[d - 1] = -Math.Max(-gain[d - 1], gain[d]);
            return gain[0];
        }

        int LeastValuableAttacker(int to, int side, ulong removed, out int type)
        {
            var color = (PieceColor)side;
            int pawn = Piece.Make(color, PieceType.Pawn);
            var pa = Tables.PawnAttacks[1 - side][to];
            for (int i = 0; i < pa.Length; i++)
            {
                int s = pa[i];
                if (pos[s] == pawn && ((removed >> s) & 1) == 0) { type = (int)PieceType.Pawn; return s; }
            }
            int knight = Piece.Make(color, PieceType.Knight);
            var kt = Tables.KnightTargets[to];
            for (int i = 0; i < kt.Length; i++)
            {
                int s = kt[i];
                if (pos[s] == knight && ((removed >> s) & 1) == 0) { type = (int)PieceType.Knight; return s; }
            }

            int bishop = Piece.Make(color, PieceType.Bishop);
            int rook = Piece.Make(color, PieceType.Rook);
            int queen = Piece.Make(color, PieceType.Queen);
            int queenSq = -1, rookSq = -1;
            for (int d = 0; d < 8; d++)
            {
                var ray = Tables.Rays[d][to];
                for (int i = 0; i < ray.Length; i++)
                {
                    int s = ray[i];
                    if (((removed >> s) & 1) != 0) continue;
                    int p = pos[s];
                    if (p == Piece.Empty) continue;
                    if (d >= 4 && p == bishop) { type = (int)PieceType.Bishop; return s; }
                    if (d < 4 && p == rook) rookSq = s;
                    if (p == queen) queenSq = s;
                    break;
                }
            }
            if (rookSq >= 0) { type = (int)PieceType.Rook; return rookSq; }
            if (queenSq >= 0) { type = (int)PieceType.Queen; return queenSq; }

            int king = Piece.Make(color, PieceType.King);
            var kk = Tables.KingTargets[to];
            for (int i = 0; i < kk.Length; i++)
            {
                int s = kk[i];
                if (pos[s] == king && ((removed >> s) & 1) == 0) { type = (int)PieceType.King; return s; }
            }
            type = 0;
            return -1;
        }

        // =====================================================================
        // Utilitaires
        // =====================================================================

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

        static int ToTt(int score, int ply)
        {
            if (score > MateBound) return score + ply;
            if (score < -MateBound) return score - ply;
            return score;
        }

        static int FromTt(int score, int ply)
        {
            if (score > MateBound) return score - ply;
            if (score < -MateBound) return score + ply;
            return score;
        }
    }
}
