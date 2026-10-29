using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Shared look and builders for the menus, so every screen matches.</summary>
public static class UIFactory
{
    public static readonly Color Background = new Color32(0x0E, 0x0B, 0x14, 0xFF);
    public static readonly Color PanelFill = new Color32(0x1A, 0x14, 0x26, 0xF5);
    public static readonly Color PanelBorder = new Color32(0x6B, 0x4F, 0xA0, 0xFF);
    public static readonly Color Gold = new Color32(0xFF, 0xD4, 0x5E, 0xFF);
    public static readonly Color TextMain = new Color32(0xED, 0xE7, 0xF6, 0xFF);
    public static readonly Color TextMuted = new Color32(0x9A, 0x8F, 0xB0, 0xFF);
    public static readonly Color Good = new Color32(0x7A, 0xE0, 0x8A, 0xFF);
    public static readonly Color Bad = new Color32(0xFF, 0x7A, 0x7A, 0xFF);

    private static readonly Color ButtonNormal = new Color32(0x2C, 0x21, 0x45, 0xFF);
    private static readonly Color ButtonHover = new Color32(0x4A, 0x35, 0x78, 0xFF);
    private static readonly Color ButtonSelected = new Color32(0x6C, 0x4D, 0xB5, 0xFF);
    private static readonly Color ButtonPressed = new Color32(0x1E, 0x16, 0x30, 0xFF);

    private static Font font;

    /// <summary>Frame in which a panel consumed the Back/Esc press, so its owner doesn't react to it too.</summary>
    public static int BackHandledFrame = -1;

    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static bool BackPressed()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;

        var pad = Gamepad.current;
        return pad != null && pad.startButton.wasPressedThisFrame;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    public static void Select(GameObject go)
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(go);
    }

    // ------------------------------------------------------------ layout

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static VerticalLayoutGroup VLayout(RectTransform rect, int padding, float spacing)
    {
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, padding, padding);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return layout;
    }

    public static HorizontalLayoutGroup HLayout(RectTransform rect, float spacing)
    {
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        return layout;
    }

    public static LayoutElement Size(GameObject go, float height, float width = 0f)
    {
        var element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        if (height > 0f) element.preferredHeight = height;
        if (width > 0f)
        {
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
        }
        return element;
    }

    // ----------------------------------------------------------- widgets

    public static Canvas MakeCanvas(string name, int sortingOrder, Transform parent = null)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static RectTransform MakeOverlay(Transform parent, string name)
    {
        var rect = Rect(name, parent);
        Stretch(rect);
        rect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);
        return rect;
    }

    public static RectTransform MakePanel(Transform parent, string name, float width, float height)
    {
        var rect = Rect(name, parent);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = Vector2.zero;

        rect.gameObject.AddComponent<Image>().color = PanelFill;
        var outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = PanelBorder;
        outline.effectDistance = new Vector2(3f, -3f);
        return rect;
    }

    public static Text MakeLabel(Transform parent, string text, int size, TextAnchor anchor, Color color, float height = 0f)
    {
        var rect = Rect("Label", parent);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = Font;
        label.text = text;
        label.fontSize = size;
        label.alignment = anchor;
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;

        if (height > 0f) Size(rect.gameObject, height);
        return label;
    }

    public static Button MakeButton(Transform parent, string text, int size, System.Action onClick,
        out Text label, float height = 54f, float width = 0f)
    {
        var rect = Rect("Button", parent);

        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.white;

        var outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = PanelBorder;
        outline.effectDistance = new Vector2(2f, -2f);

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        var colors = button.colors;
        colors.normalColor = ButtonNormal;
        colors.highlightedColor = ButtonHover;
        colors.pressedColor = ButtonPressed;
        colors.selectedColor = ButtonSelected;
        colors.disabledColor = new Color(ButtonNormal.r, ButtonNormal.g, ButtonNormal.b, 0.5f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        label = MakeLabel(rect, text, size, TextAnchor.MiddleCenter, TextMain);
        Stretch(label.rectTransform);

        Size(rect.gameObject, height, width);

        button.onClick.AddListener(() => AudioManager.Play(Sfx.Click));
        if (onClick != null) button.onClick.AddListener(() => onClick());
        return button;
    }

    public static Slider MakeSlider(Transform parent, float value, System.Action<float> onChanged, float height = 30f)
    {
        var root = Rect("Slider", parent);
        var element = Size(root.gameObject, height);
        element.flexibleWidth = 1f;

        var background = Rect("Background", root);
        background.anchorMin = new Vector2(0f, 0.25f);
        background.anchorMax = new Vector2(1f, 0.75f);
        background.offsetMin = background.offsetMax = Vector2.zero;
        background.gameObject.AddComponent<Image>().color = ButtonPressed;
        var bgOutline = background.gameObject.AddComponent<Outline>();
        bgOutline.effectColor = PanelBorder;
        bgOutline.effectDistance = new Vector2(1f, -1f);

        var fillArea = Rect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0f, 0.25f);
        fillArea.anchorMax = new Vector2(1f, 0.75f);
        fillArea.anchoredPosition = new Vector2(-5f, 0f);
        fillArea.sizeDelta = new Vector2(-20f, 0f);

        var fill = Rect("Fill", fillArea);
        fill.sizeDelta = new Vector2(10f, 0f);
        fill.gameObject.AddComponent<Image>().color = PanelBorder;

        var slideArea = Rect("Handle Slide Area", root);
        slideArea.anchorMin = Vector2.zero;
        slideArea.anchorMax = Vector2.one;
        slideArea.offsetMin = new Vector2(10f, 0f);
        slideArea.offsetMax = new Vector2(-10f, 0f);

        var handle = Rect("Handle", slideArea);
        handle.sizeDelta = new Vector2(20f, 0f);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;

        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        var colors = slider.colors;
        colors.normalColor = Gold;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color32(0xD0, 0xA8, 0x30, 0xFF);
        colors.selectedColor = Color.white;
        slider.colors = colors;

        slider.value = value;
        if (onChanged != null) slider.onValueChanged.AddListener(v => onChanged(v));
        return slider;
    }
}
