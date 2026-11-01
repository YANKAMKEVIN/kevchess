using System;
using System.Collections.Generic;
using System.Text;
using KevChess.Core;
using KevChess.Core.Fantasy;
using KevChess.Game.Audio;
using KevChess.Game.CameraRig;
using KevChess.Game.Match;
using KevChess.Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace KevChess.Game.Legends
{
    /// <summary>
    /// Interface du mode Légendes, construite par code : jauges d'éclats, barre des pouvoirs, annonces du
    /// plateau vivant, réglages de partie, promotion, fin de partie.
    /// </summary>
    public sealed class LegendsHud : MonoBehaviour
    {
        static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        static readonly Color Ice = new Color(0.62f, 0.86f, 1f);
        static readonly Color Violet = new Color(0.72f, 0.45f, 1f);

        LegendsController match;
        LegendsInteraction interaction;
        OrbitCamera orbit;
        SoundBank sfx;
        Action exitToClassic;
        Action<FantasyAction> showHint;

        CanvasScaler scaler;
        RectTransform safeRoot;
        Rect appliedSafeArea;

        Text whiteName, blackName, whiteEnergyText, blackEnergyText, status, events, movesLine;
        Image whiteTurn, blackTurn;
        Image[] whiteGauge, blackGauge;
        Button undoButton, hintButton, resignButton;
        readonly Dictionary<LegendsTool, Button> toolButtons = new Dictionary<LegendsTool, Button>();

        GameObject setupOverlay, promoOverlay, endOverlay, confirmOverlay;
        Button resumeButton;
        Button[] modeButtons, colorButtons, levelButtons;
        Text endTitle, endReason, confirmText;
        Action confirmAction;
        RectTransform promoRow;
        int setupMode = 1, setupColor;
        AILevel setupLevel = AILevel.Medium;
        PieceColor localViewSide = PieceColor.White;

        public bool IsBlockingOverlayOpen =>
            setupOverlay.activeSelf || promoOverlay.activeSelf || endOverlay.activeSelf || confirmOverlay.activeSelf;

        public void Build(LegendsController controller, LegendsInteraction legendsInteraction, OrbitCamera camera, SoundBank sounds,
            Action onExit, Action<FantasyAction> onHint)
        {
            match = controller;
            interaction = legendsInteraction;
            orbit = camera;
            sfx = sounds;
            exitToClassic = onExit;
            showHint = onHint;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 11;
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            gameObject.AddComponent<GraphicRaycaster>();
            safeRoot = UIKit.Rect("SafeArea", transform);
            UIKit.Stretch(safeRoot);

            BuildHud();
            BuildSetup();
            BuildPromotion();
            BuildEnd();
            BuildConfirm();

            match.StateChanged += Refresh;
            match.GameEnded += OnGameEnded;
            match.NewMatchStarted += () => { localViewSide = match.Game.SideToMove; Refresh(); };
            interaction.Changed += Refresh;
            UpdateLayout();
        }

        // ---------------------------------------------------------------- HUD

        void BuildHud()
        {
            var top = UIKit.Rect("TopBar", safeRoot);
            UIKit.Anchor(top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -24), new Vector2(1040, 170));
            UIKit.HStack(top, 20);
            BuildPlayerCard(top, PieceColor.White, out whiteName, out whiteEnergyText, out whiteTurn, out whiteGauge);
            BuildPlayerCard(top, PieceColor.Black, out blackName, out blackEnergyText, out blackTurn, out blackGauge);

            status = UIKit.Label("Status", safeRoot, "", 42, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Anchor(status.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -210), new Vector2(1040, 60));
            status.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);

            events = UIKit.Label("Events", safeRoot, "", 28, Violet);
            UIKit.Anchor(events.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -258), new Vector2(1020, 76));
            events.horizontalOverflow = HorizontalWrapMode.Wrap;
            events.verticalOverflow = VerticalWrapMode.Overflow;
            events.lineSpacing = 0.95f;

            movesLine = UIKit.Label("Moves", safeRoot, "", 28, UIKit.TextDim);
            UIKit.Anchor(movesLine.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -336), new Vector2(1040, 40));
            movesLine.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Barre des pouvoirs.
            var powers = UIKit.Panel("PowerBar", safeRoot, UIKit.Glass, true, Gold);
            UIKit.Anchor(powers.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 184), new Vector2(1040, 130));
            UIKit.HStack(powers.rectTransform, 10, 12);
            var cfg = FantasyConfig.Default;
            AddTool(powers.transform, LegendsTool.Shield, "Bouclier\n" + cfg.ShieldCost, Ice);
            AddTool(powers.transform, LegendsTool.Freeze, "Gel\n" + cfg.FreezeCost, Ice);
            AddTool(powers.transform, LegendsTool.Teleport, "Saut\n" + cfg.TeleportCost, Violet);
            AddTool(powers.transform, LegendsTool.Summon, "Dragon\n" + cfg.SummonCost, Gold);
            AddTool(powers.transform, LegendsTool.Breath, "Souffle", new Color(1f, 0.45f, 0.25f));

            var bar = UIKit.Panel("BottomBar", safeRoot, UIKit.Glass, true, UIKit.Cyan);
            UIKit.Anchor(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 28), new Vector2(1040, 140));
            UIKit.HStack(bar.rectTransform, 14, 16);
            UIKit.Button("Menu", bar.transform, "Menu", () => { sfx.Play(Sfx.Click); ShowSetup(true); }, null, 34);
            undoButton = UIKit.Button("Undo", bar.transform, "Annuler", () => { sfx.Play(Sfx.Click); interaction.Reset(); match.UndoForHuman(); }, null, 34);
            hintButton = UIKit.Button("Hint", bar.transform, "Indice", () => { sfx.Play(Sfx.Click); match.RequestHint(showHint); }, null, 34);
            UIKit.Button("View", bar.transform, "Vue", OnViewPressed, null, 34);
            resignButton = UIKit.Button("Resign", bar.transform, "Abandon", () =>
            {
                sfx.Play(Sfx.Click);
                Confirm("Abandonner la partie ?", () => match.Resign());
            }, UIKit.Magenta, 34);
        }

        void AddTool(Transform parent, LegendsTool tool, string label, Color accent)
        {
            var b = UIKit.Button("Tool" + tool, parent, label, () => { sfx.Play(Sfx.Click); interaction.ToggleTool(tool); }, accent, 30);
            var text = b.GetComponentInChildren<Text>();
            if (text != null) text.lineSpacing = 0.9f;
            toolButtons[tool] = b;
        }

        void BuildPlayerCard(RectTransform parent, PieceColor color, out Text name, out Text energy, out Image turn, out Image[] gauge)
        {
            var accent = color == PieceColor.White ? UIKit.Cyan : UIKit.Magenta;
            var card = UIKit.Panel(color + "Card", parent, UIKit.Glass, true, accent);
            turn = UIKit.Panel("Turn", card.transform, accent * new Color(1, 1, 1, 0.22f), false);
            UIKit.Stretch(turn.rectTransform);
            turn.raycastTarget = false;

            name = UIKit.Label("Name", card.transform, "", 36, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Stretch(name.rectTransform, 24, 84, 20, 14);
            energy = UIKit.Label("Energy", card.transform, "", 28, Gold, TextAnchor.MiddleRight);
            UIKit.Stretch(energy.rectTransform, 24, 84, 20, 14);

            // Jauge : 10 éclats.
            var row = UIKit.Rect("Gauge", card.transform);
            UIKit.Stretch(row, 24, 22, 24, 100);
            UIKit.HStack(row, 6);
            gauge = new Image[FantasyConfig.Default.MaxEnergy];
            for (int i = 0; i < gauge.Length; i++)
            {
                var seg = UIKit.Panel("Shard" + i, row, new Color(1, 1, 1, 0.08f), false);
                seg.raycastTarget = false;
                gauge[i] = seg;
            }
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

        public void ResetCamera()
        {
            if (match.Game == null) return;
            if (match.Settings.Mode == MatchMode.LocalTwoPlayers) localViewSide = match.Game.SideToMove;
            orbit.ResetView(match.Settings.Mode == MatchMode.LocalTwoPlayers ? localViewSide : match.ViewSide);
        }

        public void Refresh()
        {
            var game = match.Game;
            if (game == null) return;
            var s = match.Settings;
            var pos = game.Position;

            whiteName.text = PlayerName(PieceColor.White, s);
            blackName.text = PlayerName(PieceColor.Black, s);
            FillGauge(whiteGauge, whiteEnergyText, pos.Energy(PieceColor.White), UIKit.Cyan, pos.Config);
            FillGauge(blackGauge, blackEnergyText, pos.Energy(PieceColor.Black), UIKit.Magenta, pos.Config);
            whiteTurn.enabled = game.SideToMove == PieceColor.White && !game.IsOver;
            blackTurn.enabled = game.SideToMove == PieceColor.Black && !game.IsOver;

            status.color = UIKit.Text;
            if (game.IsOver) status.text = ResultTitle(game, s);
            else if (match.IsAIThinking) status.text = "L'IA réfléchit…";
            else if (interaction.IsTargeting) status.text = TargetHint(interaction.Tool);
            else if (interaction.Tool != LegendsTool.None) status.text = ToolHint(interaction.Tool, interaction.ArmedSquare);
            else if (game.InCheck) { status.text = "Échec !"; status.color = UIKit.Danger; }
            else if (s.Mode == MatchMode.VersusAI) status.text = s.IsHuman(game.SideToMove) ? "À vous de jouer" : "";
            else status.text = game.SideToMove == PieceColor.White ? "Aux Blancs de jouer" : "Aux Noirs de jouer";

            events.text = EventsLine(pos);
            movesLine.text = RecentMoves(game, 6);

            foreach (var kv in toolButtons)
            {
                bool available = interaction.IsToolAvailable(kv.Key);
                bool active = interaction.Tool == kv.Key;
                kv.Value.interactable = available || active;
                kv.Value.gameObject.SetActive(kv.Key != LegendsTool.Breath || available || active);
                var img = (Image)kv.Value.targetGraphic;
                img.color = active ? Gold * new Color(0.6f, 0.6f, 0.6f, 1f) : new Color(0.08f, 0.10f, 0.20f, 0.85f);
            }

            undoButton.interactable = game.Actions.Count > 0 && !match.IsAnimating;
            hintButton.interactable = match.CanHumanAct;
            resignButton.interactable = !game.IsOver && !match.IsAnimating;
        }

        static void FillGauge(Image[] gauge, Text label, int energy, Color accent, FantasyConfig cfg)
        {
            bool full = energy >= cfg.SummonCost;
            float pulse = full ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
            for (int i = 0; i < gauge.Length; i++)
                gauge[i].color = i < energy ? (full ? Gold : accent) * new Color(pulse, pulse, pulse, 0.95f) : new Color(1, 1, 1, 0.08f);
            label.text = energy + " éclat" + (energy > 1 ? "s" : "");
        }

        static string TargetHint(LegendsTool tool)
        {
            switch (tool)
            {
                case LegendsTool.Shield: return "Bouclier : touchez une de vos pièces";
                case LegendsTool.Freeze: return "Gel : touchez une pièce adverse";
                default: return "Dragon : touchez une de vos tours";
            }
        }

        static string ToolHint(LegendsTool tool, int square)
        {
            string at = square == Square.None ? "" : " " + Square.Name(square);
            switch (tool)
            {
                case LegendsTool.Shield: return "Bouclier" + at + " — jouez votre coup";
                case LegendsTool.Freeze: return "Gel" + at + " — jouez votre coup";
                case LegendsTool.Summon: return "Dragon" + at + " — jouez votre coup";
                case LegendsTool.Teleport: return "Saut : choisissez une pièce puis une case voisine";
                default: return "Souffle : choisissez la cible du dragon";
            }
        }

        static string EventsLine(FantasyPosition pos)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < FantasyPosition.HoleSlotCount; i++)
            {
                var st = pos.HoleStateAt(i);
                if (st == HoleState.None) continue;
                if (sb.Length > 0) sb.Append("   ");
                sb.Append(st == HoleState.Fissure ? "Fissure " : "Gouffre ").Append(Square.Name(pos.HoleSquare(i)))
                  .Append(st == HoleState.Fissure ? " cède dans " : " encore ").Append(pos.HoleTimer(i));
            }
            if (pos.PortalState != PortalState.None)
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append("Portails ").Append(Square.Name(pos.PortalA)).Append('–').Append(Square.Name(pos.PortalB))
                  .Append(pos.PortalState == PortalState.Announced ? " s'ouvrent dans " : " ouverts encore ").Append(pos.PortalTimer);
            }
            if (pos.CrystalSquare != Square.None)
            {
                if (sb.Length > 0) sb.Append("   ");
                sb.Append("Gisement ").Append(Square.Name(pos.CrystalSquare));
            }
            return sb.ToString();
        }

        static string PlayerName(PieceColor color, MatchSettings s)
        {
            string side = color == PieceColor.White ? "Blancs" : "Noirs";
            if (s.Mode == MatchMode.LocalTwoPlayers) return side;
            return s.HumanColor == color ? "Vous" : "IA " + MatchSettings.LevelName(s.Level);
        }

        static string RecentMoves(FantasyGame game, int maxPlies)
        {
            var n = game.Notations;
            if (n.Count == 0) return "";
            int first = Mathf.Max(0, n.Count - maxPlies);
            var sb = new StringBuilder();
            for (int i = first; i < n.Count; i++)
            {
                if (i % 2 == 0) sb.Append(i / 2 + 1).Append(". ");
                else if (i == first) sb.Append(i / 2 + 1).Append("… ");
                sb.Append(n[i]).Append("  ");
            }
            return sb.ToString().TrimEnd();
        }

        static string ResultTitle(FantasyGame game, MatchSettings s)
        {
            if (game.Result == GameResult.Draw) return "Partie nulle";
            var winner = game.Result == GameResult.WhiteWins ? PieceColor.White : PieceColor.Black;
            if (s.Mode == MatchMode.VersusAI) return winner == s.HumanColor ? "Victoire !" : "Défaite";
            return winner == PieceColor.White ? "Les Blancs gagnent" : "Les Noirs gagnent";
        }

        static string ReasonText(GameEndReason r)
        {
            switch (r)
            {
                case GameEndReason.Checkmate: return "Échec et mat";
                case GameEndReason.Stalemate: return "Pat";
                case GameEndReason.FiftyMoveRule: return "Règle des 50 coups";
                case GameEndReason.ThreefoldRepetition: return "Triple répétition";
                case GameEndReason.InsufficientMaterial: return "Matériel insuffisant";
                case GameEndReason.Resignation: return "Abandon";
                case GameEndReason.DrawAgreement: return "Nulle d'un commun accord";
                default: return "";
            }
        }

        // ---------------------------------------------------------------- écrans

        GameObject Overlay(string name, out RectTransform card, float width, float height, Color accent)
        {
            var root = UIKit.Rect(name, transform);
            UIKit.Stretch(root);
            root.gameObject.AddComponent<Image>().color = new Color(0.01f, 0.01f, 0.04f, 0.62f);
            var panel = UIKit.Panel("Card", root, UIKit.GlassStrong, true, accent);
            UIKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            card = panel.rectTransform;
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        void BuildSetup()
        {
            setupOverlay = Overlay("LegendsSetup", out var card, 960, 1500, Gold);
            UIKit.VStack(card, 20, 48);
            var title = UIKit.Label("Title", card, "LÉGENDES", 100, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(title, 130);
            var o = title.gameObject.AddComponent<Outline>();
            o.effectColor = UIKit.Magenta * new Color(1, 1, 1, 0.6f);
            o.effectDistance = new Vector2(4, -4);
            UIKit.Size(UIKit.Label("Sub", card, "Pouvoirs, dragons et plateau vivant", 34, UIKit.TextDim), 50);

            resumeButton = UIKit.Button("Resume", card, "Reprendre la partie", () =>
            {
                sfx.Play(Sfx.Click);
                if (match.Game != null && !match.Game.IsOver) ShowSetup(false);
                else if (match.ResumeSaved()) ShowSetup(false);
            }, Gold);
            UIKit.Size(resumeButton, 110);

            var modes = Row(card, "Modes", 110);
            modeButtons = new[]
            {
                UIKit.Button("Two", modes, "À deux", () => { sfx.Play(Sfx.Click); setupMode = 0; RefreshSetup(); }),
                UIKit.Button("AI", modes, "Contre l'IA", () => { sfx.Play(Sfx.Click); setupMode = 1; RefreshSetup(); }, UIKit.Magenta),
            };
            var colors = Row(card, "Colors", 100);
            string[] names = { "Blancs", "Noirs", "Hasard" };
            colorButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                colorButtons[i] = UIKit.Button("Color" + i, colors, names[i], () => { sfx.Play(Sfx.Click); setupColor = idx; RefreshSetup(); }, null, 34);
            }
            var levels = Row(card, "Levels", 100);
            levelButtons = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                var level = (AILevel)i;
                levelButtons[i] = UIKit.Button("Level" + i, levels, MatchSettings.LevelName(level), () => { sfx.Play(Sfx.Click); setupLevel = level; RefreshSetup(); }, null, 26);
            }

            UIKit.Size(UIKit.Button("Play", card, "Nouvelle partie", StartFromSetup, Gold), 120);
            UIKit.Size(UIKit.Button("Classic", card, "Échecs classiques", () => { sfx.Play(Sfx.Click); ShowSetup(false); exitToClassic(); }, UIKit.TextDim), 100);
            RefreshSetup();
        }

        static RectTransform Row(RectTransform parent, string name, float height)
        {
            var row = UIKit.Rect(name, parent);
            UIKit.HStack(row, 12);
            UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), height);
            return row;
        }

        void RefreshSetup()
        {
            for (int i = 0; i < modeButtons.Length; i++) Highlight(modeButtons[i], i == setupMode, i == 0 ? UIKit.Cyan : UIKit.Magenta);
            for (int i = 0; i < colorButtons.Length; i++)
            {
                colorButtons[i].gameObject.SetActive(setupMode == 1);
                Highlight(colorButtons[i], i == setupColor, UIKit.Cyan);
            }
            for (int i = 0; i < levelButtons.Length; i++)
            {
                levelButtons[i].gameObject.SetActive(setupMode == 1);
                Highlight(levelButtons[i], i == (int)setupLevel, UIKit.Magenta);
            }
        }

        static void Highlight(Button b, bool on, Color accent)
        {
            ((Image)b.targetGraphic).color = on ? accent * new Color(0.55f, 0.55f, 0.55f, 1f) : new Color(0.08f, 0.10f, 0.20f, 0.85f);
        }

        void StartFromSetup()
        {
            sfx.Play(Sfx.Click);
            var settings = new MatchSettings { Mode = setupMode == 0 ? MatchMode.LocalTwoPlayers : MatchMode.VersusAI, Level = setupLevel };
            settings.HumanColor = setupColor == 2 ? (UnityEngine.Random.value < 0.5f ? PieceColor.White : PieceColor.Black) : (PieceColor)setupColor;
            interaction.Reset();
            match.StartNew(settings);
            orbit.ResetView(match.Settings.Mode == MatchMode.LocalTwoPlayers ? PieceColor.White : match.ViewSide);
            ShowSetup(false);
        }

        public void ShowSetup(bool show)
        {
            if (show)
            {
                bool inGame = match.Game != null && !match.Game.IsOver;
                resumeButton.gameObject.SetActive(inGame || LegendsController.HasSaved());
                UIKit.SetButtonText(resumeButton, inGame ? "Continuer" : "Reprendre la partie");
            }
            setupOverlay.SetActive(show);
            match.Paused = IsBlockingOverlayOpen && !promoOverlay.activeSelf;
        }

        void BuildPromotion()
        {
            promoOverlay = Overlay("LegendsPromotion", out var card, 960, 440, Gold);
            UIKit.VStack(card, 16, 40);
            UIKit.Size(UIKit.Label("Title", card, "Promotion", 52, Gold, TextAnchor.MiddleCenter, FontStyle.Bold), 80);
            promoRow = UIKit.Rect("Row", card);
            UIKit.HStack(promoRow, 14);
            UIKit.Size(promoRow.gameObject.AddComponent<LayoutElement>(), 150);
        }

        /// <summary>Choix de la pièce de promotion parmi les actions possibles.</summary>
        public void AskPromotion(int from, int to, IReadOnlyList<FantasyAction> options)
        {
            for (int i = promoRow.childCount - 1; i >= 0; i--) Destroy(promoRow.GetChild(i).gameObject);
            foreach (var a in options)
            {
                var action = a;
                string label = a.Promotion == PieceType.Queen ? "Dame" : a.Promotion == PieceType.Rook ? "Tour" : a.Promotion == PieceType.Bishop ? "Fou" : "Cavalier";
                UIKit.Button("Promo" + a.Promotion, promoRow, label, () =>
                {
                    sfx.Play(Sfx.Click);
                    promoOverlay.SetActive(false);
                    interaction.OnPromotionChosen(action, from);
                }, Gold, 34);
            }
            UIKit.Button("Cancel", promoRow, "×", () =>
            {
                promoOverlay.SetActive(false);
                interaction.OnPromotionChosen(FantasyAction.Null, from);
            }, UIKit.TextDim, 40);
            promoOverlay.SetActive(true);
        }

        void BuildEnd()
        {
            endOverlay = Overlay("LegendsEnd", out var card, 900, 640, Gold);
            UIKit.VStack(card, 22, 48);
            endTitle = UIKit.Label("Title", card, "", 76, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(endTitle, 110);
            endReason = UIKit.Label("Reason", card, "", 38, UIKit.TextDim);
            UIKit.Size(endReason, 60);
            UIKit.Size(UIKit.Button("Replay", card, "Rejouer", () => { sfx.Play(Sfx.Click); endOverlay.SetActive(false); match.StartNew(match.Settings); }, Gold), 110);
            UIKit.Size(UIKit.Button("Review", card, "Voir le plateau", () => { sfx.Play(Sfx.Click); endOverlay.SetActive(false); }, UIKit.TextDim), 100);
            UIKit.Size(UIKit.Button("ToMenu", card, "Menu", () => { sfx.Play(Sfx.Click); endOverlay.SetActive(false); ShowSetup(true); }, UIKit.Magenta), 100);
        }

        void OnGameEnded()
        {
            var game = match.Game;
            endTitle.text = ResultTitle(game, match.Settings);
            endReason.text = ReasonText(game.EndReason);
            bool won = match.Settings.Mode == MatchMode.LocalTwoPlayers || game.Result == GameResult.Draw
                       || (game.Result == GameResult.WhiteWins) == (match.Settings.HumanColor == PieceColor.White);
            sfx.Play(game.Result == GameResult.Draw ? Sfx.Draw : won ? Sfx.Victory : Sfx.Defeat);
            endOverlay.SetActive(true);
            Refresh();
        }

        void BuildConfirm()
        {
            confirmOverlay = Overlay("LegendsConfirm", out var card, 860, 420, UIKit.Magenta);
            UIKit.VStack(card, 22, 48);
            confirmText = UIKit.Label("Text", card, "", 44, UIKit.Text);
            UIKit.Size(confirmText, 140);
            var row = Row(card, "Row", 110);
            UIKit.Button("No", row, "Non", () => { sfx.Play(Sfx.Click); confirmOverlay.SetActive(false); }, UIKit.TextDim);
            UIKit.Button("Yes", row, "Oui", () => { sfx.Play(Sfx.Click); confirmOverlay.SetActive(false); confirmAction?.Invoke(); }, UIKit.Magenta);
        }

        void Confirm(string text, Action onYes)
        {
            confirmText.text = text;
            confirmAction = onYes;
            confirmOverlay.SetActive(true);
        }

        public void HandleBack()
        {
            if (promoOverlay.activeSelf) return;
            if (confirmOverlay.activeSelf) { confirmOverlay.SetActive(false); return; }
            if (endOverlay.activeSelf) { endOverlay.SetActive(false); return; }
            if (interaction.Tool != LegendsTool.None) { interaction.Reset(); return; }
            if (setupOverlay.activeSelf)
            {
                if (match.Game != null && !match.Game.IsOver) ShowSetup(false);
                return;
            }
            ShowSetup(true);
        }

        void Update()
        {
            UpdateLayout();
            if (match?.Game != null)
            {
                var pos = match.Game.Position;
                // La jauge pleine pulse.
                if (pos.Energy(PieceColor.White) >= pos.Config.SummonCost || pos.Energy(PieceColor.Black) >= pos.Config.SummonCost)
                {
                    FillGauge(whiteGauge, whiteEnergyText, pos.Energy(PieceColor.White), UIKit.Cyan, pos.Config);
                    FillGauge(blackGauge, blackEnergyText, pos.Energy(PieceColor.Black), UIKit.Magenta, pos.Config);
                }
            }
        }

        void UpdateLayout()
        {
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
