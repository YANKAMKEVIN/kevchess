using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxSliderMathTests
    {
        [Test]
        public void ValueFollowsTheFingerAndSnaps()
        {
            Assert.That(SliderMath.ValueAt(-50, 0, 400), Is.EqualTo(0f));
            Assert.That(SliderMath.ValueAt(450, 0, 400), Is.EqualTo(1f));
            Assert.That(SliderMath.ValueAt(181, 0, 400), Is.EqualTo(0.45f).Within(1e-5));
            Assert.That(SliderMath.ValueAt(-100, -200, 200, 0f), Is.EqualTo(0.25f).Within(1e-5));
            Assert.That(SliderMath.ValueAt(10, 5, 5), Is.EqualTo(0f));
            Assert.That(SliderMath.Percent(0.45f), Is.EqualTo("45 %"));
        }
    }
}
