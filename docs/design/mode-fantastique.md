# KevChess — Mode « Légendes » (document de conception)

Nom à l'écran : **Légendes**. Dans le code et les documents techniques, on garde le mot « fantastique » (`Fantasy`).

Statut : **validé par Kevin le 2026-10-31** ; questions de la section 7 tranchées le même jour.

Dernière mise à jour : 2026-10-31.

## 1. Intention

Toujours des échecs : même plateau, mêmes pièces au départ, mêmes déplacements, victoire par mat. Mais
le plateau vit, les pièces sont incarnées (soldats, mage, golem, reine guerrière, roi titan), elles se
battent quand elles se prennent, et on gagne de l'énergie pour lancer des pouvoirs et réveiller un dragon.

Le mode se choisit au menu (« Classique » / « Légendes »), à deux en local ou contre l'IA (5 niveaux).

### Décisions de Kevin (2026-10-31)

| Question | Choix |
|---|---|
| Entrée du dragon | Invoqué avec l'énergie : une tour se transforme en dragon |
| Victoire | Mat classique. Un pouvoir ne peut jamais laisser son roi en échec ; un événement ne met jamais en échec le camp qui vient de jouer |
| Événements du plateau | Aléatoires mais annoncés à l'avance |
| Animations de capture | Duel long (2 à 3 s, caméra rapprochée) à chaque prise |
| Pouvoirs de la V1 du mode | Bouclier, Gel, Téléportation, Souffle du dragon |
| Pouvoir et coup | Un pouvoir **puis** un coup dans le même tour (au plus un pouvoir par tour) ; Téléportation et Souffle remplacent le coup |
| IA | Sait jouer le mode dès la première version jouable |
| Nom du mode | « Légendes » |
| Seconde invocation | Non : une invocation par camp et par partie, même si le dragon meurt |
| Bonus de centre | Seules les pièces autres que les pions comptent |
| Duels par défaut | Longs partout, y compris contre l'IA (toucher pour passer, réglage court / désactivé) |

## 2. Règles exactes

Tout ce qui n'est pas dit ici suit les règles classiques (roque, prise en passant, promotion, pat,
50 coups, triple répétition, matériel insuffisant).

### 2.1 Énergie : les éclats de cristal

- Chaque camp a une jauge de **0 à 10 éclats**. Départ : **1 éclat** chacun. Ce qui dépasse 10 est perdu.
- Gains, comptés **à la fin du tour** de celui qui gagne :

| Source | Gain |
|---|---|
| Prendre un pion | +1 |
| Prendre un cavalier, un fou ou une tour | +2 |
| Prendre une dame | +3 |
| Prendre un dragon | +4 |
| Occuper au moins 2 des 4 cases centrales (d4, e4, d5, e5) avec des pièces **autres que des pions**, à la fin de son tour | +1 |
| Arrêter une pièce sur un gisement de cristal (§ 2.5) | +2 |

- Une prise par le Souffle du dragon rapporte comme une prise normale. Une pièce tombée dans un gouffre
  ne rapporte rien à personne.
- Les deux jauges sont toujours visibles par les deux joueurs.

### 2.2 Déroulé d'un tour

