namespace KevChess.Core.Ux
{
    /// <summary>
    /// Avant de lancer une nouvelle partie : faut-il confirmer qu'on remplace une partie non terminée ?
    /// Une partie sans coup joué ou déjà finie se remplace sans question.
    /// </summary>
    public static class ReplaceGuard
    {
        /// <summary>Vrai si la partie mérite une confirmation avant d'être remplacée.</summary>
        public static bool NeedsConfirm(ChessGame game) => game != null && !game.IsOver && game.Moves.Count > 0;

        /// <summary>Question posée, avec le numéro du coup en cours (« coup 23 ») ; null si aucune confirmation n'est nécessaire.</summary>
        public static string Question(ChessGame game, bool saved = false)
        {
            if (!NeedsConfirm(game)) return null;
            return (saved ? "Remplacer la partie sauvegardée" : "Remplacer la partie en cours") +
                   "\n(coup " + game.Position.FullmoveNumber + ") ?";
        }
    }
}
