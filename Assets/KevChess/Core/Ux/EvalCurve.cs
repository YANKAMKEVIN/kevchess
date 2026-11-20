using System;
using System.Collections.Generic;

namespace KevChess.Core.Ux
{
    /// <summary>
    /// Courbe d'avantage de l'analyse : évaluation après chaque demi-coup (vue des Blancs), ramenée dans ]-1, 1[,
    /// puis tracée dans une petite image (zones blanche / noire, trait, ligne médiane).
    /// </summary>
    public static class EvalCurve
    {
        /// <summary>Centipions qui donnent une demi-hauteur : au-delà, la courbe s'aplatit (un mat reste près du bord).</summary>
        public const float HalfScaleCp = 400f;

        public const byte Empty = 0, WhiteArea = 1, BlackArea = 2, Line = 3, Middle = 4;

        /// <summary>Évaluation ramenée dans ]-1, 1[ (+ = avantage blanc).</summary>
        public static float Normalize(int cp) => cp / (Math.Abs(cp) + HalfScaleCp);

        /// <summary>
        /// Points de la courbe : index 0 = position de départ, index k = après k demi-coups.
        /// <paramref name="evalBeforeFirst"/> = évaluation de départ, <paramref name="evalsAfter"/> = après chaque coup.
        /// </summary>
        public static float[] Points(int evalBeforeFirst, IReadOnlyList<int> evalsAfter)
        {
            var p = new float[evalsAfter.Count + 1];
            p[0] = Normalize(evalBeforeFirst);
            for (int i = 0; i < evalsAfter.Count; i++) p[i + 1] = Normalize(evalsAfter[i]);
            return p;
        }

        /// <summary>Abscisse 0–1 du point d'index <paramref name="index"/> sur <paramref name="count"/> points.</summary>
        public static float X(int index, int count) => count <= 1 ? 0.5f : (float)index / (count - 1);

        /// <summary>Index du point le plus proche de l'abscisse 0–1 touchée (= nombre de demi-coups de la position à revoir).</summary>
        public static int IndexAt(float x01, int count)
        {
            if (count <= 1) return 0;
            x01 = x01 < 0f ? 0f : x01 > 1f ? 1f : x01;
            return (int)Math.Round(x01 * (count - 1));
        }

        /// <summary>Image <paramref name="width"/> × <paramref name="height"/> (lignes depuis le bas) : codes Empty, WhiteArea, BlackArea, Line, Middle.</summary>
        public static byte[] Rasterize(float[] points, int width, int height)
        {
            var img = new byte[width * height];
            if (points == null || points.Length == 0) return img;
            float mid = (height - 1) * 0.5f;
            int midRow = (int)Math.Round(mid);
            float prevY = mid + points[0] * mid;
            for (int x = 0; x < width; x++)
            {
                float t = width <= 1 ? 0f : (float)x / (width - 1) * (points.Length - 1);
                int i = Math.Min((int)t, points.Length - 1);
                int j = Math.Min(i + 1, points.Length - 1);
                float v = points[i] + (points[j] - points[i]) * (t - i);
                float y = mid + v * mid;
                for (int row = 0; row < height; row++)
                {
                    byte c = Empty;
                    if (row == midRow) c = Middle;
                    if (v > 0f && row > mid && row <= y) c = WhiteArea;
                    else if (v < 0f && row < mid && row >= y) c = BlackArea;
                    img[row * width + x] = c;
                }
                // Trait continu entre la colonne précédente et celle-ci, épais de 2 pixels.
                float lo = Math.Min(prevY, y) - 1f, hi = Math.Max(prevY, y) + 1f;
                for (int row = Math.Max(0, (int)Math.Floor(lo)); row <= Math.Min(height - 1, (int)Math.Ceiling(hi)); row++)
                    img[row * width + x] = Line;
                prevY = y;
            }
            return img;
        }
    }
}
