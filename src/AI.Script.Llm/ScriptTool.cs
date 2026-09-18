using AI.LLM.Agents.Tools;
using AI.Script.Docs;
using AI.Script.Hosting;
using AI.Script.Semantics;
using AI.Script.Std;
using System.Text;

namespace AI.Script.Llm;

/// <summary>
/// Скрипт как инструмент агента: проверить, выполнить, вернуть отчёт.
/// </summary>
/// <remarks>
/// Смысл инструмента — отдать агенту не набор разрозненных функций, а способ их соединить.
/// Агент, у которого есть тридцать инструментов «посчитать среднее», «отфильтровать»,
/// «обучить», тратит контекст на протаскивание промежуточных данных через себя; агент, у
/// которого есть один инструмент «выполни конвейер», отдаёт данные рантайму и получает
/// обратно только результат.
/// <para>
/// Данные остаются в процессе: наружу уходят числа из <c>emit</c>, усечённый транскрипт и
/// перечень артефактов, но не таблица на сорок строк, которую пришлось бы протаскивать через
/// контекст модели.
/// </para>
/// </remarks>
public sealed class ScriptTool
{
    /// <summary>Сколько строк транскрипта уходит агенту.</summary>
    public const int TranscriptLines = 40;

    /// <summary>Предельная длина строки транскрипта в отчёте.</summary>
    public const int LineLimit = 300;

    private readonly ScriptHost _host;
    private readonly Func<RunOptions> _options;
    private readonly Func<IReadOnlyList<ScriptInput>, CancellationToken, Task<IReadOnlyDictionary<string, object?>>>? _inputs;

    /// <summary>Создаёт инструмент поверх хоста.</summary>
    /// <param name="host">Хост со стандартной библиотекой.</param>
    /// <param name="options">
    /// Настройки каждого прогона. Фабрика, а не готовый объект: настройки содержат счётчики
    /// и песочницу, и один экземпляр на все вызовы означал бы общий потолок расходов у
    /// независимых запросов агента.
    /// </param>
    /// <param name="inputs">
    /// Кто подаёт объявленные скриптом входы (<c>input("продажи")</c>); <c>null</c> — входы не
    /// подаются, и скрипт с объявленным входом честно откажет до исполнения.
    /// </param>
    /// <remarks>
    /// Входы спрашиваются ПОСЛЕ проверки: до неё не известно, чего скрипт просит, а спрашивать
    /// «всё, что есть» значит читать файлы, которые никому не понадобились.
    /// </remarks>
    public ScriptTool(
        ScriptHost host,
        Func<RunOptions>? options = null,
        Func<IReadOnlyList<ScriptInput>, CancellationToken, Task<IReadOnlyDictionary<string, object?>>>? inputs = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _options = options ?? (static () => RunProfiles.Untrusted());
        _inputs = inputs;
    }

    /// <summary>Последний исход прогона: нужен вызывающему за артефактами и значениями.</summary>
    public RunResult? Last { get; private set; }

    [AgentTool("run_script", "Выполнить конвейер обработки данных на языке AIScript. " +
        "Скрипт сначала проверяется: при ошибках вернутся диагностики, и прогон не начнётся.")]
    public async Task<string> RunAsync(
        [ToolParameter("исходный текст скрипта на языке AIScript")] string script,
        CancellationToken cancellationToken = default)
    {
        string source = script ?? string.Empty;
        CheckResult check = _host.Check(source);

        if (!check.Success)
        {
            Last = null;

            return "Скрипт не прошёл проверку и не выполнялся. Исправьте ровно указанное:\n\n" + check.Render();
        }

        RunOptions options = _options();

        if (_inputs != null && check.Inputs.Count > 0)
            options.Seeded = await _inputs(check.Inputs, cancellationToken).ConfigureAwait(false);

        RunResult result = await _host.RunAsync(source, options, cancellationToken).ConfigureAwait(false);

        Last = result;

        return Report(result);
    }

