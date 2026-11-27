#nullable enable

using System.Net;
using System.Net.Http.Headers;

namespace AI.LLM.Infrastructure.Resilience;

/// <summary>Почему ключ отказал. Решает, на сколько ключ уходит в карантин.</summary>
public enum KeyFailure
{
    /// <summary>Не задано; в отчёт об отказе не принимается.</summary>
    Unspecified = 0,

    /// <summary>429: лимит запросов ключа. Лечится минутой ожидания.</summary>
    RateLimit,

    /// <summary>402: на ключе кончились средства. Могут пополнить в любой момент.</summary>
    Payment,

    /// <summary>401/403: ключ неверный, отозван или ему запрещена модель. Чинит человек.</summary>
    Unauthorized,
}

/// <summary>Сроки карантина ключа по причине отказа.</summary>
/// <param name="RateLimit">429; перебивается <c>Retry-After</c>, если сервер его прислал.</param>
/// <param name="Payment">402.</param>
/// <param name="Unauthorized">401/403.</param>
public sealed record KeyCooldowns(TimeSpan RateLimit, TimeSpan Payment, TimeSpan Unauthorized)
{
    /// <summary>Минута на лимит, полчаса на баланс, шесть часов на неверный ключ.</summary>
    public static KeyCooldowns Default { get; } = new(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30), TimeSpan.FromHours(6));
}

/// <summary>Здоровье ключа без его значения.</summary>
/// <param name="Index">Номер ключа в пуле.</param>
/// <param name="Mask">Маска: последние четыре знака.</param>
/// <param name="Health">Счёт отказов и карантин.</param>
public sealed record KeyHealth(int Index, string Mask, PoolEntryHealth Health);

/// <summary>
/// Пул ключей провайдера — один механизм ротации для всех клиентов: при отказе по ключу (лимит, неверный, кончились
/// средства) берётся следующий, сбойный ключ уходит в карантин на срок из <see cref="KeyCooldowns"/>.
/// </summary>
/// <remarks>
/// Ротация — тот же <see cref="CooldownPool{T}"/>, что у маршрутов (прокси): правило одно, сроки разные. Значения
/// ключей наружу не отдаются: снимок (<see cref="Snapshot"/>) несёт только маску. Заголовок запроса ставит
/// <see cref="Authorize(HttpRequestMessage, PoolLease{string})"/> — единственное место, где ключ попадает в запрос.
/// </remarks>
public sealed class ApiKeyPool
{
    private readonly CooldownPool<string> _pool;
    private readonly KeyCooldowns _cooldowns;

    /// <summary>Пул из ключей.</summary>
    /// <param name="keys">Ключи; пустые строки отбрасываются, повторы схлопываются. Пустой пул не допускается.</param>
    /// <param name="cooldowns">Сроки карантина; <see langword="null"/>: <see cref="KeyCooldowns.Default"/>.</param>
    /// <param name="time">Часы; <see langword="null"/>: <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentException">Ни одного непустого ключа.</exception>
    public ApiKeyPool(IEnumerable<string> keys, KeyCooldowns? cooldowns = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(keys);
        string[] clean = [.. keys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct(StringComparer.Ordinal)];
        if (clean.Length == 0)
            throw new ArgumentException("В пуле нет ни одного ключа.", nameof(keys));

        _pool = new CooldownPool<string>(clean, failuresBeforeCooldown: 1, time);
        _cooldowns = cooldowns ?? KeyCooldowns.Default;
    }

    /// <summary>Пул из одного ключа: клиенты со строкой ключа идут тем же путём, что и с пулом.</summary>
    /// <param name="key">Ключ.</param>
    public static ApiKeyPool Single(string key) => new([key]);

    /// <summary>Пул из одного ключа или <see langword="null"/>, если ключа нет (локальный сервер без ключа).</summary>
    /// <param name="key">Ключ или пусто.</param>
    public static ApiKeyPool? SingleOrNone(string? key) => string.IsNullOrWhiteSpace(key) ? null : Single(key);

    /// <summary>Сколько ключей в пуле.</summary>
    public int Count => _pool.Count;

    /// <summary>Сколько ключей готовы к работе прямо сейчас.</summary>
    public int AvailableCount => _pool.AvailableCount;

    /// <summary>Ключ на попытку.</summary>
    public PoolLease<string> Lease() => _pool.Lease();

    /// <summary>Доступный ключ, кроме испробованных этим запросом.</summary>
    /// <param name="lease">Выданный ключ.</param>
    /// <param name="exclude">Номера уже испробованных ключей.</param>
    public bool TryLeaseAvailable(out PoolLease<string> lease, IReadOnlyCollection<int>? exclude = null) =>
        _pool.TryLeaseAvailable(out lease, exclude);

    /// <summary>Отказ по ключу по коду ответа: 401/403, 402, 429; прочие коды ключ не винят.</summary>
    /// <param name="status">Код ответа.</param>
    /// <returns>Причина или <see langword="null"/>: ключ ни при чём.</returns>
    public static KeyFailure? FailureOf(HttpStatusCode status) => (int)status switch
    {
        401 or 403 => KeyFailure.Unauthorized,
        402 => KeyFailure.Payment,
        429 => KeyFailure.RateLimit,
        _ => null,
    };

    /// <summary>Ключ отказал: в карантин по причине.</summary>
    /// <param name="lease">Выданный ключ.</param>
    /// <param name="failure">Причина.</param>
    /// <param name="retryAfter">Срок, который назвал сервер (для лимита); <see langword="null"/>: не назвал.</param>
    /// <returns>Есть ли сейчас другой доступный ключ.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Причина не задана или незнакома.</exception>
    public bool ReportFailure(PoolLease<string> lease, KeyFailure failure, TimeSpan? retryAfter = null)
    {
        TimeSpan cooldown = failure switch
        {
            KeyFailure.RateLimit => retryAfter is { } wait && wait > TimeSpan.Zero ? wait : _cooldowns.RateLimit,
            KeyFailure.Payment => _cooldowns.Payment,
            KeyFailure.Unauthorized => _cooldowns.Unauthorized,
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "Причина отказа ключа не задана или незнакома."),
        };
        return _pool.ReportFailure(lease, cooldown, failure.ToString());
    }

    /// <summary>Ключ отработал.</summary>
    /// <param name="lease">Выданный ключ.</param>
    public void ReportSuccess(PoolLease<string> lease) => _pool.ReportSuccess(lease);

    /// <summary>Ставит ключ в заголовок запроса (<c>Authorization: Bearer</c>).</summary>
    /// <param name="request">Запрос.</param>
    /// <param name="lease">Выданный ключ.</param>
    public void Authorize(HttpRequestMessage request, PoolLease<string> lease)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Headers.Authorization = Bearer(lease.Item);
    }

    /// <summary>Заголовок <c>Authorization: Bearer</c> для ключа: единственное место, где он собирается.</summary>
    /// <param name="key">Ключ.</param>
    public static AuthenticationHeaderValue Bearer(string key) => new("Bearer", key.Trim());

    /// <summary>Снимок пула: маски и здоровье, без значений ключей.</summary>
    public IReadOnlyList<KeyHealth> Snapshot() =>
        [.. _pool.Snapshot().Select(h => new KeyHealth(h.Index, Mask(_pool.ItemAt(h.Index)), h))];

    /// <summary>Маска ключа для журнала: последние четыре знака.</summary>
    /// <param name="key">Ключ.</param>
    public static string Mask(string key) =>
        string.IsNullOrEmpty(key) ? "" : "…" + (key.Length <= 4 ? new string('*', key.Length) : key[^4..]);
}
