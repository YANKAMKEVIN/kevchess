# KevChess — état du projet

Jeu d'échecs 3D original pour Android, iOS et macOS. Direction artistique « cristal / néon », tout généré par code (aucun modèle ni son importé).

Dernière mise à jour : 2026-10-31.

Dépôt : https://github.com/YANKAMKEVIN/kevchess (privé).

## Décisions prises (avec Kevin)

| Sujet | Décision |
|---|---|
| Moteur | Unity 6000.6.3f1 + URP (installé dans `/Applications/Unity/Hub/Editor/6000.6.3f1`, modules Android + Mac ; module iOS à ajouter) |
| Modes V1 | Local 2 joueurs + IA maison (5 niveaux) |
| Plus tard | Stockfish embarqué en V1.1 (usage perso, GPL acceptée), jeu en ligne en V2 |
| DA | Cristal / néon futuriste, la 3D doit se ressentir (perspective, reflets, ombres, bloom, parallaxe) |
| Caméra | Orbite libre (glisser), pincer/molette = zoom, double-tap = recentrer ; bouton « Vue » |
| Contrôles | Toucher une pièce puis une case (coups légaux surlignés), ou glisser-déposer la pièce ; un geste commencé hors d'une pièce jouable fait tourner la caméra |
| Identifiant | `com.kevinyankam.kevchess` |

## Architecture

```
Assets/KevChess/
  Core/      règles pures (C#, sans UnityEngine) : Position, MoveGenerator, ChessGame, Notation (SAN/UCI), FEN, PGN
  AI/        IA pure : Searcher (alpha-bêta, TT, quiescence, null-move, LMR), Evaluator, OpeningBook, ChessAI (IChessEngine)
  Game/
    Visuals/ pièces procédurales, plateau verre/abîme, matériaux néon, effets, ambiance URP (SceneAtmosphere)
    View/    BoardView : synchronise la vue 3D avec la partie (animations, surbrillances, picking)
    Match/   MatchController (tours humain/IA, annulation, indice, sauvegarde PlayerPrefs), BoardInteraction, AIAdapter
    Input/   PointerInput (tactile EnhancedTouch + souris → tap / double-tap / glisser / pincer)
    Camera/  OrbitCamera (orbite lissée, cadrage portrait/paysage, respiration, parallaxe accéléromètre)
    UI/      Hud (uGUI construit par code : bandeau joueurs, statut, coups, menu, IA, promotion, fin de partie)
    Audio/   SoundBank (sons de verre/cristal synthétisés), Haptics (vibrations Android, désactivables)
    App/     KevChessApp : point d'entrée, se crée seul au chargement de la scène
  Editor/    BuildScript (configuration + builds Android/macOS en ligne de commande)
tests/KevChess.Tests/  tests NUnit hors Unity (Core + AI)
```

`Core` et `AI` n'ont aucune dépendance à Unity : ils se testent avec le SDK .NET 8 livré dans Unity.

## Tests

```bash
cd ~/dev/kevchess/tests/KevChess.Tests && DOTNET_CLI_TELEMETRY_OPTOUT=1 /Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet test
```

97 tests verts (≈ 6 s) : perft sur 6 positions de référence (jusqu'à profondeur 5), roque, en passant, promotion, clouage, mat, pat, 50 coups, triple répétition, matériel insuffisant, SAN, PGN, hachage incrémental ; IA : mats en 1/2, pas de dame en prise, pat évité, coups légaux, limite de temps, annulation, symétrie de l'évaluation, Maître bat Débutant.

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
- [ ] V1.1 : Stockfish via `IChessEngine` (AIAdapter est le seul point à changer)
- [x] Glisser-déposer, pièces prises en 3D, vibrations
- [ ] Mode fantastique : conception validée dans `docs/design/mode-fantastique.md` (branche `design/mode-fantastique`), prochaine étape = 1 (règles de base dans `Core/Fantasy`)
- [ ] Idées : horloge, thèmes, analyse de partie, export PGN
