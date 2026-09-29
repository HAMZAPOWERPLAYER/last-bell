using UnityEngine;

/// <summary>
/// Sizes the on-screen thumbstick relative to the canvas instead of using fixed units.
///
/// The Canvas uses CanvasScaler.ScaleWithScreenSize with matchWidthOrHeight = 0 (match WIDTH),
/// so the canvas is always 800 units wide but its HEIGHT varies with the device aspect ratio
/// (about 360 units on a 20:9 phone, 450 on 16:9, much more in portrait). A stick with a fixed
/// unit size therefore looks reasonable on one device and enormous on another, which is exactly
/// what happened with a 280-unit stick on a 360-unit-tall canvas.
///
/// This keeps the stick at a constant fraction of the SHORTER usable dimension, so it reads as
/// the same physical thumb target on every phone.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MobileControlsLayout : MonoBehaviour
{
    [Tooltip("Thumbstick base diameter as a fraction of the canvas height.")]
    public float heightFraction = 0.34f;

    [Tooltip("Hard cap on the diameter as a fraction of the canvas width (stops it ballooning on tall/narrow canvases).")]
    public float maxWidthFraction = 0.18f;

    [Tooltip("Smallest diameter we will ever draw, in canvas units.")]
    public float minDiameter = 70f;

    [Tooltip("Distance of the stick centre from the bottom-left corner, as a multiple of its diameter.")]
    public float cornerOffset = 0.50f;

    [Tooltip("The ring the player grabs and the handle travels inside (diameter = this x diameter).")]
    public float boundsScale = 1.0f;

    [Tooltip("The moving handle (diameter = this x diameter).")]
    public float handleScale = 0.40f;

    public RectTransform stickBounds;
    public RectTransform stickHandle;

    RectTransform area;
    Vector2 lastCanvasSize = Vector2.zero;

    void Awake()
    {
        area = (RectTransform)transform;
    }

    void OnEnable()
    {
        Apply();
    }

    void Update()
    {
        // Cheap guard so we only recompute when the canvas actually changes size
        // (rotation, editor game-view resize, etc).
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        RectTransform canvasRT = (RectTransform)canvas.transform;
        if (canvasRT.rect.size != lastCanvasSize) Apply();
    }

    void Apply()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || area == null) return;
        RectTransform canvasRT = (RectTransform)canvas.transform;
        Vector2 canvasSize = canvasRT.rect.size;
        if (canvasSize.x <= 0f || canvasSize.y <= 0f) return;
        lastCanvasSize = canvasSize;

        float diameter = Mathf.Max(minDiameter, Mathf.Min(canvasSize.y * heightFraction, canvasSize.x * maxWidthFraction));

        area.sizeDelta = new Vector2(diameter, diameter);
        if (stickBounds != null)
            stickBounds.sizeDelta = new Vector2(diameter * boundsScale, diameter * boundsScale);
        if (stickHandle != null)
            stickHandle.sizeDelta = new Vector2(diameter * handleScale, diameter * handleScale);

        float inset = diameter * cornerOffset + diameter * 0.5f;
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.zero;
        area.pivot = new Vector2(0.5f, 0.5f);
        area.anchoredPosition = new Vector2(inset, inset);
    }
}
