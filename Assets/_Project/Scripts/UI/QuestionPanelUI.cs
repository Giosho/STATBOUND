using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Owns the question flow on the UI side: builds the in-room QuestionStage (tablet + answer pads),
/// the status line, hint button and game-over screen in code, so there is nothing to wire up by hand.
/// Answer: stand on a pad and press Space (gamepad A). Hint: H.
/// </summary>
public class QuestionPanelUI : MonoBehaviour
{
    [SerializeField] private QuestionManager questions;
    [SerializeField] private RoomController room;
    [SerializeField] private HealthSystem health;

    private static readonly Color GoodText = new Color(0.55f, 0.95f, 0.6f);
    private static readonly Color BadText = new Color(1f, 0.5f, 0.5f);
    private static readonly Color HintText = new Color(1f, 0.87f, 0.45f);

    private Font font;
    private QuestionStage stage;
    private Text statusText;
    private Button hintButton;
    private GameObject gameOverRoot;

    private InputAction confirmKey;
    private InputAction hintKey;
    private InputAction restartKey;

    private void Awake()
    {
        if (questions == null || room == null || health == null)
        {
            Debug.LogError("QuestionPanelUI is missing references. Run STATBOUND > Setup Stage 2.", this);
            enabled = false;
            return;
        }

        var player = FindFirstObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("QuestionPanelUI needs a PlayerController in the scene.", this);
            enabled = false;
            return;
        }

        font = UIFactory.Font;
        UIFactory.EnsureEventSystem();
        stage = QuestionStage.Create(player.transform);
        BuildUI();
        BuildInput();

        // Pause menu, radio, settings and toasts live in their own component.
        if (GetComponent<GameMenus>() == null) gameObject.AddComponent<GameMenus>();

        questions.QuestionAsked += ShowQuestion;
        questions.AnswerSubmitted += ShowAnswerResult;
        questions.HintsChanged += RefreshStatus;
        room.RoomEntered += OnRoomEntered;
        health.Changed += OnHealthChanged;
        health.Died += ShowGameOver;
        GameSettings.Changed += RefreshStatus;

