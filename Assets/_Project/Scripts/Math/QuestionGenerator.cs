using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>Plain C# generator (no Unity dependencies) for mean, mode and median questions.</summary>
public sealed class QuestionGenerator
{
    private const double Eps = 1e-9;

    private readonly Random rng;
    private QuestionKind lastKind = (QuestionKind)(-1);

    public QuestionGenerator(int? seed = null)
    {
        rng = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public StatQuestion Generate(int difficulty)
    {
        difficulty = Math.Clamp(difficulty, 1, 3);

        QuestionKind kind;
        do { kind = (QuestionKind)rng.Next(3); } while (kind == lastKind);
        lastKind = kind;

        switch (kind)
        {
            case QuestionKind.Mean: return GenerateMean(difficulty);
            case QuestionKind.Mode: return GenerateMode(difficulty);
            default: return GenerateMedian(difficulty);
        }
    }

    public static string KindLabel(QuestionKind kind, bool english)
    {
        switch (kind)
        {
            case QuestionKind.Mean: return english ? "MEAN" : "СРЕДНА СТОЙНОСТ";
            case QuestionKind.Mode: return english ? "MODE" : "МОДА";
            default: return english ? "MEDIAN" : "МЕДИАНА";
        }
    }

    /// <summary>Bulgarian writes 6,5; English writes 6.5.</summary>
    public static string FormatNumber(double value, bool english)
    {
        string text = value.ToString("0.##", CultureInfo.InvariantCulture);
        return english ? text : text.Replace('.', ',');
    }

    // ---------------------------------------------------------------- mean

    private StatQuestion GenerateMean(int difficulty)
    {
        int n = difficulty == 1 ? rng.Next(3, 5) : difficulty == 2 ? rng.Next(4, 7) : rng.Next(5, 8);
        int max = difficulty == 1 ? 10 : difficulty == 2 ? 20 : 30;

        // The sum is forced to be divisible by m, so the mean is a whole number
        // (easy) or a multiple of 0.5 (harder, even-sized sets).
        int m = (difficulty == 1 || n % 2 != 0) ? n : n / 2;

        var data = new int[n];
        int sum = 0;
        for (int i = 0; i < n - 1; i++)
        {
            data[i] = rng.Next(1, max + 1);
            sum += data[i];
        }

        int last = rng.Next(1, max + 1);
        last -= (sum + last) % m;
        if (last < 1) last += m;
        data[n - 1] = last;
        sum += last;

        double mean = (double)sum / n;
        Shuffle(data);

        var sorted = (int[])data.Clone();
        Array.Sort(sorted);

        var candidates = new List<double>
        {
            MedianOf(sorted),
            mean + 1, mean - 1, mean + 2, mean - 2,
            sorted[sorted.Length - 1]
        };

        return Build(QuestionKind.Mean, difficulty, data,
            ("Намери средната стойност (средно аритметично) на числата:\n" + Join(data),
             "Find the mean (average) of the numbers:\n" + Join(data)),
            mean, candidates, 1.0,
            ("Средната стойност е сборът на всички числа, разделен на броя им.",
             "The mean is the sum of all the numbers divided by how many there are."),
            ("Сборът е " + sum + ", числата са " + n + " → " + sum + " ÷ " + n + " = " + FormatNumber(mean, false) + ".",
             "The sum is " + sum + " and there are " + n + " numbers → " + sum + " ÷ " + n + " = " + FormatNumber(mean, true) + "."));
    }

    // ---------------------------------------------------------------- mode

    private StatQuestion GenerateMode(int difficulty)
    {
        int n = difficulty == 1 ? rng.Next(5, 8) : difficulty == 2 ? rng.Next(6, 10) : rng.Next(8, 12);
        int max = difficulty == 1 ? 9 : difficulty == 2 ? 15 : 20;
        int count = difficulty == 1 ? 3 : (rng.Next(2) == 0 ? 3 : 4);

        int mode = rng.Next(1, max + 1);
        var list = new List<int>(n);
        for (int i = 0; i < count; i++) list.Add(mode);

        // Every other value appears at most count-1 times, so the mode is unique.
        var seen = new Dictionary<int, int>();
        while (list.Count < n)
        {
            int v = rng.Next(1, max + 1);
            if (v == mode) continue;
            seen.TryGetValue(v, out int c);
            if (c >= count - 1) continue;
            seen[v] = c + 1;
            list.Add(v);
        }

        var data = list.ToArray();
        Shuffle(data);

        double sum = 0, min = int.MaxValue, top = int.MinValue;
        foreach (int v in data)
        {
            sum += v;
            if (v < min) min = v;
            if (v > top) top = v;
        }

        var candidates = new List<double>();
        foreach (var kv in seen) candidates.Add(kv.Key);
        candidates.Add(Math.Round(sum / n));
        candidates.Add(top);
        candidates.Add(min);

        return Build(QuestionKind.Mode, difficulty, data,
            ("Намери модата на числата:\n" + Join(data),
             "Find the mode of the numbers:\n" + Join(data)),
            mode, candidates, 1.0,
            ("Модата е числото, което се среща най-често в редицата.",
             "The mode is the number that appears most often."),
            ("Числото " + mode + " се среща " + count + " пъти — повече от всяко друго.",
             "The number " + mode + " appears " + count + " times — more than any other."));
    }

    // -------------------------------------------------------------- median

    private StatQuestion GenerateMedian(int difficulty)
    {
        bool even = difficulty >= 2 && rng.Next(2) == 0;
        int n = even ? 2 * rng.Next(difficulty, difficulty + 2) : 2 * rng.Next(difficulty, difficulty + 2) + 1;
        int max = difficulty == 1 ? 15 : difficulty == 2 ? 30 : 50;

        var data = new int[n];
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            data[i] = rng.Next(1, max + 1);
            sum += data[i];
        }

        var sorted = (int[])data.Clone();
        Array.Sort(sorted);
        double median = MedianOf(sorted);

        var candidates = new List<double>
        {
            data[n / 2],                          // middle of the UNSORTED list
            Math.Round(sum / n * 2) / 2,          // mean mistaken for median
            sorted[n / 2 - 1],
            sorted[n / 2],
            sorted[Math.Min(n - 1, n / 2 + 1)]
        };

        string explanationBg, explanationEn;
        if (even)
        {
            int a = sorted[n / 2 - 1], b = sorted[n / 2];
            explanationBg = "Подредени: " + Join(sorted) + ". Средни са " + a + " и " + b +
                            " → (" + a + " + " + b + ") ÷ 2 = " + FormatNumber(median, false) + ".";
            explanationEn = "Sorted: " + Join(sorted) + ". The middle numbers are " + a + " and " + b +
                            " → (" + a + " + " + b + ") ÷ 2 = " + FormatNumber(median, true) + ".";
        }
        else
        {
            explanationBg = "Подредени: " + Join(sorted) + ". Средното число е " + FormatNumber(median, false) + ".";
            explanationEn = "Sorted: " + Join(sorted) + ". The middle number is " + FormatNumber(median, true) + ".";
        }

        return Build(QuestionKind.Median, difficulty, data,
            ("Намери медианата на числата:\n" + Join(data),
             "Find the median of the numbers:\n" + Join(data)),
            median, candidates, even ? 0.5 : 1.0,
            ("Първо подреди числата по големина. Медианата е в средата; при четен брой числа се взема средното аритметично на двете средни.",
             "First sort the numbers in order. The median is the middle one; with an even amount of numbers, take the average of the two middle ones."),
            (explanationBg, explanationEn));
    }

