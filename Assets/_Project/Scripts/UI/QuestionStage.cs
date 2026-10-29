using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The question as a place inside the room: a floating rune tablet in the middle and four rune pads
/// below it. The player answers by standing on a pad (it lights up) and pressing Space.
/// Everything is built in code from generated sprites, like the rest of the UI.
/// </summary>
public class QuestionStage : MonoBehaviour
{
    private enum PadState { Idle, Correct, Wrong, Dimmed }

    private sealed class Pad
    {
        public Transform Root;
        public SpriteRenderer Disc, Rune, Ripple;
        public Text Number, Prompt;
        public Image Plaque;
        public Outline PlaqueOutline;
        public RectTransform Label;
        public Transform LabelAnchor;
        public Vector3 HomePosition;
        public PadState State;
        public float Hover;
        public float Pop;
        public float Shake;
    }

    private const int PadCount = 4;
    private const float HoverRadius = 1.25f;
    private const float TabletY = 1.6f;
    private const float PadY = -2.9f;
    private const float TabletHalfWidth = 4.8f;
    private const float TabletHalfHeight = 1.8f;
    private const float ShakeTime = 0.45f;
    private const float CanvasScale = 0.01f; // 100 canvas pixels per world unit
    private static readonly float[] PadX = { -6f, -2f, 2f, 6f };

    private static readonly Color RuneIdle = new Color(0.62f, 0.5f, 0.95f, 0.55f);
    private static readonly Color RuneHover = new Color(1f, 0.83f, 0.37f, 1f);
    private static readonly Color RuneCorrect = new Color(0.48f, 0.88f, 0.54f, 1f);
    private static readonly Color RuneWrong = new Color(1f, 0.35f, 0.35f, 0.55f);
    private static readonly Color RuneDimmed = new Color(0.5f, 0.42f, 0.7f, 0.22f);
    private static readonly Color DiscIdle = new Color(0.25f, 0.17f, 0.45f, 0.4f);
    private static readonly Color DiscHover = new Color(0.95f, 0.7f, 0.2f, 0.55f);
    private static readonly Color DiscCorrect = new Color(0.2f, 0.6f, 0.3f, 0.6f);
    private static readonly Color DiscWrong = new Color(0.5f, 0.1f, 0.1f, 0.5f);
    private static readonly Color DiscDimmed = new Color(0.15f, 0.12f, 0.25f, 0.25f);
    private static readonly Color TabletFill = new Color(0.07f, 0.05f, 0.12f, 0.94f);

    private static Sprite discSprite, runeSprite, ringSprite;

    private readonly Pad[] pads = new Pad[PadCount];
    private Transform player;
    private Transform tabletRoot;
    private RectTransform tabletRect;
    private Canvas textCanvas;
    private Camera cam;
    private CanvasGroup tabletGroup;
    private SpriteRenderer tabletGlow;
    private Text titleText, promptText, feedbackText;
    private StatQuestion current;
    private Func<string> feedbackSource;
    private bool shownEnglish;
    private float tabletPop = 1f;
    private float nearFade = 1f;

    /// <summary>Index of the pad the player is standing on, or -1.</summary>
    public int Hovered { get; private set; } = -1;

    public static QuestionStage Create(Transform player)
    {
        var stage = new GameObject("QuestionStage").AddComponent<QuestionStage>();
        stage.Build(player);
        return stage;
    }

    private void Build(Transform playerTransform)
    {
        player = playerTransform;
        EnsureSprites();
        BuildTablet();
        for (int i = 0; i < PadCount; i++) pads[i] = BuildPad(i);

        shownEnglish = Loc.English;
        GameSettings.Changed += OnSettingsChanged;
    }

    private void OnDestroy()
    {
        GameSettings.Changed -= OnSettingsChanged;
    }

    // ----------------------------------------------------------- public API

    public void ShowQuestion(StatQuestion question)
    {
        current = question;
        feedbackSource = null;

        tabletPop = 0f;
        for (int i = 0; i < PadCount; i++)
        {
            pads[i].State = PadState.Idle;
            pads[i].Pop = -0.25f * i - 0.2f;
            pads[i].Shake = 0f;
        }

        RefreshTexts();
    }

