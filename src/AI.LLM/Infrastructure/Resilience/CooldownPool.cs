#nullable enable

namespace AI.LLM.Infrastructure.Resilience;

/// <summary>
/// Пул взаимозаменяемых ресурсов с карантином: ключи провайдера, маршруты (прокси). Одно правило ротации на всё:
/// отказавший элемент выводится из строя на срок, следующий запрос берёт другой, по истечении срока элемент
/// возвращается сам.
/// </summary>
/// <remarks>
/// <para>
/// Выбор — случайный среди доступных: нагрузка размазывается по пулу. Если в карантине все, <see cref="Lease"/> отдаёт
/// ближайший к возвращению (отправить запрос с подозрительным элементом лучше, чем не отправить вовсе), а
/// <see cref="TryLeaseAvailable"/> честно отвечает «нет».
/// </para>
/// <para>
/// Карантин наступает после <c>failuresBeforeCooldown</c> отказов подряд; удача обнуляет счёт. Когда срок
/// вышел, счёт тоже обнуляется: элемент получает чистый лист, как было у прежнего чёрного списка прокси.
/// </para>
/// Потокобезопасно.
/// </remarks>
/// <typeparam name="T">Ресурс.</typeparam>
public sealed class CooldownPool<T> where T : notnull
{
    private readonly Entry[] _entries;
    private readonly int _failuresBeforeCooldown;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Random _random;

    /// <summary>Создаёт пул.</summary>
    /// <param name="items">Ресурсы; пустой список допустим (пул без ресурсов).</param>
    /// <param name="failuresBeforeCooldown">Сколько отказов подряд выводят элемент в карантин; не меньше 1.</param>
    /// <param name="time">Часы; <see langword="null"/>: <see cref="TimeProvider.System"/>.</param>
    /// <param name="random">Источник случайности выбора; <see langword="null"/>: общий.</param>
    public CooldownPool(IEnumerable<T> items, int failuresBeforeCooldown = 1, TimeProvider? time = null, Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThan(failuresBeforeCooldown, 1);

        _entries = [.. items.Select((item, index) => new Entry(index, item ?? throw new ArgumentException("Элемент пула не может быть null.", nameof(items))))];
        _failuresBeforeCooldown = failuresBeforeCooldown;
        _time = time ?? TimeProvider.System;
        _random = random ?? Random.Shared;
    }

    /// <summary>Сколько элементов в пуле.</summary>
    public int Count => _entries.Length;

    /// <summary>Сколько элементов готовы к работе прямо сейчас.</summary>
    public int AvailableCount
    {
        get
        {
            lock (_gate)
                return _entries.Count(IsAvailable);
        }
    }

    /// <summary>Элемент на попытку: случайный среди доступных, а если в карантине все — ближайший к возвращению.</summary>
    /// <exception cref="InvalidOperationException">Пул пуст.</exception>
    public PoolLease<T> Lease()
    {
        lock (_gate)
        {
            if (_entries.Length == 0)
                throw new InvalidOperationException("Пул пуст: выдавать нечего.");

            Entry[] available = [.. _entries.Where(IsAvailable)];
            Entry chosen = available.Length > 0
                ? available[_random.Next(available.Length)]
                : _entries.MinBy(e => e.CoolingUntil)!;
            return new PoolLease<T>(chosen.Index, chosen.Item);
        }
    }

    /// <summary>Доступный элемент, кроме уже испробованных в этом запросе.</summary>
    /// <param name="lease">Выданный элемент.</param>
    /// <param name="exclude">Номера элементов, которые этот запрос уже пробовал; <see langword="null"/>: никаких.</param>
    /// <returns><see langword="false"/>: доступных (и не испробованных) нет.</returns>
    public bool TryLeaseAvailable(out PoolLease<T> lease, IReadOnlyCollection<int>? exclude = null)
    {
        lock (_gate)
        {
            Entry[] available = [.. _entries.Where(e => IsAvailable(e) && (exclude == null || !exclude.Contains(e.Index)))];
            if (available.Length == 0)
            {
                lease = default;
                return false;
            }

            Entry chosen = available[_random.Next(available.Length)];
            lease = new PoolLease<T>(chosen.Index, chosen.Item);
            return true;
        }
    }

    /// <summary>Элемент отказал. После порога отказов подряд он уходит в карантин на <paramref name="cooldown"/>.</summary>
    /// <param name="lease">Выданный элемент.</param>
    /// <param name="cooldown">Срок карантина; не отрицательный.</param>
    /// <param name="reason">Причина для снимка здоровья (без секретов).</param>
    /// <returns>Есть ли сейчас другой доступный элемент.</returns>
    public bool ReportFailure(PoolLease<T> lease, TimeSpan cooldown, string? reason = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cooldown, TimeSpan.Zero);

