using KevChess.Core;
using KevChess.Core.Ux;
using KevChess.Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace KevChess.Game.UI
{
    /// <summary>Bulles d'accueil de la première partie : jouer un coup, tourner la vue, recentrer.</summary>
    public sealed partial class Hud
    {
        BoardTips tips;
        GameObject tipsBubble;
        Text tipsText, tipsCounter;

        void BuildTips()
        {
            tips = new BoardTips(GamePrefs.TipsStep);

            var bubble = UIKit.Panel("Tips", hudGroup.transform, UIKit.GlassStrong, true, UIKit.Cyan);
            tipsBubble = bubble.gameObject;
            UIKit.Anchor(bubble.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 330), new Vector2(960, 170));

            tipsCounter = UIKit.Label("Counter", bubble.transform, "", 26, UIKit.Cyan, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Stretch(tipsCounter.rectTransform, 28, 120, 220, 14);
            tipsText = UIKit.Label("Text", bubble.transform, "", 34, UIKit.Text, TextAnchor.MiddleLeft);
            UIKit.Stretch(tipsText.rectTransform, 28, 14, 220, 46);
            tipsText.resizeTextForBestFit = true;
            tipsText.resizeTextMinSize = 22;
            tipsText.resizeTextMaxSize = 34;

            var skip = UIKit.Button("Skip", bubble.transform, "Passer", () =>
            {
                sfx.Play(Sfx.Click);
                tips.Skip();
                SaveTips();
            }, UIKit.TextDim, 30);
            UIKit.Anchor((RectTransform)skip.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22, 0), new Vector2(170, 96));
            tipsBubble.SetActive(false);
        }

        void SaveTips() => GamePrefs.TipsStep = tips.Step;

        /// <summary>Glissement de caméra (pixels), pour la bulle « tourner la vue ».</summary>
        public void NotifyCameraOrbited(Vector2 pixelDelta)
        {
            if (tips != null && tips.OnOrbit(pixelDelta.magnitude)) SaveTips();
        }

        void NotifyTipGesture(BoardGesture gesture)
        {
            if (tips != null && tips.OnGesture(gesture)) SaveTips();
        }

        /// <summary>Réglages : rejoue les bulles depuis la première.</summary>
        void RestartTips()
        {
            tips.Restart();
            SaveTips();
        }

        void UpdateTips()
        {
            bool show = tips.Active && match.Game != null && !match.Game.IsOver && !reviewing && !IsBlockingOverlayOpen;
            if (tipsBubble.activeSelf != show) tipsBubble.SetActive(show);
            if (!show) return;
            tipsText.text = tips.Text;
            tipsCounter.text = tips.Counter;
            // Respiration lente du liseré pour attirer l'œil sans gêner.
            var outline = tipsBubble.transform.Find("Outline");
            if (outline != null)
                outline.GetComponent<Image>().color = UIKit.Cyan * new Color(1, 1, 1, 0.45f + 0.3f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2f)));
        }
    }
}
