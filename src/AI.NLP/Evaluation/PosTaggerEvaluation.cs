using AI.ClassicMath.AlgorithmAnalysis;
using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.NLP.Morphology;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.NLP.Evaluation;

/// <summary>Ошибка разметки части речи</summary>
/// <param name="Form">Словоформа</param>
/// <param name="Expected">Эталонная часть речи</param>
/// <param name="Actual">Определённая часть речи</param>
public readonly record struct PosError(string Form, PartOfSpeech Expected, PartOfSpeech Actual)
{
    /// <summary>Запись ошибки</summary>
    public override string ToString()
        => $"{Form}: ожидалось {Expected.ToCode()}, получено {Actual.ToCode()}";
}

/// <summary>
/// Результат проверки определителя части речи на эталонном корпусе
/// </summary>
/// <remarks>
/// Точность, полнота и матрица ошибок считаются
/// <see cref="MetricsForClassification"/>: разметка части речи — обычная задача
/// классификации, и заводить для неё отдельную арифметику незачем.
/// </remarks>
public sealed class PosTaggingReport : IInterpretable
{
    internal PosTaggingReport(
        string name,
        IReadOnlyList<PartOfSpeech> labels,
        int[] expected,
        int[] actual,
        IReadOnlyList<PosError> errors,
        IReadOnlyList<string> forms)
    {
        Name = name;
        Labels = labels;
        Errors = errors;
        Forms = forms;
        Total = expected.Length;

        Correct = 0;
        for (int i = 0; i < expected.Length; i++)
            if (expected[i] == actual[i]) Correct++;

        Confusion = MetricsForClassification.ConfusionMatrix(expected, actual);
        Precision = MetricsForClassification.PrecisionForEachClass(expected, actual);
        Recall = MetricsForClassification.RecallForEachClass(expected, actual);
        FMeasure = MetricsForClassification.FMeasure(expected, actual);
    }

    /// <summary>Название проверенного определителя</summary>
    public string Name { get; }

    /// <summary>Число проверенных словоформ</summary>
    public int Total { get; }

    /// <summary>Число верно размеченных словоформ</summary>
    public int Correct { get; }

    /// <summary>Доля верных ответов</summary>
    public double Accuracy => Total == 0 ? 0 : (double)Correct / Total;

    /// <summary>Части речи в порядке индексов матрицы ошибок и векторов метрик</summary>
    public IReadOnlyList<PartOfSpeech> Labels { get; }

    /// <summary>
    /// Матрица ошибок: элемент <c>[эталон, ответ]</c> — сколько раз слово эталонной
    /// части речи получило указанную метку
    /// </summary>
    public Matrix Confusion { get; }

    /// <summary>Точность по каждой части речи в порядке <see cref="Labels"/></summary>
    public Vector Precision { get; }

    /// <summary>Полнота по каждой части речи в порядке <see cref="Labels"/></summary>
    public Vector Recall { get; }

    /// <summary>F-мера (macro)</summary>
    public double FMeasure { get; }

    /// <summary>Список ошибок</summary>
    public IReadOnlyList<PosError> Errors { get; }

    /// <summary>Проверенные словоформы</summary>
    public IReadOnlyList<string> Forms { get; }

    /// <summary>Полнота по части речи: доля её слов, размеченных верно</summary>
    /// <param name="partOfSpeech">Часть речи</param>
    public double RecallFor(PartOfSpeech partOfSpeech)
    {
        int index = IndexOf(partOfSpeech);
        return index < 0 ? 0 : Recall[index];
    }

    /// <summary>Точность по части речи: доля верных среди размеченных так</summary>
    /// <param name="partOfSpeech">Часть речи</param>
    public double PrecisionFor(PartOfSpeech partOfSpeech)
    {
        int index = IndexOf(partOfSpeech);
        return index < 0 ? 0 : Precision[index];
    }

