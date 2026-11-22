using System.Linq;
using KevChess.Core;
using KevChess.Core.Fantasy;
using NUnit.Framework;

namespace KevChess.Tests
{
    /// <summary>Règles du mode Légendes (docs/design/mode-fantastique.md, § 2).</summary>
    public class FantasyRulesTests
    {
        const string Kiwipete = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
        const string Position3 = "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1";
        const string Position4 = "r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1";
        const string Position5 = "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8";
        const string Position6 = "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10";

        static int Sq(string name) => Square.Parse(name);
        static int W(PieceType t) => Piece.Make(PieceColor.White, t);
        static int B(PieceType t) => Piece.Make(PieceColor.Black, t);

        static FantasyConfig NoEvents()
        {
            var c = FantasyConfig.Default;
            c.EventsEnabled = false;
            return c;
        }

        static FantasyPosition Pos(string fen, FantasyConfig cfg = null) => FantasyPosition.FromFen(fen, cfg ?? NoEvents());

        static System.Collections.Generic.List<FantasyAction> Legal(FantasyPosition p) => FantasyMoveGenerator.Legal(p);

        static bool Has(FantasyPosition p, System.Func<FantasyAction, bool> pred) => Legal(p).Any(pred);

        // ---------- équivalence avec les échecs classiques ----------

        [TestCase(Position.StartFen, 4)]
        [TestCase(Kiwipete, 3)]
        [TestCase(Position3, 4)]
        [TestCase(Position4, 3)]
        [TestCase(Position5, 3)]
        [TestCase(Position6, 3)]
        public void PerftEqualsClassicWithoutEnergy(string fen, int depth)
        {
            var classic = Position.FromFen(fen);
            var fantasy = FantasyPosition.FromFen(fen, FantasyConfig.Classic);
            ulong hash = fantasy.Hash;
            Assert.That(FantasyMoveGenerator.Perft(fantasy, depth), Is.EqualTo(MoveGenerator.Perft(classic, depth)));
            Assert.That(fantasy.Hash, Is.EqualTo(hash));
            Assert.That(fantasy.ToFen(), Is.EqualTo(Position.FromFen(fen).ToFen()));
        }

        [Test]
        public void MakeUnmakeRestoresEverythingWithPowers()
        {
            var p = Pos(Kiwipete);
            p.SetEnergy(PieceColor.White, 10);
            p.SetEnergy(PieceColor.Black, 10);
            p.AddFissure(Sq("d4"), 2);
            p.SetPortals(Sq("c3") - 8 + 16, 63 - (Sq("c3") + 8), PortalState.Open, 5);
            ulong hash = p.Hash;
            string fen = p.ToFen();
            long n = FantasyMoveGenerator.Perft(p, 2);
            Assert.That(n, Is.GreaterThan(48L * 40));
            Assert.That(p.Hash, Is.EqualTo(hash));
            Assert.That(p.ToFen(), Is.EqualTo(fen));
            Assert.That(p.Energy(PieceColor.White), Is.EqualTo(10));
        }

        // ---------- énergie ----------

        [Test]
        public void StartEnergyAndCaptureGains()
        {
            var game = new FantasyGame(1, NoEvents(), "4k3/8/8/3q4/4P3/8/8/4K3 w - - 0 1");
            Assert.That(game.Position.Energy(PieceColor.White), Is.EqualTo(1));
            Assert.That(game.TryMakeAction(new FantasyAction(Sq("e4"), Sq("d5"), MoveFlags.Capture)), Is.True);
            Assert.That(game.Position.Energy(PieceColor.White), Is.EqualTo(1 + 3));
            Assert.That(game.LastReport.CapturedPiece, Is.EqualTo(B(PieceType.Queen)));
        }

        [Test]
        public void CenterBonusIgnoresPawns()
        {
            var pawns = new FantasyGame(1, NoEvents(), "4k3/8/8/8/3P4/8/4P3/4K3 w - - 0 1");
            pawns.TryMakeAction(new FantasyAction(Sq("e2"), Sq("e4"), MoveFlags.DoublePush));
            Assert.That(pawns.Position.Energy(PieceColor.White), Is.EqualTo(1), "deux pions au centre : pas de bonus");

            var pieces = new FantasyGame(1, NoEvents(), "4k3/8/8/8/3N4/8/4B3/4K3 w - - 0 1");
            Assert.That(pieces.TryMakeAction(new FantasyAction(Sq("e2"), Sq("f3"))), Is.True);
            Assert.That(pieces.Position.Energy(PieceColor.White), Is.EqualTo(1));
            var game = new FantasyGame(1, NoEvents(), "4k3/8/8/8/3N4/6B1/8/4K3 w - - 0 1");
            Assert.That(game.TryMakeAction(new FantasyAction(Sq("g3"), Sq("e5"))), Is.True);
            Assert.That(game.Position.Energy(PieceColor.White), Is.EqualTo(2), "cavalier d4 + fou e5");
        }

