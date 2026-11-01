using System;
using System.Collections.Generic;

namespace KevChess.Core
{
    /// <summary>
    /// Problème de mat. SolutionUci alterne coup du joueur et réponse adverse et se termine par le mat.
    /// Le camp au trait dans Fen est celui du joueur.
    /// </summary>
    public sealed class Puzzle
    {
        public string Id { get; }
        public string Title { get; }
        public string Fen { get; }
        public string[] SolutionUci { get; }
        public int MateIn { get; }

        public Puzzle(string id, string title, string fen, int mateIn, params string[] solutionUci)
        {
            Id = id;
            Title = title;
            Fen = fen;
            MateIn = mateIn;
            SolutionUci = solutionUci ?? Array.Empty<string>();
        }

        public override string ToString() => Id + " (" + Title + ")";
    }

    /// <summary>
    /// Problèmes intégrés, du plus simple au plus dur : mats en 1, puis en 2, puis en 3.
    /// Positions construites ou tirées de parties célèbres ; chaque coup clé est unique (vérifié par force brute).
    /// </summary>
    public static class PuzzleSet
    {
        static readonly Puzzle[] puzzles =
        {
            // ---------- Mats en 1 ----------
            new Puzzle("m1-01", "La tour au bout", "4k3/8/4K3/8/8/8/8/R7 w - - 0 1", 1, "a1a8"),
            new Puzzle("m1-02", "Dame et roi", "k7/8/1K6/8/8/8/7Q/8 w - - 0 1", 1, "h2h8"),
            new Puzzle("m1-03", "Le couloir", "6k1/5ppp/8/8/8/8/8/R5K1 w - - 0 1", 1, "a1a8"),
            new Puzzle("m1-04", "Les Noirs aussi", "r5k1/8/8/8/8/8/5PPP/6K1 b - - 0 1", 1, "a8a1"),
            new Puzzle("m1-05", "Promotion", "k7/2P5/1K6/8/8/8/8/8 w - - 0 1", 1, "c7c8q"),
            new Puzzle("m1-06", "Le coup du berger", "r1bqkb1r/pppp1ppp/2n2n2/4p2Q/2B1P3/8/PPPP1PPP/RNB1K1NR w KQkq - 4 4", 1, "h5f7"),
            new Puzzle("m1-07", "Le mat du sot", "rnbqkbnr/pppp1ppp/8/4p3/6P1/5P2/PPPPP2P/RNBQKBNR b KQkq - 0 2", 1, "d8h4"),
            new Puzzle("m1-08", "Dernière rangée", "2r3k1/5ppp/8/8/8/8/5PPP/2R3K1 w - - 0 1", 1, "c1c8"),
            new Puzzle("m1-09", "Prise sur la huitième", "3r2k1/5ppp/8/8/8/8/5PPP/3Q2K1 w - - 0 1", 1, "d1d8"),
            new Puzzle("m1-10", "Mat étouffé", "6rk/6pp/8/6N1/8/8/8/6K1 w - - 0 1", 1, "g5f7"),
            new Puzzle("m1-11", "Le fou en renfort", "6k1/5p1p/6pQ/8/8/8/1B6/7K w - - 0 1", 1, "h6g7"),
            new Puzzle("m1-12", "Mat arabe", "7k/R7/5N2/8/8/8/8/6K1 w - - 0 1", 1, "a7h7"),
            new Puzzle("m1-13", "Les épaulettes", "3rkr2/8/8/8/8/8/Q7/7K w - - 0 1", 1, "a2e6"),
            new Puzzle("m1-14", "Colonne h", "5r1k/6pp/8/7Q/2B5/7R/6PP/6K1 w - - 0 1", 1, "h5h7"),
            new Puzzle("m1-15", "Dame et cavalier", "r4rk1/5ppp/8/6N1/8/3Q4/5PPP/6K1 w - - 0 1", 1, "d3h7"),
            new Puzzle("m1-16", "Piège de Kieninger", "r1b1k2r/ppppqppp/2n5/4n3/1PP2B2/5N2/1P1NPPPP/R2QKB1R b KQkq - 0 8", 1, "e5d3"),
            new Puzzle("m1-17", "Gambit Blackburne", "r1b1kbnr/pppp1Npp/8/8/3nq3/8/PPPPBP1P/RNBQKR2 b Qkq - 1 7", 1, "d4f3"),
            new Puzzle("m1-18", "Chasse au roi (Lasker 1912)", "rn3r2/pbppq1p1/1p2pN2/8/3P2NP/6P1/PPP1BP1R/R3K1k1 w Q - 5 18", 1, "e1d2"),

            // ---------- Mats en 2 ----------
            new Puzzle("m2-01", "Le mat de Légal", "rn1qkbnr/ppp2p1p/3p2p1/4N3/2B1P3/2N5/PPPP1PPP/R1BbK2R w KQkq - 0 6", 2, "c4f7", "e8e7", "c3d5"),
            new Puzzle("m2-02", "Le mat de Lolli", "r5k1/5p1p/5Pp1/8/8/8/3Q2PP/5RK1 w - - 0 1", 2, "d2h6", "g6g5", "h6g7"),
            new Puzzle("m2-03", "Le legs de Philidor", "4r2k/6pp/7N/3Q4/8/8/6PP/6K1 w - - 0 1", 2, "d5g8", "e8g8", "h6f7"),
            new Puzzle("m2-04", "Le mat d'Anastasia", "r4r1k/4Nppp/8/7Q/8/3R4/5PPP/6K1 w - - 0 1", 2, "h5h7", "h8h7", "d3h3"),
            new Puzzle("m2-05", "Le mat de Boden", "2kr3r/pp1n1ppp/2n5/8/5B2/5Q2/PPP1BPPP/2KR3R w - - 0 1", 2, "f3c6", "b7c6", "e2a6"),
            new Puzzle("m2-06", "La partie de l'Opéra (Morphy 1858)", "4kb1r/p2n1ppp/4q3/4p1B1/4P3/1Q6/PPP2PPP/2KR4 w k - 0 16", 2, "b3b8", "d7b8", "d1d8"),
            new Puzzle("m2-07", "Réti contre Tartakower (1910)", "rnbk1b1r/pp3ppp/2p5/4q3/4n3/8/PPPB1PPP/2KR1BNR w - - 0 10", 2, "d2g5", "d8e8", "d1d8"),
            new Puzzle("m2-08", "L'Immortelle (Anderssen 1851)", "r1bk2nr/p2p1pNp/n2B4/1p1NP2P/6P1/3P1Q2/P1P1K3/q5b1 w - - 1 22", 2, "f3f6", "g8f6", "d6e7"),
            new Puzzle("m2-09", "La Toujours jeune (Anderssen 1852)", "1r2k1r1/pbp1np1p/1b3P2/5B2/8/B1P2q2/P4PPP/3R2K1 w - - 2 23", 2, "f5d7", "e8f8", "a3e7"),

            // ---------- Mats en 3 ----------
            new Puzzle("m3-01", "Anastasia en trois temps", "r4rk1/5ppp/8/3N3Q/8/3R4/5PPP/6K1 w - - 0 1", 3, "d5e7", "g8h8", "h5h7", "h8h7", "d3h3"),
            new Puzzle("m3-02", "Le legs de Philidor complet", "4r1k1/5Npp/8/3Q4/8/8/6PP/6K1 w - - 0 1", 3, "f7h6", "g8h8", "d5g8", "e8g8", "h6f7"),
            new Puzzle("m3-03", "Réti contre Tartakower : le sacrifice", "rnb1kb1r/pp3ppp/2p5/4q3/4n3/3Q4/PPPB1PPP/2KR1BNR w kq - 0 9", 3, "d3d8", "e8d8", "d2g5", "d8e8", "d1d8"),
            new Puzzle("m3-04", "L'Immortelle : le final", "r1b1k1nr/p2p1ppp/n2B4/1p1NPN1P/6P1/3P1Q2/P1P1K3/q5b1 w kq - 2 21", 3, "f5g7", "e8d8", "f3f6", "g8f6", "d6e7"),
            new Puzzle("m3-05", "La Toujours jeune : le final", "1r4r1/pbpknp1p/1b3P2/8/8/B1PB1q2/P4PPP/3R2K1 w - - 0 22", 3, "d3f5", "d7e8", "f5d7", "e8f8", "a3e7"),
        };

