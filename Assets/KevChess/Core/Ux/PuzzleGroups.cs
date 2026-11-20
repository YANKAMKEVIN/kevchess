using System;
using System.Collections.Generic;

namespace KevChess.Core.Ux
{
    /// <summary>Section de la liste des puzzles : même nombre de coups pour mater.</summary>
    public sealed class PuzzleSection
    {
        public int MateIn;
        /// <summary>Index des puzzles dans la liste complète, dans l'ordre.</summary>
        public List<int> Indices = new List<int>();
        public int Solved;

        public string Title => "Mat en " + MateIn;
        public string Progress => Solved + " / " + Indices.Count;
        public float Fraction => Indices.Count == 0 ? 0f : (float)Solved / Indices.Count;
    }

    /// <summary>Liste des puzzles groupée par difficulté (mat en 1, 2, 3…) et reprise au premier non résolu.</summary>
    public static class PuzzleGroups
    {
        public static List<PuzzleSection> Build(IReadOnlyList<Puzzle> puzzles, Func<string, bool> isSolved)
        {
            var byMate = new SortedDictionary<int, PuzzleSection>();
            for (int i = 0; i < puzzles.Count; i++)
            {
                int m = puzzles[i].MateIn;
                if (!byMate.TryGetValue(m, out var section)) byMate[m] = section = new PuzzleSection { MateIn = m };
                section.Indices.Add(i);
                if (isSolved(puzzles[i].Id)) section.Solved++;
            }
            return new List<PuzzleSection>(byMate.Values);
        }

        /// <summary>Premier puzzle non résolu dans l'ordre des sections (du plus facile au plus dur) ; -1 si tout est résolu.</summary>
        public static int FirstUnsolved(IReadOnlyList<Puzzle> puzzles, Func<string, bool> isSolved)
        {
            foreach (var section in Build(puzzles, isSolved))
                foreach (int i in section.Indices)
                    if (!isSolved(puzzles[i].Id)) return i;
            return -1;
        }

        /// <summary>« Continuer : puzzle 12 » (numéro affiché dans la liste, à partir de 1).</summary>
        public static string ContinueLabel(int index) => "Continuer : puzzle " + (index + 1);
    }
}
