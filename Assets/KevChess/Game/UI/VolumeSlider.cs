using System;
using KevChess.Core.Ux;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KevChess.Game.UI
{
    /// <summary>Curseur de volume construit par code : piste, remplissage néon et bouton que l'on fait glisser.</summary>
    public sealed class VolumeSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        RectTransform track, fill, knob;
        float value;

        /// <summary>Nouvelle valeur 0–1 choisie au doigt (arrondie à 5 %).</summary>
        public Action<float> Changed;

        public static VolumeSlider Create(string name, Transform parent, Color accent)
        {
            var root = UIKit.Rect(name, parent);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0); // zone de toucher plus haute que la piste
            var s = root.gameObject.AddComponent<VolumeSlider>();

            var back = UIKit.Panel("Track", root, new Color(0.08f, 0.10f, 0.20f, 0.85f), true, accent);
            back.raycastTarget = false;
            s.track = back.rectTransform;
            s.track.anchorMin = new Vector2(0f, 0.5f);
            s.track.anchorMax = new Vector2(1f, 0.5f);
            s.track.offsetMin = new Vector2(30, -14);
            s.track.offsetMax = new Vector2(-30, 14);

            var f = UIKit.Panel("Fill", s.track, accent * new Color(1f, 1f, 1f, 0.75f), false);
            f.raycastTarget = false;
            s.fill = f.rectTransform;
            s.fill.anchorMin = Vector2.zero;
            s.fill.anchorMax = new Vector2(0f, 1f);
            s.fill.offsetMin = new Vector2(3, 3);
            s.fill.offsetMax = new Vector2(-3, -3);

            var k = UIKit.Panel("Knob", s.track, UIKit.Text, true, accent);
            k.raycastTarget = false;
            s.knob = k.rectTransform;
            s.knob.anchorMin = s.knob.anchorMax = new Vector2(0f, 0.5f);
            s.knob.pivot = new Vector2(0.5f, 0.5f);
            s.knob.sizeDelta = new Vector2(52, 52);
            return s;
        }

        public float Value
        {
            get => value;
            set
            {
                this.value = Mathf.Clamp01(value);
                fill.anchorMax = new Vector2(this.value, 1f);
                knob.anchorMin = knob.anchorMax = new Vector2(this.value, 0.5f);
                knob.anchoredPosition = Vector2.zero;
            }
        }

        public void OnPointerDown(PointerEventData eventData) => Pick(eventData);

        public void OnDrag(PointerEventData eventData) => Pick(eventData);

        void Pick(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var local)) return;
            var r = track.rect;
            float v = SliderMath.ValueAt(local.x, r.xMin, r.xMax);
            if (Mathf.Approximately(v, value)) return;
            Value = v;
            Changed?.Invoke(v);
        }
    }
}
