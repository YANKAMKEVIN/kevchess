namespace KevChess.AI.Uci
{
    /// <summary>
    /// Licence du code de KevChess : GNU GPL v3 (décision de Kevin du 2026-11-22, l'APK embarquant Stockfish
    /// sous GPL v3). Texte complet dans le fichier LICENSE à la racine du dépôt. Affiché dans l'écran
    /// « Licences » avec la notice de Stockfish.
    /// </summary>
    public static class KevChessLicense
    {
        public const string Spdx = "GPL-3.0-only";
        public const string Copyright = "Copyright (C) 2026 les auteurs de KevChess";
        /// <summary>Dépôt du code source (visible de tous une fois rendu public par Kevin).</summary>
        public const string SourceUrl = "https://github.com/YANKAMKEVIN/kevchess";
        public const string LicenseFile = "LICENSE";

        /// <summary>Paragraphe sur KevChess lui-même.</summary>
        public static string Notice =>
            "KevChess est un logiciel libre. " + Copyright + ".\n"
            + "Vous pouvez le redistribuer et le modifier selon les termes de la " + StockfishBundle.LicenseName
            + " publiée par la Free Software Foundation. Il est distribué sans aucune garantie.\n"
            + "Code source : " + SourceUrl + " (licence complète : fichier " + LicenseFile + ").";

        /// <summary>Texte complet de l'écran « Licences » : KevChess, puis Stockfish.</summary>
        public static string FullNotice => Notice + "\n\n" + StockfishBundle.Notice;
    }
}
