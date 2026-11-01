using System;
using System.Collections.Generic;
using KevChess.Core;
using UnityEngine;

namespace KevChess.Game.Match
{
    public enum MatchMode { LocalTwoPlayers, VersusAI, Puzzle }

    public enum AILevel { Beginner = 0, Easy = 1, Medium = 2, Strong = 3, Master = 4 }

    [Serializable]
    public sealed class MatchSettings
    {
        public MatchMode Mode = MatchMode.LocalTwoPlayers;
        public PieceColor HumanColor = PieceColor.White;
        public AILevel Level = AILevel.Medium;
        // Cadence en secondes (0 = sans limite). Absente des anciennes sauvegardes : elles restent sans limite.
        public int ClockBase;
        public int ClockIncrement;
        /// <summary>Identifiant du puzzle joué (mode Puzzle seulement).</summary>
        public string PuzzleId;

        public TimeControl Clock
        {
            get => new TimeControl(ClockBase, ClockIncrement);
            set { ClockBase = value.BaseSeconds; ClockIncrement = value.IncrementSeconds; }
        }

        public bool IsHuman(PieceColor color) => Mode == MatchMode.LocalTwoPlayers || color == HumanColor;

        public static string LevelName(AILevel level)
        {
            switch (level)
            {
                case AILevel.Beginner: return "Débutant";
                case AILevel.Easy: return "Facile";
                case AILevel.Medium: return "Moyen";
                case AILevel.Strong: return "Fort";
                default: return "Maître";
            }
        }
    }

    /// <summary>Sauvegarde de la partie en cours (réglages + coups UCI) dans PlayerPrefs.</summary>
    [Serializable]
    public sealed class SavedMatch
    {
        const string Key = "kevchess.save.v1";

        public MatchSettings Settings = new MatchSettings();
        public string StartFen = Position.StartFen;
        public List<string> UciMoves = new List<string>();
        // Temps restants de la pendule ; HasClock est faux dans les sauvegardes d'avant l'horloge.
        public bool HasClock;
        public double RemainingWhite;
        public double RemainingBlack;

        public static void Store(MatchSettings settings, ChessGame game, ChessClock clock = null)
        {
            if (settings.Mode == MatchMode.Puzzle) return; // un puzzle n'écrase jamais la partie sauvegardée
            var save = new SavedMatch { Settings = settings, StartFen = game.StartFen };
            foreach (var m in game.Moves) save.UciMoves.Add(m.ToUci());
            if (clock != null && !clock.IsUnlimited)
            {
                save.HasClock = true;
                save.RemainingWhite = clock.RemainingWhite;
                save.RemainingBlack = clock.RemainingBlack;
            }
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }

        public static SavedMatch Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return null;
            try { return JsonUtility.FromJson<SavedMatch>(PlayerPrefs.GetString(Key)); }
            catch (Exception) { return null; }
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        /// <summary>Pendule de la partie reprise : temps sauvegardés, ou temps de base pour une ancienne sauvegarde.</summary>
        public ChessClock RebuildClock()
        {
            var clock = new ChessClock(Settings.Clock);
            if (HasClock && !clock.IsUnlimited)
            {
                clock.RemainingWhite = Math.Max(0.5, RemainingWhite);
                clock.RemainingBlack = Math.Max(0.5, RemainingBlack);
            }
            return clock;
        }

        /// <summary>Rejoue la partie sauvegardée ; null si elle est corrompue.</summary>
        public ChessGame Rebuild()
        {
            try
            {
                var game = new ChessGame(StartFen);
                foreach (var uci in UciMoves)
                {
                    var m = Notation.Parse(game.Position, uci);
                    if (m.IsNull || !game.TryMakeMove(m)) return null;
                }
                return game;
            }
            catch (Exception)
            {
                return null; // sauvegarde corrompue : on repart d'une partie neuve
            }
        }
    }
}
