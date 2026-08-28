using AI.Script.Hosting;

namespace AI.Script.Llm;

/// <summary>Одна задача эталонного набора.</summary>
/// <param name="Name">Короткое имя для отчёта.</param>
/// <param name="Task">Задача словами пользователя — ровно то, что уйдёт модели.</param>
/// <param name="Verify">
/// Проверка результата: возвращает <c>null</c>, если решение верно, иначе — чем оно плохо.
/// </param>
public sealed record BenchmarkTask(string Name, string Task, Func<RunResult, string?> Verify);

/// <summary>Как решилась одна задача.</summary>
/// <param name="Task">Задача.</param>
/// <param name="Solution">Что получилось у модели.</param>
/// <param name="Problem">Чем плохо решение; <c>null</c> — задача решена.</param>
public sealed record BenchmarkOutcome(BenchmarkTask Task, ScriptSolution Solution, string? Problem)
{
    /// <summary>Решена ли задача.</summary>
    public bool Solved => Problem == null;
}

/// <summary>Итог по всему набору.</summary>
/// <param name="Outcomes">Исходы по задачам в порядке набора.</param>
public sealed record BenchmarkReport(IReadOnlyList<BenchmarkOutcome> Outcomes)
{
    /// <summary>Сколько задач решено.</summary>
    public int Solved
    {
        get
        {
            int count = 0;

            foreach (BenchmarkOutcome outcome in Outcomes)
            {
                if (outcome.Solved) count++;
            }

            return count;
        }
    }

    /// <summary>Сколько задач в наборе.</summary>
    public int Total => Outcomes.Count;

    /// <summary>Отчёт для человека.</summary>
    public string Render()
    {
        var lines = new List<string>(Outcomes.Count + 2) { $"Решено {Solved} из {Total}." , string.Empty };

        foreach (BenchmarkOutcome outcome in Outcomes)
        {
            string mark = outcome.Solved ? "+" : "−";
            string attempts = outcome.Solution.Attempts > 1 ? $", попыток: {outcome.Solution.Attempts}" : string.Empty;

            lines.Add($"  {mark} {outcome.Task.Name}{attempts}");

            if (outcome.Problem != null) lines.Add($"      {outcome.Problem}");
        }

        return string.Join("\n", lines);
    }
}

/// <summary>
/// Эталонный набор задач: признак готовности LLM-контура.
/// </summary>
/// <remarks>
/// Задачи сформулированы так, как их ставит человек, а не так, как их удобно проверять: в них
/// нет ни имён функций языка, ни подсказок про синтаксис. Проверяется результат в <c>emit</c>,
/// а не текст скрипта — способов посчитать среднее много, и требовать конкретный значило бы
/// мерить не то.
/// <para>
/// Числа в задачах заданы прямо в тексте: набор обязан работать без внешних файлов, иначе
/// провал невозможно отличить от отсутствия данных.
/// </para>
/// </remarks>
public static class ScriptBenchmark
{
    /// <summary>Эталонные задачи.</summary>
    public static IReadOnlyList<BenchmarkTask> Tasks { get; } =
    [
        new("статистика",
            "Даны числа 12, 7, 3, 21, 15, 9, 4. Посчитай их среднее и медиану. " +
            "Отдай результаты под именами 'среднее' и 'медиана'.",
            result => Near(result, "среднее", 10.142857, 1e-4) ?? Near(result, "медиана", 9, 1e-9)),

        new("фильтр и сумма",
            "Из чисел 4, -2, 15, 0, -7, 23, 8 оставь только положительные, сложи их " +
            "и отдай сумму под именем 'сумма', а количество отобранных — под именем 'сколько'.",
            result => Near(result, "сумма", 50, 1e-9) ?? Near(result, "сколько", 4, 1e-9)),

        new("таблица и группировка",
            "Есть продажи: Москва 120, Тверь 30, Москва 80, Тверь 45, Казань 60. " +
            "Посчитай выручку по городам и отдай выручку Москвы под именем 'москва', " +
            "а число городов — под именем 'городов'.",
            result => Near(result, "москва", 200, 1e-9) ?? Near(result, "городов", 3, 1e-9)),

        new("корреляция",
            "Для рядов x = 1, 2, 3, 4, 5 и y = 2, 4, 6, 8, 10 посчитай коэффициент корреляции Пирсона " +
            "и отдай его под именем 'корреляция'.",
            result => Near(result, "корреляция", 1, 1e-6)),

        new("матрица",
            "Перемножь матрицы [[1, 2], [3, 4]] и [[5, 6], [7, 8]], возьми элемент первой строки " +
            "и первого столбца результата и отдай его под именем 'элемент'.",
            result => Near(result, "элемент", 19, 1e-9)),

        new("кластеризация",
            "Есть точки на плоскости: (0,0), (0.2,0.1), (0.1,0.2), (5,5), (5.2,4.9), (4.9,5.1). " +
            "Раздели их на две группы и отдай под именем 'групп' число различных найденных групп, " +
            "а под именем 'объектов' — сколько всего точек.",
            result => Near(result, "групп", 2, 1e-9) ?? Near(result, "объектов", 6, 1e-9)),

        new("текст",
            "В строке «Мама мыла раму, а рама мыла маму» посчитай число слов и отдай его " +
            "под именем 'слов'.",
            result => Near(result, "слов", 7, 1e-9)),

        new("сигнал",
            "Сгенерируй синус частотой 50 Гц длительностью 1 секунда с частотой дискретизации 1000 Гц, " +
            "найди по спектру частоту с наибольшей амплитудой и отдай её под именем 'частота'.",
            result => Near(result, "частота", 50, 2)),

        new("регрессия",
            "По точкам x = 1, 2, 3, 4, 5 и y = 3, 5, 7, 9, 11 найди линейную зависимость " +
            "и отдай предсказание для x = 6 под именем 'предсказание'.",
            result => Near(result, "предсказание", 13, 0.01)),

        new("производная",
            "Найди производную функции x^3 + 2*x в точке x = 2 и отдай её под именем 'производная'.",
            result => Near(result, "производная", 14, 0.01)),
    ];

