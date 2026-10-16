using UnityEngine;

/// <summary>
/// Keeps an edge-anchored UI element inside the device's screen safe area (notches, punch holes,
/// rounded corners, the gesture bar). The component only ever pushes the element FURTHER inside -
/// the anchored position the designer authored stays the minimum margin. On desktop / editor /
/// any device without cutouts the safe area equals the full screen and nothing moves.
///
/// Usage: put it on a RectTransform anchored to one or two screen edges (e.g. the HUD pause
/// button, anchored to the top-right corner). Center-anchored elements are left alone.
/// </summary>
[DisallowMultipleComponent]
public class SafeAreaClamp : MonoBehaviour
{
    [Tooltip("Extra margin kept between the element and the safe-area edge, in canvas units.")]
    public float padding = 0f;

    RectTransform _rect;
    Vector2 _authoredPosition;
    int _lastScreenW = -1, _lastScreenH = -1;
    Rect _lastSafeArea;
    float _lastScale = -1f;

    void Awake()
    {
        _rect = transform as RectTransform;
        if (_rect != null) _authoredPosition = _rect.anchoredPosition;   // the designed margin
    }

    void OnEnable() { Apply(); }

    // Screen size, safe area and the canvas scale can all change without a layout pass
    // (rotation, multi-window, scaler init order), so poll cheaply and recompute on change.
    void Update()
    {
        var canvas = GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
        if (Screen.width != _lastScreenW || Screen.height != _lastScreenH ||
            Screen.safeArea != _lastSafeArea || !Mathf.Approximately(scale, _lastScale))
            Apply();
    }

    void Apply()
    {
        if (_rect == null) _rect = transform as RectTransform;
        if (_rect == null) return;
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        // scaleFactor is pixels per canvas unit - exactly the conversion the safe-area insets
        // need to reach the menu canvases' 800-wide reference layout.
        float scale = canvas.rootCanvas.scaleFactor;
        if (scale <= 0f) scale = 1f;

        _lastScreenW = Screen.width;
        _lastScreenH = Screen.height;
        _lastSafeArea = Screen.safeArea;
        _lastScale = scale;

        Rect safe = Screen.safeArea;
        float leftInset = safe.xMin / scale;
        float rightInset = (Screen.width - safe.xMax) / scale;
        float topInset = (Screen.height - safe.yMax) / scale;
        float bottomInset = safe.yMin / scale;

        Vector2 anchor = _rect.anchorMin;
        Vector2 pos = _authoredPosition;

        // Push inward only: safe insets larger than the authored margin expand the margin.
        if (anchor.x > 0.5f) pos.x = Mathf.Min(pos.x, -(rightInset + padding));
        else if (anchor.x < 0.5f) pos.x = Mathf.Max(pos.x, leftInset + padding);

        if (anchor.y > 0.5f) pos.y = Mathf.Min(pos.y, -(topInset + padding));
        else if (anchor.y < 0.5f) pos.y = Mathf.Max(pos.y, bottomInset + padding);

        if (pos != _rect.anchoredPosition) _rect.anchoredPosition = pos;
    }
}
