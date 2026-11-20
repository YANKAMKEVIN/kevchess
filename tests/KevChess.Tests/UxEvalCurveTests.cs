using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxEvalCurveTests
    {
        [Test]
        public void NormalizeIsBoundedAndSigned()
        {
            Assert.That(EvalCurve.Normalize(0), Is.EqualTo(0f));
            Assert.That(EvalCurve.Normalize(400), Is.EqualTo(0.5f));
            Assert.That(EvalCurve.Normalize(-400), Is.EqualTo(-0.5f));
            Assert.That(EvalCurve.Normalize(10000), Is.LessThan(1f).And.GreaterThan(0.95f));
        }

        [Test]
        public void PointsStartWithTheInitialEvaluation()
        {
            var p = EvalCurve.Points(20, new[] { 30, -400, 400 });
            Assert.That(p.Length, Is.EqualTo(4));
            Assert.That(p[2], Is.EqualTo(-0.5f));
        }

        [Test]
        public void TapMapsToTheNearestPosition()
        {
            Assert.That(EvalCurve.IndexAt(0f, 11), Is.EqualTo(0));
            Assert.That(EvalCurve.IndexAt(0.52f, 11), Is.EqualTo(5));
            Assert.That(EvalCurve.IndexAt(2f, 11), Is.EqualTo(10));
            Assert.That(EvalCurve.X(5, 11), Is.EqualTo(0.5f));
        }

        [Test]
        public void RasterShowsWhiteAboveAndBlackBelow()
        {
            const int w = 40, h = 21;
            var img = EvalCurve.Rasterize(new[] { 0.8f, 0.8f, -0.8f, -0.8f }, w, h);
            Assert.That(img[14 * w + 2], Is.EqualTo(EvalCurve.WhiteArea), "avantage blanc au-dessus du milieu");
            Assert.That(img[5 * w + 37], Is.EqualTo(EvalCurve.BlackArea), "avantage noir en dessous");
            Assert.That(img[5 * w + 2], Is.EqualTo(EvalCurve.Empty));
            Assert.That(img[10 * w + 2], Is.EqualTo(EvalCurve.Middle));
            bool line = false;
            for (int r = 0; r < h; r++) if (img[r * w + 20] == EvalCurve.Line) line = true;
            Assert.That(line, Is.True, "le trait traverse la bascule");
        }
    }
}
