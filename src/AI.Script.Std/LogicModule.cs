using AI.Logic.Ontology.RDF;
using AI.Logic.Probability;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>logic</c>: байесовский пересчёт вероятностей и вывод по онтологии.
/// </summary>
/// <remarks>
/// Байесовский пересчёт сделан одной функцией без состояния, хотя библиотека предлагает объект
/// с накоплением: скрипту нужен ответ «каковы вероятности после этих наблюдений», а не объект,
/// который помнит, что ему говорили раньше. Онтология, наоборот, состояние по существу — она
/// и осталась дескриптором.
/// </remarks>
[ScriptModule("logic", "Логика: байесовский вывод и вывод по онтологии", Version = "0.1")]
public static class LogicModule
{
    /// <summary>Тип-тег дескриптора онтологии.</summary>
    public const string OntologyHandle = "logic.ontology";

    // --- вероятностный вывод ---

    /// <summary>
    /// Пересчёт вероятностей гипотез по наблюдению.
    /// </summary>
    /// <remarks>
    /// Правдоподобия задаются для тех же гипотез, что и априорные вероятности: это не
    /// формальность, а единственный способ заметить опечатку в имени гипотезы — иначе она
    /// молча осталась бы с прежней вероятностью и увела бы весь вывод.
    /// </remarks>
    [ScriptFn("bayes", "Апостериорные вероятности гипотез по правдоподобиям наблюдения",
        Returns = "record",
        Example = "logic.bayes(priors: { болен: 0.01, здоров: 0.99 }, " +
            "likelihoods: { болен: 0.99, здоров: 0.05 })")]
    public static ScriptRecord Bayes(
        [ScriptParam("априорные вероятности гипотез")] ScriptRecord priors,
        [ScriptParam("вероятности наблюдения при каждой гипотезе")] ScriptRecord likelihoods)
    {
        if (priors.Count == 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "logic.bayes: не задано ни одной гипотезы");

        var hypotheses = new Dictionary<string, double>(priors.Count, StringComparer.Ordinal);
        var evidence = new Dictionary<string, double>(likelihoods.Count, StringComparer.Ordinal);
        double total = 0;

        for (int i = 0; i < priors.Count; i++)
        {
            double prior = Probability(priors.Values[i], $"logic.bayes: априорная вероятность '{priors.Keys[i]}'");

            hypotheses[priors.Keys[i]] = prior;
            total += prior;
        }

        if (Math.Abs(total - 1) > 1e-6)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"logic.bayes: априорные вероятности в сумме дают {total:0.###}, а должны единицу",
                "перечислите все взаимоисключающие гипотезы либо нормируйте их");
        }

        for (int i = 0; i < likelihoods.Count; i++)
        {
            string name = likelihoods.Keys[i];

            if (!hypotheses.ContainsKey(name))
            {
                throw new ScriptError(
                    DiagnosticCodes.UnknownArgument,
                    $"logic.bayes: правдоподобие задано для '{name}', а такой гипотезы нет",
                    $"гипотезы: {string.Join(", ", priors.Keys)}");
            }

            evidence[name] = Probability(likelihoods.Values[i], $"logic.bayes: правдоподобие '{name}'");
        }

        var inference = new BayesianInference();

        inference.UpdateProbabilities(evidence, hypotheses);

        var posteriors = new List<KeyValuePair<string, ScriptValue>>(priors.Count);

        foreach (string name in priors.Keys)
            posteriors.Add(new KeyValuePair<string, ScriptValue>(name, ScriptValue.Num(inference.GetPosterior(name))));

        return ScriptRecord.From(posteriors);
    }

    // --- онтология ---

    /// <summary>
    /// Онтология из таблицы триплетов.
    /// </summary>
    /// <remarks>
    /// Таблица, а не список записей: триплеты почти всегда приходят из файла или из разбора
    /// текста, и обе дороги ведут к таблице. Имена колонок фиксированы намеренно — свобода
    /// называть их как угодно здесь ничего не даёт, а ошибиться позволяет.
    /// </remarks>
    [ScriptFn("ontology", "Онтология из таблицы с колонками subject, predicate, object",
        Returns = OntologyHandle,
        Example = "let o = logic.ontology(триплеты)")]
    public static ScriptHandle BuildOntology(
        [ScriptParam("таблица триплетов: subject, predicate, object")] ScriptTable triples)
    {
        var ontology = new Ontology();

        ScriptColumn subjects = Column(triples, "subject");
        ScriptColumn predicates = Column(triples, "predicate");
        ScriptColumn objects = Column(triples, "object");

        for (int i = 0; i < triples.RowCount; i++)
        {
            ontology.AddTriple(new Triple(
                ScriptFormatter.Format(subjects[i]),
                ScriptFormatter.Format(predicates[i]),
                ScriptFormatter.Format(objects[i])));
        }

        return new ScriptHandle(OntologyHandle, ontology, $"триплетов: {triples.RowCount}");
    }

    [ScriptFn("size", "Сколько триплетов в онтологии", Example = "o.size()")]
    [ScriptMethod(OntologyHandle)]
    public static double Size([ScriptParam("онтология")] ScriptHandle ontology) =>
        Unwrap(ontology).GetTriples().Count;

    [ScriptFn("triples", "Все триплеты онтологии таблицей", Example = "o.triples()")]
    [ScriptMethod(OntologyHandle)]
    public static ScriptTable Triples(
        IScriptContext context,
        [ScriptParam("онтология")] ScriptHandle ontology) =>
        ToTable(context, Unwrap(ontology).GetTriples());

    [ScriptFn("about", "Триплеты про заданный субъект", Example = "o.about(\"Сократ\")")]
    [ScriptMethod(OntologyHandle)]
    public static ScriptTable About(
        IScriptContext context,
        [ScriptParam("онтология")] ScriptHandle ontology,
        [ScriptParam("субъект")] string subject) =>
        ToTable(context, Unwrap(ontology).GetTriplesBySubject(subject));

    [ScriptFn("subclasses", "Подклассы заданного класса", Example = "o.subclasses(\"Животное\")")]
    [ScriptMethod(OntologyHandle)]
    public static ScriptList Subclasses(
        [ScriptParam("онтология")] ScriptHandle ontology,
        [ScriptParam("класс")] string cls) =>
        ToList(Unwrap(ontology).GetSubclasses(cls));

    /// <summary>
    /// Вывод новых триплетов из имеющихся.
    /// </summary>
    /// <remarks>
    /// Возвращаются только выведенные триплеты, а не вся онтология вместе с ними: разница
    /// между «что я записал» и «что из этого следует» — обычно и есть то, ради чего вывод
    /// запускают.
    /// </remarks>
    [ScriptFn("infer", "Триплеты, следующие из записанных", Example = "o.infer()")]
    [ScriptMethod(OntologyHandle)]
    public static ScriptTable Infer(
        IScriptContext context,
        [ScriptParam("онтология")] ScriptHandle ontology) =>
        ToTable(context, new InferenceEngine(Unwrap(ontology)).Infer());

    // --- внутреннее ---

    private static Ontology Unwrap(ScriptHandle handle) => (Ontology)handle.Target;

    private static double Probability(ScriptValue value, string what)
    {
        if (value.Type != ScriptType.Num)
        {
            throw new ScriptError(
                DiagnosticCodes.TypeMismatch,
                $"{what}: ожидалось число, получено {value.Type.ToName()}");
        }

        double probability = value.RawNumber;

        return probability is >= 0 and <= 1
            ? probability
            : throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: вероятность лежит в [0, 1]");
    }

    private static ScriptColumn Column(ScriptTable table, string name)
    {
        foreach (ScriptColumn column in table.Columns)
        {
            if (string.Equals(column.Name, name, StringComparison.Ordinal)) return column;
        }

        throw new ScriptError(
            DiagnosticCodes.UnknownArgument,
            $"logic.ontology: в таблице нет колонки '{name}'",
            $"нужны колонки subject, predicate, object; есть: {string.Join(", ", table.Names())}");
    }

    private static ScriptTable ToTable(IScriptContext context, IReadOnlyList<Triple> triples)
    {
        var subjects = new ScriptValue[triples.Count];
        var predicates = new ScriptValue[triples.Count];
        var objects = new ScriptValue[triples.Count];

        for (int i = 0; i < triples.Count; i++)
        {
            subjects[i] = ScriptValue.Str(triples[i].Subject);
            predicates[i] = ScriptValue.Str(triples[i].Predicate);
            objects[i] = ScriptValue.Str(triples[i].Object);
        }

        context.CountAllocation(triples.Count * 3L);

        return ScriptTable.Create(
        [
            ScriptColumn.Own("subject", subjects),
            ScriptColumn.Own("predicate", predicates),
            ScriptColumn.Own("object", objects),
        ]);
    }

    private static ScriptList ToList(IEnumerable<string> values)
    {
        var items = new List<ScriptValue>();

        foreach (string value in values) items.Add(ScriptValue.Str(value));

        return ScriptList.From(items);
    }
}