    public void SetFeedback(Func<string> source, Color color)
    {
        feedbackSource = source;
        feedbackText.color = color;
        feedbackText.text = source != null ? source() : "";
    }

    public void MarkCorrect(int index)
    {
        for (int i = 0; i < PadCount; i++)
        {
            if (i == index) pads[i].State = PadState.Correct;
            else if (pads[i].State == PadState.Idle) pads[i].State = PadState.Dimmed;
        }
    }

    public void MarkWrong(int index)
    {
        pads[index].State = PadState.Wrong;
        pads[index].Shake = ShakeTime;
    }

    // --------------------------------------------------------------- update

    private void Update()
    {
        float dt = Time.deltaTime;

        UpdateHover();
        UpdateTablet(dt);
        for (int i = 0; i < PadCount; i++) UpdatePad(pads[i], i == Hovered, dt);
    }

    private void UpdateHover()
    {
        int best = -1;
        float bestSqr = HoverRadius * HoverRadius;

        if (player != null && pads[0] != null)
        {
            Vector2 p = player.position;
            for (int i = 0; i < PadCount; i++)
            {
                if (pads[i].State != PadState.Idle) continue;

                float sqr = (p - (Vector2)pads[i].HomePosition).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
        }

        if (best == Hovered) return;

        Hovered = best;
        if (best >= 0 && !GameMenus.IsOverlayOpen) AudioManager.Play(Sfx.Click);
    }

    private void UpdateTablet(float dt)
    {
        tabletPop = Mathf.Min(1f, tabletPop + dt * 3.5f);
        float ease = EaseOutBack(tabletPop);

        // The tablet floats, and turns see-through when the player walks underneath it.
        bool near = player != null
                    && Mathf.Abs(player.position.x) < TabletHalfWidth + 0.5f
                    && Mathf.Abs(player.position.y - TabletY) < TabletHalfHeight + 0.5f;
        nearFade = Mathf.MoveTowards(nearFade, near ? 0.28f : 1f, dt * 4f);

        tabletRoot.localPosition = new Vector3(0f, TabletY + Mathf.Sin(Time.time * 1.4f) * 0.06f, 0f);
        tabletRoot.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, ease);
        tabletGroup.alpha = Mathf.Min(Mathf.Clamp01(tabletPop * 1.5f), nearFade);

        var glow = tabletGlow.color;
        glow.a = (0.22f + 0.06f * Mathf.Sin(Time.time * 2f)) * Mathf.Clamp01(tabletPop);
        tabletGlow.color = glow;
    }

    private void UpdatePad(Pad pad, bool hovered, float dt)
    {
        pad.Pop = Mathf.Min(1f, pad.Pop + dt * 3.5f);
        pad.Hover = Mathf.MoveTowards(pad.Hover, hovered && pad.State == PadState.Idle ? 1f : 0f, dt * 8f);
        pad.Shake = Mathf.Max(0f, pad.Shake - dt);

        float time = Time.time;
        float scale = pad.Pop <= 0f ? 0f : EaseOutBack(pad.Pop);
        pad.Root.localScale = Vector3.one * scale * (1f + 0.06f * pad.Hover);

        float shakeX = Mathf.Sin(time * 70f) * 0.12f * (pad.Shake / ShakeTime);
        pad.Root.position = pad.HomePosition + new Vector3(shakeX, 0f, 0f);

        Color rune, disc, plaque, number;
        switch (pad.State)
        {
            case PadState.Correct:
                rune = RuneCorrect;
                disc = DiscCorrect;
                plaque = UIFactory.Good;
                number = UIFactory.Good;
                break;
            case PadState.Wrong:
                rune = RuneWrong;
                disc = DiscWrong;
                plaque = UIFactory.Bad;
                number = new Color(UIFactory.Bad.r, UIFactory.Bad.g, UIFactory.Bad.b, 0.55f);
                break;
            case PadState.Dimmed:
                rune = RuneDimmed;
                disc = DiscDimmed;
                plaque = UIFactory.PanelBorder;
                number = new Color(UIFactory.TextMuted.r, UIFactory.TextMuted.g, UIFactory.TextMuted.b, 0.5f);
                break;
            default:
                float pulse = 0.85f + 0.15f * Mathf.Sin(time * 6f);
                rune = Color.Lerp(RuneIdle, RuneHover, pad.Hover);
                rune.a *= Mathf.Lerp(1f, pulse, pad.Hover);
                disc = Color.Lerp(DiscIdle, DiscHover, pad.Hover);
                plaque = Color.Lerp(UIFactory.PanelBorder, UIFactory.Gold, pad.Hover);
                number = Color.Lerp(UIFactory.TextMain, UIFactory.Gold, pad.Hover);
                break;
        }

        pad.Rune.color = rune;
        pad.Disc.color = disc;
        pad.Rune.transform.localRotation = Quaternion.Euler(0f, 0f, time * (12f + 55f * pad.Hover));

        // Ripple that rolls outwards from the pad the player is standing on.
        float phase = Mathf.Repeat(time * 1.3f, 1f);
        pad.Ripple.transform.localScale = Vector3.one * 2.3f * (1f + 0.4f * phase);
        pad.Ripple.color = new Color(RuneHover.r, RuneHover.g, RuneHover.b, (1f - phase) * 0.6f * pad.Hover);

        pad.Plaque.color = new Color(0.07f, 0.05f, 0.12f, 0.88f);
        pad.PlaqueOutline.effectColor = plaque;
        pad.Number.color = number;
        pad.Prompt.color = new Color(UIFactory.Gold.r, UIFactory.Gold.g, UIFactory.Gold.b, pad.Hover);
    }

