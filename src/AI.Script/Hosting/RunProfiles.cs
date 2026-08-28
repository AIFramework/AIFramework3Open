using AI.Script.Runtime;

namespace AI.Script.Hosting;

/// <summary>
/// Готовые наборы настроек прогона: доверенный и недоверенный.
/// </summary>
/// <remarks>
/// Профиль — это не удобство, а способ не забыть. Правильные настройки для скрипта, который
/// написала модель, — это восемь полей в четырёх объектах; собирая их каждый раз руками,
/// однажды забудешь одно, и забытым окажется то, ради чего всё затевалось.
/// <para>
/// Профиль возвращает <see cref="RunOptions"/>, а не прячет их: вызывающий вправе ужесточить
/// любое поле. Ослабить он тоже вправе — но тогда это его осознанное решение, записанное
/// строкой кода, а не молчаливое умолчание.
/// </para>
/// </remarks>
public static class RunProfiles
{
    /// <summary>Таймаут недоверенного прогона.</summary>
    public static readonly TimeSpan UntrustedTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Локальная разработка: лимиты мягкие, файлы в рабочей папке доступны на запись.
    /// </summary>
    /// <param name="workdir">Рабочая папка; <c>null</c> — файлы запрещены.</param>
    public static RunOptions Trusted(string? workdir = null) => new()
    {
        Sandbox = workdir == null ? DeniedSandbox.Instance : new WorkspaceSandbox(workdir),
        Network = NetworkPolicy.Allowed,
        Limits = new ScriptLimits(),
    };

    /// <summary>
    /// Скрипт от модели либо из веб-демо: сеть выключена, файлы только на чтение, время ограничено.
    /// </summary>
    /// <param name="workdir">Рабочая папка только для чтения; <c>null</c> — файлы запрещены.</param>
    /// <remarks>
    /// Потолки ниже доверенных на порядок: недоверенный скрипт — это прототип на десяток
    /// секунд счёта, а не расчёт на ночь. Внешние вызовы запрещены не потолком, а отсутствием
    /// сети: потолок в ноль вызовов давал бы отказ по расходам вместо отказа по политике, и
    /// причина отказа читалась бы неверно.
    /// </remarks>
    public static RunOptions Untrusted(string? workdir = null) => new()
    {
        Sandbox = workdir == null ? DeniedSandbox.Instance : new WorkspaceSandbox(workdir, readOnly: true),
        Network = NetworkPolicy.Denied,
        Limits = new ScriptLimits
        {
            Steps = 2_000_000,
            Allocations = 20_000_000,
            Timeout = UntrustedTimeout,
        },
        LockedOptions = { "timeout", "steps", "workdir", "cache", "parallel" },
    };

    /// <summary>
    /// Недоверенный прогон, которому разрешены платные вызовы в заданных пределах.
    /// </summary>
    /// <param name="workdir">Рабочая папка только для чтения; <c>null</c> — файлы запрещены.</param>
    /// <param name="calls">Потолок числа внешних вызовов.</param>
    /// <param name="tokens">Потолок числа токенов.</param>
    /// <param name="cost">Потолок стоимости.</param>
    /// <param name="hosts">Белый список узлов; пусто — без ограничения по узлам.</param>
    public static RunOptions UntrustedWithNetwork(
        string? workdir = null,
        int calls = 20,
        long tokens = 100_000,
        decimal cost = 0.5m,
        params string[] hosts)
    {
        RunOptions options = Untrusted(workdir);

        options.Network = hosts is { Length: > 0 } ? NetworkPolicy.AllowHosts(hosts) : NetworkPolicy.Allowed;
        options.Limits.ExternalCalls = calls;
        options.Limits.ExternalTokens = tokens;
        options.Limits.ExternalCost = cost;

        return options;
    }
}
