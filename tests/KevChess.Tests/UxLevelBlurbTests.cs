using KevChess.AI;
using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxLevelBlurbTests
    {
        [Test]
        public void LineUsesTheEngineThinkingTime()
        {
            var medium = ChessAI.GetSettings(Difficulty.Moyen);
            Assert.That(LevelBlurb.Line((int)Difficulty.Moyen, medium.MaxTimeMs), Is.EqualTo("Solide, connaît les ouvertures · réfléchit jusqu'à 0,9 s"));
            Assert.That(LevelBlurb.Line(4, ChessAI.GetSettings(Difficulty.Maitre).MaxTimeMs), Does.EndWith("jusqu'à 3 s"));
            Assert.That(LevelBlurb.Line(99, 1000), Does.StartWith("Pleine force"));
        }

        [Test]
        public void RecordIsCompact()
        {
            Assert.That(LevelBlurb.Record(0, 0, 0), Is.EqualTo("aucune partie"));
            Assert.That(LevelBlurb.Record(3, 1, 2), Is.EqualTo("3 V · 1 N · 2 D"));
            Assert.That(LevelBlurb.Seconds(1500), Is.EqualTo("1,5 s"));
        }
    }
}
