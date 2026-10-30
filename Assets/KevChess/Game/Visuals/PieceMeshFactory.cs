using System.Collections.Generic;
using UnityEngine;

namespace KevChess.Game.Visuals
{
    /// <summary>
    /// Génère par code les maillages des six pièces, en style « cristal taillé » :
    /// facettes plates, anneaux décalés en quinconce, et un second sous-maillage (index 1)
    /// pour les accents néon (bague de pied, couronnes, gemmes, croix…).
    /// Repère : pivot au centre du pied (y = 0), la pièce regarde vers +Z.
    /// Une case = 1 unité ; hauteurs de 0.64 (pion) à 1.15 (roi).
    /// </summary>
    public static class PieceMeshFactory
    {
        /// <summary>Sous-maillage du corps cristal.</summary>
        public const int BodySubMesh = 0;
        /// <summary>Sous-maillage des accents lumineux.</summary>
        public const int AccentSubMesh = 1;

        const int Body = BodySubMesh;
        const int Accent = AccentSubMesh;

        static readonly Dictionary<PieceKind, Mesh> Cache = new Dictionary<PieceKind, Mesh>();
        static Mesh _shard;

        /// <summary>Maillage partagé (mis en cache) de la pièce demandée. Ne pas le détruire.</summary>
        public static Mesh Get(PieceKind kind)
        {
            if (Cache.TryGetValue(kind, out Mesh mesh) && mesh != null)
                return mesh;
            mesh = Build(kind).ToMesh("KevChess_" + kind);
            Cache[kind] = mesh;
            return mesh;
        }

        /// <summary>Hauteur approximative de la pièce (utile pour placer les effets).</summary>
        public static float Height(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Pawn: return 0.645f;
                case PieceKind.Rook: return 0.80f;
                case PieceKind.Knight: return 0.88f;
                case PieceKind.Bishop: return 0.95f;
                case PieceKind.Queen: return 1.07f;
                default: return 1.15f;
            }
        }

        /// <summary>Petit éclat de cristal (bipyramide allongée) pour les effets de capture.</summary>
        public static Mesh Shard()
        {
            if (_shard != null) return _shard;
            var b = new MeshBuilder(1);
            b.GemAlong(0, Vector3.zero, new Vector3(0.2f, 1f, 0.1f), 0.32f, 1f, 4);
            _shard = b.ToMesh("KevChess_Shard");
            return _shard;
        }

        /// <summary>Vide le cache (les maillages existants restent valides pour les objets qui les utilisent).</summary>
        public static void ClearCache()
        {
            Cache.Clear();
        }

        internal static MeshBuilder Build(PieceKind kind)
        {
            switch (kind)
            {
                case PieceKind.Pawn: return BuildPawn();
                case PieceKind.Knight: return BuildKnight();
                case PieceKind.Bishop: return BuildBishop();
                case PieceKind.Rook: return BuildRook();
                case PieceKind.Queen: return BuildQueen();
                default: return BuildKing();
            }
        }

        // ------------------------------------------------------------------ socle commun

        /// <summary>Pied commun : disque biseauté avec une bague néon.</summary>
        static void AddBase(List<ProfilePoint> p, float r)
        {
            p.Add(new ProfilePoint(0f, 0f));
            p.Add(new ProfilePoint(r, 0f));
            p.Add(new ProfilePoint(r, 0.028f, 0f, Accent)); // bague lumineuse
            p.Add(new ProfilePoint(r, 0.062f));
            p.Add(new ProfilePoint(r * 0.94f, 0.095f));
            p.Add(new ProfilePoint(r * 0.78f, 0.12f));
        }

        // ------------------------------------------------------------------ pion

