using System;
using System.Collections.Generic;
using System.Threading;
using KevChess.AI;
using KevChess.Core;
using KevChess.Game.Audio;
using KevChess.Game.Match;
using UnityEngine;
using UnityEngine.UI;

namespace KevChess.Game.UI
{
    /// <summary>Mode revue (positions passées, lecture seule) et analyse de fin de partie.</summary>
    public sealed partial class Hud
    {
        const int AnalysisMsPerPosition = 150;
        static readonly Color Orange = new Color(1f, 0.62f, 0.25f);

        // Revue.
        bool reviewing;
        int reviewPly;
        string reviewNote;
        int reviewNotePly = -1;
        ChessGame reviewGame; // position montrée en revue
        GameObject reviewBar;
        Button reviewFirst, reviewPrev, reviewNext, reviewLast, reviewResume;
        Text reviewCounter;

        // Analyse.
        GameObject analysisOverlay, analysisProgressGroup, analysisResultsGroup;
        RectTransform analysisFill;
        Text analysisPercent, accuracyWhite, accuracyBlack, analysisEmpty;
        Text[,] analysisCounts;
        RectTransform analysisList;
        ScrollRect analysisScroll;
        GameAnalyzer analyzer;
        CancellationTokenSource analysisCts;
        GameAnalysis analysis;
        ChessGame analyzedGame;
        int analyzedPlies;

        public bool IsReviewing => reviewing;
        public int ReviewPly => reviewPly;
        public bool IsAnalysisRunning => analysisCts != null;
        public GameAnalysis LastAnalysis => analysis;

        // ---------------------------------------------------------------- Revue

