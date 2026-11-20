using System;
using System.Threading;
using KevChess.Core;
using KevChess.Game.Audio;
using KevChess.Game.Match;
using KevChess.Game.Visuals;
using UnityEngine;
using UnityEngine.UI;

namespace KevChess.Game.UI
{
    /// <summary>Écrans secondaires : partie à deux, réglages, puzzles, actions de partie (« Plus… ») et messages brefs.</summary>
    public sealed partial class Hud
    {
        const float ToastSeconds = 1.8f;

        GameObject localOverlay, settingsOverlay, puzzlesOverlay, actionsOverlay;

        // Partie à deux.
        int localClockChoice;
        Button[] localClockButtons;
        Button localFlipButton;

        // Réglages.
        Button soundButton, hapticsButton, themeButton, contrastButton, notationButton, animButton, musicButton, flipButton;
        Text volumeLabel;
        AmbientMusic music;

        // Puzzles.
        Text puzzleProgress;
        Text[] puzzleMarks;
        ScrollRect puzzleScroll;

        // Actions de partie.
        Button actionReview, actionDraw, actionCopy, actionAnalyze, actionResign;
        bool drawPending;

        // Message bref.
        RectTransform toastRect;
        CanvasGroup toastGroup;
        Text toastText;
        float toastUntil;

        bool IsScreenOverlayOpen =>
            localOverlay.activeSelf || settingsOverlay.activeSelf || puzzlesOverlay.activeSelf || actionsOverlay.activeSelf ||
            analysisOverlay.activeSelf;

        /// <summary>Musique d'ambiance pilotée par l'écran Réglages (créée par KevChessApp).</summary>
        public void SetMusic(AmbientMusic ambient) => music = ambient;

        /// <summary>Retour Android sur un écran secondaire ; vrai s'il a été traité.</summary>
        bool HandleBackScreens()
        {
            if (actionsOverlay.activeSelf) { Show(actionsOverlay, false); return true; }
            if (analysisOverlay.activeSelf) { CloseAnalysis(); return true; }
            if (settingsOverlay.activeSelf) { Show(settingsOverlay, false); ShowMenu(true); return true; }
            if (puzzlesOverlay.activeSelf)
            {
                Show(puzzlesOverlay, false);
                if (!match.IsPuzzle) ShowMenu(true);
                return true;
            }
            if (localOverlay.activeSelf) { Show(localOverlay, false); ShowMenu(true); return true; }
            return false;
        }

        // ---------------------------------------------------------------- Partie à deux