        static MeshBuilder BuildPawn()
        {
            var p = new List<ProfilePoint>();
            AddBase(p, 0.30f);
            p.Add(new ProfilePoint(0.17f, 0.16f, 0.5f));
            p.Add(new ProfilePoint(0.125f, 0.26f, 0f));
            p.Add(new ProfilePoint(0.10f, 0.33f, 0.5f));
            p.Add(new ProfilePoint(0.165f, 0.355f, 0f, Accent)); // collerette lumineuse
            p.Add(new ProfilePoint(0.165f, 0.38f, 0f));
            p.Add(new ProfilePoint(0.10f, 0.40f, 0.5f));
            // Tête : boule taillée.
            p.Add(new ProfilePoint(0.13f, 0.43f, 0f));
            p.Add(new ProfilePoint(0.165f, 0.49f, 0.5f));
            p.Add(new ProfilePoint(0.16f, 0.55f, 0f));
            p.Add(new ProfilePoint(0.12f, 0.605f, 0.5f));
            p.Add(new ProfilePoint(0.06f, 0.635f, 0f));
            p.Add(new ProfilePoint(0f, 0.645f, 0.5f));

            var b = new MeshBuilder(2);
            b.Lathe(p, 10, Vector3.zero);
            return b;
        }

        // ------------------------------------------------------------------ tour

        static MeshBuilder BuildRook()
        {
            var p = new List<ProfilePoint>();
            AddBase(p, 0.34f);
            p.Add(new ProfilePoint(0.22f, 0.17f, 0.5f));
            p.Add(new ProfilePoint(0.20f, 0.30f, 0f));
            p.Add(new ProfilePoint(0.19f, 0.45f, 0.5f));
            p.Add(new ProfilePoint(0.21f, 0.54f, 0f));
            p.Add(new ProfilePoint(0.25f, 0.58f, 0f));
            p.Add(new ProfilePoint(0.26f, 0.60f, 0f, Accent)); // bandeau néon sous les créneaux
            p.Add(new ProfilePoint(0.26f, 0.625f, 0f));
            p.Add(new ProfilePoint(0.26f, 0.70f, 0f));         // mur de la couronne
            p.Add(new ProfilePoint(0.18f, 0.70f, 0f));         // chemin de ronde
            p.Add(new ProfilePoint(0.18f, 0.655f, 0f, Accent)); // fond lumineux, visible de dessus
            p.Add(new ProfilePoint(0f, 0.655f, 0f));

            var b = new MeshBuilder(2);
            b.Lathe(p, 12, Vector3.zero);

            // Créneaux : 6 merlons en secteurs d'anneau, légèrement taillés vers le haut.
            const int merlons = 6;
            float sector = Mathf.PI * 2f / merlons;
            float half = sector * 0.30f;
            for (int i = 0; i < merlons; i++)
            {
                float a = i * sector + sector * 0.5f;
                float a0 = a - half, a1 = a + half;
                float t0 = a - half * 0.82f, t1 = a + half * 0.82f;
                var c = new Vector3[8];
                c[0] = Polar(0.18f, a0, 0.699f);
                c[1] = Polar(0.26f, a0, 0.699f);
                c[2] = Polar(0.26f, a1, 0.699f);
                c[3] = Polar(0.18f, a1, 0.699f);
                c[4] = Polar(0.19f, t0, 0.80f);
                c[5] = Polar(0.25f, t0, 0.80f);
                c[6] = Polar(0.25f, t1, 0.80f);
                c[7] = Polar(0.19f, t1, 0.80f);
                b.Hexahedron(Body, c);
            }
            return b;
        }

        static Vector3 Polar(float r, float angle, float y)
        {
            return new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
        }

        // ------------------------------------------------------------------ fou

