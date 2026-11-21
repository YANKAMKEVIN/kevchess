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

        /// <summary>
        /// Le binaire est recompilé depuis ce code source, avec la recette de la release officielle
        /// stockfish-android-arm64-universal (NDK r27c, ARCH=arm64-universal, PGO, réseau intégré), plus
        /// -Wl,-z,max-page-size=16384 à l'édition de liens finale : segments ELF alignés sur 16 Kio
        /// (exigence Google Play). Script reproductible : tools/build-stockfish-android.sh.
        /// </summary>
        public const string BuildScript = "tools/build-stockfish-android.sh";
        public const string BuildArch = "arm64-universal";
        public const string NdkVersion = "27.2.12479018";
        public const int PageAlignment = 16384;
        /// <summary>Réseau NNUE intégré au binaire (celui du tag sf_19).</summary>
        public const string Network = "nn-1a298aa575a0.nnue";
        /// <summary>Signature « bench » du tag sf_19, vérifiée sous qemu par le script de build.</summary>
        public const long BenchSignature = 2497913;

        /// <summary>Binaire produit par le script, renommé libstockfish.so.</summary>
        public const string BinarySha256 = "887b58a0409044ff1c3caa200011686d6ca9ac0f5ab92a981331861aab2231eb";
        public const long BinarySize = 100683680;

        /// <summary>Code source correspondant exactement au binaire embarqué.</summary>
        public const string SourceUrl = "https://github.com/official-stockfish/Stockfish/tree/" + Tag;
        public const string SourceArchive = "https://github.com/official-stockfish/Stockfish/archive/refs/tags/" + Tag + ".tar.gz";

        public const string LicenseName = "GNU General Public License v3 (GPL v3)";
        public const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";

        /// <summary>Texte de l'écran « Licences ».</summary>
        public static string Notice =>
            "KevChess intègre " + Version + " (Android arm64, variante universelle, compilé sans modification "
            + "depuis le code source officiel, segments alignés sur 16 Kio), "
            + "moteur d'échecs libre écrit par les auteurs de Stockfish "
            + "(liste : fichier AUTHORS du code source).\n\n"
            + "Stockfish est distribué sous la " + LicenseName + ", sans aucune garantie. "
            + "Vous pouvez le redistribuer et le modifier selon les termes de cette licence. "
            + "Comme l'app est distribuée avec Stockfish, l'app entière relève de la GPL v3.\n\n"
            + "Code source exact de la version embarquée (tag " + Tag + ", commit " + Commit.Substring(0, 12) + ") :\n"
            + SourceUrl + "\n\n"
            + "Texte de la licence : " + LicenseUrl + "\n"
            + "(aussi dans le dépôt du jeu : LICENSES/GPL-3.0.txt et THIRD_PARTY_NOTICES.md ; "
            + "options de compilation et script de build : " + BuildScript + ")";
    }
}
