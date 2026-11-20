using KevChess.Core;
using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxReplaceGuardTests
    {
        [Test]
        public void FreshOrFinishedGamesAreReplacedSilently()
        {
            Assert.That(ReplaceGuard.Question(null), Is.Null);
            Assert.That(ReplaceGuard.Question(new ChessGame()), Is.Null);

            var mated = new ChessGame();
            foreach (var san in new[] { "f3", "e5", "g4", "Qh4#" }) Assert.That(mated.TryMakeSan(san), Is.True);
            Assert.That(mated.IsOver, Is.True);
            Assert.That(ReplaceGuard.NeedsConfirm(mated), Is.False);
        }

        [Test]
        public void GameInProgressAsksWithTheMoveNumber()
        {
            var game = new ChessGame();
            foreach (var san in new[] { "e4", "e5", "Nf3" }) Assert.That(game.TryMakeSan(san), Is.True);
            Assert.That(ReplaceGuard.Question(game), Is.EqualTo("Remplacer la partie en cours\n(coup 2) ?"));
            Assert.That(ReplaceGuard.Question(game, true), Does.StartWith("Remplacer la partie sauvegardée"));
        }
    }
}
