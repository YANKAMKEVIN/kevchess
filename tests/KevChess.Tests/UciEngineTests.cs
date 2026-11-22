using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KevChess.AI;
using KevChess.AI.Uci;
using KevChess.Core;
using NUnit.Framework;

namespace KevChess.Tests
{
    /// <summary>
    /// Faux moteur UCI en mémoire : répond comme Stockfish (id, options, uciok, readyok) et, sur « go »,
    /// joue le premier coup légal de la dernière position reçue (ou ce que le test impose).
    /// </summary>
    sealed class FakeUciEngine : IUciTransport
    {
        public readonly List<string> Received = new List<string>();
        readonly BlockingCollection<string> output = new BlockingCollection<string>();
        Position position = Position.Start();

        /// <summary>Réponse au « go » (lignes info + bestmove) ; null = premier coup légal.</summary>
        public Func<Position, string, IEnumerable<string>> OnGo;
        /// <summary>Ne répond au « go » qu'à la réception de « stop ».</summary>
        public bool WaitForStop;
        /// <summary>Ne répond plus du tout (moteur figé).</summary>
        public bool Frozen;
        public bool Disposed;
        bool searching;

        public void Send(string command)
        {
            lock (Received) Received.Add(command);
            if (Frozen) return;
            var t = command.Split(' ');
            switch (t[0])
            {
                case "uci":
                    Emit("id name FakeFish 1.0", "id author Tests",
                         "option name Threads type spin default 1 min 1 max 1024",
                         "option name Hash type spin default 16 min 1 max 33554432",
                         "option name Skill Level type spin default 20 min 0 max 20",
                         "uciok");
                    break;
                case "isready": Emit("readyok"); break;
                case "position": position = ParsePosition(command); break;
                case "go":
                    searching = true;
                    if (!WaitForStop) Answer(command);
                    break;
                case "stop":
                    if (searching) Answer(command);
                    break;
            }
        }

        void Answer(string command)
        {
            searching = false;
            var lines = OnGo != null ? OnGo(position, command) : DefaultAnswer(position);
            Emit(lines.ToArray());
        }

        static IEnumerable<string> DefaultAnswer(Position pos)
        {
            var legal = MoveGenerator.Legal(pos);
            if (legal.Count == 0) { yield return "info depth 0 score mate 0"; yield return "bestmove (none)"; yield break; }
            string m = legal[0].ToUci();
            yield return "info depth 1 seldepth 1 multipv 1 score cp 12 nodes 20 nps 2000 time 10 pv " + m;
            yield return "bestmove " + m;
        }

        static Position ParsePosition(string command)
        {
            var t = command.Split(' ').ToList();
            int movesAt = t.IndexOf("moves");
            Position pos;
            if (t[1] == "startpos") pos = Position.Start();
            else pos = Position.FromFen(string.Join(" ", t.Skip(2).Take((movesAt < 0 ? t.Count : movesAt) - 2)));
            if (movesAt >= 0)
                foreach (var u in t.Skip(movesAt + 1)) pos.MakeMove(Notation.ParseUci(pos, u));
            return pos;
        }

        void Emit(params string[] lines) { foreach (var l in lines) output.Add(l); }

        public string ReadLine(int timeoutMs) => output.TryTake(out var l, timeoutMs) ? l : null;

        public void Dispose() => Disposed = true;

        public string[] Commands() { lock (Received) return Received.ToArray(); }
    }

    public class UciProtocolTests
    {
        [Test]
        public void ParsesBestMoveWithPonder()
        {
            Assert.That(UciProtocol.TryParseBestMove("bestmove e2e4 ponder e7e5", out var b), Is.True);
            Assert.That(b.Move, Is.EqualTo("e2e4"));
            Assert.That(b.Ponder, Is.EqualTo("e7e5"));
            Assert.That(UciProtocol.TryParseBestMove("bestmove e7e8q", out b), Is.True);
            Assert.That(b.Move, Is.EqualTo("e7e8q"));
            Assert.That(b.Ponder, Is.Null);
        }

