using AI.Script.Charts;
using AI.Script.Chem;
using AI.Script.Hosting;
using AI.Script.Llm;
using AI.Script.Nn;
using AI.Script.Vision;
using AI.Script.Semantics;
using AI.Script.Std;

namespace AI.Script.UnitTests;

/// <summary>Общая обвязка тестов: запуск скрипта и извлечение результата.</summary>
internal static class Script
{
    /// <summary>Хост со стандартной библиотекой и графиками.</summary>
    public static ScriptHost Host() => StandardLibrary.CreateHost().UseCharts();

    /// <summary>
    /// Хост со всеми пространствами, включая <c>llm</c> и <c>search</c> без служб.
    /// </summary>
    /// <remarks>
    /// Службы не подключены намеренно: так проверяется ровно то, что должно работать без
    /// сети и без ключей — разбор, проверка и словесный поиск. Обращение к модели на таком
    /// хосте отказывает внятным сообщением, а не молчаливым таймаутом.
    /// </remarks>
    public static ScriptHost FullHost() => Host().UseLlm().UseChem().UseNeuralNetworks().UseVision();

    /// <summary>Выполняет скрипт.</summary>
    public static RunResult Run(string source, RunOptions? options = null) =>
        Host().RunAsync(source, options).GetAwaiter().GetResult();

    /// <summary>Выполняет скрипт в заданном хосте.</summary>
    public static RunResult RunWith(
        ScriptHost host,
        string source,
        RunOptions? options = null,
        CancellationToken cancellationToken = default)
        => host.RunAsync(source, options, cancellationToken).GetAwaiter().GetResult();

    /// <summary>Проверяет скрипт без запуска.</summary>
    public static CheckResult Check(string source) => Host().Check(source);

    /// <summary>Выполняет скрипт и требует успеха; возвращает исход.</summary>
    public static RunResult RunOk(string source, RunOptions? options = null)
    {
        RunResult result = Run(source, options);

        Assert.True(result.Success, Report(result));

        return result;
    }

    /// <summary>Вычисляет выражение и отдаёт результат как объект C#.</summary>
    /// <param name="expression">Выражение.</param>
    /// <param name="prelude">Инструкции, выполняемые до выражения.</param>
    public static object? Eval(string expression, string? prelude = null)
    {
        string source = prelude == null ? $"emit r = {expression}" : $"{prelude}\nemit r = {expression}";

        return RunOk(source).Emitted["r"];
    }

    /// <summary>Вычисляет числовое выражение.</summary>
    public static double Number(string expression, string? prelude = null) =>
        Assert.IsType<double>(Eval(expression, prelude));

    /// <summary>Вычисляет строковое выражение.</summary>
    public static string Text(string expression, string? prelude = null) =>
        Assert.IsType<string>(Eval(expression, prelude));

    /// <summary>Вычисляет логическое выражение.</summary>
    public static bool Flag(string expression, string? prelude = null) =>
        Assert.IsType<bool>(Eval(expression, prelude));

    /// <summary>Первая ошибка прогона; провал теста, если её нет.</summary>
    /// <param name="source">Текст скрипта.</param>
    /// <param name="options">Настройки прогона.</param>
    /// <param name="host">Хост; <c>null</c> — стандартный.</param>
    public static Diagnostic FailsWith(string source, RunOptions? options = null, ScriptHost? host = null)
    {
        RunResult result = host == null ? Run(source, options) : RunWith(host, source, options);

        Assert.False(result.Success, "ожидался отказ, но скрипт отработал");
        Assert.NotNull(result.Error);

        return result.Error!;
    }

    /// <summary>Первая ошибка проверки; провал теста, если её нет.</summary>
    public static Diagnostic CheckFailsWith(string source)
    {
        CheckResult result = Check(source);

        Assert.False(result.Success, "ожидалась ошибка проверки, но она не найдена");

        foreach (Diagnostic diagnostic in result.Diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error) return diagnostic;
        }

        throw new InvalidOperationException("ошибок нет");
    }

    /// <summary>Коды ошибок проверки; пусто, если ошибок нет.</summary>
    public static IReadOnlyList<string> CheckCodes(string source)
    {
        var codes = new List<string>();

        foreach (Diagnostic diagnostic in Check(source).Diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error) codes.Add(diagnostic.Code);
        }

        return codes;
    }

    /// <summary>Первая диагностика проверки с заданным кодом; провал теста, если её нет.</summary>
    public static Diagnostic CheckDiagnostic(string source, string code)
    {
        CheckResult result = Check(source);

        foreach (Diagnostic diagnostic in result.Diagnostics)
        {
            if (string.Equals(diagnostic.Code, code, StringComparison.Ordinal)) return diagnostic;
        }

        Assert.Fail($"диагностика {code} не найдена:\n{result.Render()}");
        throw new InvalidOperationException();
    }

    /// <summary>Коды предупреждений проверки.</summary>
    public static IReadOnlyList<string> CheckWarnings(string source)
    {
        var codes = new List<string>();

        foreach (Diagnostic diagnostic in Check(source).Diagnostics)
        {
            if (diagnostic.Severity == DiagnosticSeverity.Warning) codes.Add(diagnostic.Code);
        }

        return codes;
    }

    /// <summary>Отчёт для сообщения о провале теста.</summary>
    public static string Report(RunResult result) =>
        string.Join("\n", result.Transcript) + "\n" + result.Render();
}
