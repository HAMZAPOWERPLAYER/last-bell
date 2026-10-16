using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// LAST BELL menu presentation layer: one small reusable script that turns a plain panel into the
/// text-list menu style (wide spaced title, left-aligned uppercase items with no boxes, a slanted
/// translucent highlight bar, tiled grain, a pulsing vignette and a right-hand art image that
/// fades into darkness toward the left).
///
/// Rules this file follows on purpose:
///  - EVERYTHING animates on UNSCALED time, because the pause menu runs at Time.timeScale = 0.
///  - Only RectTransform, CanvasGroup.alpha and Graphic colour are touched. No particles, no
///    shaders, no DOTween, no new packages.
///  - No per-frame allocations: no strings, no lambdas, no boxing and no new WaitForSeconds in
///    any loop. AnimationCurve + Mathf only, over cached arrays.
///  - All sprites (slanted bar, grain, vignette, left-edge fade) are generated procedurally at
///    runtime, so the shipped asset size for this feature is 0 bytes.
///  - System.Random (never UnityEngine.Random) drives the title flicker, so the menus can never
///    perturb the gameplay RNG that randomises key/page placement.
///
/// The menu scripts (MainMenuUI / PauseMenuUI) build their widget tree through this component.
/// </summary>
public class LastBellMenuStyle : MonoBehaviour
{
    // ------------------------------------------------------------------ layout (800x600 canvas, match WIDTH)
    // The canvas matches WIDTH against a 800x600 reference, so on a landscape phone the usable
    // band is about +/-180 units tall and 800 units wide. ItemHeight == ItemPitch keeps the
    // tappable rect as tall as the spacing allows: 60 units is >= 48dp on a 640dp-wide device
    // (the narrowest landscape Android class in common use) and comfortably more on wider ones.
    public const float LeftMargin = -352f;
    public const float ItemWidth = 430f;
    public const float ItemHeight = 60f;
    public const float ItemPitch = 60f;
    public const float FirstItemY = 94f;
    public const float LabelIndent = 20f;
    public const float HighlightWidth = 250f;
    public const float HighlightHeight = 34f;
    public const float HighlightSlideIn = -26f;
    public const float TitleY = 160f;
    public const float SubtitleY = 133f;
    public const int TitleFontSize = 30;
    public const int SubtitleFontSize = 13;
    public const int ItemFontSize = 20;
    public const float TitleExtraSpacing = 11f;

    // Row layout. The constants above are the Last Bell defaults; these instance copies let a
    // menu with more rows (main menu + CONTINUE = 6) tighten the pitch so every row - including
    // the highlight bar - stays inside the +/-180 unit safe band. Other menus keep the defaults.
    public float rowFirstY = FirstItemY;
    public float rowPitch = ItemPitch;

    // ------------------------------------------------------------------ tunables
    [Header("Font (leave empty to use the project's built-in font)")]
    public Font displayFont;

    [Header("Colours")]
    public Color titleColour = new Color(0.88f, 0.92f, 0.88f, 1f);
    public Color subtitleColour = new Color(0.50f, 0.56f, 0.51f, 1f);
    public Color itemColour = new Color(0.60f, 0.65f, 0.61f, 1f);
    public Color itemActiveColour = new Color(0.95f, 1f, 0.95f, 1f);
    public Color highlightColour = new Color(0.70f, 0.86f, 0.60f, 0.20f);   // desaturated sickly green
    public Color backdropColour = new Color(0.016f, 0.021f, 0.020f, 1f);
    public Color grainColour = new Color(1f, 1f, 1f, 0.045f);
    public Color vignetteColour = new Color(0f, 0f, 0f, 1f);

