using System;
using System.Collections;
using System.Text;
using KevChess.Core;
using KevChess.Core.Ux;
using KevChess.Game.Audio;
using KevChess.Game.CameraRig;
using KevChess.Game.Match;
using KevChess.Game.View;
using KevChess.Game.Visuals;
using UnityEngine;
using UnityEngine.UI;

namespace KevChess.Game.UI
{
    /// <summary>
    /// Interface complète construite par code : bandeau des joueurs (pendules), statut, coups, barre d'actions,
    /// menu principal, réglages IA, choix de promotion, fin de partie, confirmation.
    /// Les autres écrans sont dans Hud.Screens.cs (réglages, puzzles, partie à deux, actions)
    /// et Hud.Review.cs (revue et analyse).
    /// </summary>
    public sealed partial class Hud : MonoBehaviour
    {
        const float MateAnimationSeconds = 2.1f;
        const float ClockAlertSeconds = 10f;

        MatchController match;
        OrbitCamera orbit;
        SoundBank sfx;
        BoardView board;
        Action<Move> showHint;

        Canvas canvas;
        CanvasScaler scaler;
        RectTransform safeRoot;
        Rect appliedSafeArea;

        Text whiteName, blackName, whiteScore, blackScore, whiteClock, blackClock, status, movesLine, openingLine;
        Image whiteTurn, blackTurn;
        Button undoButton, hintButton, viewButton, moreButton, listButton, nextButton;
        GameObject hudGroup, bottomBar;

        GameObject menuOverlay, aiOverlay, promoOverlay, endOverlay, confirmOverlay;
        Button continueButton, resumeButton;
        Text endTitle, endReason, endSummary;
        RectTransform endSparks;
        float playSeconds;
        bool playTimeKnown;
        Button endAnalyzeButton;
        Action confirmAction;
        Text confirmText;
        Action<PieceType> promotionCallback;

        // Réglages en cours d'édition dans l'écran IA.
        int aiColorChoice; // 0 blancs, 1 noirs, 2 hasard
        AILevel aiLevel = AILevel.Medium;
        int aiClockChoice;
        Button[] colorButtons, levelButtons, aiClockButtons;
        Text aiStatsLabel;

        PieceColor localViewSide = PieceColor.White;
        int lastTickSecond = -1;
        string cachedOpening;
        int cachedOpeningPly = -1;
        ChessGame cachedOpeningGame;

        /// <summary>Bouton « Légendes » du menu : l'app bascule vers le mode fantastique.</summary>
        public event Action LegendsRequested;

        public bool IsBlockingOverlayOpen =>
            menuOverlay.activeSelf || aiOverlay.activeSelf || promoOverlay.activeSelf || confirmOverlay.activeSelf || endOverlay.activeSelf ||
            IsScreenOverlayOpen;

        /// <summary>Dialogue de confirmation affiché (recette automatique).</summary>
        public bool IsConfirmOpen => confirmOverlay.activeSelf;

        /// <summary>Écran de fin de partie affiché (recette automatique).</summary>
        public bool IsEndScreenOpen => endOverlay.activeSelf;

        public void Build(MatchController matchController, OrbitCamera camera, SoundBank sounds, BoardView boardView, Action<Move> onHint)
        {
            match = matchController;
            orbit = camera;
            sfx = sounds;
            board = boardView;
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
            BuildReviewBar();
            BuildTips();
            BuildToast();
            BuildMenu();
            BuildAISetup();
            BuildLocalSetup();
            BuildSettings();
            BuildPuzzleList();
            BuildActions();
            BuildAnalysis();
            BuildPromotion();
            BuildEndScreen();
            BuildConfirm();

            match.StateChanged += Refresh;
            match.GameEnded += OnGameEnded;
            match.NewMatchStarted += OnNewMatch;
            match.MoveLanded += OnMoveLanded;
            match.PuzzleFeedback += OnPuzzleFeedback;
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
            BuildPlayerCard(top, PieceColor.White, out whiteName, out whiteScore, out whiteTurn, out whiteClock);
            BuildPlayerCard(top, PieceColor.Black, out blackName, out blackScore, out blackTurn, out blackClock);

            status = UIKit.Label("Status", hudGroup.transform, "", 46, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -190), new Vector2(1000, 64));
            status.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);

