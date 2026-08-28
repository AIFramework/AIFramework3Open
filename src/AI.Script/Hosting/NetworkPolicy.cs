using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Hosting;

/// <summary>
/// Разрешение скрипту обращаться к сети.
/// </summary>
/// <remarks>
/// Запрет по умолчанию — как и у файлов. Хост, подключивший модуль <c>llm</c> ради одного
/// доверенного скрипта, не должен внезапно открывать сеть скрипту, который написала модель.
/// <para>
/// Политика проверяется в момент обращения, а не при подключении модуля: один и тот же хост
/// исполняет и доверенные, и недоверенные скрипты, и разрешение принадлежит прогону.
/// </para>
/// </remarks>
public sealed class NetworkPolicy
{
    /// <summary>Сеть запрещена.</summary>
    public static readonly NetworkPolicy Denied = new(false, []);

    /// <summary>Сеть разрешена без ограничения по узлам.</summary>
    public static readonly NetworkPolicy Allowed = new(true, []);

    private readonly HashSet<string> _hosts;

    private NetworkPolicy(bool enabled, IEnumerable<string> hosts)
    {
        Enabled = enabled;
        _hosts = new HashSet<string>(hosts, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Разрешены ли обращения к сети.</summary>
    public bool Enabled { get; }

    /// <summary>Белый список узлов; пуст — ограничения по узлам нет.</summary>
    public IReadOnlyCollection<string> Hosts => _hosts;

    /// <summary>
    /// Разрешает сеть только для перечисленных узлов.
    /// </summary>
    /// <param name="hosts">Имена узлов, например <c>openrouter.ai</c>.</param>
    public static NetworkPolicy AllowHosts(params string[] hosts)
    {
        ArgumentNullException.ThrowIfNull(hosts);

        return new NetworkPolicy(true, hosts);
    }

    /// <summary>
    /// Проверяет право обратиться к сети; бросает отказ, если права нет.
    /// </summary>
    /// <param name="what">Что именно собирались сделать — попадёт в сообщение.</param>
    /// <param name="host">Узел назначения; <c>null</c>, если он неизвестен.</param>
    public void Require(string what, string? host = null)
    {
        if (!Enabled)
        {
            throw new ScriptError(
                DiagnosticCodes.NetworkDenied,
                $"{what}: обращение к сети запрещено настройками прогона",
                "хост запускает скрипт без доступа к сети; модели и поиск в этом режиме недоступны");
        }

        if (_hosts.Count == 0 || host == null || _hosts.Contains(host)) return;

        throw new ScriptError(
            DiagnosticCodes.NetworkDenied,
            $"{what}: узел '{host}' не в белом списке прогона",
            $"разрешены: {string.Join(", ", _hosts)}");
    }
}
