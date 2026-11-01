using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KevChess.AI;
using KevChess.Core;
using NUnit.Framework;

namespace KevChess.Tests
{
    /// <summary>Horloge, historique, notation française, ouvertures, analyse, problèmes et nulle proposée à l'IA.</summary>
    public class FeatureTests
    {
        static ChessGame Play(string fen, params string[] sans)
        {
            var game = fen == null ? new ChessGame() : new ChessGame(fen);
            foreach (var san in sans)
                Assert.That(game.TryMakeSan(san), Is.True, "coup refusé : " + san + " dans " + game.Position.ToFen());
            return game;
        }

        static List<Move> UciMoves(string line)
        {
            var pos = Position.Start();
            var list = new List<Move>();
            foreach (var uci in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var m = Notation.Parse(pos, uci);
                Assert.That(m.IsNull, Is.False, "coup illégal : " + uci + " dans " + line);
                pos.MakeMove(m);
                list.Add(m);
            }
            return list;
        }

        // =====================================================================
        // Horloge
        // =====================================================================

        [Test]
        public void TimeControlPresets()
        {
            var labels = TimeControl.Presets.Select(t => t.Label).ToArray();
            Assert.That(labels, Is.EqualTo(new[] { "Sans limite", "1+0", "3+2", "5+0", "10+5", "15+10" }));
            Assert.That(TimeControl.Presets[0].IsUnlimited, Is.True);
            Assert.That(TimeControl.Presets.Skip(1).All(t => !t.IsUnlimited), Is.True);
            Assert.That(TimeControl.Presets[2].BaseSeconds, Is.EqualTo(180));
            Assert.That(TimeControl.Presets[2].IncrementSeconds, Is.EqualTo(2));
        }

        [Test]
        public void ClockTicksAndIncrements()
        {
            var clock = new ChessClock(new TimeControl(180, 2));
            Assert.That(clock.Running, Is.Null);
            Assert.That(clock.Remaining(PieceColor.White), Is.EqualTo(180));

            clock.Tick(5); // pas encore démarrée
            Assert.That(clock.Remaining(PieceColor.White), Is.EqualTo(180));

            clock.Start(PieceColor.White);
            Assert.That(clock.Running, Is.EqualTo(PieceColor.White));
            clock.Tick(10);
            Assert.That(clock.Remaining(PieceColor.White), Is.EqualTo(170).Within(1e-9));
            Assert.That(clock.Remaining(PieceColor.Black), Is.EqualTo(180));

            clock.Press(PieceColor.White);
            Assert.That(clock.Remaining(PieceColor.White), Is.EqualTo(172).Within(1e-9));
            Assert.That(clock.Running, Is.EqualTo(PieceColor.Black));
            clock.Tick(3.5);
            Assert.That(clock.Remaining(PieceColor.Black), Is.EqualTo(176.5).Within(1e-9));
            clock.Press(PieceColor.Black);
            Assert.That(clock.Remaining(PieceColor.Black), Is.EqualTo(178.5).Within(1e-9));
            Assert.That(clock.Running, Is.EqualTo(PieceColor.White));
        }

        [Test]
        public void ClockPauseAndResume()
        {
            var clock = new ChessClock(new TimeControl(60, 0));
            clock.Start(PieceColor.Black);
            clock.Pause();
            Assert.That(clock.IsPaused, Is.True);
            Assert.That(clock.Running, Is.Null);
            Assert.That(clock.Turn, Is.EqualTo(PieceColor.Black));
            clock.Tick(30);
            Assert.That(clock.Remaining(PieceColor.Black), Is.EqualTo(60));
            clock.Resume();
            Assert.That(clock.Running, Is.EqualTo(PieceColor.Black));
            clock.Tick(30);
            Assert.That(clock.Remaining(PieceColor.Black), Is.EqualTo(30).Within(1e-9));
        }

        [Test]
        public void ClockFlagFall()
        {
            var clock = new ChessClock(new TimeControl(60, 0));
            clock.Start(PieceColor.White);
            Assert.That(clock.IsFlagged(out _), Is.False);
            clock.Tick(61);
            Assert.That(clock.Remaining(PieceColor.White), Is.EqualTo(0));
            Assert.That(clock.IsFlagged(out var loser), Is.True);
            Assert.That(loser, Is.EqualTo(PieceColor.White));
            Assert.That(clock.Running, Is.Null);
            clock.Press(PieceColor.White); // trop tard : pas d'incrément ni de bascule
            Assert.That(clock.Remaining(PieceColor.White), Is.EqualTo(0));
        }

