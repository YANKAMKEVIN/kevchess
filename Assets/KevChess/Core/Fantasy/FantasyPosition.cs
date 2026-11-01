using System;
using System.Collections.Generic;
using System.Text;

namespace KevChess.Core.Fantasy
{
    public enum HoleState { None = 0, Fissure = 1, Chasm = 2 }

    public enum PortalState { None = 0, Announced = 1, Open = 2 }

    /// <summary>Ce qui s'est passé pendant une action, pour que la vue l'anime (rempli seulement si demandé).</summary>
    public sealed class ActionReport
    {
        public int CapturedPiece, CaptureSquare = Square.None;
        /// <summary>Case d'arrivée après un éventuel passage par un portail.</summary>
        public int FinalSquare = Square.None;
        public int PortalFrom = Square.None, PortalTo = Square.None;
        public int CrystalSquare = Square.None;
        public int EnergyGained;
        public readonly List<int> FallenSquares = new List<int>(2);
        public readonly List<int> FallenPieces = new List<int>(2);
        public readonly List<int> CollapsedSquares = new List<int>(2);

        public void Clear()
        {
            CapturedPiece = Piece.Empty;
            CaptureSquare = FinalSquare = PortalFrom = PortalTo = CrystalSquare = Square.None;
            EnergyGained = 0;
            FallenSquares.Clear();
            FallenPieces.Clear();
            CollapsedSquares.Clear();
        }
    }

    /// <summary>
    /// Position du mode Légendes : plateau, état classique (trait, roque, en passant, compteurs) et état
    /// fantastique (éclats, bouclier, gel, invocation, Souffle, fissures, portails, gisement).
    /// Tout tient dans un tableau d'entiers : jouer une action sauvegarde une copie, déjouer la restaure.
    /// La position décrit toujours le début du tour du camp au trait (effets d'échéance déjà résolus).
    /// </summary>
    public sealed class FantasyPosition
    {
        public const string StartFen = Position.StartFen;

        // Disposition du tableau d'état.
        const int SSide = 64, SCastling = 65, SEp = 66, SHalf = 67, SFull = 68, SPly = 69;
        const int SEnergy = 70, SShield = 72, SFrozen = 74, SSummoned = 76, SCool = 78, SKing = 80;
        const int SHoles = 82; // 2 × (case, état, minuterie, reportée)
        const int SPortalA = 90, SPortalB = 91, SPortalState = 92, SPortalTimer = 93, SCrystal = 94;
        const int Size = 95;
        const int HoleSlots = 2;

        public static readonly int[] CenterSquares = { 27, 28, 35, 36 }; // d4 e4 d5 e5

        readonly FantasyConfig config;
        int[] s = new int[Size];
        readonly List<int[]> snapshots = new List<int[]>(256);
        readonly List<ulong> hashes = new List<ulong>(256);
        readonly List<FantasyAction> actions = new List<FantasyAction>(256);
        int depth;

        public FantasyConfig Config => config;
        public ulong Hash { get; private set; }
        /// <summary>Si non nul, rempli par chaque MakeAction.</summary>
        public ActionReport Report;

        public FantasyPosition(FantasyConfig config = null)
        {
            this.config = config ?? FantasyConfig.Default;
        }

        // ------------------------------------------------------------------ lecture

        public int this[int square] => s[square];
        public PieceColor SideToMove => (PieceColor)s[SSide];
        public CastlingRights Castling => (CastlingRights)s[SCastling];
        public int EnPassantSquare => s[SEp];
        public int HalfmoveClock => s[SHalf];
        public int FullmoveNumber => s[SFull];
        /// <summary>Demi-coups joués depuis le début de la partie (sert au calendrier des événements).</summary>
        public int Ply => s[SPly];
        public int Energy(PieceColor c) => s[SEnergy + (int)c];
        /// <summary>Case protégée par le Bouclier du camp c, ou Square.None.</summary>
        public int ShieldSquare(PieceColor c) => s[SShield + (int)c];
        /// <summary>Pièce du camp c gelée par l'adversaire, ou Square.None.</summary>
        public int FrozenSquare(PieceColor c) => s[SFrozen + (int)c];
        public bool HasSummoned(PieceColor c) => s[SSummoned + (int)c] != 0;
        public int BreathCooldown(PieceColor c) => s[SCool + (int)c];
        public int KingSquare(PieceColor c) => s[SKing + (int)c];
        public bool InCheck => IsInCheck(SideToMove);
        public FantasyAction LastAction => actions.Count == 0 ? FantasyAction.Null : actions[actions.Count - 1];
        public int HistoryCount => actions.Count;

