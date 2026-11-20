using System;
using System.Collections.Generic;

namespace KevChess.Core.Ux
{
    /// <summary>Un demi-coup de la ligne des coups : son index et son texte (« 12. Cf3 », « Cxd4 », « 12… Cxd4 »).</summary>
    public struct MoveToken
    {
        public int Ply;
        public string Text;
    }

    /// <summary>Ligne des derniers coups sous le statut, découpée en demi-coups touchables, et liste complète par coup.</summary>
    public static class MoveStrip
    {
        /// <summary>Numéro du coup et camp du demi-coup <paramref name="ply"/> (0 = premier coup joué).</summary>
        public static void Number(string startFen, int ply, out int number, out bool whiteMove)
        {
            var start = Position.FromFen(string.IsNullOrEmpty(startFen) ? Position.StartFen : startFen);
            int plyIndex = ply + (start.SideToMove == PieceColor.Black ? 1 : 0);
            number = start.FullmoveNumber + plyIndex / 2;
            whiteMove = plyIndex % 2 == 0;
        }

        /// <summary>
        /// Les <paramref name="maxPlies"/> derniers demi-coups avant <paramref name="upTo"/> (exclu).
        /// Le numéro précède chaque coup blanc, et le premier coup noir de la ligne (« 12… »).
        /// </summary>
        public static List<MoveToken> Recent(string startFen, IReadOnlyList<string> san, int upTo, int maxPlies, Func<string, string> localize = null)
        {
            var tokens = new List<MoveToken>();
            upTo = Math.Min(upTo, san.Count);
            int first = Math.Max(0, upTo - maxPlies);
            for (int i = first; i < upTo; i++)
            {
                Number(startFen, i, out int number, out bool white);
                string move = localize != null ? localize(san[i]) : san[i];
                string prefix = white ? number + ". " : i == first ? number + "… " : "";
                tokens.Add(new MoveToken { Ply = i, Text = prefix + move });
            }
            return tokens;
        }

        /// <summary>Une ligne par coup : numéro, demi-coup blanc (Ply -1 s'il manque), demi-coup noir (Ply -1 s'il manque).</summary>
        public static List<(int number, MoveToken white, MoveToken black)> FullMoves(string startFen, IReadOnlyList<string> san, Func<string, string> localize = null)
        {
            var rows = new List<(int, MoveToken, MoveToken)>();
            var none = new MoveToken { Ply = -1, Text = "" };
            for (int i = 0; i < san.Count; i++)
            {
                Number(startFen, i, out int number, out bool white);
                var token = new MoveToken { Ply = i, Text = localize != null ? localize(san[i]) : san[i] };
                if (white) rows.Add((number, token, none));
                else if (rows.Count > 0 && rows[rows.Count - 1].Item1 == number && rows[rows.Count - 1].Item3.Ply < 0)
                    rows[rows.Count - 1] = (number, rows[rows.Count - 1].Item2, token);
                else rows.Add((number, none, token));
            }
            return rows;
        }
    }
}
