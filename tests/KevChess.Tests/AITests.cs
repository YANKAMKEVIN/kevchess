using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KevChess.AI;
using KevChess.Core;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class AITests
    {
        const string Kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";

        // ---------- Utilitaires ----------

        /// <summary>Miroir couleurs inversées : rangées retournées, casse inversée, trait, roque et en passant échangés.</summary>
        public static string MirrorFen(string fen)
        {
            var parts = fen.Split(' ');
            var rows = parts[0].Split('/');
            Array.Reverse(rows);
            string board = string.Join("/", rows.Select(SwapCase));
            string side = parts[1] == "w" ? "b" : "w";
            string castling = parts[2] == "-" ? "-" : new string(SwapCase(parts[2]).OrderBy(c => "KQkq".IndexOf(c)).ToArray());
            string ep = parts[3] == "-" ? "-" : parts[3][0] + ((char)('1' + ('8' - parts[3][1]))).ToString();
            var rest = parts.Skip(4);
            return string.Join(" ", new[] { board, side, castling, ep }.Concat(rest));
        }

        static string SwapCase(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) sb.Append(char.IsUpper(c) ? char.ToLowerInvariant(c) : char.ToUpperInvariant(c));
            return sb.ToString();
        }

        static bool IsMate(Position pos) => pos.InCheck && !MoveGenerator.HasLegalMove(pos);

        static bool MatesInOne(Position pos, Move m)
        {
            pos.MakeMove(m);
            bool mate = IsMate(pos);
            pos.UnmakeMove();
            return mate;
        }

        static bool HasMateInOne(Position pos) => MoveGenerator.Legal(pos).Any(m => MatesInOne(pos, m));

        /// <summary>Vérifie par force brute (règles du Core seules) que m force le mat en 2.</summary>
        static bool ForcesMateInTwo(Position pos, Move m)
        {
            pos.MakeMove(m);
            try
            {
                if (IsMate(pos)) return true;
                var replies = MoveGenerator.Legal(pos);
                if (replies.Count == 0) return false; // pat
                foreach (var r in replies)
                {
                    pos.MakeMove(r);
                    bool ok = HasMateInOne(pos);
                    pos.UnmakeMove();
                    if (!ok) return false;
                }
                return true;
            }
            finally { pos.UnmakeMove(); }
        }

        static SearchResult Analyze(string fen, int depth, int timeMs = 0)
        {
            var searcher = new Searcher(4);
            return searcher.Search(Position.FromFen(fen), new SearchLimits { MaxDepth = depth, MaxTimeMs = timeMs });
        }

        // ---------- Mats ----------

        [TestCase("6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1", TestName = "Mat en 1 : couloir")]
        [TestCase("r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5Q2/PPPP1PPP/RNB1K1NR w KQkq - 0 1", TestName = "Mat en 1 : berger")]
        [TestCase("r5k1/8/8/8/8/8/5PPP/6K1 b - - 0 1", TestName = "Mat en 1 : noirs, couloir")]
        [TestCase("6rk/6pp/8/6N1/8/8/8/6K1 w - - 0 1", TestName = "Mat en 1 : étouffé")]
        [TestCase("k7/8/1K6/8/8/8/8/7Q w - - 0 1", TestName = "Mat en 1 : dame et roi")]
        public void FindsMateInOne(string fen)
        {
            var pos = Position.FromFen(fen);
            var r = Analyze(fen, 4);
            Assert.That(MatesInOne(pos, r.BestMove), Is.True, "coup " + r.BestMove + " ne mate pas");
            Assert.That(r.MateInMoves, Is.EqualTo(1));
        }

        [TestCase("7k/8/8/8/8/8/R7/1R4K1 w - - 0 1", TestName = "Mat en 2 : échelle")]
        [TestCase("kbK5/pp6/1P6/8/8/8/8/R7 w - - 0 1", TestName = "Mat en 2 : sacrifice Ra6")]
        [TestCase("r1b2k1r/ppp1bppp/8/1B1Q4/5q2/2P5/PPP2PPP/R3R1K1 w - - 1 0", TestName = "Mat en 2 : dame d8")]
        [TestCase("1r4k1/r7/8/8/8/8/8/7K b - - 0 1", TestName = "Mat en 2 : noirs, échelle")]
        public void FindsMateInTwo(string fen)
        {
            var pos = Position.FromFen(fen);
            var r = Analyze(fen, 6);
            Assert.That(r.MateInMoves, Is.InRange(1, 2), "score " + r.Score);
            Assert.That(ForcesMateInTwo(pos, r.BestMove), Is.True, "coup " + r.BestMove + " ne force pas le mat en 2");
        }

        [Test]
        public void PrefersShortestMate()
        {
            // Mat en 1 disponible (Ra8#) alors que d'autres coups matent plus tard.
            var r = Analyze("6k1/5ppp/8/8/8/8/1R6/R5K1 w - - 0 1", 6);
            Assert.That(r.MateInMoves, Is.EqualTo(1));
        }

        [Test]
        public void SeesBeingMated()
        {
            // Les blancs au trait ne peuvent éviter Ra1# : score négatif de mat.
            var r = Analyze("r5k1/8/8/8/8/8/5PPP/7K w - - 0 1", 5);
            Assert.That(r.Score, Is.LessThan(0));
        }

        // ---------- Tactique simple ----------

        [Test]
        public void DoesNotLeaveQueenEnPrise()
        {
            // Dame blanche d4 attaquée par le cavalier c6.
            string fen = "r1bqkbnr/pppp1ppp/2n5/8/3Q4/8/PPP2PPP/RNB1KBNR w KQkq - 0 1";
            var pos = Position.FromFen(fen);
            var m = new ChessAI(4, seed: 1).FindMove(pos, SearchLimits.Depth(5));
            pos.MakeMove(m);
            int queenSq = -1;
            for (int sq = 0; sq < 64; sq++) if (pos[sq] == Piece.Make(PieceColor.White, PieceType.Queen)) queenSq = sq;
            Assert.That(queenSq, Is.Not.EqualTo(-1), "dame perdue après " + m);
            Assert.That(pos.IsSquareAttacked(queenSq, PieceColor.Black), Is.False, "dame toujours en prise après " + m);
        }

        [TestCase("4k3/8/8/3n4/8/8/8/3RK3 w - - 0 1", "d1d5", TestName = "Prend un cavalier gratuit")]
        [TestCase("4k3/8/8/8/3q4/8/4N3/4K3 w - - 0 1", "e2d4", TestName = "Prend une dame gratuite")]
        [TestCase("4k3/8/2n5/8/8/8/1q6/RK6 w - - 0 1", "b1b2", TestName = "Le roi prend une dame non défendue")]
        public void CapturesFreePiece(string fen, string expected)
        {
            var r = Analyze(fen, 5);
            Assert.That(r.BestMove.ToUci(), Is.EqualTo(expected));
        }

        [TestCase("k7/8/1Q6/8/8/8/8/6K1 w - - 0 1")]
        [TestCase("7k/8/5QK1/8/8/8/8/8 w - - 0 1")]
        [TestCase("7k/5K2/8/8/8/8/8/6Q1 w - - 0 1")]
        public void AvoidsStalemateWhenWinning(string fen)
        {
            foreach (var level in new[] { Difficulty.Debutant, Difficulty.Maitre })
            {
                var pos = Position.FromFen(fen);
                var ai = new ChessAI(4, seed: 3);
                var m = ai.FindMove(pos, new EngineOptions { Level = level, MoveTimeMs = 200 });
                pos.MakeMove(m);
                bool stalemate = !pos.InCheck && !MoveGenerator.HasLegalMove(pos);
                Assert.That(stalemate, Is.False, level + " : " + m + " donne pat");
            }
        }

        // ---------- Robustesse ----------

        [Test]
        public void AlwaysReturnsLegalMoveOnSelfPlayPositions()
        {
            var ai = new ChessAI(4, seed: 42);
            var positions = new List<string>();
            for (int g = 0; g < 4; g++)
            {
                var game = new Game();
                for (int ply = 0; ply < 80 && !game.IsOver; ply++)
                {
                    positions.Add(game.Position.ToFen());
                    var m = ai.FindMove(game.Position, new EngineOptions { Level = (Difficulty)(g % 2), MoveTimeMs = 30, AllowBook = g > 1 });
                    Assert.That(game.TryMakeMove(m), Is.True, "coup illégal " + m + " dans " + game.Position.ToFen());
                }
            }

            var rnd = new Random(7);
            var searcher = new Searcher(4);
            foreach (var fen in positions.OrderBy(_ => rnd.Next()).Take(60))
            {
                var pos = Position.FromFen(fen);
                var legal = MoveGenerator.Legal(pos);
                var r = searcher.Search(pos, new SearchLimits { MaxDepth = 3 + rnd.Next(3), MaxTimeMs = 50 });
                if (legal.Count == 0) Assert.That(r.BestMove.IsNull, Is.True);
                else Assert.That(legal, Does.Contain(r.BestMove), fen);
                Assert.That(pos.ToFen(), Is.EqualTo(fen), "la position d'origine a été modifiée");
            }
        }

        [Test]
        public void SearchDoesNotModifyGamePosition()
        {
            var game = new Game(Kiwipete);
            ulong hash = game.Position.Hash;
            new ChessAI(4).FindMove(game.Position, SearchLimits.Depth(4));
            Assert.That(game.Position.Hash, Is.EqualTo(hash));
            Assert.That(game.Position.ToFen(), Is.EqualTo(Kiwipete));
        }

        [Test]
        public void NoMoveWhenGameIsOver()
        {
            var r = Analyze("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1", 4); // pat
            Assert.That(r.BestMove.IsNull, Is.True);
        }

        [Test]
        public void RespectsTimeLimit()
        {
            var searcher = new Searcher(8);
            foreach (int limit in new[] { 50, 250 })
            {
                var sw = Stopwatch.StartNew();
                var r = searcher.Search(Position.FromFen(Kiwipete), SearchLimits.Time(limit));
                sw.Stop();
                Assert.That(sw.ElapsedMilliseconds, Is.LessThan(limit + 120), "limite " + limit);
                Assert.That(r.Depth, Is.GreaterThanOrEqualTo(1));
                Assert.That(MoveGenerator.Legal(Position.FromFen(Kiwipete)), Does.Contain(r.BestMove));
            }
        }

        [Test]
        public void RespectsDepthLimit()
        {
            var r = Analyze(Position.StartFen, 3);
            Assert.That(r.Depth, Is.EqualTo(3));
        }

        [Test]
        public void AsyncSearchCanBeCancelled()
        {
            var ai = new ChessAI(4);
            var game = new Game(Kiwipete);
            using (var cts = new CancellationTokenSource())
            {
                var sw = Stopwatch.StartNew();
                var task = ai.FindMoveAsync(game.Position, new EngineOptions { Level = Difficulty.Maitre, MoveTimeMs = 20000, AllowBook = false }, cts.Token);
                Thread.Sleep(100);
                cts.Cancel();
                Assert.That(() => task.GetAwaiter().GetResult(), Throws.InstanceOf<OperationCanceledException>());
                Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1500));
            }
        }

        [Test]
        public void SyncSearchReturnsBestSoFarWhenCancelled()
        {
            var searcher = new Searcher(4);
            using (var cts = new CancellationTokenSource(100))
            {
                var sw = Stopwatch.StartNew();
                var r = searcher.Search(Position.FromFen(Kiwipete), new SearchLimits { CancellationToken = cts.Token });
                Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1500));
                Assert.That(r.Aborted, Is.True);
                Assert.That(MoveGenerator.Legal(Position.FromFen(Kiwipete)), Does.Contain(r.BestMove));
            }
        }

        [Test]
        public void StopFromAnotherThread()
        {
            var searcher = new Searcher(4);
            var task = Task.Run(() => searcher.Search(Position.Start(), new SearchLimits()));
            Thread.Sleep(100);
            searcher.Stop();
            Assert.That(task.Wait(1500), Is.True);
            Assert.That(task.Result.BestMove.IsNull, Is.False);
        }

        [Test]
        public async Task FindMoveAsyncOnGameAndHint()
        {
            var ai = new ChessAI(4, seed: 5);
            var game = new Game();
            var m = await ai.FindMoveAsync(game, Difficulty.Facile, CancellationToken.None);
            Assert.That(game.TryMakeMove(m), Is.True);
            var hint = await ai.SuggestMoveAsync(game, 100);
            Assert.That(game.IsLegal(hint), Is.True);
            Assert.That(game.IsLegal(ai.SuggestMove(game, 50)), Is.True);
        }

        // ---------- Évaluation ----------

        static readonly string[] EvalFens =
        {
            Position.StartFen,
            Kiwipete,
            "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
            "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1",
            "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
            "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10",
            "rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3",
            "8/8/4k3/8/2Q5/8/4K3/8 b - - 0 1",
            "8/5pk1/6p1/8/8/1P6/P4PPP/6K1 w - - 0 1",
        };

        [Test]
        public void EvaluationIsColorSymmetric()
        {
            var eval = new Evaluator();
            var fens = new List<string>(EvalFens);
            // Plus des positions variées issues de parties IA contre IA.
            var ai = new ChessAI(4, seed: 11);
            var game = new Game();
            for (int ply = 0; ply < 60 && !game.IsOver; ply++)
            {
                game.TryMakeMove(ai.FindMove(game.Position, new EngineOptions { Level = Difficulty.Debutant, MoveTimeMs = 20 }));
                fens.Add(game.Position.ToFen());
            }

            foreach (var fen in fens)
            {
                var mirrored = MirrorFen(fen);
                Assert.That(MirrorFen(mirrored), Is.EqualTo(fen), "miroir non involutif");
                int a = eval.Evaluate(Position.FromFen(fen));
                int b = eval.Evaluate(Position.FromFen(mirrored));
                Assert.That(b, Is.EqualTo(a), fen + " / " + mirrored);
            }
        }

        [Test]
        public void EvaluationBasics()
        {
            Assert.That(Math.Abs(Evaluator.EvaluateOnce(Position.Start())), Is.LessThan(30));
            // Dame de plus pour les blancs : très positif au trait blanc, très négatif au trait noir.
            Assert.That(Evaluator.EvaluateOnce(Position.FromFen("4k3/8/8/8/8/8/8/3QK3 w - - 0 1")), Is.GreaterThan(700));
            Assert.That(Evaluator.EvaluateOnce(Position.FromFen("4k3/8/8/8/8/8/8/3QK3 b - - 0 1")), Is.LessThan(-700));
            // Pion passé avancé > pion en arrière.
            int advanced = Evaluator.EvaluateOnce(Position.FromFen("4k3/8/1P6/8/8/8/8/4K3 w - - 0 1"));
            int back = Evaluator.EvaluateOnce(Position.FromFen("4k3/8/8/8/8/8/1P6/4K3 w - - 0 1"));
            Assert.That(advanced, Is.GreaterThan(back));
        }

        // ---------- Livre d'ouvertures ----------

        [Test]
        public void OpeningBookParsesAndGivesLegalMoves()
        {
            Assert.That(OpeningBook.Errors, Is.Empty);
            Assert.That(OpeningBook.PositionCount, Is.GreaterThan(100));
            var rnd = new Random(1);
            var seen = new HashSet<Move>();
            for (int i = 0; i < 30; i++)
            {
                var m = OpeningBook.Pick(Position.Start(), rnd);
                Assert.That(MoveGenerator.Legal(Position.Start()), Does.Contain(m));
                seen.Add(m);
            }
            Assert.That(seen.Count, Is.GreaterThan(1), "le livre doit varier");
            Assert.That(OpeningBook.Pick(Position.FromFen(Kiwipete), rnd).IsNull, Is.True);
        }

        // ---------- Niveaux ----------

        [Test]
        public void LevelsAreOrdered()
        {
            Assert.That(ChessAI.AllLevels.Count, Is.GreaterThanOrEqualTo(5));
            for (int i = 1; i < ChessAI.AllLevels.Count; i++)
            {
                Assert.That(ChessAI.AllLevels[i].MaxDepth, Is.GreaterThan(ChessAI.AllLevels[i - 1].MaxDepth));
                Assert.That(ChessAI.AllLevels[i].CandidateMargin, Is.LessThan(ChessAI.AllLevels[i - 1].CandidateMargin));
            }
            Assert.That(ChessAI.GetSettings(Difficulty.Debutant).DisplayName, Is.EqualTo("Débutant"));
        }

        [Test]
        public void BeginnerVariesButStaysReasonable()
        {
            // Depuis une position calme, le débutant ne joue pas toujours le même coup et ne donne pas sa dame.
            string fen = "r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3";
            var ai = new ChessAI(4, seed: 9);
            var seen = new HashSet<Move>();
            var exact = new Searcher(4).Search(Position.FromFen(fen), new SearchLimits { MaxDepth = 3, ExactRootScores = true });
            for (int i = 0; i < 12; i++)
            {
                var m = ai.FindMove(Position.FromFen(fen), new EngineOptions { Level = Difficulty.Debutant, MoveTimeMs = 100 });
                seen.Add(m);
                int score = exact.RootScores.First(rs => rs.Move == m).Score;
                Assert.That(score, Is.GreaterThan(exact.RootScores[0].Score - 400), "coup absurde " + m);
            }
            Assert.That(seen.Count, Is.GreaterThan(1));
        }

        [Test]
        public void MasterDoesNotLoseAgainstBeginner()
        {
            var ai = new ChessAI(8, seed: 2024);
            int masterWins = 0, draws = 0;
            for (int g = 0; g < 4; g++)
            {
                var game = new Game();
                var masterColor = g % 2 == 0 ? PieceColor.White : PieceColor.Black;
                ai.NewGame();
                for (int ply = 0; ply < 200 && !game.IsOver; ply++)
                {
                    bool master = game.SideToMove == masterColor;
                    var options = master
                        ? new EngineOptions { Level = Difficulty.Maitre, MoveTimeMs = 40 }
                        : new EngineOptions { Level = Difficulty.Debutant, MoveTimeMs = 40 };
                    var m = ai.FindMove(game.Position, options);
                    Assert.That(game.TryMakeMove(m), Is.True);
                }
                bool masterLost = (game.Result == GameResult.WhiteWins && masterColor == PieceColor.Black)
                                  || (game.Result == GameResult.BlackWins && masterColor == PieceColor.White);
                Assert.That(masterLost, Is.False, "Maître a perdu : " + game.ToPgn());
                if (game.Result == GameResult.Draw || game.Result == GameResult.Ongoing) draws++;
                else masterWins++;
            }
            TestContext.WriteLine("Maître : " + masterWins + " victoires, " + draws + " nulles/inachevées sur 4");
            Assert.That(masterWins, Is.GreaterThanOrEqualTo(2));
        }

        // ---------- Performance (à lancer à la main) ----------

        [Test, Explicit("mesure de performance")]
        public void Benchmark()
        {
            foreach (var fen in new[] { Position.StartFen, Kiwipete })
            {
                var searcher = new Searcher(16);
                var r = searcher.Search(Position.FromFen(fen), SearchLimits.Time(1000));
                TestContext.WriteLine(fen);
                TestContext.WriteLine("  " + r + " pv " + string.Join(" ", r.PrincipalVariation.Select(m => m.ToUci())));
            }
            foreach (var fen in new[] { Position.StartFen, Kiwipete })
            {
                var r = new Searcher(16).Search(Position.FromFen(fen), new SearchLimits { MaxTimeMs = 1000, ExactRootScores = true });
                TestContext.WriteLine("  scores racine exacts : " + r);
            }
            var sw = Stopwatch.StartNew();
            long n = MoveGenerator.Perft(Position.Start(), 5);
            TestContext.WriteLine("perft 5 : " + n + " en " + sw.ElapsedMilliseconds + " ms, " + (n * 1000 / Math.Max(1, sw.ElapsedMilliseconds)) + " feuilles/s");
            var e = new Evaluator();
            var pos = Position.FromFen(Kiwipete);
            sw.Restart();
            int sum = 0;
            for (int i = 0; i < 1000000; i++) sum += e.Evaluate(pos);
            TestContext.WriteLine("eval : " + sw.ElapsedMilliseconds + " ns/eval (" + sum + ")");
        }
    }
}