        static MeshBuilder BuildBishop()
        {
            var p = new List<ProfilePoint>();
            AddBase(p, 0.32f);
            p.Add(new ProfilePoint(0.17f, 0.17f, 0.5f));
            p.Add(new ProfilePoint(0.12f, 0.30f, 0f));
            p.Add(new ProfilePoint(0.09f, 0.45f, 0.5f));
            p.Add(new ProfilePoint(0.15f, 0.48f, 0f, Accent)); // collerette
            p.Add(new ProfilePoint(0.15f, 0.505f, 0f));
            p.Add(new ProfilePoint(0.09f, 0.53f, 0.5f));
            // Mitre en goutte taillée.
            int mitreStart = p.Count;
            p.Add(new ProfilePoint(0.12f, 0.57f, 0f));
            p.Add(new ProfilePoint(0.16f, 0.64f, 0.5f));
            p.Add(new ProfilePoint(0.165f, 0.70f, 0f));
            p.Add(new ProfilePoint(0.14f, 0.77f, 0.5f));
            p.Add(new ProfilePoint(0.09f, 0.835f, 0f));
            p.Add(new ProfilePoint(0.035f, 0.865f, 0.5f));
            int mitreEnd = p.Count - 1;
            // Perle sommitale.
            p.Add(new ProfilePoint(0.035f, 0.875f, 0f));
            p.Add(new ProfilePoint(0.055f, 0.89f, 0f));
            p.Add(new ProfilePoint(0.06f, 0.915f, 0.5f));
            p.Add(new ProfilePoint(0.04f, 0.94f, 0f));
            p.Add(new ProfilePoint(0f, 0.95f, 0.5f));

            var b = new MeshBuilder(2);
            b.Lathe(p, 12, Vector3.zero);

            // Fente de la mitre : lame lumineuse inclinée qui épouse exactement la section
            // de la mitre (+ un léger débord), ce qui dessine une entaille néon en diagonale.
            Vector3 center = new Vector3(0f, 0.715f, 0.02f);
            Quaternion tilt = Quaternion.AngleAxis(-38f, Vector3.right); // plan qui monte vers l'avant
            Vector3 axisU = tilt * Vector3.right;   // horizontal dans le plan
            Vector3 axisV = tilt * Vector3.forward; // incliné dans le plan
            Vector3 normal = tilt * Vector3.up;
            const int seg = 20;
            var outline = new Vector3[seg];
            for (int i = 0; i < seg; i++)
            {
                float ang = i * Mathf.PI * 2f / seg;
                Vector3 dir = axisU * Mathf.Cos(ang) + axisV * Mathf.Sin(ang);
                float t = 0f;
                for (int s = 0; s < 80; s++)
                {
                    Vector3 q = center + dir * (t + 0.005f);
                    float r = ProfileRadius(p, mitreStart, mitreEnd, q.y);
                    if (new Vector2(q.x, q.z).magnitude > r) break;
                    t += 0.005f;
                }
                outline[i] = center + dir * (t + 0.008f);
            }
            const float slitHalf = 0.016f;
            for (int i = 0; i < seg; i++)
            {
                Vector3 a = outline[i], c = outline[(i + 1) % seg];
                Vector3 up = normal * slitHalf;
                // Bord extérieur de la lame.
                b.QuadFacing(Accent, a - up, c - up, c + up, a + up, (a + c) * 0.5f - center);
                // Faces haute et basse de la lame (visibles là où elle dépasse).
                b.TriFacing(Accent, center + up, a + up, c + up, normal);
                b.TriFacing(Accent, center - up, a - up, c - up, -normal);
            }
            return b;
        }

        /// <summary>Rayon du profil à la hauteur y, sur une portion monotone du profil.</summary>
        static float ProfileRadius(List<ProfilePoint> p, int from, int to, float y)
        {
            if (y <= p[from].Y) return p[from].R;
            for (int i = from; i < to; i++)
            {
                if (y >= p[i].Y && y <= p[i + 1].Y)
                {
                    float t = Mathf.InverseLerp(p[i].Y, p[i + 1].Y, y);
                    // Rayon inscrit du polygone (les facettes sont en retrait par rapport aux sommets).
                    return Mathf.Lerp(p[i].R, p[i + 1].R, t) * 0.96f;
                }
            }
            return p[to].R * 0.96f;
        }

        // ------------------------------------------------------------------ dame

