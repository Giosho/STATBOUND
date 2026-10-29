using System;
using System.Collections.Generic;
using UnityEngine;

public class QuestionManager : MonoBehaviour
{
    [SerializeField] private int hintsPerRun = 3;

    private QuestionGenerator generator;
    private readonly HashSet<int> wrongChoices = new HashSet<int>();
    private bool hintShown;

    public StatQuestion Current { get; private set; }
    public bool IsSolved { get; private set; }
    public int HintsRemaining { get; private set; }

    public event Action<StatQuestion> QuestionAsked;
    /// <summary>Arguments: correct?, chosen index, question.</summary>
    public event Action<bool, int, StatQuestion> AnswerSubmitted;
    public event Action HintsChanged;

    private void Awake()
    {
        generator = new QuestionGenerator();
        HintsRemaining = hintsPerRun;
    }

    public void AskNewQuestion(int difficulty)
    {
        Current = generator.Generate(difficulty);
        IsSolved = false;
        hintShown = false;
        wrongChoices.Clear();
        QuestionAsked?.Invoke(Current);
    }

    /// <summary>Returns true if the answer was accepted as correct.</summary>
    public bool SubmitAnswer(int index)
    {
        if (Current == null || IsSolved) return false;
        if (index < 0 || index >= Current.OptionCount) return false;
        if (wrongChoices.Contains(index)) return false;

        bool correct = index == Current.CorrectIndex;
        if (correct) IsSolved = true;
        else wrongChoices.Add(index);

        AnswerSubmitted?.Invoke(correct, index, Current);
        return correct;
    }

    /// <summary>Re-reading the hint of the same question is free.</summary>
    public bool TryUseHint(out string hint)
    {
        hint = null;
        if (Current == null || IsSolved) return false;

        if (!hintShown)
        {
            if (HintsRemaining <= 0) return false;
            HintsRemaining--;
            hintShown = true;
            HintsChanged?.Invoke();
        }

        hint = Current.Hint;
        return true;
    }
}