        [Test]
        public void ClockUnlimitedNeverRuns()
        {
            var clock = new ChessClock(TimeControl.Unlimited);
            clock.Start(PieceColor.White);
            clock.Tick(10000);
            Assert.That(double.IsPositiveInfinity(clock.Remaining(PieceColor.White)), Is.True);
            Assert.That(clock.IsFlagged(out _), Is.False);
            clock.Press(PieceColor.White);
            Assert.That(clock.Turn, Is.EqualTo(PieceColor.Black));
        }

        [Test]
        public void ClockStateRestores()
        {
            var clock = new ChessClock(new TimeControl(300, 0));
            clock.RemainingWhite = 12.5;
            clock.RemainingBlack = 99;
            clock.Start(PieceColor.Black);
            clock.Tick(9);
            Assert.That(clock.Remaining(PieceColor.White), Is.EqualTo(12.5));
            Assert.That(clock.RemainingBlack, Is.EqualTo(90).Within(1e-9));
        }

        [Test]
        public void TimeoutLosesWhenOpponentCanMate()
        {
            var game = new ChessGame("4k3/8/8/8/8/8/8/R3K3 w - - 0 1");
            game.Timeout(PieceColor.Black);
            Assert.That(game.Result, Is.EqualTo(GameResult.WhiteWins));
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.Timeout));

