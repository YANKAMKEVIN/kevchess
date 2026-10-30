using KevChess.Core;

namespace KevChess.Game.Match
{
    /// <summary>
    /// Tout ce que la vue doit savoir pour animer un coup, calculé AVANT qu'il soit joué.
    /// </summary>
    public readonly struct MoveVisual
    {
        public readonly Move Move;
        public readonly int MovingPiece;
        public readonly int CapturedPiece;
        public readonly int CaptureSquare;
        public readonly int RookFrom;
        public readonly int RookTo;
        public readonly int PromotedPiece;

        MoveVisual(Move move, int moving, int captured, int captureSquare, int rookFrom, int rookTo, int promoted)
        {
            Move = move;
            MovingPiece = moving;
            CapturedPiece = captured;
            CaptureSquare = captureSquare;
            RookFrom = rookFrom;
            RookTo = rookTo;
            PromotedPiece = promoted;
        }

        public bool IsCapture => CapturedPiece != Piece.Empty;
        public bool IsCastle => RookFrom != Square.None;
        public bool IsPromotion => PromotedPiece != Piece.Empty;

        public static MoveVisual From(Position before, Move move)
        {
            int moving = before[move.From];
            var color = Piece.ColorOf(moving);

            int captureSquare = move.To;
            if (move.IsEnPassant) captureSquare = color == PieceColor.White ? move.To - 8 : move.To + 8;
            int captured = before[captureSquare];

            int rookFrom = Square.None, rookTo = Square.None;
            if (move.IsCastle)
            {
                bool kingSide = Square.File(move.To) == 6;
                rookFrom = kingSide ? move.To + 1 : move.To - 2;
                rookTo = kingSide ? move.To - 1 : move.To + 1;
            }

            int promoted = move.IsPromotion ? Piece.Make(color, move.Promotion) : Piece.Empty;
            return new MoveVisual(move, moving, captured, captured != Piece.Empty ? captureSquare : Square.None, rookFrom, rookTo, promoted);
        }
    }
}
