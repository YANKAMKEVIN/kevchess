using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using KevChess.Core;

namespace KevChess.AI.Uci
{
    /// <summary>Ligne « info » d'un moteur UCI (seuls les champs utiles au jeu et au journal).</summary>
    public sealed class UciInfo
    {
        public int Depth;
        public int SelDepth;
        public int MultiPv = 1;
        /// <summary>Vrai si la ligne contenait un score (cp ou mate).</summary>
        public bool HasScore;
        /// <summary>Score en centipions du point de vue du camp au trait (si ce n'est pas un mat).</summary>
        public int ScoreCp;
        /// <summary>Mat annoncé en coups (positif : le camp au trait mate ; négatif : il se fait mater) ; 0 sinon.</summary>
        public int MateIn;
        /// <summary>Le score n'est qu'une borne (« lowerbound » / « upperbound ») : à ne pas prendre pour le résultat.</summary>
        public bool IsBound;
        public long Nodes;
        public long Nps;
        public long TimeMs;
        public int HashFull;
        public readonly List<string> Pv = new List<string>();
        /// <summary>Texte libre (« info string ... ») ; null sinon.</summary>
        public string Text;

        public bool IsMate => HasScore && MateIn != 0;

        /// <summary>Score dans l'échelle de Searcher (mats compris : MateScore − demi-coups).</summary>
        public int Score
        {
            get
            {
                if (MateIn > 0) return Searcher.MateScore - (2 * MateIn - 1);
                if (MateIn < 0) return -(Searcher.MateScore - 2 * -MateIn);
                return ScoreCp;
            }
        }
    }

    /// <summary>Réponse « bestmove » : coup en notation UCI (null pour « (none) » / « 0000 ») et coup de réflexion éventuel.</summary>
    public struct UciBestMove
    {
        public string Move;
        public string Ponder;
        public bool IsNone => string.IsNullOrEmpty(Move);
    }

    /// <summary>Réglages Stockfish d'un niveau de jeu.</summary>
    public sealed class UciLevel
    {
        public Difficulty Level;
        /// <summary>Option « Skill Level » de Stockfish (0 à 20).</summary>
        public int SkillLevel;
        /// <summary>Profondeur max (0 = sans limite, seul le temps compte).</summary>
        public int MaxDepth;
        /// <summary>Temps de réflexion (ms) ; mêmes plafonds que l'IA maison pour garder le rythme du jeu.</summary>
        public int MoveTimeMs;
    }

    /// <summary>
    /// Protocole UCI en C# pur : formatage des commandes et lecture des réponses.
    /// Voir la spécification UCI (Stefan Meyer-Kahlen) ; testé avec un faux moteur hors Unity.
    /// </summary>
    public static class UciProtocol
    {
        /// <summary>
        /// Niveaux Stockfish : valeurs de départ, à régler au jeu. Le temps reprend les plafonds de ChessAI
        /// (le rythme du jeu ne change pas en passant d'un moteur à l'autre) ; la faiblesse vient de « Skill Level »
        /// et, pour les deux premiers niveaux, d'une profondeur bornée.
        /// </summary>
        static readonly UciLevel[] Levels =
        {
            new UciLevel { Level = Difficulty.Debutant, SkillLevel = 0,  MaxDepth = 1, MoveTimeMs = 300 },
            new UciLevel { Level = Difficulty.Facile,   SkillLevel = 3,  MaxDepth = 3, MoveTimeMs = 500 },
            new UciLevel { Level = Difficulty.Moyen,    SkillLevel = 8,  MaxDepth = 0, MoveTimeMs = 900 },
            new UciLevel { Level = Difficulty.Fort,     SkillLevel = 14, MaxDepth = 0, MoveTimeMs = 1500 },
            new UciLevel { Level = Difficulty.Maitre,   SkillLevel = 20, MaxDepth = 0, MoveTimeMs = 3000 },
        };

        public static UciLevel GetLevel(Difficulty level) => Levels[(int)level];

        // ------------------------------------------------------------------ Commandes

        public static string SetOption(string name, string value) =>
            value == null ? "setoption name " + name : "setoption name " + name + " value " + value;

