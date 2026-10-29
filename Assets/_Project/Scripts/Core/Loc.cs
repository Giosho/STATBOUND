using UnityEngine.UI;

public enum Language
{
    Bulgarian,
    English
}

/// <summary>Tiny two-language helper: every player-facing string is written as a (Bulgarian, English) pair.</summary>
public static class Loc
{
    public static bool English => GameSettings.Language == Language.English;

    public static string T(string bg, string en)
    {
        return English ? en : bg;
    }

    /// <summary>Keeps a label in the current language, including after the setting changes.</summary>
    public static void Bind(Text text, string bg, string en)
    {
        var bound = text.GetComponent<LocText>();
        if (bound == null) bound = text.gameObject.AddComponent<LocText>();
        bound.Set(bg, en);
    }
}
