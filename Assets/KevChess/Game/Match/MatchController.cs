using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using KevChess.Core;
using UnityEngine;

namespace KevChess.Game.Match
{
    /// <summary>Ce que le contrôleur attend de la vue 3D du plateau.</summary>
    public interface IBoardPresenter
    {
        /// <summary>Replace toutes les pièces sans animation (nouvelle partie, annulation, reprise).</summary>
        void ShowPosition(Position position, PieceColor viewSide);
        /// <summary>Anime un coup déjà joué dans la partie.</summary>
        IEnumerator AnimateMove(MoveVisual visual);
        /// <summary>Met à jour les marqueurs persistants : dernier coup, roi en échec.</summary>
        void ShowState(ChessGame game);
    }

    /// <summary>
    /// Déroulement d'une partie : tours humain/IA, animations, annulation, indice, sauvegarde.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        const float MinAIThinkSeconds = 0.45f;

        IBoardPresenter board;
        CancellationTokenSource aiCts;
        int generation; // invalide les résultats d'IA d'une partie précédente

        public ChessGame Game { get; private set; }
        public MatchSettings Settings { get; private set; } = new MatchSettings();
        public bool IsAnimating { get; private set; }
        public bool IsAIThinking => aiCts != null;
        public bool IsAITurn => Game != null && !Game.IsOver && !Settings.IsHuman(Game.SideToMove);

        public bool CanHumanAct =>
            Game != null && !Game.IsOver && !IsAnimating && !IsAIThinking && Settings.IsHuman(Game.SideToMove);

        /// <summary>Camp vu « depuis le bas » : le joueur humain, ou le camp au trait en local.</summary>
        public PieceColor ViewSide => Settings.Mode == MatchMode.VersusAI ? Settings.HumanColor : PieceColor.White;

        public event Action StateChanged;
        public event Action<MoveVisual> MoveStarted;
        public event Action<MoveVisual> MoveLanded;
        public event Action NewMatchStarted;
        public event Action GameEnded;

        public void Init(IBoardPresenter presenter) => board = presenter;

        public void StartNew(MatchSettings settings, string fen = null)
        {
            CancelAI();
            StopAllCoroutines();
            IsAnimating = false;
            generation++;
            Settings = settings;
            Game = new ChessGame(fen ?? Position.StartFen);
            AIAdapter.NewGame();
            SavedMatch.Store(Settings, Game);
            board.ShowPosition(Game.Position, ViewSide);
            board.ShowState(Game);
            NewMatchStarted?.Invoke();
            StateChanged?.Invoke();
            MaybeStartAI();
        }

        public static bool HasSavedMatch()
        {
            var save = SavedMatch.Load();
            return save != null && save.Rebuild() is ChessGame g && !g.IsOver;
        }

        public bool ResumeSaved()
        {
            var save = SavedMatch.Load();
            var game = save?.Rebuild();
            if (game == null || game.IsOver) return false;
            CancelAI();
            StopAllCoroutines();
            IsAnimating = false;
            generation++;
            Settings = save.Settings;
            Game = game;
            board.ShowPosition(Game.Position, ViewSide);
            board.ShowState(Game);
            NewMatchStarted?.Invoke();
            StateChanged?.Invoke();
            MaybeStartAI();
            return true;
        }

        /// <summary>Coup proposé par le joueur humain (promotion : type choisi, ou None si sans objet).</summary>
        public bool TryHumanMove(int from, int to, PieceType promotion = PieceType.None)
        {
            if (!CanHumanAct) return false;
            foreach (var m in Game.LegalMoves)
            {
                if (m.From != from || m.To != to) continue;
                if (m.IsPromotion && m.Promotion != (promotion == PieceType.None ? PieceType.Queen : promotion)) continue;
                StartCoroutine(PlayRoutine(m));
                return true;
            }
            return false;
        }

        IEnumerator PlayRoutine(Move move)
        {
            IsAnimating = true;
            bool completed = false;
            try
            {
                var visual = MoveVisual.From(Game.Position, move);
                Game.TryMakeMove(move);
                if (Game.IsOver) SavedMatch.Clear();
                else SavedMatch.Store(Settings, Game);

                SafeInvoke(MoveStarted, visual);
                StateChanged?.Invoke();
                yield return board.AnimateMove(visual);
                board.ShowState(Game);
                completed = true;
                IsAnimating = false;
                SafeInvoke(MoveLanded, visual);
            }
            finally
            {
                // Une exception (ou un arrêt) ne doit jamais geler la partie.
                IsAnimating = false;
                if (!completed && Game != null)
                {
                    board.ShowPosition(Game.Position, ViewSide);
                    board.ShowState(Game);
                }
            }

            StateChanged?.Invoke();
            if (Game.IsOver) GameEnded?.Invoke();
            else MaybeStartAI();
        }

