using KevChess.Core.Ux;
using NUnit.Framework;

namespace KevChess.Tests
{
    public class UxSilhouetteTests
    {
        [Test]
        public void TallRectangleIsCenteredAndSitsOnTheBottom()
        {
            // Rectangle 1 × 2, deux triangles d'orientations opposées (la silhouette ignore le sens des faces).
            float[] u = { 0, 1, 1, 0 };
            float[] v = { 0, 0, 2, 2 };
            int[] tris = { 0, 1, 2, 0, 3, 2 };
            const int n = 40;
            var a = Silhouette.Rasterize(u, v, tris, n, 0.1f);
            // Hauteur utile : 80 % de 40 = 32 pixels, de y = 4 à 36 ; largeur 16 centrée (12 à 28).
            Assert.That(a[5 * n + 20], Is.EqualTo(255), "intérieur");
            Assert.That(a[2 * n + 20], Is.EqualTo(0), "marge du bas");
            Assert.That(a[38 * n + 20], Is.EqualTo(0), "au-dessus");
            Assert.That(a[20 * n + 8], Is.EqualTo(0), "à gauche");
            Assert.That(a[20 * n + 31], Is.EqualTo(0), "à droite");
            Assert.That(a[20 * n + 13], Is.EqualTo(255));
        }

        [Test]
        public void EdgesAreAntialiased()
        {
            float[] u = { 0, 1, 0 };
            float[] v = { 0, 0, 1 };
            var a = Silhouette.Rasterize(u, v, new[] { 0, 1, 2 }, 32, 0f);
            bool partial = false;
            foreach (var x in a) if (x > 0 && x < 255) partial = true;
            Assert.That(partial, Is.True);
        }

        [Test]
        public void EmptyOrFlatMeshGivesEmptyImage()
        {
            Assert.That(Silhouette.Rasterize(new float[0], new float[0], new int[0], 8), Has.All.EqualTo((byte)0));
            float[] u = { 0, 1, 2 };
            float[] v = { 0, 0, 0 };
            Assert.That(Silhouette.Rasterize(u, v, new[] { 0, 1, 2 }, 8), Has.All.EqualTo((byte)0));
        }
    }
}
