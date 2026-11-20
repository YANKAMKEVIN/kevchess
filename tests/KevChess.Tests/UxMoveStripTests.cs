using System.Linq;
using KevChess.Core;
using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxMoveStripTests
    {
        static readonly string[] San = { "e4", "e5", "Nf3", "Nc6", "Bb5" };

        [Test]
        public void RecentTokensCarryPlyAndNumbers()
        {
            var t = MoveStrip.Recent(Position.StartFen, San, 5, 3);
            Assert.That(t.Select(x => x.Ply), Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(t.Select(x => x.Text), Is.EqualTo(new[] { "2. Nf3", "Nc6", "3. Bb5" }));

            var black = MoveStrip.Recent(Position.StartFen, San, 4, 2);
            Assert.That(black[0].Text, Is.EqualTo("2. Nf3"));
            var startOnBlack = MoveStrip.Recent(Position.StartFen, San, 5, 4);
            Assert.That(startOnBlack[0].Text, Is.EqualTo("1… e5"));
        }

        [Test]
        public void RecentAppliesNotation()
        {
            var t = MoveStrip.Recent(Position.StartFen, San, 3, 8, s => Notation.Localize(s, true));
            Assert.That(t.Last().Text, Is.EqualTo("2. Cf3"));
            Assert.That(MoveStrip.Recent(Position.StartFen, San, 0, 8), Is.Empty);
        }

        [Test]
        public void FullMovesPairWhiteAndBlack()
        {
            var rows = MoveStrip.FullMoves(Position.StartFen, San);
            Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(rows[1].number, Is.EqualTo(2));
            Assert.That(rows[1].white.Text, Is.EqualTo("Nf3"));
            Assert.That(rows[1].black.Ply, Is.EqualTo(3));
            Assert.That(rows[2].black.Ply, Is.EqualTo(-1));

            // Partie commencée aux Noirs : la première ligne n'a pas de coup blanc.
            var fromBlack = MoveStrip.FullMoves("4k3/8/8/8/8/8/8/4K3 b - - 0 7", new[] { "Kd7", "Kd2", "Kc7" });
            Assert.That(fromBlack[0].number, Is.EqualTo(7));
            Assert.That(fromBlack[0].white.Ply, Is.EqualTo(-1));
            Assert.That(fromBlack[1].white.Text, Is.EqualTo("Kd2"));
            Assert.That(fromBlack[1].black.Text, Is.EqualTo("Kc7"));
        }
    }
}
