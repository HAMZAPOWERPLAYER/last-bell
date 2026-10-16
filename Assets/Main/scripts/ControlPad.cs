using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using System;
using System.Collections.Generic;

namespace NeKoRoSYS.InputHandling.Mobile
{
    public class ControlPad : MonoBehaviour
    {
        [Header("Inputs")]
        [HideInInspector] public Vector2 delta = Vector2.zero;
        [SerializeField] private int touchLimit = 10;
        public HashSet<int> availableTouchIds = new();
        private Vector2 currentPos, lastPos;
        [Space]
        [Header("Events")]
        public Action<Vector2> OnTouchDrag;

        // Reading Touch.activeTouches throws InvalidOperationException while EnhancedTouch is
        // disabled, which would spam an exception every frame and kill the look pad. Claim the
        // API ourselves rather than relying only on the bootstrap initializer.
        private void Awake() => EnsureEnhancedTouch();
        private void OnEnable() => EnsureEnhancedTouch();
        private static void EnsureEnhancedTouch()
        {
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        }

        private bool IsTouchingPad(Touch touch) => RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, touch.screenPosition);
        private void OnDisable() => ForceStopTouch();
        private void Update()
        {
            delta = Vector2.zero;
            if (!EnhancedTouchSupport.enabled || Touch.activeTouches.Count == 0)
            {
                availableTouchIds.Clear();
                currentPos = Vector2.zero;
                lastPos = Vector2.zero;
                OnTouchDrag?.Invoke(delta);
                return;
            }
            GetTouch();
            foreach (var touch in Touch.activeTouches)
            { if (availableTouchIds.Contains(touch.touchId)) ProcessInput(touch); }
            OnTouchDrag?.Invoke(delta);
        }

        private void GetTouch()
        {
            foreach (var touch in Touch.activeTouches)
            {
                var touching = touch.phase is TouchPhase.Began;
                var notTouching = touch.phase is TouchPhase.Ended or TouchPhase.Canceled or TouchPhase.None;
                if (IsTouchingPad(touch) && touching && availableTouchIds.Count < touchLimit)
                {
                    // A touch that begins on top of interactive UI (HUD buttons, pause button) must
                    // not also drive the camera - the pad used to steal those touches and the view
                    // rotated while the player tapped a button. Ownership is decided at touch-down.
                    if (!IsOverUI(touch.screenPosition)) availableTouchIds.Add(touch.touchId);
                }
                else if (notTouching) availableTouchIds.Remove(touch.touchId);
            }
        }

        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

        /// <summary>True when the screen position lands on interactive UI that is not part of this pad.</summary>
        private bool IsOverUI(Vector2 screenPosition)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            uiRaycastResults.Clear();
            var data = new PointerEventData(es) { position = screenPosition };
            es.RaycastAll(data, uiRaycastResults);
            for (int i = 0; i < uiRaycastResults.Count; i++)
            {
                var hit = uiRaycastResults[i].gameObject;
                if (hit == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                return true;
            }
            return false;
        }

        private void ProcessInput(Touch touch)
        {
            if (touch.phase == TouchPhase.Moved)
            {
                currentPos += touch.delta;
                delta += currentPos - lastPos;
                lastPos = currentPos;
            }
            else if (touch.phase == TouchPhase.Stationary) delta = Vector2.zero;
        }

        public void ForceStopTouch()
        {
            delta = Vector2.zero;
            currentPos = Vector2.zero;
            lastPos = Vector2.zero;
            availableTouchIds.Clear();
        }
    }
}