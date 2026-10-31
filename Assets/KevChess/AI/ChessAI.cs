using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using KevChess.Core;

namespace KevChess.AI
{
    public enum Difficulty { Debutant = 0, Facile = 1, Moyen = 2, Fort = 3, Maitre = 4 }

    /// <summary>Réglages d'un niveau : profondeur et temps max, imprécision volontaire, livre d'ouvertures.</summary>
    public sealed class DifficultySettings
    {
        public Difficulty Level;
        public string DisplayName;
        public int MaxDepth;
        public int MaxTimeMs;
        /// <summary>Écart max (centipions) avec le meilleur coup pour qu'un coup soit candidat. 0 = toujours le meilleur.</summary>
        public int CandidateMargin;
        /// <summary>« Température » du tirage pondéré : poids = exp(-(meilleur - score) / température).</summary>
        public int Temperature;
        /// <summary>Probabilité de ne pas voir un mat forcé disponible (0 = jamais).</summary>
        public double MissMateChance;
        public bool UseBook;
    }

    /// <summary>Options passées à un moteur (ChessAI aujourd'hui, Stockfish demain).</summary>
    public sealed class EngineOptions
    {
        public Difficulty Level = Difficulty.Moyen;
        /// <summary>Remplace le temps max du niveau si > 0.</summary>
        public int MoveTimeMs;
        /// <summary>Remplace la profondeur max du niveau si > 0.</summary>
        public int MaxDepth;
        /// <summary>Autorise le livre d'ouvertures (si le niveau l'utilise).</summary>
        public bool AllowBook = true;
    }

    /// <summary>Moteur d'échecs branchable par le jeu.</summary>
    public interface IChessEngine
    {
        /// <summary>
        /// Cherche un coup légal pour la position (non modifiée). Lève OperationCanceledException si ct est annulé.
        /// Renvoie Move.Null si la partie est finie (aucun coup légal).
        /// </summary>
        Task<Move> FindMoveAsync(Position position, EngineOptions options, CancellationToken ct);
    }

    /// <summary>
    /// Façade de l'IA pour le jeu : niveaux, recherche en tâche de fond, indice.
    /// Les appels concurrents sur la même instance sont sérialisés (un moteur pour jouer, un autre pour l'indice).
    /// </summary>
    public sealed class ChessAI : IChessEngine
    {
        static readonly DifficultySettings[] Levels =
        {
            new DifficultySettings { Level = Difficulty.Debutant, DisplayName = "Débutant", MaxDepth = 2, MaxTimeMs = 300,  CandidateMargin = 280, Temperature = 110, MissMateChance = 0.35, UseBook = false },
            new DifficultySettings { Level = Difficulty.Facile,   DisplayName = "Facile",   MaxDepth = 3, MaxTimeMs = 500,  CandidateMargin = 140, Temperature = 55,  MissMateChance = 0.10, UseBook = false },
            new DifficultySettings { Level = Difficulty.Moyen,    DisplayName = "Moyen",    MaxDepth = 5, MaxTimeMs = 900,  CandidateMargin = 50,  Temperature = 20,  MissMateChance = 0,    UseBook = true },
            new DifficultySettings { Level = Difficulty.Fort,     DisplayName = "Fort",     MaxDepth = 9, MaxTimeMs = 1500, CandidateMargin = 12,  Temperature = 6,   MissMateChance = 0,    UseBook = true },
            new DifficultySettings { Level = Difficulty.Maitre,   DisplayName = "Maître",   MaxDepth = 64, MaxTimeMs = 3000, CandidateMargin = 0,  Temperature = 1,   MissMateChance = 0,    UseBook = true },
        };

        public static DifficultySettings GetSettings(Difficulty level) => Levels[(int)level];
        public static IReadOnlyList<DifficultySettings> AllLevels => Levels;

        readonly Searcher engine;
        readonly Searcher hint;
        readonly object engineGate = new object();
        readonly object hintGate = new object();
        readonly object randomGate = new object();
        readonly Random random;

        /// <summary>Résultat brut de la dernière recherche du moteur de jeu (diagnostic).</summary>
        public SearchResult LastResult { get; private set; }

        /// <summary>Vrai si le dernier coup du moteur de jeu vient du livre d'ouvertures (pas de recherche).</summary>
        public bool LastFromBook { get; private set; }

        /// <param name="hashMb">Taille de la table de transposition du moteur de jeu (l'indice utilise la moitié).</param>
        /// <param name="seed">Graine du hasard (tests reproductibles) ; null = aléatoire.</param>
        public ChessAI(int hashMb = 16, int? seed = null)
        {
            engine = new Searcher(hashMb);
            hint = new Searcher(Math.Max(1, hashMb / 2));
            random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>À appeler en début de nouvelle partie.</summary>
        public void NewGame()
        {
            lock (engineGate) engine.Reset();
            lock (hintGate) hint.Reset();
        }

        // ---------------------------------------------------------------
        // Pour le jeu
        // ---------------------------------------------------------------

        /// <summary>
        /// Coup de l'IA pour la partie, calculé hors du thread de rendu. La position est copiée immédiatement
        /// (sur le thread appelant) : la partie peut changer pendant la réflexion sans danger.
        /// Lève OperationCanceledException si ct est annulé.
        /// </summary>
        public Task<Move> FindMoveAsync(ChessGame game, Difficulty level, CancellationToken ct = default)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            return FindMoveAsync(game.Position, new EngineOptions { Level = level }, ct);
        }

