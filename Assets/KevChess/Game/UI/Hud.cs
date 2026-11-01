using System;
using System.Collections;
using System.Text;
using KevChess.Core;
using KevChess.Game.Audio;
using KevChess.Game.CameraRig;
using KevChess.Game.Match;
using UnityEngine;
using UnityEngine.UI;

namespace KevChess.Game.UI
{
    /// <summary>
    /// Interface complète construite par code : bandeau des joueurs, statut, coups, barre d'actions,
    /// menu principal, réglages IA, choix de promotion, fin de partie, confirmation.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        MatchController match;
        OrbitCamera orbit;
        SoundBank sfx;
        Action<Move> showHint;

        Canvas canvas;
        CanvasScaler scaler;
        RectTransform safeRoot;
        Rect appliedSafeArea;

        Text whiteName, blackName, whiteScore, blackScore, status, movesLine;
        Image whiteTurn, blackTurn;
        Button undoButton, hintButton, viewButton, resignButton;
        GameObject hudGroup;

        GameObject menuOverlay, aiOverlay, promoOverlay, endOverlay, confirmOverlay;
        Button continueButton, resumeButton, soundButton, hapticsButton;
        Text endTitle, endReason;
        Action confirmAction;
        Text confirmText;
        Action<PieceType> promotionCallback;

        // Réglages en cours d'édition dans l'écran IA.
        int aiColorChoice; // 0 blancs, 1 noirs, 2 hasard
        AILevel aiLevel = AILevel.Medium;
        Button[] colorButtons, levelButtons;

        PieceColor localViewSide = PieceColor.White;

        /// <summary>Bouton « Légendes » du menu : l'app bascule vers le mode fantastique.</summary>
        public event Action LegendsRequested;

        public bool IsBlockingOverlayOpen =>
            menuOverlay.activeSelf || aiOverlay.activeSelf || promoOverlay.activeSelf || confirmOverlay.activeSelf || endOverlay.activeSelf;

        public void Build(MatchController matchController, OrbitCamera camera, SoundBank sounds, Action<Move> onHint)
        {
            match = matchController;
            orbit = camera;
            sfx = sounds;
            showHint = onHint;

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            gameObject.AddComponent<GraphicRaycaster>();

            safeRoot = UIKit.Rect("SafeArea", transform);
            UIKit.Stretch(safeRoot);

            BuildHud();
            BuildMenu();
            BuildAISetup();
            BuildPromotion();
            BuildEndScreen();
            BuildConfirm();

            match.StateChanged += Refresh;
            match.GameEnded += OnGameEnded;
            match.NewMatchStarted += OnNewMatch;
            UpdateLayout();
            Refresh();
        }

        // ---------------------------------------------------------------- HUD

        void BuildHud()
        {
            hudGroup = UIKit.Rect("Hud", safeRoot).gameObject;
            UIKit.Stretch((RectTransform)hudGroup.transform);

            // Bandeau joueurs.
            var top = UIKit.Rect("TopBar", hudGroup.transform);
            UIKit.Anchor(top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(1040, 150));
            UIKit.HStack(top, 20);
            BuildPlayerCard(top, PieceColor.White, out whiteName, out whiteScore, out whiteTurn);
            BuildPlayerCard(top, PieceColor.Black, out blackName, out blackScore, out blackTurn);

            status = UIKit.Label("Status", hudGroup.transform, "", 46, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(1000, 64));
            status.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);

