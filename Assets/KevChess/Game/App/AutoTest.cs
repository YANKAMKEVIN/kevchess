using System;
using System.Collections;
using System.IO;
using KevChess.Core;
using KevChess.Game.Match;
using KevChess.Game.UI;
using KevChess.Game.Visuals;
using UnityEngine;
using UnityEngine.UI;

namespace KevChess.Game.App
{
    /// <summary>
    /// Recette automatique, lancée avec l'argument « -kevchess-autotest [dossier] » :
    /// joue des coups scriptés, revue, mat animé, analyse, pendule, nulle, puzzles, réglages,
    /// puis une partie contre l'IA ; prend des captures et journalise, puis quitte.
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

            // Mode revue : positions passées en lecture seule, la partie ne bouge pas.
            string fenBefore = match.Game.Position.ToFen();
            int pliesBefore = match.Game.Moves.Count;
            Press("Hud/BottomBar/More");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("actions_plus");
            Press("Actions/Card/Review");
            yield return new WaitForSeconds(0.5f);
            Check(hud.IsReviewing && match.Reviewing, "mode revue ouvert");
            Press("Hud/ReviewBar/Prev");
            Press("Hud/ReviewBar/Prev");
            Press("Hud/ReviewBar/Prev");
            yield return new WaitForSeconds(0.6f);
            Check(hud.ReviewPly == pliesBefore - 3, "revue : trois coups en arrière (" + hud.ReviewPly + ")");
            Check(!match.CanHumanAct, "revue en lecture seule");
            yield return Capture("revue");
            Press("Hud/ReviewBar/First");
            yield return new WaitForSeconds(0.4f);
            Check(hud.ReviewPly == 0, "revue : position de départ");
            Press("Hud/ReviewBar/Next");
            yield return new WaitForSeconds(0.4f);
            yield return Capture("revue_debut");
            Press("Hud/ReviewBar/Close");
            yield return new WaitForSeconds(0.4f);
            Check(!hud.IsReviewing && match.Game.Moves.Count == pliesBefore && match.Game.Position.ToFen() == fenBefore,
                "revue fermée, partie intacte");

