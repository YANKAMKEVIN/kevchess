using System;
using System.Collections.Generic;
using System.Diagnostics;
using KevChess.Core;
using NUnit.Framework;

namespace KevChess.Tests
{
    /// <summary>Lecture directe des coups UCI (reprise de partie, problèmes) sans calcul de SAN.</summary>
    public class UciParseTests
    {
        static int Sq(string name) => Square.Parse(name);

        [Test]
        public void ParsesSimpleMovesWithTheirFlags()
        {
            var pos = Position.Start();
            var m = Notation.ParseUci(pos, "g1f3");
            Assert.That(m.From, Is.EqualTo(Sq("g1")));
            Assert.That(m.To, Is.EqualTo(Sq("f3")));
            Assert.That(Notation.ParseUci(pos, " e2e4 ").ToUci(), Is.EqualTo("e2e4"));
        }

        [TestCase("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1g1", "O-O")]
        [TestCase("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1", "e1c1", "O-O-O")]
        [TestCase("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 1", "e8g8", "O-O")]
        [TestCase("r3k2r/8/8/8/8/8/8/R3K2R b KQkq - 0 1", "e8c8", "O-O-O")]
        public void ParsesCastling(string fen, string uci, string san)
        {
            var pos = Position.FromFen(fen);
            var m = Notation.ParseUci(pos, uci);
            Assert.That(m.IsCastle, Is.True);
            Assert.That(Notation.ToSan(pos, m), Is.EqualTo(san));
        }

        [Test]
        public void CastlingIsRefusedWithoutTheRight()
        {
            var pos = Position.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w - - 0 1");
            Assert.That(Notation.ParseUci(pos, "e1g1").IsNull, Is.True);
        }

        [TestCase("e7e8q", PieceType.Queen)]
        [TestCase("e7e8r", PieceType.Rook)]
        [TestCase("e7e8b", PieceType.Bishop)]
        [TestCase("e7e8n", PieceType.Knight)]
        [TestCase("e7d8q", PieceType.Queen)]
        [TestCase("e7d8n", PieceType.Knight)]
        public void ParsesPromotionsAndCapturePromotions(string uci, PieceType piece)
        {
            var pos = Position.FromFen("3r3k/4P3/8/8/8/8/8/4K3 w - - 0 1");
            var m = Notation.ParseUci(pos, uci);
            Assert.That(m.IsNull, Is.False, uci);
            Assert.That(m.Promotion, Is.EqualTo(piece));
            Assert.That(m.IsCapture, Is.EqualTo(uci[2] == 'd'));
            Assert.That(m.ToUci(), Is.EqualTo(uci));
        }

        [Test]
        public void PromotionNeedsItsPieceLetter()
        {
            var pos = Position.FromFen("3r3k/4P3/8/8/8/8/8/4K3 w - - 0 1");
            Assert.That(Notation.ParseUci(pos, "e7e8").IsNull, Is.True);
            Assert.That(Notation.ParseUci(pos, "e7e8k").IsNull, Is.True);
            Assert.That(Notation.ParseUci(Position.Start(), "e2e4q").IsNull, Is.True);
        }

        [Test]
        public void ParsesEnPassant()
        {
            var pos = Position.FromFen("4k3/8/8/3pP3/8/8/8/4K3 w - d6 0 1");
            var m = Notation.ParseUci(pos, "e5d6");
            Assert.That(m.IsEnPassant, Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("e2")]
        [TestCase("e2e9")]
        [TestCase("i2e4")]
        [TestCase("E2E4")]
        [TestCase("e2e4e")]
        [TestCase("e2e5")]
        [TestCase("e7e5")]
        public void InvalidOrIllegalTextGivesNull(string text)
        {
            Assert.That(Notation.ParseUci(Position.Start(), text).IsNull, Is.True);
        }

        [Test]
        public void ParseStillAcceptsSanAndUci()
        {
            var pos = Position.Start();
            Assert.That(Notation.Parse(pos, "Nf3"), Is.EqualTo(Notation.ParseUci(pos, "g1f3")));
            Assert.That(Notation.Parse(pos, "g1f3"), Is.EqualTo(Notation.ParseUci(pos, "g1f3")));
            Assert.That(Notation.Parse(pos, "e4"), Is.EqualTo(Notation.ParseUci(pos, "e2e4")));
        }

        /// <summary>Partie longue jouée au hasard (graine fixe) : la liste UCI d'une reprise de partie.</summary>
        static (string fen, List<string> ucis) RandomGame(int seed, int plies)
        {
            var rng = new Random(seed);
            var pos = Position.Start();
            string fen = pos.ToFen();
            var ucis = new List<string>();
            for (int i = 0; i < plies; i++)
            {
                var legal = MoveGenerator.Legal(pos);
                if (legal.Count == 0) break;
                var m = legal[rng.Next(legal.Count)];
                ucis.Add(m.ToUci());
                pos.MakeMove(m);
            }
            return (fen, ucis);
        }

        /// <summary>Ancien algorithme de Parse pour une UCI : SAN de tous les coups légaux, puis comparaison.</summary>
        static Move LegacyParse(Position pos, string text)
        {
            foreach (var m in MoveGenerator.Legal(pos))
                if (Notation.ToSan(pos, m) == text || m.ToUci() == text) return m;
            return Move.Null;
        }

        [Test]
        public void EveryLegalMoveRoundTripsAndMatchesTheLegacyParser()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var (fen, ucis) = RandomGame(seed, 200);
                var pos = Position.FromFen(fen);
                foreach (var uci in ucis)
                {
                    foreach (var m in MoveGenerator.Legal(pos))
                    {
                        Assert.That(Notation.ParseUci(pos, m.ToUci()), Is.EqualTo(m), m.ToUci() + " dans " + pos.ToFen());
                        Assert.That(Notation.Parse(pos, m.ToUci()), Is.EqualTo(LegacyParse(pos, m.ToUci())));
                    }
                    pos.MakeMove(Notation.ParseUci(pos, uci));
                }
            }
        }

        [Test]
        public void ReplayingAGameIsMuchCheaperThanTheLegacyParser()
        {
            var games = new List<(string, List<string>)>();
            for (int seed = 100; seed < 130; seed++) games.Add(RandomGame(seed, 160));

            long Replay(Func<Position, string, Move> parse)
            {
                var sw = Stopwatch.StartNew();
                foreach (var (fen, ucis) in games)
                {
                    var pos = Position.FromFen(fen);
                    foreach (var uci in ucis)
                    {
                        var m = parse(pos, uci);
                        Assert.That(m.IsNull, Is.False);
                        pos.MakeMove(m);
                    }
                }
                return sw.ElapsedTicks;
            }

            Replay(LegacyParse); Replay(Notation.Parse); // chauffe JIT
            long legacy = Replay(LegacyParse);
            long direct = Replay(Notation.Parse);
            double ratio = (double)legacy / Math.Max(1, direct);
            TestContext.WriteLine($"Rejeu UCI : ancien {legacy * 1000.0 / Stopwatch.Frequency:F1} ms, direct {direct * 1000.0 / Stopwatch.Frequency:F1} ms (x{ratio:F1})");
            Assert.That(ratio, Is.GreaterThan(2.0), "le parsing UCI direct doit être nettement plus rapide");
        }
    }
}
