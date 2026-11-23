using System.Runtime.CompilerServices;
using AI.Agents.Contracts.Runs;
using AI.Agents.Contracts.Tools;
using Serilog;

namespace AI.Agents.Interop.Effects;

/// <summary>
/// Гейт эффектов: обертка над любым <see cref="IToolSet"/>, которая назначает каждому вызову ключ эффекта и
/// ведет журнал эффектов запуска, чтобы одно намерение исполнялось один раз.
/// </summary>
/// <remarks>
/// <para>Правила (вид — <see cref="ToolActions.Effective(ToolAction)"/> от <see cref="ToolSpec.Action"/>,
/// повтор — <see cref="ToolIdempotencies.Effective(ToolIdempotency?)"/> от <see cref="ToolSpec.Idempotency"/>):</para>
/// <list type="number">
/// <item>Каждый вызов получает <see cref="ToolCall.EffectKey"/> из <see cref="EffectKeys.Create"/>; ключ, пришедший
/// от хоста, заменяется (с предупреждением в журнал).</item>
/// <item>Вызовы вида <see cref="ToolAction.Read"/> и <see cref="ToolAction.Compute"/> мир не меняют: они идут к набору
/// без журнала. Неизвестное набору имя тоже идет к набору: он отвечает понятной ошибкой.</item>
/// <item>Вызов вида <see cref="ToolAction.Reversible"/> и <see cref="ToolAction.Irreversible"/> (а также
/// <see cref="ToolAction.Unknown"/>) записывается в журнал до исполнения итогом <see cref="ToolStatus.Unknown"/> и
/// получает настоящий итог после.</item>
/// <item>Ключ уже в журнале с итогом <see cref="ToolStatus.Ok"/>: инструмент не вызывается, возвращается сохраненный итог.</item>
/// <item>Ключ в журнале с другим итогом (сбой, обрыв, исход неизвестен, вызов еще идет): при
/// <see cref="ToolIdempotency.Idempotent"/> и <see cref="ToolIdempotency.WithKey"/> вызов повторяется (с тем же ключом —
/// он детерминирован), при <see cref="ToolIdempotency.NotIdempotent"/> и незаданном — не повторяется: итог
/// <see cref="ToolStatus.Failed"/> с кодом <see cref="EffectGateCodes.RepeatUnsafe"/>.</item>
/// <item>Итоги <see cref="ToolStatus.Denied"/>, <see cref="ToolStatus.NeedsInput"/> и <see cref="ToolStatus.AwaitsUser"/>
/// значат, что действие не исполнялось: запись снимается, и вызов можно повторить (например, с ответом человека).
/// Так же снимается запись итога <see cref="ToolStatus.Failed"/> с кодом отказа до действия
/// (<see cref="ToolCodes.NotPerformed(string?)"/>: <see cref="ToolCodes.Transient"/>,
/// <see cref="ToolCodes.NotConfigured"/> и другие): сервер ничего не сделал.</item>
/// </list>
/// <para>
/// Журнал (<see cref="Journal"/>) хост кладет в <see cref="RunState.Effects"/>. Продолжение запуска создает гейт с
/// прежним журналом (конструктор с <c>previous</c>): ключи детерминированы запуском, поэтому исполненное второй раз
/// не исполняется, а начатое с неизвестным исходом повторяется только при объявленной безопасности повтора. О
/// каждом изменении журнала гейт сообщает событием <see cref="JournalChanged"/>, чтобы хост сохранял журнал сразу,
/// а не по концу прогона.
/// </para>
/// </remarks>
public sealed class EffectGate : IToolSet
{
    private readonly IToolSet _inner;
    private readonly ILogger? _log;
    private readonly object _sync = new();
    private readonly object _notify = new();
    private readonly List<EffectRecord> _journal = [];

    /// <summary>Оборачивает набор инструментов; журнал запуска пуст.</summary>
    /// <param name="inner">Набор, к которому идут вызовы.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/> на момент записи.</param>
    public EffectGate(IToolSet inner, ILogger? log = null)
        : this(inner, [], log)
    {
    }