    // ------------------------------------------------------------- helpers

    private StatQuestion Build(QuestionKind kind, int difficulty, int[] data, (string Bg, string En) prompt,
        double correct, List<double> candidates, double fillStep, (string Bg, string En) hint,
        (string Bg, string En) explanation)
    {
        var wrong = new List<double>(3);

        Shuffle(candidates);
        foreach (double c in candidates)
        {
            if (wrong.Count >= 3) break;
            TryAdd(wrong, c, correct);
        }

        for (int k = 1; wrong.Count < 3; k++)
        {
            TryAdd(wrong, correct + k * fillStep, correct);
            if (wrong.Count < 3) TryAdd(wrong, correct - k * fillStep, correct);
        }

        var all = new List<double>(wrong) { correct };
        Shuffle(all);

        int correctIndex = 0;
        for (int i = 0; i < 4; i++)
            if (Math.Abs(all[i] - correct) < Eps) correctIndex = i;

        return new StatQuestion
        {
            Kind = kind,
            Difficulty = difficulty,
            Data = data,
            Values = all.ToArray(),
            CorrectIndex = correctIndex,
            PromptBg = prompt.Bg,
            PromptEn = prompt.En,
            HintBg = hint.Bg,
            HintEn = hint.En,
            ExplanationBg = explanation.Bg,
            ExplanationEn = explanation.En
        };
    }

    private static bool TryAdd(List<double> wrong, double value, double correct)
    {
        if (value < 0 || Math.Abs(value - correct) < Eps) return false;
        foreach (double w in wrong)
            if (Math.Abs(w - value) < Eps) return false;

        wrong.Add(value);
        return true;
    }

    private static double MedianOf(int[] sorted)
    {
        int n = sorted.Length;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
    }

    private static string Join(int[] values)
    {
        return string.Join(", ", values);
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
