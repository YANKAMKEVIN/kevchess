namespace KevChess.AI.Uci
{
    /// <summary>
    /// Licence du code de KevChess : GNU GPL version 3 ou (à votre choix) toute version ultérieure, comme
    /// Stockfish (décisions de Kevin du 2026-11-22). Texte complet de la GPL v3 dans le fichier LICENSE à la
    /// racine du dépôt. Affiché dans l'écran « Licences » avec la notice de Stockfish.
    /// </summary>
    public static class KevChessLicense
    {
        public const string Spdx = "GPL-3.0-or-later";
        public const string Copyright = "Copyright (C) 2026 les auteurs de KevChess";
        public const string LicenseName = "GNU General Public License, version 3 ou (à votre choix) toute version ultérieure (GPL v3 ou ultérieure)";
        /// <summary>Dépôt public du code source.</summary>
        public const string SourceUrl = "https://github.com/YANKAMKEVIN/kevchess";
        public const string LicenseFile = "LICENSE";

        /// <summary>Paragraphe sur KevChess lui-même.</summary>
        public static string Notice =>
            "KevChess est un logiciel libre. " + Copyright + ".\n"
            + "Vous pouvez le redistribuer et le modifier selon les termes de la " + LicenseName
            + ", publiée par la Free Software Foundation. Il est distribué sans aucune garantie.\n"
            + "Code source : " + SourceUrl + " (licence complète : fichier " + LicenseFile + ").";

        /// <summary>Texte complet de l'écran « Licences » : KevChess, puis Stockfish.</summary>
        public static string FullNotice => Notice + "\n\n" + StockfishBundle.Notice;
    }
}
