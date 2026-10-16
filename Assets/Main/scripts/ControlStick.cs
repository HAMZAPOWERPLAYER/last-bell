using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using System;

namespace NeKoRoSYS.InputHandling.Mobile
{
    public enum StickMode { Fixed, Free, Floating }
    public enum StickAxis { X, Y, Both }
    public class ControlStick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Visuals")]
        [SerializeField] private RectTransform inputArea = null;
        [SerializeField] public RectTransform stickBounds = null;
        [SerializeField] public RectTransform stickHandle = null;
        [SerializeField] private float fullInputThreshold = 0.75f;
        [SerializeField] private float followThreshold = 1;
        [SerializeField] private float stickRange = 1f;
        [SerializeField] private float moveThreshold = 0.17f;
        [Space]
        [Header("Inputs")]
        [InputControl(layout = "Vector2")]
        [SerializeField] private string m_ControlPath;
        protected override string controlPathInternal { get => m_ControlPath; set => m_ControlPath = value; }
        [SerializeField] private ControlExtension controlExtension;
        [SerializeField] public StickMode stickMode;
        public StickMode StickMode
        {
            get => stickMode;
            set
            {
                stickMode = value;

                if (stickMode == StickMode.Fixed)
                {
                    stickBounds.anchoredPosition = fixedPosition;
                    stickHandle.anchoredPosition = Vector2.zero;
                }
                stickBounds.gameObject.SetActive(stickMode == StickMode.Fixed);
                inputArea.GetComponent<Image>().raycastTarget = stickMode != StickMode.Fixed;
            }
        }
        [SerializeField] private StickAxis stickAxis = StickAxis.Both;
        public StickAxis StickAxis { get => stickAxis; set => stickAxis = value; }
        [SerializeField] private bool fullInput;
        public bool FullInput
        {
            get => fullInput;
            set
            {
                fullInput = value;
                OnStickFullInput?.Invoke(fullInput);
                controlExtension?.ProcessButton(fullInput == true ? 1f : 0f);
            }
        }
        private Vector2 rawInput = Vector2.zero;
        public Vector2 RawInput
        {
            get => rawInput;
            set
            {
                rawInput = value;
                input = value;
                input = StickAxis == StickAxis.X ? new(input.x, 0f) : StickAxis == StickAxis.Y ? new(0f, input.y) : input;
            }
        }
        public Vector2 input = Vector2.zero;
        public bool touched = false;
        public int touchId;

        [Header("Events")]
        public Action<Vector2> OnStickDrag;
        public Action<bool> OnStickFullInput;

        private Vector2 center = new(0.5f, 0.5f);
        private Vector2 fixedPosition;
        private Canvas canvas;
        private Camera cam;
        private int enhancedTouchId = -1;

        private void Awake() => EnsureEnhancedTouch();
        protected override void OnEnable()
        {
            base.OnEnable();
            EnsureEnhancedTouch();
        }

        private static void EnsureEnhancedTouch()
        {
            if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();
        }

        private void Update()
        {
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused)
            {
                if (touched || input != Vector2.zero || fullInput) ForceStopTouch();
                return;
            }
            if (!EnhancedTouchSupport.enabled || inputArea == null) return;

            var activeTouches = Touch.activeTouches;
            if (enhancedTouchId < 0)
            {
                for (int i = 0; i < activeTouches.Count; i++)
                {
                    var touch = activeTouches[i];
                    if (touch.phase != TouchPhase.Began ||
                        !RectTransformUtility.RectangleContainsScreenPoint(inputArea, touch.screenPosition, GetCanvasCamera()))
                        continue;

                    enhancedTouchId = touch.touchId;
                    touched = true;
                    touchId = touch.touchId;
                    PositionFloatingStick(touch.screenPosition);
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
                    ForceStopTouch();
                    return;
                }

                ApplyTouchPosition(touch.screenPosition);
                return;
            }