        [Test]
        public void EnergyIsCapped()
        {
            var p = Pos("4k3/8/8/3q4/4P3/8/8/4K3 w - - 0 1");
            p.SetEnergy(PieceColor.White, 9);
            p.MakeAction(new FantasyAction(Sq("e4"), Sq("d5"), MoveFlags.Capture));
            Assert.That(p.Energy(PieceColor.White), Is.EqualTo(10));
        }

        // ---------- Bouclier ----------

        [Test]
        public void ShieldBlocksCaptureUntilOwnerNextTurn()
        {
            var game = new FantasyGame(1, NoEvents(), "4k3/8/8/3p4/4P3/8/8/4K3 b - - 0 1");
            game.Position.SetEnergy(PieceColor.Black, 2);
            game.RefreshAfterSetup();
            var shieldThenKing = new FantasyAction(Sq("e8"), Sq("f8")).WithPower(PowerKind.Shield, Sq("d5"));
            Assert.That(game.TryMakeAction(shieldThenKing), Is.True);
            Assert.That(game.Position.Energy(PieceColor.Black), Is.EqualTo(0));
            Assert.That(game.LegalActions.Any(a => a.To == Sq("d5") && a.IsCapture), Is.False, "pion protégé");
            game.TryMakeAction(new FantasyAction(Sq("e1"), Sq("e2")));
            Assert.That(game.Position.ShieldSquare(PieceColor.Black), Is.EqualTo(Square.None), "le bouclier tombe au début du tour suivant");
        }

        [Test]
        public void ShieldCannotTargetKingOrFrozenPiece()
        {
            var p = Pos("4k3/8/8/8/8/8/3N4/4K3 w - - 0 1");
            p.SetEnergy(PieceColor.White, 5);
            p.SetFrozen(PieceColor.White, Sq("d2"));
            var legal = Legal(p);
            Assert.That(legal.Any(a => a.Power == PowerKind.Shield), Is.False);
        }

        // ---------- Gel ----------

        [Test]
        public void FrozenPieceCannotMoveButStillGivesCheck()
        {
            // Tour noire gelée en e5 : elle ne bouge pas mais le roi blanc ne peut pas aller sur la colonne e.
            var p = Pos("4k3/8/8/4r3/8/8/8/3K4 b - - 0 1");
            p.SetEnergy(PieceColor.White, 3);
            p.MakeAction(new FantasyAction(Sq("e8"), Sq("f8")));
            // Blancs : Gel sur e5 puis coup du roi.
            var legal = Legal(p);
            Assert.That(legal.Any(a => a.Power == PowerKind.Freeze && a.PowerSquare == Sq("e5") && a.To == Sq("e1")), Is.False,
                "la tour gelée contrôle toujours la colonne e");
            var freeze = legal.First(a => a.Power == PowerKind.Freeze && a.PowerSquare == Sq("e5") && a.To == Sq("c1"));
            p.MakeAction(freeze);
            Assert.That(p.FrozenSquare(PieceColor.Black), Is.EqualTo(Sq("e5")));
            Assert.That(Legal(p).Any(a => a.From == Sq("e5")), Is.False, "la tour gelée ne bouge pas");
            p.MakeAction(new FantasyAction(Sq("f8"), Sq("g8")));
            Assert.That(p.FrozenSquare(PieceColor.Black), Is.EqualTo(Square.None), "dégel à la fin du tour gelé");
        }

        [Test]
        public void FreezeDoesNotRemoveCheck()
        {
            var p = Pos("4k3/8/8/8/8/8/8/r3K3 w - - 0 1");
            p.SetEnergy(PieceColor.White, 3);
            foreach (var a in Legal(p))
            {
                p.MakeAction(a);
                Assert.That(p.IsInCheck(PieceColor.White), Is.False);
                p.UnmakeAction();
            }
            Assert.That(Legal(p).Any(a => a.Power == PowerKind.Freeze && a.From == Sq("e1") && Square.Rank(a.To) == 0), Is.False);
        }

