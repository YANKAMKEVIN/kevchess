using System;
using System.Collections;
using System.IO;
using KevChess.Core;
using KevChess.Game.Match;
using KevChess.Game.UI;
using UnityEngine;

namespace KevChess.Game.App
{
    /// <summary>
    /// Recette automatique, lancée avec l'argument « -kevchess-autotest [dossier] » :
    /// joue des coups scriptés puis une partie contre l'IA, prend des captures et journalise, puis quitte.
    /// </summary>
    public sealed class AutoTest : MonoBehaviour
    {
        const string Arg = "-kevchess-autotest";

        MatchController match;
        Hud hud;
        string folder;
        int shot;
        int failures;

        public static bool Requested(out string folder)
        {
            folder = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != Arg) continue;
                folder = i + 1 < args.Length && !args[i + 1].StartsWith("-")
                    ? args[i + 1]
                    : Path.Combine(Application.persistentDataPath, "autotest");
                return true;
            }
            return false;
        }

        public void Run(MatchController matchController, Hud headsUp, string outputFolder)
        {
            match = matchController;
            hud = headsUp;
            folder = outputFolder;
            Directory.CreateDirectory(folder);
            Application.runInBackground = true; // la recette tourne même sans le focus
            StartCoroutine(Script());
        }

        IEnumerator Script()
        {
            Log("début, dossier " + folder);
            yield return new WaitForSeconds(2f);
            yield return Capture("menu");
            hud.ShowMenu(false);

            // Partie à deux : ouverture, prises, roque, promotion avec prise.
            match.StartNew(new MatchSettings { Mode = MatchMode.LocalTwoPlayers });
            yield return new WaitForSeconds(1.5f);
            yield return Capture("depart");

            string[] moves =
            {
                "e4", "d5", "exd5", "Nf6", "Bb5+", "c6", "dxc6", "Qb6", "cxb7+", "Kd8", "Nf3", "e5", "O-O", "Bd6",
                "bxa8=Q", "Nc6",
            };
            foreach (var san in moves)
            {
                var m = Notation.Parse(match.Game.Position, san);
                if (m.IsNull || !match.TryHumanMove(m.From, m.To, m.Promotion))
                {
                    Fail("coup refusé " + san + " dans " + match.Game.Position.ToFen());
                    break;
                }
                yield return WaitIdle();
                if (san == "exd5" || san == "O-O" || san == "bxa8=Q") yield return Capture("apres_" + san.Replace("=", ""));
            }
            Check(match.Game.Moves.Count == moves.Length, "16 coups joués");
            Check(match.Game.Position[Square.Parse("a8")] == Piece.Make(PieceColor.White, PieceType.Queen), "promotion visible en a8");

            // Annulation.
            match.UndoForHuman();
            yield return new WaitForSeconds(0.5f);
            Check(match.Game.Moves.Count == moves.Length - 1, "annulation d'un coup");
            yield return Capture("apres_annulation");

            // Mat du berger.
            match.StartNew(new MatchSettings { Mode = MatchMode.LocalTwoPlayers });
            foreach (var san in new[] { "e4", "e5", "Bc4", "Nc6", "Qh5", "Nf6", "Qxf7#" })
            {
                var m = Notation.Parse(match.Game.Position, san);
                match.TryHumanMove(m.From, m.To, m.Promotion);
                yield return WaitIdle();
            }
            Check(match.Game.Result == GameResult.WhiteWins, "mat détecté");
            yield return new WaitForSeconds(1.5f);
            yield return Capture("mat");

            // Contre l'IA (noirs pour l'humain : l'IA ouvre).
            match.StartNew(new MatchSettings { Mode = MatchMode.VersusAI, HumanColor = PieceColor.Black, Level = AILevel.Medium });
            float t = 0f;
            while (match.Game.Moves.Count < 1 && t < 15f) { t += Time.deltaTime; yield return null; }
            Check(match.Game.Moves.Count == 1, "l'IA a joué son premier coup (" + t.ToString("0.0") + " s)");
            yield return WaitIdle();
            for (int i = 0; i < 4 && !match.Game.IsOver; i++)
            {
                var reply = match.Game.LegalMoves[0];
                match.TryHumanMove(reply.From, reply.To, reply.Promotion);
                t = 0f;
                int expected = match.Game.Moves.Count + 1;
                while (match.Game.Moves.Count < expected + 1 && t < 15f && !match.Game.IsOver) { t += Time.deltaTime; yield return null; }
                yield return WaitIdle();
            }
            Check(match.Game.Moves.Count >= 9, "partie contre l'IA : " + match.Game.Moves.Count + " demi-coups");
            yield return Capture("contre_ia");

            Log(failures == 0 ? "TERMINÉ : OK" : "TERMINÉ : " + failures + " échec(s)");
            yield return new WaitForSeconds(0.5f);
            Application.Quit(failures == 0 ? 0 : 1);
        }

        IEnumerator WaitIdle()
        {
            float t = 0f;
            yield return null;
            while ((match.IsAnimating || match.IsAIThinking) && t < 10f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.15f);
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(folder, $"{++shot:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Log("capture " + path);
            yield return new WaitForSeconds(0.3f);
        }

        void Check(bool ok, string what)
        {
            if (ok) Log("OK " + what);
            else Fail(what);
        }

        void Fail(string what)
        {
            failures++;
            Debug.LogError("[AutoTest] ÉCHEC " + what);
        }

        static void Log(string msg) => Debug.Log("[AutoTest] " + msg);
    }
}
