using System;
using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxIconShapesTests
    {
        static float At(byte[] a, int size, float x, float y) => a[(int)(y * size) * size + (int)(x * size)] / 255f;

        [Test]
        public void EveryIconHasInkAndTransparentCorners()
        {
            foreach (IconKind kind in Enum.GetValues(typeof(IconKind)))
            {
                var a = IconShapes.Rasterize(kind, 64);
                int ink = 0;
                foreach (var v in a) if (v > 128) ink++;
                // Ni vide ni pavé plein : entre 5 % et 45 % de la surface.
                Assert.That(ink, Is.InRange(64 * 64 / 20, 64 * 64 * 45 / 100), kind.ToString());
                Assert.That(a[0] + a[63] + a[63 * 64] + a[64 * 64 - 1], Is.EqualTo(0), kind + " : coins transparents");
            }
        }

        [Test]
        public void ShapesMatchTheirMeaning()
        {
            const int n = 96;
            var more = IconShapes.Rasterize(IconKind.More, n);
            Assert.That(At(more, n, 0.5f, 0.5f), Is.EqualTo(1f), "point central plein");
            Assert.That(At(more, n, 0.36f, 0.5f), Is.EqualTo(0f), "espace entre deux points");

            var view = IconShapes.Rasterize(IconKind.View, n);
            Assert.That(At(view, n, 0.5f, 0.5f), Is.EqualTo(1f), "pupille");
            Assert.That(At(view, n, 0.27f, 0.5f), Is.EqualTo(0f), "blanc de l'œil entre pupille et contour");

            var next = IconShapes.Rasterize(IconKind.Next, n);
            Assert.That(At(next, n, 0.5f, 0.5f), Is.EqualTo(1f), "flèche horizontale");
            Assert.That(At(next, n, 0.3f, 0.75f), Is.EqualTo(0f));
        }

        [Test]
        public void CoverageIsAntialiasedAtTheEdge()
        {
            // Bord exact du point central de « More » (rayon 0,09) : demi-couverture.
            float edge = IconShapes.Coverage(IconKind.More, 0.59f, 0.5f, 1f / 64);
            Assert.That(edge, Is.EqualTo(0.5f).Within(0.05f));
            Assert.That(IconShapes.Distance(IconKind.More, 0.5f, 0.5f), Is.LessThan(0f));
        }
    }
}
