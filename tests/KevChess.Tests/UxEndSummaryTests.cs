using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxEndSummaryTests
    {
        [Test]
        public void LineListsMovesDurationOpeningAndRecord()
        {
            Assert.That(EndSummary.Line(67, 754, "Défense sicilienne", "Moyen", 3, 1, 2),
                Is.EqualTo("34 coups · 12 min · Défense sicilienne\nContre Moyen : 3 V · 1 N · 2 D"));
            Assert.That(EndSummary.Line(1, -1, ""), Is.EqualTo("1 coup"));
            Assert.That(EndSummary.Line(4, 30, null), Is.EqualTo("2 coups · moins d'1 min"));
        }

        [Test]
        public void DurationFormats()
        {
            Assert.That(EndSummary.Duration(59), Is.EqualTo("moins d'1 min"));
            Assert.That(EndSummary.Duration(60), Is.EqualTo("1 min"));
            Assert.That(EndSummary.Duration(3900), Is.EqualTo("1 h 05"));
        }

        [Test]
        public void SparksStartAtCenterFallAndFade()
        {
            var s0 = SparkBurst.At(3, 0f);
            Assert.That(s0.X, Is.EqualTo(0f));
            Assert.That(s0.Y, Is.EqualTo(0f));
            Assert.That(s0.Alpha, Is.EqualTo(1f));
            Assert.That(SparkBurst.At(3, SparkBurst.Lifetime).Alpha, Is.EqualTo(0f));
            // Déterministe pour une même graine.
            Assert.That(SparkBurst.At(5, 0.4f).X, Is.EqualTo(SparkBurst.At(5, 0.4f).X));
            // La gravité finit par ramener l'étincelle vers le bas.
            Assert.That(SparkBurst.At(3, 1.2f).Y, Is.LessThan(SparkBurst.At(3, 0.3f).Y));
        }
    }
}
