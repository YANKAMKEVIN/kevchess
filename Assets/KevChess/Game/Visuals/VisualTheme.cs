using System;
using UnityEngine;

namespace KevChess.Game.Visuals
{
    /// <summary>Couleurs d'un thème visuel (les intensités HDR sont appliquées par <see cref="NeonMaterials"/>).</summary>
    public sealed class ThemePalette
    {
        public string Name;

        // Pièces : corps cristal (couleur de base + lueur interne) et accents néon.
        public Color WhiteBody, WhiteBodyEmission, WhiteGlow;
        public Color BlackBody, BlackBodyEmission, BlackGlow;

        // Plateau (alpha = opacité du verre).
        public Color BoardDark, BoardDarkEmission, BoardLight, BoardLightEmission;
        public Color Grid, Lip, Side, Labels, Abyss;

        // Surbrillances.
        public Color Selection, LegalMove, Capture, LastMove, Check;
    }

    /// <summary>
    /// Thèmes visuels du plateau et des pièces, appliqués en direct sur les matériaux partagés
    /// et sur les instances existantes (pièces, halo d'échec, lumières d'appoint).
    /// Le choix n'est pas persisté ici : l'interface s'en charge et appelle <see cref="Apply"/>.
    /// </summary>
    public static class VisualTheme
    {
        /// <summary>Noms affichables, dans l'ordre des index.</summary>
        public static readonly string[] Names = { "Néon", "Aurore", "Émeraude", "Glace & Braise" };

        static readonly ThemePalette[] Palettes = { Neon(), Aurore(), Emeraude(), GlaceBraise() };

        static int _current;
        static bool _highContrast;

        /// <summary>Déclenché après chaque changement de thème ou du mode lisibilité.</summary>
        public static event Action Changed;

        /// <summary>Index du thème actif (dans <see cref="Names"/>).</summary>
        public static int Current => _current;

        /// <summary>Palette du thème actif.</summary>
        public static ThemePalette Palette => Palettes[_current];

        /// <summary>Palette du thème <paramref name="index"/> (borné), sans l'appliquer : pastilles des Réglages.</summary>
        public static ThemePalette PaletteOf(int index) => Palettes[Mathf.Clamp(index, 0, Palettes.Length - 1)];

        /// <summary>
        /// Mode lisibilité : liserés, accents et émission plus forts, coordonnées plus lumineuses.
        /// Réappliqué immédiatement.
        /// </summary>
        public static bool HighContrast
        {
            get => _highContrast;
            set
            {
                if (_highContrast == value) return;
                _highContrast = value;
                Refresh();
            }
        }

        /// <summary>Active le thème <paramref name="index"/> (borné) et met à jour la scène en direct.</summary>
        public static void Apply(int index)
        {
            _current = Mathf.Clamp(index, 0, Palettes.Length - 1);
            Refresh();
        }

