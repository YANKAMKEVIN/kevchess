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

Perf IA (Mac, .NET) : ~1,1–1,5 M nœuds/s, profondeur 10 en < 1 s depuis la position initiale. Non mesurée sur téléphone.

## Builds et recette (2026-10-31)

Licence Unity Personal active (activée en CLI : `unity auth login` puis `unity license activate --personal --accept-eula`).

| Cible | Résultat |
|---|---|
| macOS (Mono, `Builds/macOS/KevChess.app`, 79 Mo) | Build OK, autotest vert : 16 coups scriptés (prises, roque, promotion), annulation, mat, partie contre l'IA (1er coup IA en 0,5 s) |
| Itération 2026-10-31 (glisser-déposer, pièces prises en 3D, vibrations, menu masqué sous l'écran IA) | Mac : autotest vert, pièces prises visibles sur les côtés en paysage. Android : APK installé sur le S24, bouton Vibrations vu au menu ; reste à tester au doigt (glisser, vibrations, pièces prises en portrait, écran IA) — le S24 était occupé par une autre session |
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
- [ ] Mesurer l'IA sur téléphone, ajuster les temps par niveau
- [ ] Module iOS + build Xcode
- [ ] V1.1 : Stockfish via `IChessEngine` (AIAdapter est le seul point à changer)
- [x] Glisser-déposer, pièces prises en 3D, vibrations
- [ ] Idées : horloge, thèmes, analyse de partie, export PGN