            movesLine = UIKit.Label("Moves", hudGroup.transform, "", 30, UIKit.TextDim);
            UIKit.Anchor(movesLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -246), new Vector2(1000, 44));
            movesLine.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Nom de l'ouverture (ou titre du puzzle, note de revue), discret sous les coups.
            openingLine = UIKit.Label("Opening", hudGroup.transform, "", 27, UIKit.TextDim * new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter, FontStyle.Italic);
            UIKit.Anchor(openingLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -284), new Vector2(1000, 36));
            openingLine.horizontalOverflow = HorizontalWrapMode.Wrap;
            openingLine.resizeTextForBestFit = true;
            openingLine.resizeTextMinSize = 18;
            openingLine.resizeTextMaxSize = 27;

            // Barre d'actions : cinq boutons à pictogramme, en partie comme en puzzle.
            var bar = UIKit.Panel("BottomBar", hudGroup.transform, UIKit.Glass, true, UIKit.Cyan);
            bottomBar = bar.gameObject;
            UIKit.Anchor(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(1040, 140));
            UIKit.HStack(bar.rectTransform, 14, 14);
            UIKit.IconButton("Menu", bar.transform, IconKind.Menu, "Menu", () => { sfx.Play(Sfx.Click); ShowMenu(true); });
            undoButton = UIKit.IconButton("Undo", bar.transform, IconKind.Undo, "Annuler", () => { sfx.Play(Sfx.Click); match.UndoForHuman(); });
            listButton = UIKit.IconButton("List", bar.transform, IconKind.List, "Liste", () => { sfx.Play(Sfx.Click); OpenPuzzleList(); });
            hintButton = UIKit.IconButton("Hint", bar.transform, IconKind.Hint, "Indice", OnHintPressed);
            viewButton = UIKit.IconButton("View", bar.transform, IconKind.View, "Vue", OnViewPressed);
            moreButton = UIKit.IconButton("More", bar.transform, IconKind.More, "Plus…", () => { sfx.Play(Sfx.Click); OpenActions(); }, UIKit.Magenta);
            nextButton = UIKit.IconButton("Next", bar.transform, IconKind.Next, "Suivant", () => { sfx.Play(Sfx.Click); NextPuzzle(); }, UIKit.Magenta);
        }

        void BuildPlayerCard(RectTransform parent, PieceColor color, out Text name, out Text score, out Image turn, out Text clock)
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
            // Hauteur limitée : le nom reste sur une ligne (police réduite) plutôt que de passer à la ligne.
            UIKit.Stretch(name.rectTransform, 92, 66, 20, 14);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 22;
            name.resizeTextMaxSize = 38;
            score = UIKit.Label("Score", card.transform, "", 30, accent, TextAnchor.MiddleLeft);
            UIKit.Stretch(score.rectTransform, 92, 12, 20, 70);

            // Temps restant, à droite de la carte (masqué sans limite).
            clock = UIKit.Label("Clock", card.transform, "", 50, UIKit.Text, TextAnchor.MiddleRight, FontStyle.Bold);
            UIKit.Anchor(clock.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22, 0), new Vector2(190, 90));
            clock.horizontalOverflow = HorizontalWrapMode.Overflow;
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

        void OnHintPressed()
        {
            sfx.Play(Sfx.Click);
            if (match.IsPuzzle)
            {
                if (!match.CanHumanAct) return;
                var expected = match.Puzzle.ExpectedMove;
                if (!expected.IsNull) showHint(expected);
                return;
            }
            match.RequestHint(showHint);
        }

        /// <summary>Vue par défaut (double-tap) : camp du joueur, ou camp au trait en local.</summary>
        public void ResetCamera()
        {
            if (match.Game == null) return;
            if (match.Settings.Mode == MatchMode.LocalTwoPlayers) localViewSide = match.Game.SideToMove;
            orbit.ResetView(match.Settings.Mode == MatchMode.LocalTwoPlayers ? localViewSide : match.ViewSide);
            NotifyTipGesture(BoardGesture.CameraRecentered);
        }

        void OnMoveLanded(MoveVisual v)
        {
            // Bulle d'accueil « jouer un coup » : seulement pour un coup d'un joueur humain.
            if (match.Game.Moves.Count > 0 && match.Settings.IsHuman(Piece.Opposite(match.Game.SideToMove)))
                NotifyTipGesture(BoardGesture.MovePlayed);
            // Partie à deux : la caméra passe du côté du camp au trait.
            if (match.Settings.Mode != MatchMode.LocalTwoPlayers || !GamePrefs.AutoFlip || match.Game.IsOver) return;
            localViewSide = match.Game.SideToMove;
            orbit.ResetView(localViewSide);
        }

        void Refresh()
        {
            var game = match.Game;
            if (game == null) return;
            var s = match.Settings;
            bool puzzle = match.IsPuzzle;

            whiteName.text = PlayerName(PieceColor.White, s);
            blackName.text = PlayerName(PieceColor.Black, s);

            int balance = MaterialBalance(reviewing && reviewGame != null ? reviewGame.Position : game.Position);
            whiteScore.text = balance > 0 ? "+" + balance : "";
            blackScore.text = balance < 0 ? "+" + (-balance) : "";

            bool whiteToMove = game.SideToMove == PieceColor.White && !game.IsOver;
            bool blackToMove = game.SideToMove == PieceColor.Black && !game.IsOver;
            whiteTurn.enabled = whiteToMove;
            blackTurn.enabled = blackToMove;

            bool timed = !match.Clock.IsUnlimited;
            whiteClock.gameObject.SetActive(timed);
            blackClock.gameObject.SetActive(timed);
            float nameRight = timed ? 205 : 20;
            whiteName.rectTransform.offsetMax = new Vector2(-nameRight, -14);
            blackName.rectTransform.offsetMax = new Vector2(-nameRight, -14);
            whiteName.alignment = blackName.alignment = timed ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            UpdateClocks();

            // Barre d'actions : en puzzle, Liste et Suivant remplacent Annuler et Plus.
            undoButton.gameObject.SetActive(!puzzle);
            moreButton.gameObject.SetActive(!puzzle);
            listButton.gameObject.SetActive(puzzle);
            nextButton.gameObject.SetActive(puzzle);
            bottomBar.SetActive(!reviewing);
            undoButton.interactable = game.Moves.Count > 0 && !match.IsAnimating;
            hintButton.interactable = match.CanHumanAct;
            moreButton.interactable = !match.IsAnimating;
            nextButton.interactable = !match.IsAnimating;
            // Bouton principal du contexte mis en avant : Suivant (puzzle résolu), Indice (puzzle en cours),
            // Plus… (partie finie : revoir, analyser).
            Highlight(nextButton, puzzle && match.Puzzle.IsSolved, UIKit.Magenta);
            Highlight(hintButton, puzzle && !match.Puzzle.IsSolved, UIKit.Cyan);
            Highlight(moreButton, !puzzle && game.IsOver, UIKit.Magenta);

            if (reviewing)
            {
                RefreshReview();
                return;
            }

            status.color = UIKit.Text;
            if (puzzle)
            {
                var session = match.Puzzle;
                if (session.IsSolved)
                {
                    status.text = "Résolu !";
                    status.color = UIKit.Cyan;
                }
                else status.text = "Mat en " + session.Puzzle.MateIn + " · " + (session.PlayerColor == PieceColor.White ? "les Blancs jouent" : "les Noirs jouent");
            }
            else if (game.IsOver) status.text = ResultTitle(game, s);
            else if (match.IsAIThinking) status.text = ThinkingText();
            else if (game.InCheck)
            {
                status.text = "Échec !";
                status.color = UIKit.Danger;
            }
            else if (s.Mode == MatchMode.VersusAI) status.text = s.IsHuman(game.SideToMove) ? "À vous de jouer" : "";
            else status.text = game.SideToMove == PieceColor.White ? "Aux Blancs de jouer" : "Aux Noirs de jouer";

            movesLine.text = RecentMoves(game, game.Moves.Count, 8);
            openingLine.text = puzzle ? PuzzleCaption(match.Puzzle.Puzzle) : Opening(game);
        }

        string Opening(ChessGame game)
        {
            // Recherche refaite seulement quand la partie change.
            if (game != cachedOpeningGame || game.Moves.Count != cachedOpeningPly)
            {
                cachedOpeningGame = game;
                cachedOpeningPly = game.Moves.Count;
                cachedOpening = OpeningNames.Lookup(game.Moves, game.StartFen) ?? "";
            }
            return cachedOpening;
        }

        static string ThinkingText()
        {
            // Points animés ; les points invisibles gardent le texte immobile.
            int dots = (int)(Time.unscaledTime / 0.35f) % 4;
            return "L'IA réfléchit" + new string('.', dots) + "<color=#00000000>" + new string('.', 3 - dots) + "</color>";
        }

        void UpdateClocks()
        {
            var clock = match.Clock;
            if (clock == null || clock.IsUnlimited || match.Game == null) return;
            SetClock(whiteClock, clock, PieceColor.White);
            SetClock(blackClock, clock, PieceColor.Black);
        }

        void SetClock(Text label, ChessClock clock, PieceColor color)
        {
            double t = Math.Max(0, clock.Remaining(color));
            label.text = FormatClock(t);
            bool active = clock.Turn == color && !match.Game.IsOver;
            if (t < ClockAlertSeconds)
            {
                // Alerte : rouge, qui pulse quand la pendule tourne.
                float pulse = clock.Running == color ? 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f)) : 1f;
                label.color = UIKit.Danger * new Color(1, 1, 1, pulse);
            }
            else label.color = active ? UIKit.Text : UIKit.TextDim;
        }

        public static string FormatClock(double seconds)
        {
            if (seconds < ClockAlertSeconds)
            {
                int tenths = (int)(seconds * 10.0);
                return tenths / 10 + "," + tenths % 10; // dixièmes, virgule à la française
            }
            int s = (int)Math.Ceiling(seconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        /// <summary>Nom du joueur, sans coupure de ligne (espaces insécables) : la police réduit plutôt.</summary>
        static string PlayerName(PieceColor color, MatchSettings s) => PlayerNameText(color, s).Replace(' ', '\u00A0');

        static string PlayerNameText(PieceColor color, MatchSettings s)
        {
            string side = color == PieceColor.White ? "Blancs" : "Noirs";
            if (s.Mode == MatchMode.LocalTwoPlayers) return side;
            if (s.Mode == MatchMode.Puzzle) return s.HumanColor == color ? "Vous · " + side : side;
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

        /// <summary>Derniers coups jusqu'au demi-coup <paramref name="upTo"/> (exclu), dans la notation choisie.</summary>
        static string RecentMoves(ChessGame game, int upTo, int maxPlies)
        {
            var san = game.SanMoves;
            upTo = Mathf.Min(upTo, san.Count);
            if (upTo == 0) return "";
            int first = Mathf.Max(0, upTo - maxPlies);
            var sb = new StringBuilder();
            for (int i = first; i < upTo; i++)
            {
                MoveNumber(game.StartFen, i, out int number, out bool whiteMove);
                if (whiteMove) sb.Append(number).Append(". ");
                else if (i == first) sb.Append(number).Append("… ");
                sb.Append(GamePrefs.San(san[i])).Append("  ");
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>Numéro du coup et camp du demi-coup <paramref name="ply"/> (0 = premier coup joué).</summary>
        static void MoveNumber(string startFen, int ply, out int number, out bool whiteMove)
        {
            var start = Position.FromFen(startFen);
            int plyIndex = ply + (start.SideToMove == PieceColor.Black ? 1 : 0);
            number = start.FullmoveNumber + plyIndex / 2;
            whiteMove = plyIndex % 2 == 0;
        }

        /// <summary>« 12. Cf3 » ou « 12… Cf3 ».</summary>
        static string MoveLabel(string startFen, int ply, string san)
        {
            MoveNumber(startFen, ply, out int number, out bool whiteMove);
            return number + (whiteMove ? ". " : "… ") + GamePrefs.San(san);
        }

        // ---------------------------------------------------------------- Menus

        GameObject Overlay(string name, out RectTransform card, float width, float height, Color accent)
        {
            var root = UIKit.Rect(name, transform);
            UIKit.Stretch(root);
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.01f, 0.04f, 0.62f);
            root.gameObject.AddComponent<CanvasGroup>();

            // Carte presque opaque : le bandeau du jeu ne transparaît pas derrière.
            var panel = UIKit.Panel("Card", root, new Color(0.03f, 0.03f, 0.07f, 0.97f), true, accent);
            UIKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            card = panel.rectTransform;
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        /// <summary>Carte dont la hauteur suit son contenu (boutons masqués selon le contexte).</summary>
        static void FitHeight(RectTransform card) =>
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        void BuildMenu()
        {
            menuOverlay = Overlay("Menu", out var card, 900, 1340, UIKit.Cyan);
            UIKit.VStack(card, 24, 56);
            FitHeight(card);

            var title = UIKit.Label("Title", card, "KEVCHESS", 110, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(title, 150);
            var sh = title.gameObject.AddComponent<Outline>();
            sh.effectColor = UIKit.Magenta * new Color(1, 1, 1, 0.6f);
            sh.effectDistance = new Vector2(4, -4);
            var sub = UIKit.Label("Subtitle", card, "Échecs en trois dimensions", 36, UIKit.TextDim);
            UIKit.Size(sub, 60);

            continueButton = UIKit.Button("Continue", card, "Continuer", () => { sfx.Play(Sfx.Click); ShowMenu(false); });
            UIKit.Size(continueButton, 116);
            resumeButton = UIKit.Button("Resume", card, "Reprendre la partie", () =>
            {
                sfx.Play(Sfx.Click);
                if (match.ResumeSaved()) ShowMenu(false);
            });
            UIKit.Size(resumeButton, 116);
            var local = UIKit.Button("Local", card, "Partie à deux", () => { sfx.Play(Sfx.Click); OpenLocalSetup(); });
            UIKit.Size(local, 116);
            var vsAI = UIKit.Button("VsAI", card, "Contre l'IA", () => { sfx.Play(Sfx.Click); OpenAISetup(); }, UIKit.Magenta);
            UIKit.Size(vsAI, 116);
            var legends = UIKit.Button("Legends", card, "Légendes", () =>
            {
                sfx.Play(Sfx.Click);
                Show(menuOverlay, false);
                LegendsRequested?.Invoke();
            }, new Color(1f, 0.82f, 0.35f));
            UIKit.Size(legends, 116);
            var puzzles = UIKit.Button("Puzzles", card, "Puzzles", () => { sfx.Play(Sfx.Click); OpenPuzzleList(); }, UIKit.Magenta);
            UIKit.Size(puzzles, 116);
            var settings = UIKit.Button("Settings", card, "Réglages", () => { sfx.Play(Sfx.Click); OpenSettings(); }, UIKit.TextDim);
            UIKit.Size(settings, 100);

            if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer)
            {
                var quit = UIKit.Button("Quit", card, "Quitter", Application.Quit, UIKit.TextDim);
                UIKit.Size(quit, 100);
            }
        }

        void BuildAISetup()
        {
            aiOverlay = Overlay("AISetup", out var card, 940, 1300, UIKit.Magenta);
            UIKit.VStack(card, 16, 48);
            FitHeight(card);

            UIKit.Size(UIKit.Label("Title", card, "Contre l'IA", 72, UIKit.Magenta, TextAnchor.MiddleCenter, FontStyle.Bold), 100);
            UIKit.Size(UIKit.Label("ColorLabel", card, "Votre couleur", 34, UIKit.TextDim), 48);

            var colors = UIKit.Rect("Colors", card);
            UIKit.HStack(colors, 16);
            UIKit.Size(colors.gameObject.AddComponent<LayoutElement>(), 104);
            string[] colorNames = { "Blancs", "Noirs", "Hasard" };
            colorButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                colorButtons[i] = UIKit.Button("Color" + i, colors, colorNames[i], () => { sfx.Play(Sfx.Click); aiColorChoice = idx; RefreshAISetup(); });
            }

            UIKit.Size(UIKit.Label("LevelLabel", card, "Niveau", 34, UIKit.TextDim), 48);
            var levels = UIKit.Rect("Levels", card);
            UIKit.VStack(levels, 10);
            UIKit.Size(levels.gameObject.AddComponent<LayoutElement>(), 5 * 84 + 4 * 10);
            levelButtons = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                var level = (AILevel)i;
                levelButtons[i] = UIKit.Button("Level" + i, levels, MatchSettings.LevelName(level), () => { sfx.Play(Sfx.Click); aiLevel = level; RefreshAISetup(); }, null, 36);
                UIKit.Size(levelButtons[i], 84);
            }
            // Bilan contre le niveau choisi.
            aiStatsLabel = UIKit.Label("Stats", card, "", 30, UIKit.Cyan);
            UIKit.Size(aiStatsLabel, 44);

            UIKit.Size(UIKit.Label("ClockLabel", card, "Cadence", 34, UIKit.TextDim), 48);
            aiClockButtons = BuildClockChoices(card, i => { aiClockChoice = i; RefreshAISetup(); });

            var row = UIKit.Rect("Actions", card);
            UIKit.HStack(row, 16);
            UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), 116);
            UIKit.Button("Back", row, "Retour", () => { sfx.Play(Sfx.Click); Show(aiOverlay, false); ShowMenu(true); }, UIKit.TextDim);
            UIKit.Button("Play", row, "Jouer", StartAIMatch, UIKit.Magenta);
            RefreshAISetup();
        }

        /// <summary>Deux rangées de trois cadences (TimeControl.Presets).</summary>
        Button[] BuildClockChoices(RectTransform card, Action<int> onPick)
        {
            var presets = TimeControl.Presets;
            var buttons = new Button[presets.Length];
            var grid = UIKit.Rect("Clocks", card);
            UIKit.VStack(grid, 12);
            UIKit.Size(grid.gameObject.AddComponent<LayoutElement>(), 2 * 86 + 12);
            RectTransform row = null;
            for (int i = 0; i < presets.Length; i++)
            {
                if (i % 3 == 0)
                {
                    row = UIKit.Rect("Row" + i / 3, grid);
                    UIKit.HStack(row, 14);
                    UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), 86);
                }
                int idx = i;
                buttons[i] = UIKit.Button("Clock" + i, row, presets[i].Label, () => { sfx.Play(Sfx.Click); onPick(idx); }, null, 34);
            }
            return buttons;
        }

        void OpenAISetup()
        {
            aiClockChoice = GamePrefs.ClockPreset(true);
            RefreshAISetup();
            Show(menuOverlay, false);
            Show(aiOverlay, true);
        }

        void RefreshAISetup()
        {
            for (int i = 0; i < colorButtons.Length; i++) Highlight(colorButtons[i], i == aiColorChoice, UIKit.Cyan);
            for (int i = 0; i < levelButtons.Length; i++) Highlight(levelButtons[i], i == (int)aiLevel, UIKit.Magenta);
            for (int i = 0; i < aiClockButtons.Length; i++) Highlight(aiClockButtons[i], i == aiClockChoice, UIKit.Cyan);
            var r = GamePrefs.Stats(aiLevel);
            aiStatsLabel.text = r.Wins + r.Draws + r.Losses == 0
                ? "Aucune partie contre ce niveau"
                : "Bilan : " + Plural(r.Wins, "victoire") + " · " + Plural(r.Draws, "nulle") + " · " + Plural(r.Losses, "défaite");
        }

        static string Plural(int n, string word) => n + " " + word + (n > 1 ? "s" : "");

        static void Highlight(Button b, bool on, Color accent)
        {
            var img = (Image)b.targetGraphic;
            img.color = on ? accent * new Color(0.55f, 0.55f, 0.55f, 1f) : new Color(0.08f, 0.10f, 0.20f, 0.85f);
        }

        void StartAIMatch()
        {
            sfx.Play(Sfx.Click);
            ConfirmReplace(StartAIMatchNow);
        }

        /// <summary>
        /// Lance <paramref name="start"/> tout de suite, ou après confirmation si une partie non terminée serait perdue :
        /// la partie en cours, ou, pendant un puzzle, la partie sauvegardée qu'une nouvelle partie écraserait.
        /// </summary>
        void ConfirmReplace(Action start)
        {
            string question = match.IsPuzzle
                ? ReplaceGuard.Question(SavedMatch.Load()?.Rebuild(), true)
                : ReplaceGuard.Question(match.Game);
            if (question == null) start();
            else Confirm(question, start);
        }

        void StartAIMatchNow()
        {
            var color = aiColorChoice == 2
                ? (UnityEngine.Random.value < 0.5f ? PieceColor.White : PieceColor.Black)
                : (PieceColor)aiColorChoice;
            GamePrefs.SetClockPreset(true, aiClockChoice);
            var settings = new MatchSettings { Mode = MatchMode.VersusAI, HumanColor = color, Level = aiLevel };
            settings.Clock = TimeControl.Presets[aiClockChoice];
            match.StartNew(settings);
            Show(aiOverlay, false);
            ShowMenu(false);
        }

        public void ShowMenu(bool show)
        {
            if (show)
            {
                bool inGame = match.Game != null && !match.Game.IsOver;
                continueButton.gameObject.SetActive(inGame);
                resumeButton.gameObject.SetActive((!inGame || match.IsPuzzle) && MatchController.HasSavedMatch());
            }
            Show(menuOverlay, show);
        }

        // ---------------------------------------------------------------- Promotion

        const float PromoQueenSize = 230f, PromoPieceSize = 170f, PromoSpacing = 16f, PromoPadding = 28f;
        RectTransform promoCard;
        Image[] promoIcons;
        static readonly PieceType[] PromoTypes = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

        void BuildPromotion()
        {
            float width = PromoQueenSize + 3 * PromoPieceSize + 3 * PromoSpacing + 2 * PromoPadding;
            promoOverlay = Overlay("Promotion", out var card, width, PromoQueenSize + 2 * PromoPadding + 52, UIKit.Cyan);
            promoCard = card;
            var dimButton = promoOverlay.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(CancelPromotion);
            // La carte absorbe ses propres clics : sinon ils remonteraient jusqu'au voile et annuleraient.
            card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var title = UIKit.Label("Title", card, "Promotion", 34, UIKit.Cyan, TextAnchor.UpperCenter, FontStyle.Bold);
            UIKit.Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -14), new Vector2(width, 44));

            var row = UIKit.Rect("Choices", card);
            UIKit.Anchor(row, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, PromoPadding), new Vector2(width - 2 * PromoPadding, PromoQueenSize));
            var h = UIKit.HStack(row, PromoSpacing);
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            h.childAlignment = TextAnchor.LowerCenter;
            promoIcons = new Image[PromoTypes.Length];
            string[] names = { "Dame", "Tour", "Fou", "Cavalier" };
            for (int i = 0; i < PromoTypes.Length; i++)
                promoIcons[i] = AddPromo(row, names[i], PromoTypes[i], i == 0 ? PromoQueenSize : PromoPieceSize);
        }

        Image AddPromo(RectTransform row, string label, PieceType type, float size)
        {
            var b = UIKit.Button("Promo" + type, row, label, () =>
            {
                sfx.Play(Sfx.Click);
                Show(promoOverlay, false);
                var cb = promotionCallback;
                promotionCallback = null;
                cb?.Invoke(type);
            }, null, type == PieceType.Queen ? 32 : 26);
            var le = UIKit.Size(b, size, size);
            le.minWidth = size;
            var text = b.GetComponentInChildren<Text>();
            text.alignment = TextAnchor.LowerCenter;
            UIKit.Stretch(text.rectTransform, 6, 6, 6, 4);

            // Silhouette tirée du maillage 3D de la pièce, teinte du camp à l'ouverture.
            var iconRt = UIKit.Rect("Piece", b.transform);
            UIKit.Stretch(iconRt, size * 0.14f, size * 0.2f, size * 0.14f, size * 0.06f);
            var icon = iconRt.gameObject.AddComponent<Image>();
            icon.sprite = PieceIcons.For(type);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        /// <summary>
        /// Demande la pièce de promotion, dans une rangée posée près de la case <paramref name="square"/>.
        /// Toucher en dehors de la carte annule (callback avec None).
        /// </summary>
        public void AskPromotion(Action<PieceType> onChosen, int square = Square.None)
        {
            promotionCallback = onChosen;
            var side = match.Game != null ? match.Game.SideToMove : PieceColor.White;
            var tint = side == PieceColor.White ? UIKit.Cyan : UIKit.Magenta;
            foreach (var icon in promoIcons) icon.color = tint;
            promoCard.anchoredPosition = PromotionAnchor(square);
            Show(promoOverlay, true);
        }

        /// <summary>Position de la carte : au-dessus de la case si elle est dans le bas de l'écran, sinon en dessous ; centrée à défaut.</summary>
        Vector2 PromotionAnchor(int square)
        {
            var cam = orbit != null ? orbit.GetComponent<Camera>() : null;
            if (square == Square.None || cam == null || canvas.scaleFactor <= 0f) return Vector2.zero;
            var world = board.transform.TransformPoint(BoardBuilder.SquareToWorld(Square.File(square), Square.Rank(square)));
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f) return Vector2.zero;
            float k = canvas.scaleFactor;
            var half = new Vector2(Screen.width, Screen.height) / (2f * k);
            var local = new Vector2(screen.x / k, screen.y / k) - half;
            var cardHalf = promoCard.sizeDelta * 0.5f;
            const float gap = 70f; // la case et la pièce restent visibles
            float y = local.y < 0f ? local.y + gap + cardHalf.y : local.y - gap - cardHalf.y;
            float x = Mathf.Clamp(local.x, -half.x + cardHalf.x + 12f, half.x - cardHalf.x - 12f);
            y = Mathf.Clamp(y, -half.y + cardHalf.y + 12f, half.y - cardHalf.y - 12f);
            return new Vector2(x, y);
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
            UIKit.VStack(card, 20, 50);
            FitHeight(card);
            endTitle = UIKit.Label("Title", card, "", 84, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(endTitle, 120);
            endReason = UIKit.Label("Reason", card, "", 40, UIKit.TextDim);
            UIKit.Size(endReason, 70);
            // Bilan : coups, durée, ouverture, et bilan contre ce niveau.
            endSummary = UIKit.Label("Summary", card, "", 30, UIKit.Text);
            UIKit.Size(endSummary, 96);
            endSummary.resizeTextForBestFit = true;
            endSummary.resizeTextMinSize = 20;
            endSummary.resizeTextMaxSize = 30;

            // Étincelles de victoire, derrière la carte.
            endSparks = UIKit.Rect("Sparks", endOverlay.transform);
            UIKit.Anchor(endSparks, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            endSparks.SetSiblingIndex(card.GetSiblingIndex());
            UIKit.Size(UIKit.Button("Replay", card, "Rejouer", () =>
            {
                sfx.Play(Sfx.Click);
                var s = match.Settings;
                // Contre l'IA, on change de couleur à chaque revanche.
                var next = new MatchSettings
                {
                    Mode = s.Mode, Level = s.Level, ClockBase = s.ClockBase, ClockIncrement = s.ClockIncrement,
                    HumanColor = s.Mode == MatchMode.VersusAI ? Piece.Opposite(s.HumanColor) : s.HumanColor,
                };
                match.StartNew(next);
                Show(endOverlay, false);
            }), 116);
            endAnalyzeButton = UIKit.Button("Analyze", card, "Analyser la partie", () => { sfx.Play(Sfx.Click); OpenAnalysis(); }, UIKit.Magenta);
            UIKit.Size(endAnalyzeButton, 106);
            var row = UIKit.Rect("Row", card);
            UIKit.HStack(row, 16);
            UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), 106);
            UIKit.Button("Review", row, "Revoir", () => { sfx.Play(Sfx.Click); EnterReview(match.Game.Moves.Count); }, UIKit.TextDim, 36);
            UIKit.Button("Copy", row, "Copier PGN", () => { sfx.Play(Sfx.Click); CopyPgn(); }, UIKit.TextDim, 36);
            UIKit.Size(UIKit.Button("ToMenu", card, "Menu", () => { sfx.Play(Sfx.Click); Show(endOverlay, false); ShowMenu(true); }, UIKit.TextDim), 100);
        }

        void OnGameEnded()
        {
            var game = match.Game;
            var s = match.Settings;
            endTitle.text = ResultTitle(game, s);
            endReason.text = ReasonText(game.EndReason);
            endAnalyzeButton.interactable = game.Moves.Count > 0;

            bool humanWon = s.Mode == MatchMode.VersusAI &&
                            ((game.Result == GameResult.WhiteWins && s.HumanColor == PieceColor.White) ||
                             (game.Result == GameResult.BlackWins && s.HumanColor == PieceColor.Black));
            bool humanLost = s.Mode == MatchMode.VersusAI && game.Result != GameResult.Draw && !humanWon;
            if (s.Mode == MatchMode.VersusAI) GamePrefs.RecordResult(s.Level, humanWon ? 1 : humanLost ? -1 : 0);
            string levelName = null;
            var record = new GamePrefs.Record();
            if (s.Mode == MatchMode.VersusAI)
            {
                levelName = MatchSettings.LevelName(s.Level);
                record = GamePrefs.Stats(s.Level);
            }
            endSummary.text = EndSummary.Line(game.Moves.Count, playTimeKnown ? playSeconds : -1, Opening(game), levelName,
                record.Wins, record.Draws, record.Losses);
            bool celebrate = humanWon || (s.Mode == MatchMode.LocalTwoPlayers && game.Result != GameResult.Draw);
            endTitle.color = humanLost ? UIKit.Magenta : UIKit.Cyan;
            var resultSound = game.Result == GameResult.Draw ? Sfx.Draw : humanLost ? Sfx.Defeat : Sfx.Victory;

            float delay = 0.9f;
            if (game.EndReason == GameEndReason.Checkmate)
            {
                // Le roi maté se brise ; son et animation partent dans la même image.
                board.StartCoroutine(board.PlayMateAnimation(game.Position.KingSquare(game.SideToMove)));
                sfx.PlayMate();
                delay = MateAnimationSeconds;
            }
            else if (game.EndReason == GameEndReason.Timeout || game.EndReason == GameEndReason.TimeoutInsufficientMaterial)
                sfx.PlayTimeout();
            StartCoroutine(ShowEndScreen(delay, resultSound, celebrate));
        }

        IEnumerator ShowEndScreen(float delay, Sfx resultSound, bool celebrate)
        {
            var game = match.Game;
            yield return new WaitForSecondsRealtime(delay);
            if (match.Game != game || !game.IsOver || reviewing) yield break;
            sfx.Play(resultSound);
            Show(endOverlay, true);
            if (celebrate) StartCoroutine(PlaySparks());
        }

        /// <summary>Gerbe d'étincelles aux couleurs du thème, derrière la carte de fin (trajectoires : Core.Ux.SparkBurst).</summary>
        IEnumerator PlaySparks()
        {
            const int count = 42;
            var palette = VisualTheme.Palette;
            var sparks = new Image[count];
            for (int i = 0; i < count; i++)
            {
                var img = UIKit.Rect("Spark", endSparks).gameObject.AddComponent<Image>();
                img.sprite = UIKit.Glow;
                img.raycastTarget = false;
                img.color = i % 2 == 0 ? palette.WhiteGlow : palette.BlackGlow;
                sparks[i] = img;
            }
            int seed = UnityEngine.Random.Range(1, 1000);
            for (float t = 0f; t < SparkBurst.Lifetime && endOverlay.activeSelf; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < count; i++)
                {
                    var p = SparkBurst.At(i, t, seed);
                    var rt = sparks[i].rectTransform;
                    rt.anchoredPosition = new Vector2(p.X, p.Y);
                    rt.sizeDelta = Vector2.one * (34f * p.Scale);
                    var c = sparks[i].color;
                    c.a = p.Alpha;
                    sparks[i].color = c;
                }
                yield return null;
            }
            foreach (var img in sparks) if (img != null) Destroy(img.gameObject);
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
                case GameEndReason.Timeout: return "Temps écoulé";
                case GameEndReason.TimeoutInsufficientMaterial: return "Temps écoulé, mais matériel insuffisant pour mater";
                default: return "";
            }
        }

        void OnNewMatch()
        {
            if (reviewing) LeaveReviewState();
            localViewSide = match.Settings.Mode == MatchMode.LocalTwoPlayers ? match.Game.SideToMove : match.ViewSide;
            orbit.ResetView(localViewSide);
            Show(endOverlay, false);
            Show(promoOverlay, false);
            promotionCallback = null;
            CancelAnalysis();
            lastTickSecond = -1;
            // Durée de jeu : connue pour une partie neuve, pas pour une partie reprise.
            playSeconds = 0f;
            playTimeKnown = match.Game.Moves.Count == 0;
        }

        // ---------------------------------------------------------------- Confirmation

        void BuildConfirm()
        {
            confirmOverlay = Overlay("Confirm", out var card, 860, 440, UIKit.Magenta);
            UIKit.VStack(card, 24, 50);
            FitHeight(card);
            confirmText = UIKit.Label("Question", card, "", 44, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(confirmText, 150);
            var row = UIKit.Rect("Buttons", card);
            UIKit.HStack(row, 20);
            UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), 120);
            UIKit.Button("No", row, "Non", () => { sfx.Play(Sfx.Click); Show(confirmOverlay, false); }, UIKit.TextDim);
            UIKit.Button("Yes", row, "Oui", () =>
            {
                sfx.Play(Sfx.Click);
                Show(confirmOverlay, false);
                var action = confirmAction;
                confirmAction = null;
                action?.Invoke();
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
            if (match == null) return;
            // L'IA et la pendule attendent tant qu'un écran est ouvert (la promotion fait partie du coup).
            match.Paused = menuOverlay.activeSelf || aiOverlay.activeSelf || confirmOverlay.activeSelf || endOverlay.activeSelf ||
                           IsScreenOverlayOpen || reviewing || drawPending;

            if (match.Game == null) return;
            if (!match.Paused && !match.Game.IsOver) playSeconds += Time.unscaledDeltaTime;
            UpdateClocks();
            UpdateClockTick();
            if (!reviewing && match.IsAIThinking && !match.Game.IsOver) status.text = ThinkingText();
            UpdateToast();
            UpdateTips();
        }

        /// <summary>Tic chaque seconde sous 10 s, pour la pendule d'un joueur humain qui tourne.</summary>
        void UpdateClockTick()
        {
            var clock = match.Clock;
            var running = clock.Running;
            if (clock.IsUnlimited || running == null || match.Game.IsOver || !match.Settings.IsHuman(running.Value))
            {
                lastTickSecond = -1;
                return;
            }
            double t = clock.Remaining(running.Value);
            if (t >= ClockAlertSeconds || t <= 0) { lastTickSecond = -1; return; }
            int second = (int)Math.Ceiling(t);
            if (second == lastTickSecond) return;
            lastTickSecond = second;
            sfx.PlayClockTick();
        }

        /// <summary>Bouton retour Android / Échap : ferme l'écran du dessus, sinon ouvre le menu.</summary>
        public void HandleBack()
        {
            if (promoOverlay.activeSelf) { CancelPromotion(); return; }
            if (confirmOverlay.activeSelf) { Show(confirmOverlay, false); confirmAction = null; return; }
            if (HandleBackScreens()) return;
            if (aiOverlay.activeSelf) { Show(aiOverlay, false); ShowMenu(true); return; }
            if (endOverlay.activeSelf) { Show(endOverlay, false); return; }
            if (reviewing) { ExitReview(); return; }
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
