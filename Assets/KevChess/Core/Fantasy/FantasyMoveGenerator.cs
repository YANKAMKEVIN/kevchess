using System.Collections.Generic;

namespace KevChess.Core.Fantasy
{
    /// <summary>Quelles actions générer.</summary>
    public enum GenMode
    {
        /// <summary>Toutes les actions légales (règles, interface, tests).</summary>
        All,
        /// <summary>Recherche de l'IA : pouvoirs restreints aux cibles utiles.</summary>
        Search,
        /// <summary>Quiescence : prises, Souffles et promotions, sans pouvoir.</summary>
        Tactical,
    }

    /// <summary>Génère les actions du mode Légendes : coups (dragon compris), Téléportations, Souffles et pouvoirs.</summary>
    public static class FantasyMoveGenerator
    {
        static readonly PieceType[] Promotions = { PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight };

        /// <summary>Nombre maximal de cibles de Bouclier et de Gel étudiées par l'IA.</summary>
        public const int SearchPowerTargets = 3;

        public static List<FantasyAction> Legal(FantasyPosition pos, GenMode mode = GenMode.All)
        {
            var list = new List<FantasyAction>(96);
            GenerateLegal(pos, list, mode);
            return list;
        }

        public static void GenerateLegal(FantasyPosition pos, List<FantasyAction> list, GenMode mode = GenMode.All)
        {
            list.Clear();
            var us = pos.SideToMove;
            var cfg = pos.Config;
            bool tactical = mode == GenMode.Tactical;

            // Coups sans pouvoir.
            GeneratePseudo(pos, list, tactical, allowTeleport: !tactical);
            FilterLegal(pos, list, 0);
            if (tactical) return;

            int energy = pos.Energy(us);
            int baseCount = list.Count;

            // Bouclier et Gel ne changent pas le plateau : mêmes coups légaux (hors Téléportation, qui est déjà le pouvoir du tour).
            if (energy >= cfg.ShieldCost)
                foreach (int t in ShieldTargets(pos, mode))
                    for (int i = 0; i < baseCount; i++)
                        if (list[i].Kind != ActionKind.Teleport) list.Add(list[i].WithPower(PowerKind.Shield, t));

            if (energy >= cfg.FreezeCost)
                foreach (int t in FreezeTargets(pos, list, baseCount, mode))
                    for (int i = 0; i < baseCount; i++)
                        if (list[i].Kind != ActionKind.Teleport) list.Add(list[i].WithPower(PowerKind.Freeze, t));

            // Invocation : la tour devient dragon, puis un coup.
            if (energy >= cfg.SummonCost && !pos.HasSummoned(us))
            {
                int rook = Piece.Make(us, PieceType.Rook);
                for (int sq = 0; sq < 64; sq++)
                {
                    if (pos[sq] != rook) continue;
                    int start = list.Count;
                    pos.PushSummon(sq);
                    GeneratePseudo(pos, list, false, allowTeleport: false);
                    pos.PopSummon();
                    for (int i = start; i < list.Count; i++) list[i] = list[i].WithPower(PowerKind.Summon, sq);
                    FilterLegal(pos, list, start);
                }
            }
        }

        static void FilterLegal(FantasyPosition pos, List<FantasyAction> list, int start)
        {
            int write = start;
            var saved = pos.Report;
            pos.Report = null;
            for (int i = start; i < list.Count; i++)
            {
                var a = list[i];
                bool legal = pos.MakeAction(a);
                pos.UnmakeAction();
                if (legal) list[write++] = a;
            }
            pos.Report = saved;
            list.RemoveRange(write, list.Count - write);
        }

        public static bool HasLegalAction(FantasyPosition pos)
        {
            // Les pouvoirs n'ajoutent un coup légal que par l'Invocation ou la Téléportation : la génération complète suffit,
            // mais on essaie d'abord les coups simples.
            var list = new List<FantasyAction>(64);
            GeneratePseudo(pos, list, false, allowTeleport: true);
            var saved = pos.Report;
            pos.Report = null;
            try
            {
                foreach (var a in list)
                {
                    bool legal = pos.MakeAction(a);
                    pos.UnmakeAction();
                    if (legal) return true;
                }
            }
            finally { pos.Report = saved; }
            return Legal(pos).Count > 0;
        }

