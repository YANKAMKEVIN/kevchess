using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KevChess.AI;
using KevChess.AI.Uci;
using KevChess.Core;
using UnityEngine;

namespace KevChess.Game.Match
{
    /// <summary>
    /// Lien entre la partie et le moteur. Seul point à changer pour brancher Stockfish (V1.1) :
    /// le réglage « Moteur de l'IA » choisit l'IA maison (par défaut) ou un Stockfish externe (UCI) ;
    /// si Stockfish manque ou échoue, l'IA maison joue le coup (FallbackEngine). L'indice et la nulle
    /// restent calculés par l'IA maison.
    /// </summary>
    public static class AIAdapter
    {
        static ChessAI engine;
        static ChessAI Engine => engine ?? (engine = new ChessAI(hashMb: 16));

        static UciEngine stockfish;
        static bool stockfishStartFailed;
        static FallbackEngine fallback;
        static bool lastFromStockfish;

        /// <summary>Moteur voulu par le joueur (GamePrefs). L'IA maison reste la valeur par défaut.</summary>
        public static EngineKind Wanted { get; set; } = EngineKind.KevChess;

        /// <summary>Moteur réellement utilisé pour le prochain coup.</summary>
        public static EngineKind Active => FallbackEngine.Resolve(Wanted, StockfishUsable);

        /// <summary>Un binaire Stockfish est présent et n'a pas échoué pendant cette session.</summary>
        public static bool StockfishUsable =>
            !stockfishStartFailed && (stockfish == null ? StockfishPath() != null : !stockfish.IsBroken);

        /// <summary>
        /// Chemin d'un exécutable Stockfish.
        /// Android : le binaire livré dans l'APK comme bibliothèque native (libstockfish.so dans nativeLibraryDir,
        /// voir docs/tech/etat-projet.md, « Stockfish »). iOS : null (lancer un exécutable y est interdit).
        /// Mac, Linux, Windows et éditeur : argument « -kevchess-stockfish &lt;chemin&gt; », variable KEVCHESS_STOCKFISH,
        /// ou StreamingAssets/stockfish/stockfish (« .exe » sous Windows).
        /// </summary>
        public static string StockfishPath()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return null;
#elif UNITY_ANDROID && !UNITY_EDITOR
            if (!androidPathResolved)
            {
                androidPathResolved = true;
                var dir = AndroidUciTransport.NativeLibraryDir();
                var path = dir == null ? null : Path.Combine(dir, AndroidStockfishLibrary);
                androidPath = path != null && File.Exists(path) ? path : null;
                if (androidPath == null) Debug.LogWarning("[KevChess] Stockfish absent de l'APK (" + (path ?? "nativeLibraryDir inconnu") + ")");
            }
            return androidPath;
#else
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-kevchess-stockfish" && File.Exists(args[i + 1])) return args[i + 1];
            var env = Environment.GetEnvironmentVariable("KEVCHESS_STOCKFISH");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;
            var bundled = Path.Combine(Application.streamingAssetsPath, "stockfish", "stockfish");
            if (File.Exists(bundled)) return bundled;
            if (File.Exists(bundled + ".exe")) return bundled + ".exe";
            return null;
#endif
        }

        /// <summary>Nom du binaire Stockfish dans nativeLibraryDir (Assets/Plugins/Android/libs/arm64-v8a/).</summary>
        public const string AndroidStockfishLibrary = "libstockfish.so";

#if UNITY_ANDROID && !UNITY_EDITOR
        static bool androidPathResolved;
        static string androidPath;

        /// <summary>Résout le chemin sur le fil principal (appels Java), avant tout coup joué sur un fil de fond.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void ResolveAndroidPath() => StockfishPath();
#endif

        static IUciTransport OpenTransport(string path)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidUciTransport(path);
#else
            return new ProcessUciTransport(path);
#endif
        }

        static IChessEngine Stockfish()
        {
            if (stockfish != null && !stockfish.IsBroken) return stockfish;
            if (stockfish != null || stockfishStartFailed) return null;
            var path = StockfishPath();
            if (path == null) return null;
            try
            {
                stockfish = new UciEngine(OpenTransport(path), hashMb: 16, threads: 1);
                Application.quitting += Shutdown;
                return stockfish;
            }
            catch (Exception e)
            {
                stockfishStartFailed = true;
                Debug.LogWarning("[KevChess] Stockfish introuvable ou impossible à lancer (" + path + ") : " + e.Message);
                return null;
            }
        }

        static FallbackEngine Fallback => fallback ?? (fallback = new FallbackEngine(Stockfish, Engine));

        static void Shutdown()
        {
            stockfish?.Dispose();
            stockfish = null;
        }

        public static Task<Move> FindMoveAsync(ChessGame game, AILevel level, CancellationToken ct) =>
            FindMoveAsync(game, level, double.PositiveInfinity, 0, ct);

        /// <summary>
        /// Coup de l'IA avec une pendule : le temps de réflexion du niveau est réduit pour ne pas tomber
        /// (environ 1/30 du temps restant plus l'incrément).
        /// </summary>
        public static Task<Move> FindMoveAsync(ChessGame game, AILevel level, double remainingSeconds, int incrementSeconds, CancellationToken ct)
        {
            var difficulty = ToDifficulty(level);
            var options = new EngineOptions { Level = difficulty };
            if (!double.IsInfinity(remainingSeconds))
            {
                int levelMs = ChessAI.GetSettings(difficulty).MaxTimeMs;
                double budget = (remainingSeconds / 30.0 + incrementSeconds * 0.8) * 1000.0;
                options.MoveTimeMs = (int)Math.Max(50, Math.Min(levelMs, budget));
            }

            if (Active != EngineKind.Stockfish)
            {
                lastFromStockfish = false;
                return double.IsInfinity(remainingSeconds)
                    ? Engine.FindMoveAsync(game, difficulty, ct)
                    : Engine.FindMoveAsync(game.Position, options, ct);
            }
            return FindWithStockfishAsync(game, options, ct);
        }

        static async Task<Move> FindWithStockfishAsync(ChessGame game, EngineOptions options, CancellationToken ct)
        {
            var f = Fallback;
            var move = await f.Run(e => ((UciEngine)e).FindMoveAsync(game, options, ct), game.Position, options, ct);
            lastFromStockfish = !f.LastUsedFallback;
            if (f.LastUsedFallback && f.LastError != null)
                Debug.LogWarning("[KevChess] Stockfish a échoué, coup joué par l'IA maison : " + f.LastError.Message);
            return move;
        }

        /// <summary>L'IA (camp aiSide) accepte-t-elle la nulle proposée ?</summary>
        public static Task<bool> AcceptsDrawAsync(ChessGame game, PieceColor aiSide, CancellationToken ct) =>
            Engine.AcceptsDrawAsync(game, aiSide, ct);

        public static Task<Move> SuggestAsync(ChessGame game, CancellationToken ct) =>
            Engine.SuggestMoveAsync(game, 900, ct);

        public static void NewGame()
        {
            engine?.NewGame();
            stockfish?.NewGame();
        }

        /// <summary>Résumé de la dernière réflexion de l'IA de jeu, pour le journal.</summary>
        public static string LastSearchSummary()
        {
            if (lastFromStockfish && stockfish != null) return stockfish.LastSummary();
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