    [AgentTool("check_script", "Проверить скрипт AIScript, не выполняя его: имена, аргументы и типы.")]
    public string Check(
        [ToolParameter("исходный текст скрипта на языке AIScript")] string script)
    {
        CheckResult check = _host.Check(script ?? string.Empty);

        return check.Success ? "Проверка пройдена: замечаний нет." : check.Render();
    }

    [AgentTool("script_help", "Справка по языку AIScript: задача (например «данные»), пространство имён, " +
        "конкретная функция либо поиск по словам задачи. Без аргумента — пространства по задачам.")]
    public string Help(
        [ToolParameter("задача, имя пространства, полное имя функции либо слова задачи", Required = false)]
        string query = "")
    {
        if (string.IsNullOrWhiteSpace(query)) return _host.DescribeCapabilities(ManifestOptions.Index);

        string description = _host.Describe(query);

        if (!description.StartsWith("Ничего не найдено", StringComparison.Ordinal)) return description;

        IReadOnlyList<ManifestMatch> found = _host.Search(query);

        if (found.Count == 0) return $"По запросу «{query}» ничего не найдено.";

        var builder = new StringBuilder($"По запросу «{query}» похоже подходят:\n");

        foreach (ManifestMatch match in found)
            _ = builder.Append("  ").Append(match.Function.Signature).Append('\n');

        return builder.ToString();
    }

    /// <summary>
    /// Отчёт о прогоне для модели.
    /// </summary>
    /// <remarks>
    /// Порядок разделов не случаен: сначала итог, потом результаты, потом причина отказа.
    /// Модель читает начало внимательнее конца, а решение о следующем шаге принимается по
    /// первым двум строкам.
    /// </remarks>
    private static string Report(RunResult result)
    {
        var builder = new StringBuilder();

        _ = builder.Append(result.Success ? "Скрипт выполнен." : "Скрипт сорвался.").Append('\n');

        if (result.Emitted.Count > 0)
        {
            _ = builder.Append("\nРезультаты (emit):\n");
            _ = builder.Append(Json.Write(Binding.Marshaller.FromClr(AsRecord(result.Emitted)), pretty: true));
            _ = builder.Append('\n');
        }
        else if (result.Success)
        {
            _ = builder.Append("\nСкрипт ничего не вернул: результаты отдаются через 'emit имя = значение'.\n");
        }

        if (result.Artifacts.Count > 0)
        {
            _ = builder.Append("\nПоказано пользователю: ");

            for (int i = 0; i < result.Artifacts.Count; i++)
            {
                if (i > 0) _ = builder.Append(", ");

                _ = builder.Append(result.Artifacts[i].Kind);

                if (result.Artifacts[i].Title is string title) _ = builder.Append(" «").Append(title).Append('»');
            }

            _ = builder.Append('\n');
        }

        AppendTranscript(builder, result);

        if (result.Error is Diagnostic error)
        {
            _ = builder.Append("\nОтказ:\n").Append(error.Render()).Append('\n');
        }

        _ = builder.Append('\n').Append(result.Stats).Append('\n');

        return builder.ToString();
    }

    private static void AppendTranscript(StringBuilder builder, RunResult result)
    {
        if (result.Transcript.Count == 0) return;

        _ = builder.Append("\nВывод (print):\n");

        int skip = Math.Max(0, result.Transcript.Count - TranscriptLines);

        // Отбрасывается начало, а не конец: последние строки ближе к тому месту, где всё
        // пошло не так, и именно они нужны для следующей попытки.
        if (skip > 0) _ = builder.Append("  … пропущено строк: ").Append(skip).Append('\n');

        for (int i = skip; i < result.Transcript.Count; i++)
        {
            string line = result.Transcript[i];

            _ = builder.Append("  ")
                .Append(line.Length <= LineLimit ? line : line[..LineLimit] + "…")
                .Append('\n');
        }
    }

    private static Dictionary<string, object?> AsRecord(IReadOnlyDictionary<string, object?> values)
    {
        var record = new Dictionary<string, object?>(values.Count, StringComparer.Ordinal);

        foreach (var pair in values) record[pair.Key] = pair.Value;

        return record;
    }
}
