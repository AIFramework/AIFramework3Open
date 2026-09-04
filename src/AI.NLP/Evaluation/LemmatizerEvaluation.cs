using AI.Insights;
using AI.NLP.Lemmatization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.NLP.Evaluation;

/// <summary>Ошибка лемматизации</summary>
/// <param name="Form">Словоформа</param>
/// <param name="Expected">Эталонная лемма</param>
/// <param name="Actual">Полученная лемма</param>
/// <param name="PartOfSpeech">Часть речи</param>
/// <param name="Unchanged">Слово осталось без изменений: правило не нашлось</param>
public readonly record struct LemmaError(
    string Form, string Expected, string Actual, string PartOfSpeech, bool Unchanged)
{
    /// <summary>Запись ошибки</summary>
    public override string ToString()
        => $"{Form}: ожидалось «{Expected}», получено «{Actual}»" + (Unchanged ? " (правило не найдено)" : string.Empty);
}

/// <summary>
/// Результат проверки лемматизатора на эталонном корпусе
/// </summary>
public sealed class LemmatizationReport : IInterpretable
{
    internal LemmatizationReport(
        string name,
        int total,
        int correct,
        IReadOnlyDictionary<string, (int Total, int Correct)> byPartOfSpeech,
        IReadOnlyDictionary<CorpusSection, (int Total, int Correct)> bySection,
        IReadOnlyList<LemmaError> errors)
    {
        Name = name;
        Total = total;
        Correct = correct;
        ByPartOfSpeech = byPartOfSpeech;
        BySection = bySection;
        Errors = errors;
    }

    /// <summary>Название проверенного лемматизатора</summary>
    public string Name { get; }

    /// <summary>Число проверенных словоформ</summary>
    public int Total { get; }

    /// <summary>Число верно приведённых словоформ</summary>
    public int Correct { get; }

    /// <summary>Доля верных ответов</summary>
    public double Accuracy => Total == 0 ? 0 : (double)Correct / Total;

    /// <summary>Точность по частям речи</summary>
    public IReadOnlyDictionary<string, (int Total, int Correct)> ByPartOfSpeech { get; }

    /// <summary>Точность по разделам корпуса</summary>
    public IReadOnlyDictionary<CorpusSection, (int Total, int Correct)> BySection { get; }

    /// <summary>Список ошибок</summary>
    public IReadOnlyList<LemmaError> Errors { get; }

    /// <summary>
    /// Доля слов, оставшихся без изменений при ошибке: правило не нашлось,
    /// и лемматизатор вернул исходную форму
    /// </summary>
    public double UnchangedShare => Errors.Count == 0 ? 0 : (double)Errors.Count(e => e.Unchanged) / Errors.Count;

    /// <summary>Точность по части речи</summary>
    /// <param name="partOfSpeech">Часть речи</param>
    public double AccuracyFor(string partOfSpeech)
    {
        if (!ByPartOfSpeech.TryGetValue(partOfSpeech, out (int Total, int Correct) counts) || counts.Total == 0)
            return 0;

        return (double)counts.Correct / counts.Total;
    }

