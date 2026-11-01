using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KevChess.Core.Fantasy;

namespace KevChess.AI.Fantasy
{
    /// <summary>Moteur du mode Légendes, pendant de IChessEngine.</summary>
    public interface IFantasyEngine
    {
        void NewGame();
        Task<FantasyAction> FindActionAsync(FantasyGame game, Difficulty level, CancellationToken ct = default);
        Task<FantasyAction> SuggestActionAsync(FantasyGame game, int timeMs = 800, CancellationToken ct = default);
    }

    /// <summary>
    /// IA du mode Légendes : mêmes 5 niveaux que le mode classique (profondeur, temps, imprécision « humaine »),
    /// sans livre d'ouvertures (les pouvoirs changent la théorie).
    /// </summary>
    public sealed class FantasyAI : IFantasyEngine
    {
        readonly FantasySearcher engine;
        readonly FantasySearcher hint;
        readonly object engineGate = new object(), hintGate = new object(), randomGate = new object();
        readonly Random random;

        public FantasySearchResult LastResult { get; private set; }

        public FantasyAI(int hashMb = 16, int? seed = null)
        {
            engine = new FantasySearcher(hashMb);
            hint = new FantasySearcher(Math.Max(1, hashMb / 2));
            random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public void NewGame()
        {
            lock (engineGate) engine.Reset();
            lock (hintGate) hint.Reset();
        }

        public Task<FantasyAction> FindActionAsync(FantasyGame game, Difficulty level, CancellationToken ct = default)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var copy = game.Position.Clone();
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var a = FindAction(copy, level, ct);
                ct.ThrowIfCancellationRequested();
                return a;
            }, ct);
        }

        public FantasyAction FindAction(FantasyPosition position, Difficulty level, CancellationToken ct = default, int moveTimeMs = 0)
        {
            var s = ChessAI.GetSettings(level);
            var limits = new SearchLimits
            {
                MaxDepth = s.MaxDepth,
                MaxTimeMs = moveTimeMs > 0 ? moveTimeMs : s.MaxTimeMs,
                CancellationToken = ct,
            };
            FantasySearchResult result;
            lock (engineGate) result = engine.Search(position, limits, s.CandidateMargin > 0);
            LastResult = result;
            if (result.BestAction.IsNull) return FantasyAction.Null;
            return s.CandidateMargin > 0 ? PickHumanLike(result, s) : result.BestAction;
        }

        /// <summary>Recherche pure sur une copie de la position.</summary>
        public FantasySearchResult Analyze(FantasyPosition position, SearchLimits limits)
        {
            FantasySearchResult result;
            lock (engineGate) result = engine.Search(position, limits);
            LastResult = result;
            return result;
        }

        public Task<FantasyAction> SuggestActionAsync(FantasyGame game, int timeMs = 800, CancellationToken ct = default)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var copy = game.Position.Clone();
            return Task.Run(() =>
            {
                FantasySearchResult r;
                lock (hintGate) r = hint.Search(copy, new SearchLimits { MaxTimeMs = Math.Max(1, timeMs), CancellationToken = ct });
                ct.ThrowIfCancellationRequested();
                return r.BestAction;
            }, ct);
        }

        FantasyAction PickHumanLike(FantasySearchResult result, DifficultySettings s)
        {
            var scores = result.RootScores;
            if (scores.Length <= 1) return result.BestAction;
            int best = scores[0].Value;
            lock (randomGate)
            {
                if (best > FantasySearcher.MateBound && random.NextDouble() >= s.MissMateChance) return scores[0].Key;
                int reference = best;
                if (best > FantasySearcher.MateBound)
                    foreach (var kv in scores)
                        if (Math.Abs(kv.Value) <= FantasySearcher.MateBound) { reference = kv.Value; break; }

                var candidates = new List<KeyValuePair<FantasyAction, int>>();
                foreach (var kv in scores)
                {
                    if (kv.Value < -FantasySearcher.MateBound) continue;
                    if (kv.Value > FantasySearcher.MateBound || kv.Value >= reference - s.CandidateMargin) candidates.Add(kv);
                }
                if (candidates.Count == 0) return result.BestAction;
                double total = 0;
                var w = new double[candidates.Count];
                for (int i = 0; i < candidates.Count; i++)
                {
                    int sc = Math.Min(candidates[i].Value, reference);
                    w[i] = Math.Exp(-(reference - sc) / (double)Math.Max(1, s.Temperature));
                    total += w[i];
                }
                double r = random.NextDouble() * total;
                for (int i = 0; i < candidates.Count; i++) { r -= w[i]; if (r <= 0) return candidates[i].Key; }
                return candidates[0].Key;
            }
        }
    }
}
