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
    /// Interface du mode Légendes, construite par code. Disposition (d'après la revue de Codex) :
    /// statut et menu « ⋯ » en haut, camp adverse au-dessus du plateau, alerte du plateau vivant,
    /// plateau 3D, votre camp en dessous, cartes de pouvoir près du pouce (avec état et raison),
    /// confirmation du coût avant de dépenser, commandes discrètes. Pendant un duel, les commandes
    /// laissent la place à la légende du duel.
    /// </summary>
    public sealed class LegendsHud : MonoBehaviour
    {
        // Couleurs : cyan et magenta pour les camps, ambre pour les dangers, violet pour les portails,
        // or réservé au dragon.
        static readonly Color Amber = new Color(1f, 0.70f, 0.28f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        static readonly Color Violet = new Color(0.72f, 0.45f, 1f);
        static readonly Color Surface = new Color(0.05f, 0.06f, 0.11f, 0.94f);
        static readonly Color SurfaceSoft = new Color(0.07f, 0.08f, 0.14f, 0.90f);
        static readonly Color Border = new Color(0.30f, 0.36f, 0.50f, 0.55f);

        LegendsController match;
        LegendsInteraction interaction;
        LegendsBoardView board;
        OrbitCamera orbit;
        SoundBank sfx;
        Action exitToClassic;
        Action<FantasyAction> showHint;

        CanvasScaler scaler;
        RectTransform safeRoot;
        Rect appliedSafeArea;

        // Haut
        Text title, subtitle;
        // Cartes de camp : 0 = en haut (adversaire), 1 = en bas (vous)
        readonly PlayerStrip[] strips = new PlayerStrip[2];
        // Alerte
        GameObject alertBox;
        Text alertTitle, alertDetail, alertMore;
        bool alertExpanded;
        // Pouvoirs
        GameObject powerGroup;
        Text powerCaption, powerHint;
        readonly Dictionary<LegendsTool, PowerCard> cards = new Dictionary<LegendsTool, PowerCard>();
        // Confirmation
        GameObject confirmBox;
        Text confirmTitle, confirmCost, confirmText;
        Button confirmYes;
        // Commandes
        GameObject commandBar;
        Button undoButton, hintButton;
        // Duel
        GameObject duelBox;
        Text duelText;
        string duelCaption = "";

        GameObject setupOverlay, moreOverlay, promoOverlay, endOverlay, confirmOverlay;
        Button resumeButton;
        Button[] modeButtons, colorButtons, levelButtons, duelButtons;
        GameObject aiGroup;
        Text levelName, endTitle, endReason, confirmQuestion;
        Action confirmAction;
        RectTransform promoRow;
        int setupMode = 1, setupColor;
        AILevel setupLevel = AILevel.Medium;
        PieceColor localViewSide = PieceColor.White;

        sealed class PlayerStrip
        {
            public Image Panel, Accent;
            public Text Name, Energy;
            public Image[] Gauge;
        }

        sealed class PowerCard
        {
            public Button Button;
            public Image Background;
            public Text Name, State;
            public Color Accent;
        }

        public bool IsBlockingOverlayOpen =>
            setupOverlay.activeSelf || moreOverlay.activeSelf || promoOverlay.activeSelf || endOverlay.activeSelf || confirmOverlay.activeSelf;

        public void Build(LegendsController controller, LegendsInteraction legendsInteraction, LegendsBoardView boardView,
            OrbitCamera camera, SoundBank sounds, Action onExit, Action<FantasyAction> onHint)
        {
            match = controller;
            interaction = legendsInteraction;
            board = boardView;
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

            BuildTop();
            BuildBottom();
            BuildSetup();
            BuildMore();
            BuildPromotion();
            BuildEnd();
            BuildConfirmDialog();

            match.StateChanged += Refresh;
            match.GameEnded += OnGameEnded;
            match.NewMatchStarted += () =>
            {
                localViewSide = match.ViewSide;
                alertExpanded = false;
                orbit.ResetView(match.ViewSide); // votre camp en bas
                Refresh();
            };
            match.ActionStarted += v => duelCaption = Caption(v);
            interaction.Changed += Refresh;
            UpdateLayout();
        }

        // ---------------------------------------------------------------- haut de l'écran

        void BuildTop()
        {
            var header = UIKit.Label("Brand", safeRoot, "KEVCHESS · LÉGENDES", 24, UIKit.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44, -26), new Vector2(700, 36));
            title = UIKit.Label("Title", safeRoot, "", 54, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42, -62), new Vector2(860, 72));
            subtitle = UIKit.Label("Subtitle", safeRoot, "", 28, UIKit.TextDim, TextAnchor.MiddleLeft);
            UIKit.Anchor(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44, -134), new Vector2(860, 40));
            subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;

            var more = UIKit.Button("More", safeRoot, "•••", () => { sfx.Play(Sfx.Click); ShowOverlay(moreOverlay, true); }, UIKit.TextDim, 44);
            UIKit.Anchor((RectTransform)more.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40, -40), new Vector2(110, 96));

            strips[0] = BuildStrip(safeRoot, "TopStrip", -186);

            // Alerte du plateau vivant (la plus urgente, les autres au toucher).
            var alert = UIKit.Panel("Alert", safeRoot, new Color(0.16f, 0.10f, 0.05f, 0.92f), true, Amber * new Color(1, 1, 1, 0.7f));
            UIKit.Anchor(alert.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -306), new Vector2(1000, 96));
            var alertButton = alert.gameObject.AddComponent<Button>();
            alertButton.transition = Selectable.Transition.None;
            alertButton.onClick.AddListener(() => { alertExpanded = !alertExpanded; Refresh(); });
            alertBox = alert.gameObject;
            alertTitle = UIKit.Label("Title", alert.transform, "", 32, Amber, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Stretch(alertTitle.rectTransform, 26, 46, 200, 12);
            alertDetail = UIKit.Label("Detail", alert.transform, "", 26, UIKit.Text, TextAnchor.UpperLeft);
            UIKit.Stretch(alertDetail.rectTransform, 26, 8, 26, 50);
            alertDetail.horizontalOverflow = HorizontalWrapMode.Wrap;
            alertDetail.verticalOverflow = VerticalWrapMode.Overflow;
            alertMore = UIKit.Label("More", alert.transform, "", 24, UIKit.TextDim, TextAnchor.UpperRight);
            UIKit.Stretch(alertMore.rectTransform, 600, 46, 26, 14);
        }

        PlayerStrip BuildStrip(RectTransform parent, string name, float y, bool bottomAnchor = false)
        {
            var s = new PlayerStrip();
            s.Panel = UIKit.Panel(name, parent, SurfaceSoft, true, Border);
            var anchor = bottomAnchor ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
            UIKit.Anchor(s.Panel.rectTransform, anchor, anchor, new Vector2(0, y), new Vector2(1000, 104));
            s.Accent = UIKit.Panel("Accent", s.Panel.transform, Color.white, false);
            s.Accent.raycastTarget = false;
            UIKit.Anchor(s.Accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10, 0), new Vector2(8, 70));
            s.Name = UIKit.Label("Name", s.Panel.transform, "", 30, UIKit.Text, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Stretch(s.Name.rectTransform, 34, 50, 300, 12);
            s.Energy = UIKit.Label("Energy", s.Panel.transform, "", 26, UIKit.TextDim, TextAnchor.UpperRight);
            UIKit.Stretch(s.Energy.rectTransform, 600, 50, 26, 14);
            var row = UIKit.Rect("Gauge", s.Panel.transform);
            UIKit.Stretch(row, 34, 20, 26, 66);
            UIKit.HStack(row, 8);
            s.Gauge = new Image[FantasyConfig.Default.MaxEnergy];
            for (int i = 0; i < s.Gauge.Length; i++)
            {
                var seg = UIKit.Panel("Shard" + i, row, new Color(1, 1, 1, 0.08f), false);
                seg.raycastTarget = false;
                s.Gauge[i] = seg;
            }
            return s;
        }

        // ---------------------------------------------------------------- bas de l'écran

        void BuildBottom()
        {
            // Commandes discrètes.
            var bar = UIKit.Rect("Commands", safeRoot);
            UIKit.Anchor(bar, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(1000, 104));
            UIKit.HStack(bar, 16);
            commandBar = bar.gameObject;
            undoButton = UIKit.Button("Undo", bar, "Annuler", () => { sfx.Play(Sfx.Click); interaction.Reset(); match.UndoForHuman(); }, UIKit.TextDim, 30);
            hintButton = UIKit.Button("Hint", bar, "Indice", () => { sfx.Play(Sfx.Click); match.RequestHint(showHint); }, UIKit.TextDim, 30);
            UIKit.Button("View", bar, "Vue 3D", OnViewPressed, UIKit.TextDim, 30);

            // Cartes de pouvoir.
            var group = UIKit.Rect("Powers", safeRoot);
            UIKit.Anchor(group, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 156), new Vector2(1000, 230));
            powerGroup = group.gameObject;
            powerCaption = UIKit.Label("Caption", group, "Pouvoir facultatif, puis votre coup", 28, UIKit.Text, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Anchor(powerCaption.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(4, 0), new Vector2(760, 40));
            var perTurn = UIKit.Label("PerTurn", group, "1 par tour", 24, UIKit.TextDim, TextAnchor.UpperRight);
            UIKit.Anchor(perTurn.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-4, 0), new Vector2(240, 40));
            var row = UIKit.Rect("Cards", group);
            UIKit.Anchor(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -48), new Vector2(1000, 128));
            UIKit.HStack(row, 12);
            var ice = new Color(0.62f, 0.86f, 1f);
            AddCard(row, LegendsTool.Shield, "Bouclier", ice);
            AddCard(row, LegendsTool.Freeze, "Gel", ice);
            AddCard(row, LegendsTool.Teleport, "Saut", Violet);
            AddCard(row, LegendsTool.Summon, "Dragon", Gold);
            AddCard(row, LegendsTool.Breath, "Souffle", new Color(1f, 0.45f, 0.25f));
            powerHint = UIKit.Label("Hint", group, "", 24, UIKit.TextDim, TextAnchor.UpperLeft);
            UIKit.Anchor(powerHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(4, 0), new Vector2(1000, 40));
            powerHint.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Confirmation du pouvoir (remplace les cartes le temps de valider).
            var confirm = UIKit.Panel("ConfirmPower", safeRoot, Surface, true, UIKit.Cyan * new Color(1, 1, 1, 0.7f));
            UIKit.Anchor(confirm.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1000, 250));
            confirmBox = confirm.gameObject;
            confirmTitle = UIKit.Label("Title", confirm.transform, "", 38, UIKit.Text, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Stretch(confirmTitle.rectTransform, 30, 180, 260, 20);
            confirmCost = UIKit.Label("Cost", confirm.transform, "", 28, UIKit.Cyan, TextAnchor.UpperRight);
            UIKit.Stretch(confirmCost.rectTransform, 600, 180, 30, 26);
            confirmText = UIKit.Label("Text", confirm.transform, "", 28, UIKit.TextDim, TextAnchor.UpperLeft);
            UIKit.Stretch(confirmText.rectTransform, 30, 110, 30, 78);
            confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var buttons = UIKit.Rect("Buttons", confirm.transform);
            UIKit.Stretch(buttons, 30, 22, 30, 150);
            UIKit.HStack(buttons, 16);
            UIKit.Button("Cancel", buttons, "Annuler", () => { sfx.Play(Sfx.Click); interaction.CancelPower(); }, UIKit.TextDim, 32);
            confirmYes = UIKit.Button("Yes", buttons, "Confirmer", () => { sfx.Play(Sfx.Click); interaction.ConfirmPower(); }, UIKit.Cyan, 32);
            ((Image)confirmYes.targetGraphic).color = UIKit.Cyan * new Color(0.55f, 0.55f, 0.55f, 1f);
            confirmBox.SetActive(false);

            strips[1] = BuildStrip(safeRoot, "BottomStrip", 404, true);

            // Légende du duel (à la place des commandes pendant la mise en scène).
            var duel = UIKit.Panel("Duel", safeRoot, Surface, true, Border);
            UIKit.Anchor(duel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(1000, 150));
            duelBox = duel.gameObject;
            duelText = UIKit.Label("Text", duel.transform, "", 32, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(duelText.rectTransform, 20, 54, 20, 14);
            var skip = UIKit.Label("Skip", duel.transform, "Toucher l'écran pour passer", 26, UIKit.TextDim);
            UIKit.Stretch(skip.rectTransform, 20, 12, 20, 92);
            duelBox.SetActive(false);
        }

        void AddCard(RectTransform row, LegendsTool tool, string label, Color accent)
        {
            var b = UIKit.Button("Card" + tool, row, "", () =>
            {
                sfx.Play(Sfx.Click);
                if (!interaction.IsToolAvailable(tool) && interaction.Tool != tool) { sfx.Play(Sfx.Illegal, 0.5f); return; }
                interaction.ToggleTool(tool);
            }, accent, 26);
            var old = b.GetComponentInChildren<Text>();
            if (old != null) old.gameObject.SetActive(false);
            var card = new PowerCard { Button = b, Background = (Image)b.targetGraphic, Accent = accent };
            card.Name = UIKit.Label("Name", b.transform, label, 28, UIKit.Text, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Stretch(card.Name.rectTransform, 16, 56, 8, 14);
            card.State = UIKit.Label("State", b.transform, "", 22, UIKit.TextDim, TextAnchor.UpperLeft);
            UIKit.Stretch(card.State.rectTransform, 16, 10, 8, 66);
            card.State.horizontalOverflow = HorizontalWrapMode.Overflow;
            cards[tool] = card;
        }

        void OnViewPressed()
        {
            sfx.Play(Sfx.Click);
            if (match.Settings.Mode == MatchMode.LocalTwoPlayers) localViewSide = Piece.Opposite(localViewSide);
            orbit.ResetView(match.Settings.Mode == MatchMode.LocalTwoPlayers ? localViewSide : match.ViewSide);
            Refresh();
        }

        public void ResetCamera()
        {
            if (match.Game == null) return;
            orbit.ResetView(match.Settings.Mode == MatchMode.LocalTwoPlayers ? localViewSide : match.ViewSide);
        }

        /// <summary>Camp affiché en bas : le joueur contre l'IA, le côté de la vue en local.</summary>
        PieceColor BottomSide => match.Settings.Mode == MatchMode.VersusAI ? match.Settings.HumanColor : localViewSide;

        // ---------------------------------------------------------------- rafraîchissement

        public void Refresh()
        {
            var game = match.Game;
            if (game == null) return;
            var s = match.Settings;
            var pos = game.Position;

            var bottom = BottomSide;
            FillStrip(strips[1], bottom, pos, s, game);
            FillStrip(strips[0], Piece.Opposite(bottom), pos, s, game);

            title.color = UIKit.Text;
            subtitle.text = LastActionLine(game);
            if (game.IsOver) title.text = ResultTitle(game, s);
            else if (match.IsAnimating) { } // pendant l'animation, on n'annonce pas encore le tour suivant
            else if (match.IsAIThinking) title.text = "L'IA réfléchit…";
            else if (game.InCheck) { title.text = s.IsHuman(game.SideToMove) && s.Mode == MatchMode.VersusAI ? "Échec ! Parez-le" : "Échec !"; title.color = UIKit.Danger; }
            else if (s.Mode == MatchMode.VersusAI) title.text = s.IsHuman(game.SideToMove) ? "À vous de jouer" : "Au tour de l'IA";
            else title.text = game.SideToMove == PieceColor.White ? "Aux Blancs de jouer" : "Aux Noirs de jouer";

            RefreshAlert(pos);
            RefreshPowers(game);

            bool confirming = interaction.IsConfirming;
            confirmBox.SetActive(confirming);
            if (confirming) FillConfirm(game);

            undoButton.interactable = game.Actions.Count > 0 && !match.IsAnimating;
            hintButton.interactable = match.CanHumanAct;
        }

        void FillStrip(PlayerStrip strip, PieceColor side, FantasyPosition pos, MatchSettings s, FantasyGame game)
        {
            var accent = side == PieceColor.White ? UIKit.Cyan : UIKit.Magenta;
            string camp = side == PieceColor.White ? "Blancs" : "Noirs";
            strip.Name.text = s.Mode == MatchMode.LocalTwoPlayers ? camp
                : s.HumanColor == side ? "Vous · " + camp : "IA · " + MatchSettings.LevelName(s.Level) + " · " + camp;
            int e = pos.Energy(side);
            var cfg = pos.Config;
            bool full = e >= cfg.SummonCost && !pos.HasSummoned(side);
            strip.Energy.text = e + " / " + cfg.MaxEnergy + " éclats" + (pos.HasSummoned(side) ? " · dragon invoqué" : full ? " · dragon prêt" : "");
            strip.Energy.color = full ? Gold : UIKit.TextDim;
            float pulse = full ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 1f;
            for (int i = 0; i < strip.Gauge.Length; i++)
                strip.Gauge[i].color = i < e ? (full ? Gold : accent) * new Color(pulse, pulse, pulse, 0.95f) : new Color(1, 1, 1, 0.08f);
            bool toMove = game.SideToMove == side && !game.IsOver;
            strip.Accent.color = toMove ? accent : accent * new Color(1, 1, 1, 0.25f);
            strip.Panel.color = toMove ? new Color(0.08f, 0.10f, 0.18f, 0.95f) : SurfaceSoft;
        }

        static string LastActionLine(FantasyGame game)
        {
            var n = game.Notations;
            if (n.Count == 0) return "Les Blancs commencent.";
            int i = n.Count - 1;
            return "Dernier coup : " + (i / 2 + 1) + (i % 2 == 0 ? ". " : "… ") + n[i];
        }

        // ---------------------------------------------------------------- plateau vivant

        struct Alert { public int Urgency; public string Title, Detail; }

        void RefreshAlert(FantasyPosition pos)
        {
            var alerts = new List<Alert>();
            for (int i = 0; i < FantasyPosition.HoleSlotCount; i++)
            {
                var st = pos.HoleStateAt(i);
                if (st == HoleState.None) continue;
                string sq = Square.Name(pos.HoleSquare(i));
                int t = pos.HoleTimer(i);
                if (st == HoleState.Fissure)
                {
                    string who = pos[pos.HoleSquare(i)] != Piece.Empty ? " La pièce qui s'y trouve tombera." : " Une pièce qui s'y arrête tombera.";
                    alerts.Add(new Alert { Urgency = t, Title = "Fissure · " + sq, Detail = "S'effondre dans " + Plies(t) + "." + who });
                }
                else alerts.Add(new Alert { Urgency = 50 + t, Title = "Gouffre · " + sq, Detail = "Infranchissable pour s'arrêter, encore " + Plies(t) + "." });
            }
            if (pos.PortalState != PortalState.None)
            {
                string pair = Square.Name(pos.PortalA) + " ↔ " + Square.Name(pos.PortalB);
                if (pos.PortalState == PortalState.Announced)
                    alerts.Add(new Alert { Urgency = 20 + pos.PortalTimer, Title = "Portails · " + pair, Detail = "S'ouvrent dans " + Plies(pos.PortalTimer) + "." });
                else
                    alerts.Add(new Alert { Urgency = 60, Title = "Portails ouverts · " + pair, Detail = "Une pièce qui finit sur un anneau ressort par l'autre. Encore " + Plies(pos.PortalTimer) + "." });
            }
            if (pos.CrystalSquare != Square.None)
                alerts.Add(new Alert { Urgency = 70, Title = "Gisement · " + Square.Name(pos.CrystalSquare), Detail = "+" + pos.Config.CrystalGain + " éclats pour la première pièce qui s'y arrête." });

            alertBox.SetActive(alerts.Count > 0 && !IsDuelShowing);
            if (alerts.Count == 0) return;
            alerts.Sort((a, b) => a.Urgency.CompareTo(b.Urgency));
            alertTitle.text = alerts[0].Title;
            alertTitle.color = alerts[0].Title.StartsWith("Portails") ? Violet : alerts[0].Title.StartsWith("Gisement") ? Gold : Amber;
            var detail = new StringBuilder(alerts[0].Detail);
            if (alertExpanded)
                for (int i = 1; i < alerts.Count; i++) detail.Append('\n').Append(alerts[i].Title).Append(" — ").Append(alerts[i].Detail);
            alertDetail.text = detail.ToString();
            alertMore.text = alerts.Count > 1 ? (alertExpanded ? "masquer" : "+" + (alerts.Count - 1) + " autre" + (alerts.Count > 2 ? "s" : "")) : "";
            var rt = (RectTransform)alertBox.transform;
            rt.sizeDelta = new Vector2(1000, alertExpanded ? 96 + 40 * (alerts.Count - 1) : 96);
        }

        static string Plies(int n) => n + " demi-coup" + (n > 1 ? "s" : "");

        // ---------------------------------------------------------------- pouvoirs

        void RefreshPowers(FantasyGame game)
        {
            var pos = game.Position;
            var cfg = pos.Config;
            var side = game.SideToMove;
            bool humanTurn = match.CanHumanAct;
            int e = pos.Energy(side);
            bool dragon = false;
            for (int sq = 0; sq < 64 && !dragon; sq++) dragon = pos[sq] == Piece.Make(side, FantasyPiece.Dragon);

            foreach (var kv in cards)
            {
                var tool = kv.Key;
                var c = kv.Value;
                bool available = interaction.IsToolAvailable(tool);
                bool active = interaction.Tool == tool;
                string state;
                switch (tool)
                {
                    case LegendsTool.Shield: state = Cost(e, cfg.ShieldCost, available, "puis un coup"); break;
                    case LegendsTool.Freeze: state = Cost(e, cfg.FreezeCost, available, "puis un coup"); break;
                    case LegendsTool.Teleport: state = Cost(e, cfg.TeleportCost, available, "remplace le coup"); break;
                    case LegendsTool.Summon:
                        state = pos.HasSummoned(side) ? "déjà invoqué" : e < cfg.SummonCost ? e + " / " + cfg.SummonCost : available ? "prêt · puis un coup" : "aucune tour";
                        break;
                    default:
                        int cd = pos.BreathCooldown(side);
                        state = cd > 0 ? "recharge " + cd + " tour" + (cd > 1 ? "s" : "") : available ? "remplace le coup" : "aucune cible";
                        break;
                }
                c.State.text = state;
                c.Button.gameObject.SetActive(tool != LegendsTool.Breath || dragon);
                bool on = humanTurn && (available || active);
                c.Name.color = on ? UIKit.Text : UIKit.TextDim * new Color(1, 1, 1, 0.7f);
                c.State.color = active ? c.Accent : on ? UIKit.TextDim : UIKit.TextDim * new Color(1, 1, 1, 0.6f);
                c.Background.color = active ? c.Accent * new Color(0.42f, 0.42f, 0.42f, 1f) : on ? new Color(0.09f, 0.11f, 0.20f, 0.95f) : new Color(0.05f, 0.06f, 0.10f, 0.75f);
            }

            powerHint.text = ToolHint(interaction.Tool, interaction.IsTargeting, interaction.ArmedSquare);
            bool showPowers = humanTurn && !interaction.IsConfirming && !IsDuelShowing;
            powerGroup.SetActive(showPowers);
        }

        static string Cost(int energy, int cost, bool available, string effect) =>
            energy < cost ? energy + " / " + cost + " éclats" : available ? cost + " éclats · " + effect : "aucune cible";

        static string ToolHint(LegendsTool tool, bool targeting, int armed)
        {
            switch (tool)
            {
                case LegendsTool.Shield: return targeting ? "Touchez une de vos pièces à protéger (pas le roi)." : "Bouclier posé en " + Square.Name(armed) + " : jouez votre coup.";
                case LegendsTool.Freeze: return targeting ? "Touchez une pièce adverse : elle ne bougera pas à son prochain tour." : "Gel posé en " + Square.Name(armed) + " : jouez votre coup.";
                case LegendsTool.Summon: return targeting ? "Touchez une de vos tours : elle devient dragon." : "Dragon en " + Square.Name(armed) + " : jouez votre coup.";
                case LegendsTool.Teleport: return "Saut : choisissez une pièce, puis une case vide voisine. Termine votre tour.";
                case LegendsTool.Breath: return "Souffle : touchez le dragon puis sa cible. Remplace votre coup.";
                default: return "Saut et Souffle remplacent le coup. Bouclier, Gel et Dragon se jouent avant lui.";
            }
        }

        void FillConfirm(FantasyGame game)
        {
            var pos = game.Position;
            int sq = interaction.PendingSquare;
            int e = pos.Energy(game.SideToMove);
            string piece = PieceName(pos[sq]);
            switch (interaction.Tool)
            {
                case LegendsTool.Shield:
                    confirmTitle.text = "Bouclier · " + piece + " " + Square.Name(sq);
                    confirmCost.text = e + " → " + (e - pos.Config.ShieldCost) + " éclats";
                    confirmText.text = "Imprenable jusqu'au début de votre prochain tour (une chute dans un gouffre reste possible). Vous jouerez ensuite votre coup.";
                    UIKit.SetButtonText(confirmYes, "Poser le bouclier");
                    break;
                case LegendsTool.Freeze:
                    confirmTitle.text = "Gel · " + piece + " " + Square.Name(sq);
                    confirmCost.text = e + " → " + (e - pos.Config.FreezeCost) + " éclats";
                    confirmText.text = "Immobile à son prochain tour, mais elle contrôle toujours ses cases (le gel ne pare pas un échec). Vous jouerez ensuite votre coup.";
                    UIKit.SetButtonText(confirmYes, "Geler");
                    break;
                default:
                    confirmTitle.text = "Dragon · tour " + Square.Name(sq);
                    confirmCost.text = e + " → " + (e - pos.Config.SummonCost) + " éclats";
                    confirmText.text = "La tour devient un dragon : vol jusqu'à 3 cases, Souffle dès votre prochain tour. Une seule invocation par partie. Vous jouerez ensuite votre coup.";
                    UIKit.SetButtonText(confirmYes, "Invoquer");
                    break;
            }
        }

        static string PieceName(int piece)
        {
            switch (Piece.TypeOf(piece))
            {
                case PieceType.Pawn: return "Sentinelle";
                case PieceType.Knight: return "Chevalier";
                case PieceType.Bishop: return "Mage";
                case PieceType.Rook: return "Golem";
                case PieceType.Queen: return "Reine";
                case PieceType.King: return "Roi";
                case FantasyPiece.Dragon: return "Dragon";
                default: return "";
            }
        }

        // ---------------------------------------------------------------- duels

        bool IsDuelShowing => board != null && board.IsPlayingDuel;

        string Caption(ActionVisual v)
        {
            var a = v.Action;
            string mover = PieceName(v.MovedPiece);
            string gain = v.EnergyGained > 0 ? " · +" + v.EnergyGained + " éclat" + (v.EnergyGained > 1 ? "s" : "") : "";
            if (a.Kind == ActionKind.Breath) return "Le dragon " + Square.Name(a.From) + " brûle " + PieceName(v.CapturedPiece) + " " + Square.Name(a.To) + gain;
            if (a.Power == PowerKind.Summon && !v.IsCapture) return "Dragon invoqué en " + Square.Name(a.PowerSquare);
            if (v.IsCapture) return mover + " " + Square.Name(a.From) + " prend " + PieceName(v.CapturedPiece) + " " + Square.Name(v.CaptureSquare) + gain;
            return mover + " " + Square.Name(a.From) + " → " + Square.Name(a.To);
        }

        // ---------------------------------------------------------------- écrans

        GameObject Overlay(string name, out RectTransform card, float width, float height, Color accent, bool fullPage = false)
        {
            var root = UIKit.Rect(name, transform);
            UIKit.Stretch(root);
            root.gameObject.AddComponent<Image>().color = fullPage ? new Color(0.02f, 0.02f, 0.05f, 1f) : new Color(0.01f, 0.01f, 0.04f, 0.80f);
            var panel = UIKit.Panel("Card", root, fullPage ? new Color(0, 0, 0, 0) : new Color(0.03f, 0.03f, 0.07f, 0.98f), !fullPage, accent);
            UIKit.Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            card = panel.rectTransform;
            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        void ShowOverlay(GameObject overlay, bool show)
        {
            overlay.SetActive(show);
            match.Paused = setupOverlay.activeSelf || moreOverlay.activeSelf || endOverlay.activeSelf || confirmOverlay.activeSelf;
        }

        static Text Section(RectTransform parent, string text)
        {
            var t = UIKit.Label("Section", parent, text, 34, UIKit.Text, TextAnchor.LowerLeft, FontStyle.Bold);
            UIKit.Size(t, 54);
            return t;
        }

        static RectTransform Row(RectTransform parent, string name, float height)
        {
            var row = UIKit.Rect(name, parent);
            UIKit.HStack(row, 12);
            UIKit.Size(row.gameObject.AddComponent<LayoutElement>(), height);
            return row;
        }

        void BuildSetup()
        {
            setupOverlay = Overlay("LegendsSetup", out var card, 1000, 1820, UIKit.Cyan, true);
            UIKit.VStack(card, 10, 20);

            UIKit.Size(UIKit.Label("Brand", card, "KEVCHESS · LÉGENDES", 24, UIKit.TextDim, TextAnchor.LowerLeft, FontStyle.Bold), 40);
            UIKit.Size(UIKit.Label("Title", card, "Préparer la partie", 64, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold), 90);
            UIKit.Size(UIKit.Label("Sub", card, "Le cristal change les échecs. Le mat reste votre objectif.", 28, UIKit.TextDim, TextAnchor.UpperLeft), 50);

            Section(card, "Adversaire");
            var modes = Row(card, "Modes", 88);
            modeButtons = new[]
            {
                UIKit.Button("Two", modes, "À deux", () => { sfx.Play(Sfx.Click); setupMode = 0; RefreshSetup(); }, UIKit.TextDim, 32),
                UIKit.Button("AI", modes, "Contre l'IA", () => { sfx.Play(Sfx.Click); setupMode = 1; RefreshSetup(); }, UIKit.TextDim, 32),
            };

            var ai = UIKit.Rect("AIGroup", card);
            UIKit.VStack(ai, 10);
            UIKit.Size(ai.gameObject.AddComponent<LayoutElement>(), 54 + 88 + 54 + 88 + 30);
            aiGroup = ai.gameObject;
            var levelHead = Row(ai, "LevelHead", 54);
            Section(levelHead, "Niveau de l'IA");
            levelName = UIKit.Label("LevelName", levelHead, "", 30, UIKit.Cyan, TextAnchor.LowerRight);
            var levels = Row(ai, "Levels", 88);
            levelButtons = new Button[5];
            for (int i = 0; i < 5; i++)
            {
                var level = (AILevel)i;
                levelButtons[i] = UIKit.Button("Level" + i, levels, (i + 1).ToString(), () => { sfx.Play(Sfx.Click); setupLevel = level; RefreshSetup(); }, UIKit.TextDim, 32);
            }
            Section(ai, "Votre camp");
            var colors = Row(ai, "Colors", 88);
            string[] names = { "Blancs", "Noirs", "Hasard" };
            colorButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                colorButtons[i] = UIKit.Button("Color" + i, colors, names[i], () => { sfx.Play(Sfx.Click); setupColor = idx; RefreshSetup(); }, UIKit.TextDim, 30);
            }

            Section(card, "Duels de capture");
            var duels = Row(card, "Duels", 88);
            string[] duelNames = { "Longs · 2,5 s", "Courts", "Sans" };
            duelButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                var len = (DuelLength)i;
                duelButtons[i] = UIKit.Button("Duel" + i, duels, duelNames[i], () => { sfx.Play(Sfx.Click); LegendsBoardView.Duels = len; RefreshSetup(); }, UIKit.TextDim, 30);
            }
            UIKit.Size(UIKit.Label("DuelHint", card, "Touchez l'écran pendant un duel pour le passer.", 24, UIKit.TextDim, TextAnchor.UpperLeft), 40);

            var rules = UIKit.Panel("Rules", card, SurfaceSoft, true, Border);
            UIKit.Size(rules, 215);
            var rulesText = UIKit.Label("Text", rules.transform,
                "Les règles essentielles\n•  Un pouvoir (Bouclier, Gel), puis votre coup.\n•  Le Saut et le Souffle remplacent votre coup.\n•  À " + FantasyConfig.Default.SummonCost + " éclats, une tour devient dragon (une fois par partie).\n•  Fissures et portails sont annoncés avant d'agir.",
                28, UIKit.Text, TextAnchor.UpperLeft);
            UIKit.Stretch(rulesText.rectTransform, 30, 20, 30, 20);
            rulesText.lineSpacing = 1.15f;

                        var play = UIKit.Button("Play", card, "Commencer la partie →", StartFromSetup, UIKit.Cyan, 38);
            ((Image)play.targetGraphic).color = UIKit.Cyan * new Color(0.5f, 0.5f, 0.5f, 1f);
            UIKit.Size(play, 110);
            var row = Row(card, "Secondary", 96);
            resumeButton = UIKit.Button("Resume", row, "Reprendre la partie", () =>
            {
                sfx.Play(Sfx.Click);
                if (match.Game != null && !match.Game.IsOver) ShowOverlay(setupOverlay, false);
                else if (match.ResumeSaved()) ShowOverlay(setupOverlay, false);
            }, UIKit.TextDim, 30);
            UIKit.Button("Classic", row, "Échecs classiques", () => { sfx.Play(Sfx.Click); ShowOverlay(setupOverlay, false); exitToClassic(); }, UIKit.TextDim, 30);
            RefreshSetup();
        }

        void RefreshSetup()
        {
            for (int i = 0; i < modeButtons.Length; i++) Highlight(modeButtons[i], i == setupMode);
            aiGroup.SetActive(setupMode == 1);
            for (int i = 0; i < colorButtons.Length; i++) Highlight(colorButtons[i], i == setupColor);
            for (int i = 0; i < levelButtons.Length; i++) Highlight(levelButtons[i], i == (int)setupLevel);
            levelName.text = MatchSettings.LevelName(setupLevel);
            for (int i = 0; i < duelButtons.Length; i++) Highlight(duelButtons[i], i == (int)LegendsBoardView.Duels);
        }

        static void Highlight(Button b, bool on)
        {
            ((Image)b.targetGraphic).color = on ? new Color(0.10f, 0.32f, 0.38f, 1f) : new Color(0.07f, 0.08f, 0.14f, 0.95f);
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.color = on ? UIKit.Text : UIKit.TextDim;
        }

        void StartFromSetup()
        {
            sfx.Play(Sfx.Click);
            var settings = new MatchSettings { Mode = setupMode == 0 ? MatchMode.LocalTwoPlayers : MatchMode.VersusAI, Level = setupLevel };
            settings.HumanColor = setupColor == 2 ? (UnityEngine.Random.value < 0.5f ? PieceColor.White : PieceColor.Black) : (PieceColor)setupColor;
            interaction.Reset();
            match.StartNew(settings);
            orbit.ResetView(match.ViewSide);
            ShowOverlay(setupOverlay, false);
        }

        public void ShowSetup(bool show)
        {
            if (show)
            {
                bool inGame = match.Game != null && !match.Game.IsOver;
                resumeButton.gameObject.SetActive(inGame || LegendsController.HasSaved());
                UIKit.SetButtonText(resumeButton, inGame ? "Continuer la partie" : "Reprendre la partie");
                RefreshSetup();
            }
            ShowOverlay(setupOverlay, show);
        }

        void BuildMore()
        {
            moreOverlay = Overlay("LegendsMore", out var card, 860, 760, UIKit.Cyan);
            UIKit.VStack(card, 18, 44);
            UIKit.Size(UIKit.Label("Title", card, "Partie", 56, UIKit.Text, TextAnchor.MiddleLeft, FontStyle.Bold), 80);
            UIKit.Size(UIKit.Button("Setup", card, "Nouvelle partie / réglages", () => { sfx.Play(Sfx.Click); ShowOverlay(moreOverlay, false); ShowSetup(true); }), 110);
            UIKit.Size(UIKit.Button("Resign", card, "Abandonner", () =>
            {
                sfx.Play(Sfx.Click);
                ShowOverlay(moreOverlay, false);
                Confirm("Abandonner la partie ?", () => match.Resign());
            }, UIKit.Magenta), 110);
            UIKit.Size(UIKit.Button("Classic", card, "Échecs classiques", () => { sfx.Play(Sfx.Click); ShowOverlay(moreOverlay, false); exitToClassic(); }, UIKit.TextDim), 100);
            UIKit.Size(UIKit.Button("Close", card, "Fermer", () => { sfx.Play(Sfx.Click); ShowOverlay(moreOverlay, false); }, UIKit.TextDim), 100);
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
                string label = a.Promotion == PieceType.Queen ? "Reine" : a.Promotion == PieceType.Rook ? "Golem" : a.Promotion == PieceType.Bishop ? "Mage" : "Chevalier";
                UIKit.Button("Promo" + a.Promotion, promoRow, label, () =>
                {
                    sfx.Play(Sfx.Click);
                    promoOverlay.SetActive(false);
                    interaction.OnPromotionChosen(action, from);
                }, Gold, 30);
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
            endOverlay = Overlay("LegendsEnd", out var card, 900, 640, UIKit.Cyan);
            UIKit.VStack(card, 22, 48);
            endTitle = UIKit.Label("Title", card, "", 76, UIKit.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Size(endTitle, 110);
            endReason = UIKit.Label("Reason", card, "", 38, UIKit.TextDim);
            UIKit.Size(endReason, 60);
            UIKit.Size(UIKit.Button("Replay", card, "Rejouer", () => { sfx.Play(Sfx.Click); ShowOverlay(endOverlay, false); match.StartNew(match.Settings); }, UIKit.Cyan), 110);
            UIKit.Size(UIKit.Button("Review", card, "Voir le plateau", () => { sfx.Play(Sfx.Click); ShowOverlay(endOverlay, false); }, UIKit.TextDim), 100);
            UIKit.Size(UIKit.Button("ToMenu", card, "Réglages", () => { sfx.Play(Sfx.Click); ShowOverlay(endOverlay, false); ShowSetup(true); }, UIKit.TextDim), 100);
        }

        void OnGameEnded()
        {
            var game = match.Game;
            endTitle.text = ResultTitle(game, match.Settings);
            endReason.text = ReasonText(game.EndReason);
            bool won = match.Settings.Mode == MatchMode.LocalTwoPlayers || game.Result == GameResult.Draw
                       || (game.Result == GameResult.WhiteWins) == (match.Settings.HumanColor == PieceColor.White);
            sfx.Play(game.Result == GameResult.Draw ? Sfx.Draw : won ? Sfx.Victory : Sfx.Defeat);
            ShowOverlay(endOverlay, true);
            Refresh();
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

        void BuildConfirmDialog()
        {
            confirmOverlay = Overlay("LegendsConfirm", out var card, 860, 420, UIKit.Magenta);
            UIKit.VStack(card, 22, 48);
            confirmQuestion = UIKit.Label("Text", card, "", 44, UIKit.Text);
            UIKit.Size(confirmQuestion, 140);
            var row = Row(card, "Row", 110);
            UIKit.Button("No", row, "Non", () => { sfx.Play(Sfx.Click); ShowOverlay(confirmOverlay, false); }, UIKit.TextDim);
            UIKit.Button("Yes", row, "Oui", () => { sfx.Play(Sfx.Click); ShowOverlay(confirmOverlay, false); confirmAction?.Invoke(); }, UIKit.Magenta);
        }

        void Confirm(string text, Action onYes)
        {
            confirmQuestion.text = text;
            confirmAction = onYes;
            ShowOverlay(confirmOverlay, true);
        }

        public void HandleBack()
        {
            if (promoOverlay.activeSelf) return;
            if (confirmOverlay.activeSelf) { ShowOverlay(confirmOverlay, false); return; }
            if (moreOverlay.activeSelf) { ShowOverlay(moreOverlay, false); return; }
            if (endOverlay.activeSelf) { ShowOverlay(endOverlay, false); return; }
            if (interaction.IsConfirming || interaction.Tool != LegendsTool.None) { interaction.Reset(); return; }
            if (setupOverlay.activeSelf)
            {
                if (match.Game != null && !match.Game.IsOver) ShowOverlay(setupOverlay, false);
                return;
            }
            ShowOverlay(moreOverlay, true);
        }

        bool duelWasShowing;
        Rect lastFrame;
        readonly Vector3[] corners = new Vector3[4];

        /// <summary>
        /// Zone libre de l'écran pour le plateau 3D : entre l'alerte (en haut) et votre camp (en bas).
        /// La caméra d'orbite s'y cadre exactement.
        /// </summary>
        public void ApplyBoardFrame(bool force = false)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Canvas.ForceUpdateCanvases();
            ((RectTransform)alertBox.transform).GetWorldCorners(corners);
            float top = corners[0].y;
            strips[1].Panel.rectTransform.GetWorldCorners(corners);
            float bottom = corners[1].y;
            float h = Screen.height;
            var frame = new Rect(0.03f, bottom / h + 0.01f, 0.94f, Mathf.Max(0.2f, (top - bottom) / h - 0.02f));
            if (!force && Mathf.Abs(frame.y - lastFrame.y) < 0.002f && Mathf.Abs(frame.height - lastFrame.height) < 0.002f) return;
            lastFrame = frame;
            if (Screen.width > Screen.height) orbit.LandscapeFrame = new Rect(0.22f, frame.y, 0.56f, frame.height);
            else orbit.PortraitFrame = frame;
            orbit.RefitNow();
        }

        void Update()
        {
            UpdateLayout();
            ApplyBoardFrame();
            if (match?.Game == null) return;

            // Pendant un duel : la légende remplace les commandes ; le tour suivant n'est annoncé qu'après.
            bool duel = IsDuelShowing;
            if (duel != duelWasShowing)
            {
                duelWasShowing = duel;
                duelBox.SetActive(duel);
                commandBar.SetActive(!duel);
                Refresh();
            }
            if (duel) duelText.text = duelCaption;

            var pos = match.Game.Position;
            if (pos.Energy(PieceColor.White) >= pos.Config.SummonCost || pos.Energy(PieceColor.Black) >= pos.Config.SummonCost)
            {
                var bottom = BottomSide;
                FillStrip(strips[1], bottom, pos, match.Settings, match.Game);
                FillStrip(strips[0], Piece.Opposite(bottom), pos, match.Settings, match.Game);
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