        lock (_gate)
        {
            Entry entry = EntryOf(lease);
            DateTimeOffset now = _time.GetUtcNow();
            entry.Failures++;
            entry.LastFailure = now;
            entry.LastReason = reason;
            if (entry.Failures >= _failuresBeforeCooldown)
                entry.CoolingUntil = Later(entry.CoolingUntil, now + cooldown);

            return _entries.Any(e => e.Index != entry.Index && IsAvailable(e));
        }
    }

    /// <summary>Элемент отработал: счёт отказов и карантин сбрасываются.</summary>
    /// <param name="lease">Выданный элемент.</param>
    public void ReportSuccess(PoolLease<T> lease)
    {
        lock (_gate)
        {
            Entry entry = EntryOf(lease);
            entry.Failures = 0;
            entry.CoolingUntil = null;
            entry.LastReason = null;
            entry.LastSuccess = _time.GetUtcNow();
        }
    }

    /// <summary>Снимок здоровья пула для журнала и панели: без самих элементов, только номера.</summary>
    public IReadOnlyList<PoolEntryHealth> Snapshot()
    {
        lock (_gate)
        {
            DateTimeOffset now = _time.GetUtcNow();
            return [.. _entries.Select(e => new PoolEntryHealth(
                e.Index, e.Failures, e.CoolingUntil is { } until && until > now ? until : null, e.LastSuccess, e.LastFailure, e.LastReason))];
        }
    }

    /// <summary>Чистый лист всем элементам: счёт, карантин, история.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            foreach (Entry entry in _entries)
            {
                entry.Failures = 0;
                entry.CoolingUntil = null;
                entry.LastFailure = null;
                entry.LastSuccess = null;
                entry.LastReason = null;
            }
        }
    }

    /// <summary>Элемент по номеру (для снимков с названиями: маска ключа, адрес прокси).</summary>
    /// <param name="index">Номер элемента.</param>
    public T ItemAt(int index) => _entries[index].Item;

    private Entry EntryOf(PoolLease<T> lease)
    {
        if ((uint)lease.Index >= (uint)_entries.Length || !EqualityComparer<T>.Default.Equals(_entries[lease.Index].Item, lease.Item))
            throw new ArgumentException("Элемент выдан другим пулом.", nameof(lease));
        return _entries[lease.Index];
    }

    /// <summary>Доступен ли элемент; истёкший карантин снимается здесь же, со сбросом счёта.</summary>
    private bool IsAvailable(Entry entry)
    {
        if (entry.CoolingUntil is not { } until)
            return true;
        if (_time.GetUtcNow() < until)
            return false;

        entry.CoolingUntil = null;
        entry.Failures = 0;
        return true;
    }

    private static DateTimeOffset Later(DateTimeOffset? current, DateTimeOffset candidate) =>
        current is { } c && c > candidate ? c : candidate;

    private sealed class Entry(int index, T item)
    {
        public int Index { get; } = index;
        public T Item { get; } = item;
        public int Failures { get; set; }
        public DateTimeOffset? CoolingUntil { get; set; }
        public DateTimeOffset? LastSuccess { get; set; }
        public DateTimeOffset? LastFailure { get; set; }
        public string? LastReason { get; set; }
    }
}

/// <summary>Элемент пула, выданный на одну попытку.</summary>
/// <param name="Index">Номер элемента в пуле.</param>
/// <param name="Item">Сам элемент.</param>
public readonly record struct PoolLease<T>(int Index, T Item);

/// <summary>Здоровье элемента пула.</summary>
/// <param name="Index">Номер элемента в пуле.</param>
/// <param name="ConsecutiveFailures">Отказов подряд.</param>
/// <param name="CoolingUntil">До какого времени элемент в карантине; <see langword="null"/>: в строю.</param>
/// <param name="LastSuccess">Последняя удача.</param>
/// <param name="LastFailure">Последний отказ.</param>
/// <param name="LastReason">Причина последнего отказа.</param>
public sealed record PoolEntryHealth(
    int Index, int ConsecutiveFailures, DateTimeOffset? CoolingUntil, DateTimeOffset? LastSuccess, DateTimeOffset? LastFailure, string? LastReason)
{
    /// <summary>Элемент в карантине.</summary>
    public bool IsCooling => CoolingUntil != null;
}
