using System.Linq;
using KevChess.Core;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class RulesTests
    {
        static int Sq(string name) => Square.Parse(name);

        static void Play(ChessGame game, params string[] sans)
        {
            foreach (var san in sans)
                Assert.That(game.TryMakeSan(san), Is.True, "coup refusé : " + san + " dans " + game.Position.ToFen());
        }

        // ---------- FEN ----------

        [Test]
        public void StartPositionRoundTrips()
        {
            Assert.That(Position.Start().ToFen(), Is.EqualTo(Position.StartFen));
        }

        [Test]
        public void InvalidFenThrows()
        {
            Assert.Throws<System.FormatException>(() => Position.FromFen("8/8/8/8/8/8/8/8 w - - 0 1"));
            Assert.Throws<System.FormatException>(() => Position.FromFen("pas un fen"));
        }

        // ---------- Roque ----------

        [Test]
        public void CastlingBothSides()
        {
            var game = new ChessGame("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Assert.That(game.TryMakeMove(Sq("e1"), Sq("g1")), Is.True);
            Assert.That(game.Position[Sq("f1")], Is.EqualTo(Piece.Make(PieceColor.White, PieceType.Rook)));
            Assert.That(game.Position[Sq("h1")], Is.EqualTo(Piece.Empty));
            Assert.That(game.TryMakeMove(Sq("e8"), Sq("c8")), Is.True);
            Assert.That(game.Position[Sq("d8")], Is.EqualTo(Piece.Make(PieceColor.Black, PieceType.Rook)));
            Assert.That(game.SanMoves, Is.EqualTo(new[] { "O-O", "O-O-O" }));
            Assert.That(game.Position.Castling, Is.EqualTo(CastlingRights.None));
        }

        [Test]
        public void CannotCastleOutOfThroughOrIntoCheck()
        {
            // Hors d'échec : tour noire en e8 donne échec.
            var inCheck = new ChessGame("4r1k1/8/8/8/8/8/8/R3K2R w KQ - 0 1");
            Assert.That(inCheck.LegalMoves.Any(m => m.IsCastle), Is.False);

            // À travers : f1 contrôlée.
            var through = new ChessGame("5rk1/8/8/8/8/8/8/R3K2R w KQ - 0 1");
            Assert.That(through.LegalMoves.Any(m => m.IsCastle && Square.File(m.To) == 6), Is.False);
            Assert.That(through.LegalMoves.Any(m => m.IsCastle && Square.File(m.To) == 2), Is.True);

            // Dans l'échec : g1 contrôlée.
            var into = new ChessGame("6rk/8/8/8/8/8/8/R3K2R w KQ - 0 1");
            Assert.That(into.LegalMoves.Any(m => m.IsCastle && Square.File(m.To) == 6), Is.False);

            // b1 attaquée n'empêche pas le grand roque (seul le roi doit traverser des cases sûres).
            var b1Attacked = new ChessGame("1r4k1/8/8/8/8/8/8/R3K2R w KQ - 0 1");
            Assert.That(b1Attacked.LegalMoves.Any(m => m.IsCastle && Square.File(m.To) == 2), Is.True);
        }

        [Test]
        public void CastlingRightsLostWhenKingOrRookMovesOrRookCaptured()
        {
            var game = new ChessGame("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Play(game, "Rxa8+");
            Assert.That(game.Position.Castling, Is.EqualTo(CastlingRights.WhiteKingSide | CastlingRights.BlackKingSide));
            Play(game, "Ke7", "Kf1");
            Assert.That(game.Position.Castling, Is.EqualTo(CastlingRights.None));
        }

        [Test]
        public void CannotCastleWithPieceInBetween()
        {
            var game = new ChessGame("r3k2r/8/8/8/8/8/8/RN2K1NR w KQkq - 0 1");
            Assert.That(game.LegalMoves.Any(m => m.IsCastle), Is.False);
        }

        // ---------- En passant ----------

        [Test]
        public void EnPassantCapture()
        {
            var game = new ChessGame();
            Play(game, "e4", "a6", "e5", "d5");
            Assert.That(game.Position.EnPassantSquare, Is.EqualTo(Sq("d6")));
            Play(game, "exd6");
            Assert.That(game.Position[Sq("d5")], Is.EqualTo(Piece.Empty));
            Assert.That(game.Position[Sq("d6")], Is.EqualTo(Piece.Make(PieceColor.White, PieceType.Pawn)));
            Assert.That(game.SanMoves.Last(), Is.EqualTo("exd6"));
        }

        [Test]
        public void EnPassantOnlyImmediately()
        {
            var game = new ChessGame();
            Play(game, "e4", "a6", "e5", "d5", "a3", "a5");
            Assert.That(game.TryMakeMove(Sq("e5"), Sq("d6")), Is.False);
        }

        [Test]
        public void EnPassantForbiddenWhenItExposesKing()
        {
            // Pion blanc b5, pion noir c5 vient d'avancer de deux cases ; roi blanc a5 et tour noire h5 sur la même rangée.
            var game = new ChessGame("8/8/8/KPp4r/8/8/8/4k3 w - c6 0 2");
            Assert.That(game.LegalMoves.Any(m => m.IsEnPassant), Is.False);
        }

        [Test]
        public void UndoRestoresEnPassantCapture()
        {
            var game = new ChessGame();
            Play(game, "e4", "a6", "e5", "d5");
            string fen = game.Position.ToFen();
            Play(game, "exd6");
            game.Undo();
            Assert.That(game.Position.ToFen(), Is.EqualTo(fen));
        }

        // ---------- Promotion ----------

        [Test]
        public void PromotionToEachPiece()
        {
            foreach (var type in new[] { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight })
            {
                var game = new ChessGame("8/P6k/8/8/8/8/8/K7 w - - 0 1");
                Assert.That(game.IsPromotionMove(Sq("a7"), Sq("a8")), Is.True);
                Assert.That(game.TryMakeMove(Sq("a7"), Sq("a8"), type), Is.True);
                Assert.That(game.Position[Sq("a8")], Is.EqualTo(Piece.Make(PieceColor.White, type)));
            }
        }

        [Test]
        public void PromotionDefaultsToQueenAndSanIsCorrect()
        {
            var game = new ChessGame("1r5k/P7/8/8/8/8/8/K7 w - - 0 1");
            Assert.That(game.TryMakeMove(Sq("a7"), Sq("b8")), Is.True);
            Assert.That(game.SanMoves.Last(), Is.EqualTo("axb8=Q+"));
            game.Undo();
            Assert.That(game.Position[Sq("a7")], Is.EqualTo(Piece.Make(PieceColor.White, PieceType.Pawn)));
            Assert.That(game.Position[Sq("b8")], Is.EqualTo(Piece.Make(PieceColor.Black, PieceType.Rook)));
        }

        // ---------- Échec, mat, pat ----------

        [Test]
        public void IllegalMoveLeavingKingInCheckIsRejected()
        {
            // Le fou e2 est cloué par la tour e8.
            var game = new ChessGame("4r1k1/8/8/8/8/8/4B3/4K3 w - - 0 1");
            Assert.That(game.TryMakeMove(Sq("e2"), Sq("d3")), Is.False);
        }

        [Test]
        public void FoolsMate()
        {
            var game = new ChessGame();
            Play(game, "f3", "e5", "g4", "Qh4#");
            Assert.That(game.Result, Is.EqualTo(GameResult.BlackWins));
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.Checkmate));
            Assert.That(game.SanMoves.Last(), Is.EqualTo("Qh4#"));
            Assert.That(game.TryMakeSan("a3"), Is.False);
        }

        [Test]
        public void ScholarsMate()
        {
            var game = new ChessGame();
            Play(game, "e4", "e5", "Bc4", "Nc6", "Qh5", "Nf6", "Qxf7#");
            Assert.That(game.Result, Is.EqualTo(GameResult.WhiteWins));
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.Checkmate));
        }

        [Test]
        public void Stalemate()
        {
            var game = new ChessGame("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");
            Assert.That(game.Result, Is.EqualTo(GameResult.Draw));
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.Stalemate));
        }

        [Test]
        public void CheckmateTakesPriorityOverFiftyMoveRule()
        {
            // Le 100e demi-coup sans prise ni pion donne mat : le mat compte.
            var game = new ChessGame("7k/8/6K1/8/8/8/8/R7 w - - 99 80");
            Play(game, "Ra8#");
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.Checkmate));
        }

        // ---------- Nulles ----------

        [Test]
        public void FiftyMoveRule()
        {
            var game = new ChessGame("7k/8/8/8/8/8/8/R6K w - - 99 80");
            Play(game, "Ra2");
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.FiftyMoveRule));
            Assert.That(game.Result, Is.EqualTo(GameResult.Draw));
        }

        [Test]
        public void ThreefoldRepetition()
        {
            var game = new ChessGame();
            Play(game, "Nf3", "Nf6", "Ng1", "Ng8", "Nf3", "Nf6", "Ng1");
            Assert.That(game.IsOver, Is.False);
            Play(game, "Ng8");
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.ThreefoldRepetition));
        }

        [Test]
        public void RepetitionDistinguishesCastlingRights()
        {
            // Le roi fait un aller-retour : la position est identique visuellement mais les droits de roque diffèrent.
            var game = new ChessGame("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
            Play(game, "Kd1", "Kd8", "Ke1", "Ke8", "Kd1", "Kd8", "Ke1", "Ke8");
            // Sans distinction des droits, la position de départ serait déjà vue trois fois ici.
            Assert.That(game.IsOver, Is.False);
            Play(game, "Kd1", "Kd8");
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.ThreefoldRepetition));
        }

        [TestCase("8/8/8/4k3/8/8/8/4K3 w - - 0 1", true)]          // K vs K
        [TestCase("8/8/8/4k3/8/8/8/4KN2 w - - 0 1", true)]         // K+C vs K
        [TestCase("8/8/8/4k3/8/8/8/4KB2 w - - 0 1", true)]         // K+F vs K
        [TestCase("8/8/8/3bk3/8/8/8/4KB2 w - - 0 1", true)]        // fous de même couleur (d5 et f1 : cases claires)
        [TestCase("8/8/8/2b1k3/8/8/8/4KB2 w - - 0 1", false)]      // fous de couleurs opposées (c5 sombre)
        [TestCase("8/8/8/4k3/8/8/8/3NKN2 w - - 0 1", false)]       // deux cavaliers : mat possible
        [TestCase("8/8/8/4k3/8/8/8/4KBN1 w - - 0 1", false)]       // fou + cavalier
        [TestCase("8/8/8/4k3/8/8/4P3/4K3 w - - 0 1", false)]       // pion
        public void InsufficientMaterial(string fen, bool expected)
        {
            Assert.That(Position.FromFen(fen).IsInsufficientMaterial(), Is.EqualTo(expected));
        }

        [Test]
        public void CaptureLeadingToBareKingsIsDraw()
        {
            var game = new ChessGame("8/8/8/4k3/8/8/3q4/3K4 w - - 0 1");
            Play(game, "Kxd2");
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.InsufficientMaterial));
        }

        // ---------- Abandon, nulle, annulation ----------

        [Test]
        public void ResignAndDrawAgreement()
        {
            var g1 = new ChessGame();
            g1.Resign(PieceColor.White);
            Assert.That(g1.Result, Is.EqualTo(GameResult.BlackWins));
            var g2 = new ChessGame();
            g2.AgreeDraw();
            Assert.That(g2.Result, Is.EqualTo(GameResult.Draw));
        }

        [Test]
        public void UndoAfterMateReopensGame()
        {
            var game = new ChessGame();
            Play(game, "f3", "e5", "g4", "Qh4#");
            Assert.That(game.Undo(), Is.True);
            Assert.That(game.IsOver, Is.False);
            Assert.That(game.SanMoves.Count, Is.EqualTo(3));
        }

        // ---------- Notation ----------

        [Test]
        public void SanDisambiguation()
        {
            // Deux cavaliers peuvent aller en d2 : Nbd2 / Nfd2.
            var g = new ChessGame("4k3/8/8/8/8/8/8/1N2KN2 w - - 0 1");
            Assert.That(Notation.ToSan(g.Position, Notation.Parse(g.Position, "b1d2")), Is.EqualTo("Nbd2"));
            // Deux tours sur la même colonne : R1a3 / R5a3.
            var r = new ChessGame("4k3/8/8/R7/8/8/8/R3K3 w - - 0 1");
            Assert.That(Notation.ToSan(r.Position, Notation.Parse(r.Position, "a1a3")), Is.EqualTo("R1a3"));
            // Trois dames : désambiguïsation complète.
            var q = new ChessGame("7k/8/8/8/Q1Q5/8/Q7/4K3 w - - 0 1");
            Assert.That(Notation.ToSan(q.Position, Notation.Parse(q.Position, "a4b3")), Is.EqualTo("Qa4b3"));
        }

        [Test]
        public void PgnExport()
        {
            var game = new ChessGame();
            Play(game, "e4", "e5", "Nf3");
            Assert.That(game.ToPgn(), Does.EndWith("1. e4 e5 2. Nf3 *"));
        }
    }
}