        static void SafeInvoke(Action<MoveVisual> handler, MoveVisual v)
        {
            try { handler?.Invoke(v); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>Partie en pause (menu ouvert) : l'IA attend avant de jouer.</summary>
        public bool Paused
        {
            get => paused;
            set
            {
                if (paused == value) return;
                paused = value;
                if (!paused) MaybeStartAI();
            }
        }
        bool paused;

        async void MaybeStartAI()
        {
            if (paused || !IsAITurn || IsAIThinking || IsAnimating) return;

            int gen = generation;
            var cts = new CancellationTokenSource();
            aiCts = cts;
            StateChanged?.Invoke();

            Move move = Move.Null;
            float started = Time.realtimeSinceStartup;
            try
            {
                move = await AIAdapter.FindMoveAsync(Game, Settings.Level, cts.Token);
                Debug.Log("[KevChess] IA " + MatchSettings.LevelName(Settings.Level) + " : " +
                          Mathf.RoundToInt((Time.realtimeSinceStartup - started) * 1000f) + " ms (" + AIAdapter.LastSearchSummary() + ")");
                float wait = MinAIThinkSeconds - (Time.realtimeSinceStartup - started);
                if (wait > 0) await Task.Delay(TimeSpan.FromSeconds(wait), cts.Token);
            }
            catch (OperationCanceledException)
            {
                move = Move.Null;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                move = Move.Null;
            }
            finally
            {
                if (aiCts == cts) aiCts = null;
                cts.Dispose();
            }

            // Résultat périmé (annulation, nouvelle partie) : on l'ignore.
            if (this == null) return; // objet détruit (fermeture de l'app)
            if (gen != generation || Game == null || !IsAITurn) { StateChanged?.Invoke(); return; }

            if (move.IsNull || !Game.IsLegal(move))
            {
                // Filet de sécurité : ne jamais bloquer la partie.
                Debug.LogWarning("IA : coup invalide, premier coup légal joué à la place");
                move = Game.LegalMoves[0];
            }
            StartCoroutine(PlayRoutine(move));
        }

        void CancelAI()
        {
            if (aiCts == null) return;
            aiCts.Cancel();
            aiCts = null;
        }

        /// <summary>Annule le dernier coup du joueur (et la réponse de l'IA le cas échéant).</summary>
        public bool UndoForHuman()
        {
            if (Game == null || IsAnimating || Game.Moves.Count == 0) return false;
            bool wasThinking = IsAIThinking;
            CancelAI();
            generation++;

            if (Settings.Mode == MatchMode.VersusAI)
            {
                // Revenir au dernier moment où c'était à l'humain de jouer, au moins un coup en arrière.
                if (wasThinking || !Settings.IsHuman(Game.SideToMove)) Game.Undo();
                else
                {
                    Game.Undo();
                    if (!Settings.IsHuman(Game.SideToMove) && Game.Moves.Count > 0) Game.Undo();
                }
                // L'IA joue les blancs et n'a joué qu'un coup : on lui rend la main.
            }
            else
            {
                Game.Undo();
            }

            SavedMatch.Store(Settings, Game);
            board.ShowPosition(Game.Position, ViewSide);
            board.ShowState(Game);
            StateChanged?.Invoke();
            MaybeStartAI();
            return true;
        }

        public void Resign()
        {
            if (Game == null || Game.IsOver || IsAnimating) return;
            CancelAI();
            generation++;
            var loser = Settings.Mode == MatchMode.VersusAI ? Settings.HumanColor : Game.SideToMove;
            Game.Resign(loser);
            SavedMatch.Clear();
            board.ShowState(Game);
            StateChanged?.Invoke();
            GameEnded?.Invoke();
        }

        /// <summary>Demande un indice à l'IA pour le joueur au trait.</summary>
        public async void RequestHint(Action<Move> onHint)
        {
            if (!CanHumanAct) return;
            int gen = generation;
            int ply = Game.Moves.Count;
            Move hint;
            try { hint = await AIAdapter.SuggestAsync(Game, CancellationToken.None); }
            catch (Exception e) { Debug.LogException(e); return; }
            if (this == null || gen != generation || Game == null || Game.Moves.Count != ply || hint.IsNull) return;
            onHint?.Invoke(hint);
        }

        void OnDestroy() => CancelAI();
    }
}
