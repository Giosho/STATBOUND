using UnityEngine;

/// <summary>Player-facing settings, saved in PlayerPrefs.</summary>
public static class GameSettings
{
    private const string MusicKey = "statbound.music";
    private const string SfxKey = "statbound.sfx";
    private const string AmbientKey = "statbound.ambient";
    private const string RadioKey = "statbound.radio";
    private const string FullscreenKey = "statbound.fullscreen";
    private const string LanguageKey = "statbound.language";

    private static bool loaded;
    private static Language language = Language.Bulgarian;
    private static float music = 0.6f;
    private static float sfx = 0.8f;
    private static float ambient = 0.5f;
    private static float radio = 0.7f;
    private static bool fullscreen = true;

    public static event System.Action Changed;

    public static float Music { get { Load(); return music; } set { Load(); Set(ref music, value, MusicKey); } }
    public static float Sfx { get { Load(); return sfx; } set { Load(); Set(ref sfx, value, SfxKey); } }
    public static float Ambient { get { Load(); return ambient; } set { Load(); Set(ref ambient, value, AmbientKey); } }
    public static float Radio { get { Load(); return radio; } set { Load(); Set(ref radio, value, RadioKey); } }

    public static Language Language
    {
        get { Load(); return language; }
        set
        {
            Load();
            if (language == value) return;

            language = value;
            PlayerPrefs.SetInt(LanguageKey, (int)value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static bool Fullscreen
    {
        get { Load(); return fullscreen; }
        set
        {
            Load();
            fullscreen = value;
            PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Screen.fullScreen = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Applies the saved display mode (builds only have an effect).</summary>
    public static void ApplyDisplay()
    {
        Load();
        if (PlayerPrefs.HasKey(FullscreenKey))
            Screen.fullScreen = fullscreen;
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;

        music = PlayerPrefs.GetFloat(MusicKey, music);
        sfx = PlayerPrefs.GetFloat(SfxKey, sfx);
        ambient = PlayerPrefs.GetFloat(AmbientKey, ambient);
        radio = PlayerPrefs.GetFloat(RadioKey, radio);
        fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        language = PlayerPrefs.GetInt(LanguageKey, (int)language) == (int)Language.English
            ? Language.English
            : Language.Bulgarian;
    }

    private static void Set(ref float field, float value, string key)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(field, value)) return;

        field = value;
        PlayerPrefs.SetFloat(key, value);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