        /// <summary>Tous les problèmes, du plus simple au plus dur.</summary>
        public static IReadOnlyList<Puzzle> All => puzzles;

        public static Puzzle Find(string id)
        {
            foreach (var p in puzzles) if (p.Id == id) return p;
            return null;
        }
    }

    public enum PuzzleMoveResult { Correct, Wrong, Solved }

    /// <summary>
    /// Résolution d'un problème : le joueur propose ses coups, la réponse adverse est jouée automatiquement.
    /// Un mauvais coup n'est pas joué (la position reste inchangée, on peut réessayer).
    /// </summary>
    public sealed class PuzzleSession
    {
        int step;

        public PuzzleSession(Puzzle puzzle)
        {
            Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
            Game = new ChessGame(puzzle.Fen);
            PlayerColor = Game.SideToMove;
        }

        public Puzzle Puzzle { get; }
        public ChessGame Game { get; }
        public PieceColor PlayerColor { get; }
        public bool IsSolved { get; private set; }
        /// <summary>Nombre de coups refusés depuis le début.</summary>
        public int Mistakes { get; private set; }
        /// <summary>Index du prochain coup attendu dans SolutionUci.</summary>
        public int Step => step;

        /// <summary>Prochain coup attendu du joueur (pour un indice), Move.Null si résolu.</summary>
        public Move ExpectedMove =>
            IsSolved || step >= Puzzle.SolutionUci.Length ? Move.Null : Notation.Parse(Game.Position, Puzzle.SolutionUci[step]);