        [TestCase("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1", PieceColor.White, "h1", "g1", "c1")]
        [TestCase("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1", PieceColor.White, "a1", "c1", "g1")]
        [TestCase("r3k2r/8/8/8/8/8/8/4K3 b kq - 0 1", PieceColor.Black, "h8", "g8", "c8")]
        [TestCase("r3k2r/8/8/8/8/8/8/4K3 b kq - 0 1", PieceColor.Black, "a8", "c8", "g8")]
        public void FrozenRookCannotCastle(string fen, PieceColor side, string frozenRook, string blocked, string stillAllowed)
        {
            // § 2.3 : la pièce gelée ne bouge pas pendant le tour ; le roque déplace la tour.
            var p = Pos(fen);
            p.SetFrozen(side, Sq(frozenRook));
            var castles = Legal(p).Where(a => a.IsCastle).Select(a => a.To).ToList();
            Assert.That(castles, Does.Not.Contain(Sq(blocked)), "roque avec la tour gelée");
            Assert.That(castles, Does.Contain(Sq(stillAllowed)), "l'autre roque reste permis");
            Assert.That(p.Castling & (side == PieceColor.White ? CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide
                : CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide), Is.Not.EqualTo(CastlingRights.None),
                "le gel ne retire pas le droit de roque, il le suspend le temps du tour");
        }

        // ---------- Téléportation ----------

        [Test]
        public void TeleportToAdjacentEmptySquareNoCaptureNoPromotion()
        {
            var p = Pos("4k3/1P6/8/8/3N4/2p5/8/4K3 w - - 0 1");
            p.SetEnergy(PieceColor.White, 3);
            var tele = Legal(p).Where(a => a.Kind == ActionKind.Teleport).ToList();
            Assert.That(tele.Where(a => a.From == Sq("d4")).Select(a => a.To),
                Is.EquivalentTo(new[] { "c4", "e4", "c5", "d5", "e5", "d3", "e3" }.Select(Sq)), "c3 est occupée");
            Assert.That(tele.Any(a => a.From == Sq("b7") && Square.Rank(a.To) == 7), Is.False, "pas de promotion par téléportation");
            Assert.That(tele.Any(a => a.From == Sq("e1")), Is.False, "le roi ne se téléporte pas");
            Assert.That(tele.Any(a => a.HasPower), Is.False);
            p.MakeAction(tele.First(a => a.From == Sq("d4") && a.To == Sq("e5")));
            Assert.That(p.Energy(PieceColor.White), Is.EqualTo(0));
            Assert.That(p[Sq("e5")], Is.EqualTo(W(PieceType.Knight)));
        }

        // ---------- Dragon ----------

        [Test]
        public void SummonCostIsFiveSince20261121()
        {
            // Équilibrage du 2026-11-21 (auto-parties, docs/design/mode-fantastique.md § 6) : 10 → 5.
            Assert.That(FantasyConfig.Default.SummonCost, Is.EqualTo(5));
            Assert.That(FantasyConfig.Default.SummonCost, Is.LessThanOrEqualTo(FantasyConfig.Default.MaxEnergy));
        }

        [Test]
        public void SummonTurnsRookIntoDragonThenMoves()
        {
            var p = Pos("4k3/8/8/8/8/8/8/R3K3 w Q - 0 1");
            int cost = p.Config.SummonCost;
            p.SetEnergy(PieceColor.White, cost - 1);
            Assert.That(Legal(p).Any(a => a.Power == PowerKind.Summon), Is.False, "un éclat de moins que le coût");
            p.SetEnergy(PieceColor.White, cost + 2);
            var summons = Legal(p).Where(a => a.Power == PowerKind.Summon).ToList();
            Assert.That(summons, Is.Not.Empty);
            var fly = summons.First(a => a.From == Sq("a1") && a.To == Sq("d4"));
            p.MakeAction(fly);
            Assert.That(p[Sq("d4")], Is.EqualTo(W(FantasyPiece.Dragon)));
            Assert.That(p.Energy(PieceColor.White), Is.EqualTo(2), "le coût est payé, le reste est gardé");
            Assert.That(p.HasSummoned(PieceColor.White), Is.True);
            Assert.That(p.Castling, Is.EqualTo(CastlingRights.None));
            // Une seule invocation par partie.
            p.SetEnergy(PieceColor.White, 10);
            p.SetPiece(Sq("h1"), W(PieceType.Rook));
            p.MakeAction(new FantasyAction(Sq("e8"), Sq("f8")));
            Assert.That(Legal(p).Any(a => a.Power == PowerKind.Summon), Is.False);
        }

