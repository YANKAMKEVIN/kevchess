using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals
{
    /// <summary>
    /// Flèche néon d'indice : un trait en arc légèrement surélevé au-dessus du plateau,
    /// terminé par un chevron, avec une pulsation douce et une vague de lumière qui court
    /// du départ vers l'arrivée. Maillage procédural, matériau additif existant (traînées).
    /// </summary>
    public sealed class HintArrow : MonoBehaviour
    {
        const int Segments = 32;
        const float ShaftWidth = 0.34f;   // largeur du ruban (le cœur lumineux occupe ~40 %)
        const float ChevronLength = 0.56f;
        const float ChevronAngle = 36f;
        const float FadeIn = 0.25f;

        Mesh _mesh;
        Material _material;
        MeshRenderer _renderer;
        Color _color;
        float _shownAt;
        Color[] _colors;
        float[] _along;   // position de chaque sommet le long de la flèche (0 départ, 1 pointe)
        float[] _alpha;   // opacité de base de chaque sommet

        /// <summary>Crée la flèche (masquée) sous <paramref name="parent"/>.</summary>
        public static HintArrow Create(Transform parent = null)
        {
            var go = new GameObject("KevChess HintArrow");
            if (parent != null) go.transform.SetParent(parent, false);
            var arrow = go.AddComponent<HintArrow>();
            arrow.Setup();
            return arrow;
        }

        public bool IsVisible => _renderer != null && _renderer.enabled;

        void Setup()
        {
            _mesh = new Mesh { name = "KevChess_HintArrow" };
            _mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _material = new Material(NeonMaterials.Trail) { name = "KevChess_HintArrow" };
            _material.renderQueue = (int)RenderQueue.Transparent + 45; // au-dessus des surbrillances
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.enabled = false;
        }

        /// <summary>Affiche la flèche entre deux points du plateau (coordonnées monde, y ignoré).</summary>
        public void Show(Vector3 from, Vector3 to, Color color)
        {
            if (_mesh == null) Setup();
            _color = color;
            Build(from, to);
            _shownAt = Time.time;
            _renderer.enabled = true;
            Update();
        }

        /// <summary>Change la couleur sans reconstruire la flèche (changement de thème).</summary>
        public void SetColor(Color color) => _color = color;

        public void Hide()
        {
            if (_renderer != null) _renderer.enabled = false;
        }

        void Update()
        {
            if (!IsVisible) return;
            float t = Time.time;
            float fade = Mathf.Clamp01((t - _shownAt) / FadeIn);
            // Pulsation douce de l'ensemble.
            float pulse = 1.9f + 0.55f * Mathf.Sin(t * 3.2f);
            _material.SetColor(NeonMaterials.BaseColorProperty, _color * pulse);
            // Vague lumineuse qui court vers la pointe.
            float head = Mathf.Repeat(t * 0.75f, 1.4f) - 0.2f;
            for (int i = 0; i < _colors.Length; i++)
            {
                float d = (_along[i] - head) / 0.16f;
                float wave = Mathf.Exp(-d * d);
                float a = _alpha[i] * fade * (0.55f + 0.45f * wave);
                _colors[i] = new Color(1f, 1f, 1f, a);
            }
            _mesh.colors = _colors;
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_mesh != null) Destroy(_mesh);
        }

        // ------------------------------------------------------------------ maillage

        void Build(Vector3 from, Vector3 to)
        {
            Vector3 flat = to - from;
            flat.y = 0f;
            float dist = Mathf.Max(0.01f, flat.magnitude);
            Vector3 dir = flat / dist;
            Vector3 side = Vector3.Cross(Vector3.up, dir); // horizontal, perpendiculaire au trajet

            // Arc de Bézier quadratique : décolle du départ, retombe sur la case d'arrivée.
            Vector3 p0 = new Vector3(from.x, 0.10f, from.z);
            Vector3 p2 = new Vector3(to.x, 0.16f, to.z);
            Vector3 p1 = (p0 + p2) * 0.5f + Vector3.up * (0.45f + 0.09f * dist);

            var verts = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var along = new System.Collections.Generic.List<float>();
            var alpha = new System.Collections.Generic.List<float>();
            var tris = new System.Collections.Generic.List<int>();

            // Le trait s'arrête un peu avant la pointe pour laisser le chevron respirer.
            float shaftEnd = Mathf.Clamp01(1f - 0.12f / dist);
            for (int i = 0; i <= Segments; i++)
            {
                float u = i / (float)Segments * shaftEnd;
                Vector3 p = Bezier(p0, p1, p2, u);
                // Le ruban s'affine aux deux bouts.
                float w = ShaftWidth * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(Mathf.Min(u / 0.15f, 1f)));
                verts.Add(p - side * w * 0.5f);
                verts.Add(p + side * w * 0.5f);
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
                float fadeTail = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(u / 0.3f));
                along.Add(u); along.Add(u);
                alpha.Add(fadeTail); alpha.Add(fadeTail);
                if (i > 0)
                {
                    int b = verts.Count - 4;
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
                    tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
                }
            }

            // Chevron : deux branches lumineuses qui partent de la pointe, dans le plan de l'arc.
            Vector3 tip = p2;
            Vector3 tangent = (p2 - p1).normalized;
            Vector3 back = -tangent;
            Vector3 up = Vector3.Cross(side, tangent).normalized;
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector3 arm = Quaternion.AngleAxis(sign * ChevronAngle, up) * back;
                AddStrip(verts, uvs, along, alpha, tris, tip, tip + arm * ChevronLength, up, ShaftWidth * 0.95f);
            }
            // Remplissage léger du triangle de la pointe.
            {
                Vector3 l = tip + (Quaternion.AngleAxis(-ChevronAngle, up) * back) * ChevronLength;
                Vector3 r = tip + (Quaternion.AngleAxis(ChevronAngle, up) * back) * ChevronLength;
                int b = verts.Count;
                verts.Add(tip); verts.Add(l); verts.Add(r);
                uvs.Add(new Vector2(1f, 0.5f)); uvs.Add(new Vector2(1f, 0.5f)); uvs.Add(new Vector2(1f, 0.5f));
                along.Add(1f); along.Add(0.92f); along.Add(0.92f);
                alpha.Add(0.22f); alpha.Add(0.1f); alpha.Add(0.1f);
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
            }

            _mesh.Clear();
            _mesh.SetVertices(verts);
            _mesh.SetUVs(0, uvs);
            _colors = new Color[verts.Count];
            for (int i = 0; i < _colors.Length; i++) _colors[i] = Color.white;
            _mesh.colors = _colors;
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            _along = along.ToArray();
            _alpha = alpha.ToArray();
        }

        static void AddStrip(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<Vector2> uvs,
            System.Collections.Generic.List<float> along, System.Collections.Generic.List<float> alpha,
            System.Collections.Generic.List<int> tris, Vector3 a, Vector3 b, Vector3 normal, float width)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 side = Vector3.Cross(normal, dir).normalized * width * 0.5f;
            // Prolonge un peu au-delà de la pointe pour que les deux branches se rejoignent proprement.
            Vector3 a2 = a - dir * width * 0.2f;
            int i0 = verts.Count;
            verts.Add(a2 - side); verts.Add(a2 + side); verts.Add(b - side); verts.Add(b + side);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(1f, 1f));
            along.Add(1f); along.Add(1f); along.Add(0.9f); along.Add(0.9f);
            alpha.Add(1f); alpha.Add(1f); alpha.Add(0.8f); alpha.Add(0.8f);
            tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 1);
            tris.Add(i0 + 1); tris.Add(i0 + 2); tris.Add(i0 + 3);
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }
    }
}
