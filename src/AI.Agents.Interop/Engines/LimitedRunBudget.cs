using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Models;

namespace AI.Agents.Interop.Engines;

/// <summary>
/// Эталонный бюджет хода по пределам <see cref="RunLimits"/>: шаги, токены, деньги, вызовы модели и срок. Его делят
/// все участники хода; исчерпание любого предела отменяет <see cref="Exhausted"/>.
/// </summary>
/// <remarks>
/// <para>
/// Это ограничение работы, а не счета: исчерпанный бюджет не разрешает следующий шаг (<see cref="TryStep"/>) и
/// отменяет долгую работу внутри шага. Порядок проверки в <see cref="TryStep"/>: срок, токены, деньги, вызовы,
/// шаги; первый исчерпанный предел запоминается и дальше отдается всегда.
/// </para>
/// <para>
/// Вызов модели без стоимости (<see cref="ModelUsage.Cost"/> = <see langword="null"/>) при заданном пределе денег
/// предел не двигает: такие вызовы считаются в <see cref="UnpricedCalls"/>, чтобы хост видел, что учет денег
/// неполон, а не принимал его за точный.
/// </para>
/// </remarks>
public sealed class LimitedRunBudget : IRunBudget, IDisposable
{
    private readonly object _sync = new();
    private readonly CancellationTokenSource _exhausted;
    private readonly TimeProvider _time;
    private int _steps;
    private long _tokens;
    private decimal _cost;
    private int _calls;
    private int _unpriced;
    private StopCode? _stop;
    private bool _disposed;

    /// <summary>Создает бюджет; срок отсчитывается с этого момента.</summary>
    /// <param name="limits">Пределы хода.</param>
    /// <param name="time">Часы; <see langword="null"/>: <see cref="TimeProvider.System"/>.</param>
    public LimitedRunBudget(RunLimits limits, TimeProvider? time = null)
    {
        Limits = limits ?? throw new ArgumentNullException(nameof(limits));
        _time = time ?? TimeProvider.System;
        Deadline = limits.Duration is { } duration ? _time.GetUtcNow() + duration : null;
        _exhausted = limits.Duration is { } d ? new CancellationTokenSource(d, _time) : new CancellationTokenSource();
    }

    /// <summary>Пределы, с которыми создан бюджет.</summary>
    public RunLimits Limits { get; }

    /// <inheritdoc />
    public DateTimeOffset? Deadline { get; }

    /// <inheritdoc />
    public CancellationToken Exhausted => _exhausted.Token;

    /// <summary>Засчитанные шаги.</summary>
    public int Steps { get { lock (_sync) return _steps; } }

    /// <summary>Израсходованные токены моделей.</summary>
    public long Tokens { get { lock (_sync) return _tokens; } }

    /// <summary>Израсходованные деньги по вызовам со стоимостью.</summary>
    public decimal Cost { get { lock (_sync) return _cost; } }

    /// <summary>Вызовы модели.</summary>
    public int ModelCalls { get { lock (_sync) return _calls; } }

    /// <summary>Вызовы модели, о стоимости которых провайдер не сообщил: учет денег по ним неполон.</summary>
    public int UnpricedCalls { get { lock (_sync) return _unpriced; } }

    /// <summary>Исчерпанный предел; <see langword="null"/>: бюджет не исчерпан.</summary>
    public StopCode? Stop
    {
        get
        {
            lock (_sync)
                return _stop ?? (TimeIsOut() ? StopCode.TimeLimit : null);
        }
    }

    /// <summary>Сколько осталось до срока; <see langword="null"/>: срока нет. Не меньше нуля.</summary>
    public TimeSpan? Remaining => Deadline is { } deadline
        ? Max(TimeSpan.Zero, deadline - _time.GetUtcNow())
        : null;

    /// <inheritdoc />
    public bool TryStep(out StopCode stop)
    {
        bool cancel;
        lock (_sync)
        {
            if (_stop == null)
            {
                _stop = TimeIsOut() ? StopCode.TimeLimit
                    : SpentLimit() is { } spent ? spent
                    : Limits.Steps is { } steps && _steps >= steps ? StopCode.StepLimit
                    : null;
            }

            if (_stop is { } hit)
            {
                stop = hit;
                cancel = !_disposed;
            }
            else
            {
                _steps++;
                stop = default;
                return true;
            }
        }

        if (cancel)
            _exhausted.Cancel();
        return false;
    }

    /// <inheritdoc />
    public void Spend(ModelUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        bool cancel;
        lock (_sync)
        {
            _tokens += Math.Max(0, usage.InputTokens) + Math.Max(0, usage.OutputTokens);
            _calls++;
            if (usage.Cost is { } cost)
                _cost += cost;
            else
                _unpriced++;

            if (_stop == null && SpentLimit() is { } hit)
                _stop = hit;

            // Бюджет закрыт по концу хода, а расход пришел позже: считаем, но отменять уже нечего.
            cancel = _stop != null && !_disposed;
        }

        if (cancel)
            _exhausted.Cancel();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        _exhausted.Dispose();
    }

    /// <summary>Исчерпанный предел расхода; вызывается под замком.</summary>
    private StopCode? SpentLimit() =>
        Limits.Tokens is { } tokens && _tokens >= tokens ? StopCode.TokenLimit
        : Limits.Cost is { } cost && _cost >= cost ? StopCode.CostLimit
        : Limits.ModelCalls is { } calls && _calls >= calls ? StopCode.CallLimit
        : null;

    private bool TimeIsOut() => Deadline is { } deadline && _time.GetUtcNow() >= deadline;

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a >= b ? a : b;
}