            movesLine = UIKit.Label("Moves", hudGroup.transform, "", 30, UIKit.TextDim);
            UIKit.Anchor(movesLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -250), new Vector2(1000, 44));
            movesLine.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Barre d'actions.
            var bar = UIKit.Panel("BottomBar", hudGroup.transform, UIKit.Glass, true, UIKit.Cyan);
            UIKit.Anchor(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(1040, 140));
            UIKit.HStack(bar.rectTransform, 14, 16);
            UIKit.Button("Menu", bar.transform, "Menu", () => { sfx.Play(Sfx.Click); ShowMenu(true); }, null, 34);
            undoButton = UIKit.Button("Undo", bar.transform, "Annuler", () => { sfx.Play(Sfx.Click); match.UndoForHuman(); }, null, 34);
            hintButton = UIKit.Button("Hint", bar.transform, "Indice", () => { sfx.Play(Sfx.Click); match.RequestHint(showHint); }, null, 34);
            viewButton = UIKit.Button("View", bar.transform, "Vue", OnViewPressed, null, 34);
            resignButton = UIKit.Button("Resign", bar.transform, "Abandon", () =>
            {
                sfx.Play(Sfx.Click);
                Confirm("Abandonner la partie ?", () => match.Resign());
            }, UIKit.Magenta, 34);
        }

        void BuildPlayerCard(RectTransform parent, PieceColor color, out Text name, out Text score, out Image turn)
        {
            var accent = color == PieceColor.White ? UIKit.Cyan : UIKit.Magenta;
            var card = UIKit.Panel(color + "Card", parent, UIKit.Glass, true, accent);
            turn = UIKit.Panel("Turn", card.transform, accent * new Color(1, 1, 1, 0.22f), false);
            UIKit.Stretch(turn.rectTransform);
            turn.raycastTarget = false;

            var dot = new GameObject("Dot", typeof(RectTransform)).AddComponent<Image>();
            dot.transform.SetParent(card.transform, false);
            dot.sprite = UIKit.Glow;
            dot.color = accent;
            dot.raycastTarget = false;
            UIKit.Anchor(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(46, 0), new Vector2(56, 56));

            name = UIKit.Label("Name", card.transform, color == PieceColor.White ? "Blancs" : "Noirs", 38, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Stretch(name.rectTransform, 92, 50, 20, 14);
            score = UIKit.Label("Score", card.transform, "", 30, accent, TextAnchor.MiddleLeft);
            UIKit.Stretch(score.rectTransform, 92, 12, 20, 70);
        }

        void OnViewPressed()
        {
            sfx.Play(Sfx.Click);
            if (match.Settings.Mode == MatchMode.LocalTwoPlayers)
            {
                localViewSide = Piece.Opposite(localViewSide);
                orbit.ResetView(localViewSide);
            }
            else orbit.ResetView(match.ViewSide);
        }

        /// <summary>Vue par défaut (double-tap) : camp du joueur, ou camp au trait en local.</summary>
        public void ResetCamera()
        {
            if (match.Game == null) return;
            if (match.Settings.Mode == MatchMode.LocalTwoPlayers) localViewSide = match.Game.SideToMove;
            orbit.ResetView(match.Settings.Mode == MatchMode.LocalTwoPlayers ? localViewSide : match.ViewSide);
        }

        void Refresh()
        {
            var game = match.Game;
            if (game == null) return;
            var s = match.Settings;

            whiteName.text = PlayerName(PieceColor.White, s);
            blackName.text = PlayerName(PieceColor.Black, s);

            int balance = MaterialBalance(game.Position);
            whiteScore.text = balance > 0 ? "+" + balance : "";
            blackScore.text = balance < 0 ? "+" + (-balance) : "";

            bool whiteToMove = game.SideToMove == PieceColor.White && !game.IsOver;
            bool blackToMove = game.SideToMove == PieceColor.Black && !game.IsOver;
            whiteTurn.enabled = whiteToMove;
            blackTurn.enabled = blackToMove;

            status.color = UIKit.Text;
            if (game.IsOver) status.text = ResultTitle(game, s);
            else if (match.IsAIThinking) status.text = "L'IA réfléchit…";
            else if (game.InCheck)
            {
                status.text = "Échec !";
                status.color = UIKit.Danger;
            }
            else if (s.Mode == MatchMode.VersusAI) status.text = s.IsHuman(game.SideToMove) ? "À vous de jouer" : "";
            else status.text = game.SideToMove == PieceColor.White ? "Aux Blancs de jouer" : "Aux Noirs de jouer";

            movesLine.text = RecentMoves(game, 8);

            undoButton.interactable = game.Moves.Count > 0 && !match.IsAnimating;
            hintButton.interactable = match.CanHumanAct;
            resignButton.interactable = !game.IsOver && !match.IsAnimating;
        }

        static string PlayerName(PieceColor color, MatchSettings s)
        {
            string side = color == PieceColor.White ? "Blancs" : "Noirs";
            if (s.Mode == MatchMode.LocalTwoPlayers) return side;
            return s.HumanColor == color ? "Vous · " + side : "IA " + MatchSettings.LevelName(s.Level);
        }

        static int MaterialBalance(Position pos)
        {
            int total = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                int p = pos[sq];
                if (p == Piece.Empty) continue;
                int v;
                switch (Piece.TypeOf(p))
                {
                    case PieceType.Pawn: v = 1; break;
                    case PieceType.Knight: case PieceType.Bishop: v = 3; break;
                    case PieceType.Rook: v = 5; break;
                    case PieceType.Queen: v = 9; break;
                    default: v = 0; break;
                }
                total += Piece.ColorOf(p) == PieceColor.White ? v : -v;
            }
            return total;
        }

        static string RecentMoves(ChessGame game, int maxPlies)
        {
            var san = game.SanMoves;
            if (san.Count == 0) return "";
            var start = Position.FromFen(game.StartFen);
            bool blackFirst = start.SideToMove == PieceColor.Black;
            int first = Mathf.Max(0, san.Count - maxPlies);
            var sb = new StringBuilder();
            for (int i = first; i < san.Count; i++)
            {
                int plyIndex = i + (blackFirst ? 1 : 0);
                int number = start.FullmoveNumber + plyIndex / 2;
                bool whiteMove = plyIndex % 2 == 0;
                if (whiteMove) sb.Append(number).Append(". ");
                else if (i == first) sb.Append(number).Append("… ");
                sb.Append(san[i]).Append("  ");
            }
            return sb.ToString().TrimEnd();
        }

        // ---------------------------------------------------------------- Menus

        GameObject Overlay(string name, out RectTransform card, float width, float height, Color accent)
        {
            var root = UIKit.Rect(name, transform);
            UIKit.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.01f, 0.04f, 0.62f);
            root.gameObject.AddComponent<CanvasGroup>();

            var panel = UIKit.Panel("Card", root, UIKit.GlassStrong, true, accent);
            UIKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            card = panel.rectTransform;
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        void BuildMenu()
        {
            menuOverlay = Overlay("Menu", out var card, 900, 1480, UIKit.Cyan);
            UIKit.VStack(card, 26, 56);

            var title = UIKit.Label("Title", card, "KEVCHESS", 110, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(title, 150);
            var sh = title.gameObject.AddComponent<Outline>();
            sh.effectColor = UIKit.Magenta * new Color(1, 1, 1, 0.6f);
            sh.effectDistance = new Vector2(4, -4);
            var sub = UIKit.Label("Subtitle", card, "Échecs en trois dimensions", 36, UIKit.TextDim);
            UIKit.Size(sub, 60);

            continueButton = UIKit.Button("Continue", card, "Continuer", () => { sfx.Play(Sfx.Click); ShowMenu(false); });
            UIKit.Size(continueButton, 120);
            resumeButton = UIKit.Button("Resume", card, "Reprendre la partie", () =>
            {
                sfx.Play(Sfx.Click);
                if (match.ResumeSaved()) ShowMenu(false);
            });
            UIKit.Size(resumeButton, 120);
            var local = UIKit.Button("Local", card, "Partie à deux", () =>
            {
                sfx.Play(Sfx.Click);
                match.StartNew(new MatchSettings { Mode = MatchMode.LocalTwoPlayers });
                ShowMenu(false);
            });
            UIKit.Size(local, 120);
            var vsAI = UIKit.Button("VsAI", card, "Contre l'IA", () => { sfx.Play(Sfx.Click); Show(menuOverlay, false); Show(aiOverlay, true); }, UIKit.Magenta);
            UIKit.Size(vsAI, 120);
            var legends = UIKit.Button("Legends", card, "Légendes", () =>
            {
                sfx.Play(Sfx.Click);
                Show(menuOverlay, false);
                LegendsRequested?.Invoke();
            }, new Color(1f, 0.82f, 0.35f));
            UIKit.Size(legends, 120);
            soundButton = UIKit.Button("Sound", card, "", () =>
            {
                sfx.Muted = !sfx.Muted;
                sfx.Play(Sfx.Click);
                UpdateSoundLabel();
            });
            UIKit.Size(soundButton, 100);
            hapticsButton = UIKit.Button("Haptics", card, "", () =>
            {
                Haptics.Enabled = !Haptics.Enabled;
                sfx.Play(Sfx.Click);
                Haptics.Select();
                UpdateSoundLabel();
            });
            UIKit.Size(hapticsButton, 100);
            UpdateSoundLabel();

            if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer)
            {
                var quit = UIKit.Button("Quit", card, "Quitter", Application.Quit, UIKit.TextDim);
                UIKit.Size(quit, 100);
            }
        }

        void UpdateSoundLabel()
        {
            UIKit.SetButtonText(soundButton, sfx.Muted ? "Son : coupé" : "Son : activé");
            UIKit.SetButtonText(hapticsButton, Haptics.Enabled ? "Vibrations : activées" : "Vibrations : coupées");
        }

        void BuildAISetup()
        {
            aiOverlay = Overlay("AISetup", out var card, 940, 1300, UIKit.Magenta);
            UIKit.VStack(card, 24, 56);

            UIKit.Size(UIKit.Label("Title", card, "Contre l'IA", 72, UIKit.Magenta, TextAnchor.MiddleCenter, FontStyle.Bold), 110);
            UIKit.Size(UIKit.Label("ColorLabel", card, "Votre couleur", 36, UIKit.TextDim), 56);

            var colors = UIKit.Rect("Colors", card);
            UIKit.HStack(colors, 16);
            UIKit.Size(colors.gameObject.AddComponent<LayoutElement>(), 120);
            string[] colorNames = { "Blancs", "Noirs", "Hasard" };
            colorButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                colorButtons[i] = UIKit.Button("Color" + i, colors, colorNames[i], () => { sfx.Play(Sfx.Click); aiColorChoice = idx; RefreshAISetup(); });
            }

            UIKit.Size(UIKit.Label("LevelLabel", card, "Niveau", 36, UIKit.TextDim), 56);
            var levels = UIKit.Rect("Levels", card);
            UIKit.VStack(levels, 12);
            UIKit.Size(levels.gameObject.AddComponent<LayoutElement>(), 5 * 92 + 4 * 12);
            levelButtons = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                var level = (AILevel)i;
                levelButtons[i] = UIKit.Button("Level" + i, levels, MatchSettings.LevelName(level), () => { sfx.Play(Sfx.Click); aiLevel = level; RefreshAISetup(); }, null, 36);
                UIKit.Size(levelButtons[i], 92);
            }

            var row = UIKit.Rect("Actions", card);
            UIKit.HStack(row, 16);
            UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), 120);
            UIKit.Button("Back", row, "Retour", () => { sfx.Play(Sfx.Click); Show(aiOverlay, false); ShowMenu(true); }, UIKit.TextDim);
            UIKit.Button("Play", row, "Jouer", StartAIMatch, UIKit.Magenta);
            RefreshAISetup();
        }

        void RefreshAISetup()
        {
            for (int i = 0; i < colorButtons.Length; i++) Highlight(colorButtons[i], i == aiColorChoice, UIKit.Cyan);
            for (int i = 0; i < levelButtons.Length; i++) Highlight(levelButtons[i], i == (int)aiLevel, UIKit.Magenta);
        }

        static void Highlight(Button b, bool on, Color accent)
        {
            var img = (Image)b.targetGraphic;
            img.color = on ? accent * new Color(0.55f, 0.55f, 0.55f, 1f) : new Color(0.08f, 0.10f, 0.20f, 0.85f);
        }

        void StartAIMatch()
        {
            sfx.Play(Sfx.Click);
            var color = aiColorChoice == 2
                ? (UnityEngine.Random.value < 0.5f ? PieceColor.White : PieceColor.Black)
                : (PieceColor)aiColorChoice;
            match.StartNew(new MatchSettings { Mode = MatchMode.VersusAI, HumanColor = color, Level = aiLevel });
            Show(aiOverlay, false);
            ShowMenu(false);
        }

        public void ShowMenu(bool show)
        {
            if (show)
            {
                bool inGame = match.Game != null && !match.Game.IsOver;
                continueButton.gameObject.SetActive(inGame);
                resumeButton.gameObject.SetActive(!inGame && MatchController.HasSavedMatch());
                UpdateSoundLabel();
            }
            Show(menuOverlay, show);
        }

        // ---------------------------------------------------------------- Promotion

        void BuildPromotion()
        {
            promoOverlay = Overlay("Promotion", out var card, 960, 440, UIKit.Cyan);
            var dimButton = promoOverlay.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(CancelPromotion);
            // La carte absorbe ses propres clics : sinon ils remonteraient jusqu'au voile et annuleraient.
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            UIKit.VStack(card, 20, 40);
            UIKit.Size(UIKit.Label("Title", card, "Promotion", 56, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold), 80);
            var row = UIKit.Rect("Choices", card);
            UIKit.HStack(row, 16);
            UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), 180);
            AddPromo(row, "Dame", PieceType.Queen);
            AddPromo(row, "Tour", PieceType.Rook);
            AddPromo(row, "Fou", PieceType.Bishop);
            AddPromo(row, "Cavalier", PieceType.Knight);
        }

        void AddPromo(RectTransform row, string label, PieceType type)
        {
            UIKit.Button("Promo" + type, row, label, () =>
            {
                sfx.Play(Sfx.Click);
                Show(promoOverlay, false);
                var cb = promotionCallback;
                promotionCallback = null;
                cb?.Invoke(type);
            }, null, 40);
        }

        /// <summary>Demande la pièce de promotion. Toucher en dehors de la carte annule (callback avec None).</summary>
        public void AskPromotion(Action<PieceType> onChosen)
        {
            promotionCallback = onChosen;
            Show(promoOverlay, true);
        }

        public void CancelPromotion()
        {
            if (!promoOverlay.activeSelf) return;
            Show(promoOverlay, false);
            var cb = promotionCallback;
            promotionCallback = null;
            cb?.Invoke(PieceType.None);
        }

        // ---------------------------------------------------------------- Fin de partie

        void BuildEndScreen()
        {
            endOverlay = Overlay("GameOver", out var card, 900, 800, UIKit.Cyan);
            UIKit.VStack(card, 22, 50);
            endTitle = UIKit.Label("Title", card, "", 84, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(endTitle, 120);
            endReason = UIKit.Label("Reason", card, "", 40, UIKit.TextDim);
            UIKit.Size(endReason, 70);
            UIKit.Size(UIKit.Button("Replay", card, "Rejouer", () =>
            {
                sfx.Play(Sfx.Click);
                var s = match.Settings;
                // Contre l'IA, on change de couleur à chaque revanche.
                var next = new MatchSettings { Mode = s.Mode, Level = s.Level, HumanColor = s.Mode == MatchMode.VersusAI ? Piece.Opposite(s.HumanColor) : s.HumanColor };
                match.StartNew(next);
                Show(endOverlay, false);
            }), 120);
            UIKit.Size(UIKit.Button("Review", card, "Voir le plateau", () => { sfx.Play(Sfx.Click); Show(endOverlay, false); }, UIKit.TextDim), 110);
            UIKit.Size(UIKit.Button("ToMenu", card, "Menu", () => { sfx.Play(Sfx.Click); Show(endOverlay, false); ShowMenu(true); }, UIKit.Magenta), 110);
        }

        void OnGameEnded()
        {
            var game = match.Game;
            var s = match.Settings;
            endTitle.text = ResultTitle(game, s);
            endReason.text = ReasonText(game.EndReason);

            bool humanWon = s.Mode == MatchMode.VersusAI &&
                            ((game.Result == GameResult.WhiteWins && s.HumanColor == PieceColor.White) ||
                             (game.Result == GameResult.BlackWins && s.HumanColor == PieceColor.Black));
            bool humanLost = s.Mode == MatchMode.VersusAI && game.Result != GameResult.Draw && !humanWon;
            endTitle.color = humanLost ? UIKit.Magenta : UIKit.Cyan;
            sfx.Play(game.Result == GameResult.Draw ? Sfx.Draw : humanLost ? Sfx.Defeat : Sfx.Victory);
            StartCoroutine(ShowDelayed(endOverlay, 0.9f));
        }

        static string ResultTitle(ChessGame game, MatchSettings s)
        {
            if (game.Result == GameResult.Draw) return "Partie nulle";
            var winner = game.Result == GameResult.WhiteWins ? PieceColor.White : PieceColor.Black;
            if (s.Mode == MatchMode.VersusAI) return winner == s.HumanColor ? "Victoire !" : "Défaite";
            return winner == PieceColor.White ? "Les Blancs gagnent" : "Les Noirs gagnent";
        }

        static string ReasonText(GameEndReason reason)
        {
            switch (reason)
            {
                case GameEndReason.Checkmate: return "Échec et mat";
                case GameEndReason.Stalemate: return "Pat";
                case GameEndReason.FiftyMoveRule: return "Règle des 50 coups";
                case GameEndReason.ThreefoldRepetition: return "Triple répétition";
                case GameEndReason.InsufficientMaterial: return "Matériel insuffisant";
                case GameEndReason.Resignation: return "Abandon";
                case GameEndReason.DrawAgreement: return "Nulle par accord";
                default: return "";
            }
        }

        void OnNewMatch()
        {
            localViewSide = match.Settings.Mode == MatchMode.LocalTwoPlayers ? match.Game.SideToMove : match.ViewSide;
            orbit.ResetView(localViewSide);
            Show(endOverlay, false);
            Show(promoOverlay, false);
            promotionCallback = null;
        }

        // ---------------------------------------------------------------- Confirmation

        void BuildConfirm()
        {
            confirmOverlay = Overlay("Confirm", out var card, 860, 440, UIKit.Magenta);
            UIKit.VStack(card, 24, 50);
            confirmText = UIKit.Label("Question", card, "", 46, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(confirmText, 130);
            var row = UIKit.Rect("Buttons", card);
            UIKit.HStack(row, 20);
            UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), 120);
            UIKit.Button("No", row, "Non", () => { sfx.Play(Sfx.Click); Show(confirmOverlay, false); }, UIKit.TextDim);
            UIKit.Button("Yes", row, "Oui", () =>
            {
                sfx.Play(Sfx.Click);
                Show(confirmOverlay, false);
                confirmAction?.Invoke();
            }, UIKit.Magenta);
        }

        void Confirm(string question, Action onYes)
        {
            confirmText.text = question;
            confirmAction = onYes;
            Show(confirmOverlay, true);
        }

        // ---------------------------------------------------------------- Outils

        void Show(GameObject overlay, bool show)
        {
            if (show == overlay.activeSelf) return;
            if (!show) { overlay.SetActive(false); return; }
            overlay.transform.SetAsLastSibling();
            overlay.SetActive(true);
            StartCoroutine(FadeIn(overlay));
        }

        IEnumerator ShowDelayed(GameObject overlay, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            if (match.Game != null && match.Game.IsOver) Show(overlay, true);
        }

        static IEnumerator FadeIn(GameObject overlay)
        {
            var group = overlay.GetComponent<CanvasGroup>();
            var card = overlay.transform.Find("Card");
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.18f)
            {
                float e = 1f - (1f - t) * (1f - t);
                group.alpha = e;
                if (card != null) card.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, e);
                yield return null;
            }
            group.alpha = 1f;
            if (card != null) card.localScale = Vector3.one;
        }

        void Update()
        {
            UpdateLayout();
            // L'IA ne joue pas derrière le menu ou l'écran de réglages.
            if (match != null) match.Paused = menuOverlay.activeSelf || aiOverlay.activeSelf;
        }

        /// <summary>Bouton retour Android / Échap : ferme l'écran du dessus, sinon ouvre le menu.</summary>
        public void HandleBack()
        {
            if (promoOverlay.activeSelf) { CancelPromotion(); return; }
            if (confirmOverlay.activeSelf) { Show(confirmOverlay, false); return; }
            if (aiOverlay.activeSelf) { Show(aiOverlay, false); ShowMenu(true); return; }
            if (endOverlay.activeSelf) { Show(endOverlay, false); return; }
            if (menuOverlay.activeSelf)
            {
                if (match.Game != null && !match.Game.IsOver) ShowMenu(false);
                return;
            }
            ShowMenu(true);
        }

        void UpdateLayout()
        {
            // Portrait : on cale la largeur ; paysage : la hauteur.
            scaler.matchWidthOrHeight = Screen.width > Screen.height ? 1f : 0f;

            var safe = Screen.safeArea;
            if (safe == appliedSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
            appliedSafeArea = safe;
            safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }
    }
}
