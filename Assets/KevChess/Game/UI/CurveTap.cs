using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KevChess.Game.UI
{
    /// <summary>Toucher sur un graphique : renvoie la position touchée en coordonnées 0–1 du rectangle.</summary>
    public sealed class CurveTap : MonoBehaviour, IPointerClickHandler
    {
        public Action<Vector2> Tapped;

        public void OnPointerClick(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local)) return;
            var r = rt.rect;
            if (r.width <= 0f || r.height <= 0f) return;
            Tapped?.Invoke(new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height));
        }
    }
}