        [TestCase("bestmove (none)")]
        [TestCase("bestmove 0000")]
        [TestCase("bestmove")]
        public void BestMoveNoneMeansNoMove(string line)
        {
            Assert.That(UciProtocol.TryParseBestMove(line, out var b), Is.True);
            Assert.That(b.IsNone, Is.True);
        }

        [TestCase("info depth 3")]
        [TestCase("readyok")]
        [TestCase("")]
        [TestCase(null)]
        public void OtherLinesAreNotBestMoves(string line) =>
            Assert.That(UciProtocol.TryParseBestMove(line, out _), Is.False);

        [Test]
        public void ParsesFullInfoLine()
        {
            const string line = "info depth 18 seldepth 27 multipv 1 score cp -34 nodes 1234567 nps 987654 hashfull 412 tbhits 0 time 1250 pv e7e5 g1f3 b8c6";
            Assert.That(UciProtocol.TryParseInfo(line, out var i), Is.True);
            Assert.That(i.Depth, Is.EqualTo(18));
            Assert.That(i.SelDepth, Is.EqualTo(27));
            Assert.That(i.MultiPv, Is.EqualTo(1));
            Assert.That(i.HasScore && !i.IsMate && !i.IsBound, Is.True);
            Assert.That(i.ScoreCp, Is.EqualTo(-34));
            Assert.That(i.Score, Is.EqualTo(-34));
            Assert.That(i.Nodes, Is.EqualTo(1234567));
            Assert.That(i.Nps, Is.EqualTo(987654));
            Assert.That(i.HashFull, Is.EqualTo(412));
            Assert.That(i.TimeMs, Is.EqualTo(1250));
            Assert.That(i.Pv, Is.EqualTo(new[] { "e7e5", "g1f3", "b8c6" }));
        }

        [Test]
        public void MateScoresUseTheSearcherScale()
        {
            UciProtocol.TryParseInfo("info depth 5 score mate 2 pv d1h5", out var win);
            Assert.That(win.IsMate, Is.True);
            Assert.That(win.MateIn, Is.EqualTo(2));
            Assert.That(win.Score, Is.EqualTo(Searcher.MateScore - 3)); // mat en 2 = 3 demi-coups
            Assert.That(win.Score, Is.GreaterThan(Searcher.MateBound));

            UciProtocol.TryParseInfo("info depth 5 score mate -1", out var loss);
            Assert.That(loss.MateIn, Is.EqualTo(-1));
            Assert.That(loss.Score, Is.EqualTo(-(Searcher.MateScore - 2)));
            Assert.That(loss.Score, Is.LessThan(-Searcher.MateBound));
        }

        [Test]
        public void BoundsAndStringsAreRecognised()
        {
            UciProtocol.TryParseInfo("info depth 12 score cp 40 lowerbound nodes 10 pv e2e4", out var i);
            Assert.That(i.IsBound, Is.True);
            Assert.That(i.ScoreCp, Is.EqualTo(40));
            Assert.That(i.Nodes, Is.EqualTo(10));

            UciProtocol.TryParseInfo("info string NNUE evaluation using nn-1111.nnue enabled", out var s);
            Assert.That(s.Text, Is.EqualTo("NNUE evaluation using nn-1111.nnue enabled"));
            Assert.That(s.HasScore, Is.False);

            UciProtocol.TryParseInfo("info depth 9 currmove g1f3 currmovenumber 2", out var c);
            Assert.That(c.Depth, Is.EqualTo(9));
            Assert.That(c.Pv, Is.Empty);
        }

        [Test]
        public void GarbageNumbersDoNotThrow()
        {
            Assert.That(UciProtocol.TryParseInfo("info depth x score cp nodes", out var i), Is.True);
            Assert.That(i.Depth, Is.EqualTo(0));
            Assert.That(UciProtocol.TryParseInfo("bestmove e2e4", out _), Is.False);
        }

