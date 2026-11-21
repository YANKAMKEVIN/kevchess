# KevChess — état du projet

Jeu d'échecs 3D original pour Android, iOS et macOS. Direction artistique « cristal / néon », tout généré par code (aucun modèle ni son importé).

Dernière mise à jour : 2026-11-21.

Dépôt : https://github.com/YANKAMKEVIN/kevchess (privé).

## Décisions prises (avec Kevin)

| Sujet | Décision |
|---|---|
| Moteur | Unity 6000.6.3f1 + URP (installé dans `/Applications/Unity/Hub/Editor/6000.6.3f1`, modules Android + Mac ; module iOS à ajouter) |
| Modes V1 | Local 2 joueurs + IA maison (5 niveaux) |
| Stockfish | **Intégré à l'APK Android le 2026-11-21** (décision de Kevin, en connaissance de cause : GPL v3, poids) ; l'IA maison reste le moteur par défaut, Stockfish se choisit dans les Réglages |
| Plus tard | Jeu en ligne en V2 |
| DA | Cristal / néon futuriste, la 3D doit se ressentir (perspective, reflets, ombres, bloom, parallaxe) |
| Caméra | Orbite libre (glisser), pincer/molette = zoom, double-tap = recentrer ; bouton « Vue » |
| Contrôles | Toucher une pièce puis une case (coups légaux surlignés), ou glisser-déposer la pièce ; un geste commencé hors d'une pièce jouable fait tourner la caméra |
| Identifiant | `com.kevinyankam.kevchess` |

## Architecture

```
Assets/KevChess/
  Core/      règles pures (C#, sans UnityEngine) : Position, MoveGenerator, ChessGame, Notation (SAN/UCI), FEN, PGN
  AI/        IA pure : Searcher (alpha-bêta, TT, quiescence, null-move, LMR), Evaluator, OpeningBook, ChessAI (IChessEngine)
  AI/Uci/    moteur UCI externe (Stockfish) : UciProtocol (commandes, info, bestmove, niveaux), UciEngine (IChessEngine), ProcessUciTransport, FallbackEngine, StockfishBundle (version, empreintes, notice GPL du binaire Android)
  Game/
    Visuals/ pièces procédurales, plateau verre/abîme, matériaux néon, effets, ambiance URP (SceneAtmosphere)
    View/    BoardView : synchronise la vue 3D avec la partie (animations, surbrillances, picking)
    Match/   MatchController (tours humain/IA, annulation, indice, sauvegarde PlayerPrefs), BoardInteraction, AIAdapter, AndroidUciTransport (Stockfish lancé par ProcessBuilder)
    Input/   PointerInput (tactile EnhancedTouch + souris → tap / double-tap / glisser / pincer)
    Camera/  OrbitCamera (orbite lissée, cadrage portrait/paysage, respiration, parallaxe accéléromètre)
    UI/      Hud (uGUI construit par code : bandeau joueurs + horloge, statut, coups, ouverture, menu, IA, promotion, fin de partie), Hud.Screens (réglages, puzzles, analyse, statistiques, nulle), Hud.Review (mode revue), GamePrefs (préférences PlayerPrefs)
    Audio/   SoundBank (sons de verre/cristal synthétisés), Haptics (vibrations Android, désactivables)
    App/     KevChessApp : point d'entrée, se crée seul au chargement de la scène
    Legends/ mode Légendes : LegendsMode (bascule), LegendsController, LegendsBoardView (personnages, duels, DuelCamera), LivingBoard, LegendsHud, LegendsInteraction
  Core/Fantasy/  règles du mode Légendes (FantasyPosition, FantasyAction, FantasyMoveGenerator, EventPlanner, FantasyGame)
  AI/Fantasy/    IA du mode Légendes (FantasyEvaluator, FantasySearcher, FantasyAI)
  Game/Visuals/Fantasy/  personnages procéduraux (CharacterRig, FantasyPieceFactory, FantasyPieceView, FantasyFx)
  Editor/    BuildScript (configuration + builds Android/macOS en ligne de commande)
tests/KevChess.Tests/  tests NUnit hors Unity (Core + AI)
```

`Core` et `AI` n'ont aucune dépendance à Unity : ils se testent avec le SDK .NET 8 livré dans Unity.

## Tests

```bash
cd ~/dev/kevchess/tests/KevChess.Tests && DOTNET_CLI_TELEMETRY_OPTOUT=1 /Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet test
```

