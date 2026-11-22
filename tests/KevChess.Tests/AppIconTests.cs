using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace KevChess.Tests
{
    /// <summary>
    /// Icône d'application générée par tools/generate-app-icon.py : chaque emplacement Android de
    /// ProjectSettings pointe vers un PNG présent, de la bonne taille et du bon type de calque.
    /// </summary>
    public class AppIconTests
    {
        static string RepoRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "KevChess"))) dir = dir.Parent;
            Assert.That(dir, Is.Not.Null, "racine du dépôt introuvable");
            return dir.FullName;
        }

        static Dictionary<string, string> IconsByGuid(string root)
        {
            var map = new Dictionary<string, string>();
            foreach (var meta in Directory.GetFiles(Path.Combine(root, "Assets", "KevChess", "AppIcon"), "*.png.meta"))
            {
                var m = Regex.Match(File.ReadAllText(meta), @"guid: ([0-9a-f]{32})");
                Assert.That(m.Success, meta);
                map[m.Groups[1].Value] = meta.Substring(0, meta.Length - ".meta".Length);
            }
            return map;
        }

        static (int width, int height, int colorType) PngHeader(string path)
        {
            var b = File.ReadAllBytes(path);
            Assert.That(b.Length, Is.GreaterThan(26));
            Assert.That(b[1] == 'P' && b[2] == 'N' && b[3] == 'G', path);
            int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
            int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
            return (w, h, b[25]);
        }

        [Test]
        public void EveryAndroidIconSlotPointsToAGeneratedPngOfTheRightSize()
        {
            var root = RepoRoot();
            var icons = IconsByGuid(root);
            var settings = File.ReadAllText(Path.Combine(root, "ProjectSettings", "ProjectSettings.asset"));
            int start = settings.IndexOf("  - m_BuildTarget: Android", StringComparison.Ordinal);
            int end = settings.IndexOf("  - m_BuildTarget: tvOS", start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0));
            var block = settings.Substring(start, end - start);
            var slots = block.Split(new[] { "    - m_Textures:" }, StringSplitOptions.None);
            Assert.That(slots.Length - 1, Is.EqualTo(18), "6 adaptatives, 6 rondes, 6 classiques");
            var kinds = new Dictionary<int, int>();
            for (int i = 1; i < slots.Length; i++)
            {
                var slot = slots[i];
                int width = int.Parse(Regex.Match(slot, @"m_Width: (\d+)").Groups[1].Value);
                int kind = int.Parse(Regex.Match(slot, @"m_Kind: (\d+)").Groups[1].Value);
                kinds[kind] = kinds.TryGetValue(kind, out var n) ? n + 1 : 1;
                var guids = Regex.Matches(slot, @"guid: ([0-9a-f]{32})");
                Assert.That(guids.Count, Is.EqualTo(kind == 2 ? 2 : 1), "emplacement " + width + " de type " + kind);
                for (int g = 0; g < guids.Count; g++)
                {
                    Assert.That(icons.TryGetValue(guids[g].Groups[1].Value, out var png), Is.True, "GUID inconnu dans " + width);
                    var name = Path.GetFileName(png);
                    string expected = kind == 2 ? (g == 0 ? "icon-adaptive-bg-" : "icon-adaptive-fg-") : kind == 1 ? "icon-round-" : "icon-legacy-";
                    Assert.That(name, Is.EqualTo(expected + width + ".png"));
                    var (w, h, colorType) = PngHeader(png);
                    Assert.That((w, h), Is.EqualTo((width, width)), name);
                    // Le premier plan adaptatif et les icônes masquées ont une transparence (RGBA = type 6).
                    if (kind != 2 || g == 1) Assert.That(colorType, Is.EqualTo(6), name);
                }
            }
            Assert.That(kinds[0], Is.EqualTo(6));
            Assert.That(kinds[1], Is.EqualTo(6));
            Assert.That(kinds[2], Is.EqualTo(6));
        }

        [Test]
        public void StoreIconIsTheGooglePlaySize()
        {
            var png = Path.Combine(RepoRoot(), "Assets", "KevChess", "AppIcon", "icon-store-512.png");
            var (w, h, _) = PngHeader(png);
            Assert.That((w, h), Is.EqualTo((512, 512)));
        }
    }
}
