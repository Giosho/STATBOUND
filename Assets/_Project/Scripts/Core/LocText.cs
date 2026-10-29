using UnityEngine;
using UnityEngine.UI;

/// <summary>Added by Loc.Bind: re-applies the label's text whenever the language changes.</summary>
[RequireComponent(typeof(Text))]
public class LocText : MonoBehaviour
{
    private Text text;
    private string bg, en;

    public void Set(string bgText, string enText)
    {
        bg = bgText;
        en = enText;
        Apply();
    }

    private void OnEnable()
    {
        GameSettings.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= Apply;
    }

    private void Apply()
    {
        if (bg == null) return;
        if (text == null) text = GetComponent<Text>();
        text.text = Loc.T(bg, en);
    }
}