    /// <summary>
    /// Прогоняет набор через цикл «написал → проверил → исполнил».
    /// </summary>
    /// <param name="writer">Цикл, решающий задачи.</param>
    /// <param name="tasks">Набор; <c>null</c> — эталонный.</param>
    /// <param name="cancellationToken">Отмена.</param>
    /// <remarks>
    /// Задачи идут последовательно, а не параллельно: набор упирается в чужую службу с её
    /// ограничениями по частоте запросов, и восемь одновременных обращений чаще дают отказ по
    /// частоте, чем выигрыш во времени.
    /// </remarks>
    public static async Task<BenchmarkReport> RunAsync(
        ScriptWriter writer,
        IReadOnlyList<BenchmarkTask>? tasks = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);

        IReadOnlyList<BenchmarkTask> set = tasks ?? Tasks;
        var outcomes = new List<BenchmarkOutcome>(set.Count);

        foreach (BenchmarkTask task in set)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ScriptSolution solution = await writer.SolveAsync(task.Task, cancellationToken).ConfigureAwait(false);

            outcomes.Add(new BenchmarkOutcome(task, solution, Judge(task, solution)));
        }

        return new BenchmarkReport(outcomes);
    }

    private static string? Judge(BenchmarkTask task, ScriptSolution solution)
    {
        if (solution.Result == null) return "скрипт так и не прошёл проверку";
        if (!solution.Result.Success) return solution.Result.Error?.Message ?? "прогон сорвался";

        return task.Verify(solution.Result);
    }

    /// <summary>
    /// Сверяет число под именем с ожидаемым.
    /// </summary>
    /// <remarks>
    /// С допуском, а не точным равенством: способов посчитать среднее много, и последний бит
    /// у них разный. Требовать совпадения бит в бит значило бы проверять порядок операций
    /// вместо ответа.
    /// </remarks>
    public static string? Near(RunResult result, string name, double expected, double tolerance)
    {
        if (!result.Emitted.TryGetValue(name, out object? value))
            return $"нет результата '{name}' (есть: {string.Join(", ", result.Emitted.Keys)})";

        if (value is not double actual) return $"'{name}' — не число, а {value?.GetType().Name ?? "ничто"}";

        return Math.Abs(actual - expected) <= tolerance
            ? null
            : $"'{name}' = {actual}, а ожидалось {expected}";
    }
}
