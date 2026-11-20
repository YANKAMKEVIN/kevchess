using KevChess.Core;
using KevChess.Core.Ux;
using KevChess.Game.Match;
using UnityEngine;
using UnityEngine.UI;

namespace KevChess.Game.UI
{
    /// <summary>Carte de l'IA vivante pendant sa réflexion : reflet sur le liseré, pastille qui pulse, éclat quand le coup part.</summary>
    public sealed partial class Hud
    {
        readonly RectTransform[] cardRects = new RectTransform[2];
        readonly Image[] cardOutlines = new Image[2], cardDots = new Image[2], cardGlints = new Image[2];
        readonly Color[] cardAccents = new Color[2];
        float aiMoveAt = -10f;
        PieceColor aiMoveColor;

        void RegisterCardFx(PieceColor color, Image card, Image dot, Color accent)
        {
            int i = (int)color;
            cardRects[i] = card.rectTransform;
            var outline = card.transform.Find("Outline");
            cardOutlines[i] = outline != null ? outline.GetComponent<Image>() : null;
            cardDots[i] = dot;
            cardAccents[i] = accent;
            var glint = new GameObject("Glint", typeof(RectTransform)).AddComponent<Image>();
            glint.transform.SetParent(card.transform, false);
            glint.sprite = UIKit.Glow;
            glint.color = Color.Lerp(accent, Color.white, 0.5f);
            glint.raycastTarget = false;
            glint.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            UIKit.Anchor(glint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90));
            glint.gameObject.SetActive(false);
            cardGlints[i] = glint;
        }

        /// <summary>Coup posé : s'il vient de l'IA, sa carte jette un bref éclat.</summary>
        void NoteAIMove()
        {
            if (match.Settings.Mode != MatchMode.VersusAI || match.Game.Moves.Count == 0) return;
            var mover = Piece.Opposite(match.Game.SideToMove);
            if (match.Settings.IsHuman(mover)) return;
            aiMoveAt = Time.unscaledTime;
            aiMoveColor = mover;
        }

        void UpdateThinkingGlow()
        {
            bool thinking = !reviewing && match.Settings.Mode == MatchMode.VersusAI && match.IsAIThinking && !match.Game.IsOver;
            var aiColor = Piece.Opposite(match.Settings.HumanColor);
            float now = Time.unscaledTime;
            for (int i = 0; i < 2; i++)
            {
                if (cardRects[i] == null) continue;
                bool active = thinking && i == (int)aiColor;
                float flash = (int)aiMoveColor == i ? ThinkingGlow.Flash(now - aiMoveAt) : 0f;

                var glint = cardGlints[i];
                if (glint.gameObject.activeSelf != active) glint.gameObject.SetActive(active);
                if (active)
                {
                    var r = cardRects[i].rect;
                    ThinkingGlow.PerimeterPoint(now / ThinkingGlow.LapSeconds, r.width - 10f, r.height - 10f, out float x, out float y);
                    glint.rectTransform.anchoredPosition = new Vector2(x, y);
                }

                cardDots[i].rectTransform.localScale = Vector3.one * (active ? ThinkingGlow.DotScale(now) : 1f + 0.35f * flash);
                if (cardOutlines[i] != null)
                {
                    float a = 0.55f + (active ? 0.2f : 0f) + 0.45f * flash;
                    cardOutlines[i].color = Color.Lerp(cardAccents[i], Color.white, 0.6f * flash) * new Color(1, 1, 1, Mathf.Min(1f, a));
                }
            }
        }
    }
}
