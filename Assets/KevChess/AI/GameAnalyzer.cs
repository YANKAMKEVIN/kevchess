using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KevChess.Core;

namespace KevChess.AI
{
    public enum MoveQuality { Excellent, Bon, Imprecision, Erreur, Gaffe }

    /// <summary>Verdict sur un coup joué. Les évaluations sont en centipions, vues des Blancs (mat = ±10000).</summary>
    public sealed class MoveVerdict
    {
        /// <summary>Index du demi-coup dans la partie (0 = premier coup).</summary>
        public int Ply;
        public string San;
        public PieceColor Side;
        public MoveQuality Quality;
        public int EvalBeforeCp;
        public int EvalAfterCp;
        /// <summary>Meilleur coup selon le moteur dans la position d'avant (SAN).</summary>
        public string BestSan;
        public Move Move;
        public Move BestMove;
        /// <summary>Perte en centipions du point de vue du joueur (évaluations bornées à ±1000).</summary>
        public int LossCp;
    }

    public sealed class GameAnalysis
    {
        public List<MoveVerdict> Moves = new List<MoveVerdict>();
        /// <summary>Précision 0–100 (100 si le camp n'a joué aucun coup).</summary>
        public double AccuracyWhite;
        public double AccuracyBlack;
    }

    /// <summary>
    /// Analyse a posteriori d'une partie : chaque position est évaluée par le Searcher, puis chaque coup est jugé
    /// sur la perte qu'il cause par rapport au meilleur coup. Une instance = une analyse à la fois.
    /// </summary>
    public sealed class GameAnalyzer
    {
        /// <summary>Score affiché pour un mat (vu du camp qui mate).</summary>
        public const int MateCp = 10000;
        /// <summary>Borne des évaluations pour le calcul de la perte : une position déjà gagnée le reste.</summary>
        public const int LossClampCp = 1000;
        /// <summary>Perte maximale comptée par coup dans la précision.</summary>
        public const int AccuracyLossCap = 1000;

        readonly Searcher searcher;
        readonly object gate = new object();

        public GameAnalyzer(int hashMb = 16)
        {
            searcher = new Searcher(hashMb);
        }

        /// <summary>Profondeur maximale par position (le temps par position reste la limite principale).</summary>
        public int MaxDepth { get; set; } = 64;

        /// <summary>Analyse hors du thread appelant. Lève OperationCanceledException si ct est annulé.</summary>
        public Task<GameAnalysis> AnalyzeAsync(string startFen, IReadOnlyList<Move> moves, int msPerPosition,
            IProgress<float> progress, CancellationToken ct)
        {
            var copy = new List<Move>(moves ?? throw new ArgumentNullException(nameof(moves)));
            return Task.Run(() => Analyze(startFen, copy, msPerPosition, progress, ct), ct);
        }

        /// <summary>Version synchrone. Lève ArgumentException si un coup est illégal.</summary>
        public GameAnalysis Analyze(string startFen, IReadOnlyList<Move> moves, int msPerPosition,
            IProgress<float> progress, CancellationToken ct)
        {
            if (moves == null) throw new ArgumentNullException(nameof(moves));
            lock (gate)
            {
                searcher.Reset();
                var game = new ChessGame(string.IsNullOrEmpty(startFen) ? Position.StartFen : startFen);
                int n = moves.Count;
                var evals = new int[n + 1];
                var bests = new Move[n + 1];
                var bestSans = new string[n + 1];

                for (int i = 0; i <= n; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    evals[i] = Evaluate(game, msPerPosition, ct, out bests[i]);
                    bestSans[i] = bests[i].IsNull ? null : Notation.ToSan(game.Position, bests[i]);
                    progress?.Report((i + 1) / (float)(n + 1));
                    if (i == n) break;
                    if (!game.TryMakeMove(moves[i]))
                        throw new ArgumentException("Coup illégal au demi-coup " + i + " : " + moves[i].ToUci(), nameof(moves));
                }

                var result = new GameAnalysis();
                double lossWhite = 0, lossBlack = 0;
                int countWhite = 0, countBlack = 0;
                var start = Position.FromFen(game.StartFen);
                var side = start.SideToMove;
                for (int i = 0; i < n; i++)
                {
                    int loss = Loss(evals[i], evals[i + 1], side);
                    bool best = moves[i] == bests[i];
                    var verdict = new MoveVerdict
                    {
                        Ply = i,
                        San = game.SanMoves[i],
                        Side = side,
                        Move = moves[i],
                        BestMove = bests[i],
                        BestSan = bestSans[i],
                        EvalBeforeCp = evals[i],
                        EvalAfterCp = evals[i + 1],
                        LossCp = best ? 0 : loss,
                        Quality = best ? MoveQuality.Excellent : Classify(loss),
                    };
                    result.Moves.Add(verdict);
                    if (side == PieceColor.White) { lossWhite += Math.Min(verdict.LossCp, AccuracyLossCap); countWhite++; }
                    else { lossBlack += Math.Min(verdict.LossCp, AccuracyLossCap); countBlack++; }
                    side = Piece.Opposite(side);
                }
                result.AccuracyWhite = countWhite == 0 ? 100 : Accuracy(lossWhite / countWhite);
                result.AccuracyBlack = countBlack == 0 ? 100 : Accuracy(lossBlack / countBlack);
                return result;
            }
        }