    /// <summary>Точность по разделу корпуса</summary>
    /// <param name="section">Раздел</param>
    public double AccuracyFor(CorpusSection section)
    {
        if (!BySection.TryGetValue(section, out (int Total, int Correct) counts) || counts.Total == 0)
            return 0;

        return (double)counts.Correct / counts.Total;
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        double baseAccuracy = AccuracyFor(CorpusSection.Base);
        double suppletive = AccuracyFor(CorpusSection.Suppletive);

        // Часть речи, не взятая ни разу при заметном числе примеров, — это не погрешность
        // правил, а их отсутствие. Такой пробел стоит назвать вслух: он не лечится
        // подкруткой существующих правил.
        List<KeyValuePair<string, (int Total, int Correct)>> untouched = ByPartOfSpeech
            .Where(e => e.Value.Correct == 0 && e.Value.Total >= 5)
            .OrderBy(e => e.Key, StringComparer.Ordinal)
            .ToList();

        string untouchedText = string.Join(", ", untouched.Select(e => $"{e.Key} (0 из {e.Value.Total})"));

        // Часть речи, отставшая от прочих вдвое, тоже говорит о пробеле в правилах,
        // а не о случайных промахах: у остальных-то получается.
        List<KeyValuePair<string, (int Total, int Correct)>> weak = ByPartOfSpeech
            .Where(e => e.Value.Total >= 10 && e.Value.Correct > 0
                        && (double)e.Value.Correct / e.Value.Total < 0.5)
            .OrderBy(e => e.Key, StringComparer.Ordinal)
            .ToList();

        string weakText = string.Join(", ", weak.Select(e => $"{e.Key} ({e.Value.Correct} из {e.Value.Total})"));

        var builder = new InterpretationBuilder($"Проверка лемматизатора: {Name}")
            .Summary($"Верно приведено {Correct} словоформ из {Total} — {Fmt.Pct(Accuracy)}. "
                + $"На регулярных формах {Fmt.Pct(baseAccuracy)}, на супплетивных {Fmt.Pct(suppletive)}.")
            .Metric("Точность", Fmt.Pct(Accuracy), null, "доля верно приведённых словоформ",
                Accuracy >= 0.9 ? MetricQuality.Good : Accuracy >= 0.75 ? MetricQuality.Neutral : MetricQuality.Warning)
            .Metric("Регулярные формы", Fmt.Pct(baseAccuracy), null, "то, что правила обязаны разбирать",
                baseAccuracy >= 0.9 ? MetricQuality.Good : MetricQuality.Warning)
            .Metric("Супплетивные формы", Fmt.Pct(suppletive), null, "то, что берётся только словарём",
                suppletive >= 0.5 ? MetricQuality.Neutral : MetricQuality.Warning)
            .Metric("Ошибок", Errors.Count, null, "словоформ приведено неверно", MetricQuality.Unknown, 0)
            .Metric("Из них без изменений", Fmt.Pct(UnchangedShare), null,
                "правило не нашлось, вернулась исходная форма");

        foreach (KeyValuePair<string, (int Total, int Correct)> entry in ByPartOfSpeech.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            double accuracy = entry.Value.Total == 0 ? 0 : (double)entry.Value.Correct / entry.Value.Total;

            builder = builder.Metric(entry.Key, Fmt.Pct(accuracy), null,
                $"{entry.Value.Correct} из {entry.Value.Total}",
                accuracy >= 0.9 ? MetricQuality.Good : accuracy >= 0.6 ? MetricQuality.Neutral : MetricQuality.Warning);
        }

        return builder
            .FindingIf(untouched.Count > 0,
                $"Части речи, не разобранные ни разу: {untouchedText}. Это отсутствие правил, "
                + "а не их погрешность: пока правил нет, точность на этих словах не сдвинется.")
            .FindingIf(weak.Count > 0,
                $"Части речи, разобранные заметно хуже прочих: {weakText}. Остальные разобраны лучше, "
                + "значит дело не в трудности языка, а в том, что правил для этих слов почти нет.")
            .FindingIf(suppletive < 1.0,
                "Невзятые супплетивные формы — прямая цена отсутствия словаря: правило по суффиксу "
                + "не выведет «человек» из «людей», сколько правил ни добавляй.")
            .FindingIf(suppletive >= 1.0 && Errors.Count > 0,
                "Супплетивные формы взяты полностью — но взяты списком исключений, а не правилами. "
                + "Это и есть словарь, только очень короткий: каждое следующее слово такого рода "
                + "придётся вписывать в него вручную.")
            .FindingIf(suppletive > baseAccuracy,
                "Супплетивные формы разобраны лучше регулярных. Так бывает, когда работу тянет "
                + "встроенный список исключений, а не правила: пробел именно в правилах.")
            .FindingIf(UnchangedShare > 0.5,
                "Больше половины ошибок — это возврат исходной формы: правило просто не нашлось. "
                + "Такие ошибки безопаснее неверного разбора: слово остаётся узнаваемым.")
            .FindingIf(UnchangedShare <= 0.5 && Errors.Count > 0,
                "Значительная часть ошибок — неверный разбор, а не отказ. Такая ошибка хуже: "
                + "слово превращается в несуществующее и перестаёт совпадать с другими формами.")
            .Warning("Корпус собран вручную и невелик: доверительный интервал точности при сотне "
                + "примеров — плюс-минус несколько процентов. Числа годятся для сравнения версий "
                + "между собой, а не для сравнения с опубликованными результатами на больших корпусах.")
            .Warning("Омонимичные формы в общую точность не входят: без контекста у них нет "
                + "единственного правильного ответа, и требовать его от лемматизатора без разметки нечестно.")
            .Recommendation("Сравнивать версии лемматизатора на одном корпусе, а не на разных примерах: "
                + "иначе улучшение неотличимо от смены выборки.")
            .Build();
    }

