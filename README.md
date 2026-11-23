# KevChess

Jeu d'échecs 3D pour Android, iOS et macOS, en direction artistique « cristal / néon », entièrement généré
par code (pièces, plateau, effets, sons). Unity 6 + URP.

- Partie à deux en local ou contre l'IA maison (5 niveaux), horloge, puzzles, analyse de partie, export PGN.
- Mode **Légendes** : pouvoirs (Bouclier, Gel, Saut, Dragon), plateau vivant, duels de capture.
- Moteur **Stockfish 19** au choix dans les Réglages (APK Android).

État détaillé, architecture et décisions : [`docs/tech/etat-projet.md`](docs/tech/etat-projet.md).

## Tests

Les règles (`Assets/KevChess/Core`) et l'IA (`Assets/KevChess/AI`) n'ont aucune dépendance à Unity :

```sh
dotnet test tests/KevChess.Tests/KevChess.Tests.csproj
```

## Licence

KevChess est un logiciel libre, publié sous la **GNU General Public License version 3 ou (à votre choix)
toute version ultérieure** (GPL v3 ou ultérieure, SPDX `GPL-3.0-or-later`), comme Stockfish. Copyright (C) 2026
les auteurs de KevChess. Texte complet de la GPL v3 dans [`LICENSE`](LICENSE). Vous pouvez le redistribuer et le
modifier selon ses termes ; il est fourni sans aucune garantie. Code source : https://github.com/YANKAMKEVIN/kevchess

L'APK Android embarque **Stockfish 19**, lui aussi sous GPL v3 : auteurs, code source exact et recette de
compilation dans [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) et [`LICENSES/`](LICENSES). Les deux notices
s'affichent dans l'app : Réglages → « Licences : KevChess et Stockfish (GPL) ».
