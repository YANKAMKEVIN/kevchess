using System;

namespace KevChess.Core.Ux
{
    /// <summary>
    /// Silhouette 2D d'un maillage projeté : triangles (u, v) remplis dans une image carrée, anticrénelés (4 × 4 échantillons).
    /// Sert aux vignettes de pièces tirées des maillages procéduraux, sans caméra ni rendu.
    /// </summary>
    public static class Silhouette
    {
        const int Samples = 4;

        /// <summary>
        /// Opacités (0–255) d'une image <paramref name="size"/> × <paramref name="size"/>, rangées ligne par ligne depuis le bas.
        /// La forme est mise à l'échelle sans déformation, centrée horizontalement et posée sur le bas,
        /// avec une marge relative <paramref name="margin"/> (0–0,4).
        /// </summary>
        public static byte[] Rasterize(float[] u, float[] v, int[] triangles, int size, float margin = 0.06f)
        {
            if (u == null || v == null || triangles == null) throw new ArgumentNullException();
            if (u.Length != v.Length) throw new ArgumentException("u et v doivent avoir la même longueur");
            var alpha = new byte[size * size];
            if (u.Length == 0 || triangles.Length < 3) return alpha;

            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (int i in triangles)
            {
                if (u[i] < minU) minU = u[i];
                if (u[i] > maxU) maxU = u[i];
                if (v[i] < minV) minV = v[i];
                if (v[i] > maxV) maxV = v[i];
            }
            float extent = Math.Max(maxU - minU, maxV - minV);
            if (extent <= 0f) return alpha;

            int n = size * Samples;
            float usable = n * (1f - 2f * margin);
            float scale = usable / extent;
            float offU = (n - (maxU - minU) * scale) * 0.5f - minU * scale;
            float offV = n * margin - minV * scale;

            var hit = new bool[n * n];
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                float ax = u[a] * scale + offU, ay = v[a] * scale + offV;
                float bx = u[b] * scale + offU, by = v[b] * scale + offV;
                float cx = u[c] * scale + offU, cy = v[c] * scale + offV;
                float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                if (Math.Abs(area) < 1e-6f) continue; // triangle vu par la tranche
                int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
                int x1 = Math.Min(n - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
                int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
                int y1 = Math.Min(n - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        if (hit[y * n + x]) continue;
                        float px = x + 0.5f, py = y + 0.5f;
                        float w0 = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
                        float w1 = (cx - bx) * (py - by) - (cy - by) * (px - bx);
                        float w2 = (ax - cx) * (py - cy) - (ay - cy) * (px - cx);
                        // Les deux sens de parcours : la silhouette ignore l'orientation des faces.
                        if ((w0 >= 0f && w1 >= 0f && w2 >= 0f) || (w0 <= 0f && w1 <= 0f && w2 <= 0f)) hit[y * n + x] = true;
                    }
            }

            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                {
                    int count = 0;
                    for (int sy = 0; sy < Samples; sy++)
                        for (int sx = 0; sx < Samples; sx++)
                            if (hit[(py * Samples + sy) * n + px * Samples + sx]) count++;
                    alpha[py * size + px] = (byte)(count * 255 / (Samples * Samples));
                }
            return alpha;
        }
    }
}