        void BuildLocalSetup()
        {
            localOverlay = Overlay("LocalSetup", out var card, 940, 900, UIKit.Cyan);
            UIKit.VStack(card, 18, 50);
            FitHeight(card);
            UIKit.Size(UIKit.Label("Title", card, "Partie à deux", 72, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold), 100);
            UIKit.Size(UIKit.Label("ClockLabel", card, "Cadence", 34, UIKit.TextDim), 48);
            localClockButtons = BuildClockChoices(card, i => { localClockChoice = i; RefreshLocalSetup(); });
            localFlipButton = UIKit.Button("Flip", card, "", () =>
            {
                sfx.Play(Sfx.Click);
                GamePrefs.AutoFlip = !GamePrefs.AutoFlip;
                RefreshLocalSetup();
            }, UIKit.TextDim, 36);
            UIKit.Size(localFlipButton, 96);

            var row = UIKit.Rect("Actions", card);
            UIKit.HStack(row, 16);
            UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), 116);
            UIKit.Button("Back", row, "Retour", () => { sfx.Play(Sfx.Click); Show(localOverlay, false); ShowMenu(true); }, UIKit.TextDim);
            UIKit.Button("Play", row, "Jouer", StartLocalMatch, UIKit.Cyan);
        }

        void OpenLocalSetup()
        {
            localClockChoice = GamePrefs.ClockPreset(false);
            RefreshLocalSetup();
            Show(menuOverlay, false);
            Show(localOverlay, true);
        }

        void RefreshLocalSetup()
        {
            for (int i = 0; i < localClockButtons.Length; i++) Highlight(localClockButtons[i], i == localClockChoice, UIKit.Cyan);
            UIKit.SetButtonText(localFlipButton, GamePrefs.AutoFlip ? "Caméra : suit le camp au trait" : "Caméra : fixe");
        }

        void StartLocalMatch()
        {
            sfx.Play(Sfx.Click);
            ConfirmReplace(StartLocalMatchNow);
        }

        void StartLocalMatchNow()
        {
            GamePrefs.SetClockPreset(false, localClockChoice);
            var settings = new MatchSettings { Mode = MatchMode.LocalTwoPlayers };
            settings.Clock = TimeControl.Presets[localClockChoice];
            match.StartNew(settings);
            Show(localOverlay, false);
            ShowMenu(false);
        }

        // ---------------------------------------------------------------- Réglages

        void BuildSettings()
        {
            settingsOverlay = Overlay("Settings", out var card, 900, 1300, UIKit.Cyan);
            UIKit.VStack(card, 14, 48);
            FitHeight(card);
            UIKit.Size(UIKit.Label("Title", card, "Réglages", 72, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold), 100);

            soundButton = SettingButton(card, "Sound", () => sfx.Muted = !sfx.Muted);
            hapticsButton = SettingButton(card, "Haptics", () => { Haptics.Enabled = !Haptics.Enabled; Haptics.Select(); });
            themeButton = SettingButton(card, "Theme", () => GamePrefs.Theme = (GamePrefs.Theme + 1) % VisualTheme.Names.Length);
            contrastButton = SettingButton(card, "Contrast", () => GamePrefs.HighContrast = !GamePrefs.HighContrast);
            notationButton = SettingButton(card, "Notation", () => { GamePrefs.FrenchNotation = !GamePrefs.FrenchNotation; Refresh(); });
            animButton = SettingButton(card, "Animations", () => GamePrefs.AnimationIndex = (GamePrefs.AnimationIndex + 1) % GamePrefs.AnimationNames.Length);
            musicButton = SettingButton(card, "Music", () => { if (music != null) music.Enabled = !music.Enabled; });

            var volume = UIKit.Rect("Volume", card);
            var h = UIKit.HStack(volume, 14);
            h.childForceExpandWidth = false;
            UIKit.Size(volume.gameObject.AddComponent<LayoutElement>(), 92);
            UIKit.Size(UIKit.Button("Down", volume, "−", () => ChangeVolume(-0.1f), null, 48), -1, 150);
            volumeLabel = UIKit.Label("Value", volume, "", 36, UIKit.Text);
            UIKit.Size(volumeLabel, -1, 300, 1);
            UIKit.Size(UIKit.Button("Up", volume, "+", () => ChangeVolume(0.1f), null, 48), -1, 150);

            flipButton = SettingButton(card, "Flip", () => GamePrefs.AutoFlip = !GamePrefs.AutoFlip);

            var tipsAgain = UIKit.Button("TipsAgain", card, "Revoir les gestes du plateau", () =>
            {
                sfx.Play(Sfx.Click);
                RestartTips();
                Show(settingsOverlay, false);
                // Les bulles s'affichent sur la partie en cours ; sans partie, on revient au menu.
                if (match.Game == null || match.Game.IsOver) ShowMenu(true);
            }, UIKit.Cyan, 36);
            UIKit.Size(tipsAgain, 92);

            var back = UIKit.Button("Back", card, "Retour", () => { sfx.Play(Sfx.Click); Show(settingsOverlay, false); ShowMenu(true); }, UIKit.Magenta);
            UIKit.Size(back, 108);
        }

        Button SettingButton(RectTransform card, string name, Action toggle)
        {
            var b = UIKit.Button(name, card, "", () =>
            {
                toggle();
                sfx.Play(Sfx.Click);
                RefreshSettings();
            }, UIKit.TextDim, 36);
            UIKit.Size(b, 92);
            return b;
        }

        void ChangeVolume(float delta)
        {
            if (music == null) return;
            music.Volume = Mathf.Round((music.Volume + delta) * 10f) / 10f;
            sfx.Play(Sfx.Click);
            RefreshSettings();
        }

        void OpenSettings()
        {
            RefreshSettings();
            Show(menuOverlay, false);
            Show(settingsOverlay, true);
        }

        void RefreshSettings()
        {
            UIKit.SetButtonText(soundButton, sfx.Muted ? "Son : coupé" : "Son : activé");
            UIKit.SetButtonText(hapticsButton, Haptics.Enabled ? "Vibrations : activées" : "Vibrations : coupées");
            UIKit.SetButtonText(themeButton, "Thème : " + VisualTheme.Names[GamePrefs.Theme]);
            UIKit.SetButtonText(contrastButton, "Lisibilité renforcée : " + (GamePrefs.HighContrast ? "oui" : "non"));
            UIKit.SetButtonText(notationButton, "Notation : " + (GamePrefs.FrenchNotation ? "française (C, F, T, D, R)" : "anglaise (N, B, R, Q, K)"));
            UIKit.SetButtonText(animButton, "Animations : " + GamePrefs.AnimationNames[GamePrefs.AnimationIndex]);
            bool musicOn = music != null && music.Enabled;
            UIKit.SetButtonText(musicButton, "Musique d'ambiance : " + (musicOn ? "activée" : "coupée"));
            volumeLabel.text = "Volume : " + Mathf.RoundToInt((music != null ? music.Volume : 0f) * 100f) + " %";
            volumeLabel.color = musicOn ? UIKit.Text : UIKit.TextDim;
            UIKit.SetButtonText(flipButton, "Partie à deux : caméra " + (GamePrefs.AutoFlip ? "qui suit le camp au trait" : "fixe"));
        }

        // ---------------------------------------------------------------- Puzzles

        void BuildPuzzleList()
        {
            puzzlesOverlay = Overlay("Puzzles", out var card, 920, 1600, UIKit.Magenta);
            UIKit.VStack(card, 16, 44);
            UIKit.Size(UIKit.Label("Title", card, "Puzzles", 72, UIKit.Magenta, TextAnchor.MiddleCenter, FontStyle.Bold), 96);
            puzzleProgress = UIKit.Label("Progress", card, "", 32, UIKit.TextDim);
            UIKit.Size(puzzleProgress, 48);

            var content = UIKit.ScrollList("List", card, 10, out puzzleScroll);
            var le = UIKit.Size(puzzleScroll, 1100);
            le.flexibleHeight = 1;

            var puzzles = PuzzleSet.All;
            puzzleMarks = new Text[puzzles.Count];
            for (int i = 0; i < puzzles.Count; i++)
            {
                int idx = i;
                var row = UIKit.Button("Puzzle" + i, content, (i + 1) + ". " + puzzles[i].Title, () =>
                {
                    sfx.Play(Sfx.Click);
                    StartPuzzle(idx);
                }, puzzles[i].MateIn == 1 ? UIKit.Cyan : UIKit.Magenta, 36);
                UIKit.Size(row, 92);
                var label = row.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleLeft;
                UIKit.Stretch(label.rectTransform, 28, 4, 250, 4);
                puzzleMarks[i] = UIKit.Label("Mate", row.transform, "", 30, UIKit.TextDim, TextAnchor.MiddleRight);
                UIKit.Anchor(puzzleMarks[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24, 0), new Vector2(240, 80));
            }

            var back = UIKit.Button("Back", card, "Retour", () =>
            {
                sfx.Play(Sfx.Click);
                Show(puzzlesOverlay, false);
                if (!match.IsPuzzle) ShowMenu(true);
            }, UIKit.TextDim);
            UIKit.Size(back, 106);
        }

        void OpenPuzzleList()
        {
            var puzzles = PuzzleSet.All;
            for (int i = 0; i < puzzles.Count; i++)
            {
                bool done = GamePrefs.IsPuzzleSolved(puzzles[i].Id);
                puzzleMarks[i].text = "Mat en " + puzzles[i].MateIn + (done ? "  ✓" : "");
                puzzleMarks[i].color = done ? UIKit.Cyan : UIKit.TextDim;
            }
            puzzleProgress.text = GamePrefs.SolvedCount + " / " + puzzles.Count + " résolus";
            Show(menuOverlay, false);
            Show(puzzlesOverlay, true);
        }

        void StartPuzzle(int index)
        {
            var puzzles = PuzzleSet.All;
            if (index < 0 || index >= puzzles.Count) return;
            match.StartPuzzle(puzzles[index]);
            Show(puzzlesOverlay, false);
            Show(menuOverlay, false);
        }

        int CurrentPuzzleIndex()
        {
            if (!match.IsPuzzle) return -1;
            var puzzles = PuzzleSet.All;
            for (int i = 0; i < puzzles.Count; i++)
                if (puzzles[i].Id == match.Puzzle.Puzzle.Id) return i;
            return -1;
        }

        /// <summary>Puzzle suivant : le prochain non résolu, sinon simplement le suivant.</summary>
        void NextPuzzle()
        {
            var puzzles = PuzzleSet.All;
            int current = CurrentPuzzleIndex();
            for (int k = 1; k <= puzzles.Count; k++)
            {
                int i = (current + k) % puzzles.Count;
                if (!GamePrefs.IsPuzzleSolved(puzzles[i].Id)) { StartPuzzle(i); return; }
            }
            StartPuzzle((current + 1) % puzzles.Count);
        }

        string PuzzleCaption(Puzzle puzzle)
        {
            int index = CurrentPuzzleIndex();
            return "Puzzle " + (index + 1) + " / " + PuzzleSet.All.Count + " · " + puzzle.Title;
        }

        void OnPuzzleFeedback(PuzzleMoveResult result)
        {
            switch (result)
            {
                case PuzzleMoveResult.Wrong:
                    sfx.PlayWrong();
                    Haptics.Illegal();
                    Toast("Ce n'est pas le bon coup", UIKit.Danger);
                    break;
                case PuzzleMoveResult.Correct:
                    Toast("Bien vu ! Continuez", UIKit.Cyan);
                    break;
                case PuzzleMoveResult.Solved:
                    GamePrefs.MarkPuzzleSolved(match.Puzzle.Puzzle.Id);
                    sfx.PlayPuzzleSolved();
                    Toast(match.Puzzle.Mistakes == 0 ? "Puzzle résolu du premier coup !" : "Puzzle résolu !", UIKit.Cyan);
                    break;
            }
            Refresh();
        }

        // ---------------------------------------------------------------- Actions de partie (« Plus… »)

        void BuildActions()
        {
            actionsOverlay = Overlay("Actions", out var card, 760, 900, UIKit.Magenta);
            var dimButton = actionsOverlay.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => Show(actionsOverlay, false));
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            UIKit.VStack(card, 18, 44);
            FitHeight(card);
            UIKit.Size(UIKit.Label("Title", card, "Partie", 60, UIKit.Magenta, TextAnchor.MiddleCenter, FontStyle.Bold), 84);

            actionReview = ActionButton(card, "Review", "Revoir la partie", UIKit.Cyan, () => EnterReview(match.Game.Moves.Count));
            actionDraw = ActionButton(card, "Draw", "Proposer nulle", UIKit.Cyan, OfferDraw);
            actionCopy = ActionButton(card, "Copy", "Copier la partie (PGN)", UIKit.Cyan, CopyPgn);
            actionAnalyze = ActionButton(card, "Analyze", "Analyser la partie", UIKit.Magenta, OpenAnalysis);
            actionResign = ActionButton(card, "Resign", "Abandonner", UIKit.Magenta, () => Confirm("Abandonner la partie ?", () => match.Resign()));
            UIKit.Size(UIKit.Button("Close", card, "Fermer", () => { sfx.Play(Sfx.Click); Show(actionsOverlay, false); }, UIKit.TextDim), 96);
        }

        Button ActionButton(RectTransform card, string name, string text, Color accent, Action action)
        {
            var b = UIKit.Button(name, card, text, () =>
            {
                sfx.Play(Sfx.Click);
                Show(actionsOverlay, false);
                action();
            }, accent, 38);
            UIKit.Size(b, 104);
            return b;
        }

        void OpenActions()
        {
            var game = match.Game;
            if (game == null || match.IsPuzzle) return;
            bool over = game.IsOver;
            actionReview.gameObject.SetActive(game.Moves.Count > 0);
            actionDraw.gameObject.SetActive(!over);
            actionDraw.interactable = !drawPending;
            actionCopy.gameObject.SetActive(true);
            actionAnalyze.gameObject.SetActive(over && game.Moves.Count > 0);
            actionResign.gameObject.SetActive(!over);
            Show(actionsOverlay, true);
        }

        /// <summary>Proposition de nulle : l'IA décide seule, en local l'autre joueur confirme.</summary>
        async void OfferDraw()
        {
            var game = match.Game;
            if (game == null || game.IsOver || drawPending) return;
            if (match.Settings.Mode == MatchMode.LocalTwoPlayers)
            {
                bool whiteOffers = game.SideToMove == PieceColor.White;
                Confirm((whiteOffers ? "Les Blancs proposent nulle.\nLes Noirs acceptent-ils ?" : "Les Noirs proposent nulle.\nLes Blancs acceptent-ils ?"),
                    () => match.AgreeDraw());
                return;
            }

            // Contre l'IA : la partie reste en pause le temps de la réponse.
            drawPending = true;
            Toast("Proposition de nulle envoyée…", UIKit.TextDim);
            bool accepted;
            try
            {
                var aiSide = Piece.Opposite(match.Settings.HumanColor);
                accepted = await AIAdapter.AcceptsDrawAsync(game, aiSide, CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                accepted = false;
            }
            finally
            {
                drawPending = false;
            }
            if (this == null || match.Game != game || game.IsOver) return;
            if (accepted)
            {
                Toast("L'IA accepte la nulle", UIKit.Cyan);
                match.AgreeDraw();
            }
            else Toast("L'IA refuse la nulle", UIKit.Magenta);
        }

        void CopyPgn()
        {
            if (match.Game == null) return;
            GUIUtility.systemCopyBuffer = match.Game.ToPgn();
            Toast("PGN copié", UIKit.Cyan);
        }

        // ---------------------------------------------------------------- Message bref

        void BuildToast()
        {
            var panel = UIKit.Panel("Toast", transform, UIKit.GlassStrong, true, UIKit.Cyan);
            panel.raycastTarget = false;
            toastRect = panel.rectTransform;
            UIKit.Anchor(toastRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 210), new Vector2(860, 96));
            toastGroup = panel.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;
            toastText = UIKit.Label("Text", toastRect, "", 38, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(toastText.rectTransform, 24, 6, 24, 6);
            toastText.resizeTextForBestFit = true;
            toastText.resizeTextMinSize = 20;
            toastText.resizeTextMaxSize = 38;
            panel.gameObject.SetActive(false);
        }

        /// <summary>Message bref au-dessus de la barre d'actions, par-dessus les écrans ouverts.</summary>
        public void Toast(string message, Color accent)
        {
            toastText.text = message;
            toastText.color = Color.Lerp(UIKit.Text, accent, 0.35f);
            var outline = toastRect.Find("Outline");
            if (outline != null) outline.GetComponent<Image>().color = accent * new Color(1, 1, 1, 0.7f);
            toastRect.anchoredPosition = new Vector2(0, reviewing ? 330 : 210);
            toastRect.gameObject.SetActive(true);
            toastRect.SetAsLastSibling();
            toastUntil = Time.unscaledTime + ToastSeconds;
            toastGroup.alpha = 1f;
        }

        /// <summary>Dernier message affiché (pour la recette automatique).</summary>
        public string LastToast => toastText.text;

        void UpdateToast()
        {
            if (!toastRect.gameObject.activeSelf) return;
            float left = toastUntil - Time.unscaledTime;
            toastGroup.alpha = Mathf.Clamp01(left / 0.3f);
            if (left <= 0f) toastRect.gameObject.SetActive(false);
        }
    }
}
