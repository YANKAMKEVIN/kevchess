using KevChess.Core;
using NUnit.Framework;

namespace KevChess.Tests
{
    /// <summary>
    /// Comptes de nœuds de référence (chessprogramming.org, « Perft Results »).
    /// Toute erreur de règle (roque, en passant, promotion, clouage) change ces nombres.
    /// </summary>
    public class PerftTests
    {
        const string Kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
        const string Position3 = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1";
        const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
        const string Position4Mirrored = "r2q1rk1/pP1p2pp/Q4n2/bbp1p3/Np6/1B3NBn/pPPP1PPP/R3K2R b KQ - 0 1";
        const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";
        const string Position6 = "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10";

        [TestCase(Position.StartFen, 1, 20L)]
        [TestCase(Position.StartFen, 2, 400L)]
        [TestCase(Position.StartFen, 3, 8902L)]
        [TestCase(Position.StartFen, 4, 197281L)]
        [TestCase(Position.StartFen, 5, 4865609L)]
        [TestCase(Kiwipete, 1, 48L)]
        [TestCase(Kiwipete, 2, 2039L)]
        [TestCase(Kiwipete, 3, 97862L)]
        [TestCase(Kiwipete, 4, 4085603L)]
        [TestCase(Position3, 1, 14L)]
        [TestCase(Position3, 2, 191L)]
        [TestCase(Position3, 3, 2812L)]
        [TestCase(Position3, 4, 43238L)]
        [TestCase(Position3, 5, 674624L)]
        [TestCase(Position4, 1, 6L)]
        [TestCase(Position4, 2, 264L)]
        [TestCase(Position4, 3, 9467L)]
        [TestCase(Position4, 4, 422333L)]
        [TestCase(Position4Mirrored, 4, 422333L)]
        [TestCase(Position5, 1, 44L)]
        [TestCase(Position5, 2, 1486L)]
        [TestCase(Position5, 3, 62379L)]
        [TestCase(Position5, 4, 2103487L)]
        [TestCase(Position6, 1, 46L)]
        [TestCase(Position6, 2, 2079L)]
        [TestCase(Position6, 3, 89890L)]
        [TestCase(Position6, 4, 3894594L)]
        public void Perft(string fen, int depth, long expected)
        {
            var pos = Position.FromFen(fen);
            ulong hashBefore = pos.Hash;
            string fenBefore = pos.ToFen();

            Assert.That(MoveGenerator.Perft(pos, depth), Is.EqualTo(expected));

            // Jouer puis déjouer doit tout restaurer exactement.
            Assert.That(pos.ToFen(), Is.EqualTo(fenBefore));
            Assert.That(pos.Hash, Is.EqualTo(hashBefore));
        }

        [TestCase(Position.StartFen)]
        [TestCase(Kiwipete)]
        [TestCase(Position4)]
        [TestCase(Position5)]
        public void IncrementalHashMatchesFullRecompute(string fen)
        {
            var pos = Position.FromFen(fen);
            CheckHash(pos, 3);
        }

        static void CheckHash(Position pos, int depth)
        {
            Assert.That(pos.Hash, Is.EqualTo(pos.ComputeHash()), pos.ToFen());
            if (depth == 0) return;
            foreach (var m in MoveGenerator.Legal(pos))
            {
                pos.MakeMove(m);
                CheckHash(pos, depth - 1);
                pos.UnmakeMove();
            }
        }
    }
}