        /// <summary>Évaluation vue des Blancs de la position courante de la partie (fins de partie comprises).</summary>
        int Evaluate(ChessGame game, int ms, CancellationToken ct, out Move best)
        {
            best = Move.Null;
            if (game.IsOver)
            {
                if (game.EndReason == GameEndReason.Checkmate)
                    return game.Result == GameResult.WhiteWins ? MateCp : -MateCp;
                return 0;
            }
            var limits = new SearchLimits
            {
                MaxTimeMs = Math.Max(1, ms),
                MaxDepth = Math.Max(1, MaxDepth),
                CancellationToken = ct,
            };
            var r = searcher.Search(game.Position, limits);
            ct.ThrowIfCancellationRequested();
            best = r.BestMove;
            int score = ToCp(r.Score);
            return game.SideToMove == PieceColor.White ? score : -score;
        }

        /// <summary>Score moteur (camp au trait) → centipions, mats ramenés à ±10000.</summary>
        public static int ToCp(int engineScore)
        {
            if (engineScore > Searcher.MateBound) return MateCp;
            if (engineScore < -Searcher.MateBound) return -MateCp;
            return Math.Max(-MateCp, Math.Min(MateCp, engineScore));
        }

        /// <summary>Perte du joueur entre l'évaluation d'avant (meilleur coup) et celle d'après son coup.</summary>
        public static int Loss(int beforeWhiteCp, int afterWhiteCp, PieceColor mover)
        {
            int before = Clamp(beforeWhiteCp), after = Clamp(afterWhiteCp);
            int diff = mover == PieceColor.White ? before - after : after - before;
            return Math.Max(0, diff);
        }

        static int Clamp(int cp) => Math.Max(-LossClampCp, Math.Min(LossClampCp, cp));

        /// <summary>Seuils : ≤20 Excellent, ≤60 Bon, ≤150 Imprécision, ≤300 Erreur, au-delà Gaffe.</summary>
        public static MoveQuality Classify(int lossCp)
        {
            if (lossCp <= 20) return MoveQuality.Excellent;
            if (lossCp <= 60) return MoveQuality.Bon;
            if (lossCp <= 150) return MoveQuality.Imprecision;
            if (lossCp <= 300) return MoveQuality.Erreur;
            return MoveQuality.Gaffe;
        }

        /// <summary>
        /// Précision 0–100 d'après la perte moyenne (courbe exponentielle douce, inspirée de lichess) :
        /// 0 → 100, 20 → ~88, 50 → ~73, 100 → ~54, 300 → ~14.
        /// </summary>
        public static double Accuracy(double averageLossCp)
        {
            double loss = Math.Max(0, Math.Min(AccuracyLossCap, averageLossCp));
            double a = 103.1668 * Math.Exp(-0.006 * loss) - 3.1669;
            return Math.Max(0, Math.Min(100, a));
        }
    }
}
