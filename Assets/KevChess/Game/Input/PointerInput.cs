using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace KevChess.Game.Input
{
    /// <summary>
    /// Unifie souris et tactile en gestes simples : tap, double-tap, glisser (orbite), pincer/molette (zoom).
    /// Un geste qui commence sur l'interface est ignoré.
    /// </summary>
    public sealed class PointerInput : MonoBehaviour
    {
        const float DoubleTapSeconds = 0.32f;
        const float MaxTapSeconds = 0.6f;

        public event Action<Vector2> Tapped;
        public event Action<Vector2> DoubleTapped;
        public event Action<Vector2> Dragged;   // delta en pixels
        public event Action<float> Zoomed;      // facteur > 1 = rapprocher

        /// <summary>
        /// Appelé au début d'un appui : vrai si l'appui commence sur une pièce déplaçable.
        /// Le glisser déplace alors la pièce (PieceDragged / PieceDropped) au lieu de tourner la caméra.
        /// </summary>
        public Func<Vector2, bool> PieceGestureFilter;
        public event Action<Vector2> PieceDragged;
        public event Action<Vector2> PieceDropped;
        public event Action PieceDragCancelled;

        float dragThreshold;
        Vector2 pressStart, lastPos;
        float pressTime;
        bool pressing, dragging, gestureBlocked, multiTouchSeen, pieceGesture;
        float lastPinchDistance;
        float lastTapTime = -10f;
        Vector2 lastTapPos;

        readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        PointerEventData uiPointer;

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
            dragThreshold = Mathf.Max(10f, (Screen.dpi > 0 ? Screen.dpi : 160f) * 0.07f);
        }

        void OnDisable() => EnhancedTouchSupport.Disable();

        void Update()
        {
            if (Touch.activeTouches.Count > 0) HandleTouches();
            else if (Mouse.current != null) HandleMouse(Mouse.current);
        }

        void HandleTouches()
        {
            var touches = Touch.activeTouches;
            if (touches.Count >= 2)
            {
                if (pieceGesture && dragging) PieceDragCancelled?.Invoke();
                pieceGesture = false;
                multiTouchSeen = true;
                dragging = true;
                if (gestureBlocked) return;
                float dist = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
                if (lastPinchDistance > 0f && dist > 0f) Zoomed?.Invoke(dist / lastPinchDistance);
                lastPinchDistance = dist;
                return;
            }
            lastPinchDistance = 0f;

            var t = touches[0];
            switch (t.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    Begin(t.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Moved:
                case UnityEngine.InputSystem.TouchPhase.Stationary:
                    if (!pressing) Begin(t.screenPosition);
                    Move(t.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Ended:
                    if (pressing) End(t.screenPosition);
                    break;
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    if (pieceGesture && dragging) PieceDragCancelled?.Invoke();
                    pressing = pieceGesture = false;
                    break;
            }
        }

        void HandleMouse(Mouse mouse)
        {
            Vector2 pos = mouse.position.ReadValue();

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f && !IsOverUI(pos))
            {
                // Molette : crans de ±120 (ou ±1 selon la plateforme) ; trackpad : petits deltas continus.
                float notches = Mathf.Abs(scroll) >= 20f ? scroll / 120f : scroll * 0.25f;
                Zoomed?.Invoke(Mathf.Pow(1.12f, Mathf.Clamp(notches, -3f, 3f)));
            }

            // Clic droit : orbite directe.
            if (mouse.rightButton.isPressed && !mouse.leftButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                if (d.sqrMagnitude > 0f) Dragged?.Invoke(d);
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame) Begin(pos);
            else if (mouse.leftButton.isPressed && pressing) Move(pos);
            else if (mouse.leftButton.wasReleasedThisFrame && pressing) End(pos);
        }

        void Begin(Vector2 pos)
        {
            pressing = true;
            dragging = false;
            multiTouchSeen = false;
            pressStart = lastPos = pos;
            pressTime = Time.unscaledTime;
            gestureBlocked = IsOverUI(pos);
            pieceGesture = !gestureBlocked && PieceGestureFilter != null && PieceGestureFilter(pos);
        }

        void Move(Vector2 pos)
        {
            if (gestureBlocked) return;
            if (!dragging && (pos - pressStart).magnitude > dragThreshold)
            {
                dragging = true;
                lastPos = pos;
            }
            if (dragging && pieceGesture)
            {
                PieceDragged?.Invoke(pos);
            }
            else if (dragging && !multiTouchSeen)
            {
                Vector2 delta = pos - lastPos;
                if (delta.sqrMagnitude > 0f) Dragged?.Invoke(delta);
            }
            lastPos = pos;
        }

        void End(Vector2 pos)
        {
            pressing = false;
            if (pieceGesture && dragging)
            {
                pieceGesture = false;
                PieceDropped?.Invoke(pos);
                return;
            }
            pieceGesture = false; // appui bref sur une pièce : c'est un toucher normal
            if (gestureBlocked || dragging || multiTouchSeen) return;
            if (Time.unscaledTime - pressTime > MaxTapSeconds) return;

            // Chaque toucher est transmis ; le double-tap s'ajoute (deux touchers presque au même endroit).
            bool isDouble = Time.unscaledTime - lastTapTime < DoubleTapSeconds
                            && (pos - lastTapPos).magnitude < dragThreshold;
            Tapped?.Invoke(pos);
            if (isDouble)
            {
                lastTapTime = -10f;
                DoubleTapped?.Invoke(pos);
            }
            else
            {
                lastTapTime = Time.unscaledTime;
                lastTapPos = pos;
            }
        }

        bool IsOverUI(Vector2 screenPos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (uiPointer == null || uiPointer.currentInputModule != es.currentInputModule) uiPointer = new PointerEventData(es);
            uiPointer.position = screenPos;
            uiHits.Clear();
            es.RaycastAll(uiPointer, uiHits);
            return uiHits.Count > 0;
        }
    }
}
