using System.Collections;
using UnityEngine;

namespace KevChess.Game.Visuals.Fantasy
{
    /// <summary>
    /// Vitrine des personnages du mode Légendes, sur le modèle de <see cref="VisualsDemo"/> :
    /// construit le plateau, aligne les sept personnages des deux couleurs et joue en boucle
    /// repos, marche / galop / vol, frappes et réactions, bouclier, gel, téléportation,
    /// invocation du dragon et morts. Sans dépendance aux règles.
    /// Menu éditeur : KevChess > Visuals > Démo des personnages (Légendes).
    /// </summary>
    public sealed class FantasyDemo : MonoBehaviour
    {
        [SerializeField] bool loop = true;
        [SerializeField] float pause = 0.5f;

        static readonly CharacterKind[] Lineup =
        {
            CharacterKind.Sentinel, CharacterKind.Knight, CharacterKind.Mage, CharacterKind.Golem,
            CharacterKind.Queen, CharacterKind.King, CharacterKind.Dragon,
        };

        readonly FantasyPieceView[] _white = new FantasyPieceView[7];
        readonly FantasyPieceView[] _black = new FantasyPieceView[7];
        Transform _root;

        void Awake()
        {
            if (FindAnyObjectByType<SceneAtmosphere>() == null)
                gameObject.AddComponent<SceneAtmosphere>();
            BoardBuilder.Build(transform);
        }

        IEnumerator Start()
        {
            do
            {
                Setup();
                yield return new WaitForSeconds(2f);
                yield return Sequence();
                yield return new WaitForSeconds(1.5f);
            } while (loop);
        }

        void Setup()
        {
            if (_root != null) Destroy(_root.gameObject);
            _root = new GameObject("Characters").transform;
            _root.SetParent(transform, false);
            for (int i = 0; i < 7; i++)
            {
                _white[i] = FantasyPieceView.Create(Lineup[i], true, i, 1, _root);
                _black[i] = FantasyPieceView.Create(Lineup[i], false, i, 6, _root);
            }
        }

        IEnumerator Sequence()
        {
            // Sélection + regard des voisins.
            _white[4].SetSelected(true);
            foreach (FantasyPieceView v in _white) if (v != _white[4]) v.SetLookTarget(_white[4].transform.position);
            yield return new WaitForSeconds(1.2f);
            _white[4].SetSelected(false);
            foreach (FantasyPieceView v in _white) v.SetLookTarget(null);

            // Avance : marche, galop, vol. Les deux camps se retrouvent face à face (rangées 3 et 4).
            for (int i = 0; i < 7; i++)
            {
                _white[i].MoveTo(i, 3);
                yield return new WaitForSeconds(0.18f);
            }
            yield return new WaitForSeconds(1f);
            for (int i = 0; i < 7; i++)
            {
                _black[i].MoveTo(i, 4);
                yield return new WaitForSeconds(0.18f);
            }
            yield return new WaitForSeconds(1.2f);

            // Frappes : chaque blanc attaque son vis-à-vis, qui réagit, puis riposte.
            for (int i = 0; i < 7; i++)
            {
                FantasyPieceView a = _white[i], v = _black[i];
                yield return a.PlayAttack(v.transform.position, -1f, () => v.PlayHitReaction(a.transform.position));
                yield return new WaitForSeconds(pause * 0.4f);
            }
            for (int i = 0; i < 7; i++)
            {
                FantasyPieceView a = _black[i], v = _white[i];
                yield return a.PlayAttack(v.transform.position, -1f, () => v.PlayHitReaction(a.transform.position));
                yield return new WaitForSeconds(pause * 0.4f);
            }

            // Statuts : bouclier qui encaisse, gel.
            _white[5].SetShielded(true);
            _black[4].SetFrozen(true);
            yield return new WaitForSeconds(1f);
            yield return _black[5].PlayAttack(_white[5].transform.position, -1f, () => _white[5].PlayHitReaction(_black[5].transform.position));
            yield return new WaitForSeconds(1.2f);
            _white[5].SetShielded(false);
            _black[4].SetFrozen(false);

            // Téléportation sur la case voisine (rangée 2, vide).
            yield return _white[2].Teleport(2, 2);
            yield return new WaitForSeconds(pause);

            // Invocation : le golem blanc devient un dragon.
            yield return _white[3].TransformToDragon();
            yield return new WaitForSeconds(1.2f);

            // Duel final : le nouveau dragon dévore la sentinelle noire, le roi noir abat le chevalier blanc.
            FantasyPieceView prey = _black[0];
            yield return _white[3].PlayAttack(prey.transform.position, -1f, () => prey.PlayDeath());
            yield return new WaitForSeconds(pause);
            FantasyPieceView knight = _white[1];
            yield return _black[5].PlayAttack(knight.transform.position, -1f, () => knight.PlayDeath());
            yield return new WaitForSeconds(pause);
        }
    }
}