        [Test]
        public void DragonFliesOverPiecesAndGivesCheckThroughThem()
        {
            var p = Pos("8/8/8/4k3/4p3/4P3/4D3/K7 w - - 0 1");
            var moves = Legal(p).Where(a => a.From == Sq("e2") && a.Kind == ActionKind.Normal).Select(a => a.To).ToList();
            Assert.That(moves, Does.Contain(Sq("e4")), "prise en volant par-dessus e3");
            Assert.That(moves, Does.Not.Contain(Sq("e3")), "pièce amie");
            Assert.That(moves, Does.Not.Contain(Sq("e6")), "portée 3");
            Assert.That(p.IsSquareAttacked(Sq("e5"), PieceColor.White), Is.True, "le dragon contrôle e5 à travers e3 et e4");
            Assert.That(p.IsInCheck(PieceColor.Black), Is.True);
        }

        [Test]
        public void BreathBurnsFirstEnemyPieceThenCoolsDown()
        {
            var p = Pos("4k3/8/8/2n5/8/8/8/D3K3 w - - 0 1");
            var breath = Legal(p).Where(a => a.Kind == ActionKind.Breath).ToList();
            Assert.That(breath, Is.Empty, "c5 n'est sur aucune ligne du dragon");
            p.SetPiece(Sq("c5"), Piece.Empty);
            p.SetPiece(Sq("c3"), B(PieceType.Knight));
            p.SetPiece(Sq("a3"), B(PieceType.Pawn));
            p.SetPiece(Sq("a2"), W(PieceType.Pawn));
            breath = Legal(p).Where(a => a.Kind == ActionKind.Breath).ToList();
            Assert.That(breath.Select(a => a.To), Is.EquivalentTo(new[] { Sq("c3") }), "a3 est derrière le pion blanc a2");
            p.MakeAction(breath[0]);
            Assert.That(p[Sq("c3")], Is.EqualTo(Piece.Empty));
            Assert.That(p[Sq("a1")], Is.EqualTo(W(FantasyPiece.Dragon)));
            Assert.That(p.Energy(PieceColor.White), Is.EqualTo(1 + 2));
            p.SetPiece(Sq("b2"), B(PieceType.Knight));
            p.MakeAction(new FantasyAction(Sq("e8"), Sq("f8")));
            Assert.That(Legal(p).Any(a => a.Kind == ActionKind.Breath), Is.False, "recharge 1");
            p.MakeAction(new FantasyAction(Sq("e1"), Sq("f1")));
            p.MakeAction(new FantasyAction(Sq("f8"), Sq("e8")));
            Assert.That(Legal(p).Any(a => a.Kind == ActionKind.Breath), Is.False, "recharge 2");
            p.MakeAction(new FantasyAction(Sq("f1"), Sq("e1")));
            p.MakeAction(new FantasyAction(Sq("e8"), Sq("f8")));
            Assert.That(Legal(p).Any(a => a.Kind == ActionKind.Breath), Is.True, "disponible au 3e tour");
        }

        [Test]
        public void BreathCannotHitKingOrShieldedPiece()
        {
            var p = Pos("8/8/8/8/8/2k5/1n6/D3K3 w - - 0 1");
            p.SetShield(PieceColor.Black, Sq("b2"));
            Assert.That(Legal(p).Any(a => a.Kind == ActionKind.Breath), Is.False);
        }

        // ---------- plateau vivant ----------

