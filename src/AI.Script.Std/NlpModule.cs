using AI.DataStructs.Algebraic;
using AI.Insights;
using AI.NLP;
using AI.NLP.Evaluation;
using AI.NLP.Lemmatization;
using AI.NLP.Morphology;
using AI.NLP.Stemmers;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>nlp</c>: обработка текста.
/// </summary>
/// <remarks>
/// Поисковые модели (TF-IDF, BM25) строятся по корпусу и становятся дескрипторами: индекс
/// считается один раз, а запросов к нему бывает много. Всё остальное — чистые функции.
/// </remarks>
[ScriptModule("nlp", "Текст: нормализация, стемминг, морфология, TF-IDF, BM25", Version = "0.2")]
public static class NlpModule
{
    /// <summary>Тип-тег дескриптора поискового индекса.</summary>
    public const string IndexHandle = "nlp.index";

    private static readonly char[] s_separators = [' ', '\n', '\r', '\t'];

    private static readonly Lazy<ILemmatizer> s_lemmatizer = new(() => Lemmatizer.CreateRussian());

    private static readonly Lazy<(LemmatizationReport Lemmas, PosTaggingReport Tags)> s_morphQuality =
        new(() => (
            LemmatizerEvaluation.Evaluate(MorphologicalLemmatizer.Instance),
            PosTaggerEvaluation.Evaluate(RussianPosTagger.Instance)));

    private static readonly Lazy<HashSet<string>> s_stopWords =
        new(() => new HashSet<string>(RussianStopWords.Default, StringComparer.OrdinalIgnoreCase));

    [ScriptFn("normalize", "Приводит текст к единому виду: регистр, пробелы, знаки",
        Example = "nlp.normalize(\"  Привет,  МИР \")")]
    public static string Normalize(
        [ScriptParam("текст")] string text,
        [ScriptParam("приводить к нижнему регистру")] bool lower = true)
        => TextStandard.Normalize(text, lower);

    [ScriptFn("letters_only", "Оставляет только буквы", Example = "nlp.letters_only(text)")]
    public static string LettersOnly(
        [ScriptParam("текст")] string text,
        [ScriptParam("приводить к нижнему регистру")] bool lower = true)
        => TextStandard.OnlyChars(text, lower);

    [ScriptFn("letters_digits", "Оставляет буквы и цифры", Example = "nlp.letters_digits(text)")]
    public static string LettersAndDigits(
        [ScriptParam("текст")] string text,
        [ScriptParam("приводить к нижнему регистру")] bool lower = true)
        => TextStandard.OnlyCharsAndDigit(text, lower);

    [ScriptFn("words", "Разбивает текст на слова", Example = "nlp.words(text, drop_stop_words: true)")]
    public static ScriptList Words(
        [ScriptParam("текст")] string text,
        [ScriptParam("убирать стоп-слова")] bool dropStopWords = false)
    {
        string[] parts = SplitWords(text);
        var result = new List<ScriptValue>(parts.Length);

        foreach (string word in parts)
        {
            if (dropStopWords && s_stopWords.Value.Contains(word)) continue;

            result.Add(ScriptValue.Str(word));
        }

        return ScriptList.From(result);
    }

    [ScriptFn("sentences", "Разбивает текст на предложения", Example = "nlp.sentences(text)")]
    public static ScriptList Sentences([ScriptParam("текст")] string text)
    {
        string[] parts = TextSummarization.GetSeqs(text);
        var result = new List<ScriptValue>(parts.Length);

        foreach (string sentence in parts)
        {
            string trimmed = sentence.Trim();

            if (trimmed.Length > 0) result.Add(ScriptValue.Str(trimmed));
        }

        return ScriptList.From(result);
    }

