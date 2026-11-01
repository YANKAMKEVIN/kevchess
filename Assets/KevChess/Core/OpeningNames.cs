using System;
using System.Collections.Generic;

namespace KevChess.Core
{
    /// <summary>
    /// Noms français des ouvertures courantes, reconnus d'après les premiers coups (UCI) depuis la position initiale.
    /// La ligne connue la plus longue qui préfixe la partie l'emporte.
    /// </summary>
    public static class OpeningNames
    {
        static readonly string[][] Table =
        {
            // Premiers coups
            new[] { "e2e4", "Ouverture du pion roi" },
            new[] { "d2d4", "Ouverture du pion dame" },
            new[] { "c2c4", "Partie anglaise" },
            new[] { "g1f3", "Ouverture Réti" },
            new[] { "f2f4", "Ouverture Bird" },
            new[] { "b2b3", "Ouverture Larsen" },
            new[] { "g2g3", "Ouverture hongroise" },

            // 1.e4 e5
            new[] { "e2e4 e7e5", "Partie ouverte" },
            new[] { "e2e4 e7e5 g1f3", "Ouverture du cavalier roi" },
            new[] { "e2e4 e7e5 g1f3 b8c6 f1c4", "Partie italienne" },
            new[] { "e2e4 e7e5 g1f3 b8c6 f1c4 f8c5", "Partie italienne, Giuoco piano" },
            new[] { "e2e4 e7e5 g1f3 b8c6 f1c4 f8c5 b2b4", "Gambit Evans" },
            new[] { "e2e4 e7e5 g1f3 b8c6 f1c4 g8f6", "Défense des deux cavaliers" },
            new[] { "e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 f3g5", "Défense des deux cavaliers, attaque Cg5" },
            new[] { "e2e4 e7e5 g1f3 b8c6 f1b5", "Partie espagnole (Ruy Lopez)" },
            new[] { "e2e4 e7e5 g1f3 b8c6 f1b5 a7a6", "Partie espagnole, défense Morphy" },
            new[] { "e2e4 e7e5 g1f3 b8c6 f1b5 a7a6 b5c6", "Partie espagnole, variante d'échange" },
            new[] { "e2e4 e7e5 g1f3 b8c6 f1b5 a7a6 b5a4 g8f6 e1g1 f8e7", "Partie espagnole fermée" },
            new[] { "e2e4 e7e5 g1f3 b8c6 f1b5 g8f6", "Partie espagnole, défense berlinoise" },
            new[] { "e2e4 e7e5 g1f3 b8c6 d2d4", "Partie écossaise" },
            new[] { "e2e4 e7e5 g1f3 b8c6 d2d4 e5d4 f1c4", "Gambit écossais" },
            new[] { "e2e4 e7e5 g1f3 b8c6 b1c3 g8f6", "Partie des quatre cavaliers" },
            new[] { "e2e4 e7e5 g1f3 g8f6", "Défense russe (Petrov)" },
            new[] { "e2e4 e7e5 g1f3 d7d6", "Défense Philidor" },
            new[] { "e2e4 e7e5 g1f3 f7f5", "Gambit letton" },
            new[] { "e2e4 e7e5 f2f4", "Gambit du roi" },
            new[] { "e2e4 e7e5 f2f4 e5f4", "Gambit du roi accepté" },
            new[] { "e2e4 e7e5 f2f4 f8c5", "Gambit du roi refusé" },
            new[] { "e2e4 e7e5 b1c3", "Partie viennoise" },
            new[] { "e2e4 e7e5 f1c4", "Ouverture du fou" },
            new[] { "e2e4 e7e5 d2d4 e5d4", "Partie du centre" },
            new[] { "e2e4 e7e5 d2d4 e5d4 c2c3", "Gambit danois" },

            // Sicilienne
            new[] { "e2e4 c7c5", "Défense sicilienne" },
            new[] { "e2e4 c7c5 b1c3", "Défense sicilienne fermée" },
            new[] { "e2e4 c7c5 c2c3", "Défense sicilienne, variante Alapine" },
            new[] { "e2e4 c7c5 d2d4 c5d4 c2c3", "Gambit Smith-Morra" },
            new[] { "e2e4 c7c5 g1f3 e7e6", "Défense sicilienne, système Paulsen" },
            new[] { "e2e4 c7c5 g1f3 d7d6 f1b5", "Défense sicilienne, variante Moscou" },
            new[] { "e2e4 c7c5 g1f3 d7d6 d2d4 c5d4 f3d4 g8f6 b1c3 a7a6", "Défense sicilienne, variante Najdorf" },
            new[] { "e2e4 c7c5 g1f3 d7d6 d2d4 c5d4 f3d4 g8f6 b1c3 g7g6", "Défense sicilienne, variante du Dragon" },
            new[] { "e2e4 c7c5 g1f3 d7d6 d2d4 c5d4 f3d4 g8f6 b1c3 e7e6", "Défense sicilienne, variante Scheveningue" },
            new[] { "e2e4 c7c5 g1f3 d7d6 d2d4 c5d4 f3d4 g8f6 b1c3 b8c6", "Défense sicilienne classique" },
            new[] { "e2e4 c7c5 g1f3 b8c6 d2d4 c5d4 f3d4 g8f6 b1c3 e7e5", "Défense sicilienne, variante Sveshnikov" },

            // Française, Caro-Kann et autres réponses à 1.e4
            new[] { "e2e4 e7e6", "Défense française" },
            new[] { "e2e4 e7e6 d2d4 d7d5 e4e5", "Défense française, variante d'avance" },
            new[] { "e2e4 e7e6 d2d4 d7d5 e4d5", "Défense française, variante d'échange" },
            new[] { "e2e4 e7e6 d2d4 d7d5 b1d2", "Défense française, variante Tarrasch" },
            new[] { "e2e4 e7e6 d2d4 d7d5 b1c3 f8b4", "Défense française, variante Winawer" },
            new[] { "e2e4 e7e6 d2d4 d7d5 b1c3 g8f6", "Défense française classique" },
            new[] { "e2e4 c7c6", "Défense Caro-Kann" },
            new[] { "e2e4 c7c6 d2d4 d7d5 e4e5", "Défense Caro-Kann, variante d'avance" },
            new[] { "e2e4 c7c6 d2d4 d7d5 e4d5 c6d5", "Défense Caro-Kann, variante d'échange" },
            new[] { "e2e4 c7c6 d2d4 d7d5 b1c3 d5e4 c3e4", "Défense Caro-Kann classique" },
            new[] { "e2e4 d7d5", "Défense scandinave" },
            new[] { "e2e4 d7d5 e4d5 d8d5", "Défense scandinave, variante principale" },
            new[] { "e2e4 d7d6 d2d4 g8f6 b1c3 g7g6", "Défense Pirc" },
            new[] { "e2e4 g7g6", "Défense moderne" },
            new[] { "e2e4 g8f6", "Défense Alekhine" },
            new[] { "e2e4 b8c6", "Défense Nimzowitsch" },

            // 1.d4 d5
            new[] { "d2d4 d7d5 c2c4", "Gambit dame" },
            new[] { "d2d4 d7d5 c2c4 d5c4", "Gambit dame accepté" },
            new[] { "d2d4 d7d5 c2c4 e7e6", "Gambit dame refusé" },
            new[] { "d2d4 d7d5 c2c4 e7e6 b1c3 g8f6 c1g5", "Gambit dame refusé, variante orthodoxe" },
            new[] { "d2d4 d7d5 c2c4 c7c6", "Défense slave" },
            new[] { "d2d4 d7d5 c2c4 c7c6 g1f3 g8f6 b1c3 e7e6", "Défense semi-slave" },
            new[] { "d2d4 d7d5 c2c4 e7e5", "Contre-gambit Albin" },
            new[] { "d2d4 d7d5 c2c4 b8c6", "Défense Tchigorine" },
            new[] { "d2d4 d7d5 c1f4", "Système de Londres" },
            new[] { "d2d4 d7d5 g1f3 g8f6 c1f4", "Système de Londres" },

            // 1.d4 Cf6 et autres
            new[] { "d2d4 g8f6", "Défense indienne" },
            new[] { "d2d4 g8f6 c1f4", "Système de Londres" },
            new[] { "d2d4 g8f6 g1f3 d7d5 c1f4", "Système de Londres" },
            new[] { "d2d4 g8f6 c1g5", "Attaque Trompowsky" },
            new[] { "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7", "Défense est-indienne" },
            new[] { "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 e2e4 d7d6", "Défense est-indienne, variante classique" },
            new[] { "d2d4 g8f6 c2c4 g7g6 b1c3 d7d5", "Défense Grünfeld" },
            new[] { "d2d4 g8f6 c2c4 e7e6 b1c3 f8b4", "Défense nimzo-indienne" },
            new[] { "d2d4 g8f6 c2c4 e7e6 g1f3 b7b6", "Défense ouest-indienne" },
            new[] { "d2d4 g8f6 c2c4 e7e6 g1f3 f8b4", "Défense bogo-indienne" },
            new[] { "d2d4 g8f6 c2c4 e7e6 g2g3", "Ouverture catalane" },
            new[] { "d2d4 g8f6 c2c4 c7c5 d4d5 e7e6", "Défense Benoni moderne" },
            new[] { "d2d4 g8f6 c2c4 c7c5 d4d5 b7b5", "Gambit Benko" },
            new[] { "d2d4 f7f5", "Défense hollandaise" },

            // Anglaise et Réti
            new[] { "c2c4 e7e5", "Partie anglaise, sicilienne inversée" },
            new[] { "c2c4 c7c5", "Partie anglaise symétrique" },
            new[] { "g1f3 d7d5 c2c4", "Ouverture Réti, variante principale" },
        };

