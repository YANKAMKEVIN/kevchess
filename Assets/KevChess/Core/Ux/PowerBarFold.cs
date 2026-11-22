namespace KevChess.Core.Ux
{
    /// <summary>
    /// Barre des pouvoirs du mode Légendes, repliable (décision de Kevin du 2026-11-22 : en portrait sur
    /// l'A17, le plateau était petit). Repliée, il ne reste qu'une ligne d'en-tête avec un résumé et le
    /// bouton « Afficher » ; la carte de votre camp descend d'autant et le plateau récupère la place.
    /// Dimensions en pixels de référence (1080 × 1920) du canevas de <c>LegendsHud</c>.
    /// </summary>
    public static class PowerBarFold
    {
        /// <summary>Bas de la barre des pouvoirs (au-dessus des commandes).</summary>
        public const float Bottom = 156f;
        /// <summary>Hauteur dépliée : en-tête, cartes, aide.</summary>
        public const float ExpandedHeight = 230f;
        /// <summary>Hauteur repliée : l'en-tête seul (résumé et bouton).</summary>
        public const float CollapsedHeight = 60f;
        /// <summary>Espace entre la barre et la carte de votre camp.</summary>
        public const float Gap = 18f;

        /// <summary>Par défaut, la barre est dépliée (comportement d'avant).</summary>
        public const bool DefaultCollapsed = false;

        public static float Height(bool collapsed) => collapsed ? CollapsedHeight : ExpandedHeight;

        /// <summary>Bas de la carte de votre camp : 404 dépliée (valeur d'avant), 234 repliée.</summary>
        public static float StripBottom(bool collapsed) => Bottom + Height(collapsed) + Gap;

        /// <summary>Place rendue au plateau quand la barre est repliée (170 px de référence).</summary>
        public static float Gain => ExpandedHeight - CollapsedHeight;

        /// <summary>Texte du bouton : l'action qu'il fera.</summary>
        public static string ToggleLabel(bool collapsed) => collapsed ? "Afficher" : "Replier";

        /// <summary>En-tête de la barre : la consigne dépliée, un résumé repliée.</summary>
        public static string Caption(bool collapsed, int available, string activeTool)
        {
            if (!collapsed) return "Pouvoir facultatif, puis votre coup";
            if (!string.IsNullOrEmpty(activeTool)) return "Pouvoirs · " + activeTool + " en cours";
            if (available <= 0) return "Pouvoirs · aucun disponible";
            return "Pouvoirs · " + available + " disponible" + (available > 1 ? "s" : "");
        }

        /// <summary>Valeur enregistrée (PlayerPrefs) → état ; toute valeur inconnue redonne le défaut.</summary>
        public static bool FromStored(int stored) => stored == 1 ? true : stored == 0 ? false : DefaultCollapsed;

        public static int ToStored(bool collapsed) => collapsed ? 1 : 0;
    }
}