        RefreshStatus();
    }

    private void OnEnable()
    {
        if (confirmKey == null) return;
        confirmKey.Enable();
        hintKey.Enable();
        restartKey.Enable();
    }

    private void OnDisable()
    {
        if (confirmKey == null) return;
        confirmKey.Disable();
        hintKey.Disable();
        restartKey.Disable();
    }

    private void OnDestroy()
    {
        GameSettings.Changed -= RefreshStatus;

        if (questions != null)
        {
            questions.QuestionAsked -= ShowQuestion;
            questions.AnswerSubmitted -= ShowAnswerResult;
            questions.HintsChanged -= RefreshStatus;
        }
        if (room != null) room.RoomEntered -= OnRoomEntered;
        if (health != null)
        {
            health.Changed -= OnHealthChanged;
            health.Died -= ShowGameOver;
        }

        confirmKey?.Dispose();
        hintKey?.Dispose();
        restartKey?.Dispose();

        if (stage != null) Destroy(stage.gameObject);
    }

    // ------------------------------------------------------------- events

    private void ShowQuestion(StatQuestion q)
    {
        stage.ShowQuestion(q);
        stage.SetFeedback(() => Loc.T(
            "Застани върху отговор и натисни ИНТЕРВАЛ. Вратите са заключени.",
            "Stand on an answer and press SPACE. The doors are locked."), Color.white);

        RefreshStatus();
    }

    private void ShowAnswerResult(bool correct, int index, StatQuestion q)
    {
        if (correct)
        {
            AudioManager.Play(Sfx.Correct);
            stage.MarkCorrect(index);
            stage.SetFeedback(() => Loc.T("Вярно! ", "Correct! ") + q.Explanation +
                                    Loc.T("  Вратите се отвориха.", "  The doors are open."), GoodText);
        }
        else
        {
            AudioManager.Play(Sfx.Wrong);
            AudioManager.Play(Sfx.Damage);
            stage.MarkWrong(index);
            stage.SetFeedback(() => Loc.T("Грешно! Губиш живот. Опитай пак.", "Wrong! You lose health. Try again."), BadText);
        }
    }

    private void OnRoomEntered(int count) { RefreshStatus(); }
    private void OnHealthChanged(int current, int max) { RefreshStatus(); }

    private void ShowGameOver()
    {
        gameOverRoot.SetActive(true);
    }

    private void RefreshStatus()
    {
        if (statusText == null) return;
        statusText.text = Loc.T("Живот: ", "Health: ") + health.Current + "/" + health.Max +
                          Loc.T("     Стая: ", "     Room: ") + room.RoomsVisited +
                          Loc.T("     Подсказки: ", "     Hints: ") + questions.HintsRemaining;
    }

    // -------------------------------------------------------------- input

    private void BuildInput()
    {
        confirmKey = new InputAction("Confirm", InputActionType.Button);
        confirmKey.AddBinding("<Keyboard>/space");
        confirmKey.AddBinding("<Gamepad>/buttonSouth");
        confirmKey.performed += _ => OnConfirmPressed();

        hintKey = new InputAction("Hint", InputActionType.Button);
        hintKey.AddBinding("<Keyboard>/h");
        hintKey.AddBinding("<Gamepad>/rightShoulder");
        hintKey.performed += _ => OnHintPressed();

        restartKey = new InputAction("Restart", InputActionType.Button);
        restartKey.AddBinding("<Keyboard>/r");
        restartKey.AddBinding("<Gamepad>/start");
        restartKey.performed += _ => { if (health.IsDead) Restart(); };
    }

    private void OnConfirmPressed()
    {
        if (health.IsDead || GameMenus.IsOverlayOpen || questions.IsSolved) return;

        if (stage.Hovered >= 0)
            questions.SubmitAnswer(stage.Hovered);
        else
            stage.SetFeedback(() => Loc.T("Първо застани върху някой от отговорите.",
                "First stand on one of the answers."), HintText);
    }

    private void OnHintPressed()
    {
        if (health.IsDead || GameMenus.IsOverlayOpen) return;

        if (questions.TryUseHint(out string hint))
        {
            AudioManager.Play(Sfx.Hint);
            stage.SetFeedback(() => Loc.T("Подсказка: ", "Hint: ") + questions.Current.Hint, HintText);
        }
        else if (!questions.IsSolved)
            stage.SetFeedback(() => Loc.T("Нямаш повече подсказки!", "No hints left!"), BadText);
    }

    private void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ----------------------------------------------------------------- UI

    private void BuildUI()
    {
        var canvasGo = new GameObject("QuestionCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // above the stage text (5), below the pause menu (50)
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Status line + hint button (bottom left).
        statusText = NewText("Status", canvasGo.transform, 24, TextAnchor.LowerLeft, Color.white);
        statusText.rectTransform.anchorMin = statusText.rectTransform.anchorMax = Vector2.zero;
        statusText.rectTransform.pivot = Vector2.zero;
        statusText.rectTransform.anchoredPosition = new Vector2(16f, 72f);
        statusText.rectTransform.sizeDelta = new Vector2(800f, 36f);
        statusText.gameObject.AddComponent<Shadow>();

        hintButton = NewButton("HintButton", canvasGo.transform, out Text hintLabel, 22);
        Loc.Bind(hintLabel, "Подсказка [H]", "Hint [H]");
        var hintRect = (RectTransform)hintButton.transform;
        hintRect.anchorMin = hintRect.anchorMax = Vector2.zero;
        hintRect.pivot = Vector2.zero;
        hintRect.anchoredPosition = new Vector2(16f, 16f);
        hintRect.sizeDelta = new Vector2(220f, 48f);
        hintButton.onClick.AddListener(() =>
        {
            OnHintPressed();
            // A clicked button stays selected and would react to Space as well.
            UIFactory.Select(null);
        });

        BuildGameOver(canvasGo.transform);
    }

    private void BuildGameOver(Transform canvas)
    {
        var root = NewRect("GameOver", canvas);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

        var title = NewText("Title", root, 64, TextAnchor.MiddleCenter, BadText);
        Loc.Bind(title, "КРАЙ НА ИГРАТА", "GAME OVER");
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        title.rectTransform.anchoredPosition = new Vector2(0f, 60f);
        title.rectTransform.sizeDelta = new Vector2(900f, 100f);

        var button = NewButton("RestartButton", root, out Text label, 30);
        Loc.Bind(label, "Нова игра [R]", "New game [R]");
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -40f);
        rect.sizeDelta = new Vector2(320f, 70f);
        button.onClick.AddListener(Restart);

        var menuButton = NewButton("MenuButton", root, out Text menuLabel, 30);
        Loc.Bind(menuLabel, "Главно меню", "Main menu");
        var menuRect = (RectTransform)menuButton.transform;
        menuRect.anchorMin = menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.anchoredPosition = new Vector2(0f, -125f);
        menuRect.sizeDelta = new Vector2(320f, 70f);
        menuButton.onClick.AddListener(() => SceneManager.LoadScene(MainMenu.MainMenuSceneName));

        gameOverRoot = root.gameObject;
        gameOverRoot.SetActive(false);
    }

    private RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private Text NewText(string name, Transform parent, int size, TextAnchor anchor, Color color)
    {
        var rect = NewRect(name, parent);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private Button NewButton(string name, Transform parent, out Text label, int fontSize)
    {
        var rect = NewRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.white;

        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ApplyColors(button);

        label = NewText("Label", rect, fontSize, TextAnchor.MiddleCenter, Color.white);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static void ApplyColors(Button button)
    {
        var buttonColor = new Color(0.22f, 0.17f, 0.32f, 1f);

        var colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = Color.Lerp(buttonColor, Color.white, 0.25f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Color.Lerp(buttonColor, Color.black, 0.25f);
        colors.disabledColor = new Color(0.35f, 0.33f, 0.4f, 0.7f);
        button.colors = colors;
    }
}
