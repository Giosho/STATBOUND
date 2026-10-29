using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public enum RadioState
{
    Stopped,
    Loading,
    Playing,
    Paused
}

public sealed class RadioTrack
{
    public string Name;
    public string FilePath;
    public AudioType Type;
    public bool Failed;
}

/// <summary>
/// The player's own music. Plays local files from the "Radio" folder next to the game
/// (project root in the editor). Persistent across scenes, with its own AudioSource and volume.
/// </summary>
public class RadioController : MonoBehaviour
{
    public const string SupportedFormats = "MP3, WAV, OGG, AIF/AIFF";
    public const float StartWithRadioChance = 0.7f;

    private static readonly string[] KnownUnsupported = { ".flac", ".m4a", ".aac", ".wma", ".opus", ".mp4", ".wv" };

    public static RadioController Instance { get; private set; }

    private AudioSource source;
    private Coroutine loadRoutine;
    private readonly List<RadioTrack> tracks = new List<RadioTrack>();
    private int startFrame;
    private int failStreak;

    public IReadOnlyList<RadioTrack> Tracks => tracks;
    public int CurrentIndex { get; private set; } = -1;
    public RadioState State { get; private set; } = RadioState.Stopped;
    public bool Muted { get; private set; }
    public bool Owned { get; private set; }
    public string FolderPath { get; private set; }
    public int SkippedFiles { get; private set; }
    public string Message { get; private set; } = "";

    public float Position => source != null && source.clip != null ? source.time : 0f;
    public float Duration => source != null && source.clip != null ? source.clip.length : 0f;

    public event System.Action<bool> OwnedChanged;
    public event System.Action LibraryChanged;
    public event System.Action StateChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        new GameObject("Radio").AddComponent<RadioController>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;

        Refresh();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        source.volume = Muted ? 0f : GameSettings.Radio;