        // ------------------------------------------------------------------ cibles des pouvoirs

        static IEnumerable<int> ShieldTargets(FantasyPosition pos, GenMode mode)
        {
            var us = pos.SideToMove;
            var them = Piece.Opposite(us);
            int found = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                int p = pos[sq];
                if (!Piece.IsColor(p, us) || Piece.TypeOf(p) == PieceType.King || pos.FrozenSquare(us) == sq) continue;
                if (mode == GenMode.Search)
                {
                    // Seulement les pièces attaquées.
                    if (!pos.IsSquareAttacked(sq, them)) continue;
                    if (found++ >= SearchPowerTargets) yield break;
                }
                yield return sq;
            }
        }

        static IEnumerable<int> FreezeTargets(FantasyPosition pos, List<FantasyAction> baseActions, int baseCount, GenMode mode)
        {
            var us = pos.SideToMove;
            var them = Piece.Opposite(us);
            if (mode != GenMode.Search)
            {
                for (int sq = 0; sq < 64; sq++)
                {
                    int p = pos[sq];
                    if (Piece.IsColor(p, them) && Piece.TypeOf(p) != PieceType.King) yield return sq;
                }
                yield break;
            }

            // IA : pièces adverses qui attaquent une des nôtres ou qui défendent une case où l'on peut prendre.
            var candidates = new List<int>(8);
            for (int sq = 0; sq < 64; sq++)
            {
                int p = pos[sq];
                if (!Piece.IsColor(p, them) || Piece.TypeOf(p) == PieceType.King) continue;
                bool useful = false;
                for (int i = 0; i < baseCount && !useful; i++)
                {
                    var a = baseActions[i];
                    if (a.IsCapture && a.Kind == ActionKind.Normal && pos.PieceAttacks(sq, a.To)) useful = true;
                }
                for (int t = 0; t < 64 && !useful; t++)
                    if (Piece.IsColor(pos[t], us) && pos.PieceAttacks(sq, t)) useful = true;
                if (useful) candidates.Add(sq);
            }
            candidates.Sort((a, b) => ValueOf(pos[b]).CompareTo(ValueOf(pos[a])));
            for (int i = 0; i < candidates.Count && i < SearchPowerTargets; i++) yield return candidates[i];
        }

        static int ValueOf(int piece)
        {
            switch (Piece.TypeOf(piece))
            {
                case PieceType.Pawn: return 1;
                case PieceType.Knight: case PieceType.Bishop: return 3;
                case PieceType.Rook: return 5;
                case PieceType.Queen: return 9;
                case FantasyPiece.Dragon: return 8;
                default: return 0;
            }
        }

        // ------------------------------------------------------------------ coups pseudo-légaux