        /// <summary>
        /// Propose un coup du joueur. Correct : il est joué, ainsi que la réponse adverse (renvoyée dans reply).
        /// Solved : coup final de la solution, ou tout autre coup qui mate immédiatement. Wrong : rien n'est joué.
        /// </summary>
        public PuzzleMoveResult TryMove(Move move, out Move reply)
        {
            reply = Move.Null;
            if (IsSolved || Game.IsOver || Game.SideToMove != PlayerColor) return PuzzleMoveResult.Wrong;

            Move legal = Move.Null;
            foreach (var m in Game.LegalMoves)
                if (m.SameAs(move.From, move.To, move.Promotion)) { legal = m; break; }
            if (legal.IsNull) { Mistakes++; return PuzzleMoveResult.Wrong; }

            bool expected = step < Puzzle.SolutionUci.Length && legal.ToUci() == Puzzle.SolutionUci[step];
            if (!expected)
            {
                Game.TryMakeMove(legal);
                if (Game.EndReason == GameEndReason.Checkmate) { IsSolved = true; return PuzzleMoveResult.Solved; }
                Game.Undo();
                Mistakes++;
                return PuzzleMoveResult.Wrong;
            }

            Game.TryMakeMove(legal);
            step++;
            if (Game.EndReason == GameEndReason.Checkmate || step >= Puzzle.SolutionUci.Length)
            {
                IsSolved = true;
                return PuzzleMoveResult.Solved;
            }

            var answer = Notation.Parse(Game.Position, Puzzle.SolutionUci[step]);
            if (!answer.IsNull && Game.TryMakeMove(answer))
            {
                reply = answer;
                step++;
            }
            return PuzzleMoveResult.Correct;
        }
    }
}