        public static string SetOption(string name, int value) => SetOption(name, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>« position fen ... » pour une position isolée (sans historique : les répétitions ne sont pas connues du moteur).</summary>
        public static string PositionCommand(Position position) =>
            PositionCommand(position.ToFen(), null);

        /// <summary>
        /// « position startpos|fen ... [moves ...] » : avec l'historique, le moteur connaît les répétitions et la règle des 50 coups.
        /// </summary>
        public static string PositionCommand(string startFen, IEnumerable<Move> moves)
        {
            var sb = new StringBuilder("position ");
            if (string.IsNullOrEmpty(startFen) || startFen.Trim() == Position.StartFen) sb.Append("startpos");
            else sb.Append("fen ").Append(startFen.Trim());
            if (moves != null)
            {
                bool first = true;
                foreach (var m in moves)
                {
                    if (first) { sb.Append(" moves"); first = false; }
                    sb.Append(' ').Append(m.ToUci());
                }
            }
            return sb.ToString();
        }

        /// <summary>« go » à temps fixe, avec une profondeur max éventuelle (le moteur s'arrête à la première limite atteinte).</summary>
        public static string GoMoveTime(int moveTimeMs, int maxDepth = 0)
        {
            var sb = new StringBuilder("go");
            if (maxDepth > 0) sb.Append(" depth ").Append(maxDepth.ToString(CultureInfo.InvariantCulture));
            sb.Append(" movetime ").Append(Math.Max(1, moveTimeMs).ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>« go » avec la pendule (le moteur gère lui-même son temps).</summary>
        public static string GoClock(long whiteMs, long blackMs, long whiteIncMs, long blackIncMs, int maxDepth = 0)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder("go");
            if (maxDepth > 0) sb.Append(" depth ").Append(maxDepth.ToString(c));
            sb.Append(" wtime ").Append(Math.Max(0, whiteMs).ToString(c))
              .Append(" btime ").Append(Math.Max(0, blackMs).ToString(c));
            if (whiteIncMs > 0) sb.Append(" winc ").Append(whiteIncMs.ToString(c));
            if (blackIncMs > 0) sb.Append(" binc ").Append(blackIncMs.ToString(c));
            return sb.ToString();
        }

        // ------------------------------------------------------------------ Réponses

        static string[] Tokens(string line) =>
            (line ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>Premier mot d'une ligne du moteur (« id », « uciok », « readyok », « info », « bestmove »...), en minuscules.</summary>
        public static string Keyword(string line)
        {
            var t = Tokens(line);
            return t.Length == 0 ? "" : t[0].ToLowerInvariant();
        }

        /// <summary>Lit « bestmove &lt;coup&gt; [ponder &lt;coup&gt;] ». Faux si la ligne n'est pas un bestmove.</summary>
        public static bool TryParseBestMove(string line, out UciBestMove best)
        {
            best = default;
            var t = Tokens(line);
            if (t.Length == 0 || t[0] != "bestmove") return false;
            if (t.Length >= 2 && t[1] != "(none)" && t[1] != "0000" && t[1] != "none") best.Move = t[1];
            for (int i = 2; i + 1 < t.Length; i++)
                if (t[i] == "ponder") { best.Ponder = t[i + 1]; break; }
            return true;
        }

        /// <summary>Lit une ligne « info ». Faux si la ligne n'en est pas une. Les mots inconnus sont ignorés.</summary>
        public static bool TryParseInfo(string line, out UciInfo info)
        {
            info = null;
            var t = Tokens(line);
            if (t.Length == 0 || t[0] != "info") return false;
            info = new UciInfo();
            for (int i = 1; i < t.Length; i++)
            {
                switch (t[i])
                {
                    case "depth": info.Depth = Int(t, ++i); break;
                    case "seldepth": info.SelDepth = Int(t, ++i); break;
                    case "multipv": info.MultiPv = Int(t, ++i); break;
                    case "nodes": info.Nodes = Long(t, ++i); break;
                    case "nps": info.Nps = Long(t, ++i); break;
                    case "time": info.TimeMs = Long(t, ++i); break;
                    case "hashfull": info.HashFull = Int(t, ++i); break;
                    case "tbhits": case "cpuload": case "currmove": case "currmovenumber": case "refutation": case "currline":
                        i++; break;
                    case "score":
                        while (i + 1 < t.Length)
                        {
                            string k = t[i + 1];
                            if (k == "cp") { info.HasScore = true; info.MateIn = 0; info.ScoreCp = Int(t, i + 2); i += 2; }
                            else if (k == "mate") { info.HasScore = true; info.MateIn = Int(t, i + 2); i += 2; }
                            else if (k == "lowerbound" || k == "upperbound") { info.IsBound = true; i++; }
                            else break;
                        }
                        break;
                    case "pv":
                        // La variante court jusqu'au prochain mot-clé connu (en pratique jusqu'à la fin de la ligne).
                        while (i + 1 < t.Length && IsUciMove(t[i + 1])) info.Pv.Add(t[++i]);
                        break;
                    case "string":
                        info.Text = string.Join(" ", t, i + 1, t.Length - i - 1);
                        i = t.Length;
                        break;
                }
            }
            return true;
        }

        /// <summary>Forme d'un coup UCI : deux cases (a1-h8) et une promotion éventuelle (q, r, b, n).</summary>
        public static bool IsUciMove(string s)
        {
            if (s == null || (s.Length != 4 && s.Length != 5)) return false;
            if (s[0] < 'a' || s[0] > 'h' || s[1] < '1' || s[1] > '8' || s[2] < 'a' || s[2] > 'h' || s[3] < '1' || s[3] > '8') return false;
            return s.Length == 4 || "qrbn".IndexOf(s[4]) >= 0;
        }

        static int Int(string[] t, int i)
        {
            if (i < t.Length && int.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
            return 0;
        }

        static long Long(string[] t, int i)
        {
            if (i < t.Length && long.TryParse(t[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out long v)) return v;
            return 0;
        }
    }
}
