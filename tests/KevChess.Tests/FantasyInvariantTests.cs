using System;
using System.Linq;
using KevChess.Core;
using KevChess.Core.Fantasy;
using NUnit.Framework;

namespace KevChess.Tests
{
    /// <summary>
    /// Parties aléatoires du mode Légendes, plateau vivant actif et jauges tirées au hasard : à chaque action légale,
    /// on vérifie les invariants du § 2 (pièce gelée immobile, roque compris ; personne sur un gouffre ; roi jamais
    /// sur une fissure ; bouclier jamais pris ; jauges 0-10 ; roi jamais laissé en échec ; hachage et annulation exacts).
    /// Cette vérification a trouvé la prise en passant qui s'arrêtait sur un gouffre.
    /// </summary>
    public class FantasyInvariantTests
    {
        [Test]
        public void RandomPlayoutsKeepInvariants()
        {
            var rnd = new Random(7);
            int errors = 0; string first = null;
            for (int g = 0; g < 300; g++)
            {
                var game = new FantasyGame((ulong)rnd.Next());
                game.Position.SetEnergy(PieceColor.White, rnd.Next(0, 11));
                game.Position.SetEnergy(PieceColor.Black, rnd.Next(0, 11));
                game.RefreshAfterSetup();
                for (int ply = 0; ply < 200 && !game.IsOver; ply++)
                {
                    var pos = game.Position;
                    var a = game.LegalActions[rnd.Next(game.LegalActions.Count)];
                    var us = pos.SideToMove; var them = Piece.Opposite(us);
                    int frozen = pos.FrozenSquare(us);
                    int shieldThem = pos.ShieldSquare(them);
                    int shieldedPiece = shieldThem >= 0 && shieldThem < 64 ? pos[shieldThem] : 0;
                    ulong h = pos.Hash;
                    string err = null;
                    if (frozen >= 0 && frozen < 64 && a.From == frozen) err = "frozen moves " + a;
                    if (a.IsCastle && frozen == (Square.File(a.To) == 6 ? a.To + 1 : a.To - 2)) err = "castle frozen " + a;
                    game.TryMakeAction(a);
                    pos = game.Position;
                    for (int sq = 0; sq < 64; sq++)
                    {
                        if (pos[sq] != Piece.Empty && pos.IsChasm(sq)) err = err ?? ("piece on chasm " + Square.Name(sq) + " after " + a);
                        if (pos[sq] != Piece.Empty && Piece.TypeOf(pos[sq]) == PieceType.King && pos.HoleAt(sq) != HoleState.None) err = err ?? ("king on hole " + a);
                    }
                    if (shieldedPiece != 0 && game.LastReport.CaptureSquare == shieldThem) err = err ?? ("shield captured " + a);
                    for (int c = 0; c < 2; c++) if (pos.Energy((PieceColor)c) < 0 || pos.Energy((PieceColor)c) > 10) err = err ?? "energy";
                    if (pos.IsInCheck(us)) err = err ?? ("left in check " + a);
                    if (pos.ComputeHash() != pos.Hash) err = err ?? "hash";
                    game.Undo();
                    if (game.Position.Hash != h) err = err ?? ("undo hash " + a);
                    game.TryMakeAction(a);
                    if (err != null) { errors++; first = first ?? err + " | " + string.Join(" ", game.Notations); break; }
                }
            }
            Assert.That(errors, Is.EqualTo(0), first);
        }
    }
}