        static void Refresh()
        {
            NeonMaterials.ApplyTheme();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ palettes

        static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        static ThemePalette Neon() => new ThemePalette
        {
            Name = "Néon",
            WhiteBody = C(0.70f, 0.84f, 0.97f), WhiteBodyEmission = C(0.05f, 0.16f, 0.22f), WhiteGlow = C(0.35f, 0.92f, 1.00f),
            BlackBody = C(0.045f, 0.03f, 0.075f), BlackBodyEmission = C(0.10f, 0.015f, 0.13f), BlackGlow = C(1.00f, 0.22f, 0.85f),
            BoardDark = C(0.012f, 0.010f, 0.028f, 0.66f), BoardDarkEmission = C(0.004f, 0.002f, 0.012f),
            BoardLight = C(0.28f, 0.31f, 0.44f, 0.80f), BoardLightEmission = C(0.025f, 0.035f, 0.065f),
            Grid = C(0.30f, 0.55f, 1.0f), Lip = C(0.40f, 0.62f, 1.0f), Side = C(0.50f, 0.42f, 1.00f),
            Labels = C(0.45f, 0.70f, 1.0f), Abyss = C(0.22f, 0.14f, 0.60f),
            Selection = C(0.45f, 1.00f, 0.95f), LegalMove = C(0.55f, 0.85f, 1.00f), Capture = C(1.00f, 0.38f, 0.55f),
            LastMove = C(1.00f, 0.72f, 0.30f), Check = C(1.00f, 0.08f, 0.12f),
        };

        static ThemePalette Aurore() => new ThemePalette
        {
            Name = "Aurore",
            WhiteBody = C(0.96f, 0.84f, 0.60f), WhiteBodyEmission = C(0.20f, 0.12f, 0.03f), WhiteGlow = C(1.00f, 0.74f, 0.28f),
            BlackBody = C(0.07f, 0.035f, 0.11f), BlackBodyEmission = C(0.10f, 0.03f, 0.18f), BlackGlow = C(0.66f, 0.34f, 1.00f),
            BoardDark = C(0.024f, 0.012f, 0.032f, 0.68f), BoardDarkEmission = C(0.010f, 0.003f, 0.014f),
            BoardLight = C(0.42f, 0.34f, 0.32f, 0.80f), BoardLightEmission = C(0.06f, 0.035f, 0.03f),
            Grid = C(0.95f, 0.62f, 0.35f), Lip = C(1.00f, 0.72f, 0.42f), Side = C(0.85f, 0.45f, 0.95f),
            Labels = C(1.00f, 0.80f, 0.55f), Abyss = C(0.42f, 0.16f, 0.50f),
            Selection = C(1.00f, 0.90f, 0.55f), LegalMove = C(1.00f, 0.82f, 0.50f), Capture = C(1.00f, 0.35f, 0.65f),
            LastMove = C(0.72f, 0.48f, 1.00f), Check = C(1.00f, 0.10f, 0.10f),
        };

        static ThemePalette Emeraude() => new ThemePalette
        {
            Name = "Émeraude",
            WhiteBody = C(0.66f, 0.94f, 0.80f), WhiteBodyEmission = C(0.03f, 0.18f, 0.10f), WhiteGlow = C(0.22f, 1.00f, 0.58f),
            BlackBody = C(0.08f, 0.012f, 0.025f), BlackBodyEmission = C(0.16f, 0.01f, 0.03f), BlackGlow = C(1.00f, 0.12f, 0.28f),
            BoardDark = C(0.006f, 0.020f, 0.014f, 0.68f), BoardDarkEmission = C(0.002f, 0.010f, 0.006f),
            BoardLight = C(0.25f, 0.36f, 0.32f, 0.80f), BoardLightEmission = C(0.02f, 0.06f, 0.04f),
            Grid = C(0.30f, 0.90f, 0.60f), Lip = C(0.40f, 1.00f, 0.70f), Side = C(0.95f, 0.78f, 0.35f),
            Labels = C(0.55f, 1.00f, 0.75f), Abyss = C(0.08f, 0.40f, 0.26f),
            Selection = C(0.55f, 1.00f, 0.78f), LegalMove = C(0.50f, 1.00f, 0.75f), Capture = C(1.00f, 0.30f, 0.40f),
            LastMove = C(1.00f, 0.80f, 0.35f), Check = C(1.00f, 0.05f, 0.08f),
        };

        static ThemePalette GlaceBraise() => new ThemePalette
        {
            Name = "Glace & Braise",
            WhiteBody = C(0.80f, 0.92f, 1.00f), WhiteBodyEmission = C(0.08f, 0.16f, 0.26f), WhiteGlow = C(0.55f, 0.85f, 1.00f),
            BlackBody = C(0.06f, 0.03f, 0.02f), BlackBodyEmission = C(0.18f, 0.05f, 0.01f), BlackGlow = C(1.00f, 0.42f, 0.08f),
            BoardDark = C(0.016f, 0.013f, 0.014f, 0.68f), BoardDarkEmission = C(0.008f, 0.004f, 0.002f),
            BoardLight = C(0.34f, 0.40f, 0.50f, 0.80f), BoardLightEmission = C(0.03f, 0.05f, 0.08f),
            Grid = C(0.45f, 0.70f, 1.00f), Lip = C(0.60f, 0.82f, 1.00f), Side = C(1.00f, 0.55f, 0.25f),
            Labels = C(0.70f, 0.88f, 1.00f), Abyss = C(0.40f, 0.18f, 0.08f),
            Selection = C(0.70f, 0.95f, 1.00f), LegalMove = C(0.65f, 0.88f, 1.00f), Capture = C(1.00f, 0.42f, 0.18f),
            LastMove = C(1.00f, 0.62f, 0.28f), Check = C(1.00f, 0.06f, 0.10f),
        };
    }
}
