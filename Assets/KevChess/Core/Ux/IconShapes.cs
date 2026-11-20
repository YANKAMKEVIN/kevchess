using System;

namespace KevChess.Core.Ux
{
    /// <summary>Pictogrammes de l'interface, dessinés par code.</summary>
    public enum IconKind { Menu, Undo, Hint, View, More, List, Next }

    /// <summary>
    /// Formes des pictogrammes en champs de distance signée, dans le carré unité (x vers la droite, y vers le haut) :
    /// négatif à l'intérieur. L'interface en tire une texture blanche teintée ensuite.
    /// </summary>
    public static class IconShapes
    {
        const float Stroke = 0.075f; // demi-épaisseur des traits

        /// <summary>Distance signée au pictogramme au point (x, y) du carré unité.</summary>
        public static float Distance(IconKind kind, float x, float y)
        {
            switch (kind)
            {
                case IconKind.Menu:
                    return Min(Segment(x, y, 0.2f, 0.3f, 0.8f, 0.3f), Segment(x, y, 0.2f, 0.5f, 0.8f, 0.5f), Segment(x, y, 0.2f, 0.7f, 0.8f, 0.7f)) - Stroke;
                case IconKind.Undo:
                {
                    // Demi-cercle par le haut, puis pointe de flèche vers le bas à gauche.
                    float arc = Arc(x, y, 0.55f, 0.5f, 0.24f, 0f, 180f) - Stroke;
                    float tail = Segment(x, y, 0.79f, 0.5f, 0.79f, 0.24f) - Stroke;
                    float head = Min(Segment(x, y, 0.31f, 0.5f, 0.31f, 0.24f), Segment(x, y, 0.31f, 0.22f, 0.14f, 0.4f), Segment(x, y, 0.31f, 0.22f, 0.48f, 0.4f)) - Stroke;
                    return Min(arc, tail, head);
                }
                case IconKind.Hint:
                {
                    // Ampoule : globe plein, culot et filament.
                    float globe = Circle(x, y, 0.5f, 0.6f, 0.24f);
                    float neck = Box(x, y, 0.5f, 0.32f, 0.11f, 0.07f);
                    float bulb = Math.Min(globe, neck);
                    float foot = Segment(x, y, 0.42f, 0.16f, 0.58f, 0.16f) - 0.05f;
                    return Math.Min(bulb, foot);
                }
                case IconKind.View:
                {
                    // Œil : contour en amande (intersection de deux disques) et pupille pleine.
                    const float c = 0.2536f, r = 0.4736f;
                    float almond = Math.Max(Circle(x, y, 0.5f, 0.5f - c, r), Circle(x, y, 0.5f, 0.5f + c, r));
                    float outline = Math.Abs(almond) - Stroke * 0.8f;
                    float pupil = Circle(x, y, 0.5f, 0.5f, 0.12f);
                    return Math.Min(outline, pupil);
                }
                case IconKind.More:
                    return Min(Circle(x, y, 0.22f, 0.5f, 0.09f), Circle(x, y, 0.5f, 0.5f, 0.09f), Circle(x, y, 0.78f, 0.5f, 0.09f));
                case IconKind.List:
                {
                    float dots = Min(Circle(x, y, 0.2f, 0.3f, 0.065f), Circle(x, y, 0.2f, 0.5f, 0.065f), Circle(x, y, 0.2f, 0.7f, 0.065f));
                    float lines = Min(Segment(x, y, 0.38f, 0.3f, 0.82f, 0.3f), Segment(x, y, 0.38f, 0.5f, 0.82f, 0.5f), Segment(x, y, 0.38f, 0.7f, 0.82f, 0.7f)) - Stroke * 0.8f;
                    return Math.Min(dots, lines);
                }
                case IconKind.Next:
                {
                    float shaft = Segment(x, y, 0.18f, 0.5f, 0.78f, 0.5f);
                    float head = Math.Min(Segment(x, y, 0.8f, 0.5f, 0.57f, 0.27f), Segment(x, y, 0.8f, 0.5f, 0.57f, 0.73f));
                    return Math.Min(shaft, head) - Stroke;
                }
                default:
                    return 1f;
            }
        }

        /// <summary>Couverture 0–1 d'un pixel de taille <paramref name="pixel"/> (unités du carré) centré en (x, y), anticrénelée.</summary>
        public static float Coverage(IconKind kind, float x, float y, float pixel)
        {
            float d = Distance(kind, x, y);
            float a = 0.5f - d / pixel;
            return a < 0f ? 0f : a > 1f ? 1f : a;
        }

        /// <summary>Opacités (0–255) d'une image carrée de <paramref name="size"/> pixels, rangées ligne par ligne depuis le bas.</summary>
        public static byte[] Rasterize(IconKind kind, int size)
        {
            var alpha = new byte[size * size];
            float pixel = 1f / size;
            for (int py = 0; py < size; py++)
                for (int px = 0; px < size; px++)
                    alpha[py * size + px] = (byte)Math.Round(Coverage(kind, (px + 0.5f) * pixel, (py + 0.5f) * pixel, pixel) * 255f);
            return alpha;
        }

        // ---------------------------------------------------------------- Primitives

        static float Min(float a, float b, float c) => Math.Min(a, Math.Min(b, c));

        static float Circle(float x, float y, float cx, float cy, float r) => Length(x - cx, y - cy) - r;

        static float Box(float x, float y, float cx, float cy, float hx, float hy)
        {
            float dx = Math.Abs(x - cx) - hx, dy = Math.Abs(y - cy) - hy;
            float outside = Length(Math.Max(dx, 0f), Math.Max(dy, 0f));
            return outside + Math.Min(Math.Max(dx, dy), 0f);
        }

        /// <summary>Distance au segment [a, b] (sans épaisseur).</summary>
        static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float px = x - ax, py = y - ay, vx = bx - ax, vy = by - ay;
            float h = (px * vx + py * vy) / (vx * vx + vy * vy);
            h = h < 0f ? 0f : h > 1f ? 1f : h;
            return Length(px - vx * h, py - vy * h);
        }

        /// <summary>Distance à l'arc de cercle entre deux angles (degrés, sens trigonométrique), sans épaisseur.</summary>
        static float Arc(float x, float y, float cx, float cy, float r, float fromDeg, float toDeg)
        {
            float angle = (float)(Math.Atan2(y - cy, x - cx) * 180.0 / Math.PI);
            if (angle < 0f) angle += 360f;
            if (angle >= fromDeg && angle <= toDeg) return Math.Abs(Length(x - cx, y - cy) - r);
            double a0 = fromDeg * Math.PI / 180.0, a1 = toDeg * Math.PI / 180.0;
            float d0 = Length(x - (cx + r * (float)Math.Cos(a0)), y - (cy + r * (float)Math.Sin(a0)));
            float d1 = Length(x - (cx + r * (float)Math.Cos(a1)), y - (cy + r * (float)Math.Sin(a1)));
            return Math.Min(d0, d1);
        }

        static float Length(float x, float y) => (float)Math.Sqrt(x * x + y * y);
    }
}
