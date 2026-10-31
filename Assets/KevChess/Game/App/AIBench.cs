using System;
using System.Text;
using System.Threading.Tasks;
using KevChess.AI;
using KevChess.Core;
using UnityEngine;

namespace KevChess.Game.App
{
    /// <summary>
    /// Mesure des temps de réflexion de l'IA par niveau, sur des positions fixes (livre coupé).
    /// Lancée avec « -kevchess-aibench » (Mac) ou, sur Android, l'extra d'intent « kevchess aibench » :
    ///   adb shell am start -n com.kevinyankam.kevchess/com.unity3d.player.UnityPlayerGameActivity -e kevchess aibench
    /// Les résultats partent dans le journal (« [KevChess] bench »), l'app continue normalement.
    /// </summary>
    public static class AIBench
    {
        const string Arg = "-kevchess-aibench";

        static readonly string[] Fens =
        {
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            "r1bqkb1r/pppp1ppp/2n2n2/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R w KQkq - 4 4",
            "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
            "r2q1rk1/pp2bppp/2n1pn2/3p4/2PP4/2N1PN2/PP3PPP/R2QKB1R w KQ - 0 9",
            "2r3k1/5ppp/p3p3/1p1n4/3P4/P3BP2/1P4PP/2R3K1 b - - 0 28",
            "8/2k5/3p4/p2P1p2/P2P1P2/8/8/4K3 w - - 0 1",
        };

        public static bool Requested()
        {
            foreach (var a in Environment.GetCommandLineArgs())
                if (a == Arg) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                    return intent.Call<string>("getStringExtra", "kevchess") == "aibench";
            }
            catch (Exception) { return false; }
#else
            return false;
#endif
        }

        public static void Run()
        {
            Debug.Log("[KevChess] bench : début (" + SystemInfo.deviceModel + ", " + SystemInfo.processorType + ", " + SystemInfo.processorCount + " cœurs)");
            // Hors du thread de rendu, comme en partie : l'affichage continue pendant la mesure.
            Task.Run(() =>
            {
                try
                {
                    foreach (var settings in ChessAI.AllLevels)
                    {
                        var ai = new ChessAI(hashMb: 16, seed: 1);
                        long total = 0, worst = 0, depths = 0, nodes = 0, searchMs = 0;
                        var detail = new StringBuilder();
                        foreach (var fen in Fens)
                        {
                            var pos = Position.FromFen(fen);
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            ai.FindMove(pos, new EngineOptions { Level = settings.Level, AllowBook = false });
                            long ms = sw.ElapsedMilliseconds;
                            var r = ai.LastResult;
                            total += ms;
                            worst = Math.Max(worst, ms);
                            depths += r.Depth;
                            nodes += r.Nodes;
                            searchMs += r.ElapsedMs;
                            detail.Append(' ').Append(ms).Append("ms/p").Append(r.Depth);
                        }
                        int n = Fens.Length;
                        Debug.Log("[KevChess] bench " + settings.DisplayName + " (max " + settings.MaxTimeMs + " ms, prof. " + settings.MaxDepth + ") : moyenne " +
                                  total / n + " ms, pire " + worst + " ms, profondeur moyenne " + (depths / (float)n).ToString("0.0") + ", " +
                                  (searchMs > 0 ? nodes * 1000 / searchMs : 0) + " nœuds/s |" + detail);
                    }
                    Debug.Log("[KevChess] bench : fin");
                }
                catch (Exception e) { Debug.LogException(e); }
            });
        }
    }
}
