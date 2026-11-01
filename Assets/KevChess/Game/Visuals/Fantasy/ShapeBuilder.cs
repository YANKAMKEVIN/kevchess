using System.Collections.Generic;
using UnityEngine;

namespace KevChess.Game.Visuals.Fantasy
{
    /// <summary>
    /// Surcouche de <see cref="MeshBuilder"/> avec une pile de matrices : chaque primitive
    /// (révolution, boîte, gemme, membre, extrusion) est construite dans un repère local puis
    /// transformée. Les matrices miroir (échelle négative) sont gérées : il suffit de construire
    /// le membre droit sous une échelle (-1, 1, 1) pour obtenir le gauche.
    /// Sous-maillage 0 = corps verre, 1 = accents lumineux (même convention que PieceMeshFactory).
    /// </summary>
    internal sealed class ShapeBuilder
    {
        public const int Body = PieceMeshFactory.BodySubMesh;
        public const int Accent = PieceMeshFactory.AccentSubMesh;

        readonly MeshBuilder _b;
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();
        Matrix4x4 _m = Matrix4x4.identity;

        public ShapeBuilder(int subMeshes = 2)
        {
            _b = new MeshBuilder(subMeshes);
        }

        public int TriangleCount => _b.VertexCount / 3;

        public Mesh ToMesh(string name) => _b.ToMesh(name);

        // ------------------------------------------------------------------ repères

        public void Push(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            _stack.Push(_m);
            _m = _m * Matrix4x4.TRS(position, rotation, scale);
        }

        public void Push(Vector3 position, Quaternion rotation) => Push(position, rotation, Vector3.one);

        public void Pop()
        {
            _m = _stack.Pop();
        }

        /// <summary>Repère miroir gauche/droite (X → -X) pour les membres symétriques.</summary>
        public void PushMirror() => Push(Vector3.zero, Quaternion.identity, new Vector3(-1f, 1f, 1f));

        // ------------------------------------------------------------------ triangles

        /// <summary>Triangle à l'ordre donné (convention MeshBuilder), corrigé si le repère est miroir.</summary>
        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 wa = _m.MultiplyPoint3x4(a), wb = _m.MultiplyPoint3x4(b), wc = _m.MultiplyPoint3x4(c);
            if (_m.determinant < 0f) _b.Tri(sub, wa, wc, wb);
            else _b.Tri(sub, wa, wb, wc);
        }

        /// <summary>Triangle orienté à l'opposé du point intérieur (exprimé dans le repère local).</summary>
        public void TriOut(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 interior)
        {
            Vector3 wa = _m.MultiplyPoint3x4(a), wb = _m.MultiplyPoint3x4(b), wc = _m.MultiplyPoint3x4(c);
            Vector3 wi = _m.MultiplyPoint3x4(interior);
            Vector3 n = Vector3.Cross(wb - wa, wc - wa);
            if (Vector3.Dot(n, (wa + wb + wc) / 3f - wi) < 0f) _b.Tri(sub, wa, wc, wb);
            else _b.Tri(sub, wa, wb, wc);
        }

        /// <summary>Triangle dont la face avant regarde dans la direction locale <paramref name="facing"/>.</summary>
        public void TriFacing(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            TriOut(sub, a, b, c, (a + b + c) / 3f - facing.normalized * 0.01f);
        }

        public void QuadOut(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 interior)
        {
            TriOut(sub, a, b, c, interior);
            TriOut(sub, a, c, d, interior);
        }

        /// <summary>Quad visible des deux côtés (cape, membranes très fines).</summary>
        public void QuadDouble(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Tri(sub, a, b, c); Tri(sub, a, c, d);
            Tri(sub, a, c, b); Tri(sub, a, d, c);
        }

        // ------------------------------------------------------------------ primitives