    // ----------------------------------------------------------------- text

    private void OnSettingsChanged()
    {
        // Volume sliders raise this event too; only react to a real language change.
        if (Loc.English == shownEnglish) return;

        shownEnglish = Loc.English;
        RefreshTexts();
    }

    private void RefreshTexts()
    {
        if (current == null) return;

        titleText.text = QuestionGenerator.KindLabel(current.Kind, Loc.English) + "   •   " +
                         Loc.T("ниво", "level") + " " + current.Difficulty;
        promptText.text = current.Prompt;
        feedbackText.text = feedbackSource != null ? feedbackSource() : "";

        string press = Loc.T("[ ИНТЕРВАЛ ]", "[ SPACE ]");
        for (int i = 0; i < PadCount; i++)
        {
            pads[i].Number.text = current.OptionText(i);
            pads[i].Prompt.text = press;
        }
    }

    // ---------------------------------------------------------------- build

    private void BuildTablet()
    {
        tabletRoot = new GameObject("Tablet").transform;
        tabletRoot.SetParent(transform, false);

        var glowObject = new GameObject("Glow");
        glowObject.transform.SetParent(tabletRoot, false);
        glowObject.transform.localScale = new Vector3(13f, 6.4f, 1f);
        tabletGlow = glowObject.AddComponent<SpriteRenderer>();
        tabletGlow.sprite = discSprite;
        tabletGlow.color = new Color(0.42f, 0.3f, 0.8f, 0.22f);
        tabletGlow.sortingLayerName = "Entities";
        tabletGlow.sortingOrder = 40;

        var rect = MakeStageRect("Tablet", new Vector2(960f, 360f));
        tabletRect = rect;
        tabletGroup = rect.gameObject.AddComponent<CanvasGroup>();
        tabletGroup.interactable = false;
        tabletGroup.blocksRaycasts = false;

        var background = MakeImage("Background", rect, TabletFill);
        UIFactory.Stretch(background);
        var outline = background.gameObject.AddComponent<Outline>();
        outline.effectColor = UIFactory.Gold;
        outline.effectDistance = new Vector2(3f, -3f);

        MakeFrame(rect, 14f, 2f, UIFactory.PanelBorder);
        MakeFrame(rect, 22f, 1f, new Color(UIFactory.PanelBorder.r, UIFactory.PanelBorder.g, UIFactory.PanelBorder.b, 0.45f));

        // Gold diamonds on the corners.
        for (int i = 0; i < 4; i++)
        {
            var corner = MakeImage("Corner" + i, rect, UIFactory.Gold);
            var anchor = new Vector2(i % 2 == 0 ? 0f : 1f, i < 2 ? 1f : 0f);
            corner.anchorMin = corner.anchorMax = anchor;
            corner.anchoredPosition = Vector2.zero;
            corner.sizeDelta = new Vector2(24f, 24f);
            corner.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        titleText = UIFactory.MakeLabel(rect, "", 26, TextAnchor.MiddleCenter, new Color(0.8f, 0.7f, 1f));
        titleText.fontStyle = FontStyle.Bold;
        Anchor(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -26f), new Vector2(-80f, 40f));

        var divider = MakeImage("Divider", rect, UIFactory.PanelBorder);
        Anchor(divider, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -72f), new Vector2(520f, 3f));

