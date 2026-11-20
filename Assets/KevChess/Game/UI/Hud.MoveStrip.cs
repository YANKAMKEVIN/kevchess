using System.Collections.Generic;
using KevChess.Core;
using KevChess.Core.Ux;
using KevChess.Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace KevChess.Game.UI
{
    /// <summary>
    /// Ligne des coups sous le statut : derniers demi-coups touchables (revue sur ce coup), le dernier en couleur d'accent ;
    /// un appui long ouvre la liste complète des coups.
    /// </summary>
    public sealed partial class Hud
    {
        const int StripPlies = 8;

        RectTransform movesRow;
        readonly List<Text> moveTokens = new List<Text>();
        readonly List<int> moveTokenPlies = new List<int>();
        GameObject moveListOverlay;
        RectTransform moveListContent;
        ScrollRect moveListScroll;

        void BuildMoveStrip()
        {
            movesRow = UIKit.Rect("Moves", hudGroup.transform);
            UIKit.Anchor(movesRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -246), new Vector2(1000, 44));
            var h = UIKit.HStack(movesRow, 18);
            h.childForceExpandWidth = false;
            for (int i = 0; i < StripPlies; i++)
            {
                int slot = i;
                var t = UIKit.Label("Ply" + i, movesRow, "", 30, UIKit.TextDim);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.raycastTarget = true; // touchable
                var press = t.gameObject.AddComponent<LongPress>();
                press.Tapped = () => OnMoveTokenTapped(slot);
                press.Held = OpenMoveList;
                moveTokens.Add(t);
                moveTokenPlies.Add(-1);
            }
        }

        /// <summary>Affiche les demi-coups avant <paramref name="upTo"/> ; <paramref name="current"/> = demi-coup mis en avant.</summary>
        void SetMoveStrip(ChessGame game, int upTo, int current, Color currentColor)
        {
            var tokens = MoveStrip.Recent(game.StartFen, game.SanMoves, upTo, StripPlies, GamePrefs.San);
            for (int i = 0; i < moveTokens.Count; i++)
            {
                bool used = i < tokens.Count;
                moveTokens[i].gameObject.SetActive(used);
                moveTokenPlies[i] = used ? tokens[i].Ply : -1;
                if (!used) continue;
                moveTokens[i].text = tokens[i].Text;
                bool on = tokens[i].Ply == current;
                moveTokens[i].color = on ? currentColor : UIKit.TextDim;
                moveTokens[i].fontStyle = on ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        bool CanReviewFromStrip => match.Game != null && !match.IsPuzzle && !match.IsAnimating && !IsBlockingOverlayOpen;

        void OnMoveTokenTapped(int slot)
        {
            int ply = moveTokenPlies[slot];
            if (ply < 0) return;
            if (reviewing) { sfx.Play(Sfx.Click); ShowReviewPly(ply + 1); return; }
            if (!CanReviewFromStrip) return;
            sfx.Play(Sfx.Click);
            EnterReview(ply + 1);
        }

        // ---------------------------------------------------------------- Liste complète

        void BuildMoveList()
        {
            moveListOverlay = Overlay("MoveList", out var card, 900, 1500, UIKit.Cyan);
            UIKit.VStack(card, 16, 44);
            UIKit.Size(UIKit.Label("Title", card, "Coups de la partie", 64, UIKit.Cyan, TextAnchor.MiddleCenter, FontStyle.Bold), 90);
            UIKit.Size(UIKit.Label("Hint", card, "Toucher un coup pour revoir la position", 30, UIKit.TextDim), 44);
            moveListContent = UIKit.ScrollList("List", card, 8, out moveListScroll);
            UIKit.Size(moveListScroll, 1100).flexibleHeight = 1;
            UIKit.Size(UIKit.Button("Close", card, "Fermer", () => { sfx.Play(Sfx.Click); Show(moveListOverlay, false); }, UIKit.TextDim), 100);
        }

        void OpenMoveList()
        {
            var game = match.Game;
            if (game == null || game.Moves.Count == 0 || match.IsPuzzle || match.IsAnimating) return;
            if (IsBlockingOverlayOpen && !reviewing) return;
            sfx.Play(Sfx.Select);
            for (int i = moveListContent.childCount - 1; i >= 0; i--) Destroy(moveListContent.GetChild(i).gameObject);
            int current = reviewing ? reviewPly - 1 : game.Moves.Count - 1;
            foreach (var row in MoveStrip.FullMoves(game.StartFen, game.SanMoves, GamePrefs.San))
            {
                var line = UIKit.Rect("Move" + row.number, moveListContent);
                var hs = UIKit.HStack(line, 12);
                hs.childForceExpandWidth = false;
                UIKit.Size(line.gameObject.AddComponent<LayoutElement>(), 80);
                UIKit.Size(UIKit.Label("Number", line, row.number + ".", 34, UIKit.TextDim, TextAnchor.MiddleRight), -1, 110);
                MoveListCell(line, row.white, current, UIKit.Cyan);
                MoveListCell(line, row.black, current, UIKit.Magenta);
            }
            Show(moveListOverlay, true);
            Canvas.ForceUpdateCanvases();
            moveListScroll.verticalNormalizedPosition = 0f; // derniers coups visibles
        }

        void MoveListCell(RectTransform line, MoveToken token, int current, Color accent)
        {
            if (token.Ply < 0)
            {
                UIKit.Size(UIKit.Label("Empty", line, "…", 34, UIKit.TextDim), -1, 330);
                return;
            }
            int ply = token.Ply;
            var b = UIKit.Button("Ply" + ply, line, token.Text, () =>
            {
                sfx.Play(Sfx.Click);
                Show(moveListOverlay, false);
                if (reviewing) ShowReviewPly(ply + 1);
                else EnterReview(ply + 1);
            }, accent, 34);
            UIKit.Size(b, -1, 330);
            if (ply == current) Highlight(b, true, accent);
        }
    }
}
