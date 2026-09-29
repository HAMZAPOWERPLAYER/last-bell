using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;
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
        private readonly float maxTapInterval = 15f;
        [Space]
        [Header("Events")]
        public Action<bool> OnButtonAction;
        public Action OnDoubleTap;
        private GraphicRaycaster raycaster;
        private Coroutine colorRoutine;
        private bool visualsCaptured;

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

        public void ProcessInput(bool pressed)
        {
            if (pressed)
            {
                CheckDoubleTap();
                CancelInvoke(nameof(ResetTapAmount));
                Invoke(nameof(ResetTapAmount), maxTapInterval * Time.deltaTime);
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
            // Replaces the upstream DOTween tween so the project needs no DOTween dependency.
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
            if (colorRoutine != null)
            {
                StopCoroutine(colorRoutine);
                colorRoutine = null;
            }
            // OnDisable runs before Start() on objects that load inactive, so only restore
            // the captured visuals once they have actually been captured.
            if (visualsCaptured && icon != null)
            {
                icon.sprite = releasedIcon;
                icon.color = releasedColor;
            }
        }
    }
}