        [Test]
        public void FormatsPositionCommands()
        {
            var game = new ChessGame();
            game.TryMakeMove(Notation.Parse(game.Position, "e4"));
            game.TryMakeMove(Notation.Parse(game.Position, "e5"));
            Assert.That(UciProtocol.PositionCommand(game.StartFen, game.Moves), Is.EqualTo("position startpos moves e2e4 e7e5"));
            Assert.That(UciProtocol.PositionCommand(Position.StartFen, null), Is.EqualTo("position startpos"));

            const string fen = "r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1";
            Assert.That(UciProtocol.PositionCommand(Position.FromFen(fen)), Is.EqualTo("position fen " + fen));
            var castle = Notation.Parse(Position.FromFen(fen), "O-O");
            Assert.That(UciProtocol.PositionCommand(fen, new[] { castle }), Is.EqualTo("position fen " + fen + " moves e1g1"));
        }

        [Test]
        public void FormatsGoAndOptions()
        {
            Assert.That(UciProtocol.GoMoveTime(900), Is.EqualTo("go movetime 900"));
            Assert.That(UciProtocol.GoMoveTime(300, 2), Is.EqualTo("go depth 2 movetime 300"));
            Assert.That(UciProtocol.GoMoveTime(0), Is.EqualTo("go movetime 1"));
            Assert.That(UciProtocol.GoClock(180000, 175500, 2000, 2000), Is.EqualTo("go wtime 180000 btime 175500 winc 2000 binc 2000"));
            Assert.That(UciProtocol.GoClock(60000, 60000, 0, 0), Is.EqualTo("go wtime 60000 btime 60000"));
            Assert.That(UciProtocol.SetOption("Skill Level", 5), Is.EqualTo("setoption name Skill Level value 5"));
            Assert.That(UciProtocol.SetOption("Clear Hash", null), Is.EqualTo("setoption name Clear Hash"));
        }

        [Test]
        public void LevelsGrowStrongerAndKeepTheGameRhythm()
        {
            UciLevel prev = null;
            foreach (Difficulty d in Enum.GetValues(typeof(Difficulty)))
            {
                var l = UciProtocol.GetLevel(d);
                Assert.That(l.Level, Is.EqualTo(d));
                Assert.That(l.SkillLevel, Is.InRange(0, 20));
                Assert.That(l.MoveTimeMs, Is.EqualTo(ChessAI.GetSettings(d).MaxTimeMs), "même rythme que l'IA maison");
                if (prev != null) Assert.That(l.SkillLevel, Is.GreaterThan(prev.SkillLevel));
                prev = l;
            }
            Assert.That(UciProtocol.GetLevel(Difficulty.Maitre).SkillLevel, Is.EqualTo(20));
        }

        [TestCase("e2e4", true)]
        [TestCase("a7a8q", true)]
        [TestCase("a7a8k", false)]
        [TestCase("e9e4", false)]
        [TestCase("ponder", false)]
        public void RecognisesUciMoves(string s, bool ok) => Assert.That(UciProtocol.IsUciMove(s), Is.EqualTo(ok));
    }

    public class UciEngineTests
    {
        [Test]
        public void HandshakeSetsOptionsThenPlaysALegalMove()
        {
            var fake = new FakeUciEngine();
            var engine = new UciEngine(fake, hashMb: 16, threads: 1);
            var move = engine.FindMoveAsync(Position.Start(), new EngineOptions { Level = Difficulty.Moyen }, CancellationToken.None).Result;

            Assert.That(MoveGenerator.Legal(Position.Start()).Contains(move), Is.True);
            Assert.That(engine.Name, Is.EqualTo("FakeFish 1.0"));
            Assert.That(engine.Author, Is.EqualTo("Tests"));
            Assert.That(engine.Options, Does.Contain("Skill Level"));
            var c = fake.Commands();
            Assert.That(c, Is.EqualTo(new[]
            {
                "uci", "setoption name Threads value 1", "setoption name Hash value 16", "isready",
                "setoption name Skill Level value 8", "ucinewgame", "isready",
                "position startpos", "go movetime 900",
            }));
            Assert.That(engine.LastInfo.Depth, Is.EqualTo(1));
            Assert.That(engine.LastInfo.ScoreCp, Is.EqualTo(12));
            Assert.That(engine.LastSummary(), Does.StartWith("FakeFish 1.0 : profondeur 1, 12 cp"));
        }

