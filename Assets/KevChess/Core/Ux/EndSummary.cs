using System;
using System.Collections.Generic;

namespace KevChess.Core.Ux
{
    /// <summary>Ligne de bilan de l'écran de fin : coups, durée, ouverture, bilan contre le niveau joué.</summary>
    public static class EndSummary
    {
        /// <summary>
        /// « 34 coups · 12 min · Défense sicilienne · Contre Moyen : 3 V · 1 N · 2 D ».
        /// Durée négative = inconnue (partie reprise), ouverture vide ou niveau null = omis.
        /// </summary>
        public static string Line(int plies, double seconds, string opening, string levelName = null, int wins = 0, int draws = 0, int losses = 0)
        {
            var parts = new List<string>();
            int moves = (plies + 1) / 2;
            parts.Add(moves + (moves > 1 ? " coups" : " coup"));
            if (seconds >= 0) parts.Add(Duration(seconds));
            if (!string.IsNullOrEmpty(opening)) parts.Add(opening);
            string line = string.Join(" · ", parts);
            if (levelName != null) line += "\nContre " + levelName + " : " + wins + " V · " + draws + " N · " + losses + " D";
            return line;
        }

        /// <summary>« moins d'1 min », « 12 min », « 1 h 05 ».</summary>
        public static string Duration(double seconds)
        {
            int minutes = (int)Math.Floor(seconds / 60.0);
            if (minutes < 1) return "moins d'1 min";
            if (minutes < 60) return minutes + " min";
            return minutes / 60 + " h " + (minutes % 60).ToString("00");
        }
    }

    /// <summary>
    /// Gerbe d'étincelles de victoire : trajectoires déterministes (graine) partant du centre,
    /// en unités d'interface, avec gravité et extinction.
    /// </summary>
    public static class SparkBurst
    {
        public const float Lifetime = 1.4f;
        const float Gravity = 900f;

        public struct Spark { public float X, Y, Alpha, Scale; }

        /// <summary>Étincelle <paramref name="index"/> à l'instant <paramref name="t"/> (secondes depuis l'éclat).</summary>
        public static Spark At(int index, float t, int seed = 7)
        {
            var rng = new Random(seed * 7919 + index * 104729);
            double angle = rng.NextDouble() * Math.PI * 2.0;
            float speed = 420f + (float)rng.NextDouble() * 520f;
            float life = Lifetime * (0.6f + 0.4f * (float)rng.NextDouble());
            float vx = (float)Math.Cos(angle) * speed;
            float vy = (float)Math.Sin(angle) * speed + 260f; // gerbe un peu vers le haut
            float k = t < 0f ? 0f : t;
            var s = new Spark
            {
                X = vx * k,
                Y = vy * k - 0.5f * Gravity * k * k,
                Alpha = k >= life ? 0f : 1f - k / life,
                Scale = 0.5f + 0.5f * (float)rng.NextDouble(),
            };
            return s;
        }
    }
}
