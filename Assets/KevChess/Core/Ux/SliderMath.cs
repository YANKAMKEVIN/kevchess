using System;

namespace KevChess.Core.Ux
{
    /// <summary>Curseur horizontal : valeur 0–1 sous le doigt, arrondie à un pas, et libellé en pourcentage.</summary>
    public static class SliderMath
    {
        /// <summary>Valeur au point <paramref name="x"/> d'une piste [<paramref name="left"/>, <paramref name="right"/>], arrondie à <paramref name="step"/>.</summary>
        public static float ValueAt(float x, float left, float right, float step = 0.05f)
        {
            if (right <= left) return 0f;
            float v = (x - left) / (right - left);
            v = v < 0f ? 0f : v > 1f ? 1f : v;
            if (step > 0f) v = (float)Math.Round(v / step) * step;
            return v > 1f ? 1f : v;
        }

        /// <summary>« 45 % ».</summary>
        public static string Percent(float value) => (int)Math.Round(value * 100f) + " %";
    }
}