    /// <summary>
    /// Скользящее окно по последовательности: соседние элементы склеиваются в один фрагмент.
    /// </summary>
    /// <remarks>
    /// Нарезка на фрагменты с перекрытием — обычный первый шаг перед векторизацией: предложение
    /// в одиночку часто теряет смысл, а окно из нескольких сохраняет контекст.
    /// </remarks>
    [ScriptFn("window", "Склеивает соседние элементы в фрагменты с перекрытием",
        Example = "nlp.sentences(text) |> nlp.window(size: 5, stride: 3)")]
    public static ScriptList Window(
        [ScriptParam("последовательность строк")] ScriptList parts,
        [ScriptParam("сколько элементов в окне")] int size = 3,
        [ScriptParam("шаг окна")] int stride = 1,
        [ScriptParam("разделитель при склейке")] string separator = " ")
    {
        if (size < 1) throw new ScriptError(DiagnosticCodes.BadOperand, "nlp.window: размер окна меньше единицы");
        if (stride < 1) throw new ScriptError(DiagnosticCodes.BadOperand, "nlp.window: шаг меньше единицы");

        var result = new List<ScriptValue>();

        for (int start = 0; start < parts.Count; start += stride)
        {
            var chunk = new List<string>(size);

            for (int i = start; i < Math.Min(start + size, parts.Count); i++)
                chunk.Add(parts[i].AsString($"nlp.window: элемент {i}"));

            result.Add(ScriptValue.Str(string.Join(separator, chunk)));

            if (start + size >= parts.Count) break;
        }

        return ScriptList.From(result);
    }

    [ScriptFn("stem", "Стемминг русского слова либо списка слов", Example = "nlp.stem(\"бегущего\")")]
    public static ScriptValue Stem([ScriptParam("слово либо список слов")] ScriptValue words) =>
        Transform(words, StemmerRus.TransformingWord, "nlp.stem");

    [ScriptFn("lemma", "Лемматизация русского слова либо списка слов", Example = "nlp.lemma(\"бегущего\")")]
    public static ScriptValue Lemma([ScriptParam("слово либо список слов")] ScriptValue words) =>
        Transform(words, s_lemmatizer.Value.Lemmatize, "nlp.lemma");

    // --- морфология ---

    /// <summary>
    /// Часть речи слова.
    /// </summary>
    /// <remarks>
    /// Кодом, а не русским названием: код устойчив и им удобно сравнивать в условиях
    /// (<c>if nlp.pos(w) == "NOUN"</c>). Название на русском отдаёт <c>nlp.analyze</c>.
    /// </remarks>
    [ScriptFn("pos", "Часть речи слова либо списка слов кодом: NOUN, VERB, ADJ, ADV, PRON, NUM, PREP, CONJ, PRCL",
        Example = "nlp.pos(\"столами\")")]
    public static ScriptValue Pos([ScriptParam("слово либо список слов")] ScriptValue words) =>
        Transform(words, word => RussianPosTagger.Instance.Tag(word).ToCode(), "nlp.pos");

    [ScriptFn("analyze", "Разбор слова: лемма, часть речи и её название",
        Example = "nlp.analyze(\"городами\")")]
    public static ScriptRecord Analyze([ScriptParam("слово")] string word)
    {
        MorphAnalysis analysis = MorphologicalLemmatizer.Instance.Analyze(word);

        return Record(
            ("word", ScriptValue.Str(word ?? string.Empty)),
            ("lemma", ScriptValue.Str(analysis.Lemma)),
            ("pos", ScriptValue.Str(analysis.PartOfSpeech.ToCode())),
            ("pos_name", ScriptValue.Str(analysis.PartOfSpeech.ToRussian())));
    }

    /// <summary>
    /// Разбор всех слов текста.
    /// </summary>
    /// <remarks>
    /// Таблицей, а не тремя списками: слово, его лемма и часть речи — это одна строка,
    /// и разнесённые по спискам они разъедутся при первой же фильтрации.
    /// </remarks>
    [ScriptFn("morph", "Разбор всех слов текста таблицей «слово · лемма · часть речи»",
        Example = "nlp.morph(\"Я читал книги в городах\")")]
    public static ScriptTable Morph(
        IScriptContext context,
        [ScriptParam("текст")] string text)
    {
        string[] words = SplitWords(text ?? string.Empty);

        var forms = new ScriptValue[words.Length];
        var lemmas = new ScriptValue[words.Length];
        var tags = new ScriptValue[words.Length];

        for (int i = 0; i < words.Length; i++)
        {
            MorphAnalysis analysis = MorphologicalLemmatizer.Instance.Analyze(words[i]);

            forms[i] = ScriptValue.Str(words[i]);
            lemmas[i] = ScriptValue.Str(analysis.Lemma);
            tags[i] = ScriptValue.Str(analysis.PartOfSpeech.ToCode());
        }

        context.CountAllocation(words.Length * 3);

        return ScriptTable.Create(
        [
            ScriptColumn.Own("word", forms),
            ScriptColumn.Own("lemma", lemmas),
            ScriptColumn.Own("pos", tags),
        ]);
    }