        promptText = UIFactory.MakeLabel(rect, "", 38, TextAnchor.MiddleCenter, Color.white);
        promptText.resizeTextForBestFit = true;
        promptText.resizeTextMinSize = 22;
        promptText.resizeTextMaxSize = 38;
        promptText.rectTransform.anchorMin = Vector2.zero;
        promptText.rectTransform.anchorMax = Vector2.one;
        promptText.rectTransform.offsetMin = new Vector2(50f, 118f);
        promptText.rectTransform.offsetMax = new Vector2(-50f, -84f);

        var lower = MakeImage("LowerDivider", rect, new Color(UIFactory.PanelBorder.r, UIFactory.PanelBorder.g, UIFactory.PanelBorder.b, 0.6f));
        Anchor(lower, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 108f), new Vector2(720f, 2f));

        feedbackText = UIFactory.MakeLabel(rect, "", 24, TextAnchor.MiddleCenter, Color.white);
        feedbackText.resizeTextForBestFit = true;
        feedbackText.resizeTextMinSize = 14;
        feedbackText.resizeTextMaxSize = 24;
        Anchor(feedbackText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 20f), new Vector2(-100f, 80f));
    }

    private Pad BuildPad(int index)
    {
        var pad = new Pad();

        var root = new GameObject("Pad" + (index + 1)).transform;
        root.SetParent(transform, false);
        pad.HomePosition = new Vector3(PadX[index], PadY, 0f);
        root.position = pad.HomePosition;
        pad.Root = root;

        pad.Disc = MakeSprite("Disc", root, discSprite, 3.2f, "Floor", 5);
        pad.Ripple = MakeSprite("Ripple", root, ringSprite, 2.3f, "Floor", 6);
        pad.Rune = MakeSprite("Rune", root, runeSprite, 2.3f, "Floor", 7);

        var labelAnchor = new GameObject("LabelAnchor").transform;
        labelAnchor.SetParent(root, false);
        labelAnchor.localPosition = new Vector3(0f, 1.7f, 0f);
        pad.LabelAnchor = labelAnchor;

        var labelRect = MakeStageRect("Label" + (index + 1), new Vector2(260f, 150f));
        pad.Label = labelRect;

        var plaque = MakeImage("Plaque", labelRect, UIFactory.PanelFill);
        plaque.anchorMin = Vector2.zero;
        plaque.anchorMax = Vector2.one;
        plaque.offsetMin = new Vector2(24f, 30f);
        plaque.offsetMax = new Vector2(-24f, 0f);
        pad.Plaque = plaque.GetComponent<Image>();
        pad.PlaqueOutline = plaque.gameObject.AddComponent<Outline>();
        pad.PlaqueOutline.effectDistance = new Vector2(3f, -3f);

        pad.Number = UIFactory.MakeLabel(plaque, "", 70, TextAnchor.MiddleCenter, UIFactory.TextMain);
        pad.Number.fontStyle = FontStyle.Bold;
        pad.Number.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.Stretch(pad.Number.rectTransform);

        pad.Prompt = UIFactory.MakeLabel(labelRect, "", 22, TextAnchor.MiddleCenter, UIFactory.Gold);
        pad.Prompt.fontStyle = FontStyle.Bold;
        Anchor(pad.Prompt.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            Vector2.zero, new Vector2(0f, 30f));

        pad.Root.localScale = Vector3.zero;
        pad.Pop = -1f;
        return pad;
    }

    private static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, float size, string layer, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * size;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = layer;
        renderer.sortingOrder = order;
        return renderer;
    }

    /// <summary>
    /// A text block laid out as if 1 world unit = 100 pixels. It lives on a screen-space canvas
    /// (see Follow), because the Pixel Perfect Camera renders the world at 320x180 and would blur the text.
    /// </summary>
    private RectTransform MakeStageRect(string name, Vector2 size)
    {
        if (textCanvas == null)
        {
            var canvasObject = new GameObject("StageText");
            canvasObject.transform.SetParent(transform, false);
            textCanvas = canvasObject.AddComponent<Canvas>();
            textCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            textCanvas.sortingOrder = 5;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        var rect = UIFactory.Rect(name, textCanvas.transform);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        return rect;
    }

    /// <summary>Places a stage text block over a world position, scaled like the world around it.</summary>
    private void Follow(RectTransform rect, Vector3 worldPosition, float worldScale)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        float canvasScale = textCanvas.scaleFactor;
        float screenPerUnit = Screen.height / (2f * cam.orthographicSize);
        Vector2 screen = cam.WorldToScreenPoint(worldPosition);

        rect.anchoredPosition = (screen - new Vector2(Screen.width, Screen.height) * 0.5f) / canvasScale;
        rect.localScale = Vector3.one * (screenPerUnit / canvasScale * CanvasScale * worldScale);
    }

    private void LateUpdate()
    {
        if (tabletRect == null) return;

        Follow(tabletRect, tabletRoot.position, tabletRoot.localScale.x);
        foreach (var pad in pads)
            Follow(pad.Label, pad.LabelAnchor.position,
                pad.LabelAnchor.lossyScale.x * (1f + 0.18f * pad.Hover));
    }

    private static RectTransform MakeImage(string name, Transform parent, Color color)
    {
        var rect = UIFactory.Rect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static void MakeFrame(Transform parent, float inset, float thickness, Color color)
    {
        var top = MakeImage("FrameTop", parent, color);
        Anchor(top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -inset), new Vector2(-2f * inset, thickness));

        var bottom = MakeImage("FrameBottom", parent, color);
        Anchor(bottom, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, inset), new Vector2(-2f * inset, thickness));

        var left = MakeImage("FrameLeft", parent, color);
        Anchor(left, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(inset, 0f), new Vector2(thickness, -2f * inset));

        var right = MakeImage("FrameRight", parent, color);
        Anchor(right, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
            new Vector2(-inset, 0f), new Vector2(thickness, -2f * inset));
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // --------------------------------------------------------------- sprites

    private static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private static void EnsureSprites()
    {
        if (discSprite != null) return;

        // Soft glow.
        discSprite = BakeSprite(128, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float fade = Mathf.Clamp01(1f - r);
            return fade * fade;
        });

        // Thin ring used for the ripple.
        ringSprite = BakeSprite(256, (x, y) => Band(Mathf.Sqrt(x * x + y * y), 0.95f, 0.03f));

        // The rune circle: two rings, eight ticks, a diamond and a core ring.
        runeSprite = BakeSprite(256, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            float a = Band(r, 0.93f, 0.035f) + Band(r, 0.72f, 0.02f) + Band(r, 0.12f, 0.03f);
            a += Band(Mathf.Abs(x) + Mathf.Abs(y), 0.45f, 0.03f);

            float step = Mathf.PI / 4f;
            float angle = Mathf.Repeat(Mathf.Atan2(y, x), step);
            float arc = Mathf.Min(angle, step - angle) * r;
            float along = Mathf.Clamp01(1f - Mathf.Abs(r - 0.825f) / 0.07f);
            a += Mathf.Clamp01(1f - arc / 0.02f) * along;

            return a;
        });
    }

    private static float Band(float value, float center, float halfWidth)
    {
        return Mathf.Clamp01(1f - Mathf.Abs(value - center) / halfWidth);
    }

    private static Sprite BakeSprite(int size, Func<float, float, float> alphaAt)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color32[size * size];
        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                float x = (px + 0.5f) / size * 2f - 1f;
                float y = (py + 0.5f) / size * 2f - 1f;
                float alpha = Mathf.Clamp01(alphaAt(x, y));
                pixels[py * size + px] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