        [Test]
        public void FissureCollapsesAndPieceFalls()
        {
            var p = Pos("4k3/8/8/8/3N4/8/8/4K3 w - - 0 1");
            p.AddFissure(Sq("d4"), 2);
            var report = new ActionReport();
            p.Report = report;
            p.MakeAction(new FantasyAction(Sq("e1"), Sq("e2")));
            Assert.That(p.IsFissure(Sq("d4")), Is.True);
            p.MakeAction(new FantasyAction(Sq("e8"), Sq("e7")));
            Assert.That(p[Sq("d4")], Is.EqualTo(Piece.Empty), "le cavalier tombe");
            Assert.That(report.FallenPieces, Is.EqualTo(new[] { W(PieceType.Knight) }));
            Assert.That(p.IsChasm(Sq("d4")), Is.True);
            Assert.That(p.Energy(PieceColor.Black), Is.EqualTo(1), "une chute ne rapporte rien");
        }

        [Test]
        public void NobodyStopsOnChasmButSlidersPassOver()
        {
            var p = Pos("4k3/8/8/8/8/8/8/R3K3 w - - 0 1");
            p.AddChasm(Sq("a4"), 6);
            var rook = Legal(p).Where(a => a.From == Sq("a1")).Select(a => a.To).ToList();
            Assert.That(rook, Does.Not.Contain(Sq("a4")));
            Assert.That(rook, Does.Contain(Sq("a6")));
        }

        [Test]
        public void EnPassantNeverLandsOnChasm()
        {
            // Le pion blanc survole le gouffre e3 (permis) ; le pion noir de d4 ne peut pas prendre en passant
            // en s'arrêtant sur e3. Sans gouffre, la prise reste possible.
            foreach (bool chasm in new[] { true, false })
            {
                var p = Pos("4k3/8/8/8/3p4/8/4P3/4K3 w - - 0 1");
                if (chasm) p.AddChasm(Sq("e3"), 6);
                Assert.That(p.MakeAction(new FantasyAction(Sq("e2"), Sq("e4"), MoveFlags.DoublePush)), Is.True);
                Assert.That(Legal(p).Any(a => a.IsEnPassant), Is.EqualTo(!chasm), chasm ? "en passant sur un gouffre" : "en passant normal");
            }
        }

