using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KevChess.Game.Visuals
{
    /// <summary>
    /// Vitrine autonome de la partie visuelle : construit le plateau, place les 32 pièces et
    /// joue en boucle une courte séquence (sélection, coups légaux, déplacements, saut du cavalier,
    /// prise, échec, promotion). Sans dépendance aux règles : utile pour régler l'ambiance.
    /// Mettre ce composant sur un objet vide d'une scène avec une caméra (ajoute SceneAtmosphere si absent).
    /// </summary>
    public sealed class VisualsDemo : MonoBehaviour
    {
        [SerializeField] bool loop = true;
        [SerializeField] float pauseBetweenMoves = 0.55f;

        readonly Dictionary<Vector2Int, PieceView> _pieces = new Dictionary<Vector2Int, PieceView>();
        Transform _piecesRoot;
        BoardHighlights _highlights;

        static readonly PieceKind[] BackRank =
        {
            PieceKind.Rook, PieceKind.Knight, PieceKind.Bishop, PieceKind.Queen,
            PieceKind.King, PieceKind.Bishop, PieceKind.Knight, PieceKind.Rook
        };

        void Awake()
        {
            if (FindAnyObjectByType<SceneAtmosphere>() == null)
                gameObject.AddComponent<SceneAtmosphere>();
            BoardBuilder.Build(transform);
            _highlights = BoardHighlights.Create(transform);
        }

        IEnumerator Start()
        {
            do
            {
                SetupPieces();
                yield return new WaitForSeconds(1f);
                yield return Sequence();
                yield return new WaitForSeconds(1.5f);
            } while (loop);
        }

        void SetupPieces()
        {
            if (_piecesRoot != null) Destroy(_piecesRoot.gameObject);
            _pieces.Clear();
            _highlights.ClearAll();
            _piecesRoot = new GameObject("Pieces").transform;
            _piecesRoot.SetParent(transform, false);
            for (int f = 0; f < 8; f++)
            {
                Add(BackRank[f], true, f, 0);
                Add(PieceKind.Pawn, true, f, 1);
                Add(PieceKind.Pawn, false, f, 6);
                Add(BackRank[f], false, f, 7);
            }
        }

        void Add(PieceKind kind, bool white, int f, int r)
        {
            _pieces[new Vector2Int(f, r)] = PieceView.Create(kind, white, f, r, _piecesRoot);
        }

        IEnumerator Sequence()
        {
            // Séquence illustrative (pas une partie validée par les règles).
            yield return Move(4, 1, 4, 3);                 // e4
            yield return Move(4, 6, 4, 4);                 // e5
            yield return Move(6, 0, 5, 2);                 // Cf3 (saut)
            yield return Move(1, 7, 2, 5);                 // Cc6
            yield return Move(5, 2, 4, 4);                 // Cxe5 (prise)
            yield return Move(2, 5, 4, 4);                 // Cxe5
            yield return Move(3, 0, 7, 4);                 // Dh5
            yield return Move(6, 6, 6, 5);                 // g6
            yield return Move(7, 4, 4, 4);                 // Dxe5+ (échec)
            _highlights.ShowCheck(4, 7);
            yield return new WaitForSeconds(1.2f);
            _highlights.ClearCheck();
            yield return Move(3, 7, 4, 6);                 // De7
            // Promotion de démonstration : un pion blanc « téléporté » en 7e rangée.
            if (_pieces.TryGetValue(new Vector2Int(0, 1), out PieceView pawn))
            {
                _pieces.Remove(new Vector2Int(0, 1));
                pawn.SnapTo(0, 5);
                _pieces[new Vector2Int(0, 5)] = pawn;
                yield return new WaitForSeconds(0.4f);
                yield return Move(0, 5, 1, 6);             // axb7
                yield return Move(1, 6, 0, 7, PieceKind.Queen); // xa8=D
            }
        }

        IEnumerator Move(int ff, int fr, int tf, int tr, PieceKind? promotion = null)
        {
            var from = new Vector2Int(ff, fr);
            var to = new Vector2Int(tf, tr);
            if (!_pieces.TryGetValue(from, out PieceView piece)) yield break;

            // Sélection + coups légaux illustratifs.
            piece.SetSelected(true);
            _highlights.ShowSelection(ff, fr);
            var quiet = new List<Vector2Int>();
            var captures = new List<Vector2Int>();
            if (_pieces.ContainsKey(to)) captures.Add(to); else quiet.Add(to);
            _highlights.ShowLegalMoves(quiet, captures);
            yield return new WaitForSeconds(pauseBetweenMoves);

            _highlights.ClearSelection();
            _highlights.ClearLegalMoves();
            _pieces.TryGetValue(to, out PieceView victim);
            _pieces.Remove(from);
            _pieces[to] = piece;

            yield return piece.MoveTo(tf, tr, () =>
            {
                if (victim != null) victim.Capture();
            });
            _highlights.ShowLastMove(ff, fr, tf, tr);
            if (promotion.HasValue)
                yield return piece.Promote(promotion.Value);
            yield return new WaitForSeconds(pauseBetweenMoves * 0.5f);
        }
    }
}