        void BuildReviewBar()
        {
            var bar = UIKit.Panel("ReviewBar", hudGroup.transform, UIKit.Glass, true, UIKit.Magenta);
            reviewBar = bar.gameObject;
            UIKit.Anchor(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(1040, 140));
            var h = UIKit.HStack(bar.rectTransform, 12, 16);
            h.childForceExpandWidth = false;
            UIKit.Size(UIKit.Button("Close", bar.transform, "Fermer", () => { sfx.Play(Sfx.Click); ExitReview(); }, UIKit.Magenta, 34), -1, 160);
            reviewFirst = ReviewButton(bar.transform, "First", "|◀", () => ShowReviewPly(0));
            reviewPrev = ReviewButton(bar.transform, "Prev", "◀", () => ShowReviewPly(reviewPly - 1));
            reviewCounter = UIKit.Label("Counter", bar.transform, "", 34, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(reviewCounter, -1, 100, 1);
            reviewNext = ReviewButton(bar.transform, "Next", "▶", () => ShowReviewPly(reviewPly + 1));
            reviewLast = ReviewButton(bar.transform, "Last", "▶|", () => ShowReviewPly(match.Game.Moves.Count));
            reviewBar.SetActive(false);

            // Dans la barre, pour ne pas empiéter sur le plateau.
            reviewResume = UIKit.Button("ResumeHere", bar.transform, "Reprendre d'ici", () =>
            {
                sfx.Play(Sfx.Click);
                int ply = reviewPly;
                Confirm("Reprendre la partie d'ici ?\nLes coups suivants seront effacés.", () =>
                {
                    if (!match.ResumeFrom(ply)) Toast("Impossible de reprendre ici", UIKit.Danger);
                });
            }, UIKit.Cyan, 30);
            UIKit.Size(reviewResume, -1, 200);
            reviewResume.gameObject.SetActive(false);
        }

        Button ReviewButton(Transform parent, string name, string text, Action onClick)
        {
            var b = UIKit.Button(name, parent, text, () => { sfx.Play(Sfx.Click); onClick(); }, null, 40);
            UIKit.Size(b, -1, 112);
            return b;
        }

        /// <summary>Ouvre le mode revue sur la position après <paramref name="ply"/> demi-coups.</summary>
        public void EnterReview(int ply)
        {
            if (match.Game == null || match.IsAnimating || match.IsPuzzle) return;
            Show(endOverlay, false);
            Show(actionsOverlay, false);
            Show(analysisOverlay, false);
            reviewing = true;
            match.Reviewing = true;
            reviewNote = null;
            reviewNotePly = -1;
            reviewBar.SetActive(true);
            ShowReviewPly(ply);
        }

        public void ShowReviewPly(int ply)
        {
            var game = match.Game;
            if (!reviewing || game == null) return;
            reviewPly = Mathf.Clamp(ply, 0, game.Moves.Count);
            // Partie temporaire jusqu'à ce coup : position, dernier coup et échec marqués, sans toucher à la vraie partie.
            var view = new ChessGame(game.StartFen);
            for (int i = 0; i < reviewPly; i++) view.TryMakeMove(game.Moves[i]);
            reviewGame = view;
            board.ShowPosition(view.Position, match.ViewSide);
            board.ShowState(view);
            Refresh();
        }

        void RefreshReview()
        {
            var game = match.Game;
            int total = game.Moves.Count;
            status.color = UIKit.Magenta;
            status.text = reviewPly == 0 ? "Revue · position de départ" : "Revue · " + MoveLabel(game.StartFen, reviewPly - 1, game.SanMoves[reviewPly - 1]);
            movesLine.text = RecentMoves(game, reviewPly, 8);
            openingLine.text = reviewNotePly == reviewPly && reviewNote != null ? reviewNote : "";
            openingLine.color = reviewNotePly == reviewPly ? Orange : UIKit.TextDim;
            reviewCounter.text = reviewPly + " / " + total;
            reviewFirst.interactable = reviewPrev.interactable = reviewPly > 0;
            reviewNext.interactable = reviewLast.interactable = reviewPly < total;
            // Reprendre d'ici : partie finie, ou partie à deux (contre l'IA en cours, ce serait une annulation déguisée).
            bool canResume = (game.IsOver || match.Settings.Mode == MatchMode.LocalTwoPlayers) && reviewGame != null && !reviewGame.IsOver &&
                             !(reviewPly == total && !game.IsOver);
            reviewResume.gameObject.SetActive(canResume);
        }

        /// <summary>Quitte la revue et remet la position réelle.</summary>
        public void ExitReview()
        {
            if (!reviewing) return;
            LeaveReviewState();
            var game = match.Game;
            board.ShowPosition(game.Position, match.ViewSide);
            board.ShowState(game);
            openingLine.color = UIKit.TextDim * new Color(1, 1, 1, 0.85f);
            Refresh();
        }

        void LeaveReviewState()
        {
            reviewing = false;
            match.Reviewing = false;
            reviewBar.SetActive(false);
            reviewResume.gameObject.SetActive(false);
            openingLine.color = UIKit.TextDim * new Color(1, 1, 1, 0.85f);
        }

        // ---------------------------------------------------------------- Analyse

        void BuildAnalysis()
        {
            analysisOverlay = Overlay("Analysis", out var card, 940, 1480, UIKit.Magenta);
            UIKit.VStack(card, 14, 44);
            UIKit.Size(UIKit.Label("Title", card, "Analyse", 68, UIKit.Magenta, TextAnchor.MiddleCenter, FontStyle.Bold), 90);

            // Progression.
            var progress = UIKit.Rect("Progress", card);
            analysisProgressGroup = progress.gameObject;
            UIKit.VStack(progress, 24);
            UIKit.Size(progress.gameObject.AddComponent<LayoutElement>(), 330);
            analysisPercent = UIKit.Label("Percent", progress, "", 38, UIKit.Text);
            UIKit.Size(analysisPercent, 60);
            var bar = UIKit.ProgressBar("Bar", progress, UIKit.Magenta);
            UIKit.Size(bar.parent.GetComponent<Image>(), 56);
            analysisFill = bar;
            UIKit.Size(UIKit.Label("Hint", progress, "Le moteur évalue chaque position de la partie.", 30, UIKit.TextDim), 50);
            UIKit.Size(UIKit.Button("Cancel", progress, "Annuler", () => { sfx.Play(Sfx.Click); CloseAnalysis(); }, UIKit.TextDim), 100);

            // Résultats.
            var results = UIKit.Rect("Results", card);
            analysisResultsGroup = results.gameObject;
            UIKit.VStack(results, 12);
            var rl = results.gameObject.AddComponent<LayoutElement>();
            rl.flexibleHeight = 1;
            rl.preferredHeight = 1100;

            var acc = UIKit.Rect("Accuracy", results);
            UIKit.HStack(acc, 16);
            UIKit.Size(acc.gameObject.AddComponent<LayoutElement>(), 120);
            accuracyWhite = AccuracyCard(acc, "Blancs", UIKit.Cyan);
            accuracyBlack = AccuracyCard(acc, "Noirs", UIKit.Magenta);

            string[] rows = { "", "Excellents", "Imprécisions", "Erreurs", "Gaffes" };
            analysisCounts = new Text[rows.Length, 2];
            for (int r = 0; r < rows.Length; r++)
            {
                var line = UIKit.Rect("Row" + r, results);
                UIKit.HStack(line, 8);
                UIKit.Size(line.gameObject.AddComponent<LayoutElement>(), 46);
                var head = UIKit.Label("Name", line, rows[r], 32, UIKit.TextDim, TextAnchor.MiddleLeft);
                UIKit.Size(head, -1, 380);
                analysisCounts[r, 0] = UIKit.Label("White", line, r == 0 ? "Blancs" : "", 32, r == 0 ? UIKit.Cyan : UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                analysisCounts[r, 1] = UIKit.Label("Black", line, r == 0 ? "Noirs" : "", 32, r == 0 ? UIKit.Magenta : UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            UIKit.Size(UIKit.Label("ListTitle", results, "Erreurs et gaffes (toucher pour revoir)", 30, UIKit.TextDim), 46);
            analysisList = UIKit.ScrollList("List", results, 10, out analysisScroll);
            var ll = UIKit.Size(analysisScroll, 380);
            ll.flexibleHeight = 1;
            analysisEmpty = UIKit.Label("Empty", analysisList, "Aucune erreur ni gaffe. Bravo !", 32, UIKit.Cyan);
            UIKit.Size(analysisEmpty, 80);

            UIKit.Size(UIKit.Button("Close", results, "Fermer", () => { sfx.Play(Sfx.Click); CloseAnalysis(); }, UIKit.TextDim), 100);
        }

        Text AccuracyCard(RectTransform parent, string side, Color accent)
        {
            var card = UIKit.Panel(side, parent, UIKit.Glass, true, accent);
            var sideLabel = UIKit.Label("Side", card.transform, side, 28, UIKit.TextDim);
            UIKit.Stretch(sideLabel.rectTransform, 0, 72, 0, 8);
            var value = UIKit.Label("Value", card.transform, "", 46, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(value.rectTransform, 0, 8, 0, 40);
            return value;
        }

        /// <summary>Ouvre l'écran d'analyse et lance l'analyse (ou montre celle déjà faite pour cette partie).</summary>
        public async void OpenAnalysis()
        {
            var game = match.Game;
            if (game == null || game.Moves.Count == 0 || match.IsPuzzle) return;
            Show(endOverlay, false);
            Show(analysisOverlay, true);
            if (analysis != null && analyzedGame == game && analyzedPlies == game.Moves.Count)
            {
                ShowAnalysisResults();
                return;
            }
            if (analysisCts != null) return; // déjà en cours

            analysisProgressGroup.SetActive(true);
            analysisResultsGroup.SetActive(false);
            SetAnalysisProgress(0f);
            var cts = new CancellationTokenSource();
            analysisCts = cts;
            if (analyzer == null) analyzer = new GameAnalyzer(16);
            var progress = new Progress<float>(p => { if (analysisCts == cts) SetAnalysisProgress(p); });
            try
            {
                var result = await analyzer.AnalyzeAsync(game.StartFen, game.Moves, AnalysisMsPerPosition, progress, cts.Token);
                if (this == null || cts.IsCancellationRequested || match.Game != game) return;
                analysis = result;
                analyzedGame = game;
                analyzedPlies = game.Moves.Count;
                if (analysisOverlay.activeSelf) ShowAnalysisResults();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (this != null) Toast("Analyse impossible", UIKit.Danger);
            }
            finally
            {
                if (analysisCts == cts) analysisCts = null;
                cts.Dispose();
            }
        }

        void SetAnalysisProgress(float p)
        {
            analysisPercent.text = "Analyse en cours… " + Mathf.RoundToInt(p * 100f) + " %";
            analysisFill.anchorMax = new Vector2(Mathf.Max(0.04f, p), 1f);
        }

        void ShowAnalysisResults()
        {
            analysisProgressGroup.SetActive(false);
            analysisResultsGroup.SetActive(true);
            accuracyWhite.text = analysis.AccuracyWhite.ToString("0") + " %";
            accuracyBlack.text = analysis.AccuracyBlack.ToString("0") + " %";
            var qualities = new[] { MoveQuality.Excellent, MoveQuality.Imprecision, MoveQuality.Erreur, MoveQuality.Gaffe };
            for (int q = 0; q < qualities.Length; q++)
            {
                int w = 0, b = 0;
                foreach (var v in analysis.Moves)
                    if (v.Quality == qualities[q]) { if (v.Side == PieceColor.White) w++; else b++; }
                analysisCounts[q + 1, 0].text = w.ToString();
                analysisCounts[q + 1, 1].text = b.ToString();
            }

            // Liste des erreurs et gaffes, reconstruite à chaque affichage.
            for (int i = analysisList.childCount - 1; i >= 0; i--)
            {
                var child = analysisList.GetChild(i);
                if (child != analysisEmpty.transform) Destroy(child.gameObject);
            }
            var bad = new List<MoveVerdict>();
            foreach (var v in analysis.Moves)
                if (v.Quality == MoveQuality.Erreur || v.Quality == MoveQuality.Gaffe) bad.Add(v);
            analysisEmpty.gameObject.SetActive(bad.Count == 0);
            string fen = analyzedGame.StartFen;
            foreach (var v in bad)
            {
                var verdict = v;
                bool blunder = v.Quality == MoveQuality.Gaffe;
                string text = MoveLabel(fen, v.Ply, v.San) + (blunder ? " ??  gaffe" : " ?  erreur") +
                              (string.IsNullOrEmpty(v.BestSan) ? "" : "  ·  mieux : " + GamePrefs.San(v.BestSan));
                var row = UIKit.Button("Verdict" + v.Ply, analysisList, text, () => { sfx.Play(Sfx.Click); OpenVerdict(verdict); },
                    blunder ? UIKit.Danger : Orange, 32);
                UIKit.Size(row, 84);
                var label = row.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                UIKit.Stretch(label.rectTransform, 24, 4, 24, 4);
            }
            analysisScroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>Revue sur le coup fautif : position d'avant, flèche du meilleur coup.</summary>
        void OpenVerdict(MoveVerdict v)
        {
            Show(analysisOverlay, false);
            EnterReview(v.Ply);
            reviewNote = MoveLabel(analyzedGame.StartFen, v.Ply, v.San) + (v.Quality == MoveQuality.Gaffe ? " était une gaffe" : " était une erreur") +
                         (string.IsNullOrEmpty(v.BestSan) ? "" : " · mieux : " + GamePrefs.San(v.BestSan));
            reviewNotePly = v.Ply;
            Refresh();
            if (!v.BestMove.IsNull) board.ShowHintArrow(v.BestMove.From, v.BestMove.To);
        }

        /// <summary>Ferme l'écran d'analyse (et l'annule si elle tourne) ; revient à l'écran de fin.</summary>
        void CloseAnalysis()
        {
            if (analysisCts != null) analysisCts.Cancel();
            analysisCts = null; // une nouvelle analyse peut repartir tout de suite
            Show(analysisOverlay, false);
            if (match.Game != null && match.Game.IsOver && !reviewing) Show(endOverlay, true);
        }

        void CancelAnalysis()
        {
            if (analysisCts != null) analysisCts.Cancel();
            analysisCts = null;
            analysis = null;
            analyzedGame = null;
            if (analysisOverlay != null) Show(analysisOverlay, false);
        }

        /// <summary>Recette automatique : ouvre la ligne d'erreur numéro <paramref name="index"/> de l'analyse.</summary>
        public bool OpenVerdictForTest(int index)
        {
            if (analysis == null) return false;
            int k = 0;
            foreach (var v in analysis.Moves)
            {
                if (v.Quality != MoveQuality.Erreur && v.Quality != MoveQuality.Gaffe) continue;
                if (k++ == index) { OpenVerdict(v); return true; }
            }
            return false;
        }
    }
}
