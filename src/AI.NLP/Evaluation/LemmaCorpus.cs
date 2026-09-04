using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AI.NLP.Evaluation;

/// <summary>Раздел эталонного корпуса</summary>
public enum CorpusSection
{
    /// <summary>Регулярные формы, разбираемые по суффиксу</summary>
    Base,

    /// <summary>Супплетивные и нерегулярные формы: правилами не берутся, нужен словарь</summary>
    Suppletive,

    /// <summary>Омонимия: без контекста ответ не единственный</summary>
    Ambiguous
}

/// <summary>
/// Запись эталонного корпуса: словоформа и её лемма
/// </summary>
/// <param name="Form">Словоформа</param>
/// <param name="Lemma">Эталонная лемма</param>
/// <param name="PartOfSpeech">Часть речи</param>
/// <param name="Section">Раздел корпуса</param>
/// <param name="Note">Пояснение, если запись требует оговорки</param>
public readonly record struct LemmaSample(
    string Form, string Lemma, string PartOfSpeech, CorpusSection Section, string Note = "")
{
    /// <summary>Запись словоформы и леммы</summary>
    public override string ToString() => $"{Form} → {Lemma} ({PartOfSpeech})";
}

/// <summary>
/// Эталонный корпус лемматизации.
/// </summary>
/// <remarks>
/// <para>
/// Корпус нужен затем, что без него о качестве лемматизатора можно судить только на глаз.
/// Сто разобранных вручную примеров говорят о нём больше, чем любое рассуждение о правилах.
/// </para>
/// <para>
/// Соглашения о лемме приняты по образцу Universal Dependencies: у существительного —
/// именительный падеж единственного числа, у прилагательного — мужской род, у глагола —
/// инфинитив, причём причастия и деепричастия относятся к глаголу, от которого образованы.
/// </para>
/// <para>
/// Корпус разделён на три части. Регулярные формы проверяют суффиксальные правила.
/// Супплетивные («людей» → «человек», «шёл» → «идти») правилами не берутся принципиально:
/// они показывают, сколько стоит отсутствие словаря. Омонимичные формы («стали», «печь»)
/// вынесены отдельно и в общую точность не входят: у них без контекста нет единственного
/// правильного ответа, и требовать его от лемматизатора нечестно.
/// </para>
/// </remarks>
public sealed class LemmaCorpus
{
    private readonly List<LemmaSample> _samples;

    /// <summary>Создаёт корпус из набора записей</summary>
    /// <param name="samples">Записи</param>
    public LemmaCorpus(IEnumerable<LemmaSample> samples)
    {
        if (samples == null) throw new ArgumentNullException(nameof(samples));

        _samples = samples.ToList();
    }

    /// <summary>Записи корпуса</summary>
    public IReadOnlyList<LemmaSample> Samples => _samples;

    /// <summary>Число записей</summary>
    public int Count => _samples.Count;

    /// <summary>Части речи, представленные в корпусе</summary>
    public IReadOnlyList<string> PartsOfSpeech
        => _samples.Select(s => s.PartOfSpeech).Distinct().OrderBy(p => p, StringComparer.Ordinal).ToList();

    /// <summary>Записи заданного раздела</summary>
    /// <param name="section">Раздел</param>
    public LemmaCorpus Section(CorpusSection section)
        => new(_samples.Where(s => s.Section == section));

    /// <summary>Записи заданной части речи</summary>
    /// <param name="partOfSpeech">Часть речи</param>
    public LemmaCorpus ForPartOfSpeech(string partOfSpeech)
        => new(_samples.Where(s => s.PartOfSpeech == partOfSpeech));

    /// <summary>
    /// Встроенный эталонный корпус русского языка
    /// </summary>
    /// <remarks>
    /// Собран вручную; охватывает склонение существительных и прилагательных, спряжение
    /// глаголов, возвратные формы, причастия, местоимения, числительные и служебные слова.
    /// </remarks>
    public static LemmaCorpus Russian { get; } = LoadEmbedded("lemmas-ru.tsv");

    /// <summary>
    /// Читает корпус из текста в формате «словоформа, лемма, часть речи, раздел»,
    /// разделённых знаком табуляции
    /// </summary>
    /// <param name="text">Текст корпуса</param>
    public static LemmaCorpus Parse(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));

        var samples = new List<LemmaSample>();

        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();

            if (line.Length == 0 || line[0] == '#')
                continue;

            string[] parts = line.Split('\t');

            if (parts.Length < 4)
                continue;

            string note = string.Empty;

            if (parts.Length > 4)
            {
                note = parts[4].Trim();

                if (note.StartsWith("#", StringComparison.Ordinal))
                    note = note.Substring(1).Trim();
            }

            samples.Add(new LemmaSample(
                parts[0].Trim(),
                parts[1].Trim(),
                parts[2].Trim(),
                ParseSection(parts[3].Trim()),
                note));
        }

        return new LemmaCorpus(samples);
    }

    /// <summary>Читает корпус из файла</summary>
    /// <param name="path">Путь к файлу</param>
    public static LemmaCorpus Load(string path) => Parse(File.ReadAllText(path));

    private static CorpusSection ParseSection(string value) => value switch
    {
        "suppletive" => CorpusSection.Suppletive,
        "ambiguous" => CorpusSection.Ambiguous,
        _ => CorpusSection.Base
    };

    /// <summary>
    /// Читает встроенный корпус по окончанию имени ресурса.
    /// </summary>
    /// <remarks>
    /// Поиск идёт по суффиксу, а не по полному имени: полное имя ресурса складывается
    /// из корневого пространства имён сборки, а оно у этого проекта не совпадает с именем
    /// сборки и может измениться. Привязка к суффиксу переживает такие правки.
    /// </remarks>
    /// <remarks>
    /// Имя файла намеренно без точки перед языковым кодом: «lemmas.ru.tsv» MSBuild
    /// принимает за ресурс культуры ru и выносит в сателлитную сборку, где основной
    /// сборке он уже не виден.
    /// </remarks>
    private static LemmaCorpus LoadEmbedded(string fileName)
    {
        Assembly assembly = typeof(LemmaCorpus).Assembly;

        string resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Встроенный корпус «{fileName}» не найден в сборке. "
                + $"Доступны: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);

        return Parse(reader.ReadToEnd());
    }
}