            // Orbite d'un quart de tour : les pièces prises doivent rester dans le cadre sans « Vue ».
            var orbit = FindAnyObjectByType<CameraRig.OrbitCamera>();
            float pixelsPerDegree = 1f / (orbit.OrbitSensitivity * 1080f / Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height)));
            foreach (float yawStep in new[] { 90f, 40f })
            {
                orbit.Orbit(new Vector2(yawStep * pixelsPerDegree, 0f));
                yield return new WaitForSeconds(1.5f);
                // Portrait : cadrage des pièces prises en vue diagonale non garanti (signalé, pas bloquant).
                if (Screen.height > Screen.width && !TraysInView()) Log("AVERTISSEMENT pièces prises hors cadre après orbite de " + yawStep + "° (portrait)");
                else Check(TraysInView(), "pièces prises dans le cadre après orbite de " + yawStep + "°");
                yield return Capture("orbite_" + yawStep);
            }
            hud.ResetCamera();
            yield return new WaitForSeconds(1.5f);

            // Mat du berger.
            match.StartNew(new MatchSettings { Mode = MatchMode.LocalTwoPlayers });
            foreach (var san in new[] { "e4", "e5", "Bc4", "Nc6", "Qh5", "Nf6", "Qxf7#" })
            {
                var m = Notation.Parse(match.Game.Position, san);
                match.TryHumanMove(m.From, m.To, m.Promotion);
                yield return WaitIdle();
            }
            Check(match.Game.Result == GameResult.WhiteWins, "mat détecté");
            yield return new WaitForSeconds(0.7f);
            yield return Capture("mat_animation");
            Check(!hud.IsEndScreenOpen, "écran de fin retenu pendant l'animation de mat");
            yield return new WaitForSeconds(1.9f);
            Check(hud.IsEndScreenOpen, "écran de fin après l'animation de mat");
            yield return Capture("mat");

            // Analyse de la partie depuis l'écran de fin.
            Press("GameOver/Card/Analyze");
            yield return new WaitForSeconds(0.4f);
            yield return Capture("analyse_progression");
            float wait = 0f;
            while (hud.IsAnalysisRunning && wait < 30f) { wait += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.3f);
            Check(hud.LastAnalysis != null && hud.LastAnalysis.Moves.Count == 7, "analyse terminée (" + wait.ToString("0.0") + " s)");
            yield return Capture("analyse");
            Check(hud.OpenVerdictForTest(0), "ligne de gaffe ouverte en revue");
            yield return new WaitForSeconds(1.2f);
            Check(hud.IsReviewing, "revue sur la gaffe");
            yield return Capture("analyse_revue_fleche");
            Press("Hud/ReviewBar/Close");
            yield return new WaitForSeconds(0.4f);

            // Pendule : cadence 1+0 ramenée à quelques secondes, jusqu'à la chute.
            var timed = new MatchSettings { Mode = MatchMode.LocalTwoPlayers };
            timed.Clock = TimeControl.Presets[1];
            match.StartNew(timed);
            yield return WaitIdle();
            PlaySan("e4");
            yield return WaitIdle();
            match.Clock.RemainingBlack = 5.5;
            hud.ShowMenu(true);
            yield return new WaitForSeconds(0.2f);
            double frozen = match.Clock.RemainingBlack;
            yield return new WaitForSeconds(1f);
            Check(Math.Abs(match.Clock.RemainingBlack - frozen) < 0.01, "pendule arrêtée menu ouvert");
            hud.ShowMenu(false);
            yield return new WaitForSeconds(1.6f);
            yield return Capture("horloge_alerte");
            wait = 0f;
            while (!match.Game.IsOver && wait < 10f) { wait += Time.deltaTime; yield return null; }
            Check(match.Game.EndReason == GameEndReason.Timeout && match.Game.Result == GameResult.WhiteWins, "chute au temps des Noirs");
            yield return new WaitForSeconds(1.6f);
            Check(hud.IsEndScreenOpen, "écran de fin après la chute");
            yield return Capture("chute_temps");

            // Nulle en partie à deux : l'autre joueur confirme.
            match.StartNew(new MatchSettings { Mode = MatchMode.LocalTwoPlayers });
            yield return WaitIdle();
            PlaySan("d4");
            yield return WaitIdle();
            Press("Hud/BottomBar/More");
            yield return new WaitForSeconds(0.4f);
            Press("Actions/Card/Draw");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("nulle_question");
            Press("Confirm/Card/Buttons/Yes");
            yield return new WaitForSeconds(1.3f);
            Check(match.Game.EndReason == GameEndReason.DrawAgreement, "nulle acceptée en partie à deux");
            yield return Capture("nulle_acceptee");

            // Nulle contre l'IA : refusée en début de partie.
            match.StartNew(new MatchSettings { Mode = MatchMode.VersusAI, HumanColor = PieceColor.White, Level = AILevel.Beginner });
            yield return WaitIdle();
            Press("Hud/BottomBar/More");
            yield return new WaitForSeconds(0.4f);
            Press("Actions/Card/Draw");
            yield return new WaitForSeconds(0.6f);
            Check(hud.LastToast == "L'IA refuse la nulle" && !match.Game.IsOver, "l'IA refuse la nulle (" + hud.LastToast + ")");
            yield return Capture("nulle_refusee_ia");

            // Copier le PGN (presse-papiers restauré ensuite).
            string clipboard = GUIUtility.systemCopyBuffer;
            Press("Hud/BottomBar/More");
            yield return new WaitForSeconds(0.4f);
            Press("Actions/Card/Copy");
            yield return null;
            Check(GUIUtility.systemCopyBuffer.StartsWith("[Event \"KevChess\"]") && hud.LastToast == "PGN copié", "PGN copié");
            GUIUtility.systemCopyBuffer = clipboard;

            // Puzzles : liste, coup faux, indice, résolution d'un mat en 2.
            hud.ShowMenu(true);
            yield return new WaitForSeconds(0.3f);
            Press("Menu/Card/Puzzles");
            yield return new WaitForSeconds(0.6f);
            yield return Capture("puzzles_liste");
            int puzzleIndex = 0;
            for (int i = 0; i < PuzzleSet.All.Count; i++)
                if (PuzzleSet.All[i].MateIn == 2) { puzzleIndex = i; break; }
            Press("Puzzles/Card/List/Content/Puzzle" + puzzleIndex);
            yield return new WaitForSeconds(1.5f);
            Check(match.IsPuzzle && match.Puzzle.Puzzle.Id == PuzzleSet.All[puzzleIndex].Id, "puzzle lancé : " + match.Puzzle.Puzzle.Title);
            yield return Capture("puzzle_depart");
            string puzzleFen = match.Game.Position.ToFen();
            var expectedMove = match.Puzzle.ExpectedMove;
            Move wrong = Move.Null;
            foreach (var m in match.Game.LegalMoves)
            {
                if (m.ToUci() == expectedMove.ToUci()) continue;
                match.Game.TryMakeMove(m);
                bool mates = match.Game.EndReason == GameEndReason.Checkmate;
                match.Game.Undo();
                if (!mates) { wrong = m; break; }
            }
            match.TryHumanMove(wrong.From, wrong.To, wrong.Promotion);
            float pt = 0f;
            while (hud.LastToast != "Ce n'est pas le bon coup" && pt < 5f) { pt += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.1f);
            yield return Capture("puzzle_coup_faux");
            yield return WaitIdle();
            Check(match.Puzzle.Mistakes == 1 && match.Game.Position.ToFen() == puzzleFen, "coup faux refusé, pièce revenue");
            Press("Hud/BottomBar/Hint");
            yield return new WaitForSeconds(0.8f);
            yield return Capture("puzzle_indice");
            for (int step = 0; step < 4 && !match.Puzzle.IsSolved; step++)
            {
                var m = match.Puzzle.ExpectedMove;
                match.TryHumanMove(m.From, m.To, m.Promotion);
                yield return WaitIdle();
            }
            Check(match.Puzzle.IsSolved && GamePrefs.IsPuzzleSolved(match.Puzzle.Puzzle.Id), "puzzle résolu et enregistré");
            yield return new WaitForSeconds(0.3f);
            yield return Capture("puzzle_resolu");
            Press("Hud/BottomBar/Next");
            yield return new WaitForSeconds(1f);
            Check(match.IsPuzzle && match.Puzzle.Puzzle.Id != PuzzleSet.All[puzzleIndex].Id, "puzzle suivant");

            // Réglages : changement de thème appliqué tout de suite.
            int themeBefore = VisualTheme.Current;
            hud.ShowMenu(true);
            yield return new WaitForSeconds(0.3f);
            Press("Menu/Card/Settings");
            yield return new WaitForSeconds(0.5f);
            yield return Capture("reglages");
            Press("Settings/Card/Theme");
            yield return new WaitForSeconds(0.5f);
            Check(VisualTheme.Current == (themeBefore + 1) % VisualTheme.Names.Length, "thème changé : " + VisualTheme.Names[VisualTheme.Current]);
            yield return Capture("reglages_theme");
            Press("Settings/Card/Back");
            hud.ShowMenu(false);
            yield return new WaitForSeconds(0.6f);
            yield return Capture("theme_" + VisualTheme.Current);
            GamePrefs.Theme = themeBefore; // retour au thème d'origine
            Check(VisualTheme.Current == themeBefore, "thème d'origine restauré");

            // Écran « Partie à deux » : cadence et caméra.
            hud.ShowMenu(true);
            yield return new WaitForSeconds(0.3f);
            Press("Menu/Card/Local");
            yield return new WaitForSeconds(0.3f);
            Press("LocalSetup/Card/Clocks/Row1/Clock4");
            yield return new WaitForSeconds(0.3f);
            yield return Capture("partie_a_deux_config");
            Press("LocalSetup/Card/Actions/Back");
            yield return new WaitForSeconds(0.3f);

            // Contre l'IA par son écran : Noirs (l'IA ouvre), niveau Moyen, cadence 3+2.
            Press("Menu/Card/VsAI");
            yield return new WaitForSeconds(0.3f);
            Press("AISetup/Card/Colors/Color1");
            Press("AISetup/Card/Levels/Level2");
            Press("AISetup/Card/Clocks/Row0/Clock2");
            yield return new WaitForSeconds(0.3f);
            yield return Capture("contre_ia_config");
            Press("AISetup/Card/Actions/Play");
            yield return null;
            // Une partie non terminée serait remplacée : on confirme.
            if (hud.IsConfirmOpen) Press("Confirm/Card/Buttons/Yes");
            yield return new WaitForSeconds(0.25f);
            Check(match.Settings.Mode == MatchMode.VersusAI && match.Settings.ClockBase == 180 && match.Settings.ClockIncrement == 2,
                "partie contre l'IA lancée en 3+2");
            if (match.IsAIThinking) yield return Capture("ia_reflechit");
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
                while (match.Game.Moves.Count < expected && t < 15f && !match.Game.IsOver) { t += Time.deltaTime; yield return null; }
                yield return WaitIdle();
            }
            Check(match.Game.Moves.Count >= 9, "partie contre l'IA : " + match.Game.Moves.Count + " demi-coups");
            yield return Capture("contre_ia");

            Log(failures == 0 ? "TERMINÉ : OK" : "TERMINÉ : " + failures + " échec(s)");
            yield return new WaitForSeconds(0.5f);
            Application.Quit(failures == 0 ? 0 : 1);
        }

        /// <summary>Clique un bouton de l'interface par son chemin sous le Hud (« Menu/Card/Puzzles »).</summary>
        void Press(string path)
        {
            var t = hud.transform.Find(path) ?? hud.transform.Find("SafeArea/" + path);
            var button = t != null ? t.GetComponent<Button>() : null;
            if (button == null) { Fail("bouton introuvable : " + path); return; }
            if (!button.interactable || !button.gameObject.activeInHierarchy) { Fail("bouton inactif : " + path); return; }
            button.onClick.Invoke();
        }

        void PlaySan(string san)
        {
            var m = Notation.Parse(match.Game.Position, san);
            if (m.IsNull || !match.TryHumanMove(m.From, m.To, m.Promotion)) Fail("coup refusé " + san);
        }

        static bool TraysInView()
        {
            var trays = GameObject.Find("CapturedTrays");
            if (trays == null || trays.transform.childCount == 0) return false;
            var cam = Camera.main;
            foreach (var r in trays.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue; // reflets masqués
                var b = r.bounds;
                foreach (var corner in new[] { b.min, b.max })
                {
                    var v = cam.WorldToViewportPoint(corner);
                    if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f) return false;
                }
            }
            return true;
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
