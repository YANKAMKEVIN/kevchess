# KevChess — état du projet

Jeu d'échecs 3D original pour Android, iOS et macOS. Direction artistique « cristal / néon », tout généré par code (aucun modèle ni son importé).

Dernière mise à jour : 2026-10-30.

## Décisions prises (avec Kevin)

| Sujet | Décision |
|---|---|
| Moteur | Unity 6000.6.3f1 + URP (installé dans `/Applications/Unity/Hub/Editor/6000.6.3f1`, modules Android + Mac ; module iOS à ajouter) |
| Modes V1 | Local 2 joueurs + IA maison (5 niveaux) |
| Plus tard | Stockfish embarqué en V1.1 (usage perso, GPL acceptée), jeu en ligne en V2 |
| DA | Cristal / néon futuriste, la 3D doit se ressentir (perspective, reflets, ombres, bloom, parallaxe) |
| Caméra | Orbite libre (glisser), pincer/molette = zoom, double-tap = recentrer ; bouton « Vue » |
| Contrôles | Toucher une pièce puis une case (coups légaux surlignés) |
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
    Audio/   SoundBank (sons de verre/cristal synthétisés)
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

## Vérification de compilation sans licence

Le code Unity n'a **jamais tourné** dans l'éditeur. Il compile (0 erreur) contre les DLL UnityEngine + les sources uGUI et Input System livrées avec l'éditeur, via un projet de vérification hors dépôt (scratchpad de session). Non compilés hors éditeur : `SceneAtmosphere.Urp.cs`, `NeonAssetGenerator.Urp.cs`, `Editor/BuildScript.cs` (dépendent d'URP / UnityEditor).

## Bloquant actuel

**Licence Unity** : session expirée, aucune licence active. Kevin doit se connecter dans Unity Hub (licence Personal). Ensuite :

1. Créer un projet depuis le modèle URP dans un dossier temporaire et récupérer `ProjectSettings/`, `Packages/` et les assets URP (`Assets/Settings/`), ou ouvrir `~/dev/kevchess` et ajouter URP + Input System via le Package Manager (API C# Client, pas d'édition à la main de `manifest.json`).
2. Ouvrir une fois le projet : `NeonAssetGenerator` crée `Assets/KevChess/Game/Visuals/Resources/` (gabarits de matériaux, profil de volume) → à committer.
3. `KevChess > Configurer le projet` (scène `Assets/Scenes/Main.unity`, PlayerSettings, HDR/MSAA/ombres URP, variantes de brouillard).
4. Active Input Handling = Input System Package.
5. Builds :
   ```bash
   /Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath ~/dev/kevchess -executeMethod KevChess.EditorTools.BuildScript.BuildMac -logFile -
   /Applications/Unity/Hub/Editor/6000.6.3f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath ~/dev/kevchess -executeMethod KevChess.EditorTools.BuildScript.BuildAndroid -logFile -
   ~/Library/Android/sdk/platform-tools/adb -s RFCX80Y0FVX install -r Builds/Android/KevChess.apk
   ```
6. Passe QA sur Mac puis sur le S24 (ou S21+ `RFCR40827ZZ`) : rendu, 60 fps, gestes, IA, sauvegarde.

## Prochaines étapes

- [ ] Licence + premier lancement, corriger ce que la revue et le premier rendu révèlent
- [ ] Prototype jouable sur Mac et APK installé sur le téléphone
- [ ] Réglages visuels réels (bloom, reflets, lisibilité des pièces de dessus en portrait)
- [ ] Mesurer l'IA sur téléphone, ajuster les temps par niveau
- [ ] Module iOS + build Xcode
- [ ] V1.1 : Stockfish via `IChessEngine` (AIAdapter est le seul point à changer)
- [ ] Idées : glisser-déposer des pièces, horloge, thèmes, analyse de partie, export PGN, haptique