            enhancedTouchId = -1;
            ForceStopTouch();
        }

        private Camera GetCanvasCamera()
        {
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
        }

        private void PositionFloatingStick(Vector2 screenPosition)
        {
            if (stickMode == StickMode.Fixed || stickBounds == null || canvas == null) return;
            bool inside = RectTransformUtility.ScreenPointToLocalPointInRectangle(inputArea, screenPosition, GetCanvasCamera(), out Vector2 localPoint);
            stickBounds.anchoredPosition = inside ? localPoint - (stickBounds.anchorMax * inputArea.sizeDelta) + inputArea.pivot * inputArea.sizeDelta : Vector2.zero;
            stickBounds.gameObject.SetActive(true);
        }

        private void ApplyTouchPosition(Vector2 screenPosition)
        {
            if (canvas == null || stickBounds == null) return;
            cam = GetCanvasCamera();
            Vector2 radius = stickBounds.sizeDelta / 2f;
            RawInput = (screenPosition - RectTransformUtility.WorldToScreenPoint(cam, stickBounds.position)) / (radius * canvas.scaleFactor);
            ProcessInput(input.magnitude, input.normalized, radius);
        }

        private void Start() => FormatControlStick();

        protected override void OnDisable()
        {
            base.OnDisable();
            ForceStopTouch();
        }
        
        private void FormatControlStick()
        {
            canvas = GetComponentInParent<Canvas>();
            stickBounds.pivot = center;
            fixedPosition = stickBounds.anchoredPosition;
            stickHandle.anchorMin = center;
            stickHandle.anchorMax = center;
            stickHandle.pivot = center;
            stickHandle.anchoredPosition = Vector2.zero;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (touched) return;
            touched = true;
            touchId = eventData.pointerId;
            if (eventData.pointerId != touchId) return;
            if (StickMode != StickMode.Fixed)
            {
                bool inRect = RectTransformUtility.ScreenPointToLocalPointInRectangle(inputArea, eventData.position, cam, out Vector2 localPoint);
                stickBounds.anchoredPosition = inRect ? localPoint - (stickBounds.anchorMax * inputArea.sizeDelta) + inputArea.pivot * inputArea.sizeDelta : Vector2.zero;
                stickBounds.gameObject.SetActive(true);
            }
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!touched || eventData.pointerId != touchId) return;
            cam = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
            Vector2 radius = stickBounds.sizeDelta / 2;
            RawInput = (eventData.position - RectTransformUtility.WorldToScreenPoint(cam, stickBounds.position)) / (radius * canvas.scaleFactor);
            ProcessInput(input.magnitude, input.normalized, radius);
        }

        private void ProcessInput(float magnitude, Vector2 normalized, Vector2 radius)
        {
            if (StickMode == StickMode.Floating && magnitude > followThreshold)
            {
                Vector2 difference = normalized * (magnitude - followThreshold) * radius;
                stickBounds.anchoredPosition += difference;
            }
            FullInput = magnitude > fullInputThreshold;
            // MODIFIED (project-local): upstream quantised the output to fixed 0.45 / 0.90 steps,
            // which removed the analog speed ramp. Emit the true clamped deflection instead so
            // Controller receives a smooth 0..1 magnitude.
            input = magnitude > moveThreshold ? Vector2.ClampMagnitude(normalized * magnitude, 1f) : Vector2.zero;
            stickHandle.anchoredPosition = input * stickRange * radius;
            OnStickDrag?.Invoke(input);
            SendValueToControl(input);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != touchId) return;
            touched = false;
            if (eventData.pointerId == enhancedTouchId) enhancedTouchId = -1;
            if (StickMode != StickMode.Fixed) stickBounds.gameObject.SetActive(false);
            FullInput = false;
            input = Vector2.zero;
            stickHandle.anchoredPosition = Vector2.zero;
            OnStickDrag?.Invoke(Vector2.zero);
            SendValueToControl(Vector2.zero);
        }

        public void SetJoystick(int stickIndex) => StickMode = (StickMode)stickIndex;

        public void ForceStopTouch()
        {
            enhancedTouchId = -1;
            touched = false;
            touchId = 0;
            FullInput = false;
            rawInput = Vector2.zero;
            input = Vector2.zero;
            if (stickHandle != null) stickHandle.anchoredPosition = Vector2.zero;
            if (stickBounds != null) stickBounds.gameObject.SetActive(stickMode == StickMode.Fixed);
            OnStickDrag?.Invoke(Vector2.zero);
            SendValueToControl(Vector2.zero);
        }
    }
}