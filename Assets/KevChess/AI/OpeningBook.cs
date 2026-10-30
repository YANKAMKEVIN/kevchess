using System;
using System.Collections.Generic;
using KevChess.Core;

namespace KevChess.AI
{
    /// <summary>
    /// Petit livre d'ouvertures maison : des lignes SAN classiques, indexées par hachage de position.
    /// Le poids d'un coup = nombre de lignes qui le jouent dans cette position.
    /// Construit paresseusement une seule fois (thread-safe), puis en lecture seule.
    /// </summary>
    public static class OpeningBook
    {
        /// <summary>Lignes du livre (SAN, coups séparés par des espaces, depuis la position initiale).</summary>
        public static readonly string[] Lines =
        {
            // 1.e4 e5
            "e4 e5 Nf3 Nc6 Bb5 a6 Ba4 Nf6 O-O Be7 Re1 b5 Bb3 d6",
            "e4 e5 Nf3 Nc6 Bb5 Nf6 O-O Nxe4 d4 Nd6 Bxc6 dxc6",
            "e4 e5 Nf3 Nc6 Bb5 a6 Bxc6 dxc6 O-O f6",
            "e4 e5 Nf3 Nc6 Bc4 Bc5 c3 Nf6 d3 d6 O-O O-O",
            "e4 e5 Nf3 Nc6 Bc4 Nf6 d3 Be7 O-O O-O Re1 d6",
            "e4 e5 Nf3 Nc6 d4 exd4 Nxd4 Nf6 Nxc6 bxc6 e5 Qe7",
            "e4 e5 Nf3 Nc6 d4 exd4 Nxd4 Bc5 Be3 Qf6",
            "e4 e5 Nf3 Nc6 Nc3 Nf6 d4 exd4 Nxd4 Bb4",
            "e4 e5 Nf3 Nf6 Nxe5 d6 Nf3 Nxe4 d4 d5 Bd3 Nc6",
            "e4 e5 Nf3 d6 d4 Nf6 Nc3 Nbd7 Bc4 Be7",
            "e4 e5 Nc3 Nf6 Bc4 Nc6 d3 Bb4 Nf3 d5",
            // Sicilienne
            "e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 a6 Be3 e5",
            "e4 c5 Nf3 d6 d4 cxd4 Nxd4 Nf6 Nc3 g6 Be3 Bg7",
            "e4 c5 Nf3 Nc6 d4 cxd4 Nxd4 Nf6 Nc3 e5 Ndb5 d6",
            "e4 c5 Nf3 e6 d4 cxd4 Nxd4 Nc6 Nc3 Qc7 Be3 a6",
            "e4 c5 Nf3 e6 d4 cxd4 Nxd4 a6 Bd3 Nf6 O-O d6",
            "e4 c5 Nc3 Nc6 g3 g6 Bg2 Bg7 d3 d6",
            "e4 c5 c3 Nf6 e5 Nd5 d4 cxd4 Nf3 Nc6",
            "e4 c5 Nf3 Nc6 Bb5 g6 O-O Bg7 Re1 Nf6",
            // Française, Caro-Kann, Scandinave, Pirc, Alekhine, Moderne
            "e4 e6 d4 d5 Nc3 Nf6 Bg5 Be7 e5 Nfd7 Bxe7 Qxe7",
            "e4 e6 d4 d5 Nc3 Bb4 e5 c5 a3 Bxc3+ bxc3 Ne7",
            "e4 e6 d4 d5 e5 c5 c3 Nc6 Nf3 Qb6 a3 c4",
            "e4 e6 d4 d5 Nd2 Nf6 e5 Nfd7 Bd3 c5 c3 Nc6",
            "e4 c6 d4 d5 Nc3 dxe4 Nxe4 Bf5 Ng3 Bg6 h4 h6",
            "e4 c6 d4 d5 e5 Bf5 Nf3 e6 Be2 c5 O-O Nc6",
            "e4 c6 d4 d5 exd5 cxd5 Bd3 Nc6 c3 Nf6",
            "e4 d5 exd5 Qxd5 Nc3 Qa5 d4 Nf6 Nf3 c6",
            "e4 d6 d4 Nf6 Nc3 g6 Nf3 Bg7 Be2 O-O O-O c6",
            "e4 Nf6 e5 Nd5 d4 d6 Nf3 Bg4 Be2 e6",
            "e4 g6 d4 Bg7 Nc3 d6 Be3 a6 Qd2 Nd7",
            // 1.d4 d5
            "d4 d5 c4 e6 Nc3 Nf6 Bg5 Be7 e3 O-O Nf3 h6",
            "d4 d5 c4 e6 Nf3 Nf6 g3 Be7 Bg2 O-O O-O dxc4",
            "d4 d5 c4 e6 Nc3 c6 Nf3 Nf6 e3 Nbd7 Bd3 dxc4",
            "d4 d5 c4 c6 Nf3 Nf6 Nc3 dxc4 a4 Bf5 e3 e6",
            "d4 d5 c4 c6 Nc3 Nf6 e3 e6 Nf3 a6",
            "d4 d5 c4 dxc4 Nf3 Nf6 e3 e6 Bxc4 c5 O-O a6",
            "d4 d5 Nf3 Nf6 Bf4 e6 e3 c5 c3 Nc6 Nbd2 Bd6",
            "d4 d5 Bf4 Nf6 e3 c5 c3 Nc6 Nd2 e6 Ngf3 Bd6",
            // 1.d4 Nf6
            "d4 Nf6 c4 e6 Nc3 Bb4 e3 O-O Bd3 d5 Nf3 c5",
            "d4 Nf6 c4 e6 Nc3 Bb4 Qc2 O-O a3 Bxc3+ Qxc3 b6",
            "d4 Nf6 c4 e6 Nf3 b6 g3 Bb7 Bg2 Be7 O-O O-O",
            "d4 Nf6 c4 e6 Nf3 d5 Nc3 Be7 Bf4 O-O e3 c5",
            "d4 Nf6 c4 g6 Nc3 Bg7 e4 d6 Nf3 O-O Be2 e5",
            "d4 Nf6 c4 g6 Nc3 d5 cxd5 Nxd5 e4 Nxc3 bxc3 Bg7",
            "d4 Nf6 c4 g6 g3 Bg7 Bg2 O-O Nc3 d6 Nf3 Nbd7",
            "d4 Nf6 c4 c5 d5 e6 Nc3 exd5 cxd5 d6 e4 g6",
            "d4 Nf6 Nf3 e6 Bg5 c5 e3 Be7 Nbd2 O-O",
            "d4 Nf6 Nf3 g6 Bf4 Bg7 e3 O-O Be2 d6",
            "d4 f5 g3 Nf6 Bg2 g6 Nf3 Bg7 O-O O-O c4 d6",
            // Anglaise, Réti
            "c4 e5 Nc3 Nf6 Nf3 Nc6 g3 d5 cxd5 Nxd5 Bg2 Nb6",
            "c4 c5 Nc3 Nc6 g3 g6 Bg2 Bg7 Nf3 Nf6 O-O O-O",
            "c4 Nf6 Nc3 e6 e4 d5 e5 d4 exf6 dxc3",
            "c4 e6 Nc3 d5 d4 Nf6 Nf3 Be7",
            "Nf3 d5 g3 Nf6 Bg2 c6 O-O Bg4 d3 Nbd7",
            "Nf3 Nf6 c4 g6 Nc3 Bg7 e4 d6 d4 O-O",
            "Nf3 d5 d4 Nf6 c4 e6 Nc3 Be7",
        };

