using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxPowerBarFoldTests
    {
        [Test]
        public void ExpandedKeepsThePreviousLayout()
        {
            Assert.That(PowerBarFold.DefaultCollapsed, Is.False);
            Assert.That(PowerBarFold.Height(false), Is.EqualTo(230f));
            Assert.That(PowerBarFold.StripBottom(false), Is.EqualTo(404f));
        }

        [Test]
        public void CollapsedGivesTheSpaceBackToTheBoard()
        {
            Assert.That(PowerBarFold.StripBottom(true), Is.EqualTo(234f));
            Assert.That(PowerBarFold.StripBottom(false) - PowerBarFold.StripBottom(true), Is.EqualTo(PowerBarFold.Gain));
            Assert.That(PowerBarFold.Gain, Is.EqualTo(170f));
            // Le bouton (48 px) tient dans l'en-tête replié.
            Assert.That(PowerBarFold.CollapsedHeight, Is.GreaterThanOrEqualTo(48f));
        }

        [Test]
        public void CaptionSummarisesWhenCollapsed()
        {
            Assert.That(PowerBarFold.Caption(false, 3, null), Is.EqualTo("Pouvoir facultatif, puis votre coup"));
            Assert.That(PowerBarFold.Caption(true, 0, null), Is.EqualTo("Pouvoirs · aucun disponible"));
            Assert.That(PowerBarFold.Caption(true, 1, ""), Is.EqualTo("Pouvoirs · 1 disponible"));
            Assert.That(PowerBarFold.Caption(true, 3, null), Is.EqualTo("Pouvoirs · 3 disponibles"));
            Assert.That(PowerBarFold.Caption(true, 2, "Gel"), Is.EqualTo("Pouvoirs · Gel en cours"));
        }

        [Test]
        public void ToggleLabelNamesTheNextAction()
        {
            Assert.That(PowerBarFold.ToggleLabel(false), Is.EqualTo("Replier"));
            Assert.That(PowerBarFold.ToggleLabel(true), Is.EqualTo("Afficher"));
        }

        [Test]
        public void StoredStateRoundTripsAndFallsBackToDefault()
        {
            Assert.That(PowerBarFold.FromStored(PowerBarFold.ToStored(true)), Is.True);
            Assert.That(PowerBarFold.FromStored(PowerBarFold.ToStored(false)), Is.False);
            Assert.That(PowerBarFold.FromStored(-1), Is.EqualTo(PowerBarFold.DefaultCollapsed));
            Assert.That(PowerBarFold.FromStored(7), Is.EqualTo(PowerBarFold.DefaultCollapsed));
        }
    }
}