        /// <summary>Ajoute les coups pseudo-légaux sans pouvoir (coups, Souffles et, si permis, Téléportations).</summary>
        public static void GeneratePseudo(FantasyPosition pos, List<FantasyAction> list, bool tactical, bool allowTeleport)
        {
            var us = pos.SideToMove;
            var them = Piece.Opposite(us);
            int frozen = pos.FrozenSquare(us);
            var cfg = pos.Config;
            bool canTeleport = allowTeleport && pos.Energy(us) >= cfg.TeleportCost;

            for (int from = 0; from < 64; from++)
            {
                int piece = pos[from];
                if (!Piece.IsColor(piece, us) || from == frozen) continue;
                var type = Piece.TypeOf(piece);

                switch (type)
                {
                    case PieceType.Pawn:
                        GeneratePawn(pos, from, us, list, tactical);
                        break;
                    case PieceType.Knight:
                        GenerateSteps(pos, from, Tables.KnightTargets[from], them, list, tactical, false);
                        break;
                    case PieceType.King:
                        GenerateSteps(pos, from, Tables.KingTargets[from], them, list, tactical, true);
                        if (!tactical) GenerateCastling(pos, from, us, list);
                        break;
                    case PieceType.Bishop:
                        GenerateSliders(pos, from, 4, 8, them, list, tactical);
                        break;
                    case PieceType.Rook:
                        GenerateSliders(pos, from, 0, 4, them, list, tactical);
                        break;
                    case PieceType.Queen:
                        GenerateSliders(pos, from, 0, 8, them, list, tactical);
                        break;
                    case FantasyPiece.Dragon:
                        GenerateDragon(pos, from, them, list, tactical);
                        if (pos.BreathCooldown(us) == 0) GenerateBreath(pos, from, them, list);
                        break;
                }

                if (canTeleport && type != PieceType.King)
                {
                    foreach (int to in Tables.KingTargets[from])
                    {
                        if (pos[to] != Piece.Empty || pos.IsChasm(to)) continue;
                        if (type == PieceType.Pawn && (Square.Rank(to) == 0 || Square.Rank(to) == 7)) continue;
                        list.Add(new FantasyAction(from, to, kind: ActionKind.Teleport));
                    }
                }
            }
        }

        static bool CanCapture(FantasyPosition pos, int sq, PieceColor them)
        {
            int target = pos[sq];
            return Piece.IsColor(target, them) && Piece.TypeOf(target) != PieceType.King && pos.ShieldSquare(them) != sq;
        }

        static void GeneratePawn(FantasyPosition pos, int from, PieceColor us, List<FantasyAction> list, bool tactical)
        {
            var them = Piece.Opposite(us);
            int dir = us == PieceColor.White ? 8 : -8;
            int startRank = us == PieceColor.White ? 1 : 6;
            int promoRank = us == PieceColor.White ? 7 : 0;

            int one = from + dir;
            if (pos[one] == Piece.Empty)
            {
                bool canStop = !pos.IsChasm(one);
                if (Square.Rank(one) == promoRank)
                {
                    if (canStop) AddPromotions(from, one, MoveFlags.None, list);
                }
                else if (!tactical)
                {
                    if (canStop) list.Add(new FantasyAction(from, one));
                    int two = one + dir;
                    // Un gouffre ne bloque pas le passage, on ne peut seulement pas s'y arrêter.
                    if (Square.Rank(from) == startRank && pos[two] == Piece.Empty && !pos.IsChasm(two))
                        list.Add(new FantasyAction(from, two, MoveFlags.DoublePush));
                }
            }

            foreach (int to in Tables.PawnAttacks[(int)us][from])
            {
                if (CanCapture(pos, to, them))
                {
                    if (Square.Rank(to) == promoRank) AddPromotions(from, to, MoveFlags.Capture, list);
                    else list.Add(new FantasyAction(from, to, MoveFlags.Capture));
                }
                else if (to == pos.EnPassantSquare)
                {
                    int capSq = us == PieceColor.White ? to - 8 : to + 8;
                    if (pos.ShieldSquare(them) != capSq)
                        list.Add(new FantasyAction(from, to, MoveFlags.Capture | MoveFlags.EnPassant));
                }
            }
        }

        static void AddPromotions(int from, int to, MoveFlags flags, List<FantasyAction> list)
        {
            foreach (var t in Promotions) list.Add(new FantasyAction(from, to, flags, t));
        }

        static void GenerateSteps(FantasyPosition pos, int from, int[] targets, PieceColor them, List<FantasyAction> list, bool tactical, bool isKing)
        {
            foreach (int to in targets)
            {
                if (pos.IsChasm(to) || (isKing && pos.IsFissure(to))) continue;
                int target = pos[to];
                if (target == Piece.Empty)
                {
                    if (!tactical) list.Add(new FantasyAction(from, to));
                }
                else if (CanCapture(pos, to, them))
                {
                    list.Add(new FantasyAction(from, to, MoveFlags.Capture));
                }
            }
        }

