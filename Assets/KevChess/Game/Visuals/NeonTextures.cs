using UnityEngine;

namespace KevChess.Game.Visuals
{
    /// <summary>
    /// Petites textures générées par code (halo, anneau, cadre, grille, ciel nébuleuse).
    /// Toutes sont mises en cache et créées à la première demande.
    /// </summary>
    public static class NeonTextures
    {
        static Texture2D _radial, _ring, _frame, _softSquare, _softLine, _grid;
        static Cubemap _sky;

        /// <summary>Halo radial doux (blanc, alpha dégressif).</summary>
        public static Texture2D RadialGlow => _radial != null ? _radial : (_radial = Make("KevChess_RadialGlow", 64, (u, v) =>
        {
            float d = Mathf.Clamp01(Dist(u, v) * 2f);
            float a = (1f - d);
            return a * a * (0.65f + 0.35f * a);
        }));

        /// <summary>Anneau fin lumineux avec un halo léger.</summary>
        public static Texture2D Ring => _ring != null ? _ring : (_ring = Make("KevChess_Ring", 128, (u, v) =>
        {
            float d = Dist(u, v) * 2f; // 0 au centre, 1 au bord
            float core = Mathf.Exp(-Sq((d - 0.74f) / 0.06f));
            float halo = Mathf.Exp(-Sq((d - 0.74f) / 0.18f)) * 0.35f;
            return Mathf.Clamp01(core + halo) * (d < 1f ? 1f : 0f);
        }));

        /// <summary>Cadre carré lumineux (case sélectionnée) avec remplissage très léger.</summary>
        public static Texture2D SquareFrame => _frame != null ? _frame : (_frame = Make("KevChess_SquareFrame", 128, (u, v) =>
        {
            float e = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f; // 0 centre, 1 bord
            float line = Mathf.Exp(-Sq((e - 0.86f) / 0.045f));
            float halo = Mathf.Exp(-Sq((e - 0.86f) / 0.16f)) * 0.3f;
            float fill = 0.10f * (1f - e);
            return Mathf.Clamp01(line + halo + fill) * (e < 1f ? 1f : 0f);
        }));

        /// <summary>Carré plein aux bords adoucis (dernier coup).</summary>
        public static Texture2D SoftSquare => _softSquare != null ? _softSquare : (_softSquare = Make("KevChess_SoftSquare", 64, (u, v) =>
        {
            float e = Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f;
            return Mathf.Clamp01((1f - e) * 6f) * 0.55f + Mathf.Exp(-Sq((e - 0.9f) / 0.05f)) * 0.45f;
        }));

        /// <summary>Bande lumineuse : cœur net au milieu de v, halo doux, constante selon u (traînées, flèche).</summary>
        public static Texture2D SoftLine => _softLine != null ? _softLine : (_softLine = Make("KevChess_SoftLine", 64, (u, v) =>
        {
            float d = Mathf.Abs(v - 0.5f) * 2f; // 0 au centre, 1 au bord
            return Mathf.Clamp01(Mathf.Exp(-Sq(d / 0.22f)) + Mathf.Exp(-Sq(d / 0.6f)) * 0.4f) * Mathf.Clamp01((1f - d) * 8f);
        }));

        /// <summary>Grille fine répétable (une cellule par unité) pour le sol de l'abîme sous le plateau.</summary>
        public static Texture2D Grid
        {
            get
            {
                if (_grid != null) return _grid;
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "KevChess_Grid", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                    float du = Mathf.Min(u, 1f - u), dv = Mathf.Min(v, 1f - v);
                    float line = Mathf.Max(Mathf.Exp(-Sq(du / 0.012f)), Mathf.Exp(-Sq(dv / 0.012f)));
                    float glow = Mathf.Max(Mathf.Exp(-Sq(du / 0.06f)), Mathf.Exp(-Sq(dv / 0.06f))) * 0.25f;
                    // Petits points aux intersections.
                    float dot = Mathf.Exp(-(Sq(du) + Sq(dv)) / Sq(0.03f));
                    byte c = (byte)(Mathf.Clamp01(line + glow + dot) * 255f);
                    px[y * size + x] = new Color32(c, c, c, 255);
                }
                tex.SetPixels32(px);
                tex.Apply(true, false);
                _grid = tex;
                return tex;
            }
        }

