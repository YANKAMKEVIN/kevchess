using System;
using System.Collections.Generic;
using KevChess.Core;
using KevChess.Core.Fantasy;
using KevChess.Game.Audio;
using UnityEngine;

namespace KevChess.Game.Legends
{
    /// <summary>Outil choisi dans la barre des pouvoirs.</summary>
    public enum LegendsTool { None, Shield, Freeze, Summon, Teleport, Breath }

    /// <summary>Ce que l'interaction attend de la vue du plateau Légendes.</summary>
    public interface ILegendsBoard
    {
        bool TryPick(Ray ray, out int pieceSquare, out int boardSquare);
        int SquareUnder(Ray ray);
        void ShowSelection(int square, IReadOnlyList<FantasyAction> targets);
        void ShowPowerTargets(IEnumerable<int> squares, LegendsTool tool);
        void ShowArmedPower(LegendsTool tool, int square);
        void ClearSelection();
        void DragPiece(int square, Ray ray);
        void ReturnPiece(int square);
    }

    /// <summary>
    /// Toucher et glisser dans le mode Légendes. Un pouvoir se joue en deux temps : choisir l'outil
    /// (Bouclier, Gel, Invocation), toucher sa cible, puis jouer son coup normalement. Téléportation et
    /// Souffle changent seulement les cases proposées à la pièce sélectionnée.
    /// </summary>
    public sealed class LegendsInteraction
    {
        readonly LegendsController match;
        readonly ILegendsBoard board;
        readonly SoundBank sfx;
        readonly Camera camera;
        readonly Func<bool> blocked;
        readonly Action<int, int, IReadOnlyList<FantasyAction>> askPromotion;

        int selected = Square.None;
        readonly List<FantasyAction> targets = new List<FantasyAction>();
        int dragFrom = Square.None;
        bool dragStarted;
        bool awaitingPromotion;

        public LegendsTool Tool { get; private set; }
        /// <summary>Case visée par le pouvoir armé (Bouclier, Gel, Invocation), Square.None tant qu'elle n'est pas choisie.</summary>
        public int ArmedSquare { get; private set; } = Square.None;
        public bool IsTargeting => IsTargetedTool(Tool) && ArmedSquare == Square.None;
        public event Action Changed;

        public LegendsInteraction(LegendsController match, ILegendsBoard board, SoundBank sfx, Camera camera,
            Func<bool> blocked, Action<int, int, IReadOnlyList<FantasyAction>> askPromotion)
        {
            this.match = match;
            this.board = board;
            this.sfx = sfx;
            this.camera = camera;
            this.blocked = blocked;
            this.askPromotion = askPromotion;
            match.StateChanged += () => { if (!match.CanHumanAct) Reset(); };
        }

        static bool IsTargetedTool(LegendsTool t) => t == LegendsTool.Shield || t == LegendsTool.Freeze || t == LegendsTool.Summon;

        static PowerKind PowerOf(LegendsTool t) =>
            t == LegendsTool.Shield ? PowerKind.Shield : t == LegendsTool.Freeze ? PowerKind.Freeze : t == LegendsTool.Summon ? PowerKind.Summon : PowerKind.None;

        /// <summary>L'outil est-il utilisable maintenant (au moins une action légale) ?</summary>
        public bool IsToolAvailable(LegendsTool tool)
        {
            if (!match.CanHumanAct) return false;
            foreach (var a in match.Game.LegalActions)
            {
                switch (tool)
                {
                    case LegendsTool.Teleport: if (a.Kind == ActionKind.Teleport) return true; break;
                    case LegendsTool.Breath: if (a.Kind == ActionKind.Breath) return true; break;
                    default: if (a.Power == PowerOf(tool)) return true; break;
                }
            }
            return false;
        }

        /// <summary>Bouton de la barre des pouvoirs : choisit l'outil, ou l'annule s'il est déjà choisi.</summary>
        public void ToggleTool(LegendsTool tool)
        {
            if (!match.CanHumanAct) return;
            ClearSelection();
            if (Tool == tool || tool == LegendsTool.None || !IsToolAvailable(tool))
            {
                Tool = LegendsTool.None;
                ArmedSquare = Square.None;
                board.ClearSelection();
                Changed?.Invoke();
                return;
            }
            Tool = tool;
            ArmedSquare = Square.None;
            sfx.Play(Sfx.Select, 0.8f);
            Haptics.Select();
            if (IsTargetedTool(tool)) board.ShowPowerTargets(match.Game.PowerTargets(PowerOf(tool)), tool);
            else board.ClearSelection();
            Changed?.Invoke();
        }

        public void Reset()
        {
            ClearSelection();
            if (Tool == LegendsTool.None && ArmedSquare == Square.None) return;
            Tool = LegendsTool.None;
            ArmedSquare = Square.None;
            Changed?.Invoke();
        }

        // ---------------------------------------------------------------- glisser-déposer

