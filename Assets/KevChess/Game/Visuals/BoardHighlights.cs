using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KevChess.Game.Visuals
{
    /// <summary>
    /// Surlignages lumineux posés sur le plateau : case sélectionnée, coups légaux
    /// (point = case vide, anneau = prise), dernier coup, roi en échec (halo rouge pulsé).
    /// Les quads sont mis en pool ; aucun objet n'est créé en régime établi.
    /// </summary>
    public sealed class BoardHighlights : MonoBehaviour
    {
        const float Height = 0.02f; // au-dessus de la pointe des cases

        static Mesh _quad;

        readonly List<Transform> _legal = new List<Transform>();
        readonly List<Transform> _captures = new List<Transform>();
        readonly List<Transform> _pool = new List<Transform>();
        Transform _selection, _lastFrom, _lastTo, _check;
        Material _checkMaterial;
        Color _checkBase;

        /// <summary>Crée le composant sur un nouvel objet (enfant optionnel).</summary>
        public static BoardHighlights Create(Transform parent = null)
        {
            var go = new GameObject("KevChess Highlights");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<BoardHighlights>();
        }

        // ------------------------------------------------------------------ API

        public void ShowSelection(int file, int rank)
        {
            if (_selection == null) _selection = CreateQuad("Selection", NeonMaterials.SelectionHighlight);
            Place(_selection, file, rank, 1f);
        }

        public void ClearSelection()
        {
            if (_selection != null) _selection.gameObject.SetActive(false);
        }

        /// <summary>Affiche les coups légaux. Vector2Int = (file, rank).</summary>
        public void ShowLegalMoves(IEnumerable<Vector2Int> quietMoves, IEnumerable<Vector2Int> captures)
        {
            ClearLegalMoves();
            if (quietMoves != null)
                foreach (Vector2Int m in quietMoves)
                {
                    Transform t = Take(NeonMaterials.LegalMove);
                    Place(t, m.x, m.y, 0.42f);
                    _legal.Add(t);
                }
            if (captures != null)
                foreach (Vector2Int m in captures)
                {
                    Transform t = Take(NeonMaterials.LegalCapture);
                    Place(t, m.x, m.y, 1.0f);
                    _captures.Add(t);
                }
        }

        public void ClearLegalMoves()
        {
            for (int i = 0; i < _legal.Count; i++) Release(_legal[i]);
            for (int i = 0; i < _captures.Count; i++) Release(_captures[i]);
            _legal.Clear();
            _captures.Clear();
        }

        public void ShowLastMove(int fromFile, int fromRank, int toFile, int toRank)
        {
            if (_lastFrom == null) _lastFrom = CreateQuad("LastFrom", NeonMaterials.LastMove);
            if (_lastTo == null) _lastTo = CreateQuad("LastTo", NeonMaterials.LastMove);
            Place(_lastFrom, fromFile, fromRank, 0.96f);
            Place(_lastTo, toFile, toRank, 0.96f);
        }

        public void ClearLastMove()
        {
            if (_lastFrom != null) _lastFrom.gameObject.SetActive(false);
            if (_lastTo != null) _lastTo.gameObject.SetActive(false);
        }

        /// <summary>Halo rouge pulsé sous le roi en échec.</summary>
        public void ShowCheck(int file, int rank)
        {
            if (_check == null)
            {
                _checkMaterial = new Material(NeonMaterials.Check);
                _checkBase = _checkMaterial.GetColor(NeonMaterials.BaseColorProperty);
                _check = CreateQuad("Check", _checkMaterial);
            }
            Place(_check, file, rank, 1.5f);
        }

        public void ClearCheck()
        {
            if (_check != null) _check.gameObject.SetActive(false);
        }

        public void ClearAll()
        {
            ClearSelection();
            ClearLegalMoves();
            ClearLastMove();
            ClearCheck();
        }

        // ------------------------------------------------------------------ animation

        void Update()
        {
            float t = Time.time;
            if (_selection != null && _selection.gameObject.activeSelf)
            {
                float s = 1f + 0.03f * Mathf.Sin(t * 4f);
                _selection.localScale = new Vector3(s, 1f, s);
            }
            float dot = 0.42f * (1f + 0.12f * Mathf.Sin(t * 3.5f));
            for (int i = 0; i < _legal.Count; i++)
                _legal[i].localScale = new Vector3(dot, 1f, dot);
            float ring = 1f + 0.05f * Mathf.Sin(t * 3.5f + 1f);
            for (int i = 0; i < _captures.Count; i++)
            {
                _captures[i].localScale = new Vector3(ring, 1f, ring);
                _captures[i].localRotation = Quaternion.Euler(0f, t * 25f, 0f);
            }
            if (_check != null && _check.gameObject.activeSelf)
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(t * 6f);
                _checkMaterial.SetColor(NeonMaterials.BaseColorProperty, _checkBase * (0.5f + pulse));
                float s = 1.4f + 0.2f * pulse;
                _check.localScale = new Vector3(s, 1f, s);
            }
        }

        void OnDestroy()
        {
            if (_checkMaterial != null) Destroy(_checkMaterial);
        }

        // ------------------------------------------------------------------ pool

        Transform Take(Material material)
        {
            Transform t;
            if (_pool.Count > 0)
            {
                t = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
                t.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            else
            {
                t = CreateQuad("Move", material);
            }
            t.gameObject.SetActive(true);
            return t;
        }

        void Release(Transform t)
        {
            t.gameObject.SetActive(false);
            _pool.Add(t);
        }

        Transform CreateQuad(string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            go.SetActive(false);
            return go.transform;
        }

        static void Place(Transform t, int file, int rank, float scale)
        {
            t.localPosition = BoardBuilder.SquareToWorld(file, rank) + Vector3.up * Height;
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(scale, 1f, scale);
            t.gameObject.SetActive(true);
        }

        /// <summary>Quad horizontal 1x1 tourné vers le haut, avec UV et couleurs de sommet blanches.</summary>
        static Mesh Quad()
        {
            if (_quad != null) return _quad;
            var b = new MeshBuilder(1);
            b.QuadUp(0,
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));
            _quad = b.ToMesh("KevChess_HighlightQuad");
            return _quad;
        }
    }
}