1. **Début du tour** : les effets arrivés à échéance se résolvent (§ 2.5 : fissures qui cèdent,
   portails qui s'ouvrent ou se ferment, gouffres qui se referment). Le bouclier posé par le joueur au
   trait lors de son tour précédent disparaît.
2. **Phase pouvoir (facultative, un seul pouvoir)** : Bouclier, Gel ou Invocation du dragon. On paie le coût.
3. **Phase coup (obligatoire)** : un coup classique, **ou** une Téléportation (qui compte alors comme le
   pouvoir du tour, donc pas d'autre pouvoir), **ou** un Souffle du dragon.
4. **Fin du tour** : gains d'éclats, puis le gel posé sur ce joueur par l'adversaire fond.

Règle de légalité : à la fin de la phase coup, son propre roi ne doit pas être en échec. Un pouvoir n'est
autorisé que s'il existe ensuite au moins un coup légal. Le tour (pouvoir éventuel + coup) forme une
seule **action**, c'est l'unité que jouent l'interface, l'IA et l'annulation.

### 2.3 Pouvoirs

| Pouvoir | Coût | Cible | Effet |
|---|---|---|---|
| **Bouclier** | 2 | Une de ses pièces, sauf le roi | Jusqu'au début de son prochain tour, la pièce ne peut pas être prise (ni par un coup, ni par le Souffle). Elle bouge, attaque et donne échec normalement |
| **Gel** | 3 | Une pièce adverse, sauf le roi | La pièce ne peut pas bouger pendant le prochain tour adverse (ni coup, ni Téléportation, ni Souffle pour un dragon). **Elle continue de contrôler ses cases** : elle donne échec et défend. Elle peut être prise |
| **Téléportation** | 3 | Une de ses pièces, sauf le roi | Remplace le coup : la pièce saute sur une **case vide voisine** (8 directions). Jamais de prise. Un pion ne peut pas arriver sur la 1re ou la 8e rangée |
| **Invocation du dragon** | 10 (jauge pleine) | Une de ses tours | La tour devient un dragon sur sa case, puis on joue son coup normalement (on peut bouger le dragon tout de suite). **Une invocation par camp et par partie** |

Précisions :
- Le Gel ne retire pas un échec (la pièce gelée contrôle toujours ses cases). Le seul moyen de parer un
  échec reste un coup, une Téléportation qui s'interpose, ou une Invocation suivie d'un coup du dragon.
- Une tour téléportée ou transformée en dragon perd son droit de roque.
- La Téléportation ne remet pas à zéro le compteur des 50 coups, même pour un pion. Un pion téléporté
  sur sa rangée de départ peut à nouveau avancer de deux cases.
- On ne peut pas poser de Bouclier sur une pièce gelée de son camp (le gel l'emporte).

### 2.4 Le dragon

- **Déplacement** : comme une dame, mais **3 cases au plus**, et il **vole** : les pièces sur le chemin
  ne le bloquent pas. Il prend sur la case d'arrivée. Il contrôle donc toutes les cases à 1, 2 ou 3 pas
  dans les 8 directions, quels que soient les obstacles (il donne échec ainsi).
- **Souffle (capacité, remplace le coup)** : le dragon reste sur place et brûle la **première pièce**
  rencontrée dans une des 8 directions, à 3 cases au plus. Si c'est une pièce adverse, ni roi ni
  protégée par un Bouclier, elle est détruite (prise). Sinon le Souffle est impossible dans cette
  direction. Le Souffle ne donne donc jamais échec.
- **Recharge** : après un Souffle, il faut attendre 2 tours ; il redevient disponible au 3e tour suivant.
  Le Souffle est disponible dès le tour qui suit l'Invocation.
- Le dragon peut être pris, gelé, protégé, et il passe au-dessus des gouffres (§ 2.5).
- Valeur indicative pour l'IA : entre une tour et une dame (≈ 850 centipions), à confirmer par les
  auto-parties.

### 2.5 Plateau vivant : événements

**Calendrier.** À partir du 10e demi-coup, un événement est tiré au sort tous les 6 demi-coups (3 tours
de chaque joueur). Le tirage utilise une graine fixée au début de la partie (sauvegardée), ce qui rend la
partie rejouable et l'annulation exacte. Tirage : Fissure 40 %, Portails 30 %, Gisement 30 %, parmi les
types encore autorisés (au plus 2 fissures ou gouffres, 1 paire de portails, 1 gisement à la fois).
Les cases sont tirées parmi les cases **vides des rangées 3 à 6**.

**Annonce.** Fissures et portails sont **annoncés 4 demi-coups avant** (2 tours de chaque joueur) : la
case luit et un compte à rebours s'affiche. Le gisement apparaît tout de suite.

| Événement | Annonce | Effet |
|---|---|---|
| **Fissure → gouffre** | La case se craquelle, les fissures grandissent à chaque demi-coup | À l'échéance, la case s'effondre et devient un **gouffre pour 6 demi-coups**. Une pièce qui s'y trouve tombe et disparaît (ce n'est pas une prise : pas d'éclats, le Bouclier ne protège pas). Le roi ne peut jamais s'arrêter sur une case fissurée ni sur un gouffre. Aucune pièce ne peut s'arrêter sur un gouffre ; les pièces glissantes (fou, tour, dame) et le dragon passent au-dessus, la vue n'est pas bloquée |
| **Portails** | Deux anneaux apparaissent en pointillés | Deux cases liées, placées **en symétrie centrale** (la case `c` et la case `63 - c`) pour l'équité, ouvertes pendant 8 demi-coups. Une pièce qui **termine son coup** sur un portail ressort aussitôt par l'autre si celui-ci est vide (sinon elle reste). Le roi peut les emprunter si la case de sortie n'est pas attaquée. Les attaques ne traversent pas les portails. Jamais sur les rangées 1, 2, 7, 8 (pas de promotion par portail) |
| **Gisement de cristal** | Aucune | Un cristal pousse sur la case. La première pièce qui s'y arrête le récolte : +2 éclats pour son camp |

**Échec et événements.** Les effets se résolvent au début du tour du joueur au trait. S'il en résulte
un échec pour ce joueur, il doit le parer (et s'il ne peut pas, il est mat : l'événement était annoncé).
Si une chute découvrait un échec sur le roi **de l'autre camp** (qui vient de jouer), la fissure tient un
demi-coup de plus et cède au début du tour suivant ; si le problème se répète, elle se referme sans effet.

### 2.6 Fin de partie

- **Victoire** : échec et mat (aucune action légale, pouvoirs compris, ne sort le roi de l'échec), ou abandon.
- **Pat** : aucune action légale sans être en échec → nulle.
- **50 coups** : 100 demi-coups sans prise (Souffle compris), sans coup de pion et sans chute → nulle.
- **Triple répétition** : même position **et** même état fantastique (jauges, statuts, événements,
  recharge du Souffle) trois fois → nulle. En pratique rare.
- **Matériel insuffisant** : le dragon compte comme une pièce majeure (jamais de nulle avec un dragon).

### 2.7 Notation (sauvegarde, historique)

SAN classique, avec le dragon noté `D` et le pouvoir entre crochets avant le coup :

| Action | Exemple |
|---|---|
| Bouclier sur d4 puis Cf3 | `[S d4] Nf3` |
| Gel sur f6 puis Fxe5 | `[F f6] Bxe5` |
| Invocation (tour a1) puis le dragon va en a4 | `[D a1] Da4` |
| Téléportation du cavalier d4 en e5 | `Nd4~e5` |
| Souffle du dragon d4 sur f6 | `Dd4^f6` |

La sauvegarde contient aussi la graine des événements. L'export PGN standard n'existe pas pour ce mode.

## 3. Direction artistique

Même univers cristal / néon (cyan pour les blancs, magenta pour les noirs), tout généré par code, aucun
modèle importé. Les pièces deviennent des personnages assemblés de primitives procédurales (révolution,
extrusion, blocs biseautés, capsules) avec les matériaux néon existants : corps en verre, accents lumineux.
Pas de squelette ni d'Animator : chaque personnage est une petite hiérarchie de transformations animée par
code (respiration, pas, frappes). Pas de sang : une pièce vaincue se brise en éclats de lumière.

### 3.1 Les pièces

| Pièce | Personnage | Silhouette (lisible de dessus) | Arme / signe |
|---|---|---|---|
| Pion | **Sentinelle** | Petit soldat trapu, casque à crête | Épée courte et bouclier rond |
| Cavalier | **Chevalier monté** | Destrier de cristal cabré, cavalier penché | Lance |
| Fou | **Mage** | Longue robe conique, capuche pointue | Bâton surmonté d'un cristal flottant |
| Tour | **Golem-tour** | Tour crénelée qui a des bras de pierre et deux yeux luisants | Poings massifs |
| Dame | **Reine guerrière** | Armure fine, ailes de lumière dans le dos | Épée longue et orbe |
| Roi | **Roi titan** | Le plus grand, cape, couronne à pointes | Grande épée plantée devant lui |
| Dragon | **Dragon de cristal** | Corps serpentin en vol stationnaire, cou articulé en segments, ailes membraneuses | Gueule lumineuse, souffle néon |

Animations au repos (légères, désynchronisées) : respiration, regard qui suit la pièce sélectionnée,
cristal du mage qui tourne, ailes de la reine qui vibrent, dragon qui ondule. Déplacement : marche ou
galop court pour les personnages au sol, vol pour la reine et le dragon, le chevalier garde son saut.

### 3.2 Duels de capture (à chaque prise, 2 à 3 s)

Déroulé : la caméra se rapproche du duel (cadrage adapté portrait / paysage) → l'attaquant avance →
frappe propre à son type → réaction de la victime → elle se brise, et ses éclats **volent jusqu'à la
jauge** du vainqueur (lien visuel avec l'énergie) → la caméra revient à la vue de jeu.

| Attaquant | Frappe |
|---|---|
| Sentinelle | Estoc à l'épée après un pas de charge |
| Chevalier | Charge à la lance, la victime est projetée |
| Mage | Éclair arcanique depuis le cristal du bâton |
| Golem-tour | Saut et écrasement des deux poings, onde de choc sur le verre |
| Reine | Tourbillon d'épée aérien |
| Roi | Coup d'épée à deux mains, lent et lourd |
| Dragon (coup) | **Dévore** : il plonge, saisit la pièce dans sa gueule et l'avale, lueur qui descend dans le cou |
| Dragon (Souffle) | Jet de flammes néon en ligne, la cible fond |

Variante selon la victime : un pion chancelle et tombe, une dame pare une fois avant de céder, un
bouclier qui encaisse affiche des fissures hexagonales. Le coup final de la partie (mat) a une version
plus longue (ralenti, couronne du roi qui tombe).

Options : toucher l'écran passe le duel ; réglage « Duels : longs (défaut, y compris contre l'IA) / courts / désactivés ». Contre
l'IA, l'IA réfléchit pendant le duel.

### 3.3 Pouvoirs et plateau

| Effet | Rendu |
|---|---|
| Bouclier | Bulle hexagonale translucide autour de la pièce, qui frémit quand on la vise |
| Gel | Gangue de glace (coque translucide), givre en particules, la pièce devient bleu pâle |
| Téléportation | La pièce se défait en filaments de lumière et se recompose sur la case voisine |
| Invocation | Le golem se fissure, la lumière perce, le dragon déploie ses ailes en sortant des débris |
| Fissure annoncée | Craquelures rouges qui s'étendent à chaque demi-coup, chiffre du compte à rebours |
| Gouffre | La dalle tombe dans l'abîme, vide sombre avec braises, bord lumineux |
| Portails | Deux anneaux violets qui tournent, tourbillon au centre ; traversée = aspiration puis éjection |
| Gisement | Grappe de cristaux qui pousse ; récolte = éclats vers la jauge |
| Jauge d'éclats | 10 segments de cristal par camp dans le bandeau joueur ; pleine = elle pulse (dragon disponible) |

## 4. Changements dans le code

Principe : **ne pas toucher au mode classique** ni à sa vitesse. Le mode fantastique vit à côté, dans
des sous-espaces `Fantasy`, et réutilise ce qui peut l'être.

### 4.1 `Assets/KevChess/Core` (règles, C# pur)

Nouveau dossier `Core/Fantasy/` :

| Fichier | Rôle |
|---|---|
| `FantasyPiece` | Le type `7` (libre dans le codage actuel sur 3 bits) devient `Dragon` ; lettre FEN `d`/`D` |
| `FantasyAction` | Action compacte sur un `int` : départ (6 bits), arrivée (6), promotion (3), drapeaux (4), nature du coup — normal / Téléportation / Souffle (2), pouvoir — aucun / Bouclier / Gel / Invocation (2), case du pouvoir (6). 29 bits : se range dans la table de transposition comme un `Move` |
| `FantasyState` | Jauges, bouclier et gel en cours (case + échéance), invocation déjà faite, recharge du Souffle, fissures / gouffres / portails / gisement avec leurs comptes à rebours |
| `FantasyPosition` | Position + état fantastique, `MakeAction` / `UnmakeAction`, hachage Zobrist étendu à tout l'état, `IsSquareAttacked` qui connaît le dragon (vol) et ignore les gouffres pour la vue |
| `FantasyMoveGenerator` | Coups classiques adaptés (gouffres, gel, bouclier, portails, roi interdit sur fissures) + coups du dragon + Téléportations + Souffles + combinaisons pouvoir → coup |
| `EventPlanner` | Tirage seedé des événements, annonce, résolution, règle de report si une chute met en échec le camp qui vient de jouer |
| `FantasyGame` | Équivalent de `ChessGame` : historique d'actions, annulation, fins de partie, notation § 2.7, sauvegarde (avec la graine) |

`Position`, `MoveGenerator` et `ChessGame` restent inchangés. Si du code commun mérite d'être partagé
(attaques des pièces classiques), on l'extrait dans une classe statique utilisée par les deux, sans
changer le comportement (les perft classiques le garantissent).

Tests (`tests/KevChess.Tests`) :
- **Équivalence** : sur les 6 positions de perft de référence, avec énergie 0 et sans événement, le
  perft fantastique égale le perft classique.
- Une batterie par règle : coût et cible de chaque pouvoir, gel qui contrôle encore ses cases, bouclier
  contre coup et Souffle, Téléportation sans prise ni promotion, dragon qui vole et donne échec,
  recharge du Souffle, invocation unique, fissure qui fait tomber une pièce, roi interdit sur fissure,
  report d'une chute qui découvrirait un échec, portails symétriques, gisement, mat avec pouvoirs
  disponibles, pat, 50 coups, répétition avec état, annulation exacte (hash identique avant/après),
  aller-retour de la notation et de la sauvegarde.

### 4.2 `Assets/KevChess/AI`

Nouveau dossier `AI/Fantasy/` :
- **`FantasySearcher`** : même alpha-bêta que `Searcher` (TT, quiescence, null-move, LMR, killers),
  sur `FantasyPosition` et `FantasyAction`. Version séparée pour la V1 du mode, pour ne rien ralentir ni
  casser côté classique ; une factorisation générique pourra venir ensuite, une fois les deux stables.
- **Élagage des pouvoirs** (sinon le nombre d'actions par position est multiplié par 20) :
  - Bouclier : seulement sur ses pièces attaquées et pas assez défendues ;
  - Gel : seulement sur les pièces adverses qui attaquent une de ses pièces ou qui défendent la cible
    d'une prise possible, 3 cibles au plus ;
  - Invocation : toujours étudiée quand elle est possible (2 tours au plus) ;
  - en quiescence : prises, Souffles et promotions seulement, pas de pouvoir ;
  - ordre : coup de la TT, prises, Souffles, Invocation, coups simples, puis combinaisons avec pouvoir
    (réduites par LMR).
- **Horizon des événements** : l'IA ne voit que les événements déjà annoncés. Dans l'arbre de recherche
  aucun nouvel événement n'est tiré (la graine n'est pas consultée), donc pas de triche.
- **`FantasyEvaluator`** : l'évaluation classique plus dragon (valeur et table pièce-case centrée),
  valeur des éclats (≈ 25 cp par éclat, plus près de 10 si on a encore une tour à transformer), Souffle
  prêt, pénalité pour une pièce sur une fissure selon le compte à rebours, attrait d'un gisement proche.
- **Niveaux** : Débutant et Facile utilisent les pouvoirs peu et parfois mal (tirage parmi les bonnes
  actions, comme aujourd'hui avec la température), Maître joue tout.
- **Tests** : sauve sa dame attaquée avec le Bouclier, invoque le dragon quand la jauge est pleine et que
  c'est sûr, utilise le Gel pour prendre une pièce défendue une seule fois, ne laisse pas une pièce sur
  une fissure qui cède, trouve un mat en 2 avec pouvoirs, respecte la limite de temps.
- **Banc d'essai** : auto-parties IA contre IA (en .NET hors Unity) pour équilibrer les coûts et les gains,
  et mesure de la profondeur atteinte (objectif : au plus 2 demi-coups de moins que le mode classique
  dans le même temps).

`IChessEngine` reste celui du classique ; on ajoute une interface sœur `IFantasyEngine`, et
`Game/Match/AIAdapter` choisit selon le mode.

### 4.3 `Assets/KevChess/Game/Visuals` (tout généré par code)

Nouveau dossier `Game/Visuals/Fantasy/` :

| Fichier | Rôle |
|---|---|
| `CharacterRig` | Hiérarchie de pièces procédurales (torse, tête, bras, jambes, arme) et poses clés interpolées par code |
| `FantasyPieceFactory` | Construit les 7 personnages à partir de `MeshBuilder` (révolution, extrusion, blocs, capsules) ; parties fixes fusionnées en un seul maillage à 2 sous-maillages (corps / accents), seuls les membres animés restent séparés |
| `FantasyPieceView` | Pendant de `PieceView` : repos, marche, vol, sélection, statuts (bouclier, gel), transformation en dragon |
| `DuelDirector` | Joue un duel de capture : placement, frappe selon l'attaquant, réaction, éclats vers la jauge ; passable au toucher |
| `DuelCamera` | Prend la main sur la caméra pendant un duel puis la rend ; s'appuie sur un point d'accroche à ajouter à `OrbitCamera` (petite interface « prise de contrôle temporaire ») |
| `LivingBoardView` | Fissures (craquelures générées en texture), gouffres (dalle qui tombe), portails, gisements |
| `FantasyEffects` | Particules : souffle, givre, téléportation, éclats vers la jauge, invocation |

Hors `Visuals`, il faudra aussi toucher : `Game/View` (un `FantasyBoardView` qui synchronise
`FantasyGame` et attend la fin des duels), `Game/Match` (choix du mode, actions en deux temps pouvoir →
coup, IA fantastique), `Game/UI` (choix du mode, jauges, boutons de pouvoir, mode ciblage, annonces),
`Game/Audio` (sons synthétisés : épée, glace, souffle, craquement, portail).

**Performance mobile** : 32 personnages plus le dragon. Cible : moins de 150 appels de rendu sur le
S24 et l'A17, 60 i/s hors duel. Moyens : parties fixes fusionnées, instanciation GPU par type de pièce,
reflets sous le verre réduits ou désactivés pour les personnages si besoin, niveaux de détail pour
la vue de dessus.

## 5. Découpage en étapes

Chaque étape se termine par des tests verts, un commit et un push ; les étapes 4 et suivantes par une
recette Mac et Android (S24).

| # | Étape | Contenu | Livrable vérifiable |
|---|---|---|---|
| 0 | Conception | Ce document, validé par Kevin | Document sur la branche |
| 1 | Règles de base | `FantasyPiece`, `FantasyAction`, `FantasyState`, `FantasyPosition`, générateur : dragon, Bouclier, Gel, Téléportation, Souffle, Invocation, énergie | Tests de règles + équivalence perft verts |
| 2 | Plateau vivant et partie | `EventPlanner`, `FantasyGame`, fins de partie, notation, sauvegarde | Tests événements, annulation, sauvegarde verts |
| 3 | IA | `FantasyEvaluator`, `FantasySearcher`, élagage des pouvoirs, niveaux | Tests IA verts, mesure de profondeur, premières auto-parties |
| 4 | **Premier mode jouable** | Menu du mode, jauges, boutons de pouvoir et ciblage, marqueurs simples des événements, pièces actuelles, à deux et contre l'IA | Partie complète jouée sur Mac et S24 |
| 5 | Pièces incarnées | Les 7 personnages procéduraux, repos, marche / vol, statuts visibles | Captures, appels de rendu mesurés sur S24 |
| 6 | Duels | `DuelDirector`, `DuelCamera`, une frappe par type, dévoration et souffle du dragon, invocation, réglage de durée | Un duel de chaque type vu sur Mac et S24 |
| 7 | Plateau vivant en beauté | Fissures, gouffres, portails, gisements, sons | Recette visuelle |
| 8 | Équilibrage et finition | Auto-parties IA contre IA pour régler coûts, gains et calendrier ; perfs mobile ; QA par Kevin | Valeurs figées dans ce document |

Les étapes 1 à 3 sont du C# pur, testé hors Unity : elles ne touchent ni `OrbitCamera`, ni `BoardView`,
ni le dossier `AI` du classique, et peuvent avancer pendant le travail sur le mode classique.

## 6. Valeurs à régler pendant l'équilibrage

Ces nombres sont un point de départ, ils seront ajustés à l'étape 8 :

| Réglage | Départ |
|---|---|
| Jauge max / éclats au départ | 10 / 1 |
| Coûts Bouclier / Gel / Téléportation / Invocation | 2 / 3 / 3 / 10 |
| Bonus de centre | +1 si au moins 2 cases centrales occupées par des pièces hors pions |
| Portée du dragon / du Souffle | 3 / 3 |
| Recharge du Souffle | 1 Souffle tous les 3 tours |
| Premier événement / fréquence / annonce | demi-coup 10 / 6 demi-coups / 4 demi-coups |
| Durée d'un gouffre / des portails | 6 / 8 demi-coups |
| Durée d'un duel long / court | 2,5 s / 1 s |

## 7. Questions tranchées (2026-10-31)

1. Nom du mode à l'écran : **« Légendes »**.
2. **Une seule invocation** de dragon par camp et par partie, même si le dragon meurt.
3. Bonus de centre : **seules les pièces autres que les pions** comptent (évite une jauge pleine dès
   l'ouverture et pousse à centraliser cavaliers et fous).
4. Duels : **longs par défaut partout**, y compris contre l'IA ; toucher l'écran passe le duel et le
   réglage permet de choisir court ou désactivé.

Plus aucune question de conception n'est ouverte ; les nombres de la section 6 restent à équilibrer à
l'étape 8.
