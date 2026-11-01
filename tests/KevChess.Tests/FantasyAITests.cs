using System.Diagnostics;
using System.Linq;
using KevChess.AI;
using KevChess.AI.Fantasy;
using KevChess.Core;
using KevChess.Core.Fantasy;
using NUnit.Framework;

namespace KevChess.Tests
{
    /// <summary>L'IA sait jouer le mode Légendes.</summary>
    public class FantasyAITests
    {
        static int Sq(string name) => Square.Parse(name);

        static FantasyConfig NoEvents()
        {
            var c = FantasyConfig.Default;
            c.EventsEnabled = false;
            return c;
        }

        static FantasySearchResult Analyze(FantasyPosition p, int depth = 4, int ms = 4000) =>
            new FantasyAI(8, 1).Analyze(p, new SearchLimits { MaxDepth = depth, MaxTimeMs = ms });

        [Test]
        public void ReturnsLegalActionFromStart()
        {
            var game = new FantasyGame(5);
            var a = new FantasyAI(8, 1).FindAction(game.Position, Difficulty.Moyen, default, 300);
            Assert.That(game.IsLegal(a), Is.True, a.ToString());
        }

        static string Mirror(string fen)
        {
            var parts = fen.Split(' ');
            var rows = parts[0].Split('/').Reverse().Select(r => new string(r.Select(c => char.IsUpper(c) ? char.ToLower(c) : char.ToUpper(c)).ToArray()));
            string castle = parts[2] == "-" ? "-" : new string(parts[2].Select(c => char.IsUpper(c) ? char.ToLower(c) : char.ToUpper(c)).OrderBy(c => char.IsUpper(c) ? 0 : 1).ToArray());
            return string.Join("/", rows) + " " + (parts[1] == "w" ? "b" : "w") + " " + castle + " - 0 1";
        }

        [TestCase("r1bq1rk1/pp2bppp/2n1pn2/3p4/2PP4/2N1PN2/PP3PPP/R2QKB1R w KQ - 0 8")]
        [TestCase("4k3/pp6/8/8/3D4/8/PP6/4K3 w - - 0 1")]
        public void EvaluationIsSymmetric(string fen)
        {
            var a = FantasyPosition.FromFen(fen);
            var b = FantasyPosition.FromFen(Mirror(fen));
            a.SetEnergy(PieceColor.White, 7); b.SetEnergy(PieceColor.Black, 7);
            Assert.That(FantasyEvaluator.EvaluateOnce(a), Is.EqualTo(FantasyEvaluator.EvaluateOnce(b)));
        }

        [Test]
        public void FindsMateInOne()
        {
            var p = FantasyPosition.FromFen("6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1", NoEvents());
            var r = Analyze(p, 3);
            Assert.That(r.BestAction.To, Is.EqualTo(Sq("a8")));
            Assert.That(r.IsMate, Is.True);
        }

        [Test]
        public void ShieldsAttackedQueenOrSavesIt()
        {
            // La dame blanche d4 est attaquée par le pion e5 ; elle doit être sauvée (fuite ou Bouclier).
            var p = FantasyPosition.FromFen("4k3/8/8/4p3/3Q4/8/8/4K3 w - - 0 1", NoEvents());
            p.SetEnergy(PieceColor.White, 2);
            var r = Analyze(p, 4);
            var a = r.BestAction;
            p.MakeAction(a);
            int q = Enumerable.Range(0, 64).FirstOrDefault(sq => p[sq] == Piece.Make(PieceColor.White, PieceType.Queen), -1);
            Assert.That(q, Is.GreaterThanOrEqualTo(0));
            Assert.That(p.IsShielded(q) || !p.IsSquareAttacked(q, PieceColor.Black), Is.True, a.ToString());
        }

        [Test]
        public void SummonsDragonWhenGaugeIsFull()
        {
            var p = FantasyPosition.FromFen("r3k3/ppp5/8/8/8/8/PPP5/R3K3 w - - 0 1", NoEvents());
            p.SetEnergy(PieceColor.White, 10);
            var r = Analyze(p, 3);
            Assert.That(r.BestAction.Power, Is.EqualTo(PowerKind.Summon), r.ToString());
        }