        sealed class Entry
        {
            public Move Move;
            public int Weight;
        }

        static readonly object Gate = new object();
        static volatile Dictionary<ulong, List<Entry>> table;
        static List<string> errors;

        /// <summary>Lignes qui n'ont pas pu être lues (vide si le livre est correct).</summary>
        public static IReadOnlyList<string> Errors { get { EnsureBuilt(); return errors; } }

        /// <summary>Nombre de positions distinctes couvertes par le livre.</summary>
        public static int PositionCount { get { EnsureBuilt(); return table.Count; } }

        static void EnsureBuilt()
        {
            if (table != null) return;
            lock (Gate)
            {
                if (table != null) return;
                var t = new Dictionary<ulong, List<Entry>>();
                var errs = new List<string>();
                foreach (var line in Lines)
                {
                    var pos = Position.Start();
                    foreach (var san in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var m = Notation.Parse(pos, san);
                        if (m.IsNull) { errs.Add(line + " : coup illisible « " + san + " »"); break; }
                        if (!t.TryGetValue(pos.Hash, out var list)) { list = new List<Entry>(4); t[pos.Hash] = list; }
                        var e = list.Find(x => x.Move == m);
                        if (e == null) list.Add(new Entry { Move = m, Weight = 1 });
                        else e.Weight++;
                        pos.MakeMove(m);
                    }
                }
                errors = errs;
                table = t;
            }
        }

        /// <summary>Coups du livre pour la position (avec poids). Vide si hors livre.</summary>
        public static List<RootMoveScore> MovesFor(Position position)
        {
            EnsureBuilt();
            var result = new List<RootMoveScore>();
            if (!table.TryGetValue(position.Hash, out var list)) return result;
            var legal = MoveGenerator.Legal(position);
            foreach (var e in list)
                if (legal.Contains(e.Move)) result.Add(new RootMoveScore(e.Move, e.Weight));
            return result;
        }

        /// <summary>Tire un coup du livre au hasard (pondéré). Move.Null si hors livre.</summary>
        public static Move Pick(Position position, Random random)
        {
            var moves = MovesFor(position);
            if (moves.Count == 0) return Move.Null;
            int total = 0;
            foreach (var m in moves) total += m.Score;
            int r = random.Next(total);
            foreach (var m in moves)
            {
                r -= m.Score;
                if (r < 0) return m.Move;
            }
            return moves[moves.Count - 1].Move;
        }
    }
}
