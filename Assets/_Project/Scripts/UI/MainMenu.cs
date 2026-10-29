using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Start screen: New Game, Radio, Settings, Quit. Built entirely in code.</summary>
public class MainMenu : MonoBehaviour
{
    public const string GameSceneName = "Game";
    public const string MainMenuSceneName = "MainMenu";

    private const string GitHubUrl = "https://github.com/Giosho";
    private const string ItchUrl = "https://gioshoo.itch.io";

    private CanvasGroup buttonGroup;
    private CanvasGroup linksGroup;
    private SettingsPanel settings;
    private RadioPanel radioPanel;
    private Button newGameButton, radioButton, settingsButton;
    private Text radioHint;
    private bool shownEnglish;

    private void Start()
    {
        Time.timeScale = 1f;
        UIFactory.EnsureEventSystem();
        Build();
        UIFactory.Select(newGameButton.gameObject);
    }

    private void Build()
    {
        var canvas = UIFactory.MakeCanvas("MenuCanvas", 0, transform);
        var root = (RectTransform)canvas.transform;

        var background = UIFactory.Rect("Background", root);
        UIFactory.Stretch(background);
        background.gameObject.AddComponent<Image>().color = UIFactory.Background;
        background.gameObject.AddComponent<FloatingNumbers>();

        // Title block.
        var title = UIFactory.MakeLabel(root, "STATBOUND", 120, TextAnchor.MiddleCenter, UIFactory.Gold);
        Place(title.rectTransform, new Vector2(0f, 200f), new Vector2(1000f, 150f));
        title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(5f, -5f);

        var subtitle = UIFactory.MakeLabel(root, "", 28, TextAnchor.MiddleCenter, new Color32(0xB9, 0xA6, 0xE0, 0xFF));
        Loc.Bind(subtitle, "Подземие на средната стойност, модата и медианата",
            "A dungeon of the mean, the mode and the median");
        Place(subtitle.rectTransform, new Vector2(0f, 118f), new Vector2(1000f, 44f));

        var line = UIFactory.Rect("Line", root);
        Place(line, new Vector2(0f, 84f), new Vector2(560f, 3f));
        line.gameObject.AddComponent<Image>().color = UIFactory.PanelBorder;

        // Buttons.
        var column = UIFactory.Rect("Buttons", root);
        Place(column, new Vector2(0f, -70f), new Vector2(380f, 310f));
        var layout = UIFactory.VLayout(column, 0, 14f);
        layout.childAlignment = TextAnchor.MiddleCenter;
        buttonGroup = column.gameObject.AddComponent<CanvasGroup>();

        newGameButton = UIFactory.MakeButton(column, "", 32, StartGame, out Text newGameLabel, 62f);
        Loc.Bind(newGameLabel, "Нова игра", "New game");
        radioButton = UIFactory.MakeButton(column, "", 32, OpenRadio, out Text radioLabel, 62f);
        Loc.Bind(radioLabel, "Радио", "Radio");
        settingsButton = UIFactory.MakeButton(column, "", 32, OpenSettings, out Text settingsLabel, 62f);
        Loc.Bind(settingsLabel, "Настройки", "Settings");
        UIFactory.MakeButton(column, "", 32, Quit, out Text quitLabel, 62f);
        Loc.Bind(quitLabel, "Изход", "Quit");

        BuildLinks(root);

        radioHint = UIFactory.MakeLabel(root, "", 20, TextAnchor.MiddleCenter, UIFactory.TextMuted);
        Place(radioHint.rectTransform, new Vector2(0f, -285f), new Vector2(1000f, 30f));
        RefreshRadioHint();

        var footer = UIFactory.MakeLabel(root, "", 18, TextAnchor.MiddleCenter, UIFactory.TextMuted);
        Loc.Bind(footer,
            "WASD / стрелки – движение   •   Интервал – потвърди отговор   •   H – подсказка   •   M – радио   •   Esc – пауза",
            "WASD / arrows – move   •   Space – confirm answer   •   H – hint   •   M – radio   •   Esc – pause");
        Place(footer.rectTransform, new Vector2(0f, -330f), new Vector2(1200f, 30f));

        settings = SettingsPanel.Create(root, () => Reopen(settingsButton));
        radioPanel = RadioPanel.Create(root, () => { RefreshRadioHint(); Reopen(radioButton); });
    }

    /// <summary>Author links in the top-right corner: a coloured badge plus the site name.</summary>
    private void BuildLinks(RectTransform root)
    {
        var links = UIFactory.Rect("Links", root);
        links.anchorMin = links.anchorMax = links.pivot = new Vector2(1f, 1f);
        links.anchoredPosition = new Vector2(-24f, -24f);
        links.sizeDelta = new Vector2(210f, 124f);
        UIFactory.VLayout(links, 0, 10f);
        linksGroup = links.gameObject.AddComponent<CanvasGroup>();

        AddLink(links, "GitHub", "G", new Color32(0x33, 0x33, 0x3D, 0xFF), GitHubUrl);
        AddLink(links, "itch.io", "i", new Color32(0xFA, 0x5C, 0x5C, 0xFF), ItchUrl);
    }

    private static void AddLink(RectTransform parent, string name, string glyph, Color badgeColor, string url)
    {
        var button = UIFactory.MakeButton(parent, name, 24, () => Application.OpenURL(url), out Text label, 57f);
        label.rectTransform.offsetMin = new Vector2(52f, 0f);

        var badge = UIFactory.Rect("Badge", button.transform);
        badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f);
        badge.pivot = new Vector2(0.5f, 0.5f);
        badge.anchoredPosition = new Vector2(30f, 0f);
        badge.sizeDelta = new Vector2(36f, 36f);
        var image = badge.gameObject.AddComponent<Image>();
        image.color = badgeColor;
        image.raycastTarget = false;
        badge.gameObject.AddComponent<Outline>().effectColor = UIFactory.TextMain;

        var letter = UIFactory.MakeLabel(badge, glyph, 26, TextAnchor.MiddleCenter, Color.white);
        letter.fontStyle = FontStyle.Bold;
        UIFactory.Stretch(letter.rectTransform);
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void RefreshRadioHint()
    {
        var radio = RadioController.Instance;
        if (radio == null) return;

        radio.Refresh();
        shownEnglish = Loc.English;
        radioHint.text = radio.Tracks.Count > 0
            ? Loc.T("Радио: " + radio.Tracks.Count + " песни в папка \"Radio\"",
                "Radio: " + radio.Tracks.Count + " songs in the \"Radio\" folder")
            : Loc.T("Радио: сложи своя музика в папка \"Radio\" до играта",
                "Radio: put your own music in the \"Radio\" folder next to the game");
    }

    private void OnEnable()
    {
        GameSettings.Changed += OnSettingsChanged;
    }

    private void OnDisable()
    {
        GameSettings.Changed -= OnSettingsChanged;
    }

    private void OnSettingsChanged()
    {
        // Volume sliders raise this event too; only a language change needs a new hint text.
        if (radioHint != null && Loc.English != shownEnglish) RefreshRadioHint();
    }

    private void SetMenuInteractable(bool value)
    {
        buttonGroup.interactable = value;
        linksGroup.interactable = value;
    }

    private void Reopen(Button button)
    {
        SetMenuInteractable(true);
        UIFactory.Select(button.gameObject);
    }

    private void OpenSettings()
    {
        SetMenuInteractable(false);
        settings.Show();
    }

    private void OpenRadio()
    {
        SetMenuInteractable(false);
        radioPanel.Show();
    }

    private void StartGame()
    {
        SceneManager.LoadScene(GameSceneName);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