        [Test]
        public void UsesFreezeToWinADefendedPiece()
        {
            // Le cavalier noir d5 n'est défendu que par le pion e6 ; gelé, le pion ne peut pas reprendre.
            var p = FantasyPosition.FromFen("4k3/8/4p3/3n4/8/8/8/3QK3 w - - 0 1", NoEvents());
            p.SetEnergy(PieceColor.White, 3);
            var r = Analyze(p, 4);
            Assert.That(r.BestAction.Power, Is.EqualTo(PowerKind.Freeze), r.ToString());
            Assert.That(r.BestAction.PowerSquare, Is.EqualTo(Sq("e6")));
            Assert.That(r.BestAction.To, Is.EqualTo(Sq("d5")));
        }

        [Test]
        public void LeavesCollapsingFissure()
        {
            var p = FantasyPosition.FromFen("4k3/pp6/8/8/3N4/8/PP6/4K3 w - - 0 1", NoEvents());
            p.AddFissure(Sq("d4"), 1);
            var r = Analyze(p, 3);
            Assert.That(r.BestAction.From, Is.EqualTo(Sq("d4")), r.ToString());
        }

        [Test]
        public void BreathesOnFreePiece()
        {
            var p = FantasyPosition.FromFen("4k3/8/8/8/8/2r5/8/D3K3 w - - 0 1", NoEvents());
            var r = Analyze(p, 3);
            Assert.That(r.BestAction.To, Is.EqualTo(Sq("c3")), r.ToString());
        }

        [Test]
        public void RespectsTimeLimit()
        {
            var game = new FantasyGame(3);
            game.Position.SetEnergy(PieceColor.White, 10);
            var sw = Stopwatch.StartNew();
            new FantasyAI(8, 1).FindAction(game.Position, Difficulty.Maitre, default, 500);
            Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1500));
        }

        [Test]
        public void PlaysAWholeGameAgainstItself()
        {
            var game = new FantasyGame(77);
            var white = new FantasyAI(8, 1);
            var black = new FantasyAI(8, 2);
            int powers = 0;
            for (int i = 0; i < 160 && !game.IsOver; i++)
            {
                var ai = game.SideToMove == PieceColor.White ? white : black;
                var a = ai.FindAction(game.Position, Difficulty.Fort, default, 60);
                Assert.That(game.TryMakeAction(a), Is.True, a + " dans " + game.Position.ToFen());
                if (a.HasPower || a.Kind != ActionKind.Normal) powers++;
            }
            TestContext.WriteLine(string.Join(" ", game.Notations));
            TestContext.WriteLine("demi-coups " + game.Actions.Count + ", pouvoirs/capacités " + powers + ", fin " + game.EndReason);
            Assert.That(powers, Is.GreaterThan(0), "l'IA utilise les pouvoirs");
        }

        [Test, Explicit("Mesure : profondeur atteinte et vitesse, mode classique contre mode Légendes")]
        public void Benchmark()
        {
            var classic = new ChessAI(16, 1).Analyze(Position.Start(), new SearchLimits { MaxTimeMs = 1000 });
            var game = new FantasyGame(1);
            var fantasy = new FantasyAI(16, 1).Analyze(game.Position, new SearchLimits { MaxTimeMs = 1000 });
            var mid = FantasyPosition.FromFen("r1bq1rk1/pp2bppp/2n1pn2/3p4/2PP4/2N1PN2/PP3PPP/R2QKB1R w KQ - 0 8");
            mid.SetEnergy(PieceColor.White, 6);
            mid.SetEnergy(PieceColor.Black, 6);
            var fantasyMid = new FantasyAI(16, 1).Analyze(mid, new SearchLimits { MaxTimeMs = 1000 });
            TestContext.WriteLine("classique : " + classic);
            TestContext.WriteLine("légendes départ : " + fantasy);
            TestContext.WriteLine("légendes milieu (6 éclats) : " + fantasyMid);
        }
    }
}
