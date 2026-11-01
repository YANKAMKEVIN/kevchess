using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KevChess.Core.Fantasy
{
    /// <summary>
    /// Partie du mode Légendes : actions légales, historique (notation § 2.7), annulation, événements du
    /// plateau, fins de partie et sauvegarde.
    /// </summary>
    public sealed class FantasyGame
    {
        readonly FantasyPosition position;
        readonly string startFen;
        readonly List<FantasyAction> actions = new List<FantasyAction>();
        readonly List<string> notations = new List<string>();
        readonly List<BoardEvent> spawned = new List<BoardEvent>();
        List<FantasyAction> legal;

        public FantasyGame(ulong seed, FantasyConfig config = null, string fen = null)
        {
            Seed = seed;
            startFen = fen ?? FantasyPosition.StartFen;
            position = FantasyPosition.FromFen(startFen, config);
            Refresh();
        }

        public ulong Seed { get; }
        public string StartFen => startFen;
        /// <summary>Position courante. Ne pas la modifier : passer par TryMakeAction / Undo.</summary>
        public FantasyPosition Position => position;
        public FantasyConfig Config => position.Config;
        public PieceColor SideToMove => position.SideToMove;
        public IReadOnlyList<FantasyAction> Actions => actions;
        public IReadOnlyList<string> Notations => notations;
        public IReadOnlyList<FantasyAction> LegalActions => legal;
        public GameResult Result { get; private set; }
        public GameEndReason EndReason { get; private set; }
        public bool IsOver => Result != GameResult.Ongoing;
        public bool InCheck => position.InCheck;
        public FantasyAction LastAction => actions.Count == 0 ? FantasyAction.Null : actions[actions.Count - 1];
        /// <summary>Détails de la dernière action jouée (prise, portail, chutes, éclats).</summary>
        public ActionReport LastReport { get; } = new ActionReport();
        /// <summary>Événement tiré après la dernière action (BoardEvent.None sinon).</summary>
        public BoardEvent LastEvent => spawned.Count == 0 ? BoardEvent.None : spawned[spawned.Count - 1];

        public event Action Changed;

        public bool IsLegal(FantasyAction a)
        {
            if (IsOver) return false;
            foreach (var l in legal) if (l == a) return true;
            return false;
        }

        /// <summary>Actions légales avec ce pouvoir (PowerKind.None : sans pouvoir) depuis cette case.</summary>
        public IEnumerable<FantasyAction> ActionsFrom(int square, PowerKind power = PowerKind.None, int powerSquare = -1)
        {
            foreach (var a in legal)
            {
                if (a.From != square || a.Power != power) continue;
                if (power != PowerKind.None && a.PowerSquare != powerSquare) continue;
                yield return a;
            }
        }

        /// <summary>Cases cibles possibles d'un pouvoir (Bouclier, Gel, Invocation).</summary>
        public IEnumerable<int> PowerTargets(PowerKind power)
        {
            var seen = new HashSet<int>();
            foreach (var a in legal)
                if (a.Power == power && seen.Add(a.PowerSquare)) yield return a.PowerSquare;
        }

        public bool CanTeleport
        {
            get { foreach (var a in legal) if (a.Kind == ActionKind.Teleport) return true; return false; }
        }

        public bool TryMakeAction(FantasyAction a)
        {
            if (!IsLegal(a)) return false;
            notations.Add(FantasyNotation.ToText(position, a, legal));
            position.Report = LastReport;
            position.MakeAction(a);
            position.Report = null;
            actions.Add(a);
            spawned.Add(EventPlanner.Spawn(position, Seed));
            Refresh();
            Changed?.Invoke();
            return true;
        }

        public bool Undo()
        {
            if (actions.Count == 0) return false;
            position.UnmakeAction();
            actions.RemoveAt(actions.Count - 1);
            notations.RemoveAt(notations.Count - 1);
            spawned.RemoveAt(spawned.Count - 1);
            LastReport.Clear();
            Refresh();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Recalcule les actions légales après une mise en place manuelle de la position (tests, scénarios).</summary>
        public void RefreshAfterSetup() => Refresh();

        public void Resign(PieceColor loser)
        {
            if (IsOver) return;
            Result = loser == PieceColor.White ? GameResult.BlackWins : GameResult.WhiteWins;
            EndReason = GameEndReason.Resignation;
            Changed?.Invoke();
        }

        public void AgreeDraw()
        {
            if (IsOver) return;
            Result = GameResult.Draw;
            EndReason = GameEndReason.DrawAgreement;
            Changed?.Invoke();
        }

        void Refresh()
        {
            legal = FantasyMoveGenerator.Legal(position);
            Result = GameResult.Ongoing;
            EndReason = GameEndReason.None;
            if (legal.Count == 0)
            {
                if (position.InCheck)
                {
                    Result = position.SideToMove == PieceColor.White ? GameResult.BlackWins : GameResult.WhiteWins;
                    EndReason = GameEndReason.Checkmate;
                }
                else
                {
                    Result = GameResult.Draw;
                    EndReason = GameEndReason.Stalemate;
                }
            }
            else if (position.IsInsufficientMaterial())
            {
                Result = GameResult.Draw;
                EndReason = GameEndReason.InsufficientMaterial;
            }
            else if (position.RepetitionCount() >= 3)
            {
                Result = GameResult.Draw;
                EndReason = GameEndReason.ThreefoldRepetition;
            }
            else if (position.HalfmoveClock >= 100)
            {
                Result = GameResult.Draw;
                EndReason = GameEndReason.FiftyMoveRule;
            }
        }

        // ------------------------------------------------------------------ sauvegarde

        /// <summary>Texte de sauvegarde : version, graine, FEN de départ, actions (entiers).</summary>
        public string Save()
        {
            var sb = new StringBuilder();
            sb.Append("L1|").Append(Seed.ToString(CultureInfo.InvariantCulture)).Append('|').Append(startFen).Append('|');
            for (int i = 0; i < actions.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(actions[i].Raw.ToString(CultureInfo.InvariantCulture));
            }
            if (Result != GameResult.Ongoing && (EndReason == GameEndReason.Resignation || EndReason == GameEndReason.DrawAgreement))
                sb.Append('|').Append((int)Result).Append(':').Append((int)EndReason);
            return sb.ToString();
        }

        /// <summary>Recharge une sauvegarde en rejouant les actions. Renvoie null si le texte est invalide.</summary>
        public static FantasyGame Load(string text, FantasyConfig config = null)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var parts = text.Split('|');
            if (parts.Length < 4 || parts[0] != "L1") return null;
            if (!ulong.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong seed)) return null;
            FantasyGame game;
            try { game = new FantasyGame(seed, config, parts[2]); }
            catch (FormatException) { return null; }
            if (parts[3].Length > 0)
                foreach (var token in parts[3].Split(','))
                {
                    if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int raw)) return null;
                    if (!game.TryMakeAction(FantasyAction.FromRaw(raw))) return null;
                }
            if (parts.Length > 4)
            {
                var end = parts[4].Split(':');
                if (end.Length == 2 && int.TryParse(end[0], out int r) && int.TryParse(end[1], out int reason))
                {
                    game.Result = (GameResult)r;
                    game.EndReason = (GameEndReason)reason;
                }
            }
            return game;
        }
    }

    /// <summary>Notation du mode Légendes : SAN, dragon = D, pouvoir entre crochets avant le coup.</summary>
    public static class FantasyNotation
    {
        public static string ToText(FantasyPosition pos, FantasyAction a, IReadOnlyList<FantasyAction> legal)
        {
            var sb = new StringBuilder(16);
            if (a.HasPower) sb.Append('[').Append("-SFD"[(int)a.Power]).Append(' ').Append(Square.Name(a.PowerSquare)).Append("] ");

            int piece = pos[a.From];
            var type = Piece.TypeOf(piece);
            if (a.Power == PowerKind.Summon && a.From == a.PowerSquare) type = FantasyPiece.Dragon;

            if (a.Kind == ActionKind.Teleport)
            {
                sb.Append(FantasyPiece.SanLetter(type)).Append(Square.Name(a.From)).Append('~').Append(Square.Name(a.To));
            }
            else if (a.Kind == ActionKind.Breath)
            {
                sb.Append('D').Append(Square.Name(a.From)).Append('^').Append(Square.Name(a.To));
            }
            else if (a.IsCastle)
            {
                sb.Append(Square.File(a.To) == 6 ? "O-O" : "O-O-O");
            }
            else
            {
                if (type == PieceType.Pawn)
                {
                    if (a.IsCapture) sb.Append((char)('a' + Square.File(a.From)));
                }
                else
                {
                    sb.Append(FantasyPiece.SanLetter(type));
                    AppendDisambiguation(pos, a, type, legal, sb);
                }
                if (a.IsCapture) sb.Append('x');
                sb.Append(Square.Name(a.To));
                if (a.IsPromotion) sb.Append('=').Append(Piece.SanLetter(a.Promotion));
            }

            var saved = pos.Report;
            pos.Report = null;
            pos.MakeAction(a);
            if (pos.InCheck) sb.Append(FantasyMoveGenerator.HasLegalAction(pos) ? '+' : '#');
            pos.UnmakeAction();
            pos.Report = saved;
            return sb.ToString();
        }

        static void AppendDisambiguation(FantasyPosition pos, FantasyAction a, PieceType type, IReadOnlyList<FantasyAction> legal, StringBuilder sb)
        {
            bool ambiguous = false, sameFile = false, sameRank = false;
            foreach (var other in legal)
            {
                if (other.Kind != ActionKind.Normal || other.Power != a.Power || other.PowerSquare != a.PowerSquare) continue;
                if (other.To != a.To || other.From == a.From) continue;
                if (Piece.TypeOf(pos[other.From]) != type) continue;
                ambiguous = true;
                if (Square.File(other.From) == Square.File(a.From)) sameFile = true;
                if (Square.Rank(other.From) == Square.Rank(a.From)) sameRank = true;
            }
            if (!ambiguous) return;
            if (!sameFile) sb.Append((char)('a' + Square.File(a.From)));
            else if (!sameRank) sb.Append((char)('1' + Square.Rank(a.From)));
            else sb.Append(Square.Name(a.From));
        }
    }
}