        public int HoleSquare(int slot) => s[SHoles + slot * 4];
        public HoleState HoleStateAt(int slot) => (HoleState)s[SHoles + slot * 4 + 1];
        public int HoleTimer(int slot) => s[SHoles + slot * 4 + 2];
        public static int HoleSlotCount => HoleSlots;

        public HoleState HoleAt(int sq)
        {
            for (int i = 0; i < HoleSlots; i++)
                if (s[SHoles + i * 4 + 1] != 0 && s[SHoles + i * 4] == sq) return (HoleState)s[SHoles + i * 4 + 1];
            return HoleState.None;
        }

        public bool IsChasm(int sq) => HoleAt(sq) == HoleState.Chasm;
        public bool IsFissure(int sq) => HoleAt(sq) == HoleState.Fissure;
        public int HoleCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < HoleSlots; i++) if (s[SHoles + i * 4 + 1] != 0) n++;
                return n;
            }
        }

        public int PortalA => s[SPortalA];
        public int PortalB => s[SPortalB];
        public PortalState PortalState => (PortalState)s[SPortalState];
        public int PortalTimer => s[SPortalTimer];
        public int CrystalSquare => s[SCrystal];

        public bool IsShielded(int sq)
        {
            int p = s[sq];
            return p != Piece.Empty && s[SShield + (int)Piece.ColorOf(p)] == sq;
        }

        public bool IsFrozen(int sq)
        {
            int p = s[sq];
            return p != Piece.Empty && s[SFrozen + (int)Piece.ColorOf(p)] == sq;
        }

        /// <summary>Case occupée par un élément du plateau vivant (fissure, gouffre, portail, gisement).</summary>
        public bool IsFeatureSquare(int sq) =>
            HoleAt(sq) != HoleState.None || sq == s[SCrystal]
            || (s[SPortalState] != 0 && (sq == s[SPortalA] || sq == s[SPortalB]));

        // ------------------------------------------------------------------ création

        public static FantasyPosition Start(FantasyConfig config = null) => FromFen(StartFen, config);

        /// <summary>FEN classique (dragon = d/D). L'état fantastique part des valeurs de départ de la config.</summary>
        public static FantasyPosition FromFen(string fen, FantasyConfig config = null)
        {
            if (string.IsNullOrWhiteSpace(fen)) throw new FormatException("FEN vide");
            var parts = fen.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4) throw new FormatException("FEN incomplet : " + fen);

            var p = new FantasyPosition(config);
            var s = p.s;
            s[SKing] = s[SKing + 1] = Square.None;
            var rows = parts[0].Split('/');
            if (rows.Length != 8) throw new FormatException("FEN : 8 rangées attendues");
            for (int i = 0; i < 8; i++)
            {
                int rank = 7 - i, file = 0;
                foreach (char c in rows[i])
                {
                    if (char.IsDigit(c)) { file += c - '0'; continue; }
                    int piece = FantasyPiece.FromFenChar(c);
                    if (piece < 0 || file > 7) throw new FormatException("FEN : caractère invalide '" + c + "'");
                    int sq = Square.Make(file, rank);
                    s[sq] = piece;
                    if (Piece.TypeOf(piece) == PieceType.King) s[SKing + (int)Piece.ColorOf(piece)] = sq;
                    file++;
                }
                if (file != 8) throw new FormatException("FEN : rangée de longueur invalide");
            }
            if (s[SKing] == Square.None || s[SKing + 1] == Square.None)
                throw new FormatException("FEN : chaque camp doit avoir un roi");

            s[SSide] = parts[1] == "b" ? 1 : 0;
            var cr = CastlingRights.None;
            if (parts[2] != "-")
                foreach (char c in parts[2])
                {
                    if (c == 'K') cr |= CastlingRights.WhiteKingSide;
                    else if (c == 'Q') cr |= CastlingRights.WhiteQueenSide;
                    else if (c == 'k') cr |= CastlingRights.BlackKingSide;
                    else if (c == 'q') cr |= CastlingRights.BlackQueenSide;
                }
            s[SCastling] = (int)p.SanitizeCastling(cr);
            int ep = parts[3] == "-" ? Square.None : Square.Parse(parts[3]);
            s[SEp] = ep != Square.None && p.CanCaptureEnPassant(ep, p.SideToMove) ? ep : Square.None;
            s[SHalf] = parts.Length > 4 && int.TryParse(parts[4], out var hm) ? hm : 0;
            s[SFull] = parts.Length > 5 && int.TryParse(parts[5], out var fm) ? Math.Max(1, fm) : 1;

            s[SEnergy] = s[SEnergy + 1] = p.config.StartEnergy;
            s[SShield] = s[SShield + 1] = Square.None;
            s[SFrozen] = s[SFrozen + 1] = Square.None;
            for (int i = 0; i < HoleSlots; i++) s[SHoles + i * 4] = Square.None;
            s[SPortalA] = s[SPortalB] = Square.None;
            s[SCrystal] = Square.None;
            p.Hash = p.ComputeHash();
            return p;
        }

        public FantasyPosition Clone()
        {
            var p = new FantasyPosition(config);
            Array.Copy(s, p.s, Size);
            for (int i = 0; i < depth; i++) p.snapshots.Add((int[])snapshots[i].Clone());
            p.depth = depth;
            p.hashes.AddRange(hashes);
            p.actions.AddRange(actions);
            p.Hash = Hash;
            return p;
        }

        /// <summary>FEN du plateau seul (l'état fantastique n'y figure pas).</summary>
        public string ToFen()
        {
            var sb = new StringBuilder(90);
            for (int rank = 7; rank >= 0; rank--)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    int piece = s[Square.Make(file, rank)];
                    if (piece == Piece.Empty) { empty++; continue; }
                    if (empty > 0) { sb.Append(empty); empty = 0; }
                    sb.Append(FantasyPiece.ToFenChar(piece));
                }
                if (empty > 0) sb.Append(empty);
                if (rank > 0) sb.Append('/');
            }
            sb.Append(SideToMove == PieceColor.White ? " w " : " b ");
            var cr = Castling;
            if (cr == CastlingRights.None) sb.Append('-');
            else
            {
                if ((cr & CastlingRights.WhiteKingSide) != 0) sb.Append('K');
                if ((cr & CastlingRights.WhiteQueenSide) != 0) sb.Append('Q');
                if ((cr & CastlingRights.BlackKingSide) != 0) sb.Append('k');
                if ((cr & CastlingRights.BlackQueenSide) != 0) sb.Append('q');
            }
            sb.Append(' ').Append(Square.Name(EnPassantSquare));
            sb.Append(' ').Append(HalfmoveClock).Append(' ').Append(FullmoveNumber);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ mise en place (tests, événements)

        public void SetEnergy(PieceColor c, int value) { s[SEnergy + (int)c] = Clamp(value); Rehash(); }
        public void SetShield(PieceColor c, int sq) { s[SShield + (int)c] = sq; Rehash(); }
        public void SetFrozen(PieceColor c, int sq) { s[SFrozen + (int)c] = sq; Rehash(); }
        public void SetSummoned(PieceColor c, bool value) { s[SSummoned + (int)c] = value ? 1 : 0; Rehash(); }
        public void SetBreathCooldown(PieceColor c, int value) { s[SCool + (int)c] = value; Rehash(); }
        public void SetCrystal(int sq) { s[SCrystal] = sq; Rehash(); }

        /// <summary>Annonce une fissure qui cédera dans <paramref name="timer"/> demi-coups. Faux si plus de place.</summary>
        public bool AddFissure(int sq, int timer) => AddHole(sq, HoleState.Fissure, timer);

        public bool AddChasm(int sq, int timer) => AddHole(sq, HoleState.Chasm, timer);

        bool AddHole(int sq, HoleState state, int timer)
        {
            for (int i = 0; i < HoleSlots; i++)
            {
                int b = SHoles + i * 4;
                if (s[b + 1] != 0) continue;
                s[b] = sq; s[b + 1] = (int)state; s[b + 2] = timer; s[b + 3] = 0;
                Rehash();
                return true;
            }
            return false;
        }

        public void SetPortals(int a, int b, PortalState state, int timer)
        {
            s[SPortalA] = state == PortalState.None ? Square.None : a;
            s[SPortalB] = state == PortalState.None ? Square.None : b;
            s[SPortalState] = (int)state;
            s[SPortalTimer] = timer;
            Rehash();
        }

        /// <summary>Pose une pièce (tests). Piece.Empty pour vider la case.</summary>
        public void SetPiece(int sq, int piece)
        {
            s[sq] = piece;
            if (piece != Piece.Empty && Piece.TypeOf(piece) == PieceType.King) s[SKing + (int)Piece.ColorOf(piece)] = sq;
            Rehash();
        }

        void Rehash() => Hash = ComputeHash();

        int Clamp(int e) => e < 0 ? 0 : e > config.MaxEnergy ? config.MaxEnergy : e;

        // ------------------------------------------------------------------ hachage

        public ulong ComputeHash()
        {
            ulong h = 0;
            for (int sq = 0; sq < 64; sq++)
                if (s[sq] != Piece.Empty) h ^= Tables.ZobristPieces[s[sq], sq];
            if (s[SSide] == 1) h ^= Tables.ZobristBlackToMove;
            h ^= Tables.ZobristCastling[s[SCastling]];
            if (s[SEp] != Square.None) h ^= Tables.ZobristEnPassant[Square.File(s[SEp])];
            for (int i = SEnergy; i < Size; i++)
            {
                if (i == SKing || i == SKing + 1) continue;
                h ^= Mix((ulong)i * 0x100000001B3UL + (ulong)(s[i] + 2));
            }
            return h;
        }

        static ulong Mix(ulong z)
        {
            z += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // ------------------------------------------------------------------ jouer / déjouer

        static readonly int[] CastleMask = BuildCastleMask();

        static int[] BuildCastleMask()
        {
            var m = new int[64];
            for (int i = 0; i < 64; i++) m[i] = (int)CastlingRights.All;
            m[0] &= ~(int)CastlingRights.WhiteQueenSide;
            m[7] &= ~(int)CastlingRights.WhiteKingSide;
            m[4] &= ~(int)(CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide);
            m[56] &= ~(int)CastlingRights.BlackQueenSide;
            m[63] &= ~(int)CastlingRights.BlackKingSide;
            m[60] &= ~(int)(CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide);
            return m;
        }

        void PushSnapshot()
        {
            if (depth == snapshots.Count) snapshots.Add(new int[Size]);
            Array.Copy(s, snapshots[depth], Size);
            depth++;
        }

        /// <summary>
        /// Joue une action supposée pseudo-légale (générée par FantasyMoveGenerator) et passe au début du
        /// tour adverse. Renvoie faux si l'action laisse son roi en échec (elle reste jouée : appeler UnmakeAction).
        /// </summary>
        public bool MakeAction(FantasyAction a)
        {
            PushSnapshot();
            hashes.Add(Hash);
            actions.Add(a);
            var report = Report;
            report?.Clear();

            int us = s[SSide], them = us ^ 1;
            var usColor = (PieceColor)us;

            // 1) Pouvoir.
            switch (a.Power)
            {
                case PowerKind.Shield:
                    s[SEnergy + us] -= config.ShieldCost;
                    s[SShield + us] = a.PowerSquare;
                    break;
                case PowerKind.Freeze:
                    s[SEnergy + us] -= config.FreezeCost;
                    s[SFrozen + them] = a.PowerSquare;
                    break;
                case PowerKind.Summon:
                    s[SEnergy + us] -= config.SummonCost;
                    s[a.PowerSquare] = Piece.Make(usColor, FantasyPiece.Dragon);
                    s[SSummoned + us] = 1;
                    s[SCool + us] = 1;
                    s[SCastling] &= CastleMask[a.PowerSquare];
                    break;
            }

            // 2) Coup.
            int from = a.From, to = a.To;
            int captured = Piece.Empty, capSq = Square.None, final = Square.None;
            bool pawnMove = false;
            int newEp = Square.None;

            if (a.Kind == ActionKind.Breath)
            {
                capSq = to;
                captured = s[to];
                s[to] = Piece.Empty;
                s[SCool + us] = config.BreathCooldown;
            }
            else
            {
                int piece = s[from];
                if (a.Kind == ActionKind.Teleport)
                {
                    s[SEnergy + us] -= config.TeleportCost;
                }
                else if (a.IsEnPassant)
                {
                    capSq = us == 0 ? to - 8 : to + 8;
                    captured = s[capSq];
                    s[capSq] = Piece.Empty;
                }
                else if (s[to] != Piece.Empty)
                {
                    capSq = to;
                    captured = s[to];
                }

                s[from] = Piece.Empty;
                s[to] = a.IsPromotion ? Piece.Make(usColor, a.Promotion) : piece;
                pawnMove = a.Kind == ActionKind.Normal && Piece.TypeOf(piece) == PieceType.Pawn;
                if (Piece.TypeOf(piece) == PieceType.King) s[SKing + us] = to;
                if (s[SShield + us] == from) s[SShield + us] = to;

                if (a.IsCastle)
                {
                    int rookFrom, rookTo;
                    if (Square.File(to) == 6) { rookFrom = to + 1; rookTo = to - 1; }
                    else { rookFrom = to - 2; rookTo = to + 1; }
                    s[rookTo] = s[rookFrom];
                    s[rookFrom] = Piece.Empty;
                    if (s[SShield + us] == rookFrom) s[SShield + us] = rookTo;
                }
                s[SCastling] &= CastleMask[from] & CastleMask[to];

                final = to;
                // Portail : la pièce ressort par l'autre anneau s'il est libre.
                if (s[SPortalState] == (int)PortalState.Open && (to == s[SPortalA] || to == s[SPortalB]))
                {
                    int exit = to == s[SPortalA] ? s[SPortalB] : s[SPortalA];
                    if (s[exit] == Piece.Empty)
                    {
                        s[exit] = s[to];
                        s[to] = Piece.Empty;
                        if (Piece.TypeOf(s[exit]) == PieceType.King) s[SKing + us] = exit;
                        if (s[SShield + us] == to) s[SShield + us] = exit;
                        final = exit;
                        if (report != null) { report.PortalFrom = to; report.PortalTo = exit; }
                    }
                }

                if (a.Kind == ActionKind.Normal && (a.Flags & MoveFlags.DoublePush) != 0 && final == to)
                {
                    int ep = (from + to) / 2;
                    if (CanCaptureEnPassant(ep, (PieceColor)them)) newEp = ep;
                }
            }

            int gained = 0;
            if (captured != Piece.Empty)
            {
                gained += config.CaptureGain[(int)Piece.TypeOf(captured)];
                if (s[SFrozen + them] == capSq) s[SFrozen + them] = Square.None;
                if (report != null) { report.CapturedPiece = captured; report.CaptureSquare = capSq; }
            }
            if (final != Square.None && final == s[SCrystal])
            {
                gained += config.CrystalGain;
                s[SCrystal] = Square.None;
                if (report != null) report.CrystalSquare = final;
            }
            if (report != null) report.FinalSquare = final;

            s[SEp] = newEp;
            if (pawnMove || captured != Piece.Empty) s[SHalf] = 0;
            else s[SHalf]++;

            bool legal = !IsInCheck(usColor);

            // 3) Fin du tour : bonus de centre, gains, dégel.
            if (config.CenterGain > 0)
            {
                int n = 0;
                foreach (int c in CenterSquares)
                {
                    int p = s[c];
                    if (p != Piece.Empty && (int)Piece.ColorOf(p) == us && Piece.TypeOf(p) != PieceType.Pawn) n++;
                }
                if (n >= config.CenterSquaresNeeded) gained += config.CenterGain;
            }
            s[SEnergy + us] = Clamp(s[SEnergy + us] + gained);
            if (report != null) report.EnergyGained = gained;
            s[SFrozen + us] = Square.None;
            if (us == 1) s[SFull]++;
            s[SPly]++;

            // 4) Début du tour adverse.
            s[SSide] = them;
            s[SShield + them] = Square.None;
            if (s[SCool + them] > 0) s[SCool + them]--;
            TickEvents(usColor, report);

            Hash = ComputeHash();
            return legal;
        }

        /// <summary>Fait avancer les fissures, gouffres et portails d'un demi-coup.</summary>
        void TickEvents(PieceColor justMoved, ActionReport report)
        {
            for (int i = 0; i < HoleSlots; i++)
            {
                int b = SHoles + i * 4;
                var state = (HoleState)s[b + 1];
                if (state == HoleState.None) continue;
                if (--s[b + 2] > 0) continue;

                if (state == HoleState.Chasm)
                {
                    s[b] = Square.None; s[b + 1] = 0; s[b + 2] = 0; s[b + 3] = 0;
                    continue;
                }

                int sq = s[b];
                int piece = s[sq];
                if (piece != Piece.Empty)
                {
                    s[sq] = Piece.Empty;
                    if (IsInCheck(justMoved))
                    {
                        // La chute découvrirait un échec sur le camp qui vient de jouer : report, puis abandon.
                        s[sq] = piece;
                        if (s[b + 3] != 0) { s[b] = Square.None; s[b + 1] = 0; s[b + 2] = 0; s[b + 3] = 0; }
                        else { s[b + 2] = 1; s[b + 3] = 1; }
                        continue;
                    }
                    var pc = Piece.ColorOf(piece);
                    if (s[SFrozen + (int)pc] == sq) s[SFrozen + (int)pc] = Square.None;
                    if (s[SShield + (int)pc] == sq) s[SShield + (int)pc] = Square.None;
                    s[SHalf] = 0;
                    s[SEp] = Square.None;
                    if (report != null) { report.FallenSquares.Add(sq); report.FallenPieces.Add(piece); }
                }
                s[b + 1] = (int)HoleState.Chasm;
                s[b + 2] = config.ChasmDuration;
                s[b + 3] = 0;
                report?.CollapsedSquares.Add(sq);
            }

            var ps = (PortalState)s[SPortalState];
            if (ps != PortalState.None && --s[SPortalTimer] <= 0)
            {
                if (ps == PortalState.Announced)
                {
                    s[SPortalState] = (int)PortalState.Open;
                    s[SPortalTimer] = config.PortalDuration;
                }
                else
                {
                    s[SPortalState] = 0;
                    s[SPortalA] = s[SPortalB] = Square.None;
                    s[SPortalTimer] = 0;
                }
            }
        }

        public void UnmakeAction()
        {
            if (depth == 0) throw new InvalidOperationException("Aucune action à annuler");
            depth--;
            Array.Copy(snapshots[depth], s, Size);
            Hash = hashes[hashes.Count - 1];
            hashes.RemoveAt(hashes.Count - 1);
            actions.RemoveAt(actions.Count - 1);
        }

        /// <summary>Applique une invocation seule, sans historique (génération des coups après invocation).</summary>
        internal void PushSummon(int rookSquare)
        {
            PushSnapshot();
            int us = s[SSide];
            s[rookSquare] = Piece.Make((PieceColor)us, FantasyPiece.Dragon);
            s[SSummoned + us] = 1;
            s[SCool + us] = 1;
            s[SEnergy + us] -= config.SummonCost;
            s[SCastling] &= CastleMask[rookSquare];
        }

        internal void PopSummon()
        {
            depth--;
            Array.Copy(snapshots[depth], s, Size);
        }

        /// <summary>Passer son tour (coup nul de la recherche). Interdit en échec. Ne fait pas avancer les événements.</summary>
        public void MakeNullMove()
        {
            PushSnapshot();
            hashes.Add(Hash);
            actions.Add(FantasyAction.Null);
            int us = s[SSide], them = us ^ 1;
            s[SEp] = Square.None;
            s[SHalf]++;
            s[SFrozen + us] = Square.None;
            s[SSide] = them;
            s[SShield + them] = Square.None;
            Hash = ComputeHash();
        }

        public void UnmakeNullMove() => UnmakeAction();

        // ------------------------------------------------------------------ attaques

        bool CanCaptureEnPassant(int epSquare, PieceColor capturer)
        {
            int pawn = Piece.Make(capturer, PieceType.Pawn);
            foreach (int sq in Tables.PawnAttacks[(int)Piece.Opposite(capturer)][epSquare])
                if (s[sq] == pawn) return true;
            return false;
        }

        CastlingRights SanitizeCastling(CastlingRights cr)
        {
            int wk = Piece.Make(PieceColor.White, PieceType.King), wr = Piece.Make(PieceColor.White, PieceType.Rook);
            int bk = Piece.Make(PieceColor.Black, PieceType.King), br = Piece.Make(PieceColor.Black, PieceType.Rook);
            if (s[4] != wk) cr &= ~(CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide);
            if (s[7] != wr) cr &= ~CastlingRights.WhiteKingSide;
            if (s[0] != wr) cr &= ~CastlingRights.WhiteQueenSide;
            if (s[60] != bk) cr &= ~(CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide);
            if (s[63] != br) cr &= ~CastlingRights.BlackKingSide;
            if (s[56] != br) cr &= ~CastlingRights.BlackQueenSide;
            return cr;
        }

        /// <summary>Case attaquée par le camp « by ». Les pièces gelées contrôlent toujours leurs cases ; le dragon vole.</summary>
        public bool IsSquareAttacked(int sq, PieceColor by)
        {
            int pawn = Piece.Make(by, PieceType.Pawn);
            foreach (int t in Tables.PawnAttacks[(int)Piece.Opposite(by)][sq])
                if (s[t] == pawn) return true;

            int knight = Piece.Make(by, PieceType.Knight);
            foreach (int t in Tables.KnightTargets[sq])
                if (s[t] == knight) return true;

            int king = Piece.Make(by, PieceType.King);
            foreach (int t in Tables.KingTargets[sq])
                if (s[t] == king) return true;

            int queen = Piece.Make(by, PieceType.Queen);
            int rook = Piece.Make(by, PieceType.Rook);
            int bishop = Piece.Make(by, PieceType.Bishop);
            int dragon = Piece.Make(by, FantasyPiece.Dragon);
            int range = config.DragonRange;
            for (int d = 0; d < 8; d++)
            {
                int slider = d < 4 ? rook : bishop;
                var ray = Tables.Rays[d][sq];
                bool blocked = false;
                for (int k = 0; k < ray.Length; k++)
                {
                    int p = s[ray[k]];
                    if (k < range && p == dragon) return true;
                    if (!blocked && p != Piece.Empty)
                    {
                        if (p == slider || p == queen) return true;
                        blocked = true;
                    }
                    if (blocked && k + 1 >= range) break;
                }
            }
            return false;
        }

        /// <summary>La pièce posée sur <paramref name="from"/> attaque-t-elle <paramref name="target"/> ?</summary>
        public bool PieceAttacks(int from, int target)
        {
            int p = s[from];
            if (p == Piece.Empty || from == target) return false;
            var color = Piece.ColorOf(p);
            int df = Square.File(target) - Square.File(from), dr = Square.Rank(target) - Square.Rank(from);
            int adf = Math.Abs(df), adr = Math.Abs(dr);
            switch (Piece.TypeOf(p))
            {
                case PieceType.Pawn:
                    return adf == 1 && dr == (color == PieceColor.White ? 1 : -1);
                case PieceType.Knight:
                    return (adf == 1 && adr == 2) || (adf == 2 && adr == 1);
                case PieceType.King:
                    return Math.Max(adf, adr) == 1;
                case FantasyPiece.Dragon:
                    return (df == 0 || dr == 0 || adf == adr) && Math.Max(adf, adr) <= config.DragonRange;
                case PieceType.Bishop:
                    if (adf != adr) return false;
                    break;
                case PieceType.Rook:
                    if (df != 0 && dr != 0) return false;
                    break;
                case PieceType.Queen:
                    if (df != 0 && dr != 0 && adf != adr) return false;
                    break;
                default:
                    return false;
            }
            int step = Math.Sign(dr) * 8 + Math.Sign(df);
            for (int t = from + step; t != target; t += step)
                if (s[t] != Piece.Empty) return false;
            return true;
        }

        public bool IsInCheck(PieceColor color) => IsSquareAttacked(s[SKing + (int)color], Piece.Opposite(color));

        /// <summary>Occurrences de la position courante (état fantastique compris) depuis le dernier coup irréversible.</summary>
        public int RepetitionCount()
        {
            int count = 1;
            int limit = Math.Min(HalfmoveClock, hashes.Count);
            for (int i = 2; i <= limit; i += 2)
                if (hashes[hashes.Count - i] == Hash) count++;
            return count;
        }

        /// <summary>Matériel insuffisant : comme aux échecs classiques, mais jamais avec un dragon.</summary>
        public bool IsInsufficientMaterial()
        {
            int minors = 0, knights = 0, bishopColors = 0;
            for (int sq = 0; sq < 64; sq++)
            {
                int p = s[sq];
                if (p == Piece.Empty) continue;
                switch (Piece.TypeOf(p))
                {
                    case PieceType.Knight: minors++; knights++; break;
                    case PieceType.Bishop: minors++; bishopColors |= Square.IsLight(sq) ? 2 : 1; break;
                    case PieceType.King: break;
                    default: return false;
                }
            }
            if (minors <= 1) return true;
            return knights == 0 && bishopColors != 3;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int rank = 7; rank >= 0; rank--)
            {
                for (int file = 0; file < 8; file++) sb.Append(FantasyPiece.ToFenChar(s[Square.Make(file, rank)]));
                sb.Append('\n');
            }
            sb.Append("éclats ").Append(Energy(PieceColor.White)).Append('/').Append(Energy(PieceColor.Black));
            return sb.ToString();
        }
    }
}
