using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// In-game overlays: pause menu (Esc), radio (M), settings, radio status line and toasts.
/// Added automatically by QuestionPanelUI. Freezes time while an overlay is open.
/// </summary>
public class GameMenus : MonoBehaviour
{
    public static GameMenus Instance { get; private set; }
    public static bool IsOverlayOpen { get; private set; }

    private GameObject pauseRoot;
    private CanvasGroup pauseGroup;
    private SettingsPanel settings;
    private RadioPanel radioPanel;
    private Text radioHud;
    private GameObject toastRoot;
    private Text toastText;
    private float toastUntil;
    private float nextHudRefresh;
    private InputAction radioKey;
    private Button resumeButton, pauseRadioButton, pauseSettingsButton;

    public static void ShowToast(string message, float seconds = 4f)
    {
        if (Instance != null) Instance.Toast(message, seconds);
    }

    private void Awake()
    {
        Instance = this;
        IsOverlayOpen = false;

        Build();

        radioKey = new InputAction("Radio", InputActionType.Button);
        radioKey.AddBinding("<Keyboard>/m");
        radioKey.AddBinding("<Gamepad>/select");
        radioKey.performed += _ => ToggleRadio();

        if (RadioController.Instance != null)
            RadioController.Instance.OwnedChanged += OnRadioOwnedChanged;
    }

    private void OnEnable()
    {
        radioKey?.Enable();
    }

    private void OnDisable()
    {
        radioKey?.Disable();
    }

    private void OnDestroy()
    {
        if (RadioController.Instance != null)
            RadioController.Instance.OwnedChanged -= OnRadioOwnedChanged;

        radioKey?.Dispose();

        Time.timeScale = 1f;
        IsOverlayOpen = false;
        if (Instance == this) Instance = null;
    }

    private void Build()
    {
        var canvas = UIFactory.MakeCanvas("GameMenusCanvas", 50, transform);
        var root = (RectTransform)canvas.transform;

        // Bottom-right status: radio + key hints.
        radioHud = UIFactory.MakeLabel(root, "", 20, TextAnchor.LowerRight, UIFactory.TextMuted);
        radioHud.rectTransform.anchorMin = radioHud.rectTransform.anchorMax = new Vector2(1f, 0f);
        radioHud.rectTransform.pivot = new Vector2(1f, 0f);
        radioHud.rectTransform.anchoredPosition = new Vector2(-16f, 14f);
        radioHud.rectTransform.sizeDelta = new Vector2(520f, 64f);
        radioHud.gameObject.AddComponent<Shadow>();

        // Toast.
        var toast = UIFactory.Rect("Toast", root);
        toast.anchorMin = toast.anchorMax = new Vector2(0.5f, 0f);
        toast.pivot = new Vector2(0.5f, 0f);
        toast.anchoredPosition = new Vector2(0f, 96f);
        toast.sizeDelta = new Vector2(980f, 60f);
        toast.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.07f, 0.16f, 0.92f);
        var toastOutline = toast.gameObject.AddComponent<Outline>();
        toastOutline.effectColor = UIFactory.Gold;
        toastOutline.effectDistance = new Vector2(2f, -2f);
        toastText = UIFactory.MakeLabel(toast, "", 24, TextAnchor.MiddleCenter, UIFactory.Gold);
        UIFactory.Stretch(toastText.rectTransform);
        toastRoot = toast.gameObject;
        toastRoot.SetActive(false);

        BuildPause(root);