    /// <summary>Самые частые путаницы, от частой к редкой</summary>
    /// <param name="count">Сколько пар вернуть</param>
    public IReadOnlyList<(PartOfSpeech Expected, PartOfSpeech Actual, int Count)> TopConfusions(int count = 3)
    {
        var pairs = new List<(PartOfSpeech Expected, PartOfSpeech Actual, int Count)>();

        for (int i = 0; i < Labels.Count; i++)
        {
            for (int j = 0; j < Labels.Count; j++)
            {
                if (i == j) continue;

                int value = (int)Confusion[i, j];

                if (value > 0)
                    pairs.Add((Labels[i], Labels[j], value));
            }
        }

        return pairs.OrderByDescending(p => p.Count).Take(count).ToList();
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        IReadOnlyList<(PartOfSpeech Expected, PartOfSpeech Actual, int Count)> confusions = TopConfusions();

        string confusionText = confusions.Count == 0
            ? string.Empty
            : string.Join("; ", confusions.Select(c =>
                $"{c.Expected.ToCode()} → {c.Actual.ToCode()} ({c.Count})"));

        var builder = new InterpretationBuilder($"Разметка частей речи: {Name}")
            .Summary($"Верно размечено {Correct} словоформ из {Total} — {Fmt.Pct(Accuracy)}, F-мера {Fmt.Num(FMeasure, 3)}.")
            .Metric("Точность", Fmt.Pct(Accuracy), null, "доля верно размеченных словоформ",
                Accuracy >= 0.9 ? MetricQuality.Good : Accuracy >= 0.75 ? MetricQuality.Neutral : MetricQuality.Warning)
            .Metric("F-мера", Fmt.Num(FMeasure, 3), null, "среднее гармоническое точности и полноты",
                FMeasure >= 0.85 ? MetricQuality.Good : MetricQuality.Neutral)
            .Metric("Ошибок", Errors.Count, null, "словоформ размечено неверно", MetricQuality.Unknown, 0);

        for (int i = 0; i < Labels.Count; i++)
        {
            builder = builder.Metric(Labels[i].ToRussian(), Fmt.Pct(Recall[i]), null,
                $"полнота; точность {Fmt.Pct(Precision[i])}",
                Recall[i] >= 0.9 ? MetricQuality.Good : Recall[i] >= 0.6 ? MetricQuality.Neutral : MetricQuality.Warning);
        }

        return builder
            .FindingIf(confusions.Count > 0,
                $"Чаще всего путаются: {confusionText}. Это не случайный шум: у этих частей речи "
                + "совпадают окончания, и по одному слову их различить нельзя.")
            .Warning("Разметка идёт по одному слову, без соседей. Слова, часть речи которых "
                + "определяется только контекстом («стали», «печь», «мой»), размечены наугад — "
                + "и это предел метода, а не недоработка правил.")
            .Warning("Корпус собран вручную и невелик: числа годятся для сравнения версий "
                + "между собой, а не для сравнения с результатами на больших корпусах.")
            .Recommendation("Для разметки текста, а не отдельных слов, нужен корпус размеченных "
                + "предложений: только он позволит учесть соседей и снять омонимию.")
            .Build();
    }

    /// <summary>Краткая запись результата</summary>
    public override string ToString() => $"{Name}: {Correct}/{Total} ({Accuracy:P1})";

    private int IndexOf(PartOfSpeech partOfSpeech)
    {
        for (int i = 0; i < Labels.Count; i++)
            if (Labels[i] == partOfSpeech) return i;

        return -1;
    }
}

/// <summary>
/// Проверка определителя части речи на эталонном корпусе.
/// </summary>
/// <remarks>
/// Корпус лемм годится и для разметки: в нём у каждой словоформы проставлена часть речи.
/// Отдельный корпус для этого заводить не нужно — важно лишь, что коды разметки
/// в нём те же, что в <see cref="PartOfSpeechCodes"/>.
/// </remarks>
public static class PosTaggerEvaluation
{
    /// <summary>
    /// Проверяет определитель части речи на корпусе
    /// </summary>
    /// <param name="tagger">Проверяемый определитель</param>
    /// <param name="corpus">Корпус; по умолчанию встроенный русский</param>
    /// <param name="includeAmbiguous">Учитывать ли омонимичные формы</param>
    /// <param name="name">Название для отчёта</param>
    public static PosTaggingReport Evaluate(
        IPosTagger tagger,
        LemmaCorpus corpus = null,
        bool includeAmbiguous = false,
        string name = null)
    {
        if (tagger == null) throw new ArgumentNullException(nameof(tagger));

        corpus ??= LemmaCorpus.Russian;
        name ??= tagger.GetType().Name;

        var samples = corpus.Samples
            .Where(s => includeAmbiguous || s.Section != CorpusSection.Ambiguous)
            .ToList();

        // Метки нумеруются подряд: MetricsForClassification считает число классов
        // по максимальному значению метки, и разреженная нумерация раздула бы матрицу
        // строками несуществующих классов.
        List<PartOfSpeech> labels = samples
            .Select(s => PartOfSpeechCodes.Parse(s.PartOfSpeech))
            .Concat(samples.Select(s => tagger.Tag(s.Form)))
            .Distinct()
            .OrderBy(p => p.ToCode(), StringComparer.Ordinal)
            .ToList();

        var index = new Dictionary<PartOfSpeech, int>();
        for (int i = 0; i < labels.Count; i++)
            index[labels[i]] = i;

        int[] expected = new int[samples.Count];
        int[] actual = new int[samples.Count];
        var errors = new List<PosError>();
        var forms = new List<string>(samples.Count);

        for (int i = 0; i < samples.Count; i++)
        {
            PartOfSpeech gold = PartOfSpeechCodes.Parse(samples[i].PartOfSpeech);
            PartOfSpeech got = tagger.Tag(samples[i].Form);

            expected[i] = index[gold];
            actual[i] = index[got];
            forms.Add(samples[i].Form);

            if (gold != got)
                errors.Add(new PosError(samples[i].Form, gold, got));
        }

        return new PosTaggingReport(name, labels, expected, actual, errors, forms);
    }
}