            var start = new ChessGame();
            start.Timeout(PieceColor.White);
            Assert.That(start.Result, Is.EqualTo(GameResult.BlackWins));
            Assert.That(start.EndReason, Is.EqualTo(GameEndReason.Timeout));
        }

        [TestCase("4k3/8/8/8/8/8/8/R3K3 w - - 0 1")]   // l'adversaire (Noirs) : roi seul
        [TestCase("4kb2/8/8/8/8/8/8/R3K3 w - - 0 1")]  // roi + fou
        [TestCase("4kn2/8/8/8/8/8/8/R3K3 w - - 0 1")]  // roi + cavalier
        public void TimeoutIsDrawWhenOpponentCannotMate(string fen)
        {
            var game = new ChessGame(fen);
            game.Timeout(PieceColor.White);
            Assert.That(game.Result, Is.EqualTo(GameResult.Draw));
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.TimeoutInsufficientMaterial));
        }

        [Test]
        public void TimeoutWithTwoMinorsOrPawnStillLoses()
        {
            var bishops = new ChessGame("2b1kb2/8/8/8/8/8/8/R3K3 w - - 0 1");
            bishops.Timeout(PieceColor.White);
            Assert.That(bishops.Result, Is.EqualTo(GameResult.BlackWins));

            var pawn = new ChessGame("4k3/7p/8/8/8/8/8/R3K3 w - - 0 1");
            pawn.Timeout(PieceColor.White);
            Assert.That(pawn.Result, Is.EqualTo(GameResult.BlackWins));
        }

        [Test]
        public void TimeoutIgnoredWhenGameOver()
        {
            var game = Play(null, "f3", "e5", "g4", "Qh4#");
            game.Timeout(PieceColor.Black);
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.Checkmate));
            Assert.That(game.Result, Is.EqualTo(GameResult.BlackWins));
        }

        // =====================================================================
        // Historique
        // =====================================================================

        [Test]
        public void PositionAtReplaysHistory()
        {
            var game = new ChessGame();
            var fens = new List<string> { game.Position.ToFen() };
            foreach (var san in new[] { "e4", "e5", "Nf3", "Nc6", "Bb5", "a6", "O-O" })
            {
                Assert.That(game.TryMakeSan(san), Is.True);
                fens.Add(game.Position.ToFen());
            }
            Assert.That(game.Moves.Count, Is.EqualTo(7));
            for (int i = 0; i <= 7; i++) Assert.That(game.PositionAt(i).ToFen(), Is.EqualTo(fens[i]), "ply " + i);
            Assert.That(game.PositionAt(0).ToFen(), Is.EqualTo(Position.StartFen));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.PositionAt(8));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.PositionAt(-1));

            // Copie indépendante : la modifier ne touche pas la partie.
            var copy = game.PositionAt(7);
            copy.MakeMove(MoveGenerator.Legal(copy)[0]);
            Assert.That(game.Position.ToFen(), Is.EqualTo(fens[7]));
        }

        [Test]
        public void PositionAtFromCustomFen()
        {
            const string fen = "4k3/8/8/8/8/8/4P3/4K3 w - - 0 1";
            var game = Play(fen, "e4", "Kd7");
            Assert.That(game.PositionAt(0).ToFen(), Is.EqualTo(fen));
            Assert.That(game.PositionAt(2).ToFen(), Is.EqualTo(game.Position.ToFen()));
        }

        // =====================================================================
        // Notation française
        // =====================================================================

        [TestCase("Nf3", "Cf3")]
        [TestCase("Qxf7#", "Dxf7#")]
        [TestCase("Kxe2", "Rxe2")]
        [TestCase("Rad1", "Tad1")]
        [TestCase("Bb5+", "Fb5+")]
        [TestCase("e8=Q+", "e8=D+")]
        [TestCase("exd8=N", "exd8=C")]
        [TestCase("b1=R", "b1=T")]
        [TestCase("a8=B", "a8=F")]
        [TestCase("O-O-O", "O-O-O")]
        [TestCase("e4", "e4")]
        [TestCase("Nbd7", "Cbd7")]
        public void LocalizeToFrench(string san, string french)
        {
            Assert.That(Notation.Localize(san, true), Is.EqualTo(french));
            Assert.That(Notation.Localize(san, false), Is.EqualTo(san));
        }

        [Test]
        public void LocalizeHandlesEmpty()
        {
            Assert.That(Notation.Localize(null, true), Is.Null);
            Assert.That(Notation.Localize("", true), Is.EqualTo(""));
        }

        // =====================================================================
        // Ouvertures
        // =====================================================================

        [Test]
        public void OpeningTableLinesAreLegal()
        {
            Assert.That(OpeningNames.Count, Is.GreaterThanOrEqualTo(60));
            foreach (var line in OpeningNames.All)
            {
                var moves = UciMoves(line.Key);
                Assert.That(OpeningNames.Lookup(moves, Position.StartFen), Is.EqualTo(line.Value), line.Key);
            }
        }

        [TestCase("e4 e5 Nf3 Nc6 Bc4", "Partie italienne")]
        [TestCase("e4 e5 Nf3 Nc6 Bc4 Bc5 b4", "Gambit Evans")]
        [TestCase("e4 e5 Nf3 Nc6 Bb5", "Partie espagnole (Ruy Lopez)")]
        [TestCase("e4 e5 Nf3 Nc6 Bb5 a6 Ba4", "Partie espagnole, défense Morphy")]
        [TestCase("e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 a6 Be3", "Défense sicilienne, variante Najdorf")]
        [TestCase("e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 g6", "Défense sicilienne, variante du Dragon")]
        [TestCase("e4 c5 Nf3 Nc6", "Défense sicilienne")]
        [TestCase("e4 e6 d4 d5", "Défense française")]
        [TestCase("e4 c6 d4 d5 e5", "Défense Caro-Kann, variante d'avance")]
        [TestCase("d4 d5 c4 dxc4", "Gambit dame accepté")]
        [TestCase("d4 d5 c4 e6 Nc3", "Gambit dame refusé")]
        [TestCase("d4 Nf6 c4 g6 Nc3 Bg7 e4 d6 Nf3", "Défense est-indienne, variante classique")]
        [TestCase("d4 Nf6 c4 e6 Nc3 Bb4", "Défense nimzo-indienne")]
        [TestCase("c4 e5 Nc3", "Partie anglaise, sicilienne inversée")]
        [TestCase("Nf3 d5", "Ouverture Réti")]
        [TestCase("e4 d5 exd5 Qxd5", "Défense scandinave, variante principale")]
        [TestCase("e4 d6 d4 Nf6 Nc3 g6", "Défense Pirc")]
        [TestCase("e4 e5 Nf3 Nc6 d4 exd4", "Partie écossaise")]
        [TestCase("e4 e5 f4 exf4 Nf3", "Gambit du roi accepté")]
        [TestCase("d4 d5 Bf4", "Système de Londres")]
        [TestCase("d4 d5 c4 c6 Nf3", "Défense slave")]
        [TestCase("e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Nf6 O-O Be7", "Partie espagnole fermée")]
        public void OpeningLookup(string sans, string expected)
        {
            var game = Play(null, sans.Split(' '));
            Assert.That(OpeningNames.Lookup(game.Moves, game.StartFen), Is.EqualTo(expected));
            Assert.That(OpeningNames.Lookup(game.Moves, null), Is.EqualTo(expected));
        }

        [Test]
        public void OpeningLookupUnknownOrCustomStart()
        {
            Assert.That(OpeningNames.Lookup(new List<Move>(), Position.StartFen), Is.Null);
            var odd = Play(null, "a3");
            Assert.That(OpeningNames.Lookup(odd.Moves, odd.StartFen), Is.Null);

            const string fen = "4k3/8/8/8/8/8/4P3/4K3 w - - 0 1";
            var custom = Play(fen, "e4");
            Assert.That(OpeningNames.Lookup(custom.Moves, custom.StartFen), Is.Null);
        }

        // =====================================================================
        // Analyse de partie
        // =====================================================================

        [Test]
        public void QualityThresholds()
        {
            Assert.That(GameAnalyzer.Classify(0), Is.EqualTo(MoveQuality.Excellent));
            Assert.That(GameAnalyzer.Classify(20), Is.EqualTo(MoveQuality.Excellent));
            Assert.That(GameAnalyzer.Classify(21), Is.EqualTo(MoveQuality.Bon));
            Assert.That(GameAnalyzer.Classify(60), Is.EqualTo(MoveQuality.Bon));
            Assert.That(GameAnalyzer.Classify(150), Is.EqualTo(MoveQuality.Imprecision));
            Assert.That(GameAnalyzer.Classify(300), Is.EqualTo(MoveQuality.Erreur));
            Assert.That(GameAnalyzer.Classify(301), Is.EqualTo(MoveQuality.Gaffe));
        }

        [Test]
        public void LossIsFromMoverPointOfView()
        {
            Assert.That(GameAnalyzer.Loss(50, -100, PieceColor.White), Is.EqualTo(150));
            Assert.That(GameAnalyzer.Loss(50, -100, PieceColor.Black), Is.EqualTo(0));
            Assert.That(GameAnalyzer.Loss(-30, 270, PieceColor.Black), Is.EqualTo(300));
            // Rater un mat en restant largement gagnant ne coûte presque rien.
            Assert.That(GameAnalyzer.Loss(GameAnalyzer.MateCp, 950, PieceColor.White), Is.EqualTo(50));
            Assert.That(GameAnalyzer.ToCp(Searcher.MateScore - 3), Is.EqualTo(GameAnalyzer.MateCp));
            Assert.That(GameAnalyzer.ToCp(-Searcher.MateScore + 4), Is.EqualTo(-GameAnalyzer.MateCp));
            Assert.That(GameAnalyzer.ToCp(123), Is.EqualTo(123));
        }

        [Test]
        public void AccuracyCurve()
        {
            Assert.That(GameAnalyzer.Accuracy(0), Is.EqualTo(100).Within(0.01));
            Assert.That(GameAnalyzer.Accuracy(20), Is.InRange(85, 92));
            Assert.That(GameAnalyzer.Accuracy(100), Is.InRange(45, 65));
            Assert.That(GameAnalyzer.Accuracy(5000), Is.EqualTo(GameAnalyzer.Accuracy(GameAnalyzer.AccuracyLossCap)));
            Assert.That(GameAnalyzer.Accuracy(5000), Is.GreaterThanOrEqualTo(0));
            double previous = 101;
            for (int loss = 0; loss <= 1000; loss += 50)
            {
                double a = GameAnalyzer.Accuracy(loss);
                if (loss <= 550) Assert.That(a, Is.LessThan(previous));
                else Assert.That(a, Is.LessThanOrEqualTo(previous));
                previous = a;
            }
        }

        [Test]
        public async Task AnalyzeScholarsMate()
        {
            var game = Play(null, "e4", "e5", "Bc4", "Nc6", "Qh5", "Nf6", "Qxf7#");
            var progress = new List<float>();
            var analyzer = new GameAnalyzer(8) { MaxDepth = 6 };
            int callerThread = Environment.CurrentManagedThreadId;
            var analysis = await analyzer.AnalyzeAsync(game.StartFen, game.Moves, 300, new SyncProgress(progress), CancellationToken.None);

            Assert.That(analysis.Moves.Count, Is.EqualTo(7));
            Assert.That(progress.Count, Is.EqualTo(8));
            Assert.That(progress.Last(), Is.EqualTo(1f));

            var blunder = analysis.Moves[5];
            Assert.That(blunder.San, Is.EqualTo("Nf6"));
            Assert.That(blunder.Side, Is.EqualTo(PieceColor.Black));
            Assert.That(blunder.Quality, Is.EqualTo(MoveQuality.Gaffe));
            Assert.That(blunder.EvalAfterCp, Is.EqualTo(GameAnalyzer.MateCp));
            Assert.That(blunder.BestSan, Is.Not.Null.And.Not.EqualTo("Nf6"));

            var mate = analysis.Moves[6];
            Assert.That(mate.San, Is.EqualTo("Qxf7#"));
            Assert.That(mate.Ply, Is.EqualTo(6));
            Assert.That(mate.Quality, Is.EqualTo(MoveQuality.Excellent));
            Assert.That(mate.EvalBeforeCp, Is.EqualTo(GameAnalyzer.MateCp));
            Assert.That(mate.EvalAfterCp, Is.EqualTo(GameAnalyzer.MateCp));
            Assert.That(mate.BestSan, Is.EqualTo("Qxf7#"));

            Assert.That(analysis.AccuracyWhite, Is.InRange(0, 100));
            Assert.That(analysis.AccuracyBlack, Is.InRange(0, 100));
            Assert.That(analysis.AccuracyWhite, Is.GreaterThan(analysis.AccuracyBlack));
            for (int i = 0; i < analysis.Moves.Count; i++)
            {
                Assert.That(analysis.Moves[i].Ply, Is.EqualTo(i));
                if (i + 1 < analysis.Moves.Count)
                    Assert.That(analysis.Moves[i].EvalAfterCp, Is.EqualTo(analysis.Moves[i + 1].EvalBeforeCp));
            }
            Assert.That(callerThread, Is.GreaterThan(0));
        }

        [Test]
        public void AnalyzeBlackMatesAndCustomFen()
        {
            // Les Noirs matent depuis une position donnée : évaluation finale −10000.
            var game = Play("r5k1/8/8/8/8/8/5PPP/6K1 b - - 0 1", "Ra1#");
            var analysis = new GameAnalyzer(4) { MaxDepth = 4 }.Analyze(game.StartFen, game.Moves, 200, null, CancellationToken.None);
            Assert.That(analysis.Moves.Count, Is.EqualTo(1));
            Assert.That(analysis.Moves[0].Side, Is.EqualTo(PieceColor.Black));
            Assert.That(analysis.Moves[0].EvalAfterCp, Is.EqualTo(-GameAnalyzer.MateCp));
            Assert.That(analysis.Moves[0].Quality, Is.EqualTo(MoveQuality.Excellent));
            Assert.That(analysis.AccuracyWhite, Is.EqualTo(100));
            Assert.That(analysis.AccuracyBlack, Is.EqualTo(100).Within(0.01));
        }

        [Test]
        public void AnalyzeCanBeCancelled()
        {
            var game = Play(null, "e4", "e5", "Nf3", "Nc6", "Bb5", "a6");
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var analyzer = new GameAnalyzer(4);
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await analyzer.AnalyzeAsync(game.StartFen, game.Moves, 500, null, cts.Token));

            using var later = new CancellationTokenSource(150);
            Assert.CatchAsync<OperationCanceledException>(async () =>
                await analyzer.AnalyzeAsync(game.StartFen, game.Moves, 2000, null, later.Token));
        }

        [Test]
        public void AnalyzeRejectsIllegalMove()
        {
            var moves = new List<Move> { new Move(Square.Parse("e2"), Square.Parse("e5")) };
            Assert.Throws<ArgumentException>(() =>
                new GameAnalyzer(4) { MaxDepth = 2 }.Analyze(Position.StartFen, moves, 50, null, CancellationToken.None));
        }

        /// <summary>IProgress synchrone (Progress&lt;T&gt; poste sur le contexte et arriverait après la fin du test).</summary>
        sealed class SyncProgress : IProgress<float>
        {
            readonly List<float> values;
            public SyncProgress(List<float> values) { this.values = values; }
            public void Report(float value) { lock (values) values.Add(value); }
        }

        // =====================================================================
        // Problèmes
        // =====================================================================

        [Test]
        public void PuzzleSetIsOrderedAndUnique()
        {
            var all = PuzzleSet.All;
            Assert.That(all.Count, Is.GreaterThanOrEqualTo(30));
            Assert.That(all.Select(p => p.Id).Distinct().Count(), Is.EqualTo(all.Count));
            for (int i = 1; i < all.Count; i++)
                Assert.That(all[i].MateIn, Is.GreaterThanOrEqualTo(all[i - 1].MateIn), all[i].Id);
            Assert.That(all.Count(p => p.MateIn == 1), Is.GreaterThan(0));
            Assert.That(all.Count(p => p.MateIn == 2), Is.GreaterThan(0));
            Assert.That(all.Count(p => p.MateIn == 3), Is.GreaterThan(0));
            Assert.That(PuzzleSet.Find("m2-01"), Is.SameAs(all.First(p => p.Id == "m2-01")));
        }

        static IEnumerable<TestCaseData> AllPuzzles() =>
            PuzzleSet.All.Select(p => new TestCaseData(p).SetName("Puzzle_" + p.Id.Replace('-', '_')));

        [TestCaseSource(nameof(AllPuzzles))]
        public void PuzzleIsValid(Puzzle puzzle)
        {
            Assert.That(puzzle.Title, Is.Not.Empty);
            Position pos = null;
            Assert.DoesNotThrow(() => pos = Position.FromFen(puzzle.Fen), "FEN invalide");
            Assert.That(pos.IsInCheck(Piece.Opposite(pos.SideToMove)), Is.False, "le camp qui n'a pas le trait est en échec");
            Assert.That(puzzle.SolutionUci.Length, Is.EqualTo(2 * puzzle.MateIn - 1));

            var game = new ChessGame(puzzle.Fen);
            var player = game.SideToMove;
            for (int i = 0; i < puzzle.SolutionUci.Length; i++)
            {
                var move = Notation.Parse(game.Position, puzzle.SolutionUci[i]);
                Assert.That(move.IsNull, Is.False, "coup illégal " + puzzle.SolutionUci[i] + " (" + i + ")");
                Assert.That(game.TryMakeMove(move), Is.True);
                if (i < puzzle.SolutionUci.Length - 1)
                    Assert.That(game.IsOver, Is.False, "partie finie trop tôt au coup " + i);
            }
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.Checkmate));
            Assert.That(game.Result, Is.EqualTo(player == PieceColor.White ? GameResult.WhiteWins : GameResult.BlackWins));
        }

        static Move M(string uci) => new Move(Square.Parse(uci.Substring(0, 2)), Square.Parse(uci.Substring(2, 2)),
            MoveFlags.None, uci.Length > 4 ? (PieceType)" pnbrqk".IndexOf(uci[4]) : PieceType.None);

        [Test]
        public void PuzzleSessionFollowsSolution()
        {
            var puzzle = PuzzleSet.Find("m3-02");
            var session = new PuzzleSession(puzzle);
            Assert.That(session.PlayerColor, Is.EqualTo(PieceColor.White));
            Assert.That(session.ExpectedMove.ToUci(), Is.EqualTo(puzzle.SolutionUci[0]));

            Assert.That(session.TryMove(M(puzzle.SolutionUci[0]), out var reply), Is.EqualTo(PuzzleMoveResult.Correct));
            Assert.That(reply.ToUci(), Is.EqualTo(puzzle.SolutionUci[1]));
            Assert.That(session.Game.Moves.Count, Is.EqualTo(2));

            Assert.That(session.TryMove(M(puzzle.SolutionUci[2]), out reply), Is.EqualTo(PuzzleMoveResult.Correct));
            Assert.That(reply.ToUci(), Is.EqualTo(puzzle.SolutionUci[3]));

            Assert.That(session.TryMove(M(puzzle.SolutionUci[4]), out reply), Is.EqualTo(PuzzleMoveResult.Solved));
            Assert.That(reply.IsNull, Is.True);
            Assert.That(session.IsSolved, Is.True);
            Assert.That(session.Mistakes, Is.EqualTo(0));
            Assert.That(session.Game.EndReason, Is.EqualTo(GameEndReason.Checkmate));
            Assert.That(session.TryMove(M("a2a3"), out _), Is.EqualTo(PuzzleMoveResult.Wrong));
        }

        [Test]
        public void PuzzleSessionRejectsWrongMove()
        {
            var session = new PuzzleSession(PuzzleSet.Find("m2-03"));
            string fen = session.Game.Position.ToFen();
            Assert.That(session.TryMove(M("g2g3"), out var reply), Is.EqualTo(PuzzleMoveResult.Wrong));
            Assert.That(reply.IsNull, Is.True);
            Assert.That(session.TryMove(M("a1a8"), out _), Is.EqualTo(PuzzleMoveResult.Wrong)); // illégal
            Assert.That(session.Mistakes, Is.EqualTo(2));
            Assert.That(session.Game.Position.ToFen(), Is.EqualTo(fen));
            Assert.That(session.Game.Moves.Count, Is.EqualTo(0));
            Assert.That(session.TryMove(M("d5g8"), out reply), Is.EqualTo(PuzzleMoveResult.Correct));
            Assert.That(reply.ToUci(), Is.EqualTo("e8g8"));
        }

        [Test]
        public void PuzzleSessionAcceptsAlternativeMate()
        {
            // « Promotion » : la solution est c8=Q#, mais c8=R# mate aussi.
            var session = new PuzzleSession(PuzzleSet.Find("m1-05"));
            Assert.That(session.TryMove(M("c7c8r"), out var reply), Is.EqualTo(PuzzleMoveResult.Solved));
            Assert.That(reply.IsNull, Is.True);
            Assert.That(session.IsSolved, Is.True);
            Assert.That(session.Game.EndReason, Is.EqualTo(GameEndReason.Checkmate));
        }

        [Test]
        public void PuzzleSessionPromotion()
        {
            var session = new PuzzleSession(PuzzleSet.Find("m1-05"));
            Assert.That(session.TryMove(M("c7c8q"), out _), Is.EqualTo(PuzzleMoveResult.Solved));
        }

        // =====================================================================
        // Nulle proposée à l'IA
        // =====================================================================

        [Test]
        public void DrawDecisionRule()
        {
            Assert.That(ChessAI.DecideDraw(-500, 5000, 19), Is.False, "trop tôt");
            Assert.That(ChessAI.DecideDraw(-50, 5000, 20), Is.True);
            Assert.That(ChessAI.DecideDraw(-49, 5000, 20), Is.False, "égal mais beaucoup de matériel");
            Assert.That(ChessAI.DecideDraw(0, 1500, 30), Is.True);
            Assert.That(ChessAI.DecideDraw(49, 1500, 30), Is.True);
            Assert.That(ChessAI.DecideDraw(50, 1500, 30), Is.False);
            Assert.That(ChessAI.DecideDraw(300, 100, 60), Is.False);
            Assert.That(ChessAI.Material(Position.Start()), Is.EqualTo(2 * (800 + 640 + 660 + 1000 + 950)));
        }

        [Test]
        public async Task AIDrawOffers()
        {
            var ai = new ChessAI(4, seed: 1);

            // Avant le 20e demi-coup : refus, même perdant.
            var early = new ChessGame("4k3/8/8/8/8/8/3Q4/4K3 b - - 0 3");
            Assert.That(await ai.AcceptsDrawAsync(early, PieceColor.Black), Is.False);
            Assert.That(await ai.AcceptsDrawAsync(new ChessGame(), PieceColor.Black), Is.False);

            // Une dame de moins : accepte. Une dame de plus : refuse.
            var losing = new ChessGame("4k3/8/8/8/8/8/3Q4/4K3 b - - 0 30");
            Assert.That(await ai.AcceptsDrawAsync(losing, PieceColor.Black), Is.True);
            Assert.That(await ai.AcceptsDrawAsync(losing, PieceColor.White), Is.False);

            // Finale simplifiée et égale : accepte.
            var equal = new ChessGame("4k3/4p3/8/8/8/8/4P3/4K3 w - - 0 30");
            Assert.That(await ai.AcceptsDrawAsync(equal, PieceColor.White), Is.True);

            // Position de départ égale mais pleine de matériel, après 20 demi-coups : refuse.
            var full = new ChessGame("r1bqkb1r/pppp1ppp/2n2n2/4p3/4P3/2N2N2/PPPP1PPP/R1BQKB1R w KQkq - 4 11");
            Assert.That(await ai.AcceptsDrawAsync(full, PieceColor.White), Is.False);

            // Partie terminée : refus.
            var over = Play(null, "f3", "e5", "g4", "Qh4#");
            Assert.That(await ai.AcceptsDrawAsync(over, PieceColor.White), Is.False);
        }

        [Test]
        public void AIDrawOfferCancellation()
        {
            var ai = new ChessAI(4);
            var game = new ChessGame("4k3/8/8/8/8/8/3Q4/4K3 b - - 0 30");
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await ai.AcceptsDrawAsync(game, PieceColor.Black, cts.Token));
        }
    }
}
