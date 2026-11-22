using System.IO;
using KevChess.AI.Uci;
using NUnit.Framework;

namespace KevChess.Tests
{
    /// <summary>Code de KevChess publié sous GPL v3 : fichier LICENSE, README et écran « Licences ».</summary>
    public class KevChessLicenseTests
    {
        static string RepoRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "KevChess"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "racine du dépôt introuvable");
            return dir.FullName;
        }

        [Test]
        public void LicenseFileIsTheCompleteGplV3()
        {
            var root = RepoRoot();
            var license = File.ReadAllText(Path.Combine(root, KevChessLicense.LicenseFile));
            Assert.That(license, Does.StartWith("                    GNU GENERAL PUBLIC LICENSE"));
            Assert.That(license, Does.Contain("Version 3, 29 June 2007"));
            Assert.That(license, Does.Contain("END OF TERMS AND CONDITIONS"));
            Assert.That(license, Does.Contain("How to Apply These Terms to Your New Programs"));
            // Texte identique à celui fourni avec Stockfish.
            Assert.That(license, Is.EqualTo(File.ReadAllText(Path.Combine(root, "LICENSES", "GPL-3.0.txt"))));
        }

        [Test]
        public void ReadmeAnnouncesTheLicenseAndStockfish()
        {
            var readme = File.ReadAllText(Path.Combine(RepoRoot(), "README.md"));
            Assert.That(readme, Does.Contain("GNU General Public License version 3"));
            Assert.That(readme, Does.Contain("(LICENSE)"));
            Assert.That(readme, Does.Contain("Stockfish"));
            Assert.That(readme, Does.Contain("THIRD_PARTY_NOTICES.md"));
        }

        [Test]
        public void InAppNoticeCoversKevChessThenStockfish()
        {
            var full = KevChessLicense.FullNotice;
            Assert.That(full, Does.StartWith(KevChessLicense.Notice));
            Assert.That(full, Does.EndWith(StockfishBundle.Notice));
            Assert.That(KevChessLicense.Notice, Does.Contain("GPL v3"));
            Assert.That(KevChessLicense.Notice, Does.Contain(KevChessLicense.SourceUrl));
            Assert.That(KevChessLicense.Notice, Does.Contain(KevChessLicense.Copyright));
            Assert.That(KevChessLicense.Spdx, Is.EqualTo("GPL-3.0-only"));
        }
    }
}