        public Task<Move> FindMoveAsync(Position position, EngineOptions options, CancellationToken ct)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            var copy = position.Clone();
            options = options ?? new EngineOptions();
            return Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var move = FindMove(copy, options, ct);
                ct.ThrowIfCancellationRequested();
                return move;
            }, ct);
        }

        /// <summary>Version synchrone avec niveau (imprécision et livre compris). Renvoie le meilleur coup connu si annulé.</summary>
        public Move FindMove(Position position, Difficulty level, CancellationToken ct = default) =>
            FindMove(position, new EngineOptions { Level = level }, ct);

        public Move FindMove(Position position, EngineOptions options, CancellationToken ct = default)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            options = options ?? new EngineOptions();
            var settings = GetSettings(options.Level);

            if (settings.UseBook && options.AllowBook)
            {
                Move book;
                lock (randomGate) book = OpeningBook.Pick(position, random);
                LastFromBook = !book.IsNull;
                if (LastFromBook) return book;
            }
            LastFromBook = false;

            var limits = new SearchLimits
            {
                MaxDepth = options.MaxDepth > 0 ? options.MaxDepth : settings.MaxDepth,
                MaxTimeMs = options.MoveTimeMs > 0 ? options.MoveTimeMs : settings.MaxTimeMs,
                CancellationToken = ct,
                ExactRootScores = settings.CandidateMargin > 0,
            };

            SearchResult result;
            lock (engineGate) result = engine.Search(position, limits);
            LastResult = result;
            if (result.BestMove.IsNull) return Move.Null;
            return settings.CandidateMargin > 0 ? PickHumanLike(result, settings) : result.BestMove;
        }

        /// <summary>Recherche pure (sans livre ni imprécision) sur une copie de la position.</summary>
        public Move FindMove(Position position, SearchLimits limits)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            SearchResult result;
            lock (engineGate) result = engine.Search(position, limits ?? new SearchLimits());
            LastResult = result;
            return result.BestMove;
        }

        /// <summary>Recherche pure renvoyant tout le détail (score, profondeur, variante principale...).</summary>
        public SearchResult Analyze(Position position, SearchLimits limits)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            SearchResult result;
            lock (engineGate) result = engine.Search(position, limits ?? new SearchLimits());
            LastResult = result;
            return result;
        }

        // ---------------------------------------------------------------
        // Indice
        // ---------------------------------------------------------------

        /// <summary>Meilleur coup pour le bouton « indice » (moteur séparé : n'interfère pas avec l'IA adverse).</summary>
        public Move SuggestMove(Position position, int timeMs = 800, CancellationToken ct = default)
        {
            if (position == null) throw new ArgumentNullException(nameof(position));
            var limits = new SearchLimits { MaxTimeMs = Math.Max(1, timeMs), MaxDepth = 64, CancellationToken = ct };
            lock (hintGate) return hint.Search(position, limits).BestMove;
        }

        public Move SuggestMove(ChessGame game, int timeMs = 800, CancellationToken ct = default)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            return SuggestMove(game.Position, timeMs, ct);
        }

        /// <summary>Indice calculé hors du thread de rendu. Lève OperationCanceledException si ct est annulé.</summary>
        public Task<Move> SuggestMoveAsync(ChessGame game, int timeMs = 800, CancellationToken ct = default)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var copy = game.Position.Clone();
            return Task.Run(() =>
            {
                var m = SuggestMove(copy, timeMs, ct);
                ct.ThrowIfCancellationRequested();
                return m;
            }, ct);
        }

        // ---------------------------------------------------------------
        // Imprécision « humaine »
        // ---------------------------------------------------------------

        Move PickHumanLike(SearchResult result, DifficultySettings s)
        {
            var scores = result.RootScores;
            if (scores.Length <= 1) return result.BestMove;
            int best = scores[0].Score;

            lock (randomGate)
            {
                // Un mat forcé trouvé est joué, sauf distraction occasionnelle aux bas niveaux.
                if (best > Searcher.MateBound && random.NextDouble() >= s.MissMateChance)
                    return scores[0].Move;

                // On ne choisit jamais un coup qui se fait mater ou qui perd beaucoup plus que la marge.
                var candidates = new List<RootMoveScore>();
                int reference = best > Searcher.MateBound ? FirstNonMate(scores) : best;
                foreach (var rs in scores)
                {
                    if (rs.Score < -Searcher.MateBound) continue;
                    if (rs.Score > Searcher.MateBound || rs.Score >= reference - s.CandidateMargin) candidates.Add(rs);
                }
                if (candidates.Count == 0) return result.BestMove;

                double total = 0;
                var weights = new double[candidates.Count];
                for (int i = 0; i < candidates.Count; i++)
                {
                    int sc = Math.Min(candidates[i].Score, reference);
                    weights[i] = Math.Exp(-(reference - sc) / (double)Math.Max(1, s.Temperature));
                    total += weights[i];
                }
                double r = random.NextDouble() * total;
                for (int i = 0; i < candidates.Count; i++)
                {
                    r -= weights[i];
                    if (r <= 0) return candidates[i].Move;
                }
                return candidates[0].Move;
            }
        }

        static int FirstNonMate(RootMoveScore[] scores)
        {
            foreach (var rs in scores)
                if (Math.Abs(rs.Score) <= Searcher.MateBound) return rs.Score;
            return scores[0].Score;
        }
    }
}
