using System.Collections.Generic;
using KevChess.Core;
using KevChess.Game.Match;
using KevChess.Game.View;
using KevChess.Game.Visuals;
using UnityEngine;

namespace KevChess.Game.UI
{
    /// <summary>
    /// Réglages du joueur persistés dans PlayerPrefs (thème, lisibilité, notation, animations, caméra),
    /// statistiques contre l'IA et progression des puzzles.
    /// </summary>
    public static class GamePrefs
    {
        const string ThemeKey = "kevchess.theme";
        const string ContrastKey = "kevchess.highcontrast";
        const string NotationKey = "kevchess.notation.fr";
        const string AnimKey = "kevchess.animspeed";
        const string FlipKey = "kevchess.autoflip";
        const string ClockAIKey = "kevchess.clock.ai";
        const string ClockLocalKey = "kevchess.clock.local";
        const string PuzzlesKey = "kevchess.puzzles.solved";
        const string StatsKey = "kevchess.stats.";

        public static readonly string[] AnimationNames = { "lente", "normale", "rapide" };
        static readonly float[] AnimationSpeeds = { 0.5f, 1f, 2f };

        static HashSet<string> solved;

        /// <summary>Applique les réglages enregistrés (au démarrage, une fois la scène construite).</summary>
        public static void ApplyAll()
        {
            VisualTheme.Apply(Theme);
            VisualTheme.HighContrast = HighContrast;
            BoardView.AnimationSpeed = AnimationSpeeds[AnimationIndex];
        }

        public static int Theme
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ThemeKey, 0), 0, VisualTheme.Names.Length - 1);
            set { SetInt(ThemeKey, value); VisualTheme.Apply(value); }
        }

        public static bool HighContrast
        {
            get => PlayerPrefs.GetInt(ContrastKey, 0) == 1;
            set { SetInt(ContrastKey, value ? 1 : 0); VisualTheme.HighContrast = value; }
        }

        /// <summary>Notation française (R, D, T, F, C) ; vrai par défaut.</summary>
        public static bool FrenchNotation
        {
            get => PlayerPrefs.GetInt(NotationKey, 1) == 1;
            set => SetInt(NotationKey, value ? 1 : 0);
        }

        /// <summary>0 lente, 1 normale, 2 rapide.</summary>
        public static int AnimationIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(AnimKey, 1), 0, AnimationSpeeds.Length - 1);
            set
            {
                int v = Mathf.Clamp(value, 0, AnimationSpeeds.Length - 1);
                SetInt(AnimKey, v);
                BoardView.AnimationSpeed = AnimationSpeeds[v];
            }
        }

        /// <summary>Partie à deux : la caméra se tourne vers le camp au trait après chaque coup.</summary>
        public static bool AutoFlip
        {
            get => PlayerPrefs.GetInt(FlipKey, 0) == 1;
            set => SetInt(FlipKey, value ? 1 : 0);
        }

        /// <summary>Index de la dernière cadence choisie dans TimeControl.Presets.</summary>
        public static int ClockPreset(bool versusAI) =>
            Mathf.Clamp(PlayerPrefs.GetInt(versusAI ? ClockAIKey : ClockLocalKey, 0), 0, TimeControl.Presets.Length - 1);

        public static void SetClockPreset(bool versusAI, int index) => SetInt(versusAI ? ClockAIKey : ClockLocalKey, index);

        /// <summary>SAN dans la notation choisie.</summary>
        public static string San(string san) => Notation.Localize(san, FrenchNotation);

        // ---------------------------------------------------------------- Puzzles

        static HashSet<string> Solved
        {
            get
            {
                if (solved != null) return solved;
                solved = new HashSet<string>();
                foreach (var id in PlayerPrefs.GetString(PuzzlesKey, "").Split(','))
                    if (id.Length > 0) solved.Add(id);
                return solved;
            }
        }

        public static bool IsPuzzleSolved(string id) => Solved.Contains(id);
        public static int SolvedCount => Solved.Count;

        public static void MarkPuzzleSolved(string id)
        {
            if (!Solved.Add(id)) return;
            PlayerPrefs.SetString(PuzzlesKey, string.Join(",", Solved));
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- Statistiques

        public struct Record { public int Wins, Draws, Losses; }

        public static Record Stats(AILevel level)
        {
            var parts = PlayerPrefs.GetString(StatsKey + (int)level, "0,0,0").Split(',');
            var r = new Record();
            if (parts.Length == 3)
            {
                int.TryParse(parts[0], out r.Wins);
                int.TryParse(parts[1], out r.Draws);
                int.TryParse(parts[2], out r.Losses);
            }
            return r;
        }

        /// <summary>Enregistre un résultat contre l'IA : +1 victoire, 0 nulle, -1 défaite.</summary>
        public static void RecordResult(AILevel level, int outcome)
        {
            var r = Stats(level);
            if (outcome > 0) r.Wins++;
            else if (outcome < 0) r.Losses++;
            else r.Draws++;
            PlayerPrefs.SetString(StatsKey + (int)level, r.Wins + "," + r.Draws + "," + r.Losses);
            PlayerPrefs.Save();
        }

        static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
        }
    }
}
