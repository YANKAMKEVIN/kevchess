using System;
using System.Collections.Generic;
using KevChess.Core;
using UnityEngine;

namespace KevChess.Game.Match
{
    public enum MatchMode { LocalTwoPlayers, VersusAI }

    public enum AILevel { Beginner = 0, Easy = 1, Medium = 2, Strong = 3, Master = 4 }

    [Serializable]
    public sealed class MatchSettings
    {
        public MatchMode Mode = MatchMode.LocalTwoPlayers;
        public PieceColor HumanColor = PieceColor.White;
        public AILevel Level = AILevel.Medium;

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

        public static void Store(MatchSettings settings, ChessGame game)
        {
            var save = new SavedMatch { Settings = settings, StartFen = game.StartFen };
            foreach (var m in game.Moves) save.UciMoves.Add(m.ToUci());
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
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