        // Track finished on its own: go to the next one.
        if (State == RadioState.Playing && source.clip != null && !source.isPlaying
            && Application.isFocused && Time.frameCount - startFrame > 3)
        {
            Play(CurrentIndex + 1, true);
        }
    }

    // ------------------------------------------------------ ownership

    public void RollForNewRun()
    {
        Owned = Random.value < StartWithRadioChance;
        OwnedChanged?.Invoke(Owned);
    }

    public void SetOwned(bool owned)
    {
        Owned = owned;
        OwnedChanged?.Invoke(Owned);
    }

    // -------------------------------------------------------- library

    public void Refresh()
    {
        string currentPath = CurrentIndex >= 0 && CurrentIndex < tracks.Count ? tracks[CurrentIndex].FilePath : null;
        tracks.Clear();
        SkippedFiles = 0;

        try
        {
            EnsureFolder();

            var files = Directory.GetFiles(FolderPath);
            System.Array.Sort(files, System.StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                AudioType type = TypeFor(ext);

                if (type != AudioType.UNKNOWN)
                    tracks.Add(new RadioTrack { Name = Path.GetFileNameWithoutExtension(file), FilePath = file, Type = type });
                else if (System.Array.IndexOf(KnownUnsupported, ext) >= 0)
                    SkippedFiles++;
            }
        }
        catch (System.Exception e)
        {
            Message = Loc.T("Не мога да прочета папката Radio: ", "Can't read the Radio folder: ") + e.Message;
        }

        CurrentIndex = -1;
        if (currentPath != null)
            CurrentIndex = tracks.FindIndex(t => t.FilePath == currentPath);

        if (CurrentIndex < 0 && State != RadioState.Stopped)
            Stop();

        LibraryChanged?.Invoke();
    }

    private void EnsureFolder()
    {
        if (FolderPath == null)
        {
            string primary = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Radio"));
            try
            {
                Directory.CreateDirectory(primary);
                FolderPath = primary;
            }
            catch (System.Exception)
            {
                FolderPath = Path.Combine(Application.persistentDataPath, "Radio");
                Directory.CreateDirectory(FolderPath);
            }
        }

        Directory.CreateDirectory(FolderPath);

        string readme = Path.Combine(FolderPath, "ПРОЧЕТИ_МЕ.txt");
        if (!File.Exists(readme))
        {
            try
            {
                File.WriteAllText(readme,
                    "РАДИО НА STATBOUND\r\n" +
                    "===================\r\n\r\n" +
                    "Сложи в тази папка твоя музика (само файлове, които имаш право да ползваш).\r\n" +
                    "В играта отвори Радио (меню или клавиш M) и натисни \"Обнови списъка\".\r\n\r\n" +
                    "Поддържани формати: " + SupportedFormats + "\r\n" +
                    "Неподдържани: FLAC, M4A, AAC, WMA, OPUS - конвертирай ги в MP3 или WAV.\r\n");
            }
            catch (System.Exception) { /* read-only location: not important */ }
        }
    }

    private static AudioType TypeFor(string ext)
    {
        switch (ext)
        {
            case ".mp3": return AudioType.MPEG;
            case ".wav": return AudioType.WAV;
            case ".ogg": return AudioType.OGGVORBIS;
            case ".aif":
            case ".aiff": return AudioType.AIFF;
            default: return AudioType.UNKNOWN;
        }
    }

    // ------------------------------------------------------ transport

    public void Play(int index, bool autoSkipOnError = false)
    {
        if (tracks.Count == 0)
        {
            Message = Loc.T("В папката Radio няма песни. Формати: ", "The Radio folder has no songs. Formats: ") +
                      SupportedFormats + ".";
            StateChanged?.Invoke();
            return;
        }

        index = ((index % tracks.Count) + tracks.Count) % tracks.Count;
        CurrentIndex = index;

        if (loadRoutine != null) StopCoroutine(loadRoutine);
        source.Stop();
        State = RadioState.Loading;
        Message = "";
        StateChanged?.Invoke();

        loadRoutine = StartCoroutine(LoadAndPlay(tracks[index], autoSkipOnError));
    }

    private IEnumerator LoadAndPlay(RadioTrack track, bool autoSkipOnError)
    {
        string uri = new System.Uri(track.FilePath).AbsoluteUri;

        using (var request = UnityWebRequestMultimedia.GetAudioClip(uri, track.Type))
        {
            yield return request.SendWebRequest();

            AudioClip clip = null;
            string error = null;

            if (request.result != UnityWebRequest.Result.Success)
            {
                error = request.error;
            }
            else
            {
                clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null || clip.length < 0.05f) error = Loc.T("празен или повреден файл", "empty or corrupt file");
            }

            if (error != null)
            {
                Fail(track, error, autoSkipOnError);
                yield break;
            }

            clip.name = track.Name;
            var old = source.clip;
            source.clip = clip;
            source.Play();
            if (old != null) Destroy(old);

            failStreak = 0;
            startFrame = Time.frameCount;
            State = RadioState.Playing;
            Message = "";
            StateChanged?.Invoke();
        }

        loadRoutine = null;
    }

    private void Fail(RadioTrack track, string reason, bool autoSkip)
    {
        track.Failed = true;
        failStreak++;
        State = RadioState.Stopped;
        Message = Loc.T("Не мога да пусна \"", "Can't play \"") + track.Name + "\" (" + reason +
                  Loc.T("). Поддържани формати: ", "). Supported formats: ") + SupportedFormats + ".";
        loadRoutine = null;
        StateChanged?.Invoke();
        LibraryChanged?.Invoke();

        // Keep going through the playlist, but never loop forever if every file is broken.
        if (autoSkip && failStreak < tracks.Count)
            Play(CurrentIndex + 1, true);
    }

    public void Pause()
    {
        if (State != RadioState.Playing) return;
        source.Pause();
        State = RadioState.Paused;
        StateChanged?.Invoke();
    }

    public void Resume()
    {
        if (State != RadioState.Paused) return;
        source.UnPause();
        startFrame = Time.frameCount;
        State = RadioState.Playing;
        StateChanged?.Invoke();
    }

    public void TogglePlayPause()
    {
        switch (State)
        {
            case RadioState.Playing: Pause(); break;
            case RadioState.Paused: Resume(); break;
            case RadioState.Stopped: Play(CurrentIndex >= 0 ? CurrentIndex : 0); break;
        }
    }

    public void Stop()
    {
        if (loadRoutine != null) { StopCoroutine(loadRoutine); loadRoutine = null; }
        source.Stop();
        State = RadioState.Stopped;
        StateChanged?.Invoke();
    }

    public void Next()
    {
        failStreak = 0;
        Play(CurrentIndex < 0 ? 0 : CurrentIndex + 1);
    }

    public void Previous()
    {
        failStreak = 0;
        Play(CurrentIndex < 0 ? 0 : CurrentIndex - 1);
    }

    public void ToggleMute()
    {
        Muted = !Muted;
        StateChanged?.Invoke();
    }
}
