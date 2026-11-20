using System.Collections.Generic;
using System.Linq;
using KevChess.Core;
using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxPuzzleGroupsTests
    {
        [Test]
        public void SectionsFollowMateDepthAndCountSolved()
        {
            var all = PuzzleSet.All;
            var solved = new HashSet<string> { all[0].Id };
            var sections = PuzzleGroups.Build(all, solved.Contains);
            Assert.That(sections.Select(s => s.MateIn), Is.Ordered);
            Assert.That(sections.Sum(s => s.Indices.Count), Is.EqualTo(all.Count));
            Assert.That(sections.Sum(s => s.Solved), Is.EqualTo(1));
            foreach (var s in sections)
                Assert.That(s.Indices.All(i => all[i].MateIn == s.MateIn), Is.True);
            var first = sections.First(s => s.Indices.Contains(0));
            Assert.That(first.Progress, Is.EqualTo("1 / " + first.Indices.Count));
            Assert.That(first.Title, Is.EqualTo("Mat en " + all[0].MateIn));
        }

        [Test]
        public void ContinueGoesToTheFirstUnsolvedEasiestPuzzle()
        {
            var a = new Puzzle("a", "A", Position.StartFen, 2, "e2e4");
            var b = new Puzzle("b", "B", Position.StartFen, 1, "e2e4");
            var c = new Puzzle("c", "C", Position.StartFen, 1, "e2e4");
            var list = new[] { a, b, c };
            Assert.That(PuzzleGroups.FirstUnsolved(list, id => false), Is.EqualTo(1), "mat en 1 d'abord");
            Assert.That(PuzzleGroups.FirstUnsolved(list, id => id == "b"), Is.EqualTo(2));
            Assert.That(PuzzleGroups.FirstUnsolved(list, id => id != "a"), Is.EqualTo(0));
            Assert.That(PuzzleGroups.FirstUnsolved(list, id => true), Is.EqualTo(-1));
            Assert.That(PuzzleGroups.ContinueLabel(11), Is.EqualTo("Continuer : puzzle 12"));
        }
    }
}
