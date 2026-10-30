using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals
{
    /// <summary>
    /// Point de profil pour un tour (surface de révolution).
    /// Le segment qui part de ce point vers le suivant utilise <see cref="Sub"/> comme sous-maillage.
    /// <see cref="Twist"/> décale l'anneau d'une fraction de secteur : en alternant 0 et 0.5,
    /// on obtient des facettes triangulaires façon taille de gemme.
    /// </summary>
    internal struct ProfilePoint
    {
        public float R;
        public float Y;
        public float Twist;
        public int Sub;

        public ProfilePoint(float r, float y, float twist = 0f, int sub = 0)
        {
            R = r;
            Y = y;
            Twist = twist;
            Sub = sub;
        }
    }

    /// <summary>
    /// Constructeur de maillage à normales plates (chaque triangle a ses propres sommets),
    /// pour que chaque facette accroche la lumière indépendamment.
    /// </summary>
    internal sealed class MeshBuilder
    {
        readonly List<Vector3> _vertices = new List<Vector3>(1024);
        readonly List<Vector3> _normals = new List<Vector3>(1024);
        readonly List<Vector2> _uvs = new List<Vector2>(1024);
        readonly List<Color32> _colors = new List<Color32>(1024);
        readonly List<int>[] _subMeshes;

        public MeshBuilder(int subMeshCount = 1)
        {
            _subMeshes = new List<int>[Mathf.Max(1, subMeshCount)];
            for (int i = 0; i < _subMeshes.Length; i++)
                _subMeshes[i] = new List<int>(1024);
        }

        public int VertexCount => _vertices.Count;

        /// <summary>Triangle en ordre horaire vu de face (convention Unity).</summary>
        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c)
        {
            Tri(sub, a, b, c, Vector2.zero, Vector2.zero, Vector2.zero);
        }

        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-14f)
                return; // triangle dégénéré (pôle d'un tour, etc.)
            n.Normalize();
            var list = _subMeshes[Mathf.Clamp(sub, 0, _subMeshes.Length - 1)];
            int i0 = _vertices.Count;
            _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
            _normals.Add(n); _normals.Add(n); _normals.Add(n);
            _uvs.Add(ua); _uvs.Add(ub); _uvs.Add(uc);
            var white = new Color32(255, 255, 255, 255);
            _colors.Add(white); _colors.Add(white); _colors.Add(white);
            list.Add(i0); list.Add(i0 + 1); list.Add(i0 + 2);
        }

        /// <summary>Triangle dont la face avant est orientée vers <paramref name="expected"/>.</summary>
        public void TriFacing(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 expected)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, expected) < 0f)
                Tri(sub, a, c, b);
            else
                Tri(sub, a, b, c);
        }

        /// <summary>Triangle orienté vers l'extérieur d'un solide convexe dont <paramref name="interior"/> est un point intérieur.</summary>
        public void TriOut(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 interior)
        {
            TriFacing(sub, a, b, c, (a + b + c) / 3f - interior);
        }

        public void QuadFacing(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 expected)
        {
            TriFacing(sub, a, b, c, expected);
            TriFacing(sub, a, c, d, expected);
        }

        /// <summary>Quad horizontal orienté vers le haut, avec UV.</summary>
        public void QuadUp(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            // a,b,c,d dans l'ordre horaire vu du dessus.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), Vector3.up) < 0f)
            {
                Tri(sub, a, c, b, ua, uc, ub);
                Tri(sub, a, d, c, ua, ud, uc);
            }
            else
            {
                Tri(sub, a, b, c, ua, ub, uc);
                Tri(sub, a, c, d, ua, uc, ud);
            }
        }

        // ------------------------------------------------------------------ primitives

        /// <summary>Surface de révolution autour de l'axe Y, à facettes plates.</summary>
        public void Lathe(IList<ProfilePoint> profile, int sides, Vector3 offset)
        {
            float step = Mathf.PI * 2f / sides;
            for (int k = 0; k < profile.Count - 1; k++)
            {
                ProfilePoint p0 = profile[k];
                ProfilePoint p1 = profile[k + 1];
                for (int j = 0; j < sides; j++)
                {
                    Vector3 a = Ring(p0, j, step) + offset;
                    Vector3 d = Ring(p0, j + 1, step) + offset;
                    Vector3 b = Ring(p1, j, step) + offset;
                    Vector3 c = Ring(p1, j + 1, step) + offset;
                    // Ordre horaire vu de l'extérieur (voir dérivation : θ croissant = vers la droite).
                    Tri(p0.Sub, a, b, c);
                    Tri(p0.Sub, a, c, d);
                }
            }
        }

        static Vector3 Ring(ProfilePoint p, int j, float step)
        {
            float a = (j + p.Twist) * step;
            return new Vector3(Mathf.Cos(a) * p.R, p.Y, Mathf.Sin(a) * p.R);
        }

        /// <summary>Solide convexe à 8 sommets (boîte déformée). Ordre : bas (0..3) puis haut (4..7), même tour.</summary>
        public void Hexahedron(int sub, Vector3[] c)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < 8; i++) center += c[i];
            center /= 8f;
            Quad(sub, c[0], c[1], c[2], c[3], center); // bas
            Quad(sub, c[4], c[5], c[6], c[7], center); // haut
            for (int i = 0; i < 4; i++)
            {
                int n = (i + 1) % 4;
                Quad(sub, c[i], c[n], c[n + 4], c[i + 4], center);
            }
        }

        void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 interior)
        {
            Vector3 expected = (a + b + c + d) * 0.25f - interior;
            QuadFacing(sub, a, b, c, d, expected);
        }

        /// <summary>Boîte orientée, chanfreinée (arêtes verticales coupées) pour un rendu taillé.</summary>
        public void Box(int sub, Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            var c = new Vector3[8];
            c[0] = center + rotation * new Vector3(-h.x, -h.y, -h.z);
            c[1] = center + rotation * new Vector3(h.x, -h.y, -h.z);
            c[2] = center + rotation * new Vector3(h.x, -h.y, h.z);
            c[3] = center + rotation * new Vector3(-h.x, -h.y, h.z);
            c[4] = center + rotation * new Vector3(-h.x, h.y, -h.z);
            c[5] = center + rotation * new Vector3(h.x, h.y, -h.z);
            c[6] = center + rotation * new Vector3(h.x, h.y, h.z);
            c[7] = center + rotation * new Vector3(-h.x, h.y, h.z);
            Hexahedron(sub, c);
        }

        /// <summary>Gemme octaédrique (bipyramide à <paramref name="sides"/> côtés).</summary>
        public void Gem(int sub, Vector3 center, float radius, float halfHeight, int sides = 6, float rotation = 0f)
        {
            Vector3 top = center + Vector3.up * halfHeight;
            Vector3 bottom = center - Vector3.up * halfHeight;
            float step = Mathf.PI * 2f / sides;
            for (int i = 0; i < sides; i++)
            {
                float a0 = rotation + i * step, a1 = rotation + (i + 1) * step;
                Vector3 p0 = center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                TriOut(sub, top, p0, p1, center);
                TriOut(sub, bottom, p0, p1, center);
            }
        }

        /// <summary>Gemme couchée selon un axe arbitraire (pour les yeux du cavalier par ex.).</summary>
        public void GemAlong(int sub, Vector3 center, Vector3 axis, float radius, float halfLength, int sides = 6)
        {
            axis.Normalize();
            Vector3 t = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 b = Vector3.Cross(axis, t);
            Vector3 tip0 = center + axis * halfLength;
            Vector3 tip1 = center - axis * halfLength;
            float step = Mathf.PI * 2f / sides;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * step, a1 = (i + 1) * step;
                Vector3 p0 = center + (t * Mathf.Cos(a0) + b * Mathf.Sin(a0)) * radius;
                Vector3 p1 = center + (t * Mathf.Cos(a1) + b * Mathf.Sin(a1)) * radius;
                TriOut(sub, tip0, p0, p1, center);
                TriOut(sub, tip1, p0, p1, center);
            }
        }

        /// <summary>
        /// Extrusion biseautée d'un polygone 2D (u = z avant, v = y haut) le long de X,
        /// d'épaisseur totale 2*<paramref name="halfThickness"/>, avec un biseau de <paramref name="bevel"/>.
        /// </summary>
        public void ExtrudeBeveled(int sub, IList<Vector2> polygon, float halfThickness, float bevel, Vector3 offset)
        {
            var pts = new List<Vector2>(polygon);
            if (SignedArea(pts) < 0f)
                pts.Reverse(); // on travaille en sens trigonométrique

            int n = pts.Count;
            var edgeNormals = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 d = (pts[(i + 1) % n] - pts[i]).normalized;
                edgeNormals[i] = new Vector2(d.y, -d.x); // normale sortante en sens trigo
            }

            // Polygone intérieur (face avant du biseau).
            var inset = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 nPrev = edgeNormals[(i - 1 + n) % n];
                Vector2 nNext = edgeNormals[i];
                Vector2 m = (nPrev + nNext);
                if (m.sqrMagnitude < 1e-6f) m = nNext;
                m.Normalize();
                float cos = Mathf.Max(0.35f, Vector2.Dot(m, nNext));
                inset[i] = pts[i] - m * (bevel / cos);
            }

            float hb = halfThickness - bevel;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 en = new Vector3(0f, edgeNormals[i].y, edgeNormals[i].x);
                Vector3 a0 = To3(pts[i], -hb) + offset, b0 = To3(pts[j], -hb) + offset;
                Vector3 a1 = To3(pts[i], hb) + offset, b1 = To3(pts[j], hb) + offset;
                QuadFacing(sub, a0, b0, b1, a1, en);

                // Biseaux des deux côtés.
                Vector3 ia = To3(inset[i], halfThickness) + offset, ib = To3(inset[j], halfThickness) + offset;
                QuadFacing(sub, a1, b1, ib, ia, (en + Vector3.right).normalized);
                Vector3 ja = To3(inset[i], -halfThickness) + offset, jb = To3(inset[j], -halfThickness) + offset;
                QuadFacing(sub, a0, b0, jb, ja, (en - Vector3.right).normalized);
            }

            // Faces latérales : triangulation du polygone original appliquée au polygone intérieur.
            List<int> tris = Triangulate(pts);
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector2 p = inset[tris[t]], q = inset[tris[t + 1]], r = inset[tris[t + 2]];
                TriFacing(sub, To3(p, halfThickness) + offset, To3(q, halfThickness) + offset, To3(r, halfThickness) + offset, Vector3.right);
                TriFacing(sub, To3(p, -halfThickness) + offset, To3(q, -halfThickness) + offset, To3(r, -halfThickness) + offset, Vector3.left);
            }
        }

        static Vector3 To3(Vector2 p, float x) => new Vector3(x, p.y, p.x);

        public static float SignedArea(IList<Vector2> pts)
        {
            float a = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 p = pts[i], q = pts[(i + 1) % pts.Count];
                a += p.x * q.y - q.x * p.y;
            }
            return a * 0.5f;
        }

        /// <summary>Triangulation par oreilles d'un polygone simple en sens trigonométrique.</summary>
        public static List<int> Triangulate(IList<Vector2> pts)
        {
            var result = new List<int>(pts.Count * 3);
            var idx = new List<int>(pts.Count);
            for (int i = 0; i < pts.Count; i++) idx.Add(i);

            int guard = pts.Count * pts.Count;
            while (idx.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                for (int i = 0; i < idx.Count; i++)
                {
                    int ip = idx[(i - 1 + idx.Count) % idx.Count];
                    int ic = idx[i];
                    int inx = idx[(i + 1) % idx.Count];
                    Vector2 a = pts[ip], b = pts[ic], c = pts[inx];
                    if (Cross(b - a, c - b) <= 1e-7f) continue; // sommet réflexe
                    bool inside = false;
                    for (int k = 0; k < idx.Count; k++)
                    {
                        int o = idx[k];
                        if (o == ip || o == ic || o == inx) continue;
                        if (PointInTriangle(pts[o], a, b, c)) { inside = true; break; }
                    }
                    if (inside) continue;
                    result.Add(ip); result.Add(ic); result.Add(inx);
                    idx.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break; // polygone dégénéré : on arrête proprement
            }
            if (idx.Count == 3)
            {
                result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]);
            }
            return result;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetColors(_colors);
            mesh.subMeshCount = _subMeshes.Length;
            for (int i = 0; i < _subMeshes.Length; i++)
                mesh.SetTriangles(_subMeshes[i], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