        static void GenerateSliders(FantasyPosition pos, int from, int dirStart, int dirEnd, PieceColor them, List<FantasyAction> list, bool tactical)
        {
            for (int d = dirStart; d < dirEnd; d++)
            {
                foreach (int to in Tables.Rays[d][from])
                {
                    int target = pos[to];
                    if (target == Piece.Empty)
                    {
                        if (!tactical && !pos.IsChasm(to)) list.Add(new FantasyAction(from, to));
                        continue;
                    }
                    if (CanCapture(pos, to, them)) list.Add(new FantasyAction(from, to, MoveFlags.Capture));
                    break;
                }
            }
        }

        /// <summary>Le dragon vole : 1 à 3 cases dans les 8 directions, par-dessus les pièces.</summary>
        static void GenerateDragon(FantasyPosition pos, int from, PieceColor them, List<FantasyAction> list, bool tactical)
        {
            int range = pos.Config.DragonRange;
            for (int d = 0; d < 8; d++)
            {
                var ray = Tables.Rays[d][from];
                for (int k = 0; k < ray.Length && k < range; k++)
                {
                    int to = ray[k];
                    int target = pos[to];
                    if (target == Piece.Empty)
                    {
                        if (!tactical && !pos.IsChasm(to)) list.Add(new FantasyAction(from, to));
                    }
                    else if (CanCapture(pos, to, them))
                    {
                        list.Add(new FantasyAction(from, to, MoveFlags.Capture));
                    }
                }
            }
        }

        /// <summary>Souffle : la première pièce rencontrée à portée, si elle est adverse, ni roi ni protégée.</summary>
        static void GenerateBreath(FantasyPosition pos, int from, PieceColor them, List<FantasyAction> list)
        {
            int range = pos.Config.BreathRange;
            for (int d = 0; d < 8; d++)
            {
                var ray = Tables.Rays[d][from];
                for (int k = 0; k < ray.Length && k < range; k++)
                {
                    int to = ray[k];
                    if (pos[to] == Piece.Empty) continue;
                    if (CanCapture(pos, to, them)) list.Add(new FantasyAction(from, to, kind: ActionKind.Breath));
                    break;
                }
            }
        }

        static void GenerateCastling(FantasyPosition pos, int from, PieceColor us, List<FantasyAction> list)
        {
            var them = Piece.Opposite(us);
            bool white = us == PieceColor.White;
            var kingSide = white ? CastlingRights.WhiteKingSide : CastlingRights.BlackKingSide;
            var queenSide = white ? CastlingRights.WhiteQueenSide : CastlingRights.BlackQueenSide;
            if ((pos.Castling & (kingSide | queenSide)) == 0) return;
            if (pos.IsSquareAttacked(from, them)) return;

            if ((pos.Castling & kingSide) != 0
                && pos[from + 1] == Piece.Empty && pos[from + 2] == Piece.Empty
                && !pos.IsSquareAttacked(from + 1, them))
                list.Add(new FantasyAction(from, from + 2, MoveFlags.Castle));

            if ((pos.Castling & queenSide) != 0
                && pos[from - 1] == Piece.Empty && pos[from - 2] == Piece.Empty && pos[from - 3] == Piece.Empty
                && !pos.IsSquareAttacked(from - 1, them))
                list.Add(new FantasyAction(from, from - 2, MoveFlags.Castle));
        }

        /// <summary>Nombre de feuilles à la profondeur donnée.</summary>
        public static long Perft(FantasyPosition pos, int depth, GenMode mode = GenMode.All)
        {
            if (depth == 0) return 1;
            var list = Legal(pos, mode);
            if (depth == 1) return list.Count;
            long nodes = 0;
            foreach (var a in list)
            {
                pos.MakeAction(a);
                nodes += Perft(pos, depth - 1, mode);
                pos.UnmakeAction();
            }
            return nodes;
        }
    }
}
