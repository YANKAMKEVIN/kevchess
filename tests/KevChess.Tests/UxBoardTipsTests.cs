using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxBoardTipsTests
    {
        [Test]
        public void TipsFollowTheGestureOrder()
        {
            var tips = new BoardTips();
            Assert.That(tips.Active, Is.True);
            Assert.That(tips.Expected, Is.EqualTo(BoardGesture.MovePlayed));
            Assert.That(tips.Counter, Is.EqualTo("1 / 3"));

            // Un geste hors d'ordre ne ferme rien.
            Assert.That(tips.OnGesture(BoardGesture.CameraRecentered), Is.False);
            Assert.That(tips.OnGesture(BoardGesture.MovePlayed), Is.True);
            Assert.That(tips.Expected, Is.EqualTo(BoardGesture.CameraOrbited));
            Assert.That(tips.OnGesture(BoardGesture.CameraOrbited), Is.True);
            Assert.That(tips.OnGesture(BoardGesture.CameraRecentered), Is.True);
            Assert.That(tips.Active, Is.False);
            Assert.That(tips.Text, Is.Null);
        }

        [Test]
        public void OrbitTipNeedsARealDrag()
        {
            var tips = new BoardTips(1);
            Assert.That(tips.OnOrbit(40f), Is.False);
            Assert.That(tips.OnOrbit(-60f), Is.False);
            Assert.That(tips.OnOrbit(BoardTips.OrbitPixels), Is.True);
            Assert.That(tips.Step, Is.EqualTo(2));
        }

        [Test]
        public void OrbitIgnoredWhenAnotherTipIsShown()
        {
            var tips = new BoardTips();
            Assert.That(tips.OnOrbit(1000f), Is.False);
            Assert.That(tips.Step, Is.EqualTo(0));
        }

        [Test]
        public void SkipAndRestart()
        {
            var tips = new BoardTips(2);
            tips.Skip();
            Assert.That(tips.Active, Is.False);
            tips.Restart();
            Assert.That(tips.Step, Is.EqualTo(0));
            Assert.That(new BoardTips(99).Step, Is.EqualTo(BoardTips.Count));
            Assert.That(new BoardTips(-3).Step, Is.EqualTo(0));
        }
    }
}