    /// <summary>Краткая запись результата</summary>
    public override string ToString() => $"{Name}: {Correct}/{Total} ({Accuracy:P1})";
}

/// <summary>
/// Проверка лемматизатора на эталонном корпусе.
/// </summary>
/// <remarks>
/// Сравнение ведётся после приведения к нижнему регистру и замены «ё» на «е»: лемматизатор
/// делает эту нормализацию сам, и наказывать его за неё значило бы измерять не то.
/// </remarks>
public static class LemmatizerEvaluation
{
    /// <summary>
    /// Проверяет лемматизатор на корпусе
    /// </summary>
    /// <param name="lemmatizer">Проверяемый лемматизатор</param>
    /// <param name="corpus">Корпус; по умолчанию встроенный русский</param>
    /// <param name="includeAmbiguous">Учитывать ли омонимичные формы в общей точности</param>
    /// <param name="name">Название для отчёта</param>
    public static LemmatizationReport Evaluate(
        ILemmatizer lemmatizer,
        LemmaCorpus corpus = null,
        bool includeAmbiguous = false,
        string name = null)
    {
        if (lemmatizer == null) throw new ArgumentNullException(nameof(lemmatizer));

        corpus ??= LemmaCorpus.Russian;
        name ??= lemmatizer.GetType().Name;

        var byPartOfSpeech = new Dictionary<string, (int Total, int Correct)>(StringComparer.Ordinal);
        var bySection = new Dictionary<CorpusSection, (int Total, int Correct)>();
        var errors = new List<LemmaError>();

        int total = 0;
        int correct = 0;

        foreach (LemmaSample sample in corpus.Samples)
        {
            if (!includeAmbiguous && sample.Section == CorpusSection.Ambiguous)
                continue;

            string actual = lemmatizer.Lemmatize(sample.Form);
            bool hit = Normalize(actual) == Normalize(sample.Lemma);

            total++;

            if (hit)
                correct++;
            else
                errors.Add(new LemmaError(
                    sample.Form, sample.Lemma, actual, sample.PartOfSpeech,
                    Normalize(actual) == Normalize(sample.Form)));

            Accumulate(byPartOfSpeech, sample.PartOfSpeech, hit);
            Accumulate(bySection, sample.Section, hit);
        }

        return new LemmatizationReport(name, total, correct, byPartOfSpeech, bySection, errors);
    }

    /// <summary>
    /// Сравнивает два лемматизатора на одном корпусе
    /// </summary>
    /// <param name="first">Первый лемматизатор</param>
    /// <param name="second">Второй лемматизатор</param>
    /// <param name="corpus">Корпус; по умолчанию встроенный русский</param>
    /// <returns>Разность точностей и формы, разобранные по-разному</returns>
    public static (double Difference, IReadOnlyList<string> Diverged) Compare(
        ILemmatizer first, ILemmatizer second, LemmaCorpus corpus = null)
    {
        if (first == null) throw new ArgumentNullException(nameof(first));
        if (second == null) throw new ArgumentNullException(nameof(second));

        corpus ??= LemmaCorpus.Russian;

        LemmatizationReport left = Evaluate(first, corpus);
        LemmatizationReport right = Evaluate(second, corpus);

        var diverged = new List<string>();

        foreach (LemmaSample sample in corpus.Samples)
        {
            if (Normalize(first.Lemmatize(sample.Form)) != Normalize(second.Lemmatize(sample.Form)))
                diverged.Add(sample.Form);
        }

        return (right.Accuracy - left.Accuracy, diverged);
    }

    private static void Accumulate<TKey>(Dictionary<TKey, (int Total, int Correct)> counters, TKey key, bool hit)
    {
        counters.TryGetValue(key, out (int Total, int Correct) current);
        counters[key] = (current.Total + 1, current.Correct + (hit ? 1 : 0));
    }

    private static string Normalize(string value)
        => value == null ? string.Empty : value.Trim().ToLowerInvariant().Replace('ё', 'е');
}
