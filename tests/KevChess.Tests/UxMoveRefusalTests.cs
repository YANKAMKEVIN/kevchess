using KevChess.Core;
using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxMoveRefusalTests
    {
        static MoveRefusal Explain(string fen, string from, string to) =>
            MoveRefusal.Explain(Position.FromFen(fen), Square.Parse(from), Square.Parse(to));

        [Test]
        public void MoveThatIgnoresCheckPointsAtTheChecker()
        {
            // Dame noire en h4 : échec au roi blanc e1 par la diagonale ; jouer a2-a3 ne pare pas.
            var r = Explain("rnb1kbnr/pppp1ppp/8/4p3/7q/5P2/PPPPP1PP/RNBQKBNR w KQkq - 1 3", "a2", "a3");
            Assert.That(r.Reason, Is.EqualTo(RefusalReason.InCheck));
            Assert.That(r.KingSquare, Is.EqualTo(Square.Parse("e1")));
            Assert.That(r.Attackers, Is.EqualTo(new[] { Square.Parse("h4") }));
            Assert.That(r.Message, Does.Contain("échec"));
        }

        [Test]
        public void ImpossibleMoveWhileInCheckStillRemindsTheCheck()
        {
            var r = Explain("rnb1kbnr/pppp1ppp/8/4p3/7q/5P2/PPPPP1PP/RNBQKBNR w KQkq - 1 3", "a2", "a5");
            Assert.That(r.Reason, Is.EqualTo(RefusalReason.InCheck));
        }

        [Test]
        public void PinnedPieceNamesThePinner()
        {
            // Cavalier blanc e2 cloué par la tour e8 sur la colonne e.
            var r = Explain("4r2k/8/8/8/8/8/4N3/4K3 w - - 0 1", "e2", "c3");
            Assert.That(r.Reason, Is.EqualTo(RefusalReason.Pinned));
            Assert.That(r.KingSquare, Is.EqualTo(Square.Parse("e1")));
            Assert.That(r.Attackers, Is.EqualTo(new[] { Square.Parse("e8") }));
        }

        [Test]
        public void KingCannotStepIntoAttack()
        {
            var r = Explain("3r3k/8/8/8/8/8/8/4K3 w - - 0 1", "e1", "d1");
            Assert.That(r.Reason, Is.EqualTo(RefusalReason.KingAttacked));
            Assert.That(r.KingSquare, Is.EqualTo(Square.Parse("d1")));
            Assert.That(r.Attackers, Is.EqualTo(new[] { Square.Parse("d8") }));
        }

        [Test]
        public void NothingToSayForGeometryOrLegalMoves()
        {
            Assert.That(Explain(Position.StartFen, "b1", "b3").Reason, Is.EqualTo(RefusalReason.None));
            Assert.That(Explain(Position.StartFen, "e2", "e4").Reason, Is.EqualTo(RefusalReason.None));
            Assert.That(Explain(Position.StartFen, "e7", "e5").Reason, Is.EqualTo(RefusalReason.None), "pièce adverse");
            Assert.That(Explain(Position.StartFen, "b1", "b3").Message, Is.Null);
        }
    }
}
