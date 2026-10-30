using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals
{
    /// <summary>
    /// Construit le plateau 8x8 : cases en verre (sombre fumé / clair givré) légèrement taillées
    /// en pointe de diamant, lignes néon entre les cases, cadre épais biseauté avec liseré
    /// lumineux, coordonnées discrètes, et un « abîme » sous le verre (sol à grille lointaine).
    ///
    /// Les cases sont semi-transparentes : les pièces posent un reflet miroir sous la surface
    /// (voir <see cref="PieceView"/>), ce qui donne l'effet de plateau très réfléchissant
    /// sans coût de réflexion planaire.
    ///
    /// Repère : case (file, rank) → x = file - 3.5, z = rank - 3.5, y = 0. a1 = (0,0) côté blancs (-Z).
    /// </summary>
    public static class BoardBuilder
    {
        public const float SquareSize = 1f;
        /// <summary>Profondeur du sol de l'abîme sous la surface.</summary>
        public const float AbyssDepth = 1.35f;
        /// <summary>Largeur du cadre autour de l'aire de jeu.</summary>
        public const float FrameWidth = 0.5f;
        /// <summary>Hauteur du dessus du cadre.</summary>
        public const float FrameTop = 0.08f;

        const float Half = 4f;
        const float TileApex = 0.012f;

        /// <summary>Centre monde de la case (y = 0, surface du plateau).</summary>
        public static Vector3 SquareToWorld(int file, int rank)
        {
            return new Vector3(file - 3.5f, 0f, rank - 3.5f);
        }

        /// <summary>Case sous un point monde (seuls x et z comptent). Faux si hors plateau.</summary>
        public static bool WorldToSquare(Vector3 p, out int file, out int rank)
        {
            file = Mathf.FloorToInt(p.x + Half);
            rank = Mathf.FloorToInt(p.z + Half);
            if (file < 0 || file > 7 || rank < 0 || rank > 7)
            {
                file = rank = -1;
                return false;
            }
            return true;
        }

        /// <summary>Vrai si la case (file, rank) est sombre (a1 est sombre).</summary>
        public static bool IsDarkSquare(int file, int rank) => ((file + rank) & 1) == 0;

        /// <summary>
        /// Construit le plateau complet et renvoie sa racine. Un BoxCollider couvrant l'aire de jeu
        /// est ajouté sur la racine pour le picking (Physics.Raycast puis <see cref="WorldToSquare"/>).
        /// </summary>
        public static GameObject Build(Transform parent = null)
        {
            var root = new GameObject("KevChess Board");
            if (parent != null) root.transform.SetParent(parent, false);

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, -0.05f, 0f);
            col.size = new Vector3(8f, 0.1f, 8f);

            AddPart(root, "Surface", BuildSurface(), new[] { NeonMaterials.BoardDark, NeonMaterials.BoardLight }, false, true);
            AddPart(root, "Grid", BuildGrid(), new[] { NeonMaterials.BoardGrid }, false, false);
            AddPart(root, "Frame", BuildFrame(), new[]
            {
                NeonMaterials.Frame, NeonMaterials.FrameNeonSouth, NeonMaterials.FrameNeonSide, NeonMaterials.FrameNeonNorth, NeonMaterials.FrameNeonSide, NeonMaterials.FrameLip
            }, true, true);
            AddPart(root, "Abyss", BuildAbyss(), new[] { NeonMaterials.Abyss }, false, false);
            AddPart(root, "Labels", BuildLabels(), new[] { NeonMaterials.Labels }, false, false);
            return root;
        }

        static void AddPart(GameObject root, string name, Mesh mesh, Material[] materials, bool castShadows, bool receiveShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = materials;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            mr.receiveShadows = receiveShadows;
            mr.lightProbeUsage = LightProbeUsage.Off;
        }

        // ------------------------------------------------------------------ cases

        static Mesh BuildSurface()
        {
            var b = new MeshBuilder(2);
            for (int f = 0; f < 8; f++)
            for (int r = 0; r < 8; r++)
            {
                int sub = IsDarkSquare(f, r) ? 0 : 1;
                Vector3 c = SquareToWorld(f, r);
                Vector3 apex = c + Vector3.up * TileApex;
                Vector3 p00 = c + new Vector3(-0.5f, 0f, -0.5f);
                Vector3 p10 = c + new Vector3(0.5f, 0f, -0.5f);
                Vector3 p11 = c + new Vector3(0.5f, 0f, 0.5f);
                Vector3 p01 = c + new Vector3(-0.5f, 0f, 0.5f);
                // Pyramide très plate : 4 facettes qui renvoient la lumière différemment.
                b.TriFacing(sub, p00, p10, apex, Vector3.up);
                b.TriFacing(sub, p10, p11, apex, Vector3.up);
                b.TriFacing(sub, p11, p01, apex, Vector3.up);
                b.TriFacing(sub, p01, p00, apex, Vector3.up);
            }
            return b.ToMesh("KevChess_BoardSurface");
        }

        static Mesh BuildGrid()
        {
            var b = new MeshBuilder(1);
            const float w = 0.011f; // demi-largeur des lignes
            const float y = 0.004f;
            for (int i = 0; i <= 8; i++)
            {
                float t = i - Half;
                // Lignes parallèles à X.
                b.QuadUp(0, new Vector3(-Half, y, t - w), new Vector3(Half, y, t - w), new Vector3(Half, y, t + w), new Vector3(-Half, y, t + w),
                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                // Lignes parallèles à Z.
                b.QuadUp(0, new Vector3(t - w, y, -Half), new Vector3(t + w, y, -Half), new Vector3(t + w, y, Half), new Vector3(t - w, y, Half),
                    Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            }
            return b.ToMesh("KevChess_BoardGrid");
        }

        // ------------------------------------------------------------------ cadre

        struct FramePoint
        {
            public float D, Y;
            public int Tag; // 0 corps, 1 liseré (couleur du côté), 2 lèvre intérieure
            public FramePoint(float d, float y, int tag = 0) { D = d; Y = y; Tag = tag; }
        }

        // Profil balayé autour du carré : d = distance vers l'extérieur depuis le bord des cases.
        static readonly FramePoint[] FrameProfile =
        {
            new FramePoint(0f, -AbyssDepth),
            new FramePoint(0f, -0.02f),
            new FramePoint(0f, 0.035f, 2),          // lèvre lumineuse autour de l'aire de jeu
            new FramePoint(0.035f, FrameTop),
            new FramePoint(0.43f, FrameTop),
            new FramePoint(0.48f, 0.035f),
            new FramePoint(0.50f, -0.01f, 1),       // liseré néon
            new FramePoint(0.50f, -0.05f),
            new FramePoint(0.47f, -0.36f),
            new FramePoint(0.30f, -0.46f),
        };

        static Mesh BuildFrame()
        {
            // Sous-maillages : 0 corps, 1 néon sud (blancs), 2 néon est, 3 néon nord (noirs), 4 néon ouest, 5 lèvre.
            var b = new MeshBuilder(6);
            // Coins dans l'ordre : sud-ouest, sud-est, nord-est, nord-ouest.
            var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
            var outward = new[] { new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(-1, 0, 0) };
            for (int s = 0; s < 4; s++)
            {
                Vector2 c0 = corners[s], c1 = corners[(s + 1) % 4];
                for (int k = 0; k < FrameProfile.Length - 1; k++)
                {
                    FramePoint p0 = FrameProfile[k], p1 = FrameProfile[k + 1];
                    Vector3 a = FrameVertex(c0, p0), bb = FrameVertex(c1, p0);
                    Vector3 c = FrameVertex(c1, p1), d = FrameVertex(c0, p1);
                    float dd = p1.D - p0.D, dy = p1.Y - p0.Y;
                    // Normale du profil : (-dy, dd) dans le plan (d, y) → extérieur de la matière.
                    Vector3 expected = outward[s] * -dy + Vector3.up * dd;
                    int sub = p0.Tag == 1 ? 1 + s : p0.Tag == 2 ? 5 : 0;
                    b.QuadFacing(sub, a, bb, c, d, expected);
                }
            }
            // Dessous du cadre (fermeture, rarement visible).
            FramePoint last = FrameProfile[FrameProfile.Length - 1];
            float outer = Half + last.D;
            b.QuadFacing(0, new Vector3(-outer, last.Y, -outer), new Vector3(outer, last.Y, -outer), new Vector3(outer, last.Y, outer), new Vector3(-outer, last.Y, outer), Vector3.down);
            return b.ToMesh("KevChess_BoardFrame");
        }

        static Vector3 FrameVertex(Vector2 corner, FramePoint p)
        {
            float e = Half + p.D;
            return new Vector3(corner.x * e, p.Y, corner.y * e);
        }

        // ------------------------------------------------------------------ abîme

        static Mesh BuildAbyss()
        {
            var b = new MeshBuilder(1);
            float y = -AbyssDepth;
            // UV = coordonnées monde : la texture de grille se répète une fois par case.
            b.QuadUp(0,
                new Vector3(-Half, y, -Half), new Vector3(-Half, y, Half), new Vector3(Half, y, Half), new Vector3(Half, y, -Half),
                new Vector2(0, 0), new Vector2(0, 8), new Vector2(8, 8), new Vector2(8, 0));
            return b.ToMesh("KevChess_Abyss");
        }

        // ------------------------------------------------------------------ coordonnées

        // Police bitmap 5x7 maison (ligne 0 = haut).
        static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
        {
            { 'a', new[] { ".....", ".....", ".###.", "....#", ".####", "#...#", ".####" } },
            { 'b', new[] { "#....", "#....", "####.", "#...#", "#...#", "#...#", "####." } },
            { 'c', new[] { ".....", ".....", ".####", "#....", "#....", "#....", ".####" } },
            { 'd', new[] { "....#", "....#", ".####", "#...#", "#...#", "#...#", ".####" } },
            { 'e', new[] { ".....", ".....", ".###.", "#...#", "#####", "#....", ".####" } },
            { 'f', new[] { "..##.", ".#...", ".#...", "####.", ".#...", ".#...", ".#..." } },
            { 'g', new[] { ".....", ".####", "#...#", "#...#", ".####", "....#", ".###." } },
            { 'h', new[] { "#....", "#....", "####.", "#...#", "#...#", "#...#", "#...#" } },
            { '1', new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." } },
            { '2', new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" } },
            { '3', new[] { "####.", "....#", "....#", ".###.", "....#", "....#", "####." } },
            { '4', new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." } },
            { '5', new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." } },
            { '6', new[] { ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###." } },
            { '7', new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." } },
            { '8', new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." } },
        };

        static Mesh BuildLabels()
        {
            var b = new MeshBuilder(1);
            float mid = Half + (0.035f + 0.43f) * 0.5f; // milieu du dessus du cadre
            for (int i = 0; i < 8; i++)
            {
                char letter = (char)('a' + i);
                char digit = (char)('1' + i);
                float t = i - 3.5f;
                // Lisibles depuis le côté des blancs…
                AddGlyph(b, letter, new Vector3(t, FrameTop, -mid), 0f);
                AddGlyph(b, digit, new Vector3(-mid, FrameTop, t), 0f);
                // …et depuis le côté des noirs (tournés de 180°).
                AddGlyph(b, letter, new Vector3(t, FrameTop, mid), 180f);
                AddGlyph(b, digit, new Vector3(mid, FrameTop, t), 180f);
            }
            return b.ToMesh("KevChess_Labels");
        }

        static void AddGlyph(MeshBuilder b, char ch, Vector3 center, float yaw)
        {
            if (!Glyphs.TryGetValue(ch, out string[] rows)) return;
            const float px = 0.026f;
            const float gap = 0.003f;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            Vector3 lift = Vector3.up * 0.0015f;
            for (int row = 0; row < 7; row++)
            for (int col = 0; col < 5; col++)
            {
                if (rows[row][col] != '#') continue;
                float x0 = (col - 2.5f) * px + gap, x1 = (col - 1.5f) * px - gap;
                float z1 = (3.5f - row) * px - gap, z0 = (2.5f - row) * px + gap;
                Vector3 a = center + lift + rot * new Vector3(x0, 0f, z0);
                Vector3 bb = center + lift + rot * new Vector3(x0, 0f, z1);
                Vector3 c = center + lift + rot * new Vector3(x1, 0f, z1);
                Vector3 d = center + lift + rot * new Vector3(x1, 0f, z0);
                b.QuadUp(0, a, bb, c, d, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            }
        }
    }
}