        [Test]
        public void LevelAndTimeOverridesReachTheEngine()
        {
            var fake = new FakeUciEngine();
            var engine = new UciEngine(fake);
            engine.FindMove(Position.Start(), "position startpos", new EngineOptions { Level = Difficulty.Debutant }, CancellationToken.None);
            engine.FindMove(Position.Start(), "position startpos", new EngineOptions { Level = Difficulty.Debutant, MoveTimeMs = 120, MaxDepth = 4 }, CancellationToken.None);
            engine.FindMove(Position.Start(), "position startpos", new EngineOptions { Level = Difficulty.Maitre }, CancellationToken.None);
            var c = fake.Commands();
            Assert.That(c.Count(x => x == "setoption name Skill Level value 0"), Is.EqualTo(1), "option renvoyée seulement quand elle change");
            Assert.That(c, Does.Contain("go depth 1 movetime 300"));
            Assert.That(c, Does.Contain("go depth 4 movetime 120"));
            Assert.That(c, Does.Contain("setoption name Skill Level value 20"));
            Assert.That(c, Does.Contain("go movetime 3000"));
            Assert.That(c.Count(x => x == "ucinewgame"), Is.EqualTo(1));
        }

        [Test]
        public void NewGameSendsUciNewGameBeforeTheNextSearch()
        {
            var fake = new FakeUciEngine();
            var engine = new UciEngine(fake);
            engine.FindMove(Position.Start(), "position startpos", null, CancellationToken.None);
            engine.NewGame();
            engine.FindMove(Position.Start(), "position startpos", null, CancellationToken.None);
            Assert.That(fake.Commands().Count(x => x == "ucinewgame"), Is.EqualTo(2));
        }

        [Test]
        public void SendsTheGameHistory()
        {
            var fake = new FakeUciEngine();
            var engine = new UciEngine(fake);
            var game = new ChessGame();
            game.TryMakeMove(Notation.Parse(game.Position, "e4"));
            var move = engine.FindMoveAsync(game, new EngineOptions(), CancellationToken.None).Result;
            Assert.That(fake.Commands(), Does.Contain("position startpos moves e2e4"));
            Assert.That(game.IsLegal(move), Is.True, "coup des noirs, validé sur la position après e4");
        }