    [Header("Timing (unscaled seconds)")]
    public float titleFadeDuration = 0.8f;
    public float titleSpacingDrift = 0.6f;      // extra spacing at t=0, eased to 1.0
    public float subtitleDelay = 0.12f;
    public float subtitleFadeDuration = 0.5f;
    public float artFadeDuration = 0.9f;
    public float itemStagger = 0.06f;
    public float itemDelay = 0.10f;
    public float itemFadeDuration = 0.34f;
    public float itemSlideOffset = -34f;
    public float closeDuration = 0.15f;
    public float vignettePeriod = 7.5f;
    public float vignetteAlphaMin = 0.30f;
    public float vignetteAlphaMax = 0.55f;
    public float flickerMinInterval = 4f;
    public float flickerMaxInterval = 11f;

    [Header("Curves")]
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve slideCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve highlightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Highlight bar")]
    public float highlightInDuration = 0.12f;
    public float highlightOutDuration = 0.12f;
    public float highlightHoverScale = 0.35f;

    [Tooltip("Overall alpha of the right-hand art when the menu is open.")]
    [Range(0f, 1f)] public float artAlpha = 1f;

    // ------------------------------------------------------------------ runtime state
    public class ItemView
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Text label;
        public Image highlight;
        public CanvasGroup highlightGroup;
        public Button button;
        public Vector2 restPos;
        public int index;
        public bool pressed;
        public bool hovered;
        public float highlightT;
        public bool bright;
    }

    class AuxFade
    {
        public CanvasGroup group;
        public float closedAlpha;
        public float openAlpha;
    }

    readonly List<ItemView> _items = new List<ItemView>();
    readonly List<AuxFade> _aux = new List<AuxFade>();

    RectTransform _content;
    RectTransform _titleRoot;
    CanvasGroup _titleGroup;
    CanvasGroup _subtitleGroup;
    CanvasGroup _artGroup;
    CanvasGroup _vignetteGroup;
    readonly List<RectTransform> _titleGlyphs = new List<RectTransform>();
    readonly List<float> _titleGlyphX = new List<float>();
    bool _spacingApplied;
    bool _built;
    bool _open;
    Coroutine _pulse;
    Coroutine _flicker;
    RectTransform _selfRect;

    // scratch buffers, allocated once so no animation frame allocates
    float[] _scratchAlpha;
    Vector2[] _scratchPos;
    float[] _scratchAux;

    System.Random _rng;

    // ================================================================== procedural sprites
    static Sprite _slantSprite;
    static Sprite _grainSprite;
    static Sprite _vignetteSprite;
    static Sprite _leftFadeSprite;

    static Texture2D NewTexture(int w, int h, string name, TextureWrapMode wrap, FilterMode filter)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.wrapMode = wrap;
        tex.filterMode = filter;
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    static Sprite MakeSprite(Texture2D tex, Vector2 pivot)
    {
        var sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), pivot, 100f, 0,
            SpriteMeshType.FullRect);
        sp.name = tex.name;
        sp.hideFlags = HideFlags.HideAndDontSave;
        return sp;
    }

    /// <summary>Translucent parallelogram (skewed rectangle) used for the highlight bar.</summary>
    public static Sprite SlantSprite()
    {
        if (_slantSprite != null) return _slantSprite;
        const int w = 64, h = 32;
        const float skew = 5f;      // texture px; stretches to ~19 units over a 34 unit tall bar
        var tex = NewTexture(w, h, "LastBell_Slant", TextureWrapMode.Clamp, FilterMode.Bilinear);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);           // 0 = bottom, 1 = top
            float shift = skew * (1f - v);          // top edge sits to the right of the bottom edge
            float vFade = Mathf.Min(1f, Mathf.Min(v, 1f - v) * 12f);
            for (int x = 0; x < w; x++)
            {
                float xl = x - shift;
                float a = (xl >= 0f && xl <= w - 1f) ? 1f : 0f;
                if (a > 0f)
                {
                    float edge = Mathf.Min(xl, (w - 1f) - xl);
                    a *= Mathf.Clamp01(edge / 2.5f);
                    a *= vFade;
                }
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(a * 255f, 0f, 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        _slantSprite = MakeSprite(tex, new Vector2(0f, 0.5f));   // left-centre pivot: it slides in from the left
        return _slantSprite;
    }

    /// <summary>Small tiling noise used for the film grain layer (RawImage uvRect tiles it).</summary>
    public static Texture2D GrainTexture()
    {
        if (_grainSprite != null) return _grainSprite.texture;
        const int n = 32;
        var tex = NewTexture(n, n, "LastBell_Grain", TextureWrapMode.Repeat, FilterMode.Point);
        var px = new Color32[n * n];
        var rnd = new System.Random(20261003);
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)rnd.Next(0, 256);
            px[i] = new Color32(v, v, v, 255);
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        _grainSprite = MakeSprite(tex, new Vector2(0.5f, 0.5f));
        return tex;
    }

    /// <summary>Radial black vignette; stretched to the screen and pulsed at low alpha.</summary>
    public static Sprite VignetteSprite()
    {
        if (_vignetteSprite != null) return _vignetteSprite;
        const int n = 64;
        var tex = NewTexture(n, n, "LastBell_Vignette", TextureWrapMode.Clamp, FilterMode.Bilinear);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            float ny = (y / (float)(n - 1)) * 2f - 1f;
            for (int x = 0; x < n; x++)
            {
                float nx = (x / (float)(n - 1)) * 2f - 1f;
                float r = Mathf.Sqrt(nx * nx + ny * ny * 0.92f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 1.18f, r));
                px[y * n + x] = new Color32(0, 0, 0, (byte)Mathf.Clamp(a * 255f, 0f, 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        _vignetteSprite = MakeSprite(tex, new Vector2(0.5f, 0.5f));
        return _vignetteSprite;
    }

    /// <summary>Horizontal black gradient: opaque at the left edge, transparent at the right.</summary>
    public static Sprite LeftFadeSprite()
    {
        if (_leftFadeSprite != null) return _leftFadeSprite;
        const int w = 64, h = 4;
        var tex = NewTexture(w, h, "LastBell_LeftFade", TextureWrapMode.Clamp, FilterMode.Bilinear);
        var px = new Color32[w * h];
        for (int x = 0; x < w; x++)
        {
            float t = x / (float)(w - 1);
            float a = Mathf.Pow(1f - t, 1.45f);
            byte ab = (byte)Mathf.Clamp(a * 255f, 0f, 255f);
            for (int y = 0; y < h; y++) px[y * w + x] = new Color32(0, 0, 0, ab);
        }
        tex.SetPixels32(px);
        tex.Apply(false, false);
        _leftFadeSprite = MakeSprite(tex, new Vector2(0.5f, 0.5f));
        return _leftFadeSprite;
    }

    public static Font BuiltinFont()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    Font FontOrBuiltin()
    {
        return displayFont != null ? displayFont : BuiltinFont();
    }

    // ================================================================== construction
    void Awake()
    {
        EnsureBuilt();
    }

    /// <summary>Creates the full-screen content root (idempotent).</summary>
    public RectTransform EnsureBuilt()
    {
        if (_built && _content != null) return _content;
        if (_rng == null) _rng = new System.Random(20261003);

        _selfRect = GetComponent<RectTransform>();
        if (_selfRect == null) _selfRect = gameObject.AddComponent<RectTransform>();
        if (_selfRect.parent != null)
        {
            _selfRect.anchorMin = Vector2.zero;
            _selfRect.anchorMax = Vector2.one;
            _selfRect.offsetMin = Vector2.zero;
            _selfRect.offsetMax = Vector2.zero;
        }

        var go = new GameObject("MenuContent", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        _content = go.GetComponent<RectTransform>();
        _content.anchorMin = Vector2.zero;
        _content.anchorMax = Vector2.one;
        _content.offsetMin = Vector2.zero;
        _content.offsetMax = Vector2.zero;

        BuildBackground(_content);
        _built = true;
        return _content;
    }

    public RectTransform Content { get { return _content; } }

    void BuildBackground(RectTransform parent)
    {
        // near black base. It is the one full-screen raycast target, so while the menu is open it
        // swallows taps meant for the HUD underneath (the old pause panel used to do this).
        AddImage(parent, "Backdrop", null, backdropColour, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);

        // tiled grain (cheap: one small texture, exact-repeat uvRect)
        var grainGo = new GameObject("Grain", typeof(RawImage));
        grainGo.transform.SetParent(parent, false);
        var grainRt = grainGo.GetComponent<RectTransform>();
        grainRt.anchorMin = Vector2.zero; grainRt.anchorMax = Vector2.one;
        grainRt.offsetMin = Vector2.zero; grainRt.offsetMax = Vector2.zero;
        var grain = grainGo.GetComponent<RawImage>();
        grain.texture = GrainTexture();
        grain.color = grainColour;
        grain.uvRect = new Rect(0f, 0f, 40f, 22f);
        grain.raycastTarget = false;

        // vignette sits above the art, below the text
        var vig = AddImage(parent, "Vignette", VignetteSprite(), vignetteColour,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        _vignetteGroup = vig.gameObject.AddComponent<CanvasGroup>();
        _vignetteGroup.alpha = vignetteAlphaMin;
        _vignetteGroup.interactable = false;
        _vignetteGroup.blocksRaycasts = false;
        vig.transform.SetAsLastSibling();
    }

    /// <summary>Right-hand art slot. Pass null to keep the slot empty (drop your own image in later).</summary>
    public void BuildArt(Sprite art)
    {
        EnsureBuilt();
        var holder = new GameObject("Art", typeof(RectTransform));
        holder.transform.SetParent(_content, false);
        var holderRt = holder.GetComponent<RectTransform>();
        holderRt.anchorMin = new Vector2(1f, 0.5f);
        holderRt.anchorMax = new Vector2(1f, 0.5f);
        holderRt.pivot = new Vector2(1f, 0.5f);
        holderRt.anchoredPosition = new Vector2(0f, 0f);
        holderRt.sizeDelta = new Vector2(560f, 460f);
        _artGroup = holder.AddComponent<CanvasGroup>();
        _artGroup.alpha = 0f;

        var img = AddImage(holderRt, "ArtImage", art, Color.white,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        img.preserveAspect = true;
        img.enabled = art != null;

        // left-edge fade: the art dissolves into darkness toward the left of the screen
        var fade = new GameObject("ArtLeftFade", typeof(Image));
        fade.transform.SetParent(_content, false);
        var fadeRt = fade.GetComponent<RectTransform>();
        fadeRt.anchorMin = new Vector2(0f, 0f);
        fadeRt.anchorMax = new Vector2(1f, 1f);
        fadeRt.pivot = new Vector2(0f, 0.5f);
        fadeRt.offsetMin = new Vector2(0f, 0f);
        fadeRt.offsetMax = new Vector2(-320f, 0f);       // covers the left 480 units of the screen
        var fadeImg = fade.GetComponent<Image>();
        fadeImg.sprite = LeftFadeSprite();
        fadeImg.color = Color.white;
        fadeImg.raycastTarget = false;
        fade.transform.SetSiblingIndex(holder.transform.GetSiblingIndex() + 1);

        _vignetteGroup.transform.SetAsLastSibling();
    }

    /// <summary>Wide-spaced title, one Text per glyph so letter spacing can drift without TMP.</summary>
    public void BuildTitle(string text)
    {
        EnsureBuilt();
        var root = new GameObject("Title", typeof(RectTransform));
        root.transform.SetParent(_content, false);
        _titleRoot = root.GetComponent<RectTransform>();
        _titleRoot.anchorMin = _titleRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _titleRoot.pivot = new Vector2(0f, 0.5f);
        _titleRoot.anchoredPosition = new Vector2(LeftMargin, TitleY);
        _titleRoot.sizeDelta = new Vector2(ItemWidth, 44f);
        _titleGroup = root.AddComponent<CanvasGroup>();
        _titleGroup.alpha = 0f;

        var font = FontOrBuiltin();
        font.RequestCharactersInTexture(text, TitleFontSize, FontStyle.Normal);

        _titleGlyphs.Clear();
        _titleGlyphX.Clear();
        float x = 0f;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            float adv;
            CharacterInfo ci;
            if (font.GetCharacterInfo(c, out ci, TitleFontSize, FontStyle.Normal)) adv = ci.advance;
            else adv = TitleFontSize * 0.6f;
            if (adv <= 0f) adv = TitleFontSize * 0.6f;

            if (c != ' ')
            {
                var g = new GameObject("G" + i, typeof(Text));
                g.transform.SetParent(_titleRoot, false);
                var grt = g.GetComponent<RectTransform>();
                grt.anchorMin = grt.anchorMax = new Vector2(0f, 0.5f);
                grt.pivot = new Vector2(0f, 0.5f);
                grt.sizeDelta = new Vector2(adv + 6f, 44f);
                grt.anchoredPosition = new Vector2(x, 0f);
                var t = g.GetComponent<Text>();
                t.font = font;
                t.fontSize = TitleFontSize;
                t.fontStyle = FontStyle.Normal;
                t.text = c.ToString();
                t.color = titleColour;
                t.alignment = TextAnchor.MiddleLeft;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.supportRichText = false;
                t.raycastTarget = false;
                _titleGlyphs.Add(grt);
                _titleGlyphX.Add(x);
            }
            x += adv;
        }

        ApplyTitleSpacing(1f);
    }

    public void BuildSubtitle(string text)
    {
        EnsureBuilt();
        var go = new GameObject("Subtitle", typeof(Text));
        go.transform.SetParent(_content, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(LeftMargin, SubtitleY);
        rt.sizeDelta = new Vector2(ItemWidth, 20f);
        var t = go.GetComponent<Text>();
        t.font = FontOrBuiltin();
        t.fontSize = SubtitleFontSize;
        t.text = text;
        t.color = subtitleColour;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = false;
        t.raycastTarget = false;
        _subtitleGroup = go.AddComponent<CanvasGroup>();
        _subtitleGroup.alpha = 0f;
    }

    /// <summary>Adds one left-aligned, box-less menu row. rowIndex 0 is the top item.</summary>
    public ItemView AddItem(int rowIndex, string label, UnityAction onClick)
    {
        EnsureBuilt();

        var go = new GameObject("Item_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(_content, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);                 // left-anchored list
        rt.sizeDelta = new Vector2(ItemWidth, ItemHeight);
        var rest = new Vector2(LeftMargin, rowFirstY - rowIndex * rowPitch);
        rt.anchoredPosition = rest;

        var hit = go.GetComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);            // invisible but raycastable tap target
        hit.raycastTarget = true;

        var group = go.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        // slanted highlight bar, behind the label
        var bar = AddImage(rt, "Highlight", SlantSprite(), highlightColour,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(HighlightWidth, HighlightHeight), false);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.anchoredPosition = new Vector2(HighlightSlideIn, 0f);
        var barGroup = bar.gameObject.AddComponent<CanvasGroup>();
        barGroup.alpha = 0f;
        barGroup.interactable = false;
        barGroup.blocksRaycasts = false;

        var textGo = new GameObject("Label", typeof(Text));
        textGo.transform.SetParent(rt, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 0.5f);
        trt.anchorMax = new Vector2(1f, 0.5f);
        trt.pivot = new Vector2(0f, 0.5f);
        trt.offsetMin = new Vector2(LabelIndent, -14f);
        trt.offsetMax = new Vector2(-8f, 14f);
        var t = textGo.GetComponent<Text>();
        t.font = FontOrBuiltin();
        t.fontSize = ItemFontSize;
        t.text = label;
        t.color = itemColour;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = false;
        t.raycastTarget = false;

        var btn = go.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;      // we drive every visual ourselves
        btn.targetGraphic = hit;
        if (onClick != null) btn.onClick.AddListener(onClick);

        var pointer = go.AddComponent<LastBellMenuItemPointer>();
        pointer.style = this;
        pointer.index = _items.Count;

        var item = new ItemView();
        item.rect = rt;
        item.group = group;
        item.label = t;
        item.highlight = bar;
        item.highlightGroup = barGroup;
        item.button = btn;
        item.restPos = rest;
        item.index = _items.Count;
        item.bright = false;
        _items.Add(item);
        return item;
    }

    /// <summary>Extra element faded with the menu (e.g. the HUD pause button).</summary>
    public void RegisterAuxFade(CanvasGroup group, float closedAlpha, float openAlpha)
    {
        if (group == null) return;
        var aux = new AuxFade();
        aux.group = group;
        aux.closedAlpha = closedAlpha;
        aux.openAlpha = openAlpha;
        _aux.Add(aux);
    }

    static Image AddImage(RectTransform parent, string name, Sprite sprite, Color colour,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, bool raycast)
    {
        var go = new GameObject(name, typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        if (anchorMin == anchorMax) rt.sizeDelta = size;
        else { rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; }
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = colour;
        img.raycastTarget = raycast;
        return img;
    }

    // ================================================================== pointer input
    public void SetItemPressed(int index, bool pressed)
    {
        if (index < 0 || index >= _items.Count) return;
        _items[index].pressed = pressed;
        if (!pressed) _items[index].hovered = false;
    }

    public void SetItemHover(int index, bool hovered)
    {
        if (index < 0 || index >= _items.Count) return;
        _items[index].hovered = hovered;
    }

    void ApplyHighlight(ItemView it, float k)
    {
        it.highlightGroup.alpha = k;
        var p = it.highlight.rectTransform.anchoredPosition;
        p.x = Mathf.Lerp(HighlightSlideIn, 0f, k);
        it.highlight.rectTransform.anchoredPosition = p;

        bool bright = k > 0.45f;
        if (bright != it.bright)
        {
            it.bright = bright;
            it.label.color = bright ? itemActiveColour : itemColour;
        }
    }

    void Update()
    {
        if (!_built) return;
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            float target = it.pressed ? 1f : (it.hovered ? highlightHoverScale : 0f);
            if (Mathf.Approximately(it.highlightT, target)) continue;
            float dur = target > it.highlightT ? highlightInDuration : highlightOutDuration;
            it.highlightT = Mathf.MoveTowards(it.highlightT, target, dt / Mathf.Max(0.01f, dur));
            ApplyHighlight(it, highlightCurve.Evaluate(it.highlightT));
        }
    }

    // ================================================================== animation
    IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    void EnsureScratch()
    {
        if (_scratchAlpha == null || _scratchAlpha.Length < _items.Count)
        {
            _scratchAlpha = new float[_items.Count];
            _scratchPos = new Vector2[_items.Count];
        }
        if (_scratchAux == null || _scratchAux.Length < _aux.Count) _scratchAux = new float[_aux.Count];
    }

    /// <summary>
    /// Repositions the title glyphs to fake letter spacing (legacy Text has none). drift = 1 is the
    /// resting spacing; larger values push the glyphs apart for the open animation.
    /// </summary>
    void ApplyTitleSpacing(float drift)
    {
        int n = _titleGlyphs.Count;
        if (n == 0) return;
        for (int i = 0; i < n; i++)
        {
            float target = _titleGlyphX[i] + i * TitleExtraSpacing * drift;
            var grt = _titleGlyphs[i];
            if (!_spacingApplied || Mathf.Abs(grt.anchoredPosition.x - target) > 0.001f)
                grt.anchoredPosition = new Vector2(target, 0f);
        }
        _spacingApplied = true;
    }

    /// <summary>Snaps every animated value to the fully open state (no tween).</summary>
    public void SettleOpen()
    {
        EnsureBuilt();
        _content.gameObject.SetActive(true);
        _open = true;
        if (_titleGroup != null) _titleGroup.alpha = 1f;
        _spacingApplied = false;
        ApplyTitleSpacing(1f);
        if (_subtitleGroup != null) _subtitleGroup.alpha = 1f;
        if (_artGroup != null) _artGroup.alpha = artAlpha;
        _vignetteGroup.alpha = vignetteAlphaMin;
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            it.group.alpha = 1f;
            it.rect.anchoredPosition = it.restPos;
            it.highlightT = 0f;
            ApplyHighlight(it, 0f);
        }
        for (int i = 0; i < _aux.Count; i++)
        {
            _aux[i].group.alpha = _aux[i].openAlpha;
            _aux[i].group.blocksRaycasts = _aux[i].openAlpha > 0.01f;
            _aux[i].group.interactable = _aux[i].openAlpha > 0.01f;
        }
    }

    /// <summary>Snaps every animated value to the fully closed state and hides the content.</summary>
    public void SettleClosed()
    {
        EnsureBuilt();
        _open = false;
        ApplyClosedValues();
        for (int i = 0; i < _items.Count; i++) ApplyHighlight(_items[i], 0f);
    }

    void ApplyClosedValues()
    {
        if (_titleGroup != null) _titleGroup.alpha = 0f;
        if (_subtitleGroup != null) _subtitleGroup.alpha = 0f;
        if (_artGroup != null) _artGroup.alpha = 0f;
        if (_vignetteGroup != null) _vignetteGroup.alpha = 0f;
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            it.group.alpha = 0f;
            it.rect.anchoredPosition = it.restPos + new Vector2(itemSlideOffset, 0f);
        }
        for (int i = 0; i < _aux.Count; i++)
        {
            _aux[i].group.alpha = _aux[i].closedAlpha;
            _aux[i].group.blocksRaycasts = _aux[i].closedAlpha > 0.01f;
            _aux[i].group.interactable = _aux[i].closedAlpha > 0.01f;
        }
        if (_content != null) _content.gameObject.SetActive(false);
    }

    public void StopAnimations()
    {
        StopAllCoroutines();
        _pulse = null;
        _flicker = null;
    }

    /// <summary>Fades the menu in: title, subtitle, staggered items, art. Unscaled time.</summary>
    public IEnumerator OpenRoutine()
    {
        EnsureBuilt();
        StopAnimations();
        ApplyClosedValues();
        _content.gameObject.SetActive(true);
        _open = true;
        EnsureScratch();

        float total = Mathf.Max(titleFadeDuration,
            Mathf.Max(artFadeDuration, itemDelay + _items.Count * itemStagger + itemFadeDuration));

        float t = 0f;
        while (t < total)
        {
            t += Time.unscaledDeltaTime;

            if (_titleGroup != null)
            {
                float k = fadeCurve.Evaluate(Mathf.Clamp01(t / Mathf.Max(0.01f, titleFadeDuration)));
                _titleGroup.alpha = k;
                _spacingApplied = false;
                ApplyTitleSpacing(Mathf.Lerp(1f + titleSpacingDrift, 1f, k));
            }
            if (_subtitleGroup != null)
            {
                float k = fadeCurve.Evaluate(Mathf.Clamp01((t - subtitleDelay) / Mathf.Max(0.01f, subtitleFadeDuration)));
                _subtitleGroup.alpha = k;
            }
            if (_artGroup != null)
            {
                _artGroup.alpha = artAlpha * fadeCurve.Evaluate(Mathf.Clamp01(t / Mathf.Max(0.01f, artFadeDuration)));
            }
            _vignetteGroup.alpha = Mathf.Lerp(0f, vignetteAlphaMin,
                fadeCurve.Evaluate(Mathf.Clamp01(t / Mathf.Max(0.01f, artFadeDuration))));

            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                float start = itemDelay + i * itemStagger;
                float k = Mathf.Clamp01((t - start) / Mathf.Max(0.01f, itemFadeDuration));
                it.group.alpha = fadeCurve.Evaluate(k);
                it.rect.anchoredPosition = Vector2.LerpUnclamped(it.restPos + new Vector2(itemSlideOffset, 0f),
                    it.restPos, slideCurve.Evaluate(k));
            }
            for (int i = 0; i < _aux.Count; i++)
                _aux[i].group.alpha = Mathf.Lerp(_aux[i].closedAlpha, _aux[i].openAlpha,
                    fadeCurve.Evaluate(Mathf.Clamp01(t / Mathf.Max(0.01f, artFadeDuration))));

            yield return null;
        }

        SettleOpen();
        _pulse = StartCoroutine(VignettePulseRoutine());
        _flicker = StartCoroutine(TitleFlickerRoutine());
    }

    /// <summary>Quick reverse (closeDuration) used before unpausing. Unscaled time.</summary>
    public IEnumerator CloseRoutine()
    {
        EnsureBuilt();
        StopAnimations();
        _content.gameObject.SetActive(true);
        EnsureScratch();
        _open = false;

        float title0 = _titleGroup != null ? _titleGroup.alpha : 0f;
        float sub0 = _subtitleGroup != null ? _subtitleGroup.alpha : 0f;
        float art0 = _artGroup != null ? _artGroup.alpha : 0f;
        float vig0 = _vignetteGroup != null ? _vignetteGroup.alpha : 0f;
        for (int i = 0; i < _items.Count; i++)
        {
            _scratchAlpha[i] = _items[i].group.alpha;
            _scratchPos[i] = _items[i].rect.anchoredPosition;
        }
        for (int i = 0; i < _aux.Count; i++) _scratchAux[i] = _aux[i].group.alpha;

        float d = Mathf.Max(0.01f, closeDuration);
        float t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = fadeCurve.Evaluate(Mathf.Clamp01(t / d));
            if (_titleGroup != null) _titleGroup.alpha = Mathf.Lerp(title0, 0f, k);
            if (_subtitleGroup != null) _subtitleGroup.alpha = Mathf.Lerp(sub0, 0f, k);
            if (_artGroup != null) _artGroup.alpha = Mathf.Lerp(art0, 0f, k);
            if (_vignetteGroup != null) _vignetteGroup.alpha = Mathf.Lerp(vig0, 0f, k);
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                it.group.alpha = Mathf.Lerp(_scratchAlpha[i], 0f, k);
                it.rect.anchoredPosition = Vector2.Lerp(_scratchPos[i],
                    it.restPos + new Vector2(itemSlideOffset * 0.5f, 0f), k);
            }
            for (int i = 0; i < _aux.Count; i++)
                _aux[i].group.alpha = Mathf.Lerp(_scratchAux[i], _aux[i].closedAlpha, k);
            yield return null;
        }

        SettleClosed();
    }

    IEnumerator VignettePulseRoutine()
    {
        float period = Mathf.Max(1f, vignettePeriod);
        while (true)
        {
            float phase = Mathf.Repeat(Time.unscaledTime, period) / period;
            float s = 0.5f - 0.5f * Mathf.Cos(phase * 2f * Mathf.PI);
            _vignetteGroup.alpha = Mathf.Lerp(vignetteAlphaMin, vignetteAlphaMax, s);
            yield return null;
        }
    }

    IEnumerator TitleFlickerRoutine()
    {
        while (true)
        {
            float wait = Mathf.Lerp(flickerMinInterval, flickerMaxInterval, (float)_rng.NextDouble());
            yield return WaitUnscaled(wait);
            int dips = 1 + _rng.Next(2);
            for (int d = 0; d < dips; d++)
            {
                yield return DipTitle(_titleGroup.alpha, 0.42f, 0.045f);
                yield return DipTitle(0.42f, 1f, 0.09f);
                yield return WaitUnscaled(0.05f + (float)_rng.NextDouble() * 0.12f);
            }
        }
    }

    IEnumerator DipTitle(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _titleGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _titleGroup.alpha = to;
    }

    /// <summary>The content root; menu scripts toggle panels that live beside it.</summary>
    public bool IsOpen { get { return _open; } }
}

/// <summary>
/// Routes pointer state on a menu row back to the style. Split out so the style owns no
/// per-item closures and the animation code stays allocation free.
/// </summary>
public class LastBellMenuItemPointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerEnterHandler, IPointerExitHandler
{
    public LastBellMenuStyle style;
    public int index;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (style != null) style.SetItemPressed(index, true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (style != null) style.SetItemPressed(index, false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (style != null) style.SetItemHover(index, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (style != null)
        {
            style.SetItemHover(index, false);
            style.SetItemPressed(index, false);
        }
    }
}