        static readonly List<KeyValuePair<string[], string>> Lines = Build();

        static List<KeyValuePair<string[], string>> Build()
        {
            var list = new List<KeyValuePair<string[], string>>(Table.Length);
            foreach (var row in Table)
                list.Add(new KeyValuePair<string[], string>(row[0].Split(' '), row[1]));
            return list;
        }

        /// <summary>Nombre de lignes connues.</summary>
        public static int Count => Lines.Count;

        /// <summary>Toutes les lignes (UCI séparés par des espaces, nom), pour les tests et l'affichage.</summary>
        public static IEnumerable<KeyValuePair<string, string>> All
        {
            get { foreach (var row in Table) yield return new KeyValuePair<string, string>(row[0], row[1]); }
        }

        /// <summary>
        /// Nom de l'ouverture la plus longue reconnue au début de <paramref name="moves"/>, ou null.
        /// Uniquement depuis la position initiale standard (startFen null ou égal à Position.StartFen).
        /// </summary>
        public static string Lookup(IReadOnlyList<Move> moves, string startFen)
        {
            if (moves == null || moves.Count == 0) return null;
            if (startFen != null && !IsStandardStart(startFen)) return null;

            var uci = new string[moves.Count];
            for (int i = 0; i < moves.Count; i++) uci[i] = moves[i].ToUci();

            string best = null;
            int bestLength = 0;
            foreach (var line in Lines)
            {
                var seq = line.Key;
                if (seq.Length > uci.Length || seq.Length <= bestLength) continue;
                bool match = true;
                for (int i = 0; i < seq.Length && match; i++)
                    if (!string.Equals(seq[i], uci[i], StringComparison.Ordinal)) match = false;
                if (!match) continue;
                best = line.Value;
                bestLength = seq.Length;
            }
            return best;
        }

        static bool IsStandardStart(string fen)
        {
            var trimmed = fen.Trim();
            if (trimmed == Position.StartFen) return true;
            // Tolère des compteurs de coups différents : seule la position compte.
            var a = trimmed.Split(' ');
            var b = Position.StartFen.Split(' ');
            if (a.Length < 4) return false;
            for (int i = 0; i < 4; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
