using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Music player UI: play, pause, stop, next/previous, mute, volume and a clickable track list.</summary>
public class RadioPanel : MonoBehaviour
{
    private RadioController radio;
    private System.Action onClosed;

    private Text nowPlaying, status, playLabel, muteLabel, info, volumePercent;
    private RectTransform progressFill, listContent;
    private ScrollRect scroll;
    private Slider volume;
    private Button playButton;
    private readonly List<Button> trackButtons = new List<Button>();
    private readonly List<Text> trackLabels = new List<Text>();
    private GameObject lastSelected;
    private string lastInfo;

    public bool IsOpen => gameObject.activeSelf;

    public static RadioPanel Create(Transform parent, System.Action onClosed)
    {
        var go = new GameObject("RadioPanel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        UIFactory.Stretch((RectTransform)go.transform);

        var panel = go.AddComponent<RadioPanel>();
        panel.Build(onClosed);
        go.SetActive(false);
        return panel;
    }

    private void Build(System.Action closedCallback)
    {
        onClosed = closedCallback;
        radio = RadioController.Instance;

        UIFactory.MakeOverlay(transform, "Dim");
        var panel = UIFactory.MakePanel(transform, "Panel", 880f, 680f);
        UIFactory.VLayout(panel, 26, 10);

        var title = UIFactory.MakeLabel(panel, "", 42, TextAnchor.MiddleCenter, UIFactory.Gold, 52f);
        Loc.Bind(title, "РАДИО", "RADIO");
        nowPlaying = UIFactory.MakeLabel(panel, "", 28, TextAnchor.MiddleCenter, UIFactory.TextMain, 38f);
        nowPlaying.horizontalOverflow = HorizontalWrapMode.Overflow;
        status = UIFactory.MakeLabel(panel, "", 20, TextAnchor.MiddleCenter, UIFactory.TextMuted, 28f);

        // Progress bar.
        var bar = UIFactory.Rect("Progress", panel);
        UIFactory.Size(bar.gameObject, 10f);
        bar.gameObject.AddComponent<Image>().color = new Color32(0x1E, 0x16, 0x30, 0xFF);
        progressFill = UIFactory.Rect("Fill", bar);
        progressFill.anchorMin = Vector2.zero;
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
        progressFill.gameObject.AddComponent<Image>().color = UIFactory.Gold;

        // Transport buttons.
        var controls = UIFactory.Rect("Controls", panel);
        UIFactory.HLayout(controls, 10f);
        UIFactory.Size(controls.gameObject, 54f);
        UIFactory.MakeButton(controls, "", 22, () => radio.Previous(), out Text previousLabel, 54f);
        Loc.Bind(previousLabel, "Назад", "Previous");
        playButton = UIFactory.MakeButton(controls, "", 24, () => radio.TogglePlayPause(), out playLabel, 54f);
        UIFactory.MakeButton(controls, "", 22, () => radio.Stop(), out Text stopLabel, 54f);
        Loc.Bind(stopLabel, "Стоп", "Stop");
        UIFactory.MakeButton(controls, "", 22, () => radio.Next(), out Text nextLabel, 54f);
        Loc.Bind(nextLabel, "Напред", "Next");
        UIFactory.MakeButton(controls, "", 22, () => radio.ToggleMute(), out muteLabel, 54f);

        // Volume.
        var volumeRow = UIFactory.Rect("VolumeRow", panel);
        UIFactory.HLayout(volumeRow, 14f);
        UIFactory.Size(volumeRow.gameObject, 36f);
        var volumeName = UIFactory.MakeLabel(volumeRow, "", 24, TextAnchor.MiddleLeft, UIFactory.TextMain);
        Loc.Bind(volumeName, "Сила", "Volume");
        UIFactory.Size(volumeName.gameObject, 0f, 110f);
        volumePercent = UIFactory.MakeLabel(volumeRow, "", 22, TextAnchor.MiddleRight, UIFactory.Gold);
        UIFactory.Size(volumePercent.gameObject, 0f, 70f);
        volume = UIFactory.MakeSlider(volumeRow, GameSettings.Radio, v =>
        {
            GameSettings.Radio = v;
            volumePercent.text = Mathf.RoundToInt(v * 100f) + "%";
        });
        volume.transform.SetSiblingIndex(1);

        BuildTrackList(panel);

        info = UIFactory.MakeLabel(panel, "", 16, TextAnchor.UpperLeft, UIFactory.TextMuted, 60f);

        // Bottom buttons.
        var bottom = UIFactory.Rect("Bottom", panel);
        UIFactory.HLayout(bottom, 10f);
        UIFactory.Size(bottom.gameObject, 50f);
        UIFactory.MakeButton(bottom, "", 22, OpenFolder, out Text folderLabel, 50f);
        Loc.Bind(folderLabel, "Отвори папката", "Open folder");
        UIFactory.MakeButton(bottom, "", 22, () => radio.Refresh(), out Text refreshLabel, 50f);
        Loc.Bind(refreshLabel, "Обнови списъка", "Refresh list");
        UIFactory.MakeButton(bottom, "", 22, Hide, out Text closeLabel, 50f);
        Loc.Bind(closeLabel, "Затвори", "Close");

        radio.LibraryChanged += OnLibraryChanged;
    }

    private void BuildTrackList(RectTransform panel)
    {
        var view = UIFactory.Rect("TrackList", panel);
        var element = UIFactory.Size(view.gameObject, 190f);
        element.flexibleHeight = 1f;
        view.gameObject.AddComponent<Image>().color = new Color32(0x12, 0x0E, 0x1C, 0xFF);

        var viewport = UIFactory.Rect("Viewport", view);
        UIFactory.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        listContent = UIFactory.Rect("Content", viewport);
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.offsetMin = listContent.offsetMax = Vector2.zero;

        var layout = UIFactory.VLayout(listContent, 6, 4);
        layout.childAlignment = TextAnchor.UpperCenter;
        var fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
    }

    private void OnDestroy()
    {
        if (radio != null) radio.LibraryChanged -= OnLibraryChanged;
    }

    private void OnLibraryChanged()
    {
        if (IsOpen) RebuildList();
    }

    private void RebuildList()
    {
        foreach (Transform child in listContent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        trackButtons.Clear();
        trackLabels.Clear();

        if (radio.Tracks.Count == 0)
        {
            var empty = UIFactory.MakeLabel(listContent,
                Loc.T("Няма песни. Сложи MP3, WAV или OGG файлове в папката Radio и натисни \"Обнови списъка\".",
                    "No songs. Put MP3, WAV or OGG files in the Radio folder and press \"Refresh list\"."),
                20, TextAnchor.MiddleCenter, UIFactory.TextMuted, 110f);
            empty.horizontalOverflow = HorizontalWrapMode.Wrap;
            return;
        }

        for (int i = 0; i < radio.Tracks.Count; i++)
        {
            int index = i;
            var track = radio.Tracks[i];
            string title = (i + 1) + ".  " + track.Name + (track.Failed ? Loc.T("   (грешка)", "   (error)") : "");

            var button = UIFactory.MakeButton(listContent, title, 20, () => radio.Play(index), out Text label, 36f);
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.rectTransform.offsetMin = new Vector2(14f, 0f);

            trackButtons.Add(button);
            trackLabels.Add(label);
        }
    }

    public void Show()
    {
        radio.Refresh();
        volume.SetValueWithoutNotify(GameSettings.Radio);
        volumePercent.text = Mathf.RoundToInt(GameSettings.Radio * 100f) + "%";

        gameObject.SetActive(true);
        RebuildList();
        lastInfo = null;
        UIFactory.Select(playButton.gameObject);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        onClosed?.Invoke();
    }

    private void OpenFolder()
    {
        try
        {
            Application.OpenURL(new System.Uri(radio.FolderPath).AbsoluteUri);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not open the Radio folder: " + e.Message);
        }
    }

    private void Update()
    {
        if (UIFactory.BackPressed())
        {
            UIFactory.BackHandledFrame = Time.frameCount;
            Hide();
            return;
        }

        RefreshLive();
        ScrollToSelection();
    }

    private void RefreshLive()
    {
        bool hasTrack = radio.CurrentIndex >= 0 && radio.CurrentIndex < radio.Tracks.Count;
        nowPlaying.text = hasTrack ? radio.Tracks[radio.CurrentIndex].Name
            : Loc.T("— нищо не се пуска —", "— nothing is playing —");

        string time = Format(radio.Position) + " / " + Format(radio.Duration);
        switch (radio.State)
        {
            case RadioState.Playing: status.text = Loc.T("Пуска се", "Playing") + "   •   " + time; break;
            case RadioState.Paused: status.text = Loc.T("Пауза", "Paused") + "   •   " + time; break;
            case RadioState.Loading: status.text = Loc.T("Зареждане…", "Loading…"); break;
            default: status.text = Loc.T("Спряно", "Stopped"); break;
        }
        if (radio.Muted) status.text += Loc.T("   •   заглушено", "   •   muted");

        float progress = radio.Duration > 0.01f ? Mathf.Clamp01(radio.Position / radio.Duration) : 0f;
        progressFill.anchorMax = new Vector2(progress, 1f);

        playLabel.text = radio.State == RadioState.Playing ? Loc.T("Пауза", "Pause")
            : radio.State == RadioState.Paused ? Loc.T("Продължи", "Resume")
            : radio.State == RadioState.Loading ? "…" : Loc.T("Пусни", "Play");
        muteLabel.text = radio.Muted ? Loc.T("Включи звука", "Unmute") : Loc.T("Заглуши", "Mute");

        for (int i = 0; i < trackLabels.Count; i++)
        {
            bool current = i == radio.CurrentIndex && radio.State != RadioState.Stopped;
            trackLabels[i].color = current ? UIFactory.Gold : UIFactory.TextMain;
        }

        string text = Loc.T("Папка: ", "Folder: ") + radio.FolderPath +
                      Loc.T("\nФормати: ", "\nFormats: ") + RadioController.SupportedFormats;
        if (radio.SkippedFiles > 0)
            text += Loc.T("   •   пропуснати неподдържани файлове: " + radio.SkippedFiles + " (конвертирай ги в MP3/WAV)",
                "   •   skipped unsupported files: " + radio.SkippedFiles + " (convert them to MP3/WAV)");
        if (!string.IsNullOrEmpty(radio.Message))
            text += "\n" + radio.Message;

        if (text != lastInfo)
        {
            lastInfo = text;
            info.text = text;
            info.color = string.IsNullOrEmpty(radio.Message) ? UIFactory.TextMuted : UIFactory.Bad;
        }
    }

    private void ScrollToSelection()
    {
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected == lastSelected) return;
        lastSelected = selected;

        for (int i = 0; i < trackButtons.Count; i++)
        {
            if (trackButtons[i] == null || trackButtons[i].gameObject != selected) continue;
            scroll.verticalNormalizedPosition = trackButtons.Count > 1 ? 1f - i / (float)(trackButtons.Count - 1) : 1f;
            return;
        }
    }

    private static string Format(float seconds)
    {
        int s = Mathf.Max(0, (int)seconds);
        return (s / 60) + ":" + (s % 60).ToString("00");
    }
}
