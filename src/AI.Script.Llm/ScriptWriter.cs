using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.Llm;

/// <summary>Как закончилась попытка решить задачу скриптом.</summary>
/// <param name="Script">Последний текст скрипта.</param>
/// <param name="Result">Исход прогона; <c>null</c>, если до прогона не дошло.</param>
/// <param name="Diagnostics">Диагностики последней проверки.</param>
/// <param name="Attempts">Сколько раз модель писала скрипт.</param>
public sealed record ScriptSolution(
    string Script,
    RunResult? Result,
    IReadOnlyList<Diagnostic> Diagnostics,
    int Attempts)
{
    /// <summary>Отработал ли скрипт до конца.</summary>
    public bool Success => Result?.Success == true;
}

/// <summary>Настройки цикла «написал → проверил → исполнил».</summary>
public sealed class ScriptWriterOptions
{
    /// <summary>
    /// Сколько раз модель может переписать скрипт после диагностик.
    /// </summary>
    /// <remarks>
    /// Две по умолчанию. Первая исправляет опечатку в имени функции, вторая — редкий случай,
    /// когда исправление породило новую ошибку. Дальше модель обычно ходит по кругу, и каждая
    /// следующая попытка — это оплаченный запрос без надежды на новый результат.
    /// </remarks>
    public int MaxRepairs { get; set; } = 2;

    /// <summary>Температура генерации.</summary>
    public double Temperature { get; set; }

    /// <summary>Настройки прогона; по умолчанию — недоверенный профиль.</summary>
    public Func<RunOptions> RunOptions { get; set; } = static () => RunProfiles.Untrusted();

    /// <summary>
    /// Отдавать ли модели отказ прогона на исправление.
    /// </summary>
    /// <remarks>
    /// По умолчанию да: сорвавшийся <c>assert</c> и деление на ноль исправимы ровно так же,
    /// как ошибка проверки. Но прогон уже что-то сделал — записал файл, потратил токены, — и
    /// хост, которому это небезразлично, вправе оставить одну попытку.
    /// </remarks>
    public bool RepairFailedRuns { get; set; } = true;
}

/// <summary>
/// Цикл «написал → проверил → исполнил»: модель пишет скрипт, проверка ловит ошибки до запуска.
/// </summary>
/// <remarks>
/// Ради этого цикла проверка и существует. Она стоит миллисекунды и не имеет побочных
/// эффектов, поэтому опечатка модели в имени функции обходится в один дешёвый ответ с
/// диагностикой, а не в полный прогон и не в правдоподобный неверный результат.
/// <para>
/// Диагностики уходят обратно дословно: они написаны так, чтобы по ним исправляли не
/// догадываясь, и пересказ своими словами теряет ровно эту их часть.
/// </para>
/// </remarks>
public sealed class ScriptWriter
{
    private readonly ILLMClient _llm;
    private readonly ScriptHost _host;
    private readonly ScriptWriterOptions _options;

    /// <summary>Создаёт цикл.</summary>
    /// <param name="llm">Клиент языковой модели.</param>
    /// <param name="host">Хост, на котором скрипт проверяется и исполняется.</param>
    /// <param name="options">Настройки цикла.</param>
    public ScriptWriter(ILLMClient llm, ScriptHost host, ScriptWriterOptions? options = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _options = options ?? new ScriptWriterOptions();
    }

    /// <summary>
    /// Решает задачу скриптом: пишет, проверяет, при нужде переписывает, исполняет.
    /// </summary>
    /// <param name="task">Задача словами пользователя.</param>
    /// <param name="cancellationToken">Отмена.</param>
    public async Task<ScriptSolution> SolveAsync(string task, CancellationToken cancellationToken = default)
    {
        var history = new List<LLMMessage>
        {
            new(LLMMessage.SystemRole, ScriptPrompt.System(_host)),
            new(LLMMessage.UserRole, task),
        };

        var settings = new GenerateSettings();

        if (_options.Temperature > 0) settings.Temperature = _options.Temperature;

        string script = string.Empty;
        IReadOnlyList<Diagnostic> diagnostics = [];
        int attempts = 0;

        for (int attempt = 0; attempt <= Math.Max(0, _options.MaxRepairs); attempt++)
        {
            string answer = await _llm.SendAsync(history, settings, cancellationToken).ConfigureAwait(false);

            attempts++;
            script = ScriptPrompt.ExtractScript(answer);

            CheckResult check = _host.Check(script);

            diagnostics = check.Diagnostics;

            if (!check.Success)
            {
                history.Add(new LLMMessage(LLMMessage.AssistantRole, answer));
                history.Add(new LLMMessage(LLMMessage.UserRole, ScriptPrompt.Repair(script, check.Diagnostics)));

                continue;
            }

            RunResult result = await _host
                .RunAsync(script, _options.RunOptions(), cancellationToken)
                .ConfigureAwait(false);

            if (result.Success || !_options.RepairFailedRuns)
                return new ScriptSolution(script, result, result.Diagnostics, attempts);

            // Скрипт разобран и проверен, но сорвался на исполнении. Для модели это такая же
            // диагностика с позицией, как и ошибка проверки, — разница лишь в том, когда её
            // заметили.
            if (attempt == Math.Max(0, _options.MaxRepairs))
                return new ScriptSolution(script, result, result.Diagnostics, attempts);

            history.Add(new LLMMessage(LLMMessage.AssistantRole, answer));
            history.Add(new LLMMessage(LLMMessage.UserRole, Failure(script, result)));
        }

        return new ScriptSolution(script, null, diagnostics, attempts);
    }

    private static string Failure(string script, RunResult result)
    {
        string reason = result.Error?.Render() ?? "скрипт сорвался без диагностики";

        return $"Скрипт прошёл проверку, но сорвался при выполнении. Вот он:\n\n{script}\n\n" +
            $"Отказ:\n\n{reason}\n\nИсправь причину и верни скрипт целиком.";
    }
}