347 tests verts (≈ 30 s ; 297 avant l'adaptateur UCI, 44 tests UCI ajoutés le 2026-11-21, 1 pour le coût du dragon, 5 pour le binaire Stockfish embarqué (empreinte, alignement 16 Kio, recette de build) et ses licences), dont 42 pour le mode Légendes (règles, équivalence perft avec le classique sans énergie, plateau vivant, sauvegarde, IA). Classique : perft sur 6 positions de référence (jusqu'à profondeur 5), roque, en passant, promotion, clouage, mat, pat, 50 coups, triple répétition, matériel insuffisant, SAN, PGN, hachage incrémental ; IA : mats en 1/2, pas de dame en prise, pat évité, coups légaux, limite de temps, annulation, symétrie de l'évaluation, Maître bat Débutant.

Perf IA (Mac, .NET) : ~1,1–1,5 M nœuds/s, profondeur 10 en < 1 s depuis la position initiale.

Temps de réflexion par niveau (bench `AIBench`, 6 positions fixes, livre coupé) :

| Niveau (plafond) | Galaxy A17 (IL2CPP, MT6789) | Mac M3 (Mono) |
|---|---|---|
| Débutant (300 ms, prof. 2) | moy. 27 ms, pire 141 ms | moy. 22 ms |
| Facile (500 ms, prof. 3) | moy. 61 ms, pire 267 ms | moy. 73 ms |
| Moyen (900 ms, prof. 5) | moy. 325 ms, pire 742 ms, prof. 5 | moy. 325 ms |
| Fort (1500 ms, prof. 9) | moy. 1225 ms, pire 1501 ms, prof. 5–8 | moy. 841 ms |
| Maître (3000 ms) | moy. 2117 ms, pire 3001 ms, prof. 9–14 | moy. 1893 ms |

~300–480 k nœuds/s sur l'A17. Budgets gardés tels quels : rien ne dépasse 3 s sur le téléphone le plus lent, et les niveaux bas restent rythmés par l'attente minimale de 0,45 s (`MatchController.MinAIThinkSeconds`). S24 non mesuré (débranché), forcément plus rapide.
Lancer le bench : `adb shell am start -S -n com.kevinyankam.kevchess/com.unity3d.player.UnityPlayerGameActivity -e kevchess aibench` puis `adb logcat -s Unity | grep bench` (Mac : `-kevchess-aibench`). En partie, chaque coup de l'IA écrit `[KevChess] IA <niveau> : <ms> (profondeur, nœuds…)`.

## Les 20 améliorations (2026-11-01)