        /// <summary>
        /// Ciel « nébuleuse » sombre (cubemap) : dégradé indigo, volutes cyan/magenta, étoiles.
        /// Sert de skybox et de réflexion d'environnement pour le cristal.
        /// </summary>
        public static Cubemap NebulaSky
        {
            get
            {
                if (_sky != null) return _sky;
                const int size = 128;
                var cube = new Cubemap(size, TextureFormat.RGBA32, true) { name = "KevChess_NebulaSky", filterMode = FilterMode.Trilinear };
                var px = new Color[size * size];
                for (int f = 0; f < 6; f++)
                {
                    var face = (CubemapFace)f;
                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float sc = (x + 0.5f) / size * 2f - 1f;
                        float tc = (y + 0.5f) / size * 2f - 1f; // ligne 0 = haut de la face
                        Vector3 d = FaceDirection(face, sc, tc).normalized;
                        px[y * size + x] = SkyColor(d, f * 7919 + y * size + x);
                    }
                    cube.SetPixels(px, face);
                }
                cube.Apply(true, false);
                _sky = cube;
                return cube;
            }
        }

        static Vector3 FaceDirection(CubemapFace face, float sc, float tc)
        {
            switch (face)
            {
                case CubemapFace.PositiveX: return new Vector3(1f, -tc, -sc);
                case CubemapFace.NegativeX: return new Vector3(-1f, -tc, sc);
                case CubemapFace.PositiveY: return new Vector3(sc, 1f, tc);
                case CubemapFace.NegativeY: return new Vector3(sc, -1f, -tc);
                case CubemapFace.PositiveZ: return new Vector3(sc, -tc, 1f);
                default: return new Vector3(-sc, -tc, -1f);
            }
        }

        static readonly Color SkyLow = new Color(0.004f, 0.003f, 0.012f);
        static readonly Color SkyHorizon = new Color(0.030f, 0.018f, 0.070f);
        static readonly Color SkyHigh = new Color(0.010f, 0.016f, 0.045f);
        static readonly Color NebulaCyan = new Color(0.05f, 0.30f, 0.42f);
        static readonly Color NebulaMagenta = new Color(0.40f, 0.06f, 0.36f);

        static Color SkyColor(Vector3 d, int seed)
        {
            // Dégradé vertical avec une bande plus claire à l'horizon.
            float h = d.y;
            Color c = h > 0f ? Color.Lerp(SkyHorizon, SkyHigh, Mathf.Sqrt(h)) : Color.Lerp(SkyHorizon, SkyLow, Mathf.Sqrt(-h));

            // Volutes : bruit fractal continu sur la sphère.
            float n1 = Fbm(d * 2.2f + new Vector3(3.1f, 0.7f, 5.3f));
            float n2 = Fbm(d * 3.1f + new Vector3(-4.2f, 2.9f, 1.1f));
            float maskCyan = Mathf.Clamp01(Vector3.Dot(d, new Vector3(-0.55f, 0.35f, 0.76f).normalized) * 0.8f + 0.3f);
            float maskMag = Mathf.Clamp01(Vector3.Dot(d, new Vector3(0.7f, 0.25f, -0.66f).normalized) * 0.8f + 0.3f);
            float upper = Mathf.Clamp01(h * 2.5f + 0.6f);
            c += NebulaCyan * (Mathf.Pow(Mathf.Clamp01(n1 * 1.6f - 0.45f), 2.2f) * maskCyan * upper);
            c += NebulaMagenta * (Mathf.Pow(Mathf.Clamp01(n2 * 1.6f - 0.45f), 2.2f) * maskMag * upper);

            // Étoiles : rares points, plus denses dans l'hémisphère haut.
            uint hash = Hash((uint)seed);
            float r = (hash & 0xFFFF) / 65535f;
            if (h > -0.15f && r > 0.9965f)
            {
                float b = ((hash >> 16) & 0xFF) / 255f;
                c += Color.Lerp(new Color(0.6f, 0.75f, 1f), new Color(1f, 0.8f, 0.95f), b) * (0.35f + 0.65f * b);
            }
            c.a = 1f;
            return c;
        }

        static float Fbm(Vector3 p)
        {
            float sum = 0f, amp = 0.5f;
            for (int i = 0; i < 4; i++)
            {
                sum += amp * Noise3(p);
                p = p * 2.03f + new Vector3(1.7f, 9.2f, 3.4f);
                amp *= 0.5f;
            }
            return sum / 0.9375f;
        }

        // Bruit 3D approximatif à partir de trois bruits de Perlin 2D (suffisant pour un ciel).
        static float Noise3(Vector3 p)
        {
            float a = Mathf.PerlinNoise(p.x + 17.3f, p.y + 4.1f);
            float b = Mathf.PerlinNoise(p.y + 31.7f, p.z + 8.9f);
            float c = Mathf.PerlinNoise(p.z + 2.3f, p.x + 13.7f);
            return (a + b + c) / 3f;
        }

        static uint Hash(uint x)
        {
            x ^= x >> 16; x *= 0x7feb352d;
            x ^= x >> 15; x *= 0x846ca68b;
            x ^= x >> 16;
            return x;
        }

        // ------------------------------------------------------------------ utilitaires

        delegate float AlphaFunc(float u, float v);

        static Texture2D Make(string name, int size, AlphaFunc alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float a = Mathf.Clamp01(alpha((x + 0.5f) / size, (y + 0.5f) / size));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply(true, true); // non lisible ensuite : économise la mémoire CPU
            return tex;
        }

        static float Dist(float u, float v) => Mathf.Sqrt(Sq(u - 0.5f) + Sq(v - 0.5f));
        static float Sq(float x) => x * x;
    }
}
