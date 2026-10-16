using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using System.Collections.Generic;
using System.Collections;
using System;

namespace NeKoRoSYS.InputHandling.Mobile
{
    public class ControlButton : OnScreenControl, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Visuals")]
        [SerializeField] private Sprite pressedIcon;
        [SerializeField] private Color releasedColor = Color.white, pressedColor = Color.grey;
        [SerializeField] private bool animateSprite = true, animateColor = true;
        [SerializeField] private Image icon;
        private Sprite releasedIcon;
        [Space]
        [Header("Inputs")]
        [InputControl(layout = "Button")]
        [SerializeField] private string m_ControlPath;
        protected override string controlPathInternal { get => m_ControlPath; set => m_ControlPath = value; }
        [SerializeField] private ControlExtension controlExtension;
        private Vector2 startPos;
        public bool touched = false;
        public int touchId, touchAmount;
        private const float DoubleTapResetDelaySeconds = 0.25f;
        [Space]
        [Header("Events")]
        public Action<bool> OnButtonAction;
        public Action OnDoubleTap;
        private GraphicRaycaster raycaster;
        private Coroutine colorRoutine;
        private bool visualsCaptured;

        // Direct EnhancedTouch handling - bypasses EventSystem entirely
        private int enhancedTouchId = -1;
        private RectTransform selfRect;

        internal void ResetTapAmount()
        {
            touchAmount = 0;
            controlExtension?.ProcessButton(0f);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ForceStopTouch();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
            Diag.Log("ControlButton.OnPointerDown on " + gameObject.name);
            if (touched) return;
            touched = true;
            touchId = eventData.pointerId;
            if (eventData.pointerId != touchId) return;
            startPos = eventData.position;
            CheckOverlap(eventData, ExecuteEvents.pointerDownHandler);
            ProcessInput(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != touchId) return;
            touched = false;
            CheckOverlap(eventData, ExecuteEvents.pointerUpHandler);
            ProcessInput(false);
        }

        private void Start()
        {
            raycaster = FindObjectOfType<GraphicRaycaster>();
            if (icon != null) releasedIcon = icon.sprite;
            visualsCaptured = true;
        }

        private void Awake()
        {
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
            selfRect = GetComponent<RectTransform>();
        }

        // BUGFIX: this was `private void OnEnable()`, which HID OnScreenControl.OnEnable (CS0114)
        // instead of overriding it. OnDisable below does call base.OnDisable(), so the pairing was
        // broken in a way that mattered: base.OnEnable is what runs SetupInputControl() - creating
        // the synthetic control that SendValueToControl feeds - and increments s_nbActiveInstances,
        // which base.OnDisable then decremented. The control path was therefore dead and the
        // OnScreenControl.HasAnyActive count drifted negative. ControlStick already overrides
        // correctly; this now matches it.
        protected override void OnEnable()
        {
            base.OnEnable();
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        }

        private void Update()
        {
            if (!EnhancedTouchSupport.enabled || selfRect == null) return;

            // While paused, the direct EnhancedTouch path bypasses the EventSystem (and the pause
            // panel's raycast block), so without this guard the flashlight could be toggled
            // through the pause overlay. Mirrors ControlStick's pause handling.
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            {
                if (touched || enhancedTouchId >= 0)
                {
                    ForceStopTouch();
                    ProcessInput(false);
                }
                return;
            }

            var activeTouches = Touch.activeTouches;
            if (activeTouches.Count == 0)
            {
                if (enhancedTouchId >= 0)
                {
                    enhancedTouchId = -1;
                    touched = false;
                    ProcessInput(false);
                }
                return;
            }

            if (enhancedTouchId < 0)
            {
                // Dedupe: the EventSystem pointer path (OnPointerDown) may already own this press.
                // Both paths used to fire ProcessInput(true) for one tap, double-invoking
                // OnButtonAction and the double-tap counter. First path to claim it wins.
                if (touched) return;

                for (int i = 0; i < activeTouches.Count; i++)
                {
                    var touch = activeTouches[i];
                    if (touch.phase != TouchPhase.Began) continue;
                    if (!RectTransformUtility.RectangleContainsScreenPoint(selfRect, touch.screenPosition)) continue;
                    enhancedTouchId = touch.touchId;
                    touched = true;
                    touchId = touch.touchId;
                    Diag.Log("ControlButton EnhancedTouch DOWN on " + gameObject.name);
                    ProcessInput(true);
                    break;
                }
            }

            if (enhancedTouchId < 0) return;

            for (int i = 0; i < activeTouches.Count; i++)
            {
                var touch = activeTouches[i];
                if (touch.touchId != enhancedTouchId) continue;
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.None)
                {
                    enhancedTouchId = -1;
                    touched = false;
                    Diag.Log("ControlButton EnhancedTouch UP on " + gameObject.name);
                    ProcessInput(false);
                }
                return;
            }

            enhancedTouchId = -1;
            touched = false;
            ProcessInput(false);
        }

        public void ProcessInput(bool pressed)
        {
            if (pressed)
            {
                CheckDoubleTap();
                CancelInvoke(nameof(ResetTapAmount));
                Invoke(nameof(ResetTapAmount), DoubleTapResetDelaySeconds);
            }
            OnButtonAction?.Invoke(pressed);
            SendValueToControl(pressed ? 1.0f : 0.0f);
            if (gameObject.activeInHierarchy) PlayVisuals(pressed ? pressedIcon : releasedIcon, pressed ? pressedColor : releasedColor);
        }

        private void CheckDoubleTap()
        {
            touchAmount++;
            if (touchAmount >= 2)
            {
                ResetTapAmount();
                OnDoubleTap?.Invoke();
                controlExtension?.ProcessButton(1f);
            }
        }

        private void PlayVisuals(Sprite targetIcon, Color targetColor)
        {
            if (icon == null) return;
            if (targetIcon != null && animateSprite) icon.sprite = targetIcon;
            if (!animateColor)
            {
                icon.color = targetColor;
                return;
            }
            if (colorRoutine != null) StopCoroutine(colorRoutine);
            colorRoutine = StartCoroutine(FadeIconColor(targetColor, 0.15f));
        }

        private IEnumerator FadeIconColor(Color targetColor, float duration)
        {
            Color startColor = icon.color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                icon.color = Color.Lerp(startColor, targetColor, elapsed / duration);
                yield return null;
            }
            icon.color = targetColor;
            colorRoutine = null;
        }

        public void CheckOverlap<T>(PointerEventData eventData, ExecuteEvents.EventFunction<T> eventFunction) where T : IEventSystemHandler
        {
            if (raycaster == null) return;
            List<RaycastResult> results = new();
            eventData.position = startPos;
            raycaster.Raycast(eventData, results);
            foreach (RaycastResult result in results)
            {
                if (result.gameObject == gameObject) continue;
                if (!result.gameObject.TryGetComponent(out ControlButton button)) return;
                if (Equals(eventFunction, ExecuteEvents.pointerDownHandler) && button.touched) break;
                else if (Equals(eventFunction, ExecuteEvents.pointerUpHandler) && !button.touched) break;
                ExecuteEvents.Execute(result.gameObject, eventData, eventFunction);
            }
        }

        public void ForceStopTouch()
        {
            touched = false;
            touchId = 0;
            enhancedTouchId = -1;
            if (colorRoutine != null)
            {
                StopCoroutine(colorRoutine);
                colorRoutine = null;
            }
            if (visualsCaptured && icon != null)
            {
                icon.sprite = releasedIcon;
                icon.color = releasedColor;
            }
        }
    }
}
