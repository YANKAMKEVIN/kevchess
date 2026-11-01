using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using KevChess.AI;
using KevChess.AI.Fantasy;
using KevChess.Core;
using KevChess.Core.Fantasy;
using KevChess.Game.Match;
using UnityEngine;

namespace KevChess.Game.Legends
{
    /// <summary>Ce qu'il faut savoir pour animer une action (capturé avant que la partie n'avance).</summary>
    public sealed class ActionVisual
    {
        public FantasyAction Action;
        public int MovedPiece;
        public bool MoverIsWhite;
        public int CapturedPiece;
        public int CaptureSquare = Square.None;
        public int FinalSquare = Square.None;
        public int PortalFrom = Square.None, PortalTo = Square.None;
        public int CrystalSquare = Square.None;
        public int EnergyGained;
        public int[] FallenSquares = Array.Empty<int>();
        public int[] FallenPieces = Array.Empty<int>();
        public int[] CollapsedSquares = Array.Empty<int>();
        public BoardEvent SpawnedEvent;
        public bool GivesCheck;
        public bool EndsGame;

        public bool IsCapture => CapturedPiece != Piece.Empty;
        public bool IsPromotion => Action.IsPromotion;
        public bool IsCastle => Action.IsCastle;
    }

    /// <summary>Ce que le contrôleur attend de la vue du plateau Légendes.</summary>
    public interface ILegendsPresenter
    {
        void ShowPosition(FantasyGame game, PieceColor viewSide);
        IEnumerator AnimateAction(ActionVisual visual, FantasyGame game);
        void ShowState(FantasyGame game);
    }

    /// <summary>Déroulement d'une partie du mode Légendes : tours humain/IA, animations, annulation, sauvegarde.</summary>
    public sealed class LegendsController : MonoBehaviour
    {
        const float MinAIThinkSeconds = 0.5f;
        const string SaveKey = "kevchess.legends.v1";

        static FantasyAI engine;
        static FantasyAI Engine => engine ?? (engine = new FantasyAI(16));

        ILegendsPresenter board;
        CancellationTokenSource aiCts;
        int generation;
        bool paused;

        public FantasyGame Game { get; private set; }
        public MatchSettings Settings { get; private set; } = new MatchSettings();
        public bool IsAnimating { get; private set; }
        public bool IsAIThinking => aiCts != null;
        public bool IsAITurn => Game != null && !Game.IsOver && !Settings.IsHuman(Game.SideToMove);
        public bool CanHumanAct => Game != null && !Game.IsOver && !IsAnimating && !IsAIThinking && Settings.IsHuman(Game.SideToMove);
        public PieceColor ViewSide => Settings.Mode == MatchMode.VersusAI ? Settings.HumanColor : PieceColor.White;

        public event Action StateChanged;
        public event Action<ActionVisual> ActionStarted;
        public event Action<ActionVisual> ActionLanded;
        public event Action NewMatchStarted;
        public event Action GameEnded;

        public void Init(ILegendsPresenter presenter) => board = presenter;

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

        public void StartNew(MatchSettings settings)
        {
            ResetFlow();
            Settings = settings;
            Game = new FantasyGame((ulong)DateTime.UtcNow.Ticks ^ (ulong)UnityEngine.Random.Range(1, int.MaxValue));
            Engine.NewGame();
            Store();
            ShowAll();
        }

        public static bool HasSaved() => Load(out _, out var g) && !g.IsOver;

        public bool ResumeSaved()
        {
            if (!Load(out var settings, out var game) || game.IsOver) return false;
            ResetFlow();
            Settings = settings;
            Game = game;
            ShowAll();
            return true;
        }

        void ResetFlow()
        {
            CancelAI();
            StopAllCoroutines();
            IsAnimating = false;
            generation++;
        }

        void ShowAll()
        {
            board.ShowPosition(Game, ViewSide);
            board.ShowState(Game);
            NewMatchStarted?.Invoke();
            StateChanged?.Invoke();
            MaybeStartAI();
        }

        /// <summary>Après une mise en place manuelle de la position (recette automatique) : tout réafficher.</summary>
        public void RefreshAfterSetup()
        {
            Game.RefreshAfterSetup();
            board.ShowPosition(Game, ViewSide);
            board.ShowState(Game);
            StateChanged?.Invoke();
        }

        public bool TryHumanAction(FantasyAction action)
        {
            if (!CanHumanAct || !Game.IsLegal(action)) return false;
            StartCoroutine(PlayRoutine(action));
            return true;
        }

