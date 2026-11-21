using System;
using System.IO;
using System.Security.Cryptography;
using KevChess.AI.Uci;
using NUnit.Framework;

namespace KevChess.Tests
{
    /// <summary>Stockfish embarqué pour Android : binaire attendu, provenance et notice GPL.</summary>
    public class StockfishBundleTests
    {
        static string RepoRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "KevChess"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "racine du dépôt introuvable");
            return dir.FullName;
        }

        static string BinaryPath() =>
            Path.Combine(RepoRoot(), "Assets", "Plugins", "Android", "libs", "arm64-v8a", "libstockfish.so");

        [Test]
        public void BinaryIsTheOfficialAarch64Release()
        {
            var path = BinaryPath();
            Assert.That(File.Exists(path), Is.True, path);
            var info = new FileInfo(path);
            Assert.That(info.Length, Is.EqualTo(StockfishBundle.BinarySize));
            Assert.That(info.Length, Is.LessThan(100L * 1024 * 1024), "GitHub refuse les fichiers de plus de 100 Mio hors LFS");

            var header = new byte[20];
            using (var f = File.OpenRead(path)) Assert.That(f.Read(header, 0, header.Length), Is.EqualTo(header.Length));
            Assert.That(header[0] == 0x7F && header[1] == (byte)'E' && header[2] == (byte)'L' && header[3] == (byte)'F', Is.True, "ELF");
            Assert.That(header[4], Is.EqualTo(2), "ELF 64 bits");
            Assert.That(header[5], Is.EqualTo(1), "petit-boutiste");
            Assert.That(BitConverter.ToUInt16(header, 18), Is.EqualTo(0xB7), "EM_AARCH64");

            using (var sha = SHA256.Create())
            using (var f = File.OpenRead(path))
                Assert.That(Convert.ToHexString(sha.ComputeHash(f)).ToLowerInvariant(), Is.EqualTo(StockfishBundle.BinarySha256));
        }

        [Test]
        public void NoticePointsToTheExactSourceAndLicense()
        {
            Assert.That(StockfishBundle.SourceUrl, Does.EndWith("/tree/" + StockfishBundle.Tag));
            Assert.That(StockfishBundle.ReleaseAsset, Does.Contain("/download/" + StockfishBundle.Tag + "/"));
            Assert.That(StockfishBundle.Commit, Does.Match("^[0-9a-f]{40}$"));
            Assert.That(StockfishBundle.BinarySha256, Does.Match("^[0-9a-f]{64}$"));
            var notice = StockfishBundle.Notice;
            Assert.That(notice, Does.Contain(StockfishBundle.Version));
            Assert.That(notice, Does.Contain(StockfishBundle.SourceUrl));
            Assert.That(notice, Does.Contain("GPL v3"));
            Assert.That(notice, Does.Contain(StockfishBundle.LicenseUrl));
        }

        [Test]
        public void LicenseFilesAreInTheRepository()
        {
            var root = RepoRoot();
            var gpl = File.ReadAllText(Path.Combine(root, "LICENSES", "GPL-3.0.txt"));
            Assert.That(gpl, Does.Contain("GNU GENERAL PUBLIC LICENSE"));
            Assert.That(gpl, Does.Contain("Version 3, 29 June 2007"));
            Assert.That(File.ReadAllText(Path.Combine(root, "LICENSES", "Stockfish-AUTHORS.txt")), Does.Contain("Tord Romstad"));
            var notices = File.ReadAllText(Path.Combine(root, "THIRD_PARTY_NOTICES.md"));
            Assert.That(notices, Does.Contain(StockfishBundle.SourceUrl));
            Assert.That(notices, Does.Contain(StockfishBundle.BinarySha256));
        }
    }
}