        public bool CanStartDrag(Vector2 screenPos)
        {
            dragFrom = Square.None;
            dragStarted = false;
            if (awaitingPromotion || blocked() || !match.CanHumanAct || IsTargeting) return false;
            if (!board.TryPick(camera.ScreenPointToRay(screenPos), out int ps, out int bs)) return false;
            int sq = ps != Square.None ? ps : bs;
            if (sq == Square.None || !Piece.IsColor(match.Game.Position[sq], match.Game.SideToMove)) return false;
            dragFrom = sq;
            return true;
        }

        public void OnDrag(Vector2 screenPos)
        {
            if (dragFrom == Square.None || !match.CanHumanAct) return;
            if (!dragStarted)
            {
                dragStarted = true;
                if (selected != dragFrom) Select(dragFrom);
            }
            // Le dragon qui souffle ne bouge pas : pas de glisser en mode Souffle.
            if (Tool != LegendsTool.Breath) board.DragPiece(dragFrom, camera.ScreenPointToRay(screenPos));
        }

        public void OnDrop(Vector2 screenPos)
        {
            int from = dragFrom;
            dragFrom = Square.None;
            if (from == Square.None || !dragStarted) return;
            dragStarted = false;
            int to = board.SquareUnder(camera.ScreenPointToRay(screenPos));
            if (match.CanHumanAct && TryTarget(to)) return;
            board.ReturnPiece(from);
            if (to != from && to != Square.None) Illegal();
        }

        public void OnDragCancelled()
        {
            if (dragFrom != Square.None && dragStarted) board.ReturnPiece(dragFrom);
            dragFrom = Square.None;
            dragStarted = false;
        }

        // ---------------------------------------------------------------- toucher

        public bool OnTap(Vector2 screenPos)
        {
            if (awaitingPromotion || blocked()) return true;
            if (!board.TryPick(camera.ScreenPointToRay(screenPos), out int ps, out int bs))
            {
                ClearSelection();
                return false;
            }
            if (!match.CanHumanAct) return true;
            var game = match.Game;

            if (IsTargeting)
            {
                var power = PowerOf(Tool);
                foreach (int candidate in new[] { ps, bs })
                {
                    if (candidate == Square.None) continue;
                    foreach (int t in game.PowerTargets(power))
                    {
                        if (t != candidate) continue;
                        ArmedSquare = t;
                        board.ShowArmedPower(Tool, t);
                        sfx.Play(Sfx.Select);
                        Haptics.Select();
                        Changed?.Invoke();
                        return true;
                    }
                }
                Illegal();
                return true;
            }

            if (selected != Square.None && (TryTarget(ps) || TryTarget(bs))) return true;

            int sq = ps != Square.None ? ps : bs;
            if (Piece.IsColor(game.Position[sq], game.SideToMove))
            {
                if (sq == selected) ClearSelection();
                else Select(sq);
                return true;
            }
            if (selected != Square.None) Illegal();
            ClearSelection();
            return true;
        }

        bool Matches(FantasyAction a)
        {
            switch (Tool)
            {
                case LegendsTool.Teleport: return a.Kind == ActionKind.Teleport;
                case LegendsTool.Breath: return a.Kind == ActionKind.Breath;
                case LegendsTool.None: return a.Kind == ActionKind.Normal && !a.HasPower;
                default: return a.Kind == ActionKind.Normal && a.Power == PowerOf(Tool) && a.PowerSquare == ArmedSquare;
            }
        }

        void Select(int square)
        {
            selected = square;
            targets.Clear();
            foreach (var a in match.Game.LegalActions)
                if (a.From == square && Matches(a)) targets.Add(a);
            board.ShowSelection(square, targets);
            if (IsTargetedTool(Tool) && ArmedSquare != Square.None) board.ShowArmedPower(Tool, ArmedSquare);
            sfx.Play(Sfx.Select, 0.7f);
            Haptics.Select();
        }

        bool TryTarget(int square)
        {
            if (square == Square.None || selected == Square.None) return false;
            var matching = new List<FantasyAction>(4);
            foreach (var a in targets) if (a.To == square) matching.Add(a);
            if (matching.Count == 0) return false;
            int from = selected;
            ClearSelection();
            if (matching.Count > 1 && matching[0].IsPromotion)
            {
                awaitingPromotion = true;
                askPromotion(from, square, matching);
                return true;
            }
            Play(matching[0]);
            return true;
        }

        /// <summary>Retour du choix de promotion (action nulle : annulé).</summary>
        public void OnPromotionChosen(FantasyAction action, int from)
        {
            awaitingPromotion = false;
            if (action.IsNull) { board.ReturnPiece(from); return; }
            Play(action);
        }

        void Play(FantasyAction a)
        {
            Tool = LegendsTool.None;
            ArmedSquare = Square.None;
            Changed?.Invoke();
            if (!match.TryHumanAction(a)) Illegal();
        }

        public void ClearSelection()
        {
            if (selected == Square.None && targets.Count == 0) return;
            selected = Square.None;
            targets.Clear();
            board.ClearSelection();
            if (IsTargetedTool(Tool) && ArmedSquare != Square.None) board.ShowArmedPower(Tool, ArmedSquare);
        }

        void Illegal()
        {
            sfx.Play(Sfx.Illegal, 0.6f);
            Haptics.Illegal();
        }
    }
}
