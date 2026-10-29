using UnityEngine;
using UnityEngine.UI;

/// <summary>Volume sliders, display mode and language. Esc / Back closes it.</summary>
public class SettingsPanel : MonoBehaviour
{
    private System.Action onClosed;
    private Slider firstSlider;
    private Text fullscreenLabel;
    private Text languageLabel;
    private float lastClickTime;

    public bool IsOpen => gameObject.activeSelf;

    public static SettingsPanel Create(Transform parent, System.Action onClosed)
    {
        var go = new GameObject("SettingsPanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        UIFactory.Stretch((RectTransform)go.transform);

        var panel = go.AddComponent<SettingsPanel>();
        panel.Build(onClosed);
        go.SetActive(false);
        return panel;
    }

    private void Build(System.Action closedCallback)
    {
        onClosed = closedCallback;

        UIFactory.MakeOverlay(transform, "Dim");
        var panel = UIFactory.MakePanel(transform, "Panel", 660f, 650f);
        UIFactory.VLayout(panel, 30, 14);

        var title = UIFactory.MakeLabel(panel, "", 42, TextAnchor.MiddleCenter, UIFactory.Gold, 58f);
        Loc.Bind(title, "НАСТРОЙКИ", "SETTINGS");

        firstSlider = AddRow(panel, "Музика", "Music", GameSettings.Music, v => GameSettings.Music = v);
        AddRow(panel, "Ефекти", "Effects", GameSettings.Sfx, v =>
        {
            GameSettings.Sfx = v;
            if (Time.unscaledTime - lastClickTime > 0.15f)
            {
                lastClickTime = Time.unscaledTime;
                AudioManager.Play(Sfx.Click);
            }
        });
        AddRow(panel, "Атмосфера", "Ambience", GameSettings.Ambient, v => GameSettings.Ambient = v);
        AddRow(panel, "Радио", "Radio", GameSettings.Radio, v => GameSettings.Radio = v);

        UIFactory.MakeButton(panel, "", 26, ToggleFullscreen, out fullscreenLabel, 52f);
        UIFactory.MakeButton(panel, "", 26, ToggleLanguage, out languageLabel, 52f);

        var hint = UIFactory.MakeLabel(panel, "", 16, TextAnchor.MiddleCenter, UIFactory.TextMuted, 28f);
        Loc.Bind(hint, "Стрелки или WASD – избор   •   ляво/дясно – промяна   •   Esc – назад",
            "Arrows or WASD – select   •   left/right – change   •   Esc – back");

        UIFactory.MakeButton(panel, "", 28, Hide, out Text backLabel, 56f);
        Loc.Bind(backLabel, "Назад", "Back");
        RefreshLabels();
    }

    private static Slider AddRow(RectTransform panel, string labelBg, string labelEn, float value, System.Action<float> setter)
    {
        var row = UIFactory.Rect("Row", panel);
        UIFactory.HLayout(row, 14f);
        UIFactory.Size(row.gameObject, 42f);

        var name = UIFactory.MakeLabel(row, "", 26, TextAnchor.MiddleLeft, UIFactory.TextMain);
        Loc.Bind(name, labelBg, labelEn);
        UIFactory.Size(name.gameObject, 0f, 190f);

        var percent = UIFactory.MakeLabel(row, "", 24, TextAnchor.MiddleRight, UIFactory.Gold);
        UIFactory.Size(percent.gameObject, 0f, 70f);

        var slider = UIFactory.MakeSlider(row, value, v =>
        {
            setter(v);
            percent.text = Mathf.RoundToInt(v * 100f) + "%";
        });
        percent.text = Mathf.RoundToInt(value * 100f) + "%";

        // Label, slider, percent order.
        slider.transform.SetSiblingIndex(1);
        return slider;
    }

    private void ToggleFullscreen()
    {
        GameSettings.Fullscreen = !GameSettings.Fullscreen;
    }

    private void ToggleLanguage()
    {
        GameSettings.Language = GameSettings.Language == Language.English ? Language.Bulgarian : Language.English;
    }

    private void OnEnable()
    {
        GameSettings.Changed += RefreshLabels;
    }

    private void OnDisable()
    {
        GameSettings.Changed -= RefreshLabels;
    }

    private void RefreshLabels()
    {
        if (fullscreenLabel == null || languageLabel == null) return;

        fullscreenLabel.text = Loc.T("Цял екран: ", "Fullscreen: ") +
                               (GameSettings.Fullscreen ? Loc.T("ДА", "ON") : Loc.T("НЕ", "OFF"));
        languageLabel.text = Loc.T("Език: Български", "Language: English");
    }

    public void Show()
    {
        gameObject.SetActive(true);
        RefreshLabels();
        UIFactory.Select(firstSlider.gameObject);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        onClosed?.Invoke();
    }

    private void Update()
    {
        if (!UIFactory.BackPressed()) return;

        UIFactory.BackHandledFrame = Time.frameCount;
        Hide();
    }
}