        IEnumerator PlayRoutine(FantasyAction action)
        {
            IsAnimating = true;
            bool completed = false;
            try
            {
                var pos = Game.Position;
                var visual = new ActionVisual
                {
                    Action = action,
                    MovedPiece = pos[action.From],
                    MoverIsWhite = pos.SideToMove == PieceColor.White,
                };
                Game.TryMakeAction(action);
                var r = Game.LastReport;
                visual.CapturedPiece = r.CapturedPiece;
                visual.CaptureSquare = r.CaptureSquare;
                visual.FinalSquare = r.FinalSquare;
                visual.PortalFrom = r.PortalFrom;
                visual.PortalTo = r.PortalTo;
                visual.CrystalSquare = r.CrystalSquare;
                visual.EnergyGained = r.EnergyGained;
                visual.FallenSquares = r.FallenSquares.ToArray();
                visual.FallenPieces = r.FallenPieces.ToArray();
                visual.CollapsedSquares = r.CollapsedSquares.ToArray();
                visual.SpawnedEvent = Game.LastEvent;
                visual.GivesCheck = Game.InCheck;
                visual.EndsGame = Game.IsOver;
                if (Game.IsOver) ClearSave(); else Store();

                Safe(ActionStarted, visual);
                StateChanged?.Invoke();
                yield return board.AnimateAction(visual, Game);
                board.ShowState(Game);
                completed = true;
                IsAnimating = false;
                Safe(ActionLanded, visual);
            }
            finally
            {
                IsAnimating = false;
                if (!completed && Game != null)
                {
                    board.ShowPosition(Game, ViewSide);
                    board.ShowState(Game);
                }
            }
            StateChanged?.Invoke();
            if (Game.IsOver) GameEnded?.Invoke();
            else MaybeStartAI();
        }

        static void Safe(Action<ActionVisual> h, ActionVisual v)
        {
            try { h?.Invoke(v); }
            catch (Exception e) { Debug.LogException(e); }
        }

        async void MaybeStartAI()
        {
            if (paused || !IsAITurn || IsAIThinking || IsAnimating) return;
            int gen = generation;
            var cts = new CancellationTokenSource();
            aiCts = cts;
            StateChanged?.Invoke();

            FantasyAction action = FantasyAction.Null;
            float started = Time.realtimeSinceStartup;
            try
            {
                action = await Engine.FindActionAsync(Game, (Difficulty)(int)Settings.Level, cts.Token);
                Debug.Log("[KevChess] IA Légendes " + MatchSettings.LevelName(Settings.Level) + " : " +
                          Mathf.RoundToInt((Time.realtimeSinceStartup - started) * 1000f) + " ms (" + Engine.LastResult + ")");
                float wait = MinAIThinkSeconds - (Time.realtimeSinceStartup - started);
                if (wait > 0) await Task.Delay(TimeSpan.FromSeconds(wait), cts.Token);
            }
            catch (OperationCanceledException) { action = FantasyAction.Null; }
            catch (Exception e) { Debug.LogException(e); action = FantasyAction.Null; }
            finally
            {
                if (aiCts == cts) aiCts = null;
                cts.Dispose();
            }

            if (this == null) return;
            if (gen != generation || Game == null || !IsAITurn) { StateChanged?.Invoke(); return; }
            if (action.IsNull || !Game.IsLegal(action))
            {
                Debug.LogWarning("IA Légendes : action invalide, première action légale jouée à la place");
                action = Game.LegalActions[0];
            }
            StartCoroutine(PlayRoutine(action));
        }

        void CancelAI()
        {
            if (aiCts == null) return;
            aiCts.Cancel();
            aiCts = null;
        }

        public bool UndoForHuman()
        {
            if (Game == null || IsAnimating || Game.Actions.Count == 0) return false;
            bool wasThinking = IsAIThinking;
            CancelAI();
            generation++;
            if (Settings.Mode == MatchMode.VersusAI)
            {
                if (wasThinking || !Settings.IsHuman(Game.SideToMove)) Game.Undo();
                else
                {
                    Game.Undo();
                    if (!Settings.IsHuman(Game.SideToMove) && Game.Actions.Count > 0) Game.Undo();
                }
            }
            else Game.Undo();
            Store();
            board.ShowPosition(Game, ViewSide);
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
            Game.Resign(Settings.Mode == MatchMode.VersusAI ? Settings.HumanColor : Game.SideToMove);
            ClearSave();
            board.ShowState(Game);
            StateChanged?.Invoke();
            GameEnded?.Invoke();
        }

        /// <summary>Abandonne le flux en cours (sortie du mode) sans toucher à la sauvegarde.</summary>
        public void Suspend()
        {
            if (Game != null && !Game.IsOver) Store();
            ResetFlow();
        }

        public async void RequestHint(Action<FantasyAction> onHint)
        {
            if (!CanHumanAct) return;
            int gen = generation, ply = Game.Actions.Count;
            FantasyAction hint;
            try { hint = await Engine.SuggestActionAsync(Game, 800, CancellationToken.None); }
            catch (Exception e) { Debug.LogException(e); return; }
            if (this == null || gen != generation || Game == null || Game.Actions.Count != ply || hint.IsNull) return;
            onHint?.Invoke(hint);
        }

        // ---------------------------------------------------------------- sauvegarde

        [Serializable]
        sealed class SaveData
        {
            public MatchSettings Settings = new MatchSettings();
            public string Game;
        }

        void Store()
        {
            if (Game == null) return;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SaveData { Settings = Settings, Game = Game.Save() }));
            PlayerPrefs.Save();
        }

        public void StoreNow() => Store();

        static void ClearSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }

        static bool Load(out MatchSettings settings, out FantasyGame game)
        {
            settings = null;
            game = null;
            if (!PlayerPrefs.HasKey(SaveKey)) return false;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
                if (data == null) return false;
                game = FantasyGame.Load(data.Game);
                settings = data.Settings ?? new MatchSettings();
                return game != null;
            }
            catch (Exception) { return false; }
        }

        void OnDestroy() => CancelAI();
    }
}