        /// <summary>Surface de révolution autour de l'axe Y local (profil de bas en haut).</summary>
        public void Lathe(IList<ProfilePoint> profile, int sides)
        {
            float step = Mathf.PI * 2f / sides;
            for (int k = 0; k < profile.Count - 1; k++)
            {
                ProfilePoint p0 = profile[k], p1 = profile[k + 1];
                for (int j = 0; j < sides; j++)
                {
                    Vector3 a = Ring(p0, j, step), d = Ring(p0, j + 1, step);
                    Vector3 b = Ring(p1, j, step), c = Ring(p1, j + 1, step);
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

        /// <summary>Révolution placée et orientée (axe Y local → <paramref name="rotation"/> * up).</summary>
        public void Lathe(IList<ProfilePoint> profile, int sides, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Push(position, rotation, scale);
            Lathe(profile, sides);
            Pop();
        }

        /// <summary>Ellipsoïde taillé (anneaux en quinconce).</summary>
        public void Ellipsoid(int sub, Vector3 center, Vector3 radii, int sides = 8, int rings = 4)
        {
            var p = new List<ProfilePoint>(rings + 2);
            for (int i = 0; i <= rings; i++)
            {
                float t = Mathf.PI * i / rings - Mathf.PI * 0.5f;
                p.Add(new ProfilePoint(Mathf.Cos(t), Mathf.Sin(t), (i & 1) * 0.5f, sub));
            }
            Lathe(p, sides, center, Quaternion.identity, radii);
        }

        /// <summary>Membre tronconique de <paramref name="a"/> à <paramref name="b"/>, fermé aux deux bouts.</summary>
        public void Limb(int sub, Vector3 a, Vector3 b, float ra, float rb, int sides = 6)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            var p = new List<ProfilePoint>(5)
            {
                new ProfilePoint(0f, 0f, 0f, sub),
                new ProfilePoint(ra * 0.8f, 0f, 0f, sub),
                new ProfilePoint(ra, len * 0.12f, 0.5f, sub),
                new ProfilePoint(rb, len * 0.88f, 0f, sub),
                new ProfilePoint(rb * 0.8f, len, 0.5f, sub),
                new ProfilePoint(0f, len, 0f, sub),
            };
            Lathe(p, sides, a, Quaternion.FromToRotation(Vector3.up, d / len), Vector3.one);
        }

        /// <summary>Chaîne de membres (queue, cou) passant par les points donnés.</summary>
        public void Chain(int sub, IList<Vector3> points, IList<float> radii, int sides = 6)
        {
            for (int i = 0; i < points.Count - 1; i++)
                Limb(sub, points[i], points[i + 1], radii[i], radii[i + 1], sides);
        }

        /// <summary>Boîte orientée.</summary>
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

        public void Box(int sub, Vector3 center, Vector3 size) => Box(sub, center, size, Quaternion.identity);

        /// <summary>Solide convexe à 8 sommets : bas (0..3) puis haut (4..7), même sens de rotation.</summary>
        public void Hexahedron(int sub, Vector3[] c)
        {
            Vector3 center = Vector3.zero;
            for (int i = 0; i < 8; i++) center += c[i];
            center /= 8f;
            QuadOut(sub, c[0], c[1], c[2], c[3], center);
            QuadOut(sub, c[4], c[5], c[6], c[7], center);
            for (int i = 0; i < 4; i++)
            {
                int n = (i + 1) % 4;
                QuadOut(sub, c[i], c[n], c[n + 4], c[i + 4], center);
            }
        }

        /// <summary>Bipyramide verticale.</summary>
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

        /// <summary>Bipyramide couchée selon un axe (yeux, cornes, pointes).</summary>
        public void GemAlong(int sub, Vector3 center, Vector3 axis, float radius, float halfLength, int sides = 4)
        {
            axis.Normalize();
            Vector3 t = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 b = Vector3.Cross(axis, t);
            Vector3 tip0 = center + axis * halfLength, tip1 = center - axis * halfLength;
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

        /// <summary>Corne / pointe : pyramide de la base <paramref name="a"/> à la pointe <paramref name="tip"/>.</summary>
        public void Spike(int sub, Vector3 a, Vector3 tip, float radius, int sides = 4)
        {
            Vector3 axis = (tip - a).normalized;
            Vector3 t = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 b = Vector3.Cross(axis, t);
            Vector3 interior = a + (tip - a) * 0.25f;
            float step = Mathf.PI * 2f / sides;
            for (int i = 0; i < sides; i++)
            {
                Vector3 p0 = a + (t * Mathf.Cos(i * step) + b * Mathf.Sin(i * step)) * radius;
                Vector3 p1 = a + (t * Mathf.Cos((i + 1) * step) + b * Mathf.Sin((i + 1) * step)) * radius;
                TriOut(sub, tip, p0, p1, interior);
                TriOut(sub, a, p0, p1, interior);
            }
        }

        /// <summary>
        /// Lame plate en losange de <paramref name="origin"/> dans la direction <paramref name="dir"/> :
        /// largeur <paramref name="width"/>, épaisseur relative <paramref name="flat"/>, plat tourné vers <paramref name="side"/>.
        /// </summary>
        public void Blade(int sub, Vector3 origin, Vector3 dir, float length, float width, Vector3 side, float flat = 0.3f)
        {
            dir.Normalize();
            Vector3 w = Vector3.ProjectOnPlane(side, dir).normalized * (width * 0.5f);
            Vector3 th = Vector3.Cross(dir, w).normalized * (width * 0.5f * flat);
            Vector3 b0 = origin, tip = origin + dir * length;
            Vector3 mid = origin + dir * (length * 0.18f);
            Vector3 shoulder = origin + dir * (length * 0.82f);
            Vector3 interior = origin + dir * (length * 0.5f);
            // Base → épaulement (section losange), puis pointe.
            Vector3[] r0 = { b0 + w * 0.8f, b0 + th, b0 - w * 0.8f, b0 - th };
            Vector3[] r1 = { mid + w, mid + th, mid - w, mid - th };
            Vector3[] r2 = { shoulder + w * 0.9f, shoulder + th * 0.9f, shoulder - w * 0.9f, shoulder - th * 0.9f };
            for (int i = 0; i < 4; i++)
            {
                int n = (i + 1) % 4;
                QuadOut(sub, r0[i], r1[i], r1[n], r0[n], interior);
                QuadOut(sub, r1[i], r2[i], r2[n], r1[n], interior);
                TriOut(sub, r2[i], tip, r2[n], interior);
            }
            QuadOut(sub, r0[0], r0[1], r0[2], r0[3], interior);
        }

        /// <summary>
        /// Extrusion d'un polygone 2D (plan XY local) sur l'épaisseur 2*<paramref name="halfThickness"/> en Z.
        /// </summary>
        public void Extrude(int sub, IList<Vector2> polygon, float halfThickness)
        {
            var pts = new List<Vector2>(polygon);
            if (MeshBuilder.SignedArea(pts) < 0f) pts.Reverse();
            int n = pts.Count;
            Vector3 interiorZ = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 d = (pts[j] - pts[i]).normalized;
                var en = new Vector3(d.y, -d.x, 0f); // normale sortante en sens trigonométrique
                Vector3 a0 = new Vector3(pts[i].x, pts[i].y, -halfThickness), b0 = new Vector3(pts[j].x, pts[j].y, -halfThickness);
                Vector3 a1 = new Vector3(pts[i].x, pts[i].y, halfThickness), b1 = new Vector3(pts[j].x, pts[j].y, halfThickness);
                Vector3 mid = (a0 + b1) * 0.5f;
                QuadOut(sub, a0, b0, b1, a1, mid - en * 0.01f);
            }
            List<int> tris = MeshBuilder.Triangulate(pts);
            for (int t = 0; t < tris.Count; t += 3)
            {
                Vector2 p = pts[tris[t]], q = pts[tris[t + 1]], r = pts[tris[t + 2]];
                TriFacing(sub, new Vector3(p.x, p.y, halfThickness), new Vector3(q.x, q.y, halfThickness), new Vector3(r.x, r.y, halfThickness), Vector3.forward);
                TriFacing(sub, new Vector3(p.x, p.y, -halfThickness), new Vector3(q.x, q.y, -halfThickness), new Vector3(r.x, r.y, -halfThickness), Vector3.back);
            }
        }

        /// <summary>Extrusion dans le plan sagittal : polygone (z avant, y haut), épaisseur en X.</summary>
        public void ExtrudeSagittal(int sub, IList<Vector2> polygonZY, float halfThickness, Vector3 offset)
        {
            // Rotation de -90° autour de Y : X local → Z, Z local → -X.
            Push(offset, Quaternion.Euler(0f, -90f, 0f));
            Extrude(sub, polygonZY, halfThickness);
            Pop();
        }
    }
}
