using System;
using System.Collections.Generic;

namespace KevChess.Core
{
    public enum GameResult { Ongoing, WhiteWins, BlackWins, Draw }

    public enum GameEndReason
    {
        None,
        Checkmate,
        Stalemate,
        FiftyMoveRule,
        ThreefoldRepetition,
        InsufficientMaterial,
        Resignation,
        DrawAgreement,
    }

    /// <summary>
    /// Partie complète : coups légaux, historique (SAN), annulation et détection de fin de partie.
    /// Les nulles par répétition triple et règle des 50 coups sont déclarées automatiquement.
    /// </summary>
    public sealed class ChessGame
    {
        readonly Position position;
        readonly string startFen;
        readonly List<Move> moves = new List<Move>();
        readonly List<string> sanMoves = new List<string>();
        List<Move> legalMoves;

        public ChessGame() : this(Position.StartFen) { }

        public ChessGame(string fen)
        {
            startFen = fen;
            position = Position.FromFen(fen);
            Refresh();
        }

        /// <summary>Position courante. Ne pas la modifier directement : passer par TryMakeMove / Undo.</summary>
        public Position Position => position;
        public string StartFen => startFen;
        public PieceColor SideToMove => position.SideToMove;
        public IReadOnlyList<Move> Moves => moves;
        public IReadOnlyList<string> SanMoves => sanMoves;
        public IReadOnlyList<Move> LegalMoves => legalMoves;
        public GameResult Result { get; private set; }
        public GameEndReason EndReason { get; private set; }
        public bool IsOver => Result != GameResult.Ongoing;
        public bool InCheck => position.InCheck;
        public Move LastMove => moves.Count == 0 ? Move.Null : moves[moves.Count - 1];

        /// <summary>Déclenché après chaque coup joué, annulé ou fin de partie.</summary>
        public event Action Changed;

        public IEnumerable<Move> LegalMovesFrom(int square)
        {
            foreach (var m in legalMoves)
                if (m.From == square) yield return m;
        }

        public bool IsLegal(Move move)
        {
            if (IsOver) return false;
            foreach (var m in legalMoves) if (m == move) return true;
            return false;
        }

        /// <summary>Joue un coup désigné par ses cases (promotion en dame par défaut).</summary>
        public bool TryMakeMove(int from, int to, PieceType promotion = PieceType.None)
        {
            if (IsOver) return false;
            bool needsPromotion = false;
            foreach (var m in legalMoves)
            {
                if (m.From != from || m.To != to) continue;
                if (!m.IsPromotion) return TryMakeMove(m);
                needsPromotion = true;
            }
            if (!needsPromotion) return false;
            return TryMakeMove(new Move(from, to,
                position[to] != Piece.Empty ? MoveFlags.Capture : MoveFlags.None,
                promotion == PieceType.None ? PieceType.Queen : promotion));
        }

        public bool TryMakeMove(Move move)
        {
            if (!IsLegal(move)) return false;
            sanMoves.Add(Notation.ToSan(position, move));
            position.MakeMove(move);
            moves.Add(move);
            Refresh();
            Changed?.Invoke();
            return true;
        }

        public bool TryMakeSan(string san)
        {
            var m = Notation.Parse(position, san);
            return !m.IsNull && TryMakeMove(m);
        }

        /// <summary>Indique si ce couple de cases demande un choix de promotion.</summary>
        public bool IsPromotionMove(int from, int to)
        {
            foreach (var m in legalMoves)
                if (m.From == from && m.To == to && m.IsPromotion) return true;
            return false;
        }

        public bool Undo()
        {
            if (moves.Count == 0) return false;
            position.UnmakeMove();
            moves.RemoveAt(moves.Count - 1);
            sanMoves.RemoveAt(sanMoves.Count - 1);
            Refresh();
            Changed?.Invoke();
            return true;
        }

        public void Resign(PieceColor loser)
        {
            if (IsOver) return;
            Result = loser == PieceColor.White ? GameResult.BlackWins : GameResult.WhiteWins;
            EndReason = GameEndReason.Resignation;
            Changed?.Invoke();
        }

        public void AgreeDraw()
        {
            if (IsOver) return;
            Result = GameResult.Draw;
            EndReason = GameEndReason.DrawAgreement;
            Changed?.Invoke();
        }

        void Refresh()
        {
            legalMoves = MoveGenerator.Legal(position);
            Result = GameResult.Ongoing;
            EndReason = GameEndReason.None;

            // Le mat et le pat priment sur les autres nulles.
            if (legalMoves.Count == 0)
            {
                if (position.InCheck)
                {
                    Result = position.SideToMove == PieceColor.White ? GameResult.BlackWins : GameResult.WhiteWins;
                    EndReason = GameEndReason.Checkmate;
                }
                else
                {
                    Result = GameResult.Draw;
                    EndReason = GameEndReason.Stalemate;
                }
            }
            else if (position.IsInsufficientMaterial())
            {
                Result = GameResult.Draw;
                EndReason = GameEndReason.InsufficientMaterial;
            }
            else if (position.RepetitionCount() >= 3)
            {
                Result = GameResult.Draw;
                EndReason = GameEndReason.ThreefoldRepetition;
            }
            else if (position.HalfmoveClock >= 100)
            {
                Result = GameResult.Draw;
                EndReason = GameEndReason.FiftyMoveRule;
            }
        }

        /// <summary>Partie au format PGN minimal (balises Result et FEN si besoin).</summary>
        public string ToPgn()
        {
            var sb = new System.Text.StringBuilder();
            string result = Result == GameResult.WhiteWins ? "1-0" : Result == GameResult.BlackWins ? "0-1" : Result == GameResult.Draw ? "1/2-1/2" : "*";
            sb.Append("[Event \"KevChess\"]\n[Result \"").Append(result).Append("\"]\n");
            if (startFen != Position.StartFen) sb.Append("[SetUp \"1\"]\n[FEN \"").Append(startFen).Append("\"]\n");
            sb.Append('\n');

            var start = Position.FromFen(startFen);
            int number = start.FullmoveNumber;
            bool black = start.SideToMove == PieceColor.Black;
            for (int i = 0; i < sanMoves.Count; i++)
            {
                if (!black) sb.Append(number).Append(". ");
                else if (i == 0) sb.Append(number).Append("... ");
                sb.Append(sanMoves[i]).Append(' ');
                if (black) number++;
                black = !black;
            }
            sb.Append(result);
            return sb.ToString();
        }
    }
}