Logique (Core/AI, testée) : horloge `ChessClock` + cadences `TimeControl.Presets` (Sans limite, 1+0, 3+2, 5+0, 10+5, 15+10 ; chute au temps → nulle si l'adversaire ne peut pas mater) ; `ChessGame.PositionAt` (revue) ; notation française `Notation.Localize` ; noms d'ouvertures `OpeningNames` (90 lignes) ; analyse `GameAnalyzer` (précision par camp, excellent / bon / imprécision / erreur / gaffe, meilleur coup) ; 32 puzzles `PuzzleSet` (mats en 1/2/3, chacun validé par test) + `PuzzleSession` ; nulle proposée à l'IA `ChessAI.AcceptsDrawAsync`.
Visuels et son : 4 thèmes `VisualTheme` (Néon, Aurore, Émeraude, Glace & Braise) + lisibilité renforcée ; animation de mat (roi qui bascule et se brise, onde de choc, étincelles) ; flèche 3D d'indice ; cavalier qui saute en arc, traînées lumineuses ; `BoardView.AnimationSpeed` ; musique d'ambiance générative `AmbientMusic` (coupée par défaut) ; sons roque, promotion, mat, chute, tic d'horloge, puzzle.
Interface : horloge dans les cartes (alerte sous 10 s) et choix de la cadence ; mode revue ⏮ ◀ ▶ ⏭ (+ « Reprendre d'ici ») ; écran d'analyse (toucher une gaffe → revue avec flèche du meilleur coup) ; puzzles (liste, progression, indice, suivant) ; Réglages (son, vibrations, thème, lisibilité, notation, vitesse, musique + volume, caméra qui se retourne en partie à deux) ; statistiques par niveau d'IA ; proposer nulle ; copier le PGN ; « L'IA réfléchit… » ; nom de l'ouverture. Actions secondaires dans « Plus… » (barre du bas).
Vérifié : 195 tests (avant fusion des Légendes), autotest Mac étendu vert (revue, analyse, mat animé, chute au temps, nulle, PGN, puzzle juste et faux, changement de thème, partie 3+2 contre l'IA), captures en portrait regardées. Android (A17, APK depuis main avec les Légendes) : nouveau menu (Légendes, Puzzles, Réglages), liste des puzzles, puzzle 1 résolu au glisser (Ta8#, « Résolu ! »). Corrigé après cette recette : en puzzle, les rangées de pièces prises montraient toutes les pièces absentes du jeu complet ; elles comptent maintenant depuis la position de départ de la partie (`BoardView.SetTrayBaseline`). Non vérifié : sons et musique à l'oreille (synthétisés, jamais écoutés), recette tactile complète des nouveaux écrans sur téléphone.

## Stockfish (V1.1, 2026-11-21)

Fait (C# pur dans `AI/Uci`, testé hors Unity avec un faux moteur UCI en mémoire et un faux moteur en shell lancé comme processus) :
- `UciProtocol` : `position startpos|fen ... moves ...` (l'historique est envoyé : le moteur voit les répétitions), `go movetime` / `go depth` / `go wtime btime winc binc`, `setoption`, lecture de `bestmove` (avec `ponder`, `(none)`) et de `info` (profondeur, score cp ou mat ramené à l'échelle de `Searcher`, bornes ignorées, nœuds, nps, temps, variante, `info string`).
- `UciEngine : IChessEngine` : poignée de main (`uci`/`uciok`, `Threads` 1, `Hash` 16 Mo, `isready`), `ucinewgame` à chaque nouvelle partie, `Skill Level` selon le niveau, annulation par `stop` en restant synchronisé, `stop` forcé si le moteur dépasse son temps (+1,5 s), moteur muet ou arrêté → hors service. Coup illégal refusé.
- `FallbackEngine` : si Stockfish manque, échoue ou rend un coup nul en cours de partie, l'IA maison joue le coup. L'indice et la proposition de nulle restent à l'IA maison.
- Réglage « Moteur de l'IA » (Réglages, `GamePrefs.Engine`, clé `kevchess.engine`) : KevChess (intégrée, **par défaut**) ou Stockfish ; affiche « Stockfish absent : KevChess » sans binaire.
- `AIAdapter` cherche le binaire. Mac / PC / éditeur : argument `-kevchess-stockfish <chemin>`, variable `KEVCHESS_STOCKFISH`, ou `StreamingAssets/stockfish/stockfish[.exe]` (essai sur Mac : `brew install stockfish` puis `-kevchess-stockfish /opt/homebrew/bin/stockfish`). Android : `nativeLibraryDir/libstockfish.so` (ci-dessous). iOS : aucun (lancer un exécutable y est interdit).

Niveaux Stockfish (valeurs de départ, `UciProtocol.Levels`) : même temps que l'IA maison (300 / 500 / 900 / 1500 / 3000 ms) ; `Skill Level` 0 / 3 / 8 / 14 / 20 ; profondeur max 1 et 3 pour Débutant et Facile.

### Stockfish dans l'APK Android (2026-11-21, décision de Kevin)

- **Binaire** : Stockfish **19** (tag officiel `sf_19` du 2026-09-05, la plus récente), variante Android `arm64-universal` (choisit à l'exécution entre armv8 et armv8-dotprod ; depuis Stockfish 19 c'est la seule variante arm64 publiée pour Android). **Recompilé le 2026-11-21 pour l'alignement 16 Kio** (voir ci-dessous) : code source du tag non modifié, recette de la release officielle, empreintes figées dans `THIRD_PARTY_NOTICES.md`, `StockfishBundle` et les tests (binaire `887b58a0…`, 100 683 680 octets). Le binaire officiel d'origine (`ffd8fc20…1daf8`, aligné sur 4 Kio) est remplacé.
- **Format vérifié** : `ELF 64-bit LSB executable, ARM aarch64, statically linked, stripped`, **100 695 968 octets (96 Mio)**, réseaux NNUE inclus. Segments alignés sur 4 Kio.
- **Place** : `Assets/Plugins/Android/libs/arm64-v8a/libstockfish.so` (importé comme plugin Android ARM64). Android extrait les bibliothèques natives, exécutables, dans `nativeLibraryDir` : c'est le seul endroit d'où l'app peut lancer un binaire (depuis Android 10, un fichier copié dans les données de l'app n'est plus exécutable). Pour que l'extraction ait lieu, `Editor/StockfishAndroidPackaging` (après génération du projet Gradle) met `packagingOptions.jniLibs.useLegacyPackaging = true` dans le module launcher.
- **Lancement** : `AndroidUciTransport` (`Game/Match`) lance le binaire par `java.lang.ProcessBuilder` (`System.Diagnostics.Process` n'existe pas sous IL2CPP Android) ; deux fils dédiés, attachés à la JVM, écrivent les commandes et lisent la sortie. `AIAdapter` résout le chemin au démarrage (fil principal) et branche ce transport dans `UciEngine`. Binaire absent, refusé ou planté → « Stockfish absent : KevChess » dans les Réglages et l'IA maison joue (`FallbackEngine`, déjà en place).
- **Git** : le dépôt n'utilise pas Git LFS (aucun `.gitattributes`) ; le binaire est donc commité tel quel, sans règle LFS ajoutée. 96 Mio passent sous la limite GitHub de 100 Mio par fichier, mais chaque mise à jour du binaire alourdit l'historique d'autant : pour en changer souvent, passer ce fichier en LFS.
- **Poids** : l'APK grossit d'environ 75 à 80 Mo (le binaire compressé dans l'APK ; 96 Mio une fois installé, extrait sur le téléphone), soit plus que les 31–36 Mo actuels.
- **Licence (GPL v3)** : texte de la licence dans `LICENSES/GPL-3.0.txt`, auteurs dans `LICENSES/Stockfish-AUTHORS.txt`, notice et lien vers le code source exact (`https://github.com/official-stockfish/Stockfish/tree/sf_19`, commit `edb0d9db6731`) dans `THIRD_PARTY_NOTICES.md` et dans l'app : Réglages → « Licences (Stockfish, GPL v3) » (nouvel écran, il n'y avait pas d'écran « À propos »).
- **⚠ Pour Kevin : l'application entière, distribuée avec Stockfish, relève de la GPL v3.** Toute personne qui reçoit l'APK (même un ami à qui tu l'envoies) a droit au code source complet, Stockfish **et KevChess**, sous GPL v3, et peut le redistribuer. Le dépôt KevChess est privé aujourd'hui : avant de distribuer l'APK hors usage personnel, il faudra publier (ou fournir sur demande) le code de KevChess sous GPL v3, ou retirer Stockfish de l'APK.
- **Non vérifié** (ni Unity ni Android ici) : build Unity avec le plugin, extraction réelle dans `nativeLibraryDir`, lancement et partie contre Stockfish sur téléphone, écran « Licences ». Vérifié : format du binaire, empreintes, compilation hors moteur du code Unity (assemblies de référence, Android et Mac), patch Gradle sur des fichiers d'exemple, suite de tests.
- **Alignement 16 Kio (Google Play), réglé le 2026-11-21** : le binaire officiel avait ses segments `LOAD` alignés sur 4 Kio (`p_align` 0x1000, et décalages non congrus aux adresses modulo 16 Kio) : ce n'était pas qu'une question de zip (`useLegacyPackaging` extrait déjà le fichier), un noyau à pages de 16 Ko ne pouvait pas le charger et Play le refuse. Il est donc recompilé par `tools/build-stockfish-android.sh` : NDK r27c (27.2.12479018, celui de la release officielle), `make profile-build ARCH=arm64-universal COMP=ndk` avec `LDFLAGS="-static ..."` (recette de `arm_compilation.yml` au tag `sf_19`, PGO sous qemu), puis une nouvelle édition de liens finale avec `-Wl,-z,max-page-size=16384`, `make strip`. Vérifié ici : 4 segments `LOAD` à 0x4000, ELF aarch64 statique, signature `bench` 2497913 identique au tag, sous qemu `uci` puis `go depth 10` → `bestmove e2e4`. Test `LoadSegmentsAreAlignedOn16KiB` ajouté. Le réseau `nn-1a298aa575a0.nnue` n'étant pas téléchargeable depuis ce conteneur (proxy), il a été extrait du binaire officiel et vérifié octet pour octet (SHA-256 complet identique à celui du dépôt official-stockfish/networks) ; ailleurs, `make net` le télécharge. Reste pour Kevin : essai sur téléphone (et idéalement sur un appareil ou émulateur à pages de 16 Ko), puis la vérification d'alignement de la Play Console.

## Auto-parties du mode Légendes (2026-11-21)

Test explicite `FantasyAITests.SelfPlayBalance` (30 parties IA Fort contre IA Fort, 40 ms par action, graines fixes), lancé hors Unity :
`dotnet test tests/KevChess.Tests --filter Name=SelfPlayBalance --logger "console;verbosity=detailed"` (≈ 80 s).

| Mesure | Résultat |
|---|---|
| Résultats | blancs 17, noirs 13, 0 nulle ; 66 demi-coups en moyenne, toutes les parties finies par mat |
| Pouvoirs par partie | Bouclier 5,5 ; Gel 3,7 ; Téléportation 1,3 ; Invocation du dragon 0,2 ; Souffle 0,1 |
| Événements par partie | fissures 5,1 ; portails 2,6 ; gisements 2,0 |

Constat : le dragon n'apparaissait presque jamais (5 invocations en 30 parties) : l'IA dépensait ses éclats en Boucliers et Gels avant d'atteindre 10.

**Décision de Kevin (2026-11-21) : baisser le coût de l'invocation.** Balayage par auto-parties (test explicite `FantasyAITests.SummonCostSweep`, coûts 4 à 10, 30 ou 60 parties chacun, ≈ 1 min 30 par série de 30 parties) : coût **10 → 5** (`FantasyConfig.SummonCost`). Mesuré à 5 : 1,3 puis 0,8 invocation par partie (≈ 1,0 sur 90 parties, contre 0,1 à 10 sur la même machine) ; Bouclier 4,0, Gel 1,9, Téléportation 0,3, Souffle 0,4 par partie ; 45 demi-coups en moyenne ; blancs 32, noirs 28. Détail par coût : `docs/design/mode-fantastique.md` § 8. Les règles affichées dans l'écran du mode reprennent le coût. À confirmer en jouant (ressenti de Kevin).

## Builds et recette (2026-10-31)

Licence Unity Personal active (activée en CLI : `unity auth login` puis `unity license activate --personal --accept-eula`).

| Cible | Résultat |
|---|---|
| macOS (Mono, `Builds/macOS/KevChess.app`, 79 Mo) | Build OK, autotest vert : 16 coups scriptés (prises, roque, promotion), annulation, mat, partie contre l'IA (1er coup IA en 0,5 s) |
| Itération 2026-10-31 (glisser-déposer, pièces prises en 3D, vibrations, menu masqué sous l'écran IA) | Mac : autotest vert, pièces prises visibles sur les côtés en paysage. Android (S24 `RFCX80Y0FVX`, paysage) : écran IA sans menu derrière, glisser-déposer (1. e4 d5 2. exd5), pion pris affiché à côté du plateau, dépôt interdit → pièce renvoyée, glisser sur une case vide → orbite, vibrations confirmées dans `dumpsys vibrator_manager` (8/16/32/22 ms). Non vu : pièces prises en portrait (téléphone resté en paysage) ; après une orbite de 90° les pièces prises peuvent sortir du cadre jusqu'au bouton « Vue » |
| Itération 2026-10-31 (pièces prises après orbite, temps IA) | Les rangées de pièces prises changent d'axe selon le quart de tour de la caméra (hystérésis de 10°) et le cadrage suit l'orbite en gardant le zoom (relatif au cadrage exact ; « Vue » le remet à 1). Autotest Mac : orbites 90° et 40° avec pièces prises vérifiées dans le cadre. Galaxy A17 en portrait : rangées au-dessus et au-dessous du plateau, visibles après des orbites de ~60° et ~120°. Rangée proche d'abord peu lisible (vue de dessus) : en portrait, pièces prises plus grosses (0,68) et rapprochées du cadre, et toutes les pièces prises se penchent en arrière (pivot à la base, jusqu'à 35°) quand on les voit de trop haut, pour montrer leur profil. Vérifié sur l'A17 (portrait) et le Mac (paysage). S24 non revu (débranché) |
| Android (IL2CPP arm64, `Builds/Android/KevChess.apk`, 31 Mo) | Build OK, installé sur **Galaxy A17** (SM-A175F, `adb -s R5GYB2R7W2Y`, accord de Kevin) : rendu 3D, menu, sélection au toucher, coups légaux affichés, 1. e4 e5 joués |

Commandes :
```bash
U=/Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity
$U -batchmode -quit -projectPath ~/dev/kevchess -buildTarget OSXUniversal -executeMethod KevChess.EditorTools.BuildScript.BuildMac -logFile Logs/build-mac.log
$U -batchmode -quit -projectPath ~/dev/kevchess -buildTarget Android -executeMethod KevChess.EditorTools.BuildScript.BuildAndroid -logFile Logs/build-android.log
~/Library/Android/sdk/platform-tools/adb -s R5GYB2R7W2Y install -r Builds/Android/KevChess.apk
# Recette automatique avec captures (Mac) :
Builds/macOS/KevChess.app/Contents/MacOS/KevChess -kevchess-autotest <dossier> -logFile <dossier>.log
```

Pièges rencontrés :
- En batchmode, toujours passer `-buildTarget` : `BuildScript` s'arrête (code 3) si la cible active ne correspond pas.
- Désactiver le stripping des variantes de post-traitement fait perdre les ressources SSAO : la feature SSAO du renderer PC (modèle URP) faisait planter le pipeline sur Mac (écran noir). Feature retirée.
- Le compilateur de shaders Unity plante parfois (« Lost connection with shader compiler ») : relancer le build.
- URP 17 strippait toutes les variantes de `UberPost` sur Android (écran noir derrière l'UI) : « Strip Unused Post Processing Variants » désactivé dans `UniversalRenderPipelineGlobalSettings.asset`.
- Mac : seul le module Mono est installé (pas IL2CPP).
- L'app Mac se met en pause sans le focus (`runInBackground = false`) ; l'autotest force `runInBackground`.
- La config URP (Packages, ProjectSettings, Assets/Settings) vient du modèle `com.unity.template.urp-blank` (qualités Mobile et PC).

## Prochaines étapes

- [x] Licence + premier lancement, corrections de la revue
- [x] Prototype jouable sur Mac et APK installé sur le téléphone (Galaxy A17)
- [ ] Passe QA manuelle par Kevin (IA sur téléphone, gestes caméra, sons, promotion, sauvegarde)
- [x] Cadrage exact par projection (plateau pleine largeur en portrait, centré hors interface)
- [ ] Réglages visuels réels (bloom, reflets, lisibilité des pièces de dessus en portrait)
- [x] Mesurer l'IA sur téléphone (A17) ; budgets jugés corrects, inchangés
- [ ] Revoir sur le S24 : portrait, pièces prises, temps IA
- [x] Pièces prises en portrait : rangée proche lisible (plus grosse, penchée pour montrer son profil)
- [ ] Module iOS + build Xcode
- [x] V1.1 : Stockfish via `IChessEngine` : adaptateur UCI, réglage « Moteur de l'IA » et binaire Android (Stockfish 19) intégrés le 2026-11-21, binaire recompilé aligné sur 16 Kio pour Google Play (voir « Stockfish (V1.1) ») ; reste l'essai sur téléphone
- [x] Glisser-déposer, pièces prises en 3D, vibrations
- [ ] Mode Légendes (`docs/design/mode-fantastique.md`) : **fusionné dans `main`** (la branche `feature/legendes` n'a plus aucun commit d'avance, vérifié le 2026-11-21 ; ses tests passent avec le reste). Équilibrage : coût de l'invocation du dragon baissé de 10 à 5 le 2026-11-21 (décision de Kevin, voir « Auto-parties du mode Légendes ») ; il reste la recette S24. Historique : règles, plateau vivant, IA, personnages procéduraux, duels de capture, plateau vivant visuel et interface faits ; bouton « Légendes » dans le menu. Recette : `-kevchess-legends-autotest <dossier>`. Recette Android 2026-11-02 sur le Galaxy A17 (APK IL2CPP 36 Mo) : autotest Légendes vert (duel, invocation, Bouclier, Gel, téléportation, plateau vivant, partie contre l'IA ; IA Moyen profondeur 5 en ≈ 0,2 s par coup). Sur Android, `-kevchess-legends-autotest` se passe par `adb shell am start ... -e unity "-kevchess-legends-autotest"` ; ses captures restent dans le stockage privé de l'app (non debuggable) : faire des `adb exec-out screencap` pendant le test. À revoir : plateau petit en portrait sur l'A17 (barre des pouvoirs en plus du bandeau). Reste : S24, équilibrage par auto-parties IA contre IA, retouches visuelles (chevalier, sentinelle, reflets des personnages). IA : profondeur 9 en 0,5 s au départ (classique 10), 6 en milieu de partie avec pouvoirs
- [x] Horloge, thèmes, analyse de partie, export PGN, puzzles, réglages (les 20 améliorations)
- [ ] Écouter les sons et la musique d'ambiance, recette tactile des nouveaux écrans sur téléphone
