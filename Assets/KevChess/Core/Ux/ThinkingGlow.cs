using System;

namespace KevChess.Core.Ux
{
    /// <summary>
    /// Animation de la carte de l'IA qui réfléchit : reflet qui court sur le liseré (tour complet par période),
    /// pastille qui pulse, puis bref éclat quand le coup part.
    /// </summary>
    public static class ThinkingGlow
    {
        /// <summary>Durée d'un tour du reflet autour de la carte.</summary>
        public const float LapSeconds = 1.6f;
        public const float FlashSeconds = 0.45f;

        /// <summary>
        /// Point du périmètre d'un rectangle <paramref name="width"/> × <paramref name="height"/> centré sur l'origine,
        /// à la phase <paramref name="phase"/> (0–1, sens horaire depuis le coin haut gauche).
        /// </summary>
        public static void PerimeterPoint(float phase, float width, float height, out float x, out float y)
        {
            phase -= (float)Math.Floor(phase);
            float perimeter = 2f * (width + height);
            float d = phase * perimeter;
            float hw = width * 0.5f, hh = height * 0.5f;
            if (d < width) { x = -hw + d; y = hh; return; }
            d -= width;
            if (d < height) { x = hw; y = hh - d; return; }
            d -= height;
            if (d < width) { x = hw - d; y = -hh; return; }
            d -= width;
            x = -hw;
            y = -hh + d;
        }

        /// <summary>Échelle de la pastille qui pulse (1 ± 0,18, deux battements par tour).</summary>
        public static float DotScale(float time) => 1f + 0.18f * (float)Math.Sin(time * Math.PI * 2.0 * 2.0 / LapSeconds);

        /// <summary>Intensité 0–1 de l'éclat, <paramref name="sinceMove"/> secondes après le coup (0 hors de l'éclat).</summary>
        public static float Flash(float sinceMove)
        {
            if (sinceMove < 0f || sinceMove >= FlashSeconds) return 0f;
            float k = 1f - sinceMove / FlashSeconds;
            return k * k;
        }
    }
}
