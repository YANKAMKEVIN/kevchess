using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxThinkingGlowTests
    {
        [Test]
        public void GlintRunsAroundTheCard()
        {
            ThinkingGlow.PerimeterPoint(0f, 400, 100, out float x, out float y);
            Assert.That((x, y), Is.EqualTo((-200f, 50f)), "coin haut gauche");
            // Périmètre 1000 : 0,4 → 400 = coin haut droit ; 0,5 → bord droit à mi-hauteur.
            ThinkingGlow.PerimeterPoint(0.4f, 400, 100, out x, out y);
            Assert.That(x, Is.EqualTo(200f).Within(1e-3));
            Assert.That(y, Is.EqualTo(50f).Within(1e-3));
            ThinkingGlow.PerimeterPoint(0.45f, 400, 100, out x, out y);
            Assert.That(y, Is.EqualTo(0f).Within(1e-3));
            ThinkingGlow.PerimeterPoint(1.95f, 400, 100, out x, out y);
            Assert.That(x, Is.EqualTo(-200f).Within(1e-3), "phase repliée, bord gauche");
        }

        [Test]
        public void PulseAndFlashStayInRange()
        {
            for (float t = 0; t < 4f; t += 0.1f)
                Assert.That(ThinkingGlow.DotScale(t), Is.InRange(0.8f, 1.2f));
            Assert.That(ThinkingGlow.Flash(0f), Is.EqualTo(1f));
            Assert.That(ThinkingGlow.Flash(ThinkingGlow.FlashSeconds), Is.EqualTo(0f));
            Assert.That(ThinkingGlow.Flash(-1f), Is.EqualTo(0f));
        }
    }
}
