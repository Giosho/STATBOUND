public enum QuestionKind
{
    Mean,
    Mode,
    Median
}

/// <summary>
/// A generated question with four options and exactly one correct answer.
/// Texts are stored in both languages so the player can switch language mid-question.
/// </summary>
public sealed class StatQuestion
{
    public QuestionKind Kind;
    public int Difficulty;
    public int[] Data;
    public double[] Values;
    public int CorrectIndex;
    public string PromptBg, PromptEn;
    public string HintBg, HintEn;
    public string ExplanationBg, ExplanationEn;

    public string Prompt => Loc.T(PromptBg, PromptEn);
    public string Hint => Loc.T(HintBg, HintEn);
    public string Explanation => Loc.T(ExplanationBg, ExplanationEn);
    public int OptionCount => Values.Length;

    public string OptionText(int index)
    {
        return QuestionGenerator.FormatNumber(Values[index], Loc.English);
    }
}