        static MeshBuilder BuildQueen()
        {
            var p = new List<ProfilePoint>();
            AddBase(p, 0.35f);
            p.Add(new ProfilePoint(0.19f, 0.17f, 0.5f));
            p.Add(new ProfilePoint(0.13f, 0.32f, 0f));
            p.Add(new ProfilePoint(0.10f, 0.50f, 0.5f));
            p.Add(new ProfilePoint(0.11f, 0.60f, 0f));
            p.Add(new ProfilePoint(0.17f, 0.63f, 0f, Accent));
            p.Add(new ProfilePoint(0.17f, 0.655f, 0f));
            p.Add(new ProfilePoint(0.11f, 0.68f, 0.5f));
            // Coupe de la couronne.
            p.Add(new ProfilePoint(0.14f, 0.74f, 0f));
            p.Add(new ProfilePoint(0.20f, 0.84f, 0.5f));
            p.Add(new ProfilePoint(0.235f, 0.895f, 0f, Accent)); // bandeau du diadème
            p.Add(new ProfilePoint(0.235f, 0.915f, 0f));
            p.Add(new ProfilePoint(0.20f, 0.915f, 0f));
            // Dôme intérieur.
            p.Add(new ProfilePoint(0.15f, 0.935f, 0.5f));
            p.Add(new ProfilePoint(0.09f, 0.975f, 0f));
            p.Add(new ProfilePoint(0.04f, 0.995f, 0.5f));
            p.Add(new ProfilePoint(0f, 1.0f, 0f));

            var b = new MeshBuilder(2);
            b.Lathe(p, 18, Vector3.zero);

            // Pointes de la couronne, légèrement évasées, chacune coiffée d'une gemme.
            const int spikes = 9;
            float step = Mathf.PI * 2f / spikes;
            for (int i = 0; i < spikes; i++)
            {
                float a = i * step;
                Vector3 l = Polar(0.232f, a - step * 0.36f, 0.91f);
                Vector3 r = Polar(0.232f, a + step * 0.36f, 0.91f);
                Vector3 inner = Polar(0.19f, a, 0.91f);
                Vector3 apex = Polar(0.262f, a, 1.005f);
                Vector3 interior = (l + r + inner + apex) * 0.25f;
                b.TriOut(Body, l, r, apex, interior);
                b.TriOut(Body, r, inner, apex, interior);
                b.TriOut(Body, inner, l, apex, interior);
                b.TriOut(Body, l, r, inner, interior);
                b.Gem(Accent, apex + Vector3.up * 0.012f, 0.02f, 0.022f, 4, a);
            }
            // Gemme centrale.
            b.Gem(Accent, new Vector3(0f, 1.035f, 0f), 0.045f, 0.035f, 6);
            return b;
        }

        // ------------------------------------------------------------------ roi

        static MeshBuilder BuildKing()
        {
            var p = new List<ProfilePoint>();
            AddBase(p, 0.36f);
            p.Add(new ProfilePoint(0.20f, 0.17f, 0.5f));
            p.Add(new ProfilePoint(0.14f, 0.33f, 0f));
            p.Add(new ProfilePoint(0.11f, 0.52f, 0.5f));
            p.Add(new ProfilePoint(0.12f, 0.63f, 0f));
            p.Add(new ProfilePoint(0.18f, 0.66f, 0f, Accent));
            p.Add(new ProfilePoint(0.18f, 0.685f, 0f));
            p.Add(new ProfilePoint(0.12f, 0.71f, 0.5f));
            // Couronne droite et taillée.
            p.Add(new ProfilePoint(0.15f, 0.76f, 0f));
            p.Add(new ProfilePoint(0.205f, 0.85f, 0.5f));
            p.Add(new ProfilePoint(0.215f, 0.90f, 0f, Accent));
            p.Add(new ProfilePoint(0.215f, 0.925f, 0f));
            p.Add(new ProfilePoint(0.19f, 0.935f, 0f));
            p.Add(new ProfilePoint(0.15f, 0.97f, 0.5f));
            p.Add(new ProfilePoint(0.08f, 1.0f, 0f));
            p.Add(new ProfilePoint(0.04f, 1.01f, 0.5f));
            p.Add(new ProfilePoint(0f, 1.012f, 0f));

            var b = new MeshBuilder(2);
            b.Lathe(p, 16, Vector3.zero);

            // Fleurons verticaux sur le pourtour de la couronne.
            const int ribs = 8;
            for (int i = 0; i < ribs; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / ribs;
                Vector3 c = Polar(0.205f, a, 0.875f);
                b.Box(Body, c, new Vector3(0.03f, 0.10f, 0.03f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 45f, 0f));
            }

            // Croix stylisée, en losanges taillés, tournée vers l'avant (+Z).
            b.Box(Accent, new Vector3(0f, 1.075f, 0f), new Vector3(0.05f, 0.15f, 0.05f), Quaternion.Euler(0f, 45f, 0f));
            b.Box(Accent, new Vector3(0f, 1.09f, 0f), new Vector3(0.14f, 0.045f, 0.045f), Quaternion.Euler(45f, 0f, 0f));
            b.Gem(Accent, new Vector3(0f, 1.09f, 0f), 0.04f, 0.04f, 4, Mathf.PI * 0.25f);
            b.Gem(Accent, new Vector3(0f, 1.155f, 0f), 0.022f, 0.02f, 4);
            return b;
        }

