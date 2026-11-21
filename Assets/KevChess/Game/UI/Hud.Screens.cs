using System;
using System.Threading;
using KevChess.AI.Uci;
using KevChess.Core;
using KevChess.Core.Ux;
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
        Button soundButton, hapticsButton, contrastButton, notationButton, animButton, musicButton, flipButton, engineButton;
        Button[] themeButtons;
        Text volumeLabel;
        VolumeSlider volumeSlider;
        Coroutine peekRoutine;
        AmbientMusic music;

        // Puzzles.
        Text puzzleProgress;
        Text[] puzzleMarks;
        ScrollRect puzzleScroll;
        Button puzzleContinue;
        Text[] puzzleSectionLabels;
        RectTransform[] puzzleSectionFills;

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
            analysisOverlay.activeSelf || moveListOverlay.activeSelf;

        /// <summary>Musique d'ambiance pilotée par l'écran Réglages (créée par KevChessApp).</summary>
        public void SetMusic(AmbientMusic ambient) => music = ambient;

        /// <summary>Retour Android sur un écran secondaire ; vrai s'il a été traité.</summary>
        bool HandleBackScreens()
        {
            if (moveListOverlay.activeSelf) { Show(moveListOverlay, false); return true; }
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
            UIKit.VStack(card, 12, 44);
            FitHeight(card);
            UIKit.Size(UIKit.Label("Title", card, "Réglages", 72, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold), 96);

            // Oui / non : interrupteurs.
            soundButton = SwitchSetting(card, "Sound", "Son", () => sfx.Muted = !sfx.Muted);
            hapticsButton = SwitchSetting(card, "Haptics", "Vibrations", () => { Haptics.Enabled = !Haptics.Enabled; Haptics.Select(); });
            musicButton = SwitchSetting(card, "Music", "Musique d'ambiance", () => { if (music != null) music.Enabled = !music.Enabled; });

            // Volume de la musique : curseur.
            var volume = UIKit.Rect("Volume", card);
            UIKit.Size(volume.gameObject.AddComponent<LayoutElement>(), 84);
            volumeLabel = UIKit.Label("Label", volume, "", 32, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.Anchor(volumeLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28, 0), new Vector2(250, 80));
            volumeSlider = VolumeSlider.Create("Slider", volume, UIKit.Cyan);
            var sr = (RectTransform)volumeSlider.transform;
            sr.anchorMin = new Vector2(0f, 0f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.offsetMin = new Vector2(270, 0);
            sr.offsetMax = new Vector2(-10, 0);
            volumeSlider.Changed = v =>
            {
                if (music == null) return;
                music.Volume = v;
                RefreshSettings();
            };

            // Thème : quatre pastilles aux couleurs des thèmes ; la carte s'efface un instant pour montrer le plateau.
            UIKit.Size(UIKit.Label("ThemeLabel", card, "Thème", 32, UIKit.TextDim, TextAnchor.MiddleLeft), 44);
            var themes = UIKit.Rect("Themes", card);
            UIKit.HStack(themes, 14);
            UIKit.Size(themes.gameObject.AddComponent<LayoutElement>(), 112);
            themeButtons = new Button[VisualTheme.Names.Length];
            for (int i = 0; i < themeButtons.Length; i++)
            {
                int idx = i;
                themeButtons[i] = ThemeSwatch(themes, idx, () =>
                {
                    sfx.Play(Sfx.Click);
                    GamePrefs.Theme = idx;
                    RefreshSettings();
                    PeekBoard(settingsOverlay);
                });
            }

            contrastButton = SwitchSetting(card, "Contrast", "Lisibilité renforcée", () => GamePrefs.HighContrast = !GamePrefs.HighContrast);
            flipButton = SwitchSetting(card, "Flip", "Partie à deux : la caméra suit le camp au trait", () => GamePrefs.AutoFlip = !GamePrefs.AutoFlip);

            // Choix à plusieurs valeurs : valeur affichée à droite, toucher pour changer.
            notationButton = ValueSetting(card, "Notation", "Notation", () => { GamePrefs.FrenchNotation = !GamePrefs.FrenchNotation; Refresh(); });
            animButton = ValueSetting(card, "Animations", "Animations", () => GamePrefs.AnimationIndex = (GamePrefs.AnimationIndex + 1) % GamePrefs.AnimationNames.Length);
            engineButton = ValueSetting(card, "Engine", "Moteur de l'IA", () =>
                GamePrefs.Engine = GamePrefs.Engine == EngineKind.Stockfish ? EngineKind.KevChess : EngineKind.Stockfish);

            var tipsAgain = UIKit.Button("TipsAgain", card, "Revoir les gestes du plateau", () =>
            {
                sfx.Play(Sfx.Click);
                RestartTips();
                Show(settingsOverlay, false);
                // Les bulles s'affichent sur la partie en cours ; sans partie, on revient au menu.
                if (match.Game == null || match.Game.IsOver) ShowMenu(true);
            }, UIKit.Cyan, 34);
            UIKit.Size(tipsAgain, 88);

            var back = UIKit.Button("Back", card, "Retour", () => { sfx.Play(Sfx.Click); Show(settingsOverlay, false); ShowMenu(true); }, UIKit.Magenta);
            UIKit.Size(back, 104);
        }

        Button SwitchSetting(RectTransform card, string name, string title, Action toggle)
        {
            var b = UIKit.SwitchRow(name, card, title, () =>
            {
                toggle();
                sfx.Play(Sfx.Click);
                RefreshSettings();
            });
            UIKit.Size(b, 88);
            return b;
        }

        Button ValueSetting(RectTransform card, string name, string title, Action change)
        {
            var b = UIKit.ValueRow(name, card, title, () =>
            {
                change();
                sfx.Play(Sfx.Click);
                RefreshSettings();
            });
            UIKit.Size(b, 88);
            return b;
        }

        /// <summary>Pastille de thème : trois bandes (blancs, noirs, sélection) et le nom dessous.</summary>
        Button ThemeSwatch(RectTransform row, int index, Action onClick)
        {
            var palette = VisualTheme.PaletteOf(index);
            var b = UIKit.Button("Theme" + index, row, VisualTheme.Names[index], onClick, palette.Selection, 24);
            var label = b.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.LowerCenter;
            label.fontStyle = FontStyle.Normal;
            UIKit.Stretch(label.rectTransform, 6, 6, 6, 4);
            var colors = new[] { palette.WhiteGlow, palette.BlackGlow, palette.Selection };
            for (int i = 0; i < colors.Length; i++)
            {
                var dot = new GameObject("Dot" + i, typeof(RectTransform)).AddComponent<Image>();
                dot.transform.SetParent(b.transform, false);
                dot.sprite = UIKit.Glow;
                dot.color = colors[i];
                dot.raycastTarget = false;
                UIKit.Anchor(dot.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - 1) * 46f, -14), new Vector2(54, 54));
            }
            return b;
        }

        /// <summary>Rend l'écran translucide un instant, pour voir l'effet d'un thème sur le plateau.</summary>
        void PeekBoard(GameObject overlay)
        {
            if (peekRoutine != null) StopCoroutine(peekRoutine);
            peekRoutine = StartCoroutine(Peek(overlay.GetComponent<CanvasGroup>()));
        }

        System.Collections.IEnumerator Peek(CanvasGroup group)
        {
            const float low = 0.28f, fadeOut = 0.15f, hold = 1.1f, fadeIn = 0.3f;
            // Écran fermé en cours de route : il retrouve aussitôt son opacité.
            for (float t = 0f; t < fadeOut + hold + fadeIn && group.gameObject.activeSelf; t += Time.unscaledDeltaTime)
            {
                group.alpha = t < fadeOut ? Mathf.Lerp(1f, low, t / fadeOut)
                    : t < fadeOut + hold ? low
                    : Mathf.Lerp(low, 1f, (t - fadeOut - hold) / fadeIn);
                yield return null;
            }
            group.alpha = 1f;
            peekRoutine = null;
        }

        void OpenSettings()
        {
            RefreshSettings();
            Show(menuOverlay, false);
            Show(settingsOverlay, true);
        }

        void RefreshSettings()
        {
            UIKit.SetSwitch(soundButton, !sfx.Muted, UIKit.Cyan);
            UIKit.SetSwitch(hapticsButton, Haptics.Enabled, UIKit.Cyan);
            bool musicOn = music != null && music.Enabled;
            UIKit.SetSwitch(musicButton, musicOn, UIKit.Cyan);
            float vol = music != null ? music.Volume : 0f;
            volumeSlider.Value = vol;
            volumeLabel.text = "Volume " + SliderMath.Percent(vol);
            volumeLabel.color = musicOn ? UIKit.Text : UIKit.TextDim;
            for (int i = 0; i < themeButtons.Length; i++)
                Highlight(themeButtons[i], i == GamePrefs.Theme, VisualTheme.PaletteOf(i).Selection);
            UIKit.SetSwitch(contrastButton, GamePrefs.HighContrast, UIKit.Cyan);
            UIKit.SetSwitch(flipButton, GamePrefs.AutoFlip, UIKit.Cyan);
            UIKit.SetRowValue(notationButton, GamePrefs.FrenchNotation ? "française (C, F, T, D, R)" : "anglaise (N, B, R, Q, K)");
            UIKit.SetRowValue(animButton, GamePrefs.AnimationNames[GamePrefs.AnimationIndex]);
            UIKit.SetRowValue(engineButton, GamePrefs.Engine == EngineKind.KevChess ? "KevChess (intégrée)"
                : AIAdapter.StockfishUsable ? "Stockfish" : "Stockfish absent : KevChess");
        }

        // ---------------------------------------------------------------- Puzzles

        void BuildPuzzleList()
        {
            puzzlesOverlay = Overlay("Puzzles", out var card, 920, 1600, UIKit.Magenta);
            UIKit.VStack(card, 16, 44);
            UIKit.Size(UIKit.Label("Title", card, "Puzzles", 72, UIKit.Magenta, TextAnchor.MiddleCenter, FontStyle.Bold), 96);
            puzzleProgress = UIKit.Label("Progress", card, "", 32, UIKit.TextDim);
            UIKit.Size(puzzleProgress, 48);

            // Reprise directe au premier puzzle non résolu.
            puzzleContinue = UIKit.Button("Continue", card, "", () =>
            {
                sfx.Play(Sfx.Click);
                int next = PuzzleGroups.FirstUnsolved(PuzzleSet.All, GamePrefs.IsPuzzleSolved);
                if (next >= 0) StartPuzzle(next);
            }, UIKit.Magenta);
            UIKit.Size(puzzleContinue, 104);

            var content = UIKit.ScrollList("List", card, 10, out puzzleScroll);
            var le = UIKit.Size(puzzleScroll, 1000);
            le.flexibleHeight = 1;

            // Une section par difficulté : titre, avancement et barre, puis ses puzzles.
            var puzzles = PuzzleSet.All;
            puzzleMarks = new Text[puzzles.Count];
            var sections = PuzzleGroups.Build(puzzles, GamePrefs.IsPuzzleSolved);
            puzzleSectionLabels = new Text[sections.Count];
            puzzleSectionFills = new RectTransform[sections.Count];
            for (int s = 0; s < sections.Count; s++)
            {
                var section = sections[s];
                var accent = section.MateIn == 1 ? UIKit.Cyan : UIKit.Magenta;
                var head = UIKit.Rect("Section" + section.MateIn, content);
                UIKit.Size(head.gameObject.AddComponent<LayoutElement>(), 96);
                var title = UIKit.Label("Title", head, section.Title, 40, accent, TextAnchor.UpperLeft, FontStyle.Bold);
                UIKit.Stretch(title.rectTransform, 12, 36, 12, 6);
                puzzleSectionLabels[s] = UIKit.Label("Count", head, "", 30, UIKit.TextDim, TextAnchor.UpperRight);
                UIKit.Stretch(puzzleSectionLabels[s].rectTransform, 12, 36, 16, 10);
                var fill = UIKit.ProgressBar("Bar", head, accent);
                var bar = (RectTransform)fill.parent;
                bar.anchorMin = new Vector2(0f, 0f);
                bar.anchorMax = new Vector2(1f, 0f);
                bar.pivot = new Vector2(0.5f, 0f);
                bar.offsetMin = new Vector2(12, 4);
                bar.offsetMax = new Vector2(-12, 30);
                puzzleSectionFills[s] = fill;

                foreach (int i in section.Indices)
                {
                    int idx = i;
                    var row = UIKit.Button("Puzzle" + i, content, (i + 1) + ". " + puzzles[i].Title, () =>
                    {
                        sfx.Play(Sfx.Click);
                        StartPuzzle(idx);
                    }, accent, 36);
                    UIKit.Size(row, 92);
                    var label = row.GetComponentInChildren<Text>();
                    label.alignment = TextAnchor.MiddleLeft;
                    UIKit.Stretch(label.rectTransform, 28, 4, 120, 4);
                    puzzleMarks[i] = UIKit.Label("Mark", row.transform, "", 34, UIKit.Cyan, TextAnchor.MiddleRight, FontStyle.Bold);
                    UIKit.Anchor(puzzleMarks[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28, 0), new Vector2(100, 80));
                }
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
                puzzleMarks[i].text = GamePrefs.IsPuzzleSolved(puzzles[i].Id) ? "✓" : "";
            var sections = PuzzleGroups.Build(puzzles, GamePrefs.IsPuzzleSolved);
            for (int s = 0; s < sections.Count && s < puzzleSectionLabels.Length; s++)
            {
                puzzleSectionLabels[s].text = sections[s].Progress;
                puzzleSectionFills[s].anchorMax = new Vector2(Mathf.Max(0.02f, sections[s].Fraction), 1f);
            }
            puzzleProgress.text = GamePrefs.SolvedCount + " / " + puzzles.Count + " résolus";
            int next = PuzzleGroups.FirstUnsolved(puzzles, GamePrefs.IsPuzzleSolved);
            puzzleContinue.gameObject.SetActive(next >= 0);
            if (next >= 0) UIKit.SetButtonText(puzzleContinue, PuzzleGroups.ContinueLabel(next));
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
