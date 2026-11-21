namespace KevChess.AI.Uci
{
    /// <summary>
    /// Stockfish livré dans l'APK Android (Assets/Plugins/Android/libs/arm64-v8a/libstockfish.so) : version,
    /// provenance, empreinte et notice de licence affichée dans l'écran « Licences ». Stockfish est un logiciel
    /// libre sous GNU GPL v3 ; l'app distribuée avec lui relève de la GPL v3 (docs/tech/etat-projet.md).
    /// À tenir à jour si le binaire change (THIRD_PARTY_NOTICES.md et LICENSES/ en même temps).
    /// </summary>
    public static class StockfishBundle
    {
        public const string Version = "Stockfish 19";
        public const string Tag = "sf_19";
        /// <summary>Commit du tag officiel sf_19 (official-stockfish/Stockfish).</summary>
        public const string Commit = "edb0d9db6731067ec50ce619ff372b463bc4dd5d";

        /// <summary>Archive officielle de la release, qui contient le binaire et son code source (dossier src/).</summary>
        public const string ReleaseAsset =
            "https://github.com/official-stockfish/Stockfish/releases/download/" + Tag + "/stockfish-android-arm64-universal.tar.gz";
        public const string ReleaseAssetSha256 = "ebb24051aa4a222b4daaf049b882ecf1163d370c128fe02316602643f4d5e426";

        /// <summary>Binaire extrait de l'archive (stockfish-android-arm64-universal), renommé libstockfish.so.</summary>
        public const string BinarySha256 = "ffd8fc2004d3d19f9fdab95d84c92709aea92e8d3af405eda0b281b23cf1daf8";
        public const long BinarySize = 100695968;

        /// <summary>Code source correspondant exactement au binaire embarqué.</summary>
        public const string SourceUrl = "https://github.com/official-stockfish/Stockfish/tree/" + Tag;
        public const string SourceArchive = "https://github.com/official-stockfish/Stockfish/archive/refs/tags/" + Tag + ".tar.gz";

        public const string LicenseName = "GNU General Public License v3 (GPL v3)";
        public const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";

        /// <summary>Texte de l'écran « Licences ».</summary>
        public static string Notice =>
            "KevChess intègre " + Version + " (binaire officiel Android arm64, variante universelle), "
            + "moteur d'échecs libre écrit par les auteurs de Stockfish "
            + "(liste : fichier AUTHORS du code source).\n\n"
            + "Stockfish est distribué sous la " + LicenseName + ", sans aucune garantie. "
            + "Vous pouvez le redistribuer et le modifier selon les termes de cette licence. "
            + "Comme l'app est distribuée avec Stockfish, l'app entière relève de la GPL v3.\n\n"
            + "Code source exact de la version embarquée (tag " + Tag + ", commit " + Commit.Substring(0, 12) + ") :\n"
            + SourceUrl + "\n\n"
            + "Texte de la licence : " + LicenseUrl + "\n"
            + "(aussi dans le dépôt du jeu : LICENSES/GPL-3.0.txt et THIRD_PARTY_NOTICES.md)";
    }
}
