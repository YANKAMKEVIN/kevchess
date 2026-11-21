using System;
using System.IO;
using System.Security.Cryptography;
using KevChess.AI.Uci;
using NUnit.Framework;

namespace KevChess.Tests
{
    /// <summary>Stockfish embarqué pour Android : binaire attendu, alignement 16 Kio, recette de build, provenance et notice GPL.</summary>
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
        public void BinaryIsTheFrozenAarch64Build()
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

        /// <summary>
        /// Google Play exige des bibliothèques natives alignées sur 16 Kio (appareils à pages de 16 Ko) :
        /// chaque segment PT_LOAD doit avoir p_align ≥ 0x4000 et un décalage congru à son adresse modulo 0x4000.
        /// </summary>
        [Test]
        public void LoadSegmentsAreAlignedOn16KiB()
        {
            var data = File.ReadAllBytes(BinaryPath());
            ulong page = StockfishBundle.PageAlignment;
            Assert.That(page, Is.EqualTo(16384UL));
            var phoff = BitConverter.ToUInt64(data, 32);
            int phentsize = BitConverter.ToUInt16(data, 54), phnum = BitConverter.ToUInt16(data, 56);
            Assert.That(phentsize, Is.EqualTo(56), "Elf64_Phdr");
            int loads = 0;
            for (int i = 0; i < phnum; i++)
            {
                int at = checked((int)phoff + i * phentsize);
                if (BitConverter.ToUInt32(data, at) != 1) continue; // PT_LOAD
                loads++;
                ulong offset = BitConverter.ToUInt64(data, at + 8), vaddr = BitConverter.ToUInt64(data, at + 16);
                ulong align = BitConverter.ToUInt64(data, at + 48);
                Assert.That(align, Is.GreaterThanOrEqualTo(page), $"LOAD {i} : p_align 0x{align:X}");
                Assert.That(offset % page, Is.EqualTo(vaddr % page), $"LOAD {i} : décalage et adresse non congrus");
            }
            Assert.That(loads, Is.GreaterThan(0), "aucun segment LOAD");
        }

        [Test]
        public void BuildScriptReproducesTheFrozenRecipe()
        {
            var script = File.ReadAllText(Path.Combine(RepoRoot(), StockfishBundle.BuildScript));
            Assert.That(script, Does.Contain("TAG=" + StockfishBundle.Tag));
            Assert.That(script, Does.Contain("COMMIT=" + StockfishBundle.Commit));
            Assert.That(script, Does.Contain("NDK_VERSION=" + StockfishBundle.NdkVersion));
            Assert.That(script, Does.Contain("BENCH=" + StockfishBundle.BenchSignature));
            Assert.That(script, Does.Contain("NNUE_NAME=" + StockfishBundle.Network));
            Assert.That(script, Does.Contain("PAGE=" + StockfishBundle.PageAlignment));
            Assert.That(script, Does.Contain("ARCH=" + StockfishBundle.BuildArch));
            Assert.That(script, Does.Contain("max-page-size=$PAGE"));
        }

        [Test]
        public void NoticePointsToTheExactSourceAndLicense()
        {
            Assert.That(StockfishBundle.SourceUrl, Does.EndWith("/tree/" + StockfishBundle.Tag));
            Assert.That(StockfishBundle.Commit, Does.Match("^[0-9a-f]{40}$"));
            Assert.That(StockfishBundle.BinarySha256, Does.Match("^[0-9a-f]{64}$"));
            var notice = StockfishBundle.Notice;
            Assert.That(notice, Does.Contain(StockfishBundle.Version));
            Assert.That(notice, Does.Contain(StockfishBundle.SourceUrl));
            Assert.That(notice, Does.Contain("GPL v3"));
            Assert.That(notice, Does.Contain(StockfishBundle.LicenseUrl));
            Assert.That(notice, Does.Contain(StockfishBundle.BuildScript));
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
            Assert.That(notices, Does.Contain(StockfishBundle.Commit));
            Assert.That(notices, Does.Contain(StockfishBundle.BuildScript));
            Assert.That(notices, Does.Contain("-Wl,-z,max-page-size=16384"));
        }
    }
}
