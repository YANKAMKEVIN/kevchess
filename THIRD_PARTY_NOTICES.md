# Logiciels tiers distribués avec KevChess

## Stockfish 19 (application Android)

L'APK Android embarque le moteur d'échecs **Stockfish 19**, binaire officiel non modifié, lancé comme
processus séparé et piloté par le protocole UCI (réglage « Moteur de l'IA » ; l'IA maison reste le
moteur par défaut).

| | |
|---|---|
| Fichier dans le dépôt | `Assets/Plugins/Android/libs/arm64-v8a/libstockfish.so` (renommé pour être livré comme bibliothèque native) |
| Provenance | Release officielle `sf_19` de [official-stockfish/Stockfish](https://github.com/official-stockfish/Stockfish), archive `stockfish-android-arm64-universal.tar.gz` |
| Archive téléchargée | https://github.com/official-stockfish/Stockfish/releases/download/sf_19/stockfish-android-arm64-universal.tar.gz — SHA-256 `ebb24051aa4a222b4daaf049b882ecf1163d370c128fe02316602643f4d5e426` |
| Binaire (`stockfish-android-arm64-universal`) | 100 695 968 octets — SHA-256 `ffd8fc2004d3d19f9fdab95d84c92709aea92e8d3af405eda0b281b23cf1daf8` |
| Code source exact | https://github.com/official-stockfish/Stockfish/tree/sf_19 (commit `edb0d9db6731067ec50ce619ff372b463bc4dd5d`) ; archive : https://github.com/official-stockfish/Stockfish/archive/refs/tags/sf_19.tar.gz. L'archive de la release contient aussi ce code (dossier `src/`). |
| Auteurs | Les auteurs de Stockfish : `LICENSES/Stockfish-AUTHORS.txt` (fichier `AUTHORS` de la release) |
| Licence | GNU General Public License version 3 : `LICENSES/GPL-3.0.txt` (fichier `Copying.txt` de la release) |

Stockfish est un logiciel libre : vous pouvez le redistribuer et le modifier selon les termes de la
GNU GPL v3 publiée par la Free Software Foundation. Il est distribué SANS AUCUNE GARANTIE, sans même la
garantie implicite de qualité marchande ou d'adéquation à un usage particulier.

**Conséquence pour KevChess :** l'application distribuée avec Stockfish relève, dans son ensemble, de la
GPL v3. Quiconque reçoit l'APK doit pouvoir obtenir le code source correspondant (celui de Stockfish
ci-dessus et celui de KevChess) sous la même licence.

La même notice s'affiche dans l'app : Réglages → « Licences (Stockfish, GPL v3) »
(`KevChess.AI.Uci.StockfishBundle.Notice`).