        settings = SettingsPanel.Create(root, () => ReturnToPause(pauseSettingsButton));
        radioPanel = RadioPanel.Create(root, () => ReturnToPause(pauseRadioButton));
    }

    private void BuildPause(RectTransform root)
    {
        var overlay = UIFactory.MakeOverlay(root, "Pause");
        pauseRoot = overlay.gameObject;

        var panel = UIFactory.MakePanel(overlay, "Panel", 460f, 520f);
        var layout = UIFactory.VLayout(panel, 34, 14);
        layout.childAlignment = TextAnchor.MiddleCenter;
        pauseGroup = panel.gameObject.AddComponent<CanvasGroup>();

        var title = UIFactory.MakeLabel(panel, "", 48, TextAnchor.MiddleCenter, UIFactory.Gold, 70f);
        Loc.Bind(title, "ПАУЗА", "PAUSED");

        resumeButton = UIFactory.MakeButton(panel, "", 30, ClosePause, out Text resumeLabel, 58f);
        Loc.Bind(resumeLabel, "Продължи", "Resume");
        pauseRadioButton = UIFactory.MakeButton(panel, "", 30, OpenRadioFromPause, out Text radioLabel, 58f);
        Loc.Bind(radioLabel, "Радио", "Radio");
        pauseSettingsButton = UIFactory.MakeButton(panel, "", 30, OpenSettingsFromPause, out Text settingsLabel, 58f);
        Loc.Bind(settingsLabel, "Настройки", "Settings");
        UIFactory.MakeButton(panel, "", 30, ToMainMenu, out Text menuLabel, 58f);
        Loc.Bind(menuLabel, "Главно меню", "Main menu");

        pauseRoot.SetActive(false);
    }

    // ------------------------------------------------------------ state

    private void Update()
    {
        if (UIFactory.BackPressed() && UIFactory.BackHandledFrame != Time.frameCount
            && !settings.IsOpen && !radioPanel.IsOpen)
        {
            if (pauseRoot.activeSelf) ClosePause();
            else OpenPause();
        }

        bool open = pauseRoot.activeSelf || settings.IsOpen || radioPanel.IsOpen;
        if (open != IsOverlayOpen)
        {
            IsOverlayOpen = open;
            Time.timeScale = open ? 0f : 1f;
        }

        if (Time.unscaledTime >= nextHudRefresh)
        {
            nextHudRefresh = Time.unscaledTime + 0.25f;
            RefreshHud();
        }

        if (toastRoot.activeSelf && Time.unscaledTime > toastUntil)
            toastRoot.SetActive(false);
    }

    private void OpenPause()
    {
        pauseGroup.interactable = true;
        pauseRoot.SetActive(true);
        UIFactory.Select(resumeButton.gameObject);
    }

    private void ClosePause()
    {
        pauseRoot.SetActive(false);
    }

    private void ReturnToPause(Button button)
    {
        if (!pauseRoot.activeSelf) return;

        pauseGroup.interactable = true;
        UIFactory.Select(button.gameObject);
    }

    private void OpenSettingsFromPause()
    {
        pauseGroup.interactable = false;
        settings.Show();
    }

    private void OpenRadioFromPause()
    {
        if (!HasRadio()) return;

        pauseGroup.interactable = false;
        radioPanel.Show();
    }

    private void ToggleRadio()
    {
        if (pauseRoot.activeSelf || settings.IsOpen) return;

        if (!HasRadio()) return;

        if (radioPanel.IsOpen) radioPanel.Hide();
        else radioPanel.Show();
    }

    private bool HasRadio()
    {
        var radio = RadioController.Instance;
        if (radio != null && radio.Owned) return true;

        Toast(Loc.T("Още нямаш радио – потърси го в някоя от стаите!",
            "You don't have the radio yet – look for it in one of the rooms!"), 3f);
        return false;
    }

    private void ToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenu.MainMenuSceneName);
    }

    // -------------------------------------------------------------- HUD

    private void RefreshHud()
    {
        var radio = RadioController.Instance;
        string keys = Loc.T("M – радио   •   Esc – пауза", "M – radio   •   Esc – pause");

        if (radio == null || !radio.Owned)
        {
            radioHud.text = Loc.T("Радио: още не е намерено\nEsc – пауза", "Radio: not found yet\nEsc – pause");
            return;
        }

        string line;
        switch (radio.State)
        {
            case RadioState.Playing: line = Loc.T("Радио: ", "Radio: ") + CurrentName(radio); break;
            case RadioState.Paused: line = Loc.T("Радио (пауза): ", "Radio (paused): ") + CurrentName(radio); break;
            default: line = Loc.T("Радио: готово – натисни M", "Radio: ready – press M"); break;
        }

        radioHud.text = line + "\n" + keys;
    }

    private static string CurrentName(RadioController radio)
    {
        return radio.CurrentIndex >= 0 && radio.CurrentIndex < radio.Tracks.Count
            ? radio.Tracks[radio.CurrentIndex].Name
            : "";
    }

    private void OnRadioOwnedChanged(bool owned)
    {
        if (!owned) return;
        Toast(Loc.T("Имаш РАДИО! Натисни M и пусни музиката си от папка \"Radio\".",
            "You got the RADIO! Press M and play your music from the \"Radio\" folder."), 5f);
    }

    public void Toast(string message, float seconds)
    {
        toastText.text = message;
        toastUntil = Time.unscaledTime + seconds;
        toastRoot.SetActive(true);
        toastRoot.transform.SetAsLastSibling();
    }
}