        // ------------------------------------------------------------------ cavalier

        // Profil de tête de cheval (u = avant/Z, v = hauteur/Y). Dessin original.
        static readonly Vector2[] HorseProfile =
        {
            new Vector2(-0.20f, 0.10f), // arrière bas
            new Vector2(0.20f, 0.10f),  // avant bas
            new Vector2(0.19f, 0.22f),  // poitrail
            new Vector2(0.13f, 0.36f),
            new Vector2(0.10f, 0.45f),  // gorge
            new Vector2(0.16f, 0.50f),  // ganache
            new Vector2(0.27f, 0.53f),  // menton
            new Vector2(0.315f, 0.575f),// bout du museau
            new Vector2(0.31f, 0.635f), // naseau
            new Vector2(0.24f, 0.70f),  // chanfrein
            new Vector2(0.14f, 0.77f),  // front
            new Vector2(0.09f, 0.81f),
            new Vector2(0.06f, 0.88f),  // pointe de l'oreille
            new Vector2(0.00f, 0.80f),
            new Vector2(-0.09f, 0.75f), // nuque
            new Vector2(-0.17f, 0.63f), // encolure
            new Vector2(-0.21f, 0.46f),
            new Vector2(-0.23f, 0.29f),
            new Vector2(-0.22f, 0.16f),
        };

        // Crinière en dents de cristal, qui dépasse derrière l'encolure.
        static readonly Vector2[] ManeProfile =
        {
            new Vector2(-0.05f, 0.785f),
            new Vector2(-0.13f, 0.77f),
            new Vector2(-0.13f, 0.71f),
            new Vector2(-0.215f, 0.67f),
            new Vector2(-0.195f, 0.60f),
            new Vector2(-0.265f, 0.53f),
            new Vector2(-0.23f, 0.46f),
            new Vector2(-0.275f, 0.37f),
            new Vector2(-0.235f, 0.31f),
            new Vector2(-0.19f, 0.31f),
            new Vector2(-0.17f, 0.50f),
            new Vector2(-0.13f, 0.63f),
            new Vector2(-0.07f, 0.72f),
        };

        static MeshBuilder BuildKnight()
        {
            var p = new List<ProfilePoint>();
            AddBase(p, 0.34f);
            p.Add(new ProfilePoint(0.25f, 0.135f, 0.5f));
            p.Add(new ProfilePoint(0f, 0.135f, 0f));

            var b = new MeshBuilder(2);
            b.Lathe(p, 12, Vector3.zero);

            const float half = 0.105f;
            b.ExtrudeBeveled(Body, HorseProfile, half, 0.035f, Vector3.zero);
            b.ExtrudeBeveled(Accent, ManeProfile, 0.035f, 0.012f, Vector3.zero);

            // Yeux : petites gemmes lumineuses de part et d'autre.
            b.GemAlong(Accent, new Vector3(half - 0.002f, 0.685f, 0.13f), new Vector3(0.15f, 0.15f, 1f), 0.022f, 0.04f, 4);
            b.GemAlong(Accent, new Vector3(-half + 0.002f, 0.685f, 0.13f), new Vector3(-0.15f, 0.15f, 1f), 0.022f, 0.04f, 4);
            // Naseau.
            b.Gem(Accent, new Vector3(0f, 0.60f, 0.312f), 0.025f, 0.02f, 4);
            return b;
        }
    }
}
