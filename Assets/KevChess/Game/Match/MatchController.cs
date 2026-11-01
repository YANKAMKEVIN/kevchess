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
    /// Déroulement d'une partie : tours humain/IA, pendule, animations, annulation, indice, sauvegarde, puzzles.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        const float MinAIThinkSeconds = 0.45f;

        const float PuzzleReplyDelay = 0.5f;

        IBoardPresenter board;
        CancellationTokenSource aiCts;
        bool aiMovePending; // coup de l'IA calculé, en attente de la fin de la pause
        int generation; // invalide les résultats d'IA d'une partie précédente

        public ChessGame Game { get; private set; }
        public MatchSettings Settings { get; private set; } = new MatchSettings();
        /// <summary>Pendule de la partie (sans limite par défaut).</summary>
        public ChessClock Clock { get; private set; } = new ChessClock(TimeControl.Unlimited);
        /// <summary>Puzzle en cours (mode Puzzle), sinon null.</summary>
        public PuzzleSession Puzzle { get; private set; }
        public bool IsPuzzle => Settings.Mode == MatchMode.Puzzle && Puzzle != null;
        public bool IsAnimating { get; private set; }
        public bool IsAIThinking => aiCts != null || aiMovePending;
        public bool IsAITurn => Game != null && !Game.IsOver && Settings.Mode == MatchMode.VersusAI && !Settings.IsHuman(Game.SideToMove);
        /// <summary>Mode revue : le plateau montre une position passée, personne ne joue.</summary>
        public bool Reviewing { get; set; }

        public bool CanHumanAct =>
            Game != null && !Game.IsOver && !IsAnimating && !IsAIThinking && !Reviewing && Settings.IsHuman(Game.SideToMove) &&
            (Puzzle == null || !Puzzle.IsSolved);

        /// <summary>Camp vu « depuis le bas » : le joueur humain, ou le camp au trait en local.</summary>
        public PieceColor ViewSide => Settings.Mode == MatchMode.LocalTwoPlayers ? PieceColor.White : Settings.HumanColor;

        public event Action StateChanged;
        public event Action<MoveVisual> MoveStarted;
        public event Action<MoveVisual> MoveLanded;
        public event Action NewMatchStarted;
        public event Action GameEnded;
        /// <summary>Résultat d'un coup proposé dans un puzzle (après son animation).</summary>
        public event Action<PuzzleMoveResult> PuzzleFeedback;

        public void Init(IBoardPresenter presenter) => board = presenter;

        public void StartNew(MatchSettings settings, string fen = null)
        {
            InvalidateAI();
            StopAllCoroutines();
            IsAnimating = false;
            Reviewing = false;
            Puzzle = null;
            Settings = settings;
            Game = new ChessGame(fen ?? Position.StartFen);
            Clock = new ChessClock(settings.Clock);
            Clock.Start(Game.SideToMove);
            AIAdapter.NewGame();
            SavedMatch.Store(Settings, Game, Clock);
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
            InvalidateAI();
            StopAllCoroutines();
            IsAnimating = false;
            Reviewing = false;
            Puzzle = null;
            Settings = save.Settings;
            Game = game;
            Clock = save.RebuildClock();
            Clock.Start(Game.SideToMove);
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
                StartCoroutine(IsPuzzle ? PuzzleRoutine(m) : PlayRoutine(m));
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
                var mover = Game.SideToMove;
                Game.TryMakeMove(move);
                Clock.Press(mover);
                if (Game.IsOver) SavedMatch.Clear();
                else SavedMatch.Store(Settings, Game, Clock);

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

        // ---------------------------------------------------------------- Puzzles

        /// <summary>Lance un puzzle : position du problème, le joueur a le trait, l'adversaire répond seul.</summary>
        public void StartPuzzle(Puzzle puzzle)
        {
            InvalidateAI();
            StopAllCoroutines();
            IsAnimating = false;
            Reviewing = false;
            Puzzle = new PuzzleSession(puzzle);
            Game = Puzzle.Game;
            Settings = new MatchSettings { Mode = MatchMode.Puzzle, HumanColor = Puzzle.PlayerColor, PuzzleId = puzzle.Id };
            Clock = new ChessClock(TimeControl.Unlimited);
            board.ShowPosition(Game.Position, ViewSide);
            board.ShowState(Game);
            NewMatchStarted?.Invoke();
            StateChanged?.Invoke();
        }

        IEnumerator PuzzleRoutine(Move move)
        {
            IsAnimating = true;
            bool completed = false;
            PuzzleMoveResult result;
            try
            {
                var visual = MoveVisual.From(Game.Position, move);
                result = Puzzle.TryMove(move, out var reply);
                if (result == PuzzleMoveResult.Wrong)
                {
                    // Coup faux : on le montre, puis la pièce revient à sa place.
                    Game.TryMakeMove(move);
                    SafeInvoke(MoveStarted, visual);
                    StateChanged?.Invoke();
                    yield return board.AnimateMove(visual);
                    PuzzleFeedback?.Invoke(result);
                    yield return new WaitForSeconds(0.35f);
                    var back = MoveVisual.From(Game.Position, new Move(move.To, move.From));
                    Game.Undo();
                    yield return board.AnimateMove(back);
                    board.ShowPosition(Game.Position, ViewSide);
                    board.ShowState(Game);
                }
                else
                {
                    // La réponse adverse est retirée le temps d'animer le coup du joueur, puis rejouée.
                    if (!reply.IsNull) Game.Undo();
                    SafeInvoke(MoveStarted, visual);
                    StateChanged?.Invoke();
                    yield return board.AnimateMove(visual);
                    board.ShowState(Game);
                    SafeInvoke(MoveLanded, visual);
                    if (!reply.IsNull)
                    {
                        PuzzleFeedback?.Invoke(PuzzleMoveResult.Correct);
                        yield return new WaitForSeconds(PuzzleReplyDelay);
                        var replyVisual = MoveVisual.From(Game.Position, reply);
                        Game.TryMakeMove(reply);
                        SafeInvoke(MoveStarted, replyVisual);
                        StateChanged?.Invoke();
                        yield return board.AnimateMove(replyVisual);
                        board.ShowState(Game);
                        SafeInvoke(MoveLanded, replyVisual);
                    }
                    if (result == PuzzleMoveResult.Solved) PuzzleFeedback?.Invoke(result);
                }
                completed = true;
            }
            finally
            {
                IsAnimating = false;
                if (!completed && Game != null)
                {
                    board.ShowPosition(Game.Position, ViewSide);
                    board.ShowState(Game);
                }
            }
            StateChanged?.Invoke();
        }

        // ---------------------------------------------------------------- Pendule

        void Update()
        {
            if (Game == null || Game.IsOver || Clock == null || Clock.IsUnlimited) return;
            // La pendule s'arrête quand un écran est ouvert et pendant les animations.
            bool hold = paused || IsAnimating || Reviewing;
            if (hold && !Clock.IsPaused) Clock.Pause();
            else if (!hold && Clock.IsPaused) Clock.Resume();
            Clock.Tick(Time.deltaTime);
            if (Clock.IsFlagged(out var loser)) FlagFall(loser);
        }

        void FlagFall(PieceColor loser)
        {
            InvalidateAI();
            Game.Timeout(loser);
            SavedMatch.Clear();
            board.ShowState(Game);
            StateChanged?.Invoke();
            GameEnded?.Invoke();
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
                var aiSide = Game.SideToMove;
                move = await AIAdapter.FindMoveAsync(Game, Settings.Level, Clock.Remaining(aiSide), Clock.Control.IncrementSeconds, cts.Token);
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
            if (!paused) { StartCoroutine(PlayRoutine(move)); return; }
            // Un écran s'est ouvert pendant la réflexion : le coup attend qu'il se ferme.
            aiMovePending = true;
            StartCoroutine(PlayWhenUnpaused(move, gen));
        }

        IEnumerator PlayWhenUnpaused(Move move, int gen)
        {
            while (paused && gen == generation) yield return null;
            if (gen != generation) yield break;
            aiMovePending = false;
            if (Game == null || !IsAITurn || !Game.IsLegal(move)) { StateChanged?.Invoke(); yield break; }
            yield return PlayRoutine(move);
        }

        void CancelAI()
        {
            if (aiCts == null) return;
            aiCts.Cancel();
            aiCts = null;
        }

        /// <summary>Annule la réflexion de l'IA et périme tout résultat en cours.</summary>
        void InvalidateAI()
        {
            CancelAI();
            aiMovePending = false;
            generation++;
        }

        /// <summary>Annule le dernier coup du joueur (et la réponse de l'IA le cas échéant).</summary>
        public bool UndoForHuman()
        {
            if (Game == null || IsAnimating || Game.Moves.Count == 0 || IsPuzzle) return false;
            bool wasThinking = IsAIThinking;
            InvalidateAI();

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

            if (Clock.IsFlagged(out _)) Clock = new ChessClock(Settings.Clock);
            Clock.Start(Game.SideToMove); // les temps restants sont conservés
            SavedMatch.Store(Settings, Game, Clock);
            board.ShowPosition(Game.Position, ViewSide);
            board.ShowState(Game);
            StateChanged?.Invoke();
            MaybeStartAI();
            return true;
        }

        public void Resign()
        {
            if (Game == null || Game.IsOver || IsAnimating || IsPuzzle) return;
            InvalidateAI();
            var loser = Settings.Mode == MatchMode.VersusAI ? Settings.HumanColor : Game.SideToMove;
            Game.Resign(loser);
            SavedMatch.Clear();
            board.ShowState(Game);
            StateChanged?.Invoke();
            GameEnded?.Invoke();
        }

        /// <summary>Nulle acceptée (par l'IA ou par l'autre joueur).</summary>
        public void AgreeDraw()
        {
            if (Game == null || Game.IsOver || IsAnimating || IsPuzzle) return;
            InvalidateAI();
            Game.AgreeDraw();
            SavedMatch.Clear();
            board.ShowState(Game);
            StateChanged?.Invoke();
            GameEnded?.Invoke();
        }

        /// <summary>
        /// Reprend la partie depuis le demi-coup donné (mode revue) : l'historique suivant est supprimé.
        /// Faux si la position est déjà finale.
        /// </summary>
        public bool ResumeFrom(int ply)
        {
            if (Game == null || IsAnimating || IsPuzzle || ply < 0 || ply > Game.Moves.Count) return false;
            var game = new ChessGame(Game.StartFen);
            for (int i = 0; i < ply; i++)
                if (!game.TryMakeMove(Game.Moves[i])) return false;
            if (game.IsOver) return false;

            InvalidateAI();
            StopAllCoroutines();
            Reviewing = false;
            Game = game;
            // Après une chute au temps, la pendule repart de la cadence de départ.
            if (Clock.IsFlagged(out _)) Clock = new ChessClock(Settings.Clock);
            Clock.Start(Game.SideToMove);
            SavedMatch.Store(Settings, Game, Clock);
            board.ShowPosition(Game.Position, ViewSide);
            board.ShowState(Game);
            NewMatchStarted?.Invoke();
            StateChanged?.Invoke();
            MaybeStartAI();
            return true;
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
