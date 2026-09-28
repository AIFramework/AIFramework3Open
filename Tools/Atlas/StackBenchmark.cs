using AI.Script.Llm;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Задачи бенчмарка AIScript с шагами, какими их передал бы клиент-модель через <c>find_stack</c>.
/// </summary>
/// <remarks>
/// Текст задач берётся из <see cref="ScriptBenchmark.Tasks"/>, а не копируется. Шаги
/// написаны словами действия, без имён функций: они проверяют, найдёт ли Атлас функции по
/// описанию шага и соберёт ли из них цепочку, которая проходит проверку типов. Разбиение
/// моделью (<see cref="TaskSteps.PlanAsync"/>) меряется на тех же задачах, когда есть ключ.
/// </remarks>
public static class StackBenchmark
{
    private static readonly Dictionary<string, string[]> Steps = new(StringComparer.Ordinal)
    {
        ["статистика"] = ["среднее значение чисел", "медиана чисел"],
        ["фильтр и сумма"] = ["оставить элементы, удовлетворяющие условию", "сумма элементов вектора", "количество элементов"],
        ["таблица и группировка"] = ["сгруппировать строки таблицы по колонке и посчитать сумму", "отфильтровать строки таблицы по условию", "число строк таблицы"],
        ["корреляция"] = ["коэффициент корреляции Пирсона двух рядов"],
        ["матрица"] = ["перемножить две матрицы", "взять элемент матрицы по строке и столбцу"],
        ["кластеризация"] = ["кластеризация точек методом k-средних", "метки кластеров для точек"],
        ["текст"] = ["разбить текст на слова", "число элементов списка"],
        ["сигнал"] = ["сгенерировать синусоиду заданной частоты", "спектр сигнала", "частота с максимальной амплитудой спектра"],
        ["регрессия"] = ["построить линейную регрессию по точкам", "предсказание модели регрессии для нового значения"],
        ["производная"] = ["численная производная функции в точке"],
    };

    /// <summary>
    /// Верные функции на каждом шаге — по эталонным решениям из <c>BenchmarkTests</c>. Пустой
    /// набор — правильный ответ «пробел»: элемент матрицы берётся индексом, предсказание —
    /// методом модели, а не функцией пространства.
    /// </summary>
    private static readonly Dictionary<string, string[][]> Expected = new(StringComparer.Ordinal)
    {
        ["статистика"] = [["stat.mean"], ["stat.median"]],
        ["фильтр и сумма"] = [["core.filter"], ["vec.sum"], ["core.len"]],
        ["таблица и группировка"] = [["table.group_by"], ["table.filter"], ["core.len", "table.rows"]],
        ["корреляция"] = [["stat.corr"]],
        ["матрица"] = [["mat.mul"], []],
        ["кластеризация"] = [["ml.kmeans"], []],
        ["текст"] = [["str.split", "nlp.words"], ["core.len"]],
        ["сигнал"] = [["signal.sine"], ["dsp.fft"], ["vec.argmax"]],
        ["регрессия"] = [["ml.linreg"], []],
        ["производная"] = [["solve.derivative_fn"]],
    };

    /// <summary>Задачи: имя, текст, шаги, верные функции шагов.</summary>
    public static IReadOnlyList<(string Name, string Task, IReadOnlyList<string> Steps, IReadOnlyList<string[]> Expected)> Tasks =>
        [.. ScriptBenchmark.Tasks
            .Where(task => Steps.ContainsKey(task.Name))
            .Select(task => (task.Name, task.Task, (IReadOnlyList<string>)Steps[task.Name], (IReadOnlyList<string[]>)Expected[task.Name]))];

    /// <summary>Верно ли выбран шаг: функция из эталона либо пробел там, где функции нет.</summary>
    public static bool IsRight(StackStep step, string[] expected) =>
        expected.Length == 0 ? step.Hit is null : step.Hit?.Unit.Script is { } script && expected.Contains(script);
}