    /// <summary>
    /// Измеренное качество морфологического разбора.
    /// </summary>
    /// <remarks>
    /// Функция нужна затем, что разбор ошибается, и тот, кто им пользуется, вправе знать
    /// насколько — до того, как построит на нём выводы. Числа считаются на встроенном
    /// эталонном корпусе при первом обращении и дальше берутся готовыми: корпус и правила
    /// в пределах запуска не меняются.
    /// </remarks>
    [ScriptFn("morph_quality", "Измеренное на эталонном корпусе качество разбора: точность лемм и частей речи",
        Example = "nlp.morph_quality()")]
    public static ScriptRecord MorphQuality(IScriptContext context)
    {
        (LemmatizationReport lemmas, PosTaggingReport tags) = s_morphQuality.Value;

        context.CountAllocation(lemmas.Total);

        return Record(
            ("corpus", ScriptValue.Num(lemmas.Total)),
            ("lemma_accuracy", ScriptValue.Num(lemmas.Accuracy)),
            ("noun_accuracy", ScriptValue.Num(lemmas.AccuracyFor("NOUN"))),
            ("pos_accuracy", ScriptValue.Num(tags.Accuracy)),
            ("pos_f_measure", ScriptValue.Num(tags.FMeasure)),
            ("explain", ScriptValue.Str(lemmas.Interpret().ToLlmText())));
    }

    [ScriptFn("is_stop_word", "Является ли слово стоп-словом", Example = "nlp.is_stop_word(\"и\")")]
    public static bool IsStopWord([ScriptParam("слово")] string word) => s_stopWords.Value.Contains(word);

    [ScriptFn("similarity", "Сходство двух текстов по множествам слов (коэффициент Дайса)",
        Example = "nlp.similarity(a, b)")]
    public static double Similarity(
        [ScriptParam("первый текст")] string first,
        [ScriptParam("второй текст")] string second)
        => TextStandard.SimTextDice(WordSet(first), WordSet(second));

    [ScriptFn("summarize", "Извлекающее реферирование: самые весомые предложения",
        Example = "nlp.summarize(text, sentences: 3)")]
    public static string Summarize(
        [ScriptParam("текст")] string text,
        [ScriptParam("сколько предложений оставить")] int sentences = 1)
    {
        if (sentences < 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "nlp.summarize: нужно хотя бы одно предложение");

        return new TextSummarization().Summarization(text, sentences);
    }

    /// <summary>
    /// Мешок слов: частоты слов по корпусу в виде таблицы.
    /// </summary>
    /// <remarks>
    /// Таблицей, а не матрицей: словарь всё равно нужен рядом с числами, иначе колонки
    /// матрицы невозможно соотнести со словами.
    /// </remarks>
    [ScriptFn("bow", "Мешок слов: частоты по корпусу таблицей «слово → сколько»",
        Example = "nlp.bow(docs, top: 20)")]
    public static ScriptTable BagOfWords(
        [ScriptParam("список документов")] string[] docs,
        [ScriptParam("сколько самых частых слов оставить; 0 — все")] int top = 50,
        [ScriptParam("убирать стоп-слова")] bool dropStopWords = true)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (string document in docs)
        {
            foreach (string word in SplitWords(document))
            {
                if (dropStopWords && s_stopWords.Value.Contains(word)) continue;

                counts[word] = counts.TryGetValue(word, out int seen) ? seen + 1 : 1;
            }
        }

        var ordered = new List<KeyValuePair<string, int>>(counts);

        ordered.Sort((left, right) => right.Value != left.Value
            ? right.Value.CompareTo(left.Value)
            : string.CompareOrdinal(left.Key, right.Key));

        int take = top < 1 ? ordered.Count : Math.Min(top, ordered.Count);

        var words = new ScriptValue[take];
        var frequencies = new Vector(take);

        for (int i = 0; i < take; i++)
        {
            words[i] = ScriptValue.Str(ordered[i].Key);
            frequencies[i] = ordered[i].Value;
        }

