using System;
using System.Collections;
using System.IO;
using KevChess.Core;
using KevChess.Game.Match;
using KevChess.Game.UI;
using KevChess.Game.Visuals;
using UnityEngine;

namespace KevChess.Game.View
{
    /// <summary>
    /// Vitrine visuelle lancée avec « -kevchess-visuals [dossier] » : joue quelques coups puis capture
    /// les 4 thèmes, le mode lisibilité, la traînée de glissade, le saut du cavalier, la flèche d'indice
    /// et les étapes de l'animation de mat, puis quitte. Sert à juger le rendu sans toucher à l'écran.
    /// </summary>
    public sealed class VisualsShowcase : MonoBehaviour
    {
        const string Arg = "-kevchess-visuals";

        string folder;
        int shot;
        int failures;
        MatchController match;
        Hud hud;
        BoardView board;
        bool hideEndOverlay;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != Arg) continue;
                string dir = i + 1 < args.Length && !args[i + 1].StartsWith("-")
                    ? args[i + 1]
                    : Path.Combine(Application.persistentDataPath, "visuals");
                var go = new GameObject("VisualsShowcase");
                go.AddComponent<VisualsShowcase>().folder = dir;
                return;
            }
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            Application.runInBackground = true;
            yield return new WaitForSeconds(1.5f);
            match = FindAnyObjectByType<MatchController>();
            hud = FindAnyObjectByType<Hud>();
            board = FindAnyObjectByType<BoardView>();
            if (match == null || board == null)
            {
                Log("ÉCHEC : application introuvable");
                Application.Quit(1);
                yield break;
            }
            if (hud != null) hud.ShowMenu(false);

            match.StartNew(new MatchSettings { Mode = MatchMode.LocalTwoPlayers });
            yield return new WaitForSeconds(1f);

            // Glissade avec traînée (capture en plein mouvement), au ralenti pour la voir.
            BoardView.AnimationSpeed = 0.5f;
            yield return Play("e4", 0f);
            yield return Play("e5", 0f);
            yield return Play("Nf3", 0.55f, "saut_cavalier");
            yield return Play("Nc6", 0f);
            yield return Play("Bc4", 0.62f, "glissade_fou");
            BoardView.AnimationSpeed = 1f;
            yield return Play("Nf6", 0f);
            yield return new WaitForSeconds(0.6f);

            // Thèmes.
            for (int i = 0; i < VisualTheme.Names.Length; i++)
            {
                VisualTheme.Apply(i);
                Check(VisualTheme.Current == i, "thème " + VisualTheme.Names[i] + " actif");
                yield return new WaitForSeconds(0.5f);
                yield return Capture("theme_" + i);
            }
            VisualTheme.HighContrast = true;
            VisualTheme.Apply(0);
            yield return new WaitForSeconds(0.5f);
            yield return Capture("theme_0_lisibilite");
            VisualTheme.HighContrast = false;

            // Flèche d'indice (thème Aurore pour vérifier qu'elle suit la palette).
            board.ShowHintArrow(Square.Parse("f3"), Square.Parse("g5"));
            yield return new WaitForSeconds(0.8f);
            yield return Capture("fleche_cavalier");
            board.ShowHintArrow(Square.Parse("d1"), Square.Parse("h5"));
            VisualTheme.Apply(1);
            yield return new WaitForSeconds(0.8f);
            yield return Capture("fleche_dame_aurore");
            board.ClearHintArrow();
            VisualTheme.Apply(0);

            // Mat du berger, puis animation de mat (avec le thème Glace & Braise pour la dernière série).
            for (int pass = 0; pass < 2; pass++)
            {
                VisualTheme.Apply(pass == 0 ? 0 : 3);
                match.StartNew(new MatchSettings { Mode = MatchMode.LocalTwoPlayers });
                yield return new WaitForSeconds(0.5f);
                foreach (var san in new[] { "e4", "e5", "Bc4", "Nc6", "Qh5", "Nf6", "Qxf7#" })
                    yield return Play(san, 0f);
                Check(match.Game.Result == GameResult.WhiteWins, "mat détecté");
                hideEndOverlay = true;
                int king = match.Game.Position.KingSquare(PieceColor.Black);
                var routine = StartCoroutine(board.PlayMateAnimation(king));
                float start = Time.time;
                foreach (float at in pass == 0 ? new[] { 0.55f, 0.95f, 1.3f, 1.6f, 2.0f } : new[] { 1.35f, 1.8f })
                {
                    while (Time.time - start < at) yield return null;
                    yield return Capture("mat" + pass + "_" + Mathf.RoundToInt(at * 100));
                }
                yield return routine;
                Check(GameObject.Find("Black King") == null, "roi maté brisé");
                hideEndOverlay = false;
            }
            VisualTheme.Apply(0);

            Log(failures == 0 ? "TERMINÉ : OK" : "TERMINÉ : " + failures + " échec(s)");
            yield return new WaitForSeconds(0.5f);
            Application.Quit(failures == 0 ? 0 : 1);
        }

        void LateUpdate()
        {
            if (!hideEndOverlay) return;
            var overlay = GameObject.Find("GameOver");
            if (overlay != null) overlay.SetActive(false);
        }

        IEnumerator Play(string san, float captureAfter, string captureName = null)
        {
            var m = Notation.Parse(match.Game.Position, san);
            if (m.IsNull || !match.TryHumanMove(m.From, m.To, m.Promotion))
            {
                failures++;
                Log("ÉCHEC coup refusé " + san);
                yield break;
            }
            if (captureName != null)
            {
                yield return new WaitForSeconds(captureAfter);
                yield return Capture(captureName);
                foreach (var trail in FindObjectsByType<TrailRenderer>())
                    if (trail.emitting || trail.positionCount > 0)
                        Log("traînée " + trail.transform.parent.name + " : " + trail.positionCount + " points, émission " + trail.emitting);
            }
            float t = 0f;
            yield return null;
            while (match.IsAnimating && t < 10f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.1f);
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(folder, $"{++shot:00}_{name}.png");
            ScreenCapture.CaptureScreenshot(path);
            Log("capture " + path);
        }

        void Check(bool ok, string what)
        {
            if (ok) Log("OK " + what);
            else { failures++; Log("ÉCHEC " + what); }
        }

        static void Log(string msg) => Debug.Log("[Visuals] " + msg);
    }
}
