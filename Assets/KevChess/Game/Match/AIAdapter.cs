using System.Threading;
using System.Threading.Tasks;
using KevChess.AI;
using KevChess.Core;

namespace KevChess.Game.Match
{
    /// <summary>Lien entre la partie et le moteur. Seul point à changer pour brancher Stockfish (V1.1).</summary>
    public static class AIAdapter
    {
        static ChessAI engine;
        static ChessAI Engine => engine ?? (engine = new ChessAI(hashMb: 16));

        public static Task<Move> FindMoveAsync(ChessGame game, AILevel level, CancellationToken ct) =>
            Engine.FindMoveAsync(game, ToDifficulty(level), ct);

        /// <summary>
        /// Coup de l'IA avec une pendule : le temps de réflexion du niveau est réduit pour ne pas tomber
        /// (environ 1/30 du temps restant plus l'incrément).
        /// </summary>
        public static Task<Move> FindMoveAsync(ChessGame game, AILevel level, double remainingSeconds, int incrementSeconds, CancellationToken ct)
        {
            var difficulty = ToDifficulty(level);
            if (double.IsInfinity(remainingSeconds)) return Engine.FindMoveAsync(game, difficulty, ct);
            int levelMs = ChessAI.GetSettings(difficulty).MaxTimeMs;
            double budget = (remainingSeconds / 30.0 + incrementSeconds * 0.8) * 1000.0;
            int ms = (int)System.Math.Max(50, System.Math.Min(levelMs, budget));
            return Engine.FindMoveAsync(game.Position, new EngineOptions { Level = difficulty, MoveTimeMs = ms }, ct);
        }

        /// <summary>L'IA (camp aiSide) accepte-t-elle la nulle proposée ?</summary>
        public static Task<bool> AcceptsDrawAsync(ChessGame game, PieceColor aiSide, CancellationToken ct) =>
            Engine.AcceptsDrawAsync(game, aiSide, ct);

        public static Task<Move> SuggestAsync(ChessGame game, CancellationToken ct) =>
            Engine.SuggestMoveAsync(game, 900, ct);

        public static void NewGame() => engine?.NewGame();

        /// <summary>Résumé de la dernière réflexion de l'IA de jeu, pour le journal.</summary>
        public static string LastSearchSummary()
        {
            if (engine == null) return "";
            if (engine.LastFromBook) return "livre d'ouvertures";
            var r = engine.LastResult;
            return r == null ? "" : "profondeur " + r.Depth + ", " + r.Nodes + " nœuds, recherche " + r.ElapsedMs + " ms, " + r.NodesPerSecond + " nœuds/s";
        }

        public static Difficulty DifficultyOf(AILevel level) => ToDifficulty(level);

        static Difficulty ToDifficulty(AILevel level)
        {
            switch (level)
            {
                case AILevel.Beginner: return Difficulty.Debutant;
                case AILevel.Easy: return Difficulty.Facile;
                case AILevel.Medium: return Difficulty.Moyen;
                case AILevel.Strong: return Difficulty.Fort;
                default: return Difficulty.Maitre;
            }
        }
    }
}
