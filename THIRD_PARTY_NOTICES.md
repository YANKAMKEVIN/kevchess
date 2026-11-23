# Logiciels tiers distribués avec KevChess

## Stockfish 19 (application Android)

L'APK Android embarque le moteur d'échecs **Stockfish 19**, lancé comme processus séparé et piloté par le
protocole UCI (réglage « Moteur de l'IA » ; l'IA maison reste le moteur par défaut). Le binaire est
**compilé par nos soins, sans aucune modification du code source**, depuis le tag officiel `sf_19`, avec la
recette de la release officielle `stockfish-android-arm64-universal` et une seule option ajoutée à
l'édition de liens (alignement des segments sur 16 Kio, exigé par Google Play).

| | |
|---|---|
| Fichier dans le dépôt | `Assets/Plugins/Android/libs/arm64-v8a/libstockfish.so` (exécutable ELF aarch64 statique, renommé pour être livré comme bibliothèque native) |
| Binaire | 100 683 680 octets — SHA-256 `887b58a0409044ff1c3caa200011686d6ca9ac0f5ab92a981331861aab2231eb` — segments `LOAD` alignés sur 16 Kio (`p_align` 0x4000) |
| Code source exact | https://github.com/official-stockfish/Stockfish/tree/sf_19 (commit `edb0d9db6731067ec50ce619ff372b463bc4dd5d`, non modifié) ; archive : https://github.com/official-stockfish/Stockfish/archive/refs/tags/sf_19.tar.gz |
| Réseau NNUE intégré | `nn-1a298aa575a0.nnue` (réseau par défaut du tag, SHA-256 `1a298aa575a085434d29027978dc36867fe9c5bcea9376654b7a8eba1e52dfc2`, 98 511 183 octets), obtenu par `make net` (https://tests.stockfishchess.org/api/nn/nn-1a298aa575a0.nnue ou le dépôt official-stockfish/networks) |
| Script de build | `tools/build-stockfish-android.sh` (dans ce dépôt) |
| Auteurs | Les auteurs de Stockfish : `LICENSES/Stockfish-AUTHORS.txt` (fichier `AUTHORS` du code source) |
| Licence | GNU General Public License version 3 : `LICENSES/GPL-3.0.txt` (fichier `Copying.txt` du code source) |

### Reproduire le binaire

Sur Linux x86-64, avec `git`, `make`, `python3`, `qemu-user`, `bsdextrautils` (hexdump), `readelf` et le
NDK Android **r27c** (`sdkmanager "ndk;27.2.12479018"`) :

```sh
ANDROID_HOME=~/android-sdk tools/build-stockfish-android.sh
```

Le script fait exactement ceci (recette de `.github/workflows/arm_compilation.yml` au tag `sf_19`) :

```sh
git clone --depth 1 --branch sf_19 https://github.com/official-stockfish/Stockfish.git   # vérifie le commit edb0d9db…
cd Stockfish/src && make net                                                           # nn-1a298aa575a0.nnue
export PATH=$NDK/toolchains/llvm/prebuilt/linux-x86_64/bin:$PATH                       # aarch64-linux-android29-clang++ (clang 18.0.3)
export LDFLAGS="-static -Wno-unused-command-line-argument"
make clean
make -j profile-build ARCH=arm64-universal COMP=ndk RUN_PREFIX=qemu-aarch64            # PGO sous qemu, variantes armv8 et armv8-dotprod
rm -f stockfish
make profile-build ARCH=arm64-universal COMP=ndk RUN_PREFIX=qemu-aarch64 \
     EXTRALDFLAGS="-Wl,-z,max-page-size=16384"                                          # seule différence : nouvelle édition de liens finale
make strip ARCH=arm64-universal COMP=ndk
```

puis il vérifie l'alignement de chaque segment `LOAD` (0x4000), la signature `bench` du tag
(**2497913** nœuds, sous qemu) et une réponse UCI (`go depth 10` → `bestmove`). La compilation est
déterministe à options égales, mais l'empreinte du binaire peut varier selon la machine (chemins de
compilation) : le code, les options et la signature `bench` sont ce qui garantit la correspondance.

Le binaire officiel précédemment embarqué (`stockfish-android-arm64-universal.tar.gz` de la release
`sf_19`, binaire SHA-256 `ffd8fc2004d3d19f9fdab95d84c92709aea92e8d3af405eda0b281b23cf1daf8`) était aligné
sur 4 Kio seulement ; il a été remplacé le 2026-11-21.

Stockfish est un logiciel libre : vous pouvez le redistribuer et le modifier selon les termes de la
GNU GPL v3 publiée par la Free Software Foundation. Il est distribué SANS AUCUNE GARANTIE, sans même la
garantie implicite de qualité marchande ou d'adéquation à un usage particulier.

**Conséquence pour KevChess :** l'application distribuée avec Stockfish relève, dans son ensemble, de la
GPL v3. Quiconque reçoit l'APK doit pouvoir obtenir le code source correspondant (celui de Stockfish
ci-dessus et celui de KevChess) sous la même licence.
Le code de KevChess est publié sous GPL v3 ou (à votre choix) toute version ultérieure (`GPL-3.0-or-later`,
fichier `LICENSE` à la racine, https://github.com/YANKAMKEVIN/kevchess).

La même notice s'affiche dans l'app, après celle de KevChess : Réglages → « Licences : KevChess et
Stockfish (GPL) » (`KevChess.AI.Uci.KevChessLicense.FullNotice`).
