using System.Collections.Generic;

namespace KevChess.Core.Fantasy
{
    public enum BoardEventKind { None, Fissure, Portals, Crystal }

    /// <summary>Événement tiré au sort et ajouté à la position.</summary>
    public readonly struct BoardEvent
    {
        public readonly BoardEventKind Kind;
        public readonly int SquareA, SquareB;

        public BoardEvent(BoardEventKind kind, int a, int b = Square.None)
        {
            Kind = kind; SquareA = a; SquareB = b;
        }

        public bool IsNone => Kind == BoardEventKind.None;
        public static readonly BoardEvent None = default;
    }

    /// <summary>
    /// Calendrier du plateau vivant : tirage pseudo-aléatoire à partir d'une graine et du numéro de demi-coup,
    /// donc identique quand on rejoue la partie. Appelé seulement par la partie réelle, jamais dans la recherche
    /// de l'IA (qui ne connaît ainsi que les événements déjà annoncés).
    /// </summary>
    public static class EventPlanner
    {
        public static bool IsEventPly(FantasyConfig cfg, int ply) =>
            cfg.EventsEnabled && ply >= cfg.FirstEventPly && (ply - cfg.FirstEventPly) % cfg.EventInterval == 0;

        /// <summary>Tire et applique l'événement prévu au demi-coup courant de la position, s'il y en a un.</summary>
        public static BoardEvent Spawn(FantasyPosition pos, ulong seed)
        {
            var cfg = pos.Config;
            int ply = pos.Ply;
            if (!IsEventPly(cfg, ply)) return BoardEvent.None;

            ulong state = seed ^ ((ulong)ply * 0x9E3779B97F4A7C15UL);
            int fissureW = pos.HoleCount < cfg.MaxHoles ? cfg.FissureWeight : 0;
            int portalW = pos.PortalState == PortalState.None ? cfg.PortalWeight : 0;
            int crystalW = pos.CrystalSquare == Square.None ? cfg.CrystalWeight : 0;

            // On retire les types sans case libre.
            var free = FreeSquares(pos, 2, 5);
            var portalSquares = new List<int>();
            foreach (int sq in free)
                if (Square.Rank(sq) <= 3 && free.Contains(63 - sq)) portalSquares.Add(sq);
            if (free.Count == 0) fissureW = crystalW = 0;
            if (portalSquares.Count == 0) portalW = 0;

            int total = fissureW + portalW + crystalW;
            if (total == 0) return BoardEvent.None;
            int roll = (int)(Next(ref state) % (ulong)total);

            if (roll < fissureW)
            {
                int sq = free[(int)(Next(ref state) % (ulong)free.Count)];
                pos.AddFissure(sq, cfg.AnnounceDelay);
                return new BoardEvent(BoardEventKind.Fissure, sq);
            }
            if (roll < fissureW + portalW)
            {
                int a = portalSquares[(int)(Next(ref state) % (ulong)portalSquares.Count)];
                pos.SetPortals(a, 63 - a, PortalState.Announced, cfg.AnnounceDelay);
                return new BoardEvent(BoardEventKind.Portals, a, 63 - a);
            }
            int c = free[(int)(Next(ref state) % (ulong)free.Count)];
            pos.SetCrystal(c);
            return new BoardEvent(BoardEventKind.Crystal, c);
        }

        /// <summary>Cases vides, sans élément du plateau vivant, entre deux rangées (indices 0-7) comprises.</summary>
        static List<int> FreeSquares(FantasyPosition pos, int minRank, int maxRank)
        {
            var list = new List<int>(32);
            for (int sq = minRank * 8; sq < (maxRank + 1) * 8; sq++)
                if (pos[sq] == Piece.Empty && !pos.IsFeatureSquare(sq)) list.Add(sq);
            return list;
        }

        static ulong Next(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