        [Test]
        public void ParsesPromotionAndCastlingFromTheEngine()
        {
            var fake = new FakeUciEngine { OnGo = (p, go) => new[] { "bestmove e1g1" } };
            var engine = new UciEngine(fake);
            var castle = engine.FindMove(Position.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1"), "position startpos", null, CancellationToken.None);
            Assert.That(castle.IsCastle, Is.True);

            fake.OnGo = (p, go) => new[] { "info depth 3 score mate 1 pv a7a8n", "bestmove a7a8n ponder h8g8" };
            var promo = engine.FindMove(Position.FromFen("7k/P7/8/8/8/8/8/K7 w - - 0 1"), "position startpos", null, CancellationToken.None);
            Assert.That(promo.Promotion, Is.EqualTo(PieceType.Knight));
            Assert.That(engine.LastBestMove.Ponder, Is.EqualTo("h8g8"));
            Assert.That(engine.LastInfo.MateIn, Is.EqualTo(1));
        }

        [Test]
        public void KeepsTheLastExactInfoIgnoringBoundsAndSecondaryLines()
        {
            var fake = new FakeUciEngine
            {
                OnGo = (p, go) => new[]
                {
                    "info string NNUE enabled",
                    "info depth 10 score cp 30 nodes 100 pv e2e4 e7e5",
                    "info depth 11 multipv 2 score cp 10 pv d2d4",
                    "info depth 11 score cp 90 lowerbound pv e2e4",
                    "info depth 11 currmove g1f3 currmovenumber 3",
                    "bestmove e2e4",
                },
            };
            var engine = new UciEngine(fake);
            engine.FindMove(Position.Start(), "position startpos", null, CancellationToken.None);
            Assert.That(engine.LastInfo.Depth, Is.EqualTo(10));
            Assert.That(engine.LastInfo.ScoreCp, Is.EqualTo(30));
        }

        [Test]
        public void NoneMeansGameOver()
        {
            var engine = new UciEngine(new FakeUciEngine());
            var mated = Position.FromFen("rnb1kbnr/pppp1ppp/8/4p3/6Pq/5P2/PPPPP2P/RNBQKBNR w KQkq - 1 3");
            Assert.That(engine.FindMove(mated, UciProtocol.PositionCommand(mated), null, CancellationToken.None).IsNull, Is.True);
        }

        [Test]
        public void IllegalBestMoveIsRejected()
        {
            var engine = new UciEngine(new FakeUciEngine { OnGo = (p, go) => new[] { "bestmove e2e5" } });
            Assert.Throws<InvalidDataException>(() => engine.FindMove(Position.Start(), "position startpos", null, CancellationToken.None));
            Assert.That(engine.IsBroken, Is.False, "un coup illégal n'empêche pas de réessayer");
        }

        [Test]
        public void CancellationSendsStopAndStaysInSync()
        {
            var fake = new FakeUciEngine { WaitForStop = true };
            var engine = new UciEngine(fake);
            using (var cts = new CancellationTokenSource(100))
            {
                var task = engine.FindMoveAsync(Position.Start(), new EngineOptions { MoveTimeMs = 10000 }, cts.Token);
                var ex = Assert.Throws<AggregateException>(() => task.Wait(5000));
                Assert.That(ex.InnerException, Is.InstanceOf<OperationCanceledException>());
            }
            Assert.That(fake.Commands().Last(), Is.EqualTo("stop"));
            // Le bestmove de la recherche annulée a été lu : la suivante ne le confond pas avec le sien.
            fake.WaitForStop = false;
            var pos = Position.FromFen("4k3/8/8/8/8/8/8/4K2R w K - 0 1");
            fake.OnGo = (p, go) => new[] { "bestmove h1h8" };
            Assert.That(engine.FindMove(pos, "position startpos", null, CancellationToken.None).ToUci(), Is.EqualTo("h1h8"));
        }

        [Test]
        public void EngineThatOverrunsItsTimeIsStopped()
        {
            var fake = new FakeUciEngine { WaitForStop = true };
            var engine = new UciEngine(fake) { GraceMs = 50 };
            var move = engine.FindMove(Position.Start(), "position startpos", new EngineOptions { MoveTimeMs = 50 }, CancellationToken.None);
            Assert.That(move.IsNull, Is.False);
            Assert.That(fake.Commands(), Does.Contain("stop"));
            Assert.That(engine.LastElapsedMs, Is.LessThan(2000));
        }

        [Test]
        public void FrozenEngineIsMarkedBroken()
        {
            var engine = new UciEngine(new FakeUciEngine { Frozen = true }) { HandshakeTimeoutMs = 100 };
            Assert.Throws<TimeoutException>(() => engine.Initialize());
            Assert.That(engine.IsBroken, Is.True);
            Assert.Throws<InvalidOperationException>(() => engine.FindMove(Position.Start(), "position startpos", null, CancellationToken.None));
        }

        [Test]
        public void EngineSilentAfterStopIsMarkedBroken()
        {
            var fake = new FakeUciEngine();
            var engine = new UciEngine(fake) { GraceMs = 20, StopTimeoutMs = 100 };
            engine.Initialize();
            fake.Frozen = true;
            Assert.Throws<TimeoutException>(() => engine.FindMove(Position.Start(), "position startpos", new EngineOptions { MoveTimeMs = 20 }, CancellationToken.None));
            Assert.That(engine.IsBroken, Is.True);
            Assert.That(fake.Disposed, Is.True, "un moteur hors service est fermé tout de suite (il ne servira plus)");
        }

        [Test]
        public void SilentHandshakeClosesTheTransport()
        {
            var fake = new FakeUciEngine { Frozen = true };
            var engine = new UciEngine(fake) { HandshakeTimeoutMs = 50 };
            Assert.Throws<TimeoutException>(() => engine.Initialize());
            Assert.That(fake.Disposed, Is.True);
        }

        /// <summary>
        /// Vrai processus figé en pleine recherche, sourd au « stop » : une fois le moteur déclaré hors service,
        /// le processus est tué au lieu de tourner (et de vider la batterie) jusqu'à la fermeture de l'app.
        /// </summary>
        [Test]
        public void StuckProcessIsKilledOnceBroken()
        {
            if (!File.Exists("/bin/sh")) Assert.Ignore("/bin/sh absent");
            string dir = Path.Combine(Path.GetTempPath(), "kevchess-stuck-uci-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string script = Path.Combine(dir, "engine.sh"), pidFile = Path.Combine(dir, "pid");
            File.WriteAllText(script,
                "echo $$ > \"" + pidFile + "\"\n" +
                "while read cmd rest; do\n" +
                "  case \"$cmd\" in\n" +
                "    uci) echo 'uciok';;\n" +
                "    isready) echo 'readyok';;\n" +
                "    go) while :; do :; done;;\n" +
                "  esac\n" +
                "done\n");
            int pid = -1;
            try
            {
                var engine = new UciEngine(new ProcessUciTransport("/bin/sh", "\"" + script + "\"")) { GraceMs = 20, StopTimeoutMs = 100 };
                engine.Initialize();
                pid = int.Parse(File.ReadAllText(pidFile).Trim());
                Assert.Throws<TimeoutException>(() => engine.FindMove(Position.Start(), "position startpos", new EngineOptions { MoveTimeMs = 20 }, CancellationToken.None));
                Assert.That(engine.IsBroken, Is.True);
                Assert.That(ProcessIsAlive(pid), Is.False, "le moteur figé doit être tué");
            }
            finally
            {
                if (pid > 0 && ProcessIsAlive(pid)) try { System.Diagnostics.Process.GetProcessById(pid).Kill(); } catch (Exception) { }
                Directory.Delete(dir, true);
            }
        }

        static bool ProcessIsAlive(int pid)
        {
            try
            {
                using (var p = System.Diagnostics.Process.GetProcessById(pid))
                {
                    if (p.HasExited) return false;
                    p.WaitForExit(1000); // laisse au kill le temps d'aboutir
                    return !p.HasExited;
                }
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        [Test]
        public void DisposeSendsQuit()
        {
            var fake = new FakeUciEngine();
            var engine = new UciEngine(fake);
            engine.Dispose();
            Assert.That(fake.Commands().Last(), Is.EqualTo("quit"));
            Assert.That(fake.Disposed, Is.True);
        }

        /// <summary>Le vrai transport par processus, avec un faux moteur écrit en shell (Mac / Linux).</summary>
        [Test]
        public void ProcessTransportTalksToAnExternalEngine()
        {
            if (!File.Exists("/bin/sh")) Assert.Ignore("/bin/sh absent");
            string script = Path.Combine(Path.GetTempPath(), "kevchess-fake-uci-" + Guid.NewGuid().ToString("N") + ".sh");
            File.WriteAllText(script,
                "while read cmd rest; do\n" +
                "  case \"$cmd\" in\n" +
                "    uci) echo 'id name ShellFish'; echo 'uciok';;\n" +
                "    isready) echo 'readyok';;\n" +
                "    go) echo 'info depth 1 score cp 5 pv e2e4'; echo 'bestmove e2e4';;\n" +
                "    quit) exit 0;;\n" +
                "  esac\n" +
                "done\n");
            try
            {
                using (var engine = new UciEngine(new ProcessUciTransport("/bin/sh", "\"" + script + "\"")))
                {
                    var move = engine.FindMoveAsync(Position.Start(), new EngineOptions(), CancellationToken.None).Result;
                    Assert.That(move.ToUci(), Is.EqualTo("e2e4"));
                    Assert.That(engine.Name, Is.EqualTo("ShellFish"));
                }
            }
            finally { File.Delete(script); }
        }
    }

    public class FallbackEngineTests
    {
        sealed class Scripted : IChessEngine
        {
            public Func<Position, CancellationToken, Move> Play;
            public int Calls;
            public Task<Move> FindMoveAsync(Position position, EngineOptions options, CancellationToken ct)
            {
                Calls++;
                return Task.Run(() => Play(position, ct));
            }
        }

        static readonly Move E4 = Notation.Parse(Position.Start(), "e4");
        static readonly Move D4 = Notation.Parse(Position.Start(), "d4");

        [Test]
        public void UsesThePrimaryWhenItWorks()
        {
            var main = new Scripted { Play = (p, ct) => E4 };
            var backup = new Scripted { Play = (p, ct) => D4 };
            var f = new FallbackEngine(() => main, backup);
            Assert.That(f.FindMoveAsync(Position.Start(), null, CancellationToken.None).Result, Is.EqualTo(E4));
            Assert.That(f.LastUsedFallback, Is.False);
            Assert.That(backup.Calls, Is.EqualTo(0));
        }

        [Test]
        public void FallsBackOnErrorMissingEngineOrNullMove()
        {
            var backup = new Scripted { Play = (p, ct) => D4 };
            var failing = new Scripted { Play = (p, ct) => throw new TimeoutException() };
            var f = new FallbackEngine(() => failing, backup);
            Assert.That(f.FindMoveAsync(Position.Start(), null, CancellationToken.None).Result, Is.EqualTo(D4));
            Assert.That(f.LastUsedFallback, Is.True);
            Assert.That(f.LastError, Is.InstanceOf<TimeoutException>());

            f = new FallbackEngine(() => null, backup);
            Assert.That(f.FindMoveAsync(Position.Start(), null, CancellationToken.None).Result, Is.EqualTo(D4));

            var empty = new Scripted { Play = (p, ct) => Move.Null };
            f = new FallbackEngine(() => empty, backup);
            Assert.That(f.FindMoveAsync(Position.Start(), null, CancellationToken.None).Result, Is.EqualTo(D4));
        }

        [Test]
        public void NullMoveIsAcceptedWhenTheGameIsOver()
        {
            var mated = Position.FromFen("rnb1kbnr/pppp1ppp/8/4p3/6Pq/5P2/PPPPP2P/RNBQKBNR w KQkq - 1 3");
            var backup = new Scripted { Play = (p, ct) => D4 };
            var f = new FallbackEngine(() => new Scripted { Play = (p, ct) => Move.Null }, backup);
            Assert.That(f.FindMoveAsync(mated, null, CancellationToken.None).Result.IsNull, Is.True);
            Assert.That(backup.Calls, Is.EqualTo(0));
        }

        [Test]
        public void CancellationIsNotSwallowed()
        {
            var backup = new Scripted { Play = (p, ct) => D4 };
            var slow = new Scripted { Play = (p, ct) => { Thread.Sleep(200); ct.ThrowIfCancellationRequested(); return E4; } };
            var f = new FallbackEngine(() => slow, backup);
            using (var cts = new CancellationTokenSource(50))
            {
                var ex = Assert.Throws<AggregateException>(() => f.FindMoveAsync(Position.Start(), null, cts.Token).Wait(5000));
                Assert.That(ex.InnerException, Is.InstanceOf<OperationCanceledException>());
            }
            Assert.That(backup.Calls, Is.EqualTo(0));
        }

        [Test]
        public void FallsBackToTheRealHouseAIWhenStockfishIsFrozen()
        {
            var frozen = new UciEngine(new FakeUciEngine { Frozen = true }) { HandshakeTimeoutMs = 50 };
            var f = new FallbackEngine(() => frozen.IsBroken ? null : frozen, new ChessAI(hashMb: 1, seed: 1));
            var move = f.FindMoveAsync(Position.Start(), new EngineOptions { Level = Difficulty.Debutant }, CancellationToken.None).Result;
            Assert.That(MoveGenerator.Legal(Position.Start()).Contains(move), Is.True);
            Assert.That(f.LastUsedFallback, Is.True);
            Assert.That(frozen.IsBroken, Is.True);
        }

        [TestCase(EngineKind.KevChess, false, EngineKind.KevChess)]
        [TestCase(EngineKind.KevChess, true, EngineKind.KevChess)]
        [TestCase(EngineKind.Stockfish, false, EngineKind.KevChess)]
        [TestCase(EngineKind.Stockfish, true, EngineKind.Stockfish)]
        public void ResolvesTheEngineChoice(EngineKind wanted, bool available, EngineKind expected) =>
            Assert.That(FallbackEngine.Resolve(wanted, available), Is.EqualTo(expected));

        [Test]
        public void HouseAIStaysTheDefault() => Assert.That(default(EngineKind), Is.EqualTo(EngineKind.KevChess));
    }
}
