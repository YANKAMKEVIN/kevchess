using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KevChess.Game.UI
{
    /// <summary>Toucher court ou appui long sur un élément d'interface (le graphique doit capter les rayons).</summary>
    public sealed class LongPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        public const float HoldSeconds = 0.5f;

        public Action Tapped;
        public Action Held;

        float downAt = -1f;
        bool fired;

        public void OnPointerDown(PointerEventData eventData)
        {
            downAt = Time.unscaledTime;
            fired = false;
        }

        public void OnPointerUp(PointerEventData eventData) => downAt = -1f;

        public void OnPointerExit(PointerEventData eventData) => downAt = -1f;

        public void OnPointerClick(PointerEventData eventData)
        {
            // Après un appui long, le relâcher ne compte pas comme un toucher.
            if (fired) { fired = false; return; }
            Tapped?.Invoke();
        }

        void Update()
        {
            if (downAt < 0f || fired || Time.unscaledTime - downAt < HoldSeconds) return;
            fired = true;
            downAt = -1f;
            Held?.Invoke();
        }

        void OnDisable()
        {
            downAt = -1f;
            fired = false;
        }
    }
}