        return ScriptTable.Create(
        [
            ScriptColumn.Own("word", words),
            ScriptColumn.FromVector("count", frequencies),
        ]);
    }

    // --- поисковые индексы ---

    [ScriptFn("tfidf", "Строит индекс TF-IDF по корпусу документов", Returns = IndexHandle,
        Example = "let index = nlp.tfidf(docs)")]
    public static ScriptHandle TfIdf(
        IScriptContext context,
        [ScriptParam("список документов")] string[] docs)
    {
        RequireCorpus(docs, "nlp.tfidf");
        context.CountAllocation(docs.Length);

        var index = new TextIndex(new TFIDF(docs), docs.Length);

        return new ScriptHandle(IndexHandle, index, index.ToString());
    }

    [ScriptFn("bm25", "Строит индекс BM25 по корпусу документов", Returns = IndexHandle,
        Example = "let index = nlp.bm25(docs)")]
    public static ScriptHandle Bm25(
        IScriptContext context,
        [ScriptParam("список документов")] string[] docs,
        [ScriptParam("параметр насыщения k1")] double k1 = 1.5,
        [ScriptParam("параметр длины документа b")] double b = 0.75)
    {
        RequireCorpus(docs, "nlp.bm25");
        context.CountAllocation(docs.Length);

        var index = new TextIndex(new BM25(docs, k1, b), docs.Length);

        return new ScriptHandle(IndexHandle, index, index.ToString());
    }

    /// <summary>
    /// Ищет документы по запросу.
    /// </summary>
    /// <remarks>
    /// Возвращает таблицу с номером документа и оценкой, а не один номер: по одному номеру
    /// нельзя ни отсортировать выдачу, ни понять, насколько уверенно нашлось.
    /// </remarks>
    [ScriptFn("search", "Ищет документы по запросу", Example = "index.search(\"прокси\", top: 5)")]
    [ScriptMethod(IndexHandle)]
    public static ScriptTable Search(
        [ScriptParam("индекс")] ScriptHandle index,
        [ScriptParam("запрос")] string query,
        [ScriptParam("сколько результатов")] int top = 5)
    {
        if (top < 1) throw new ScriptError(DiagnosticCodes.BadOperand, "nlp.search: нужен хотя бы один результат");

        IReadOnlyList<(int Document, double Score)> found = ((TextIndex)index.Target).Search(query, top);

        var documents = new Vector(found.Count);
        var scores = new Vector(found.Count);

        for (int i = 0; i < found.Count; i++)
        {
            documents[i] = found[i].Document;
            scores[i] = found[i].Score;
        }

        return ScriptTable.Create(
        [
            ScriptColumn.FromVector("doc", documents),
            ScriptColumn.FromVector("score", scores),
        ]);
    }

    [ScriptFn("score", "Оценка соответствия документа запросу", Example = "index.score(\"прокси\", doc: 0)")]
    [ScriptMethod(IndexHandle)]
    public static double Score(
        [ScriptParam("индекс")] ScriptHandle index,
        [ScriptParam("запрос")] string query,
        [ScriptParam("номер документа")] int doc)
        => ((TextIndex)index.Target).Score(query, doc);

    private static ScriptValue Transform(ScriptValue words, Func<string, string> transform, string what)
    {
        if (words.Type == ScriptType.Str) return ScriptValue.Str(transform(words.AsString(what)));

        ScriptList list = words.AsList(what);
        var result = new ScriptValue[list.Count];

        for (int i = 0; i < list.Count; i++)
            result[i] = ScriptValue.Str(transform(list[i].AsString($"{what}: элемент {i}")));

        return ScriptValue.List(ScriptList.Own(result));
    }

    private static string[] SplitWords(string text) =>
        TextStandard.OnlyCharsAndDigit(text).Split(s_separators, StringSplitOptions.RemoveEmptyEntries);

    private static HashSet<string> WordSet(string text) => new(SplitWords(text), StringComparer.Ordinal);

    private static ScriptRecord Record(params (string Name, ScriptValue Value)[] fields)
    {
        var built = new List<KeyValuePair<string, ScriptValue>>(fields.Length);

        foreach ((string name, ScriptValue value) in fields)
            built.Add(new KeyValuePair<string, ScriptValue>(name, value));

        return ScriptRecord.From(built);
    }

    private static void RequireCorpus(string[] docs, string what)
    {
        if (docs.Length > 0) return;

        throw new ScriptError(DiagnosticCodes.SizeMismatch, $"{what}: корпус пуст");
    }
}