        [Test]
        public void KingNeverStopsOnFissure()
        {
            var p = Pos("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
            p.AddFissure(Sq("e2"), 4);
            Assert.That(Legal(p).Any(a => a.To == Sq("e2")), Is.False);
        }

        [Test]
        public void FallIsPostponedWhenItWouldExposeTheMover()
        {
            // Le cavalier noir en e4 masque le roi blanc de la tour noire e8.
            var p = Pos("4r1k1/8/8/8/4n3/8/8/4K3 w - - 0 1");
            p.AddFissure(Sq("e4"), 1);
            // Les blancs jouent ; au début du tour noir la chute découvrirait un échec sur les blancs : report.
            p.MakeAction(new FantasyAction(Sq("e1"), Sq("e2")));
            Assert.That(p[Sq("e4")], Is.EqualTo(B(PieceType.Knight)), "chute reportée");
            Assert.That(p.IsFissure(Sq("e4")), Is.True);
            // Les noirs jouent ailleurs : au début du tour blanc la chute peut se faire (échec aux blancs, qui sont au trait).
            p.MakeAction(new FantasyAction(Sq("g8"), Sq("h8")));
            Assert.That(p[Sq("e4")], Is.EqualTo(Piece.Empty));
            Assert.That(p.InCheck, Is.True);
        }

        [Test]
        public void PortalTransportsPieceToFreeExit()
        {
            var p = Pos("4k3/8/8/8/8/8/8/R3K3 w - - 0 1");
            int a = Sq("a3"), b = 63 - Sq("a3");
            p.SetPortals(a, b, PortalState.Open, 8);
            var report = new ActionReport();
            p.Report = report;
            p.MakeAction(new FantasyAction(Sq("a1"), Sq("a3")));
            Assert.That(p[b], Is.EqualTo(W(PieceType.Rook)));
            Assert.That(p[a], Is.EqualTo(Piece.Empty));
            Assert.That(report.PortalTo, Is.EqualTo(b));
            Assert.That(Square.Name(b), Is.EqualTo("h6"));
        }

        [Test]
        public void CrystalGivesEnergy()
        {
            var p = Pos("4k3/8/8/8/8/8/8/R3K3 w - - 0 1");
            p.SetCrystal(Sq("a5"));
            p.MakeAction(new FantasyAction(Sq("a1"), Sq("a5")));
            Assert.That(p.Energy(PieceColor.White), Is.EqualTo(3));
            Assert.That(p.CrystalSquare, Is.EqualTo(Square.None));
        }

        [Test]
        public void EventsAreDeterministicFromSeed()
        {
            string Play(ulong seed)
            {
                var game = new FantasyGame(seed);
                var rng = new System.Random(7);
                var log = new System.Text.StringBuilder();
                for (int i = 0; i < 40 && !game.IsOver; i++)
                {
                    var legal = game.LegalActions;
                    game.TryMakeAction(legal[rng.Next(legal.Count)]);
                    if (!game.LastEvent.IsNone) log.Append(game.LastEvent.Kind).Append(game.LastEvent.SquareA).Append(';');
                }
                return log.ToString();
            }
            string first = Play(42);
            Assert.That(first, Is.Not.Empty, "des événements apparaissent");
            Assert.That(Play(42), Is.EqualTo(first));
        }

        // ---------- fin de partie, sauvegarde ----------

        [Test]
        public void CheckmateStillExistsWithEnergy()
        {
            // Mat du couloir : les pions ne peuvent pas se téléporter sur la 1re rangée.
            var game = new FantasyGame(1, NoEvents(), "6k1/5ppp/8/8/8/8/5PPP/r5K1 w - - 0 1");
            Assert.That(game.IsOver, Is.True);
            Assert.That(game.EndReason, Is.EqualTo(GameEndReason.Checkmate));
        }

        [Test]
        public void TeleportCanParryCheck()
        {
            var p = Pos("6k1/8/8/8/8/8/5PPP/r3N1K1 w - - 0 1");
            // Roi g1 en échec par la tour a1 : le fou e2 peut s'interposer, en jouant ou en se téléportant.
            p.SetPiece(Sq("e1"), Piece.Empty);
            p.SetPiece(Sq("e2"), W(PieceType.Bishop));
            p.SetEnergy(PieceColor.White, 3);
            var legal = Legal(p);
            Assert.That(legal.All(a => a.Kind == ActionKind.Teleport || a.From == Sq("e2")), Is.True);
            Assert.That(legal.Any(a => a.Kind == ActionKind.Teleport && a.From == Sq("e2") && a.To == Sq("f1")), Is.True);
        }

        [Test]
        public void SaveAndLoadReplaysTheSameGame()
        {
            var game = new FantasyGame(1234);
            var rng = new System.Random(3);
            for (int i = 0; i < 30 && !game.IsOver; i++)
                game.TryMakeAction(game.LegalActions[rng.Next(game.LegalActions.Count)]);
            var loaded = FantasyGame.Load(game.Save());
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.Position.Hash, Is.EqualTo(game.Position.Hash));
            Assert.That(loaded.Notations, Is.EqualTo(game.Notations));
        }

        [Test]
        public void UndoRestoresPositionAndEvents()
        {
            var game = new FantasyGame(99);
            var rng = new System.Random(11);
            var hashes = new System.Collections.Generic.List<ulong> { game.Position.Hash };
            for (int i = 0; i < 24 && !game.IsOver; i++)
            {
                game.TryMakeAction(game.LegalActions[rng.Next(game.LegalActions.Count)]);
                hashes.Add(game.Position.Hash);
            }
            for (int i = hashes.Count - 1; i > 0; i--)
            {
                Assert.That(game.Position.Hash, Is.EqualTo(hashes[i]));
                game.Undo();
            }
            Assert.That(game.Position.Hash, Is.EqualTo(hashes[0]));
        }

        [Test]
        public void NotationOfPowersTeleportAndBreath()
        {
            var p = Pos("4k3/8/8/8/3N4/8/8/D3K3 w - - 0 1");
            p.SetEnergy(PieceColor.White, 3);
            p.SetPiece(Sq("c3"), B(PieceType.Pawn));
            var legal = Legal(p);
            Assert.That(FantasyNotation.ToText(p, new FantasyAction(Sq("d4"), Sq("e5"), kind: ActionKind.Teleport), legal), Is.EqualTo("Nd4~e5"));
            Assert.That(FantasyNotation.ToText(p, new FantasyAction(Sq("a1"), Sq("c3"), kind: ActionKind.Breath), legal), Is.EqualTo("Da1^c3"));
            var shield = new FantasyAction(Sq("e1"), Sq("f2")).WithPower(PowerKind.Shield, Sq("d4"));
            Assert.That(FantasyNotation.ToText(p, shield, legal), Is.EqualTo("[S d4] Kf2"));
        }
    }
}