    /// <summary>Оборачивает набор инструментов и продолжает журнал прежнего прогона того же запуска.</summary>
    /// <param name="inner">Набор, к которому идут вызовы.</param>
    /// <param name="previous">
    /// Журнал запуска (<see cref="RunState.Effects"/>) в порядке начала вызовов. Ключ с итогом
    /// <see cref="ToolStatus.Ok"/> не исполняется второй раз; ключ с другим итогом (в том числе
    /// <see cref="ToolStatus.Unknown"/> у потерянного прогона) повторяется только при объявленной безопасности повтора.
    /// </param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/> на момент записи.</param>
    /// <exception cref="ArgumentException">В журнале пустая запись или один ключ дважды: журнал испорчен, а молча
    /// выбрать одну из записей нельзя.</exception>
    public EffectGate(IToolSet inner, IEnumerable<EffectRecord> previous, ILogger? log = null)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ArgumentNullException.ThrowIfNull(previous);
        _log = log;

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (EffectRecord? record in previous)
        {
            if (record?.Key == null || record.Result == null)
                throw new ArgumentException("В прежнем журнале эффектов пустая запись.", nameof(previous));
            if (!keys.Add(record.Key))
                throw new ArgumentException($"В прежнем журнале эффектов ключ {record.Key} встречается дважды.", nameof(previous));
            _journal.Add(record);
        }
    }

    /// <summary>
    /// Журнал изменился: добавлена, заменена или снята запись. Аргумент — снимок всего журнала; при одновременных
    /// вызовах последний снимок всегда самый новый. Обработчик зовется синхронно и не должен звать гейт; его
    /// исключение пишется в журнал ошибкой и вызов инструмента не прерывает.
    /// </summary>
    public event Action<IReadOnlyList<EffectRecord>>? JournalChanged;

    /// <inheritdoc />
    public IReadOnlyList<ToolSpec> Available => _inner.Available;

    /// <summary>
    /// Журнал эффектов запуска в порядке начала вызовов: снимок на момент чтения. Запись с итогом
    /// <see cref="ToolStatus.Unknown"/> — вызов начат, исход неизвестен.
    /// </summary>
    public IReadOnlyList<EffectRecord> Journal
    {
        get
        {
            lock (_sync)
                return [.. _journal];
        }
    }

    private ILogger Logger => _log ?? Log.Logger;

    /// <inheritdoc />
    public IAsyncEnumerable<ToolUpdate> CallAsync(ToolCall call, ToolContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(context);
        return CallCoreAsync(call, context, cancellationToken);
    }

    private async IAsyncEnumerable<ToolUpdate> CallCoreAsync(
        ToolCall call, ToolContext context, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(context);

        string key = EffectKeys.Create(context.RunId, call.Name, call.Arguments);
        if (call.EffectKey != null && call.EffectKey != key)
        {
            Logger.Warning(
                "Гейт эффектов: ключ вызова {Tool} ({CallId}) от хоста заменен ключом фреймворка", call.Name, call.Id);
        }

        ToolCall keyed = call with { EffectKey = key };
        ToolSpec? spec = (_inner.Available ?? []).FirstOrDefault(s => ToolSpec.NameComparer.Equals(s.Name, call.Name));

        if (spec == null || spec.Action.Effective() is ToolAction.Read or ToolAction.Compute)
        {
            await foreach (ToolUpdate update in _inner.CallAsync(keyed, context, cancellationToken).ConfigureAwait(false))
                yield return update;
            yield break;
        }

        EffectRecord? previous;
        ToolResult? refusal = null;
        lock (_sync)
        {
            previous = _journal.Find(r => r.Key == key);

            if (previous?.Result.Status == ToolStatus.Ok)
            {
                refusal = previous.Result;
            }
            else if (previous != null && spec.Idempotency.Effective() == ToolIdempotency.NotIdempotent)
            {
                refusal = RepeatUnsafe(spec, previous);
            }
            else
            {
                Put(new EffectRecord { Key = key, Tool = spec.Name, Result = Pending(spec.Name) });
            }
        }

        if (refusal == null)
            Notify();

        if (refusal != null)
        {
            if (refusal.Status == ToolStatus.Ok)
                Logger.Information("Гейт эффектов: {Tool} с этим ключом уже исполнен, возвращен сохраненный итог", spec.Name);
            else
                Logger.Warning("Гейт эффектов: повтор {Tool} не исполнен: инструмент не идемпотентен", spec.Name);

            yield return new ToolUpdate.Final { Result = refusal };
            yield break;
        }

        ToolResult? final = null;
        string interruption = EffectGateCodes.Interrupted;
        IAsyncEnumerator<ToolUpdate> enumerator = _inner.CallAsync(keyed, context, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (true)
            {
                ToolUpdate current;
                try
                {
                    if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
                        break;
                    current = enumerator.Current;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    interruption = EffectGateCodes.Exception;
                    throw;
                }

                if (current is ToolUpdate.Final done)
                {
                    final = done.Result;
                    Settle(key, spec.Name, final, previous);
                    yield return current;
                    yield break;
                }

                yield return current;
            }

            // Поток кончился без итога: исход неизвестен, итог составляет вызывающий (контракт ToolUpdate).
            final = new ToolResult
            {
                Status = ToolStatus.Unknown,
                Text = "инструмент не вернул итога; исход неизвестен",
                Origin = new Origin { Kind = OriginKind.Trusted },
                ErrorCode = EffectGateCodes.NoResult,
            };
            Settle(key, spec.Name, final, previous);
            yield return new ToolUpdate.Final { Result = final };
        }
        finally
        {
            if (final == null)
            {
                // Исключение, отмена или потребитель перестал читать до итога: действие могло состояться.
                Settle(key, spec.Name, new ToolResult
                {
                    Status = ToolStatus.Unknown,
                    Text = "вызов прерван до итога; исход неизвестен",
                    Origin = new Origin { Kind = OriginKind.Trusted },
                    ErrorCode = interruption,
                }, previous);
            }

            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Записывает итог; если действие не исполнялось (отказ, вопрос, отказ до действия), возвращает прежнюю запись
    /// или снимает ее.
    /// </summary>
    private void Settle(string key, string tool, ToolResult result, EffectRecord? previous)
    {
        lock (_sync)
        {
            if (NotPerformed(result))
            {
                int index = _journal.FindIndex(r => r.Key == key);
                if (index >= 0)
                {
                    if (previous != null)
                        _journal[index] = previous;
                    else
                        _journal.RemoveAt(index);
                }
            }
            else
            {
                Put(new EffectRecord { Key = key, Tool = tool, Result = result });
            }
        }

        Notify();
    }

    /// <summary>Итог значит, что действие не исполнялось: мир не изменился, запись журнала не нужна.</summary>
    private static bool NotPerformed(ToolResult result) =>
        result.Status is ToolStatus.Denied or ToolStatus.NeedsInput or ToolStatus.AwaitsUser
        || (result.Status == ToolStatus.Failed && ToolCodes.NotPerformed(result.ErrorCode));

    /// <summary>
    /// Сообщает снимок журнала. Снимок берется под замком уведомлений: последний вызванный обработчик видит самый
    /// новый журнал, даже если изменения шли из разных потоков.
    /// </summary>
    private void Notify()
    {
        if (JournalChanged == null)
            return;

        lock (_notify)
        {
            Action<IReadOnlyList<EffectRecord>>? handler = JournalChanged;
            if (handler == null)
                return;

            try
            {
                handler(Journal);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Гейт эффектов: обработчик JournalChanged упал; журнал в памяти гейта цел");
            }
        }
    }

    /// <summary>Заменяет запись с тем же ключом или добавляет новую в конец. Вызывается под замком.</summary>
    private void Put(EffectRecord record)
    {
        int index = _journal.FindIndex(r => r.Key == record.Key);
        if (index < 0)
            _journal.Add(record);
        else
            _journal[index] = record;
    }

    private static ToolResult Pending(string tool) => new()
    {
        Status = ToolStatus.Unknown,
        Text = $"вызов {tool} начат, исход еще неизвестен",
        Origin = new Origin { Kind = OriginKind.Trusted },
    };

    private static ToolResult RepeatUnsafe(ToolSpec spec, EffectRecord previous) => new()
    {
        Status = ToolStatus.Failed,
        Text = $"Повтор не выполнен: вызов {spec.Name} с теми же аргументами в этом запуске уже начинался " +
               $"(итог: {previous.Result.Status}), а повтор этого инструмента не объявлен безопасным. " +
               "Действие могло состояться; повторить его может только человек.",
        Origin = new Origin { Kind = OriginKind.Trusted },
        ErrorCode = EffectGateCodes.RepeatUnsafe,
    };

    /// <summary>Идет ли набор уже через гейт: тогда движок не оборачивает его второй раз.</summary>
    internal static bool IsGated(IToolSet set) => set switch
    {
        EffectGate => true,
        Humans.HumanChannelToolSet human => IsGated(human.Inner),
        _ => false,
    };
}
